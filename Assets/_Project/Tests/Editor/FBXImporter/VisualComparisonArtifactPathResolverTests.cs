using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonArtifactPathResolverTests
    {
        [Test]
        public void Given_ProjectRelativePath_When_Resolving_Then_ReturnsAbsoluteProjectPath()
        {
            string projectRoot = Path.Combine(Path.GetTempPath(), "visual-compare-project");
            string resolved = (string)Invoke(
                "ResolveProjectRelative",
                "Docs/summary.json",
                projectRoot);

            Assert.That(resolved, Is.EqualTo(Path.Combine(projectRoot, "Docs", "summary.json")));
        }

        [Test]
        public void Given_ProjectRelativeArtifactPath_When_ConvertingToAbsolute_Then_UsesProjectRoot()
        {
            string projectRoot = Path.Combine(Path.GetTempPath(), "visual-compare-project");

            Assert.That(
                Invoke("ToAbsoluteProjectPath", "Docs/summary.json", projectRoot),
                Is.EqualTo(Path.Combine(projectRoot, "Docs", "summary.json")));
        }

        [Test]
        public void Given_ProjectArtifactPath_When_MakingRelative_Then_NormalizesSeparators()
        {
            string projectRoot = Path.Combine(Path.GetTempPath(), "visual-compare-project");
            string absolutePath = Path.Combine(projectRoot, "Docs", "summary.json");

            Assert.That(
                Invoke("MakeProjectRelative", absolutePath, projectRoot),
                Is.EqualTo("Docs/summary.json"));
        }

        [Test]
        public void Given_EquivalentPaths_When_Comparing_Then_ReturnsTrue()
        {
            string root = Path.Combine(Path.GetTempPath(), "visual-compare-project");

            Assert.That(
                Invoke("ReferToSameFile", Path.Combine(root, "Docs", "..", "summary.json"), Path.Combine(root, "summary.json")),
                Is.True);
        }

        [Test]
        public void Given_ShortSessionId_When_BuildingSafeId_Then_SanitizesWithoutShortening()
        {
            string result = (string)Invoke(
                "BuildSafeSessionId",
                " when:sample session ",
                "visual_compare",
                Path.Combine(Path.GetTempPath(), "visual-compare-project"),
                "Docs/ComparisonSessions",
                240,
                (object)new[] { "summary.json", "summary.md" });

            Assert.That(result, Is.EqualTo("when_sample_session"));
        }

        [Test]
        public void Given_LongSessionId_When_BuildingSafeId_Then_ReservesLeafFilePathLength()
        {
            string projectRoot = Path.Combine(Path.GetTempPath(), "visual-compare-project");
            const string outputDirectory = "Docs/ComparisonSessions";
            const int maxFullPathLength = 120;
            string[] leafFileNames = { "summary.json", "longer-summary.md" };
            string sessionId = new string('a', 180);

            string result = (string)Invoke(
                "BuildSafeSessionId",
                sessionId,
                "visual_compare",
                projectRoot,
                outputDirectory,
                maxFullPathLength,
                (object)leafFileNames);

            string rootFolder = Path.Combine(projectRoot, outputDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int expectedMaxLength = Math.Max(
                16,
                maxFullPathLength - rootFolder.Length - 2 - "longer-summary.md".Length);
            Assert.That(result.Length, Is.EqualTo(expectedMaxLength));
            Assert.That(result, Does.EndWith("_" + result.Substring(result.Length - 8)));
        }

        [Test]
        public void Given_ExtractedSessionPathPolicy_When_CheckingRunner_Then_PathLengthHelpersAreRemoved()
        {
            BindingFlags privateStatic = BindingFlags.NonPublic | BindingFlags.Static;

            Assert.That(
                typeof(YybVisualComparisonBatchRunner).GetMethod("BuildSafeSummarySessionId", privateStatic),
                Is.Null);
            Assert.That(
                typeof(YybVisualComparisonBatchRunner).GetMethod("ShortenFileNameToLength", privateStatic),
                Is.Null);
        }

        private static object Invoke(string methodName, params object[] arguments)
        {
            Type resolverType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonArtifactPathResolver",
                throwOnError: false);
            Assert.That(resolverType, Is.Not.Null, "모델 중립 비교 산출물 경로 resolver가 필요합니다.");

            MethodInfo method = resolverType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, arguments);
        }
    }
}
