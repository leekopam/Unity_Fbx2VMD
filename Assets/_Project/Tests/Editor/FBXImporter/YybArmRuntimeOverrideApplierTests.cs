using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybArmRuntimeOverrideApplierTests
    {
        [Test]
        public void Given_SwingLimitOverride_When_Toggled_Then_OnlyChangesSwingLimitSettings()
        {
            var pipelineObject = new GameObject("arm swing limit override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.ShouldUseManualAnimatorBodyRotationReference = false;
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.ShouldUseManualAnimatorHipsLocalPositionReference = false;

                bool enabledApplied = ApplySwingLimit(pipeline, true);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.enableYybArmSwingLimitCorrection, Is.True);
                Assert.That(pipeline.YybArmSwingLimitWeight, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingMaxDownDot, Is.EqualTo(0.42f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingMinHandHorizontalRatio, Is.EqualTo(0.07f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingMaxHandBelowShoulderRatio, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseManualAnimatorBodyRotationReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False);

                bool disabledApplied = ApplySwingLimit(pipeline, false);

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.enableYybArmSwingLimitCorrection, Is.False);
                Assert.That(pipeline.YybArmSwingLimitWeight, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_SwingLimitOverride_When_HorizontalReachProvided_Then_ClampsReachSettings()
        {
            var pipelineObject = new GameObject("arm swing horizontal reach override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplySwingLimit(
                    pipeline,
                    true,
                    horizontalReachLimitWeight: 1.5f,
                    maxHandHorizontalReachRatio: -0.2f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingHorizontalReachLimitWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingMaxHandHorizontalReachRatio, Is.EqualTo(0f).Within(0.0001f));

                bool disabledApplied = ApplySwingLimit(
                    pipeline,
                    false,
                    horizontalReachLimitWeight: 0.75f,
                    maxHandHorizontalReachRatio: 0.55f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingHorizontalReachLimitWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingMaxHandHorizontalReachRatio, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_SwingLimitOverride_When_HorizontalReachGateProvided_Then_ClampsGateSeparately()
        {
            var pipelineObject = new GameObject("arm swing horizontal reach gate override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplySwingLimit(
                    pipeline,
                    true,
                    maxHandBelowShoulderRatio: 1.8f,
                    horizontalReachMaxHandBelowShoulderRatio: 0.95f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingMaxHandBelowShoulderRatio, Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingHorizontalReachMaxHandBelowShoulderRatio, Is.EqualTo(0.95f).Within(0.0001f));

                bool disabledApplied = ApplySwingLimit(
                    pipeline,
                    false,
                    maxHandBelowShoulderRatio: 1.8f,
                    horizontalReachMaxHandBelowShoulderRatio: 0.95f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingHorizontalReachMaxHandBelowShoulderRatio, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_SwingLimitOverride_When_ElbowGuardProvided_Then_ClampsAngle()
        {
            var pipelineObject = new GameObject("arm swing elbow guard override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplySwingLimit(
                    pipeline,
                    true,
                    horizontalReachMinElbowAngleAfterApply: 200f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(
                    pipeline.YybArmSwingHorizontalReachMinElbowAngleAfterApply,
                    Is.EqualTo(180f).Within(0.0001f));

                bool disabledApplied = ApplySwingLimit(
                    pipeline,
                    false,
                    horizontalReachMinElbowAngleAfterApply: 12f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(
                    pipeline.YybArmSwingHorizontalReachMinElbowAngleAfterApply,
                    Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_SwingLimitOverride_When_RaisedPoseSettingsProvided_Then_ClampsSettings()
        {
            var pipelineObject = new GameObject("arm swing raised pose override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplySwingLimit(
                    pipeline,
                    true,
                    raisedPoseHorizontalReachLimitWeight: 1.5f,
                    raisedPoseMinUpperArmDownDot: -0.25f,
                    raisedPoseMaxHandBelowShoulderRatio: 1.75f,
                    raisedPoseMaxHandHorizontalReachRatio: 1.8f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingRaisedPoseHorizontalReachLimitWeight, Is.EqualTo(1f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMinUpperArmDownDot, Is.EqualTo(0f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio, Is.EqualTo(1.5f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio, Is.EqualTo(1.5f));

                bool disabledApplied = ApplySwingLimit(
                    pipeline,
                    false,
                    raisedPoseHorizontalReachLimitWeight: 0.75f,
                    raisedPoseMinUpperArmDownDot: 0.25f,
                    raisedPoseMaxHandBelowShoulderRatio: 0.8f,
                    raisedPoseMaxHandHorizontalReachRatio: 1f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingRaisedPoseHorizontalReachLimitWeight, Is.EqualTo(0f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMinUpperArmDownDot, Is.EqualTo(0.25f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio, Is.EqualTo(0.8f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_SleeveAnchorOverride_When_Toggled_Then_ClampsOnlySleeveAnchorSettings()
        {
            var pipelineObject = new GameObject("arm sleeve anchor override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.enableYybArmDirectionRetargetCorrection = false;
                pipeline.enableYybArmSwingLimitCorrection = false;
                pipeline.ShouldUseManualAnimatorBodyRotationReference = false;
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.ShouldUseManualAnimatorHipsLocalPositionReference = false;

                bool enabledApplied = ApplySleeveAnchor(
                    pipeline,
                    true,
                    sleeveInfluence: 0.45f,
                    shoulderCapInfluence: 0.2f,
                    maxDegrees: 42f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.enableYybArmSleeveAnchorCorrection, Is.True);
                Assert.That(pipeline.YybArmSleeveAnchorInfluence, Is.EqualTo(0.45f).Within(0.0001f));
                Assert.That(pipeline.YybArmShoulderCapAnchorInfluence, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(pipeline.YybArmSleeveAnchorMaxDegrees, Is.EqualTo(42f).Within(0.0001f));
                Assert.That(pipeline.enableYybArmDirectionRetargetCorrection, Is.False);
                Assert.That(pipeline.enableYybArmSwingLimitCorrection, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorBodyRotationReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False);

                bool clampedApplied = ApplySleeveAnchor(
                    pipeline,
                    true,
                    sleeveInfluence: 1.5f,
                    shoulderCapInfluence: -0.5f,
                    maxDegrees: 150f);

                Assert.That(clampedApplied, Is.True);
                Assert.That(pipeline.YybArmSleeveAnchorInfluence, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(pipeline.YybArmShoulderCapAnchorInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmSleeveAnchorMaxDegrees, Is.EqualTo(120f).Within(0.0001f));

                bool disabledApplied = ApplySleeveAnchor(
                    pipeline,
                    false,
                    sleeveInfluence: 0.45f,
                    shoulderCapInfluence: 0.2f,
                    maxDegrees: 42f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.enableYybArmSleeveAnchorCorrection, Is.False);
                Assert.That(pipeline.YybArmSleeveAnchorInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmShoulderCapAnchorInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmSleeveAnchorMaxDegrees, Is.EqualTo(42f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_VisualTwistOverride_When_Toggled_Then_ClampsOnlyVisualTwistSettings()
        {
            var pipelineObject = new GameObject("arm visual twist override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.enableYybArmDirectionRetargetCorrection = false;
                pipeline.enableYybArmSwingLimitCorrection = false;
                pipeline.ShouldUseManualAnimatorBodyRotationReference = false;
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.ShouldUseManualAnimatorHipsLocalPositionReference = false;

                bool enabledApplied = ApplyVisualTwist(
                    pipeline,
                    true,
                    upperArmInfluence: 0.25f,
                    forearmInfluence: 0.6f,
                    upperArmMaxDegrees: 30f,
                    forearmMaxDegrees: 50f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.enableYybArmVisualTwistCorrection, Is.True);
                Assert.That(pipeline.YybArmVisualUpperArmInfluence, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(pipeline.YybArmVisualForearmInfluence, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(pipeline.YybArmVisualUpperArmMaxDegrees, Is.EqualTo(30f).Within(0.0001f));
                Assert.That(pipeline.YybArmVisualForearmMaxDegrees, Is.EqualTo(50f).Within(0.0001f));
                Assert.That(pipeline.enableYybArmDirectionRetargetCorrection, Is.False);
                Assert.That(pipeline.enableYybArmSwingLimitCorrection, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorBodyRotationReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False);

                bool clampedApplied = ApplyVisualTwist(
                    pipeline,
                    true,
                    upperArmInfluence: 1.5f,
                    forearmInfluence: -0.5f,
                    upperArmMaxDegrees: 150f,
                    forearmMaxDegrees: -8f);

                Assert.That(clampedApplied, Is.True);
                Assert.That(pipeline.YybArmVisualUpperArmInfluence, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(pipeline.YybArmVisualForearmInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmVisualUpperArmMaxDegrees, Is.EqualTo(120f).Within(0.0001f));
                Assert.That(pipeline.YybArmVisualForearmMaxDegrees, Is.EqualTo(0f).Within(0.0001f));

                bool disabledApplied = ApplyVisualTwist(
                    pipeline,
                    false,
                    upperArmInfluence: 0.25f,
                    forearmInfluence: 0.6f,
                    upperArmMaxDegrees: 30f,
                    forearmMaxDegrees: 50f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.enableYybArmVisualTwistCorrection, Is.False);
                Assert.That(pipeline.YybArmVisualUpperArmInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmVisualForearmInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmVisualUpperArmMaxDegrees, Is.EqualTo(30f).Within(0.0001f));
                Assert.That(pipeline.YybArmVisualForearmMaxDegrees, Is.EqualTo(50f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_RightSleeveSilhouetteOffsetOverride_When_Toggled_Then_ClampsOnlySleeveOffsetSettings()
        {
            var pipelineObject = new GameObject("right sleeve silhouette offset override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.ShouldUseManualAnimatorBodyPositionXzReference = false;
                pipeline.enableYybArmSleeveAnchorCorrection = true;

                bool enabledApplied = ApplyRightSleeveSilhouetteOffset(
                    pipeline,
                    true,
                    localOffsetX: -0.055f,
                    frameGateStart: 90f,
                    frameGateEnd: 90f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.useYybRightSleeveSilhouetteLocalOffsetReference, Is.True);
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetX, Is.EqualTo(-0.055f).Within(0.0001f));
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateStart, Is.EqualTo(90f).Within(0.0001f));
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd, Is.EqualTo(90f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorBodyPositionXzReference, Is.False);
                Assert.That(pipeline.enableYybArmSleeveAnchorCorrection, Is.True);

                bool clampedApplied = ApplyRightSleeveSilhouetteOffset(
                    pipeline,
                    true,
                    localOffsetX: 0.5f,
                    frameGateStart: -10f,
                    frameGateEnd: 7000f);

                Assert.That(clampedApplied, Is.True);
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetX, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateStart, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd, Is.EqualTo(6000f).Within(0.0001f));

                bool lowerClampedApplied = ApplyRightSleeveSilhouetteOffset(
                    pipeline,
                    true,
                    localOffsetX: -0.5f,
                    frameGateStart: 90f,
                    frameGateEnd: 90f);

                Assert.That(lowerClampedApplied, Is.True);
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetX, Is.EqualTo(-0.2f).Within(0.0001f));

                bool disabledApplied = ApplyRightSleeveSilhouetteOffset(
                    pipeline,
                    false,
                    localOffsetX: -0.055f,
                    frameGateStart: 90f,
                    frameGateEnd: 90f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.useYybRightSleeveSilhouetteLocalOffsetReference, Is.False);
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetX, Is.EqualTo(-0.055f).Within(0.0001f));
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateStart, Is.EqualTo(90f).Within(0.0001f));
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd, Is.EqualTo(90f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_DirectionOverride_When_Enabled_Then_ClampsOnlyDirectionSettings()
        {
            var pipelineObject = new GameObject("arm direction override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.enableYybArmSwingLimitCorrection = false;
                pipeline.ShouldUseManualAnimatorBodyRotationReference = false;
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.ShouldUseManualAnimatorHipsLocalPositionReference = false;

                bool enabledApplied = ApplyDirection(
                    pipeline,
                    true,
                    upperArmWeight: 0.4f,
                    forearmWeight: 0.55f,
                    upperArmMaxDegrees: 22f,
                    forearmMaxDegrees: 35f,
                    leftSideWeightScale: 0.7f,
                    rightSideWeightScale: 0.8f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.enableYybArmDirectionRetargetCorrection, Is.True);
                Assert.That(pipeline.YybArmDirectionUpperArmWeight, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionForearmWeight, Is.EqualTo(0.55f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionUpperArmMaxDegrees, Is.EqualTo(22f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionForearmMaxDegrees, Is.EqualTo(35f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionLeftSideWeightScale, Is.EqualTo(0.7f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionRightSideWeightScale, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(pipeline.enableYybArmSwingLimitCorrection, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorBodyRotationReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False);

                bool clampedApplied = ApplyDirection(
                    pipeline,
                    true,
                    upperArmWeight: 1.5f,
                    forearmWeight: -0.5f,
                    upperArmMaxDegrees: 150f,
                    forearmMaxDegrees: -8f,
                    leftSideWeightScale: -0.5f,
                    rightSideWeightScale: 1.25f);

                Assert.That(clampedApplied, Is.True);
                Assert.That(pipeline.YybArmDirectionUpperArmWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionForearmWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionUpperArmMaxDegrees, Is.EqualTo(120f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionForearmMaxDegrees, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionLeftSideWeightScale, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionRightSideWeightScale, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_DirectionOverride_When_Disabled_Then_ZerosWeightsAndPreservesAngleLimits()
        {
            var pipelineObject = new GameObject("disabled arm direction override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();

                bool applied = ApplyDirection(
                    pipeline,
                    false,
                    upperArmWeight: 0.4f,
                    forearmWeight: 0.55f,
                    upperArmMaxDegrees: 22f,
                    forearmMaxDegrees: 35f,
                    leftSideWeightScale: 0.7f,
                    rightSideWeightScale: 0.8f);

                Assert.That(applied, Is.True);
                Assert.That(pipeline.enableYybArmDirectionRetargetCorrection, Is.False);
                Assert.That(pipeline.YybArmDirectionUpperArmWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionForearmWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionUpperArmMaxDegrees, Is.EqualTo(22f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionForearmMaxDegrees, Is.EqualTo(35f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionLeftSideWeightScale, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionRightSideWeightScale, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        private static bool ApplyDirection(
            FBXVmdPipeline pipeline,
            bool enabled,
            float upperArmWeight,
            float forearmWeight,
            float upperArmMaxDegrees,
            float forearmMaxDegrees,
            float leftSideWeightScale,
            float rightSideWeightScale)
        {
            MethodInfo applyMethod = FindApplyMethod("ApplyDirection");

            return (bool)applyMethod.Invoke(
                null,
                new object[]
                {
                    pipeline,
                    enabled,
                    upperArmWeight,
                    forearmWeight,
                    upperArmMaxDegrees,
                    forearmMaxDegrees,
                    leftSideWeightScale,
                    rightSideWeightScale
                });
        }

        private static bool ApplySleeveAnchor(
            FBXVmdPipeline pipeline,
            bool enabled,
            float sleeveInfluence,
            float shoulderCapInfluence,
            float maxDegrees)
        {
            MethodInfo applyMethod = FindApplyMethod("ApplySleeveAnchor");

            return (bool)applyMethod.Invoke(
                null,
                new object[]
                {
                    pipeline,
                    enabled,
                    sleeveInfluence,
                    shoulderCapInfluence,
                    maxDegrees
                });
        }

        private static bool ApplyVisualTwist(
            FBXVmdPipeline pipeline,
            bool enabled,
            float upperArmInfluence,
            float forearmInfluence,
            float upperArmMaxDegrees,
            float forearmMaxDegrees)
        {
            MethodInfo applyMethod = FindApplyMethod("ApplyVisualTwist");

            return (bool)applyMethod.Invoke(
                null,
                new object[]
                {
                    pipeline,
                    enabled,
                    upperArmInfluence,
                    forearmInfluence,
                    upperArmMaxDegrees,
                    forearmMaxDegrees
                });
        }

        private static bool ApplyRightSleeveSilhouetteOffset(
            FBXVmdPipeline pipeline,
            bool enabled,
            float localOffsetX,
            float frameGateStart,
            float frameGateEnd)
        {
            MethodInfo applyMethod = FindApplyMethod("ApplyRightSleeveSilhouetteOffset");

            return (bool)applyMethod.Invoke(
                null,
                new object[]
                {
                    pipeline,
                    enabled,
                    localOffsetX,
                    frameGateStart,
                    frameGateEnd
                });
        }

        private static bool ApplySwingLimit(
            FBXVmdPipeline pipeline,
            bool enabled,
            float weight = 0.5f,
            float maxDownDot = 0.42f,
            float minHandHorizontalRatio = 0.07f,
            float maxHandBelowShoulderRatio = 0.6f,
            float horizontalReachLimitWeight = 0f,
            float maxHandHorizontalReachRatio = 0f,
            float horizontalReachMaxHandBelowShoulderRatio = 0f,
            float horizontalReachMinElbowAngleAfterApply = 0f,
            float raisedPoseHorizontalReachLimitWeight = 0f,
            float raisedPoseMinUpperArmDownDot = 0.55f,
            float raisedPoseMaxHandBelowShoulderRatio = 0.05f,
            float raisedPoseMaxHandHorizontalReachRatio = 0f)
        {
            MethodInfo applyMethod = FindApplyMethod("ApplySwingLimit");

            return (bool)applyMethod.Invoke(
                null,
                new object[]
                {
                    pipeline,
                    enabled,
                    weight,
                    maxDownDot,
                    minHandHorizontalRatio,
                    maxHandBelowShoulderRatio,
                    horizontalReachLimitWeight,
                    maxHandHorizontalReachRatio,
                    horizontalReachMaxHandBelowShoulderRatio,
                    horizontalReachMinElbowAngleAfterApply,
                    raisedPoseHorizontalReachLimitWeight,
                    raisedPoseMinUpperArmDownDot,
                    raisedPoseMaxHandBelowShoulderRatio,
                    raisedPoseMaxHandHorizontalReachRatio
                });
        }

        private static MethodInfo FindApplyMethod(string methodName)
        {
            Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybArmRuntimeOverrideApplier",
                throwOnError: false);
            Assert.That(applierType, Is.Not.Null, "팔 override 적용기가 필요합니다.");

            MethodInfo applyMethod = applierType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null, $"{methodName} 적용 메서드가 필요합니다.");
            return applyMethod;
        }
    }
}
