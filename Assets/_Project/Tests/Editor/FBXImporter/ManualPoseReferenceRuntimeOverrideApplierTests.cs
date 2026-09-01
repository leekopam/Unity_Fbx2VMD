using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class ManualPoseReferenceRuntimeOverrideApplierTests
    {
        [Test]
        public void Given_GenericCharacterPipeline_When_TogglingFootLocalRotation_Then_ChangesOnlyFootRotationSettings()
        {
            var pipelineObject = new GameObject("generic foot local rotation reference pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.ShouldUseManualAnimatorFootLocalRotationReference = false;
                pipeline.manualAnimatorFootLocalRotationReferenceWeight = 0f;
                pipeline.ShouldUseManualAnimatorHipsLocalPositionReference = true;
                pipeline.ShouldUseManualAnimatorFootHeightGroundingReference = true;
                pipeline.useManualAnimatorBipedIkFootPositionReference = true;
                pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference = true;
                Type applierType = FindApplierType();

                InvokeApply(applierType, "ApplyFootLocalRotation", pipeline, true);

                Assert.That(pipeline.ShouldUseManualAnimatorFootLocalRotationReference, Is.True);
                Assert.That(pipeline.manualAnimatorFootLocalRotationReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHeightGroundingReference, Is.True);
                Assert.That(pipeline.useManualAnimatorBipedIkFootPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);

                InvokeApply(applierType, "ApplyFootLocalRotation", pipeline, false);

                Assert.That(pipeline.ShouldUseManualAnimatorFootLocalRotationReference, Is.False);
                Assert.That(pipeline.manualAnimatorFootLocalRotationReferenceWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHeightGroundingReference, Is.True);
                Assert.That(pipeline.useManualAnimatorBipedIkFootPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_GenericCharacterPipeline_When_TogglingHandLocalRotation_Then_ChangesOnlyHandSwitch()
        {
            var pipelineObject = new GameObject("generic hand local rotation reference pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.useManualAnimatorHandLocalRotationReference = false;
                pipeline.useManualAnimatorThumbLocalRotationReference = false;
                pipeline.useManualAnimatorHandPalmFrameReference = false;
                pipeline.manualAnimatorHandPalmFrameWeight = 0f;
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.ShouldUseManualAnimatorFingerPoseReference = false;
                Type applierType = FindApplierType();

                InvokeApply(applierType, "ApplyHandLocalRotation", pipeline, true);

                Assert.That(pipeline.useManualAnimatorHandLocalRotationReference, Is.True);
                Assert.That(pipeline.useManualAnimatorThumbLocalRotationReference, Is.False);
                Assert.That(pipeline.useManualAnimatorHandPalmFrameReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFingerPoseReference, Is.False);

                InvokeApply(applierType, "ApplyHandLocalRotation", pipeline, false);

                Assert.That(pipeline.useManualAnimatorHandLocalRotationReference, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_GenericCharacterPipeline_When_TogglingThumbLocalRotation_Then_ChangesOnlyThumbSwitch()
        {
            var pipelineObject = new GameObject("generic thumb local rotation reference pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.useManualAnimatorThumbLocalRotationReference = false;
                pipeline.useManualAnimatorHandLocalRotationReference = false;
                pipeline.useManualAnimatorHandPalmFrameReference = false;
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.ShouldUseManualAnimatorFingerPoseReference = false;
                Type applierType = FindApplierType();

                InvokeApply(applierType, "ApplyThumbLocalRotation", pipeline, true);

                Assert.That(pipeline.useManualAnimatorThumbLocalRotationReference, Is.True);
                Assert.That(pipeline.useManualAnimatorHandLocalRotationReference, Is.False);
                Assert.That(pipeline.useManualAnimatorHandPalmFrameReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFingerPoseReference, Is.False);

                InvokeApply(applierType, "ApplyThumbLocalRotation", pipeline, false);

                Assert.That(pipeline.useManualAnimatorThumbLocalRotationReference, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_GenericCharacterPipeline_When_ApplyingHandPalmFrame_Then_ClampsAndScopesSettings()
        {
            var pipelineObject = new GameObject("generic hand palm frame reference pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.useManualAnimatorHandPalmFrameReference = false;
                pipeline.manualAnimatorHandPalmFrameWeight = 0f;
                pipeline.useManualAnimatorHandLocalRotationReference = false;
                pipeline.useManualAnimatorThumbLocalRotationReference = false;
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.ShouldUseManualAnimatorFingerPoseReference = false;
                Type applierType = FindApplierType();

                InvokeApply(applierType, "ApplyHandPalmFrame", pipeline, true, 0.35f);

                Assert.That(pipeline.useManualAnimatorHandPalmFrameReference, Is.True);
                Assert.That(pipeline.manualAnimatorHandPalmFrameWeight, Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(pipeline.useManualAnimatorHandLocalRotationReference, Is.False);
                Assert.That(pipeline.useManualAnimatorThumbLocalRotationReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFingerPoseReference, Is.False);

                InvokeApply(applierType, "ApplyHandPalmFrame", pipeline, true, 2f);

                Assert.That(pipeline.manualAnimatorHandPalmFrameWeight, Is.EqualTo(1f).Within(0.0001f));

                InvokeApply(applierType, "ApplyHandPalmFrame", pipeline, true, -1f);

                Assert.That(pipeline.manualAnimatorHandPalmFrameWeight, Is.EqualTo(0f).Within(0.0001f));

                InvokeApply(applierType, "ApplyHandPalmFrame", pipeline, false, 0.35f);

                Assert.That(pipeline.useManualAnimatorHandPalmFrameReference, Is.False);
                Assert.That(pipeline.manualAnimatorHandPalmFrameWeight, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_GenericCharacterPipeline_When_ApplyingBipedIkFootPosition_Then_ClampsAndScopesSettings()
        {
            var pipelineObject = new GameObject("generic biped IK foot position reference pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.useManualAnimatorBipedIkFootPositionReference = false;
                pipeline.manualAnimatorBipedIkFootPositionReferenceWeight = 0f;
                pipeline.manualAnimatorBipedIkFootPositionReferenceMaxOffset = 0f;
                pipeline.ShouldUseManualAnimatorFootLocalRotationReference = false;
                pipeline.ShouldUseManualAnimatorFootHeightGroundingReference = false;
                pipeline.enableFinalIkFootGroundingExperiment = false;
                Type applierType = FindApplierType();

                InvokeApply(applierType, "ApplyBipedIkFootPosition", pipeline, true, 0.2f, 0.04f);

                Assert.That(pipeline.useManualAnimatorBipedIkFootPositionReference, Is.True);
                Assert.That(pipeline.manualAnimatorBipedIkFootPositionReferenceWeight, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBipedIkFootPositionReferenceMaxOffset, Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseManualAnimatorFootLocalRotationReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHeightGroundingReference, Is.False);
                Assert.That(pipeline.enableFinalIkFootGroundingExperiment, Is.False);

                InvokeApply(applierType, "ApplyBipedIkFootPosition", pipeline, true, 2f, -1f);

                Assert.That(pipeline.manualAnimatorBipedIkFootPositionReferenceWeight, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorBipedIkFootPositionReferenceMaxOffset, Is.EqualTo(0f));

                InvokeApply(applierType, "ApplyBipedIkFootPosition", pipeline, true, -1f, 0.12f);

                Assert.That(pipeline.manualAnimatorBipedIkFootPositionReferenceWeight, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorBipedIkFootPositionReferenceMaxOffset, Is.EqualTo(0.12f).Within(0.0001f));

                InvokeApply(applierType, "ApplyBipedIkFootPosition", pipeline, false, 0.65f, 0.12f);

                Assert.That(pipeline.useManualAnimatorBipedIkFootPositionReference, Is.False);
                Assert.That(pipeline.manualAnimatorBipedIkFootPositionReferenceWeight, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorBipedIkFootPositionReferenceMaxOffset, Is.EqualTo(0.12f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_GenericCharacterPipeline_When_ApplyingBodyPositionXz_Then_ClampsAndScopesSettings()
        {
            var pipelineObject = new GameObject("generic body position XZ reference pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.usePreSetHumanPoseRightEndpointPositionReference = false;
                pipeline.ShouldUseRetargetBodyPositionXZRootMotion = false;
                pipeline.ShouldUseManualAnimatorHipsLocalPositionReference = true;
                pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference = false;
                Type applierType = FindApplierType();

                InvokeApply(
                    applierType,
                    "ApplyBodyPositionXz",
                    pipeline,
                    true,
                    0.45f,
                    0.025f,
                    300f,
                    600f,
                    30f,
                    0.25f,
                    0.75f);

                Assert.That(pipeline.ShouldUseManualAnimatorBodyPositionXzReference, Is.True);
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceWeight, Is.EqualTo(0.45f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceMaxOffset, Is.EqualTo(0.025f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceFrameGateStart, Is.EqualTo(300f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceFrameGateEnd, Is.EqualTo(600f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames, Is.EqualTo(30f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceAxisXScale, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceAxisZScale, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.usePreSetHumanPoseRightEndpointPositionReference, Is.False);
                Assert.That(pipeline.ShouldUseRetargetBodyPositionXZRootMotion, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.False);

                InvokeApply(
                    applierType,
                    "ApplyBodyPositionXz",
                    pipeline,
                    true,
                    2f,
                    -1f,
                    -2f,
                    -3f,
                    -4f,
                    2f,
                    -1f);

                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceWeight, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceMaxOffset, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceFrameGateStart, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceFrameGateEnd, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceAxisXScale, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceAxisZScale, Is.EqualTo(0f));

                InvokeApply(
                    applierType,
                    "ApplyBodyPositionXz",
                    pipeline,
                    false,
                    0.45f,
                    0.025f,
                    300f,
                    600f,
                    30f,
                    0.25f,
                    0.75f);

                Assert.That(pipeline.ShouldUseManualAnimatorBodyPositionXzReference, Is.False);
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceWeight, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceMaxOffset, Is.EqualTo(0.025f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceFrameGateStart, Is.EqualTo(300f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceFrameGateEnd, Is.EqualTo(600f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames, Is.EqualTo(30f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceAxisXScale, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceAxisZScale, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.usePreSetHumanPoseRightEndpointPositionReference, Is.False);
                Assert.That(pipeline.ShouldUseRetargetBodyPositionXZRootMotion, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_GenericCharacterPipeline_When_ApplyingHipsLocalPosition_Then_ClampsAndScopesSettings()
        {
            var pipelineObject = new GameObject("generic hips local position reference pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.ShouldUseManualAnimatorHipsLocalPositionReference = false;
                pipeline.manualAnimatorHipsLocalPositionWeight = 0f;
                pipeline.manualAnimatorHipsLocalPositionMaxOffset = 0f;
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.useManualAnimatorBipedIkFootPositionReference = true;
                pipeline.ShouldUseManualAnimatorFootHeightGroundingReference = true;
                pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference = true;
                Type applierType = FindApplierType();

                InvokeApply(applierType, "ApplyHipsLocalPosition", pipeline, true, 0.25f, 0.04f);

                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.True);
                Assert.That(pipeline.manualAnimatorHipsLocalPositionWeight, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorHipsLocalPositionMaxOffset, Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.useManualAnimatorBipedIkFootPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHeightGroundingReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);

                InvokeApply(applierType, "ApplyHipsLocalPosition", pipeline, true, 2f, -1f);

                Assert.That(pipeline.manualAnimatorHipsLocalPositionWeight, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorHipsLocalPositionMaxOffset, Is.EqualTo(0f));

                InvokeApply(applierType, "ApplyHipsLocalPosition", pipeline, true, -1f, 0.12f);

                Assert.That(pipeline.manualAnimatorHipsLocalPositionWeight, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorHipsLocalPositionMaxOffset, Is.EqualTo(0.12f).Within(0.0001f));

                InvokeApply(applierType, "ApplyHipsLocalPosition", pipeline, false, 0.25f, 0.04f);

                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False);
                Assert.That(pipeline.manualAnimatorHipsLocalPositionWeight, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorHipsLocalPositionMaxOffset, Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.useManualAnimatorBipedIkFootPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHeightGroundingReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_GenericCharacterPipeline_When_ApplyingFootHipsAlignedResidualYaw_Then_ClampsAndScopesSettings()
        {
            var pipelineObject = new GameObject("generic foot hips aligned residual yaw reference pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference = false;
                pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceWeight = 0f;
                pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = 0f;
                pipeline.useManualAnimatorBipedIkFootPositionReference = true;
                pipeline.ShouldUseManualAnimatorHipsLocalPositionReference = true;
                pipeline.ShouldUseManualAnimatorFootHeightGroundingReference = true;
                Type applierType = FindApplierType();

                InvokeApply(applierType, "ApplyFootHipsAlignedResidualYaw", pipeline, true, 0.8f, 12f);

                Assert.That(pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);
                Assert.That(pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle, Is.EqualTo(12f).Within(0.0001f));
                Assert.That(pipeline.useManualAnimatorBipedIkFootPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHeightGroundingReference, Is.True);

                InvokeApply(applierType, "ApplyFootHipsAlignedResidualYaw", pipeline, true, 2f, -1f);

                Assert.That(pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle, Is.EqualTo(0f));

                InvokeApply(applierType, "ApplyFootHipsAlignedResidualYaw", pipeline, true, -1f, 24f);

                Assert.That(pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle, Is.EqualTo(24f).Within(0.0001f));

                InvokeApply(applierType, "ApplyFootHipsAlignedResidualYaw", pipeline, false, 0.8f, 12f);

                Assert.That(pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.False);
                Assert.That(pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle, Is.EqualTo(12f).Within(0.0001f));
                Assert.That(pipeline.useManualAnimatorBipedIkFootPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootHeightGroundingReference, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_GenericCharacterPipeline_When_ApplyingFullBodyReference_Then_ClampsAndScopesSettings()
        {
            var pipelineObject = new GameObject("generic character pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.ManualPoseReferenceRuntimeOverrideApplier",
                    throwOnError: false);
                Assert.That(applierType, Is.Not.Null, "모델 중립적인 수동 포즈 참조 override 적용기가 필요합니다.");

                MethodInfo applyMethod = applierType.GetMethod(
                    "ApplyFullBodyPose",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(applyMethod, Is.Not.Null);

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new object[]
                    {
                        pipeline,
                        true,
                        2f,
                        true,
                        false,
                        true,
                        false,
                        true,
                        false,
                        -1f,
                        12f
                    });

                Assert.That(applied, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
                Assert.That(pipeline.manualAnimatorFullBodyPoseReferenceWeight, Is.EqualTo(1f));
                Assert.That(pipeline.ShouldExcludeManualAnimatorFullBodyLowerMuscles, Is.True);
                Assert.That(pipeline.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly, Is.False);
                Assert.That(pipeline.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly, Is.True);
                Assert.That(pipeline.manualAnimatorFullBodyPoseRightArmMusclesOnly, Is.False);
                Assert.That(pipeline.manualAnimatorFullBodyPoseLeftArmMusclesOnly, Is.True);
                Assert.That(pipeline.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly, Is.False);
                Assert.That(pipeline.manualAnimatorFullBodyPoseFrameGateStart, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorFullBodyPoseFrameGateEnd, Is.EqualTo(12f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        private static Type FindApplierType()
        {
            Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ManualPoseReferenceRuntimeOverrideApplier",
                throwOnError: false);
            Assert.That(applierType, Is.Not.Null, "모델 중립적인 수동 포즈 참조 override 적용기가 필요합니다.");
            return applierType;
        }

        private static void InvokeApply(Type applierType, string methodName, params object[] arguments)
        {
            MethodInfo applyMethod = applierType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null, $"{methodName} 적용 메서드가 필요합니다.");
            Assert.That((bool)applyMethod.Invoke(null, arguments), Is.True);
        }
    }
}
