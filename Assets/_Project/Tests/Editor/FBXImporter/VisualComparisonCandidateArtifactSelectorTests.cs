using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonCandidateArtifactSelectorTests
    {
        [Test]
        public void Given_PassingRawCandidate_When_Selecting_Then_ReturnsAcceptanceArtifact()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "VisualComparisonCandidateSelector_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string vmdPath = Path.Combine(root, "raw.vmd");
            string metricsPath = Path.Combine(root, "raw.csv");

            try
            {
                File.WriteAllText(vmdPath, "raw-vmd");
                File.WriteAllText(metricsPath, "raw-metrics");
                var summary = new MotionComparisonFrameQualitySummary
                {
                    frame_quality_evaluation_role = "evaluation_candidate_metrics",
                    candidate_label = "Main_Auto raw",
                    status = "pass",
                    candidate_vmd_path = vmdPath,
                    candidate_metrics_csv = metricsPath
                };
                Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
                Type selectorType = runtimeAssembly.GetType(
                    "Fbx2Vmd.FBXImporter.VisualComparisonCandidateArtifactSelector",
                    throwOnError: false);
                Assert.That(selectorType, Is.Not.Null, "모델 중립 후보 산출물 선택 경계가 필요합니다.");

                MethodInfo selectMethod = selectorType.GetMethod(
                    "Select",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(selectMethod, Is.Not.Null);

                object selection = selectMethod.Invoke(
                    null,
                    new object[] { new[] { summary }, root });
                Assert.That(selection, Is.Not.Null);
                Type selectionType = selection.GetType();

                Assert.That(
                    selectionType.GetField("selected_candidate_role").GetValue(selection),
                    Is.EqualTo("evaluation_candidate_metrics"));
                Assert.That(
                    selectionType.GetField("selected_candidate_output_role").GetValue(selection),
                    Is.EqualTo("user_facing_export_artifact"));
                Assert.That(
                    selectionType.GetField("selected_candidate_is_acceptance_artifact").GetValue(selection),
                    Is.True);
                Assert.That(
                    selectionType.GetField("selected_candidate_preserves_raw_diagnostic").GetValue(selection),
                    Is.False);
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
        public void Given_CandidateArtifactSelectorOwner_When_InspectingResponsibilities_Then_RunnerHasNoSelectionWrapper()
        {
            Type selectorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCandidateArtifactSelector",
                throwOnError: true);
            Assert.That(
                selectorType.GetMethod(
                    "Select",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Not.Null);

            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner, Assembly-CSharp");
            Assert.That(runnerType, Is.Not.Null);
            Assert.That(
                runnerType.GetMethod(
                    "BuildCandidateArtifactSelection",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null);
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
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonCandidateSelector_" + Guid.NewGuid().ToString("N"));
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
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonCandidateSelector_" + Guid.NewGuid().ToString("N"));
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
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonCandidateSelector_" + Guid.NewGuid().ToString("N"));
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
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonCandidateSelector_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string metricsPath = Path.Combine(root, "main-auto.csv");
            string vmdPath = Path.Combine(root, "main-auto.vmd");

            try
            {
                File.WriteAllText(metricsPath, "main-auto-metrics");
                File.WriteAllText(vmdPath, "main-auto-vmd");
                var raw = new MotionComparisonFrameQualitySummary
                {
                    candidate_label = "Main_Auto automatic path",
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
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonCandidateSelector_" + Guid.NewGuid().ToString("N"));
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
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonCandidateSelector_" + Guid.NewGuid().ToString("N"));
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
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonCandidateSelector_" + Guid.NewGuid().ToString("N"));
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
                    candidate_label = "Main_Recoding 자동 경로",
                    frame_quality_evaluation_role = "evaluation_candidate_metrics",
                    status = "pass",
                    candidate_metrics_csv = recordingMetricsPath,
                    candidate_vmd_path = recordingVmdPath
                };
                var mainAuto = new MotionComparisonFrameQualitySummary
                {
                    candidate_label = "Main_Auto 자동 경로",
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

        private static object BuildCandidateArtifactSelection(
            params MotionComparisonFrameQualitySummary[] summaries)
        {
            Type selectorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCandidateArtifactSelector",
                throwOnError: true);
            MethodInfo method = selectorType.GetMethod(
                "Select",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            return method.Invoke(null, new object[] { summaries, string.Empty });
        }

        private static void WriteIntegratedPrimaryExportManifest(
            string manifestPath,
            string rawDiagnosticVmdPath)
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
