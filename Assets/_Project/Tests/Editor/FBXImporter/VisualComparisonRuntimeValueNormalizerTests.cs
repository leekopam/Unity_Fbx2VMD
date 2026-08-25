using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonRuntimeValueNormalizerTests
    {
        [TestCase(float.NaN, 2f, 2f)]
        [TestCase(float.PositiveInfinity, 2f, 2f)]
        [TestCase(0f, 2f, 2f)]
        [TestCase(-1f, 2f, 2f)]
        [TestCase(1.25f, 2f, 1.25f)]
        public void Given_RuntimeValue_When_NormalizingPositive_Then_UsesFinitePositiveOrFallback(
            float value,
            float fallbackValue,
            float expected)
        {
            Assert.That(Invoke("NormalizePositive", value, fallbackValue), Is.EqualTo(expected));
        }

        [TestCase(float.NaN, 2f, 2f)]
        [TestCase(float.NegativeInfinity, 2f, 2f)]
        [TestCase(-1.25f, 2f, -1.25f)]
        public void Given_RuntimeValue_When_NormalizingFinite_Then_RejectsOnlyNonFiniteValues(
            float value,
            float fallbackValue,
            float expected)
        {
            Assert.That(Invoke("NormalizeFinite", value, fallbackValue), Is.EqualTo(expected));
        }

        private static float Invoke(string methodName, float value, float fallbackValue)
        {
            Type normalizerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonRuntimeValueNormalizer",
                throwOnError: false);
            Assert.That(normalizerType, Is.Not.Null, "모델 중립 런타임 값 정규화 경계가 필요합니다.");

            MethodInfo method = normalizerType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(null, new object[] { value, fallbackValue });
        }
    }
}
