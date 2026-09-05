using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidArmQualityMetricCalculatorTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void Given_MatchingDirections_When_CalculatingError_Then_ReturnsZero()
        {
            object[] arguments = { Vector3.right, Vector3.right, 0f };

            Assert.That(InvokeTry("TryCalculateDirectionErrorDegrees", arguments), Is.True);
            Assert.That((float)arguments[2], Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Given_InvalidDirection_When_CalculatingError_Then_RejectsInput()
        {
            object[] arguments =
            {
                new Vector3(float.NaN, 0f, 0f),
                Vector3.right,
                0f
            };

            Assert.That(InvokeTry("TryCalculateDirectionErrorDegrees", arguments), Is.False);
            Assert.That((float)arguments[2], Is.Zero);
        }

        [Test]
        public void Given_AxialRotation_When_CalculatingTwist_Then_ReturnsAxialAngle()
        {
            object[] arguments =
            {
                Quaternion.AngleAxis(35f, Vector3.right),
                Vector3.right,
                0f
            };

            Assert.That(InvokeTry("TryCalculateTwistAngleDegrees", arguments), Is.True);
            Assert.That((float)arguments[2], Is.EqualTo(35f).Within(Tolerance));
        }

        [Test]
        public void Given_SwingRotation_When_CalculatingTwist_Then_ReturnsZero()
        {
            object[] arguments =
            {
                Quaternion.AngleAxis(40f, Vector3.up),
                Vector3.right,
                0f
            };

            Assert.That(InvokeTry("TryCalculateTwistAngleDegrees", arguments), Is.True);
            Assert.That((float)arguments[2], Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Given_ParallelSegments_When_CalculatingDistance_Then_ReturnsGap()
        {
            object[] arguments =
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                Vector3.up + Vector3.right,
                0f
            };

            Assert.That(InvokeTry("TryCalculateSegmentDistance", arguments), Is.True);
            Assert.That((float)arguments[4], Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Given_IntersectingSegments_When_CalculatingDistance_Then_ReturnsZero()
        {
            object[] arguments =
            {
                Vector3.left,
                Vector3.right,
                Vector3.down,
                Vector3.up,
                0f
            };

            Assert.That(InvokeTry("TryCalculateSegmentDistance", arguments), Is.True);
            Assert.That((float)arguments[4], Is.EqualTo(0f).Within(Tolerance));
        }

        private static bool InvokeTry(string methodName, object[] arguments)
        {
            Type calculatorType =
                typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.HumanoidArmQualityMetricCalculator",
                    throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null,
                "팔 품질 지표를 계산하는 모델 중립 순수 계산기가 필요합니다.");

            MethodInfo method = calculatorType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return (bool)method.Invoke(null, arguments);
        }
    }
}
