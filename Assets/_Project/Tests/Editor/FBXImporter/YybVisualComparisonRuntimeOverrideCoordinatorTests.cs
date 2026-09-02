using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonRuntimeOverrideCoordinatorTests
    {
        [Test]
        public void Given_RunState_When_Applying_Then_DelegatesGenericAndYybOverrides()
        {
            var pipelineObject = new GameObject("visual comparison override pipeline");
            try
            {
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
                Type stateType = runtimeAssembly.GetType(
                    "Fbx2Vmd.FBXImporter.YybVisualComparisonRunStateData",
                    throwOnError: true);
                Type coordinatorType = runtimeAssembly.GetType(
                    "Fbx2Vmd.FBXImporter.YybVisualComparisonRuntimeOverrideCoordinator",
                    throwOnError: false);
                Assert.That(coordinatorType, Is.Not.Null, "runtime override 조립 경계가 필요합니다.");

                object state = Activator.CreateInstance(stateType, nonPublic: true);
                stateType.GetField("enableManualAnimatorFootLocalRotationRuntimeOverride").SetValue(state, true);
                stateType.GetField("enableYybArmSwingLimitRuntimeOverride").SetValue(state, true);
                stateType.GetField("yybArmSwingLimitWeight").SetValue(state, 0.75f);
                stateType.GetField("enableRetargetBodyPositionXzRootMotionRuntimeOverride").SetValue(state, true);
                stateType.GetField("disableTargetHumanoidBonePositionLockRuntimeOverride").SetValue(state, true);
                stateType.GetField("enableYybRightSleeveSilhouetteOffsetRuntimeOverride").SetValue(state, true);
                stateType.GetField("yybRightSleeveSilhouetteLocalOffsetX").SetValue(state, 0.1f);
                stateType.GetField("yybRightSleeveSilhouetteLocalOffsetFrameGateStart").SetValue(state, 120f);
                stateType.GetField("yybRightSleeveSilhouetteLocalOffsetFrameGateEnd").SetValue(state, 360f);

                MethodInfo applyMethod = coordinatorType.GetMethod(
                    "Apply",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(applyMethod, Is.Not.Null);

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new[] { pipeline, state, (object)0.5f, 1f, 0.125f, 1f });

                Assert.That(applied, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootLocalRotationReference, Is.True);
                Assert.That(pipeline.enableYybArmSwingLimitCorrection, Is.True);
                Assert.That(pipeline.YybArmSwingLimitWeight, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseRetargetBodyPositionXZRootMotion, Is.True);
                Assert.That(pipeline.ShouldLockTargetHumanoidBonePositions, Is.False);
                Assert.That(pipeline.useYybRightSleeveSilhouetteLocalOffsetReference, Is.True);
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetX, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateStart, Is.EqualTo(120f));
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd, Is.EqualTo(360f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }
        [Test]
        public void MainSceneRuntimeOverrides_DefaultOptionsPreservePromotedSceneDefaults()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, "Main_Auto scene must contain FBXVmdPipeline.");
            ClearYybVisualComparisonRunnerState("default-options-preserve-scene-defaults-test");

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootLocalRotationReference, Is.True);
            Assert.That(fileManager.manualAnimatorFootLocalRotationReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.True);
            Assert.That(fileManager.manualAnimatorBodyRotationReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight, Is.EqualTo(0.125f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);
            Assert.That(fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle, Is.EqualTo(45f).Within(0.0001f));
            Assert.That(fileManager.usePostSetHumanPoseRightEndpointPositionReference, Is.False);
            Assert.That(fileManager.postSetHumanPoseRightEndpointPositionReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.postSetHumanPoseRightEndpointPositionReferenceMaxOffset, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(fileManager.postSetHumanPoseRightEndpointPositionReferencePositiveZScale, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
            Assert.That(fileManager.manualAnimatorFullBodyPoseReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.ShouldExcludeManualAnimatorFullBodyLowerMuscles, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseLeftArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateStart, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateEnd, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fileManager.useManualAnimatorHandLocalRotationReference, Is.True);
            Assert.That(fileManager.ShouldLockTargetHumanoidBonePositions, Is.True);
            Assert.That(fileManager.enableYybArmSwingLimitCorrection, Is.True);
            Assert.That(fileManager.YybArmSwingLimitWeight, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(fileManager.YybArmSwingMaxDownDot, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(fileManager.YybArmSwingMaxHandBelowShoulderRatio, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(fileManager.YybArmSwingHorizontalReachLimitWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.YybArmSwingMaxHandHorizontalReachRatio, Is.EqualTo(0.06f).Within(0.0001f));
            Assert.That(fileManager.YybArmSwingRaisedPoseHorizontalReachLimitWeight, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(fileManager.YybArmSwingRaisedPoseMinUpperArmDownDot, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(fileManager.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(fileManager.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(fileManager.enableYybArmSleeveAnchorCorrection, Is.True);
            Assert.That(fileManager.YybArmSleeveAnchorInfluence, Is.EqualTo(0.825f).Within(0.0001f));
            Assert.That(fileManager.enableYybArmVisualTwistCorrection, Is.True);
        }

        [Test]
        public void MainSceneRuntimeOverrides_LowerBodyForceOffOptionsDisablePromotedSceneDefaults()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, "Main_Auto scene must contain FBXVmdPipeline.");
            Assert.That(fileManager.ShouldUseManualAnimatorFootLocalRotationReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);

            ClearYybVisualComparisonRunnerState("lower-body-force-off-test");
            SetYybVisualComparisonRunOption("enableManualAnimatorFootLocalRotationRuntimeOverride", true);
            SetYybVisualComparisonRunOption("disableManualAnimatorFootLocalRotationRuntimeOverride", true);
            SetYybVisualComparisonRunOption("enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride", true);
            SetYybVisualComparisonRunOption("disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride", true);
            SetYybVisualComparisonRunOption("enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride", true);
            SetYybVisualComparisonRunOption("disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride", true);

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootLocalRotationReference, Is.False);
            Assert.That(fileManager.manualAnimatorFootLocalRotationReferenceWeight, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.False);
            Assert.That(fileManager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.False);
            Assert.That(fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, Is.EqualTo(0f).Within(0.0001f));

            ClearYybVisualComparisonRunnerState("lower-body-force-off-test-cleanup");
        }

        [Test]
        public void MainSceneRuntimeOverrides_LegChainSegmentDetailOptionsPreservePromotedSceneDefaults()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, "Main_Auto scene must contain FBXVmdPipeline.");
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(60f).Within(0.0001f));

            ClearYybVisualComparisonRunnerState("leg-chain-segment-detail-test");
            SetYybVisualComparisonRunOption("disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride", true);
            SetYybVisualComparisonRunOption("manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle", 3f);
            SetYybVisualComparisonRunOption("disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride", true);
            SetYybVisualComparisonRunOption("manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle", 2f);

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(fileManager.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference, Is.True);
            Assert.That(fileManager.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(fileManager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference, Is.True);
            Assert.That(fileManager.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(fileManager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.False);
            Assert.That(fileManager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle, Is.EqualTo(0f).Within(0.0001f));

            ClearYybVisualComparisonRunnerState("leg-chain-segment-detail-test-cleanup");
        }

        [Test]
        public void MainSceneRuntimeOverrides_FullBodyForceOffOptionsDisablePromotedSceneDefaults()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, "Main_Auto scene must contain FBXVmdPipeline.");
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootLocalRotationReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);

            ClearYybVisualComparisonRunnerState("full-body-force-off-test");
            SetYybVisualComparisonRunOption("enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunOption("disableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunOption("enableManualAnimatorBodyRotationRuntimeOverride", true);
            SetYybVisualComparisonRunOption("disableManualAnimatorBodyRotationRuntimeOverride", true);

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseReferenceWeight, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.False);
            Assert.That(fileManager.manualAnimatorBodyRotationReferenceWeight, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                fileManager.ShouldUseManualAnimatorFootLocalRotationReference,
                Is.True,
                "Full-body force-off probes must not disable lower-body localRotation compensation.");
            Assert.That(
                fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference,
                Is.True,
                "Full-body force-off probes must not disable lower-body segment compensation.");
            Assert.That(
                fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference,
                Is.True,
                "Full-body force-off probes must not disable lower-body residual yaw compensation.");

            ClearYybVisualComparisonRunnerState("full-body-force-off-test-cleanup");
        }

        [Test]
        public void MainSceneRuntimeOverrides_FullBodyPoseMaskOptionsKeepRuntimeScopeIsolated()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, "Main_Auto scene must contain FBXVmdPipeline.");
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);

            ClearYybVisualComparisonRunnerState("full-body-mask-exclude-lower-body-test");
            SetYybVisualComparisonRunOption("enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride", true);

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
            Assert.That(fileManager.manualAnimatorFullBodyPoseReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.ShouldExcludeManualAnimatorFullBodyLowerMuscles, Is.True);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseLeftArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateStart, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateEnd, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);

            ClearYybVisualComparisonRunnerState("full-body-mask-lower-body-only-test");
            SetYybVisualComparisonRunOption("enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride", true);

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
            Assert.That(fileManager.manualAnimatorFullBodyPoseReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.ShouldExcludeManualAnimatorFullBodyLowerMuscles, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly, Is.True);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseLeftArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateStart, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateEnd, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);

            ClearYybVisualComparisonRunnerState("full-body-mask-leg-twist-only-test");
            SetYybVisualComparisonRunOption("enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride", true);

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
            Assert.That(fileManager.manualAnimatorFullBodyPoseReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.ShouldExcludeManualAnimatorFullBodyLowerMuscles, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly, Is.True);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseLeftArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateStart, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateEnd, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);

            ClearYybVisualComparisonRunnerState("full-body-mask-right-arm-frame-gate-test");
            SetYybVisualComparisonRunOption("enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride", true);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseReferenceFrameGateStart", 88f);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseReferenceFrameGateEnd", 92f);

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
            Assert.That(fileManager.manualAnimatorFullBodyPoseReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.ShouldExcludeManualAnimatorFullBodyLowerMuscles, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightArmMusclesOnly, Is.True);
            Assert.That(fileManager.manualAnimatorFullBodyPoseLeftArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateStart, Is.EqualTo(88f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateEnd, Is.EqualTo(92f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);

            ClearYybVisualComparisonRunnerState("full-body-mask-left-arm-frame-gate-test");
            SetYybVisualComparisonRunOption("enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride", true);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseReferenceFrameGateStart", 396f);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseReferenceFrameGateEnd", 396f);

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
            Assert.That(fileManager.manualAnimatorFullBodyPoseReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.ShouldExcludeManualAnimatorFullBodyLowerMuscles, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseLeftArmMusclesOnly, Is.True);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateStart, Is.EqualTo(396f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateEnd, Is.EqualTo(396f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);

            ClearYybVisualComparisonRunnerState("full-body-mask-right-sleeve-chain-frame-gate-test");
            SetYybVisualComparisonRunOption("enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride", true);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseReferenceFrameGateStart", 90f);
            SetYybVisualComparisonRunOption("manualAnimatorFullBodyPoseReferenceFrameGateEnd", 90f);

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
            Assert.That(fileManager.manualAnimatorFullBodyPoseReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.ShouldExcludeManualAnimatorFullBodyLowerMuscles, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly, Is.False);
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseLeftArmMusclesOnly, Is.False);
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly, Is.True);
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateStart, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateEnd, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);

            ClearYybVisualComparisonRunnerState("full-body-mask-test-cleanup");
        }

        [Test]
        public void MainSceneRuntimeOverrides_SetHumanPoseRightLegTwistOutputKeepsRuntimeScopeIsolated()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, "Main_Auto scene must contain FBXVmdPipeline.");
            Assert.That(fileManager.ShouldUseSetHumanPoseRightLegTwistOutputReference, Is.False);
            Assert.That(fileManager.setHumanPoseRightLegTwistOutputReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fileManager.setHumanPoseRightLegTwistOutputReferenceMaxDelta, Is.EqualTo(0.02f).Within(0.0001f));

            ClearYybVisualComparisonRunnerState("set-human-pose-right-leg-twist-output-test");
            SetYybVisualComparisonRunOption("enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride", true);
            SetYybVisualComparisonRunOption("setHumanPoseRightLegTwistOutputReferenceWeight", 0.5f);
            SetYybVisualComparisonRunOption("setHumanPoseRightLegTwistOutputReferenceMaxDelta", 0.01f);

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseSetHumanPoseRightLegTwistOutputReference, Is.True);
            Assert.That(fileManager.setHumanPoseRightLegTwistOutputReferenceWeight, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fileManager.setHumanPoseRightLegTwistOutputReferenceMaxDelta, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);

            ClearYybVisualComparisonRunnerState("set-human-pose-right-leg-twist-output-cleanup");
        }

        private static void ClearYybVisualComparisonRunnerState(string reason)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner, Assembly-CSharp");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ClearStaleRunState",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a state reset hook for editor tests.");
            method.Invoke(null, new object[] { reason });
        }

        private static bool ApplyMainSceneRuntimeOverrides(FBXVmdPipeline manager)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner, Assembly-CSharp");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyMainSceneRuntimeOverrides",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must preserve scene defaults when no runtime-only override is enabled.");
            return (bool)method.Invoke(null, new object[] { manager });
        }

        private static void SetYybVisualComparisonRunOption<T>(string optionName, T value)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner, Assembly-CSharp");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            FieldInfo optionsField = runnerType.GetField(
                "_currentRunOptions",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(optionsField, Is.Not.Null, "비교 실행기의 현재 옵션 경계가 필요합니다.");

            object options = optionsField.GetValue(null);
            Assert.That(options, Is.Not.Null, "비교 실행기의 현재 옵션이 필요합니다.");
            FieldInfo optionField = options.GetType().GetField(
                optionName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(optionField, Is.Not.Null, $"비교 실행 옵션 {optionName}이 필요합니다.");
            optionField.SetValue(options, value);
        }

    }
}
