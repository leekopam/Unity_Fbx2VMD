using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonScreenshotOverridePolicyTests
    {
        [TestCase(1920, -1, 1920)]
        [TestCase(0, -1, -1)]
        public void Given_CaptureDimension_When_Normalizing_Then_UsesPositiveValueOrSentinel(
            int value,
            int noOverrideValue,
            int expected)
        {
            Assert.That(Invoke("NormalizeCaptureDimension", value, noOverrideValue), Is.EqualTo(expected));
        }

        [TestCase(0.1f, float.NaN, 0.25f)]
        [TestCase(3f, float.NaN, 2f)]
        [TestCase(1.25f, float.NaN, 1.25f)]
        public void Given_Padding_When_Normalizing_Then_ClampsExistingRange(
            float value,
            float noOverrideValue,
            float expected)
        {
            Assert.That(Invoke("NormalizePadding", value, noOverrideValue), Is.EqualTo(expected));
        }

        [TestCase(-0.5f, float.NaN, 0f)]
        [TestCase(1.5f, float.NaN, 1f)]
        [TestCase(0.4f, float.NaN, 0.4f)]
        public void Given_ViewportCenter_When_Normalizing_Then_ClampsToUnitRange(
            float value,
            float noOverrideValue,
            float expected)
        {
            Assert.That(Invoke("NormalizeVerticalViewportCenter", value, noOverrideValue), Is.EqualTo(expected));
        }

        [Test]
        public void Given_NonFiniteFramingValue_When_CheckingOverride_Then_ReturnsFalse()
        {
            Assert.That(Invoke("HasFiniteFramingOverride", float.NaN), Is.False);
            Assert.That(Invoke("HasFiniteFramingOverride", 0.5f), Is.True);
        }

        private static object Invoke(string methodName, params object[] arguments)
        {
            Type policyType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonScreenshotOverridePolicy",
                throwOnError: false);
            Assert.That(policyType, Is.Not.Null, "모델 중립 screenshot override 정책 경계가 필요합니다.");

            MethodInfo method = policyType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, arguments);
        }
    }
}
