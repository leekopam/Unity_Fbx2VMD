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
        public void Given_PreSetEndpointSettings_When_Applied_Then_ClampsValuesAndScopesChanges()
        {
            var pipelineObject = new GameObject("pre-set endpoint override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.usePostSetHumanPoseRightEndpointPositionReference = true;
                pipeline.postSetHumanPoseRightEndpointPositionReferenceWeight = 0.6f;
                pipeline.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition = true;
                MethodInfo applyMethod = FindApplyMethod("ApplyPreSetReference");

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.7f, 0.025f, 0.5f, 0.25f, 180f, 900f, false, false, false, false });

                Assert.That(applied, Is.True);
                Assert.That(pipeline.usePreSetHumanPoseRightEndpointPositionReference, Is.True);
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceWeight, Is.EqualTo(0.7f).Within(0.0001f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceMaxOffset, Is.EqualTo(0.025f).Within(0.0001f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferencePositiveZScale, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart, Is.EqualTo(180f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd, Is.EqualTo(900f));
                Assert.That(pipeline.usePostSetHumanPoseRightEndpointPositionReference, Is.True);
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceWeight, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition, Is.True);

                bool clamped = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 2f, -1f, 2f, -1f, -2f, -3f, false, false, false, false });

                Assert.That(clamped, Is.True);
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceWeight, Is.EqualTo(1f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceMaxOffset, Is.EqualTo(0f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferencePositiveZScale, Is.EqualTo(1f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, Is.EqualTo(0f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart, Is.EqualTo(0f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd, Is.EqualTo(0f));
                Assert.That(pipeline.usePostSetHumanPoseRightEndpointPositionReference, Is.True);
                Assert.That(pipeline.postSetHumanPoseRightEndpointPositionReferenceWeight, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_PreSetEndpointFlags_When_Applied_Then_PreservesIndependentMappings()
        {
            var pipelineObject = new GameObject("pre-set endpoint flag override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                MethodInfo applyMethod = FindApplyMethod("ApplyPreSetReference");

                bool leftSideApplied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.5f, 0.1f, 0.7f, 0.3f, 10f, 20f, true, false, false, false });

                Assert.That(leftSideApplied, Is.True);
                Assert.That(pipeline.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition, Is.True);
                Assert.That(pipeline.preSetHumanPoseEndpointPositionUseGhostCurrentBasis, Is.False);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyX, Is.False);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ, Is.False);

                bool ghostBasisApplied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.5f, 0.1f, 0.7f, 0.3f, 10f, 20f, false, true, false, false });

                Assert.That(ghostBasisApplied, Is.True);
                Assert.That(pipeline.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition, Is.False);
                Assert.That(pipeline.preSetHumanPoseEndpointPositionUseGhostCurrentBasis, Is.True);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyX, Is.False);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ, Is.False);

                bool invertXApplied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.5f, 0.1f, 0.7f, 0.3f, 10f, 20f, false, false, true, false });

                Assert.That(invertXApplied, Is.True);
                Assert.That(pipeline.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition, Is.False);
                Assert.That(pipeline.preSetHumanPoseEndpointPositionUseGhostCurrentBasis, Is.False);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyX, Is.True);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ, Is.False);

                bool invertZApplied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.5f, 0.1f, 0.7f, 0.3f, 10f, 20f, false, false, false, true });

                Assert.That(invertZApplied, Is.True);
                Assert.That(pipeline.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition, Is.False);
                Assert.That(pipeline.preSetHumanPoseEndpointPositionUseGhostCurrentBasis, Is.False);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyX, Is.False);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_PreSetEndpointSettings_When_Disabled_Then_ClearsConditionalValuesAndPreservesCaps()
        {
            var pipelineObject = new GameObject("disabled pre-set endpoint override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.usePostSetHumanPoseRightEndpointPositionReference = true;
                pipeline.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition = true;
                pipeline.usePostSetHumanPoseRightFootEvaluatorXzReference = true;
                MethodInfo applyMethod = FindApplyMethod("ApplyPreSetReference");

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, false, 0.8f, 0.04f, 0.25f, 0.25f, 899f, 901f, true, true, true, true });

                Assert.That(applied, Is.True);
                Assert.That(pipeline.usePreSetHumanPoseRightEndpointPositionReference, Is.False);
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceWeight, Is.EqualTo(0f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceMaxOffset, Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferencePositiveZScale, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart, Is.EqualTo(899f));
                Assert.That(pipeline.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd, Is.EqualTo(901f));
                Assert.That(pipeline.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition, Is.False);
                Assert.That(pipeline.preSetHumanPoseEndpointPositionUseGhostCurrentBasis, Is.False);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyX, Is.False);
                Assert.That(pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ, Is.False);
                Assert.That(pipeline.usePostSetHumanPoseRightEndpointPositionReference, Is.True);
                Assert.That(pipeline.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition, Is.True);
                Assert.That(pipeline.usePostSetHumanPoseRightFootEvaluatorXzReference, Is.True);
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
