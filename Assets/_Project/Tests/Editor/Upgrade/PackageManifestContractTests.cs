using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// 패키지 매니페스트 계약: 필수 패키지 존재 + 해석된 버전을 JSON으로 내보내
    /// 버전 간 diff를 가능하게 한다.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class PackageManifestContractTests
    {
        private static string PackagesDir =>
            Path.Combine(Directory.GetCurrentDirectory(), "Packages");

        // 파이프라인이 직접 의존하는 패키지 — 빠지면 해당 기능 경로가 죽는다.
        private static readonly string[] RequiredPackages =
        {
            "com.unity.render-pipelines.universal",
            "com.unity.recorder",
            "com.unity.animation.rigging",
            "com.unity.burst",
            "com.unity.collections",
            "com.unity.mathematics",
            "com.unity.test-framework",
            "com.hecomi.ulipsync",
            "com.vrmc.univrm",
            "jp.lilxyzw.liltoon",
        };

        [Test]
        public void RequiredPackages_AreDeclared()
        {
            var manifest = JObject.Parse(File.ReadAllText(
                Path.Combine(PackagesDir, "manifest.json")));
            var dependencies = (JObject)manifest["dependencies"];

            var failures = new StringBuilder();
            foreach (string packageId in RequiredPackages)
            {
                if (dependencies[packageId] == null)
                {
                    failures.AppendLine(packageId);
                }
            }

            Assert.That(failures.ToString(), Is.Empty, "manifest에 필수 패키지가 없음");
        }

        [Test]
        public void ResolvedVersions_AreExportedForDiff()
        {
            string lockPath = Path.Combine(PackagesDir, "packages-lock.json");
            Assert.That(File.Exists(lockPath), Is.True, "packages-lock.json이 없음");

            var lockFile = JObject.Parse(File.ReadAllText(lockPath));
            var dependencies = (JObject)lockFile["dependencies"];

            var versions = new SortedDictionary<string, string>();
            foreach (var pair in dependencies)
            {
                versions[pair.Key] = pair.Value["version"]?.ToString() ?? "<unknown>";
            }

            var report = new StringBuilder("{\n");
            foreach (var pair in versions)
            {
                report.Append("  \"").Append(pair.Key).Append("\": \"")
                    .Append(pair.Value).Append("\",\n");
            }
            if (report.Length > 2)
            {
                report.Remove(report.Length - 2, 1); // 후행 콤마 제거
            }
            report.Append("}\n");

            string reportPath = UpgradeReadinessReport.Write("resolved-packages.json", report.ToString());
            Assert.That(File.Exists(reportPath), Is.True, "패키지 버전 리포트 미작성");
        }
    }
}
