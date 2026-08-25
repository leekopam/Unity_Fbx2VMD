using System;

namespace Fbx2Vmd.FBXImporter
{
    internal static class ReferenceVideoDiagnosticsMapper
    {
        public static void Initialize(
            VisualComparisonFrameRoleDiagnosticsData diagnostics,
            float referenceClipStartSeconds,
            float requestedDurationSeconds,
            string provenanceEvidencePath,
            string analysisResultPath,
            string frameMetricsPath,
            string contactSheetPath)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            diagnostics.reference_mp4_provenance_evidence_path = provenanceEvidencePath ?? string.Empty;
            diagnostics.reference_mp4_analysis_result_path = analysisResultPath ?? string.Empty;
            diagnostics.reference_mp4_frame_metrics_path = frameMetricsPath ?? string.Empty;
            diagnostics.reference_mp4_contact_sheet_path = contactSheetPath ?? string.Empty;
            diagnostics.reference_mp4_current_clip_start_seconds = Math.Max(0f, referenceClipStartSeconds);
            diagnostics.reference_mp4_current_clip_duration_seconds = Math.Max(0f, requestedDurationSeconds);
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
        }

        public static void Apply(
            VisualComparisonFrameRoleDiagnosticsData diagnostics,
            ReferenceVideoDiagnosticsData referenceVideo,
            ReferenceVideoClipCoverageData coverage,
            bool provenanceEvidenceExists,
            bool contactSheetExists)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            if (referenceVideo == null)
            {
                throw new ArgumentNullException(nameof(referenceVideo));
            }

            if (coverage == null)
            {
                throw new ArgumentNullException(nameof(coverage));
            }

            diagnostics.reference_mp4_provenance_evidence_exists = provenanceEvidenceExists;
            diagnostics.reference_mp4_contact_sheet_exists = contactSheetExists;
            diagnostics.reference_mp4_analysis_result_exists = referenceVideo.AnalysisFileExists;
            diagnostics.reference_mp4_analysis_error = referenceVideo.AnalysisError;
            diagnostics.reference_mp4_analysis_schema = referenceVideo.AnalysisSchema;
            diagnostics.reference_mp4_extracted_frame_count = referenceVideo.ExtractedFrameCount;
            diagnostics.reference_mp4_width = referenceVideo.VideoWidth;
            diagnostics.reference_mp4_height = referenceVideo.VideoHeight;
            diagnostics.reference_mp4_avg_frame_rate = referenceVideo.AverageFrameRate;
            diagnostics.reference_mp4_stream_duration_seconds = referenceVideo.StreamDurationSeconds;
            diagnostics.reference_mp4_total_video_frames = referenceVideo.TotalVideoFrames;
            diagnostics.reference_mp4_frame_metrics_exists = referenceVideo.FrameMetricsFileExists;
            diagnostics.reference_mp4_frame_metrics_error = referenceVideo.FrameMetricsError;
            diagnostics.reference_mp4_frame_metrics_schema = referenceVideo.FrameMetricsSchema;
            diagnostics.reference_mp4_frame_metrics_sample_count = referenceVideo.FrameMetricsSampleCount;
            diagnostics.reference_mp4_frame_metrics_extracted_frame_count =
                referenceVideo.FrameMetricsExtractedFrameCount;
            diagnostics.reference_mp4_avg_bbox_height_ratio = referenceVideo.AverageBBoxHeightRatio;
            diagnostics.reference_mp4_avg_bbox_width_ratio = referenceVideo.AverageBBoxWidthRatio;
            diagnostics.reference_mp4_center_x_range_ratio = referenceVideo.CenterXRangeRatio;
            diagnostics.reference_mp4_max_bottom_gap_ratio = referenceVideo.MaxBottomGapRatio;
            diagnostics.reference_mp4_avg_bright_area_ratio = referenceVideo.AverageBrightAreaRatio;
            diagnostics.reference_mp4_current_clip_end_seconds = coverage.EndSeconds;
            diagnostics.reference_mp4_current_clip_sample_gap_seconds = coverage.SampleGapSeconds;
            if (diagnostics.reference_mp4_current_clip_duration_seconds <= 0f)
            {
                return;
            }

            diagnostics.reference_mp4_current_clip_sample_count = coverage.SampleCount;
            diagnostics.reference_mp4_current_clip_sample_seconds = coverage.SampleSeconds;
            if (coverage.SampleCount <= 0)
            {
                return;
            }

            diagnostics.reference_mp4_current_clip_first_sample_seconds = coverage.FirstSampleSeconds;
            diagnostics.reference_mp4_current_clip_last_sample_seconds = coverage.LastSampleSeconds;
            diagnostics.reference_mp4_current_clip_sample_coverage_ratio = coverage.SampleCoverageRatio;
            diagnostics.reference_mp4_current_clip_avg_bbox_height_ratio = coverage.AverageBBoxHeightRatio;
            diagnostics.reference_mp4_current_clip_avg_bbox_width_ratio = coverage.AverageBBoxWidthRatio;
            diagnostics.reference_mp4_current_clip_center_x_range_ratio = coverage.CenterXRangeRatio;
            diagnostics.reference_mp4_current_clip_max_bottom_gap_ratio = coverage.MaxBottomGapRatio;
            diagnostics.reference_mp4_current_clip_avg_bright_area_ratio = coverage.AverageBrightAreaRatio;
        }
    }
}
