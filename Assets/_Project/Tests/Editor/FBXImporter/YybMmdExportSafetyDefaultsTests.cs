using Member_Han.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybMmdExportSafetyDefaultsTests
    {
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
            Assert.That(fileManager.stabilizeGroundedFootXZ, Is.False, "This slice must not use per-foot X/Z locking while validating center/root Y floor correction.");
            Assert.That(fileManager.GroundedFootLockWeight, Is.EqualTo(0f).Within(0.0001f), "Foot-lock correction must be fully disabled for center/root-only floor correction.");
            Assert.That(fileManager.FreezeRootYAfterInitialGrounding, Is.False, "Root Y must keep following final foot grounding to avoid MMD foot sinking.");
            Assert.That(fileManager.RetargetPrewarmFrameCount, Is.GreaterThanOrEqualTo(120), "Main_Auto full-reference smoke must use the prewarm cap so frame-0 grounding residual is not still at the per-frame limit when recording starts.");
            Assert.That(fileManager.MaxLateVisualGroundingStepPerFrame, Is.GreaterThanOrEqualTo(0.04f), "Late visual grounding must be able to clear the observed t60 foot penetration before the next diagnostic sample.");
            Assert.That(fileManager.enableYybArmSwingLimitCorrection, Is.False, "This acceptance path must not change arm/body correction while validating foot-only MMD export fixes.");
            Assert.That(fileManager.enableAnatomicalArmGuard, Is.False, "Foot-only MMD export validation must not alter arm pose.");
            Assert.That(fileManager.attachTargetArmDeformationGuard, Is.False, "Foot-only MMD export validation must not attach arm deformation guards.");
            Assert.That(fileManager.enableYybArmVisualTwistCorrection, Is.False, "Foot-only MMD export validation must not twist arms.");
            Assert.That(fileManager.enableYybArmSleeveAnchorCorrection, Is.False, "Foot-only MMD export validation must not anchor sleeves/arms.");
            Assert.That(fileManager.enableThumbAnatomicalGuard, Is.False, "Foot-only MMD export validation must not alter thumbs.");
            Assert.That(fileManager.enableThumbLocalRotationGuard, Is.False, "Foot-only MMD export validation must not clamp thumb rotations.");
            Assert.That(fileManager.enableThumbVisualLengthGuard, Is.False, "Foot-only MMD export validation must not reshape thumbs.");
            Assert.That(fileManager.failEditorSmokeOnThumbRisk, Is.False, "This acceptance path records the source FBX pose and must report thumb diagnostics without failing the foot/root export smoke.");
            Assert.That(fileManager.useManualAnimatorFullBodyPoseReference, Is.False, "Full-body pose reference changes body pose and must stay out of the center/root-only floor correction slice.");
            Assert.That(fileManager.useManualAnimatorHipsLocalPositionReference, Is.True, "Hips local reference must be enabled only with the YYB-rest anchored helper so t60 Hips collapse is corrected without full-body pose override.");
            Assert.That(fileManager.useManualAnimatorFootHeightGroundingReference, Is.True, "Main_Auto must preserve the manual reference lowest-foot lift so the t30 foot-height arc is not flattened by grounding.");
            Assert.That(fileManager.manualAnimatorFootHeightGroundingReferenceMaxLift, Is.EqualTo(0.08f).Within(0.0001f), "Foot-height grounding reference must be capped below the root teleport threshold.");
            Assert.That(fileManager.clampRetargetHipsLocalPositionSpikes, Is.False, "Hips local clamps change pose internals and must stay out of the center/root-only floor correction slice.");

            var yybPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku.prefab");
            Assert.That(yybPrefab, Is.Not.Null, "YYB prefab must be loadable.");

            var recorder = yybPrefab.GetComponent<UnityHumanoidVMDRecorder>();
            Assert.That(recorder, Is.Not.Null, "YYB prefab must contain UnityHumanoidVMDRecorder.");
            Assert.That(recorder.UseBottomCenter, Is.True, "YYB MMD export must write the center bone from the foot-level bottom center instead of the humanoid hips height.");
            Assert.That(recorder.KeyReductionLevel, Is.EqualTo(1), "MMD character export must keep every recorded frame; reduced keys cause visible stepped playback and apparent teleports.");
            Assert.That(recorder.MaxRecordedFramesPerLateUpdate, Is.EqualTo(1), "Recording must not burst multiple VMD frames from a single rendered Unity pose.");
            Assert.That(recorder.ParentOfAllOffset, Is.EqualTo(Vector3.zero), "YYB MMD export must not use a static global/root lift; floor correction is frame-local center Y only.");
            Assert.That(recorder.MmdFootIkExportOffset, Is.EqualTo(Vector3.zero), "YYB MMD export must not add a static IK lift; it causes visible hover in MMD playback.");
            Assert.That(recorder.ClampMmdFootIkYToFloor, Is.False, "YYB MMD export must not clamp foot/toe IK Y in this slice; only center/root Y may be lifted.");
            Assert.That(recorder.LiftMmdCenterYToKeepFeetAboveFloor, Is.True, "YYB MMD export must resolve floor penetration by lifting center/root Y per frame.");
            Assert.That(recorder.MinMmdFootIkY, Is.EqualTo(0.05f).Within(0.0001f), "YYB MMD export should keep the effective foot IK height at the same floor clearance seen in Unity smoke metrics.");
            Assert.That(recorder.ClampMmdCenterExportDeltaSpikes, Is.True, "YYB MMD export must clamp one-frame center movement so MMD play cannot teleport.");
            Assert.That(recorder.MaxMmdCenterExportDeltaPerFrame, Is.LessThanOrEqualTo(0.12f), "YYB MMD center movement must stay below the teleport threshold used by export QA.");
            Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.True, "YYB MMD export must clamp foot/toe IK one-frame jumps so MMD playback cannot snap through IK targets.");
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
    }
}
