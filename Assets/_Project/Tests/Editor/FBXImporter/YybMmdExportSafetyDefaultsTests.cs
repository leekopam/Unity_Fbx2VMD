using Member_Han.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RootMotion.FinalIK;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybMmdExportSafetyDefaultsTests
    {
        private const float ExpectedYybMmdExportMaxDeltaPerFrame = 0.11f;

        private static readonly Type[] YybReferenceClipResolverParameterTypes =
        {
            typeof(string),
            typeof(Func<string, bool>)
        };

        [Test]
        public void MainAutoScene_UsesMmdSafeYybExportDefaults()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");

            FileManager fileManager = UnityEngine.Object.FindObjectOfType<FileManager>();

            Assert.That(fileManager, Is.Not.Null, "Main_Auto scene must contain FileManager.");
            Assert.That(fileManager.stabilizeGroundedFootXZ, Is.False, "Rollback preset must not enable per-foot X/Z locking.");
            Assert.That(fileManager.GroundedFootLockWeight, Is.EqualTo(0.45f).Within(0.0001f), "Rollback preset must restore the pre-reference-video foot-lock blend.");
            Assert.That(fileManager.FreezeRootYAfterInitialGrounding, Is.True, "Root Y must freeze after initial grounding so live playback does not chase per-frame foot noise.");
            Assert.That(fileManager.RetargetPrewarmFrameCount, Is.EqualTo(6), "Rollback preset must remove the 120-frame prewarm added by the reference-video tuning pass.");
            Assert.That(fileManager.MaxLateVisualGroundingStepPerFrame, Is.EqualTo(0.003f).Within(0.0001f), "Rollback preset must restore the conservative late visual grounding step.");
            Assert.That(fileManager.enableYybArmSwingLimitCorrection, Is.False, "Rollback preset must keep the arm/body swing limiter disabled.");
            Assert.That(fileManager.enableAnatomicalArmGuard, Is.True, "Main_Auto must keep arm anatomy protection while validating the shared YYB playback/export path.");
            Assert.That(fileManager.attachTargetArmDeformationGuard, Is.True, "Main_Auto must attach arm deformation guards while validating the shared YYB playback/export path.");
            Assert.That(fileManager.enableYybArmVisualTwistCorrection, Is.True, "Main_Auto must keep YYB arm visual twist correction for the shared playback/export path.");
            Assert.That(fileManager.enableYybArmSleeveAnchorCorrection, Is.True, "Main_Auto must keep sleeve anchor correction for the shared playback/export path.");
            Assert.That(fileManager.enableThumbAnatomicalGuard, Is.True, "Main_Auto must keep thumb anatomy protection for the shared playback/export path.");
            Assert.That(fileManager.enableThumbLocalRotationGuard, Is.True, "Main_Auto must keep thumb local rotation protection for the shared playback/export path.");
            Assert.That(fileManager.enableThumbVisualLengthGuard, Is.True, "Main_Auto must keep thumb visual length protection for the shared playback/export path.");
            Assert.That(fileManager.failEditorSmokeOnThumbRisk, Is.True, "Editor smoke must fail when thumb risk exceeds the threshold.");
            Assert.That(fileManager.useManualAnimatorFullBodyPoseReference, Is.False, "Full-body pose reference changes body pose and must stay out of the center/root-only floor correction slice.");
            Assert.That(fileManager.useManualAnimatorHipsLocalPositionReference, Is.False, "Rollback preset must remove the manual hips local-position override from the reference-video tuning pass.");
            Assert.That(fileManager.useManualAnimatorFootHeightGroundingReference, Is.False, "Rollback preset must remove manual lowest-foot grounding from the reference-video tuning pass.");
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
        }

        [Test]
        public void YybMmdExportPrefabs_UseGuardedCenterFootAndToeClampMargin()
        {
            AssertYybMmdExportClampMargin(
                "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku.prefab");
            AssertYybMmdExportClampMargin(
                "Assets/_ManualReference/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_Prefab.prefab");
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
        public void MainScenes_PreserveRegressionSafeRetargetDefaultsForYybPlayback()
        {
            AssertRegressionSafeRetargetDefaults("Assets/_Project/Scene/Main_Auto.unity");
            AssertRegressionSafeRetargetDefaults("Assets/_Project/Scene/Main_Recoding.unity");
        }

        [Test]
        public void MainSceneRootMotionPolicy_KeepsMainAutoFixedAndAllowsMainRecordingMovement()
        {
            AssertSceneRootMotionPolicy(
                "Assets/_Project/Scene/Main_Auto.unity",
                expectedUseRetargetBodyPositionXZRootMotion: false,
                expectedUseEditorHumanoidRootTranslationReference: false,
                expectedClampRetargetHipsLocalPositionSpikes: false);
            AssertSceneRootMotionPolicy(
                "Assets/_Project/Scene/Main_Recoding.unity",
                expectedUseRetargetBodyPositionXZRootMotion: true,
                expectedUseEditorHumanoidRootTranslationReference: true,
                expectedClampRetargetHipsLocalPositionSpikes: true);
        }

        [Test]
        public void MainRecordingRootMotionPolicy_LimitsPreviewRootStepForTeleportGuard()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Recoding.unity");

            FileManager fileManager = UnityEngine.Object.FindObjectOfType<FileManager>();

            Assert.That(fileManager, Is.Not.Null, "Main_recoding scene must contain FileManager.");
            Assert.That(
                fileManager.MaxRetargetRootDeltaPerFrame,
                Is.EqualTo(0.006f).Within(0.0001f),
                "Main_recoding preview path must cap one-frame X/Z root motion below the visible root jump threshold.");
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
                var manager = managerObject.AddComponent<FileManager>();
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
                var manager = managerObject.AddComponent<FileManager>();
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
                var manager = managerObject.AddComponent<FileManager>();
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
        public void Given_ControlledImportFbxExists_When_ResolvingYybReferenceClipPath_Then_MatchesMainAutoSmokeInputPriority()
        {
            string controlledPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
            string projectPath = "Assets/_Project/FBX/satisfaction_2.fbx";

            string resolved = ResolveYybReferenceClipAssetPath(
                "satisfaction_2",
                controlledPath,
                projectPath);

            Assert.That(resolved, Is.EqualTo(controlledPath));
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
            object diagnostics = BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount: 6001,
                baselineRecordedFrameCount: 6234,
                candidateRecordedFrameCount: 5900);

            Assert.That(GetField<int>(diagnostics, "reference_target_frame_count"), Is.EqualTo(6001));
            Assert.That(GetField<int>(diagnostics, "baseline_recorded_frame_count"), Is.EqualTo(6234));
            Assert.That(GetField<int>(diagnostics, "candidate_recorded_frame_count"), Is.EqualTo(5900));
            Assert.That(GetField<int>(diagnostics, "candidate_frame_count_delta_from_reference_target"), Is.EqualTo(-101));
            Assert.That(GetField<string>(diagnostics, "target_frame_count_role"), Does.Contain("ref_mmd_mp4"));
            Assert.That(GetField<string>(diagnostics, "baseline_recorded_frame_count_role"), Does.Contain("Sub_Manual"));
            Assert.That(GetField<string>(diagnostics, "candidate_recorded_frame_count_role"), Does.Contain("Main_Auto"));
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
            string manifestPath = Path.Combine(root, "main.integrated_vertical_solve_primary_export.json");

            try
            {
                File.WriteAllText(metricsPath, "corrected-main-auto-metrics");
                File.WriteAllText(vmdPath, "corrected-main-auto-vmd");
                File.WriteAllText(manifestPath, "{}");
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
        public void Given_MainRecordingAndMainAutoSummaries_When_BuildingCandidateArtifactSelection_Then_SelectsMainAutoAcceptanceArtifact()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybMmdExportSafetyDefaultsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string recordingMetricsPath = Path.Combine(root, "main-recording.csv");
            string recordingVmdPath = Path.Combine(root, "main-recording.vmd");
            string mainAutoMetricsPath = Path.Combine(root, "main-auto.csv");
            string mainAutoVmdPath = Path.Combine(root, "main-auto.vmd");
            string mainAutoManifestPath = Path.Combine(root, "main-auto.integrated_vertical_solve_primary_export.json");

            try
            {
                File.WriteAllText(recordingMetricsPath, "main-recording-metrics");
                File.WriteAllText(recordingVmdPath, "main-recording-vmd");
                File.WriteAllText(mainAutoMetricsPath, "main-auto-metrics");
                File.WriteAllText(mainAutoVmdPath, "main-auto-vmd");
                File.WriteAllText(mainAutoManifestPath, "{}");
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
        public void Given_CaptureModes_When_CheckingSummaryCandidateMode_Then_IncludesBothMainScenes()
        {
            Assert.That(IsMainSceneCandidateMode("MainAuto"), Is.True);
            Assert.That(IsMainSceneCandidateMode("MainRecording"), Is.True);
            Assert.That(IsMainSceneCandidateMode("SubManualTestPrefab"), Is.False);
            Assert.That(IsMainSceneCandidateMode("SubManualYyb"), Is.False);
        }

        [Test]
        public void Given_MainRecordingStableCandidate_When_ExportIkSourceDiagnosticsExists_Then_CopiesDiagnosticsBesideStableVmd()
        {
            Type runnerType = Type.GetType(
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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

        private static void AssertRegressionSafeRetargetDefaults(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath);

            FileManager fileManager = UnityEngine.Object.FindObjectOfType<FileManager>();

            Assert.That(fileManager, Is.Not.Null, $"{scenePath} must contain FileManager.");
            Assert.That(fileManager.MovementScaleMultiplier, Is.EqualTo(1f).Within(0.0001f), $"{scenePath} must keep root movement playback active.");
            Assert.That(fileManager.enableAnatomicalArmGuard, Is.True, $"{scenePath} must keep the arm anatomy guard enabled.");
            Assert.That(fileManager.attachTargetArmDeformationGuard, Is.True, $"{scenePath} must attach the target arm deformation guard.");
            Assert.That(fileManager.enableYybArmVisualTwistCorrection, Is.True, $"{scenePath} must keep YYB arm visual twist correction enabled.");
            Assert.That(fileManager.enableYybArmSleeveAnchorCorrection, Is.True, $"{scenePath} must keep sleeve anchor correction enabled.");
            Assert.That(fileManager.useManualAnimatorThumbLocalRotationReference, Is.True, $"{scenePath} must keep manual thumb local rotation reference enabled.");
            Assert.That(fileManager.useManualAnimatorThumbSegmentDirectionReference, Is.True, $"{scenePath} must keep manual thumb segment direction reference enabled.");
            Assert.That(fileManager.useManualAnimatorThumbHandDirectionReference, Is.True, $"{scenePath} must keep manual thumb hand direction reference enabled.");
            Assert.That(fileManager.useManualAnimatorThumbBasePositionReference, Is.True, $"{scenePath} must keep manual thumb base position reference enabled.");
            Assert.That(fileManager.enableThumbAnatomicalGuard, Is.True, $"{scenePath} must keep thumb anatomy guard enabled.");
            Assert.That(fileManager.preserveManualFingerReferenceThumbMuscles, Is.True, $"{scenePath} must preserve manual thumb muscles while using the manual finger reference.");
            Assert.That(fileManager.enableThumbLocalRotationGuard, Is.True, $"{scenePath} must keep thumb local rotation guard enabled.");
            Assert.That(fileManager.syncDetachedThumbBaseHelpers, Is.True, $"{scenePath} must keep detached thumb helper rotation sync enabled.");
            Assert.That(fileManager.syncDetachedThumbBaseHelperPositions, Is.True, $"{scenePath} must keep detached thumb helper position sync enabled.");
            Assert.That(fileManager.stabilizeThumbWebbingCrease, Is.True, $"{scenePath} must keep thumb webbing crease stabilization enabled.");
            Assert.That(fileManager.enableThumbVisualLengthGuard, Is.True, $"{scenePath} must keep thumb visual length guard enabled.");
            Assert.That(fileManager.failEditorSmokeOnThumbRisk, Is.True, $"{scenePath} must fail editor smoke when thumb risk exceeds the threshold.");
        }

        private static void AssertSceneRootMotionPolicy(
            string scenePath,
            bool expectedUseRetargetBodyPositionXZRootMotion,
            bool expectedUseEditorHumanoidRootTranslationReference,
            bool expectedClampRetargetHipsLocalPositionSpikes)
        {
            EditorSceneManager.OpenScene(scenePath);

            FileManager fileManager = UnityEngine.Object.FindObjectOfType<FileManager>();

            Assert.That(fileManager, Is.Not.Null, $"{scenePath} must contain FileManager.");
            Assert.That(
                fileManager.preserveRetargetBodyPosition,
                Is.True,
                $"{scenePath} must keep body-position preservation enabled so Y height remains stable while scene-specific X/Z root policy is applied.");
            Assert.That(
                fileManager.useRetargetBodyPositionXZRootMotion,
                Is.EqualTo(expectedUseRetargetBodyPositionXZRootMotion),
                $"{scenePath} must keep the requested scene-specific X/Z root-motion policy.");
            Assert.That(
                fileManager.useEditorHumanoidRootTranslationReference,
                Is.EqualTo(expectedUseEditorHumanoidRootTranslationReference),
                $"{scenePath} must use the requested scene-specific Humanoid RootT translation policy.");
            Assert.That(
                fileManager.clampRetargetHipsLocalPositionSpikes,
                Is.EqualTo(expectedClampRetargetHipsLocalPositionSpikes),
                $"{scenePath} must match the scene-specific Hips local-position spike policy.");
        }

        private static void AssertRootYFreezeAfterInitialGrounding(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath);

            FileManager fileManager = UnityEngine.Object.FindObjectOfType<FileManager>();

            Assert.That(fileManager, Is.Not.Null, $"{scenePath} must contain FileManager.");
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

        private static void AssertFinalIkFootGroundingDefaults(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath);

            FileManager fileManager = UnityEngine.Object.FindObjectOfType<FileManager>();

            Assert.That(fileManager, Is.Not.Null, $"{scenePath} must contain FileManager.");
            Assert.That(GetField<bool>(fileManager, "enableFinalIkFootGroundingExperiment"), Is.False, "Final IK foot grounding experiment must stay opt-in.");
            Assert.That(GetField<float>(fileManager, "finalIkFootGroundingWeight"), Is.LessThanOrEqualTo(0.25f), "Default experiment weight must remain low enough to avoid replacing PoseSpaceRetargeter output.");
            Assert.That(GetField<float>(fileManager, "finalIkFootGroundingMaxStep"), Is.LessThanOrEqualTo(0.08f), "Default max step must stay below the current A7 guard relaxation boundary.");
            Assert.That(GetField<float>(fileManager, "finalIkFootGroundingFootRotationWeight"), Is.EqualTo(0f).Within(0.0001f), "Initial experiment must not rotate feet until visual evidence proves it safe.");
        }

        private static void InvokeFinalIkFootGroundingConfiguration(FileManager manager, GameObject targetObject)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ConfigureFinalIkFootGroundingExperiment",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(GameObject) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a narrow Final IK foot grounding configuration seam.");
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
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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
            int candidateRecordedFrameCount)
        {
            Type runnerType = Type.GetType(
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "BuildSummaryFrameRoleDiagnostics",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(int), typeof(int), typeof(int) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner summary must separate ref MP4/MMD target, Sub_Manual baseline, and Main_Auto candidate frame counts.");

            return method.Invoke(
                null,
                new object[] { referenceTargetFrameCount, baselineRecordedFrameCount, candidateRecordedFrameCount });
        }

        private static object BuildCandidateArtifactSelection(params MotionComparisonFrameQualitySummary[] summaries)
        {
            Type runnerType = Type.GetType(
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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

        private static bool CanStartNextJob(bool isRunning, bool hasActiveJob, bool activeJobFinished)
        {
            Type runnerType = Type.GetType(
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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

        private static bool ApplyFinalIkFootGroundingRuntimeOverride(FileManager manager, bool enabled)
        {
            Type runnerType = Type.GetType(
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ApplyFinalIkFootGroundingRuntimeOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FileManager), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must support a runtime-only Final IK foot grounding override for OFF/ON visual comparisons.");

            return (bool)method.Invoke(null, new object[] { manager, enabled });
        }

        private static HumanoidSampleCode SelectActiveManualRecorder(string targetNameToken)
        {
            Type runnerType = Type.GetType(
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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

        private static object BuildSampleOrderingDiagnostic(
            string jobMode,
            string sceneName,
            string metricsCsvPath)
        {
            Type runnerType = Type.GetType(
                "Member_Han.Modules.FBXImporter.EditorTools.YybVisualComparisonBatchRunner, Assembly-CSharp-Editor");
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

        private static void SetField<T>(object instance, string fieldName, T value)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");

            field.SetValue(instance, value);
        }
    }
}
