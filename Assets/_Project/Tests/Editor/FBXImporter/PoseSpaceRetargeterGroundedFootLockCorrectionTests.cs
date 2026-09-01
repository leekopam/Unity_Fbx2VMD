using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.Retargeting;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterGroundedFootLockCorrectionTests
    {
        [Test]
        public void Given_NoFootCorrections_When_CalculatingRootCorrection_Then_ReturnsFalse()
        {
            bool shouldApply = GroundingStabilizer.TryCalculateGroundedFootLockRootCorrection(
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
            bool shouldApply = GroundingStabilizer.TryCalculateGroundedFootLockRootCorrection(
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
            bool shouldApply = GroundingStabilizer.TryCalculateGroundedFootLockRootCorrection(
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
            bool shouldApply = GroundingStabilizer.TryCalculateGroundedFootLockRootCorrection(
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
            bool shouldApply = GroundingStabilizer.TryCalculateGroundedFootLockRootCorrection(
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
            bool shouldAccumulate = GroundingStabilizer.TryCalculateFootLockCorrection(
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
        public void Given_UnlockedFootAtContactHeight_When_CalculatingFootLockCorrection_Then_StartsLock()
        {
            bool shouldAccumulate = GroundingStabilizer.TryCalculateFootLockCorrection(
                bottomY: 0.08f,
                footPosition: new Vector3(1f, 2f, -3f),
                targetHeight: 0f,
                locked: false,
                lockPosition: Vector3.zero,
                out bool nextLocked,
                out Vector3 nextLockPosition,
                out Vector3 correction);

            Assert.That(shouldAccumulate, Is.False);
            Assert.That(nextLocked, Is.True);
            Assert.That(nextLockPosition, Is.EqualTo(new Vector3(1f, 0f, -3f)));
            Assert.That(correction, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Given_LockedFootStillGrounded_When_CalculatingFootLockCorrection_Then_ReturnsPlanarCorrection()
        {
            bool shouldAccumulate = GroundingStabilizer.TryCalculateFootLockCorrection(
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
            bool shouldAccumulate = GroundingStabilizer.TryCalculateFootLockCorrection(
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
        public void Given_LockedFootAtReleaseHeight_When_CalculatingFootLockCorrection_Then_RefreshesLockPosition()
        {
            bool shouldAccumulate = GroundingStabilizer.TryCalculateFootLockCorrection(
                bottomY: 0.14f,
                footPosition: new Vector3(1f, 0f, 2f),
                targetHeight: 0f,
                locked: true,
                lockPosition: new Vector3(9f, 0f, 9f),
                out bool nextLocked,
                out Vector3 nextLockPosition,
                out Vector3 correction);

            Assert.That(shouldAccumulate, Is.False);
            Assert.That(nextLocked, Is.False);
            Assert.That(nextLockPosition, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(correction, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Given_LockedFootCorrectionExceedsResetDistance_When_CalculatingFootLockCorrection_Then_ResetsLockAndAccumulatesZero()
        {
            bool shouldAccumulate = GroundingStabilizer.TryCalculateFootLockCorrection(
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

        [Test]
        public void Given_LockedFootCorrectionAtResetDistance_When_CalculatingFootLockCorrection_Then_KeepsCorrection()
        {
            bool shouldAccumulate = GroundingStabilizer.TryCalculateFootLockCorrection(
                bottomY: 0.02f,
                footPosition: new Vector3(0.25f, 0f, 0f),
                targetHeight: 0f,
                locked: true,
                lockPosition: Vector3.zero,
                out bool nextLocked,
                out Vector3 nextLockPosition,
                out Vector3 correction);

            Assert.That(shouldAccumulate, Is.True);
            Assert.That(nextLocked, Is.True);
            Assert.That(nextLockPosition, Is.EqualTo(Vector3.zero));
            Assert.That(correction, Is.EqualTo(new Vector3(-0.25f, 0f, 0f)));
        }

        [Test]
        public void Given_NonFiniteFootBottom_When_CalculatingFootLockCorrection_Then_UnlocksWithoutChangingLockPosition()
        {
            var lockPosition = new Vector3(1f, 0f, 2f);

            bool shouldAccumulate = GroundingStabilizer.TryCalculateFootLockCorrection(
                bottomY: float.NaN,
                footPosition: new Vector3(3f, 0f, 4f),
                targetHeight: 0f,
                locked: true,
                lockPosition: lockPosition,
                out bool nextLocked,
                out Vector3 nextLockPosition,
                out Vector3 correction);

            Assert.That(shouldAccumulate, Is.False);
            Assert.That(nextLocked, Is.False);
            Assert.That(nextLockPosition, Is.EqualTo(lockPosition));
            Assert.That(correction, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Given_FootGroundingCalculations_When_CheckingOwnership_Then_PoseSpaceRetargeterDoesNotOwnThem()
        {
            string[] methodNames =
            {
                "TryCalculateEstimatedFootRadius",
                "TryCalculateFootBottomY",
                "TryCalculateLowestFootBottomY",
                "TryCalculateGroundedFootLockRootCorrection",
                "TryCalculateFootLockCorrection"
            };

            foreach (string methodName in methodNames)
            {
                Assert.That(
                    typeof(PoseSpaceRetargeter).GetMember(
                        methodName,
                        BindingFlags.Static | BindingFlags.NonPublic),
                    Is.Empty,
                    methodName);
            }
        }
    }
}
