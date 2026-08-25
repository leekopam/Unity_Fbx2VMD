using System;

namespace Fbx2Vmd.FBXImporter
{
    [Serializable]
    internal class VisualComparisonSampleOrderingDiagnosticData
    {
        public string job_mode;
        public string scene_name;
        public string metrics_csv;
        public int metric_row_count;
        public string first_metric_reason;
        public int first_metric_recorder_frame;
        public int first_metric_engine_frame_count;
        public float first_metric_time_since_level_load;
        public float first_metric_animation_clip_time;
        public float first_metric_grounding_vertical_step_last;
        public float first_metric_grounding_initial_vertical_step;
        public int first_metric_grounding_step_clamp_count;
        public int first_metric_grounding_smoothed_count;
        public float first_metric_grounding_max_step_per_frame;
        public float first_metric_grounding_vertical_step_to_max_ratio;
        public bool first_metric_grounding_vertical_step_at_max_step;
        public string finish_metric_reason;
        public int finish_metric_recorder_frame;
        public int finish_metric_engine_frame_count;
        public float finish_metric_time_since_level_load;
        public float finish_metric_animation_clip_time;
        public float finish_metric_grounding_vertical_step_last;
        public int finish_metric_grounding_step_clamp_count;
        public int finish_metric_grounding_smoothed_count;
        public float finish_metric_grounding_max_step_per_frame;
        public float finish_metric_grounding_vertical_step_to_max_ratio;
        public bool finish_metric_grounding_vertical_step_at_max_step;
        public int recording_metric_recorder_frame_span;
        public int recording_metric_engine_frame_span;
        public float recording_metric_time_since_level_load_span;
        public int recording_grounding_step_clamp_delta;
        public int recording_grounding_smoothed_delta;
        public string recording_phase_span_role;
        public string grounding_step_limit_role;
    }
}
