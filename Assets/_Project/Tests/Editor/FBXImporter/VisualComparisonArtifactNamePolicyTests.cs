using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonArtifactNamePolicyTests
    {
        [Test]
        public void Given_BlankOrSpacedName_When_Sanitizing_Then_UsesCallerFallbackAndUnderscores()
        {
            Assert.That(Invoke("SanitizeFileName", string.Empty, "visual compare"), Is.EqualTo("visual_compare"));
            Assert.That(Invoke("SanitizeFileName", " model session ", "fallback"), Is.EqualTo("model_session"));
        }

        [Test]
        public void Given_LongName_When_Shortening_Then_ReturnsStableBoundedName()
        {
            string value = new string('a', 80);

            string first = (string)Invoke("ShortenToLength", value, 24);
            string second = (string)Invoke("ShortenToLength", value, 24);

            Assert.That(first, Has.Length.EqualTo(24));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Contain("_"));
        }

        [Test]
        public void Given_EvidenceRole_When_BuildingFileName_Then_PreservesPrefixAndExtension()
        {
            string fileName = (string)Invoke(
                "BuildEvidenceFileName",
                "vmd",
                "auto result",
                ".vmd",
                ".vmd",
                "candidate");

            Assert.That(fileName, Is.EqualTo("vmd-auto_result.vmd"));
        }

        private static object Invoke(string methodName, params object[] arguments)
        {
            Type policyType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonArtifactNamePolicy",
                throwOnError: false);
            Assert.That(policyType, Is.Not.Null, "모델 중립 비교 산출물 이름 정책 경계가 필요합니다.");

            MethodInfo method = policyType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, arguments);
        }
    }
}
