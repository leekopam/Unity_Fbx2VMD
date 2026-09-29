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

        private const float BottleneckShareOfTotal = 0.3f;
        private const float BottleneckMinAbsMs = 200f;
        private const float TrendRegressionFactor = 1.5f;
        private const float TrendRegressionMinDeltaMs = 50f;
        private const int TrendMinHistory = 3;
        private const float FrameSpikeFactor = 2f;
        private const float FrameSpikeMinMs = 33f;
        private const long GcSpikeMinBytes = 10L * 1024L * 1024L;

        /// <summary>
        /// 사용자 대기·프리뷰 상태입니다. 파이프라인 작업 구간이 아니므로 병목/트렌드/GC 판정에서 제외합니다.
        /// 상태명은 FBXSessionState enum의 ToString 결과와 문자열로 맞춥니다(역방향 어셈블리 참조 불가).
        /// </summary>
        private static readonly HashSet<string> InteractiveStages = new HashSet<string>(StringComparer.Ordinal)
        {
            "Idle", "Ready", "PreviewPlaying", "PreviewPaused",
        };

        /// <summary>
        /// 동일 이름의 스테이지 샘플을 첫 등장 순서대로 하나로 합산합니다.
        /// 구 버전 리포트의 프레임 단위 하트비트(동일 스테이지 수천 샘플)도 정상 판정하기 위한 호환층입니다.
        /// deltaMs/gcDeltaBytes는 합산, message는 마지막 값을 씁니다.
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
                            stage = s.stage,
                            sinceRunStartMs = s.sinceRunStartMs,
                        };
                        byStage[key] = agg;
                        order.Add(key);
                    }
                    agg.message = s.message;
                    agg.deltaMs += s.deltaMs;
                    agg.gcDeltaBytes += s.gcDeltaBytes;
                }
            }
            var result = new List<ProfilingStageSample>(order.Count);
            foreach (string key in order)
            {
                result.Add(byStage[key]);
            }
            return result;
        }

        private static bool IsInteractiveStage(string stage)
        {
            return InteractiveStages.Contains(stage ?? string.Empty);
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
            ProfilingRunRecord current, IReadOnlyList<ProfilingRunRecord> history)
        {
            var findings = new List<ProfilingFinding>();
            if (current == null)
            {
                return findings;
            }

            AnalyzeBottlenecks(current, findings);
            AnalyzeTrend(current, history, findings);
            AnalyzeFailure(current, findings);
            AnalyzeFrameSpike(current, findings);
            AnalyzeGcSpike(current, findings);
            return findings;
        }

        private static void AnalyzeBottlenecks(ProfilingRunRecord current, List<ProfilingFinding> findings)
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
                bool shareHit = stage.deltaMs >= current.totalMs * BottleneckShareOfTotal;
                if (shareHit && stage.deltaMs >= BottleneckMinAbsMs)
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
            ProfilingRunRecord current, IReadOnlyList<ProfilingRunRecord> history, List<ProfilingFinding> findings)
        {
            if (history == null || history.Count < TrendMinHistory || current.stages == null)
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
                    if (IsInteractiveStage(stage.stage))
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
                if (IsInteractiveStage(stage.stage))
                {
                    continue;
                }
                if (!historyByStage.TryGetValue(stage.stage, out List<float> samples) ||
                    samples.Count < TrendMinHistory)
                {
                    continue;
                }

                float median = Median(samples);
                bool regressed =
                    stage.deltaMs > median * TrendRegressionFactor &&
                    stage.deltaMs - median >= TrendRegressionMinDeltaMs;
                if (regressed)
                {
                    findings.Add(new ProfilingFinding
                    {
                        kind = KindTrendRegression,
                        severity = SeverityWarning,
                        stage = stage.stage,
                        detail = $"트렌드 회귀: {stage.stage} {stage.deltaMs:F0}ms > 과거 중앙값 {median:F0}ms x {TrendRegressionFactor:F1} (표본 {samples.Count}회)",
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

        private static void AnalyzeFrameSpike(ProfilingRunRecord current, List<ProfilingFinding> findings)
        {
            if (current.sampledFrameCount <= 0 || current.sampledFrameSumMs <= 0f)
            {
                return;
            }

            float mean = current.sampledFrameSumMs / current.sampledFrameCount;
            if (current.sampledFrameMaxMs > mean * FrameSpikeFactor &&
                current.sampledFrameMaxMs > FrameSpikeMinMs)
            {
                findings.Add(new ProfilingFinding
                {
                    kind = KindFrameSpike,
                    severity = SeverityWarning,
                    stage = string.Empty,
                    detail = $"프레임 스파이크: 최대 {current.sampledFrameMaxMs:F1}ms > 평균 {mean:F1}ms x {FrameSpikeFactor:F1} ({current.sampledFrameCount}프레임)",
                    suggestedScopes = new List<string>(FrameSpikeScopes),
                });
            }
        }

        private static void AnalyzeGcSpike(ProfilingRunRecord current, List<ProfilingFinding> findings)
        {
            if (current.stages == null)
            {
                return;
            }

            foreach (ProfilingStageSample stage in AggregateStagesByName(current.stages))
            {
                if (IsInteractiveStage(stage.stage))
                {
                    continue;
                }
                if (stage.gcDeltaBytes > GcSpikeMinBytes)
                {
                    findings.Add(new ProfilingFinding
                    {
                        kind = KindGcSpike,
                        severity = SeverityWarning,
                        stage = stage.stage,
                        detail = $"GC 스파이크: {stage.stage} +{stage.gcDeltaBytes / (1024f * 1024f):F1}MB",
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

        private static float Median(List<float> samples)
        {
            var sorted = samples.OrderBy(v => v).ToArray();
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) * 0.5f
                : sorted[mid];
        }
    }
}
