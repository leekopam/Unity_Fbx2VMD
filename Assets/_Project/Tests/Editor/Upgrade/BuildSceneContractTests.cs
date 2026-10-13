using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// 씬 무결성 계약: 빌드 씬 존재, 전 씬 MissingScript=0, 컴포넌트 인벤토리 JSON 내보내기.
    /// 패키지 제거/버전 변경으로 컴포넌트 타입이 사라지면 MissingScript로 드러난다.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class BuildSceneContractTests
    {
        private const string SceneRoot = "Assets/_Project/Scene";

        private string _restoreScenePath;

        [SetUp]
        public void CaptureActiveScene()
        {
            _restoreScenePath = SceneManager.GetActiveScene().path;
        }

        [TearDown]
        public void RestoreActiveScene()
        {
            if (!string.IsNullOrEmpty(_restoreScenePath) && File.Exists(_restoreScenePath))
            {
                EditorSceneManager.OpenScene(_restoreScenePath);
            }
        }

        private static List<string> AllProjectScenes()
        {
            return Directory.GetFiles(SceneRoot, "*.unity", SearchOption.TopDirectoryOnly)
                .Select(p => p.Replace('\\', '/'))
                .ToList();
        }

        [Test]
        public void AllBuildScenes_ExistOnDisk()
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                Assert.That(File.Exists(scene.path), Is.True,
                    $"빌드 설정에 등록된 씬이 디스크에 없음: {scene.path}");
            }
        }

        [Test]
        public void AllProjectScenes_HaveNoMissingScripts()
        {
            var failures = new StringBuilder();

            foreach (string scenePath in AllProjectScenes())
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int missing = scene.GetRootGameObjects()
                    .Sum(root => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root));

                if (missing > 0)
                {
                    failures.AppendLine($"{scenePath}: missing={missing}");
                }
            }

            Assert.That(failures.ToString(), Is.Empty,
                "Missing Script 컴포넌트가 있는 씬이 있음 (패키지/스크립트 타입 손실 신호)");
        }

        [Test]
        public void SceneComponentInventory_IsExportedForDiff()
        {
            // 업그레이드 전후로 이 JSON을 diff하면 어떤 컴포넌트가 사라졌는지 바로 보인다.
            var report = new StringBuilder("[\n");

            foreach (string scenePath in AllProjectScenes())
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var counts = new Dictionary<string, int>();

                foreach (Component component in scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Component>(true)))
                {
                    // Missing Script는 null Component로 나타난다.
                    string key = component == null ? "<missing-script>" : component.GetType().FullName;
                    counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                }

                report.Append("  {\n    \"scene\": \"").Append(scenePath).Append("\",\n    \"components\": {\n");
                foreach (var pair in counts.OrderBy(p => p.Key))
                {
                    report.Append("      \"").Append(pair.Key).Append("\": ").Append(pair.Value).Append(",\n");
                }
                if (report.Length > 2 && report[report.Length - 2] == ',')
                {
                    report.Remove(report.Length - 2, 1); // components 후행 콤마 제거
                }
                report.Append("    }\n  },\n");
            }
            if (report.Length > 2)
            {
                report.Remove(report.Length - 2, 1); // 배열 후행 콤마 제거
            }
            report.Append("]\n");

            string path = UpgradeReadinessReport.Write("scene-components.json", report.ToString());
            Assert.That(File.Exists(path), Is.True, "컴포넌트 인벤토리 리포트가 작성되지 않음");
        }
    }
}
