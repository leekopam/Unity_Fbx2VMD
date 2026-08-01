using Fbx2Vmd.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterGroundedFootLockCorrectionTests
    {
        private static readonly Type[] GroundedFootLockCorrectionParameterTypes =
        {
            typeof(Vector3),
            typeof(int),
            typeof(float),
            typeof(float),
            typeof(Vector3).MakeByRefType()
        };

        private static readonly Type[] FootLockCorrectionParameterTypes =
        {
            typeof(float),
            typeof(Vector3),
            typeof(float),
            typeof(bool),
            typeof(Vector3),
            typeof(bool).MakeByRefType(),
            typeof(Vector3).MakeByRefType(),
            typeof(Vector3).MakeByRefType()
        };

        [Test]
        public void Given_NoFootCorrections_When_CalculatingRootCorrection_Then_ReturnsFalse()
        {
            bool shouldApply = TryCalculateGroundedFootLockRootCorrection(
                correctionSum: new Vector3(1f, 2f, 3f),
                correctionCount: 0,
                groundedFootLockWeight: 0.5f,
                maxGroundedFootLockStep: 0.1f,
                out Vector3 correction);

            Assert.That(shouldApply, Is.False);
            Assert.That(correction, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Given_WeightedAverageWithinMaxStep_When_CalculatingRootCorrection_Then_DropsYAndAppliesWeight()
        {
            bool shouldApply = TryCalculateGroundedFootLockRootCorrection(
                correctionSum: new Vector3(0.08f, 0.2f, -0.04f),
                correctionCount: 2,
                groundedFootLockWeight: 0.5f,
                maxGroundedFootLockStep: 0.1f,
                out Vector3 correction);

            Assert.That(shouldApply, Is.True);
            Assert.That(correction.x, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(correction.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(correction.z, Is.EqualTo(-0.01f).Within(0.0001f));
        }

        [Test]
        public void Given_CorrectionExceedsMaxStep_When_CalculatingRootCorrection_Then_ClampsMagnitude()
        {
            bool shouldApply = TryCalculateGroundedFootLockRootCorrection(
                correctionSum: new Vector3(0.3f, 0f, 0.4f),
                correctionCount: 1,
                groundedFootLockWeight: 1f,
                maxGroundedFootLockStep: 0.25f,
                out Vector3 correction);

            Assert.That(shouldApply, Is.True);
            Assert.That(correction.x, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(correction.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(correction.z, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(correction.magnitude, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void Given_TinyCorrection_When_CalculatingRootCorrection_Then_ReturnsFalse()
        {
            bool shouldApply = TryCalculateGroundedFootLockRootCorrection(
                correctionSum: new Vector3(0.000001f, 0f, 0f),
                correctionCount: 1,
                groundedFootLockWeight: 1f,
                maxGroundedFootLockStep: 0.1f,
                out Vector3 correction);

            Assert.That(shouldApply, Is.False);
            Assert.That(correction.x, Is.EqualTo(0.000001f).Within(0.0000001f));
        }

        [Test]
        public void Given_NonFiniteCorrection_When_CalculatingRootCorrection_Then_ReturnsFalse()
        {
            bool shouldApply = TryCalculateGroundedFootLockRootCorrection(
                correctionSum: new Vector3(float.NaN, 0f, 0f),
                correctionCount: 1,
                groundedFootLockWeight: 1f,
                maxGroundedFootLockStep: 0.1f,
                out Vector3 correction);

            Assert.That(shouldApply, Is.False);
            Assert.That(float.IsNaN(correction.x), Is.True);
        }

        [Test]
        public void Given_UnlockedFootInsideContact_When_CalculatingFootLockCorrection_Then_StartsLockWithoutCorrection()
        {
            bool shouldAccumulate = TryCalculateFootLockCorrection(
                bottomY: 0.04f,
                footPosition: new Vector3(1f, 2f, -3f),
                targetHeight: 0f,
                locked: false,
                lockPosition: Vector3.zero,
                out bool nextLocked,
                out Vector3 nextLockPosition,
                out Vector3 correction);

            Assert.That(shouldAccumulate, Is.False);
            Assert.That(nextLocked, Is.True);
            Assert.That(nextLockPosition.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(nextLockPosition.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(nextLockPosition.z, Is.EqualTo(-3f).Within(0.0001f));
            Assert.That(correction, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Given_LockedFootStillGrounded_When_CalculatingFootLockCorrection_Then_ReturnsPlanarCorrection()
        {
            bool shouldAccumulate = TryCalculateFootLockCorrection(
                bottomY: 0.02f,
                footPosition: new Vector3(0.95f, 4f, 1.94f),
                targetHeight: 0f,
                locked: true,
                lockPosition: new Vector3(1f, 0f, 2f),
                out bool nextLocked,
                out Vector3 nextLockPosition,
                out Vector3 correction);

            Assert.That(shouldAccumulate, Is.True);
            Assert.That(nextLocked, Is.True);
            Assert.That(nextLockPosition.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(nextLockPosition.z, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(correction.x, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(correction.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(correction.z, Is.EqualTo(0.06f).Within(0.0001f));
        }

        [Test]
        public void Given_LockedFootAboveReleaseHeight_When_CalculatingFootLockCorrection_Then_UnlocksWithoutCorrection()
        {
            bool shouldAccumulate = TryCalculateFootLockCorrection(
                bottomY: 0.15f,
                footPosition: new Vector3(1f, 0f, 2f),
                targetHeight: 0f,
                locked: true,
                lockPosition: new Vector3(1f, 0f, 2f),
                out bool nextLocked,
                out Vector3 nextLockPosition,
                out Vector3 correction);

            Assert.That(shouldAccumulate, Is.False);
            Assert.That(nextLocked, Is.False);
            Assert.That(nextLockPosition.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(nextLockPosition.z, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(correction, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Given_LockedFootCorrectionExceedsResetDistance_When_CalculatingFootLockCorrection_Then_ResetsLockAndAccumulatesZero()
        {
            bool shouldAccumulate = TryCalculateFootLockCorrection(
                bottomY: 0.02f,
                footPosition: new Vector3(1f, 0f, 0f),
                targetHeight: 0f,
                locked: true,
                lockPosition: Vector3.zero,
                out bool nextLocked,
                out Vector3 nextLockPosition,
                out Vector3 correction);

            Assert.That(shouldAccumulate, Is.True);
            Assert.That(nextLocked, Is.True);
            Assert.That(nextLockPosition.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(nextLockPosition.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(nextLockPosition.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(correction, Is.EqualTo(Vector3.zero));
        }

        private static bool TryCalculateGroundedFootLockRootCorrection(
            Vector3 correctionSum,
            int correctionCount,
            float groundedFootLockWeight,
            float maxGroundedFootLockStep,
            out Vector3 correction)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateGroundedFootLockRootCorrection",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: GroundedFootLockCorrectionParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for grounded foot-lock root correction calculation.");

            object[] args =
            {
                correctionSum,
                correctionCount,
                groundedFootLockWeight,
                maxGroundedFootLockStep,
                Vector3.zero
            };

            bool shouldApply = (bool)method.Invoke(null, args);
            correction = (Vector3)args[4];
            return shouldApply;
        }

        private static bool TryCalculateFootLockCorrection(
            float bottomY,
            Vector3 footPosition,
            float targetHeight,
            bool locked,
            Vector3 lockPosition,
            out bool nextLocked,
            out Vector3 nextLockPosition,
            out Vector3 correction)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateFootLockCorrection",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: FootLockCorrectionParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for per-foot lock correction calculation.");

            object[] args =
            {
                bottomY,
                footPosition,
                targetHeight,
                locked,
                lockPosition,
                false,
                Vector3.zero,
                Vector3.zero
            };

            bool shouldAccumulate = (bool)method.Invoke(null, args);
            nextLocked = (bool)args[5];
            nextLockPosition = (Vector3)args[6];
            correction = (Vector3)args[7];
            return shouldAccumulate;
        }
    }
}
