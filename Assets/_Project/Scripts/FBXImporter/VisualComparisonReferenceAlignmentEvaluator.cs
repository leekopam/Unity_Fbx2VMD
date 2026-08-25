namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonReferenceAlignmentEvaluator
    {
        private const int MinMatchedSampleCount = 5;
        private const float MaxSecondsGap = 0.1f;
        private const float MaxBoundingBoxHeightRatioDelta = 0.05f;
        private const float MaxBottomGapRatioDelta = 0.02f;
        private const float MaxSilhouetteProfileL1Delta = 0.15f;
        private const float MaxSilhouetteProfileBandDelta = 0.25f;
        private const float MaxSilhouetteLandmarkEndpointDelta = 0.30f;
        private const float EndpointPixelTolerance = 0.001f;

        internal static bool HasAlignedEvidence(
            VisualComparisonFrameRoleDiagnosticsData diagnostics)
        {
            if (diagnostics == null)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(diagnostics.candidate_screenshot_frame_metrics_error) &&
                string.IsNullOrWhiteSpace(diagnostics.reference_mp4_analysis_error) &&
                string.IsNullOrWhiteSpace(diagnostics.reference_mp4_frame_metrics_error) &&
                diagnostics.reference_mp4_current_clip_sample_count >= MinMatchedSampleCount &&
                diagnostics.candidate_vs_reference_time_matched_sample_count >= MinMatchedSampleCount &&
                diagnostics.candidate_screenshot_nonblank_frame_count >=
                    diagnostics.candidate_vs_reference_time_matched_sample_count &&
                IsFinite(diagnostics.candidate_vs_reference_time_matched_max_seconds_gap) &&
                diagnostics.candidate_vs_reference_time_matched_max_seconds_gap <= MaxSecondsGap &&
                IsFinite(diagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta) &&
                diagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta <=
                    MaxBoundingBoxHeightRatioDelta &&
                IsFinite(diagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta) &&
                diagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta <=
                    MaxBottomGapRatioDelta &&
                IsFinite(diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta) &&
                diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta <=
                    MaxSilhouetteProfileL1Delta &&
                IsFinite(diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta) &&
                diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta <=
                    MaxSilhouetteProfileBandDelta &&
                IsFinite(diagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta) &&
                diagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta <=
                    MaxSilhouetteLandmarkEndpointDelta + EndpointPixelTolerance;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
