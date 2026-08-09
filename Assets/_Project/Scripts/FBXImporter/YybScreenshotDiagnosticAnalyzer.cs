#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using static Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner;

namespace Fbx2Vmd.FBXImporter
{
    internal static partial class YybScreenshotDiagnosticAnalyzer
    {
        private const string CandidateScreenshotFramingView = "front";
        private const float CandidateScreenshotBrightLuminanceThreshold = 0.08f;
        private const byte CandidateScreenshotOpaqueAlphaThreshold = 8;
        private const int ImageSpaceSilhouetteProfileBandCount = 4;
        private const float ReferenceAlignedVisualEvidenceMaxBBoxNormalizedImageSpaceKeypointL1Delta = 0.30f;
        private const float ReferenceAlignedVisualEvidenceEndpointPixelTolerance = 0.001f;

        internal static void AttachCandidateScreenshotFrameDiagnostics(
            YybVisualComparisonBatchRunner.SummaryFrameRoleDiagnostics diagnostics,
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
            YybVisualComparisonBatchRunner.SummaryFrameRoleDiagnostics diagnostics,
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
            var timedSamples = new List<CandidateScreenshotFrameSample>();
            foreach (CandidateScreenshotFrameSample sample in metrics.Samples)
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
            YybVisualComparisonBatchRunner.SummaryFrameRoleDiagnostics diagnostics,
            List<CandidateScreenshotFrameSample> candidateSamples)
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
            float referenceClipStartSeconds = Mathf.Max(
                0f,
                diagnostics.reference_mp4_current_clip_start_seconds);
            float referenceClipDurationSeconds = Mathf.Max(
                0f,
                diagnostics.reference_mp4_current_clip_duration_seconds);
            foreach (YybVisualComparisonBatchRunner.ReferenceMp4FrameMetricRow referenceRow in diagnostics.referenceMp4CurrentClipRows)
            {
                if (referenceRow == null || float.IsNaN(referenceRow.seconds))
                {
                    continue;
                }

                float referenceLocalSeconds = Mathf.Clamp(
                    referenceRow.seconds - referenceClipStartSeconds,
                    0f,
                    referenceClipDurationSeconds);
                CandidateScreenshotFrameSample nearestSample = null;
                float nearestGap = float.PositiveInfinity;
                foreach (CandidateScreenshotFrameSample candidateSample in candidateSamples)
                {
                    if (candidateSample == null ||
                        candidateSample.Metric == null ||
                        !candidateSample.Metric.HasBrightPixels ||
                        float.IsNaN(candidateSample.Seconds))
                    {
                        continue;
                    }

                    float gap = Mathf.Abs(candidateSample.Seconds - referenceLocalSeconds);
                    if (gap < nearestGap)
                    {
                        nearestGap = gap;
                        nearestSample = candidateSample;
                    }
                }

                if (nearestSample == null || float.IsInfinity(nearestGap))
                {
                    continue;
                }

                CandidateScreenshotFrameMetric candidateMetric = nearestSample.Metric;
                float bboxHeightDelta = Mathf.Abs(candidateMetric.BBoxHeightRatio - referenceRow.bboxHeightRatio);
                float bboxWidthDelta = Mathf.Abs(candidateMetric.BBoxWidthRatio - referenceRow.bboxWidthRatio);
                float centerXDelta = Mathf.Abs(candidateMetric.CenterX - referenceRow.centerXRatio);
                float bottomGapDelta = Mathf.Abs(candidateMetric.BottomGapRatio - referenceRow.bottomGapRatio);
                float brightAreaDelta = Mathf.Abs(candidateMetric.BrightAreaRatio - referenceRow.brightAreaRatio);
                float referenceTopGapRatio = ResolveFrameTopGapRatio(
                    referenceRow.bottomGapRatio,
                    referenceRow.bboxHeightRatio);
                bool referenceTouchesFrameEdge = IsFrameEdgeTouched(referenceRow.bottomGapRatio, referenceTopGapRatio);
                bool candidateTouchesFrameEdge =
                    IsFrameEdgeTouched(candidateMetric.BottomGapRatio, candidateMetric.TopGapRatio);
                bool cropSafeSample = !referenceTouchesFrameEdge && !candidateTouchesFrameEdge;
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
                if (TryComputeSilhouetteProfileDelta(
                    candidateMetric.SilhouetteSpanProfile,
                    referenceRow.silhouetteSpanProfile,
                    out int matchedBandCount,
                    out float silhouetteProfileL1Delta,
                    out float silhouetteProfileBandDelta))
                {
                    silhouetteProfileBandCount = Mathf.Max(silhouetteProfileBandCount, matchedBandCount);
                    silhouetteProfileCount++;
                    sumSilhouetteProfileL1Delta += silhouetteProfileL1Delta;
                    maxSilhouetteProfileL1Delta = Mathf.Max(maxSilhouetteProfileL1Delta, silhouetteProfileL1Delta);
                    maxSilhouetteProfileBandDelta = Mathf.Max(maxSilhouetteProfileBandDelta, silhouetteProfileBandDelta);
                    if (cropSafeSample)
                    {
                        cropSafeSilhouetteProfileCount++;
                        sumCropSafeSilhouetteProfileL1Delta += silhouetteProfileL1Delta;
                        maxCropSafeSilhouetteProfileL1Delta =
                            Mathf.Max(maxCropSafeSilhouetteProfileL1Delta, silhouetteProfileL1Delta);
                    }
                }
                if (TryComputeSilhouetteEndpointDelta(
                    candidateMetric.SilhouetteEndpointProfile,
                    referenceRow.silhouetteEndpointProfile,
                    out int matchedEndpointBandCount,
                    out float silhouetteEndpointDelta,
                    out float silhouetteEndpointMaxDelta))
                {
                    silhouetteLandmarkBandCount = Mathf.Max(silhouetteLandmarkBandCount, matchedEndpointBandCount);
                    silhouetteLandmarkCount++;
                    sumSilhouetteLandmarkEndpointDelta += silhouetteEndpointDelta;
                    maxSilhouetteLandmarkEndpointDelta =
                        Mathf.Max(maxSilhouetteLandmarkEndpointDelta, silhouetteEndpointMaxDelta);
                }
                if (TryComputeImageSpaceKeypointDelta(
                    candidateMetric.ImageSpaceKeypointProfile,
                    referenceRow.imageSpaceKeypointProfile,
                    out int matchedKeypointCount,
                    out float keypointL1Delta,
                    out float keypointMaxL1Delta))
                {
                    imageSpaceKeypointCount = Mathf.Max(imageSpaceKeypointCount, matchedKeypointCount);
                    imageSpaceKeypointSampleCount++;
                    sumImageSpaceKeypointL1Delta += keypointL1Delta;
                    maxImageSpaceKeypointL1Delta =
                        Mathf.Max(maxImageSpaceKeypointL1Delta, keypointMaxL1Delta);
                    if (cropSafeSample)
                    {
                        cropSafeImageSpaceKeypointSampleCount++;
                        sumCropSafeImageSpaceKeypointL1Delta += keypointL1Delta;
                        maxCropSafeImageSpaceKeypointL1Delta =
                            Mathf.Max(maxCropSafeImageSpaceKeypointL1Delta, keypointMaxL1Delta);
                    }
                }
                if (TryComputeBBoxNormalizedImageSpaceKeypointDelta(
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
                    out int matchedBBoxNormalizedKeypointCount,
                    out float bboxNormalizedKeypointL1Delta,
                    out float bboxNormalizedKeypointMaxL1Delta,
                    out int bboxNormalizedKeypointMaxIndex,
                    out float bboxNormalizedKeypointMaxXDelta,
                    out float bboxNormalizedKeypointMaxYDelta,
                    out float bboxNormalizedKeypointMaxCandidateX,
                    out float bboxNormalizedKeypointMaxCandidateY,
                    out float bboxNormalizedKeypointMaxReferenceX,
                    out float bboxNormalizedKeypointMaxReferenceY))
                {
                    bboxNormalizedImageSpaceKeypointCount =
                        Mathf.Max(bboxNormalizedImageSpaceKeypointCount, matchedBBoxNormalizedKeypointCount);
                    bboxNormalizedImageSpaceKeypointSampleCount++;
                    sumBBoxNormalizedImageSpaceKeypointL1Delta += bboxNormalizedKeypointL1Delta;
                    if (cropSafeSample)
                    {
                        cropSafeBBoxNormalizedImageSpaceKeypointSampleCount++;
                        sumCropSafeBBoxNormalizedImageSpaceKeypointL1Delta += bboxNormalizedKeypointL1Delta;
                        maxCropSafeBBoxNormalizedImageSpaceKeypointL1Delta =
                            Mathf.Max(maxCropSafeBBoxNormalizedImageSpaceKeypointL1Delta, bboxNormalizedKeypointMaxL1Delta);
                    }
                    if (TryComputeKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointDelta(
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
                        out int matchedKeypointLocalCropSafeKeypointCount,
                        out int excludedKeypointLocalCropSafeKeypointCount,
                        out float keypointLocalCropSafeKeypointL1Delta,
                        out float keypointLocalCropSafeKeypointMaxL1Delta,
                        out int keypointLocalCropSafeKeypointMaxIndex,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _))
                    {
                        keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount =
                            Mathf.Max(
                                keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount,
                                matchedKeypointLocalCropSafeKeypointCount);
                        keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount++;
                        keypointLocalCropSafeBBoxNormalizedImageSpaceKeypointExcludedCount +=
                            excludedKeypointLocalCropSafeKeypointCount;
                        sumKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta +=
                            keypointLocalCropSafeKeypointL1Delta;
                        if (keypointLocalCropSafeKeypointMaxL1Delta >
                            maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta)
                        {
                            maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta =
                                keypointLocalCropSafeKeypointMaxL1Delta;
                            maxKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointLabel =
                                ResolveImageSpaceKeypointLabel(keypointLocalCropSafeKeypointMaxIndex);
                        }
                    }

                    if (bboxNormalizedKeypointMaxL1Delta > maxBBoxNormalizedImageSpaceKeypointL1Delta)
                    {
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_index =
                            bboxNormalizedKeypointMaxIndex;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label =
                            ResolveImageSpaceKeypointLabel(bboxNormalizedKeypointMaxIndex);
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_seconds =
                            referenceRow.seconds;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_seconds =
                            nearestSample.Seconds;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_recorder_frame =
                            nearestSample.RecorderFrame;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_x_delta =
                            bboxNormalizedKeypointMaxXDelta;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_y_delta =
                            bboxNormalizedKeypointMaxYDelta;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_x =
                            bboxNormalizedKeypointMaxCandidateX;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_y =
                            bboxNormalizedKeypointMaxCandidateY;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_x =
                            bboxNormalizedKeypointMaxReferenceX;
                        diagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_y =
                            bboxNormalizedKeypointMaxReferenceY;
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
                        Mathf.Max(maxBBoxNormalizedImageSpaceKeypointL1Delta, bboxNormalizedKeypointMaxL1Delta);
                }

                if (candidateMetric.HasNonHairBrightPixels &&
                    referenceRow.hasNonHairBrightPixels &&
                    TryComputeBBoxNormalizedImageSpaceKeypointDelta(
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
                        out int matchedNonHairBBoxNormalizedKeypointCount,
                        out float nonHairBBoxNormalizedKeypointL1Delta,
                        out float nonHairBBoxNormalizedKeypointMaxL1Delta,
                        out int nonHairBBoxNormalizedKeypointMaxIndex,
                        out float nonHairBBoxNormalizedKeypointMaxXDelta,
                        out float nonHairBBoxNormalizedKeypointMaxYDelta,
                        out float nonHairBBoxNormalizedKeypointMaxCandidateX,
                        out float nonHairBBoxNormalizedKeypointMaxCandidateY,
                        out float nonHairBBoxNormalizedKeypointMaxReferenceX,
                        out float nonHairBBoxNormalizedKeypointMaxReferenceY))
                {
                    nonHairBBoxNormalizedImageSpaceKeypointCount =
                        Mathf.Max(
                            nonHairBBoxNormalizedImageSpaceKeypointCount,
                            matchedNonHairBBoxNormalizedKeypointCount);
                    nonHairBBoxNormalizedImageSpaceKeypointSampleCount++;
                    sumNonHairBBoxNormalizedImageSpaceKeypointL1Delta += nonHairBBoxNormalizedKeypointL1Delta;
                    if (nonHairBBoxNormalizedKeypointMaxL1Delta >
                        maxNonHairBBoxNormalizedImageSpaceKeypointL1Delta)
                    {
                        maxNonHairBBoxNormalizedImageSpaceKeypointL1Delta =
                            nonHairBBoxNormalizedKeypointMaxL1Delta;
                        maxNonHairBBoxNormalizedImageSpaceKeypointIndex =
                            nonHairBBoxNormalizedKeypointMaxIndex;
                        maxNonHairBBoxNormalizedImageSpaceKeypointLabel =
                            ResolveImageSpaceKeypointLabel(nonHairBBoxNormalizedKeypointMaxIndex);

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
                            nonHairBBoxNormalizedKeypointMaxXDelta;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_y_delta =
                            nonHairBBoxNormalizedKeypointMaxYDelta;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_x =
                            nonHairBBoxNormalizedKeypointMaxCandidateX;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_y =
                            nonHairBBoxNormalizedKeypointMaxCandidateY;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_x =
                            nonHairBBoxNormalizedKeypointMaxReferenceX;
                        diagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_y =
                            nonHairBBoxNormalizedKeypointMaxReferenceY;
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

                    if (TryComputeKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointDelta(
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
                        out int matchedNonHairKeypointLocalCropSafeKeypointCount,
                        out int excludedNonHairKeypointLocalCropSafeKeypointCount,
                        out float nonHairKeypointLocalCropSafeKeypointL1Delta,
                        out float nonHairKeypointLocalCropSafeKeypointMaxL1Delta,
                        out int nonHairKeypointLocalCropSafeKeypointMaxIndex,
                        out float nonHairKeypointLocalCropSafeKeypointMaxXDelta,
                        out float nonHairKeypointLocalCropSafeKeypointMaxYDelta,
                        out float nonHairKeypointLocalCropSafeKeypointMaxCandidateX,
                        out float nonHairKeypointLocalCropSafeKeypointMaxCandidateY,
                        out float nonHairKeypointLocalCropSafeKeypointMaxReferenceX,
                        out float nonHairKeypointLocalCropSafeKeypointMaxReferenceY))
                    {
                        nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount =
                            Mathf.Max(
                                nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCount,
                                matchedNonHairKeypointLocalCropSafeKeypointCount);
                        nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointSampleCount++;
                        nonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointExcludedCount +=
                            excludedNonHairKeypointLocalCropSafeKeypointCount;
                        sumNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta +=
                            nonHairKeypointLocalCropSafeKeypointL1Delta;
                        if (nonHairKeypointLocalCropSafeKeypointMaxL1Delta >
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta)
                        {
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointL1Delta =
                                nonHairKeypointLocalCropSafeKeypointMaxL1Delta;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointLabel =
                                ResolveImageSpaceKeypointLabel(nonHairKeypointLocalCropSafeKeypointMaxIndex);
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointIndex =
                                nonHairKeypointLocalCropSafeKeypointMaxIndex;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointXDelta =
                                nonHairKeypointLocalCropSafeKeypointMaxXDelta;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointYDelta =
                                nonHairKeypointLocalCropSafeKeypointMaxYDelta;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCandidateX =
                                nonHairKeypointLocalCropSafeKeypointMaxCandidateX;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointCandidateY =
                                nonHairKeypointLocalCropSafeKeypointMaxCandidateY;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointReferenceX =
                                nonHairKeypointLocalCropSafeKeypointMaxReferenceX;
                            maxNonHairKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointReferenceY =
                                nonHairKeypointLocalCropSafeKeypointMaxReferenceY;
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

            string[] headers = SplitSimpleCsvLine(lines[0]);
            int viewIndex = IndexOfHeader(headers, "view");
            int pathIndex = IndexOfHeader(headers, "path");
            int recorderFrameIndex = IndexOfHeader(headers, "recorderFrame");
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

                string[] cells = SplitSimpleCsvLine(lines[i]);
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
                if (!TryAnalyzeCandidateScreenshotFrame(screenshotPath, out CandidateScreenshotFrameMetric frameMetric, out string error))
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
                metrics.Samples.Add(new CandidateScreenshotFrameSample(parsedRecorderFrame, frameMetric));

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
            out CandidateScreenshotFrameMetric metric,
            out string error)
        {
            metric = new CandidateScreenshotFrameMetric
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
                int minX = width;
                int minY = height;
                int maxX = -1;
                int maxY = -1;
                int brightPixelCount = 0;
                for (int y = 0; y < height; y++)
                {
                    int rowOffset = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        Color32 pixel = pixels[rowOffset + x];
                        if (!IsCandidateBrightPixel(pixel))
                        {
                            continue;
                        }

                        brightPixelCount++;
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                    }
                }

                int totalPixels = Mathf.Max(1, width * height);
                metric.BrightAreaRatio = brightPixelCount / (float)totalPixels;
                metric.HasBrightPixels = brightPixelCount > 0;
                if (!metric.HasBrightPixels)
                {
                    return true;
                }

                metric.BBoxHeightRatio = (maxY - minY + 1) / (float)height;
                metric.BBoxWidthRatio = (maxX - minX + 1) / (float)width;
                metric.CenterX = ((minX + maxX + 1) * 0.5f) / width;
                metric.BottomGapRatio = minY / (float)height;
                metric.TopGapRatio = (height - maxY - 1) / (float)height;
                FillBandedImageSpaceLimbSpanMetrics(pixels, width, height, minY, maxY, metric);
                metric.SilhouetteSpanProfile = BuildSilhouetteSpanProfile(
                    pixels,
                    width,
                    minY,
                    maxY,
                    ImageSpaceSilhouetteProfileBandCount);
                metric.SilhouetteEndpointProfile = BuildSilhouetteEndpointProfile(
                    pixels,
                    width,
                    minY,
                    maxY,
                    ImageSpaceSilhouetteProfileBandCount);
                metric.ImageSpaceKeypointProfile = BuildImageSpaceSilhouetteKeypointProfile(
                    pixels,
                    width,
                    height,
                    minY,
                    maxY,
                    ImageSpaceSilhouetteProfileBandCount);
                if (TryAnalyzeImageSpaceSilhouette(
                    pixels,
                    width,
                    height,
                    IsCandidateNonHairBrightPixel,
                    out float nonHairBBoxHeightRatio,
                    out float nonHairBBoxWidthRatio,
                    out float nonHairCenterX,
                    out float nonHairBottomGapRatio,
                    out float nonHairTopGapRatio,
                    out float[] nonHairImageSpaceKeypointProfile))
                {
                    metric.HasNonHairBrightPixels = true;
                    metric.NonHairBBoxHeightRatio = nonHairBBoxHeightRatio;
                    metric.NonHairBBoxWidthRatio = nonHairBBoxWidthRatio;
                    metric.NonHairCenterX = nonHairCenterX;
                    metric.NonHairBottomGapRatio = nonHairBottomGapRatio;
                    metric.NonHairTopGapRatio = nonHairTopGapRatio;
                    metric.NonHairImageSpaceKeypointProfile = nonHairImageSpaceKeypointProfile;
                }
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static bool TryAnalyzeImageSpaceSilhouette(
            Color32[] pixels,
            int width,
            int height,
            Func<Color32, bool> pixelPredicate,
            out float bboxHeightRatio,
            out float bboxWidthRatio,
            out float centerX,
            out float bottomGapRatio,
            out float topGapRatio,
            out float[] imageSpaceKeypointProfile)
        {
            bboxHeightRatio = float.NaN;
            bboxWidthRatio = float.NaN;
            centerX = float.NaN;
            bottomGapRatio = float.NaN;
            topGapRatio = float.NaN;
            imageSpaceKeypointProfile = Array.Empty<float>();
            if (pixels == null || width <= 0 || height <= 0 || pixelPredicate == null)
            {
                return false;
            }

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!pixelPredicate(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return false;
            }

            bboxHeightRatio = (maxY - minY + 1) / (float)height;
            bboxWidthRatio = (maxX - minX + 1) / (float)width;
            centerX = ((minX + maxX + 1) * 0.5f) / width;
            bottomGapRatio = minY / (float)height;
            topGapRatio = (height - maxY - 1) / (float)height;
            imageSpaceKeypointProfile = BuildImageSpaceSilhouetteKeypointProfile(
                pixels,
                width,
                height,
                minY,
                maxY,
                ImageSpaceSilhouetteProfileBandCount,
                pixelPredicate);
            return true;
        }

        private static void FillBandedImageSpaceLimbSpanMetrics(
            Color32[] pixels,
            int width,
            int height,
            int minY,
            int maxY,
            CandidateScreenshotFrameMetric metric)
        {
            if (pixels == null || metric == null || width <= 0 || height <= 0 || maxY < minY)
            {
                return;
            }

            int bboxHeight = maxY - minY + 1;
            int upperStartY = minY + Mathf.CeilToInt(bboxHeight * 0.5f);
            int lowerMinX = width;
            int lowerMaxX = -1;
            int upperMinX = width;
            int upperMaxX = -1;
            for (int y = minY; y <= maxY; y++)
            {
                int rowOffset = y * width;
                bool upperBand = y >= upperStartY;
                for (int x = 0; x < width; x++)
                {
                    Color32 pixel = pixels[rowOffset + x];
                    if (!IsCandidateBrightPixel(pixel))
                    {
                        continue;
                    }

                    if (upperBand)
                    {
                        upperMinX = Mathf.Min(upperMinX, x);
                        upperMaxX = Mathf.Max(upperMaxX, x);
                    }
                    else
                    {
                        lowerMinX = Mathf.Min(lowerMinX, x);
                        lowerMaxX = Mathf.Max(lowerMaxX, x);
                    }
                }
            }

            if (upperMaxX >= upperMinX)
            {
                metric.UpperLimbSpanRatio = (upperMaxX - upperMinX + 1) / (float)width;
            }

            if (lowerMaxX >= lowerMinX)
            {
                metric.LowerLimbSpanRatio = (lowerMaxX - lowerMinX + 1) / (float)width;
            }
        }

        private static float[] BuildSilhouetteSpanProfile(
            Color32[] pixels,
            int width,
            int minY,
            int maxY,
            int bandCount)
        {
            if (pixels == null || width <= 0 || maxY < minY || bandCount <= 0)
            {
                return Array.Empty<float>();
            }

            int bboxHeight = maxY - minY + 1;
            var minXByBand = new int[bandCount];
            var maxXByBand = new int[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                minXByBand[i] = width;
                maxXByBand[i] = -1;
            }

            for (int y = minY; y <= maxY; y++)
            {
                int bandIndex = Mathf.Clamp(((y - minY) * bandCount) / bboxHeight, 0, bandCount - 1);
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!IsCandidateBrightPixel(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minXByBand[bandIndex] = Mathf.Min(minXByBand[bandIndex], x);
                    maxXByBand[bandIndex] = Mathf.Max(maxXByBand[bandIndex], x);
                }
            }

            var profile = new float[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                profile[i] = maxXByBand[i] >= minXByBand[i]
                    ? (maxXByBand[i] - minXByBand[i] + 1) / (float)width
                    : 0f;
            }

            return profile;
        }

        private static bool TryComputeSilhouetteProfileDelta(
            float[] candidateProfile,
            float[] referenceProfile,
            out int bandCount,
            out float l1Delta,
            out float maxBandDelta)
        {
            bandCount = 0;
            l1Delta = float.NaN;
            maxBandDelta = float.NaN;
            if (candidateProfile == null || referenceProfile == null)
            {
                return false;
            }

            int length = Mathf.Min(candidateProfile.Length, referenceProfile.Length);
            if (length <= 0)
            {
                return false;
            }

            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteCount = 0;
            for (int i = 0; i < length; i++)
            {
                float candidate = candidateProfile[i];
                float reference = referenceProfile[i];
                if (!IsFiniteMetric(candidate) || !IsFiniteMetric(reference))
                {
                    continue;
                }

                float delta = Mathf.Abs(candidate - reference);
                sumDelta += delta;
                maxDelta = Mathf.Max(maxDelta, delta);
                finiteCount++;
            }

            if (finiteCount <= 0)
            {
                return false;
            }

            bandCount = finiteCount;
            l1Delta = sumDelta / finiteCount;
            maxBandDelta = maxDelta;
            return true;
        }

        private static float[] BuildSilhouetteEndpointProfile(
            Color32[] pixels,
            int width,
            int minY,
            int maxY,
            int bandCount)
        {
            if (pixels == null || width <= 0 || maxY < minY || bandCount <= 0)
            {
                return Array.Empty<float>();
            }

            int bboxHeight = maxY - minY + 1;
            var minXByBand = new int[bandCount];
            var maxXByBand = new int[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                minXByBand[i] = width;
                maxXByBand[i] = -1;
            }

            for (int y = minY; y <= maxY; y++)
            {
                int bandIndex = Mathf.Clamp(((y - minY) * bandCount) / bboxHeight, 0, bandCount - 1);
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!IsCandidateBrightPixel(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minXByBand[bandIndex] = Mathf.Min(minXByBand[bandIndex], x);
                    maxXByBand[bandIndex] = Mathf.Max(maxXByBand[bandIndex], x);
                }
            }

            var endpoints = new float[bandCount * 2];
            for (int i = 0; i < bandCount; i++)
            {
                int leftIndex = i * 2;
                int rightIndex = leftIndex + 1;
                if (maxXByBand[i] >= minXByBand[i])
                {
                    endpoints[leftIndex] = minXByBand[i] / (float)width;
                    endpoints[rightIndex] = (maxXByBand[i] + 1) / (float)width;
                }
                else
                {
                    endpoints[leftIndex] = float.NaN;
                    endpoints[rightIndex] = float.NaN;
                }
            }

            return endpoints;
        }

        private static bool TryComputeSilhouetteEndpointDelta(
            float[] candidateEndpoints,
            float[] referenceEndpoints,
            out int bandCount,
            out float endpointDelta,
            out float maxEndpointDelta)
        {
            bandCount = 0;
            endpointDelta = float.NaN;
            maxEndpointDelta = float.NaN;
            if (candidateEndpoints == null || referenceEndpoints == null)
            {
                return false;
            }

            int length = Mathf.Min(candidateEndpoints.Length, referenceEndpoints.Length);
            if (length <= 1)
            {
                return false;
            }

            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteEndpointCount = 0;
            for (int i = 0; i < length; i++)
            {
                float candidate = candidateEndpoints[i];
                float reference = referenceEndpoints[i];
                if (!IsFiniteMetric(candidate) || !IsFiniteMetric(reference))
                {
                    continue;
                }

                float delta = Mathf.Abs(candidate - reference);
                sumDelta += delta;
                maxDelta = Mathf.Max(maxDelta, delta);
                finiteEndpointCount++;
            }

            if (finiteEndpointCount <= 0)
            {
                return false;
            }

            bandCount = finiteEndpointCount / 2;
            endpointDelta = sumDelta / finiteEndpointCount;
            maxEndpointDelta = maxDelta;
            return true;
        }

        private static float[] BuildImageSpaceSilhouetteKeypointProfile(
            Color32[] pixels,
            int width,
            int height,
            int minY,
            int maxY,
            int bandCount)
        {
            return BuildImageSpaceSilhouetteKeypointProfile(
                pixels,
                width,
                height,
                minY,
                maxY,
                bandCount,
                IsCandidateBrightPixel);
        }

        private static float[] BuildImageSpaceSilhouetteKeypointProfile(
            Color32[] pixels,
            int width,
            int height,
            int minY,
            int maxY,
            int bandCount,
            Func<Color32, bool> pixelPredicate)
        {
            if (pixels == null || width <= 0 || height <= 0 || maxY < minY || bandCount <= 0)
            {
                return Array.Empty<float>();
            }

            var keypoints = new List<float>((2 + (bandCount * 2)) * 2);
            AppendBBoxCenterlineEndpointKeypoint(
                pixels,
                width,
                height,
                minY,
                maxY,
                useBottomEndpoint: true,
                keypoints,
                pixelPredicate);
            AppendBBoxCenterlineEndpointKeypoint(
                pixels,
                width,
                height,
                minY,
                maxY,
                useBottomEndpoint: false,
                keypoints,
                pixelPredicate);

            int bboxHeight = maxY - minY + 1;
            var minXByBand = new int[bandCount];
            var maxXByBand = new int[bandCount];
            var minYByBand = new int[bandCount];
            var maxYByBand = new int[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                minXByBand[i] = width;
                maxXByBand[i] = -1;
                minYByBand[i] = height;
                maxYByBand[i] = -1;
            }

            for (int y = minY; y <= maxY; y++)
            {
                int bandIndex = Mathf.Clamp(((y - minY) * bandCount) / bboxHeight, 0, bandCount - 1);
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!pixelPredicate(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minXByBand[bandIndex] = Mathf.Min(minXByBand[bandIndex], x);
                    maxXByBand[bandIndex] = Mathf.Max(maxXByBand[bandIndex], x);
                    minYByBand[bandIndex] = Mathf.Min(minYByBand[bandIndex], y);
                    maxYByBand[bandIndex] = Mathf.Max(maxYByBand[bandIndex], y);
                }
            }

            for (int i = 0; i < bandCount; i++)
            {
                if (maxXByBand[i] >= minXByBand[i])
                {
                    float y = ((minYByBand[i] + maxYByBand[i] + 1) * 0.5f) / height;
                    AppendKeypoint(keypoints, minXByBand[i] / (float)width, y);
                    AppendKeypoint(keypoints, (maxXByBand[i] + 1) / (float)width, y);
                }
                else
                {
                    AppendMissingKeypoint(keypoints);
                    AppendMissingKeypoint(keypoints);
                }
            }

            return keypoints.ToArray();
        }

        private static void AppendBBoxCenterlineEndpointKeypoint(
            Color32[] pixels,
            int width,
            int height,
            int minY,
            int maxY,
            bool useBottomEndpoint,
            List<float> keypoints,
            Func<Color32, bool> pixelPredicate)
        {
            int minX = width;
            int maxX = -1;
            for (int y = minY; y <= maxY; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!pixelPredicate(pixels[rowOffset + x]))
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                }
            }

            if (maxX >= minX)
            {
                float endpointY = (useBottomEndpoint ? minY : maxY) / (float)height;
                AppendKeypoint(keypoints, ((minX + maxX + 1) * 0.5f) / width, endpointY);
            }
            else
            {
                AppendMissingKeypoint(keypoints);
            }
        }

        private static void AppendKeypoint(List<float> keypoints, float x, float y)
        {
            keypoints.Add(x);
            keypoints.Add(y);
        }

        private static void AppendMissingKeypoint(List<float> keypoints)
        {
            keypoints.Add(float.NaN);
            keypoints.Add(float.NaN);
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

        private static bool TryComputeImageSpaceKeypointDelta(
            float[] candidateKeypoints,
            float[] referenceKeypoints,
            out int keypointCount,
            out float l1Delta,
            out float maxL1Delta)
        {
            keypointCount = 0;
            l1Delta = float.NaN;
            maxL1Delta = float.NaN;
            if (candidateKeypoints == null || referenceKeypoints == null)
            {
                return false;
            }

            int length = Mathf.Min(candidateKeypoints.Length, referenceKeypoints.Length);
            if (length <= 1)
            {
                return false;
            }

            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteKeypointCount = 0;
            for (int i = 0; i + 1 < length; i += 2)
            {
                float candidateX = candidateKeypoints[i];
                float candidateY = candidateKeypoints[i + 1];
                float referenceX = referenceKeypoints[i];
                float referenceY = referenceKeypoints[i + 1];
                if (!IsFiniteMetric(candidateX) ||
                    !IsFiniteMetric(candidateY) ||
                    !IsFiniteMetric(referenceX) ||
                    !IsFiniteMetric(referenceY))
                {
                    continue;
                }

                float delta = Mathf.Abs(candidateX - referenceX) + Mathf.Abs(candidateY - referenceY);
                sumDelta += delta;
                maxDelta = Mathf.Max(maxDelta, delta);
                finiteKeypointCount++;
            }

            if (finiteKeypointCount <= 0)
            {
                return false;
            }

            keypointCount = finiteKeypointCount;
            l1Delta = sumDelta / finiteKeypointCount;
            maxL1Delta = maxDelta;
            return true;
        }

        private static bool TryComputeBBoxNormalizedImageSpaceKeypointDelta(
            float[] candidateKeypoints,
            float candidateCenterX,
            float candidateBBoxWidth,
            float candidateBottomGap,
            float candidateBBoxHeight,
            float[] referenceKeypoints,
            float referenceCenterX,
            float referenceBBoxWidth,
            float referenceBottomGap,
            float referenceBBoxHeight,
            out int keypointCount,
            out float l1Delta,
            out float maxL1Delta)
        {
            return TryComputeBBoxNormalizedImageSpaceKeypointDelta(
                candidateKeypoints,
                candidateCenterX,
                candidateBBoxWidth,
                candidateBottomGap,
                candidateBBoxHeight,
                referenceKeypoints,
                referenceCenterX,
                referenceBBoxWidth,
                referenceBottomGap,
                referenceBBoxHeight,
                out keypointCount,
                out l1Delta,
                out maxL1Delta,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);
        }

        private static bool TryComputeBBoxNormalizedImageSpaceKeypointDelta(
            float[] candidateKeypoints,
            float candidateCenterX,
            float candidateBBoxWidth,
            float candidateBottomGap,
            float candidateBBoxHeight,
            float[] referenceKeypoints,
            float referenceCenterX,
            float referenceBBoxWidth,
            float referenceBottomGap,
            float referenceBBoxHeight,
            out int keypointCount,
            out float l1Delta,
            out float maxL1Delta,
            out int maxKeypointIndex,
            out float maxXDelta,
            out float maxYDelta,
            out float maxCandidateX,
            out float maxCandidateY,
            out float maxReferenceX,
            out float maxReferenceY)
        {
            keypointCount = 0;
            l1Delta = float.NaN;
            maxL1Delta = float.NaN;
            maxKeypointIndex = -1;
            maxXDelta = float.NaN;
            maxYDelta = float.NaN;
            maxCandidateX = float.NaN;
            maxCandidateY = float.NaN;
            maxReferenceX = float.NaN;
            maxReferenceY = float.NaN;
            if (candidateKeypoints == null ||
                referenceKeypoints == null ||
                !IsFiniteMetric(candidateCenterX) ||
                !IsFiniteMetric(candidateBBoxWidth) ||
                !IsFiniteMetric(candidateBottomGap) ||
                !IsFiniteMetric(candidateBBoxHeight) ||
                !IsFiniteMetric(referenceCenterX) ||
                !IsFiniteMetric(referenceBBoxWidth) ||
                !IsFiniteMetric(referenceBottomGap) ||
                !IsFiniteMetric(referenceBBoxHeight) ||
                candidateBBoxWidth <= 0f ||
                candidateBBoxHeight <= 0f ||
                referenceBBoxWidth <= 0f ||
                referenceBBoxHeight <= 0f)
            {
                return false;
            }

            int length = Mathf.Min(candidateKeypoints.Length, referenceKeypoints.Length);
            if (length <= 1)
            {
                return false;
            }

            float candidateLeft = candidateCenterX - (candidateBBoxWidth * 0.5f);
            float referenceLeft = referenceCenterX - (referenceBBoxWidth * 0.5f);
            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteKeypointCount = 0;
            for (int i = 0; i + 1 < length; i += 2)
            {
                float candidateX = candidateKeypoints[i];
                float candidateY = candidateKeypoints[i + 1];
                float referenceX = referenceKeypoints[i];
                float referenceY = referenceKeypoints[i + 1];
                if (!IsFiniteMetric(candidateX) ||
                    !IsFiniteMetric(candidateY) ||
                    !IsFiniteMetric(referenceX) ||
                    !IsFiniteMetric(referenceY))
                {
                    continue;
                }

                float candidateNormalizedX = (candidateX - candidateLeft) / candidateBBoxWidth;
                float candidateNormalizedY = (candidateY - candidateBottomGap) / candidateBBoxHeight;
                float referenceNormalizedX = (referenceX - referenceLeft) / referenceBBoxWidth;
                float referenceNormalizedY = (referenceY - referenceBottomGap) / referenceBBoxHeight;
                float delta =
                    Mathf.Abs(candidateNormalizedX - referenceNormalizedX) +
                    Mathf.Abs(candidateNormalizedY - referenceNormalizedY);
                sumDelta += delta;
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                    maxKeypointIndex = i / 2;
                    maxXDelta = Mathf.Abs(candidateNormalizedX - referenceNormalizedX);
                    maxYDelta = Mathf.Abs(candidateNormalizedY - referenceNormalizedY);
                    maxCandidateX = candidateNormalizedX;
                    maxCandidateY = candidateNormalizedY;
                    maxReferenceX = referenceNormalizedX;
                    maxReferenceY = referenceNormalizedY;
                }
                finiteKeypointCount++;
            }

            if (finiteKeypointCount <= 0)
            {
                return false;
            }

            keypointCount = finiteKeypointCount;
            l1Delta = sumDelta / finiteKeypointCount;
            maxL1Delta = maxDelta;
            return true;
        }

        private static bool TryComputeKeypointLocalCropSafeBBoxNormalizedImageSpaceKeypointDelta(
            float[] candidateKeypoints,
            float candidateCenterX,
            float candidateBBoxWidth,
            float candidateBottomGap,
            float candidateBBoxHeight,
            float candidateTopGap,
            float[] referenceKeypoints,
            float referenceCenterX,
            float referenceBBoxWidth,
            float referenceBottomGap,
            float referenceBBoxHeight,
            float referenceTopGap,
            out int keypointCount,
            out int excludedKeypointCount,
            out float l1Delta,
            out float maxL1Delta,
            out int maxKeypointIndex,
            out float maxXDelta,
            out float maxYDelta,
            out float maxCandidateX,
            out float maxCandidateY,
            out float maxReferenceX,
            out float maxReferenceY)
        {
            keypointCount = 0;
            excludedKeypointCount = 0;
            l1Delta = float.NaN;
            maxL1Delta = float.NaN;
            maxKeypointIndex = -1;
            maxXDelta = float.NaN;
            maxYDelta = float.NaN;
            maxCandidateX = float.NaN;
            maxCandidateY = float.NaN;
            maxReferenceX = float.NaN;
            maxReferenceY = float.NaN;
            if (candidateKeypoints == null ||
                referenceKeypoints == null ||
                !IsFiniteMetric(candidateCenterX) ||
                !IsFiniteMetric(candidateBBoxWidth) ||
                !IsFiniteMetric(candidateBottomGap) ||
                !IsFiniteMetric(candidateBBoxHeight) ||
                !IsFiniteMetric(candidateTopGap) ||
                !IsFiniteMetric(referenceCenterX) ||
                !IsFiniteMetric(referenceBBoxWidth) ||
                !IsFiniteMetric(referenceBottomGap) ||
                !IsFiniteMetric(referenceBBoxHeight) ||
                !IsFiniteMetric(referenceTopGap) ||
                candidateBBoxWidth <= 0f ||
                candidateBBoxHeight <= 0f ||
                referenceBBoxWidth <= 0f ||
                referenceBBoxHeight <= 0f)
            {
                return false;
            }

            int length = Mathf.Min(candidateKeypoints.Length, referenceKeypoints.Length);
            if (length <= 1)
            {
                return false;
            }

            int totalKeypointCount = length / 2;
            int bandCount = Mathf.Max(0, (totalKeypointCount - 2) / 2);
            float candidateLeft = candidateCenterX - (candidateBBoxWidth * 0.5f);
            float referenceLeft = referenceCenterX - (referenceBBoxWidth * 0.5f);
            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteKeypointCount = 0;
            int excludedCount = 0;
            for (int i = 0; i + 1 < length; i += 2)
            {
                int keypointIndex = i / 2;
                float candidateX = candidateKeypoints[i];
                float candidateY = candidateKeypoints[i + 1];
                float referenceX = referenceKeypoints[i];
                float referenceY = referenceKeypoints[i + 1];
                if (!IsFiniteMetric(candidateX) ||
                    !IsFiniteMetric(candidateY) ||
                    !IsFiniteMetric(referenceX) ||
                    !IsFiniteMetric(referenceY))
                {
                    continue;
                }

                if (IsKeypointAffectedByVerticalFrameEdge(
                        keypointIndex,
                        bandCount,
                        referenceBottomGap,
                        referenceTopGap) ||
                    IsKeypointAffectedByVerticalFrameEdge(
                        keypointIndex,
                        bandCount,
                        candidateBottomGap,
                        candidateTopGap))
                {
                    excludedCount++;
                    continue;
                }

                float candidateNormalizedX = (candidateX - candidateLeft) / candidateBBoxWidth;
                float candidateNormalizedY = (candidateY - candidateBottomGap) / candidateBBoxHeight;
                float referenceNormalizedX = (referenceX - referenceLeft) / referenceBBoxWidth;
                float referenceNormalizedY = (referenceY - referenceBottomGap) / referenceBBoxHeight;
                float delta =
                    Mathf.Abs(candidateNormalizedX - referenceNormalizedX) +
                    Mathf.Abs(candidateNormalizedY - referenceNormalizedY);
                sumDelta += delta;
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                    maxKeypointIndex = keypointIndex;
                    maxXDelta = Mathf.Abs(candidateNormalizedX - referenceNormalizedX);
                    maxYDelta = Mathf.Abs(candidateNormalizedY - referenceNormalizedY);
                    maxCandidateX = candidateNormalizedX;
                    maxCandidateY = candidateNormalizedY;
                    maxReferenceX = referenceNormalizedX;
                    maxReferenceY = referenceNormalizedY;
                }
                finiteKeypointCount++;
            }

            if (finiteKeypointCount <= 0)
            {
                excludedKeypointCount = excludedCount;
                return false;
            }

            keypointCount = finiteKeypointCount;
            excludedKeypointCount = excludedCount;
            l1Delta = sumDelta / finiteKeypointCount;
            maxL1Delta = maxDelta;
            return true;
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

    }
}
#endif
