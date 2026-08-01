using Fbx2Vmd.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterGroundingVerticalStepTests
    {
        private static readonly Type[] GroundingVerticalStepParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(bool),
            typeof(bool),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(bool).MakeByRefType(),
            typeof(bool).MakeByRefType(),
            typeof(bool).MakeByRefType()
        };

        private static readonly Type[] GroundingAdjustmentParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float).MakeByRefType()
        };

        private static readonly Type[] EditorFootHeightGroundingReferenceParameterTypes =
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
            bool calculated = TryCalculateEditorFootHeightGroundingReferenceTarget(
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
            bool calculated = TryCalculateEditorFootHeightGroundingReferenceTarget(
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
            bool calculated = TryCalculateEditorFootHeightGroundingReferenceTarget(
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
            bool calculated = TryCalculateEditorFootHeightGroundingReferenceTarget(
                baseTargetHeight: 0.01f,
                referenceCurrentLowestFootY: 0.2f,
                referenceRestLowestFootY: 0f,
                weight: 1f,
                maxLift: 0f,
                out float targetHeight);

            Assert.That(calculated, Is.True);
            Assert.That(targetHeight, Is.EqualTo(0.21f).Within(0.0001f));
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
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "CalculateGroundingVerticalStep",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: GroundingVerticalStepParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for raycast grounding vertical step calculation.");

            object[] args =
            {
                currentY,
                adjustment,
                wasGroundingInitialized,
                smoothGrounding,
                groundingSmoothing,
                maxGroundingVerticalStepPerFrame,
                groundingDeadZone,
                previousGroundingVerticalStep,
                false,
                false,
                false
            };

            float step = (float)method.Invoke(null, args);
            skippedByDeadZone = (bool)args[8];
            smoothed = (bool)args[9];
            clamped = (bool)args[10];
            return step;
        }

        private static bool TryCalculateGroundingAdjustment(
            float targetHeight,
            float contactBottomY,
            out float adjustment)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateGroundingAdjustment",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: GroundingAdjustmentParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for grounding adjustment calculation.");

            object[] args =
            {
                targetHeight,
                contactBottomY,
                0f
            };

            bool resolved = (bool)method.Invoke(null, args);
            adjustment = (float)args[2];
            return resolved;
        }

        private static bool TryCalculateEditorFootHeightGroundingReferenceTarget(
            float baseTargetHeight,
            float referenceCurrentLowestFootY,
            float referenceRestLowestFootY,
            float weight,
            float maxLift,
            out float targetHeight)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateEditorFootHeightGroundingReferenceTarget",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorFootHeightGroundingReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for Manual Animator foot-height grounding reference calculation.");

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
