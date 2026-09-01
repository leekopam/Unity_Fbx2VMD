using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.Retargeting;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterGroundingVerticalStepTests
    {
        private const float GroundingDirectionReversalStepScale = 0.4f;

        private static readonly Type[] FootHeightReferenceTargetParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float).MakeByRefType()
        };

        [Test]
        public void Given_FiniteGroundingHeights_When_CalculatingAdjustment_Then_ReturnsTargetMinusContact()
        {
            bool resolved = TryCalculateGroundingAdjustment(
                targetHeight: 0.03f,
                contactBottomY: -0.17f,
                out float adjustment);

            Assert.That(resolved, Is.True);
            Assert.That(adjustment, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void Given_NonFiniteGroundingAdjustment_When_CalculatingAdjustment_Then_ReturnsFalse()
        {
            bool resolved = TryCalculateGroundingAdjustment(
                targetHeight: float.PositiveInfinity,
                contactBottomY: -0.17f,
                out float adjustment);

            Assert.That(resolved, Is.False);
            Assert.That(adjustment, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_InitialGrounding_When_CalculatingVerticalStep_Then_AppliesFullAdjustment()
        {
            float step = CalculateGroundingVerticalStep(
                currentY: 1.0f,
                adjustment: -0.2f,
                wasGroundingInitialized: false,
                smoothGrounding: true,
                groundingSmoothing: 0.25f,
                maxGroundingVerticalStepPerFrame: 0.01f,
                groundingDeadZone: 0.005f,
                previousGroundingVerticalStep: float.NaN,
                out bool skippedByDeadZone,
                out bool smoothed,
                out bool clamped);

            Assert.That(step, Is.EqualTo(-0.2f).Within(0.0001f));
            Assert.That(skippedByDeadZone, Is.False);
            Assert.That(smoothed, Is.False);
            Assert.That(clamped, Is.False);
        }

        [Test]
        public void Given_InitializedAdjustmentInsideDeadZone_When_CalculatingVerticalStep_Then_SkipsStep()
        {
            float step = CalculateGroundingVerticalStep(
                currentY: 1.0f,
                adjustment: 0.003f,
                wasGroundingInitialized: true,
                smoothGrounding: true,
                groundingSmoothing: 0.25f,
                maxGroundingVerticalStepPerFrame: 0.01f,
                groundingDeadZone: 0.005f,
                previousGroundingVerticalStep: 0.01f,
                out bool skippedByDeadZone,
                out bool smoothed,
                out bool clamped);

            Assert.That(step, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(skippedByDeadZone, Is.True);
            Assert.That(smoothed, Is.False);
            Assert.That(clamped, Is.False);
        }

        [Test]
        public void Given_InitializedSmoothCorrection_When_CalculatingVerticalStep_Then_SubtractsDeadZoneAndClamps()
        {
            float step = CalculateGroundingVerticalStep(
                currentY: 1.0f,
                adjustment: 0.08f,
                wasGroundingInitialized: true,
                smoothGrounding: true,
                groundingSmoothing: 0.5f,
                maxGroundingVerticalStepPerFrame: 0.01f,
                groundingDeadZone: 0.005f,
                previousGroundingVerticalStep: 0.01f,
                out bool skippedByDeadZone,
                out bool smoothed,
                out bool clamped);

            Assert.That(step, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(skippedByDeadZone, Is.False);
            Assert.That(smoothed, Is.True);
            Assert.That(clamped, Is.True);
        }

        [Test]
        public void Given_DirectionReversal_When_CalculatingVerticalStep_Then_UsesReducedClampLimit()
        {
            float step = CalculateGroundingVerticalStep(
                currentY: 1.0f,
                adjustment: -0.08f,
                wasGroundingInitialized: true,
                smoothGrounding: true,
                groundingSmoothing: 1f,
                maxGroundingVerticalStepPerFrame: 0.01f,
                groundingDeadZone: 0f,
                previousGroundingVerticalStep: 0.01f,
                out bool skippedByDeadZone,
                out bool smoothed,
                out bool clamped);

            Assert.That(step, Is.EqualTo(-0.004f).Within(0.0001f));
            Assert.That(skippedByDeadZone, Is.False);
            Assert.That(smoothed, Is.False);
            Assert.That(clamped, Is.True);
        }

        [Test]
        public void Given_PreviousStepBelowNoiseThreshold_When_CalculatingVerticalStep_Then_KeepsRegularClampLimit()
        {
            float step = CalculateGroundingVerticalStep(
                currentY: 1.0f,
                adjustment: 0.05f,
                wasGroundingInitialized: true,
                smoothGrounding: true,
                groundingSmoothing: 1f,
                maxGroundingVerticalStepPerFrame: 0.1f,
                groundingDeadZone: 0f,
                previousGroundingVerticalStep: -0.0001f,
                out bool skippedByDeadZone,
                out bool smoothed,
                out bool clamped);

            Assert.That(step, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(skippedByDeadZone, Is.False);
            Assert.That(smoothed, Is.False);
            Assert.That(clamped, Is.False);
        }

        [Test]
        public void Given_GroundingCalculation_When_CheckingOwnership_Then_UsesGroundingStabilizer()
        {
            Assert.That(
                typeof(GroundingStabilizer).GetMethod("CalculateVerticalStep", BindingFlags.Static | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(
                typeof(GroundingStabilizer).GetMethod("TryCalculateAdjustment", BindingFlags.Static | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(
                typeof(GroundingStabilizer).GetMethod("CalculateLateVisualGroundingStep", BindingFlags.Static | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(
                typeof(GroundingStabilizer).GetMethod("TryCalculateLateVisualGroundingEffectiveResidual", BindingFlags.Static | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(
                typeof(GroundingStabilizer).GetMethod("ShouldSkipLateVisualGroundingForActiveVerticalStep", BindingFlags.Static | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(
                typeof(GroundingStabilizer).GetMethod("TryCalculateLateVisualGroundingAppliedPosition", BindingFlags.Static | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(
                typeof(GroundingStabilizer).GetMethod("ResolveGroundingContactBottomY", BindingFlags.Static | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(
                typeof(GroundingStabilizer).GetMethod(
                    "TryCalculateFootHeightReferenceTarget",
                    BindingFlags.Static | BindingFlags.Public,
                    binder: null,
                    types: FootHeightReferenceTargetParameterTypes,
                    modifiers: null),
                Is.Not.Null);

            const BindingFlags RetargeterCalculationFlags =
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember("CalculateGroundingVerticalStep", RetargeterCalculationFlags),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember("TryCalculateGroundingAdjustment", RetargeterCalculationFlags),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember("IsGroundingDirectionReversal", RetargeterCalculationFlags),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember("CalculateLateVisualGroundingStep", RetargeterCalculationFlags),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember("TryCalculateLateVisualGroundingEffectiveResidual", RetargeterCalculationFlags),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember("ShouldSkipLateVisualGroundingForActiveVerticalStep", RetargeterCalculationFlags),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember("TryCalculateLateVisualGroundingAppliedPosition", RetargeterCalculationFlags),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "ResolveGroundingContactBottomY",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "TryCalculateEditorFootHeightGroundingReferenceTarget",
                    RetargeterCalculationFlags),
                Is.Empty);
        }

        [Test]
        public void Given_PrewarmGroundingDiagnostics_When_ResettingPlaybackStabilityMetrics_Then_ResetsCountersWithoutClearingSettledState()
        {
            var gameObject = new GameObject("retargeter-reset-metrics-test");
            try
            {
                var retargeter = gameObject.AddComponent<PoseSpaceRetargeter>();
                SetField(retargeter, "_groundingInitialized", true);
                SetField(retargeter, "_groundingStepClampedCount", 12);
                SetField(retargeter, "_groundingSmoothedCount", 60);
                SetField(retargeter, "_maxGroundingAdjustment", 0.52f);
                SetField(retargeter, "_lastGroundingVerticalStep", 0.028f);
                SetField(retargeter, "_maxGroundingVerticalStep", 0.45f);
                SetField(retargeter, "_maxGroundingVerticalStepAfterInitial", 0.1f);
                SetField(retargeter, "_allowEditorFootHeightGroundingReference", false);

                retargeter.ResetPlaybackStabilityMetrics();

                Assert.That(retargeter.GroundingStepClampedCount, Is.EqualTo(0));
                Assert.That(retargeter.GroundingSmoothedCount, Is.EqualTo(0));
                Assert.That(retargeter.MaxGroundingAdjustment, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(retargeter.MaxGroundingVerticalStep, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(retargeter.MaxGroundingVerticalStepAfterInitial, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(retargeter.LastGroundingVerticalStep, Is.EqualTo(0.028f).Within(0.0001f));
                Assert.That(GetField<bool>(retargeter, "_groundingInitialized"), Is.True);
                Assert.That(GetField<bool>(retargeter, "_allowEditorFootHeightGroundingReference"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Given_ManualReferenceFootLift_When_CalculatingGroundingTarget_Then_AddsPositiveLift()
        {
            bool calculated = TryCalculateFootHeightReferenceTarget(
                baseTargetHeight: 0f,
                referenceCurrentLowestFootY: -0.3259f,
                referenceRestLowestFootY: -0.4245f,
                weight: 0.5f,
                maxLift: 0.08f,
                out float targetHeight);

            Assert.That(calculated, Is.True);
            Assert.That(targetHeight, Is.EqualTo(0.0493f).Within(0.0001f));
        }

        [Test]
        public void Given_ManualReferenceFootDropsBelowRest_When_CalculatingGroundingTarget_Then_DoesNotPushBelowFloor()
        {
            bool calculated = TryCalculateFootHeightReferenceTarget(
                baseTargetHeight: 0f,
                referenceCurrentLowestFootY: -0.4305f,
                referenceRestLowestFootY: -0.4245f,
                weight: 1f,
                maxLift: 0.08f,
                out float targetHeight);

            Assert.That(calculated, Is.True);
            Assert.That(targetHeight, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_ManualReferenceFootLiftExceedsCap_When_CalculatingGroundingTarget_Then_ClampsLift()
        {
            bool calculated = TryCalculateFootHeightReferenceTarget(
                baseTargetHeight: 0.01f,
                referenceCurrentLowestFootY: 0.2f,
                referenceRestLowestFootY: 0f,
                weight: 1f,
                maxLift: 0.06f,
                out float targetHeight);

            Assert.That(calculated, Is.True);
            Assert.That(targetHeight, Is.EqualTo(0.07f).Within(0.0001f));
        }

        [Test]
        public void Given_ManualReferenceFootLiftAndZeroMaxLift_When_CalculatingGroundingTarget_Then_TreatsLiftAsUnlimited()
        {
            bool calculated = TryCalculateFootHeightReferenceTarget(
                baseTargetHeight: 0.01f,
                referenceCurrentLowestFootY: 0.2f,
                referenceRestLowestFootY: 0f,
                weight: 1f,
                maxLift: 0f,
                out float targetHeight);

            Assert.That(calculated, Is.True);
            Assert.That(targetHeight, Is.EqualTo(0.21f).Within(0.0001f));
        }

        [Test]
        public void Given_ManualReferenceFootLiftAndWeightAboveOne_When_CalculatingGroundingTarget_Then_ClampsWeight()
        {
            bool calculated = TryCalculateFootHeightReferenceTarget(
                baseTargetHeight: 0.01f,
                referenceCurrentLowestFootY: 0.05f,
                referenceRestLowestFootY: 0f,
                weight: 2f,
                maxLift: 0.08f,
                out float targetHeight);

            Assert.That(calculated, Is.True);
            Assert.That(targetHeight, Is.EqualTo(0.06f).Within(0.0001f));
        }

        [Test]
        public void Given_NonFiniteManualReferenceFootHeight_When_CalculatingGroundingTarget_Then_ReturnsBaseTarget()
        {
            bool calculated = TryCalculateFootHeightReferenceTarget(
                baseTargetHeight: 0.01f,
                referenceCurrentLowestFootY: float.NaN,
                referenceRestLowestFootY: 0f,
                weight: 1f,
                maxLift: 0.08f,
                out float targetHeight);

            Assert.That(calculated, Is.False);
            Assert.That(targetHeight, Is.EqualTo(0.01f).Within(0.0001f));
        }

        private static float CalculateGroundingVerticalStep(
            float currentY,
            float adjustment,
            bool wasGroundingInitialized,
            bool smoothGrounding,
            float groundingSmoothing,
            float maxGroundingVerticalStepPerFrame,
            float groundingDeadZone,
            float previousGroundingVerticalStep,
            out bool skippedByDeadZone,
            out bool smoothed,
            out bool clamped)
        {
            return GroundingStabilizer.CalculateVerticalStep(
                currentY,
                adjustment,
                wasGroundingInitialized,
                smoothGrounding,
                groundingSmoothing,
                maxGroundingVerticalStepPerFrame,
                groundingDeadZone,
                previousGroundingVerticalStep,
                GroundingDirectionReversalStepScale,
                out skippedByDeadZone,
                out smoothed,
                out clamped);
        }

        private static bool TryCalculateGroundingAdjustment(
            float targetHeight,
            float contactBottomY,
            out float adjustment)
        {
            return GroundingStabilizer.TryCalculateAdjustment(
                targetHeight,
                contactBottomY,
                out adjustment);
        }

        private static bool TryCalculateFootHeightReferenceTarget(
            float baseTargetHeight,
            float referenceCurrentLowestFootY,
            float referenceRestLowestFootY,
            float weight,
            float maxLift,
            out float targetHeight)
        {
            MethodInfo method = typeof(GroundingStabilizer).GetMethod(
                "TryCalculateFootHeightReferenceTarget",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: FootHeightReferenceTargetParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "GroundingStabilizer should own pure foot-height reference target calculation.");

            object[] args =
            {
                baseTargetHeight,
                referenceCurrentLowestFootY,
                referenceRestLowestFootY,
                weight,
                maxLift,
                baseTargetHeight
            };

            bool calculated = (bool)method.Invoke(null, args);
            targetHeight = (float)args[5];
            return calculated;
        }

        private static void SetField<T>(PoseSpaceRetargeter retargeter, string fieldName, T value)
        {
            FieldInfo field = typeof(PoseSpaceRetargeter).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");
            field.SetValue(retargeter, value);
        }

        private static T GetField<T>(PoseSpaceRetargeter retargeter, string fieldName)
        {
            FieldInfo field = typeof(PoseSpaceRetargeter).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");
            return (T)field.GetValue(retargeter);
        }
    }
}
