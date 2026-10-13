using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System.IO;
using System.Text;
using UnityEngine;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// Assimp 네이티브 임포트 경로의 골든 계약.
    /// assimp.dll 로드 → FBX 파싱 → 씬 오브젝트 생성까지가 네이티브 의존 경로이며
    /// Unity 버전이 바뀌어도 동일해야 할 결과물을 JSON으로 남긴다.
    /// </summary>
    // assimp.dll은 Windows x64 네이티브 — Linux CI에서는 실행 불가
    [Platform("Win")]
    [Category("UpgradeReadiness")]
    public class AssimpImportGoldenTests
    {
        private const string TestFbxPath = "Assets/_Project/Tests/PerfFixtures/test.fbx";
        private const string ProjectFbxRoot = "Assets/_Project/FBX";

        [Test]
        public void AssimpLibrary_LoadsInEditor()
        {
            AssimpLibraryLoader.LoadLibrary();
            Assert.That(AssimpLibraryLoader.IsLoaded, Is.True,
                "assimp.dll 네이티브 로드 실패");
        }

        [Test]
        public void AllProjectFbx_InspectionMetrics_AreExportedForDiff()
        {
            AssimpLibraryLoader.LoadLibrary();
            Assert.That(AssimpLibraryLoader.IsLoaded, Is.True, "assimp.dll 로드 실패");

            // FBX 드롭 폴더는 gitignored 로컬 자산 — 없는 환경(클린 클론)에서는 스킵
            if (!Directory.Exists(ProjectFbxRoot))
            {
                Assert.Ignore($"{ProjectFbxRoot} 없음 — 로컬 FBX 드롭 폴더 부재");
            }

            var report = new StringBuilder("[\n");
            var failures = new StringBuilder();

            foreach (string fbxPath in Directory.GetFiles(ProjectFbxRoot, "*.fbx"))
            {
                var inspection = AssimpFBXImporter.InspectAnimationFile(fbxPath.Replace('\\', '/'));

                report.Append("  {\n")
                    .Append("    \"file\": \"").Append(Path.GetFileName(fbxPath)).Append("\",\n")
                    .Append("    \"readable\": ").Append(inspection.FileReadable ? "true" : "false").Append(",\n")
                    .Append("    \"succeeded\": ").Append(inspection.ImportSucceeded ? "true" : "false").Append(",\n")
                    .Append("    \"animations\": ").Append(inspection.AnimationCount).Append(",\n")
                    .Append("    \"posKeys\": ").Append(inspection.PositionKeyCount).Append(",\n")
                    .Append("    \"rotKeys\": ").Append(inspection.RotationKeyCount).Append(",\n")
                    .Append("    \"scaleKeys\": ").Append(inspection.ScaleKeyCount).Append(",\n")
                    .Append("    \"maxLenSec\": ").Append(inspection.MaxAnimationLengthSeconds
                        .ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n")
                    .Append("    \"error\": \"").Append(inspection.ErrorMessage?
                        .Replace('\\', '/').Replace("\"", "'")
                        .Replace("\r", " ").Replace("\n", " ").Replace("\t", " ") ?? "").Append("\"\n")
                    .Append("  },\n");

                if (!inspection.FileReadable)
                {
                    failures.AppendLine($"{fbxPath}: 읽기 실패");
                }
            }
            if (report.Length > 2)
            {
                report.Remove(report.Length - 2, 1); // 후행 콤마 제거
            }
            report.Append("]\n");

            string reportPath = UpgradeReadinessReport.Write("assimp-fbx-metrics.json", report.ToString());
            Assert.That(File.Exists(reportPath), Is.True, "Assimp 메트릭 리포트 미작성");
            Assert.That(failures.ToString(), Is.Empty, "읽을 수 없는 FBX가 있음");
        }

        [Test]
        public void TestFbx_ImportsIntoScene()
        {
            var importer = new AssimpFBXImporter();
            GameObject root = null;

            try
            {
                root = importer.ImportSynchronouslyForEditorDiagnostics(TestFbxPath);

                Assert.That(root, Is.Not.Null, "test.fbx 임포트 결과가 null");
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.GreaterThan(1),
                    "임포트된 계층이 비어 있음");
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }
    }
}
