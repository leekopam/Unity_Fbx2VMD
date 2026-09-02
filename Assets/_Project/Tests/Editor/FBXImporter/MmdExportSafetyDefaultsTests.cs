using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public void MainScenes_PreserveRegressionSafeRetargetDefaultsForYybPlayback()
        {
            AssertRegressionSafeRetargetDefaults("Assets/_Project/Scene/Main_Auto.unity", expectedMovementScaleMultiplier: 1f);
            AssertMovingRootRetargetDefaults("Assets/_Project/Scene/Main_Recoding.unity", minMovementScaleMultiplier: 0.9f);
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
        public void Given_MainRecordingStableCandidate_When_ExportIkSourceDiagnosticsExists_Then_CopiesDiagnosticsBesideStableVmd()
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner, Assembly-CSharp");
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
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner, Assembly-CSharp");
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

        private static float ReadFBXVmdPipelineFloat(FBXVmdPipeline manager, string fieldName)
        {
            FieldInfo field = typeof(FBXVmdPipeline).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"FBXVmdPipeline must expose {fieldName}.");
            return (float)field.GetValue(manager);
        }

        private static float ReadFloatField(object target, string fieldName)
        {
            Assert.That(target, Is.Not.Null, "Target object is required for reflective field read.");
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            object value = field != null
                ? field.GetValue(target)
                : target.GetType().GetProperty(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);
            Assert.That(value, Is.TypeOf<float>(), $"{target.GetType().Name} must expose float member {fieldName} for focused runtime diagnostics.");
            return (float)value;
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

        private static T GetField<T>(object instance, string fieldName)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return (T)field.GetValue(instance);
            }

            PropertyInfo property = instance.GetType().GetProperty(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Expected field or property '{fieldName}' to exist.");
            return (T)property.GetValue(instance);
        }

    }
}
