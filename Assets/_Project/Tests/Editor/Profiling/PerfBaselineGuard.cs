using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.Tests.Editor.Profiling
{
    /// <summary>
    /// 성능 베이스라인(커밋 대상 JSON)과 현재 측정값을 비교합니다.
    /// 환경 변수 FBX2VMD_PERF_UPDATE_BASELINE=1 일 때는 비교 대신 베이스라인을 갱신합니다.
    /// 베이스라인이 없으면 이번 값을 기록만 하고 통과합니다(첫 측정).
    /// </summary>
    public static class PerfBaselineGuard
    {
        public const string UpdateEnvVar = "FBX2VMD_PERF_UPDATE_BASELINE";
        public const float DefaultTolerance = 0.2f;

        /// <summary>에디터 실행 중 수동 갱신용. 테스트 직전에 true로 두면 이번 실행을 베이스라인으로 기록합니다.</summary>
        public static bool ForceUpdate;

        [Serializable]
        private class BaselineEntry
        {
            public string name;
            public float valueMs;
        }

        [Serializable]
        private class BaselineFile
        {
            public List<BaselineEntry> entries = new List<BaselineEntry>();
        }

        public static string BaselinePath =>
            Path.Combine(Application.dataPath, "_Project", "Tests", "PerfBaselines", "baselines.json");

        /// <summary>베이스라인은 머신별로 분리합니다. 다른 하드웨어의 절대값 비교는 무의미하기 때문입니다.</summary>
        private static string KeyOf(string name) => $"{name}|{SystemInfo.deviceModel}";

        /// <summary>
        /// name의 측정값이 베이스라인*(1+tolerance)을 넘으면 메시지와 함께 false를 반환합니다.
        /// 갱신 모드이면 베이스라인 파일을 쓰고 true를 반환합니다.
        /// </summary>
        public static bool Check(string name, float measuredMs, out string message, float tolerance = DefaultTolerance)
        {
            string key = KeyOf(name);
            BaselineFile file = LoadFile();
            BaselineEntry entry = file.entries.Find(e => e.name == key);
            bool updateMode = ForceUpdate || string.Equals(
                Environment.GetEnvironmentVariable(UpdateEnvVar), "1", StringComparison.Ordinal);

            if (updateMode)
            {
                if (entry == null)
                {
                    entry = new BaselineEntry { name = key };
                    file.entries.Add(entry);
                }
                entry.valueMs = measuredMs;
                SaveFile(file);
                message = $"베이스라인 갱신: {key}={measuredMs:F3}ms";
                return true;
            }

            if (entry == null)
            {
                message = $"베이스라인 없음(첫 측정으로 통과): {name}={measuredMs:F3}ms";
                return true;
            }

            float limit = entry.valueMs * (1f + tolerance);
            if (measuredMs > limit)
            {
                message = $"성능 회귀: {name} {measuredMs:F3}ms > baseline {entry.valueMs:F3}ms * {1f + tolerance:F2}";
                return false;
            }

            message = $"{name}: {measuredMs:F3}ms <= {limit:F3}ms (baseline {entry.valueMs:F3}ms)";
            return true;
        }

        private static BaselineFile LoadFile()
        {
            try
            {
                if (File.Exists(BaselinePath))
                {
                    BaselineFile loaded = JsonUtility.FromJson<BaselineFile>(File.ReadAllText(BaselinePath));
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 베이스라인 로드 실패: {e.Message}");
            }

            return new BaselineFile();
        }

        private static void SaveFile(BaselineFile file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath));
            File.WriteAllText(BaselinePath, JsonUtility.ToJson(file, true));
        }
    }
}
