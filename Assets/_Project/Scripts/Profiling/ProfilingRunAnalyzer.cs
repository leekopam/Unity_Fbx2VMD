using System;
using System.Collections.Generic;
using System.Linq;

namespace Fbx2Vmd.Profiling
{
    /// <summary>
    /// 런 기록을 규칙 기반으로 자동 분석해 병목·회귀·실패·프레임/GC 스파이크를 찾아냅니다.
    /// 업계 관행(롤링 베이스라인 상대 임계, 중앙값 비교, 상위 마커 집계)을 런 단위 데이터에 맞춰 축소 적용했습니다.
    /// 원인 진단이 아니라 "주의할 지점"을 자동 표시하는 수준입니다.
    /// </summary>
    public static class ProfilingRunAnalyzer
    {
        public const string KindBottleneck = "Bottleneck";
        public const string KindTrendRegression = "TrendRegression";
        public const string KindFailure = "Failure";
        public const string KindFrameSpike = "FrameSpike";
        public const string KindGcSpike = "GcSpike";

        public const string SeverityInfo = "Info";
        public const string SeverityWarning = "Warning";
        public const string SeverityError = "Error";

        // 기본값은 ProfilingAnalysisThresholds와 동일하게 유지한다(파일 없을 때의 폴백).
        private static readonly ProfilingAnalysisThresholds DefaultThresholds =
            new ProfilingAnalysisThresholds();

        /// <summary>
        /// 사용자 대기·프리뷰 상태입니다. 파이프라인 작업 구간이 아니므로 병목/트렌드/GC 판정에서 제외합니다.
        /// 상태명은 FBXSessionState enum의 ToString 결과와 문자열로 맞춥니다(역방향 어셈블리 참조 불가).
        /// </summary>
        private static readonly HashSet<string> InteractiveStages = new HashSet<string>(StringComparer.Ordinal)
        {
            "Idle", "Ready", "PreviewPlaying", "PreviewPaused",
        };

        /// <summary>
        /// 런 종료를 나타내는 결과 스테이지입니다. 실패·종료 경로의 소요는 런 간 비교 의미가 없어
        /// 트렌드 판정에서 제외합니다(병목·GC 판정에는 실제 소요이므로 포함합니다).
        /// </summary>
        private static readonly HashSet<string> OutcomeStages = new HashSet<string>(StringComparer.Ordinal)
        {
            "Success", "Failed", "Cancelled", "AbortedByNewRun",
        };

        /// <summary>
        /// 동일 이름의 스테이지 샘플을 첫 등장 순서대로 하나로 합산합니다.
        /// 구 버전 리포트의 프레임 단위 하트비트(동일 스테이지 수천 샘플)도 정상 판정하기 위한 호환층입니다.
        /// deltaMs/gcDeltaBytes/calls는 합산, message는 마지막 값을 씁니다.
        /// calls가 0인 구 스키마 샘플은 호출 1회로 간주합니다.
        /// </summary>
        public static List<ProfilingStageSample> AggregateStagesByName(IReadOnlyList<ProfilingStageSample> stages)
        {
            var order = new List<string>();
            var byStage = new Dictionary<string, ProfilingStageSample>(StringComparer.Ordinal);
            if (stages != null)
            {
                foreach (ProfilingStageSample s in stages)
                {
                    if (s == null)
                    {
                        continue;
                    }
                    string key = s.stage ?? string.Empty;
                    if (!byStage.TryGetValue(key, out ProfilingStageSample agg))
                    {
                        agg = new ProfilingStageSample
                        {
                            // 손상된 리포트의 null 스테이지명도 정규화해 호출측 딕셔너리 키 사용을 보장한다.
                            stage = key,
                            sinceRunStartMs = s.sinceRunStartMs,
                        };
                        byStage[key] = agg;
                        order.Add(key);
                    }
                    agg.message = s.message;
                    agg.deltaMs += s.deltaMs;
                    agg.gcDeltaBytes += s.gcDeltaBytes;
                    agg.calls += Math.Max(1, s.calls);
                }
            }
            var result = new List<ProfilingStageSample>(order.Count);
            foreach (string key in order)
            {
                result.Add(byStage[key]);
            }
            return result;
        }

        public static bool IsInteractiveStage(string stage)
        {
            return InteractiveStages.Contains(stage ?? string.Empty);
        }

        public static bool IsOutcomeStage(string stage)
        {
            return OutcomeStages.Contains(stage ?? string.Empty);
        }

        /// <summary>트렌드 판정에서 제외할 스테이지인지 판별합니다(사용자 대기 + 종료 스테이지).</summary>
        private static bool SkipInTrend(string stage)
        {
            return IsInteractiveStage(stage) || IsOutcomeStage(stage);
        }

        /// <summary>
        /// 스테이지별 PerfScope 심화 삽입 후보입니다. 플래그된 구간의 실제 코드 경로를 가리킵니다.
        /// </summary>
        private static readonly Dictionary<string, string[]> StageScopeCandidates =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Selected"] = new[] { "FBXImportController.CopyToControlledImportFolder" },
                ["Copied"] = new[] { "FBXImportController.ConfigureEditorImportSettingsIfNeeded" },
                ["LoadingFbx"] = new[]
                {
                    "FBXImportController.ImportModelAsync (Assimp 네이티브 임포트)",
                    "FBXImportController.ExtractPrimaryClip",
                    "RuntimeHumanoidReferencePoseApplier.TryApply",
                    "FBXImportController.LoadBoneMappingRuntime",
                    "HumanoidAvatarBuilder.SetupHumanoid",
                },
                ["AvatarReady"] = new[]
                {
                    "FBXVmdPipeline 재생 준비 경로",
                    "NativeSkinningCorrectionPlaybackDriver.BeginPreparation",
                    "HumanoidMotionPlaybackController 클립 준비",
                },
                ["Ready"] = new[]
                {
                    "FBXVmdPipeline.TryStartImportedMotionPlaybackNow",
                    "FBXVmdPipeline.TryStartImportedMotionRecordingNow",
                    "NativeSkinningCorrectionPlaybackDriver.BeginPreparation",
                },
                ["Retargeting"] = new[]
                {
                    "FBXVmdPipeline.TickNativeSkinningCorrectionPreparation",
                    "HumanoidMotionRecordingController.Tick",
                    "HumanoidMotionPlaybackController.Tick",
                },
                ["Recording"] = new[]
                {
                    "VMDRecordingController 녹화 루프",
                    "HumanoidMotionRecordingController.Tick",
                },
            };

        private static readonly string[] FrameSpikeScopes = new[]
        {
            "FBXVmdPipeline.LateUpdate 프레임 루프",
            "HumanoidMotionPlaybackController.Tick",
        };

        /// <summary>
        /// 현재 런을 분석합니다. history에는 같은 라벨·머신의 과거 런(최신순 권장)을 넘기고, 없으면 null이나 빈 목록을 넘깁니다.
        /// </summary>
        public static List<ProfilingFinding> Analyze(
            ProfilingRunRecord current, IReadOnlyList<ProfilingRunRecord> history,
            ProfilingAnalysisThresholds thresholds = null)
        {
            thresholds ??= DefaultThresholds;
            var findings = new List<ProfilingFinding>();
            if (current == null)
            {
                return findings;
            }

            AnalyzeBottlenecks(current, findings, thresholds);
            AnalyzeTrend(current, history, findings, thresholds);
            AnalyzeFailure(current, findings);
            AnalyzeFrameSpike(current, findings, thresholds);
            AnalyzeGcSpike(current, findings, thresholds);
            return findings;
        }

        private static void AnalyzeBottlenecks(ProfilingRunRecord current,
            List<ProfilingFinding> findings, ProfilingAnalysisThresholds t)
        {
            if (current.stages == null || current.totalMs <= 0f)
            {
                return;
            }

            foreach (ProfilingStageSample stage in AggregateStagesByName(current.stages))
            {
                if (IsInteractiveStage(stage.stage))
                {
                    continue;
                }
                bool shareHit = stage.deltaMs >= current.totalMs * t.bottleneckShareOfTotal;
                if (shareHit && stage.deltaMs >= t.bottleneckMinAbsMs)
                {
                    findings.Add(new ProfilingFinding
                    {
                        kind = KindBottleneck,
                        severity = SeverityWarning,
                        stage = stage.stage,
                        detail = $"병목 후보: {stage.stage} {stage.deltaMs:F0}ms (전체 {current.totalMs:F0}ms의 {stage.deltaMs / current.totalMs * 100f:F0}%) — {stage.message}",
                        suggestedScopes = ScopesFor(stage.stage),
                    });
                }
            }
        }

        private static void AnalyzeTrend(
            ProfilingRunRecord current, IReadOnlyList<ProfilingRunRecord> history,
            List<ProfilingFinding> findings, ProfilingAnalysisThresholds t)
        {
            if (history == null || history.Count < t.trendMinHistory || current.stages == null)
            {
                return;
            }

            // 같은 스테이지명의 과거 deltaMs(런별 합산) 중앙값 대비 급증을 감지한다.
            var historyByStage = new Dictionary<string, List<float>>(StringComparer.Ordinal);
            foreach (ProfilingRunRecord past in history)
            {
                if (past?.stages == null)
                {
                    continue;
                }

                foreach (ProfilingStageSample stage in AggregateStagesByName(past.stages))
                {
                    if (SkipInTrend(stage.stage))
                    {
                        continue;
                    }
                    if (!historyByStage.TryGetValue(stage.stage, out List<float> samples))
                    {
                        samples = new List<float>();
                        historyByStage[stage.stage] = samples;
                    }
                    samples.Add(stage.deltaMs);
                }
            }

            foreach (ProfilingStageSample stage in AggregateStagesByName(current.stages))
            {
                if (SkipInTrend(stage.stage))
                {
                    continue;
                }
                if (!historyByStage.TryGetValue(stage.stage, out List<float> samples) ||
                    samples.Count < t.trendMinHistory)
                {
                    continue;
                }

                float median = Median(samples);
                bool regressed =
                    stage.deltaMs > median * t.trendRegressionFactor &&
                    stage.deltaMs - median >= t.trendRegressionMinDeltaMs;
                if (regressed)
                {
                    findings.Add(new ProfilingFinding
                    {
                        kind = KindTrendRegression,
                        severity = SeverityWarning,
                        stage = stage.stage,
                        detail = $"트렌드 회귀: {stage.stage} {stage.deltaMs:F0}ms > 과거 중앙값 {median:F0}ms x {t.trendRegressionFactor:F1} (표본 {samples.Count}회)",
                        suggestedScopes = ScopesFor(stage.stage),
                    });
                }
            }
        }

        private static void AnalyzeFailure(ProfilingRunRecord current, List<ProfilingFinding> findings)
        {
            bool failed = string.Equals(current.outcome, "Failed", StringComparison.Ordinal) ||
                          string.Equals(current.outcome, "Cancelled", StringComparison.Ordinal);
            if (!failed)
            {
                return;
            }

            ProfilingStageSample terminal = current.stages != null && current.stages.Count > 0
                ? current.stages[current.stages.Count - 1]
                : null;
            findings.Add(new ProfilingFinding
            {
                kind = KindFailure,
                severity = SeverityError,
                stage = terminal?.stage ?? current.outcome,
                detail = $"런 실패: {current.outcome} — {terminal?.message}",
                suggestedScopes = ScopesFor(terminal?.stage),
            });
        }

        private static void AnalyzeFrameSpike(ProfilingRunRecord current,
            List<ProfilingFinding> findings, ProfilingAnalysisThresholds t)
        {
            if (current.sampledFrameCount <= 0 || current.sampledFrameSumMs <= 0f)
            {
                return;
            }

            float mean = current.sampledFrameSumMs / current.sampledFrameCount;
            if (current.sampledFrameMaxMs > mean * t.frameSpikeFactor &&
                current.sampledFrameMaxMs > t.frameSpikeMinMs)
            {
                findings.Add(new ProfilingFinding
                {
                    kind = KindFrameSpike,
                    severity = SeverityWarning,
                    stage = string.Empty,
                    detail = $"프레임 스파이크: 최대 {current.sampledFrameMaxMs:F1}ms > 평균 {mean:F1}ms x {t.frameSpikeFactor:F1} ({current.sampledFrameCount}프레임)",
                    suggestedScopes = new List<string>(FrameSpikeScopes),
                });
            }
        }

        private static void AnalyzeGcSpike(ProfilingRunRecord current,
            List<ProfilingFinding> findings, ProfilingAnalysisThresholds t)
        {
            if (current.stages == null)
            {
                return;
            }

            long minBytes = (long)(t.gcSpikeMinMb * 1024f * 1024f);
            foreach (ProfilingStageSample stage in AggregateStagesByName(current.stages))
            {
                if (IsInteractiveStage(stage.stage))
                {
                    continue;
                }
                if (stage.gcDeltaBytes > minBytes)
                {
                    findings.Add(new ProfilingFinding
                    {
                        kind = KindGcSpike,
                        severity = SeverityWarning,
                        stage = stage.stage,
                        detail = $"GC 스파이크: {stage.stage} +{stage.gcDeltaBytes / (1024f * 1024f):F1}MB",
                        suggestedScopes = ScopesFor(stage.stage),
                    });
                }
            }
        }

        private static List<string> ScopesFor(string stage)
        {
            var scopes = new List<string>();
            if (!string.IsNullOrEmpty(stage) &&
                StageScopeCandidates.TryGetValue(stage, out string[] candidates))
            {
                scopes.AddRange(candidates);
            }
            return scopes;
        }

        public static float Median(List<float> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                return 0f;
            }
            var sorted = samples.OrderBy(v => v).ToArray();
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) * 0.5f
                : sorted[mid];
        }

        /// <summary>프레임 시간 범위 선택의 집계 결과입니다.</summary>
        public struct FrameRangeStats
        {
            public int count;
            public float minMs;
            public float maxMs;
            public float meanMs;
            public float p95Ms;
        }

        /// <summary>frameSamplesMs의 [startInclusive, endExclusive) 구간 집계입니다. 범위는 내부에서 클램프합니다.</summary>
        public static FrameRangeStats FrameStats(float[] samples, int startInclusive, int endExclusive)
        {
            var stats = new FrameRangeStats();
            if (samples == null || samples.Length == 0)
            {
                return stats;
            }

            int start = Math.Max(0, Math.Min(startInclusive, endExclusive));
            int end = Math.Min(samples.Length, Math.Max(startInclusive, endExclusive));
            if (start >= end)
            {
                return stats;
            }

            float sum = 0f;
            stats.minMs = float.MaxValue;
            stats.maxMs = float.MinValue;
            var sorted = new float[end - start];
            for (int i = start; i < end; i++)
            {
                float v = samples[i];
                sorted[i - start] = v;
                sum += v;
                stats.minMs = Math.Min(stats.minMs, v);
                stats.maxMs = Math.Max(stats.maxMs, v);
            }
            stats.count = end - start;
            stats.meanMs = sum / stats.count;
            Array.Sort(sorted);
            stats.p95Ms = sorted[Math.Clamp(
                (int)Math.Ceiling(sorted.Length * 0.95) - 1, 0, sorted.Length - 1)];
            return stats;
        }
    }
}
