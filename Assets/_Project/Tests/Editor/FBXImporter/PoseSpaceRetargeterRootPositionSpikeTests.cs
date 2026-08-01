using Fbx2Vmd.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterRootPositionSpikeTests
    {
        private static readonly Type[] RootPositionSpikeClampParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(float),
            typeof(Vector3).MakeByRefType(),
            typeof(float).MakeByRefType()
        };

        private static readonly Type[] HipsLocalPositionSpikeClampParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(float),
            typeof(Vector3).MakeByRefType(),
            typeof(float).MakeByRefType()
        };

        private static readonly Type[] ImplicitBodyPositionRootGuardParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(bool)
        };

        private static readonly Type[] ExplicitBodyRootMotionGuardParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(bool),
            typeof(Vector3)
        };

        private static readonly Type[] ImplicitRootGuardReferenceParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(float)
        };

        private static readonly Type[] PoseSolveRootPositionParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(bool)
        };

        private static readonly Type[] RootMotionCarrierRestoreParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(bool)
        };

        [Test]
        public void Given_RootDeltaWithinLimit_When_CalculatingClamp_Then_KeepsCurrentPosition()
        {
            bool clamped = TryCalculateRootPositionSpikeClamp(
                positionBeforePose: Vector3.zero,
                currentPosition: new Vector3(0.03f, 0f, 0.04f),
                maxRootDeltaPerFrame: 0.1f,
                out Vector3 clampedPosition,
                out float deltaMagnitude);

            Assert.That(clamped, Is.False);
            Assert.That(deltaMagnitude, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(clampedPosition.x, Is.EqualTo(0.03f).Within(0.0001f));
            Assert.That(clampedPosition.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(clampedPosition.z, Is.EqualTo(0.04f).Within(0.0001f));
        }

        [Test]
        public void Given_RootDeltaExceedsLimit_When_CalculatingClamp_Then_ClampsFromPositionBeforePose()
        {
            bool clamped = TryCalculateRootPositionSpikeClamp(
                positionBeforePose: new Vector3(1f, 2f, 3f),
                currentPosition: new Vector3(1.3f, 2f, 3.4f),
                maxRootDeltaPerFrame: 0.25f,
                out Vector3 clampedPosition,
                out float deltaMagnitude);

            Assert.That(clamped, Is.True);
            Assert.That(deltaMagnitude, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(clampedPosition.x, Is.EqualTo(1.15f).Within(0.0001f));
            Assert.That(clampedPosition.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(clampedPosition.z, Is.EqualTo(3.2f).Within(0.0001f));
        }

        [Test]
        public void Given_NonFiniteRootDelta_When_CalculatingClamp_Then_ReportsNaNAndDoesNotClamp()
        {
            bool clamped = TryCalculateRootPositionSpikeClamp(
                positionBeforePose: Vector3.zero,
                currentPosition: new Vector3(float.NaN, 0f, 0f),
                maxRootDeltaPerFrame: 0.25f,
                out Vector3 clampedPosition,
                out float deltaMagnitude);

            Assert.That(clamped, Is.False);
            Assert.That(deltaMagnitude, Is.NaN);
            Assert.That(float.IsNaN(clampedPosition.x), Is.True);
        }

        [Test]
        public void Given_HipsLocalDeltaWithinLimit_When_CalculatingClamp_Then_KeepsCurrentPosition()
        {
            bool clamped = TryCalculateHipsLocalPositionSpikeClamp(
                previousLocalPosition: new Vector3(0f, 1f, 0f),
                currentLocalPosition: new Vector3(0.02f, 1.01f, -0.01f),
                maxDeltaPerFrame: 0.08f,
                out Vector3 clampedPosition,
                out float deltaMagnitude);

            Assert.That(clamped, Is.False);
            Assert.That(deltaMagnitude, Is.EqualTo(0.0244949f).Within(0.0001f));
            Assert.That(clampedPosition.x, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(clampedPosition.y, Is.EqualTo(1.01f).Within(0.0001f));
            Assert.That(clampedPosition.z, Is.EqualTo(-0.01f).Within(0.0001f));
        }

        [Test]
        public void Given_HipsLocalDeltaSpike_When_CalculatingClamp_Then_ClampsFromPreviousPosition()
        {
            bool clamped = TryCalculateHipsLocalPositionSpikeClamp(
                previousLocalPosition: new Vector3(-0.005f, 1.026f, -0.023f),
                currentLocalPosition: new Vector3(-0.355f, 0.986f, 0.185f),
                maxDeltaPerFrame: 0.08f,
                out Vector3 clampedPosition,
                out float deltaMagnitude);

            Assert.That(clamped, Is.True);
            Assert.That(deltaMagnitude, Is.EqualTo(0.40912f).Within(0.0001f));
            Assert.That(Vector3.Distance(new Vector3(-0.005f, 1.026f, -0.023f), clampedPosition), Is.EqualTo(0.08f).Within(0.0001f));
        }

        [Test]
        public void Given_BodyPositionRootMotionDisabled_When_ApplyingImplicitRootGuard_Then_RestoresRootXZAndKeepsPoseY()
        {
            Vector3 guarded = ApplyImplicitBodyPositionRootGuard(
                positionBeforePose: new Vector3(1f, 2f, 3f),
                currentPosition: new Vector3(4f, 2.5f, -2f),
                allowBodyPositionXZRootMotion: false);

            Assert.That(guarded.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(guarded.y, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(guarded.z, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void Given_BodyPositionRootMotionEnabled_When_ApplyingImplicitRootGuard_Then_KeepsPoseRootPosition()
        {
            Vector3 currentPosition = new Vector3(4f, 2.5f, -2f);

            Vector3 guarded = ApplyImplicitBodyPositionRootGuard(
                positionBeforePose: new Vector3(1f, 2f, 3f),
                currentPosition: currentPosition,
                allowBodyPositionXZRootMotion: true);

            Assert.That(guarded.x, Is.EqualTo(currentPosition.x).Within(0.0001f));
            Assert.That(guarded.y, Is.EqualTo(currentPosition.y).Within(0.0001f));
            Assert.That(guarded.z, Is.EqualTo(currentPosition.z).Within(0.0001f));
        }

        [Test]
        public void Given_ExplicitBodyRootDelta_When_ApplyingImplicitRootGuard_Then_RestoresPoseRootXZBeforeExplicitMotion()
        {
            Vector3 guarded = ApplyImplicitBodyPositionRootGuard(
                positionBeforePose: new Vector3(1f, 2f, 3f),
                currentPosition: new Vector3(4f, 2.5f, -2f),
                allowBodyPositionXZRootMotion: true,
                explicitBodyRootDelta: new Vector3(0.01f, 0f, -0.02f));

            Assert.That(guarded.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(guarded.y, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(guarded.z, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void Given_StationaryMovementScale_When_SelectingImplicitRootGuardReference_Then_UsesSessionAnchor()
        {
            Vector3 reference = SelectImplicitRootGuardReference(
                rootAnchorPosition: new Vector3(1f, 2f, 3f),
                positionBeforePose: new Vector3(4f, 2f, -2f),
                movementScaleMultiplier: 0f);

            Assert.That(reference.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(reference.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(reference.z, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void Given_ExplicitMovementScale_When_SelectingImplicitRootGuardReference_Then_UsesFramePosition()
        {
            Vector3 reference = SelectImplicitRootGuardReference(
                rootAnchorPosition: new Vector3(1f, 2f, 3f),
                positionBeforePose: new Vector3(4f, 2f, -2f),
                movementScaleMultiplier: 0.5f);

            Assert.That(reference.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(reference.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(reference.z, Is.EqualTo(-2f).Within(0.0001f));
        }

        [Test]
        public void Given_MovingRootEnabled_When_SelectingPoseSolveRootPosition_Then_UsesAnchorXZAndKeepsCurrentY()
        {
            Vector3 poseSolvePosition = SelectPoseSolveRootPosition(
                currentRootPosition: new Vector3(2f, 0.35f, -4f),
                rootAnchorPosition: new Vector3(0.1f, -0.2f, 0.3f),
                isolateRootMotionFromPoseSolve: true);

            Assert.That(poseSolvePosition.x, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(poseSolvePosition.y, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(poseSolvePosition.z, Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void Given_MovingRootDisabled_When_SelectingPoseSolveRootPosition_Then_KeepsCurrentPosition()
        {
            Vector3 currentPosition = new Vector3(2f, 0.35f, -4f);

            Vector3 poseSolvePosition = SelectPoseSolveRootPosition(
                currentRootPosition: currentPosition,
                rootAnchorPosition: new Vector3(0.1f, -0.2f, 0.3f),
                isolateRootMotionFromPoseSolve: false);

            Assert.That(poseSolvePosition.x, Is.EqualTo(currentPosition.x).Within(0.0001f));
            Assert.That(poseSolvePosition.y, Is.EqualTo(currentPosition.y).Within(0.0001f));
            Assert.That(poseSolvePosition.z, Is.EqualTo(currentPosition.z).Within(0.0001f));
        }

        [Test]
        public void Given_MovingRootEnabled_When_RestoringCarrierAfterPose_Then_UsesCarrierXZAndPoseY()
        {
            Vector3 restoredPosition = RestoreRootMotionCarrierPositionAfterPose(
                rootMotionCarrierPositionBeforePose: new Vector3(2f, 0.35f, -4f),
                poseSolvedPosition: new Vector3(0.1f, 0.5f, 0.3f),
                isolateRootMotionFromPoseSolve: true);

            Assert.That(restoredPosition.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(restoredPosition.y, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(restoredPosition.z, Is.EqualTo(-4f).Within(0.0001f));
        }

        [Test]
        public void Given_MovingRootDisabled_When_RestoringCarrierAfterPose_Then_KeepsPoseSolvedPosition()
        {
            Vector3 poseSolvedPosition = new Vector3(0.1f, 0.5f, 0.3f);

            Vector3 restoredPosition = RestoreRootMotionCarrierPositionAfterPose(
                rootMotionCarrierPositionBeforePose: new Vector3(2f, 0.35f, -4f),
                poseSolvedPosition: poseSolvedPosition,
                isolateRootMotionFromPoseSolve: false);

            Assert.That(restoredPosition.x, Is.EqualTo(poseSolvedPosition.x).Within(0.0001f));
            Assert.That(restoredPosition.y, Is.EqualTo(poseSolvedPosition.y).Within(0.0001f));
            Assert.That(restoredPosition.z, Is.EqualTo(poseSolvedPosition.z).Within(0.0001f));
        }

        private static bool TryCalculateRootPositionSpikeClamp(
            Vector3 positionBeforePose,
            Vector3 currentPosition,
            float maxRootDeltaPerFrame,
            out Vector3 clampedPosition,
            out float deltaMagnitude)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateRootPositionSpikeClamp",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RootPositionSpikeClampParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for root position spike clamp calculation.");

            object[] args =
            {
                positionBeforePose,
                currentPosition,
                maxRootDeltaPerFrame,
                currentPosition,
                float.NaN
            };

            bool clamped = (bool)method.Invoke(null, args);
            clampedPosition = (Vector3)args[3];
            deltaMagnitude = (float)args[4];
            return clamped;
        }

        private static bool TryCalculateHipsLocalPositionSpikeClamp(
            Vector3 previousLocalPosition,
            Vector3 currentLocalPosition,
            float maxDeltaPerFrame,
            out Vector3 clampedPosition,
            out float deltaMagnitude)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateHipsLocalPositionSpikeClamp",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: HipsLocalPositionSpikeClampParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for target Hips localPosition spike clamp calculation.");

            object[] args =
            {
                previousLocalPosition,
                currentLocalPosition,
                maxDeltaPerFrame,
                currentLocalPosition,
                float.NaN
            };

            bool clamped = (bool)method.Invoke(null, args);
            clampedPosition = (Vector3)args[3];
            deltaMagnitude = (float)args[4];
            return clamped;
        }

        private static Vector3 ApplyImplicitBodyPositionRootGuard(
            Vector3 positionBeforePose,
            Vector3 currentPosition,
            bool allowBodyPositionXZRootMotion)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "ApplyImplicitBodyPositionRootGuard",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ImplicitBodyPositionRootGuardParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for implicit bodyPosition root guard calculation.");

            object[] args =
            {
                positionBeforePose,
                currentPosition,
                allowBodyPositionXZRootMotion
            };

            return (Vector3)method.Invoke(null, args);
        }

        private static Vector3 SelectPoseSolveRootPosition(
            Vector3 currentRootPosition,
            Vector3 rootAnchorPosition,
            bool isolateRootMotionFromPoseSolve)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "SelectPoseSolveRootPosition",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: PoseSolveRootPositionParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should isolate the moving root carrier from SetHumanPose root X/Z solve.");

            object[] args =
            {
                currentRootPosition,
                rootAnchorPosition,
                isolateRootMotionFromPoseSolve
            };

            return (Vector3)method.Invoke(null, args);
        }

        private static Vector3 RestoreRootMotionCarrierPositionAfterPose(
            Vector3 rootMotionCarrierPositionBeforePose,
            Vector3 poseSolvedPosition,
            bool isolateRootMotionFromPoseSolve)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "RestoreRootMotionCarrierPositionAfterPose",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RootMotionCarrierRestoreParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should restore moving-root carrier X/Z after SetHumanPose before applying explicit root delta.");

            object[] args =
            {
                rootMotionCarrierPositionBeforePose,
                poseSolvedPosition,
                isolateRootMotionFromPoseSolve
            };

            return (Vector3)method.Invoke(null, args);
        }

        private static Vector3 SelectImplicitRootGuardReference(
            Vector3 rootAnchorPosition,
            Vector3 positionBeforePose,
            float movementScaleMultiplier)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "SelectImplicitRootGuardReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ImplicitRootGuardReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for implicit root guard reference selection.");

            object[] args =
            {
                rootAnchorPosition,
                positionBeforePose,
                movementScaleMultiplier
            };

            return (Vector3)method.Invoke(null, args);
        }

        private static Vector3 ApplyImplicitBodyPositionRootGuard(
            Vector3 positionBeforePose,
            Vector3 currentPosition,
            bool allowBodyPositionXZRootMotion,
            Vector3 explicitBodyRootDelta)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "ApplyImplicitBodyPositionRootGuard",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ExplicitBodyRootMotionGuardParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose an explicit-body-root overload so SetHumanPose root X/Z and explicit body root motion are not applied in the same step.");

            object[] args =
            {
                positionBeforePose,
                currentPosition,
                allowBodyPositionXZRootMotion,
                explicitBodyRootDelta
            };

            return (Vector3)method.Invoke(null, args);
        }

    }
}
