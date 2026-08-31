#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonRequestOptionsMapper
    {
        private const float DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight = 1f;
        private const float DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = 6.2f;
        private const float DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = 0f;
        private const float DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = 0f;
        private const float DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = 0f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = 0f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = 1f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = 0.125f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = 0f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = 0f;
        private const float DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = 1f;
        private const float DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = 0f;
        private const float DefaultManualAnimatorBodyRotationReferenceWeight = 1f;
        private const float DefaultManualAnimatorFullBodyPoseReferenceWeight = 1f;
        private const float DefaultSetHumanPoseRightLegTwistOutputReferenceWeight = 1f;
        private const float DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta = 0.02f;
        private const float DefaultManualAnimatorHandPalmFrameWeight = 1f;
        private const float DefaultRetargetPoseVisualSpikeCurrentWeight = 0.65f;
        private const float DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset = 0f;
        private const float DefaultRetargetArmStretchMuscleLimit = 0.5f;
        private const float DefaultYybArmSwingLimitWeight = 0.85f;
        private const float DefaultYybArmSwingMaxDownDot = 0.68f;
        private const float DefaultYybArmSwingMinHandHorizontalRatio = 0.05f;
        private const float DefaultYybArmSwingMaxHandBelowShoulderRatio = 0.75f;
        private const float DefaultYybArmSwingHorizontalReachLimitWeight = 0f;
        private const float DefaultYybArmSwingMaxHandHorizontalReachRatio = 0f;
        private const float DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio = 0f;
        private const float DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply = 0f;
        private const float DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight = 0.2f;
        private const float DefaultYybArmSwingRaisedPoseMinUpperArmDownDot = 0.55f;
        private const float DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio = 0.05f;
        private const float DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio = 0.55f;
        private const float DefaultYybArmDirectionUpperArmWeight = 0.65f;
        private const float DefaultYybArmDirectionForearmWeight = 0.75f;
        private const float DefaultYybArmDirectionUpperArmMaxDegrees = 65f;
        private const float DefaultYybArmDirectionForearmMaxDegrees = 85f;
        private const float DefaultYybArmDirectionLeftSideWeightScale = 1f;
        private const float DefaultYybArmDirectionRightSideWeightScale = 1f;
        private const float DefaultYybArmSleeveAnchorInfluence = 0.825f;
        private const float DefaultYybArmShoulderCapAnchorInfluence = 0f;
        private const float DefaultYybArmSleeveAnchorMaxDegrees = 85f;
        private const float DefaultYybArmVisualUpperArmInfluence = 0.35f;
        private const float DefaultYybArmVisualForearmInfluence = 0.75f;
        private const float DefaultYybArmVisualUpperArmMaxDegrees = 45f;
        private const float DefaultYybArmVisualForearmMaxDegrees = 75f;
        private const float DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight = 1f;
        private const float DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = 15f;
        private const float DefaultManualAnimatorBipedIkFootPositionReferenceWeight = 0.65f;
        private const float DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset = 0.12f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight = 1f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset = 0.04f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale = 1f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = 1f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart = 0f;
        private const float DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = 0f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight = 1f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset = 0.025f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale = 1f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = 1f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart = 0f;
        private const float DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = 0f;
        private const float DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = 0.049f;
        private const float DefaultManualAnimatorHipsLocalPositionReferenceWeight = 0.25f;
        private const float DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset = 0.04f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceWeight = 0.45f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceMaxOffset = 0.025f;
        private const float DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = 0f;

        [Serializable]
        internal sealed class RequestData
        {
            public string request_id;
            public string requested_at = default;
            public string fbx_file = default;
            public float duration_seconds = 31f;
            public bool finger_closeups = default;
            public float mmd_ik_delta_guard_limit_vmd = float.NaN;
            public float mmd_ik_delta_guard_recovery_trigger_vmd = float.NaN;
            public float mmd_ik_delta_guard_recovery_debt_vmd = float.NaN;
            public int mmd_ik_delta_guard_recovery_hold_frames = default;
            public bool final_ik_foot_grounding_enabled = default;
            public bool manual_animator_foot_local_rotation_enabled = default;
            public bool manual_animator_foot_local_rotation_disabled = default;
            public bool manual_animator_full_body_pose_enabled = default;
            public bool manual_animator_full_body_pose_disabled = default;
            public float manual_animator_full_body_pose_weight =
                DefaultManualAnimatorFullBodyPoseReferenceWeight;
            public bool manual_animator_full_body_pose_exclude_lower_body_muscles = default;
            public bool manual_animator_full_body_pose_lower_body_muscles_only = default;
            public bool manual_animator_full_body_pose_leg_twist_muscles_only = default;
            public bool manual_animator_full_body_pose_right_arm_muscles_only = default;
            public bool manual_animator_full_body_pose_left_arm_muscles_only = default;
            public bool manual_animator_full_body_pose_right_sleeve_chain_muscles_only = default;
            public float manual_animator_full_body_pose_frame_gate_start = default;
            public float manual_animator_full_body_pose_frame_gate_end = default;
            public bool set_human_pose_right_leg_twist_output_enabled = default;
            public float set_human_pose_right_leg_twist_output_weight =
                DefaultSetHumanPoseRightLegTwistOutputReferenceWeight;
            public float set_human_pose_right_leg_twist_output_max_delta =
                DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta;
            public bool manual_animator_body_rotation_enabled = default;
            public bool manual_animator_body_rotation_disabled = default;
            public float manual_animator_body_rotation_weight =
                DefaultManualAnimatorBodyRotationReferenceWeight;
            public bool manual_animator_hand_local_rotation_enabled = default;
            public bool manual_animator_thumb_local_rotation_enabled = default;
            public bool manual_animator_hand_palm_frame_enabled = default;
            public float manual_animator_hand_palm_frame_weight = DefaultManualAnimatorHandPalmFrameWeight;
            public bool retarget_pose_visual_spike_smoothing_override = default;
            public bool retarget_pose_visual_spike_smoothing_enabled = true;
            public float retarget_pose_visual_spike_current_weight =
                DefaultRetargetPoseVisualSpikeCurrentWeight;
            public float retarget_pose_visual_spike_forearm_stretch_clamp_max_offset =
                DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset;
            public bool retarget_arm_stretch_clamp_enabled = default;
            public float retarget_arm_stretch_muscle_limit = DefaultRetargetArmStretchMuscleLimit;
            public bool yyb_arm_swing_limit_enabled = default;
            public float yyb_arm_swing_limit_weight = DefaultYybArmSwingLimitWeight;
            public float yyb_arm_swing_max_down_dot = DefaultYybArmSwingMaxDownDot;
            public float yyb_arm_swing_min_hand_horizontal_ratio =
                DefaultYybArmSwingMinHandHorizontalRatio;
            public float yyb_arm_swing_max_hand_below_shoulder_ratio =
                DefaultYybArmSwingMaxHandBelowShoulderRatio;
            public float yyb_arm_swing_horizontal_reach_limit_weight =
                DefaultYybArmSwingHorizontalReachLimitWeight;
            public float yyb_arm_swing_max_hand_horizontal_reach_ratio =
                DefaultYybArmSwingMaxHandHorizontalReachRatio;
            public float yyb_arm_swing_horizontal_reach_max_hand_below_shoulder_ratio =
                DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
            public float yyb_arm_swing_horizontal_reach_min_elbow_angle_after_apply =
                DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply;
            public float yyb_arm_swing_raised_pose_horizontal_reach_limit_weight =
                DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight;
            public float yyb_arm_swing_raised_pose_min_upper_arm_down_dot =
                DefaultYybArmSwingRaisedPoseMinUpperArmDownDot;
            public float yyb_arm_swing_raised_pose_max_hand_below_shoulder_ratio =
                DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
            public float yyb_arm_swing_raised_pose_max_hand_horizontal_reach_ratio =
                DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
            public bool yyb_arm_direction_retarget_enabled = default;
            public float yyb_arm_direction_upper_arm_weight = DefaultYybArmDirectionUpperArmWeight;
            public float yyb_arm_direction_forearm_weight = DefaultYybArmDirectionForearmWeight;
            public float yyb_arm_direction_upper_arm_max_degrees = DefaultYybArmDirectionUpperArmMaxDegrees;
            public float yyb_arm_direction_forearm_max_degrees = DefaultYybArmDirectionForearmMaxDegrees;
            public float yyb_arm_direction_left_side_weight_scale =
                DefaultYybArmDirectionLeftSideWeightScale;
            public float yyb_arm_direction_right_side_weight_scale =
                DefaultYybArmDirectionRightSideWeightScale;
            public bool yyb_arm_sleeve_anchor_override = default;
            public bool yyb_arm_sleeve_anchor_enabled = true;
            public float yyb_arm_sleeve_anchor_influence = DefaultYybArmSleeveAnchorInfluence;
            public float yyb_arm_shoulder_cap_anchor_influence = DefaultYybArmShoulderCapAnchorInfluence;
            public float yyb_arm_sleeve_anchor_max_degrees = DefaultYybArmSleeveAnchorMaxDegrees;
            public bool yyb_arm_visual_twist_override = default;
            public bool yyb_arm_visual_twist_enabled = true;
            public float yyb_arm_visual_upper_arm_influence = DefaultYybArmVisualUpperArmInfluence;
            public float yyb_arm_visual_forearm_influence = DefaultYybArmVisualForearmInfluence;
            public float yyb_arm_visual_upper_arm_max_degrees = DefaultYybArmVisualUpperArmMaxDegrees;
            public float yyb_arm_visual_forearm_max_degrees = DefaultYybArmVisualForearmMaxDegrees;
            public bool manual_animator_lower_body_segment_direction_enabled = default;
            public bool manual_animator_lower_body_segment_direction_disabled = default;
            public float manual_animator_lower_body_segment_direction_weight =
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight;
            public float manual_animator_lower_body_segment_direction_max_angle =
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            public bool manual_animator_upper_leg_to_lower_leg_segment_direction_disabled = default;
            public float manual_animator_upper_leg_to_lower_leg_segment_direction_max_angle =
                DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            public bool manual_animator_lower_leg_to_foot_segment_direction_disabled = default;
            public float manual_animator_lower_leg_to_foot_segment_direction_max_angle =
                DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manual_animator_left_lower_leg_to_foot_segment_direction_max_angle =
                DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_max_angle =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_axis_xz_scale =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_blend_weight =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_start =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_end =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            public float manual_animator_right_lower_leg_to_foot_segment_direction_endpoint_blend_weight =
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            public bool manual_animator_foot_to_toes_segment_direction_disabled = default;
            public float manual_animator_foot_to_toes_segment_direction_max_angle =
                DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            public bool manual_animator_foot_hips_aligned_residual_yaw_enabled = default;
            public bool manual_animator_foot_hips_aligned_residual_yaw_disabled = default;
            public float manual_animator_foot_hips_aligned_residual_yaw_weight =
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            public float manual_animator_foot_hips_aligned_residual_yaw_max_angle =
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            public bool post_set_human_pose_right_endpoint_position_enabled = default;
            public float post_set_human_pose_right_endpoint_position_weight =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight;
            public float post_set_human_pose_right_endpoint_position_max_offset =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            public float post_set_human_pose_right_endpoint_position_positive_z_scale =
                DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            public float post_set_human_pose_right_endpoint_position_toes_blend_weight =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            public float post_set_human_pose_right_endpoint_position_frame_gate_start =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            public float post_set_human_pose_right_endpoint_position_frame_gate_end =
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            public bool post_set_human_pose_endpoint_position_use_left_side = default;
            public bool pre_set_human_pose_right_endpoint_position_enabled = default;
            public float pre_set_human_pose_right_endpoint_position_weight =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight;
            public float pre_set_human_pose_right_endpoint_position_max_offset =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            public float pre_set_human_pose_right_endpoint_position_positive_z_scale =
                DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            public float pre_set_human_pose_right_endpoint_position_toes_blend_weight =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            public float pre_set_human_pose_right_endpoint_position_frame_gate_start =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            public float pre_set_human_pose_right_endpoint_position_frame_gate_end =
                DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            public bool pre_set_human_pose_endpoint_position_use_left_side = default;
            public bool pre_set_human_pose_endpoint_position_use_ghost_current_basis = default;
            public bool pre_set_human_pose_endpoint_position_invert_body_position_x = default;
            public bool pre_set_human_pose_endpoint_position_invert_body_position_z = default;
            public bool post_set_human_pose_right_foot_evaluator_xz_reference_enabled = default;
            public float post_set_human_pose_right_foot_evaluator_xz_reference_target_magnitude =
                DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            public bool manual_animator_biped_ik_foot_position_enabled = default;
            public float manual_animator_biped_ik_foot_position_weight =
                DefaultManualAnimatorBipedIkFootPositionReferenceWeight;
            public float manual_animator_biped_ik_foot_position_max_offset =
                DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset;
            public bool manual_animator_hips_local_position_enabled = default;
            public float manual_animator_hips_local_position_weight =
                DefaultManualAnimatorHipsLocalPositionReferenceWeight;
            public float manual_animator_hips_local_position_max_offset =
                DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset;
            public bool manual_animator_body_position_xz_enabled = default;
            public float manual_animator_body_position_xz_weight =
                DefaultManualAnimatorBodyPositionXzReferenceWeight;
            public float manual_animator_body_position_xz_max_offset =
                DefaultManualAnimatorBodyPositionXzReferenceMaxOffset;
            public float manual_animator_body_position_xz_frame_gate_start = default;
            public float manual_animator_body_position_xz_frame_gate_end = default;
            public float manual_animator_body_position_xz_frame_gate_blend_frames = default;
            public float manual_animator_body_position_xz_axis_x_scale = 1f;
            public float manual_animator_body_position_xz_axis_z_scale = 1f;
            public bool yyb_right_sleeve_silhouette_offset_enabled = default;
            public float yyb_right_sleeve_silhouette_local_offset_x = default;
            public float yyb_right_sleeve_silhouette_local_offset_frame_gate_start = default;
            public float yyb_right_sleeve_silhouette_local_offset_frame_gate_end = default;
            public bool retarget_body_position_xz_root_motion_enabled = default;
            public bool target_humanoid_bone_position_lock_disabled = default;
            public bool vmd_playback_probe_enabled = default;
            public bool vmd_playback_probe_apply_ik_targets = default;
            public bool reference_mmd_timing_enabled = default;
            public string segment = "head";
            public int diagnostic_capture_width_override = default;
            public int diagnostic_capture_height_override = default;
            public float diagnostic_screenshot_padding_override = float.NaN;
            public float diagnostic_screenshot_vertical_viewport_center_override = float.NaN;
        }

        internal static YybVisualComparisonRunOptions Map(RequestData request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return new YybVisualComparisonRunOptions
            {
                fbxFileName = request.fbx_file,
                durationSeconds = request.duration_seconds,
                enableFingerCloseups = request.finger_closeups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented = true,
                mmdIkDeltaGuardLimitOverrideVmd = request.mmd_ik_delta_guard_limit_vmd,
                mmdIkDeltaGuardRecoveryTriggerVmd = request.mmd_ik_delta_guard_recovery_trigger_vmd,
                mmdIkDeltaGuardRecoveryDebtThresholdVmd = request.mmd_ik_delta_guard_recovery_debt_vmd,
                mmdIkDeltaGuardRecoveryHoldFrames = request.mmd_ik_delta_guard_recovery_hold_frames,
                enableFinalIkFootGroundingRuntimeOverride = request.final_ik_foot_grounding_enabled,
                enableManualAnimatorFootLocalRotationRuntimeOverride = request.manual_animator_foot_local_rotation_enabled,
                disableManualAnimatorFootLocalRotationRuntimeOverride = request.manual_animator_foot_local_rotation_disabled,
                enableManualAnimatorFullBodyPoseRuntimeOverride = request.manual_animator_full_body_pose_enabled,
                disableManualAnimatorFullBodyPoseRuntimeOverride = request.manual_animator_full_body_pose_disabled,
                manualAnimatorFullBodyPoseReferenceWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_full_body_pose_weight,
                    DefaultManualAnimatorFullBodyPoseReferenceWeight)),
                manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride =
                    request.manual_animator_full_body_pose_exclude_lower_body_muscles,
                manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride =
                    request.manual_animator_full_body_pose_lower_body_muscles_only,
                manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride =
                    request.manual_animator_full_body_pose_leg_twist_muscles_only,
                manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride =
                    request.manual_animator_full_body_pose_right_arm_muscles_only,
                manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride =
                    request.manual_animator_full_body_pose_left_arm_muscles_only,
                manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride =
                    request.manual_animator_full_body_pose_right_sleeve_chain_muscles_only,
                manualAnimatorFullBodyPoseReferenceFrameGateStart = Mathf.Max(
                    0f,
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.manual_animator_full_body_pose_frame_gate_start,
                        0f)),
                manualAnimatorFullBodyPoseReferenceFrameGateEnd = Mathf.Max(
                    0f,
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.manual_animator_full_body_pose_frame_gate_end,
                        0f)),
                enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride =
                    request.set_human_pose_right_leg_twist_output_enabled,
                setHumanPoseRightLegTwistOutputReferenceWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                    request.set_human_pose_right_leg_twist_output_weight,
                    DefaultSetHumanPoseRightLegTwistOutputReferenceWeight)),
                setHumanPoseRightLegTwistOutputReferenceMaxDelta = Mathf.Max(0f, VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                    request.set_human_pose_right_leg_twist_output_max_delta,
                    DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta)),
                enableManualAnimatorBodyRotationRuntimeOverride = request.manual_animator_body_rotation_enabled,
                disableManualAnimatorBodyRotationRuntimeOverride = request.manual_animator_body_rotation_disabled,
                manualAnimatorBodyRotationReferenceWeight = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_body_rotation_weight,
                    DefaultManualAnimatorBodyRotationReferenceWeight),
                enableManualAnimatorHandLocalRotationRuntimeOverride =
                    request.manual_animator_hand_local_rotation_enabled,
                enableManualAnimatorThumbLocalRotationRuntimeOverride =
                    request.manual_animator_thumb_local_rotation_enabled,
                enableManualAnimatorHandPalmFrameRuntimeOverride =
                    request.manual_animator_hand_palm_frame_enabled,
                manualAnimatorHandPalmFrameWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_hand_palm_frame_weight,
                    DefaultManualAnimatorHandPalmFrameWeight)),
                overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings =
                    request.retarget_pose_visual_spike_smoothing_override,
                enableRetargetPoseVisualSpikeSmoothingRuntimeOverride =
                    request.retarget_pose_visual_spike_smoothing_enabled,
                retargetPoseVisualSpikeCurrentWeight = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.retarget_pose_visual_spike_current_weight,
                        DefaultRetargetPoseVisualSpikeCurrentWeight),
                    0.1f,
                    1f),
                retargetPoseVisualSpikeForearmStretchClampMaxOffset = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.retarget_pose_visual_spike_forearm_stretch_clamp_max_offset,
                        DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset)),
                enableRetargetArmStretchClampRuntimeOverride =
                    request.retarget_arm_stretch_clamp_enabled,
                retargetArmStretchMuscleLimit = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.retarget_arm_stretch_muscle_limit,
                        DefaultRetargetArmStretchMuscleLimit),
                    0f,
                    DefaultRetargetArmStretchMuscleLimit),
                enableYybArmSwingLimitRuntimeOverride = request.yyb_arm_swing_limit_enabled,
                yybArmSwingLimitWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.yyb_arm_swing_limit_weight,
                    DefaultYybArmSwingLimitWeight)),
                yybArmSwingMaxDownDot = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.yyb_arm_swing_max_down_dot,
                    DefaultYybArmSwingMaxDownDot)),
                yybArmSwingMinHandHorizontalRatio = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.yyb_arm_swing_min_hand_horizontal_ratio,
                        DefaultYybArmSwingMinHandHorizontalRatio),
                    0f,
                    1.5f),
                yybArmSwingMaxHandBelowShoulderRatio = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.yyb_arm_swing_max_hand_below_shoulder_ratio,
                        DefaultYybArmSwingMaxHandBelowShoulderRatio),
                    0f,
                    1.5f),
                yybArmSwingHorizontalReachLimitWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.yyb_arm_swing_horizontal_reach_limit_weight,
                    DefaultYybArmSwingHorizontalReachLimitWeight)),
                yybArmSwingMaxHandHorizontalReachRatio = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.yyb_arm_swing_max_hand_horizontal_reach_ratio,
                        DefaultYybArmSwingMaxHandHorizontalReachRatio),
                    0f,
                    1.5f),
                yybArmSwingHorizontalReachMaxHandBelowShoulderRatio = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.yyb_arm_swing_horizontal_reach_max_hand_below_shoulder_ratio,
                        DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio),
                    0f,
                    1.5f),
                yybArmSwingHorizontalReachMinElbowAngleAfterApply = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.yyb_arm_swing_horizontal_reach_min_elbow_angle_after_apply,
                        DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply),
                    0f,
                    180f),
                yybArmSwingRaisedPoseHorizontalReachLimitWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.yyb_arm_swing_raised_pose_horizontal_reach_limit_weight,
                    DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight)),
                yybArmSwingRaisedPoseMinUpperArmDownDot = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.yyb_arm_swing_raised_pose_min_upper_arm_down_dot,
                    DefaultYybArmSwingRaisedPoseMinUpperArmDownDot)),
                yybArmSwingRaisedPoseMaxHandBelowShoulderRatio = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.yyb_arm_swing_raised_pose_max_hand_below_shoulder_ratio,
                        DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio),
                    0f,
                    1.5f),
                yybArmSwingRaisedPoseMaxHandHorizontalReachRatio = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.yyb_arm_swing_raised_pose_max_hand_horizontal_reach_ratio,
                        DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio),
                    0f,
                    1.5f),
                enableYybArmDirectionRetargetRuntimeOverride = request.yyb_arm_direction_retarget_enabled,
                yybArmDirectionUpperArmWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.yyb_arm_direction_upper_arm_weight,
                    DefaultYybArmDirectionUpperArmWeight)),
                yybArmDirectionForearmWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.yyb_arm_direction_forearm_weight,
                    DefaultYybArmDirectionForearmWeight)),
                yybArmDirectionUpperArmMaxDegrees = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.yyb_arm_direction_upper_arm_max_degrees,
                        DefaultYybArmDirectionUpperArmMaxDegrees),
                    0f,
                    120f),
                yybArmDirectionForearmMaxDegrees = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                        request.yyb_arm_direction_forearm_max_degrees,
                        DefaultYybArmDirectionForearmMaxDegrees),
                    0f,
                    120f),
                yybArmDirectionLeftSideWeightScale = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                    request.yyb_arm_direction_left_side_weight_scale,
                    DefaultYybArmDirectionLeftSideWeightScale)),
                yybArmDirectionRightSideWeightScale = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                    request.yyb_arm_direction_right_side_weight_scale,
                    DefaultYybArmDirectionRightSideWeightScale)),
                overrideYybArmSleeveAnchorRuntimeSettings = request.yyb_arm_sleeve_anchor_override,
                enableYybArmSleeveAnchorRuntimeOverride = request.yyb_arm_sleeve_anchor_enabled,
                yybArmSleeveAnchorInfluence = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                    request.yyb_arm_sleeve_anchor_influence,
                    DefaultYybArmSleeveAnchorInfluence)),
                yybArmShoulderCapAnchorInfluence = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                    request.yyb_arm_shoulder_cap_anchor_influence,
                    DefaultYybArmShoulderCapAnchorInfluence)),
                yybArmSleeveAnchorMaxDegrees = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.yyb_arm_sleeve_anchor_max_degrees,
                        DefaultYybArmSleeveAnchorMaxDegrees),
                    0f,
                    120f),
                overrideYybArmVisualTwistRuntimeSettings = request.yyb_arm_visual_twist_override,
                enableYybArmVisualTwistRuntimeOverride = request.yyb_arm_visual_twist_enabled,
                yybArmVisualUpperArmInfluence = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                    request.yyb_arm_visual_upper_arm_influence,
                    DefaultYybArmVisualUpperArmInfluence)),
                yybArmVisualForearmInfluence = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                    request.yyb_arm_visual_forearm_influence,
                    DefaultYybArmVisualForearmInfluence)),
                yybArmVisualUpperArmMaxDegrees = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.yyb_arm_visual_upper_arm_max_degrees,
                        DefaultYybArmVisualUpperArmMaxDegrees),
                    0f,
                    120f),
                yybArmVisualForearmMaxDegrees = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.yyb_arm_visual_forearm_max_degrees,
                        DefaultYybArmVisualForearmMaxDegrees),
                    0f,
                    120f),
                enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = request.manual_animator_lower_body_segment_direction_enabled,
                disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride =
                    request.manual_animator_lower_body_segment_direction_disabled,
                manualAnimatorLowerBodySegmentDirectionReferenceWeight = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_lower_body_segment_direction_weight,
                    DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight),
                manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_lower_body_segment_direction_max_angle,
                    DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle),
                disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride =
                    request.manual_animator_upper_leg_to_lower_leg_segment_direction_disabled,
                manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_upper_leg_to_lower_leg_segment_direction_max_angle,
                    DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle),
                disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride =
                    request.manual_animator_lower_leg_to_foot_segment_direction_disabled,
                manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_lower_leg_to_foot_segment_direction_max_angle,
                    DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle),
                manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_left_lower_leg_to_foot_segment_direction_max_angle,
                    DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle),
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_right_lower_leg_to_foot_segment_direction_max_angle,
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle),
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.manual_animator_right_lower_leg_to_foot_segment_direction_axis_xz_scale,
                        DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale)),
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.manual_animator_right_lower_leg_to_foot_segment_direction_blend_weight,
                        DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight)),
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_start,
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart),
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_end,
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd),
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.manual_animator_right_lower_leg_to_foot_segment_direction_endpoint_blend_weight,
                        DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight)),
                disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride =
                    request.manual_animator_foot_to_toes_segment_direction_disabled,
                manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_foot_to_toes_segment_direction_max_angle,
                    DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle),
                enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride =
                    request.manual_animator_foot_hips_aligned_residual_yaw_enabled,
                disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride =
                    request.manual_animator_foot_hips_aligned_residual_yaw_disabled,
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_foot_hips_aligned_residual_yaw_weight,
                    DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight),
                manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_foot_hips_aligned_residual_yaw_max_angle,
                    DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle),
                enablePostSetHumanPoseRightEndpointPositionRuntimeOverride =
                    request.post_set_human_pose_right_endpoint_position_enabled,
                postSetHumanPoseRightEndpointPositionReferenceWeight = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.post_set_human_pose_right_endpoint_position_weight,
                        DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight)),
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.post_set_human_pose_right_endpoint_position_max_offset,
                    DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset),
                postSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.post_set_human_pose_right_endpoint_position_positive_z_scale,
                        DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale)),
                postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.post_set_human_pose_right_endpoint_position_toes_blend_weight,
                        DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight)),
                postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.post_set_human_pose_right_endpoint_position_frame_gate_start,
                    DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart),
                postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.post_set_human_pose_right_endpoint_position_frame_gate_end,
                    DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd),
                ShouldUseLeftSideForPostSetHumanPoseEndpointPosition =
                    request.post_set_human_pose_endpoint_position_use_left_side,
                enablePreSetHumanPoseRightEndpointPositionRuntimeOverride =
                    request.pre_set_human_pose_right_endpoint_position_enabled,
                preSetHumanPoseRightEndpointPositionReferenceWeight = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.pre_set_human_pose_right_endpoint_position_weight,
                        DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight)),
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.pre_set_human_pose_right_endpoint_position_max_offset,
                    DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset),
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.pre_set_human_pose_right_endpoint_position_positive_z_scale,
                        DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale)),
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.pre_set_human_pose_right_endpoint_position_toes_blend_weight,
                        DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight)),
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.pre_set_human_pose_right_endpoint_position_frame_gate_start,
                    DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart),
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.pre_set_human_pose_right_endpoint_position_frame_gate_end,
                    DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd),
                ShouldUseLeftSideForPreSetHumanPoseEndpointPosition =
                    request.pre_set_human_pose_endpoint_position_use_left_side,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                    request.pre_set_human_pose_endpoint_position_use_ghost_current_basis,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyX =
                    request.pre_set_human_pose_endpoint_position_invert_body_position_x,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyZ =
                    request.pre_set_human_pose_endpoint_position_invert_body_position_z,
                usePostSetHumanPoseRightFootEvaluatorXzReference =
                    request.post_set_human_pose_right_foot_evaluator_xz_reference_enabled,
                postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.post_set_human_pose_right_foot_evaluator_xz_reference_target_magnitude,
                    DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude),
                enableManualAnimatorBipedIkFootPositionRuntimeOverride = request.manual_animator_biped_ik_foot_position_enabled,
                manualAnimatorBipedIkFootPositionReferenceWeight = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_biped_ik_foot_position_weight,
                    DefaultManualAnimatorBipedIkFootPositionReferenceWeight),
                manualAnimatorBipedIkFootPositionReferenceMaxOffset = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_biped_ik_foot_position_max_offset,
                    DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset),
                enableManualAnimatorHipsLocalPositionRuntimeOverride =
                    request.manual_animator_hips_local_position_enabled,
                manualAnimatorHipsLocalPositionReferenceWeight = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_hips_local_position_weight,
                    DefaultManualAnimatorHipsLocalPositionReferenceWeight),
                manualAnimatorHipsLocalPositionReferenceMaxOffset = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_hips_local_position_max_offset,
                    DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset),
                enableManualAnimatorBodyPositionXzRuntimeOverride =
                    request.manual_animator_body_position_xz_enabled,
                manualAnimatorBodyPositionXzReferenceWeight = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.manual_animator_body_position_xz_weight,
                        DefaultManualAnimatorBodyPositionXzReferenceWeight)),
                manualAnimatorBodyPositionXzReferenceMaxOffset = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_body_position_xz_max_offset,
                    DefaultManualAnimatorBodyPositionXzReferenceMaxOffset),
                manualAnimatorBodyPositionXzReferenceFrameGateStart = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_body_position_xz_frame_gate_start,
                    0f),
                manualAnimatorBodyPositionXzReferenceFrameGateEnd = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_body_position_xz_frame_gate_end,
                    0f),
                manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.manual_animator_body_position_xz_frame_gate_blend_frames,
                    DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames),
                manualAnimatorBodyPositionXzReferenceAxisXScale = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.manual_animator_body_position_xz_axis_x_scale,
                        1f)),
                manualAnimatorBodyPositionXzReferenceAxisZScale = Mathf.Clamp01(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.manual_animator_body_position_xz_axis_z_scale,
                        1f)),
                enableYybRightSleeveSilhouetteOffsetRuntimeOverride =
                    request.yyb_right_sleeve_silhouette_offset_enabled,
                yybRightSleeveSilhouetteLocalOffsetX = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.yyb_right_sleeve_silhouette_local_offset_x,
                        0f),
                    -0.2f,
                    0.2f),
                yybRightSleeveSilhouetteLocalOffsetFrameGateStart = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.yyb_right_sleeve_silhouette_local_offset_frame_gate_start,
                        0f),
                    0f,
                    6000f),
                yybRightSleeveSilhouetteLocalOffsetFrameGateEnd = Mathf.Clamp(
                    VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                        request.yyb_right_sleeve_silhouette_local_offset_frame_gate_end,
                        0f),
                    0f,
                    6000f),
                enableRetargetBodyPositionXzRootMotionRuntimeOverride =
                    request.retarget_body_position_xz_root_motion_enabled,
                disableTargetHumanoidBonePositionLockRuntimeOverride =
                    request.target_humanoid_bone_position_lock_disabled,
                enableVmdPlaybackProbeRuntimeOverride = request.vmd_playback_probe_enabled,
                applyVmdPlaybackProbeIkTargetsRuntimeOverride = request.vmd_playback_probe_apply_ik_targets,
                editorDiagnosticSmokeSegment = request.segment,
                enableReferenceMmdTimingRuntimeOverride = request.reference_mmd_timing_enabled,
                diagnosticCaptureWidthOverride = Mathf.Max(0, request.diagnostic_capture_width_override),
                diagnosticCaptureHeightOverride = Mathf.Max(0, request.diagnostic_capture_height_override),
                diagnosticScreenshotPaddingOverride = VisualComparisonRuntimeValueNormalizer.NormalizePositive(
                    request.diagnostic_screenshot_padding_override,
                    float.NaN),
                diagnosticScreenshotVerticalViewportCenterOverride = VisualComparisonRuntimeValueNormalizer.NormalizeFinite(
                    request.diagnostic_screenshot_vertical_viewport_center_override,
                    float.NaN)
            };
        }
    }
}
#endif
