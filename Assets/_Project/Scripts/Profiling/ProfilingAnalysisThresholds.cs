using System;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.Profiling
{
    /// <summary>
    /// 자동 분석 임계값입니다. PerfBaselines/analysis-thresholds.json이 있으면 그 값을,
    /// 없거나 파싱 실패 시 코드 기본값을 사용합니다(리포트 생성을 막지 않습니다).
    /// </summary>
    [Serializable]
    public class ProfilingAnalysisThresholds
    {
        public float bottleneckShareOfTotal = 0.3f;
        public float bottleneckMinAbsMs = 200f;
        public float trendRegressionFactor = 1.5f;
        public float trendRegressionMinDeltaMs = 50f;
        public int trendMinHistory = 3;
        public float frameSpikeFactor = 2f;
        public float frameSpikeMinMs = 33f;
        public float gcSpikeMinMb = 10f;

        public static string ConfigPath =>
            Path.Combine(Application.dataPath, "_Project", "Tests", "PerfBaselines",
                "analysis-thresholds.json");

        /// <summary>설정 파일을 읽습니다. 어떤 실패든 기본값으로 폴백합니다.</summary>
        public static ProfilingAnalysisThresholds Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var loaded = JsonUtility.FromJson<ProfilingAnalysisThresholds>(
                        File.ReadAllText(ConfigPath));
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 분석 임계값 로드 실패(기본값 사용): {e.Message}");
            }
            return new ProfilingAnalysisThresholds();
        }
    }
}
