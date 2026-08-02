using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterLateVisualGroundingStepTests
    {
        private static readonly Type[] LateVisualGroundingStepParameterTypes =
        {
            typeof(float),
            typeof(bool),
            typeof(bool),
            typeof(float),
            typeof(float),
            typeof(float)
        };

        private static readonly Type[] LateVisualGroundingEffectiveResidualParameterTypes =
        {
            typeof(float),
            typeof(bool),
            typeof(float),
            typeof(float),
            typeof(float).MakeByRefType(),
            typeof(bool).MakeByRefType()
        };

        private static readonly Type[] LateVisualGroundingAppliedPositionParameterTypes =
        {
            typeof(Vector3),
            typeof(float),
            typeof(Vector3).MakeByRefType()
        };

        private static readonly Type[] LateVisualGroundingActiveStepSkipParameterTypes =
        {
            typeof(float),
            typeof(bool),
            typeof(float)
        };

        [Test]
        public void Given_SmoothingDisabled_When_CalculatingLateVisualGroundingStep_Then_ReturnsResidual()
        {
            float step = CalculateLateVisualGroundingStep(
                residual: 0.2f,
                smoothLateVisualGroundingCorrection: false,
                lateVisualGroundingInitialized: true,
                lateVisualGroundingSnapThreshold: 0.03f,
                lateVisualGroundingSmoothing: 0.25f,
                maxLateVisualGroundingStepPerFrame: 0.003f);

            Assert.That(step, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void Given_SmoothingEnabledBeforeInitialization_When_CalculatingLateVisualGroundingStep_Then_ReturnsResidual()
        {
            float step = CalculateLateVisualGroundingStep(
                residual: -0.2f,
                smoothLateVisualGroundingCorrection: true,
                lateVisualGroundingInitialized: false,
                lateVisualGroundingSnapThreshold: 0.03f,
                lateVisualGroundingSmoothing: 0.25f,
                maxLateVisualGroundingStepPerFrame: 0.003f);

            Assert.That(step, Is.EqualTo(-0.2f).Within(0.0001f));
        }

        [Test]
        public void Given_InitializedLargeHoverResidualWithLowSmoothing_When_CalculatingLateVisualGroundingStep_Then_UsesMinimumSnapSmoothing()
        {
            float step = CalculateLateVisualGroundingStep(
                residual: -0.2f,
                smoothLateVisualGroundingCorrection: true,
                lateVisualGroundingInitialized: true,
                lateVisualGroundingSnapThreshold: 0.03f,
                lateVisualGroundingSmoothing: 0.05f,
                maxLateVisualGroundingStepPerFrame: 1f);

            Assert.That(step, Is.EqualTo(-0.02f).Within(0.0001f));
        }

        [Test]
        public void Given_InitializedSmoothedStepExceedsMax_When_CalculatingLateVisualGroundingStep_Then_ClampsBySign()
        {
            float step = CalculateLateVisualGroundingStep(
                residual: -0.2f,
                smoothLateVisualGroundingCorrection: true,
                lateVisualGroundingInitialized: true,
                lateVisualGroundingSnapThreshold: 0.03f,
                lateVisualGroundingSmoothing: 0.25f,
                maxLateVisualGroundingStepPerFrame: 0.003f);

            Assert.That(step, Is.EqualTo(-0.003f).Within(0.0001f));
        }

        [Test]
        public void Given_ResidualInsideDeadZone_When_CalculatingLateVisualGroundingEffectiveResidual_Then_SkipsWithoutMaxWarning()
        {
            bool shouldApply = TryCalculateLateVisualGroundingEffectiveResidual(
                residual: 0.0005f,
                smoothLateVisualGroundingCorrection: true,
                groundingDeadZone: 0.005f,
                maxLateVisualGroundingCorrection: 0.2f,
                out float effectiveResidual,
                out bool exceededMaxCorrection);

            Assert.That(shouldApply, Is.False);
            Assert.That(effectiveResidual, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(exceededMaxCorrection, Is.False);
        }

        [Test]
        public void Given_ResidualAboveMaxCorrection_When_CalculatingLateVisualGroundingEffectiveResidual_Then_SkipsAndReportsMaxExceeded()
        {
            bool shouldApply = TryCalculateLateVisualGroundingEffectiveResidual(
                residual: -0.25f,
                smoothLateVisualGroundingCorrection: true,
                groundingDeadZone: 0.005f,
                maxLateVisualGroundingCorrection: 0.2f,
                out float effectiveResidual,
                out bool exceededMaxCorrection);

            Assert.That(shouldApply, Is.False);
            Assert.That(effectiveResidual, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(exceededMaxCorrection, Is.True);
        }

        [Test]
        public void Given_PenetrationResidualBeyondConfiguredDeadZone_When_CalculatingLateVisualGroundingEffectiveResidual_Then_DoesNotSubtractDeadZone()
        {
            bool shouldApply = TryCalculateLateVisualGroundingEffectiveResidual(
                residual: 0.010f,
                smoothLateVisualGroundingCorrection: true,
                groundingDeadZone: 0.005f,
                maxLateVisualGroundingCorrection: 0.2f,
                out float effectiveResidual,
                out bool exceededMaxCorrection);

            Assert.That(shouldApply, Is.True);
            Assert.That(effectiveResidual, Is.EqualTo(0.010f).Within(0.0001f), "Floor penetration is a hard visual constraint, so the whole upward correction should be available.");
            Assert.That(exceededMaxCorrection, Is.False);
        }

        [Test]
        public void Given_PenetrationResidualInsideConfiguredDeadZone_When_CalculatingLateVisualGroundingEffectiveResidual_Then_StillAppliesFloorCorrection()
        {
            bool shouldApply = TryCalculateLateVisualGroundingEffectiveResidual(
                residual: 0.003f,
                smoothLateVisualGroundingCorrection: true,
                groundingDeadZone: 0.005f,
                maxLateVisualGroundingCorrection: 0.2f,
                out float effectiveResidual,
                out bool exceededMaxCorrection);

            Assert.That(shouldApply, Is.True, "Visible mesh floor penetration must not be left below the floor by the smoothing dead zone.");
            Assert.That(effectiveResidual, Is.EqualTo(0.003f).Within(0.0001f));
            Assert.That(exceededMaxCorrection, Is.False);
        }

        [Test]
        public void Given_FloatingResidualInsideConfiguredDeadZone_When_CalculatingLateVisualGroundingEffectiveResidual_Then_StillAppliesHoverCorrection()
        {
            bool shouldApply = TryCalculateLateVisualGroundingEffectiveResidual(
                residual: -0.003f,
                smoothLateVisualGroundingCorrection: true,
                groundingDeadZone: 0.005f,
                maxLateVisualGroundingCorrection: 0.2f,
                out float effectiveResidual,
                out bool exceededMaxCorrection);

            Assert.That(shouldApply, Is.True, "Positive mesh-ground gap is visible hover, so the penetration/noise dead zone should not leave it uncorrected.");
            Assert.That(effectiveResidual, Is.EqualTo(-0.003f).Within(0.0001f));
            Assert.That(exceededMaxCorrection, Is.False);
        }

        [Test]
        public void Given_InitializedPenetrationResidual_When_CalculatingLateVisualGroundingStep_Then_AppliesFullFloorCorrection()
        {
            float step = CalculateLateVisualGroundingStep(
                residual: 0.010f,
                smoothLateVisualGroundingCorrection: true,
                lateVisualGroundingInitialized: true,
                lateVisualGroundingSnapThreshold: 0.03f,
                lateVisualGroundingSmoothing: 0.25f,
                maxLateVisualGroundingStepPerFrame: 0.003f);

            Assert.That(step, Is.EqualTo(0.010f).Within(0.0001f), "Mesh penetration must be resolved in the current frame instead of leaking below-floor frames into the VMD capture.");
        }

        [Test]
        public void Given_InitializedLargePenetrationResidual_When_CalculatingLateVisualGroundingStep_Then_UsesFloorRecoveryMinimum()
        {
            float step = CalculateLateVisualGroundingStep(
                residual: 0.185f,
                smoothLateVisualGroundingCorrection: true,
                lateVisualGroundingInitialized: true,
                lateVisualGroundingSnapThreshold: 0.03f,
                lateVisualGroundingSmoothing: 0.25f,
                maxLateVisualGroundingStepPerFrame: 0.05f);

            Assert.That(step, Is.EqualTo(0.1f).Within(0.0001f), "Large floor penetration must recover enough in one frame to clear the reference t60 below-floor sample without using full smoothing for hover corrections.");
        }

        [Test]
        public void Given_FinitePositionAndResidual_When_CalculatingLateVisualGroundingAppliedPosition_Then_AddsResidualToY()
        {
            bool shouldApply = TryCalculateLateVisualGroundingAppliedPosition(
                new Vector3(1f, 2f, 3f),
                appliedResidual: -0.25f,
                out Vector3 appliedPosition);

            Assert.That(shouldApply, Is.True);
            Assert.That(appliedPosition.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(appliedPosition.y, Is.EqualTo(1.75f).Within(0.0001f));
            Assert.That(appliedPosition.z, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void Given_NonFinitePosition_When_CalculatingLateVisualGroundingAppliedPosition_Then_ReturnsFalse()
        {
            bool shouldApply = TryCalculateLateVisualGroundingAppliedPosition(
                new Vector3(float.NaN, 2f, 3f),
                appliedResidual: 0.1f,
                out Vector3 appliedPosition);

            Assert.That(shouldApply, Is.False);
            Assert.That(appliedPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Given_NonFiniteResidual_When_CalculatingLateVisualGroundingAppliedPosition_Then_ReturnsFalse()
        {
            bool shouldApply = TryCalculateLateVisualGroundingAppliedPosition(
                new Vector3(1f, 2f, 3f),
                appliedResidual: float.PositiveInfinity,
                out Vector3 appliedPosition);

            Assert.That(shouldApply, Is.False);
            Assert.That(appliedPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Given_ActiveGroundingStepSameDirection_When_CheckingLateVisualGroundingSkip_Then_DoesNotSkip()
        {
            bool shouldSkip = ShouldSkipLateVisualGroundingForActiveVerticalStep(
                residual: -0.02f,
                smoothLateVisualGroundingCorrection: true,
                lastGroundingVerticalStep: -0.002f);

            Assert.That(shouldSkip, Is.False, "Floating correction should not be skipped when the active grounding step is already moving in the same direction.");
        }

        [Test]
        public void Given_ActiveGroundingStepOppositeDirection_When_CheckingLateVisualGroundingSkip_Then_Skips()
        {
            bool shouldSkip = ShouldSkipLateVisualGroundingForActiveVerticalStep(
                residual: -0.02f,
                smoothLateVisualGroundingCorrection: true,
                lastGroundingVerticalStep: 0.002f);

            Assert.That(shouldSkip, Is.True, "Late visual grounding should still avoid fighting an active opposite-direction grounding step.");
        }

        [Test]
        public void Given_SmoothingDisabled_When_CheckingLateVisualGroundingSkip_Then_DoesNotSkip()
        {
            bool shouldSkip = ShouldSkipLateVisualGroundingForActiveVerticalStep(
                residual: -0.02f,
                smoothLateVisualGroundingCorrection: false,
                lastGroundingVerticalStep: 0.002f);

            Assert.That(shouldSkip, Is.False);
        }

        private static float CalculateLateVisualGroundingStep(
            float residual,
            bool smoothLateVisualGroundingCorrection,
            bool lateVisualGroundingInitialized,
            float lateVisualGroundingSnapThreshold,
            float lateVisualGroundingSmoothing,
            float maxLateVisualGroundingStepPerFrame)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "CalculateLateVisualGroundingStep",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: LateVisualGroundingStepParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for late visual grounding step calculation.");

            return (float)method.Invoke(
                null,
                new object[]
                {
                    residual,
                    smoothLateVisualGroundingCorrection,
                    lateVisualGroundingInitialized,
                    lateVisualGroundingSnapThreshold,
                    lateVisualGroundingSmoothing,
                    maxLateVisualGroundingStepPerFrame
                });
        }

        private static bool TryCalculateLateVisualGroundingEffectiveResidual(
            float residual,
            bool smoothLateVisualGroundingCorrection,
            float groundingDeadZone,
            float maxLateVisualGroundingCorrection,
            out float effectiveResidual,
            out bool exceededMaxCorrection)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateLateVisualGroundingEffectiveResidual",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: LateVisualGroundingEffectiveResidualParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for late visual grounding residual filtering.");

            object[] args =
            {
                residual,
                smoothLateVisualGroundingCorrection,
                groundingDeadZone,
                maxLateVisualGroundingCorrection,
                0f,
                false
            };

            bool shouldApply = (bool)method.Invoke(null, args);
            effectiveResidual = (float)args[4];
            exceededMaxCorrection = (bool)args[5];
            return shouldApply;
        }

        private static bool TryCalculateLateVisualGroundingAppliedPosition(
            Vector3 currentPosition,
            float appliedResidual,
            out Vector3 appliedPosition)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateLateVisualGroundingAppliedPosition",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: LateVisualGroundingAppliedPositionParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for late visual grounding position application.");

            object[] args =
            {
                currentPosition,
                appliedResidual,
                Vector3.zero
            };

            bool shouldApply = (bool)method.Invoke(null, args);
            appliedPosition = (Vector3)args[2];
            return shouldApply;
        }

        private static bool ShouldSkipLateVisualGroundingForActiveVerticalStep(
            float residual,
            bool smoothLateVisualGroundingCorrection,
            float lastGroundingVerticalStep)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "ShouldSkipLateVisualGroundingForActiveVerticalStep",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: LateVisualGroundingActiveStepSkipParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for late visual grounding active-step skip decisions.");

            return (bool)method.Invoke(
                null,
                new object[]
                {
                    residual,
                    smoothLateVisualGroundingCorrection,
                    lastGroundingVerticalStep
                });
        }
    }
}
