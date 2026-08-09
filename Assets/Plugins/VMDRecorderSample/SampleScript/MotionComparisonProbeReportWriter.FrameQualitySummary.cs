using System;

public sealed partial class MotionComparisonFrameQualitySummary
{
    // ---- Identity & Status ----
    public string baseline_label;
    public string candidate_label;
    public string baseline_metrics_csv;
    public string candidate_metrics_csv;
    public string frame_quality_evaluation_role;
    public string frame_quality_evaluation_basis;
    public string candidate_vmd_path;
    public string mmd_result_status;
    public string mmd_report_path;
    public string mmd_run_dir;
    public string mmd_after_play_screenshot_path;
    public string mmd_finished_at;
    public string status;
    public string status_reason;

    // ---- Frame Counts ----
    public int baseline_metric_frames;
    public int candidate_metric_frames;
    public int compared_frames;
    public int missing_baseline_frames;
    public int missing_candidate_frames;
    public int baseline_recorded_frame_count;
    public int candidate_recorded_frame_count;
    public int target_frame_count;
    public int baseline_frame_count_delta_from_target;
    public int candidate_frame_count_delta_from_target;
    public int candidate_below_floor_metric_frames;

    // ---- Gate & YYB Risk ----
    public string floor_contact_gate_status;
    public string floor_contact_gate_status_reason;
    public string floor_contact_corrected_diagnostic_status;
    public string floor_contact_corrected_diagnostic_status_reason;
    public int candidate_root_step_spike_frames;
    public bool candidate_yyb_deformation_risk_column_present;
    public int candidate_yyb_deformation_risk_frame_count;
    public int candidate_yyb_deformation_risk_missing_frames;
    public float candidate_yyb_max_deformation_risk;
    public bool candidate_yyb_sleeve_thickness_risk_column_present;
    public int candidate_yyb_sleeve_thickness_risk_frame_count;
    public int candidate_yyb_sleeve_thickness_risk_missing_frames;
    public float candidate_yyb_max_sleeve_thickness_risk;

    // ---- VMD Bone & Motion ----
    public int candidate_vmd_bone_frames;
    public int candidate_vmd_max_bone_frame_index;
    public int candidate_vmd_center_spike_frames;
    public int candidate_vmd_foot_ik_spike_frames;
    public int candidate_arm_motion_frames;
    public int candidate_leg_motion_frames;
    public float candidate_arm_motion_root_travel;
    public float candidate_leg_motion_root_travel;
    public float candidate_limb_motion_root_travel;
    public float max_candidate_limb_motion_root_step;

    // ---- Same-Frame Limb Pose Delta ----
    public float max_same_frame_arm_pose_delta;
    public float max_same_frame_leg_pose_delta;
    public float max_same_frame_limb_pose_delta;
    public int max_same_frame_limb_pose_delta_recorder_frame;
    public int max_same_frame_limb_pose_delta_candidate_recorder_frame;
    public string max_same_frame_limb_pose_delta_source;
    public float max_same_frame_guard_normalized_arm_pose_delta;
    public float max_same_frame_guard_normalized_limb_pose_delta;
    public float max_same_frame_limb_pose_gate_delta;
    public int max_same_frame_limb_pose_gate_delta_recorder_frame;
    public int max_same_frame_limb_pose_gate_delta_candidate_recorder_frame;
    public string max_same_frame_limb_pose_gate_delta_source;
    public float max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range;
    public int max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range_recorder_frame;
    public int max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range_candidate_recorder_frame;
    public string max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range_source;
    public int raw_limb_pose_delta_saturated_frame_count;
    public float raw_limb_pose_delta_excess_over_guard_normalized;
    public string raw_limb_pose_delta_saturation_basis;

    // ---- Pre-Retarget & Foot Bottom ----
    public int pre_retarget_start_compared_frames;
    public float pre_retarget_start_max_same_frame_arm_pose_delta;
    public float pre_retarget_start_max_same_frame_limb_pose_delta;
    public float pre_retarget_start_max_same_frame_guard_normalized_arm_pose_delta;
    public float pre_retarget_start_max_same_frame_guard_normalized_limb_pose_delta;
    public int pre_retarget_start_max_same_frame_limb_pose_delta_recorder_frame;
    public int pre_retarget_start_max_same_frame_limb_pose_delta_candidate_recorder_frame;
    public string pre_retarget_start_evaluation_basis;
    public float min_baseline_foot_bottom_y;
    public float min_candidate_foot_bottom_y;
    public float min_candidate_foot_bottom_ground_gap;

    // ---- Same-Frame Hips/Root Deltas ----
    public float max_same_frame_root_position_delta;
    public float max_same_frame_root_y_delta;
    public float max_same_frame_hips_y_delta;
    public int max_same_frame_hips_y_delta_recorder_frame;
    public int max_same_frame_hips_y_delta_candidate_recorder_frame;
    public float max_same_frame_body_position_y_delta;
    public float max_same_frame_hips_local_y_delta;
    public float max_same_frame_hips_xz_delta;
    public int max_same_frame_hips_xz_delta_recorder_frame;
    public int max_same_frame_hips_xz_delta_candidate_recorder_frame;
    public float max_same_frame_grounding_vertical_step_delta;
    public float max_same_frame_foot_height_reference_lift_delta;
    public float max_same_frame_candidate_grounding_vertical_step_change;
    public float max_same_frame_candidate_foot_height_reference_lift_change;
    public float max_same_frame_hips_y_delta_root_y_component;
    public float max_same_frame_hips_y_delta_body_position_y_component;
    public float max_same_frame_hips_y_delta_hips_local_y_component;
    public float max_same_frame_hips_y_delta_foot_bottom_y_delta_at_frame;
    public float max_same_frame_foot_bottom_y_delta;
    public int max_same_frame_foot_bottom_y_delta_recorder_frame;
    public int max_same_frame_foot_bottom_y_delta_candidate_recorder_frame;

    // ---- Same-Frame Foot XZ Deltas ----
    public float max_same_frame_left_foot_xz_delta;
    public float max_same_frame_right_foot_xz_delta;
    public float max_same_frame_foot_xz_delta;
    public int max_same_frame_foot_xz_delta_recorder_frame;
    public int max_same_frame_foot_xz_delta_candidate_recorder_frame;
    public string max_same_frame_foot_xz_delta_side;
    public float max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range;
    public int max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range_recorder_frame;
    public int max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range_candidate_recorder_frame;
    public string max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range_side;
    public float max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range;
    public int max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range_recorder_frame;
    public int max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range_candidate_recorder_frame;
    public string max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range_side;
    public float max_same_frame_foot_xz_delta_after_hips_xz_alignment;
    public float max_same_frame_foot_xz_delta_after_hips_xz_alignment_x;
    public float max_same_frame_foot_xz_delta_after_hips_xz_alignment_z;
    public float max_same_frame_foot_xz_delta_after_hips_xz_alignment_angle_degrees;
    public int max_same_frame_foot_xz_delta_after_hips_xz_alignment_recorder_frame;
    public int max_same_frame_foot_xz_delta_after_hips_xz_alignment_candidate_recorder_frame;
    public string max_same_frame_foot_xz_delta_after_hips_xz_alignment_side;
    public float max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range;
    public int max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range_recorder_frame;
    public int max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range_candidate_recorder_frame;
    public string max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range_side;
    public float max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range;
    public int max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range_recorder_frame;
    public int max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range_candidate_recorder_frame;
    public string max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range_side;

    // ---- Vertical Solve Prototype ----
    public string vertical_solve_prototype_status;
    public string vertical_solve_prototype_status_reason;
    public string vertical_solve_prototype_basis;
    public float vertical_solve_prototype_max_same_frame_hips_y_delta;
    public float vertical_solve_prototype_max_same_frame_foot_bottom_y_delta;
    public float vertical_solve_prototype_max_same_frame_root_position_delta;
    public int vertical_solve_prototype_below_floor_metric_frames;
    public int vertical_solve_prototype_target_frame_count;
    public int vertical_solve_prototype_candidate_recorded_frame_count;
    public int vertical_solve_prototype_hips_correction_recorder_frame;
    public int vertical_solve_prototype_hips_correction_candidate_recorder_frame;
    public float vertical_solve_prototype_hips_correction_y;
    public int vertical_solve_prototype_foot_correction_recorder_frame;
    public int vertical_solve_prototype_foot_correction_candidate_recorder_frame;
    public float vertical_solve_prototype_foot_correction_y;

    // ---- Vertical Solve Postprocess ----
    public string vertical_solve_postprocess_metrics_csv;
    public string vertical_solve_postprocess_status;
    public string vertical_solve_postprocess_status_reason;
    public string vertical_solve_postprocess_basis;
    public float vertical_solve_postprocess_max_same_frame_hips_y_delta;
    public float vertical_solve_postprocess_max_same_frame_foot_bottom_y_delta;
    public float vertical_solve_postprocess_max_same_frame_root_position_delta;
    public int vertical_solve_postprocess_below_floor_metric_frames;
    public int vertical_solve_postprocess_target_frame_count;
    public int vertical_solve_postprocess_candidate_recorded_frame_count;
    public int vertical_solve_postprocess_corrected_metric_frames;

    // ---- Vertical Solve Corrected Candidate ----
    public string vertical_solve_corrected_candidate_metrics_csv;
    public string vertical_solve_corrected_candidate_vmd_path;
    public string vertical_solve_corrected_candidate_manifest_path;
    public string vertical_solve_corrected_candidate_status;
    public string vertical_solve_corrected_candidate_status_reason;
    public string vertical_solve_corrected_candidate_basis;
    public float vertical_solve_corrected_candidate_max_same_frame_hips_y_delta;
    public float vertical_solve_corrected_candidate_max_same_frame_foot_bottom_y_delta;
    public float vertical_solve_corrected_candidate_max_same_frame_root_position_delta;
    public int vertical_solve_corrected_candidate_below_floor_metric_frames;
    public int vertical_solve_corrected_candidate_target_frame_count;
    public int vertical_solve_corrected_candidate_recorded_frame_count;
    public int vertical_solve_corrected_candidate_corrected_metric_frames;

    // ---- Recording Start & Max Values ----
    public float candidate_foot_height_reference_lift_max;
    public int candidate_first_recorded_recorder_frame;
    public float candidate_first_recorded_root_y;
    public float candidate_first_recorded_body_position_y;
    public float candidate_first_recorded_hips_local_y;
    public float candidate_first_recorded_hips_y;
    public float candidate_recording_start_root_y;
    public float candidate_recording_start_body_position_y;
    public float candidate_recording_start_hips_local_y;
    public float candidate_recording_start_hips_y;
    public float candidate_recording_start_hips_reference_before_local_y;
    public float candidate_recording_start_hips_reference_after_local_y;
    public float candidate_recording_start_hips_reference_delta_y;
    public int candidate_recording_start_hips_reference_flip_detected;
    public string candidate_recording_start_hips_reference_stage;
    public string same_frame_hips_y_contribution_basis;
    public float max_candidate_root_step;
    public float candidate_retarget_root_delta_max;
    public float candidate_retarget_pose_delta_max;
    public float candidate_grounding_vertical_step_max;
    public float max_candidate_vmd_center_step;
    public float max_candidate_vmd_foot_ik_step;
    public float min_candidate_vmd_foot_ik_y;
    public float min_candidate_vmd_effective_foot_ik_y;
}
