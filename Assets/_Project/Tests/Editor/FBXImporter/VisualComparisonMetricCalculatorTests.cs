using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonMetricCalculatorTests
    {
        [Test]
        public void Given_ReportedGroundingRatio_When_Resolving_Then_PrefersReportedValue()
        {
            Assert.That(InvokeFloat("ResolveGroundingStepToMaxRatio", 0.75f, 0.2f, 0.1f), Is.EqualTo(0.75f));
        }

        [Test]
        public void Given_MissingReportedRatio_When_Resolving_Then_CalculatesAbsoluteStepRatio()
        {
            Assert.That(InvokeFloat("ResolveGroundingStepToMaxRatio", float.NaN, -0.15f, 0.2f), Is.EqualTo(0.75f));
        }

        [TestCase(0.95f, true)]
        [TestCase(0.949f, false)]
        [TestCase(float.NaN, false)]
        public void Given_GroundingRatio_When_CheckingMaximum_Then_AppliesExistingThreshold(float ratio, bool expected)
        {
            Assert.That(InvokeBool("IsGroundingStepAtMax", ratio), Is.EqualTo(expected));
        }

        [TestCase(10, 15, 5)]
        [TestCase(-1, 15, -1)]
        public void Given_IntegerMetrics_When_CalculatingSpan_Then_PreservesInvalidSentinel(
            int first,
            int finish,
            int expected)
        {
            Assert.That(InvokeInt("CalculateIntSpan", first, finish), Is.EqualTo(expected));
        }

        [Test]
        public void Given_FloatMetrics_When_CalculatingSpan_Then_ReturnsDifference()
        {
            Assert.That(InvokeFloat("CalculateFloatSpan", 1.25f, 2f), Is.EqualTo(0.75f));
        }

        [Test]
        public void Given_NonFiniteFloatMetric_When_CalculatingSpan_Then_ReturnsNaN()
        {
            Assert.That(InvokeFloat("CalculateFloatSpan", float.NaN, 2f), Is.NaN);
        }

        private static float InvokeFloat(string methodName, params object[] arguments)
        {
            return (float)FindMethod(methodName).Invoke(null, arguments);
        }

        private static int InvokeInt(string methodName, params object[] arguments)
        {
            return (int)FindMethod(methodName).Invoke(null, arguments);
        }

        private static bool InvokeBool(string methodName, params object[] arguments)
        {
            return (bool)FindMethod(methodName).Invoke(null, arguments);
        }

        private static MethodInfo FindMethod(string methodName)
        {
            Type calculatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonMetricCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null, "모델 중립 비교 metric 계산 경계가 필요합니다.");

            MethodInfo method = calculatorType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }
    }
}
