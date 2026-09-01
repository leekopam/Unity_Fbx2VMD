using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanPoseEndpointRuntimeOverrideApplierTests
    {
        [Test]
        public void Given_PostSetEndpointSettings_When_Applied_Then_ClampsAndScopesValues()
        {
            var pipelineObject = new GameObject("post-set endpoint override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.useManualAnimatorBipedIkFootPositionReference = false;
                pipeline.enableFinalIkFootGroundingExperiment = false;
                MethodInfo applyMethod = FindApplyMethod("ApplyPostSetReference");

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.75f, 0.035f, 0.25f, 0.25f, 899f, 901f, true, false, 0.049f });

                Assert.That(applied, Is.True);
                Assert.That(pipeline.usePostSetHumanPoseRightEndpointPositionReference, Is.True);
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceWeight, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceMaxOffset, Is.EqualTo(0.035f).Within(0.0001f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferencePositiveZScale, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart, Is.EqualTo(899f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd, Is.EqualTo(901f));
                Assert.That(pipeline.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition, Is.True);
                Assert.That(pipeline.usePostSetHumanPoseRightFootEvaluatorXzReference, Is.False);
                Assert.That(pipeline.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude, Is.EqualTo(0.049f).Within(0.0001f));
                Assert.That(pipeline.useManualAnimatorBipedIkFootPositionReference, Is.False);
                Assert.That(pipeline.enableFinalIkFootGroundingExperiment, Is.False);

                bool evaluatorApplied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.75f, 0.035f, 0.25f, 0.25f, 899f, 901f, false, true, 0.049f });

                Assert.That(evaluatorApplied, Is.True);
                Assert.That(pipeline.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition, Is.False);
                Assert.That(pipeline.usePostSetHumanPoseRightFootEvaluatorXzReference, Is.True);
                Assert.That(pipeline.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude, Is.EqualTo(0.049f).Within(0.0001f));
                Assert.That(pipeline.useManualAnimatorBipedIkFootPositionReference, Is.False);
                Assert.That(pipeline.enableFinalIkFootGroundingExperiment, Is.False);

                bool clamped = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 2f, -1f, 2f, -1f, -2f, -3f, true, true, -4f });

                Assert.That(clamped, Is.True);
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceWeight, Is.EqualTo(1f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceMaxOffset, Is.EqualTo(0f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferencePositiveZScale, Is.EqualTo(1f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, Is.EqualTo(0f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart, Is.EqualTo(0f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd, Is.EqualTo(0f));
                Assert.That(pipeline.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition, Is.True);
                Assert.That(pipeline.usePostSetHumanPoseRightFootEvaluatorXzReference, Is.True);
                Assert.That(pipeline.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_PostSetEndpointSettings_When_Disabled_Then_ClearsConditionalValuesAndPreservesCaps()
        {
            var pipelineObject = new GameObject("disabled post-set endpoint override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                MethodInfo applyMethod = FindApplyMethod("ApplyPostSetReference");

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, false, 0.8f, 0.04f, 0.25f, 0.25f, 899f, 901f, true, true, 0.049f });

                Assert.That(applied, Is.True);
                Assert.That(pipeline.usePostSetHumanPoseRightEndpointPositionReference, Is.False);
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceWeight, Is.EqualTo(0f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceMaxOffset, Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferencePositiveZScale, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart, Is.EqualTo(899f));
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd, Is.EqualTo(901f));
                Assert.That(pipeline.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition, Is.False);
                Assert.That(pipeline.usePostSetHumanPoseRightFootEvaluatorXzReference, Is.False);
                Assert.That(pipeline.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude, Is.EqualTo(0.049f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_PreSetEndpointSettings_When_Applied_Then_PreservesAxisFlags()
        {
            var pipelineObject = new GameObject("pre-set endpoint override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                MethodInfo applyMethod = FindApplyMethod("ApplyPreSetReference");

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.5f, 0.1f, 0.7f, 0.3f, 10f, 20f, true, true, true, false });

                Assert.That(applied, Is.True);
                Assert.That(pipeline.usePreSetHumanPoseRightEndpointPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition, Is.True);
                Assert.That(pipeline.preSetHumanPoseEndpointPositionUseGhostCurrentBasis, Is.True);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyX, Is.True);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        private static MethodInfo FindApplyMethod(string methodName)
        {
            Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanPoseEndpointRuntimeOverrideApplier",
                throwOnError: false);
            Assert.That(applierType, Is.Not.Null, "모델 중립 HumanPose 끝점 override 적용기가 필요합니다.");

            MethodInfo applyMethod = applierType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null);
            return applyMethod;
        }
    }
}
