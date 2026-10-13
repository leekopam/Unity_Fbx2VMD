using System.IO;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// 업그레이드 전후 diff용 JSON 리포트를 TestResults/UpgradeReadiness/ 아래에 기록함.
    /// </summary>
    public static class UpgradeReadinessReport
    {
        public static string ReportDirectory =>
            Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "UpgradeReadiness");

        public static string Write(string fileName, string content)
        {
            Directory.CreateDirectory(ReportDirectory);
            string path = Path.Combine(ReportDirectory, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>
        /// gitignored 유료 자산(MMD4Mecanim, Final IK, Magica Cloth 2)이 없는 환경이면
        /// 테스트를 실패 대신 Ignore 처리함 — 클린 클론/CI에서는 프로비저닝 전제를
        /// 검증할 수 없다.
        /// </summary>
        public static void RequireProvisionedAssets()
        {
            string root = Directory.GetCurrentDirectory();
            var missing = new System.Collections.Generic.List<string>();
            if (!Directory.Exists(Path.Combine(root, "Assets", "MMD4Mecanim")))
            {
                missing.Add("MMD4Mecanim");
            }
            if (!Directory.Exists(Path.Combine(root, "Assets", "Plugins", "RootMotion")))
            {
                missing.Add("FinalIK(RootMotion)");
            }
            // 두 설치 경로 모두 허용 — 어느 한 곳이면 설치로 본다
            if (!Directory.Exists(Path.Combine(root, "Assets", "_Project", "Plugins", "MagicaCloth2"))
                && !Directory.Exists(Path.Combine(root, "Assets", "MagicaCloth2")))
            {
                missing.Add("MagicaCloth2");
            }
            if (missing.Count > 0)
            {
                NUnit.Framework.Assert.Ignore(
                    "유료 자산 미설치 환경 — 클린 클론/CI: " + string.Join(", ", missing));
            }
        }
    }
}
