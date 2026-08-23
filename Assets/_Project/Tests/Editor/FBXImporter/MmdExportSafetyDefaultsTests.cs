using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RootMotion.FinalIK;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class MmdExportSafetyDefaultsTests
    {
        private const float ExpectedYybMmdExportMaxDeltaPerFrame = 0.11f;
        private const float MaxSmokeSafeThumbIndexSpreadAngle = 50f;
        private const float MaxSmokeSafeThumbProjectionMaxPalmNormal = 0.5f;

        private static readonly Type[] YybReferenceClipResolverParameterTypes =
        {
            typeof(string),
            typeof(Func<string, bool>)
        };

        [Test]
        public void MainAutoScene_UsesMmdSafeYybExportDefaults()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, "Main_Auto scene must contain FBXVmdPipeline.");
            Assert.That(fileManager.ShouldStabilizeGroundedFootXZ, Is.False, "Rollback preset must not enable per-foot X/Z locking.");
            Assert.That(fileManager.GroundedFootLockWeight, Is.EqualTo(0.45f).Within(0.0001f), "Rollback preset must restore the pre-reference-video foot-lock blend.");
            Assert.That(fileManager.FreezeRootYAfterInitialGrounding, Is.True, "Root Y must freeze after initial grounding so live playback does not chase per-frame foot noise.");
            Assert.That(fileManager.RetargetPrewarmFrameCount, Is.EqualTo(6), "Rollback preset must remove the 120-frame prewarm added by the reference-video tuning pass.");
            Assert.That(fileManager.MaxLateVisualGroundingStepPerFrame, Is.EqualTo(0.003f).Within(0.0001f), "Rollback preset must restore the conservative late visual grounding step.");
            Assert.That(fileManager.enableYybArmSwingLimitCorrection, Is.True, "Main_Auto must promote the MP4-aligned arm swing limiter after the default playback compare removed the frame-quality failure.");
            Assert.That(fileManager.YybArmSwingLimitWeight, Is.EqualTo(0.6f).Within(0.0001f), "Main_Auto must use the accepted runtime arm swing blend for MP4-aligned playback.");
            Assert.That(fileManager.YybArmSwingMaxDownDot, Is.EqualTo(0.75f).Within(0.0001f), "Main_Auto must keep the accepted upper-arm down-dot cap.");
            Assert.That(fileManager.YybArmSwingMinHandHorizontalRatio, Is.EqualTo(0.05f).Within(0.0001f), "Main_Auto must keep the accepted horizontal trigger ratio.");
            Assert.That(fileManager.YybArmSwingMaxHandBelowShoulderRatio, Is.EqualTo(1.5f).Within(0.0001f), "Main_Auto must keep the accepted below-shoulder tolerance.");
            Assert.That(fileManager.YybArmSwingHorizontalReachLimitWeight, Is.EqualTo(1f).Within(0.0001f), "Main_Auto must apply the accepted horizontal reach clamp strength.");
            Assert.That(fileManager.YybArmSwingMaxHandHorizontalReachRatio, Is.EqualTo(0.06f).Within(0.0001f), "Main_Auto must use the measured reach cap that reduces non-hair average, local average, upper span, and silhouette average without worsening the current max metrics.");
            Assert.That(fileManager.YybArmSwingRaisedPoseHorizontalReachLimitWeight, Is.EqualTo(0.25f).Within(0.0001f), "Main_Auto must keep the accepted raised-pose reach cap that reduces upper-band and silhouette residuals without worsening full or non-hair max.");
            Assert.That(fileManager.YybArmSwingRaisedPoseMinUpperArmDownDot, Is.EqualTo(0.55f).Within(0.0001f), "Main_Auto must only apply the raised-pose reach cap when the upper arm is still meaningfully lowered.");
            Assert.That(fileManager.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio, Is.EqualTo(0.05f).Within(0.0001f), "Main_Auto must keep the raised-pose reach cap out of the natural below-shoulder swing frames.");
            Assert.That(fileManager.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio, Is.EqualTo(0.55f).Within(0.0001f), "Main_Auto must cap only the wide raised-pose horizontal reach that drives the MP4 upper-band residual.");
            Assert.That(fileManager.enableAnatomicalArmGuard, Is.True, "Main_Auto must keep arm anatomy protection while validating the shared YYB playback/export path.");
            Assert.That(fileManager.attachTargetArmDeformationGuard, Is.True, "Main_Auto must attach arm deformation guards while validating the shared YYB playback/export path.");
            Assert.That(fileManager.targetGuardClampAnatomicalArmMuscles, Is.True, "Main_Auto must clamp target-side arm muscles after YYB arm swing correction so late guard output cannot reopen limb-pose failures.");
            Assert.That(fileManager.targetGuardClampArmStretchMuscles, Is.True, "Main_Auto must clamp target-side forearm stretch after YYB arm swing correction.");
            Assert.That(fileManager.enableYybArmVisualTwistCorrection, Is.True, "Main_Auto must keep YYB arm visual twist correction for the shared playback/export path.");
            Assert.That(fileManager.enableYybArmSleeveAnchorCorrection, Is.True, "Main_Auto must keep sleeve anchor correction for the shared playback/export path.");
            Assert.That(fileManager.YybArmSleeveAnchorInfluence, Is.EqualTo(0.825f).Within(0.0001f), "Main_Auto must use the measured sleeve anchor influence that reduces non-hair avg without worsening non-hair max, silhouette, or full max.");
            Assert.That(fileManager.enableThumbAnatomicalGuard, Is.True, "Main_Auto must keep thumb anatomy protection for the shared playback/export path.");
            Assert.That(fileManager.enableThumbLocalRotationGuard, Is.True, "Main_Auto must keep thumb local rotation protection for the shared playback/export path.");
            Assert.That(fileManager.enableThumbVisualLengthGuard, Is.True, "Main_Auto must keep thumb visual length protection for the shared playback/export path.");
            Assert.That(fileManager.failEditorSmokeOnThumbRisk, Is.True, "Editor smoke must fail when thumb risk exceeds the threshold.");
            Assert.That(fileManager.clampRetargetArmStretchMuscles, Is.True, "Main_Auto must clamp retarget arm stretch muscles to prevent corrected left forearm stretch spikes from reopening the limb-pose gate.");
            Assert.That(fileManager.ArmStretchMuscleLimit, Is.EqualTo(0.5f).Within(0.0001f), "Main_Auto must keep the measured 0.5 arm stretch limit that reduces the corrected left forearm stretch gate delta below 1.0.");
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True, "Main_Auto must blend the manual reference pose to reduce the non-hair band_3_right arm/sleeve residual in normal playback/import.");
            Assert.That(fileManager.manualAnimatorFullBodyPoseReferenceWeight, Is.EqualTo(1f).Within(0.0001f), "Main_Auto must use the measured full-body reference blend that reduces full max, non-hair max, and non-hair avg while the remaining upper/silhouette trade-off stays tracked.");
            Assert.That(fileManager.ShouldExcludeManualAnimatorFullBodyLowerMuscles, Is.False, "Main_Auto must not mask lower-body muscles unless a runtime diagnostic explicitly requests it.");
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly, Is.False, "Main_Auto must not limit full-body reference to lower-body muscles unless a runtime diagnostic explicitly requests it.");
            Assert.That(fileManager.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly, Is.False, "Main_Auto must not limit full-body reference to leg twist muscles unless a runtime diagnostic explicitly requests it.");
            Assert.That(fileManager.manualAnimatorFullBodyPoseRightArmMusclesOnly, Is.False, "Main_Auto must not limit full-body reference to right-arm muscles unless a runtime diagnostic explicitly requests it.");
            Assert.That(fileManager.manualAnimatorFullBodyPoseLeftArmMusclesOnly, Is.False, "Main_Auto must not limit full-body reference to left-arm muscles unless a runtime diagnostic explicitly requests it.");
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateStart, Is.EqualTo(0f).Within(0.0001f), "Main_Auto must keep full-body reference frame gates disabled by default.");
            Assert.That(fileManager.manualAnimatorFullBodyPoseFrameGateEnd, Is.EqualTo(0f).Within(0.0001f), "Main_Auto must keep full-body reference frame gates disabled by default.");
            Assert.That(fileManager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, "Rollback preset must remove the manual hips local-position override from the reference-video tuning pass.");
            Assert.That(fileManager.ShouldUseManualAnimatorFootHeightGroundingReference, Is.False, "Rollback preset must remove manual lowest-foot grounding from the reference-video tuning pass.");
            Assert.That(fileManager.ShouldUseManualAnimatorFootLocalRotationReference, Is.True, "Main_Auto must promote the accepted lower-body localRotation reference after it kept MP4 compare gates passing.");
            Assert.That(fileManager.manualAnimatorFootLocalRotationReferenceWeight, Is.EqualTo(1f).Within(0.0001f), "Main_Auto must keep the accepted leg-chain localRotation blend.");
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.True, "Main_Auto must promote the accepted bodyRotation reference for MP4-aligned playback.");
            Assert.That(fileManager.manualAnimatorBodyRotationReferenceWeight, Is.EqualTo(1f).Within(0.0001f), "Main_Auto must keep the accepted bodyRotation blend.");
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True, "Main_Auto must promote the accepted lower-body segment direction guard.");
            Assert.That(fileManager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(1f).Within(0.0001f), "Main_Auto must keep the accepted lower-body segment direction blend.");
            Assert.That(fileManager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(60f).Within(0.0001f), "Main_Auto must keep the accepted lower-body segment direction cap.");
            Assert.That(fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight, Is.EqualTo(0.125f).Within(0.0001f), "Main_Auto must use the measured right LowerLegToFoot soft blend that reduces right foot X/Z and hips-aligned foot residual without moving hips.");
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True, "Main_Auto must promote the accepted foot hips-aligned residual yaw guard.");
            Assert.That(fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, Is.EqualTo(1f).Within(0.0001f), "Main_Auto must keep the accepted residual yaw blend.");
            Assert.That(fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle, Is.EqualTo(45f).Within(0.0001f), "Main_Auto must keep the accepted residual yaw cap.");
            Assert.That(fileManager.usePostSetHumanPoseRightEndpointPositionReference, Is.False, "Main_Auto must keep the post-SetHumanPose endpoint carrier runtime-only until twist and manual-layer ablation metrics justify promotion.");
            Assert.That(fileManager.postSetHumanPoseRightEndpointPositionReferenceWeight, Is.EqualTo(1f).Within(0.0001f), "Main_Auto must preserve the endpoint diagnostic blend value while the diagnostic is disabled.");
            Assert.That(fileManager.postSetHumanPoseRightEndpointPositionReferenceMaxOffset, Is.EqualTo(0.04f).Within(0.0001f), "Main_Auto must preserve the endpoint diagnostic cap while the diagnostic remains runtime-only.");
            Assert.That(fileManager.postSetHumanPoseRightEndpointPositionReferencePositiveZScale, Is.EqualTo(1f).Within(0.0001f), "Main_Auto must preserve the symmetric X/Z endpoint diagnostic scale.");
            Assert.That(fileManager.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, Is.EqualTo(1f).Within(0.0001f), "Main_Auto must preserve the foot/toes average endpoint diagnostic blend.");
            Assert.That(fileManager.manualAnimatorFootHeightGroundingReferenceMaxLift, Is.EqualTo(0.08f).Within(0.0001f), "Serialized cap remains available but must be inactive while the reference-video foot-height reference is disabled.");
            Assert.That(fileManager.clampRetargetHipsLocalPositionSpikes, Is.False, "Hips local clamps change pose internals and must stay out of the center/root-only floor correction slice.");
            Assert.That(fileManager.vmdRecordingPlaybackSpeed, Is.EqualTo(1f).Within(0.0001f), "Main_Auto VMD export must default to normal playback speed.");
            Assert.That(fileManager.useKnownMmdReferenceTiming, Is.False, "Reference timing must be opt-in so Main_Auto does not accelerate normal VMD generation by default.");
            Assert.That(fileManager.showGhostModel, Is.False, "Main_Auto must not show imported Ghost models until the user enables the debug option.");
            Assert.That(fileManager.showGhostSkeletonWhenNoRenderers, Is.False, "Rendererless Ghost skeleton fallback must stay off while Ghost display is disabled.");

            var yybPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku.prefab");
            Assert.That(yybPrefab, Is.Not.Null, "YYB prefab must be loadable.");

            var recorder = yybPrefab.GetComponent<UnityHumanoidVMDRecorder>();
            Assert.That(recorder, Is.Not.Null, "YYB prefab must contain UnityHumanoidVMDRecorder.");
            Assert.That(recorder.IgnoreInitialPosition, Is.False, "Rollback preset must restore the pre-reference-video initial position behavior.");
            Assert.That(recorder.FreezeParentOfAllMotionWhenIgnoringInitialPosition, Is.False, "Rollback preset must keep the new freeze path disabled when initial position is not ignored.");
            Assert.That(recorder.UseBottomCenter, Is.False, "Rollback preset must restore humanoid center export instead of bottom-center export.");
            Assert.That(recorder.KeyReductionLevel, Is.EqualTo(2), "Rollback preset must restore the pre-reference-video key reduction level.");
            Assert.That(recorder.MaxRecordedFramesPerLateUpdate, Is.EqualTo(1), "Recording must not burst multiple VMD frames from a single rendered Unity pose.");
            Assert.That(recorder.ParentOfAllOffset, Is.EqualTo(Vector3.zero), "YYB MMD export must not use a static global/root lift; floor correction is frame-local center Y only.");
            Assert.That(recorder.MmdFootIkExportOffset, Is.EqualTo(Vector3.zero), "YYB MMD export must not add a static IK lift; it causes visible hover in MMD playback.");
            Assert.That(recorder.ClampMmdFootIkYToFloor, Is.False, "YYB MMD export must not clamp foot/toe IK Y in this slice; only center/root Y may be lifted.");
            Assert.That(recorder.LiftMmdCenterYToKeepFeetAboveFloor, Is.True, "YYB MMD export must resolve floor penetration by lifting center/root Y per frame.");
            Assert.That(recorder.MinMmdFootIkY, Is.EqualTo(0.05f).Within(0.0001f), "YYB MMD export should keep effective foot IK height at the same floor clearance seen in Unity smoke metrics.");
            Assert.That(recorder.ClampMmdCenterExportDeltaSpikes, Is.True, "YYB MMD export must clamp one-frame center movement so MMD playback cannot teleport.");
            Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.True, "YYB MMD export must clamp foot/toe IK one-frame jumps so MMD playback cannot snap through IK targets.");
            Assert.That(recorder.UseMmdIkDynamicToggleOnLargeExportSteps, Is.True, "YYB MMD export must keep raw foot/toe IK target travel and hide only large-step visual pulls with VMD IK footer toggles.");
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
            SetYybVisualComparisonRunnerStaticField("_enableManualAnimatorFootLocalRotationRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_disableManualAnimatorFootLocalRotationRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride", true);

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
            SetYybVisualComparisonRunnerStaticField("_disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle", 3f);
            SetYybVisualComparisonRunnerStaticField("_disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle", 2f);

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
            SetYybVisualComparisonRunnerStaticField("_enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_disableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_enableManualAnimatorBodyRotationRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_disableManualAnimatorBodyRotationRuntimeOverride", true);

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
            SetYybVisualComparisonRunnerStaticField("_enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride", true);

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
            SetYybVisualComparisonRunnerStaticField("_enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride", true);

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
            SetYybVisualComparisonRunnerStaticField("_enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride", true);

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
            SetYybVisualComparisonRunnerStaticField("_enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseReferenceFrameGateStart", 88f);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseReferenceFrameGateEnd", 92f);

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
            SetYybVisualComparisonRunnerStaticField("_enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseReferenceFrameGateStart", 396f);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseReferenceFrameGateEnd", 396f);

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
            SetYybVisualComparisonRunnerStaticField("_enableManualAnimatorFullBodyPoseRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseReferenceFrameGateStart", 90f);
            SetYybVisualComparisonRunnerStaticField("_manualAnimatorFullBodyPoseReferenceFrameGateEnd", 90f);

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
        public void ManualFullBodyPoseRightArmMask_FiltersOnlyRightArmChain()
        {
            GameObject host = new GameObject("right-arm-mask-test");
            try
            {
                PoseSpaceRetargeter retargeter = host.AddComponent<PoseSpaceRetargeter>();
                retargeter.manualAnimatorFullBodyPoseRightArmMusclesOnly = true;

                MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                    "ShouldApplyManualFullBodyPoseReferenceMuscle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null, "Pose retargeter must expose a private manual full-body muscle filter.");

                int rightArm = FindHumanMuscleIndex("Right", "Arm");
                int rightForearm = FindHumanMuscleIndex("Right", "Forearm");
                int leftArm = FindHumanMuscleIndex("Left", "Arm");
                int rightUpperLeg = FindHumanMuscleIndex("Right", "Upper Leg");
                int rightIndex = FindHumanMuscleIndex("Right", "Index");

                Assert.That((bool)method.Invoke(retargeter, new object[] { rightArm }), Is.True);
                Assert.That((bool)method.Invoke(retargeter, new object[] { rightForearm }), Is.True);
                Assert.That((bool)method.Invoke(retargeter, new object[] { leftArm }), Is.False);
                Assert.That((bool)method.Invoke(retargeter, new object[] { rightUpperLeg }), Is.False);
                Assert.That((bool)method.Invoke(retargeter, new object[] { rightIndex }), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ManualFullBodyPoseLeftArmMask_FiltersOnlyLeftArmChain()
        {
            GameObject host = new GameObject("left-arm-mask-test");
            try
            {
                PoseSpaceRetargeter retargeter = host.AddComponent<PoseSpaceRetargeter>();
                retargeter.manualAnimatorFullBodyPoseLeftArmMusclesOnly = true;

                MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                    "ShouldApplyManualFullBodyPoseReferenceMuscle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null, "Pose retargeter must expose a private manual full-body muscle filter.");

                int leftArm = FindHumanMuscleIndex("Left", "Arm");
                int leftForearm = FindHumanMuscleIndex("Left", "Forearm");
                int rightArm = FindHumanMuscleIndex("Right", "Arm");
                int leftUpperLeg = FindHumanMuscleIndex("Left", "Upper Leg");
                int leftIndex = FindHumanMuscleIndex("Left", "Index");

                Assert.That((bool)method.Invoke(retargeter, new object[] { leftArm }), Is.True);
                Assert.That((bool)method.Invoke(retargeter, new object[] { leftForearm }), Is.True);
                Assert.That((bool)method.Invoke(retargeter, new object[] { rightArm }), Is.False);
                Assert.That((bool)method.Invoke(retargeter, new object[] { leftUpperLeg }), Is.False);
                Assert.That((bool)method.Invoke(retargeter, new object[] { leftIndex }), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ManualFullBodyPoseRightSleeveChainMask_FiltersOnlySpineAndRightSleeveChain()
        {
            GameObject host = new GameObject("right-sleeve-chain-mask-test");
            try
            {
                PoseSpaceRetargeter retargeter = host.AddComponent<PoseSpaceRetargeter>();
                retargeter.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly = true;

                MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                    "ShouldApplyManualFullBodyPoseReferenceMuscle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null, "Pose retargeter must expose a private manual full-body muscle filter.");

                int spine = FindHumanMuscleIndex("Spine");
                int rightArm = FindHumanMuscleIndex("Right", "Arm");
                int rightForearm = FindHumanMuscleIndex("Right", "Forearm");
                int leftArm = FindHumanMuscleIndex("Left", "Arm");
                int rightUpperLeg = FindHumanMuscleIndex("Right", "Upper Leg");
                int rightIndex = FindHumanMuscleIndex("Right", "Index");

                Assert.That((bool)method.Invoke(retargeter, new object[] { spine }), Is.True);
                Assert.That((bool)method.Invoke(retargeter, new object[] { rightArm }), Is.True);
                Assert.That((bool)method.Invoke(retargeter, new object[] { rightForearm }), Is.True);
                Assert.That((bool)method.Invoke(retargeter, new object[] { leftArm }), Is.False);
                Assert.That((bool)method.Invoke(retargeter, new object[] { rightUpperLeg }), Is.False);
                Assert.That((bool)method.Invoke(retargeter, new object[] { rightIndex }), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
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
            SetYybVisualComparisonRunnerStaticField("_enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride", true);
            SetYybVisualComparisonRunnerStaticField("_setHumanPoseRightLegTwistOutputReferenceWeight", 0.5f);
            SetYybVisualComparisonRunnerStaticField("_setHumanPoseRightLegTwistOutputReferenceMaxDelta", 0.01f);

            Assert.That(ApplyMainSceneRuntimeOverrides(fileManager), Is.True);
            Assert.That(fileManager.ShouldUseSetHumanPoseRightLegTwistOutputReference, Is.True);
            Assert.That(fileManager.setHumanPoseRightLegTwistOutputReferenceWeight, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fileManager.setHumanPoseRightLegTwistOutputReferenceMaxDelta, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);

            ClearYybVisualComparisonRunnerState("set-human-pose-right-leg-twist-output-cleanup");
        }

        [Test]
        public void Given_YybSideHairSilhouetteGuard_When_Applied_Then_ContractsOnlyHairChains()
        {
            Type guardType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidYybHairSilhouetteGuard");
            Assert.That(
                guardType,
                Is.Null,
                "Rejected side-hair guard must stay removed from the runtime surface; the accepted baseline keeps limb pose corrections only.");
        }

        [Test]
        public void YybMmdExportProductionPrefab_UsesAcceptedRuntimeVisualRecoveryDefaults()
        {
            AssertYybMmdExportClampMargin(
                "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku.prefab");
            AssertYybMmdExportRecoveryDefaults(
                "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku.prefab",
                expectedEnabled: true,
                expectedLimit: 0.1209f,
                expectedTrigger: 0.26f,
                expectedDebt: 0.08f,
                expectedHoldFrames: 3);
            AssertYybMmdExportDynamicToggleDefaults(
                "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku.prefab",
                expectedEnabled: true,
                expectedFootThreshold: 0.12f,
                expectedToeThreshold: 0.12f);
        }

        [Test]
        public void YybMmdExportManualReferencePrefab_StaysClampOnlyBaseline()
        {
            AssertYybMmdExportClampMargin(
                "Assets/_ManualReference/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_Prefab.prefab");
            AssertYybMmdExportRecoveryDefaults(
                "Assets/_ManualReference/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_Prefab.prefab",
                expectedEnabled: false,
                expectedLimit: 0.12f,
                expectedTrigger: 0.30f,
                expectedDebt: 0f,
                expectedHoldFrames: 0);
            AssertYybMmdExportDynamicToggleDefaults(
                "Assets/_ManualReference/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_Prefab.prefab",
                expectedEnabled: false,
                expectedFootThreshold: 0.12f,
                expectedToeThreshold: 0.12f);
        }

        [Test]
        public void Given_RuntimeMmdIkDeltaOverride_When_ApplyingToRecorder_Then_ChangesOnlyFootAndToeIkClamp()
        {
            var recorderObject = new GameObject("runtime override recorder");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.ClampMmdIkExportDeltaSpikes = true;
                recorder.MaxMmdCenterExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdFootIkExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdToeIkExportDeltaPerFrame = 0.11f;

                bool applied = ApplyMmdIkDeltaGuardRuntimeOverride(recorder, 0.12f);

                Assert.That(applied, Is.True);
                Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.True);
                Assert.That(recorder.MaxMmdCenterExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.MaxMmdFootIkExportDeltaPerFrame, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(recorder.MaxMmdToeIkExportDeltaPerFrame, Is.EqualTo(0.12f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        [Test]
        public void Given_RuntimeMmdIkDeltaRecoveryOverride_When_ApplyingToRecorder_Then_KeepsBaseClampAndSetsRecoveryWindow()
        {
            var recorderObject = new GameObject("runtime recovery override recorder");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.ClampMmdIkExportDeltaSpikes = true;
                recorder.MaxMmdCenterExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdFootIkExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdToeIkExportDeltaPerFrame = 0.11f;

                bool applied = ApplyMmdIkDeltaGuardRuntimeOverride(recorder, 0.12f, 0.30f);

                Assert.That(applied, Is.True);
                Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.True);
                Assert.That(recorder.MaxMmdCenterExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.MaxMmdFootIkExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.MaxMmdToeIkExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.UseMmdIkExportDeltaRecoveryLimit, Is.True);
                Assert.That(recorder.MmdIkExportDeltaRecoveryLimitPerFrame, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryTriggerPerFrame, Is.EqualTo(0.30f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        [Test]
        public void Given_RuntimeMmdIkDeltaRecoveryDebtOverride_When_ApplyingToRecorder_Then_SetsDebtRecoveryWindow()
        {
            var recorderObject = new GameObject("runtime recovery debt override recorder");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.ClampMmdIkExportDeltaSpikes = true;
                recorder.MaxMmdCenterExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdFootIkExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdToeIkExportDeltaPerFrame = 0.11f;

                bool applied = ApplyMmdIkDeltaGuardRuntimeOverride(recorder, 0.12099f, 0.26f, 0.08f);

                Assert.That(applied, Is.True);
                Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.True);
                Assert.That(recorder.MaxMmdCenterExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.MaxMmdFootIkExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.MaxMmdToeIkExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.UseMmdIkExportDeltaRecoveryLimit, Is.True);
                Assert.That(recorder.MmdIkExportDeltaRecoveryLimitPerFrame, Is.EqualTo(0.12099f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryTriggerPerFrame, Is.EqualTo(0.26f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame, Is.EqualTo(0.08f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        [Test]
        public void Given_RuntimeMmdIkDeltaRecoveryHoldOverride_When_ApplyingToRecorder_Then_SetsHoldWindow()
        {
            var recorderObject = new GameObject("runtime recovery hold override recorder");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.ClampMmdIkExportDeltaSpikes = true;
                recorder.MaxMmdCenterExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdFootIkExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdToeIkExportDeltaPerFrame = 0.11f;

                bool applied = ApplyMmdIkDeltaGuardRuntimeOverride(recorder, 0.1209f, 0.26f, 0.08f, 3);

                Assert.That(applied, Is.True);
                Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.True);
                Assert.That(recorder.MaxMmdCenterExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.MaxMmdFootIkExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.MaxMmdToeIkExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.UseMmdIkExportDeltaRecoveryLimit, Is.True);
                Assert.That(recorder.MmdIkExportDeltaRecoveryLimitPerFrame, Is.EqualTo(0.1209f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryTriggerPerFrame, Is.EqualTo(0.26f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryHoldFrames, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        [Test]
        public void MainScenes_PreserveRegressionSafeRetargetDefaultsForYybPlayback()
        {
            AssertRegressionSafeRetargetDefaults("Assets/_Project/Scene/Main_Auto.unity", expectedMovementScaleMultiplier: 1f);
            AssertMovingRootRetargetDefaults("Assets/_Project/Scene/Main_Recoding.unity", minMovementScaleMultiplier: 0.9f);
        }

        [Test]
        public void Given_ManualThumbOverrideSpreadExceedsSceneCap_When_ResolvingVisualLengthLimit_Then_KeepsConfiguredSmokeCap()
        {
            MethodInfo method = typeof(HumanoidThumbDeformationGuard).GetMethod(
                "ResolveManualOverrideMaxSpreadAngle",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, "High-risk manual thumb override must not relax the scene smoke-safe spread cap.");

            Assert.That((float)method.Invoke(null, new object[] { 50f, 52f }), Is.EqualTo(50f).Within(0.0001f));
            Assert.That((float)method.Invoke(null, new object[] { 54f, 52f }), Is.EqualTo(52f).Within(0.0001f));
            Assert.That((float)method.Invoke(null, new object[] { 50f, 48f }), Is.EqualTo(48f).Within(0.0001f));
            Assert.That((float)method.Invoke(null, new object[] { -1f, 52f }), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_ManualThumbProjectionRiskExceedsSmokeLimit_When_CheckingPreserveBypass_Then_BypassesManualReferencePreserve()
        {
            MethodInfo method = typeof(HumanoidThumbDeformationGuard).GetMethod(
                "ShouldBypassManualThumbProjectionPreserveForSmokeRisk",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, "Projection correction must bypass manual reference preserve when the current thumb projection already exceeds smoke risk.");

            Assert.That((bool)method.Invoke(null, new object[] { -0.008f, 0.358f, 0.5f }), Is.True);
            Assert.That((bool)method.Invoke(null, new object[] { 0.093f, 0.358f, 0.5f }), Is.False);
            Assert.That((bool)method.Invoke(null, new object[] { 0.505f, 0.358f, 0.5f }), Is.False);
        }

        [Test]
        public void MainSceneRootMotionPolicy_KeepsMainAutoStationaryAndMainRecordingMovingRootCarrier()
        {
            AssertSceneRootMotionPolicy(
                "Assets/_Project/Scene/Main_Auto.unity",
                expectedPreserveRetargetBodyPosition: true,
                expectedUseRetargetBodyPositionXZRootMotion: false,
                expectedUseEditorHumanoidRootTranslationReference: false,
                expectedClampRetargetHipsLocalPositionSpikes: false);
            AssertSceneRootMotionPolicy(
                "Assets/_Project/Scene/Main_Recoding.unity",
                expectedPreserveRetargetBodyPosition: false,
                expectedUseRetargetBodyPositionXZRootMotion: true,
                expectedUseEditorHumanoidRootTranslationReference: false,
                expectedClampRetargetHipsLocalPositionSpikes: true);
        }

        [Test]
        public void MainRecordingRootMotionPolicy_EnablesMovingRootCarrierForNaturalMotion()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Recoding.unity");

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, "Main_recoding scene must contain FBXVmdPipeline.");
            Assert.That(
                fileManager.MovementScaleMultiplier,
                Is.GreaterThanOrEqualTo(0.9f),
                "Main_Recoding must keep the natural moving-root carrier enabled for manual-style preview/export.");
            Assert.That(
                fileManager.ShouldUseRetargetBodyPositionXZRootMotion,
                Is.True,
                "Main_Recoding must preserve bodyPosition X/Z root motion instead of behaving like Main_Auto.");
            Assert.That(
                fileManager.ShouldUseEditorHumanoidRootTranslationReference,
                Is.False,
                "Main_Recoding must not add Humanoid RootT translation on top of bodyPosition X/Z root motion.");
            Assert.That(
                fileManager.MaxRetargetRootDeltaPerFrame,
                Is.EqualTo(0.006f).Within(0.0001f),
                "Main_Recoding keeps the spike guard as a fallback while moving-root recovery is validated.");
        }

        [Test]
        public void MainScenes_FreezeRootYAfterInitialGroundingForLivePlaybackStability()
        {
            AssertRootYFreezeAfterInitialGrounding("Assets/_Project/Scene/Main_Auto.unity");
            AssertRootYFreezeAfterInitialGrounding("Assets/_Project/Scene/Main_Recoding.unity");
        }

        [Test]
        public void MainScenes_KeepFinalIkFootGroundingExperimentDisabledByDefault()
        {
            AssertFinalIkFootGroundingDefaults("Assets/_Project/Scene/Main_Auto.unity");
            AssertFinalIkFootGroundingDefaults("Assets/_Project/Scene/Main_Recoding.unity");
        }

        [Test]
        public void Given_FinalIkFootGroundingExperimentEnabled_When_ConfiguringTarget_Then_UsesBipedGrounderWithoutVrik()
        {
            var managerObject = new GameObject("final ik foot grounding manager");
            var targetObject = new GameObject("final ik foot grounding target");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                SetField(manager, "enableFinalIkFootGroundingExperiment", true);
                SetField(manager, "finalIkFootGroundingWeight", 0.15f);
                SetField(manager, "finalIkFootGroundingMaxStep", 0.05f);
                SetField(manager, "finalIkFootGroundingFootRadius", 0.06f);
                SetField(manager, "finalIkFootGroundingPrediction", 0f);
                SetField(manager, "finalIkFootGroundingFootRotationWeight", 0f);
                SetField(manager, "finalIkFootGroundingPelvisDamper", 0.1f);

                InvokeFinalIkFootGroundingConfiguration(manager, targetObject);

                var bipedIk = targetObject.GetComponent<BipedIK>();
                var grounder = targetObject.GetComponent<GrounderBipedIK>();

                Assert.That(bipedIk, Is.Not.Null, "Final IK foot grounding experiment must use BipedIK as the narrow foot solver.");
                Assert.That(grounder, Is.Not.Null, "Final IK foot grounding experiment must add GrounderBipedIK for foot contact correction.");
                Assert.That(targetObject.GetComponent<VRIK>(), Is.Null, "Foot grounding experiment must not install VRIK, which would replace the whole retargeting solve.");
                Assert.That(grounder.ik, Is.SameAs(bipedIk));
                Assert.That(grounder.weight, Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(grounder.spineBend, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(grounder.solver.maxStep, Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(grounder.solver.footRadius, Is.EqualTo(0.06f).Within(0.0001f));
                Assert.That(grounder.solver.prediction, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(grounder.solver.footRotationWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(grounder.solver.pelvisDamper, Is.EqualTo(0.1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void Given_FinalIkFootGroundingExperimentWasEnabled_When_DisabledAndReconfigured_Then_DisablesAllFinalIkFootSolvers()
        {
            var managerObject = new GameObject("final ik foot grounding manager");
            var targetObject = new GameObject("final ik foot grounding target");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                SetField(manager, "enableFinalIkFootGroundingExperiment", true);
                SetField(manager, "finalIkFootGroundingWeight", 0.15f);

                InvokeFinalIkFootGroundingConfiguration(manager, targetObject);

                var bipedIk = targetObject.GetComponent<BipedIK>();
                var grounder = targetObject.GetComponent<GrounderBipedIK>();

                Assert.That(bipedIk, Is.Not.Null, "The enabled experiment should add the BipedIK solver before the OFF regression path is exercised.");
                Assert.That(grounder, Is.Not.Null, "The enabled experiment should add the GrounderBipedIK solver before the OFF regression path is exercised.");
                Assert.That(bipedIk.enabled, Is.True);
                Assert.That(grounder.enabled, Is.True);

                SetField(manager, "enableFinalIkFootGroundingExperiment", false);

                InvokeFinalIkFootGroundingConfiguration(manager, targetObject);

                Assert.That(grounder.enabled, Is.False, "OFF reconfiguration must disable GrounderBipedIK so it cannot alter the visual A/B baseline.");
                Assert.That(grounder.weight, Is.EqualTo(0f).Within(0.0001f), "OFF reconfiguration must zero the GrounderBipedIK master weight.");
                Assert.That(bipedIk.enabled, Is.False, "OFF reconfiguration must disable BipedIK as well; leaving it enabled can keep SolverManager fixTransforms active.");
                Assert.That(bipedIk.fixTransforms, Is.False, "OFF reconfiguration must make BipedIK transform fixing inert for clean OFF/ON A/B tests.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void Given_FinalIkFootGroundingRuntimeOverride_When_Disabled_Then_CleansExistingFootSolversForBaseline()
        {
            var managerObject = new GameObject("final ik runtime override manager");
            var targetObject = new GameObject("final ik runtime override target");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.targetCharacter = targetObject;
                var bipedIk = targetObject.AddComponent<BipedIK>();
                var grounder = targetObject.AddComponent<GrounderBipedIK>();
                grounder.ik = bipedIk;
                grounder.weight = 0.15f;
                bipedIk.enabled = true;
                bipedIk.fixTransforms = true;
                grounder.enabled = true;

                bool enabledApplied = ApplyFinalIkFootGroundingRuntimeOverride(manager, true);
                bool disabledApplied = ApplyFinalIkFootGroundingRuntimeOverride(manager, false);

                Assert.That(enabledApplied, Is.True);
                Assert.That(disabledApplied, Is.True);
                Assert.That(GetField<bool>(manager, "enableFinalIkFootGroundingExperiment"), Is.False);
                Assert.That(grounder.enabled, Is.False, "Explicit OFF runtime comparison must disable prior GrounderBipedIK state.");
                Assert.That(grounder.weight, Is.EqualTo(0f).Within(0.0001f), "Explicit OFF runtime comparison must zero GrounderBipedIK influence.");
                Assert.That(bipedIk.enabled, Is.False, "Explicit OFF runtime comparison must disable prior BipedIK state.");
                Assert.That(bipedIk.fixTransforms, Is.False, "Explicit OFF runtime comparison must make BipedIK fixTransforms inert.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorFootLocalRotationRuntimeOverride_When_Toggled_Then_OnlyChangesReferenceSwitchAndWeight()
        {
            var managerObject = new GameObject("manual animator foot local rotation runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorFootLocalRotationReference = false;
                manager.manualAnimatorFootLocalRotationReferenceWeight = 0f;

                bool enabledApplied = ApplyManualAnimatorFootLocalRotationRuntimeOverride(manager, true);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorFootLocalRotationReference, Is.True);
                Assert.That(manager.manualAnimatorFootLocalRotationReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, "Foot/toe runtime candidate must not re-enable the rejected hips/local body pose copy path.");
                Assert.That(manager.ShouldUseManualAnimatorFootHeightGroundingReference, Is.False, "Foot/toe runtime candidate must not change the grounding reference path.");

                bool disabledApplied = ApplyManualAnimatorFootLocalRotationRuntimeOverride(manager, false);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorFootLocalRotationReference, Is.False);
                Assert.That(manager.manualAnimatorFootLocalRotationReferenceWeight, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorFullBodyPoseRuntimeOverride_When_Toggled_Then_OnlyChangesFullBodyReferenceSwitch()
        {
            var managerObject = new GameObject("manual animator full body pose runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.ShouldUseManualAnimatorHipsLocalPositionReference = false;
                manager.ShouldUseManualAnimatorFootHeightGroundingReference = false;
                manager.ShouldUseManualAnimatorFootLocalRotationReference = false;

                bool enabledApplied = ApplyManualAnimatorFullBodyPoseRuntimeOverride(manager, true);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, "Full-body pose candidate must not re-enable the rejected hips localPosition copy path.");
                Assert.That(manager.ShouldUseManualAnimatorFootHeightGroundingReference, Is.False, "Full-body pose candidate must not change the grounding reference path.");
                Assert.That(manager.ShouldUseManualAnimatorFootLocalRotationReference, Is.False, "Full-body pose candidate must not implicitly enable the leg-chain localRotation candidate.");

                bool disabledApplied = ApplyManualAnimatorFullBodyPoseRuntimeOverride(manager, false);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorFullBodyPoseRuntimeOverride_When_CustomWeightProvided_Then_ClampsWeight()
        {
            var managerObject = new GameObject("manual animator full body pose runtime override weight manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplyManualAnimatorFullBodyPoseRuntimeOverride(manager, true, 0.35f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True);
                Assert.That(
                    GetField<float>(manager, "manualAnimatorFullBodyPoseReferenceWeight"),
                    Is.EqualTo(0.35f).Within(0.0001f));

                bool clampedApplied = ApplyManualAnimatorFullBodyPoseRuntimeOverride(manager, true, 2f);

                Assert.That(clampedApplied, Is.True);
                Assert.That(
                    GetField<float>(manager, "manualAnimatorFullBodyPoseReferenceWeight"),
                    Is.EqualTo(1f).Within(0.0001f));

                bool disabledApplied = ApplyManualAnimatorFullBodyPoseRuntimeOverride(manager, false, 0.35f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(
                    GetField<float>(manager, "manualAnimatorFullBodyPoseReferenceWeight"),
                    Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorBodyRotationRuntimeOverride_When_Toggled_Then_OnlyChangesBodyRotationSwitch()
        {
            var managerObject = new GameObject("manual animator body rotation runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorBodyRotationReference = false;
                manager.manualAnimatorBodyRotationReferenceWeight = 0f;
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.ShouldUseManualAnimatorHipsLocalPositionReference = false;
                manager.ShouldUseManualAnimatorFootHeightGroundingReference = false;
                manager.ShouldUseManualAnimatorFootLocalRotationReference = false;

                bool enabledApplied = ApplyManualAnimatorBodyRotationRuntimeOverride(manager, true);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorBodyRotationReference, Is.True);
                Assert.That(manager.manualAnimatorBodyRotationReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False, "Body rotation candidate must not replace full-body muscles.");
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, "Body rotation candidate must not re-enable the rejected hips localPosition copy path.");
                Assert.That(manager.ShouldUseManualAnimatorFootHeightGroundingReference, Is.False, "Body rotation candidate must not change the grounding reference path.");
                Assert.That(manager.ShouldUseManualAnimatorFootLocalRotationReference, Is.False, "Body rotation candidate must not implicitly enable the leg-chain localRotation candidate.");

                bool disabledApplied = ApplyManualAnimatorBodyRotationRuntimeOverride(manager, false);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorBodyRotationReference, Is.False);
                Assert.That(manager.manualAnimatorBodyRotationReferenceWeight, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorBodyRotationRuntimeOverride_When_CustomWeightProvided_Then_ClampsWeight()
        {
            var managerObject = new GameObject("manual animator body rotation runtime override weight manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplyManualAnimatorBodyRotationRuntimeOverride(manager, true, 0.35f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorBodyRotationReference, Is.True);
                Assert.That(manager.manualAnimatorBodyRotationReferenceWeight, Is.EqualTo(0.35f).Within(0.0001f));

                bool clampedApplied = ApplyManualAnimatorBodyRotationRuntimeOverride(manager, true, 2f);

                Assert.That(clampedApplied, Is.True);
                Assert.That(manager.manualAnimatorBodyRotationReferenceWeight, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_RetargetPoseVisualSpikeSmoothingRuntimeOverride_When_Applied_Then_OnlyChangesSmoothingSettings()
        {
            var managerObject = new GameObject("retarget pose visual spike smoothing runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.smoothRetargetPoseOnVisualStepSpike = true;
                manager.RetargetPoseVisualSpikeCurrentWeight = 0.65f;
                manager.RetargetPoseVisualSpikeForearmStretchClampMaxOffset = 0f;
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.enableYybArmSwingLimitCorrection = false;
                manager.usePostSetHumanPoseRightEndpointPositionReference = false;

                bool disabledApplied = ApplyRetargetPoseVisualSpikeSmoothingRuntimeOverride(
                    manager,
                    enabled: false,
                    currentWeight: 1.5f,
                    forearmStretchClampMaxOffset: 2f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.smoothRetargetPoseOnVisualStepSpike, Is.False);
                Assert.That(manager.RetargetPoseVisualSpikeCurrentWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.RetargetPoseVisualSpikeForearmStretchClampMaxOffset, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False, "Visual spike smoothing candidate must not replace full-body muscles.");
                Assert.That(manager.enableYybArmSwingLimitCorrection, Is.False, "Visual spike smoothing candidate must not implicitly change the arm swing limiter.");
                Assert.That(manager.usePostSetHumanPoseRightEndpointPositionReference, Is.False, "Visual spike smoothing candidate must not enable endpoint compensation.");

                bool enabledApplied = ApplyRetargetPoseVisualSpikeSmoothingRuntimeOverride(
                    manager,
                    enabled: true,
                    currentWeight: 0.05f,
                    forearmStretchClampMaxOffset: 0.15f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.smoothRetargetPoseOnVisualStepSpike, Is.True);
                Assert.That(manager.RetargetPoseVisualSpikeCurrentWeight, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(manager.RetargetPoseVisualSpikeForearmStretchClampMaxOffset, Is.EqualTo(0.15f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorHandLocalRotationRuntimeOverride_When_Toggled_Then_OnlyChangesHandLocalSwitch()
        {
            var managerObject = new GameObject("manual animator hand local rotation runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.useManualAnimatorHandLocalRotationReference = false;
                manager.useManualAnimatorThumbLocalRotationReference = false;
                manager.useManualAnimatorHandPalmFrameReference = false;
                manager.manualAnimatorHandPalmFrameWeight = 0f;
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.ShouldUseManualAnimatorFingerPoseReference = false;

                bool enabledApplied = ApplyManualAnimatorHandLocalRotationRuntimeOverride(manager, true);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.useManualAnimatorHandLocalRotationReference, Is.True);
                Assert.That(manager.useManualAnimatorThumbLocalRotationReference, Is.False, "Hand local candidate must not implicitly enable thumb local rotation reference.");
                Assert.That(manager.useManualAnimatorHandPalmFrameReference, Is.False, "Hand local candidate must not implicitly enable palm-frame reference.");
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False, "Hand local candidate must not replace full-body muscles.");
                Assert.That(manager.ShouldUseManualAnimatorFingerPoseReference, Is.False, "Hand local candidate must not enable finger curl copy.");

                bool disabledApplied = ApplyManualAnimatorHandLocalRotationRuntimeOverride(manager, false);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.useManualAnimatorHandLocalRotationReference, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorThumbLocalRotationRuntimeOverride_When_Toggled_Then_OnlyChangesThumbLocalSwitch()
        {
            var managerObject = new GameObject("manual animator thumb local rotation runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.useManualAnimatorThumbLocalRotationReference = false;
                manager.useManualAnimatorHandLocalRotationReference = false;
                manager.useManualAnimatorHandPalmFrameReference = false;
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.ShouldUseManualAnimatorFingerPoseReference = false;

                bool enabledApplied = ApplyManualAnimatorThumbLocalRotationRuntimeOverride(manager, true);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.useManualAnimatorThumbLocalRotationReference, Is.True);
                Assert.That(manager.useManualAnimatorHandLocalRotationReference, Is.False, "Thumb candidate must not implicitly enable whole-hand local rotation reference.");
                Assert.That(manager.useManualAnimatorHandPalmFrameReference, Is.False, "Thumb candidate must not implicitly enable palm-frame reference.");
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False, "Thumb candidate must not replace full-body muscles.");
                Assert.That(manager.ShouldUseManualAnimatorFingerPoseReference, Is.False, "Thumb candidate must not enable finger curl copy.");

                bool disabledApplied = ApplyManualAnimatorThumbLocalRotationRuntimeOverride(manager, false);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.useManualAnimatorThumbLocalRotationReference, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorHandPalmFrameRuntimeOverride_When_CustomWeightProvided_Then_ClampsWeight()
        {
            var managerObject = new GameObject("manual animator palm frame runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.useManualAnimatorHandPalmFrameReference = false;
                manager.manualAnimatorHandPalmFrameWeight = 0f;
                manager.useManualAnimatorHandLocalRotationReference = false;
                manager.useManualAnimatorThumbLocalRotationReference = false;
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.ShouldUseManualAnimatorFingerPoseReference = false;

                bool enabledApplied = ApplyManualAnimatorHandPalmFrameRuntimeOverride(manager, true, 0.35f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.useManualAnimatorHandPalmFrameReference, Is.True);
                Assert.That(manager.manualAnimatorHandPalmFrameWeight, Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(manager.useManualAnimatorHandLocalRotationReference, Is.False, "Palm-frame candidate must not implicitly enable whole-hand local rotation reference.");
                Assert.That(manager.useManualAnimatorThumbLocalRotationReference, Is.False, "Palm-frame candidate must not implicitly enable thumb local rotation reference.");
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False, "Palm-frame candidate must not replace full-body muscles.");
                Assert.That(manager.ShouldUseManualAnimatorFingerPoseReference, Is.False, "Palm-frame candidate must not enable finger curl copy.");

                bool clampedApplied = ApplyManualAnimatorHandPalmFrameRuntimeOverride(manager, true, 2f);

                Assert.That(clampedApplied, Is.True);
                Assert.That(manager.manualAnimatorHandPalmFrameWeight, Is.EqualTo(1f).Within(0.0001f));

                bool disabledApplied = ApplyManualAnimatorHandPalmFrameRuntimeOverride(manager, false, 0.35f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.useManualAnimatorHandPalmFrameReference, Is.False);
                Assert.That(manager.manualAnimatorHandPalmFrameWeight, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_RetargetArmStretchClampRuntimeOverride_When_Enabled_Then_AlsoClampsTargetGuardStretch()
        {
            var managerObject = new GameObject("retarget arm stretch clamp runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.enableAnatomicalArmGuard = false;
                manager.clampRetargetArmStretchMuscles = false;
                manager.targetGuardClampAnatomicalArmMuscles = false;
                manager.targetGuardClampArmStretchMuscles = false;
                manager.ArmStretchMuscleLimit = 0f;

                bool enabledApplied = ApplyRetargetArmStretchClampRuntimeOverride(manager, true, 0.75f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.enableAnatomicalArmGuard, Is.True);
                Assert.That(manager.clampRetargetArmStretchMuscles, Is.True);
                Assert.That(manager.targetGuardClampAnatomicalArmMuscles, Is.True);
                Assert.That(manager.targetGuardClampArmStretchMuscles, Is.True);
                Assert.That(manager.ArmStretchMuscleLimit, Is.EqualTo(0.5f).Within(0.0001f));

                bool disabledApplied = ApplyRetargetArmStretchClampRuntimeOverride(manager, false, 0.5f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.clampRetargetArmStretchMuscles, Is.False);
                Assert.That(manager.targetGuardClampAnatomicalArmMuscles, Is.False);
                Assert.That(manager.targetGuardClampArmStretchMuscles, Is.False);
                Assert.That(manager.ArmStretchMuscleLimit, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_YybArmSwingLimitRuntimeOverride_When_Toggled_Then_OnlyChangesSwingLimitSettings()
        {
            var managerObject = new GameObject("yyb arm swing limit runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.enableYybArmSwingLimitCorrection = false;
                manager.YybArmSwingLimitWeight = 0f;
                manager.YybArmSwingMaxDownDot = 0.68f;
                manager.YybArmSwingMinHandHorizontalRatio = 0.05f;
                manager.YybArmSwingMaxHandBelowShoulderRatio = 0.75f;
                manager.ShouldUseManualAnimatorBodyRotationReference = false;
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.ShouldUseManualAnimatorHipsLocalPositionReference = false;

                bool enabledApplied = ApplyYybArmSwingLimitRuntimeOverride(
                    manager,
                    true,
                    0.5f,
                    0.42f,
                    0.07f,
                    0.6f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.enableYybArmSwingLimitCorrection, Is.True);
                Assert.That(manager.YybArmSwingLimitWeight, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(manager.YybArmSwingMaxDownDot, Is.EqualTo(0.42f).Within(0.0001f));
                Assert.That(manager.YybArmSwingMinHandHorizontalRatio, Is.EqualTo(0.07f).Within(0.0001f));
                Assert.That(manager.YybArmSwingMaxHandBelowShoulderRatio, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(manager.ShouldUseManualAnimatorBodyRotationReference, Is.False, "Arm swing candidate must not implicitly enable bodyRotation reference.");
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False, "Arm swing candidate must not replace full-body muscles.");
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, "Arm swing candidate must not re-enable the rejected hips localPosition copy path.");

                bool disabledApplied = ApplyYybArmSwingLimitRuntimeOverride(
                    manager,
                    false,
                    0.5f,
                    0.42f,
                    0.07f,
                    0.6f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.enableYybArmSwingLimitCorrection, Is.False);
                Assert.That(manager.YybArmSwingLimitWeight, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_YybArmSwingLimitRuntimeOverride_When_HorizontalReachProvided_Then_ClampsReachSettings()
        {
            var managerObject = new GameObject("yyb arm swing horizontal reach runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                FieldInfo reachWeightField = typeof(FBXVmdPipeline).GetField("YybArmSwingHorizontalReachLimitWeight");
                FieldInfo maxReachField = typeof(FBXVmdPipeline).GetField("YybArmSwingMaxHandHorizontalReachRatio");
                Assert.That(reachWeightField, Is.Not.Null, "YYB arm swing runtime candidate must expose horizontal reach limit weight.");
                Assert.That(maxReachField, Is.Not.Null, "YYB arm swing runtime candidate must expose a max horizontal reach ratio.");

                Type runnerType = Type.GetType(
                    "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
                Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

                MethodInfo method = runnerType.GetMethod(
                    "ApplyYybArmSwingLimitRuntimeOverride",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: new[]
                    {
                        typeof(FBXVmdPipeline),
                        typeof(bool),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float)
                    },
                    modifiers: null);

                Assert.That(method, Is.Not.Null, "YYB arm swing override must accept horizontal reach clamp settings for silhouette-preserving candidates.");

                bool enabledApplied = (bool)method.Invoke(
                    null,
                    new object[]
                    {
                        manager,
                        true,
                        0.5f,
                        0.42f,
                        0.07f,
                        0.6f,
                        1.5f,
                        -0.2f
                    });

                Assert.That(enabledApplied, Is.True);
                Assert.That((float)reachWeightField.GetValue(manager), Is.EqualTo(1f).Within(0.0001f));
                Assert.That((float)maxReachField.GetValue(manager), Is.EqualTo(0f).Within(0.0001f));

                bool disabledApplied = (bool)method.Invoke(
                    null,
                    new object[]
                    {
                        manager,
                        false,
                        0.5f,
                        0.42f,
                        0.07f,
                        0.6f,
                        0.75f,
                        0.55f
                    });

                Assert.That(disabledApplied, Is.True);
                Assert.That((float)reachWeightField.GetValue(manager), Is.EqualTo(0f).Within(0.0001f));
                Assert.That((float)maxReachField.GetValue(manager), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_YybArmSwingLimitRuntimeOverride_When_HorizontalReachBelowShoulderGateProvided_Then_ClampsGateSeparately()
        {
            var managerObject = new GameObject("yyb arm swing horizontal reach below shoulder gate manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                FieldInfo gateField = typeof(FBXVmdPipeline).GetField(
                    "YybArmSwingHorizontalReachMaxHandBelowShoulderRatio");
                Assert.That(gateField, Is.Not.Null, "YYB arm swing runtime candidate must expose a horizontal-reach-only below-shoulder gate.");

                Type runnerType = Type.GetType(
                    "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
                Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

                MethodInfo method = runnerType.GetMethod(
                    "ApplyYybArmSwingLimitRuntimeOverride",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: new[]
                    {
                        typeof(FBXVmdPipeline),
                        typeof(bool),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float)
                    },
                    modifiers: null);

                Assert.That(method, Is.Not.Null, "YYB arm swing override must accept a horizontal-reach-only below-shoulder gate.");

                bool enabledApplied = (bool)method.Invoke(
                    null,
                    new object[]
                    {
                        manager,
                        true,
                        0.6f,
                        0.75f,
                        0.05f,
                        1.5f,
                        1f,
                        0.08f,
                        0.95f
                    });

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.YybArmSwingMaxHandBelowShoulderRatio, Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That((float)gateField.GetValue(manager), Is.EqualTo(0.95f).Within(0.0001f));

                bool disabledApplied = (bool)method.Invoke(
                    null,
                    new object[]
                    {
                        manager,
                        false,
                        0.6f,
                        0.75f,
                        0.05f,
                        1.5f,
                        1f,
                        0.08f,
                        0.95f
                    });

                Assert.That(disabledApplied, Is.True);
                Assert.That((float)gateField.GetValue(manager), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_YybArmSwingLimitRuntimeOverride_When_HorizontalReachElbowGuardProvided_Then_ClampsGate()
        {
            var managerObject = new GameObject("yyb arm swing horizontal reach elbow guard manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                FieldInfo elbowGuardField = typeof(FBXVmdPipeline).GetField(
                    "YybArmSwingHorizontalReachMinElbowAngleAfterApply");
                Assert.That(elbowGuardField, Is.Not.Null, "YYB arm swing runtime candidate must expose a post-horizontal-reach elbow saturation guard.");

                Type runnerType = Type.GetType(
                    "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
                Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

                MethodInfo method = runnerType.GetMethod(
                    "ApplyYybArmSwingLimitRuntimeOverride",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: new[]
                    {
                        typeof(FBXVmdPipeline),
                        typeof(bool),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(float)
                    },
                    modifiers: null);

                Assert.That(method, Is.Not.Null, "YYB arm swing override must accept a row-local horizontal reach elbow guard.");

                bool enabledApplied = (bool)method.Invoke(
                    null,
                    new object[]
                    {
                        manager,
                        true,
                        0.6f,
                        0.75f,
                        0.05f,
                        1.5f,
                        1f,
                        0.08f,
                        0.6f,
                        200f
                    });

                Assert.That(enabledApplied, Is.True);
                Assert.That((float)elbowGuardField.GetValue(manager), Is.EqualTo(180f).Within(0.0001f));

                bool disabledApplied = (bool)method.Invoke(
                    null,
                    new object[]
                    {
                        manager,
                        false,
                        0.6f,
                        0.75f,
                        0.05f,
                        1.5f,
                        1f,
                        0.08f,
                        0.6f,
                        12f
                    });

                Assert.That(disabledApplied, Is.True);
                Assert.That((float)elbowGuardField.GetValue(manager), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_YybArmDirectionRetargetRuntimeOverride_When_Toggled_Then_OnlyChangesDirectionSettings()
        {
            var managerObject = new GameObject("yyb arm direction runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.enableYybArmDirectionRetargetCorrection = false;
                manager.YybArmDirectionUpperArmWeight = 0f;
                manager.YybArmDirectionForearmWeight = 0f;
                manager.YybArmDirectionUpperArmMaxDegrees = 0f;
                manager.YybArmDirectionForearmMaxDegrees = 0f;
                manager.enableYybArmSwingLimitCorrection = false;
                manager.ShouldUseManualAnimatorBodyRotationReference = false;
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.ShouldUseManualAnimatorHipsLocalPositionReference = false;

                bool enabledApplied = ApplyYybArmDirectionRetargetRuntimeOverride(
                    manager,
                    true,
                    upperArmWeight: 0.4f,
                    forearmWeight: 0.55f,
                    upperArmMaxDegrees: 22f,
                    forearmMaxDegrees: 35f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.enableYybArmDirectionRetargetCorrection, Is.True);
                Assert.That(manager.YybArmDirectionUpperArmWeight, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(manager.YybArmDirectionForearmWeight, Is.EqualTo(0.55f).Within(0.0001f));
                Assert.That(manager.YybArmDirectionUpperArmMaxDegrees, Is.EqualTo(22f).Within(0.0001f));
                Assert.That(manager.YybArmDirectionForearmMaxDegrees, Is.EqualTo(35f).Within(0.0001f));
                Assert.That(manager.enableYybArmSwingLimitCorrection, Is.False, "Arm direction candidate must not implicitly enable the swing limiter.");
                Assert.That(manager.ShouldUseManualAnimatorBodyRotationReference, Is.False, "Arm direction candidate must not implicitly enable bodyRotation reference.");
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False, "Arm direction candidate must not replace full-body muscles.");
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, "Arm direction candidate must not re-enable the rejected hips localPosition copy path.");

                bool clampedApplied = ApplyYybArmDirectionRetargetRuntimeOverride(
                    manager,
                    true,
                    upperArmWeight: 1.5f,
                    forearmWeight: -0.5f,
                    upperArmMaxDegrees: 150f,
                    forearmMaxDegrees: -8f);

                Assert.That(clampedApplied, Is.True);
                Assert.That(manager.YybArmDirectionUpperArmWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.YybArmDirectionForearmWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.YybArmDirectionUpperArmMaxDegrees, Is.EqualTo(120f).Within(0.0001f));
                Assert.That(manager.YybArmDirectionForearmMaxDegrees, Is.EqualTo(0f).Within(0.0001f));

                bool disabledApplied = ApplyYybArmDirectionRetargetRuntimeOverride(
                    manager,
                    false,
                    upperArmWeight: 0.4f,
                    forearmWeight: 0.55f,
                    upperArmMaxDegrees: 22f,
                    forearmMaxDegrees: 35f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.enableYybArmDirectionRetargetCorrection, Is.False);
                Assert.That(manager.YybArmDirectionUpperArmWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.YybArmDirectionForearmWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.YybArmDirectionUpperArmMaxDegrees, Is.EqualTo(22f).Within(0.0001f));
                Assert.That(manager.YybArmDirectionForearmMaxDegrees, Is.EqualTo(35f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_YybArmDirectionRetargetRuntimeOverride_When_SideScalesProvided_Then_ClampsSideScales()
        {
            var managerObject = new GameObject("yyb arm direction side scale manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplyYybArmDirectionRetargetRuntimeOverride(
                    manager,
                    true,
                    upperArmWeight: 0.4f,
                    forearmWeight: 0.55f,
                    upperArmMaxDegrees: 22f,
                    forearmMaxDegrees: 35f,
                    leftSideWeightScale: -0.5f,
                    rightSideWeightScale: 1.25f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(ReadFBXVmdPipelineFloat(manager, "YybArmDirectionLeftSideWeightScale"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFBXVmdPipelineFloat(manager, "YybArmDirectionRightSideWeightScale"), Is.EqualTo(1f).Within(0.0001f));

                bool disabledApplied = ApplyYybArmDirectionRetargetRuntimeOverride(
                    manager,
                    false,
                    upperArmWeight: 0.4f,
                    forearmWeight: 0.55f,
                    upperArmMaxDegrees: 22f,
                    forearmMaxDegrees: 35f,
                    leftSideWeightScale: 0.7f,
                    rightSideWeightScale: 0.8f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(ReadFBXVmdPipelineFloat(manager, "YybArmDirectionLeftSideWeightScale"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFBXVmdPipelineFloat(manager, "YybArmDirectionRightSideWeightScale"), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ArmDirectionGuardGhostAnimatorDestroyed_When_LateUpdateRuns_Then_DisablesWithoutMissingReference()
        {
            var ghostObject = new GameObject("destroyed ghost animator");
            var targetObject = new GameObject("target arm direction guard");
            try
            {
                var ghostAnimator = ghostObject.AddComponent<Animator>();
                var targetAnimator = targetObject.AddComponent<Animator>();
                var guard = targetObject.AddComponent<HumanoidArmDirectionRetargetGuard>();
                guard.enableDirectionRetarget = true;
                guard.enabled = true;

                SetField(guard, "_ghostAnimator", ghostAnimator);
                SetField(guard, "_targetAnimator", targetAnimator);
                SetField(guard, "_configured", true);
                AddArmDirectionRetargetSegment(
                    guard,
                    HumanBodyBones.LeftUpperArm,
                    HumanBodyBones.LeftLowerArm);

                UnityEngine.Object.DestroyImmediate(ghostObject);

                Assert.DoesNotThrow(() => InvokeInstance(guard, "LateUpdate"));
                Assert.That(guard.enabled, Is.False);
                Assert.That(GetField<bool>(guard, "_configured"), Is.False);
            }
            finally
            {
                if (targetObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(targetObject);
                }

                if (ghostObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(ghostObject);
                }
            }
        }

        [Test]
        public void Given_YybArmSleeveAnchorRuntimeOverride_When_Toggled_Then_OnlyChangesSleeveAnchorSettings()
        {
            var managerObject = new GameObject("yyb arm sleeve anchor runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.enableYybArmSleeveAnchorCorrection = false;
                manager.YybArmSleeveAnchorInfluence = 0f;
                manager.YybArmShoulderCapAnchorInfluence = 0f;
                manager.YybArmSleeveAnchorMaxDegrees = 0f;
                manager.enableYybArmDirectionRetargetCorrection = false;
                manager.enableYybArmSwingLimitCorrection = false;
                manager.ShouldUseManualAnimatorBodyRotationReference = false;
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.ShouldUseManualAnimatorHipsLocalPositionReference = false;

                bool enabledApplied = ApplyYybArmSleeveAnchorRuntimeOverride(
                    manager,
                    true,
                    sleeveInfluence: 0.45f,
                    shoulderCapInfluence: 0.2f,
                    maxDegrees: 42f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.enableYybArmSleeveAnchorCorrection, Is.True);
                Assert.That(manager.YybArmSleeveAnchorInfluence, Is.EqualTo(0.45f).Within(0.0001f));
                Assert.That(manager.YybArmShoulderCapAnchorInfluence, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(manager.YybArmSleeveAnchorMaxDegrees, Is.EqualTo(42f).Within(0.0001f));
                Assert.That(manager.enableYybArmDirectionRetargetCorrection, Is.False, "Sleeve anchor candidate must not implicitly enable arm direction retarget.");
                Assert.That(manager.enableYybArmSwingLimitCorrection, Is.False, "Sleeve anchor candidate must not implicitly enable the swing limiter.");
                Assert.That(manager.ShouldUseManualAnimatorBodyRotationReference, Is.False, "Sleeve anchor candidate must not implicitly enable bodyRotation reference.");
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False, "Sleeve anchor candidate must not replace full-body muscles.");
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, "Sleeve anchor candidate must not re-enable the rejected hips localPosition copy path.");

                bool clampedApplied = ApplyYybArmSleeveAnchorRuntimeOverride(
                    manager,
                    true,
                    sleeveInfluence: 1.5f,
                    shoulderCapInfluence: -0.5f,
                    maxDegrees: 150f);

                Assert.That(clampedApplied, Is.True);
                Assert.That(manager.YybArmSleeveAnchorInfluence, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.YybArmShoulderCapAnchorInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.YybArmSleeveAnchorMaxDegrees, Is.EqualTo(120f).Within(0.0001f));

                bool disabledApplied = ApplyYybArmSleeveAnchorRuntimeOverride(
                    manager,
                    false,
                    sleeveInfluence: 0.45f,
                    shoulderCapInfluence: 0.2f,
                    maxDegrees: 42f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.enableYybArmSleeveAnchorCorrection, Is.False);
                Assert.That(manager.YybArmSleeveAnchorInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.YybArmShoulderCapAnchorInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.YybArmSleeveAnchorMaxDegrees, Is.EqualTo(42f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_YybArmVisualTwistRuntimeOverride_When_Toggled_Then_OnlyChangesVisualTwistSettings()
        {
            var managerObject = new GameObject("yyb arm visual twist runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.enableYybArmVisualTwistCorrection = false;
                manager.YybArmVisualUpperArmInfluence = 0f;
                manager.YybArmVisualForearmInfluence = 0f;
                manager.YybArmVisualUpperArmMaxDegrees = 0f;
                manager.YybArmVisualForearmMaxDegrees = 0f;
                manager.enableYybArmDirectionRetargetCorrection = false;
                manager.enableYybArmSwingLimitCorrection = false;
                manager.ShouldUseManualAnimatorBodyRotationReference = false;
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.ShouldUseManualAnimatorHipsLocalPositionReference = false;

                bool enabledApplied = ApplyYybArmVisualTwistRuntimeOverride(
                    manager,
                    true,
                    upperArmInfluence: 0.25f,
                    forearmInfluence: 0.6f,
                    upperArmMaxDegrees: 30f,
                    forearmMaxDegrees: 50f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.enableYybArmVisualTwistCorrection, Is.True);
                Assert.That(manager.YybArmVisualUpperArmInfluence, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(manager.YybArmVisualForearmInfluence, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(manager.YybArmVisualUpperArmMaxDegrees, Is.EqualTo(30f).Within(0.0001f));
                Assert.That(manager.YybArmVisualForearmMaxDegrees, Is.EqualTo(50f).Within(0.0001f));
                Assert.That(manager.enableYybArmDirectionRetargetCorrection, Is.False, "Visual twist candidate must not implicitly enable arm direction retarget.");
                Assert.That(manager.enableYybArmSwingLimitCorrection, Is.False, "Visual twist candidate must not implicitly enable the swing limiter.");
                Assert.That(manager.ShouldUseManualAnimatorBodyRotationReference, Is.False, "Visual twist candidate must not implicitly enable bodyRotation reference.");
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False, "Visual twist candidate must not replace full-body muscles.");
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, "Visual twist candidate must not re-enable the rejected hips localPosition copy path.");

                bool clampedApplied = ApplyYybArmVisualTwistRuntimeOverride(
                    manager,
                    true,
                    upperArmInfluence: 1.5f,
                    forearmInfluence: -0.5f,
                    upperArmMaxDegrees: 150f,
                    forearmMaxDegrees: -8f);

                Assert.That(clampedApplied, Is.True);
                Assert.That(manager.YybArmVisualUpperArmInfluence, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(manager.YybArmVisualForearmInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.YybArmVisualUpperArmMaxDegrees, Is.EqualTo(120f).Within(0.0001f));
                Assert.That(manager.YybArmVisualForearmMaxDegrees, Is.EqualTo(0f).Within(0.0001f));

                bool disabledApplied = ApplyYybArmVisualTwistRuntimeOverride(
                    manager,
                    false,
                    upperArmInfluence: 0.25f,
                    forearmInfluence: 0.6f,
                    upperArmMaxDegrees: 30f,
                    forearmMaxDegrees: 50f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.enableYybArmVisualTwistCorrection, Is.False);
                Assert.That(manager.YybArmVisualUpperArmInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.YybArmVisualForearmInfluence, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.YybArmVisualUpperArmMaxDegrees, Is.EqualTo(30f).Within(0.0001f));
                Assert.That(manager.YybArmVisualForearmMaxDegrees, Is.EqualTo(50f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorBipedIkFootPositionRuntimeOverride_When_Toggled_Then_OnlyChangesFootIkSwitchAndCaps()
        {
            var managerObject = new GameObject("manual animator biped ik foot position runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.useManualAnimatorBipedIkFootPositionReference = false;
                manager.manualAnimatorBipedIkFootPositionReferenceWeight = 0f;
                manager.manualAnimatorBipedIkFootPositionReferenceMaxOffset = 0f;

                bool enabledApplied = ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(manager, true);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.True);
                Assert.That(manager.manualAnimatorBipedIkFootPositionReferenceWeight, Is.EqualTo(0.65f).Within(0.0001f));
                Assert.That(manager.manualAnimatorBipedIkFootPositionReferenceMaxOffset, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(manager.ShouldUseManualAnimatorFootLocalRotationReference, Is.False, "BipedIK foot position candidate must not implicitly enable the leg-chain localRotation candidate.");
                Assert.That(manager.ShouldUseManualAnimatorFootHeightGroundingReference, Is.False, "BipedIK foot position candidate must not change the grounding reference path.");
                Assert.That(manager.enableFinalIkFootGroundingExperiment, Is.False, "BipedIK foot position candidate must not enable GrounderBipedIK.");

                bool disabledApplied = ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(manager, false);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False);
                Assert.That(manager.manualAnimatorBipedIkFootPositionReferenceWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.manualAnimatorBipedIkFootPositionReferenceMaxOffset, Is.EqualTo(0.12f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorBipedIkFootPositionRuntimeOverride_When_CustomWeightAndCapProvided_Then_UsesCustomCandidateValues()
        {
            var managerObject = new GameObject("manual animator biped ik foot position custom runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.useManualAnimatorBipedIkFootPositionReference = false;
                manager.manualAnimatorBipedIkFootPositionReferenceWeight = 0f;
                manager.manualAnimatorBipedIkFootPositionReferenceMaxOffset = 0f;

                bool enabledApplied = ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(
                    manager,
                    true,
                    weight: 0.2f,
                    maxOffset: 0.04f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.True);
                Assert.That(manager.manualAnimatorBipedIkFootPositionReferenceWeight, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(manager.manualAnimatorBipedIkFootPositionReferenceMaxOffset, Is.EqualTo(0.04f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_PostSetHumanPoseEndpointRuntimeOverride_When_Applied_Then_OnlyChangesEndpointClampSwitchAndCaps()
        {
            var managerObject = new GameObject("post set human pose endpoint runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    true,
                    weight: 0.75f,
                    maxOffset: 0.035f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.True);
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceWeight"), Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceMaxOffset"), Is.EqualTo(0.035f).Within(0.0001f));
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False, "Post-SetHumanPose endpoint candidate must not enable the late BipedIK candidate.");
                Assert.That(manager.enableFinalIkFootGroundingExperiment, Is.False, "Post-SetHumanPose endpoint candidate must not enable GrounderBipedIK.");

                bool disabledApplied = ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    false,
                    weight: 0.75f,
                    maxOffset: 0.035f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.False);
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceWeight"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceMaxOffset"), Is.EqualTo(0.035f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_PostSetHumanPoseEndpointPositiveZScaleRuntimeOverride_When_Applied_Then_ScalesOnlyPositiveZCarrier()
        {
            var managerObject = new GameObject("post set human pose endpoint positive z runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    true,
                    weight: 0.8f,
                    maxOffset: 0.04f,
                    positiveZScale: 0.25f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.True);
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceWeight"), Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceMaxOffset"), Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferencePositiveZScale"), Is.EqualTo(0.25f).Within(0.0001f));

                bool calculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                    desiredFootPosition: new Vector3(0.02f, 0f, 0.02f),
                    desiredToesPosition: new Vector3(0.02f, 0f, 0.02f),
                    currentFootPosition: Vector3.zero,
                    currentToesPosition: Vector3.zero,
                    weight: 1f,
                    maxOffset: 0.04f,
                    positiveZScale: 0f,
                    out Vector3 nextFootPosition);

                Assert.That(calculated, Is.True);
                Assert.That(nextFootPosition.x, Is.EqualTo(0.02f).Within(0.0001f));
                Assert.That(nextFootPosition.z, Is.EqualTo(0f).Within(0.0001f));

                bool disabledApplied = ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    false,
                    weight: 0.8f,
                    maxOffset: 0.04f,
                    positiveZScale: 0.25f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.False);
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceWeight"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferencePositiveZScale"), Is.EqualTo(0.25f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_PostSetHumanPoseEndpointFrameGatedRuntimeOverride_When_Applied_Then_PreservesDiagnosticWindow()
        {
            var managerObject = new GameObject("post set human pose endpoint frame gate runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    true,
                    weight: 0.8f,
                    maxOffset: 0.04f,
                    positiveZScale: 1f,
                    toesBlendWeight: 0.25f,
                    frameGateStart: 899f,
                    frameGateEnd: 901f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.True);
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight"), Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceFrameGateStart"), Is.EqualTo(899f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd"), Is.EqualTo(901f).Within(0.0001f));

                bool disabledApplied = ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    false,
                    weight: 0.8f,
                    maxOffset: 0.04f,
                    positiveZScale: 1f,
                    toesBlendWeight: 0.25f,
                    frameGateStart: 899f,
                    frameGateEnd: 901f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.False);
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceWeight"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight"), Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceFrameGateStart"), Is.EqualTo(899f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd"), Is.EqualTo(901f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_PostSetHumanPoseEndpointLeftSideRuntimeOverride_When_Applied_Then_PreservesRowLocalSideSwitch()
        {
            var managerObject = new GameObject("post set human pose endpoint left-side runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    true,
                    weight: 0.8f,
                    maxOffset: 0.04f,
                    positiveZScale: 1f,
                    toesBlendWeight: 1f,
                    frameGateStart: 300f,
                    frameGateEnd: 600f,
                    useLeftSide: true,
                    evaluatorXzReferenceEnabled: false,
                    evaluatorXzTargetMagnitude: 0.049f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.True);
                Assert.That(ReadBoolField(manager, "ShouldUseLeftSideForPostSetHumanPoseEndpointPosition"), Is.True);
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceFrameGateStart"), Is.EqualTo(300f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd"), Is.EqualTo(600f).Within(0.0001f));

                bool disabledApplied = ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    false,
                    weight: 0.8f,
                    maxOffset: 0.04f,
                    positiveZScale: 1f,
                    toesBlendWeight: 1f,
                    frameGateStart: 300f,
                    frameGateEnd: 600f,
                    useLeftSide: true,
                    evaluatorXzReferenceEnabled: false,
                    evaluatorXzTargetMagnitude: 0.049f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.False);
                Assert.That(ReadBoolField(manager, "ShouldUseLeftSideForPostSetHumanPoseEndpointPosition"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_PostSetHumanPoseEvaluatorXzRuntimeOverride_When_Applied_Then_UsesFirstOffsetBasis()
        {
            var managerObject = new GameObject("post set human pose evaluator xz runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    true,
                    weight: 0.9f,
                    maxOffset: 0.12f,
                    positiveZScale: 1f,
                    toesBlendWeight: 1f,
                    frameGateStart: 3550f,
                    frameGateEnd: 3553f,
                    evaluatorXzReferenceEnabled: true,
                    evaluatorXzTargetMagnitude: 0.049f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.True);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightFootEvaluatorXzReference"), Is.True);
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude"), Is.EqualTo(0.049f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceMaxOffset"), Is.EqualTo(0.12f).Within(0.0001f));

                bool disabledApplied = ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    false,
                    weight: 0.9f,
                    maxOffset: 0.12f,
                    positiveZScale: 1f,
                    toesBlendWeight: 1f,
                    frameGateStart: 3550f,
                    frameGateEnd: 3553f,
                    evaluatorXzReferenceEnabled: true,
                    evaluatorXzTargetMagnitude: 0.049f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.False);
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightFootEvaluatorXzReference"), Is.False);
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightEndpointPositionReferenceWeight"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude"), Is.EqualTo(0.049f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_PreSetHumanPoseEndpointRuntimeOverride_When_Applied_Then_UsesSeparatePreSolveSwitchAndCaps()
        {
            var managerObject = new GameObject("pre set human pose endpoint runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    true,
                    weight: 0.7f,
                    maxOffset: 0.025f,
                    positiveZScale: 0.5f,
                    toesBlendWeight: 0.25f,
                    frameGateStart: 180f,
                    frameGateEnd: 900f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePreSetHumanPoseRightEndpointPositionReference"), Is.True);
                Assert.That(ReadFloatField(manager, "preSetHumanPoseRightEndpointPositionReferenceWeight"), Is.EqualTo(0.7f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "preSetHumanPoseRightEndpointPositionReferenceMaxOffset"), Is.EqualTo(0.025f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "preSetHumanPoseRightEndpointPositionReferencePositiveZScale"), Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight"), Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "preSetHumanPoseRightEndpointPositionReferenceFrameGateStart"), Is.EqualTo(180f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd"), Is.EqualTo(900f).Within(0.0001f));
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.False,
                    "The pre-SetHumanPose endpoint candidate must not enable the already rejected post-solve endpoint path.");

                bool disabledApplied = ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    false,
                    weight: 0.7f,
                    maxOffset: 0.025f,
                    positiveZScale: 0.5f,
                    toesBlendWeight: 0.25f,
                    frameGateStart: 180f,
                    frameGateEnd: 900f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePreSetHumanPoseRightEndpointPositionReference"), Is.False);
                Assert.That(ReadFloatField(manager, "preSetHumanPoseRightEndpointPositionReferenceWeight"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "preSetHumanPoseRightEndpointPositionReferenceMaxOffset"), Is.EqualTo(0.025f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "preSetHumanPoseRightEndpointPositionReferenceFrameGateStart"), Is.EqualTo(180f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd"), Is.EqualTo(900f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_PreSetHumanPoseEndpointLeftSideRuntimeOverride_When_Applied_Then_PreservesPreSolveSideSwitch()
        {
            var managerObject = new GameObject("pre set human pose left endpoint runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    true,
                    weight: 1f,
                    maxOffset: 0.012f,
                    positiveZScale: 1f,
                    toesBlendWeight: 0f,
                    frameGateStart: 300f,
                    frameGateEnd: 600f,
                    useLeftSide: true,
                    useGhostCurrentBasis: true);

                Assert.That(enabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePreSetHumanPoseRightEndpointPositionReference"), Is.True);
                Assert.That(ReadBoolField(manager, "ShouldUseLeftSideForPreSetHumanPoseEndpointPosition"), Is.True);
                Assert.That(ReadBoolField(manager, "preSetHumanPoseEndpointPositionUseGhostCurrentBasis"), Is.True);
                Assert.That(ReadBoolField(manager, "ShouldInvertPreSetHumanPoseEndpointPositionBodyX"), Is.False);
                Assert.That(ReadBoolField(manager, "ShouldInvertPreSetHumanPoseEndpointPositionBodyZ"), Is.False);
                Assert.That(ReadFloatField(manager, "preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadBoolField(manager, "usePostSetHumanPoseRightEndpointPositionReference"), Is.False,
                    "The pre-SetHumanPose left endpoint candidate must not enable the already rejected post-solve endpoint path.");
                Assert.That(ReadBoolField(manager, "ShouldUseLeftSideForPostSetHumanPoseEndpointPosition"), Is.False,
                    "The pre-SetHumanPose side switch must stay isolated from the post-solve endpoint side switch.");

                bool disabledApplied = ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    false,
                    weight: 1f,
                    maxOffset: 0.012f,
                    positiveZScale: 1f,
                    toesBlendWeight: 0f,
                    frameGateStart: 300f,
                    frameGateEnd: 600f,
                    useLeftSide: true,
                    useGhostCurrentBasis: true);

                Assert.That(disabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePreSetHumanPoseRightEndpointPositionReference"), Is.False);
                Assert.That(ReadBoolField(manager, "ShouldUseLeftSideForPreSetHumanPoseEndpointPosition"), Is.False);
                Assert.That(ReadBoolField(manager, "preSetHumanPoseEndpointPositionUseGhostCurrentBasis"), Is.False);
                Assert.That(ReadBoolField(manager, "ShouldInvertPreSetHumanPoseEndpointPositionBodyX"), Is.False);
                Assert.That(ReadBoolField(manager, "ShouldInvertPreSetHumanPoseEndpointPositionBodyZ"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_PreSetHumanPoseEndpointBodyPositionInversionRuntimeOverride_When_Applied_Then_PreservesAxisFlags()
        {
            var managerObject = new GameObject("pre set human pose body position inversion runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    true,
                    weight: 1f,
                    maxOffset: 0.012f,
                    positiveZScale: 1f,
                    toesBlendWeight: 0f,
                    frameGateStart: 300f,
                    frameGateEnd: 600f,
                    useLeftSide: true,
                    useGhostCurrentBasis: true,
                    invertBodyPositionX: false,
                    invertBodyPositionZ: true);

                Assert.That(enabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePreSetHumanPoseRightEndpointPositionReference"), Is.True);
                Assert.That(ReadBoolField(manager, "ShouldUseLeftSideForPreSetHumanPoseEndpointPosition"), Is.True);
                Assert.That(ReadBoolField(manager, "preSetHumanPoseEndpointPositionUseGhostCurrentBasis"), Is.True);
                Assert.That(ReadBoolField(manager, "ShouldInvertPreSetHumanPoseEndpointPositionBodyX"), Is.False);
                Assert.That(ReadBoolField(manager, "ShouldInvertPreSetHumanPoseEndpointPositionBodyZ"), Is.True);

                bool disabledApplied = ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
                    manager,
                    false,
                    weight: 1f,
                    maxOffset: 0.012f,
                    positiveZScale: 1f,
                    toesBlendWeight: 0f,
                    frameGateStart: 300f,
                    frameGateEnd: 600f,
                    useLeftSide: true,
                    useGhostCurrentBasis: true,
                    invertBodyPositionX: false,
                    invertBodyPositionZ: true);

                Assert.That(disabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "usePreSetHumanPoseRightEndpointPositionReference"), Is.False);
                Assert.That(ReadBoolField(manager, "ShouldUseLeftSideForPreSetHumanPoseEndpointPosition"), Is.False);
                Assert.That(ReadBoolField(manager, "preSetHumanPoseEndpointPositionUseGhostCurrentBasis"), Is.False);
                Assert.That(ReadBoolField(manager, "ShouldInvertPreSetHumanPoseEndpointPositionBodyX"), Is.False);
                Assert.That(ReadBoolField(manager, "ShouldInvertPreSetHumanPoseEndpointPositionBodyZ"), Is.False,
                    "Disabling the runtime candidate must clear the inversion flag so restore runs return to the default route.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_TargetHumanoidBonePositionLockRuntimeOverride_When_Toggled_Then_OnlyChangesSkeletonBasisLock()
        {
            var managerObject = new GameObject("target humanoid bone position lock runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldLockTargetHumanoidBonePositions = true;
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.ShouldUseManualAnimatorHipsLocalPositionReference = false;
                manager.useManualAnimatorBipedIkFootPositionReference = false;

                bool unlockedApplied = ApplyTargetHumanoidBonePositionLockRuntimeOverride(manager, false);

                Assert.That(unlockedApplied, Is.True);
                Assert.That(manager.ShouldLockTargetHumanoidBonePositions, Is.False);
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False);
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False);

                bool lockedApplied = ApplyTargetHumanoidBonePositionLockRuntimeOverride(manager, true);

                Assert.That(lockedApplied, Is.True);
                Assert.That(manager.ShouldLockTargetHumanoidBonePositions, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False);
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_RetargetBodyPositionXzRootMotionRuntimeOverride_When_Toggled_Then_OnlyChangesSolverRootBasis()
        {
            var managerObject = new GameObject("retarget body position xz root motion runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseRetargetBodyPositionXZRootMotion = false;
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.usePreSetHumanPoseRightEndpointPositionReference = false;
                manager.ShouldLockTargetHumanoidBonePositions = true;

                bool enabledApplied = ApplyRetargetBodyPositionXzRootMotionRuntimeOverride(manager, true);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.ShouldUseRetargetBodyPositionXZRootMotion, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(manager.usePreSetHumanPoseRightEndpointPositionReference, Is.False);
                Assert.That(manager.ShouldLockTargetHumanoidBonePositions, Is.True);

                bool disabledApplied = ApplyRetargetBodyPositionXzRootMotionRuntimeOverride(manager, false);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.ShouldUseRetargetBodyPositionXZRootMotion, Is.False);
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(manager.usePreSetHumanPoseRightEndpointPositionReference, Is.False);
                Assert.That(manager.ShouldLockTargetHumanoidBonePositions, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorBodyPositionXzRuntimeOverride_When_Toggled_Then_OnlyChangesSolverBodyPositionBasis()
        {
            var managerObject = new GameObject("manual body position xz runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.usePreSetHumanPoseRightEndpointPositionReference = false;
                manager.ShouldUseRetargetBodyPositionXZRootMotion = false;

                bool enabledApplied = ApplyManualAnimatorBodyPositionXzRuntimeOverride(
                    manager,
                    true,
                    0.45f,
                    0.025f,
                    frameGateStart: 300f,
                    frameGateEnd: 600f,
                    frameGateBlendFrames: 30f,
                    axisXScale: 0.25f,
                    axisZScale: 0.75f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "ShouldUseManualAnimatorBodyPositionXzReference"), Is.True);
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceWeight"), Is.EqualTo(0.45f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceMaxOffset"), Is.EqualTo(0.025f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceFrameGateStart"), Is.EqualTo(300f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceFrameGateEnd"), Is.EqualTo(600f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames"), Is.EqualTo(30f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceAxisXScale"), Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceAxisZScale"), Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(manager.usePreSetHumanPoseRightEndpointPositionReference, Is.False);
                Assert.That(manager.ShouldUseRetargetBodyPositionXZRootMotion, Is.False);

                bool disabledApplied = ApplyManualAnimatorBodyPositionXzRuntimeOverride(
                    manager,
                    false,
                    0.45f,
                    0.025f,
                    frameGateStart: 300f,
                    frameGateEnd: 600f,
                    frameGateBlendFrames: 30f,
                    axisXScale: 0.25f,
                    axisZScale: 0.75f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "ShouldUseManualAnimatorBodyPositionXzReference"), Is.False);
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceFrameGateStart"), Is.EqualTo(300f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceFrameGateEnd"), Is.EqualTo(600f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames"), Is.EqualTo(30f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceAxisXScale"), Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorBodyPositionXzReferenceAxisZScale"), Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(manager.usePreSetHumanPoseRightEndpointPositionReference, Is.False);
                Assert.That(manager.ShouldUseRetargetBodyPositionXZRootMotion, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_RightSleeveSilhouetteOffsetRuntimeOverride_When_Toggled_Then_OnlyChangesFrameLocalSleeveSettings()
        {
            var managerObject = new GameObject("right sleeve silhouette offset runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorFullBodyPoseReference = false;
                manager.ShouldUseManualAnimatorBodyPositionXzReference = false;
                manager.enableYybArmSleeveAnchorCorrection = true;

                bool enabledApplied = ApplyYybRightSleeveSilhouetteOffsetRuntimeOverride(
                    manager,
                    true,
                    localOffsetX: -0.055f,
                    frameGateStart: 90f,
                    frameGateEnd: 90f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "useYybRightSleeveSilhouetteLocalOffsetReference"), Is.True);
                Assert.That(ReadFloatField(manager, "yybRightSleeveSilhouetteLocalOffsetX"), Is.EqualTo(-0.055f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "yybRightSleeveSilhouetteLocalOffsetFrameGateStart"), Is.EqualTo(90f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "yybRightSleeveSilhouetteLocalOffsetFrameGateEnd"), Is.EqualTo(90f).Within(0.0001f));
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(manager.ShouldUseManualAnimatorBodyPositionXzReference, Is.False);
                Assert.That(manager.enableYybArmSleeveAnchorCorrection, Is.True);

                bool clampedApplied = ApplyYybRightSleeveSilhouetteOffsetRuntimeOverride(
                    manager,
                    true,
                    localOffsetX: 0.5f,
                    frameGateStart: -10f,
                    frameGateEnd: 7000f);

                Assert.That(clampedApplied, Is.True);
                Assert.That(ReadFloatField(manager, "yybRightSleeveSilhouetteLocalOffsetX"), Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "yybRightSleeveSilhouetteLocalOffsetFrameGateStart"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "yybRightSleeveSilhouetteLocalOffsetFrameGateEnd"), Is.EqualTo(6000f).Within(0.0001f));

                bool disabledApplied = ApplyYybRightSleeveSilhouetteOffsetRuntimeOverride(
                    manager,
                    false,
                    localOffsetX: -0.055f,
                    frameGateStart: 90f,
                    frameGateEnd: 90f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(ReadBoolField(manager, "useYybRightSleeveSilhouetteLocalOffsetReference"), Is.False);
                Assert.That(ReadFloatField(manager, "yybRightSleeveSilhouetteLocalOffsetX"), Is.EqualTo(-0.055f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "yybRightSleeveSilhouetteLocalOffsetFrameGateStart"), Is.EqualTo(90f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "yybRightSleeveSilhouetteLocalOffsetFrameGateEnd"), Is.EqualTo(90f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_RightSleeveSilhouetteOffsetFrameGate_When_ExposedInInspector_Then_Frame90IsSelectable()
        {
            AssertRangeMaxAtLeast<FBXVmdPipeline>("yybRightSleeveSilhouetteLocalOffsetFrameGateStart", 90f);
            AssertRangeMaxAtLeast<FBXVmdPipeline>("yybRightSleeveSilhouetteLocalOffsetFrameGateEnd", 90f);
            AssertRangeMaxAtLeast<PoseSpaceRetargeter>("yybRightSleeveSilhouetteLocalOffsetFrameGateStart", 90f);
            AssertRangeMaxAtLeast<PoseSpaceRetargeter>("yybRightSleeveSilhouetteLocalOffsetFrameGateEnd", 90f);
        }

        [Test]
        public void Given_PostSetHumanPoseEndpointFrameGate_When_ExposedInInspector_Then_LegacyFrameWindowIsSelectable()
        {
            const float discoveredLegacyGateEnd = 3553f;
            string[] fieldNames =
            {
                "postSetHumanPoseRightEndpointPositionReferenceFrameGateStart",
                "postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd"
            };

            foreach (string fieldName in fieldNames)
            {
                AssertRangeMaxAtLeast<FBXVmdPipeline>(fieldName, discoveredLegacyGateEnd);
                AssertRangeMaxAtLeast<PoseSpaceRetargeter>(fieldName, discoveredLegacyGateEnd);
            }
        }

        [Test]
        public void Given_PostSetHumanPoseEndpointPositiveZScale_When_CorrectionExceedsCap_Then_DoesNotIncreaseBaselineClampedX()
        {
            bool baselineCalculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition: new Vector3(0.08f, 0f, 0.06f),
                desiredToesPosition: new Vector3(0.08f, 0f, 0.06f),
                currentFootPosition: Vector3.zero,
                currentToesPosition: Vector3.zero,
                weight: 1f,
                maxOffset: 0.05f,
                positiveZScale: 1f,
                out Vector3 baselineFootPosition);

            bool suppressedCalculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition: new Vector3(0.08f, 0f, 0.06f),
                desiredToesPosition: new Vector3(0.08f, 0f, 0.06f),
                currentFootPosition: Vector3.zero,
                currentToesPosition: Vector3.zero,
                weight: 1f,
                maxOffset: 0.05f,
                positiveZScale: 0f,
                out Vector3 suppressedFootPosition);

            Assert.That(baselineCalculated, Is.True);
            Assert.That(suppressedCalculated, Is.True);
            Assert.That(baselineFootPosition.x, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(baselineFootPosition.z, Is.EqualTo(0.03f).Within(0.0001f));
            Assert.That(suppressedFootPosition.x, Is.LessThanOrEqualTo(baselineFootPosition.x + 0.0001f));
            Assert.That(suppressedFootPosition.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_PostSetHumanPoseEndpointToesBlend_When_RecalculatingDirection_Then_CanUseFootOnlyOrFootToesAverage()
        {
            bool averageCalculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition: new Vector3(0.04f, 0f, 0f),
                desiredToesPosition: new Vector3(0f, 0f, 0.04f),
                currentFootPosition: Vector3.zero,
                currentToesPosition: Vector3.zero,
                weight: 1f,
                maxOffset: 0.2f,
                positiveZScale: 1f,
                toesBlendWeight: 1f,
                out Vector3 averageFootPosition);

            bool footOnlyCalculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition: new Vector3(0.04f, 0f, 0f),
                desiredToesPosition: new Vector3(0f, 0f, 0.04f),
                currentFootPosition: Vector3.zero,
                currentToesPosition: Vector3.zero,
                weight: 1f,
                maxOffset: 0.2f,
                positiveZScale: 1f,
                toesBlendWeight: 0f,
                out Vector3 footOnlyPosition);

            Assert.That(averageCalculated, Is.True);
            Assert.That(footOnlyCalculated, Is.True);
            Assert.That(averageFootPosition.x, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(averageFootPosition.z, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(footOnlyPosition.x, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(footOnlyPosition.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_PostSetHumanPoseEvaluatorXzReference_When_FirstOffsetDrifts_Then_ReducesToTargetMagnitude()
        {
            bool calculated = TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
                referenceFootPosition: new Vector3(1.594633f, 0f, 0.070673f),
                currentFootPosition: new Vector3(0.324088f, 0f, 0.020131f),
                firstMatchedFootOffset: new Vector3(-1.375309f, 0f, 0.033983f),
                targetMagnitude: 0.049f,
                weight: 1f,
                maxOffset: 0.2f,
                out Vector3 nextFootPosition);

            Assert.That(calculated, Is.True);
            Assert.That(nextFootPosition.x, Is.EqualTo(0.25746f).Within(0.0001f));
            Assert.That(nextFootPosition.z, Is.EqualTo(0.073888f).Within(0.0001f));

            float remainingX = nextFootPosition.x - 1.594633f - (-1.375309f);
            float remainingZ = nextFootPosition.z - 0.070673f - 0.033983f;
            Assert.That(new Vector2(remainingX, remainingZ).magnitude, Is.EqualTo(0.049f).Within(0.0001f));
        }

        [Test]
        public void Given_ManualAnimatorHipsLocalPositionRuntimeOverride_When_CustomWeightAndCapProvided_Then_UsesCustomCandidateValues()
        {
            var managerObject = new GameObject("manual animator hips local position runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorHipsLocalPositionReference = false;
                manager.manualAnimatorHipsLocalPositionWeight = 0f;
                manager.manualAnimatorHipsLocalPositionMaxOffset = 0f;

                bool enabledApplied = ApplyManualAnimatorHipsLocalPositionRuntimeOverride(
                    manager,
                    true,
                    weight: 0.25f,
                    maxOffset: 0.04f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.True);
                Assert.That(manager.manualAnimatorHipsLocalPositionWeight, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(manager.manualAnimatorHipsLocalPositionMaxOffset, Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(manager.ShouldUseManualAnimatorFullBodyPoseReference, Is.False, "Hips local-position candidate must not enable the full-body pose copy path.");
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False, "Hips local-position candidate must not enable the rejected BipedIK pull path.");
                Assert.That(manager.ShouldUseManualAnimatorFootHeightGroundingReference, Is.False, "Hips local-position candidate must not change grounding.");

                bool disabledApplied = ApplyManualAnimatorHipsLocalPositionRuntimeOverride(
                    manager,
                    false,
                    weight: 0.25f,
                    maxOffset: 0.04f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False);
                Assert.That(manager.manualAnimatorHipsLocalPositionWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.manualAnimatorHipsLocalPositionMaxOffset, Is.EqualTo(0.04f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ManualAnimatorFootHipsAlignedResidualYawRuntimeOverride_When_CustomWeightAndCapProvided_Then_UsesCustomCandidateValues()
        {
            var managerObject = new GameObject("manual animator foot residual yaw runtime override manager");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                manager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference = false;
                manager.manualAnimatorFootHipsAlignedResidualYawReferenceWeight = 0f;
                manager.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = 0f;

                bool enabledApplied = ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride(
                    manager,
                    true,
                    weight: 0.8f,
                    maxAngle: 12f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True);
                Assert.That(manager.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(manager.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle, Is.EqualTo(12f).Within(0.0001f));
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False, "Foot residual yaw candidate must not enable the rejected BipedIK pull path.");
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, "Foot residual yaw candidate must not re-enable the rejected hips localPosition copy path.");
                Assert.That(manager.ShouldUseManualAnimatorFootHeightGroundingReference, Is.False, "Foot residual yaw candidate must not change grounding.");

                bool disabledApplied = ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride(
                    manager,
                    false,
                    weight: 0.8f,
                    maxAngle: 12f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.False);
                Assert.That(manager.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle, Is.EqualTo(12f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
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

                bool enabledApplied = ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                    manager,
                    true,
                    weight: 0.75f,
                    maxAngle: 6.2f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(manager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(6.2f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.False);
                Assert.That(manager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.useManualAnimatorBipedIkFootPositionReference, Is.False, "Segment direction candidate must not enable the rejected BipedIK pull path.");
                Assert.That(manager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, "Segment direction candidate must not re-enable the rejected hips localPosition copy path.");
                Assert.That(manager.ShouldUseManualAnimatorFootHeightGroundingReference, Is.False, "Segment direction candidate must not change grounding.");

                bool disabledApplied = ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
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

                bool applied = ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
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

                bool applied = ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
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

                bool applied = ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
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
                Assert.That(ReadFloatField(manager, "manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle"), Is.EqualTo(2f).Within(0.0001f));
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

                bool applied = ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
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
                Assert.That(ReadFloatField(manager, "manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle"), Is.EqualTo(4f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale"), Is.EqualTo(0.25f).Within(0.0001f));
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

                bool applied = ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
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
                Assert.That(ReadFloatField(manager, "manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle"), Is.EqualTo(4f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale"), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight"), Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd"), Is.EqualTo(0f).Within(0.0001f));
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

                bool applied = ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
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
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle"), Is.EqualTo(4f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale"), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart"), Is.EqualTo(900f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd"), Is.EqualTo(930f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle"), Is.EqualTo(0f).Within(0.0001f));
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

                bool applied = ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
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
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle"), Is.EqualTo(4f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight"), Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight"), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(ReadFloatField(manager, "manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(manager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference, Is.False);
                Assert.That(manager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_LowerBodySegmentDirectionReferenceOnly_When_ConfiguringManualReference_Then_PreparesReferenceAnimator()
        {
            var managerObject = new GameObject("manual animator lower body segment direction reference manager");
            var retargeterObject = new GameObject("manual animator lower body segment direction reference retargeter");
            try
            {
                var manager = managerObject.AddComponent<FBXVmdPipeline>();
                var retargeter = retargeterObject.AddComponent<PoseSpaceRetargeter>();
                manager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = true;

                AnimationClip referenceClip = LoadFirstHumanoidAnimationClip("Assets/_Project/FBX/satisfaction_2.fbx");
                Assert.That(referenceClip, Is.Not.Null, "satisfaction_2 reference clip must be available for lower-body segment A/B probes.");

                InvokeConfigureEditorManualFingerPoseReference(manager, retargeter, referenceClip);

                Assert.That(
                    GetField<Animator>(retargeter, "_editorFingerReferenceAnimator"),
                    Is.Not.Null,
                    "Lower-body segment direction reference depends on the manual reference Animator; otherwise the runtime candidate is inert.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(retargeterObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Given_ProjectFbxExists_When_ResolvingYybReferenceClipPath_Then_UsesProjectReferenceBeforeControlledImport()
        {
            string controlledPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
            string projectPath = "Assets/_Project/FBX/satisfaction_2.fbx";

            string resolved = ResolveYybReferenceClipAssetPath(
                "satisfaction_2",
                controlledPath,
                projectPath);

            Assert.That(resolved, Is.EqualTo(projectPath));
        }

        [Test]
        public void Given_CandidateFrameCountDiffersFromReference_When_ResolvingSummaryTargetFrameCount_Then_KeepsReferenceTarget()
        {
            int resolved = ResolveSummaryTargetFrameCount(
                referenceTargetFrameCount: 6001,
                mainAutoFrameCount: 5900);

            Assert.That(resolved, Is.EqualTo(6001));
        }

        [Test]
        public void Given_MainAutoFrameCountIsUnavailable_When_ResolvingSummaryTargetFrameCount_Then_KeepsReferenceTarget()
        {
            int resolved = ResolveSummaryTargetFrameCount(
                referenceTargetFrameCount: 6234,
                mainAutoFrameCount: 0);

            Assert.That(resolved, Is.EqualTo(6234));
        }

        [Test]
        public void Given_FullSatisfactionReferenceTiming_When_ResolvingReferenceMmdTargetFrameCount_Then_Uses6001FrameReference()
        {
            int resolved = ResolveReferenceMmdTargetFrameCount(
                "satisfaction_2.fbx",
                requestedDurationSeconds: 207.7833f,
                configuredTargetFrameCount: 6234,
                referenceClipLengthSeconds: 207.7833f,
                recordingFrameRate: 30f);

            Assert.That(resolved, Is.EqualTo(6001));
        }

        [Test]
        public void Given_ShortSatisfactionSmoke_When_ResolvingReferenceMmdTargetFrameCount_Then_KeepsConfiguredSmokeTarget()
        {
            int resolved = ResolveReferenceMmdTargetFrameCount(
                "satisfaction_2.fbx",
                requestedDurationSeconds: 31f,
                configuredTargetFrameCount: 930,
                referenceClipLengthSeconds: 207.7833f,
                recordingFrameRate: 30f);

            Assert.That(resolved, Is.EqualTo(930));
        }

        [Test]
        public void Given_FrameCounts_When_BuildingSummaryFrameRoleDiagnostics_Then_SeparatesReferenceTargetFromRecordedBaselines()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybReferenceMp4Diagnostics_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 7,\n" +
                    "  \"video\": {\n" +
                    "    \"width\": 1280,\n" +
                    "    \"height\": 720,\n" +
                    "    \"avg_frame_rate\": \"30/1\",\n" +
                    "    \"stream_duration\": \"2.500\",\n" +
                    "    \"nb_frames\": \"75\"\n" +
                    "  }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 4,\n" +
                    "  \"extractedFrameCount\": 7,\n" +
                    "  \"avgBBoxHeightRatio\": 0.42,\n" +
                    "  \"centerXRangeRatio\": 0.24,\n" +
                    "  \"maxBottomGapRatio\": 0.05,\n" +
                    "  \"avgBrightAreaRatio\": 0.12,\n" +
                    "  \"rows\": [\n" +
                    "    { \"seconds\": 0.0, \"bboxHeightRatio\": 0.4, \"centerXRatio\": 0.2, \"bottomGapRatio\": 0.01, \"brightAreaRatio\": 0.1 },\n" +
                    "    { \"seconds\": 1.5, \"bboxHeightRatio\": 0.6, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.02, \"brightAreaRatio\": 0.2 },\n" +
                    "    { \"seconds\": 3.0, \"bboxHeightRatio\": 0.5, \"centerXRatio\": 0.4, \"bottomGapRatio\": 0.03, \"brightAreaRatio\": 0.3 },\n" +
                    "    { \"seconds\": 5.0, \"bboxHeightRatio\": 0.9, \"centerXRatio\": 0.9, \"bottomGapRatio\": 0.04, \"brightAreaRatio\": 0.9 }\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 6001,
                    baselineRecordedFrameCount: 6234,
                    candidateRecordedFrameCount: 5900,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath);

                Assert.That(GetField<int>(diagnostics, "reference_target_frame_count"), Is.EqualTo(6001));
                Assert.That(GetField<int>(diagnostics, "baseline_recorded_frame_count"), Is.EqualTo(6234));
                Assert.That(GetField<int>(diagnostics, "candidate_recorded_frame_count"), Is.EqualTo(5900));
                Assert.That(GetField<int>(diagnostics, "candidate_frame_count_delta_from_reference_target"), Is.EqualTo(-101));
                Assert.That(GetField<string>(diagnostics, "target_frame_count_role"), Does.Contain("ref_mmd_mp4"));
                Assert.That(GetField<string>(diagnostics, "baseline_recorded_frame_count_role"), Does.Contain("Sub_Manual"));
                Assert.That(GetField<string>(diagnostics, "candidate_recorded_frame_count_role"), Does.Contain("Main_Auto"));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_provenance_evidence_path"), Is.EqualTo(provenancePath));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_analysis_result_path"), Is.EqualTo(resultPath));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_frame_metrics_path"), Is.EqualTo(frameMetricsPath));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_contact_sheet_path"), Is.EqualTo(contactSheetPath));
                Assert.That(GetField<bool>(diagnostics, "reference_mp4_provenance_evidence_exists"), Is.True);
                Assert.That(GetField<bool>(diagnostics, "reference_mp4_analysis_result_exists"), Is.True);
                Assert.That(GetField<bool>(diagnostics, "reference_mp4_frame_metrics_exists"), Is.True);
                Assert.That(GetField<bool>(diagnostics, "reference_mp4_contact_sheet_exists"), Is.True);
                Assert.That(GetField<string>(diagnostics, "reference_mp4_canonical_context"), Does.Contain("Sub_Manual"));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_canonical_context"), Does.Contain("MMD"));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_analysis_schema"), Is.EqualTo("ref-mp4-analysis-fixture-v1"));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_frame_metrics_schema"), Is.EqualTo("ref-mp4-frame-metrics-fixture-v1"));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_width"), Is.EqualTo(1280));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_height"), Is.EqualTo(720));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_avg_frame_rate"), Is.EqualTo("30/1"));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_stream_duration_seconds"), Is.EqualTo(2.5f).Within(0.000001f));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_total_video_frames"), Is.EqualTo(75));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_frame_metrics_sample_count"), Is.EqualTo(4));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_frame_metrics_extracted_frame_count"), Is.EqualTo(7));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_avg_bbox_height_ratio"), Is.EqualTo(0.42f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_center_x_range_ratio"), Is.EqualTo(0.24f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_max_bottom_gap_ratio"), Is.EqualTo(0.05f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_duration_seconds"), Is.EqualTo(3f).Within(0.000001f));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_current_clip_sample_count"), Is.EqualTo(3));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_first_sample_seconds"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_last_sample_seconds"), Is.EqualTo(3f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_sample_coverage_ratio"), Is.EqualTo(1f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_sample_gap_seconds"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bbox_height_ratio"), Is.EqualTo(0.5f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_center_x_range_ratio"), Is.EqualTo(0.3f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_max_bottom_gap_ratio"), Is.EqualTo(0.03f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bright_area_ratio"), Is.EqualTo(0.2f).Within(0.000001f));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_current_clip_sample_basis"), Does.Contain("requested duration"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesCandidateFramingToReferenceMp4()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateFramingDiagnostics_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string rightIgnored = Path.Combine(frameFolder, "right-ignored.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 2,\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"avgBBoxHeightRatio\": 0.5,\n" +
                    "  \"centerXRangeRatio\": 0.1,\n" +
                    "  \"maxBottomGapRatio\": 0.2,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    "    { \"seconds\": 0.0, \"bboxHeightRatio\": 0.4, \"centerXRatio\": 0.1, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.2 },\n" +
                    "    { \"seconds\": 3.0, \"bboxHeightRatio\": 0.5, \"centerXRatio\": 0.2, \"bottomGapRatio\": 0.2, \"brightAreaRatio\": 0.3 }\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(frontA, new RectInt(2, 1, 4, 8));
                WriteFixturePng(frontB, new RectInt(4, 2, 4, 5));
                WriteFixturePng(rightIgnored, new RectInt(0, 0, 10, 10));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,t3,90,front,{frontB}\n" +
                    $"fixture,Main_Auto,t3,90,right,{rightIgnored}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<string>(diagnostics, "candidate_screenshot_frame_index_path"), Is.EqualTo(indexPath));
                Assert.That(GetField<bool>(diagnostics, "candidate_screenshot_frame_index_exists"), Is.True);
                Assert.That(GetField<string>(diagnostics, "candidate_screenshot_frame_metrics_view"), Is.EqualTo("front"));
                Assert.That(GetField<int>(diagnostics, "candidate_screenshot_frame_metrics_sample_count"), Is.EqualTo(2));
                Assert.That(GetField<int>(diagnostics, "candidate_screenshot_nonblank_frame_count"), Is.EqualTo(2));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_bbox_height_ratio"), Is.EqualTo(0.65f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_center_x_range_ratio"), Is.EqualTo(0.2f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_max_bottom_gap_ratio"), Is.EqualTo(0.2f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_max_top_gap_ratio"), Is.EqualTo(0.3f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_bright_area_ratio"), Is.EqualTo(0.26f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_avg_bbox_height_ratio_delta"), Is.EqualTo(0.15f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_center_x_range_ratio_delta"), Is.EqualTo(0.1f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_max_bottom_gap_ratio_delta"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_avg_bright_area_ratio_delta"), Is.EqualTo(0.01f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bbox_height_ratio"), Is.EqualTo(0.45f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_current_clip_avg_bbox_height_ratio_delta"), Is.EqualTo(0.2f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_current_clip_center_x_range_ratio_delta"), Is.EqualTo(0.1f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_current_clip_max_bottom_gap_ratio_delta"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_current_clip_avg_bright_area_ratio_delta"), Is.EqualTo(0.01f).Within(0.000001f));
                Assert.That(GetField<string>(diagnostics, "candidate_screenshot_frame_metrics_basis"), Does.Contain("front"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ReportsCandidateTimingCoverageAgainstReferenceSamples()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateTimingDiagnostics_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string frontC = Path.Combine(frameFolder, "front-c.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 3,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 3,\n" +
                    "  \"extractedFrameCount\": 3,\n" +
                    "  \"avgBBoxHeightRatio\": 0.5,\n" +
                    "  \"centerXRangeRatio\": 0.1,\n" +
                    "  \"maxBottomGapRatio\": 0.2,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    "    { \"seconds\": 0.0, \"bboxHeightRatio\": 0.4, \"centerXRatio\": 0.1, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.2 },\n" +
                    "    { \"seconds\": 1.5, \"bboxHeightRatio\": 0.5, \"centerXRatio\": 0.2, \"bottomGapRatio\": 0.2, \"brightAreaRatio\": 0.3 },\n" +
                    "    { \"seconds\": 3.0, \"bboxHeightRatio\": 0.6, \"centerXRatio\": 0.3, \"bottomGapRatio\": 0.3, \"brightAreaRatio\": 0.4 }\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(frontA, new RectInt(2, 1, 4, 8));
                WriteFixturePng(frontB, new RectInt(3, 2, 4, 6));
                WriteFixturePng(frontC, new RectInt(4, 2, 4, 5));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,t1.7,51,front,{frontB}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontC}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<int>(diagnostics, "candidate_screenshot_time_sample_count"), Is.EqualTo(3));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_first_sample_seconds"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_last_sample_seconds"), Is.EqualTo(3f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_sample_coverage_ratio"), Is.EqualTo(1f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_sample_gap_seconds"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_max_ref_sample_seconds_gap"), Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_ref_sample_seconds_gap"), Is.EqualTo(0.06666667f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_screenshot_sample_timing_basis"), Does.Contain("recorderFrame"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_TailSegmentStart_When_BuildingSummaryFrameRoleDiagnostics_Then_UsesMatchingReferenceMp4Window()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybTailReferenceWindowDiagnostics_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string frontC = Path.Combine(frameFolder, "front-c.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 6,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"153.0\", \"nb_frames\": \"4590\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 6,\n" +
                    "  \"extractedFrameCount\": 6,\n" +
                    "  \"avgBBoxHeightRatio\": 0.5,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.3,\n" +
                    "  \"maxBottomGapRatio\": 0.2,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    "    { \"seconds\": 0.0, \"bboxHeightRatio\": 0.1, \"bboxWidthRatio\": 0.2, \"centerXRatio\": 0.1, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.2 },\n" +
                    "    { \"seconds\": 1.5, \"bboxHeightRatio\": 0.2, \"bboxWidthRatio\": 0.3, \"centerXRatio\": 0.2, \"bottomGapRatio\": 0.2, \"brightAreaRatio\": 0.3 },\n" +
                    "    { \"seconds\": 3.0, \"bboxHeightRatio\": 0.3, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.3, \"bottomGapRatio\": 0.3, \"brightAreaRatio\": 0.4 },\n" +
                    "    { \"seconds\": 150.0, \"bboxHeightRatio\": 0.6, \"bboxWidthRatio\": 0.7, \"centerXRatio\": 0.4, \"bottomGapRatio\": 0.01, \"brightAreaRatio\": 0.5 },\n" +
                    "    { \"seconds\": 151.5, \"bboxHeightRatio\": 0.7, \"bboxWidthRatio\": 0.8, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.02, \"brightAreaRatio\": 0.6 },\n" +
                    "    { \"seconds\": 153.0, \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.9, \"centerXRatio\": 0.6, \"bottomGapRatio\": 0.03, \"brightAreaRatio\": 0.7 }\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(frontA, new RectInt(2, 1, 4, 8));
                WriteFixturePng(frontB, new RectInt(3, 2, 4, 6));
                WriteFixturePng(frontC, new RectInt(4, 2, 4, 5));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,t1.5,45,front,{frontB}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontC}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnosticsWithReferenceClipStart(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    referenceClipStartSeconds: 150f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_start_seconds"), Is.EqualTo(150f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_end_seconds"), Is.EqualTo(153f).Within(0.000001f));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_current_clip_sample_count"), Is.EqualTo(3));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_first_sample_seconds"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_last_sample_seconds"), Is.EqualTo(3f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bbox_height_ratio"), Is.EqualTo(0.7f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_center_x_range_ratio"), Is.EqualTo(0.2f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_max_ref_sample_seconds_gap"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_seconds_gap"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_current_clip_sample_basis"), Does.Contain("clip start"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesTimeMatchedCandidateAndReferenceFraming()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateTimeMatchedFraming_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string frontC = Path.Combine(frameFolder, "front-c.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 3,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 3,\n" +
                    "  \"extractedFrameCount\": 3,\n" +
                    "  \"avgBBoxHeightRatio\": 0.58,\n" +
                    "  \"avgBBoxWidthRatio\": 0.46,\n" +
                    "  \"centerXRangeRatio\": 0.13,\n" +
                    "  \"maxBottomGapRatio\": 0.22,\n" +
                    "  \"avgBrightAreaRatio\": 0.26,\n" +
                    "  \"rows\": [\n" +
                    "    { \"seconds\": 0.0, \"bboxHeightRatio\": 0.7, \"bboxWidthRatio\": 0.44, \"centerXRatio\": 0.45, \"bottomGapRatio\": 0.08, \"brightAreaRatio\": 0.30 },\n" +
                    "    { \"seconds\": 1.5, \"bboxHeightRatio\": 0.55, \"bboxWidthRatio\": 0.38, \"centerXRatio\": 0.52, \"bottomGapRatio\": 0.22, \"brightAreaRatio\": 0.27 },\n" +
                    "    { \"seconds\": 3.0, \"bboxHeightRatio\": 0.48, \"bboxWidthRatio\": 0.56, \"centerXRatio\": 0.58, \"bottomGapRatio\": 0.18, \"brightAreaRatio\": 0.22 }\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(frontA, new RectInt(2, 1, 4, 8));
                WriteFixturePng(frontB, new RectInt(3, 2, 4, 6));
                WriteFixturePng(frontC, new RectInt(4, 2, 4, 5));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,t1.5,45,front,{frontB}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontC}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_sample_count"), Is.EqualTo(3));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_seconds_gap"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_bbox_height_ratio_abs_delta"), Is.EqualTo(0.05666667f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bbox_width_ratio"), Is.EqualTo(0.46f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_bbox_width_ratio"), Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_bbox_width_ratio_abs_delta"), Is.EqualTo(0.07333333f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_width_ratio_abs_delta"), Is.EqualTo(0.16f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_center_x_ratio_abs_delta"), Is.EqualTo(0.03f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta"), Is.EqualTo(0.02f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_bright_area_ratio_abs_delta"), Is.EqualTo(0.02333333f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_framing_metric_basis"), Does.Contain("nearest"));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_image_space_limb_span_basis"), Does.Contain("bbox width"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesBandedImageSpaceLimbSpans()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateBandedLimbSpan_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string refB = Path.Combine(frameFolder, "ref-b.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 2,\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.5,\n" +
                    "  \"centerXRangeRatio\": 0.1,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.30 }},\n" +
                    $"    {{ \"seconds\": 3.0, \"framePath\": \"{refB.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.20 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 4), new RectInt(2, 5, 6, 4));
                WriteFixturePng(refB, new RectInt(4, 1, 2, 4), new RectInt(3, 5, 4, 4));
                WriteFixturePng(frontA, new RectInt(2, 1, 5, 4), new RectInt(1, 5, 8, 4));
                WriteFixturePng(frontB, new RectInt(4, 1, 2, 4), new RectInt(2, 5, 5, 4));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontB}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_upper_limb_span_ratio"), Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_lower_limb_span_ratio"), Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_upper_limb_span_ratio"), Is.EqualTo(0.65f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_lower_limb_span_ratio"), Is.EqualTo(0.35f).Within(0.00001f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_limb_band_sample_count"), Is.EqualTo(2));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_upper_limb_span_ratio_abs_delta"), Is.EqualTo(0.15f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_upper_limb_span_ratio_abs_delta"), Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_lower_limb_span_ratio_abs_delta"), Is.EqualTo(0.05f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_lower_limb_span_ratio_abs_delta"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_image_space_limb_band_basis"), Does.Contain("silhouette"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesSilhouetteProfileLimbSpans()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateSilhouetteProfile_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string refB = Path.Combine(frameFolder, "ref-b.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 2,\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.5,\n" +
                    "  \"centerXRangeRatio\": 0.1,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.30 }},\n" +
                    $"    {{ \"seconds\": 3.0, \"framePath\": \"{refB.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.20 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 4), new RectInt(2, 5, 6, 4));
                WriteFixturePng(refB, new RectInt(4, 1, 2, 4), new RectInt(3, 5, 4, 4));
                WriteFixturePng(frontA, new RectInt(2, 1, 5, 4), new RectInt(1, 5, 8, 4));
                WriteFixturePng(frontB, new RectInt(4, 1, 2, 4), new RectInt(2, 5, 5, 4));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontB}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_silhouette_profile_band_count"), Is.EqualTo(4));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_silhouette_profile_sample_count"), Is.EqualTo(2));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_silhouette_profile_l1_abs_delta"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta"), Is.EqualTo(0.15f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta"), Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_silhouette_profile_basis"), Does.Contain("4-band"));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_silhouette_landmark_band_count"), Is.EqualTo(4));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_silhouette_landmark_sample_count"), Is.EqualTo(2));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_silhouette_landmark_endpoint_abs_delta"), Is.EqualTo(0.05f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_silhouette_landmark_basis"), Does.Contain("left/right"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesDeterministicImageSpaceKeypoints()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateImageSpaceKeypoints_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string refB = Path.Combine(frameFolder, "ref-b.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 2,\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.5,\n" +
                    "  \"centerXRangeRatio\": 0.1,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.30 }},\n" +
                    $"    {{ \"seconds\": 3.0, \"framePath\": \"{refB.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.20 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 4), new RectInt(2, 5, 6, 4));
                WriteFixturePng(refB, new RectInt(4, 1, 2, 4), new RectInt(3, 5, 4, 4));
                WriteFixturePng(frontA, new RectInt(2, 1, 5, 4), new RectInt(1, 5, 8, 4));
                WriteFixturePng(frontB, new RectInt(4, 1, 2, 4), new RectInt(2, 5, 5, 4));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontB}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_image_space_keypoint_sample_count"), Is.EqualTo(2));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_image_space_keypoint_count"), Is.EqualTo(10));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta"), Is.EqualTo(0.045f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_image_space_keypoint_basis"), Does.Contain("deterministic 2D silhouette keypoints"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateAndReferenceFrameImages_When_FramingDiffers_Then_SeparatesBBoxNormalizedKeypointResidual()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateFramingNormalizedKeypoints_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.32,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.32 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 8));
                WriteFixturePng(frontA, new RectInt(2, 1, 6, 8));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta"), Is.EqualTo(0.08f).Within(0.00001f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_count"), Is.EqualTo(10));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta_removed_by_bbox_normalization"), Is.EqualTo(0.08f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta_removed_by_bbox_normalization"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_basis"), Does.Contain("bbox-normalized"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateTopRowHasSparseSilhouettePixels_When_BuildingSummaryFrameRoleDiagnostics_Then_UsesRobustCapCenterKeypoints()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateRobustCapKeypoints_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.32,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.32 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 8));
                WriteFixturePng(frontA, new RectInt(3, 1, 4, 7), new RectInt(3, 8, 1, 1));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_x_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_image_space_keypoint_basis"), Does.Contain("bbox centerline"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateAndReferenceFrameImages_When_NormalizedShapeDiffers_Then_RecordsMaxBBoxNormalizedKeypointAttribution()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateNormalizedKeypointAttribution_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.32,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.32 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 8));
                WriteFixturePng(frontA, new RectInt(3, 1, 4, 4), new RectInt(4, 5, 2, 4));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label"), Is.EqualTo("band_2_left"));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_seconds"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_seconds"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_recorder_frame"), Is.EqualTo(0));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_x_delta"), Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_y_delta"), Is.EqualTo(0f).Within(0.00001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MaxBBoxNormalizedAttributionTouchesFrameEdge_When_BuildingDiagnostics_Then_RecordsClipContext()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateNormalizedKeypointCropContext_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 1.0,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.0,\n" +
                    "  \"avgBrightAreaRatio\": 0.4,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 1.0, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.0, \"brightAreaRatio\": 0.4 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 0, 4, 10));
                WriteFixturePng(frontA, new RectInt(3, 1, 4, 4), new RectInt(4, 5, 2, 4));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_touches_frame_edge"), Is.True);
                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_touches_frame_edge"), Is.False);
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_bottom_gap"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_bottom_gap"), Is.EqualTo(0.1f).Within(0.00001f));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_TimeMatchedSamplesIncludeFrameEdgeTouch_When_BuildingDiagnostics_Then_RecordsCropSafeKeypointAggregate()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCropSafeKeypointAggregate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refEdge = Path.Combine(frameFolder, "ref-edge.png");
            string refSafe = Path.Combine(frameFolder, "ref-safe.png");
            string frontEdge = Path.Combine(frameFolder, "front-edge.png");
            string frontSafe = Path.Combine(frameFolder, "front-safe.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 2,\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"avgBBoxHeightRatio\": 0.9,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.36,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refEdge.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 1.0, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.0, \"brightAreaRatio\": 0.4 }},\n" +
                    $"    {{ \"seconds\": 1.0, \"framePath\": \"{refSafe.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.32 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refEdge, new RectInt(3, 0, 4, 10));
                WriteFixturePng(frontEdge, new RectInt(3, 1, 4, 4), new RectInt(4, 5, 2, 4));
                WriteFixturePng(refSafe, new RectInt(3, 1, 4, 8));
                WriteFixturePng(frontSafe, new RectInt(3, 1, 4, 8));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"edge,Main_Auto,start,0,front,{frontEdge}\n" +
                    $"safe,Main_Auto,middle,30,front,{frontSafe}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_avg_bbox_width_ratio_abs_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_avg_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_basis"), Does.Contain("edge-touch"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_FrameEdgeTouchOnlyAffectsVerticalCap_When_BuildingDiagnostics_Then_RecordsKeypointLocalCropSafeAggregate()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybKeypointLocalCropSafeAggregate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refEdge = Path.Combine(frameFolder, "ref-edge.png");
            string frontEdge = Path.Combine(frameFolder, "front-edge.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 1.0,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.0,\n" +
                    "  \"avgBrightAreaRatio\": 0.4,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refEdge.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 1.0, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.0, \"brightAreaRatio\": 0.4 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refEdge, new RectInt(3, 0, 4, 10));
                WriteFixturePng(frontEdge, new RectInt(3, 0, 4, 5), new RectInt(4, 5, 2, 5));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"edge,Main_Auto,start,0,front,{frontEdge}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_sample_count"), Is.EqualTo(0));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count"), Is.EqualTo(4));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count"), Is.EqualTo(6));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_keypoint_local_crop_safe_basis"), Does.Contain("keypoint-local"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_HairLikeSilhouetteExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybNonHairKeypointAggregate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refFrame = Path.Combine(frameFolder, "ref-hair.png");
            string candidateFrame = Path.Combine(frameFolder, "candidate-hair.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.6,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.34,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refFrame.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.6, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.34 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePngWithColor(
                    refFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(0, 210, 210, 255)));
                WriteFixturePngWithColor(
                    candidateFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(0, 210, 210, 255)),
                    new FixturePngFill(new RectInt(8, 5, 1, 2), new Color32(0, 210, 210, 255)));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"hair,Main_Auto,start,0,front,{candidateFrame}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label"), Is.EqualTo("band_2_right"));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_count"), Is.EqualTo(10));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_avg_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_basis"), Does.Contain("cyan/teal hair-like"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_DarkTealHairShadowExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybDarkHairShadowKeypointAggregate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refFrame = Path.Combine(frameFolder, "ref-dark-hair.png");
            string candidateFrame = Path.Combine(frameFolder, "candidate-dark-hair.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.6,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.34,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refFrame.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.6, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.34 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePngWithColor(
                    refFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(25, 52, 54, 255)));
                WriteFixturePngWithColor(
                    candidateFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(25, 52, 54, 255)),
                    new FixturePngFill(new RectInt(8, 5, 1, 2), new Color32(25, 52, 54, 255)));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"darkhair,Main_Auto,start,0,front,{candidateFrame}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label"), Is.EqualTo("band_2_right"));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_count"), Is.EqualTo(10));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_avg_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_basis"), Does.Contain("dark teal hair-shadow"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_NonHairSilhouetteExtendsCandidateBand_When_BuildingDiagnostics_Then_RecordsNonHairMaxAttribution()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybNonHairMaxAttribution_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refFrame = Path.Combine(frameFolder, "ref-nonhair.png");
            string candidateFrame = Path.Combine(frameFolder, "candidate-nonhair.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.6,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.34,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refFrame.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.6, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.34 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePngWithColor(
                    refFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(255, 255, 255, 255)));
                WriteFixturePngWithColor(
                    candidateFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 5, 1, 2), new Color32(255, 255, 255, 255)));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"nonhair,Main_Auto,start,0,front,{candidateFrame}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_label"), Is.EqualTo("band_2_right"));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_index"), Is.EqualTo(7));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_seconds"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_seconds"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_recorder_frame"), Is.EqualTo(0));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_x_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_y_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_x"), Is.GreaterThan(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_x")));
                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_touches_frame_edge"), Is.False);
                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_touches_frame_edge"), Is.False);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_NonHairFrameEdgeTouchStillLeavesMiddleBandResidual_When_BuildingDiagnostics_Then_RecordsNonHairKeypointLocalCropSafeAggregate()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybNonHairKeypointLocalCropSafe_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refFrame = Path.Combine(frameFolder, "ref-nonhair-edge.png");
            string candidateFrame = Path.Combine(frameFolder, "candidate-nonhair-edge.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 1.0,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.0,\n" +
                    "  \"avgBrightAreaRatio\": 0.4,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refFrame.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 1.0, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.0, \"brightAreaRatio\": 0.4 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePngWithColor(
                    refFrame,
                    new FixturePngFill(new RectInt(3, 0, 4, 10), new Color32(255, 255, 255, 255)));
                WriteFixturePngWithColor(
                    candidateFrame,
                    new FixturePngFill(new RectInt(3, 0, 4, 10), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 5, 1, 3), new Color32(255, 255, 255, 255)));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"nonhair-edge,Main_Auto,start,0,front,{candidateFrame}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_touches_frame_edge"), Is.True);
                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_touches_frame_edge"), Is.True);
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count"), Is.EqualTo(4));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count"), Is.EqualTo(6));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_index"), Is.GreaterThanOrEqualTo(0));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_x_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_y_delta"), Is.LessThan(0.001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_candidate_x"), Is.Not.EqualTo(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_reference_x")).Within(0.001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_required_x_reduction_to_threshold"), Is.GreaterThan(0f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label"), Is.EqualTo("band_1_right"));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_basis"), Does.Contain("non-hair"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_RawCandidateFailsAndCorrectedCandidatePasses_When_BuildingCandidateArtifactSelection_Then_SelectsCorrectedWithoutHidingRaw()
        {
            var raw = new MotionComparisonFrameQualitySummary
            {
                frame_quality_evaluation_role = "evaluation_candidate_metrics",
                status = "fail",
                status_reason = "same-frame hips Y delta warning threshold exceeded",
                candidate_metrics_csv = "raw.csv",
                candidate_vmd_path = "raw.vmd"
            };
            var corrected = new MotionComparisonFrameQualitySummary
            {
                frame_quality_evaluation_role = "corrected_candidate_metrics",
                status = "pass",
                status_reason = "same-frame Unity metrics and VMD export checks stayed within thresholds",
                candidate_metrics_csv = "corrected.csv",
                candidate_vmd_path = "corrected.vmd"
            };

            object selection = BuildCandidateArtifactSelection(raw, corrected);

            Assert.That(GetField<string>(selection, "selected_candidate_role"), Is.EqualTo("corrected_candidate_metrics"));
            Assert.That(GetField<string>(selection, "selected_candidate_status"), Is.EqualTo("pass"));
            Assert.That(GetField<string>(selection, "selected_candidate_vmd_path"), Is.EqualTo("corrected.vmd"));
            Assert.That(GetField<string>(selection, "selected_candidate_metrics_csv"), Is.EqualTo("corrected.csv"));
            Assert.That(GetField<string>(selection, "raw_candidate_status"), Is.EqualTo("fail"));
            Assert.That(GetField<string>(selection, "raw_candidate_vmd_path"), Is.EqualTo("raw.vmd"));
            Assert.That(GetField<string>(selection, "raw_candidate_status_reason"), Does.Contain("hips Y"));
            Assert.That(GetField<string>(selection, "selection_basis"), Does.Contain("raw candidate remains"));
            Assert.That(GetField<string>(selection, "selected_candidate_output_role"), Is.EqualTo("user_facing_export_artifact"));
            Assert.That(GetField<bool>(selection, "selected_candidate_preserves_raw_diagnostic"), Is.True);
        }

        [Test]
        public void Given_SelectedCorrectedCandidateFilesExist_When_BuildingCandidateArtifactSelection_Then_MarksFinalExportAcceptanceArtifact()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybMmdExportSafetyDefaultsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string rawVmdPath = Path.Combine(root, "raw.vmd");
            string correctedVmdPath = Path.Combine(root, "corrected.vmd");
            string correctedMetricsPath = Path.Combine(root, "corrected.csv");
            string correctedManifestPath = Path.Combine(root, "corrected.json");

            try
            {
                File.WriteAllText(rawVmdPath, "raw-vmd");
                File.WriteAllText(correctedVmdPath, "corrected-vmd");
                File.WriteAllText(correctedMetricsPath, "metrics");
                File.WriteAllText(correctedManifestPath, "manifest");

                var raw = new MotionComparisonFrameQualitySummary
                {
                    frame_quality_evaluation_role = "evaluation_candidate_metrics",
                    status = "fail",
                    status_reason = "same-frame hips Y delta warning threshold exceeded",
                    candidate_metrics_csv = Path.Combine(root, "raw.csv"),
                    candidate_vmd_path = rawVmdPath,
                    vertical_solve_corrected_candidate_manifest_path = correctedManifestPath
                };
                var corrected = new MotionComparisonFrameQualitySummary
                {
                    frame_quality_evaluation_role = "corrected_candidate_metrics",
                    status = "pass",
                    status_reason = "same-frame Unity metrics and VMD export checks stayed within thresholds",
                    candidate_metrics_csv = correctedMetricsPath,
                    candidate_vmd_path = correctedVmdPath
                };

                object selection = BuildCandidateArtifactSelection(raw, corrected);

                Assert.That(GetField<string>(selection, "selected_candidate_output_role"), Is.EqualTo("user_facing_export_artifact"));
                Assert.That(GetField<bool>(selection, "selected_candidate_is_acceptance_artifact"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_vmd_exists"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_metrics_exists"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_manifest_exists"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_differs_from_raw_vmd"), Is.True);
                Assert.That(GetField<string>(selection, "selected_candidate_manifest_path"), Is.EqualTo(correctedManifestPath));
                Assert.That(GetField<string>(selection, "selected_candidate_acceptance_basis"), Does.Contain("final acceptance/export candidate"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CorrectedMetricsPassAndVmdIsRawCopy_When_BuildingCandidateArtifactSelection_Then_KeepsDiagnosticOnly()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybMmdExportSafetyDefaultsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string rawVmdPath = Path.Combine(root, "raw.vmd");
            string correctedVmdPath = Path.Combine(root, "corrected.vmd");
            string rawMetricsPath = Path.Combine(root, "raw.csv");
            string correctedMetricsPath = Path.Combine(root, "corrected.csv");
            string correctedManifestPath = Path.Combine(root, "corrected.json");

            try
            {
                File.WriteAllText(rawVmdPath, "same-vmd");
                File.WriteAllText(correctedVmdPath, "same-vmd");
                File.WriteAllText(rawMetricsPath, "raw-vertical-metrics");
                File.WriteAllText(correctedMetricsPath, "corrected-vertical-metrics");
                File.WriteAllText(correctedManifestPath, "manifest");

                var raw = new MotionComparisonFrameQualitySummary
                {
                    frame_quality_evaluation_role = "evaluation_candidate_metrics",
                    status = "fail",
                    status_reason = "same-frame foot bottom Y delta fail threshold exceeded",
                    candidate_metrics_csv = rawMetricsPath,
                    candidate_vmd_path = rawVmdPath,
                    vertical_solve_corrected_candidate_manifest_path = correctedManifestPath
                };
                var corrected = new MotionComparisonFrameQualitySummary
                {
                    frame_quality_evaluation_role = "corrected_candidate_metrics",
                    status = "pass",
                    status_reason = "same-frame Unity metrics and VMD export checks stayed within thresholds",
                    candidate_metrics_csv = correctedMetricsPath,
                    candidate_vmd_path = correctedVmdPath
                };

                object selection = BuildCandidateArtifactSelection(raw, corrected);

                Assert.That(GetField<string>(selection, "selected_candidate_role"), Is.EqualTo("corrected_candidate_metrics"));
                Assert.That(GetField<bool>(selection, "selected_candidate_differs_from_raw_vmd"), Is.False);
                Assert.That(GetField<bool>(selection, "selected_candidate_differs_from_raw_metrics"), Is.True);
                Assert.That(GetField<string>(selection, "selected_candidate_output_role"), Is.EqualTo("diagnostic_artifact"));
                Assert.That(GetField<bool>(selection, "selected_candidate_is_acceptance_artifact"), Is.False);
                Assert.That(GetField<string>(selection, "selected_candidate_acceptance_basis"), Does.Contain("raw-copy VMD"));
                Assert.That(GetField<string>(selection, "selection_basis"), Does.Contain("diagnostic evidence"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_SelectedCorrectedCandidateManifestIsMissing_When_BuildingCandidateArtifactSelection_Then_WritesManifestAndMarksAcceptanceArtifact()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybMmdExportSafetyDefaultsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string rawVmdPath = Path.Combine(root, "raw.vmd");
            string correctedVmdPath = Path.Combine(root, "corrected.vmd");
            string rawMetricsPath = Path.Combine(root, "raw.csv");
            string correctedMetricsPath = Path.Combine(root, "corrected.csv");
            string correctedManifestPath = Path.Combine(root, "corrected.json");

            try
            {
                File.WriteAllText(rawVmdPath, "raw-vmd");
                File.WriteAllText(correctedVmdPath, "corrected-vmd");
                File.WriteAllText(rawMetricsPath, "raw-vertical-metrics");
                File.WriteAllText(correctedMetricsPath, "corrected-vertical-metrics");

                var raw = new MotionComparisonFrameQualitySummary
                {
                    frame_quality_evaluation_role = "evaluation_candidate_metrics",
                    status = "fail",
                    status_reason = "same-frame foot bottom Y delta fail threshold exceeded",
                    candidate_metrics_csv = rawMetricsPath,
                    candidate_vmd_path = rawVmdPath,
                    vertical_solve_corrected_candidate_manifest_path = correctedManifestPath
                };
                var corrected = new MotionComparisonFrameQualitySummary
                {
                    frame_quality_evaluation_role = "corrected_candidate_metrics",
                    status = "pass",
                    status_reason = "same-frame Unity metrics and VMD export checks stayed within thresholds",
                    candidate_metrics_csv = correctedMetricsPath,
                    candidate_vmd_path = correctedVmdPath
                };

                object selection = BuildCandidateArtifactSelection(raw, corrected);

                Assert.That(File.Exists(correctedManifestPath), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_manifest_exists"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_is_acceptance_artifact"), Is.True);
                string manifest = File.ReadAllText(correctedManifestPath);
                Assert.That(manifest, Does.Contain("\"artifact_role\":\"corrected_vertical_solve_candidate\""));
                Assert.That(manifest, Does.Contain(EscapeJsonPath(rawMetricsPath)));
                Assert.That(manifest, Does.Contain(EscapeJsonPath(rawVmdPath)));
                Assert.That(manifest, Does.Contain(EscapeJsonPath(correctedMetricsPath)));
                Assert.That(manifest, Does.Contain(EscapeJsonPath(correctedVmdPath)));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_RawMainAutoCandidatePasses_When_BuildingCandidateArtifactSelection_Then_MarksRawExportAcceptanceArtifact()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybMmdExportSafetyDefaultsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string metricsPath = Path.Combine(root, "main-auto.csv");
            string vmdPath = Path.Combine(root, "main-auto.vmd");

            try
            {
                File.WriteAllText(metricsPath, "main-auto-metrics");
                File.WriteAllText(vmdPath, "main-auto-vmd");
                var raw = new MotionComparisonFrameQualitySummary
                {
                    candidate_label = "Main_Auto YYB automatic path",
                    frame_quality_evaluation_role = "raw_candidate_metrics",
                    status = "pass",
                    status_reason = "same-frame Unity metrics and VMD export checks stayed within thresholds",
                    candidate_metrics_csv = metricsPath,
                    candidate_vmd_path = vmdPath
                };

                object selection = BuildCandidateArtifactSelection(raw);

                Assert.That(GetField<string>(selection, "selected_candidate_role"), Is.EqualTo("raw_candidate_metrics"));
                Assert.That(GetField<string>(selection, "selected_candidate_output_role"), Is.EqualTo("user_facing_export_artifact"));
                Assert.That(GetField<bool>(selection, "selected_candidate_vmd_exists"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_metrics_exists"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_manifest_exists"), Is.False);
                Assert.That(GetField<bool>(selection, "selected_candidate_preserves_raw_diagnostic"), Is.False);
                Assert.That(GetField<bool>(selection, "selected_candidate_is_acceptance_artifact"), Is.True);
                Assert.That(GetField<string>(selection, "selected_candidate_acceptance_basis"), Does.Contain("raw VMD/metrics"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CorrectedCandidateDoesNotPass_When_BuildingCandidateArtifactSelection_Then_KeepsRawAsSelectedCandidate()
        {
            var raw = new MotionComparisonFrameQualitySummary
            {
                frame_quality_evaluation_role = "evaluation_candidate_metrics",
                status = "fail",
                status_reason = "same-frame hips Y delta warning threshold exceeded",
                candidate_metrics_csv = "raw.csv",
                candidate_vmd_path = "raw.vmd"
            };
            var corrected = new MotionComparisonFrameQualitySummary
            {
                frame_quality_evaluation_role = "corrected_candidate_metrics",
                status = "fail",
                status_reason = "below-floor foot/IK sample detected",
                candidate_metrics_csv = "corrected.csv",
                candidate_vmd_path = "corrected.vmd"
            };

            object selection = BuildCandidateArtifactSelection(raw, corrected);

            Assert.That(GetField<string>(selection, "selected_candidate_role"), Is.EqualTo("evaluation_candidate_metrics"));
            Assert.That(GetField<string>(selection, "selected_candidate_status"), Is.EqualTo("fail"));
            Assert.That(GetField<string>(selection, "selected_candidate_vmd_path"), Is.EqualTo("raw.vmd"));
            Assert.That(GetField<string>(selection, "corrected_candidate_status"), Is.EqualTo("fail"));
            Assert.That(GetField<string>(selection, "selection_basis"), Does.Contain("corrected candidate is not passing"));
        }

        [Test]
        public void Given_IntegratedVerticalSolveOutputPasses_When_BuildingCandidateArtifactSelection_Then_MarksPrimaryOutputAsAcceptanceArtifact()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybMmdExportSafetyDefaultsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string metricsPath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            string rawDiagnosticVmdPath = Path.Combine(root, "main.raw_vertical_solve_diagnostic.vmd");
            string manifestPath = Path.Combine(root, "main.integrated_vertical_solve_primary_export.json");

            try
            {
                File.WriteAllText(metricsPath, "corrected-main-auto-metrics");
                File.WriteAllText(vmdPath, "corrected-main-auto-vmd");
                File.WriteAllText(rawDiagnosticVmdPath, "raw-main-auto-vmd");
                WriteIntegratedPrimaryExportManifest(manifestPath, rawDiagnosticVmdPath);
                var integrated = new MotionComparisonFrameQualitySummary
                {
                    frame_quality_evaluation_role = "main_auto_integrated_vertical_solve_metrics",
                    status = "pass",
                    status_reason = "same-frame Unity metrics and VMD export checks stayed within thresholds",
                    candidate_metrics_csv = metricsPath,
                    candidate_vmd_path = vmdPath,
                    vertical_solve_corrected_candidate_manifest_path = manifestPath
                };

                object selection = BuildCandidateArtifactSelection(integrated);

                Assert.That(GetField<string>(selection, "selected_candidate_role"), Is.EqualTo("main_auto_integrated_vertical_solve_metrics"));
                Assert.That(GetField<string>(selection, "selected_candidate_status"), Is.EqualTo("pass"));
                Assert.That(GetField<string>(selection, "selected_candidate_output_role"), Is.EqualTo("user_facing_export_artifact"));
                Assert.That(GetField<bool>(selection, "selected_candidate_vmd_exists"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_metrics_exists"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_differs_from_raw_vmd"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_is_acceptance_artifact"), Is.True);
                Assert.That(GetField<string>(selection, "selected_candidate_acceptance_basis"), Does.Contain("primary Main_Auto export"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_IntegratedVerticalSolveOutputMatchesRawDiagnostic_When_BuildingCandidateArtifactSelection_Then_DoesNotMarkAcceptanceArtifact()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybMmdExportSafetyDefaultsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string metricsPath = Path.Combine(root, "main.csv");
            string vmdPath = Path.Combine(root, "main.vmd");
            string rawDiagnosticVmdPath = Path.Combine(root, "main.raw_vertical_solve_diagnostic.vmd");
            string manifestPath = Path.Combine(root, "main.integrated_vertical_solve_primary_export.json");

            try
            {
                File.WriteAllText(metricsPath, "main-auto-metrics");
                File.WriteAllText(vmdPath, "same-main-auto-vmd");
                File.WriteAllText(rawDiagnosticVmdPath, "same-main-auto-vmd");
                WriteIntegratedPrimaryExportManifest(manifestPath, rawDiagnosticVmdPath);
                var integrated = new MotionComparisonFrameQualitySummary
                {
                    frame_quality_evaluation_role = "main_auto_integrated_vertical_solve_metrics",
                    status = "pass",
                    status_reason = "same-frame Unity metrics and VMD export checks stayed within thresholds",
                    candidate_metrics_csv = metricsPath,
                    candidate_vmd_path = vmdPath,
                    vertical_solve_corrected_candidate_manifest_path = manifestPath
                };

                object selection = BuildCandidateArtifactSelection(integrated);

                Assert.That(GetField<string>(selection, "selected_candidate_role"), Is.EqualTo("main_auto_integrated_vertical_solve_metrics"));
                Assert.That(GetField<bool>(selection, "selected_candidate_vmd_exists"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_metrics_exists"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_manifest_exists"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_differs_from_raw_vmd"), Is.False);
                Assert.That(GetField<bool>(selection, "selected_candidate_is_acceptance_artifact"), Is.False);
                Assert.That(GetField<string>(selection, "selected_candidate_acceptance_basis"), Does.Contain("not a final acceptance/export artifact"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MainRecordingAndMainAutoSummaries_When_BuildingCandidateArtifactSelection_Then_SelectsMainAutoAcceptanceArtifact()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybMmdExportSafetyDefaultsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string recordingMetricsPath = Path.Combine(root, "main-recording.csv");
            string recordingVmdPath = Path.Combine(root, "main-recording.vmd");
            string mainAutoMetricsPath = Path.Combine(root, "main-auto.csv");
            string mainAutoVmdPath = Path.Combine(root, "main-auto.vmd");
            string mainAutoRawDiagnosticVmdPath = Path.Combine(root, "main-auto.raw_vertical_solve_diagnostic.vmd");
            string mainAutoManifestPath = Path.Combine(root, "main-auto.integrated_vertical_solve_primary_export.json");

            try
            {
                File.WriteAllText(recordingMetricsPath, "main-recording-metrics");
                File.WriteAllText(recordingVmdPath, "main-recording-vmd");
                File.WriteAllText(mainAutoMetricsPath, "main-auto-metrics");
                File.WriteAllText(mainAutoVmdPath, "main-auto-vmd");
                File.WriteAllText(mainAutoRawDiagnosticVmdPath, "main-auto-raw-vmd");
                WriteIntegratedPrimaryExportManifest(mainAutoManifestPath, mainAutoRawDiagnosticVmdPath);
                var mainRecording = new MotionComparisonFrameQualitySummary
                {
                    candidate_label = "Main_Recoding YYB 자동 경로",
                    frame_quality_evaluation_role = "evaluation_candidate_metrics",
                    status = "pass",
                    candidate_metrics_csv = recordingMetricsPath,
                    candidate_vmd_path = recordingVmdPath
                };
                var mainAuto = new MotionComparisonFrameQualitySummary
                {
                    candidate_label = "Main_Auto YYB 자동 경로",
                    frame_quality_evaluation_role = "main_auto_integrated_vertical_solve_metrics",
                    status = "pass",
                    candidate_metrics_csv = mainAutoMetricsPath,
                    candidate_vmd_path = mainAutoVmdPath,
                    vertical_solve_corrected_candidate_manifest_path = mainAutoManifestPath
                };

                object selection = BuildCandidateArtifactSelection(mainRecording, mainAuto);

                Assert.That(GetField<string>(selection, "selected_candidate_role"), Is.EqualTo("main_auto_integrated_vertical_solve_metrics"));
                Assert.That(GetField<string>(selection, "selected_candidate_metrics_csv"), Is.EqualTo(mainAutoMetricsPath));
                Assert.That(GetField<string>(selection, "selected_candidate_vmd_path"), Is.EqualTo(mainAutoVmdPath));
                Assert.That(GetField<bool>(selection, "selected_candidate_differs_from_raw_vmd"), Is.True);
                Assert.That(GetField<bool>(selection, "selected_candidate_is_acceptance_artifact"), Is.True);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_FrameQualitySummaryFails_When_BuildingCompletionFailures_Then_PromotesToRunFailure()
        {
            var mainAuto = new MotionComparisonFrameQualitySummary
            {
                candidate_label = "Main_Auto YYB automatic path",
                frame_quality_evaluation_role = "main_auto_integrated_vertical_solve_metrics",
                status = "fail",
                status_reason = "same-frame limb pose delta threshold exceeded",
                candidate_metrics_csv = "main-auto.csv",
                candidate_vmd_path = "main-auto.vmd"
            };

            string[] failures = BuildFrameQualityFailureMessages(mainAuto);

            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures[0], Does.Contain("frame-quality gate failed"));
            Assert.That(failures[0], Does.Contain("Main_Auto YYB automatic path"));
            Assert.That(failures[0], Does.Contain("same-frame limb pose delta"));
        }

        [Test]
        public void Given_RawDiagnosticFailsButMainAutoIntegratedArtifactPasses_When_BuildingCompletionFailures_Then_DoesNotPromoteRawDiagnostic()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybMmdExportSafetyDefaultsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string rawMetricsPath = Path.Combine(root, "main-recording.csv");
            string rawVmdPath = Path.Combine(root, "main-recording.vmd");
            string mainAutoMetricsPath = Path.Combine(root, "main-auto.csv");
            string mainAutoVmdPath = Path.Combine(root, "main-auto.vmd");
            string mainAutoRawDiagnosticVmdPath = Path.Combine(root, "main-auto.raw_vertical_solve_diagnostic.vmd");
            string mainAutoManifestPath = Path.Combine(root, "main-auto.integrated_vertical_solve_primary_export.json");

            try
            {
                File.WriteAllText(rawMetricsPath, "raw-main-recording-metrics");
                File.WriteAllText(rawVmdPath, "raw-main-recording-vmd");
                File.WriteAllText(mainAutoMetricsPath, "main-auto-integrated-metrics");
                File.WriteAllText(mainAutoVmdPath, "main-auto-integrated-vmd");
                File.WriteAllText(mainAutoRawDiagnosticVmdPath, "main-auto-raw-vmd");
                WriteIntegratedPrimaryExportManifest(mainAutoManifestPath, mainAutoRawDiagnosticVmdPath);
                var mainRecordingRaw = new MotionComparisonFrameQualitySummary
                {
                    candidate_label = "Main_Recoding YYB automatic path",
                    frame_quality_evaluation_role = "evaluation_candidate_metrics",
                    status = "fail",
                    status_reason = "same-frame foot XZ delta fail threshold exceeded",
                    candidate_metrics_csv = rawMetricsPath,
                    candidate_vmd_path = rawVmdPath
                };
                var mainAutoIntegrated = new MotionComparisonFrameQualitySummary
                {
                    candidate_label = "Main_Auto YYB automatic path",
                    frame_quality_evaluation_role = "main_auto_integrated_vertical_solve_metrics",
                    status = "pass",
                    status_reason = "same-frame Unity metrics and VMD export checks stayed within thresholds",
                    candidate_metrics_csv = mainAutoMetricsPath,
                    candidate_vmd_path = mainAutoVmdPath,
                    vertical_solve_corrected_candidate_manifest_path = mainAutoManifestPath
                };

                string[] failures = BuildFrameQualityFailureMessages(mainRecordingRaw, mainAutoIntegrated);

                Assert.That(failures, Is.Empty);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_ImportedFbxVisualEvidenceMatchesReference_When_SubManualPoseGateDiffers_Then_DoesNotPromoteToRunFailure()
        {
            var mainAuto = new MotionComparisonFrameQualitySummary
            {
                candidate_label = "Main_Auto YYB automatic path",
                frame_quality_evaluation_role = "evaluation_candidate_metrics",
                status = "fail",
                status_reason = "same-frame limb pose delta threshold exceeded; same-frame foot bottom Y delta warning threshold exceeded",
                candidate_metrics_csv = "main-auto.csv",
                candidate_vmd_path = "main-auto.vmd"
            };

            string[] failures = BuildFrameQualityFailureMessages(
                new[] { mainAuto },
                BuildReferenceAlignedImportedFbxDiagnostics());

            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void Given_ImportedFbxVisualEvidenceHasSinglePixelEndpointQuantization_When_SubManualPoseGateDiffers_Then_DoesNotPromoteToRunFailure()
        {
            var mainAuto = new MotionComparisonFrameQualitySummary
            {
                candidate_label = "Main_Auto YYB automatic path",
                frame_quality_evaluation_role = "evaluation_candidate_metrics",
                status = "fail",
                status_reason = "same-frame limb pose delta threshold exceeded; same-frame foot bottom Y delta warning threshold exceeded",
                candidate_metrics_csv = "main-auto.csv",
                candidate_vmd_path = "main-auto.vmd"
            };
            object diagnostics = BuildReferenceAlignedImportedFbxDiagnostics();
            SetDiagnosticsField(
                diagnostics,
                "candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta",
                0.30078125f);

            string[] failures = BuildFrameQualityFailureMessages(
                new[] { mainAuto },
                diagnostics);

            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void Given_ReplayRawVerticalResidualHasReferenceAlignedCorrectedCandidate_When_BuildingCompletionFailures_Then_KeepsRawDiagnosticOnly()
        {
            var replayRaw = new MotionComparisonFrameQualitySummary
            {
                candidate_label = "Main_Recoding YYB VMD replay probe",
                frame_quality_evaluation_role = "evaluation_candidate_metrics",
                status = "fail",
                status_reason = "same-frame limb pose delta threshold exceeded; same-frame foot bottom Y delta fail threshold exceeded",
                candidate_metrics_csv = "vmd-replay.csv",
                candidate_vmd_path = "vmd-replay.vmd"
            };
            var replayCorrected = new MotionComparisonFrameQualitySummary
            {
                candidate_label = "Main_Recoding YYB VMD replay probe corrected_vertical_solve_candidate",
                frame_quality_evaluation_role = "corrected_candidate_metrics",
                status = "fail",
                status_reason = "same-frame limb pose delta threshold exceeded",
                candidate_metrics_csv = "vmd-replay.corrected.csv",
                candidate_vmd_path = "vmd-replay.corrected.vmd"
            };

            string[] failures = BuildFrameQualityFailureMessages(
                new[] { replayRaw, replayCorrected },
                BuildReferenceAlignedImportedFbxDiagnostics());

            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void Given_RawVerticalResidualHasReferenceAlignedCorrectedPass_When_BuildingCompletionFailures_Then_KeepsRawDiagnosticOnly()
        {
            var mainAutoRaw = new MotionComparisonFrameQualitySummary
            {
                candidate_label = "Main_Auto YYB automatic path",
                frame_quality_evaluation_role = "evaluation_candidate_metrics",
                status = "fail",
                status_reason = "same-frame hips Y delta warning threshold exceeded; same-frame foot bottom Y delta fail threshold exceeded",
                candidate_metrics_csv = "main-auto.csv",
                candidate_vmd_path = "main-auto.vmd"
            };
            var mainAutoCorrected = new MotionComparisonFrameQualitySummary
            {
                candidate_label = "Main_Auto YYB automatic path corrected_vertical_solve_candidate",
                frame_quality_evaluation_role = "corrected_candidate_metrics",
                status = "pass",
                status_reason = "corrected candidate metrics artifact stayed within thresholds under the raw frame_quality evaluator",
                candidate_metrics_csv = "main-auto.corrected.csv",
                candidate_vmd_path = "main-auto.corrected.vmd"
            };

            string[] failures = BuildFrameQualityFailureMessages(
                new[] { mainAutoRaw, mainAutoCorrected },
                BuildReferenceAlignedImportedFbxDiagnostics());

            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void Given_CaptureModes_When_CheckingSummaryCandidateMode_Then_IncludesBothMainScenes()
        {
            Assert.That(IsMainSceneCandidateMode("MainAuto"), Is.True);
            Assert.That(IsMainSceneCandidateMode("MainRecording"), Is.True);
            Assert.That(IsMainSceneCandidateMode("MainRecordingVmdPlaybackProbe"), Is.True);
            Assert.That(IsMainSceneCandidateMode("SubManualTestPrefab"), Is.False);
            Assert.That(IsMainSceneCandidateMode("SubManualYyb"), Is.False);
        }

        [Test]
        public void Given_MainSceneCandidateModes_When_ResolvingIntegratedVerticalSolveRole_Then_ReplayAndMainAutoUseSeparateRoles()
        {
            Assert.That(
                ResolveIntegratedVerticalSolveRole("MainAuto"),
                Is.EqualTo("main_auto_integrated_vertical_solve_metrics"));
            Assert.That(
                ResolveIntegratedVerticalSolveRole("MainRecordingVmdPlaybackProbe"),
                Is.EqualTo("vmd_replay_integrated_vertical_solve_metrics"));
            Assert.That(ResolveIntegratedVerticalSolveRole("MainRecording"), Is.Empty);
            Assert.That(ResolveIntegratedVerticalSolveRole("SubManualTestPrefab"), Is.Empty);
        }

        [Test]
        public void Given_MainSceneCandidateFailedButHasMetricsAndVmd_When_CheckingFrameQualityEligibility_Then_KeepsDiagnosticCandidate()
        {
            Assert.That(
                ShouldBuildFrameQualityDiagnostic(success: false, metricsCsvPath: "failed.csv", vmdPath: "failed.vmd"),
                Is.True);
            Assert.That(
                ShouldBuildFrameQualityDiagnostic(success: false, metricsCsvPath: "failed.csv", vmdPath: ""),
                Is.False);
            Assert.That(
                ShouldBuildFrameQualityDiagnostic(success: true, metricsCsvPath: "", vmdPath: ""),
                Is.True);
        }

        [Test]
        public void Given_VmdPlaybackProbeDisabled_When_BuildingCaptureJobs_Then_KeepsExistingFourJobSession()
        {
            string[] modes = BuildCaptureJobModes(enableVmdPlaybackProbeRuntimeOverride: false);

            Assert.That(modes, Is.EqualTo(new[]
            {
                "SubManualTestPrefab",
                "SubManualYyb",
                "MainRecording",
                "MainAuto"
            }));
        }

        [Test]
        public void Given_VmdPlaybackProbeEnabled_When_BuildingCaptureJobs_Then_AddsReplayCandidateAfterMainRecording()
        {
            string[] modes = BuildCaptureJobModes(enableVmdPlaybackProbeRuntimeOverride: true);

            Assert.That(modes, Is.EqualTo(new[]
            {
                "SubManualTestPrefab",
                "SubManualYyb",
                "MainRecording",
                "MainRecordingVmdPlaybackProbe",
                "MainAuto"
            }));
        }

        [Test]
        public void Given_VisualCompareSegmentTail_When_ResolvingSmokeSegment_Then_UsesTailCaptureWindow()
        {
            Assert.That(ResolveVisualCompareSmokeSegment("tail"), Is.EqualTo("Tail"));
            Assert.That(ResolveVisualCompareSmokeSegment("middle"), Is.EqualTo("Middle"));
            Assert.That(ResolveVisualCompareSmokeSegment(""), Is.EqualTo("Head"));
            Assert.That(ResolveVisualCompareSmokeSegment("unknown"), Is.EqualTo("Head"));
        }

        [Test]
        public void Given_VisualCompareSegmentTail_When_BuildingManualCapturePlan_Then_AlignsSubManualToTailWindow()
        {
            object plan = BuildManualAnimatorCapturePlan(
                "testPrefab",
                "neo_1_001.fbx",
                referenceClipLengthSeconds: 184.85f,
                requestedDurationSeconds: 31f,
                segment: "tail");

            Assert.That(GetField<float>(plan, "StartTimeSeconds"), Is.EqualTo(153.85f).Within(0.001f));
            Assert.That(GetField<float>(plan, "DurationSeconds"), Is.EqualTo(31f).Within(0.0001f));
            Assert.That(GetField<int>(plan, "TargetFrameCount"), Is.EqualTo(930));
            Assert.That(
                GetField<string>(plan, "OutputBaseName"),
                Is.EqualTo("testPrefab_neo_1_001_tail_31s_animtime"));
            Assert.That(
                GetField<string>(plan, "ComparisonLabel"),
                Is.EqualTo("manual_testPrefab_neo_1_001_tail_31s_animtime"));
        }

        [Test]
        public void Given_VisualCompareSegmentHead_When_BuildingManualCapturePlan_Then_KeepsLegacyHeadOutputName()
        {
            object plan = BuildManualAnimatorCapturePlan(
                "testPrefab",
                "neo_1_001.fbx",
                referenceClipLengthSeconds: 184.85f,
                requestedDurationSeconds: 31f,
                segment: "head");

            Assert.That(GetField<float>(plan, "StartTimeSeconds"), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(GetField<float>(plan, "DurationSeconds"), Is.EqualTo(31f).Within(0.0001f));
            Assert.That(GetField<int>(plan, "TargetFrameCount"), Is.EqualTo(930));
            Assert.That(
                GetField<string>(plan, "OutputBaseName"),
                Is.EqualTo("testPrefab_neo_1_001_31s_animtime"));
        }

        [Test]
        public void Given_VisualCompareSegmentMiddle_When_BuildingProbeSampleTimes_Then_ShiftsSamplesToReferenceClipWindow()
        {
            float referenceClipStartSeconds = 88.39167f;
            float requestedDurationSeconds = 31f;
            float[] referenceLocalSampleSeconds =
            {
                1.6083298f,
                11.6083298f,
                21.6083298f
            };

            float[] sampleTimes = BuildReferenceMp4AlignedProbeSampleTimes(
                referenceClipStartSeconds,
                requestedDurationSeconds,
                referenceLocalSampleSeconds);

            Assert.That(sampleTimes, Is.Ordered.Ascending);
            Assert.That(sampleTimes, Has.None.LessThan(referenceClipStartSeconds - 0.0001f));
            Assert.That(sampleTimes, Has.None.GreaterThan(referenceClipStartSeconds + requestedDurationSeconds + 0.0001f));
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + 3f);
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + 10f);
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + referenceLocalSampleSeconds[0]);
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + referenceLocalSampleSeconds[1]);
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + referenceLocalSampleSeconds[2]);
            AssertDoesNotContainTime(sampleTimes, 3f);
            AssertDoesNotContainTime(sampleTimes, 10f);
        }

        [Test]
        public void Given_ReferenceMp4SampleWithinHalfFrameOfDefaultSample_When_BuildingProbeSampleTimes_Then_DeduplicatesToSingleCaptureFrame()
        {
            float referenceClipStartSeconds = 176.78334f;
            float requestedDurationSeconds = 31f;
            float[] referenceLocalSampleSeconds =
            {
                3.2166595f,
                13.21666f,
                23.149658f
            };

            float[] sampleTimes = BuildReferenceMp4AlignedProbeSampleTimes(
                referenceClipStartSeconds,
                requestedDurationSeconds,
                referenceLocalSampleSeconds);

            int nearThirteenSecondSamples = sampleTimes.Count(time =>
                Mathf.Abs(time - (referenceClipStartSeconds + 13.2f)) <= (0.5f / 30f) + 0.0001f ||
                Mathf.Abs(time - (referenceClipStartSeconds + 13.21666f)) <= (0.5f / 30f) + 0.0001f);

            Assert.That(nearThirteenSecondSamples, Is.EqualTo(1));
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + 13.21666f);
            Assert.That(sampleTimes.Length, Is.EqualTo(9));
        }

        [Test]
        public void Given_ReferenceMmdTimingScale_When_BuildingProbeSampleTimes_Then_MapsReferenceSecondsToCandidateClipSeconds()
        {
            float candidateClipStartSeconds = 176.78334f;
            float requestedDurationSeconds = 31f;
            float candidateClipSecondsPerReferenceSecond = 207.78334f / (6001f / 30f);
            float[] referenceLocalSampleSeconds =
            {
                3.2166595f,
                13.21666f,
                23.149658f
            };

            float[] sampleTimes = BuildReferenceMp4AlignedProbeSampleTimes(
                candidateClipStartSeconds,
                requestedDurationSeconds,
                referenceLocalSampleSeconds,
                candidateClipSecondsPerReferenceSecond);

            Assert.That(sampleTimes, Is.Ordered.Ascending);
            AssertContainsTime(
                sampleTimes,
                candidateClipStartSeconds + (3.2166595f * candidateClipSecondsPerReferenceSecond));
            AssertContainsTime(
                sampleTimes,
                candidateClipStartSeconds + (23.149658f * candidateClipSecondsPerReferenceSecond));
            AssertDoesNotContainTime(sampleTimes, candidateClipStartSeconds + 3.2166595f);
            AssertDoesNotContainTime(sampleTimes, candidateClipStartSeconds + 23.149658f);
        }

        [Test]
        public void Given_MainRecordingStableCandidate_When_ExportIkSourceDiagnosticsExists_Then_CopiesDiagnosticsBesideStableVmd()
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            Type captureModeType = runnerType.GetNestedType("CaptureMode", BindingFlags.NonPublic);
            Type captureJobType = runnerType.GetNestedType("CaptureJob", BindingFlags.NonPublic);
            Assert.That(captureModeType, Is.Not.Null);
            Assert.That(captureJobType, Is.Not.Null);

            FieldInfo activeJobField = runnerType.GetField("_activeJob", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo summaryDirectoryField = runnerType.GetField("_summaryDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo buildMethod = runnerType.GetMethod(
                "BuildStableCandidateResult",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(VmdSaveResult) },
                modifiers: null);
            Assert.That(activeJobField, Is.Not.Null);
            Assert.That(summaryDirectoryField, Is.Not.Null);
            Assert.That(buildMethod, Is.Not.Null);

            object originalActiveJob = activeJobField.GetValue(null);
            string originalSummaryDirectory = (string)summaryDirectoryField.GetValue(null);
            string root = Path.Combine(Path.GetTempPath(), "YybStableCandidateDiagnostics_" + Guid.NewGuid().ToString("N"));
            string sourceDirectory = Path.Combine(root, "source");
            string summaryDirectory = Path.Combine(root, "summary");
            Directory.CreateDirectory(sourceDirectory);
            Directory.CreateDirectory(summaryDirectory);
            string sourceVmdPath = Path.Combine(sourceDirectory, "source.vmd");
            string sourceRotationCsvPath = Path.Combine(sourceDirectory, "source.export_rotation_diagnostics.csv");
            string sourceIkCsvPath = Path.Combine(sourceDirectory, "source.export_ik_source_samples.csv");

            try
            {
                File.WriteAllText(sourceVmdPath, "vmd");
                File.WriteAllText(sourceRotationCsvPath, "rotation");
                File.WriteAllText(sourceIkCsvPath, "ik-source");

                object captureJob = Activator.CreateInstance(captureJobType);
                captureJobType.GetField("Mode").SetValue(
                    captureJob,
                    Enum.Parse(captureModeType, "MainRecording"));
                captureJobType.GetField("ScenePath").SetValue(captureJob, "Assets/_Project/Scene/Main_Recoding.unity");
                captureJobType.GetField("SceneName").SetValue(captureJob, "Main_Recoding");
                captureJobType.GetField("DisplayName").SetValue(captureJob, "Main_Recoding YYB automatic path");

                activeJobField.SetValue(null, captureJob);
                summaryDirectoryField.SetValue(null, summaryDirectory);

                var sourceResult = VmdSaveResult.Ok(
                    sourceVmdPath,
                    frameCount: 3,
                    fileSizeBytes: new FileInfo(sourceVmdPath).Length,
                    exportRotationDiagnosticsCsvPath: sourceRotationCsvPath,
                    exportIkSourceDiagnosticsCsvPath: sourceIkCsvPath);

                var stableResult = (VmdSaveResult)buildMethod.Invoke(null, new object[] { sourceResult });

                Assert.That(Path.GetFileName(stableResult.FilePath), Is.EqualTo("vmd-rec.vmd"));
                Assert.That(File.Exists(stableResult.FilePath), Is.True);
                Assert.That(Path.GetFileName(stableResult.ExportIkSourceDiagnosticsCsvPath), Is.EqualTo("vmd-rec.export_ik_source_samples.csv"));
                Assert.That(File.Exists(stableResult.ExportIkSourceDiagnosticsCsvPath), Is.True);
                Assert.That(File.ReadAllText(stableResult.ExportIkSourceDiagnosticsCsvPath), Is.EqualTo("ik-source"));
                Assert.That(Path.GetFileName(stableResult.ExportRotationDiagnosticsCsvPath), Is.EqualTo("vmd-rec.export_rotation_diagnostics.csv"));
                Assert.That(File.Exists(stableResult.ExportRotationDiagnosticsCsvPath), Is.True);
            }
            finally
            {
                activeJobField.SetValue(null, originalActiveJob);
                summaryDirectoryField.SetValue(null, originalSummaryDirectory);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MainRecordingSmokeFailedButVmdExists_When_BuildingStableCandidate_Then_CopiesVmdAndKeepsFailure()
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            Type captureModeType = runnerType.GetNestedType("CaptureMode", BindingFlags.NonPublic);
            Type captureJobType = runnerType.GetNestedType("CaptureJob", BindingFlags.NonPublic);
            Assert.That(captureModeType, Is.Not.Null);
            Assert.That(captureJobType, Is.Not.Null);

            FieldInfo activeJobField = runnerType.GetField("_activeJob", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo summaryDirectoryField = runnerType.GetField("_summaryDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo buildMethod = runnerType.GetMethod(
                "BuildStableCandidateResult",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(VmdSaveResult) },
                modifiers: null);
            Assert.That(activeJobField, Is.Not.Null);
            Assert.That(summaryDirectoryField, Is.Not.Null);
            Assert.That(buildMethod, Is.Not.Null);

            object originalActiveJob = activeJobField.GetValue(null);
            string originalSummaryDirectory = (string)summaryDirectoryField.GetValue(null);
            string root = Path.Combine(Path.GetTempPath(), "YybStableCandidateFailure_" + Guid.NewGuid().ToString("N"));
            string sourceDirectory = Path.Combine(root, "source");
            string summaryDirectory = Path.Combine(root, "summary");
            Directory.CreateDirectory(sourceDirectory);
            Directory.CreateDirectory(summaryDirectory);
            string sourceVmdPath = Path.Combine(sourceDirectory, "source.vmd");

            try
            {
                File.WriteAllText(sourceVmdPath, "failed-but-usable-vmd");

                object captureJob = Activator.CreateInstance(captureJobType);
                captureJobType.GetField("Mode").SetValue(
                    captureJob,
                    Enum.Parse(captureModeType, "MainRecording"));
                captureJobType.GetField("ScenePath").SetValue(captureJob, "Assets/_Project/Scene/Main_Recoding.unity");
                captureJobType.GetField("SceneName").SetValue(captureJob, "Main_Recoding");
                captureJobType.GetField("DisplayName").SetValue(captureJob, "Main_Recoding YYB automatic path");

                activeJobField.SetValue(null, captureJob);
                summaryDirectoryField.SetValue(null, summaryDirectory);

                var sourceResult = new VmdSaveResult
                {
                    Success = false,
                    FilePath = sourceVmdPath,
                    ErrorMessage = "YYB deformation risk 0.365 > 0.35",
                    FrameCount = 930,
                    FileSizeBytes = new FileInfo(sourceVmdPath).Length
                };

                var stableResult = (VmdSaveResult)buildMethod.Invoke(null, new object[] { sourceResult });

                Assert.That(stableResult.Success, Is.False);
                Assert.That(stableResult.ErrorMessage, Is.EqualTo(sourceResult.ErrorMessage));
                Assert.That(stableResult.FrameCount, Is.EqualTo(930));
                Assert.That(Path.GetFileName(stableResult.FilePath), Is.EqualTo("vmd-rec.vmd"));
                Assert.That(File.Exists(stableResult.FilePath), Is.True);
                Assert.That(stableResult.FileSizeBytes, Is.EqualTo(new FileInfo(stableResult.FilePath).Length));
            }
            finally
            {
                activeJobField.SetValue(null, originalActiveJob);
                summaryDirectoryField.SetValue(null, originalSummaryDirectory);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_ActiveCaptureJobIsUnfinished_When_CheckingStartNextJobGate_Then_IgnoresDuplicateAdvance()
        {
            Assert.That(CanStartNextJob(isRunning: true, hasActiveJob: true, activeJobFinished: false), Is.False);
            Assert.That(CanStartNextJob(isRunning: true, hasActiveJob: true, activeJobFinished: true), Is.True);
            Assert.That(CanStartNextJob(isRunning: true, hasActiveJob: false, activeJobFinished: false), Is.True);
            Assert.That(CanStartNextJob(isRunning: false, hasActiveJob: false, activeJobFinished: false), Is.False);
        }

        [Test]
        public void Given_SubManualYybRecorderIsInactive_When_SelectingManualRecorder_Then_ActivatesOnlyTargetRecorder()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject testPrefab = null;
            GameObject yyb = null;
            try
            {
                testPrefab = new GameObject("testPrefab");
                testPrefab.AddComponent<HumanoidSampleCode>();
                yyb = new GameObject("YYB Hatsune Miku_default_1.0ver");
                HumanoidSampleCode yybRecorder = yyb.AddComponent<HumanoidSampleCode>();
                yyb.SetActive(false);

                HumanoidSampleCode selected = SelectActiveManualRecorder("YYB Hatsune Miku_default_1.0ver");

                Assert.That(selected, Is.SameAs(yybRecorder));
                Assert.That(yyb.activeSelf, Is.True, "Sub_Manual YYB capture must enable the YYB recorder before StartAutoRecording starts coroutines.");
                Assert.That(yyb.activeInHierarchy, Is.True, "The selected YYB recorder must be active in hierarchy.");
                Assert.That(testPrefab.activeSelf, Is.False, "Sub_Manual capture must keep only one manual target visible.");
            }
            finally
            {
                if (testPrefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(testPrefab);
                }
                if (yyb != null)
                {
                    UnityEngine.Object.DestroyImmediate(yyb);
                }
            }
        }

        [Test]
        public void Given_AutoRecordingWasStartedBeforeStart_When_HumanoidSampleCodeStartRuns_Then_DoesNotClearRecordingSession()
        {
            GameObject target = null;
            try
            {
                target = new GameObject("manual-recorder");
                HumanoidSampleCode sampleCode = target.AddComponent<HumanoidSampleCode>();
                FieldInfo activeField = typeof(HumanoidSampleCode).GetField(
                    "_isRecordingSessionActive",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(activeField, Is.Not.Null);
                activeField.SetValue(sampleCode, true);

                MethodInfo startMethod = typeof(HumanoidSampleCode).GetMethod(
                    "Start",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(startMethod, Is.Not.Null);
                startMethod.Invoke(sampleCode, null);

                Assert.That(
                    activeField.GetValue(sampleCode),
                    Is.EqualTo(true),
                    "HumanoidSampleCode.Start must not call SetReady over an already-started runner recording session.");
            }
            finally
            {
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        [Test]
        public void Given_ProbeScreenshotFramingOverride_When_Applied_Then_ClampsPaddingAndViewportCenter()
        {
            GameObject target = null;
            try
            {
                target = new GameObject("diagnostic-probe");
                MotionComparisonProbe probe = target.AddComponent<MotionComparisonProbe>();

                MethodInfo method = typeof(MotionComparisonProbe).GetMethod(
                    "SetScreenshotFraming",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: new[] { typeof(float), typeof(float) },
                    modifiers: null);

                Assert.That(method, Is.Not.Null, "Diagnostic screenshot framing must be overrideable without changing production defaults.");

                method.Invoke(probe, new object[] { 0.1f, 1.5f });

                Assert.That(GetProperty<float>(probe, "ScreenshotPadding"), Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(GetProperty<float>(probe, "ScreenshotVerticalViewportCenter"), Is.EqualTo(1f).Within(0.0001f));

                method.Invoke(probe, new object[] { 0.75f, 0.4f });

                Assert.That(GetProperty<float>(probe, "ScreenshotPadding"), Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(GetProperty<float>(probe, "ScreenshotVerticalViewportCenter"), Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        [Test]
        public void Given_RecordingDiagnosticsFramingOverride_When_StartingProbe_Then_AppliesToComparisonProbe()
        {
            GameObject target = null;
            try
            {
                target = new GameObject("diagnostic-recorder");
                HumanoidSampleCode sampleCode = target.AddComponent<HumanoidSampleCode>();

                MethodInfo setDiagnosticsMethod = typeof(HumanoidSampleCode).GetMethod(
                    "SetRecordingDiagnostics",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: new[]
                    {
                        typeof(bool),
                        typeof(bool),
                        typeof(bool),
                        typeof(float[]),
                        typeof(int),
                        typeof(int),
                        typeof(float),
                        typeof(float)
                    },
                    modifiers: null);
                Assert.That(setDiagnosticsMethod, Is.Not.Null, "Recorder diagnostics must pass screenshot framing overrides to MotionComparisonProbe.");

                setDiagnosticsMethod.Invoke(
                    sampleCode,
                    new object[] { true, false, false, null, 1920, 1080, 0.75f, 0.4f });

                MethodInfo startComparisonProbeMethod = typeof(HumanoidSampleCode).GetMethod(
                    "StartComparisonProbe",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(startComparisonProbeMethod, Is.Not.Null);
                startComparisonProbeMethod.Invoke(sampleCode, new object[] { "diagnostic" });

                MotionComparisonProbe probe = target.GetComponent<MotionComparisonProbe>();
                Assert.That(probe, Is.Not.Null);
                Assert.That(probe.ScreenshotWidth, Is.EqualTo(1920));
                Assert.That(probe.ScreenshotHeight, Is.EqualTo(1080));
                Assert.That(GetProperty<float>(probe, "ScreenshotPadding"), Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(GetProperty<float>(probe, "ScreenshotVerticalViewportCenter"), Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        [Test]
        public void Given_HeadWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock()
        {
            MethodInfo method = typeof(MotionComparisonProbe).GetMethod(
                "ResolveDiagnosticSampleClock",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Head-window visual compare sampling must use the primed animation clock so t3/t6/t10 stay on the intended clip time.");

            float clock = (float)method.Invoke(
                null,
                new object[] { true, true, new[] { 0f, 3f, 6f }, 90, 3.025f, 3.0333333f });

            Assert.That(clock, Is.EqualTo(3.025f).Within(0.0001f));
        }

        [Test]
        public void Given_NonZeroClipWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock()
        {
            MethodInfo method = typeof(MotionComparisonProbe).GetMethod(
                "ResolveDiagnosticSampleClock",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            float clock = (float)method.Invoke(
                null,
                new object[] { true, true, new[] { 88f, 91f, 98f }, 90, 91.025f, 3.0333333f });

            Assert.That(clock, Is.EqualTo(91.025f).Within(0.0001f));
        }

        [Test]
        public void Given_MetricsCsv_When_BuildingSampleOrderingDiagnostics_Then_ReportsFrameZeroPrewarmAndGroundingOrdering()
        {
            string tempCsv = Path.Combine(
                Path.GetTempPath(),
                "yyb-sample-ordering-diagnostics-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                File.WriteAllText(
                    tempCsv,
                    string.Join(
                        Environment.NewLine,
                        "reason,timeSinceLevelLoad,frameCount,recorderFrame,animationClipTime,retargetGroundingVerticalStepLast,retargetGroundingInitialVerticalStep,retargetGroundingStepClampCount,retargetGroundingSmoothedCount",
                        "start,1.5,120,0,0,0.1,0.45,12,60",
                        "finish,201.1,7208,6001,200,0.01,0.45,2196,5620"));

                object diagnostics = BuildSampleOrderingDiagnostic(
                    "MainAuto",
                    "Main_Auto",
                    tempCsv);

                Assert.That(GetField<string>(diagnostics, "job_mode"), Is.EqualTo("MainAuto"));
                Assert.That(GetField<string>(diagnostics, "scene_name"), Is.EqualTo("Main_Auto"));
                Assert.That(GetField<int>(diagnostics, "metric_row_count"), Is.EqualTo(2));
                Assert.That(GetField<string>(diagnostics, "first_metric_reason"), Is.EqualTo("start"));
                Assert.That(GetField<int>(diagnostics, "first_metric_recorder_frame"), Is.EqualTo(0));
                Assert.That(GetField<int>(diagnostics, "first_metric_engine_frame_count"), Is.EqualTo(120));
                Assert.That(GetField<float>(diagnostics, "first_metric_time_since_level_load"), Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(GetField<float>(diagnostics, "first_metric_animation_clip_time"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetField<float>(diagnostics, "first_metric_grounding_vertical_step_last"), Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(GetField<float>(diagnostics, "first_metric_grounding_initial_vertical_step"), Is.EqualTo(0.45f).Within(0.0001f));
                Assert.That(GetField<int>(diagnostics, "first_metric_grounding_step_clamp_count"), Is.EqualTo(12));
                Assert.That(GetField<int>(diagnostics, "first_metric_grounding_smoothed_count"), Is.EqualTo(60));
                Assert.That(GetField<string>(diagnostics, "finish_metric_reason"), Is.EqualTo("finish"));
                Assert.That(GetField<int>(diagnostics, "finish_metric_recorder_frame"), Is.EqualTo(6001));
                Assert.That(GetField<int>(diagnostics, "recording_metric_recorder_frame_span"), Is.EqualTo(6001));
                Assert.That(GetField<int>(diagnostics, "recording_metric_engine_frame_span"), Is.EqualTo(7088));
                Assert.That(GetField<float>(diagnostics, "recording_metric_time_since_level_load_span"), Is.EqualTo(199.6f).Within(0.0001f));
                Assert.That(GetField<int>(diagnostics, "recording_grounding_step_clamp_delta"), Is.EqualTo(2184));
                Assert.That(GetField<int>(diagnostics, "recording_grounding_smoothed_delta"), Is.EqualTo(5560));
                Assert.That(GetField<string>(diagnostics, "recording_phase_span_role"), Does.Contain("finish-first"));
            }
            finally
            {
                if (File.Exists(tempCsv))
                {
                    File.Delete(tempCsv);
                }
            }
        }

        [Test]
        public void Given_MetricsCsvWithGroundingStepLimit_When_BuildingSampleOrderingDiagnostics_Then_SeparatesPrewarmResidualFromRecordingCounters()
        {
            string tempCsv = Path.Combine(
                Path.GetTempPath(),
                "yyb-grounding-step-limit-diagnostics-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                File.WriteAllText(
                    tempCsv,
                    string.Join(
                        Environment.NewLine,
                        "reason,timeSinceLevelLoad,frameCount,recorderFrame,animationClipTime,retargetGroundingVerticalStepLast,retargetGroundingInitialVerticalStep,retargetGroundingStepClampCount,retargetGroundingSmoothedCount,retargetGroundingMaxStepPerFrame",
                        "start,1.5,120,0,0,-0.01,0.45,0,0,0.01",
                        "finish,201.1,6121,6001,200,-0.0005,0.45,2167,5563,0.01"));

                object diagnostics = BuildSampleOrderingDiagnostic(
                    "MainAuto",
                    "Main_Auto",
                    tempCsv);

                Assert.That(GetField<float>(diagnostics, "first_metric_grounding_max_step_per_frame"), Is.EqualTo(0.01f).Within(0.0001f));
                Assert.That(GetField<float>(diagnostics, "first_metric_grounding_vertical_step_to_max_ratio"), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetField<bool>(diagnostics, "first_metric_grounding_vertical_step_at_max_step"), Is.True);
                Assert.That(GetField<float>(diagnostics, "finish_metric_grounding_vertical_step_to_max_ratio"), Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(GetField<bool>(diagnostics, "finish_metric_grounding_vertical_step_at_max_step"), Is.False);
                Assert.That(GetField<int>(diagnostics, "recording_grounding_step_clamp_delta"), Is.EqualTo(2167));
                Assert.That(GetField<int>(diagnostics, "recording_grounding_smoothed_delta"), Is.EqualTo(5563));
                Assert.That(GetField<string>(diagnostics, "grounding_step_limit_role"), Does.Contain("prewarm"));
            }
            finally
            {
                if (File.Exists(tempCsv))
                {
                    File.Delete(tempCsv);
                }
            }
        }

        private static string ResolveYybReferenceClipAssetPath(
            string fbxFileName,
            params string[] existingAssetPaths)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ResolveReferenceClipAssetPath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: YybReferenceClipResolverParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a fakeable resolver so manual reference and Main_Auto smoke use the same FBX source priority.");

            var existing = new HashSet<string>(existingAssetPaths, StringComparer.OrdinalIgnoreCase);
            Func<string, bool> assetExists = existing.Contains;
            return (string)method.Invoke(null, new object[] { fbxFileName, assetExists });
        }

        private static int ResolveSummaryTargetFrameCount(int referenceTargetFrameCount, int mainAutoFrameCount)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ResolveSummaryTargetFrameCount",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(int), typeof(int) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must keep summary target frames independent from the Main_Auto candidate capture so frame-count regressions remain visible.");

            return (int)method.Invoke(null, new object[] { referenceTargetFrameCount, mainAutoFrameCount });
        }

        private static void AssertRegressionSafeRetargetDefaults(string scenePath, float expectedMovementScaleMultiplier)
        {
            EditorSceneManager.OpenScene(scenePath);

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, $"{scenePath} must contain FBXVmdPipeline.");
            Assert.That(fileManager.MovementScaleMultiplier, Is.EqualTo(expectedMovementScaleMultiplier).Within(0.0001f), $"{scenePath} must keep the expected visible root carrier movement scale.");
            Assert.That(fileManager.enableAnatomicalArmGuard, Is.True, $"{scenePath} must keep the arm anatomy guard enabled.");
            Assert.That(fileManager.attachTargetArmDeformationGuard, Is.True, $"{scenePath} must attach the target arm deformation guard.");
            Assert.That(fileManager.targetGuardClampAnatomicalArmMuscles, Is.True, $"{scenePath} must clamp target-side arm muscles after YYB arm swing correction.");
            Assert.That(fileManager.targetGuardClampArmStretchMuscles, Is.True, $"{scenePath} must clamp target-side forearm stretch after YYB arm swing correction.");
            Assert.That(fileManager.enableYybArmVisualTwistCorrection, Is.True, $"{scenePath} must keep YYB arm visual twist correction enabled.");
            Assert.That(fileManager.enableYybArmSleeveAnchorCorrection, Is.True, $"{scenePath} must keep sleeve anchor correction enabled.");
            Assert.That(fileManager.YybArmSleeveAnchorInfluence, Is.EqualTo(0.825f).Within(0.0001f), $"{scenePath} must keep the measured sleeve anchor influence that reduces non-hair avg without worsening the current max metrics.");
            Assert.That(fileManager.ShouldUseManualAnimatorFingerPoseReference, Is.False, $"{scenePath} must not copy manual finger pose into the normal Play/import path.");
            Assert.That(fileManager.ShouldUseManualAnimatorBodyRotationReference, Is.True, $"{scenePath} must use the accepted MP4-aligned body rotation reference in the normal Play/import path.");
            Assert.That(fileManager.manualAnimatorBodyRotationReferenceWeight, Is.EqualTo(1f).Within(0.0001f), $"{scenePath} must keep the accepted body rotation blend.");
            Assert.That(fileManager.ShouldUseManualAnimatorBodyPositionYReference, Is.False, $"{scenePath} must not copy manual body Y into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorHandLocalRotationReference, Is.True, $"{scenePath} must use the measured manual hand local rotation reference in the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorThumbLocalRotationReference, Is.False, $"{scenePath} must not copy manual thumb local rotation into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorThumbSegmentDirectionReference, Is.False, $"{scenePath} must not copy manual thumb segment direction into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorThumbHandDirectionReference, Is.False, $"{scenePath} must not copy manual thumb hand direction into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorHandPalmFrameReference, Is.False, $"{scenePath} must not copy manual palm frame into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorThumbBasePositionReference, Is.False, $"{scenePath} must not copy manual thumb base position into the normal Play/import path.");
            Assert.That(fileManager.enableThumbAnatomicalGuard, Is.True, $"{scenePath} must keep thumb anatomy guard enabled.");
            Assert.That(fileManager.preserveManualFingerReferenceThumbMuscles, Is.False, $"{scenePath} must not preserve manual thumb muscles while the manual finger reference is disabled.");
            Assert.That(fileManager.enableThumbLocalRotationGuard, Is.True, $"{scenePath} must keep thumb local rotation guard enabled.");
            Assert.That(fileManager.syncDetachedThumbBaseHelpers, Is.True, $"{scenePath} must keep detached thumb helper rotation sync enabled.");
            Assert.That(fileManager.syncDetachedThumbBaseHelperPositions, Is.True, $"{scenePath} must keep detached thumb helper position sync enabled.");
            Assert.That(fileManager.stabilizeThumbWebbingCrease, Is.True, $"{scenePath} must keep thumb webbing crease stabilization enabled.");
            Assert.That(fileManager.enableThumbVisualLengthGuard, Is.True, $"{scenePath} must keep thumb visual length guard enabled.");
            Assert.That(
                fileManager.ThumbIndexMaxSpreadAngle,
                Is.LessThanOrEqualTo(MaxSmokeSafeThumbIndexSpreadAngle),
                $"{scenePath} must cap thumb-index spread before YYB smoke deformation risk can exceed 0.35.");
            Assert.That(
                fileManager.ThumbProjectionMaxPalmNormal,
                Is.LessThanOrEqualTo(MaxSmokeSafeThumbProjectionMaxPalmNormal),
                $"{scenePath} must cap thumb palm-normal projection before YYB smoke deformation risk can exceed 0.35.");
            Assert.That(fileManager.failEditorSmokeOnThumbRisk, Is.True, $"{scenePath} must fail editor smoke when thumb risk exceeds the threshold.");
            Assert.That(fileManager.ShouldUseManualAnimatorFootLocalRotationReference, Is.True, $"{scenePath} must use the accepted MP4-aligned foot/toe localRotation reference in the normal Play/import path.");
            Assert.That(fileManager.manualAnimatorFootLocalRotationReferenceWeight, Is.EqualTo(1f).Within(0.0001f), $"{scenePath} must keep the accepted foot/toe localRotation blend.");
            Assert.That(fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True, $"{scenePath} must use the accepted lower-body segment direction guard.");
            Assert.That(fileManager.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(1f).Within(0.0001f), $"{scenePath} must keep the accepted lower-body segment direction blend.");
            Assert.That(fileManager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(60f).Within(0.0001f), $"{scenePath} must keep the accepted lower-body segment direction cap.");
            Assert.That(fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, Is.True, $"{scenePath} must use the accepted foot hips-aligned residual yaw guard.");
            Assert.That(fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, Is.EqualTo(1f).Within(0.0001f), $"{scenePath} must keep the accepted foot residual yaw blend.");
            Assert.That(fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle, Is.EqualTo(45f).Within(0.0001f), $"{scenePath} must keep the accepted foot residual yaw cap.");
            Assert.That(fileManager.enableYybArmSwingLimitCorrection, Is.True, $"{scenePath} must use the accepted MP4-aligned arm swing limiter.");
            Assert.That(fileManager.YybArmSwingLimitWeight, Is.EqualTo(0.6f).Within(0.0001f), $"{scenePath} must keep the accepted arm swing blend.");
            Assert.That(fileManager.YybArmSwingMaxDownDot, Is.EqualTo(0.75f).Within(0.0001f), $"{scenePath} must keep the accepted upper-arm down-dot cap.");
            Assert.That(fileManager.YybArmSwingMinHandHorizontalRatio, Is.EqualTo(0.05f).Within(0.0001f), $"{scenePath} must keep the accepted horizontal trigger ratio.");
            Assert.That(fileManager.YybArmSwingMaxHandBelowShoulderRatio, Is.EqualTo(1.5f).Within(0.0001f), $"{scenePath} must keep the accepted below-shoulder tolerance.");
            Assert.That(fileManager.YybArmSwingHorizontalReachLimitWeight, Is.EqualTo(1f).Within(0.0001f), $"{scenePath} must keep the accepted horizontal reach clamp strength.");
            Assert.That(fileManager.YybArmSwingMaxHandHorizontalReachRatio, Is.EqualTo(0.06f).Within(0.0001f), $"{scenePath} must keep the measured horizontal reach cap that reduces non-hair average, local average, upper span, and silhouette average without worsening the current max metrics.");
            Assert.That(fileManager.YybArmSwingRaisedPoseHorizontalReachLimitWeight, Is.EqualTo(0.25f).Within(0.0001f), $"{scenePath} must keep the accepted raised-pose reach cap without the rejected hair-length candidate.");
            Assert.That(fileManager.YybArmSwingRaisedPoseMinUpperArmDownDot, Is.EqualTo(0.55f).Within(0.0001f), $"{scenePath} must keep the raised-pose cap limited to mildly lowered upper arms.");
            Assert.That(fileManager.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio, Is.EqualTo(0.05f).Within(0.0001f), $"{scenePath} must avoid applying the raised-pose cap to below-shoulder swing frames.");
            Assert.That(fileManager.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio, Is.EqualTo(0.55f).Within(0.0001f), $"{scenePath} must cap only wide raised-pose horizontal reach.");
            Assert.That(fileManager.ShouldUseManualAnimatorFullBodyPoseReference, Is.True, $"{scenePath} must blend the manual reference pose as the default playback source.");
            Assert.That(fileManager.manualAnimatorFullBodyPoseReferenceWeight, Is.EqualTo(1f).Within(0.0001f), $"{scenePath} must use the measured full-body reference blend that reduces full max, non-hair max, and non-hair avg while the remaining upper/silhouette trade-off stays tracked.");
            Assert.That(fileManager.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False, $"{scenePath} must not copy manual Hips localPosition into the default playback/import path.");
            Assert.That(fileManager.manualAnimatorHipsLocalPositionMaxOffset, Is.EqualTo(0.12f).Within(0.0001f), $"{scenePath} must keep only the conservative serialized Hips reference cap while the reference is disabled.");
        }

        private static void AssertSceneRootMotionPolicy(
            string scenePath,
            bool expectedPreserveRetargetBodyPosition,
            bool expectedUseRetargetBodyPositionXZRootMotion,
            bool expectedUseEditorHumanoidRootTranslationReference,
            bool expectedClampRetargetHipsLocalPositionSpikes)
        {
            EditorSceneManager.OpenScene(scenePath);

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, $"{scenePath} must contain FBXVmdPipeline.");
            Assert.That(
                fileManager.ShouldPreserveRetargetBodyPosition,
                Is.EqualTo(expectedPreserveRetargetBodyPosition),
                $"{scenePath} must match the scene-specific body-position preservation policy.");
            Assert.That(
                fileManager.ShouldUseRetargetBodyPositionXZRootMotion,
                Is.EqualTo(expectedUseRetargetBodyPositionXZRootMotion),
                $"{scenePath} must keep the requested scene-specific X/Z root-motion policy.");
            Assert.That(
                fileManager.ShouldUseEditorHumanoidRootTranslationReference,
                Is.EqualTo(expectedUseEditorHumanoidRootTranslationReference),
                $"{scenePath} must use the requested scene-specific Humanoid RootT translation policy.");
            Assert.That(
                fileManager.clampRetargetHipsLocalPositionSpikes,
                Is.EqualTo(expectedClampRetargetHipsLocalPositionSpikes),
                $"{scenePath} must match the scene-specific Hips local-position spike policy.");
        }

        private static void AssertMovingRootRetargetDefaults(string scenePath, float minMovementScaleMultiplier)
        {
            EditorSceneManager.OpenScene(scenePath);

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, $"{scenePath} must contain FBXVmdPipeline.");
            Assert.That(fileManager.ShouldPreserveRetargetBodyPosition, Is.False, $"{scenePath} must let the imported FBX body position drive the moving-root solve.");
            Assert.That(fileManager.ShouldUseRetargetBodyPositionXZRootMotion, Is.True, $"{scenePath} must preserve X/Z body root motion.");
            Assert.That(fileManager.MovementScaleMultiplier, Is.GreaterThanOrEqualTo(minMovementScaleMultiplier), $"{scenePath} must not suppress moving-root preview/export.");
            Assert.That(fileManager.ShouldUseEditorHumanoidRootTranslationReference, Is.False, $"{scenePath} must avoid adding a second root translation source.");
        }

        private static void AssertRootYFreezeAfterInitialGrounding(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath);

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, $"{scenePath} must contain FBXVmdPipeline.");
            Assert.That(
                fileManager.FreezeRootYAfterInitialGrounding,
                Is.True,
                $"{scenePath} must freeze target root Y after the initial grounding pass so live playback does not chase per-frame foot noise.");
        }

        private static void AssertYybMmdExportClampMargin(string prefabPath)
        {
            var yybPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(yybPrefab, Is.Not.Null, $"{prefabPath} must be loadable.");

            var recorder = yybPrefab.GetComponent<UnityHumanoidVMDRecorder>();
            Assert.That(recorder, Is.Not.Null, $"{prefabPath} must contain UnityHumanoidVMDRecorder.");
            Assert.That(
                recorder.MaxMmdCenterExportDeltaPerFrame,
                Is.EqualTo(ExpectedYybMmdExportMaxDeltaPerFrame).Within(0.0001f),
                $"{prefabPath} must keep center export clamp below the 0.12m teleport threshold with margin.");
            Assert.That(
                recorder.MaxMmdFootIkExportDeltaPerFrame,
                Is.EqualTo(ExpectedYybMmdExportMaxDeltaPerFrame).Within(0.0001f),
                $"{prefabPath} must keep foot IK export clamp below the 0.12m teleport threshold with margin.");
            Assert.That(
                recorder.MaxMmdToeIkExportDeltaPerFrame,
                Is.EqualTo(ExpectedYybMmdExportMaxDeltaPerFrame).Within(0.0001f),
                $"{prefabPath} must keep toe IK export clamp below the 0.12m teleport threshold with margin.");
        }

        private static void AssertYybMmdExportRecoveryDefaults(
            string prefabPath,
            bool expectedEnabled,
            float expectedLimit,
            float expectedTrigger,
            float expectedDebt,
            int expectedHoldFrames)
        {
            var yybPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(yybPrefab, Is.Not.Null, $"{prefabPath} must be loadable.");

            var recorder = yybPrefab.GetComponent<UnityHumanoidVMDRecorder>();
            Assert.That(recorder, Is.Not.Null, $"{prefabPath} must contain UnityHumanoidVMDRecorder.");
            Assert.That(
                recorder.UseMmdIkExportDeltaRecoveryLimit,
                Is.EqualTo(expectedEnabled),
                $"{prefabPath} must match the reviewed A7 recovery default scope.");
            Assert.That(
                recorder.MmdIkExportDeltaRecoveryLimitPerFrame,
                Is.EqualTo(expectedLimit).Within(0.0001f),
                $"{prefabPath} must keep the reviewed A7 recovery limit.");
            Assert.That(
                recorder.MmdIkExportDeltaRecoveryTriggerPerFrame,
                Is.EqualTo(expectedTrigger).Within(0.0001f),
                $"{prefabPath} must keep the reviewed A7 recovery trigger.");
            Assert.That(
                recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame,
                Is.EqualTo(expectedDebt).Within(0.0001f),
                $"{prefabPath} must keep the reviewed A7 recovery debt threshold.");
            Assert.That(
                recorder.MmdIkExportDeltaRecoveryHoldFrames,
                Is.EqualTo(expectedHoldFrames),
                $"{prefabPath} must keep the reviewed A7 recovery hold window.");
        }

        private static void AssertYybMmdExportDynamicToggleDefaults(
            string prefabPath,
            bool expectedEnabled,
            float expectedFootThreshold,
            float expectedToeThreshold)
        {
            var yybPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(yybPrefab, Is.Not.Null, $"{prefabPath} must be loadable.");

            var recorder = yybPrefab.GetComponent<UnityHumanoidVMDRecorder>();
            Assert.That(recorder, Is.Not.Null, $"{prefabPath} must contain UnityHumanoidVMDRecorder.");
            Assert.That(
                recorder.UseMmdIkDynamicToggleOnLargeExportSteps,
                Is.EqualTo(expectedEnabled),
                $"{prefabPath} must match the reviewed A7 dynamic IK footer toggle scope.");
            Assert.That(
                recorder.MmdIkDynamicToggleFootStepThreshold,
                Is.EqualTo(expectedFootThreshold).Within(0.0001f),
                $"{prefabPath} must keep the reviewed A7 dynamic foot IK step threshold.");
            Assert.That(
                recorder.MmdIkDynamicToggleToeStepThreshold,
                Is.EqualTo(expectedToeThreshold).Within(0.0001f),
                $"{prefabPath} must keep the reviewed A7 dynamic toe IK step threshold.");
        }

        private static void AssertFinalIkFootGroundingDefaults(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath);

            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, $"{scenePath} must contain FBXVmdPipeline.");
            Assert.That(GetField<bool>(fileManager, "enableFinalIkFootGroundingExperiment"), Is.False, "Final IK foot grounding experiment must stay opt-in.");
            Assert.That(GetField<float>(fileManager, "finalIkFootGroundingWeight"), Is.LessThanOrEqualTo(0.25f), "Default experiment weight must remain low enough to avoid replacing PoseSpaceRetargeter output.");
            Assert.That(GetField<float>(fileManager, "finalIkFootGroundingMaxStep"), Is.LessThanOrEqualTo(0.08f), "Default max step must stay below the current A7 guard relaxation boundary.");
            Assert.That(GetField<float>(fileManager, "finalIkFootGroundingFootRotationWeight"), Is.EqualTo(0f).Within(0.0001f), "Initial experiment must not rotate feet until visual evidence proves it safe.");
        }

        private static void InvokeFinalIkFootGroundingConfiguration(FBXVmdPipeline manager, GameObject targetObject)
        {
            MethodInfo method = typeof(FBXVmdPipeline).GetMethod(
                "ConfigureFinalIkFootGroundingExperiment",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(GameObject) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FBXVmdPipeline must expose a narrow Final IK foot grounding configuration seam.");
            method.Invoke(manager, new object[] { targetObject });
        }

        private static int ResolveReferenceMmdTargetFrameCount(
            string fbxFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ResolveReferenceMmdTargetFrameCount",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(float), typeof(int), typeof(float), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must derive the ref MP4/MMD target frame count from reference timing instead of the candidate capture.");

            return (int)method.Invoke(
                null,
                new object[]
                {
                    fbxFileName,
                    requestedDurationSeconds,
                    configuredTargetFrameCount,
                    referenceClipLengthSeconds,
                    recordingFrameRate
                });
        }

        private static object BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            string provenancePath,
            string resultPath,
            string frameMetricsPath,
            string contactSheetPath)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "BuildSummaryFrameRoleDiagnostics",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(float),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner summary must separate ref MP4/MMD target, Sub_Manual baseline, and Main_Auto candidate frame counts.");

            return method.Invoke(
                null,
                new object[]
                {
                    referenceTargetFrameCount,
                    baselineRecordedFrameCount,
                    candidateRecordedFrameCount,
                    requestedDurationSeconds,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath
                });
        }

        private static object BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            string provenancePath,
            string resultPath,
            string frameMetricsPath,
            string contactSheetPath,
            string candidateFrameIndexPath)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "BuildSummaryFrameRoleDiagnostics",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(float),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner summary must compare candidate screenshot framing metrics to the ref MP4 bbox/framing metrics.");

            return method.Invoke(
                null,
                new object[]
                {
                    referenceTargetFrameCount,
                    baselineRecordedFrameCount,
                    candidateRecordedFrameCount,
                    requestedDurationSeconds,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    candidateFrameIndexPath
                });
        }

        private static object BuildSummaryFrameRoleDiagnosticsWithReferenceClipStart(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string provenancePath,
            string resultPath,
            string frameMetricsPath,
            string contactSheetPath,
            string candidateFrameIndexPath)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "BuildSummaryFrameRoleDiagnostics",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(float),
                    typeof(float),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner summary must align ref MP4 diagnostics to the active head/middle/tail clip start.");

            return method.Invoke(
                null,
                new object[]
                {
                    referenceTargetFrameCount,
                    baselineRecordedFrameCount,
                    candidateRecordedFrameCount,
                    requestedDurationSeconds,
                    referenceClipStartSeconds,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    candidateFrameIndexPath
                });
        }

        private static void WriteFixturePng(string path, RectInt brightRect)
        {
            WriteFixturePng(path, new[] { brightRect });
        }

        private static void WriteFixturePng(string path, params RectInt[] brightRects)
        {
            var texture = new Texture2D(10, 10, TextureFormat.RGBA32, mipChain: false);
            try
            {
                Color32[] pixels = new Color32[100];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(0, 0, 0, 255);
                }

                foreach (RectInt brightRect in brightRects)
                {
                    for (int y = brightRect.yMin; y < brightRect.yMax; y++)
                    {
                        for (int x = brightRect.xMin; x < brightRect.xMax; x++)
                        {
                            pixels[(y * 10) + x] = new Color32(255, 255, 255, 255);
                        }
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private struct FixturePngFill
        {
            public FixturePngFill(RectInt rect, Color32 color)
            {
                Rect = rect;
                Color = color;
            }

            public RectInt Rect;
            public Color32 Color;
        }

        private static void WriteFixturePngWithColor(string path, params FixturePngFill[] fills)
        {
            var texture = new Texture2D(10, 10, TextureFormat.RGBA32, mipChain: false);
            try
            {
                Color32[] pixels = new Color32[100];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(0, 0, 0, 255);
                }

                foreach (FixturePngFill fill in fills)
                {
                    for (int y = fill.Rect.yMin; y < fill.Rect.yMax; y++)
                    {
                        for (int x = fill.Rect.xMin; x < fill.Rect.xMax; x++)
                        {
                            pixels[(y * 10) + x] = fill.Color;
                        }
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static object BuildCandidateArtifactSelection(params MotionComparisonFrameQualitySummary[] summaries)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "BuildCandidateArtifactSelection",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(MotionComparisonFrameQualitySummary[]) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner summary must select the user-facing candidate artifact without hiding the raw candidate gate.");

            return method.Invoke(null, new object[] { summaries });
        }

        private static void WriteIntegratedPrimaryExportManifest(string manifestPath, string rawDiagnosticVmdPath)
        {
            File.WriteAllText(
                manifestPath,
                "{\n" +
                "  \"artifact_role\": \"integrated_vertical_solve_primary_export\",\n" +
                "  \"raw_diagnostic_vmd_path\": \"" + EscapeJsonPath(rawDiagnosticVmdPath) + "\"\n" +
                "}\n");
        }

        private static string EscapeJsonPath(string path)
        {
            return (path ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static bool CanStartNextJob(bool isRunning, bool hasActiveJob, bool activeJobFinished)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "CanStartNextJob",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(bool), typeof(bool), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must gate duplicate delayed StartNextJob calls while an active job is unfinished.");

            return (bool)method.Invoke(null, new object[] { isRunning, hasActiveJob, activeJobFinished });
        }

        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(UnityHumanoidVMDRecorder recorder, float overrideLimitVmd)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyMmdIkDeltaGuardRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(UnityHumanoidVMDRecorder), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support a runtime-only IK delta guard override for candidate visual comparisons.");

            return (bool)method.Invoke(null, new object[] { recorder, overrideLimitVmd });
        }

        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(
            UnityHumanoidVMDRecorder recorder,
            float overrideLimitVmd,
            float recoveryTriggerVmd)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyMmdIkDeltaGuardRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(UnityHumanoidVMDRecorder), typeof(float), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support a runtime-only conditional IK delta recovery override.");

            return (bool)method.Invoke(null, new object[] { recorder, overrideLimitVmd, recoveryTriggerVmd });
        }

        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(
            UnityHumanoidVMDRecorder recorder,
            float overrideLimitVmd,
            float recoveryTriggerVmd,
            float recoveryDebtThresholdVmd)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyMmdIkDeltaGuardRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(UnityHumanoidVMDRecorder), typeof(float), typeof(float), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support a runtime-only IK lag-debt recovery override.");

            return (bool)method.Invoke(null, new object[] { recorder, overrideLimitVmd, recoveryTriggerVmd, recoveryDebtThresholdVmd });
        }

        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(
            UnityHumanoidVMDRecorder recorder,
            float overrideLimitVmd,
            float recoveryTriggerVmd,
            float recoveryDebtThresholdVmd,
            int recoveryHoldFrames)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyMmdIkDeltaGuardRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(UnityHumanoidVMDRecorder), typeof(float), typeof(float), typeof(float), typeof(int) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support a runtime-only IK recovery hold override.");

            return (bool)method.Invoke(null, new object[] { recorder, overrideLimitVmd, recoveryTriggerVmd, recoveryDebtThresholdVmd, recoveryHoldFrames });
        }

        private static bool ApplyFinalIkFootGroundingRuntimeOverride(FBXVmdPipeline manager, bool enabled)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyFinalIkFootGroundingRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support a runtime-only Final IK foot grounding override for OFF/ON visual comparisons.");

            return (bool)method.Invoke(null, new object[] { manager, enabled });
        }

        private static bool ApplyManualAnimatorFootLocalRotationRuntimeOverride(FBXVmdPipeline manager, bool enabled)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorFootLocalRotationRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support a runtime-only foot/toe localRotation reference override for lower-body A/B probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled });
        }

        private static bool ApplyManualAnimatorFullBodyPoseRuntimeOverride(FBXVmdPipeline manager, bool enabled)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorFullBodyPoseRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a runtime-only full-body pose reference override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(null, new object[] { manager, enabled });
        }

        private static bool ApplyManualAnimatorFullBodyPoseRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorFullBodyPoseRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a weighted runtime-only full-body pose reference override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, weight });
        }

        private static bool ApplyManualAnimatorBodyRotationRuntimeOverride(FBXVmdPipeline manager, bool enabled)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorBodyRotationRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a runtime-only body rotation reference override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(null, new object[] { manager, enabled });
        }

        private static bool ApplyManualAnimatorBodyRotationRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorBodyRotationRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a weighted runtime-only body rotation reference override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, weight });
        }

        private static bool ApplyRetargetPoseVisualSpikeSmoothingRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float currentWeight,
            float forearmStretchClampMaxOffset = 0f)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyRetargetPoseVisualSpikeSmoothingRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a runtime-only visual spike smoothing override for frame 180 carrier probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, currentWeight, forearmStretchClampMaxOffset });
        }

        private static bool ApplyRetargetArmStretchClampRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float stretchLimit)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyRetargetArmStretchClampRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a runtime-only arm stretch clamp override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, stretchLimit });
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyYybArmSwingLimitRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(FBXVmdPipeline),
                    typeof(bool),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a runtime-only arm swing limiter override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(
                null,
                new object[]
                {
                    manager,
                    enabled,
                    weight,
                    maxDownDot,
                    minHandHorizontalRatio,
                    maxHandBelowShoulderRatio
                });
        }

        private static bool ApplyYybArmDirectionRetargetRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float upperArmWeight,
            float forearmWeight,
            float upperArmMaxDegrees,
            float forearmMaxDegrees)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyYybArmDirectionRetargetRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(FBXVmdPipeline),
                    typeof(bool),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a runtime-only arm direction retarget override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(
                null,
                new object[]
                {
                    manager,
                    enabled,
                    upperArmWeight,
                    forearmWeight,
                    upperArmMaxDegrees,
                    forearmMaxDegrees
                });
        }

        private static bool ApplyYybArmDirectionRetargetRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float upperArmWeight,
            float forearmWeight,
            float upperArmMaxDegrees,
            float forearmMaxDegrees,
            float leftSideWeightScale,
            float rightSideWeightScale)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyYybArmDirectionRetargetRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(FBXVmdPipeline),
                    typeof(bool),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose side-specific arm direction retarget runtime scales for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(
                null,
                new object[]
                {
                    manager,
                    enabled,
                    upperArmWeight,
                    forearmWeight,
                    upperArmMaxDegrees,
                    forearmMaxDegrees,
                    leftSideWeightScale,
                    rightSideWeightScale
                });
        }

        private static float ReadFBXVmdPipelineFloat(FBXVmdPipeline manager, string fieldName)
        {
            FieldInfo field = typeof(FBXVmdPipeline).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"FBXVmdPipeline must expose {fieldName}.");
            return (float)field.GetValue(manager);
        }

        private static bool ApplyYybArmSleeveAnchorRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float sleeveInfluence,
            float shoulderCapInfluence,
            float maxDegrees)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyYybArmSleeveAnchorRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(FBXVmdPipeline),
                    typeof(bool),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a runtime-only sleeve anchor override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(
                null,
                new object[]
                {
                    manager,
                    enabled,
                    sleeveInfluence,
                    shoulderCapInfluence,
                    maxDegrees
                });
        }

        private static bool ApplyYybArmVisualTwistRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float upperArmInfluence,
            float forearmInfluence,
            float upperArmMaxDegrees,
            float forearmMaxDegrees)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyYybArmVisualTwistRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(FBXVmdPipeline),
                    typeof(bool),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a runtime-only visual twist override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(
                null,
                new object[]
                {
                    manager,
                    enabled,
                    upperArmInfluence,
                    forearmInfluence,
                    upperArmMaxDegrees,
                    forearmMaxDegrees
                });
        }

        private static bool ApplyYybRightSleeveSilhouetteOffsetRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float localOffsetX,
            float frameGateStart,
            float frameGateEnd)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyYybRightSleeveSilhouetteOffsetRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(FBXVmdPipeline),
                    typeof(bool),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a runtime-only frame-local right sleeve silhouette offset for band_3_right correction probes.");

            return (bool)method.Invoke(
                null,
                new object[] { manager, enabled, localOffsetX, frameGateStart, frameGateEnd });
        }

        private static bool ApplyManualAnimatorHandLocalRotationRuntimeOverride(FBXVmdPipeline manager, bool enabled)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorHandLocalRotationRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a runtime-only hand local rotation reference override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(null, new object[] { manager, enabled });
        }

        private static bool ApplyManualAnimatorThumbLocalRotationRuntimeOverride(FBXVmdPipeline manager, bool enabled)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorThumbLocalRotationRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a runtime-only thumb local rotation reference override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(null, new object[] { manager, enabled });
        }

        private static bool ApplyManualAnimatorHandPalmFrameRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorHandPalmFrameRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a weighted runtime-only hand palm-frame reference override for Ref MP4 visual comparison candidates.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, weight });
        }

        private static bool ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(FBXVmdPipeline manager, bool enabled)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorBipedIkFootPositionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support a runtime-only BipedIK foot position reference override for lower-body A/B probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled });
        }

        private static bool ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxOffset)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorBipedIkFootPositionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support custom BipedIK foot position candidate weight and max offset for lower-body A/B probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, weight, maxOffset });
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxOffset)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                manager,
                enabled,
                weight,
                maxOffset,
                positiveZScale: 1f);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                manager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                frameGateStart: 0f,
                frameGateEnd: 0f);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float frameGateStart,
            float frameGateEnd)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                manager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight: 1f,
                frameGateStart,
                frameGateEnd);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyPostSetHumanPoseEndpointPositionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support a runtime-only post-SetHumanPose endpoint toes blend for direction recalculation probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, weight, maxOffset, positiveZScale, toesBlendWeight, frameGateStart, frameGateEnd });
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd,
            bool evaluatorXzReferenceEnabled,
            float evaluatorXzTargetMagnitude)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                manager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight,
                frameGateStart,
                frameGateEnd,
                useLeftSide: false,
                evaluatorXzReferenceEnabled,
                evaluatorXzTargetMagnitude);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd,
            bool useLeftSide,
            bool evaluatorXzReferenceEnabled,
            float evaluatorXzTargetMagnitude)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyPostSetHumanPoseEndpointPositionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support runtime-only evaluator-basis right foot X/Z probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, weight, maxOffset, positiveZScale, toesBlendWeight, frameGateStart, frameGateEnd, useLeftSide, evaluatorXzReferenceEnabled, evaluatorXzTargetMagnitude });
        }

        private static bool ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd,
            bool useLeftSide = false,
            bool useGhostCurrentBasis = false,
            bool invertBodyPositionX = false,
            bool invertBodyPositionZ = false)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyPreSetHumanPoseEndpointPositionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(bool), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support runtime-only pre-SetHumanPose endpoint X/Z probes with bodyPosition axis inversion.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, weight, maxOffset, positiveZScale, toesBlendWeight, frameGateStart, frameGateEnd, useLeftSide, useGhostCurrentBasis, invertBodyPositionX, invertBodyPositionZ });
        }

        private static bool ApplyTargetHumanoidBonePositionLockRuntimeOverride(FBXVmdPipeline manager, bool enabled)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyTargetHumanoidBonePositionLockRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support runtime-only target skeleton basis lock probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled });
        }

        private static bool ApplyRetargetBodyPositionXzRootMotionRuntimeOverride(FBXVmdPipeline manager, bool enabled)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyRetargetBodyPositionXzRootMotionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support runtime-only SetHumanPose solver root-basis probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled });
        }

        private static bool ApplyManualAnimatorBodyPositionXzRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxOffset,
            float frameGateStart,
            float frameGateEnd,
            float frameGateBlendFrames,
            float axisXScale,
            float axisZScale)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorBodyPositionXzRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support runtime-only manual bodyPosition X/Z solver input probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, weight, maxOffset, frameGateStart, frameGateEnd, frameGateBlendFrames, axisXScale, axisZScale });
        }

        private static bool TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            out Vector3 nextFootPosition)
        {
            Type diagnosticsType = typeof(PoseSpaceRetargeter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics",
                throwOnError: true);
            MethodInfo method = diagnosticsType.GetMethod(
                "TryCalculateReferencePosition",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(Vector3).MakeByRefType()
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "Post-SetHumanPose endpoint candidate must expose positive-Z carrier scaling for middle-window probes.");

            object[] args =
            {
                desiredFootPosition,
                desiredToesPosition,
                currentFootPosition,
                currentToesPosition,
                weight,
                maxOffset,
                positiveZScale,
                Vector3.zero
            };
            bool result = (bool)method.Invoke(null, args);
            nextFootPosition = (Vector3)args[7];
            return result;
        }

        private static bool TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            out Vector3 nextFootPosition)
        {
            Type diagnosticsType = typeof(PoseSpaceRetargeter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics",
                throwOnError: true);
            MethodInfo method = diagnosticsType.GetMethod(
                "TryCalculateReferencePosition",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(Vector3).MakeByRefType()
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "Post-SetHumanPose endpoint candidate must expose a runtime-only foot/toes blend for direction recalculation probes.");

            object[] args =
            {
                desiredFootPosition,
                desiredToesPosition,
                currentFootPosition,
                currentToesPosition,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight,
                Vector3.zero
            };
            bool result = (bool)method.Invoke(null, args);
            nextFootPosition = (Vector3)args[8];
            return result;
        }

        private static bool TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            Vector3 firstMatchedFootOffset,
            float targetMagnitude,
            float weight,
            float maxOffset,
            out Vector3 nextFootPosition)
        {
            Type diagnosticsType = typeof(PoseSpaceRetargeter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics",
                throwOnError: true);
            MethodInfo method = diagnosticsType.GetMethod(
                "TryCalculateEvaluatorXzReferencePosition",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(Vector3),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(Vector3).MakeByRefType()
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "Post-SetHumanPose evaluator-basis candidate must expose first-offset X/Z correction for middle-window probes.");

            object[] args =
            {
                referenceFootPosition,
                currentFootPosition,
                firstMatchedFootOffset,
                targetMagnitude,
                weight,
                maxOffset,
                Vector3.zero
            };

            bool result = (bool)method.Invoke(null, args);
            nextFootPosition = (Vector3)args[6];
            return result;
        }

        private static bool ApplyManualAnimatorHipsLocalPositionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxOffset)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorHipsLocalPositionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support custom Hips local-position candidate weight and max offset for bbox-normalized pose probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, weight, maxOffset });
        }

        private static bool ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxAngle)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FBXVmdPipeline), typeof(bool), typeof(float), typeof(float) },
                modifiers: null);
            if (method == null)
            {
                method = runnerType.GetMethod(
                    "ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride",
                    BindingFlags.Static | BindingFlags.NonPublic);
            }

            Assert.That(method, Is.Not.Null, "YYB runner must support custom foot residual yaw candidate weight and max angle for lower-body A/B probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, weight, maxAngle });
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxAngle)
        {
            return ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                manager,
                enabled,
                weight,
                maxAngle,
                disableFootToToes: false,
                footToToesMaxAngle: 0f);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
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
                    typeof(bool),
                    typeof(float)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support runtime-only leg-chain segment direction overrides.");

            return (bool)method.Invoke(null, new object[]
            {
                manager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg,
                upperLegToLowerLegMaxAngle,
                disableLowerLegToFoot,
                lowerLegToFootMaxAngle,
                disableFootToToes,
                footToToesMaxAngle
            });
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            float leftLowerLegToFootMaxAngle,
            float rightLowerLegToFootMaxAngle,
            float rightLowerLegToFootAxisXzScale,
            float rightLowerLegToFootBlendWeight,
            float rightLowerLegToFootFrameGateStart,
            float rightLowerLegToFootFrameGateEnd,
            float rightLowerLegToFootEndpointBlendWeight,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
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

            Assert.That(method, Is.Not.Null, "YYB runner must support runtime-only endpoint-blend Right LowerLegToFoot segment direction overrides.");

            return (bool)method.Invoke(null, new object[]
            {
                manager,
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

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            float leftLowerLegToFootMaxAngle,
            float rightLowerLegToFootMaxAngle,
            float rightLowerLegToFootAxisXzScale,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
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
                    typeof(bool),
                    typeof(float)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support runtime-only axis-aware Right LowerLegToFoot segment direction overrides.");

            return (bool)method.Invoke(null, new object[]
            {
                manager,
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
                disableFootToToes,
                footToToesMaxAngle
            });
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            float leftLowerLegToFootMaxAngle,
            float rightLowerLegToFootMaxAngle,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
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
                    typeof(bool),
                    typeof(float)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support runtime-only side-specific LowerLegToFoot segment direction overrides.");

            return (bool)method.Invoke(null, new object[]
            {
                manager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg,
                upperLegToLowerLegMaxAngle,
                disableLowerLegToFoot,
                lowerLegToFootMaxAngle,
                leftLowerLegToFootMaxAngle,
                rightLowerLegToFootMaxAngle,
                disableFootToToes,
                footToToesMaxAngle
            });
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline manager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(FBXVmdPipeline),
                    typeof(bool),
                    typeof(float),
                    typeof(float),
                    typeof(bool),
                    typeof(float)
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support a runtime-only lower-body segment direction override for foot/toe drift probes.");

            return (bool)method.Invoke(null, new object[] { manager, enabled, weight, maxAngle, disableFootToToes, footToToesMaxAngle });
        }

        private static float ReadFloatField(object target, string fieldName)
        {
            Assert.That(target, Is.Not.Null, "Target object is required for reflective field read.");
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} must expose {fieldName} for focused runtime diagnostics.");
            object value = field.GetValue(target);
            Assert.That(value, Is.TypeOf<float>(), $"{fieldName} must be a float field.");
            return (float)value;
        }

        private static void AssertRangeMaxAtLeast<T>(string fieldName, float expectedMax) where T : class
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{typeof(T).Name}.{fieldName} must exist.");

            var range = field.GetCustomAttribute<UnityEngine.RangeAttribute>();
            Assert.That(range, Is.Not.Null, $"{typeof(T).Name}.{fieldName} must expose an Inspector range.");
            Assert.That(
                range.max,
                Is.GreaterThanOrEqualTo(expectedMax),
                $"{typeof(T).Name}.{fieldName} Inspector range must include the discovered legacy frame gate {expectedMax:0}.");
        }

        private static bool ReadBoolField(object target, string fieldName)
        {
            Assert.That(target, Is.Not.Null, "Target object is required for reflective field read.");
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} must expose {fieldName} for focused runtime diagnostics.");
            object value = field.GetValue(target);
            Assert.That(value, Is.TypeOf<bool>(), $"{fieldName} must be a bool field.");
            return (bool)value;
        }

        private static AnimationClip LoadFirstHumanoidAnimationClip(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => clip != null && clip.humanMotion);
        }

        private static void InvokeConfigureEditorManualFingerPoseReference(
            FBXVmdPipeline manager,
            PoseSpaceRetargeter retargeter,
            AnimationClip referenceClip)
        {
            MethodInfo method = typeof(FBXVmdPipeline).GetMethod(
                "ConfigureEditorManualFingerPoseReference",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(PoseSpaceRetargeter), typeof(AnimationClip) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FBXVmdPipeline must prepare the manual reference Animator for all manual lower-body A/B candidates.");
            method.Invoke(manager, new object[] { retargeter, referenceClip });
        }

        private static HumanoidSampleCode SelectActiveManualRecorder(string targetNameToken)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "SelectActiveManualRecorder",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must activate the selected Sub_Manual recorder before starting capture.");

            return (HumanoidSampleCode)method.Invoke(null, new object[] { targetNameToken });
        }

        private static bool IsMainSceneCandidateMode(string jobMode)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "IsMainSceneCandidateMode",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose the main-scene candidate predicate for summary coverage tests.");

            return (bool)method.Invoke(null, new object[] { jobMode });
        }

        private static string ResolveIntegratedVerticalSolveRole(string jobMode)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ResolveIntegratedVerticalSolveRole",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must promote bounded vertical solve artifacts for Main_Auto and VMD replay with distinct roles.");

            return (string)method.Invoke(null, new object[] { jobMode });
        }

        private static bool ShouldBuildFrameQualityDiagnostic(bool success, string metricsCsvPath, string vmdPath)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ShouldBuildFrameQualityDiagnostic",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(bool), typeof(string), typeof(string) },
                modifiers: null);
            Assert.That(method, Is.Not.Null, "ShouldBuildFrameQualityDiagnostic must exist.");
            return (bool)method.Invoke(null, new object[] { success, metricsCsvPath, vmdPath });
        }

        private static string[] BuildFrameQualityFailureMessages(params MotionComparisonFrameQualitySummary[] summaries)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "BuildFrameQualityFailureMessages",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(MotionComparisonFrameQualitySummary[]) },
                modifiers: null);
            Assert.That(method, Is.Not.Null, "YYB runner must promote frame-quality fail summaries to run failures.");
            return (string[])method.Invoke(null, new object[] { summaries });
        }

        private static string[] BuildFrameQualityFailureMessages(
            MotionComparisonFrameQualitySummary[] summaries,
            object frameRoleDiagnostics)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");
            Assert.That(frameRoleDiagnostics, Is.Not.Null);

            MethodInfo method = runnerType.GetMethod(
                "BuildFrameQualityFailureMessages",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(MotionComparisonFrameQualitySummary[]), frameRoleDiagnostics.GetType() },
                modifiers: null);
            Assert.That(
                method,
                Is.Not.Null,
                "YYB runner must consider reference MP4 image-space evidence before promoting Sub_Manual pose deltas to run failures.");
            return (string[])method.Invoke(null, new object[] { summaries, frameRoleDiagnostics });
        }

        private static object BuildReferenceAlignedImportedFbxDiagnostics()
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            Type diagnosticsType = runnerType.GetNestedType(
                "SummaryFrameRoleDiagnostics",
                BindingFlags.NonPublic);
            Assert.That(diagnosticsType, Is.Not.Null, "YYB runner summary diagnostics type must remain available.");

            object diagnostics = Activator.CreateInstance(diagnosticsType);
            SetDiagnosticsField(diagnostics, "reference_mp4_current_clip_sample_count", 7);
            SetDiagnosticsField(diagnostics, "candidate_screenshot_nonblank_frame_count", 8);
            SetDiagnosticsField(diagnostics, "candidate_vs_reference_time_matched_sample_count", 7);
            SetDiagnosticsField(diagnostics, "candidate_vs_reference_time_matched_max_seconds_gap", 0f);
            SetDiagnosticsField(diagnostics, "candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta", 0.021f);
            SetDiagnosticsField(diagnostics, "candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta", 0.006f);
            SetDiagnosticsField(diagnostics, "candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta", 0.131f);
            SetDiagnosticsField(diagnostics, "candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta", 0.205f);
            SetDiagnosticsField(diagnostics, "candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta", 0.285f);
            SetDiagnosticsField(diagnostics, "candidate_screenshot_frame_metrics_error", string.Empty);
            SetDiagnosticsField(diagnostics, "reference_mp4_analysis_error", string.Empty);
            SetDiagnosticsField(diagnostics, "reference_mp4_frame_metrics_error", string.Empty);
            return diagnostics;
        }

        private static void SetDiagnosticsField(object diagnostics, string fieldName, object value)
        {
            FieldInfo field = diagnostics.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"diagnostics field {fieldName} must exist.");
            field.SetValue(diagnostics, value);
        }

        private static void ClearYybVisualComparisonRunnerState(string reason)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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

        private static void SetYybVisualComparisonRunnerStaticField<T>(string fieldName, T value)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            FieldInfo field = runnerType.GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"YYB runner field {fieldName} must exist for runtime override tests.");
            field.SetValue(null, value);
        }

        private static string[] BuildCaptureJobModes(bool enableVmdPlaybackProbeRuntimeOverride)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "BuildCaptureJobs",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must build a testable same-session job list for VMD replay A/B probes.");

            var jobs = (Array)method.Invoke(null, new object[] { enableVmdPlaybackProbeRuntimeOverride });
            return jobs.Cast<object>()
                .Select(job => job.GetType().GetField("Mode").GetValue(job).ToString())
                .ToArray();
        }

        private static string ResolveVisualCompareSmokeSegment(string segment)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ResolveEditorDiagnosticSmokeSegment",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB visual comparison runner must expose a segment resolver so tail visual-review targets can be replayed fresh.");

            return method.Invoke(null, new object[] { segment }).ToString();
        }

        private static object BuildManualAnimatorCapturePlan(
            string labelSuffix,
            string fbxFileName,
            float referenceClipLengthSeconds,
            float requestedDurationSeconds,
            string segment)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo segmentMethod = runnerType.GetMethod(
                "ResolveEditorDiagnosticSmokeSegment",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);
            Assert.That(segmentMethod, Is.Not.Null);
            object resolvedSegment = segmentMethod.Invoke(null, new object[] { segment });

            MethodInfo method = runnerType.GetMethod(
                "BuildManualAnimatorCapturePlan",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(float),
                    typeof(float),
                    resolvedSegment.GetType()
                },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must build a testable manual capture plan so Sub_Manual uses the same head/middle/tail segment as Main_Auto.");
            return method.Invoke(
                null,
                new[] { labelSuffix, fbxFileName, referenceClipLengthSeconds, requestedDurationSeconds, resolvedSegment });
        }

        private static float[] BuildReferenceMp4AlignedProbeSampleTimes(
            float referenceClipStartSeconds,
            float requestedDurationSeconds,
            float[] referenceLocalSampleSeconds)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "BuildReferenceMp4AlignedProbeSampleTimes",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(float), typeof(float), typeof(float[]) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must shift probe sample times into the active ref MP4 clip window.");
            return (float[])method.Invoke(
                null,
                new object[] { referenceClipStartSeconds, requestedDurationSeconds, referenceLocalSampleSeconds });
        }

        private static float[] BuildReferenceMp4AlignedProbeSampleTimes(
            float candidateClipStartSeconds,
            float requestedDurationSeconds,
            float[] referenceLocalSampleSeconds,
            float candidateClipSecondsPerReferenceSecond)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "BuildReferenceMp4AlignedProbeSampleTimes",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(float), typeof(float), typeof(float[]), typeof(float) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must scale Ref MP4 local seconds into candidate clip seconds for segment reference timing probes.");
            return (float[])method.Invoke(
                null,
                new object[]
                {
                    candidateClipStartSeconds,
                    requestedDurationSeconds,
                    referenceLocalSampleSeconds,
                    candidateClipSecondsPerReferenceSecond
                });
        }

        private static void AssertContainsTime(float[] sampleTimes, float expected)
        {
            Assert.That(
                sampleTimes.Any(time => Mathf.Abs(time - expected) <= 0.0001f),
                Is.True,
                $"Expected sample time {expected:0.000000}.");
        }

        private static void AssertDoesNotContainTime(float[] sampleTimes, float unexpected)
        {
            Assert.That(
                sampleTimes.Any(time => Mathf.Abs(time - unexpected) <= 0.0001f),
                Is.False,
                $"Did not expect unshifted sample time {unexpected:0.000000}.");
        }

        private static int FindHumanMuscleIndex(params string[] tokens)
        {
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string muscleName = HumanTrait.MuscleName[i] ?? string.Empty;
                if (tokens.All(token => muscleName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return i;
                }
            }

            Assert.Fail($"Expected Humanoid muscle containing tokens: {string.Join(", ", tokens)}.");
            return -1;
        }

        private static object BuildSampleOrderingDiagnostic(
            string jobMode,
            string sceneName,
            string metricsCsvPath)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "BuildSampleOrderingDiagnostic",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(string) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner summary must expose frame-0/prewarm/grounding sample ordering diagnostics.");

            return method.Invoke(null, new object[] { jobMode, sceneName, metricsCsvPath });
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");

            return (T)field.GetValue(instance);
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            Assert.That(instance, Is.Not.Null);
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Expected property '{propertyName}' to exist.");

            return (T)property.GetValue(instance);
        }

        private static void SetField<T>(object instance, string fieldName, T value)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");

            field.SetValue(instance, value);
        }

        private static void AddArmDirectionRetargetSegment(
            HumanoidArmDirectionRetargetGuard guard,
            HumanBodyBones sourceBone,
            HumanBodyBones endBone)
        {
            Assert.That(guard, Is.Not.Null);
            FieldInfo segmentsField = typeof(HumanoidArmDirectionRetargetGuard).GetField(
                "_segments",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(segmentsField, Is.Not.Null, "Arm direction guard must keep testable segment state.");

            Type segmentType = typeof(HumanoidArmDirectionRetargetGuard).GetNestedType(
                "SegmentMapping",
                BindingFlags.NonPublic);
            Assert.That(segmentType, Is.Not.Null, "Arm direction guard segment mapping must remain available.");

            object segment = Activator.CreateInstance(
                segmentType,
                sourceBone,
                endBone,
                Quaternion.identity,
                1f,
                30f);
            Assert.That(segment, Is.Not.Null);

            var segments = (System.Collections.IList)segmentsField.GetValue(guard);
            segments.Add(segment);
        }

        private static void InvokeInstance(object instance, string methodName)
        {
            Assert.That(instance, Is.Not.Null);
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Expected instance method '{methodName}' to exist.");

            method.Invoke(instance, null);
        }
    }
}
