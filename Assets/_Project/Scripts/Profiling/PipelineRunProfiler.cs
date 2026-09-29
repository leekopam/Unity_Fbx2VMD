using System;
using System.Collections.Generic;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Fbx2Vmd.Profiling
{
    /// <summary>
    /// FBX→VMD 변환 세션 1회를 작업(Run) 단위로 계측하는 정적 레코더입니다.
    /// BeginConversionSession에서 BeginRun, SetSessionState에서 NoteStage가 호출됩니다.
    /// 활성 런이 없으면 모든 호출이 즉시 반환되어 오버헤드가 무시할 수준입니다.
    /// 메인 스레드 전용으로 설계했습니다.
    /// </summary>
    public static class PipelineRunProfiler
    {
        private static ProfilingRunRecord _current;
        private static Stopwatch _runWatch;
        private static long _runStartGcBytes;
        private static float _lastStageMarkMs;
        private static long _lastStageGcBytes;
        private static readonly Dictionary<string, ProfilingMetricSample> _metricIndex =
            new Dictionary<string, ProfilingMetricSample>();

        /// <summary>마지막으로 저장된 리포트 경로입니다. 테스트에서 검증용으로 사용합니다.</summary>
        public static string LastWrittenReportPath { get; private set; } = string.Empty;

        public static bool IsRunActive => _current != null;

        /// <summary>새 변환 런 계측을 시작합니다. 이미 진행 중이면 기존 런을 버리고 다시 시작합니다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void BeginRun(string label)
        {
            if (_current != null)
            {
                EndRun("AbortedByNewRun");
            }

            _runWatch = Stopwatch.StartNew();
            // Unity 2022의 GC 카운터는 현재 스레드 기준이라 워커 스레드 할당은 잡히지 않는다(한계로 문서화).
            _runStartGcBytes = GC.GetAllocatedBytesForCurrentThread();
            _lastStageMarkMs = 0f;
            _lastStageGcBytes = _runStartGcBytes;
            _metricIndex.Clear();

            string runStamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            _current = new ProfilingRunRecord
            {
                runId = $"{runStamp}-{Guid.NewGuid().ToString("N").Substring(0, 6)}",
                label = label ?? string.Empty,
                startedUtc = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion,
                deviceModel = SystemInfo.deviceModel,
                revision = Environment.GetEnvironmentVariable("FBX2VMD_GIT_COMMIT") ?? string.Empty,
            };
        }

        /// <summary>
        /// 세션 상태 전이를 스테이지 샘플로 기록합니다.
        /// isTerminal이면 런을 마감하고 JSON을 저장합니다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void NoteStage(string stage, string message, bool isTerminal)
        {
            if (_current == null)
            {
                return;
            }

            float nowMs = (float)_runWatch.Elapsed.TotalMilliseconds;
            long nowGc = GC.GetAllocatedBytesForCurrentThread();

            // 같은 스테이지의 연속 호출(프레임 단위 진행률 하트비트)은 마지막 샘플에 병합한다.
            // 스키마 불변식 sinceRunStartMs - deltaMs = 구간 시작을 유지하기 위해
            // 누적 시각을 구간 끝으로 갱신하고 소요·GC를 합산한다.
            int stageCount = _current.stages.Count;
            ProfilingStageSample last = stageCount > 0 ? _current.stages[stageCount - 1] : null;
            if (!isTerminal && last != null &&
                string.Equals(last.stage, stage ?? string.Empty, StringComparison.Ordinal))
            {
                last.message = message ?? string.Empty;
                last.deltaMs += nowMs - _lastStageMarkMs;
                last.sinceRunStartMs = nowMs;
                last.gcDeltaBytes += nowGc - _lastStageGcBytes;
            }
            else
            {
                _current.stages.Add(new ProfilingStageSample
                {
                    stage = stage ?? string.Empty,
                    message = message ?? string.Empty,
                    sinceRunStartMs = nowMs,
                    deltaMs = nowMs - _lastStageMarkMs,
                    gcDeltaBytes = nowGc - _lastStageGcBytes,
                });
            }
            _lastStageMarkMs = nowMs;
            _lastStageGcBytes = nowGc;

            if (isTerminal)
            {
                EndRun(stage);
            }
        }

        /// <summary>PerfScope.Dispose가 호출하는 지표 집계 지점입니다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal static void RecordMetric(string name, float elapsedMs, long gcAllocBytes)
        {
            if (_current == null)
            {
                return;
            }

            if (!_metricIndex.TryGetValue(name, out ProfilingMetricSample sample))
            {
                sample = new ProfilingMetricSample { name = name };
                _metricIndex[name] = sample;
                _current.metrics.Add(sample);
            }

            sample.calls++;
            sample.totalMs += elapsedMs;
            sample.maxMs = Mathf.Max(sample.maxMs, elapsedMs);
            sample.totalGcAllocBytes += gcAllocBytes;
        }

        /// <summary>프레임 시간 샘플을 누적합니다. 런 활성 중에만 호출 의미가 있습니다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void SampleFrame(float deltaTimeSeconds)
        {
            if (_current == null)
            {
                return;
            }

            float ms = deltaTimeSeconds * 1000f;
            _current.sampledFrameCount++;
            _current.sampledFrameSumMs += ms;
            if (ms > _current.sampledFrameMaxMs)
            {
                _current.sampledFrameMaxMs = ms;
            }
        }

        /// <summary>런을 마감하고 리포트를 저장합니다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void EndRun(string outcome)
        {
            if (_current == null)
            {
                return;
            }

            _current.endedUtc = DateTime.UtcNow.ToString("o");
            _current.outcome = outcome ?? string.Empty;
            _current.totalMs = (float)_runWatch.Elapsed.TotalMilliseconds;
            _current.totalGcAllocBytes = GC.GetAllocatedBytesForCurrentThread() - _runStartGcBytes;

            try
            {
                _current.analysis = ProfilingRunAnalyzer.Analyze(_current, LoadHistory(_current));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 런 분석 실패(기록은 계속): {e.Message}");
            }

            try
            {
                LastWrittenReportPath = string.Empty;
                LastWrittenReportPath = ProfilingReportWriter.Write(_current);
                Debug.Log($"[Profiling] 런 기록 저장: {LastWrittenReportPath} " +
                          $"(total={_current.totalMs:F1}ms, stages={_current.stages.Count})");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 런 기록 저장 실패: {e.Message}");
            }

            _current = null;
            _metricIndex.Clear();
        }

        /// <summary>같은 라벨·머신의 최근 런 최대 20개를 로드합니다. 실패해도 빈 목록으로 진행합니다.</summary>
        private static List<ProfilingRunRecord> LoadHistory(ProfilingRunRecord current)
        {
            var history = new List<ProfilingRunRecord>();
            try
            {
                foreach (string path in ProfilingReportWriter.ListReports())
                {
                    if (history.Count >= 20)
                    {
                        break;
                    }

                    ProfilingRunRecord past = ProfilingReportWriter.Load(path);
                    if (past == null || past.runId == current.runId)
                    {
                        continue;
                    }
                    if (!string.Equals(past.label, current.label, StringComparison.Ordinal) ||
                        !string.Equals(past.deviceModel, current.deviceModel, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    history.Add(past);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 이력 로드 실패(분석은 이번 런만으로 진행): {e.Message}");
            }

            return history;
        }

        /// <summary>저장 없이 현재 런을 폐기합니다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void AbortRun()
        {
            _current = null;
            _metricIndex.Clear();
        }
    }
}
