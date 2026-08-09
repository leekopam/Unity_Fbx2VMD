#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public static partial class YybVisualComparisonBatchRunner
    {

        private static MotionComparisonFrameQualitySummary[] BuildFrameQualitySummaries()
        {
            return BuildFrameQualitySummaries(BuildCurrentSummaryFrameRoleDiagnostics());
        }

        private static SummaryFrameRoleDiagnostics BuildCurrentSummaryFrameRoleDiagnostics()
        {
            return BuildSummaryFrameRoleDiagnostics(
                ResolveSummaryTargetFrameCount(),
                ResolveFrameCount(CaptureMode.SubManualTestPrefab),
                ResolveFrameCount(CaptureMode.MainAuto));
        }

        private static MotionComparisonFrameQualitySummary[] BuildFrameQualitySummaries(
            SummaryFrameRoleDiagnostics frameRoleDiagnostics)
        {
            CaptureResult baseline = Results.FirstOrDefault(result =>
                string.Equals(result.jobMode, CaptureMode.SubManualTestPrefab.ToString(), StringComparison.Ordinal));
            if (baseline == null)
            {
                return Array.Empty<MotionComparisonFrameQualitySummary>();
            }

            List<MotionComparisonFrameQualitySummary> frameQualitySummaries = new List<MotionComparisonFrameQualitySummary>();
            foreach (CaptureResult candidate in EnumerateMainSceneCandidates())
            {
                frameQualitySummaries.AddRange(BuildFrameQualitySummariesForCandidate(baseline, candidate));
            }

            foreach (MotionComparisonFrameQualitySummary frameQualitySummary in frameQualitySummaries)
            {
                MotionComparisonProbeReportWriter.AttachLatestMmdAutomationEvidence(
                    frameQualitySummary,
                    _projectRoot,
                    Path.Combine(_projectRoot, MmdAutomationRunsRelativePath));
            }

            MotionComparisonFrameQualitySummary[] summaries = frameQualitySummaries.ToArray();
            ApplyImportedFbxVisualEvidenceFrameQualityPolicy(summaries, frameRoleDiagnostics);
            return summaries;
        }

        private static void PromoteFrameQualityFailuresToRunFailures(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics = null)
        {
            foreach (string failure in BuildFrameQualityFailureMessages(frameQualitySummaries, frameRoleDiagnostics))
            {
                if (!Failures.Contains(failure))
                {
                    Failures.Add(failure);
                }
            }
        }

        private static string[] BuildFrameQualityFailureMessages(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries)
        {
            return BuildFrameQualityFailureMessages(frameQualitySummaries, null);
        }

        private static string[] BuildFrameQualityFailureMessages(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics)
        {
            if (frameQualitySummaries == null || frameQualitySummaries.Length == 0)
            {
                return Array.Empty<string>();
            }

            if (frameRoleDiagnostics != null)
            {
                ApplyImportedFbxVisualEvidenceFrameQualityPolicy(frameQualitySummaries, frameRoleDiagnostics);
            }

            List<string> failures = new List<string>();
            bool acceptedUserFacingArtifactPreservesRawDiagnostic =
                HasAcceptedUserFacingArtifactPreservingRawDiagnostic(frameQualitySummaries);
            foreach (MotionComparisonFrameQualitySummary summary in frameQualitySummaries)
            {
                if (summary == null ||
                    !string.Equals(summary.status, "fail", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (acceptedUserFacingArtifactPreservesRawDiagnostic &&
                    IsRawCandidateRole(summary))
                {
                    continue;
                }

                string candidate = string.IsNullOrWhiteSpace(summary.candidate_label)
                    ? "unknown candidate"
                    : summary.candidate_label;
                string role = string.IsNullOrWhiteSpace(summary.frame_quality_evaluation_role)
                    ? "unknown_role"
                    : summary.frame_quality_evaluation_role;
                string reason = string.IsNullOrWhiteSpace(summary.status_reason)
                    ? "status=fail"
                    : summary.status_reason;

                failures.Add(
                    "frame-quality gate failed: " +
                    $"candidate={candidate}; " +
                    $"role={role}; " +
                    $"reason={reason}; " +
                    $"metrics={summary.candidate_metrics_csv ?? string.Empty}; " +
                    $"vmd={summary.candidate_vmd_path ?? string.Empty}");
            }

            return failures.ToArray();
        }

        private static bool HasAcceptedUserFacingArtifactPreservingRawDiagnostic(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries)
        {
            SummaryCandidateArtifactSelection selection = BuildCandidateArtifactSelection(frameQualitySummaries);
            return selection != null &&
                selection.selected_candidate_is_acceptance_artifact &&
                selection.selected_candidate_preserves_raw_diagnostic &&
                string.Equals(selection.selected_candidate_output_role, "user_facing_export_artifact", StringComparison.Ordinal);
        }

        private static void ApplyImportedFbxVisualEvidenceFrameQualityPolicy(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics)
        {
            if (!HasReferenceAlignedImportedFbxVisualEvidence(frameRoleDiagnostics) ||
                frameQualitySummaries == null ||
                frameQualitySummaries.Length == 0)
            {
                return;
            }

            foreach (MotionComparisonFrameQualitySummary summary in frameQualitySummaries)
            {
                if (summary == null ||
                    !string.Equals(summary.status, "fail", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsSubManualPoseOnlyResidual(summary))
                {
                    MarkFrameQualityAsImportedFbxReferenceAligned(
                        summary,
                        "Sub_Manual Unity pose delta kept as diagnostic because time-matched ref MP4 image-space evidence is aligned");
                    continue;
                }

                if (IsEvaluationCandidateRole(summary) &&
                    HasReferenceAlignedCorrectedCounterpart(summary, frameQualitySummaries))
                {
                    MarkFrameQualityAsImportedFbxReferenceAligned(
                        summary,
                        "raw replay vertical residual kept as diagnostic because corrected candidate and ref MP4 image-space evidence are aligned");
                }
            }
        }

        private static bool HasReferenceAlignedImportedFbxVisualEvidence(
            SummaryFrameRoleDiagnostics frameRoleDiagnostics)
        {
            if (frameRoleDiagnostics == null)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(frameRoleDiagnostics.candidate_screenshot_frame_metrics_error) &&
                string.IsNullOrWhiteSpace(frameRoleDiagnostics.reference_mp4_analysis_error) &&
                string.IsNullOrWhiteSpace(frameRoleDiagnostics.reference_mp4_frame_metrics_error) &&
                frameRoleDiagnostics.reference_mp4_current_clip_sample_count >= ReferenceAlignedVisualEvidenceMinMatchedSamples &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_sample_count >= ReferenceAlignedVisualEvidenceMinMatchedSamples &&
                frameRoleDiagnostics.candidate_screenshot_nonblank_frame_count >=
                    frameRoleDiagnostics.candidate_vs_reference_time_matched_sample_count &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_seconds_gap) &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_max_seconds_gap <= ReferenceAlignedVisualEvidenceMaxSecondsGap &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta) &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta <=
                    ReferenceAlignedVisualEvidenceMaxBboxHeightDelta &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta) &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta <=
                    ReferenceAlignedVisualEvidenceMaxBottomGapDelta &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta) &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta <=
                    ReferenceAlignedVisualEvidenceMaxSilhouetteProfileL1Delta &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta) &&
                frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta <=
                    ReferenceAlignedVisualEvidenceMaxSilhouetteProfileBandDelta &&
                IsFiniteMetric(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta) &&
                IsWithinEndpointPixelTolerance(
                    frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta,
                    ReferenceAlignedVisualEvidenceMaxSilhouetteLandmarkEndpointDelta);
        }

        private static bool IsWithinEndpointPixelTolerance(float value, float threshold)
        {
            return value <= threshold + ReferenceAlignedVisualEvidenceEndpointPixelTolerance;
        }

        private static bool IsSubManualPoseOnlyResidual(MotionComparisonFrameQualitySummary summary)
        {
            string reason = summary.status_reason ?? string.Empty;
            return reason.IndexOf("same-frame limb pose delta threshold exceeded", StringComparison.OrdinalIgnoreCase) >= 0 &&
                reason.IndexOf("YYB deformation risk", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("below-floor", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("root position delta threshold exceeded", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("one-frame root/center/IK teleport", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("stationary preview limb-motion root travel", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("same-frame hips Y delta fail threshold exceeded", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("same-frame foot bottom Y delta fail threshold exceeded", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool HasReferenceAlignedCorrectedCounterpart(
            MotionComparisonFrameQualitySummary summary,
            MotionComparisonFrameQualitySummary[] frameQualitySummaries)
        {
            string candidate = summary.candidate_label ?? string.Empty;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            return frameQualitySummaries.Any(other =>
                !ReferenceEquals(other, summary) &&
                other != null &&
                IsCorrectedCandidateForRawCandidate(candidate, other.candidate_label ?? string.Empty) &&
                IsCorrectedCandidateRole(other) &&
                (string.Equals(other.status, "pass", StringComparison.OrdinalIgnoreCase) ||
                    IsSubManualPoseOnlyResidual(other)));
        }

        private static bool IsCorrectedCandidateForRawCandidate(string rawCandidateLabel, string correctedCandidateLabel)
        {
            if (string.IsNullOrWhiteSpace(rawCandidateLabel) ||
                string.IsNullOrWhiteSpace(correctedCandidateLabel))
            {
                return false;
            }

            return string.Equals(correctedCandidateLabel, rawCandidateLabel, StringComparison.Ordinal) ||
                correctedCandidateLabel.StartsWith(rawCandidateLabel + " ", StringComparison.Ordinal);
        }

        private static bool IsEvaluationCandidateRole(MotionComparisonFrameQualitySummary summary)
        {
            return string.Equals(
                summary.frame_quality_evaluation_role,
                "evaluation_candidate_metrics",
                StringComparison.Ordinal);
        }

        private static bool IsCorrectedCandidateRole(MotionComparisonFrameQualitySummary summary)
        {
            return string.Equals(
                summary.frame_quality_evaluation_role,
                "corrected_candidate_metrics",
                StringComparison.Ordinal);
        }

        private static void MarkFrameQualityAsImportedFbxReferenceAligned(
            MotionComparisonFrameQualitySummary summary,
            string basis)
        {
            summary.status = "pass";
            summary.status_reason = string.IsNullOrWhiteSpace(summary.status_reason)
                ? basis
                : $"{basis}; diagnostic={summary.status_reason}";
        }

        private static IEnumerable<CaptureResult> EnumerateMainSceneCandidates()
        {
            return Results.Where(result =>
                result != null &&
                IsMainSceneCandidateMode(result.jobMode) &&
                ShouldBuildFrameQualityDiagnostic(result.success, result.comparisonMetricsCsvPath, result.vmdPath));
        }

        private static bool IsMainSceneCandidateMode(string jobMode)
        {
            return string.Equals(jobMode, CaptureMode.MainRecording.ToString(), StringComparison.Ordinal) ||
                string.Equals(jobMode, CaptureMode.MainRecordingVmdPlaybackProbe.ToString(), StringComparison.Ordinal) ||
                string.Equals(jobMode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal);
        }

        private static bool ShouldBuildFrameQualityDiagnostic(bool success, string metricsCsvPath, string vmdPath)
        {
            return success ||
                (!string.IsNullOrWhiteSpace(metricsCsvPath) &&
                    !string.IsNullOrWhiteSpace(vmdPath));
        }

        private static MotionComparisonFrameQualitySummary[] BuildFrameQualitySummariesForCandidate(
            CaptureResult baseline,
            CaptureResult candidate)
        {
            if (baseline == null || candidate == null)
            {
                return Array.Empty<MotionComparisonFrameQualitySummary>();
            }

            ResolveShortCandidateVmdPath(candidate);
            MotionComparisonFrameQualitySummary summary = MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                baseline.jobDisplayName,
                ToAbsoluteProjectPath(baseline.comparisonMetricsCsvPath),
                candidate.jobDisplayName,
                ToAbsoluteProjectPath(candidate.comparisonMetricsCsvPath),
                ToAbsoluteProjectPath(candidate.vmdPath),
                baseline.frameCount,
                candidate.frameCount,
                ResolveSummaryTargetFrameCount());
            string integratedVerticalSolveRole = ResolveIntegratedVerticalSolveRole(candidate.jobMode);
            if (!string.IsNullOrEmpty(integratedVerticalSolveRole) &&
                MotionComparisonProbeReportWriter.TryPromoteVerticalSolveCorrectedCandidateToPrimaryExport(
                    summary,
                    out VerticalSolvePrimaryExportPromotion promotion))
            {
                candidate.fileSizeBytes = promotion.promoted_vmd_bytes;
                summary = MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                    baseline.jobDisplayName,
                    ToAbsoluteProjectPath(baseline.comparisonMetricsCsvPath),
                    candidate.jobDisplayName,
                    ToAbsoluteProjectPath(candidate.comparisonMetricsCsvPath),
                    ToAbsoluteProjectPath(candidate.vmdPath),
                    baseline.frameCount,
                    candidate.frameCount,
                    ResolveSummaryTargetFrameCount());
                summary.frame_quality_evaluation_role = integratedVerticalSolveRole;
                summary.frame_quality_evaluation_basis = ResolveIntegratedVerticalSolveBasis(candidate.jobMode);
                summary.vertical_solve_corrected_candidate_manifest_path = promotion.integrated_manifest_path;
            }
            MotionComparisonFrameQualitySummary[] summaries =
                MotionComparisonProbeReportWriter.BuildFrameQualityEvaluationEntries(summary);
            return summaries;
        }

        private static string ResolveIntegratedVerticalSolveRole(string jobMode)
        {
            if (string.Equals(jobMode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal))
            {
                return "main_auto_integrated_vertical_solve_metrics";
            }

            if (string.Equals(jobMode, CaptureMode.MainRecordingVmdPlaybackProbe.ToString(), StringComparison.Ordinal))
            {
                return "vmd_replay_integrated_vertical_solve_metrics";
            }

            return string.Empty;
        }

        private static string ResolveIntegratedVerticalSolveBasis(string jobMode)
        {
            if (string.Equals(jobMode, CaptureMode.MainRecordingVmdPlaybackProbe.ToString(), StringComparison.Ordinal))
            {
                return "primary VMD replay diagnostic output after bounded vertical solve promotion; raw replay metrics/VMD were preserved as raw_vertical_solve_diagnostic artifacts";
            }

            return "primary Main_Auto result paths after bounded vertical solve promotion; raw metrics/VMD were preserved as raw_vertical_solve_diagnostic artifacts";
        }

        private static void ResolveShortCandidateVmdPath(CaptureResult candidate)
        {
            if (candidate == null ||
                !IsMainSceneCandidateMode(candidate.jobMode) ||
                string.Equals(candidate.jobMode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(_summaryDirectory))
            {
                return;
            }

            string sourceExtension = Path.GetExtension(candidate.vmdPath);
            if (string.IsNullOrWhiteSpace(sourceExtension))
            {
                sourceExtension = ".vmd";
            }

            string shortPath = Path.Combine(
                _summaryDirectory,
                BuildCandidateVmdEvidenceFileName(candidate.jobMode, sourceExtension));
            if (!File.Exists(shortPath))
            {
                return;
            }

            string currentAbsolutePath = ToAbsoluteProjectPath(candidate.vmdPath);
            if (string.Equals(currentAbsolutePath, shortPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            candidate.vmdPath = MakeProjectRelativePath(shortPath);
            candidate.fileSizeBytes = new FileInfo(shortPath).Length;
            SavePersistedState();
        }

        private static int ResolveFrameCount(CaptureMode mode)
        {
            CaptureResult result = Results.FirstOrDefault(captureResult =>
                string.Equals(captureResult.jobMode, mode.ToString(), StringComparison.Ordinal));
            return result != null ? result.frameCount : 0;
        }

        private static int ResolveMainAutoFrameCount()
        {
            return ResolveFrameCount(CaptureMode.MainAuto);
        }

        private static int ResolveSummaryTargetFrameCount()
        {
            return ResolveSummaryTargetFrameCount(
                ResolveReferenceMmdTargetFrameCount(
                    _fbxFileName,
                    _durationSeconds,
                    _targetFrameCount,
                    _referenceClip != null ? _referenceClip.length : 0f,
                    DefaultFrameRate),
                ResolveMainAutoFrameCount());
        }

        private static int ResolveSummaryTargetFrameCount(int referenceTargetFrameCount, int mainAutoFrameCount)
        {
            _ = mainAutoFrameCount;
            return Mathf.Max(0, referenceTargetFrameCount);
        }

        private static int ResolveReferenceMmdTargetFrameCount(
            string fbxFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate)
        {
            if (TryResolveKnownMmdReferenceTargetFrameCount(
                    fbxFileName,
                    requestedDurationSeconds,
                    configuredTargetFrameCount,
                    referenceClipLengthSeconds,
                    recordingFrameRate,
                    out int referenceTargetFrameCount))
            {
                return referenceTargetFrameCount;
            }

            return Mathf.Max(0, configuredTargetFrameCount);
        }

        private static bool TryResolveKnownMmdReferenceTargetFrameCount(
            string fbxFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate,
            out int referenceTargetFrameCount)
        {
            referenceTargetFrameCount = 0;
            if (recordingFrameRate <= 0f ||
                float.IsNaN(recordingFrameRate) ||
                float.IsInfinity(recordingFrameRate) ||
                requestedDurationSeconds <= 0f ||
                float.IsNaN(requestedDurationSeconds) ||
                float.IsInfinity(requestedDurationSeconds) ||
                configuredTargetFrameCount <= 0 ||
                referenceClipLengthSeconds <= 0f ||
                float.IsNaN(referenceClipLengthSeconds) ||
                float.IsInfinity(referenceClipLengthSeconds))
            {
                return false;
            }

            string cleanBaseName = Path.GetFileNameWithoutExtension(fbxFileName ?? string.Empty);
            if (!string.Equals(cleanBaseName, SatisfactionReferenceOutputBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int knownReferenceFrameCount = SatisfactionReferenceMaxMmdFrame + 1;
            float knownReferenceDurationSeconds = knownReferenceFrameCount / recordingFrameRate;
            float frameToleranceSeconds = 0.5f / recordingFrameRate;
            bool clipCoversReference = referenceClipLengthSeconds + frameToleranceSeconds >= knownReferenceDurationSeconds;
            bool requestCoversReference = requestedDurationSeconds + frameToleranceSeconds >= knownReferenceDurationSeconds;
            bool configuredFramesCoverReference = configuredTargetFrameCount >= knownReferenceFrameCount;
            if (!clipCoversReference || !requestCoversReference || !configuredFramesCoverReference)
            {
                return false;
            }

            referenceTargetFrameCount = knownReferenceFrameCount;
            return true;
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                _durationSeconds,
                ResolveReferenceMp4CurrentClipStartSeconds());
        }

        private static float ResolveReferenceMp4CurrentClipStartSeconds()
        {
            float referenceClipLengthSeconds = _referenceClip != null ? _referenceClip.length : 0f;
            ReferenceMmdTimingPlan timingPlan = BuildReferenceMmdTimingPlan(
                referenceClipLengthSeconds,
                _durationSeconds,
                _editorDiagnosticSmokeSegment,
                _enableReferenceMmdTimingRuntimeOverride);
            return timingPlan.ReferenceMp4StartSeconds;
        }

        private static float ResolveKnownReferenceMmdDurationSeconds()
        {
            return (SatisfactionReferenceMaxMmdFrame + 1) / DefaultFrameRate;
        }

        private static ReferenceMmdTimingPlan BuildReferenceMmdTimingPlan(
            float referenceClipLengthSeconds,
            float requestedDurationSeconds,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment,
            bool enabled)
        {
            float safeClipLength = Mathf.Max(0f, referenceClipLengthSeconds);
            float safeDuration = Mathf.Max(0.1f, requestedDurationSeconds);
            float defaultStart = CalculateEditorDiagnosticSmokeStartTime(
                safeClipLength,
                safeDuration,
                segment);

            ReferenceMmdTimingPlan plan = new ReferenceMmdTimingPlan
            {
                Enabled = false,
                HasCandidateTimingOverride = false,
                ReferenceMp4StartSeconds = defaultStart,
                CandidateClipStartSeconds = defaultStart,
                CandidateClipSecondsPerReferenceSecond = 1f,
                ReferenceDurationSeconds = safeClipLength
            };

            float knownReferenceDuration = ResolveKnownReferenceMmdDurationSeconds();
            if (!enabled ||
                safeClipLength <= 0f ||
                knownReferenceDuration <= 0f ||
                float.IsNaN(knownReferenceDuration) ||
                float.IsInfinity(knownReferenceDuration))
            {
                return plan;
            }

            float referenceStart = CalculateEditorDiagnosticSmokeStartTime(
                knownReferenceDuration,
                safeDuration,
                segment);
            float candidateScale = Mathf.Max(0.0001f, safeClipLength / knownReferenceDuration);
            float candidateStart = referenceStart * candidateScale;
            float maxCandidateStart = Mathf.Max(0f, safeClipLength - (safeDuration * candidateScale));

            plan.Enabled = true;
            plan.HasCandidateTimingOverride = true;
            plan.ReferenceMp4StartSeconds = referenceStart;
            plan.CandidateClipStartSeconds = Mathf.Clamp(candidateStart, 0f, maxCandidateStart);
            plan.CandidateClipSecondsPerReferenceSecond = candidateScale;
            plan.ReferenceDurationSeconds = knownReferenceDuration;
            return plan;
        }

        private static float[] BuildReferenceMp4AlignedProbeSampleTimes(
            float referenceClipStartSeconds,
            float requestedDurationSeconds)
        {
            return BuildReferenceMp4AlignedProbeSampleTimes(
                referenceClipStartSeconds,
                requestedDurationSeconds,
                LoadReferenceMp4CurrentClipLocalSampleSeconds(
                    referenceClipStartSeconds,
                    requestedDurationSeconds));
        }

        private static float[] BuildReferenceMp4AlignedProbeSampleTimes(
            float referenceClipStartSeconds,
            float requestedDurationSeconds,
            float[] referenceLocalSampleSeconds)
        {
            return BuildReferenceMp4AlignedProbeSampleTimes(
                referenceClipStartSeconds,
                requestedDurationSeconds,
                referenceLocalSampleSeconds,
                1f);
        }

        private static float[] BuildReferenceMp4AlignedProbeSampleTimes(
            float referenceClipStartSeconds,
            float requestedDurationSeconds,
            float[] referenceLocalSampleSeconds,
            float candidateClipSecondsPerReferenceSecond)
        {
            float safeStart = Mathf.Max(0f, referenceClipStartSeconds);
            float safeDuration = Mathf.Max(0.1f, requestedDurationSeconds);
            float localSampleScale =
                candidateClipSecondsPerReferenceSecond <= 0f ||
                float.IsNaN(candidateClipSecondsPerReferenceSecond) ||
                float.IsInfinity(candidateClipSecondsPerReferenceSecond)
                    ? 1f
                    : candidateClipSecondsPerReferenceSecond;
            var absoluteSampleTimes = new List<float>();
            AddSegmentLocalProbeSamples(
                absoluteSampleTimes,
                safeStart,
                safeDuration,
                ReferenceMp4ProbeDefaultLocalSampleTimes,
                localSampleScale);
            AddSegmentLocalProbeSamples(
                absoluteSampleTimes,
                safeStart,
                safeDuration,
                referenceLocalSampleSeconds,
                localSampleScale);

            if (absoluteSampleTimes.Count <= 0)
            {
                return Array.Empty<float>();
            }

            absoluteSampleTimes.Sort();
            var deduplicated = new List<float>(absoluteSampleTimes.Count);
            float dedupeSeconds = (0.5f / DefaultFrameRate) + 0.0001f;
            foreach (float sampleTime in absoluteSampleTimes)
            {
                if (deduplicated.Count > 0 &&
                    Mathf.Abs(deduplicated[deduplicated.Count - 1] - sampleTime) <= dedupeSeconds)
                {
                    deduplicated[deduplicated.Count - 1] = sampleTime;
                    continue;
                }

                deduplicated.Add(sampleTime);
            }

            return deduplicated.ToArray();
        }

        private static void AddSegmentLocalProbeSamples(
            List<float> absoluteSampleTimes,
            float referenceClipStartSeconds,
            float requestedDurationSeconds,
            IEnumerable<float> localSampleSeconds,
            float localSampleScale)
        {
            if (absoluteSampleTimes == null || localSampleSeconds == null)
            {
                return;
            }

            const float epsilonSeconds = 0.0001f;
            foreach (float localSampleSecond in localSampleSeconds)
            {
                if (float.IsNaN(localSampleSecond) ||
                    float.IsInfinity(localSampleSecond) ||
                    localSampleSecond < -epsilonSeconds ||
                    localSampleSecond > requestedDurationSeconds + epsilonSeconds)
                {
                    continue;
                }

                absoluteSampleTimes.Add(referenceClipStartSeconds + (Mathf.Clamp(
                    localSampleSecond,
                    0f,
                    requestedDurationSeconds) * Mathf.Max(0.0001f, localSampleScale)));
            }
        }

        private static float[] LoadReferenceMp4CurrentClipLocalSampleSeconds(
            float referenceClipStartSeconds,
            float requestedDurationSeconds)
        {
            string frameMetricsPath = ResolveProjectRelativePath(ReferenceMp4FrameMetricsRelativePath);
            if (!File.Exists(frameMetricsPath))
            {
                return Array.Empty<float>();
            }

            try
            {
                ReferenceMp4FrameMetrics metrics = JsonUtility.FromJson<ReferenceMp4FrameMetrics>(
                    File.ReadAllText(frameMetricsPath, Encoding.UTF8));
                return ExtractReferenceMp4CurrentClipLocalSampleSeconds(
                    metrics,
                    referenceClipStartSeconds,
                    requestedDurationSeconds);
            }
            catch (Exception ex)
            {
                AppendRunnerTrace(
                    $"reference mp4 sample load failed path={ReferenceMp4FrameMetricsRelativePath} " +
                    $"error={ex.GetType().Name}: {ex.Message}");
                return Array.Empty<float>();
            }
        }

        private static float[] ExtractReferenceMp4CurrentClipLocalSampleSeconds(
            ReferenceMp4FrameMetrics metrics,
            float referenceClipStartSeconds,
            float requestedDurationSeconds)
        {
            if (metrics == null || metrics.rows == null)
            {
                return Array.Empty<float>();
            }

            float safeStart = Mathf.Max(0f, referenceClipStartSeconds);
            float safeDuration = Mathf.Max(0.1f, requestedDurationSeconds);
            float endSeconds = safeStart + safeDuration;
            const float epsilonSeconds = 0.0001f;
            var localSampleSeconds = new List<float>();
            foreach (ReferenceMp4FrameMetricRow row in metrics.rows)
            {
                if (row == null ||
                    float.IsNaN(row.seconds) ||
                    float.IsInfinity(row.seconds) ||
                    row.seconds < safeStart - epsilonSeconds ||
                    row.seconds > endSeconds + epsilonSeconds)
                {
                    continue;
                }

                localSampleSeconds.Add(Mathf.Clamp(row.seconds - safeStart, 0f, safeDuration));
            }

            localSampleSeconds.Sort();
            return localSampleSeconds.ToArray();
        }

        private static string FormatProbeSampleTimes(float[] sampleTimes)
        {
            if (sampleTimes == null || sampleTimes.Length <= 0)
            {
                return "none";
            }

            return string.Join(
                "/",
                sampleTimes.Select(sampleTime => sampleTime.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                0f);
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            float referenceClipStartSeconds)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                referenceClipStartSeconds,
                ReferenceMp4ProvenanceEvidenceRelativePath,
                ReferenceMp4AnalysisResultRelativePath,
                ReferenceMp4FrameMetricsRelativePath,
                ReferenceMp4ContactSheetRelativePath);
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            string referenceMp4ProvenanceEvidencePath,
            string referenceMp4AnalysisResultPath,
            string referenceMp4FrameMetricsPath,
            string referenceMp4ContactSheetPath)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                0f,
                referenceMp4ProvenanceEvidencePath,
                referenceMp4AnalysisResultPath,
                referenceMp4FrameMetricsPath,
                referenceMp4ContactSheetPath);
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string referenceMp4ProvenanceEvidencePath,
            string referenceMp4AnalysisResultPath,
            string referenceMp4FrameMetricsPath,
            string referenceMp4ContactSheetPath)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                referenceClipStartSeconds,
                referenceMp4ProvenanceEvidencePath,
                referenceMp4AnalysisResultPath,
                referenceMp4FrameMetricsPath,
                referenceMp4ContactSheetPath,
                ResolveCandidateFrameIndexPathForDiagnostics());
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            string referenceMp4ProvenanceEvidencePath,
            string referenceMp4AnalysisResultPath,
            string referenceMp4FrameMetricsPath,
            string referenceMp4ContactSheetPath,
            string candidateFrameIndexPath)
        {
            return BuildSummaryFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                0f,
                referenceMp4ProvenanceEvidencePath,
                referenceMp4AnalysisResultPath,
                referenceMp4FrameMetricsPath,
                referenceMp4ContactSheetPath,
                candidateFrameIndexPath);
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string referenceMp4ProvenanceEvidencePath,
            string referenceMp4AnalysisResultPath,
            string referenceMp4FrameMetricsPath,
            string referenceMp4ContactSheetPath,
            string candidateFrameIndexPath)
        {
            SummaryFrameRoleDiagnostics diagnostics = new SummaryFrameRoleDiagnostics
            {
                reference_target_frame_count = Mathf.Max(0, referenceTargetFrameCount),
                baseline_recorded_frame_count = Mathf.Max(0, baselineRecordedFrameCount),
                candidate_recorded_frame_count = Mathf.Max(0, candidateRecordedFrameCount),
                baseline_frame_count_delta_from_reference_target = referenceTargetFrameCount > 0
                    ? baselineRecordedFrameCount - referenceTargetFrameCount
                    : 0,
                candidate_frame_count_delta_from_reference_target = referenceTargetFrameCount > 0
                    ? candidateRecordedFrameCount - referenceTargetFrameCount
                    : 0,
                target_frame_count_role = "ref_mmd_mp4 expected frame range for the full satisfaction_2 reference",
                baseline_recorded_frame_count_role = "Sub_Manual recorded comparison baseline; reported separately and not used as target_frame_count",
                candidate_recorded_frame_count_role = "Main_Auto candidate capture under test",
                frame_quality_metric_basis = "Unity pose metrics compare Sub_Manual and Main_Auto rows by recorderFrame; the ref_mmd_mp4 count is only the frame-count target",
                vmd_export_metric_basis = "VMD export spike and floor metrics are evaluated on the Main_Auto candidate VMD"
            };
            AttachReferenceMp4Diagnostics(
                diagnostics,
                requestedDurationSeconds,
                referenceClipStartSeconds,
                referenceMp4ProvenanceEvidencePath,
                referenceMp4AnalysisResultPath,
                referenceMp4FrameMetricsPath,
                referenceMp4ContactSheetPath);
            YybScreenshotDiagnosticAnalyzer.AttachCandidateScreenshotFrameDiagnostics(
                diagnostics,
                candidateFrameIndexPath,
                _projectRoot);
            return diagnostics;
        }

        private static void AttachReferenceMp4Diagnostics(
            SummaryFrameRoleDiagnostics diagnostics,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string referenceMp4ProvenanceEvidencePath,
            string referenceMp4AnalysisResultPath,
            string referenceMp4FrameMetricsPath,
            string referenceMp4ContactSheetPath)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.reference_mp4_provenance_evidence_path = referenceMp4ProvenanceEvidencePath ?? string.Empty;
            diagnostics.reference_mp4_analysis_result_path = referenceMp4AnalysisResultPath ?? string.Empty;
            diagnostics.reference_mp4_frame_metrics_path = referenceMp4FrameMetricsPath ?? string.Empty;
            diagnostics.reference_mp4_contact_sheet_path = referenceMp4ContactSheetPath ?? string.Empty;
            diagnostics.reference_mp4_canonical_context =
                "Ref MP4 is a manually postprocessed MMD render from Sub_Manual testPrefab + satisfaction_2; it anchors visual framing/provenance while Unity pose gates compare Sub_Manual metrics to main candidates.";
            diagnostics.reference_mp4_analysis_metric_basis =
                "MP4 analysis supplies visual bbox/framing context; frame-quality gates remain same-recorderFrame Unity metrics and VMD export checks.";
            diagnostics.reference_mp4_current_clip_start_seconds = Mathf.Max(0f, referenceClipStartSeconds);
            diagnostics.reference_mp4_current_clip_duration_seconds = Mathf.Max(0f, requestedDurationSeconds);
            diagnostics.reference_mp4_current_clip_end_seconds =
                diagnostics.reference_mp4_current_clip_start_seconds +
                diagnostics.reference_mp4_current_clip_duration_seconds;
            diagnostics.reference_mp4_current_clip_first_sample_seconds = float.NaN;
            diagnostics.reference_mp4_current_clip_last_sample_seconds = float.NaN;
            diagnostics.reference_mp4_current_clip_sample_gap_seconds =
                diagnostics.reference_mp4_current_clip_duration_seconds;
            diagnostics.reference_mp4_current_clip_sample_basis =
                "Counts reference MP4 frame-metrics rows whose seconds are within the active clip start and requested duration for this visual compare run; stored sample seconds are local to the clip start.";
            diagnostics.reference_mp4_current_clip_framing_metric_basis =
                "Aggregates ref MP4 bbox/framing rows within the active clip start and requested duration, so head/middle/tail candidate screenshot deltas are aligned to the matching reference video window.";
            diagnostics.reference_mp4_current_clip_avg_bbox_height_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_avg_bbox_width_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_center_x_range_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_max_bottom_gap_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_avg_bright_area_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_avg_upper_limb_span_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_avg_lower_limb_span_ratio = float.NaN;
            diagnostics.reference_mp4_current_clip_sample_seconds = Array.Empty<float>();

            string resultPath = ResolveProjectRelativePath(diagnostics.reference_mp4_analysis_result_path);
            string frameMetricsPath = ResolveProjectRelativePath(diagnostics.reference_mp4_frame_metrics_path);
            string contactSheetPath = ResolveProjectRelativePath(diagnostics.reference_mp4_contact_sheet_path);
            diagnostics.reference_mp4_provenance_evidence_exists =
                File.Exists(ResolveProjectRelativePath(diagnostics.reference_mp4_provenance_evidence_path));
            diagnostics.reference_mp4_analysis_result_exists = File.Exists(resultPath);
            diagnostics.reference_mp4_frame_metrics_exists = File.Exists(frameMetricsPath);
            diagnostics.reference_mp4_contact_sheet_exists = File.Exists(contactSheetPath);

            if (diagnostics.reference_mp4_analysis_result_exists)
            {
                try
                {
                    ReferenceMp4AnalysisResult analysis = JsonUtility.FromJson<ReferenceMp4AnalysisResult>(
                        File.ReadAllText(resultPath, Encoding.UTF8));
                    if (analysis != null)
                    {
                        diagnostics.reference_mp4_analysis_schema = analysis.schema ?? string.Empty;
                        diagnostics.reference_mp4_extracted_frame_count = Mathf.Max(0, analysis.extractedFrameCount);
                        if (analysis.video != null)
                        {
                            diagnostics.reference_mp4_width = Mathf.Max(0, analysis.video.width);
                            diagnostics.reference_mp4_height = Mathf.Max(0, analysis.video.height);
                            diagnostics.reference_mp4_avg_frame_rate = analysis.video.avg_frame_rate ?? string.Empty;
                            diagnostics.reference_mp4_stream_duration_seconds = ParseInvariantFloat(analysis.video.stream_duration);
                            diagnostics.reference_mp4_total_video_frames = ParseInvariantInt(analysis.video.nb_frames);
                        }
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.reference_mp4_analysis_error = ex.GetType().Name + ": " + ex.Message;
                }
            }

            if (diagnostics.reference_mp4_frame_metrics_exists)
            {
                try
                {
                    ReferenceMp4FrameMetrics metrics = JsonUtility.FromJson<ReferenceMp4FrameMetrics>(
                        File.ReadAllText(frameMetricsPath, Encoding.UTF8));
                    if (metrics != null)
                    {
                        diagnostics.reference_mp4_frame_metrics_schema = metrics.schema ?? string.Empty;
                        diagnostics.reference_mp4_frame_metrics_sample_count = Mathf.Max(0, metrics.sampleCount);
                        diagnostics.reference_mp4_frame_metrics_extracted_frame_count = Mathf.Max(0, metrics.extractedFrameCount);
                        diagnostics.reference_mp4_avg_bbox_height_ratio = metrics.avgBBoxHeightRatio;
                        diagnostics.reference_mp4_avg_bbox_width_ratio = metrics.avgBBoxWidthRatio;
                        diagnostics.reference_mp4_center_x_range_ratio = metrics.centerXRangeRatio;
                        diagnostics.reference_mp4_max_bottom_gap_ratio = metrics.maxBottomGapRatio;
                        diagnostics.reference_mp4_avg_bright_area_ratio = metrics.avgBrightAreaRatio;
                        AttachReferenceMp4CurrentClipCoverage(diagnostics, metrics);
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.reference_mp4_frame_metrics_error = ex.GetType().Name + ": " + ex.Message;
                }
            }
        }

        private static void AttachReferenceMp4CurrentClipCoverage(
            SummaryFrameRoleDiagnostics diagnostics,
            ReferenceMp4FrameMetrics metrics)
        {
            if (diagnostics == null || metrics == null || metrics.rows == null)
            {
                return;
            }

            float startSeconds = Mathf.Max(0f, diagnostics.reference_mp4_current_clip_start_seconds);
            float durationSeconds = Mathf.Max(0f, diagnostics.reference_mp4_current_clip_duration_seconds);
            float endSeconds = startSeconds + durationSeconds;
            diagnostics.reference_mp4_current_clip_end_seconds = endSeconds;
            if (durationSeconds <= 0f)
            {
                diagnostics.reference_mp4_current_clip_sample_gap_seconds = 0f;
                return;
            }

            const float epsilonSeconds = 0.0001f;
            int count = 0;
            float firstSeconds = float.PositiveInfinity;
            float lastSeconds = float.NegativeInfinity;
            float sumBBoxHeight = 0f;
            float sumBBoxWidth = 0f;
            float sumBrightArea = 0f;
            float sumUpperLimbSpan = 0f;
            float sumLowerLimbSpan = 0f;
            int limbSpanSampleCount = 0;
            float maxBottomGap = float.NegativeInfinity;
            float minCenterX = float.PositiveInfinity;
            float maxCenterX = float.NegativeInfinity;
            var sampleSeconds = new List<float>();
            diagnostics.referenceMp4CurrentClipRows.Clear();
            foreach (ReferenceMp4FrameMetricRow row in metrics.rows)
            {
                if (row == null)
                {
                    continue;
                }

                float seconds = row.seconds;
                if (float.IsNaN(seconds) ||
                    seconds < startSeconds - epsilonSeconds ||
                    seconds > endSeconds + epsilonSeconds)
                {
                    continue;
                }

                float localSeconds = Mathf.Clamp(seconds - startSeconds, 0f, durationSeconds);
                count++;
                firstSeconds = Mathf.Min(firstSeconds, localSeconds);
                lastSeconds = Mathf.Max(lastSeconds, localSeconds);
                diagnostics.referenceMp4CurrentClipRows.Add(row);
                sampleSeconds.Add(localSeconds);
                sumBBoxHeight += row.bboxHeightRatio;
                sumBBoxWidth += row.bboxWidthRatio;
                sumBrightArea += row.brightAreaRatio;
                maxBottomGap = Mathf.Max(maxBottomGap, row.bottomGapRatio);
                minCenterX = Mathf.Min(minCenterX, row.centerXRatio);
                maxCenterX = Mathf.Max(maxCenterX, row.centerXRatio);
                string framePath = ResolveProjectRelativePath(row.framePath);
                if (YybScreenshotDiagnosticAnalyzer.TryAnalyzeCandidateScreenshotFrame(
                        framePath,
                        out var imageMetric,
                        out _) &&
                    IsFiniteMetric(imageMetric.UpperLimbSpanRatio) &&
                    IsFiniteMetric(imageMetric.LowerLimbSpanRatio))
                {
                    row.upperLimbSpanRatio = imageMetric.UpperLimbSpanRatio;
                    row.lowerLimbSpanRatio = imageMetric.LowerLimbSpanRatio;
                    row.silhouetteSpanProfile = imageMetric.SilhouetteSpanProfile;
                    row.silhouetteEndpointProfile = imageMetric.SilhouetteEndpointProfile;
                    row.imageSpaceKeypointProfile = imageMetric.ImageSpaceKeypointProfile;
                    row.hasNonHairBrightPixels = imageMetric.HasNonHairBrightPixels;
                    row.nonHairBBoxHeightRatio = imageMetric.NonHairBBoxHeightRatio;
                    row.nonHairBBoxWidthRatio = imageMetric.NonHairBBoxWidthRatio;
                    row.nonHairCenterXRatio = imageMetric.NonHairCenterX;
                    row.nonHairBottomGapRatio = imageMetric.NonHairBottomGapRatio;
                    row.nonHairImageSpaceKeypointProfile = imageMetric.NonHairImageSpaceKeypointProfile;
                    sumUpperLimbSpan += imageMetric.UpperLimbSpanRatio;
                    sumLowerLimbSpan += imageMetric.LowerLimbSpanRatio;
                    limbSpanSampleCount++;
                }
            }

            diagnostics.reference_mp4_current_clip_sample_count = count;
            diagnostics.reference_mp4_current_clip_sample_seconds = sampleSeconds.ToArray();
            if (count <= 0)
            {
                diagnostics.reference_mp4_current_clip_sample_gap_seconds = durationSeconds;
                return;
            }

            diagnostics.reference_mp4_current_clip_first_sample_seconds = firstSeconds;
            diagnostics.reference_mp4_current_clip_last_sample_seconds = lastSeconds;
            diagnostics.reference_mp4_current_clip_sample_coverage_ratio = Mathf.Clamp01(lastSeconds / durationSeconds);
            diagnostics.reference_mp4_current_clip_sample_gap_seconds = Mathf.Max(0f, durationSeconds - lastSeconds);
            diagnostics.reference_mp4_current_clip_avg_bbox_height_ratio = sumBBoxHeight / count;
            diagnostics.reference_mp4_current_clip_avg_bbox_width_ratio = sumBBoxWidth / count;
            diagnostics.reference_mp4_current_clip_center_x_range_ratio = maxCenterX - minCenterX;
            diagnostics.reference_mp4_current_clip_max_bottom_gap_ratio = maxBottomGap;
            diagnostics.reference_mp4_current_clip_avg_bright_area_ratio = sumBrightArea / count;
            if (limbSpanSampleCount > 0)
            {
                diagnostics.reference_mp4_current_clip_avg_upper_limb_span_ratio =
                    sumUpperLimbSpan / limbSpanSampleCount;
                diagnostics.reference_mp4_current_clip_avg_lower_limb_span_ratio =
                    sumLowerLimbSpan / limbSpanSampleCount;
            }
        }

        private static string ResolveCandidateFrameIndexPathForDiagnostics()
        {
            CaptureResult mainAuto = Results.FirstOrDefault(result =>
                result != null &&
                string.Equals(result.jobMode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(result.comparisonFrameIndexPath));
            if (mainAuto != null)
            {
                return mainAuto.comparisonFrameIndexPath;
            }

            CaptureResult fallback = Results.FirstOrDefault(result =>
                result != null &&
                IsMainSceneCandidateMode(result.jobMode) &&
                !string.IsNullOrWhiteSpace(result.comparisonFrameIndexPath));
            return fallback != null
                ? fallback.comparisonFrameIndexPath
                : string.Empty;
        }
        private static string ResolveProjectRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            {
                return relativePath ?? string.Empty;
            }

            string projectRoot = ResolveProjectRootForDiagnostics();
            return string.IsNullOrWhiteSpace(projectRoot)
                ? relativePath
                : Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ResolveProjectRootForDiagnostics()
        {
            if (!string.IsNullOrWhiteSpace(_projectRoot))
            {
                return _projectRoot;
            }

            string dataPath = Application.dataPath;
            DirectoryInfo projectRoot = string.IsNullOrWhiteSpace(dataPath)
                ? null
                : Directory.GetParent(dataPath);
            if (projectRoot == null)
            {
                throw new InvalidOperationException("Unity 프로젝트 루트를 확인할 수 없습니다.");
            }

            return projectRoot.FullName;
        }

        private static int ParseInvariantInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
        }

        private static float ParseInvariantFloat(string value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : float.NaN;
        }
        private static SummaryCandidateArtifactSelection BuildCandidateArtifactSelection(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries)
        {
            SummaryCandidateArtifactSelection selection = new SummaryCandidateArtifactSelection();
            if (frameQualitySummaries == null || frameQualitySummaries.Length == 0)
            {
                selection.selection_basis = "no frame_quality summary is available";
                return selection;
            }

            MotionComparisonFrameQualitySummary mainAutoIntegrated = frameQualitySummaries.FirstOrDefault(summary =>
                summary != null &&
                string.Equals(summary.frame_quality_evaluation_role, "main_auto_integrated_vertical_solve_metrics", StringComparison.Ordinal));
            MotionComparisonFrameQualitySummary raw = frameQualitySummaries.FirstOrDefault(summary =>
                summary != null &&
                string.Equals(summary.frame_quality_evaluation_role, "evaluation_candidate_metrics", StringComparison.Ordinal) &&
                IsMainAutoSummary(summary));
            if (raw == null)
            {
                raw = frameQualitySummaries.FirstOrDefault(summary =>
                    summary != null &&
                    string.Equals(summary.frame_quality_evaluation_role, "evaluation_candidate_metrics", StringComparison.Ordinal));
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
                string.Equals(summary.frame_quality_evaluation_role, "corrected_candidate_metrics", StringComparison.Ordinal) &&
                IsMainAutoSummary(summary));
            if (corrected == null)
            {
                corrected = frameQualitySummaries.FirstOrDefault(summary =>
                    summary != null &&
                    string.Equals(summary.frame_quality_evaluation_role, "corrected_candidate_metrics", StringComparison.Ordinal));
            }

            FillRawCandidateSelectionFields(selection, raw);
            FillCorrectedCandidateSelectionFields(selection, corrected);

            bool correctedPasses = corrected != null &&
                string.Equals(corrected.status, "pass", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(corrected.candidate_vmd_path) &&
                !string.IsNullOrWhiteSpace(corrected.candidate_metrics_csv);
            bool integratedPrimaryPasses = mainAutoIntegrated != null &&
                string.Equals(mainAutoIntegrated.status, "pass", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(mainAutoIntegrated.candidate_vmd_path) &&
                !string.IsNullOrWhiteSpace(mainAutoIntegrated.candidate_metrics_csv);
            if (integratedPrimaryPasses)
            {
                FillSelectedCandidateFields(selection, mainAutoIntegrated);
                selection.selected_candidate_output_role = "user_facing_export_artifact";
                selection.selected_candidate_preserves_raw_diagnostic = true;
                FillSelectedCandidateAcceptanceEvidence(selection, raw, mainAutoIntegrated, mainAutoIntegrated.vertical_solve_corrected_candidate_manifest_path);
                selection.selection_basis =
                    "primary Main_Auto export paths passed after bounded vertical solve integration; raw diagnostic artifacts remain preserved";
                return selection;
            }

            if (correctedPasses)
            {
                FillSelectedCandidateFields(selection, corrected);
                selection.selected_candidate_output_role = "user_facing_export_artifact";
                selection.selected_candidate_preserves_raw_diagnostic = raw != null;
                FillSelectedCandidateAcceptanceEvidence(selection, raw, corrected, raw != null ? raw.vertical_solve_corrected_candidate_manifest_path : string.Empty);
                selection.selection_basis = selection.selected_candidate_is_acceptance_artifact
                    ? "corrected candidate passed frame-quality gates and is selected for user-facing export; raw candidate remains recorded for diagnostics"
                    : "corrected candidate passed metric gates but did not produce a distinct export VMD; selected as diagnostic evidence while raw candidate remains recorded";
                return selection;
            }

            if (raw != null)
            {
                FillSelectedCandidateFields(selection, raw);
                bool rawPasses = string.Equals(raw.status, "pass", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(raw.candidate_vmd_path) &&
                    !string.IsNullOrWhiteSpace(raw.candidate_metrics_csv);
                if (rawPasses)
                {
                    selection.selected_candidate_output_role = "user_facing_export_artifact";
                    selection.selected_candidate_preserves_raw_diagnostic = false;
                    FillSelectedCandidateAcceptanceEvidence(selection, raw, raw, string.Empty);
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

        private static bool IsMainAutoSummary(MotionComparisonFrameQualitySummary summary)
        {
            return summary != null &&
                !string.IsNullOrWhiteSpace(summary.candidate_label) &&
                summary.candidate_label.IndexOf("Main_Auto", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void FillSelectedCandidateFields(
            SummaryCandidateArtifactSelection selection,
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

        private static void FillSelectedCandidateAcceptanceEvidence(
            SummaryCandidateArtifactSelection selection,
            MotionComparisonFrameQualitySummary raw,
            MotionComparisonFrameQualitySummary selected,
            string selectedManifestPath)
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
            bool selectedRawPrimary = IsRawCandidateRole(selected);
            if (selectedCorrectedArtifact)
            {
                EnsureCorrectedCandidateSelectionManifest(selection, raw);
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
                ? ResolveIntegratedPrimaryRawDiagnosticVmdPath(selection.selected_candidate_manifest_path)
                : string.Empty;
            bool differsFromRawSummary =
                raw != null &&
                !string.IsNullOrWhiteSpace(raw.candidate_vmd_path) &&
                !string.IsNullOrWhiteSpace(selection.selected_candidate_vmd_path) &&
                File.Exists(raw.candidate_vmd_path) &&
                selection.selected_candidate_vmd_exists &&
                !PathsReferToSameFile(raw.candidate_vmd_path, selection.selected_candidate_vmd_path) &&
                FilesDiffer(raw.candidate_vmd_path, selection.selected_candidate_vmd_path);
            bool differsFromIntegratedRawDiagnostic =
                selectedIntegratedPrimary &&
                !string.IsNullOrWhiteSpace(integratedRawDiagnosticVmdPath) &&
                !string.IsNullOrWhiteSpace(selection.selected_candidate_vmd_path) &&
                File.Exists(integratedRawDiagnosticVmdPath) &&
                selection.selected_candidate_vmd_exists &&
                !PathsReferToSameFile(integratedRawDiagnosticVmdPath, selection.selected_candidate_vmd_path) &&
                FilesDiffer(integratedRawDiagnosticVmdPath, selection.selected_candidate_vmd_path);
            selection.selected_candidate_differs_from_raw_vmd =
                differsFromRawSummary || differsFromIntegratedRawDiagnostic;
            selection.selected_candidate_differs_from_raw_metrics =
                raw != null &&
                !string.IsNullOrWhiteSpace(raw.candidate_metrics_csv) &&
                !string.IsNullOrWhiteSpace(selection.selected_candidate_metrics_csv) &&
                File.Exists(raw.candidate_metrics_csv) &&
                selection.selected_candidate_metrics_exists &&
                !PathsReferToSameFile(raw.candidate_metrics_csv, selection.selected_candidate_metrics_csv) &&
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
                string.Equals(selection.selected_candidate_output_role, "user_facing_export_artifact", StringComparison.Ordinal) &&
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

        private static bool IsRawCandidateRole(MotionComparisonFrameQualitySummary summary)
        {
            return summary != null &&
                (string.Equals(summary.frame_quality_evaluation_role, "raw_candidate_metrics", StringComparison.Ordinal) ||
                    string.Equals(summary.frame_quality_evaluation_role, "evaluation_candidate_metrics", StringComparison.Ordinal));
        }

        private static void EnsureCorrectedCandidateSelectionManifest(
            SummaryCandidateArtifactSelection selection,
            MotionComparisonFrameQualitySummary raw)
        {
            if (selection == null || raw == null)
            {
                return;
            }

            string manifestPath = ResolveSelectionArtifactPath(selection.selected_candidate_manifest_path, string.Empty);
            if (string.IsNullOrWhiteSpace(manifestPath) || File.Exists(manifestPath))
            {
                return;
            }

            string rawMetricsPath = ResolveSelectionArtifactPath(raw.candidate_metrics_csv, string.Empty);
            string rawVmdPath = ResolveSelectionArtifactPath(raw.candidate_vmd_path, string.Empty);
            string correctedMetricsPath = ResolveSelectionArtifactPath(selection.selected_candidate_metrics_csv, string.Empty);
            string correctedVmdPath = ResolveSelectionArtifactPath(selection.selected_candidate_vmd_path, string.Empty);
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

        private static string ResolveIntegratedPrimaryRawDiagnosticVmdPath(string manifestPath)
        {
            string absoluteManifestPath = ResolveSelectionArtifactPath(manifestPath, string.Empty);
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

                return ResolveSelectionArtifactPath(
                    manifest.raw_diagnostic_vmd_path,
                    Path.GetDirectoryName(absoluteManifestPath));
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string ResolveSelectionArtifactPath(string path, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            if (!string.IsNullOrWhiteSpace(_projectRoot))
            {
                return ToAbsoluteProjectPath(normalized);
            }

            return string.IsNullOrWhiteSpace(baseDirectory)
                ? normalized
                : Path.Combine(baseDirectory, normalized);
        }

        private static bool PathsReferToSameFile(string leftPath, string rightPath)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(leftPath),
                    Path.GetFullPath(rightPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
            }
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

        private static void FillRawCandidateSelectionFields(
            SummaryCandidateArtifactSelection selection,
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

        private static void FillCorrectedCandidateSelectionFields(
            SummaryCandidateArtifactSelection selection,
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
    }
}
#endif
