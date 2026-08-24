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
    }
}
