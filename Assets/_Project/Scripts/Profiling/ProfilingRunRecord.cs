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
        public int sampledFrameCount;
        public float sampledFrameSumMs;
        public float sampledFrameMaxMs;
        public List<ProfilingStageSample> stages = new List<ProfilingStageSample>();
        public List<ProfilingMetricSample> metrics = new List<ProfilingMetricSample>();
        /// <summary>EndRun 시점에 ProfilingRunAnalyzer가 채우는 자동 분석 결과입니다.</summary>
        public List<ProfilingFinding> analysis = new List<ProfilingFinding>();
    }
}
