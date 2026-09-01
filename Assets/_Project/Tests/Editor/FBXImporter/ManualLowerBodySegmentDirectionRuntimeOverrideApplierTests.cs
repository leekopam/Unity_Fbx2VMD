using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests
    {
        [Test]
        public void Given_ChangedDetailSetting_When_CheckingDetails_Then_ReturnsTrue()
        {
            Type applierType = FindApplierType();
            MethodInfo hasDetailsMethod = applierType.GetMethod(
                "HasDetails",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(hasDetailsMethod, Is.Not.Null);

            bool hasDetails = (bool)hasDetailsMethod.Invoke(
                null,
                new object[]
                {
                    false,
                    0f,
                    false,
                    0f,
                    0f,
                    0f,
                    1f,
                    1f,
                    0.125f,
                    0.125f,
                    0f,
                    0f,
                    0.5f,
                    1f,
                    false,
                    0f
                });

            Assert.That(hasDetails, Is.True);
        }

        [Test]
        public void Given_LowerBodySegmentDirectionSettings_When_Applied_Then_ClampsValues()
        {
            var pipelineObject = new GameObject("lower body segment direction override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                bool applied = InvokeApply(
                    pipeline,
                    true,
                    weight: 1.5f,
                    maxAngle: -1f,
                    disableUpperLegToLowerLeg: true,
                    upperLegToLowerLegMaxAngle: -2f,
                    disableLowerLegToFoot: true,
                    lowerLegToFootMaxAngle: -3f,
                    leftLowerLegToFootMaxAngle: -4f,
                    rightLowerLegToFootMaxAngle: -5f,
                    rightLowerLegToFootAxisXzScale: 2f,
                    rightLowerLegToFootBlendWeight: -1f,
                    rightLowerLegToFootFrameGateStart: -6f,
                    rightLowerLegToFootFrameGateEnd: -7f,
                    rightLowerLegToFootEndpointBlendWeight: 2f,
                    disableFootToToes: true,
                    footToToesMaxAngle: -8f);

                Assert.That(applied, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
                Assert.That(pipeline.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(0f));
                Assert.That(pipeline.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference, Is.True);
                Assert.That(pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight, Is.EqualTo(1f));
                Assert.That(pipeline.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorLowerBodySegmentDirectionRuntimeOverride_When_Toggled_Then_OnlyChangesSegmentDirectionSwitchAndCaps()
        {
            var managerObject = new GameObject("manual animator lower body segment direction runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = false;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight = 0f;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = 0f;
                manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference = true;
                manager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = 4f;

                bool enabledApplied = InvokeApply(
                    manager,
                    true,
                    weight: 0.75f,
                    maxAngle: 6.2f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(6.2f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight, Is.EqualTo(0.125f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.False);
                Assert.That(manager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False, "Segment direction candidate must not enable the rejected BipedIK pull path.");
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, "Segment direction candidate must not re-enable the rejected hips localPosition copy path.");
                Assert.That(manager.ShouldUseManualAnimatorFootHeightGroundingReference, Is.False, "Segment direction candidate must not change grounding.");

                bool disabledApplied = InvokeApply(
                    manager,
                    false,
                    weight: 0.75f,
                    maxAngle: 6.2f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.False);
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(6.2f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.False);
                Assert.That(manager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorFootToToesSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyToeSegments()
        {
            var managerObject = new GameObject("manual animator foot toes segment direction runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = false;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight = 0f;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = 0f;
                manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference = false;
                manager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = 0f;

                bool applied = InvokeApply(
                    manager,
                    true,
                    weight: 1f,
                    maxAngle: 60f,
                    disableFootToToes: true,
                    footToToesMaxAngle: 2f);

                Assert.That(applied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(60f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(manager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.False, "FootToToes segment ablation must not alter residual yaw correction.");
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False, "FootToToes segment ablation must not enable the rejected BipedIK pull path.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorLegChainSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyRequestedSegments()
        {
            var managerObject = new GameObject("manual animator leg chain segment direction runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = false;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight = 0f;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = 0f;
                manager.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference = false;
                manager.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = 0f;
                manager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference = false;
                manager.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = 0f;
                manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference = false;
                manager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = 0f;

                bool applied = InvokeApply(
                    manager,
                    true,
                    weight: 1f,
                    maxAngle: 60f,
                    disableUpperLegToLowerLeg: true,
                    upperLegToLowerLegMaxAngle: 3f,
                    disableLowerLegToFoot: true,
                    lowerLegToFootMaxAngle: 2f,
                    disableFootToToes: false,
                    footToToesMaxAngle: 0f);

                Assert.That(applied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(60f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle, Is.EqualTo(3f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.False);
                Assert.That(manager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.False, "Leg-chain segment ablation must not alter residual yaw correction.");
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False, "Leg-chain segment ablation must not enable the rejected BipedIK pull path.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_RightLowerLegToFootSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyRightSide()
        {
            var managerObject = new GameObject("manual animator right lower leg segment direction runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = false;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight = 0f;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = 0f;

                bool applied = InvokeApply(
                    manager,
                    true,
                    weight: 1f,
                    maxAngle: 60f,
                    disableUpperLegToLowerLeg: false,
                    upperLegToLowerLegMaxAngle: 0f,
                    disableLowerLegToFoot: false,
                    lowerLegToFootMaxAngle: 0f,
                    leftLowerLegToFootMaxAngle: 0f,
                    rightLowerLegToFootMaxAngle: 2f,
                    disableFootToToes: false,
                    footToToesMaxAngle: 0f);

                Assert.That(applied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(60f).Within(0.0001f));
                Assert.That(manager.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference, Is.False);
                Assert.That(manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.False);
                Assert.That(manager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.False, "Right-side segment ablation must not alter residual yaw correction.");
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False, "Right-side segment ablation must not enable the rejected BipedIK pull path.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_RightLowerLegToFootAxisAwareRuntimeOverride_When_Applied_Then_ScalesOnlyRightAxisXzContribution()
        {
            var managerObject = new GameObject("manual animator right lower leg axis-aware runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = false;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight = 0f;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = 0f;

                bool applied = InvokeApply(
                    manager,
                    true,
                    weight: 1f,
                    maxAngle: 60f,
                    disableUpperLegToLowerLeg: false,
                    upperLegToLowerLegMaxAngle: 0f,
                    disableLowerLegToFoot: false,
                    lowerLegToFootMaxAngle: 0f,
                    leftLowerLegToFootMaxAngle: 0f,
                    rightLowerLegToFootMaxAngle: 4f,
                    rightLowerLegToFootAxisXzScale: 0.25f,
                    rightLowerLegToFootBlendWeight: 1f,
                    rightLowerLegToFootFrameGateStart: 0f,
                    rightLowerLegToFootFrameGateEnd: 0f,
                    rightLowerLegToFootEndpointBlendWeight: 1f,
                    disableFootToToes: false,
                    footToToesMaxAngle: 0f);

                Assert.That(applied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(4f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference, Is.False);
                Assert.That(manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.False);
                Assert.That(manager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.False, "Axis-aware lower leg ablation must not alter residual yaw correction.");
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False, "Axis-aware lower leg ablation must not enable the rejected BipedIK pull path.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_RightLowerLegToFootSoftBlendRuntimeOverride_When_Applied_Then_ScalesOnlyRightCorrectionWeight()
        {
            var managerObject = new GameObject("manual animator right lower leg soft-blend runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = false;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight = 0f;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = 0f;

                bool applied = InvokeApply(
                    manager,
                    true,
                    weight: 1f,
                    maxAngle: 60f,
                    disableUpperLegToLowerLeg: false,
                    upperLegToLowerLegMaxAngle: 0f,
                    disableLowerLegToFoot: false,
                    lowerLegToFootMaxAngle: 0f,
                    leftLowerLegToFootMaxAngle: 0f,
                    rightLowerLegToFootMaxAngle: 4f,
                    rightLowerLegToFootAxisXzScale: 1f,
                    rightLowerLegToFootBlendWeight: 0.5f,
                    rightLowerLegToFootFrameGateStart: 0f,
                    rightLowerLegToFootFrameGateEnd: 0f,
                    rightLowerLegToFootEndpointBlendWeight: 1f,
                    disableFootToToes: false,
                    footToToesMaxAngle: 0f);

                Assert.That(applied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(4f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference, Is.False);
                Assert.That(manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.False);
                Assert.That(manager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.False, "Soft-blend lower leg ablation must not alter residual yaw correction.");
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False, "Soft-blend lower leg ablation must not enable the rejected BipedIK pull path.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_RightLowerLegToFootFrameGatedRuntimeOverride_When_Applied_Then_GatesOnlyRightCapWindow()
        {
            var managerObject = new GameObject("manual animator right lower leg frame-gated runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = false;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight = 0f;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = 0f;

                bool applied = InvokeApply(
                    manager,
                    true,
                    weight: 1f,
                    maxAngle: 60f,
                    disableUpperLegToLowerLeg: false,
                    upperLegToLowerLegMaxAngle: 0f,
                    disableLowerLegToFoot: false,
                    lowerLegToFootMaxAngle: 0f,
                    leftLowerLegToFootMaxAngle: 0f,
                    rightLowerLegToFootMaxAngle: 4f,
                    rightLowerLegToFootAxisXzScale: 1f,
                    rightLowerLegToFootBlendWeight: 1f,
                    rightLowerLegToFootFrameGateStart: 900f,
                    rightLowerLegToFootFrameGateEnd: 930f,
                    rightLowerLegToFootEndpointBlendWeight: 1f,
                    disableFootToToes: false,
                    footToToesMaxAngle: 0f);

                Assert.That(applied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(4f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart, Is.EqualTo(900f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd, Is.EqualTo(930f).Within(0.0001f));
                Assert.That(manager.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference, Is.False);
                Assert.That(manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_RightLowerLegToFootEndpointBlendRuntimeOverride_When_Applied_Then_ScalesOnlyRightEndpointDriftCompensation()
        {
            var managerObject = new GameObject("manual animator right lower leg endpoint blend runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = false;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight = 0f;
                manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = 0f;

                bool applied = InvokeApply(
                    manager,
                    true,
                    weight: 1f,
                    maxAngle: 60f,
                    disableUpperLegToLowerLeg: false,
                    upperLegToLowerLegMaxAngle: 0f,
                    disableLowerLegToFoot: false,
                    lowerLegToFootMaxAngle: 0f,
                    leftLowerLegToFootMaxAngle: 0f,
                    rightLowerLegToFootMaxAngle: 4f,
                    rightLowerLegToFootAxisXzScale: 1f,
                    rightLowerLegToFootBlendWeight: 1f,
                    rightLowerLegToFootFrameGateStart: 0f,
                    rightLowerLegToFootFrameGateEnd: 0f,
                    rightLowerLegToFootEndpointBlendWeight: 0.5f,
                    disableFootToToes: false,
                    footToToesMaxAngle: 0f);

                Assert.That(applied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(4f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(manager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference, Is.False);
                Assert.That(manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        private static bool InvokeApply(
            FBXVmdPipeline pipeline,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg = false,
            float upperLegToLowerLegMaxAngle = 0f,
            bool disableLowerLegToFoot = false,
            float lowerLegToFootMaxAngle = 0f,
            float leftLowerLegToFootMaxAngle = 0f,
            float rightLowerLegToFootMaxAngle = 0f,
            float rightLowerLegToFootAxisXzScale = 1f,
            float rightLowerLegToFootBlendWeight = 0.125f,
            float rightLowerLegToFootFrameGateStart = 0f,
            float rightLowerLegToFootFrameGateEnd = 0f,
            float rightLowerLegToFootEndpointBlendWeight = 1f,
            bool disableFootToToes = false,
            float footToToesMaxAngle = 0f)
        {
            Type applierType = FindApplierType();
            MethodInfo applyMethod = applierType.GetMethod(
                "Apply",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(FBXVmdPipeline),
                    typeof(bool),
                    typeof(float),
                    typeof(float),
                    typeof(bool),
                    typeof(float),
                    typeof(bool),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(bool),
                    typeof(float)
                },
                modifiers: null);
            Assert.That(applyMethod, Is.Not.Null);

            return (bool)applyMethod.Invoke(
                null,
                new object[]
                {
                    pipeline,
                    enabled,
                    weight,
                    maxAngle,
                    disableUpperLegToLowerLeg,
                    upperLegToLowerLegMaxAngle,
                    disableLowerLegToFoot,
                    lowerLegToFootMaxAngle,
                    leftLowerLegToFootMaxAngle,
                    rightLowerLegToFootMaxAngle,
                    rightLowerLegToFootAxisXzScale,
                    rightLowerLegToFootBlendWeight,
                    rightLowerLegToFootFrameGateStart,
                    rightLowerLegToFootFrameGateEnd,
                    rightLowerLegToFootEndpointBlendWeight,
                    disableFootToToes,
                    footToToesMaxAngle
                });
        }


        private static Type FindApplierType()
        {
            Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ManualLowerBodySegmentDirectionRuntimeOverrideApplier",
                throwOnError: false);
            Assert.That(applierType, Is.Not.Null, "모델 중립 하체 방향 override 적용기가 필요합니다.");
            return applierType;
        }
    }
}
