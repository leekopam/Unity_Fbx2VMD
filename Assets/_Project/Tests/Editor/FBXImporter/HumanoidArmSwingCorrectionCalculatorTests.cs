using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidArmSwingCorrectionCalculatorTests
    {
        private const float AngleToleranceDegrees = 0.01f;

        [Test]
        public void Given_MatchingDirections_When_Calculating_Then_ReturnsIdentity()
        {
            object[] arguments =
            {
                Vector3.right,
                Vector3.right,
                Quaternion.identity,
                0f
            };

            Assert.That(InvokeTryCalculate(arguments), Is.True);
            Assert.That(Quaternion.Angle(
                    Quaternion.identity,
                    (Quaternion)arguments[2]),
                Is.LessThanOrEqualTo(AngleToleranceDegrees));
            Assert.That((float)arguments[3],
                Is.LessThanOrEqualTo(AngleToleranceDegrees));
        }

        [Test]
        public void Given_ValidDirections_When_Calculating_Then_AlignsWithShortestSwing()
        {
            Vector3 currentDirection = Vector3.right;
            Vector3 referenceDirection =
                Quaternion.AngleAxis(10f, Vector3.up) * currentDirection;
            object[] arguments =
            {
                currentDirection,
                referenceDirection,
                Quaternion.identity,
                0f
            };

            Assert.That(InvokeTryCalculate(arguments), Is.True);
            var correction = (Quaternion)arguments[2];

            Assert.That(
                Vector3.Angle(correction * currentDirection, referenceDirection),
                Is.LessThanOrEqualTo(AngleToleranceDegrees));
            Assert.That((float)arguments[3],
                Is.EqualTo(10f).Within(AngleToleranceDegrees));

            Vector3 rotationVector = new Vector3(
                correction.x,
                correction.y,
                correction.z);
            Assert.That(
                Mathf.Abs(Vector3.Dot(rotationVector, currentDirection.normalized)),
                Is.LessThanOrEqualTo(0.0001f),
                "최단 Swing 보정은 현재 팔 길이축의 Twist 성분을 추가하면 안 됩니다.");
        }

        [Test]
        public void Given_LargeDirectionError_When_Calculating_Then_LimitsSingleFrameCorrection()
        {
            object[] arguments =
            {
                Vector3.right,
                Vector3.up,
                Quaternion.identity,
                0f
            };

            Assert.That(InvokeTryCalculate(arguments), Is.True);
            var correction = (Quaternion)arguments[2];

            Assert.That(Quaternion.Angle(Quaternion.identity, correction),
                Is.EqualTo(15f).Within(AngleToleranceDegrees));
            Assert.That((float)arguments[3],
                Is.EqualTo(90f).Within(AngleToleranceDegrees));
        }

        [TestCase(float.NaN, 0f, 0f)]
        [TestCase(0f, 0f, 0f)]
        public void Given_InvalidDirection_When_Calculating_Then_RejectsWithoutRotation(
            float x,
            float y,
            float z)
        {
            object[] arguments =
            {
                new Vector3(x, y, z),
                Vector3.right,
                Quaternion.identity,
                0f
            };

            Assert.That(InvokeTryCalculate(arguments), Is.False);
            Assert.That(Quaternion.Angle(
                    Quaternion.identity,
                    (Quaternion)arguments[2]),
                Is.LessThanOrEqualTo(AngleToleranceDegrees));
            Assert.That((float)arguments[3], Is.Zero);
        }

        private static bool InvokeTryCalculate(object[] arguments)
        {
            Type calculatorType =
                typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.HumanoidArmSwingCorrectionCalculator",
                    throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null,
                "팔 방향을 Twist 없이 정렬하는 순수 Swing 계산기가 필요합니다.");

            MethodInfo method = calculatorType.GetMethod(
                "TryCalculate",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "TryCalculate 메서드가 필요합니다.");
            return (bool)method.Invoke(null, arguments);
        }
    }
}
