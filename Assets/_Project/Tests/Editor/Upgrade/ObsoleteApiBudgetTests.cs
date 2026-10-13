using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// 노후 API 사용량 예산. Find* 스윕 완료 — 폐기 대상(제네릭/비제네릭 FindObjectOfType·
    /// FindObjectsOfType)은 0곳, 잔여 8곳은 전부 비폐기 API인 FindObjectsOfTypeAll.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class ObsoleteApiBudgetTests
    {
        private const int FindCallsBudget = 8;
        private const int FindFilesBudget = 6;
        private const int Ppv2FilesBudget = 7;

        private static readonly Regex FindCallPattern =
            new Regex(@"FindObjects?OfType(All)?\s*[<(]", RegexOptions.Compiled);

        private static string[] ProjectSources()
        {
            // Upgrade 테스트 폴더 자체는 제외 — 이 파일의 정규식/네임스페이스
            // 리터럴이 집계에 섞이는 것을 막는다.
            string upgradeTestDir = Path.Combine(
                UnityEngine.Application.dataPath, "_Project", "Tests", "Editor", "Upgrade");

            return Directory.GetFiles(
                    Path.Combine(UnityEngine.Application.dataPath, "_Project"),
                    "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.StartsWith(upgradeTestDir))
                .ToArray();
        }

        [Test]
        public void FindObjectOfType_Usage_DoesNotExceedBaseline()
        {
            int callCount = 0;
            int fileCount = 0;

            foreach (string path in ProjectSources())
            {
                // 자기 자신의 정규식 리터럴이 집계되는 것을 막는다.
                if (path.EndsWith(nameof(ObsoleteApiBudgetTests) + ".cs"))
                {
                    continue;
                }

                int matches = FindCallPattern.Matches(File.ReadAllText(path)).Count;
                if (matches > 0)
                {
                    callCount += matches;
                    fileCount++;
                }
            }

            UnityEngine.Debug.Log(
                $"[UpgradeReadiness] Find* 사용: calls={callCount} files={fileCount}");
            Assert.That(callCount, Is.LessThanOrEqualTo(FindCallsBudget),
                $"Find* 호출이 베이스라인({FindCallsBudget})을 초과함: {callCount}");
            Assert.That(fileCount, Is.LessThanOrEqualTo(FindFilesBudget),
                $"Find* 사용 파일이 베이스라인({FindFilesBudget})을 초과함: {fileCount}");
        }

        [Test]
        public void Ppv2UsageFiles_DoNotGrow()
        {
            int fileCount = 0;

            foreach (string path in ProjectSources())
            {
                if (path.EndsWith(nameof(ObsoleteApiBudgetTests) + ".cs"))
                {
                    continue;
                }

                if (File.ReadAllText(path).Contains("UnityEngine.Rendering.PostProcessing"))
                {
                    fileCount++;
                }
            }

            UnityEngine.Debug.Log($"[UpgradeReadiness] PPv2 사용 파일: {fileCount}");
            Assert.That(fileCount, Is.LessThanOrEqualTo(Ppv2FilesBudget),
                $"PPv2 사용 파일이 늘어남 ({fileCount} > {Ppv2FilesBudget}) — 신규 PPv2 사용 금지");
        }
    }
}
