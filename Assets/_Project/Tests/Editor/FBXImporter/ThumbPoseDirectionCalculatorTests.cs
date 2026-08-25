using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class ThumbPoseDirectionCalculatorTests
    {
        [Test]
        public void Given_SpreadExceedsLimit_When_CalculatingCorrection_Then_RotatesTowardIndex()
        {
            bool corrected = TryCalculateCorrectedDirection(
                Vector3.right,
                Vector3.up,
                hasIndexDirection: true,
                minimumPalmNormal: -1f,
                maximumPalmNormal: 1f,
                maximumSpreadAngle: 45f,
                indexSpreadWeight: 1f,
                projectionWeight: 0f,
                out Vector3 correctedDirection);

            Assert.That(corrected, Is.True);
            Assert.That(Vector3.Angle(correctedDirection, Vector3.up), Is.LessThanOrEqualTo(45.001f));
        }

        [Test]
        public void Given_ProjectionOutsideRange_When_CalculatingCorrection_Then_ClampsPalmNormalComponent()
        {
            Vector3 sourceDirection = new Vector3(1f, -0.5f, 0f).normalized;

            bool corrected = TryCalculateCorrectedDirection(
                sourceDirection,
                Vector3.zero,
                hasIndexDirection: false,
                minimumPalmNormal: 0f,
                maximumPalmNormal: 1f,
                maximumSpreadAngle: 90f,
                indexSpreadWeight: 0f,
                projectionWeight: 1f,
                out Vector3 correctedDirection);

            Assert.That(corrected, Is.True);
            Assert.That(Vector3.Dot(correctedDirection, Vector3.up), Is.GreaterThanOrEqualTo(-0.001f));
        }

        [Test]
        public void Given_DirectionWithinLimits_When_CalculatingCorrection_Then_ReturnsFalse()
        {
            Vector3 sourceDirection = new Vector3(1f, 0.5f, 0f).normalized;

            bool corrected = TryCalculateCorrectedDirection(
                sourceDirection,
                Vector3.right,
                hasIndexDirection: true,
                minimumPalmNormal: 0f,
                maximumPalmNormal: 1f,
                maximumSpreadAngle: 45f,
                indexSpreadWeight: 1f,
                projectionWeight: 1f,
                out Vector3 correctedDirection);

            Assert.That(corrected, Is.False);
            Assert.That(Vector3.Angle(correctedDirection, sourceDirection), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_AllCorrectionWeightsDisabled_When_CalculatingCorrection_Then_ReturnsFalse()
        {
            bool corrected = TryCalculateCorrectedDirection(
                Vector3.down,
                Vector3.up,
                hasIndexDirection: true,
                minimumPalmNormal: 0f,
                maximumPalmNormal: 1f,
                maximumSpreadAngle: 10f,
                indexSpreadWeight: 0f,
                projectionWeight: 0f,
                out Vector3 correctedDirection);

            Assert.That(corrected, Is.False);
            Assert.That(correctedDirection, Is.EqualTo(Vector3.down));
        }

        [Test]
        public void Given_SegmentBendExceedsLimit_When_CalculatingStraightenedDirection_Then_RotatesTowardProximal()
        {
            bool corrected = TryCalculateStraightenedDirection(
                Vector3.right,
                Vector3.up,
                maximumBendAngle: 45f,
                straightenWeight: 1f,
                out Vector3 correctedDirection);

            Assert.That(corrected, Is.True);
            Assert.That(Vector3.Angle(correctedDirection, Vector3.right), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_SegmentBendWithinLimit_When_CalculatingStraightenedDirection_Then_ReturnsFalse()
        {
            bool corrected = TryCalculateStraightenedDirection(
                Vector3.right,
                Vector3.up,
                maximumBendAngle: 90f,
                straightenWeight: 1f,
                out Vector3 correctedDirection);

            Assert.That(corrected, Is.False);
            Assert.That(correctedDirection, Is.EqualTo(Vector3.up));
        }

        [Test]
        public void Given_StraightenWeightDisabled_When_CalculatingStraightenedDirection_Then_ReturnsFalse()
        {
            bool corrected = TryCalculateStraightenedDirection(
                Vector3.right,
                Vector3.up,
                maximumBendAngle: 45f,
                straightenWeight: 0f,
                out Vector3 correctedDirection);

            Assert.That(corrected, Is.False);
            Assert.That(correctedDirection, Is.EqualTo(Vector3.up));
        }

        private static bool TryCalculateStraightenedDirection(
            Vector3 proximalDirection,
            Vector3 intermediateDirection,
            float maximumBendAngle,
            float straightenWeight,
            out Vector3 correctedDirection)
        {
            Type calculatorType = typeof(HumanoidThumbDeformationGuard).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ThumbPoseDirectionCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null);

            MethodInfo method = calculatorType.GetMethod(
                "TryCalculateStraightenedDirection",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] args =
            {
                proximalDirection,
                intermediateDirection,
                maximumBendAngle,
                straightenWeight,
                Vector3.zero
            };
            bool corrected = (bool)method.Invoke(null, args);
            correctedDirection = (Vector3)args[4];
            return corrected;
        }

        private static bool TryCalculateCorrectedDirection(
            Vector3 sourceDirection,
            Vector3 indexDirection,
            bool hasIndexDirection,
            float minimumPalmNormal,
            float maximumPalmNormal,
            float maximumSpreadAngle,
            float indexSpreadWeight,
            float projectionWeight,
            out Vector3 correctedDirection)
        {
            Type calculatorType = typeof(HumanoidThumbDeformationGuard).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ThumbPoseDirectionCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null);

            MethodInfo method = calculatorType.GetMethod(
                "TryCalculateCorrectedDirection",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] args =
            {
                sourceDirection,
                indexDirection,
                hasIndexDirection,
                Vector3.right,
                Vector3.up,
                Vector3.forward,
                minimumPalmNormal,
                maximumPalmNormal,
                maximumSpreadAngle,
                indexSpreadWeight,
                projectionWeight,
                Vector3.zero
            };
            bool corrected = (bool)method.Invoke(null, args);
            correctedDirection = (Vector3)args[11];
            return corrected;
        }
    }
}
