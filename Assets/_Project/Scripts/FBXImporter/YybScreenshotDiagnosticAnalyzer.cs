#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using static Fbx2Vmd.FBXImporter.VisualComparisonFrameGeometryCalculator;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybScreenshotDiagnosticAnalyzer
    {
        private const string CandidateScreenshotFramingView = "front";
        private const float CandidateScreenshotBrightLuminanceThreshold = 0.08f;
        private const byte CandidateScreenshotOpaqueAlphaThreshold = 8;
        private const int ImageSpaceSilhouetteProfileBandCount = 4;
        private const float ReferenceAlignedVisualEvidenceMaxBBoxNormalizedImageSpaceKeypointL1Delta = 0.30f;
        private const float ReferenceAlignedVisualEvidenceEndpointPixelTolerance = 0.001f;

        internal static void AttachCandidateScreenshotFrameDiagnostics(
            VisualComparisonFrameRoleDiagnosticsData diagnostics,
            string candidateFrameIndexPath,
            string projectRoot)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.candidate_screenshot_frame_index_path = candidateFrameIndexPath ?? string.Empty;
            diagnostics.candidate_screenshot_frame_metrics_view = CandidateScreenshotFramingView;
            diagnostics.candidate_screenshot_frame_metrics_basis =
                "Computes lightweight bbox/framing metrics from Unity candidate screenshot index front-view PNGs and compares them to the ref MP4 bbox/framing metrics. Bbox width is used as an image-space horizontal limb span proxy.";
            diagnostics.candidate_screenshot_avg_bbox_height_ratio = float.NaN;
            diagnostics.candidate_screenshot_avg_bbox_width_ratio = float.NaN;
            diagnostics.candidate_screenshot_avg_upper_limb_span_ratio = float.NaN;
            diagnostics.candidate_screenshot_avg_lower_limb_span_ratio = float.NaN;
            diagnostics.candidate_screenshot_center_x_range_ratio = float.NaN;
            diagnostics.candidate_screenshot_max_bottom_gap_ratio = float.NaN;
            diagnostics.candidate_screenshot_max_top_gap_ratio = float.NaN;
            diagnostics.candidate_screenshot_avg_bright_area_ratio = float.NaN;
            diagnostics.candidate_screenshot_time_sample_count = 0;
            diagnostics.candidate_screenshot_first_sample_seconds = float.NaN;
            diagnostics.candidate_screenshot_last_sample_seconds = float.NaN;
            diagnostics.candidate_screenshot_sample_coverage_ratio = float.NaN;
            diagnostics.candidate_screenshot_sample_gap_seconds =
                Mathf.Max(0f, diagnostics.reference_mp4_current_clip_duration_seconds);
            diagnostics.candidate_screenshot_max_ref_sample_seconds_gap = float.NaN;
            diagnostics.candidate_screenshot_avg_ref_sample_seconds_gap = float.NaN;
            diagnostics.candidate_screenshot_ref_sample_gap_count = 0;
            diagnostics.candidate_screenshot_sample_seconds = Array.Empty<float>();
            diagnostics.candidate_screenshot_sample_timing_basis =
                "Derives candidate screenshot sample seconds from index.csv recorderFrame values and the candidate recorded frame count over requested duration.";
            diagnostics.candidate_vs_reference_avg_bbox_height_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_avg_bbox_width_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_center_x_range_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_max_bottom_gap_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_avg_bright_area_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_current_clip_avg_bbox_height_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_current_clip_avg_bbox_width_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_current_clip_center_x_range_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_current_clip_max_bottom_gap_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_current_clip_avg_bright_area_ratio_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_max_seconds_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_bbox_height_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_bbox_width_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_width_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_center_x_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_bright_area_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_limb_band_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_avg_upper_limb_span_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_upper_limb_span_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_lower_limb_span_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_lower_limb_span_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_silhouette_profile_band_count = 0;
            diagnostics.candidate_vs_reference_time_matched_silhouette_profile_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_avg_silhouette_profile_l1_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_silhouette_landmark_band_count = 0;
            diagnostics.candidate_vs_reference_time_matched_silhouette_landmark_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_avg_silhouette_landmark_endpoint_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_image_space_keypoint_count = 0;
            diagnostics.candidate_vs_reference_time_matched_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_count = 0;
            diagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_avg_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta_removed_by_bbox_normalization = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta_removed_by_bbox_normalization = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_count = 0;
            diagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_non_hair_avg_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_label = string.Empty;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_index = -1;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_seconds = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_seconds = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_recorder_frame = -1;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_x_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_y_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_x = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_y = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_x = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_y = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_bottom_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_top_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_bottom_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_top_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_bottom_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_top_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_bottom_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_top_gap = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_width_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_width_ratio_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_silhouette_profile_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_silhouette_profile_l1_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_max_silhouette_profile_l1_abs_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_max_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_bbox_normalized_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count = 0;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count = 0;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label = string.Empty;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count = 0;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count = 0;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count = 0;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label = string.Empty;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_index = -1;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_x_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_y_delta = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_candidate_x = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_candidate_y = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_reference_x = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_reference_y = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_required_x_reduction_to_threshold = float.NaN;
            diagnostics.candidate_vs_reference_time_matched_framing_metric_basis =
                "Compares each current-clip ref MP4 sample with the nearest candidate screenshot sample in seconds, then reports absolute bbox/framing deltas.";
            diagnostics.candidate_vs_reference_time_matched_image_space_limb_span_basis =
                "Uses front-view bbox width ratio as an image-space horizontal limb span proxy because tracked 2D keypoints are not yet available in the ref MP4 analysis.";
            diagnostics.candidate_vs_reference_time_matched_image_space_limb_band_basis =
                "Computes upper/lower silhouette band widths from the same ref MP4 and candidate PNG pixels, then compares time-matched samples as a keypoint-free image-space limb span proxy.";
            diagnostics.candidate_vs_reference_time_matched_silhouette_profile_basis =
                "Computes a 4-band bottom-to-top silhouette width profile from the same ref MP4 and candidate PNG pixels, then compares time-matched profile L1 deltas as a keypoint-free image-space limb/pose proxy.";
            diagnostics.candidate_vs_reference_time_matched_silhouette_landmark_basis =
                "Computes 4-band left/right silhouette endpoints from the same ref MP4 and candidate PNG pixels, then compares time-matched endpoint deltas as keypoint-free image-space silhouette landmarks.";
            diagnostics.candidate_vs_reference_time_matched_image_space_keypoint_basis =
                "Computes deterministic 2D silhouette keypoints from shared bright-pixel PNG analysis: bottom/top bbox centerline endpoints and 4-band left/right endpoints.";
            diagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_basis =
                "Computes bbox-normalized deterministic 2D silhouette keypoints after normalizing each sample into its own bbox coordinate space; bottom/top centers use each silhouette bbox centerline so sparse hair/skirt edge pixels do not dominate horizontal max residual attribution.";
            diagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_basis =
                "Computes a parallel bbox-normalized deterministic keypoint aggregate after excluding cyan/teal hair-like and dark teal hair-shadow silhouette pixels, so YYB twintail/hair motion cannot be mistaken for arm/leg endpoint residual.";
            diagnostics.candidate_vs_reference_time_matched_crop_safe_basis =
                "Aggregates only time-matched samples where neither the reference MP4 frame nor the candidate screenshot touches the frame edge; edge-touch samples are reported by the full metrics but excluded from crop-safe pose/shape aggregates.";
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_basis =
                "Aggregates bbox-normalized deterministic keypoints after excluding only keypoints directly affected by bottom/top frame-edge contact; this keypoint-local crop-safe view can retain middle-band pose/shape residuals from samples whose cap endpoints are cropped.";
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_basis =
                "Repeats keypoint-local crop-safe aggregation on the non-hair silhouette only, separating YYB hair exclusion from bottom/top frame-edge exclusion.";

            string resolvedIndexPath = ResolveProjectRelativePath(diagnostics.candidate_screenshot_frame_index_path, projectRoot);
            diagnostics.candidate_screenshot_frame_index_exists = File.Exists(resolvedIndexPath);
            if (!diagnostics.candidate_screenshot_frame_index_exists)
            {
                return;
            }

            try
            {
                CandidateScreenshotFrameMetrics metrics = BuildCandidateScreenshotFrameMetrics(
                    resolvedIndexPath,
                    projectRoot);
                diagnostics.candidate_screenshot_frame_metrics_sample_count = metrics.SampleCount;
                diagnostics.candidate_screenshot_nonblank_frame_count = metrics.NonblankCount;
                if (metrics.SampleCount > 0)
                {
                    diagnostics.candidate_screenshot_avg_bbox_height_ratio = metrics.AvgBBoxHeightRatio;
                    diagnostics.candidate_screenshot_avg_bbox_width_ratio = metrics.AvgBBoxWidthRatio;
                    diagnostics.candidate_screenshot_avg_upper_limb_span_ratio = metrics.AvgUpperLimbSpanRatio;
                    diagnostics.candidate_screenshot_avg_lower_limb_span_ratio = metrics.AvgLowerLimbSpanRatio;
                    diagnostics.candidate_screenshot_center_x_range_ratio = metrics.CenterXRangeRatio;
                    diagnostics.candidate_screenshot_max_bottom_gap_ratio = metrics.MaxBottomGapRatio;
                    diagnostics.candidate_screenshot_max_top_gap_ratio = metrics.MaxTopGapRatio;
                    diagnostics.candidate_screenshot_avg_bright_area_ratio = metrics.AvgBrightAreaRatio;
                    diagnostics.candidate_vs_reference_avg_bbox_height_ratio_delta =
                        metrics.AvgBBoxHeightRatio - diagnostics.reference_mp4_avg_bbox_height_ratio;
                    diagnostics.candidate_vs_reference_avg_bbox_width_ratio_delta =
                        metrics.AvgBBoxWidthRatio - diagnostics.reference_mp4_avg_bbox_width_ratio;
                    diagnostics.candidate_vs_reference_center_x_range_ratio_delta =
                        float.IsNaN(metrics.CenterXRangeRatio)
                            ? float.NaN
                            : metrics.CenterXRangeRatio - diagnostics.reference_mp4_center_x_range_ratio;
                    diagnostics.candidate_vs_reference_max_bottom_gap_ratio_delta =
                        metrics.MaxBottomGapRatio - diagnostics.reference_mp4_max_bottom_gap_ratio;
                    diagnostics.candidate_vs_reference_avg_bright_area_ratio_delta =
                        metrics.AvgBrightAreaRatio - diagnostics.reference_mp4_avg_bright_area_ratio;
                    diagnostics.candidate_vs_reference_current_clip_avg_bbox_height_ratio_delta =
                        metrics.AvgBBoxHeightRatio - diagnostics.reference_mp4_current_clip_avg_bbox_height_ratio;
                    diagnostics.candidate_vs_reference_current_clip_avg_bbox_width_ratio_delta =
                        metrics.AvgBBoxWidthRatio - diagnostics.reference_mp4_current_clip_avg_bbox_width_ratio;
                    diagnostics.candidate_vs_reference_current_clip_center_x_range_ratio_delta =
                        float.IsNaN(metrics.CenterXRangeRatio)
                            ? float.NaN
                            : metrics.CenterXRangeRatio - diagnostics.reference_mp4_current_clip_center_x_range_ratio;
                    diagnostics.candidate_vs_reference_current_clip_max_bottom_gap_ratio_delta =
                        metrics.MaxBottomGapRatio - diagnostics.reference_mp4_current_clip_max_bottom_gap_ratio;
                    diagnostics.candidate_vs_reference_current_clip_avg_bright_area_ratio_delta =
                        metrics.AvgBrightAreaRatio - diagnostics.reference_mp4_current_clip_avg_bright_area_ratio;
                }

                AttachCandidateScreenshotTimingDiagnostics(diagnostics, metrics);

                if (!string.IsNullOrWhiteSpace(metrics.Error))
                {
                    diagnostics.candidate_screenshot_frame_metrics_error = metrics.Error;
                }
            }
            catch (Exception ex)
            {
                diagnostics.candidate_screenshot_frame_metrics_error = ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static void AttachCandidateScreenshotTimingDiagnostics(
            VisualComparisonFrameRoleDiagnosticsData diagnostics,
            CandidateScreenshotFrameMetrics metrics)
        {
            if (diagnostics == null || metrics == null || metrics.Samples.Count <= 0)
            {
                return;
            }

            float durationSeconds = Mathf.Max(0f, diagnostics.reference_mp4_current_clip_duration_seconds);
            if (durationSeconds <= 0f || diagnostics.candidate_recorded_frame_count <= 0)
            {
                return;
            }

            float framesPerSecond = diagnostics.candidate_recorded_frame_count / durationSeconds;
            if (framesPerSecond <= 0f || float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond))
            {
                return;
            }

            var seconds = new List<float>();
            var timedSamples = new List<VisualComparisonCandidateFrameSample>();
            foreach (VisualComparisonCandidateFrameSample sample in metrics.Samples)
            {
                if (sample == null || sample.RecorderFrame < 0)
                {
                    continue;
                }

                sample.Seconds = Mathf.Clamp(sample.RecorderFrame / framesPerSecond, 0f, durationSeconds);
                seconds.Add(sample.Seconds);
                timedSamples.Add(sample);
            }

            if (seconds.Count <= 0)
            {
                return;
            }

            seconds.Sort();
            diagnostics.candidate_screenshot_time_sample_count = seconds.Count;
            diagnostics.candidate_screenshot_sample_seconds = seconds.ToArray();
            diagnostics.candidate_screenshot_first_sample_seconds = seconds[0];
            diagnostics.candidate_screenshot_last_sample_seconds = seconds[seconds.Count - 1];
            diagnostics.candidate_screenshot_sample_coverage_ratio =
                Mathf.Clamp01(diagnostics.candidate_screenshot_last_sample_seconds / durationSeconds);
            diagnostics.candidate_screenshot_sample_gap_seconds =
                Mathf.Max(0f, durationSeconds - diagnostics.candidate_screenshot_last_sample_seconds);

            float[] referenceSeconds = diagnostics.reference_mp4_current_clip_sample_seconds;
            if (referenceSeconds == null || referenceSeconds.Length <= 0)
            {
                return;
            }

            int gapCount = 0;
            float gapSum = 0f;
            float maxGap = 0f;
            foreach (float referenceSecond in referenceSeconds)
            {
                if (float.IsNaN(referenceSecond))
                {
                    continue;
                }

                float nearestGap = float.PositiveInfinity;
                foreach (float candidateSecond in seconds)
                {
                    nearestGap = Mathf.Min(nearestGap, Mathf.Abs(candidateSecond - referenceSecond));
                }

                if (float.IsInfinity(nearestGap))
                {
                    continue;
                }

                gapCount++;
                gapSum += nearestGap;
                maxGap = Mathf.Max(maxGap, nearestGap);
            }

            diagnostics.candidate_screenshot_ref_sample_gap_count = gapCount;
            if (gapCount > 0)
            {
                diagnostics.candidate_screenshot_max_ref_sample_seconds_gap = maxGap;
                diagnostics.candidate_screenshot_avg_ref_sample_seconds_gap = gapSum / gapCount;
            }

            AttachCandidateScreenshotTimeMatchedFramingDiagnostics(diagnostics, timedSamples);
        }

        private static void AttachCandidateScreenshotTimeMatchedFramingDiagnostics(
            VisualComparisonFrameRoleDiagnosticsData diagnostics,
            List<VisualComparisonCandidateFrameSample> candidateSamples)
        {
            if (diagnostics == null ||
                candidateSamples == null ||
                candidateSamples.Count <= 0 ||
                diagnostics.referenceMp4CurrentClipRows.Count <= 0)
            {
                return;
            }

            int count = 0;
            float maxSecondsGap = 0f;
            float sumBBoxHeightDelta = 0f;
            float maxBBoxHeightDelta = 0f;
            float sumBBoxWidthDelta = 0f;
            float maxBBoxWidthDelta = 0f;
            float sumCenterXDelta = 0f;
            float maxBottomGapDelta = 0f;
            float sumBrightAreaDelta = 0f;
            int limbBandCount = 0;
            float sumUpperLimbSpanDelta = 0f;
            float maxUpperLimbSpanDelta = 0f;
            float sumLowerLimbSpanDelta = 0f;
            float maxLowerLimbSpanDelta = 0f;
            int silhouetteProfileBandCount = 0;
            int silhouetteProfileCount = 0;
            float sumSilhouetteProfileL1Delta = 0f;
            float maxSilhouetteProfileL1Delta = 0f;
            float maxSilhouetteProfileBandDelta = 0f;
            int silhouetteLandmarkBandCount = 0;
            int silhouetteLandmarkCount = 0;
            float sumSilhouetteLandmarkEndpointDelta = 0f;
            float maxSilhouetteLandmarkEndpointDelta = 0f;
            int imageSpaceKeypointCount = 0;
            int imageSpaceKeypointSampleCount = 0;
            float sumImageSpaceKeypointL1Delta = 0f;
            float maxImageSpaceKeypointL1Delta = 0f;
            int bboxNormalizedImageSpaceKeypointCount = 0;
            int bboxNormalizedImageSpaceKeypointSampleCount = 0;
            float sumBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            float maxBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            int nonHairBBoxNormalizedImageSpaceKeypointCount = 0;
            int nonHairBBoxNormalizedImageSpaceKeypointSampleCount = 0;
            float sumNonHairBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            float maxNonHairBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            string maxNonHairBBoxNormalizedImageSpaceKeypointLabel = string.Empty;
            int maxNonHairBBoxNormalizedImageSpaceKeypointIndex = -1;
            int cropSafeSampleCount = 0;
            float sumCropSafeBBoxWidthDelta = 0f;
            float maxCropSafeBBoxWidthDelta = 0f;
            int cropSafeSilhouetteProfileCount = 0;
            float sumCropSafeSilhouetteProfileL1Delta = 0f;
            float maxCropSafeSilhouetteProfileL1Delta = 0f;
            int cropSafeImageSpaceKeypointSampleCount = 0;
            float sumCropSafeImageSpaceKeypointL1Delta = 0f;
            float maxCropSafeImageSpaceKeypointL1Delta = 0f;
            int cropSafeBBoxNormalizedImageSpaceKeypointSampleCount = 0;
            float sumCropSafeBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            float maxCropSafeBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            int keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount = 0;
            int keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount = 0;
            int keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointExcludedCount = 0;
            float sumKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            float maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            string maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointLabel = string.Empty;
            int nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount = 0;
            int nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount = 0;
            int nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointExcludedCount = 0;
            float sumNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            float maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta = 0f;
            string maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointLabel = string.Empty;
            int maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointIndex = -1;
            float maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointXDelta = float.NaN;
            float maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointYDelta = float.NaN;
            float maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCandidateX = float.NaN;
            float maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCandidateY = float.NaN;
            float maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointReferenceX = float.NaN;
            float maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointReferenceY = float.NaN;
            VisualComparisonTimeMatchedFramePair[] timeMatchedPairs =
                VisualComparisonTimeMatchedFramePairBuilder.Build(
                    diagnostics.referenceMp4CurrentClipRows,
                    candidateSamples,
                    diagnostics.reference_mp4_current_clip_start_seconds,
                    diagnostics.reference_mp4_current_clip_duration_seconds);
            foreach (VisualComparisonTimeMatchedFramePair pair in timeMatchedPairs)
            {
                ReferenceMp4FrameMetricRow referenceRow = pair.ReferenceRow;
                VisualComparisonCandidateFrameSample nearestSample = pair.CandidateSample;
                float nearestGap = pair.SecondsGap;
                VisualComparisonCandidateFrameMetric candidateMetric = nearestSample.Metric;
                float bboxHeightDelta = Mathf.Abs(candidateMetric.BBoxHeightRatio - referenceRow.bboxHeightRatio);
                float bboxWidthDelta = Mathf.Abs(candidateMetric.BBoxWidthRatio - referenceRow.bboxWidthRatio);
                float centerXDelta = Mathf.Abs(candidateMetric.CenterX - referenceRow.centerXRatio);
                float bottomGapDelta = Mathf.Abs(candidateMetric.BottomGapRatio - referenceRow.bottomGapRatio);
                float brightAreaDelta = Mathf.Abs(candidateMetric.BrightAreaRatio - referenceRow.brightAreaRatio);
                float referenceTopGapRatio = pair.ReferenceTopGapRatio;
                bool referenceTouchesFrameEdge = pair.ReferenceTouchesFrameEdge;
                bool candidateTouchesFrameEdge = pair.CandidateTouchesFrameEdge;
                bool cropSafeSample = pair.IsCropSafe;
                if (cropSafeSample)
                {
                    cropSafeSampleCount++;
                    sumCropSafeBBoxWidthDelta += bboxWidthDelta;
                    maxCropSafeBBoxWidthDelta = Mathf.Max(maxCropSafeBBoxWidthDelta, bboxWidthDelta);
                }

                if (IsFiniteMetric(candidateMetric.UpperLimbSpanRatio) &&
                    IsFiniteMetric(candidateMetric.LowerLimbSpanRatio) &&
                    IsFiniteMetric(referenceRow.upperLimbSpanRatio) &&
                    IsFiniteMetric(referenceRow.lowerLimbSpanRatio))
                {
                    float upperLimbSpanDelta =
                        Mathf.Abs(candidateMetric.UpperLimbSpanRatio - referenceRow.upperLimbSpanRatio);
                    float lowerLimbSpanDelta =
                        Mathf.Abs(candidateMetric.LowerLimbSpanRatio - referenceRow.lowerLimbSpanRatio);
                    limbBandCount++;
                    sumUpperLimbSpanDelta += upperLimbSpanDelta;
                    maxUpperLimbSpanDelta = Mathf.Max(maxUpperLimbSpanDelta, upperLimbSpanDelta);
                    sumLowerLimbSpanDelta += lowerLimbSpanDelta;
                    maxLowerLimbSpanDelta = Mathf.Max(maxLowerLimbSpanDelta, lowerLimbSpanDelta);
                }
                if (VisualComparisonProfileDeltaCalculator.TryCalculate(
                    candidateMetric.SilhouetteSpanProfile,
                    referenceRow.silhouetteSpanProfile,
                    out VisualComparisonProfileDelta silhouetteProfileDelta))
                {
                    silhouetteProfileBandCount = Mathf.Max(
                        silhouetteProfileBandCount,
                        silhouetteProfileDelta.ComparedValueCount);
                    silhouetteProfileCount++;
                    sumSilhouetteProfileL1Delta += silhouetteProfileDelta.MeanAbsoluteDelta;
                    maxSilhouetteProfileL1Delta = Mathf.Max(
                        maxSilhouetteProfileL1Delta,
                        silhouetteProfileDelta.MeanAbsoluteDelta);
                    maxSilhouetteProfileBandDelta = Mathf.Max(
                        maxSilhouetteProfileBandDelta,
                        silhouetteProfileDelta.MaxAbsoluteDelta);
                    if (cropSafeSample)
                    {
                        cropSafeSilhouetteProfileCount++;
                        sumCropSafeSilhouetteProfileL1Delta += silhouetteProfileDelta.MeanAbsoluteDelta;
                        maxCropSafeSilhouetteProfileL1Delta =
                            Mathf.Max(
                                maxCropSafeSilhouetteProfileL1Delta,
                                silhouetteProfileDelta.MeanAbsoluteDelta);
                    }
                }
                if (VisualComparisonProfileDeltaCalculator.TryCalculatePaired(
                    candidateMetric.SilhouetteEndpointProfile,
                    referenceRow.silhouetteEndpointProfile,
                    out VisualComparisonProfileDelta silhouetteEndpointDelta))
                {
                    silhouetteLandmarkBandCount = Mathf.Max(
                        silhouetteLandmarkBandCount,
                        silhouetteEndpointDelta.ComparedValueCount / 2);
                    silhouetteLandmarkCount++;
                    sumSilhouetteLandmarkEndpointDelta += silhouetteEndpointDelta.MeanAbsoluteDelta;
                    maxSilhouetteLandmarkEndpointDelta =
                        Mathf.Max(
                            maxSilhouetteLandmarkEndpointDelta,
                            silhouetteEndpointDelta.MaxAbsoluteDelta);
                }
                if (VisualComparisonKeypointDeltaCalculator.TryCalculate(
                    candidateMetric.ImageSpaceKeypointProfile,
                    referenceRow.imageSpaceKeypointProfile,
                    out VisualComparisonKeypointDelta keypointDelta))
                {
                    imageSpaceKeypointCount = Mathf.Max(
                        imageSpaceKeypointCount,
                        keypointDelta.ComparedKeypointCount);
                    imageSpaceKeypointSampleCount++;
                    sumImageSpaceKeypointL1Delta += keypointDelta.MeanL1Delta;
                    maxImageSpaceKeypointL1Delta =
                        Mathf.Max(maxImageSpaceKeypointL1Delta, keypointDelta.MaxL1Delta);
                    if (cropSafeSample)
                    {
                        cropSafeImageSpaceKeypointSampleCount++;
                        sumCropSafeImageSpaceKeypointL1Delta += keypointDelta.MeanL1Delta;
                        maxCropSafeImageSpaceKeypointL1Delta =
                            Mathf.Max(
                                maxCropSafeImageSpaceKeypointL1Delta,
                                keypointDelta.MaxL1Delta);
                    }
                }
                if (VisualComparisonKeypointDeltaCalculator.TryCalculateBBoxNormalized(
                    candidateMetric.ImageSpaceKeypointProfile,
                    candidateMetric.CenterX,
                    candidateMetric.BBoxWidthRatio,
                    candidateMetric.BottomGapRatio,
                    candidateMetric.BBoxHeightRatio,
                    referenceRow.imageSpaceKeypointProfile,
                    referenceRow.centerXRatio,
                    referenceRow.bboxWidthRatio,
                    referenceRow.bottomGapRatio,
                    referenceRow.bboxHeightRatio,
                    out VisualComparisonBBoxNormalizedKeypointDelta bboxNormalizedKeypointDelta))
                {
                    bboxNormalizedImageSpaceKeypointCount =
                        Mathf.Max(
                            bboxNormalizedImageSpaceKeypointCount,
                            bboxNormalizedKeypointDelta.ComparedKeypointCount);
                    bboxNormalizedImageSpaceKeypointSampleCount++;
                    sumBBoxNormalizedImageSpaceKeypointL1Delta += bboxNormalizedKeypointDelta.MeanL1Delta;
                    if (cropSafeSample)
                    {
                        cropSafeBBoxNormalizedImageSpaceKeypointSampleCount++;
                        sumCropSafeBBoxNormalizedImageSpaceKeypointL1Delta +=
                            bboxNormalizedKeypointDelta.MeanL1Delta;
                        maxCropSafeBBoxNormalizedImageSpaceKeypointL1Delta =
                            Mathf.Max(
                                maxCropSafeBBoxNormalizedImageSpaceKeypointL1Delta,
                                bboxNormalizedKeypointDelta.MaxL1Delta);
                    }
                    if (VisualComparisonKeypointDeltaCalculator.TryCalculateCropSafeBBoxNormalized(
                        candidateMetric.ImageSpaceKeypointProfile,
                        candidateMetric.CenterX,
                        candidateMetric.BBoxWidthRatio,
                        candidateMetric.BottomGapRatio,
                        candidateMetric.BBoxHeightRatio,
                        candidateMetric.TopGapRatio,
                        referenceRow.imageSpaceKeypointProfile,
                        referenceRow.centerXRatio,
                        referenceRow.bboxWidthRatio,
                        referenceRow.bottomGapRatio,
                        referenceRow.bboxHeightRatio,
                        referenceTopGapRatio,
                        ReferenceAlignedVisualEvidenceEndpointPixelTolerance,
                        out VisualComparisonBBoxNormalizedKeypointDelta keypointLocalCropSafeDelta))
                    {
                        keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount =
                            Mathf.Max(
                                keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount,
                                keypointLocalCropSafeDelta.ComparedKeypointCount);
                        keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount++;
                        keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointExcludedCount +=
                            keypointLocalCropSafeDelta.ExcludedKeypointCount;
                        sumKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta +=
                            keypointLocalCropSafeDelta.MeanL1Delta;
                        if (keypointLocalCropSafeDelta.MaxL1Delta >
                            maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta)
                        {
                            maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta =
                                keypointLocalCropSafeDelta.MaxL1Delta;
                            maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointLabel =
                                ResolveImageSpaceKeypointLabel(keypointLocalCropSafeDelta.MaxKeypointIndex);
                        }
                    }

                    if (bboxNormalizedKeypointDelta.MaxL1Delta > maxBBoxNormalizedImageSpaceKeypointL1Delta)
                    {
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_index =
                            bboxNormalizedKeypointDelta.MaxKeypointIndex;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label =
                            ResolveImageSpaceKeypointLabel(bboxNormalizedKeypointDelta.MaxKeypointIndex);
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_seconds =
                            referenceRow.seconds;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_seconds =
                            nearestSample.Seconds;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_recorder_frame =
                            nearestSample.RecorderFrame;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_x_delta =
                            bboxNormalizedKeypointDelta.MaxXDelta;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_y_delta =
                            bboxNormalizedKeypointDelta.MaxYDelta;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_x =
                            bboxNormalizedKeypointDelta.MaxCandidateX;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_y =
                            bboxNormalizedKeypointDelta.MaxCandidateY;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_x =
                            bboxNormalizedKeypointDelta.MaxReferenceX;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_y =
                            bboxNormalizedKeypointDelta.MaxReferenceY;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_bottom_gap =
                            referenceRow.bottomGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_top_gap =
                            referenceTopGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_bottom_gap =
                            candidateMetric.BottomGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_top_gap =
                            candidateMetric.TopGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_touches_frame_edge =
                            referenceTouchesFrameEdge;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_touches_frame_edge =
                            candidateTouchesFrameEdge;
                    }
                    maxBBoxNormalizedImageSpaceKeypointL1Delta =
                        Mathf.Max(
                            maxBBoxNormalizedImageSpaceKeypointL1Delta,
                            bboxNormalizedKeypointDelta.MaxL1Delta);
                }

                if (candidateMetric.HasNonHairBrightPixels &&
                    referenceRow.hasNonHairBrightPixels &&
                    VisualComparisonKeypointDeltaCalculator.TryCalculateBBoxNormalized(
                        candidateMetric.NonHairImageSpaceKeypointProfile,
                        candidateMetric.NonHairCenterX,
                        candidateMetric.NonHairBBoxWidthRatio,
                        candidateMetric.NonHairBottomGapRatio,
                        candidateMetric.NonHairBBoxHeightRatio,
                        referenceRow.nonHairImageSpaceKeypointProfile,
                        referenceRow.nonHairCenterXRatio,
                        referenceRow.nonHairBBoxWidthRatio,
                        referenceRow.nonHairBottomGapRatio,
                        referenceRow.nonHairBBoxHeightRatio,
                        out VisualComparisonBBoxNormalizedKeypointDelta nonHairBBoxNormalizedKeypointDelta))
                {
                    nonHairBBoxNormalizedImageSpaceKeypointCount =
                        Mathf.Max(
                            nonHairBBoxNormalizedImageSpaceKeypointCount,
                            nonHairBBoxNormalizedKeypointDelta.ComparedKeypointCount);
                    nonHairBBoxNormalizedImageSpaceKeypointSampleCount++;
                    sumNonHairBBoxNormalizedImageSpaceKeypointL1Delta +=
                        nonHairBBoxNormalizedKeypointDelta.MeanL1Delta;
                    if (nonHairBBoxNormalizedKeypointDelta.MaxL1Delta >
                        maxNonHairBBoxNormalizedImageSpaceKeypointL1Delta)
                    {
                        maxNonHairBBoxNormalizedImageSpaceKeypointL1Delta =
                            nonHairBBoxNormalizedKeypointDelta.MaxL1Delta;
                        maxNonHairBBoxNormalizedImageSpaceKeypointIndex =
                            nonHairBBoxNormalizedKeypointDelta.MaxKeypointIndex;
                        maxNonHairBBoxNormalizedImageSpaceKeypointLabel =
                            ResolveImageSpaceKeypointLabel(nonHairBBoxNormalizedKeypointDelta.MaxKeypointIndex);

                        float referenceNonHairTopGapRatio = ResolveFrameTopGapRatio(
                            referenceRow.nonHairBottomGapRatio,
                            referenceRow.nonHairBBoxHeightRatio);
                        float candidateNonHairTopGapRatio = ResolveFrameTopGapRatio(
                            candidateMetric.NonHairBottomGapRatio,
                            candidateMetric.NonHairBBoxHeightRatio);

                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_seconds =
                            referenceRow.seconds;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_seconds =
                            nearestSample.Seconds;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_recorder_frame =
                            nearestSample.RecorderFrame;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_x_delta =
                            nonHairBBoxNormalizedKeypointDelta.MaxXDelta;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_y_delta =
                            nonHairBBoxNormalizedKeypointDelta.MaxYDelta;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_x =
                            nonHairBBoxNormalizedKeypointDelta.MaxCandidateX;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_y =
                            nonHairBBoxNormalizedKeypointDelta.MaxCandidateY;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_x =
                            nonHairBBoxNormalizedKeypointDelta.MaxReferenceX;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_y =
                            nonHairBBoxNormalizedKeypointDelta.MaxReferenceY;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_bottom_gap =
                            referenceRow.nonHairBottomGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_top_gap =
                            referenceNonHairTopGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_bottom_gap =
                            candidateMetric.NonHairBottomGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_top_gap =
                            candidateNonHairTopGapRatio;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_touches_frame_edge =
                            IsFrameEdgeTouched(referenceRow.nonHairBottomGapRatio, referenceNonHairTopGapRatio);
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_touches_frame_edge =
                            IsFrameEdgeTouched(candidateMetric.NonHairBottomGapRatio, candidateNonHairTopGapRatio);
                    }

                    if (VisualComparisonKeypointDeltaCalculator.TryCalculateCropSafeBBoxNormalized(
                        candidateMetric.NonHairImageSpaceKeypointProfile,
                        candidateMetric.NonHairCenterX,
                        candidateMetric.NonHairBBoxWidthRatio,
                        candidateMetric.NonHairBottomGapRatio,
                        candidateMetric.NonHairBBoxHeightRatio,
                        ResolveFrameTopGapRatio(
                            candidateMetric.NonHairBottomGapRatio,
                            candidateMetric.NonHairBBoxHeightRatio),
                        referenceRow.nonHairImageSpaceKeypointProfile,
                        referenceRow.nonHairCenterXRatio,
                        referenceRow.nonHairBBoxWidthRatio,
                        referenceRow.nonHairBottomGapRatio,
                        referenceRow.nonHairBBoxHeightRatio,
                        ResolveFrameTopGapRatio(
                            referenceRow.nonHairBottomGapRatio,
                            referenceRow.nonHairBBoxHeightRatio),
                        ReferenceAlignedVisualEvidenceEndpointPixelTolerance,
                        out VisualComparisonBBoxNormalizedKeypointDelta nonHairKeypointLocalCropSafeDelta))
                    {
                        nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount =
                            Mathf.Max(
                                nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount,
                                nonHairKeypointLocalCropSafeDelta.ComparedKeypointCount);
                        nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount++;
                        nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointExcludedCount +=
                            nonHairKeypointLocalCropSafeDelta.ExcludedKeypointCount;
                        sumNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta +=
                            nonHairKeypointLocalCropSafeDelta.MeanL1Delta;
                        if (nonHairKeypointLocalCropSafeDelta.MaxL1Delta >
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta)
                        {
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta =
                                nonHairKeypointLocalCropSafeDelta.MaxL1Delta;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointLabel =
                                ResolveImageSpaceKeypointLabel(nonHairKeypointLocalCropSafeDelta.MaxKeypointIndex);
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointIndex =
                                nonHairKeypointLocalCropSafeDelta.MaxKeypointIndex;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointXDelta =
                                nonHairKeypointLocalCropSafeDelta.MaxXDelta;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointYDelta =
                                nonHairKeypointLocalCropSafeDelta.MaxYDelta;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCandidateX =
                                nonHairKeypointLocalCropSafeDelta.MaxCandidateX;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCandidateY =
                                nonHairKeypointLocalCropSafeDelta.MaxCandidateY;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointReferenceX =
                                nonHairKeypointLocalCropSafeDelta.MaxReferenceX;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointReferenceY =
                                nonHairKeypointLocalCropSafeDelta.MaxReferenceY;
                        }
                    }
                }

                count++;
                maxSecondsGap = Mathf.Max(maxSecondsGap, nearestGap);
                sumBBoxHeightDelta += bboxHeightDelta;
                maxBBoxHeightDelta = Mathf.Max(maxBBoxHeightDelta, bboxHeightDelta);
                sumBBoxWidthDelta += bboxWidthDelta;
                maxBBoxWidthDelta = Mathf.Max(maxBBoxWidthDelta, bboxWidthDelta);
                sumCenterXDelta += centerXDelta;
                maxBottomGapDelta = Mathf.Max(maxBottomGapDelta, bottomGapDelta);
                sumBrightAreaDelta += brightAreaDelta;
            }

            diagnostics.candidate_vs_reference_time_matched_sample_count = count;
            if (count <= 0)
            {
                return;
            }

            diagnostics.candidate_vs_reference_time_matched_max_seconds_gap = maxSecondsGap;
            diagnostics.candidate_vs_reference_time_matched_avg_bbox_height_ratio_abs_delta =
                sumBBoxHeightDelta / count;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta =
                maxBBoxHeightDelta;
            diagnostics.candidate_vs_reference_time_matched_avg_bbox_width_ratio_abs_delta =
                sumBBoxWidthDelta / count;
            diagnostics.candidate_vs_reference_time_matched_max_bbox_width_ratio_abs_delta =
                maxBBoxWidthDelta;
            diagnostics.candidate_vs_reference_time_matched_avg_center_x_ratio_abs_delta =
                sumCenterXDelta / count;
            diagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta =
                maxBottomGapDelta;
            diagnostics.candidate_vs_reference_time_matched_avg_bright_area_ratio_abs_delta =
                sumBrightAreaDelta / count;
            diagnostics.candidate_vs_reference_time_matched_limb_band_sample_count = limbBandCount;
            if (limbBandCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_avg_upper_limb_span_ratio_abs_delta =
                    sumUpperLimbSpanDelta / limbBandCount;
                diagnostics.candidate_vs_reference_time_matched_max_upper_limb_span_ratio_abs_delta =
                    maxUpperLimbSpanDelta;
                diagnostics.candidate_vs_reference_time_matched_avg_lower_limb_span_ratio_abs_delta =
                    sumLowerLimbSpanDelta / limbBandCount;
                diagnostics.candidate_vs_reference_time_matched_max_lower_limb_span_ratio_abs_delta =
                    maxLowerLimbSpanDelta;
            }
            diagnostics.candidate_vs_reference_time_matched_silhouette_profile_band_count =
                silhouetteProfileBandCount;
            diagnostics.candidate_vs_reference_time_matched_silhouette_profile_sample_count =
                silhouetteProfileCount;
            if (silhouetteProfileCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_avg_silhouette_profile_l1_abs_delta =
                    sumSilhouetteProfileL1Delta / silhouetteProfileCount;
                diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta =
                    maxSilhouetteProfileL1Delta;
                diagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta =
                    maxSilhouetteProfileBandDelta;
            }
            diagnostics.candidate_vs_reference_time_matched_silhouette_landmark_band_count =
                silhouetteLandmarkBandCount;
            diagnostics.candidate_vs_reference_time_matched_silhouette_landmark_sample_count =
                silhouetteLandmarkCount;
            if (silhouetteLandmarkCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_avg_silhouette_landmark_endpoint_abs_delta =
                    sumSilhouetteLandmarkEndpointDelta / silhouetteLandmarkCount;
                diagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta =
                    maxSilhouetteLandmarkEndpointDelta;
            }
            diagnostics.candidate_vs_reference_time_matched_image_space_keypoint_count =
                imageSpaceKeypointCount;
            diagnostics.candidate_vs_reference_time_matched_image_space_keypoint_sample_count =
                imageSpaceKeypointSampleCount;
            if (imageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta =
                    sumImageSpaceKeypointL1Delta / imageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta =
                    maxImageSpaceKeypointL1Delta;
            }
            diagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_count =
                bboxNormalizedImageSpaceKeypointCount;
            diagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_sample_count =
                bboxNormalizedImageSpaceKeypointSampleCount;
            if (bboxNormalizedImageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_avg_bbox_normalized_image_space_keypoint_l1_delta =
                    sumBBoxNormalizedImageSpaceKeypointL1Delta / bboxNormalizedImageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta =
                    maxBBoxNormalizedImageSpaceKeypointL1Delta;
                if (imageSpaceKeypointSampleCount > 0)
                {
                    diagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta_removed_by_bbox_normalization =
                        diagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta -
                        diagnostics.candidate_vs_reference_time_matched_avg_bbox_normalized_image_space_keypoint_l1_delta;
                    diagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta_removed_by_bbox_normalization =
                        diagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta -
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta;
                }
            }

            diagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_count =
                nonHairBBoxNormalizedImageSpaceKeypointCount;
            diagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_sample_count =
                nonHairBBoxNormalizedImageSpaceKeypointSampleCount;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_label =
                maxNonHairBBoxNormalizedImageSpaceKeypointLabel ?? string.Empty;
            diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_index =
                maxNonHairBBoxNormalizedImageSpaceKeypointIndex;
            if (nonHairBBoxNormalizedImageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_non_hair_avg_bbox_normalized_image_space_keypoint_l1_delta =
                    sumNonHairBBoxNormalizedImageSpaceKeypointL1Delta /
                    nonHairBBoxNormalizedImageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta =
                    maxNonHairBBoxNormalizedImageSpaceKeypointL1Delta;
            }

            diagnostics.candidate_vs_reference_time_matched_crop_safe_sample_count = cropSafeSampleCount;
            if (cropSafeSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_width_ratio_abs_delta =
                    sumCropSafeBBoxWidthDelta / cropSafeSampleCount;
                diagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_width_ratio_abs_delta =
                    maxCropSafeBBoxWidthDelta;
            }

            diagnostics.candidate_vs_reference_time_matched_crop_safe_silhouette_profile_sample_count =
                cropSafeSilhouetteProfileCount;
            if (cropSafeSilhouetteProfileCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_silhouette_profile_l1_abs_delta =
                    sumCropSafeSilhouetteProfileL1Delta / cropSafeSilhouetteProfileCount;
                diagnostics.candidate_vs_reference_time_matched_crop_safe_max_silhouette_profile_l1_abs_delta =
                    maxCropSafeSilhouetteProfileL1Delta;
            }

            diagnostics.candidate_vs_reference_time_matched_crop_safe_image_space_keypoint_sample_count =
                cropSafeImageSpaceKeypointSampleCount;
            if (cropSafeImageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_image_space_keypoint_l1_delta =
                    sumCropSafeImageSpaceKeypointL1Delta / cropSafeImageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_crop_safe_max_image_space_keypoint_l1_delta =
                    maxCropSafeImageSpaceKeypointL1Delta;
            }

            diagnostics.candidate_vs_reference_time_matched_crop_safe_bbox_normalized_image_space_keypoint_sample_count =
                cropSafeBBoxNormalizedImageSpaceKeypointSampleCount;
            if (cropSafeBBoxNormalizedImageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta =
                    sumCropSafeBBoxNormalizedImageSpaceKeypointL1Delta /
                    cropSafeBBoxNormalizedImageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta =
                    maxCropSafeBBoxNormalizedImageSpaceKeypointL1Delta;
            }
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count =
                keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count =
                keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count =
                keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointExcludedCount;
            diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label =
                maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointLabel ?? string.Empty;
            if (keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta =
                    sumKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta /
                    keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta =
                    maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta;
            }

            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count =
                nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count =
                nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count =
                nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointExcludedCount;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label =
                maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointLabel ?? string.Empty;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_index =
                maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointIndex;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_x_delta =
                maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointXDelta;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_y_delta =
                maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointYDelta;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_candidate_x =
                maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCandidateX;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_candidate_y =
                maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCandidateY;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_reference_x =
                maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointReferenceX;
            diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_reference_y =
                maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointReferenceY;
            if (IsFiniteMetric(maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointXDelta) &&
                IsFiniteMetric(maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointYDelta))
            {
                float remainingXBudget = Mathf.Max(
                    0f,
                    ReferenceAlignedVisualEvidenceMaxBBoxNormalizedImageSpaceKeypointL1Delta -
                    maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointYDelta);
                diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_required_x_reduction_to_threshold =
                    Mathf.Max(
                        0f,
                        maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointXDelta -
                        remainingXBudget);
            }
            if (nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount > 0)
            {
                diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta =
                    sumNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta /
                    nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount;
                diagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta =
                    maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta;
            }
        }

        private static CandidateScreenshotFrameMetrics BuildCandidateScreenshotFrameMetrics(
            string frameIndexPath,
            string projectRoot)
        {
            var metrics = new CandidateScreenshotFrameMetrics();
            string[] lines = File.ReadAllLines(frameIndexPath, Encoding.UTF8);
            if (lines.Length <= 1)
            {
                return metrics;
            }

            string[] headers = VisualComparisonCsvMetricReader.SplitLine(lines[0]);
            int viewIndex = VisualComparisonCsvMetricReader.FindHeaderIndex(headers, "view");
            int pathIndex = VisualComparisonCsvMetricReader.FindHeaderIndex(headers, "path");
            int recorderFrameIndex = VisualComparisonCsvMetricReader.FindHeaderIndex(headers, "recorderFrame");
            if (pathIndex < 0)
            {
                return metrics;
            }

            float sumHeight = 0f;
            float sumWidth = 0f;
            float sumUpperLimbSpan = 0f;
            float sumLowerLimbSpan = 0f;
            int limbSpanCount = 0;
            float sumBrightArea = 0f;
            float maxBottomGap = 0f;
            float maxTopGap = 0f;
            float minCenterX = float.PositiveInfinity;
            float maxCenterX = float.NegativeInfinity;
            var errors = new List<string>();

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                string[] cells = VisualComparisonCsvMetricReader.SplitLine(lines[i]);
                if (pathIndex >= cells.Length)
                {
                    continue;
                }

                if (viewIndex >= 0 &&
                    viewIndex < cells.Length &&
                    !string.Equals(cells[viewIndex], CandidateScreenshotFramingView, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string screenshotPath = ResolveProjectRelativePath(cells[pathIndex], projectRoot);
                if (!TryAnalyzeCandidateScreenshotFrame(screenshotPath, out VisualComparisonCandidateFrameMetric frameMetric, out string error))
                {
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        errors.Add(error);
                    }

                    continue;
                }

                metrics.SampleCount++;
                int parsedRecorderFrame = -1;
                if (recorderFrameIndex >= 0 &&
                    recorderFrameIndex < cells.Length &&
                    int.TryParse(cells[recorderFrameIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int recorderFrame))
                {
                    parsedRecorderFrame = recorderFrame;
                    metrics.RecorderFrames.Add(parsedRecorderFrame);
                }
                metrics.Samples.Add(new VisualComparisonCandidateFrameSample(parsedRecorderFrame, frameMetric));

                sumHeight += frameMetric.BBoxHeightRatio;
                sumWidth += frameMetric.BBoxWidthRatio;
                if (IsFiniteMetric(frameMetric.UpperLimbSpanRatio) &&
                    IsFiniteMetric(frameMetric.LowerLimbSpanRatio))
                {
                    sumUpperLimbSpan += frameMetric.UpperLimbSpanRatio;
                    sumLowerLimbSpan += frameMetric.LowerLimbSpanRatio;
                    limbSpanCount++;
                }
                sumBrightArea += frameMetric.BrightAreaRatio;
                maxBottomGap = Mathf.Max(maxBottomGap, frameMetric.BottomGapRatio);
                maxTopGap = Mathf.Max(maxTopGap, frameMetric.TopGapRatio);
                if (frameMetric.HasBrightPixels)
                {
                    metrics.NonblankCount++;
                    minCenterX = Mathf.Min(minCenterX, frameMetric.CenterX);
                    maxCenterX = Mathf.Max(maxCenterX, frameMetric.CenterX);
                }
            }

            if (metrics.SampleCount <= 0)
            {
                metrics.Error = string.Join("; ", errors);
                return metrics;
            }

            metrics.AvgBBoxHeightRatio = sumHeight / metrics.SampleCount;
            metrics.AvgBBoxWidthRatio = sumWidth / metrics.SampleCount;
            if (limbSpanCount > 0)
            {
                metrics.AvgUpperLimbSpanRatio = sumUpperLimbSpan / limbSpanCount;
                metrics.AvgLowerLimbSpanRatio = sumLowerLimbSpan / limbSpanCount;
            }
            metrics.AvgBrightAreaRatio = sumBrightArea / metrics.SampleCount;
            metrics.MaxBottomGapRatio = maxBottomGap;
            metrics.MaxTopGapRatio = maxTopGap;
            metrics.CenterXRangeRatio = metrics.NonblankCount > 0
                ? maxCenterX - minCenterX
                : float.NaN;
            metrics.Error = string.Join("; ", errors);
            return metrics;
        }

        internal static bool TryAnalyzeCandidateScreenshotFrame(
            string screenshotPath,
            out VisualComparisonCandidateFrameMetric metric,
            out string error)
        {
            metric = new VisualComparisonCandidateFrameMetric
            {
                CenterX = float.NaN,
                BottomGapRatio = 1f,
                TopGapRatio = 1f
            };
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(screenshotPath) || !File.Exists(screenshotPath))
            {
                error = "missing screenshot: " + (screenshotPath ?? string.Empty);
                return false;
            }

            byte[] bytes = File.ReadAllBytes(screenshotPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes))
                {
                    error = "unreadable screenshot: " + screenshotPath;
                    return false;
                }

                int width = texture.width;
                int height = texture.height;
                if (width <= 0 || height <= 0)
                {
                    error = "empty screenshot dimensions: " + screenshotPath;
                    return false;
                }

                Color32[] pixels = texture.GetPixels32();
                VisualComparisonSilhouetteMetricCalculator.TryCalculateGeometry(
                    pixels,
                    width,
                    height,
                    ImageSpaceSilhouetteProfileBandCount,
                    IsCandidateBrightPixel,
                    out VisualComparisonSilhouetteGeometry brightGeometry);

                int totalPixels = Mathf.Max(1, width * height);
                metric.BrightAreaRatio = brightGeometry.Bounds.MatchedPixelCount / (float)totalPixels;
                metric.HasBrightPixels = brightGeometry.Bounds.HasMatches;
                if (!metric.HasBrightPixels)
                {
                    return true;
                }

                metric.BBoxHeightRatio = brightGeometry.BBoxHeightRatio;
                metric.BBoxWidthRatio = brightGeometry.BBoxWidthRatio;
                metric.CenterX = brightGeometry.CenterX;
                metric.BottomGapRatio = brightGeometry.BottomGapRatio;
                metric.TopGapRatio = brightGeometry.TopGapRatio;
                metric.ImageSpaceKeypointProfile = brightGeometry.KeypointProfile;
                if (VisualComparisonSilhouetteMetricCalculator.TryCalculateBandMetrics(
                    pixels,
                    width,
                    height,
                    brightGeometry.Bounds,
                    ImageSpaceSilhouetteProfileBandCount,
                    IsCandidateBrightPixel,
                    out VisualComparisonSilhouetteBandMetrics brightBandMetrics))
                {
                    metric.UpperLimbSpanRatio = brightBandMetrics.UpperSpanRatio;
                    metric.LowerLimbSpanRatio = brightBandMetrics.LowerSpanRatio;
                    metric.SilhouetteSpanProfile = brightBandMetrics.SpanProfile;
                    metric.SilhouetteEndpointProfile = brightBandMetrics.EndpointProfile;
                }

                if (VisualComparisonSilhouetteMetricCalculator.TryCalculateGeometry(
                    pixels,
                    width,
                    height,
                    ImageSpaceSilhouetteProfileBandCount,
                    IsCandidateNonHairBrightPixel,
                    out VisualComparisonSilhouetteGeometry nonHairGeometry) &&
                    nonHairGeometry.Bounds.HasMatches)
                {
                    metric.HasNonHairBrightPixels = true;
                    metric.NonHairBBoxHeightRatio = nonHairGeometry.BBoxHeightRatio;
                    metric.NonHairBBoxWidthRatio = nonHairGeometry.BBoxWidthRatio;
                    metric.NonHairCenterX = nonHairGeometry.CenterX;
                    metric.NonHairBottomGapRatio = nonHairGeometry.BottomGapRatio;
                    metric.NonHairTopGapRatio = nonHairGeometry.TopGapRatio;
                    metric.NonHairImageSpaceKeypointProfile = nonHairGeometry.KeypointProfile;
                }
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string ResolveImageSpaceKeypointLabel(int keypointIndex)
        {
            if (keypointIndex == 0)
            {
                return "bottom_center";
            }

            if (keypointIndex == 1)
            {
                return "top_center";
            }

            int bandEndpointIndex = keypointIndex - 2;
            if (bandEndpointIndex < 0)
            {
                return $"keypoint_{keypointIndex}";
            }

            int bandIndex = bandEndpointIndex / 2;
            string side = bandEndpointIndex % 2 == 0 ? "left" : "right";
            return $"band_{bandIndex}_{side}";
        }

        private static bool IsCandidateBrightPixel(Color32 pixel)
        {
            float luminance =
                ((pixel.r * 0.2126f) + (pixel.g * 0.7152f) + (pixel.b * 0.0722f)) / 255f;
            return pixel.a > CandidateScreenshotOpaqueAlphaThreshold &&
                   luminance > CandidateScreenshotBrightLuminanceThreshold;
        }

        private static bool IsCandidateNonHairBrightPixel(Color32 pixel)
        {
            return IsCandidateBrightPixel(pixel) && !IsCandidateHairLikePixel(pixel);
        }

        private static bool IsCandidateHairLikePixel(Color32 pixel)
        {
            return IsCandidateCyanTealHairLikePixel(pixel) ||
                   IsCandidateDarkTealHairShadowPixel(pixel);
        }

        private static bool IsCandidateCyanTealHairLikePixel(Color32 pixel)
        {
            if (!IsCandidateBrightPixel(pixel))
            {
                return false;
            }

            return pixel.g >= 90 &&
                   pixel.b >= 90 &&
                   pixel.r <= 170 &&
                   pixel.g >= pixel.r * 1.15f &&
                   pixel.b >= pixel.r * 1.10f &&
                   Mathf.Abs(pixel.g - pixel.b) <= 100;
        }

        private static bool IsCandidateDarkTealHairShadowPixel(Color32 pixel)
        {
            if (!IsCandidateBrightPixel(pixel))
            {
                return false;
            }

            return pixel.r <= 80 &&
                   pixel.g >= 25 &&
                   pixel.b >= 25 &&
                   pixel.g >= pixel.r * 1.35f &&
                   pixel.b >= pixel.r * 1.35f &&
                   Mathf.Abs(pixel.g - pixel.b) <= 80;
        }

        private static string ResolveProjectRelativePath(string relativePath, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            {
                return relativePath ?? string.Empty;
            }

            string resolvedProjectRoot = projectRoot;
            if (string.IsNullOrWhiteSpace(resolvedProjectRoot))
            {
                DirectoryInfo projectRootDirectory = Directory.GetParent(Application.dataPath);
                if (projectRootDirectory == null)
                {
                    throw new InvalidOperationException("Unity 프로젝트 루트를 확인할 수 없습니다.");
                }

                resolvedProjectRoot = projectRootDirectory.FullName;
            }

            return Path.Combine(resolvedProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private sealed class CandidateScreenshotFrameMetrics
        {
            public int SampleCount;
            public int NonblankCount;
            public float AvgBBoxHeightRatio = float.NaN;
            public float AvgBBoxWidthRatio = float.NaN;
            public float AvgUpperLimbSpanRatio = float.NaN;
            public float AvgLowerLimbSpanRatio = float.NaN;
            public float CenterXRangeRatio = float.NaN;
            public float MaxBottomGapRatio = float.NaN;
            public float MaxTopGapRatio = float.NaN;
            public float AvgBrightAreaRatio = float.NaN;
            public readonly List<int> RecorderFrames = new List<int>();
            public readonly List<VisualComparisonCandidateFrameSample> Samples =
                new List<VisualComparisonCandidateFrameSample>();
            public string Error = string.Empty;
        }

    }
}
#endif
