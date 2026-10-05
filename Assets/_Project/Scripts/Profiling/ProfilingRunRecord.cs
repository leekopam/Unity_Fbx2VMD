using System;
using System.Collections.Generic;

namespace Fbx2Vmd.Profiling
{
    /// <summary>
    /// 세션 상태 진입 시점의 누적 타이밍 샘플입니다.
    /// </summary>
    [Serializable]
    public class ProfilingStageSample
    {
        public string stage;
        public string message;
        public float sinceRunStartMs;
        public float deltaMs;
        public long gcDeltaBytes;
        /// <summary>이 샘플에 병합된 NoteStage 호출 수입니다. 구 리포트(필드 없음)는 0으로 읽히며 집계 시 1로 간주합니다.</summary>
        public int calls;
    }

    /// <summary>
    /// PerfScope로 계측한 임의 구간의 집계입니다.
    /// </summary>
    [Serializable]
    public class ProfilingMetricSample
    {
        public string name;
        public int calls;
        public float totalMs;
        public float maxMs;
        public long totalGcAllocBytes;
    }

    /// <summary>
    /// 런 분석 결과 1건입니다. kind는 규칙 종류, severity는 심각도(Info/Warning/Error)입니다.
    /// suggestedScopes는 계측 심화(PerfScope 삽입) 후보이며 JEV 진단 도구의 선택지로도 쓰입니다.
    /// </summary>
    [Serializable]
    public class ProfilingFinding
    {
        public string kind;
        public string severity;
        public string stage;
        public string detail;
        public List<string> suggestedScopes = new List<string>();
    }

    /// <summary>
    /// 변환 세션 1회 실행의 성능 기록입니다. Artifacts/Profiling에 JSON으로 저장됩니다.
    /// </summary>
    [Serializable]
    public class ProfilingRunRecord
    {
        public int schemaVersion = 1;
        public string runId;
        public string label;
        public string startedUtc;
        public string endedUtc;
        public string unityVersion;
        public string deviceModel;
        public string revision;
        public string outcome;
        public float totalMs;
        public long totalGcAllocBytes;
        /// <summary>프로세스 전체 누적 할당(GC.GetTotalAllocatedBytes) 기준 런 할당입니다. 워커 스레드 포함 — 병렬 경로의 할당도 잡힙니다. 현재 Unity BCL에 API가 없어 -1로 기록됩니다.</summary>
        public long totalAllocAllThreadsBytes;
        public int sampledFrameCount;
        public float sampledFrameSumMs;
        public float sampledFrameMaxMs;
        /// <summary>EndRun에서 프레임 샘플로 계산하는 분위수입니다.</summary>
        public float frameP50Ms;
        public float frameP95Ms;
        public float frameP99Ms;
        /// <summary>런 동안 관측된 ProfilerRecorder 메모리 카운터 최대값(프레임 경계 샘플, 워커 스레드 포함).</summary>
        public long gcReservedBytes;
        public long systemUsedBytes;
        /// <summary>비교 정당성을 위한 입력 컨텍스트입니다. -1은 미기록입니다.</summary>
        public long inputBytes = -1;
        public float clipLengthSec = -1f;
        public int boneCount = -1;
        /// <summary>런 동안 수집한 프레임 시간 샘플(ms)입니다. 뷰어 차트·범위 집계용으로 보존합니다.</summary>
        public float[] frameSamplesMs = new float[0];
        public List<ProfilingStageSample> stages = new List<ProfilingStageSample>();
        public List<ProfilingMetricSample> metrics = new List<ProfilingMetricSample>();
        /// <summary>EndRun 시점에 ProfilingRunAnalyzer가 채우는 자동 분석 결과입니다.</summary>
        public List<ProfilingFinding> analysis = new List<ProfilingFinding>();
    }
}
