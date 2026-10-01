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
                        loaded.Sanitize();
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

        /// <summary>
        /// 비정상 입력(NaN/Infinity/음수/0)을 유효 범위로 보정합니다.
        /// 수동 편집된 JSON이나 편집 패널 입력이 그대로 분석에 들어가지 않도록 경계에서 호출합니다.
        /// </summary>
        public void Sanitize()
        {
            bottleneckShareOfTotal = ClampFinite(bottleneckShareOfTotal, 0.01f, 1f, 0.3f);
            bottleneckMinAbsMs = ClampFinite(bottleneckMinAbsMs, 1f, float.MaxValue, 200f);
            trendRegressionFactor = ClampFinite(trendRegressionFactor, 1f, float.MaxValue, 1.5f);
            trendRegressionMinDeltaMs =
                ClampFinite(trendRegressionMinDeltaMs, 1f, float.MaxValue, 50f);
            trendMinHistory = Mathf.Max(1, trendMinHistory);
            frameSpikeFactor = ClampFinite(frameSpikeFactor, 1f, float.MaxValue, 2f);
            frameSpikeMinMs = ClampFinite(frameSpikeMinMs, 1f, float.MaxValue, 33f);
            gcSpikeMinMb = ClampFinite(gcSpikeMinMb, 0.01f, float.MaxValue, 10f);
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// 임계값을 JSON으로 저장합니다. path를 생략하면 공용 ConfigPath에 기록합니다.
        /// 저장 전 Sanitize를 적용해 NaN/Infinity/범위 외 값이 파일에 기록되지 않게 합니다
        /// (전달된 인스턴스의 필드도 정규화됩니다).
        /// </summary>
        public static void Save(ProfilingAnalysisThresholds thresholds, string path = null)
        {
            var safe = thresholds ?? new ProfilingAnalysisThresholds();
            safe.Sanitize();
            string target = path ?? ConfigPath;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, JsonUtility.ToJson(safe, true));
        }
    }
}
