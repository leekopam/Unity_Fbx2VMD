using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonFrameGeometryCalculatorTests
    {
        [TestCase(0.2f, 0.5f, 0.3f)]
        [TestCase(0.7f, 0.5f, 0f)]
        public void Given_FrameBounds_When_CalculatingTopGap_Then_ClampsToFrame(
            float bottomGapRatio,
            float boundingBoxHeightRatio,
            float expected)
        {
            float result = InvokeFloat(
                "ResolveFrameTopGapRatio",
                bottomGapRatio,
                boundingBoxHeightRatio);

            Assert.That(result, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void Given_NonFiniteFrameBounds_When_CalculatingTopGap_Then_ReturnsNaN()
        {
            float result = InvokeFloat("ResolveFrameTopGapRatio", float.NaN, 0.5f);

            Assert.That(result, Is.NaN);
        }

        [TestCase(0.001f, 0.5f, true)]
        [TestCase(0.5f, 0.001f, true)]
        [TestCase(0.0011f, 0.0011f, false)]
        [TestCase(float.NaN, float.NaN, false)]
        public void Given_FrameGaps_When_CheckingEdgeTouch_Then_UsesPixelTolerance(
            float bottomGapRatio,
            float topGapRatio,
            bool expected)
        {
            bool result = InvokeBool("IsFrameEdgeTouched", bottomGapRatio, topGapRatio);

            Assert.That(result, Is.EqualTo(expected));
        }

        private static float InvokeFloat(string methodName, params object[] arguments)
        {
            return (float)FindMethod(methodName).Invoke(null, arguments);
        }

        private static bool InvokeBool(string methodName, params object[] arguments)
        {
            return (bool)FindMethod(methodName).Invoke(null, arguments);
        }

        private static MethodInfo FindMethod(string methodName)
        {
            Type calculatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonFrameGeometryCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null, "모델 중립 프레임 기하 계산 경계가 필요합니다.");

            MethodInfo method = calculatorType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }
    }
}
