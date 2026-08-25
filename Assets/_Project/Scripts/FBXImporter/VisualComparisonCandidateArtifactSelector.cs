#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonCandidateArtifactSelector
    {
        internal static VisualComparisonCandidateArtifactSelectionData Select(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries,
            string projectRoot)
        {
            var selection = new VisualComparisonCandidateArtifactSelectionData();
            if (frameQualitySummaries == null || frameQualitySummaries.Length == 0)
            {
                selection.selection_basis = "no frame_quality summary is available";
                return selection;
            }

            MotionComparisonFrameQualitySummary mainAutoIntegrated = frameQualitySummaries.FirstOrDefault(summary =>
                summary != null &&
                string.Equals(
                    summary.frame_quality_evaluation_role,
                    "main_auto_integrated_vertical_solve_metrics",
                    StringComparison.Ordinal));
            MotionComparisonFrameQualitySummary raw = frameQualitySummaries.FirstOrDefault(summary =>
                summary != null &&
                string.Equals(
                    summary.frame_quality_evaluation_role,
                    "evaluation_candidate_metrics",
                    StringComparison.Ordinal) &&
                IsMainAutoSummary(summary));
            if (raw == null)
            {
                raw = frameQualitySummaries.FirstOrDefault(summary =>
                    summary != null &&
                    string.Equals(
                        summary.frame_quality_evaluation_role,
                        "evaluation_candidate_metrics",
                        StringComparison.Ordinal));
            }

            if (raw == null)
            {
                raw = mainAutoIntegrated;
            }

            if (raw == null)
            {
                raw = frameQualitySummaries.FirstOrDefault(summary => summary != null);
            }

            MotionComparisonFrameQualitySummary corrected = frameQualitySummaries.FirstOrDefault(summary =>
                summary != null &&
                string.Equals(
                    summary.frame_quality_evaluation_role,
                    "corrected_candidate_metrics",
                    StringComparison.Ordinal) &&
                IsMainAutoSummary(summary));
            if (corrected == null)
            {
                corrected = frameQualitySummaries.FirstOrDefault(summary =>
                    summary != null &&
                    string.Equals(
                        summary.frame_quality_evaluation_role,
                        "corrected_candidate_metrics",
                        StringComparison.Ordinal));
            }

            FillRawCandidateFields(selection, raw);
            FillCorrectedCandidateFields(selection, corrected);

            bool correctedPasses = HasPassingArtifact(corrected);
            bool integratedPrimaryPasses = HasPassingArtifact(mainAutoIntegrated);
            if (integratedPrimaryPasses)
            {
                FillSelectedCandidateFields(selection, mainAutoIntegrated);
                selection.selected_candidate_output_role = "user_facing_export_artifact";
                selection.selected_candidate_preserves_raw_diagnostic = true;
                FillAcceptanceEvidence(
                    selection,
                    raw,
                    mainAutoIntegrated,
                    mainAutoIntegrated.vertical_solve_corrected_candidate_manifest_path,
                    projectRoot);
                selection.selection_basis =
                    "primary Main_Auto export paths passed after bounded vertical solve integration; raw diagnostic artifacts remain preserved";
                return selection;
            }

            if (correctedPasses)
            {
                FillSelectedCandidateFields(selection, corrected);
                selection.selected_candidate_output_role = "user_facing_export_artifact";
                selection.selected_candidate_preserves_raw_diagnostic = raw != null;
                FillAcceptanceEvidence(
                    selection,
                    raw,
                    corrected,
                    raw != null ? raw.vertical_solve_corrected_candidate_manifest_path : string.Empty,
                    projectRoot);
                selection.selection_basis = selection.selected_candidate_is_acceptance_artifact
                    ? "corrected candidate passed frame-quality gates and is selected for user-facing export; raw candidate remains recorded for diagnostics"
                    : "corrected candidate passed metric gates but did not produce a distinct export VMD; selected as diagnostic evidence while raw candidate remains recorded";
                return selection;
            }

            if (raw != null)
            {
                FillSelectedCandidateFields(selection, raw);
                if (HasPassingArtifact(raw))
                {
                    selection.selected_candidate_output_role = "user_facing_export_artifact";
                    selection.selected_candidate_preserves_raw_diagnostic = false;
                    FillAcceptanceEvidence(selection, raw, raw, string.Empty, projectRoot);
                    selection.selection_basis =
                        "raw candidate passed frame-quality gates and is selected for user-facing export; no corrected candidate was required";
                }
                else
                {
                    selection.selection_basis = corrected == null
                        ? "no corrected candidate is available; selected raw/evaluation candidate for diagnostics"
                        : "corrected candidate is not passing; selected raw/evaluation candidate for diagnostics";
                }
            }

            return selection;
        }

        private static bool HasPassingArtifact(MotionComparisonFrameQualitySummary summary)
        {
            return summary != null &&
                string.Equals(summary.status, "pass", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(summary.candidate_vmd_path) &&
                !string.IsNullOrWhiteSpace(summary.candidate_metrics_csv);
        }

        private static bool IsMainAutoSummary(MotionComparisonFrameQualitySummary summary)
        {
            return summary != null &&
                !string.IsNullOrWhiteSpace(summary.candidate_label) &&
                summary.candidate_label.IndexOf("Main_Auto", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void FillSelectedCandidateFields(
            VisualComparisonCandidateArtifactSelectionData selection,
            MotionComparisonFrameQualitySummary summary)
        {
            if (selection == null || summary == null)
            {
                return;
            }

            selection.selected_candidate_role = summary.frame_quality_evaluation_role ?? string.Empty;
            selection.selected_candidate_status = summary.status ?? string.Empty;
            selection.selected_candidate_status_reason = summary.status_reason ?? string.Empty;
            selection.selected_candidate_metrics_csv = summary.candidate_metrics_csv ?? string.Empty;
            selection.selected_candidate_vmd_path = summary.candidate_vmd_path ?? string.Empty;
        }

        private static void FillAcceptanceEvidence(
            VisualComparisonCandidateArtifactSelectionData selection,
            MotionComparisonFrameQualitySummary raw,
            MotionComparisonFrameQualitySummary selected,
            string selectedManifestPath,
            string projectRoot)
        {
            if (selection == null || selected == null)
            {
                return;
            }

            selection.selected_candidate_manifest_path = selectedManifestPath ?? string.Empty;
            bool selectedCorrectedArtifact = string.Equals(
                selected.frame_quality_evaluation_role,
                "corrected_candidate_metrics",
                StringComparison.Ordinal);
            bool selectedIntegratedPrimary = string.Equals(
                selected.frame_quality_evaluation_role,
                "main_auto_integrated_vertical_solve_metrics",
                StringComparison.Ordinal);
            bool selectedRawPrimary = VisualComparisonFrameQualityFailurePolicy.IsRawCandidateRole(selected);
            if (selectedCorrectedArtifact)
            {
                EnsureCorrectedCandidateManifest(selection, raw, projectRoot);
            }

            selection.selected_candidate_vmd_exists =
                !string.IsNullOrWhiteSpace(selection.selected_candidate_vmd_path) &&
                File.Exists(selection.selected_candidate_vmd_path);
            selection.selected_candidate_metrics_exists =
                !string.IsNullOrWhiteSpace(selection.selected_candidate_metrics_csv) &&
                File.Exists(selection.selected_candidate_metrics_csv);
            selection.selected_candidate_manifest_exists =
                !string.IsNullOrWhiteSpace(selection.selected_candidate_manifest_path) &&
                File.Exists(selection.selected_candidate_manifest_path);
            string integratedRawDiagnosticVmdPath = selectedIntegratedPrimary
                ? ResolveIntegratedPrimaryRawDiagnosticVmdPath(
                    selection.selected_candidate_manifest_path,
                    projectRoot)
                : string.Empty;
            bool differsFromRawSummary =
                raw != null &&
                !string.IsNullOrWhiteSpace(raw.candidate_vmd_path) &&
                !string.IsNullOrWhiteSpace(selection.selected_candidate_vmd_path) &&
                File.Exists(raw.candidate_vmd_path) &&
                selection.selected_candidate_vmd_exists &&
                !VisualComparisonArtifactPathResolver.ReferToSameFile(
                    raw.candidate_vmd_path,
                    selection.selected_candidate_vmd_path) &&
                FilesDiffer(raw.candidate_vmd_path, selection.selected_candidate_vmd_path);
            bool differsFromIntegratedRawDiagnostic =
                selectedIntegratedPrimary &&
                !string.IsNullOrWhiteSpace(integratedRawDiagnosticVmdPath) &&
                !string.IsNullOrWhiteSpace(selection.selected_candidate_vmd_path) &&
                File.Exists(integratedRawDiagnosticVmdPath) &&
                selection.selected_candidate_vmd_exists &&
                !VisualComparisonArtifactPathResolver.ReferToSameFile(
                    integratedRawDiagnosticVmdPath,
                    selection.selected_candidate_vmd_path) &&
                FilesDiffer(integratedRawDiagnosticVmdPath, selection.selected_candidate_vmd_path);
            selection.selected_candidate_differs_from_raw_vmd =
                differsFromRawSummary || differsFromIntegratedRawDiagnostic;
            selection.selected_candidate_differs_from_raw_metrics =
                raw != null &&
                !string.IsNullOrWhiteSpace(raw.candidate_metrics_csv) &&
                !string.IsNullOrWhiteSpace(selection.selected_candidate_metrics_csv) &&
                File.Exists(raw.candidate_metrics_csv) &&
                selection.selected_candidate_metrics_exists &&
                !VisualComparisonArtifactPathResolver.ReferToSameFile(
                    raw.candidate_metrics_csv,
                    selection.selected_candidate_metrics_csv) &&
                FilesDiffer(raw.candidate_metrics_csv, selection.selected_candidate_metrics_csv);

            bool selectedPasses = string.Equals(selected.status, "pass", StringComparison.OrdinalIgnoreCase);
            bool hasRequiredFiles = selectedCorrectedArtifact
                ? selection.selected_candidate_vmd_exists &&
                  selection.selected_candidate_metrics_exists &&
                  selection.selected_candidate_manifest_exists &&
                  selection.selected_candidate_differs_from_raw_vmd
                : selectedIntegratedPrimary
                    ? selection.selected_candidate_vmd_exists &&
                      selection.selected_candidate_metrics_exists &&
                      selection.selected_candidate_manifest_exists &&
                      selection.selected_candidate_differs_from_raw_vmd
                    : selectedRawPrimary &&
                      selection.selected_candidate_vmd_exists &&
                      selection.selected_candidate_metrics_exists;
            selection.selected_candidate_is_acceptance_artifact =
                selectedPasses &&
                (selectedCorrectedArtifact || selectedIntegratedPrimary || selectedRawPrimary) &&
                string.Equals(
                    selection.selected_candidate_output_role,
                    "user_facing_export_artifact",
                    StringComparison.Ordinal) &&
                (selectedRawPrimary || selection.selected_candidate_preserves_raw_diagnostic) &&
                hasRequiredFiles;
            if (selectedCorrectedArtifact &&
                selection.selected_candidate_vmd_exists &&
                selection.selected_candidate_metrics_exists &&
                selection.selected_candidate_manifest_exists &&
                !selection.selected_candidate_differs_from_raw_vmd)
            {
                selection.selected_candidate_output_role = "diagnostic_artifact";
            }

            selection.selected_candidate_acceptance_basis = selection.selected_candidate_is_acceptance_artifact
                ? selectedIntegratedPrimary
                    ? "selected primary Main_Auto export VMD/metrics/manifest is the final acceptance/export candidate; raw diagnostic files remain preserved"
                    : selectedCorrectedArtifact
                        ? "selected corrected VMD/metrics/manifest is the final acceptance/export candidate; raw candidate remains diagnostic"
                        : "selected raw VMD/metrics is the final acceptance/export candidate; no corrected artifact was required"
                : selectedCorrectedArtifact &&
                  selection.selected_candidate_vmd_exists &&
                  selection.selected_candidate_metrics_exists &&
                  selection.selected_candidate_manifest_exists &&
                  !selection.selected_candidate_differs_from_raw_vmd
                    ? "selected corrected metrics/manifest use a raw-copy VMD, so they are diagnostic only; raw candidate remains the diagnostic baseline"
                    : "selected candidate is not a final acceptance/export artifact yet; raw candidate remains the diagnostic baseline";
        }

        private static void EnsureCorrectedCandidateManifest(
            VisualComparisonCandidateArtifactSelectionData selection,
            MotionComparisonFrameQualitySummary raw,
            string projectRoot)
        {
            if (selection == null || raw == null)
            {
                return;
            }

            string manifestPath = ResolveArtifactPath(
                selection.selected_candidate_manifest_path,
                projectRoot,
                string.Empty);
            if (string.IsNullOrWhiteSpace(manifestPath) || File.Exists(manifestPath))
            {
                return;
            }

            string rawMetricsPath = ResolveArtifactPath(raw.candidate_metrics_csv, projectRoot, string.Empty);
            string rawVmdPath = ResolveArtifactPath(raw.candidate_vmd_path, projectRoot, string.Empty);
            string correctedMetricsPath = ResolveArtifactPath(
                selection.selected_candidate_metrics_csv,
                projectRoot,
                string.Empty);
            string correctedVmdPath = ResolveArtifactPath(
                selection.selected_candidate_vmd_path,
                projectRoot,
                string.Empty);
            if (!File.Exists(rawMetricsPath) ||
                !File.Exists(rawVmdPath) ||
                !File.Exists(correctedMetricsPath) ||
                !File.Exists(correctedVmdPath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 기존 진단 산출물의 manifest_source 계약을 유지함.
            string json =
                "{" +
                "\"artifact_role\":\"corrected_vertical_solve_candidate\"," +
                "\"generated_at\":\"" + EscapeJson(DateTime.Now.ToString("o", CultureInfo.InvariantCulture)) + "\"," +
                "\"raw_candidate_metrics_csv\":\"" + EscapeJson(raw.candidate_metrics_csv) + "\"," +
                "\"raw_candidate_vmd_path\":\"" + EscapeJson(raw.candidate_vmd_path) + "\"," +
                "\"corrected_candidate_metrics_csv\":\"" + EscapeJson(selection.selected_candidate_metrics_csv) + "\"," +
                "\"corrected_candidate_vmd_path\":\"" + EscapeJson(selection.selected_candidate_vmd_path) + "\"," +
                "\"frame_quality_evaluator\":\"raw_frame_quality_evaluator\"," +
                "\"manifest_source\":\"yyb_visual_candidate_selection\"" +
                "}";
            File.WriteAllText(manifestPath, json, Encoding.UTF8);
        }

        private static string ResolveIntegratedPrimaryRawDiagnosticVmdPath(
            string manifestPath,
            string projectRoot)
        {
            string absoluteManifestPath = ResolveArtifactPath(manifestPath, projectRoot, string.Empty);
            if (string.IsNullOrWhiteSpace(absoluteManifestPath) || !File.Exists(absoluteManifestPath))
            {
                return string.Empty;
            }

            try
            {
                IntegratedVerticalSolvePrimaryExportManifest manifest =
                    JsonUtility.FromJson<IntegratedVerticalSolvePrimaryExportManifest>(
                        File.ReadAllText(absoluteManifestPath, Encoding.UTF8));
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.raw_diagnostic_vmd_path))
                {
                    return string.Empty;
                }

                return ResolveArtifactPath(
                    manifest.raw_diagnostic_vmd_path,
                    projectRoot,
                    Path.GetDirectoryName(absoluteManifestPath));
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string ResolveArtifactPath(string path, string projectRoot, string baseDirectory)
        {
            return VisualComparisonArtifactPathResolver.ResolveArtifactPath(
                path,
                projectRoot,
                baseDirectory);
        }

        private static bool FilesDiffer(string leftPath, string rightPath)
        {
            FileInfo left = new FileInfo(leftPath);
            FileInfo right = new FileInfo(rightPath);
            if (left.Length != right.Length)
            {
                return true;
            }

            byte[] leftBytes = File.ReadAllBytes(leftPath);
            byte[] rightBytes = File.ReadAllBytes(rightPath);
            if (leftBytes.Length != rightBytes.Length)
            {
                return true;
            }

            for (int i = 0; i < leftBytes.Length; i++)
            {
                if (leftBytes[i] != rightBytes[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static void FillRawCandidateFields(
            VisualComparisonCandidateArtifactSelectionData selection,
            MotionComparisonFrameQualitySummary raw)
        {
            if (selection == null || raw == null)
            {
                return;
            }

            selection.raw_candidate_status = raw.status ?? string.Empty;
            selection.raw_candidate_status_reason = raw.status_reason ?? string.Empty;
            selection.raw_candidate_metrics_csv = raw.candidate_metrics_csv ?? string.Empty;
            selection.raw_candidate_vmd_path = raw.candidate_vmd_path ?? string.Empty;
        }

        private static void FillCorrectedCandidateFields(
            VisualComparisonCandidateArtifactSelectionData selection,
            MotionComparisonFrameQualitySummary corrected)
        {
            if (selection == null || corrected == null)
            {
                return;
            }

            selection.corrected_candidate_status = corrected.status ?? string.Empty;
            selection.corrected_candidate_status_reason = corrected.status_reason ?? string.Empty;
            selection.corrected_candidate_metrics_csv = corrected.candidate_metrics_csv ?? string.Empty;
            selection.corrected_candidate_vmd_path = corrected.candidate_vmd_path ?? string.Empty;
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        [Serializable]
        private sealed class IntegratedVerticalSolvePrimaryExportManifest
        {
            public string raw_diagnostic_vmd_path = string.Empty;
        }
    }
}
#endif
