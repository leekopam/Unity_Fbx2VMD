using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonSampleOrderingDiagnosticBuilder
    {
        public static void Populate(
            VisualComparisonSampleOrderingDiagnosticData diagnostic,
            string jobMode,
            string sceneName,
            string metricsCsvPath,
            string projectRoot)
        {
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            diagnostic.job_mode = jobMode ?? string.Empty;
            diagnostic.scene_name = sceneName ?? string.Empty;
            diagnostic.metrics_csv = metricsCsvPath ?? string.Empty;
            string absolutePath = VisualComparisonArtifactPathResolver.ToAbsoluteProjectPath(metricsCsvPath, projectRoot);
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return;
            }

            string[] lines = File.ReadAllLines(absolutePath, Encoding.UTF8);
            if (lines.Length <= 1)
            {
                return;
            }

            string[] headers = VisualComparisonCsvMetricReader.SplitLine(lines[0]);
            Dictionary<string, int> indices = VisualComparisonCsvMetricReader.BuildIndexMap(headers);
            List<string[]> rows = new List<string[]>();
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                if (string.IsNullOrWhiteSpace(lines[lineIndex]))
                {
                    continue;
                }

                rows.Add(VisualComparisonCsvMetricReader.SplitLine(lines[lineIndex]));
            }

            diagnostic.metric_row_count = rows.Count;
            if (rows.Count == 0)
            {
                return;
            }

            string[] first = rows[0];
            string[] finish = rows.LastOrDefault(row =>
                string.Equals(VisualComparisonCsvMetricReader.ReadString(row, indices, "reason"), "finish", StringComparison.OrdinalIgnoreCase))
                ?? rows[rows.Count - 1];

            diagnostic.first_metric_reason = VisualComparisonCsvMetricReader.ReadString(first, indices, "reason");
            diagnostic.first_metric_recorder_frame = VisualComparisonCsvMetricReader.ReadInt(first, indices, "recorderFrame");
            diagnostic.first_metric_engine_frame_count = VisualComparisonCsvMetricReader.ReadInt(first, indices, "frameCount");
            diagnostic.first_metric_time_since_level_load = VisualComparisonCsvMetricReader.ReadFloat(first, indices, "timeSinceLevelLoad");
            diagnostic.first_metric_animation_clip_time = VisualComparisonCsvMetricReader.ReadFloat(first, indices, "animationClipTime");
            diagnostic.first_metric_grounding_vertical_step_last = VisualComparisonCsvMetricReader.ReadFloat(first, indices, "retargetGroundingVerticalStepLast");
            diagnostic.first_metric_grounding_initial_vertical_step = VisualComparisonCsvMetricReader.ReadFloat(first, indices, "retargetGroundingInitialVerticalStep");
            diagnostic.first_metric_grounding_step_clamp_count = VisualComparisonCsvMetricReader.ReadInt(first, indices, "retargetGroundingStepClampCount");
            diagnostic.first_metric_grounding_smoothed_count = VisualComparisonCsvMetricReader.ReadInt(first, indices, "retargetGroundingSmoothedCount");
            diagnostic.first_metric_grounding_max_step_per_frame = VisualComparisonCsvMetricReader.ReadFloat(first, indices, "retargetGroundingMaxStepPerFrame");
            diagnostic.first_metric_grounding_vertical_step_to_max_ratio = ResolveGroundingStepToMaxRatio(
                first,
                indices,
                diagnostic.first_metric_grounding_vertical_step_last,
                diagnostic.first_metric_grounding_max_step_per_frame);
            diagnostic.first_metric_grounding_vertical_step_at_max_step =
                VisualComparisonMetricCalculator.IsGroundingStepAtMax(diagnostic.first_metric_grounding_vertical_step_to_max_ratio);
            diagnostic.finish_metric_reason = VisualComparisonCsvMetricReader.ReadString(finish, indices, "reason");
            diagnostic.finish_metric_recorder_frame = VisualComparisonCsvMetricReader.ReadInt(finish, indices, "recorderFrame");
            diagnostic.finish_metric_engine_frame_count = VisualComparisonCsvMetricReader.ReadInt(finish, indices, "frameCount");
            diagnostic.finish_metric_time_since_level_load = VisualComparisonCsvMetricReader.ReadFloat(finish, indices, "timeSinceLevelLoad");
            diagnostic.finish_metric_animation_clip_time = VisualComparisonCsvMetricReader.ReadFloat(finish, indices, "animationClipTime");
            diagnostic.finish_metric_grounding_vertical_step_last = VisualComparisonCsvMetricReader.ReadFloat(finish, indices, "retargetGroundingVerticalStepLast");
            diagnostic.finish_metric_grounding_step_clamp_count = VisualComparisonCsvMetricReader.ReadInt(finish, indices, "retargetGroundingStepClampCount");
            diagnostic.finish_metric_grounding_smoothed_count = VisualComparisonCsvMetricReader.ReadInt(finish, indices, "retargetGroundingSmoothedCount");
            diagnostic.finish_metric_grounding_max_step_per_frame = VisualComparisonCsvMetricReader.ReadFloat(finish, indices, "retargetGroundingMaxStepPerFrame");
            diagnostic.finish_metric_grounding_vertical_step_to_max_ratio = ResolveGroundingStepToMaxRatio(
                finish,
                indices,
                diagnostic.finish_metric_grounding_vertical_step_last,
                diagnostic.finish_metric_grounding_max_step_per_frame);
            diagnostic.finish_metric_grounding_vertical_step_at_max_step =
                VisualComparisonMetricCalculator.IsGroundingStepAtMax(diagnostic.finish_metric_grounding_vertical_step_to_max_ratio);
            diagnostic.recording_metric_recorder_frame_span = VisualComparisonMetricCalculator.CalculateIntSpan(
                diagnostic.first_metric_recorder_frame,
                diagnostic.finish_metric_recorder_frame);
            diagnostic.recording_metric_engine_frame_span = VisualComparisonMetricCalculator.CalculateIntSpan(
                diagnostic.first_metric_engine_frame_count,
                diagnostic.finish_metric_engine_frame_count);
            diagnostic.recording_metric_time_since_level_load_span = VisualComparisonMetricCalculator.CalculateFloatSpan(
                diagnostic.first_metric_time_since_level_load,
                diagnostic.finish_metric_time_since_level_load);
            diagnostic.recording_grounding_step_clamp_delta = VisualComparisonMetricCalculator.CalculateIntSpan(
                diagnostic.first_metric_grounding_step_clamp_count,
                diagnostic.finish_metric_grounding_step_clamp_count);
            diagnostic.recording_grounding_smoothed_delta = VisualComparisonMetricCalculator.CalculateIntSpan(
                diagnostic.first_metric_grounding_smoothed_count,
                diagnostic.finish_metric_grounding_smoothed_count);
            diagnostic.recording_phase_span_role =
                "finish-first recording phase metrics; absolute first engine frame includes scene load/import/prewarm startup offset and can vary between Unity batch runs";
            diagnostic.grounding_step_limit_role =
                "prewarm residual is identified by the first recorder-frame grounding step reaching its configured max; recording clamp/smoothed deltas are finish-first counters inside the captured phase";
        }
        private static float ResolveGroundingStepToMaxRatio(
            string[] row,
            Dictionary<string, int> indices,
            float step,
            float maxStep)
        {
            float reportedRatio = VisualComparisonCsvMetricReader.ReadFloat(
                row,
                indices,
                "retargetGroundingLastStepToMaxStepRatio");
            return VisualComparisonMetricCalculator.ResolveGroundingStepToMaxRatio(
                reportedRatio,
                step,
                maxStep);
        }
    }
}
