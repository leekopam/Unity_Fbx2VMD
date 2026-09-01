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
        public void Given_GenericCharacterPipeline_When_ApplyingLowerBodyReferences_Then_ClampsSettings()
        {
            var pipelineObject = new GameObject("generic lower body reference pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                Type applierType = FindApplierType();

                InvokeApply(applierType, "ApplyFootHipsAlignedResidualYaw", pipeline, true, 2f, -1f);
                InvokeApply(applierType, "ApplyBipedIkFootPosition", pipeline, true, 2f, -1f);
                InvokeApply(applierType, "ApplyHipsLocalPosition", pipeline, true, 2f, -1f);
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

                Assert.That(pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);
                Assert.That(pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle, Is.EqualTo(0f));
                Assert.That(pipeline.useManualAnimatorBipedIkFootPositionReference, Is.True);
                Assert.That(pipeline.manualAnimatorBipedIkFootPositionReferenceWeight, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorBipedIkFootPositionReferenceMaxOffset, Is.EqualTo(0f));
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.True);
                Assert.That(pipeline.manualAnimatorHipsLocalPositionWeight, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorHipsLocalPositionMaxOffset, Is.EqualTo(0f));
                Assert.That(pipeline.ShouldUseManualAnimatorBodyPositionXzReference, Is.True);
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceWeight, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceAxisXScale, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorBodyPositionXzReferenceAxisZScale, Is.EqualTo(0f));
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
