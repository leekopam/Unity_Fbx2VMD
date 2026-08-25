using System;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonSummarySettingsSnapshotter
    {
        internal static void Capture(
            YybVisualComparisonSummaryData summary,
            YybVisualComparisonRunStateData state,
            int targetFrameCount,
            string vmdPlaybackProbeSourceVmdPath)
        {
            if (summary == null)
            {
                throw new ArgumentNullException(nameof(summary));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            summary.session_id = state.summarySessionId;
            summary.fbx_file = state.fbxFileName;
            summary.duration_seconds = state.durationSeconds;
            summary.target_frame_count = targetFrameCount;
            summary.segment = state.editorDiagnosticSmokeSegment;
            summary.finger_closeups = state.enableFingerCloseups;
            summary.recorder_parent_ik_offsets_when_center_parented =
                state.enableRecorderParentFrameIkOffsetsWhenCenterParented;
            summary.mmd_ik_delta_guard_limit_override_vmd = state.mmdIkDeltaGuardLimitOverrideVmd;
            summary.mmd_ik_delta_guard_recovery_trigger_vmd = state.mmdIkDeltaGuardRecoveryTriggerVmd;
            summary.mmd_ik_delta_guard_recovery_debt_vmd = state.mmdIkDeltaGuardRecoveryDebtThresholdVmd;
            summary.mmd_ik_delta_guard_recovery_hold_frames = state.mmdIkDeltaGuardRecoveryHoldFrames;
            summary.final_ik_foot_grounding_enabled = state.enableFinalIkFootGroundingRuntimeOverride;
            summary.manual_animator_foot_local_rotation_enabled =
                state.enableManualAnimatorFootLocalRotationRuntimeOverride;
            summary.manual_animator_foot_local_rotation_disabled =
                state.disableManualAnimatorFootLocalRotationRuntimeOverride;
            summary.manual_animator_full_body_pose_enabled =
                state.enableManualAnimatorFullBodyPoseRuntimeOverride;
            summary.manual_animator_full_body_pose_disabled =
                state.disableManualAnimatorFullBodyPoseRuntimeOverride;
            summary.manual_animator_full_body_pose_weight = state.manualAnimatorFullBodyPoseReferenceWeight;
            summary.manual_animator_full_body_pose_exclude_lower_body_muscles =
                state.manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride;
            summary.manual_animator_full_body_pose_lower_body_muscles_only =
                state.manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride;
            summary.manual_animator_full_body_pose_leg_twist_muscles_only =
                state.manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride;
            summary.manual_animator_full_body_pose_right_arm_muscles_only =
                state.manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride;
            summary.manual_animator_full_body_pose_left_arm_muscles_only =
                state.manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride;
            summary.manual_animator_full_body_pose_right_sleeve_chain_muscles_only =
                state.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride;
            summary.manual_animator_full_body_pose_frame_gate_start =
                state.manualAnimatorFullBodyPoseReferenceFrameGateStart;
            summary.manual_animator_full_body_pose_frame_gate_end =
                state.manualAnimatorFullBodyPoseReferenceFrameGateEnd;
            summary.set_human_pose_right_leg_twist_output_enabled =
                state.enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride;
            summary.set_human_pose_right_leg_twist_output_weight =
                state.setHumanPoseRightLegTwistOutputReferenceWeight;
            summary.set_human_pose_right_leg_twist_output_max_delta =
                state.setHumanPoseRightLegTwistOutputReferenceMaxDelta;
            summary.manual_animator_body_rotation_enabled = state.enableManualAnimatorBodyRotationRuntimeOverride;
            summary.manual_animator_body_rotation_disabled = state.disableManualAnimatorBodyRotationRuntimeOverride;
            summary.manual_animator_body_rotation_weight = state.manualAnimatorBodyRotationReferenceWeight;
            summary.manual_animator_hand_local_rotation_enabled =
                state.enableManualAnimatorHandLocalRotationRuntimeOverride;
            summary.manual_animator_thumb_local_rotation_enabled =
                state.enableManualAnimatorThumbLocalRotationRuntimeOverride;
            summary.manual_animator_hand_palm_frame_enabled = state.enableManualAnimatorHandPalmFrameRuntimeOverride;
            summary.manual_animator_hand_palm_frame_weight = state.manualAnimatorHandPalmFrameWeight;
            summary.retarget_pose_visual_spike_smoothing_override =
                state.overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings;
            summary.retarget_pose_visual_spike_smoothing_enabled =
                state.enableRetargetPoseVisualSpikeSmoothingRuntimeOverride;
            summary.retarget_pose_visual_spike_current_weight = state.retargetPoseVisualSpikeCurrentWeight;
            summary.retarget_pose_visual_spike_forearm_stretch_clamp_max_offset =
                state.retargetPoseVisualSpikeForearmStretchClampMaxOffset;
            summary.retarget_arm_stretch_clamp_enabled = state.enableRetargetArmStretchClampRuntimeOverride;
            summary.retarget_arm_stretch_muscle_limit = state.retargetArmStretchMuscleLimit;
            summary.yyb_arm_swing_limit_enabled = state.enableYybArmSwingLimitRuntimeOverride;
            summary.yyb_arm_swing_limit_weight = state.yybArmSwingLimitWeight;
            summary.yyb_arm_swing_max_down_dot = state.yybArmSwingMaxDownDot;
            summary.yyb_arm_swing_min_hand_horizontal_ratio = state.yybArmSwingMinHandHorizontalRatio;
            summary.yyb_arm_swing_max_hand_below_shoulder_ratio = state.yybArmSwingMaxHandBelowShoulderRatio;
            summary.yyb_arm_swing_horizontal_reach_limit_weight = state.yybArmSwingHorizontalReachLimitWeight;
            summary.yyb_arm_swing_max_hand_horizontal_reach_ratio = state.yybArmSwingMaxHandHorizontalReachRatio;
            summary.yyb_arm_swing_horizontal_reach_max_hand_below_shoulder_ratio =
                state.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
            summary.yyb_arm_swing_horizontal_reach_min_elbow_angle_after_apply =
                state.yybArmSwingHorizontalReachMinElbowAngleAfterApply;
            summary.yyb_arm_swing_raised_pose_horizontal_reach_limit_weight =
                state.yybArmSwingRaisedPoseHorizontalReachLimitWeight;
            summary.yyb_arm_swing_raised_pose_min_upper_arm_down_dot = state.yybArmSwingRaisedPoseMinUpperArmDownDot;
            summary.yyb_arm_swing_raised_pose_max_hand_below_shoulder_ratio =
                state.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
            summary.yyb_arm_swing_raised_pose_max_hand_horizontal_reach_ratio =
                state.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
            summary.yyb_arm_direction_retarget_enabled = state.enableYybArmDirectionRetargetRuntimeOverride;
            summary.yyb_arm_direction_upper_arm_weight = state.yybArmDirectionUpperArmWeight;
            summary.yyb_arm_direction_forearm_weight = state.yybArmDirectionForearmWeight;
            summary.yyb_arm_direction_upper_arm_max_degrees = state.yybArmDirectionUpperArmMaxDegrees;
            summary.yyb_arm_direction_forearm_max_degrees = state.yybArmDirectionForearmMaxDegrees;
            summary.yyb_arm_direction_left_side_weight_scale = state.yybArmDirectionLeftSideWeightScale;
            summary.yyb_arm_direction_right_side_weight_scale = state.yybArmDirectionRightSideWeightScale;
            summary.yyb_arm_sleeve_anchor_override = state.overrideYybArmSleeveAnchorRuntimeSettings;
            summary.yyb_arm_sleeve_anchor_enabled = state.enableYybArmSleeveAnchorRuntimeOverride;
            summary.yyb_arm_sleeve_anchor_influence = state.yybArmSleeveAnchorInfluence;
            summary.yyb_arm_shoulder_cap_anchor_influence = state.yybArmShoulderCapAnchorInfluence;
            summary.yyb_arm_sleeve_anchor_max_degrees = state.yybArmSleeveAnchorMaxDegrees;
            summary.yyb_arm_visual_twist_override = state.overrideYybArmVisualTwistRuntimeSettings;
            summary.yyb_arm_visual_twist_enabled = state.enableYybArmVisualTwistRuntimeOverride;
            summary.yyb_arm_visual_upper_arm_influence = state.yybArmVisualUpperArmInfluence;
            summary.yyb_arm_visual_forearm_influence = state.yybArmVisualForearmInfluence;
            summary.yyb_arm_visual_upper_arm_max_degrees = state.yybArmVisualUpperArmMaxDegrees;
            summary.yyb_arm_visual_forearm_max_degrees = state.yybArmVisualForearmMaxDegrees;
            summary.manual_animator_lower_body_segment_direction_enabled =
                state.enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            summary.manual_animator_lower_body_segment_direction_disabled =
                state.disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            summary.manual_animator_lower_body_segment_direction_weight =
                state.manualAnimatorLowerBodySegmentDirectionReferenceWeight;
            summary.manual_animator_lower_body_segment_direction_max_angle =
                state.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            summary.manual_animator_upper_leg_to_lower_leg_segment_direction_disabled =
                state.disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride;
            summary.manual_animator_upper_leg_to_lower_leg_segment_direction_max_angle =
                state.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            summary.manual_animator_lower_leg_to_foot_segment_direction_disabled =
                state.disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride;
            summary.manual_animator_lower_leg_to_foot_segment_direction_max_angle =
                state.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            summary.manual_animator_left_lower_leg_to_foot_segment_direction_max_angle =
                state.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            summary.manual_animator_right_lower_leg_to_foot_segment_direction_max_angle =
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            summary.manual_animator_right_lower_leg_to_foot_segment_direction_axis_xz_scale =
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            summary.manual_animator_right_lower_leg_to_foot_segment_direction_blend_weight =
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            summary.manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_start =
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            summary.manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_end =
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            summary.manual_animator_right_lower_leg_to_foot_segment_direction_endpoint_blend_weight =
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            summary.manual_animator_foot_to_toes_segment_direction_disabled =
                state.disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride;
            summary.manual_animator_foot_to_toes_segment_direction_max_angle =
                state.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            summary.manual_animator_foot_hips_aligned_residual_yaw_enabled =
                state.enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            summary.manual_animator_foot_hips_aligned_residual_yaw_disabled =
                state.disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            summary.manual_animator_foot_hips_aligned_residual_yaw_weight =
                state.manualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            summary.manual_animator_foot_hips_aligned_residual_yaw_max_angle =
                state.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            summary.post_set_human_pose_right_endpoint_position_enabled =
                state.enablePostSetHumanPoseRightEndpointPositionRuntimeOverride;
            summary.post_set_human_pose_right_endpoint_position_weight =
                state.postSetHumanPoseRightEndpointPositionReferenceWeight;
            summary.post_set_human_pose_right_endpoint_position_max_offset =
                state.postSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            summary.post_set_human_pose_right_endpoint_position_positive_z_scale =
                state.postSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            summary.post_set_human_pose_right_endpoint_position_toes_blend_weight =
                state.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            summary.post_set_human_pose_right_endpoint_position_frame_gate_start =
                state.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            summary.post_set_human_pose_right_endpoint_position_frame_gate_end =
                state.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            summary.post_set_human_pose_endpoint_position_use_left_side =
                state.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition;
            summary.pre_set_human_pose_right_endpoint_position_enabled =
                state.enablePreSetHumanPoseRightEndpointPositionRuntimeOverride;
            summary.pre_set_human_pose_right_endpoint_position_weight =
                state.preSetHumanPoseRightEndpointPositionReferenceWeight;
            summary.pre_set_human_pose_right_endpoint_position_max_offset =
                state.preSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            summary.pre_set_human_pose_right_endpoint_position_positive_z_scale =
                state.preSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            summary.pre_set_human_pose_right_endpoint_position_toes_blend_weight =
                state.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            summary.pre_set_human_pose_right_endpoint_position_frame_gate_start =
                state.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            summary.pre_set_human_pose_right_endpoint_position_frame_gate_end =
                state.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            summary.pre_set_human_pose_endpoint_position_use_left_side =
                state.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
            summary.pre_set_human_pose_endpoint_position_use_ghost_current_basis =
                state.preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            summary.pre_set_human_pose_endpoint_position_invert_body_position_x =
                state.ShouldInvertPreSetHumanPoseEndpointPositionBodyX;
            summary.pre_set_human_pose_endpoint_position_invert_body_position_z =
                state.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ;
            summary.post_set_human_pose_right_foot_evaluator_xz_reference_enabled =
                state.usePostSetHumanPoseRightFootEvaluatorXzReference;
            summary.post_set_human_pose_right_foot_evaluator_xz_reference_target_magnitude =
                state.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            summary.manual_animator_biped_ik_foot_position_enabled =
                state.enableManualAnimatorBipedIkFootPositionRuntimeOverride;
            summary.manual_animator_biped_ik_foot_position_weight =
                state.manualAnimatorBipedIkFootPositionReferenceWeight;
            summary.manual_animator_biped_ik_foot_position_max_offset =
                state.manualAnimatorBipedIkFootPositionReferenceMaxOffset;
            summary.manual_animator_hips_local_position_enabled = state.enableManualAnimatorHipsLocalPositionRuntimeOverride;
            summary.manual_animator_hips_local_position_weight = state.manualAnimatorHipsLocalPositionReferenceWeight;
            summary.manual_animator_hips_local_position_max_offset = state.manualAnimatorHipsLocalPositionReferenceMaxOffset;
            summary.manual_animator_body_position_xz_enabled = state.enableManualAnimatorBodyPositionXzRuntimeOverride;
            summary.manual_animator_body_position_xz_weight = state.manualAnimatorBodyPositionXzReferenceWeight;
            summary.manual_animator_body_position_xz_max_offset = state.manualAnimatorBodyPositionXzReferenceMaxOffset;
            summary.manual_animator_body_position_xz_frame_gate_start = state.manualAnimatorBodyPositionXzReferenceFrameGateStart;
            summary.manual_animator_body_position_xz_frame_gate_end = state.manualAnimatorBodyPositionXzReferenceFrameGateEnd;
            summary.manual_animator_body_position_xz_frame_gate_blend_frames =
                state.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
            summary.manual_animator_body_position_xz_axis_x_scale = state.manualAnimatorBodyPositionXzReferenceAxisXScale;
            summary.manual_animator_body_position_xz_axis_z_scale = state.manualAnimatorBodyPositionXzReferenceAxisZScale;
            summary.retarget_body_position_xz_root_motion_enabled =
                state.enableRetargetBodyPositionXzRootMotionRuntimeOverride;
            summary.target_humanoid_bone_position_lock_disabled =
                state.disableTargetHumanoidBonePositionLockRuntimeOverride;
            summary.vmd_playback_probe_enabled = state.enableVmdPlaybackProbeRuntimeOverride;
            summary.vmd_playback_probe_apply_ik_targets = state.applyVmdPlaybackProbeIkTargetsRuntimeOverride;
            summary.vmd_playback_probe_source_vmd_path = vmdPlaybackProbeSourceVmdPath;
            summary.reference_mmd_timing_enabled = state.enableReferenceMmdTimingRuntimeOverride;
            summary.diagnostic_capture_width_override = state.diagnosticCaptureWidthOverride;
            summary.diagnostic_capture_height_override = state.diagnosticCaptureHeightOverride;
            summary.diagnostic_screenshot_padding_override = state.diagnosticScreenshotPaddingOverride;
            summary.diagnostic_screenshot_vertical_viewport_center_override =
                state.diagnosticScreenshotVerticalViewportCenterOverride;
        }
    }
}
