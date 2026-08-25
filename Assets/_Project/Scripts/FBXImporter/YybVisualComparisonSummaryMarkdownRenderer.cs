using System;
using System.Globalization;
using System.Text;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonSummaryMarkdownRenderer
    {
        internal static string RenderHeader(YybVisualComparisonSummaryData summary)
        {
            if (summary == null)
            {
                throw new ArgumentNullException(nameof(summary));
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# YYB Visual Comparison Batch");
            builder.AppendLine();
            builder.AppendLine($"- session id: `{EscapeMarkdown(summary.session_id)}`");
            builder.AppendLine($"- generated at: `{FormatGeneratedAt(summary.generated_at)}`");
            builder.AppendLine($"- fbx file: `{EscapeMarkdown(summary.fbx_file)}`");
            builder.AppendLine($"- duration seconds: `{summary.duration_seconds:F2}`");
            builder.AppendLine($"- target frames: `{summary.target_frame_count}`");
            builder.AppendLine($"- segment: `{EscapeMarkdown(summary.segment)}`");
            builder.AppendLine($"- finger closeups: `{summary.finger_closeups}`");
            builder.AppendLine($"- recorder parent IK offsets (center-parented): `{summary.recorder_parent_ik_offsets_when_center_parented}`");
            builder.AppendLine($"- MMD IK delta guard runtime override VMD: `{FormatRuntimeOverride(summary.mmd_ik_delta_guard_limit_override_vmd)}`");
            builder.AppendLine($"- MMD IK delta guard recovery trigger VMD: `{FormatRuntimeOverride(summary.mmd_ik_delta_guard_recovery_trigger_vmd)}`");
            builder.AppendLine($"- MMD IK delta guard recovery debt VMD: `{FormatRuntimeOverride(summary.mmd_ik_delta_guard_recovery_debt_vmd)}`");
            builder.AppendLine($"- MMD IK delta guard recovery hold frames: `{FormatRuntimeOverride(summary.mmd_ik_delta_guard_recovery_hold_frames)}`");
            builder.AppendLine($"- Final IK foot grounding runtime override: `{summary.final_ik_foot_grounding_enabled}`");
            builder.AppendLine($"- Manual Animator lower-body localRotation runtime override: `{summary.manual_animator_foot_local_rotation_enabled}`");
            builder.AppendLine($"- Manual Animator lower-body localRotation runtime disable: `{summary.manual_animator_foot_local_rotation_disabled}`");
            builder.AppendLine($"- Manual Animator full-body pose runtime override: `{summary.manual_animator_full_body_pose_enabled}`");
            builder.AppendLine($"- Manual Animator full-body pose runtime disable: `{summary.manual_animator_full_body_pose_disabled}`");
            builder.AppendLine($"- Manual Animator full-body pose weight: `{summary.manual_animator_full_body_pose_weight:F3}`");
            builder.AppendLine($"- Manual Animator full-body pose exclude lower-body muscles: `{summary.manual_animator_full_body_pose_exclude_lower_body_muscles}`");
            builder.AppendLine($"- Manual Animator full-body pose lower-body muscles only: `{summary.manual_animator_full_body_pose_lower_body_muscles_only}`");
            builder.AppendLine($"- Manual Animator full-body pose leg twist muscles only: `{summary.manual_animator_full_body_pose_leg_twist_muscles_only}`");
            builder.AppendLine($"- Manual Animator full-body pose right arm muscles only: `{summary.manual_animator_full_body_pose_right_arm_muscles_only}`");
            builder.AppendLine($"- Manual Animator full-body pose left arm muscles only: `{summary.manual_animator_full_body_pose_left_arm_muscles_only}`");
            builder.AppendLine($"- Manual Animator full-body pose right sleeve chain muscles only: `{summary.manual_animator_full_body_pose_right_sleeve_chain_muscles_only}`");
            builder.AppendLine($"- Manual Animator full-body pose frame gate: `{summary.manual_animator_full_body_pose_frame_gate_start:F1}-{summary.manual_animator_full_body_pose_frame_gate_end:F1}`");
            builder.AppendLine($"- SetHumanPose right leg twist output reference: `{summary.set_human_pose_right_leg_twist_output_enabled}`");
            builder.AppendLine($"- SetHumanPose right leg twist output reference weight: `{summary.set_human_pose_right_leg_twist_output_weight:F3}`");
            builder.AppendLine($"- SetHumanPose right leg twist output reference max delta: `{summary.set_human_pose_right_leg_twist_output_max_delta:F3}`");
            builder.AppendLine($"- Manual Animator body rotation runtime override: `{summary.manual_animator_body_rotation_enabled}`");
            builder.AppendLine($"- Manual Animator body rotation runtime disable: `{summary.manual_animator_body_rotation_disabled}`");
            builder.AppendLine($"- Manual Animator body rotation weight: `{summary.manual_animator_body_rotation_weight:F3}`");
            builder.AppendLine($"- Manual Animator hand local rotation runtime override: `{summary.manual_animator_hand_local_rotation_enabled}`");
            builder.AppendLine($"- Manual Animator thumb local rotation runtime override: `{summary.manual_animator_thumb_local_rotation_enabled}`");
            builder.AppendLine($"- Manual Animator hand palm-frame runtime override: `{summary.manual_animator_hand_palm_frame_enabled}`");
            builder.AppendLine($"- Manual Animator hand palm-frame weight: `{summary.manual_animator_hand_palm_frame_weight:F3}`");
            builder.AppendLine($"- Retarget pose visual spike smoothing runtime settings override: `{summary.retarget_pose_visual_spike_smoothing_override}`");
            builder.AppendLine($"- Retarget pose visual spike smoothing enabled: `{summary.retarget_pose_visual_spike_smoothing_enabled}`");
            builder.AppendLine($"- Retarget pose visual spike current weight: `{summary.retarget_pose_visual_spike_current_weight:F3}`");
            builder.AppendLine($"- Retarget pose visual spike forearm stretch clamp max offset: `{summary.retarget_pose_visual_spike_forearm_stretch_clamp_max_offset:F3}`");
            builder.AppendLine($"- Retarget arm stretch clamp runtime override: `{summary.retarget_arm_stretch_clamp_enabled}`");
            builder.AppendLine($"- Retarget arm stretch muscle limit: `{summary.retarget_arm_stretch_muscle_limit:F3}`");
            builder.AppendLine($"- YYB arm swing limit runtime override: `{summary.yyb_arm_swing_limit_enabled}`");
            builder.AppendLine($"- YYB arm swing limit weight: `{summary.yyb_arm_swing_limit_weight:F3}`");
            builder.AppendLine($"- YYB arm swing max down dot: `{summary.yyb_arm_swing_max_down_dot:F3}`");
            builder.AppendLine($"- YYB arm swing min hand horizontal ratio: `{summary.yyb_arm_swing_min_hand_horizontal_ratio:F3}`");
            builder.AppendLine($"- YYB arm swing max hand below shoulder ratio: `{summary.yyb_arm_swing_max_hand_below_shoulder_ratio:F3}`");
            builder.AppendLine($"- YYB arm swing horizontal reach limit weight: `{summary.yyb_arm_swing_horizontal_reach_limit_weight:F3}`");
            builder.AppendLine($"- YYB arm swing max hand horizontal reach ratio: `{summary.yyb_arm_swing_max_hand_horizontal_reach_ratio:F3}`");
            builder.AppendLine($"- YYB arm swing horizontal reach max hand below shoulder ratio: `{summary.yyb_arm_swing_horizontal_reach_max_hand_below_shoulder_ratio:F3}`");
            builder.AppendLine($"- YYB arm swing horizontal reach min elbow angle after apply: `{summary.yyb_arm_swing_horizontal_reach_min_elbow_angle_after_apply:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose horizontal reach limit weight: `{summary.yyb_arm_swing_raised_pose_horizontal_reach_limit_weight:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose min upper-arm down dot: `{summary.yyb_arm_swing_raised_pose_min_upper_arm_down_dot:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose max hand below shoulder ratio: `{summary.yyb_arm_swing_raised_pose_max_hand_below_shoulder_ratio:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose max hand horizontal reach ratio: `{summary.yyb_arm_swing_raised_pose_max_hand_horizontal_reach_ratio:F3}`");
            builder.AppendLine($"- YYB arm direction retarget runtime override: `{summary.yyb_arm_direction_retarget_enabled}`");
            builder.AppendLine($"- YYB arm direction upper-arm weight: `{summary.yyb_arm_direction_upper_arm_weight:F3}`");
            builder.AppendLine($"- YYB arm direction forearm weight: `{summary.yyb_arm_direction_forearm_weight:F3}`");
            builder.AppendLine($"- YYB arm direction upper-arm max degrees: `{summary.yyb_arm_direction_upper_arm_max_degrees:F3}`");
            builder.AppendLine($"- YYB arm direction forearm max degrees: `{summary.yyb_arm_direction_forearm_max_degrees:F3}`");
            builder.AppendLine($"- YYB arm direction left-side weight scale: `{summary.yyb_arm_direction_left_side_weight_scale:F3}`");
            builder.AppendLine($"- YYB arm direction right-side weight scale: `{summary.yyb_arm_direction_right_side_weight_scale:F3}`");
            builder.AppendLine($"- YYB arm sleeve anchor runtime settings override: `{summary.yyb_arm_sleeve_anchor_override}`");
            builder.AppendLine($"- YYB arm sleeve anchor runtime enabled: `{summary.yyb_arm_sleeve_anchor_enabled}`");
            builder.AppendLine($"- YYB arm sleeve anchor influence: `{summary.yyb_arm_sleeve_anchor_influence:F3}`");
            builder.AppendLine($"- YYB arm shoulder cap anchor influence: `{summary.yyb_arm_shoulder_cap_anchor_influence:F3}`");
            builder.AppendLine($"- YYB arm sleeve anchor max degrees: `{summary.yyb_arm_sleeve_anchor_max_degrees:F3}`");
            builder.AppendLine($"- YYB arm visual twist runtime settings override: `{summary.yyb_arm_visual_twist_override}`");
            builder.AppendLine($"- YYB arm visual twist runtime enabled: `{summary.yyb_arm_visual_twist_enabled}`");
            builder.AppendLine($"- YYB arm visual upper-arm influence: `{summary.yyb_arm_visual_upper_arm_influence:F3}`");
            builder.AppendLine($"- YYB arm visual forearm influence: `{summary.yyb_arm_visual_forearm_influence:F3}`");
            builder.AppendLine($"- YYB arm visual upper-arm max degrees: `{summary.yyb_arm_visual_upper_arm_max_degrees:F3}`");
            builder.AppendLine($"- YYB arm visual forearm max degrees: `{summary.yyb_arm_visual_forearm_max_degrees:F3}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction runtime override: `{summary.manual_animator_lower_body_segment_direction_enabled}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction runtime disable: `{summary.manual_animator_lower_body_segment_direction_disabled}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction weight: `{summary.manual_animator_lower_body_segment_direction_weight:F3}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction max angle: `{summary.manual_animator_lower_body_segment_direction_max_angle:F3}`");
            builder.AppendLine($"- Manual Animator UpperLegToLowerLeg segment direction runtime disable: `{summary.manual_animator_upper_leg_to_lower_leg_segment_direction_disabled}`");
            builder.AppendLine($"- Manual Animator UpperLegToLowerLeg segment direction max angle override: `{summary.manual_animator_upper_leg_to_lower_leg_segment_direction_max_angle:F3}`");
            builder.AppendLine($"- Manual Animator LowerLegToFoot segment direction runtime disable: `{summary.manual_animator_lower_leg_to_foot_segment_direction_disabled}`");
            builder.AppendLine($"- Manual Animator LowerLegToFoot segment direction max angle override: `{summary.manual_animator_lower_leg_to_foot_segment_direction_max_angle:F3}`");
            builder.AppendLine($"- Manual Animator Left LowerLegToFoot segment direction max angle override: `{summary.manual_animator_left_lower_leg_to_foot_segment_direction_max_angle:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction max angle override: `{summary.manual_animator_right_lower_leg_to_foot_segment_direction_max_angle:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction axis X/Z scale: `{summary.manual_animator_right_lower_leg_to_foot_segment_direction_axis_xz_scale:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction blend weight: `{summary.manual_animator_right_lower_leg_to_foot_segment_direction_blend_weight:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction frame gate: `{summary.manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_start:F0}-{summary.manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_end:F0}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction endpoint blend weight: `{summary.manual_animator_right_lower_leg_to_foot_segment_direction_endpoint_blend_weight:F3}`");
            builder.AppendLine($"- Manual Animator FootToToes segment direction runtime disable: `{summary.manual_animator_foot_to_toes_segment_direction_disabled}`");
            builder.AppendLine($"- Manual Animator FootToToes segment direction max angle override: `{summary.manual_animator_foot_to_toes_segment_direction_max_angle:F3}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw runtime override: `{summary.manual_animator_foot_hips_aligned_residual_yaw_enabled}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw runtime disable: `{summary.manual_animator_foot_hips_aligned_residual_yaw_disabled}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw weight: `{summary.manual_animator_foot_hips_aligned_residual_yaw_weight:F3}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw max angle: `{summary.manual_animator_foot_hips_aligned_residual_yaw_max_angle:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position runtime override: `{summary.post_set_human_pose_right_endpoint_position_enabled}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position weight: `{summary.post_set_human_pose_right_endpoint_position_weight:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position max offset: `{summary.post_set_human_pose_right_endpoint_position_max_offset:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position positive-Z scale: `{summary.post_set_human_pose_right_endpoint_position_positive_z_scale:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position toes blend weight: `{summary.post_set_human_pose_right_endpoint_position_toes_blend_weight:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position frame gate: `{summary.post_set_human_pose_right_endpoint_position_frame_gate_start:F0}-{summary.post_set_human_pose_right_endpoint_position_frame_gate_end:F0}`");
            builder.AppendLine($"- Post-SetHumanPose endpoint position use left side: `{summary.post_set_human_pose_endpoint_position_use_left_side}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position runtime override: `{summary.pre_set_human_pose_right_endpoint_position_enabled}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position weight: `{summary.pre_set_human_pose_right_endpoint_position_weight:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position max offset: `{summary.pre_set_human_pose_right_endpoint_position_max_offset:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position positive-Z scale: `{summary.pre_set_human_pose_right_endpoint_position_positive_z_scale:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position toes blend weight: `{summary.pre_set_human_pose_right_endpoint_position_toes_blend_weight:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position frame gate: `{summary.pre_set_human_pose_right_endpoint_position_frame_gate_start:F0}-{summary.pre_set_human_pose_right_endpoint_position_frame_gate_end:F0}`");
            builder.AppendLine($"- Pre-SetHumanPose endpoint position use left side: `{summary.pre_set_human_pose_endpoint_position_use_left_side}`");
            builder.AppendLine($"- Pre-SetHumanPose endpoint position use ghost/current basis: `{summary.pre_set_human_pose_endpoint_position_use_ghost_current_basis}`");
            builder.AppendLine($"- Pre-SetHumanPose endpoint bodyPosition invert X/Z: `{summary.pre_set_human_pose_endpoint_position_invert_body_position_x}/{summary.pre_set_human_pose_endpoint_position_invert_body_position_z}`");
            builder.AppendLine($"- Post-SetHumanPose right foot evaluator X/Z reference: `{summary.post_set_human_pose_right_foot_evaluator_xz_reference_enabled}`");
            builder.AppendLine($"- Post-SetHumanPose right foot evaluator X/Z target magnitude: `{summary.post_set_human_pose_right_foot_evaluator_xz_reference_target_magnitude:F3}`");
            builder.AppendLine($"- Manual Animator BipedIK foot position runtime override: `{summary.manual_animator_biped_ik_foot_position_enabled}`");
            builder.AppendLine($"- Manual Animator BipedIK foot position weight: `{summary.manual_animator_biped_ik_foot_position_weight:F3}`");
            builder.AppendLine($"- Manual Animator BipedIK foot position max offset: `{summary.manual_animator_biped_ik_foot_position_max_offset:F3}`");
            builder.AppendLine($"- Manual Animator Hips local-position runtime override: `{summary.manual_animator_hips_local_position_enabled}`");
            builder.AppendLine($"- Manual Animator Hips local-position weight: `{summary.manual_animator_hips_local_position_weight:F3}`");
            builder.AppendLine($"- Manual Animator Hips local-position max offset: `{summary.manual_animator_hips_local_position_max_offset:F3}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z runtime override: `{summary.manual_animator_body_position_xz_enabled}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z weight: `{summary.manual_animator_body_position_xz_weight:F3}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z max offset: `{summary.manual_animator_body_position_xz_max_offset:F3}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z frame gate: `{summary.manual_animator_body_position_xz_frame_gate_start:F0}-{summary.manual_animator_body_position_xz_frame_gate_end:F0}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z frame gate blend frames: `{summary.manual_animator_body_position_xz_frame_gate_blend_frames:F0}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z axis scale: `{summary.manual_animator_body_position_xz_axis_x_scale:F3}/{summary.manual_animator_body_position_xz_axis_z_scale:F3}`");
            builder.AppendLine($"- Retarget bodyPosition X/Z root motion runtime override: `{summary.retarget_body_position_xz_root_motion_enabled}`");
            builder.AppendLine($"- Target humanoid bone position lock disabled runtime override: `{summary.target_humanoid_bone_position_lock_disabled}`");
            builder.AppendLine($"- VMD playback probe runtime override: `{summary.vmd_playback_probe_enabled}`");
            builder.AppendLine($"- VMD playback probe apply IK targets: `{summary.vmd_playback_probe_apply_ik_targets}`");
            builder.AppendLine($"- VMD playback probe source VMD: `{EscapeMarkdown(summary.vmd_playback_probe_source_vmd_path)}`");
            builder.AppendLine($"- reference MMD timing runtime override: `{summary.reference_mmd_timing_enabled}`");
            builder.AppendLine($"- diagnostic capture width override: `{FormatRuntimeOverride(summary.diagnostic_capture_width_override)}`");
            builder.AppendLine($"- diagnostic capture height override: `{FormatRuntimeOverride(summary.diagnostic_capture_height_override)}`");
            builder.AppendLine($"- diagnostic screenshot padding override: `{FormatFramingOverride(summary.diagnostic_screenshot_padding_override)}`");
            builder.AppendLine($"- diagnostic screenshot viewport center override: `{FormatFramingOverride(summary.diagnostic_screenshot_vertical_viewport_center_override)}`");
            builder.AppendLine($"- reference clip: `{EscapeMarkdown(summary.reference_clip_name)}`");
            builder.AppendLine($"- reference clip asset: `{EscapeMarkdown(summary.reference_clip_asset_path)}`");
            builder.AppendLine();
            return builder.ToString();
        }

        private static string FormatGeneratedAt(string generatedAt)
        {
            return DateTime.TryParse(
                generatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime parsed)
                ? parsed.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : generatedAt ?? string.Empty;
        }

        private static string FormatRuntimeOverride(float value)
        {
            return VmdIkDeltaGuardRuntimeOverrideApplier.HasLimit(value)
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : "none";
        }

        private static string FormatRuntimeOverride(int value)
        {
            return value > 0 ? value.ToString(CultureInfo.InvariantCulture) : "none";
        }

        private static string FormatFramingOverride(float value)
        {
            return VisualComparisonScreenshotOverridePolicy.HasFiniteFramingOverride(value)
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : "none";
        }

        private static string EscapeMarkdown(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("|", "\\|");
        }
    }
}
