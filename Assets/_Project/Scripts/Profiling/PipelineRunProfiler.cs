using System;
using System.Collections.Generic;
using Unity.Profiling;
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
        private static long _runStartTotalAllocBytes;
        private static float _lastStageMarkMs;
        private static long _lastStageGcBytes;
        private static readonly List<float> _frameSamples = new List<float>();
        private static ProfilerRecorder _gcReservedRecorder;
        private static ProfilerRecorder _systemUsedRecorder;
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
            // 워커 스레드 할당을 잡기 위해 프로세스 전체 할당량도 별도로 누적한다.
            // 에디터 세션에서는 런과 무관한 ambient 할당(에디터 루프·임포트 워커 등)이 섞인다.
            _runStartTotalAllocBytes = GC.GetTotalAllocatedBytes(false);
            _lastStageMarkMs = 0f;
            _lastStageGcBytes = _runStartGcBytes;
            _metricIndex.Clear();
            _frameSamples.Clear();
            StartMemoryRecorders();

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
                last.calls++;
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
                    calls = 1,
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
            _frameSamples.Add(ms);
            if (ms > _current.sampledFrameMaxMs)
            {
                _current.sampledFrameMaxMs = ms;
            }
            SampleMemoryPeak(_current);
        }

        /// <summary>
        /// 런의 입력 컨텍스트를 기록합니다(비교 정당성 확보용). -1 이하 값은 미기록으로 둡니다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void SetContext(long inputBytes = -1, float clipLengthSec = -1f, int boneCount = -1)
        {
            if (_current == null)
            {
                return;
            }
            if (inputBytes >= 0) _current.inputBytes = inputBytes;
            if (clipLengthSec >= 0f) _current.clipLengthSec = clipLengthSec;
            if (boneCount >= 0) _current.boneCount = boneCount;
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
            _current.totalAllocAllThreadsBytes =
                GC.GetTotalAllocatedBytes(false) - _runStartTotalAllocBytes;
            CaptureMemoryCounters(_current);
            ComputeFramePercentiles(_current);
            _current.frameSamplesMs = _frameSamples.ToArray();

            try
            {
                _current.analysis = ProfilingRunAnalyzer.Analyze(
                    _current, LoadHistory(_current), ProfilingAnalysisThresholds.Load());
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
            _frameSamples.Clear();
            DisposeMemoryRecorders();
        }

        /// <summary>엔진 메모리 카운터를 런 동안 켭니다. 카운터명이 없는 환경이면 조용히 건너뜁니다.</summary>
        private static void StartMemoryRecorders()
        {
            try
            {
                _gcReservedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
                _systemUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 메모리 카운터 시작 실패(측정은 계속): {e.Message}");
            }
        }

        /// <summary>프레임 경계에 갱신되는 카운터 값의 런 중 최대치를 누적합니다(피크 기준).</summary>
        private static void SampleMemoryPeak(ProfilingRunRecord record)
        {
            try
            {
                if (_gcReservedRecorder.Valid && _gcReservedRecorder.LastValue > record.gcReservedBytes)
                {
                    record.gcReservedBytes = _gcReservedRecorder.LastValue;
                }
                if (_systemUsedRecorder.Valid && _systemUsedRecorder.LastValue > record.systemUsedBytes)
                {
                    record.systemUsedBytes = _systemUsedRecorder.LastValue;
                }
            }
            catch { /* 카운터 읽기 실패는 계측을 막지 않음 */ }
        }

        private static void CaptureMemoryCounters(ProfilingRunRecord record)
        {
            try
            {
                if (_gcReservedRecorder.Valid && _gcReservedRecorder.LastValue > record.gcReservedBytes)
                {
                    record.gcReservedBytes = _gcReservedRecorder.LastValue;
                }
                if (_systemUsedRecorder.Valid && _systemUsedRecorder.LastValue > record.systemUsedBytes)
                {
                    record.systemUsedBytes = _systemUsedRecorder.LastValue;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 메모리 카운터 읽기 실패: {e.Message}");
            }
        }

        private static void DisposeMemoryRecorders()
        {
            if (_gcReservedRecorder.Valid)
            {
                _gcReservedRecorder.Dispose();
            }
            if (_systemUsedRecorder.Valid)
            {
                _systemUsedRecorder.Dispose();
            }
            _gcReservedRecorder = default;
            _systemUsedRecorder = default;
        }

        /// <summary>프레임 샘플에서 p50/p95/p99를 계산합니다. 표본 10개 미만이면 생략합니다.</summary>
        private static void ComputeFramePercentiles(ProfilingRunRecord record)
        {
            if (_frameSamples.Count < 10)
            {
                return;
            }

            var sorted = _frameSamples.ToArray();
            Array.Sort(sorted);
            record.frameP50Ms = Percentile(sorted, 0.50f);
            record.frameP95Ms = Percentile(sorted, 0.95f);
            record.frameP99Ms = Percentile(sorted, 0.99f);
        }

        private static float Percentile(float[] sorted, float fraction)
        {
            int index = Mathf.Clamp(Mathf.CeilToInt(sorted.Length * fraction) - 1, 0, sorted.Length - 1);
            return sorted[index];
        }

        /// <summary>같은 라벨·머신의 최근 런 최대 20개를 로드합니다. 실패해도 빈 목록으로 진행합니다.</summary>
        private static List<ProfilingRunRecord> LoadHistory(ProfilingRunRecord current)
        {
            try
            {
                return ProfilingReportWriter.LoadMatching(current, 20);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 이력 로드 실패(분석은 이번 런만으로 진행): {e.Message}");
                return new List<ProfilingRunRecord>();
            }
        }

        /// <summary>저장 없이 현재 런을 폐기합니다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void AbortRun()
        {
            _current = null;
            _metricIndex.Clear();
            _frameSamples.Clear();
            DisposeMemoryRecorders();
        }
    }
}
