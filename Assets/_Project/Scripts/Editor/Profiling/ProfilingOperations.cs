using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Profiling.EditorTools
{
    /// <summary>
    /// 프로파일링 운영 동작 모음입니다.
    /// - 에디터 메뉴: 이 머신의 성능 베이스라인을 재측정해 baselines.json에 기록
    /// - 배치 진입점: -executeMethod Fbx2Vmd.Profiling.EditorTools.BatchOps.RunFixtureProfile
    ///   로 헤드리스 환경에서 픽스처 계측 런과 리포트를 생성합니다.
    /// </summary>
    public static class BatchOps
    {
        private const string FixturePath = "Assets/_Project/Tests/PerfFixtures/test.fbx";

        /// <summary>CI/에이전트용 헤드리스 계측 런. 픽스처 검사+본 매핑 로드를 계측하고 리포트를 저장합니다.</summary>
        public static void RunFixtureProfile()
        {
            PipelineRunProfiler.BeginRun("batch-fixture");
            try
            {
                if (File.Exists(FixturePath))
                {
                    using (PerfScope.Measure("Assimp.Inspect.TestFbx"))
                    {
                        AssimpFBXImporter.InspectAnimationFile(FixturePath);
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[Profiling] 픽스처 없음: {FixturePath}");
                }

                MethodInfo load = typeof(FBXImportController).GetMethod(
                    "LoadBoneMappingRuntime", BindingFlags.Static | BindingFlags.NonPublic);
                if (load != null)
                {
                    using (PerfScope.Measure("BoneMapping.Load"))
                    {
                        load.Invoke(null, null);
                    }
                }

                PipelineRunProfiler.NoteStage("Success", "배치 픽스처 계측 완료", isTerminal: true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[Profiling] 배치 계측 실패: {e}");
                PipelineRunProfiler.EndRun("Failed");
            }
            UnityEngine.Debug.Log($"[Profiling] 배치 리포트: {PipelineRunProfiler.LastWrittenReportPath}");
        }

        /// <summary>Tools &gt; Profiling &gt; 베이스라인 재측정 — 이 머신의 기준값을 새로 기록합니다.</summary>
        [MenuItem("Tools/Profiling/베이스라인 재측정(이 머신)")]
        public static void RebuildBaselines()
        {
            var results = new System.Text.StringBuilder();

            // PerfScope 1회 호출 비용
            const int loops = 200;
            for (int i = 0; i < loops; i++)
            {
                using (PerfScope.Measure("Bench.Warmup")) { }
            }
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < loops; i++)
            {
                using (PerfScope.Measure("Bench.Scope")) { }
            }
            watch.Stop();
            float perCallUs = (float)watch.Elapsed.TotalMilliseconds / loops * 1000f;
            results.AppendLine(WriteBaseline("PerfScope.PerCall", perCallUs));

            // Assimp 픽스처 파싱(5회 중앙값)
            if (File.Exists(FixturePath))
            {
                AssimpFBXImporter.InspectAnimationFile(FixturePath);
                float median = MedianOf(5,
                    () => AssimpFBXImporter.InspectAnimationFile(FixturePath));
                results.AppendLine(WriteBaseline("Assimp.Inspect.TestFbx", median));
            }

            // 본 매핑 로드(10회 중앙값)
            MethodInfo load = typeof(FBXImportController).GetMethod(
                "LoadBoneMappingRuntime", BindingFlags.Static | BindingFlags.NonPublic);
            if (load != null)
            {
                load.Invoke(null, null);
                float median = MedianOf(10, () => load.Invoke(null, null));
                results.AppendLine(WriteBaseline("BoneMapping.Load", median));
            }

            AssetDatabase.ImportAsset(BaselineAssetPath);
            UnityEngine.Debug.Log($"[Profiling] 베이스라인 재측정 완료\n{results}");
        }

        // PerfBaselineGuard(테스트 어셈블리)와 동일한 스키마/머신 키 규칙을 미러한다.
        // 테스트 asmdef는 autoReferenced=false라 직접 참조할 수 없다.
        private const string BaselineAssetPath = "Assets/_Project/Tests/PerfBaselines/baselines.json";

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

        private static string WriteBaseline(string name, float valueMs)
        {
            string key = $"{name}|{SystemInfo.deviceModel}";
            string path = Path.Combine(Directory.GetCurrentDirectory(), BaselineAssetPath);
            BaselineFile file = new BaselineFile();
            if (File.Exists(path))
            {
                file = JsonUtility.FromJson<BaselineFile>(File.ReadAllText(path)) ?? file;
            }
            BaselineEntry entry = file.entries.Find(e => e.name == key);
            if (entry == null)
            {
                entry = new BaselineEntry { name = key };
                file.entries.Add(entry);
            }
            entry.valueMs = valueMs;
            File.WriteAllText(path, JsonUtility.ToJson(file, true));
            return $"베이스라인 갱신: {key}={valueMs:F3}ms";
        }

        private static float MedianOf(int runs, Action action)
        {
            var samples = new float[runs];
            for (int i = 0; i < runs; i++)
            {
                var watch = Stopwatch.StartNew();
                action();
                watch.Stop();
                samples[i] = (float)watch.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);
            return samples[runs / 2];
        }
    }
}
