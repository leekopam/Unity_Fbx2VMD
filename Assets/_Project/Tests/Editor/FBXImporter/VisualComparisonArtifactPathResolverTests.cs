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
