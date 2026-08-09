
#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public static partial class YybVisualComparisonBatchRunner
    {
        private static void WriteSummaryJson(
            string path,
            MotionComparisonFrameQualitySummary[] frameQualitySummaries = null,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics = null)
        {
            frameRoleDiagnostics = frameRoleDiagnostics ?? BuildCurrentSummaryFrameRoleDiagnostics();
            frameQualitySummaries = frameQualitySummaries ?? BuildFrameQualitySummaries(frameRoleDiagnostics);
            int summaryTargetFrameCount = ResolveSummaryTargetFrameCount();
            SummaryContainer summary = new SummaryContainer
            {
                session_id = _summarySessionId,
                generated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                fbx_file = _fbxFileName,
                duration_seconds = _durationSeconds,
                target_frame_count = summaryTargetFrameCount,
                segment = _editorDiagnosticSmokeSegment.ToString(),
                finger_closeups = _enableFingerCloseups,
                recorder_parent_ik_offsets_when_center_parented = _enableRecorderParentFrameIkOffsetsWhenCenterParented,
                mmd_ik_delta_guard_limit_override_vmd = _mmdIkDeltaGuardLimitOverrideVmd,
                mmd_ik_delta_guard_recovery_trigger_vmd = _mmdIkDeltaGuardRecoveryTriggerVmd,
                mmd_ik_delta_guard_recovery_debt_vmd = _mmdIkDeltaGuardRecoveryDebtThresholdVmd,
                mmd_ik_delta_guard_recovery_hold_frames = _mmdIkDeltaGuardRecoveryHoldFrames,
                final_ik_foot_grounding_enabled = _enableFinalIkFootGroundingRuntimeOverride,
                manual_animator_foot_local_rotation_enabled = _enableManualAnimatorFootLocalRotationRuntimeOverride,
                manual_animator_foot_local_rotation_disabled = _disableManualAnimatorFootLocalRotationRuntimeOverride,
                manual_animator_full_body_pose_enabled = _enableManualAnimatorFullBodyPoseRuntimeOverride,
                manual_animator_full_body_pose_disabled = _disableManualAnimatorFullBodyPoseRuntimeOverride,
                manual_animator_full_body_pose_weight = _manualAnimatorFullBodyPoseReferenceWeight,
                manual_animator_full_body_pose_exclude_lower_body_muscles =
                    _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride,
                manual_animator_full_body_pose_lower_body_muscles_only =
                    _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride,
                manual_animator_full_body_pose_leg_twist_muscles_only =
                    _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride,
                manual_animator_full_body_pose_right_arm_muscles_only =
                    _manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride,
                manual_animator_full_body_pose_left_arm_muscles_only =
                    _manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride,
                manual_animator_full_body_pose_right_sleeve_chain_muscles_only =
                    _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride,
                manual_animator_full_body_pose_frame_gate_start =
                    _manualAnimatorFullBodyPoseReferenceFrameGateStart,
                manual_animator_full_body_pose_frame_gate_end =
                    _manualAnimatorFullBodyPoseReferenceFrameGateEnd,
                set_human_pose_right_leg_twist_output_enabled =
                    _enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride,
                set_human_pose_right_leg_twist_output_weight =
                    _setHumanPoseRightLegTwistOutputReferenceWeight,
                set_human_pose_right_leg_twist_output_max_delta =
                    _setHumanPoseRightLegTwistOutputReferenceMaxDelta,
                manual_animator_body_rotation_enabled = _enableManualAnimatorBodyRotationRuntimeOverride,
                manual_animator_body_rotation_disabled = _disableManualAnimatorBodyRotationRuntimeOverride,
                manual_animator_body_rotation_weight = _manualAnimatorBodyRotationReferenceWeight,
                manual_animator_hand_local_rotation_enabled = _enableManualAnimatorHandLocalRotationRuntimeOverride,
                manual_animator_thumb_local_rotation_enabled = _enableManualAnimatorThumbLocalRotationRuntimeOverride,
                manual_animator_hand_palm_frame_enabled = _enableManualAnimatorHandPalmFrameRuntimeOverride,
                manual_animator_hand_palm_frame_weight = _manualAnimatorHandPalmFrameWeight,
                retarget_pose_visual_spike_smoothing_override =
                    _overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings,
                retarget_pose_visual_spike_smoothing_enabled =
                    _enableRetargetPoseVisualSpikeSmoothingRuntimeOverride,
                retarget_pose_visual_spike_current_weight = _retargetPoseVisualSpikeCurrentWeight,
                retarget_pose_visual_spike_forearm_stretch_clamp_max_offset =
                    _retargetPoseVisualSpikeForearmStretchClampMaxOffset,
                retarget_arm_stretch_clamp_enabled =
                    _enableRetargetArmStretchClampRuntimeOverride,
                retarget_arm_stretch_muscle_limit = _retargetArmStretchMuscleLimit,
                yyb_arm_swing_limit_enabled = _enableYybArmSwingLimitRuntimeOverride,
                yyb_arm_swing_limit_weight = _yybArmSwingLimitWeight,
                yyb_arm_swing_max_down_dot = _yybArmSwingMaxDownDot,
                yyb_arm_swing_min_hand_horizontal_ratio = _yybArmSwingMinHandHorizontalRatio,
                yyb_arm_swing_max_hand_below_shoulder_ratio = _yybArmSwingMaxHandBelowShoulderRatio,
                yyb_arm_swing_horizontal_reach_limit_weight = _yybArmSwingHorizontalReachLimitWeight,
                yyb_arm_swing_max_hand_horizontal_reach_ratio = _yybArmSwingMaxHandHorizontalReachRatio,
                yyb_arm_swing_horizontal_reach_max_hand_below_shoulder_ratio =
                    _yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                yyb_arm_swing_horizontal_reach_min_elbow_angle_after_apply =
                    _yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                yyb_arm_swing_raised_pose_horizontal_reach_limit_weight =
                    _yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                yyb_arm_swing_raised_pose_min_upper_arm_down_dot =
                    _yybArmSwingRaisedPoseMinUpperArmDownDot,
                yyb_arm_swing_raised_pose_max_hand_below_shoulder_ratio =
                    _yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                yyb_arm_swing_raised_pose_max_hand_horizontal_reach_ratio =
                    _yybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                yyb_arm_direction_retarget_enabled = _enableYybArmDirectionRetargetRuntimeOverride,
                yyb_arm_direction_upper_arm_weight = _yybArmDirectionUpperArmWeight,
                yyb_arm_direction_forearm_weight = _yybArmDirectionForearmWeight,
                yyb_arm_direction_upper_arm_max_degrees = _yybArmDirectionUpperArmMaxDegrees,
                yyb_arm_direction_forearm_max_degrees = _yybArmDirectionForearmMaxDegrees,
                yyb_arm_direction_left_side_weight_scale = _yybArmDirectionLeftSideWeightScale,
                yyb_arm_direction_right_side_weight_scale = _yybArmDirectionRightSideWeightScale,
                yyb_arm_sleeve_anchor_override = _overrideYybArmSleeveAnchorRuntimeSettings,
                yyb_arm_sleeve_anchor_enabled = _enableYybArmSleeveAnchorRuntimeOverride,
                yyb_arm_sleeve_anchor_influence = _yybArmSleeveAnchorInfluence,
                yyb_arm_shoulder_cap_anchor_influence = _yybArmShoulderCapAnchorInfluence,
                yyb_arm_sleeve_anchor_max_degrees = _yybArmSleeveAnchorMaxDegrees,
                yyb_arm_visual_twist_override = _overrideYybArmVisualTwistRuntimeSettings,
                yyb_arm_visual_twist_enabled = _enableYybArmVisualTwistRuntimeOverride,
                yyb_arm_visual_upper_arm_influence = _yybArmVisualUpperArmInfluence,
                yyb_arm_visual_forearm_influence = _yybArmVisualForearmInfluence,
                yyb_arm_visual_upper_arm_max_degrees = _yybArmVisualUpperArmMaxDegrees,
                yyb_arm_visual_forearm_max_degrees = _yybArmVisualForearmMaxDegrees,
                manual_animator_lower_body_segment_direction_enabled = _enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                manual_animator_lower_body_segment_direction_disabled =
                    _disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                manual_animator_lower_body_segment_direction_weight = _manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                manual_animator_lower_body_segment_direction_max_angle = _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                manual_animator_upper_leg_to_lower_leg_segment_direction_disabled =
                    _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                manual_animator_upper_leg_to_lower_leg_segment_direction_max_angle =
                    _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                manual_animator_lower_leg_to_foot_segment_direction_disabled =
                    _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                manual_animator_lower_leg_to_foot_segment_direction_max_angle =
                    _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manual_animator_left_lower_leg_to_foot_segment_direction_max_angle =
                    _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manual_animator_right_lower_leg_to_foot_segment_direction_max_angle =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manual_animator_right_lower_leg_to_foot_segment_direction_axis_xz_scale =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                manual_animator_right_lower_leg_to_foot_segment_direction_blend_weight =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_start =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_end =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                manual_animator_right_lower_leg_to_foot_segment_direction_endpoint_blend_weight =
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                manual_animator_foot_to_toes_segment_direction_disabled =
                    _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                manual_animator_foot_to_toes_segment_direction_max_angle =
                    _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
                manual_animator_foot_hips_aligned_residual_yaw_enabled = _enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                manual_animator_foot_hips_aligned_residual_yaw_disabled =
                    _disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride,
                manual_animator_foot_hips_aligned_residual_yaw_weight = _manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                manual_animator_foot_hips_aligned_residual_yaw_max_angle = _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
                post_set_human_pose_right_endpoint_position_enabled =
                    _enablePostSetHumanPoseRightEndpointPositionRuntimeOverride,
                post_set_human_pose_right_endpoint_position_weight =
                    _postSetHumanPoseRightEndpointPositionReferenceWeight,
                post_set_human_pose_right_endpoint_position_max_offset =
                    _postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                post_set_human_pose_right_endpoint_position_positive_z_scale =
                    _postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                post_set_human_pose_right_endpoint_position_toes_blend_weight =
                    _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                post_set_human_pose_right_endpoint_position_frame_gate_start =
                    _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                post_set_human_pose_right_endpoint_position_frame_gate_end =
                    _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                post_set_human_pose_endpoint_position_use_left_side =
                    _postSetHumanPoseEndpointPositionUseLeftSide,
                pre_set_human_pose_right_endpoint_position_enabled =
                    _enablePreSetHumanPoseRightEndpointPositionRuntimeOverride,
                pre_set_human_pose_right_endpoint_position_weight =
                    _preSetHumanPoseRightEndpointPositionReferenceWeight,
                pre_set_human_pose_right_endpoint_position_max_offset =
                    _preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                pre_set_human_pose_right_endpoint_position_positive_z_scale =
                    _preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                pre_set_human_pose_right_endpoint_position_toes_blend_weight =
                    _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                pre_set_human_pose_right_endpoint_position_frame_gate_start =
                    _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                pre_set_human_pose_right_endpoint_position_frame_gate_end =
                    _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                pre_set_human_pose_endpoint_position_use_left_side =
                    _preSetHumanPoseEndpointPositionUseLeftSide,
                pre_set_human_pose_endpoint_position_use_ghost_current_basis =
                    _preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                pre_set_human_pose_endpoint_position_invert_body_position_x =
                    _preSetHumanPoseEndpointPositionInvertBodyPositionX,
                pre_set_human_pose_endpoint_position_invert_body_position_z =
                    _preSetHumanPoseEndpointPositionInvertBodyPositionZ,
                post_set_human_pose_right_foot_evaluator_xz_reference_enabled =
                    _usePostSetHumanPoseRightFootEvaluatorXzReference,
                post_set_human_pose_right_foot_evaluator_xz_reference_target_magnitude =
                    _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                manual_animator_biped_ik_foot_position_enabled = _enableManualAnimatorBipedIkFootPositionRuntimeOverride,
                manual_animator_biped_ik_foot_position_weight = _manualAnimatorBipedIkFootPositionReferenceWeight,
                manual_animator_biped_ik_foot_position_max_offset = _manualAnimatorBipedIkFootPositionReferenceMaxOffset,
                manual_animator_hips_local_position_enabled = _enableManualAnimatorHipsLocalPositionRuntimeOverride,
                manual_animator_hips_local_position_weight = _manualAnimatorHipsLocalPositionReferenceWeight,
                manual_animator_hips_local_position_max_offset = _manualAnimatorHipsLocalPositionReferenceMaxOffset,
                manual_animator_body_position_xz_enabled = _enableManualAnimatorBodyPositionXzRuntimeOverride,
                manual_animator_body_position_xz_weight = _manualAnimatorBodyPositionXzReferenceWeight,
                manual_animator_body_position_xz_max_offset = _manualAnimatorBodyPositionXzReferenceMaxOffset,
                manual_animator_body_position_xz_frame_gate_start =
                    _manualAnimatorBodyPositionXzReferenceFrameGateStart,
                manual_animator_body_position_xz_frame_gate_end =
                    _manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                manual_animator_body_position_xz_frame_gate_blend_frames =
                    _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                manual_animator_body_position_xz_axis_x_scale =
                    _manualAnimatorBodyPositionXzReferenceAxisXScale,
                manual_animator_body_position_xz_axis_z_scale =
                    _manualAnimatorBodyPositionXzReferenceAxisZScale,
                retarget_body_position_xz_root_motion_enabled =
                    _enableRetargetBodyPositionXzRootMotionRuntimeOverride,
                target_humanoid_bone_position_lock_disabled =
                    _disableTargetHumanoidBonePositionLockRuntimeOverride,
                vmd_playback_probe_enabled = _enableVmdPlaybackProbeRuntimeOverride,
                vmd_playback_probe_apply_ik_targets = _applyVmdPlaybackProbeIkTargetsRuntimeOverride,
                vmd_playback_probe_source_vmd_path = MakeProjectRelativePath(_vmdPlaybackProbeSourceVmdPath),
                reference_mmd_timing_enabled = _enableReferenceMmdTimingRuntimeOverride,
                diagnostic_capture_width_override = _diagnosticCaptureWidthOverride,
                diagnostic_capture_height_override = _diagnosticCaptureHeightOverride,
                diagnostic_screenshot_padding_override = _diagnosticScreenshotPaddingOverride,
                diagnostic_screenshot_vertical_viewport_center_override =
                    _diagnosticScreenshotVerticalViewportCenterOverride,
                reference_clip_name = _referenceClip != null ? _referenceClip.name : string.Empty,
                reference_clip_asset_path = _referenceClipAssetPath,
                results = Results.ToArray(),
                frame_count_roles = frameRoleDiagnostics,
                sample_ordering_diagnostics = BuildSampleOrderingDiagnostics(),
                selected_candidate_artifact = BuildCandidateArtifactSelection(frameQualitySummaries),
                frame_quality_summaries = frameQualitySummaries,
                failures = Failures.ToArray()
            };

            string json = JsonUtility.ToJson(summary, true);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        private static void WriteSummaryMarkdown(
            string path,
            MotionComparisonFrameQualitySummary[] frameQualitySummaries = null,
            SummaryFrameRoleDiagnostics frameRoleDiagnostics = null)
        {
            frameRoleDiagnostics = frameRoleDiagnostics ?? BuildCurrentSummaryFrameRoleDiagnostics();
            frameQualitySummaries = frameQualitySummaries ?? BuildFrameQualitySummaries(frameRoleDiagnostics);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# YYB Visual Comparison Batch");
            builder.AppendLine();
            builder.AppendLine($"- session id: `{_summarySessionId}`");
            builder.AppendLine($"- generated at: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            builder.AppendLine($"- fbx file: `{_fbxFileName}`");
            builder.AppendLine($"- duration seconds: `{_durationSeconds:F2}`");
            builder.AppendLine($"- target frames: `{ResolveSummaryTargetFrameCount()}`");
            builder.AppendLine($"- segment: `{_editorDiagnosticSmokeSegment}`");
            builder.AppendLine($"- finger closeups: `{_enableFingerCloseups}`");
            builder.AppendLine($"- recorder parent IK offsets (center-parented): `{_enableRecorderParentFrameIkOffsetsWhenCenterParented}`");
            builder.AppendLine($"- MMD IK delta guard runtime override VMD: `{FormatRuntimeOverride(_mmdIkDeltaGuardLimitOverrideVmd)}`");
            builder.AppendLine($"- MMD IK delta guard recovery trigger VMD: `{FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryTriggerVmd)}`");
            builder.AppendLine($"- MMD IK delta guard recovery debt VMD: `{FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryDebtThresholdVmd)}`");
            builder.AppendLine($"- MMD IK delta guard recovery hold frames: `{FormatRuntimeOverride(_mmdIkDeltaGuardRecoveryHoldFrames)}`");
            builder.AppendLine($"- Final IK foot grounding runtime override: `{_enableFinalIkFootGroundingRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator lower-body localRotation runtime override: `{_enableManualAnimatorFootLocalRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator lower-body localRotation runtime disable: `{_disableManualAnimatorFootLocalRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose runtime override: `{_enableManualAnimatorFullBodyPoseRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose runtime disable: `{_disableManualAnimatorFullBodyPoseRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose weight: `{_manualAnimatorFullBodyPoseReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator full-body pose exclude lower-body muscles: `{_manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose lower-body muscles only: `{_manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose leg twist muscles only: `{_manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose right arm muscles only: `{_manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose left arm muscles only: `{_manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose right sleeve chain muscles only: `{_manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator full-body pose frame gate: `{_manualAnimatorFullBodyPoseReferenceFrameGateStart:F1}-{_manualAnimatorFullBodyPoseReferenceFrameGateEnd:F1}`");
            builder.AppendLine($"- SetHumanPose right leg twist output reference: `{_enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride}`");
            builder.AppendLine($"- SetHumanPose right leg twist output reference weight: `{_setHumanPoseRightLegTwistOutputReferenceWeight:F3}`");
            builder.AppendLine($"- SetHumanPose right leg twist output reference max delta: `{_setHumanPoseRightLegTwistOutputReferenceMaxDelta:F3}`");
            builder.AppendLine($"- Manual Animator body rotation runtime override: `{_enableManualAnimatorBodyRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator body rotation runtime disable: `{_disableManualAnimatorBodyRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator body rotation weight: `{_manualAnimatorBodyRotationReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator hand local rotation runtime override: `{_enableManualAnimatorHandLocalRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator thumb local rotation runtime override: `{_enableManualAnimatorThumbLocalRotationRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator hand palm-frame runtime override: `{_enableManualAnimatorHandPalmFrameRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator hand palm-frame weight: `{_manualAnimatorHandPalmFrameWeight:F3}`");
            builder.AppendLine($"- Retarget pose visual spike smoothing runtime settings override: `{_overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings}`");
            builder.AppendLine($"- Retarget pose visual spike smoothing enabled: `{_enableRetargetPoseVisualSpikeSmoothingRuntimeOverride}`");
            builder.AppendLine($"- Retarget pose visual spike current weight: `{_retargetPoseVisualSpikeCurrentWeight:F3}`");
            builder.AppendLine($"- Retarget pose visual spike forearm stretch clamp max offset: `{_retargetPoseVisualSpikeForearmStretchClampMaxOffset:F3}`");
            builder.AppendLine($"- Retarget arm stretch clamp runtime override: `{_enableRetargetArmStretchClampRuntimeOverride}`");
            builder.AppendLine($"- Retarget arm stretch muscle limit: `{_retargetArmStretchMuscleLimit:F3}`");
            builder.AppendLine($"- YYB arm swing limit runtime override: `{_enableYybArmSwingLimitRuntimeOverride}`");
            builder.AppendLine($"- YYB arm swing limit weight: `{_yybArmSwingLimitWeight:F3}`");
            builder.AppendLine($"- YYB arm swing max down dot: `{_yybArmSwingMaxDownDot:F3}`");
            builder.AppendLine($"- YYB arm swing min hand horizontal ratio: `{_yybArmSwingMinHandHorizontalRatio:F3}`");
            builder.AppendLine($"- YYB arm swing max hand below shoulder ratio: `{_yybArmSwingMaxHandBelowShoulderRatio:F3}`");
            builder.AppendLine($"- YYB arm swing horizontal reach limit weight: `{_yybArmSwingHorizontalReachLimitWeight:F3}`");
            builder.AppendLine($"- YYB arm swing max hand horizontal reach ratio: `{_yybArmSwingMaxHandHorizontalReachRatio:F3}`");
            builder.AppendLine($"- YYB arm swing horizontal reach max hand below shoulder ratio: `{_yybArmSwingHorizontalReachMaxHandBelowShoulderRatio:F3}`");
            builder.AppendLine($"- YYB arm swing horizontal reach min elbow angle after apply: `{_yybArmSwingHorizontalReachMinElbowAngleAfterApply:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose horizontal reach limit weight: `{_yybArmSwingRaisedPoseHorizontalReachLimitWeight:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose min upper-arm down dot: `{_yybArmSwingRaisedPoseMinUpperArmDownDot:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose max hand below shoulder ratio: `{_yybArmSwingRaisedPoseMaxHandBelowShoulderRatio:F3}`");
            builder.AppendLine($"- YYB arm swing raised-pose max hand horizontal reach ratio: `{_yybArmSwingRaisedPoseMaxHandHorizontalReachRatio:F3}`");
            builder.AppendLine($"- YYB arm direction retarget runtime override: `{_enableYybArmDirectionRetargetRuntimeOverride}`");
            builder.AppendLine($"- YYB arm direction upper-arm weight: `{_yybArmDirectionUpperArmWeight:F3}`");
            builder.AppendLine($"- YYB arm direction forearm weight: `{_yybArmDirectionForearmWeight:F3}`");
            builder.AppendLine($"- YYB arm direction upper-arm max degrees: `{_yybArmDirectionUpperArmMaxDegrees:F3}`");
            builder.AppendLine($"- YYB arm direction forearm max degrees: `{_yybArmDirectionForearmMaxDegrees:F3}`");
            builder.AppendLine($"- YYB arm direction left-side weight scale: `{_yybArmDirectionLeftSideWeightScale:F3}`");
            builder.AppendLine($"- YYB arm direction right-side weight scale: `{_yybArmDirectionRightSideWeightScale:F3}`");
            builder.AppendLine($"- YYB arm sleeve anchor runtime settings override: `{_overrideYybArmSleeveAnchorRuntimeSettings}`");
            builder.AppendLine($"- YYB arm sleeve anchor runtime enabled: `{_enableYybArmSleeveAnchorRuntimeOverride}`");
            builder.AppendLine($"- YYB arm sleeve anchor influence: `{_yybArmSleeveAnchorInfluence:F3}`");
            builder.AppendLine($"- YYB arm shoulder cap anchor influence: `{_yybArmShoulderCapAnchorInfluence:F3}`");
            builder.AppendLine($"- YYB arm sleeve anchor max degrees: `{_yybArmSleeveAnchorMaxDegrees:F3}`");
            builder.AppendLine($"- YYB arm visual twist runtime settings override: `{_overrideYybArmVisualTwistRuntimeSettings}`");
            builder.AppendLine($"- YYB arm visual twist runtime enabled: `{_enableYybArmVisualTwistRuntimeOverride}`");
            builder.AppendLine($"- YYB arm visual upper-arm influence: `{_yybArmVisualUpperArmInfluence:F3}`");
            builder.AppendLine($"- YYB arm visual forearm influence: `{_yybArmVisualForearmInfluence:F3}`");
            builder.AppendLine($"- YYB arm visual upper-arm max degrees: `{_yybArmVisualUpperArmMaxDegrees:F3}`");
            builder.AppendLine($"- YYB arm visual forearm max degrees: `{_yybArmVisualForearmMaxDegrees:F3}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction runtime override: `{_enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction runtime disable: `{_disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction weight: `{_manualAnimatorLowerBodySegmentDirectionReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator lower-body segment direction max angle: `{_manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator UpperLegToLowerLeg segment direction runtime disable: `{_disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator UpperLegToLowerLeg segment direction max angle override: `{_manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator LowerLegToFoot segment direction runtime disable: `{_disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator LowerLegToFoot segment direction max angle override: `{_manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator Left LowerLegToFoot segment direction max angle override: `{_manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction max angle override: `{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction axis X/Z scale: `{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction blend weight: `{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight:F3}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction frame gate: `{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart:F0}-{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd:F0}`");
            builder.AppendLine($"- Manual Animator Right LowerLegToFoot segment direction endpoint blend weight: `{_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight:F3}`");
            builder.AppendLine($"- Manual Animator FootToToes segment direction runtime disable: `{_disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator FootToToes segment direction max angle override: `{_manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw runtime override: `{_enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw runtime disable: `{_disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw weight: `{_manualAnimatorFootHipsAlignedResidualYawReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator foot hips-aligned residual yaw max angle: `{_manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position runtime override: `{_enablePostSetHumanPoseRightEndpointPositionRuntimeOverride}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position weight: `{_postSetHumanPoseRightEndpointPositionReferenceWeight:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position max offset: `{_postSetHumanPoseRightEndpointPositionReferenceMaxOffset:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position positive-Z scale: `{_postSetHumanPoseRightEndpointPositionReferencePositiveZScale:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position toes blend weight: `{_postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight:F3}`");
            builder.AppendLine($"- Post-SetHumanPose right endpoint position frame gate: `{_postSetHumanPoseRightEndpointPositionReferenceFrameGateStart:F0}-{_postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd:F0}`");
            builder.AppendLine($"- Post-SetHumanPose endpoint position use left side: `{_postSetHumanPoseEndpointPositionUseLeftSide}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position runtime override: `{_enablePreSetHumanPoseRightEndpointPositionRuntimeOverride}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position weight: `{_preSetHumanPoseRightEndpointPositionReferenceWeight:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position max offset: `{_preSetHumanPoseRightEndpointPositionReferenceMaxOffset:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position positive-Z scale: `{_preSetHumanPoseRightEndpointPositionReferencePositiveZScale:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position toes blend weight: `{_preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight:F3}`");
            builder.AppendLine($"- Pre-SetHumanPose right endpoint position frame gate: `{_preSetHumanPoseRightEndpointPositionReferenceFrameGateStart:F0}-{_preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd:F0}`");
            builder.AppendLine($"- Pre-SetHumanPose endpoint position use left side: `{_preSetHumanPoseEndpointPositionUseLeftSide}`");
            builder.AppendLine($"- Pre-SetHumanPose endpoint position use ghost/current basis: `{_preSetHumanPoseEndpointPositionUseGhostCurrentBasis}`");
            builder.AppendLine($"- Pre-SetHumanPose endpoint bodyPosition invert X/Z: `{_preSetHumanPoseEndpointPositionInvertBodyPositionX}/{_preSetHumanPoseEndpointPositionInvertBodyPositionZ}`");
            builder.AppendLine($"- Post-SetHumanPose right foot evaluator X/Z reference: `{_usePostSetHumanPoseRightFootEvaluatorXzReference}`");
            builder.AppendLine($"- Post-SetHumanPose right foot evaluator X/Z target magnitude: `{_postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude:F3}`");
            builder.AppendLine($"- Manual Animator BipedIK foot position runtime override: `{_enableManualAnimatorBipedIkFootPositionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator BipedIK foot position weight: `{_manualAnimatorBipedIkFootPositionReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator BipedIK foot position max offset: `{_manualAnimatorBipedIkFootPositionReferenceMaxOffset:F3}`");
            builder.AppendLine($"- Manual Animator Hips local-position runtime override: `{_enableManualAnimatorHipsLocalPositionRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator Hips local-position weight: `{_manualAnimatorHipsLocalPositionReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator Hips local-position max offset: `{_manualAnimatorHipsLocalPositionReferenceMaxOffset:F3}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z runtime override: `{_enableManualAnimatorBodyPositionXzRuntimeOverride}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z weight: `{_manualAnimatorBodyPositionXzReferenceWeight:F3}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z max offset: `{_manualAnimatorBodyPositionXzReferenceMaxOffset:F3}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z frame gate: `{_manualAnimatorBodyPositionXzReferenceFrameGateStart:F0}-{_manualAnimatorBodyPositionXzReferenceFrameGateEnd:F0}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z frame gate blend frames: `{_manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames:F0}`");
            builder.AppendLine($"- Manual Animator bodyPosition X/Z axis scale: `{_manualAnimatorBodyPositionXzReferenceAxisXScale:F3}/{_manualAnimatorBodyPositionXzReferenceAxisZScale:F3}`");
            builder.AppendLine($"- Retarget bodyPosition X/Z root motion runtime override: `{_enableRetargetBodyPositionXzRootMotionRuntimeOverride}`");
            builder.AppendLine($"- Target humanoid bone position lock disabled runtime override: `{_disableTargetHumanoidBonePositionLockRuntimeOverride}`");
            builder.AppendLine($"- VMD playback probe runtime override: `{_enableVmdPlaybackProbeRuntimeOverride}`");
            builder.AppendLine($"- VMD playback probe apply IK targets: `{_applyVmdPlaybackProbeIkTargetsRuntimeOverride}`");
            builder.AppendLine($"- VMD playback probe source VMD: `{EscapeMarkdown(MakeProjectRelativePath(_vmdPlaybackProbeSourceVmdPath))}`");
            builder.AppendLine($"- reference MMD timing runtime override: `{_enableReferenceMmdTimingRuntimeOverride}`");
            builder.AppendLine($"- diagnostic capture width override: `{FormatRuntimeOverride(_diagnosticCaptureWidthOverride)}`");
            builder.AppendLine($"- diagnostic capture height override: `{FormatRuntimeOverride(_diagnosticCaptureHeightOverride)}`");
            builder.AppendLine($"- diagnostic screenshot padding override: `{FormatDiagnosticScreenshotFramingOverride(_diagnosticScreenshotPaddingOverride)}`");
            builder.AppendLine($"- diagnostic screenshot viewport center override: `{FormatDiagnosticScreenshotFramingOverride(_diagnosticScreenshotVerticalViewportCenterOverride)}`");
            builder.AppendLine($"- reference clip: `{(_referenceClip != null ? _referenceClip.name : "")}`");
            builder.AppendLine($"- reference clip asset: `{EscapeMarkdown(_referenceClipAssetPath)}`");
            builder.AppendLine();

            builder.AppendLine("## Frame Count Roles");
            builder.AppendLine();
            builder.AppendLine($"- ref target: `{frameRoleDiagnostics.reference_target_frame_count}` ({EscapeMarkdown(frameRoleDiagnostics.target_frame_count_role)})");
            builder.AppendLine($"- Sub_Manual baseline recorded frames: `{frameRoleDiagnostics.baseline_recorded_frame_count}` ({EscapeMarkdown(frameRoleDiagnostics.baseline_recorded_frame_count_role)})");
            builder.AppendLine($"- Main_Auto candidate recorded frames: `{frameRoleDiagnostics.candidate_recorded_frame_count}` ({EscapeMarkdown(frameRoleDiagnostics.candidate_recorded_frame_count_role)})");
            builder.AppendLine($"- metric basis: {EscapeMarkdown(frameRoleDiagnostics.frame_quality_metric_basis)}");
            builder.AppendLine();
            builder.AppendLine("## Reference MP4 Diagnostics");
            builder.AppendLine();
            builder.AppendLine($"- provenance: `{EscapeMarkdown(frameRoleDiagnostics.reference_mp4_provenance_evidence_path)}` (exists={frameRoleDiagnostics.reference_mp4_provenance_evidence_exists})");
            builder.AppendLine($"- analysis result: `{EscapeMarkdown(frameRoleDiagnostics.reference_mp4_analysis_result_path)}` (exists={frameRoleDiagnostics.reference_mp4_analysis_result_exists})");
            builder.AppendLine($"- frame metrics: `{EscapeMarkdown(frameRoleDiagnostics.reference_mp4_frame_metrics_path)}` (exists={frameRoleDiagnostics.reference_mp4_frame_metrics_exists})");
            builder.AppendLine($"- contact sheet: `{EscapeMarkdown(frameRoleDiagnostics.reference_mp4_contact_sheet_path)}` (exists={frameRoleDiagnostics.reference_mp4_contact_sheet_exists})");
            builder.AppendLine($"- canonical context: {EscapeMarkdown(frameRoleDiagnostics.reference_mp4_canonical_context)}");
            builder.AppendLine($"- video: `{frameRoleDiagnostics.reference_mp4_width}x{frameRoleDiagnostics.reference_mp4_height}`, `{EscapeMarkdown(frameRoleDiagnostics.reference_mp4_avg_frame_rate)}`, frames `{frameRoleDiagnostics.reference_mp4_total_video_frames}`, duration `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_stream_duration_seconds)}`");
            builder.AppendLine($"- bbox metrics: samples `{frameRoleDiagnostics.reference_mp4_frame_metrics_sample_count}`, avg height `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_avg_bbox_height_ratio)}`, avg width `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_avg_bbox_width_ratio)}`, center X range `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_center_x_range_ratio)}`, max bottom gap `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_max_bottom_gap_ratio)}`");
            builder.AppendLine($"- current clip coverage: start `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_start_seconds)}`, end `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_end_seconds)}`, duration `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_duration_seconds)}`, samples `{frameRoleDiagnostics.reference_mp4_current_clip_sample_count}`, first local `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_first_sample_seconds)}`, last local `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_last_sample_seconds)}`, coverage `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_sample_coverage_ratio)}`, gap `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_sample_gap_seconds)}`");
            builder.AppendLine($"- current clip bbox metrics: avg height `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_avg_bbox_height_ratio)}`, avg width `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_avg_bbox_width_ratio)}`, center X range `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_center_x_range_ratio)}`, max bottom gap `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_max_bottom_gap_ratio)}`, avg bright area `{FormatQualityFloat(frameRoleDiagnostics.reference_mp4_current_clip_avg_bright_area_ratio)}`");
            builder.AppendLine($"- current clip coverage basis: {EscapeMarkdown(frameRoleDiagnostics.reference_mp4_current_clip_sample_basis)}");
            builder.AppendLine($"- current clip framing basis: {EscapeMarkdown(frameRoleDiagnostics.reference_mp4_current_clip_framing_metric_basis)}");
            builder.AppendLine($"- candidate screenshot framing: index `{EscapeMarkdown(frameRoleDiagnostics.candidate_screenshot_frame_index_path)}` (exists={frameRoleDiagnostics.candidate_screenshot_frame_index_exists}), view `{EscapeMarkdown(frameRoleDiagnostics.candidate_screenshot_frame_metrics_view)}`, samples `{frameRoleDiagnostics.candidate_screenshot_frame_metrics_sample_count}`, nonblank `{frameRoleDiagnostics.candidate_screenshot_nonblank_frame_count}`, avg height `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_avg_bbox_height_ratio)}`, avg width `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_avg_bbox_width_ratio)}`, center X range `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_center_x_range_ratio)}`, max bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_max_bottom_gap_ratio)}`, max top gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_max_top_gap_ratio)}`, avg bright area `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_avg_bright_area_ratio)}`");
            builder.AppendLine($"- candidate screenshot timing: samples `{frameRoleDiagnostics.candidate_screenshot_time_sample_count}`, first `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_first_sample_seconds)}`, last `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_last_sample_seconds)}`, coverage `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_sample_coverage_ratio)}`, gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_sample_gap_seconds)}`, max ref gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_max_ref_sample_seconds_gap)}`, avg ref gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_screenshot_avg_ref_sample_seconds_gap)}`");
            builder.AppendLine($"- candidate/ref time-matched framing: samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_sample_count}`, max time gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_seconds_gap)}`, avg bbox height abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_bbox_height_ratio_abs_delta)}`, max bbox height abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta)}`, avg bbox width abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_bbox_width_ratio_abs_delta)}`, max bbox width abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_width_ratio_abs_delta)}`, avg center X abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_center_x_ratio_abs_delta)}`, max bottom gap abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta)}`, avg bright area abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_bright_area_ratio_abs_delta)}`");
            builder.AppendLine($"- candidate/ref time-matched limb bands: samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_limb_band_sample_count}`, avg upper span abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_upper_limb_span_ratio_abs_delta)}`, max upper span abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_upper_limb_span_ratio_abs_delta)}`, avg lower span abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_lower_limb_span_ratio_abs_delta)}`, max lower span abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_lower_limb_span_ratio_abs_delta)}`");
            builder.AppendLine($"- candidate/ref time-matched silhouette profile: bands `{frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_profile_band_count}`, samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_profile_sample_count}`, avg L1 abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_silhouette_profile_l1_abs_delta)}`, max L1 abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta)}`, max band abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta)}`");
            builder.AppendLine($"- candidate/ref time-matched silhouette landmarks: bands `{frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_landmark_band_count}`, samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_landmark_sample_count}`, avg endpoint abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_silhouette_landmark_endpoint_abs_delta)}`, max endpoint abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta)}`");
            builder.AppendLine($"- candidate/ref time-matched image-space keypoints: keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_image_space_keypoint_count}`, samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_image_space_keypoint_sample_count}`, avg L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta)}`, max L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta)}`");
            builder.AppendLine($"- candidate/ref bbox-normalized keypoints: keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_count}`, samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_sample_count}`, avg L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_bbox_normalized_image_space_keypoint_l1_delta)}`, max L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta)}`, avg removed `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta_removed_by_bbox_normalization)}`, max removed `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta_removed_by_bbox_normalization)}`");
            builder.AppendLine($"- candidate/ref non-hair bbox-normalized keypoints: keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_count}`, samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_sample_count}`, avg L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_avg_bbox_normalized_image_space_keypoint_l1_delta)}`, max L1 delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta)}`, max label `{EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_label)}`");
            builder.AppendLine($"- candidate/ref bbox-normalized max keypoint attribution: label `{EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label)}`, keypoint `{frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_index}`, ref seconds `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_seconds)}`, candidate seconds `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_seconds)}`, recorder frame `{frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_recorder_frame}`, x delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_x_delta)}`, y delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_y_delta)}`");
            builder.AppendLine($"- candidate/ref bbox-normalized max keypoint crop context: ref touches edge `{frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_touches_frame_edge}`, candidate touches edge `{frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_touches_frame_edge}`, ref bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_bottom_gap)}`, ref top gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_top_gap)}`, candidate bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_bottom_gap)}`, candidate top gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_top_gap)}`");
            builder.AppendLine($"- candidate/ref non-hair bbox-normalized max keypoint attribution: label `{EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_label)}`, keypoint `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_index}`, ref seconds `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_seconds)}`, candidate seconds `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_seconds)}`, recorder frame `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_recorder_frame}`, x delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_x_delta)}`, y delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_y_delta)}`");
            builder.AppendLine($"- candidate/ref non-hair bbox-normalized max keypoint crop context: ref touches edge `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_touches_frame_edge}`, candidate touches edge `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_touches_frame_edge}`, ref bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_bottom_gap)}`, ref top gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_top_gap)}`, candidate bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_bottom_gap)}`, candidate top gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_top_gap)}`");
            builder.AppendLine($"- candidate/ref crop-safe time-matched: samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_sample_count}`, avg bbox width abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_width_ratio_abs_delta)}`, max bbox width abs delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_width_ratio_abs_delta)}`, silhouette samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_silhouette_profile_sample_count}`, avg silhouette L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_avg_silhouette_profile_l1_abs_delta)}`, max silhouette L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_max_silhouette_profile_l1_abs_delta)}`");
            builder.AppendLine($"- candidate/ref crop-safe keypoints: image samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_image_space_keypoint_sample_count}`, avg image L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_avg_image_space_keypoint_l1_delta)}`, max image L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_max_image_space_keypoint_l1_delta)}`, bbox-normalized samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_bbox_normalized_image_space_keypoint_sample_count}`, avg bbox-normalized L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta)}`, max bbox-normalized L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta)}`");
            builder.AppendLine($"- candidate/ref keypoint-local crop-safe bbox-normalized keypoints: samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count}`, keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count}`, excluded keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count}`, avg L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta)}`, max L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta)}`, max label `{EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label)}`");
            builder.AppendLine($"- candidate/ref non-hair keypoint-local crop-safe bbox-normalized keypoints: samples `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count}`, keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count}`, excluded keypoints `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count}`, avg L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_avg_bbox_normalized_image_space_keypoint_l1_delta)}`, max L1 `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta)}`, max label `{EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label)}`");
            builder.AppendLine($"- candidate/ref non-hair keypoint-local crop-safe max attribution: keypoint `{frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_index}`, x delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_x_delta)}`, y delta `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_y_delta)}`, candidate x/y `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_candidate_x)}`/`{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_candidate_y)}`, reference x/y `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_reference_x)}`/`{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_reference_y)}`, required x reduction to `{ReferenceAlignedVisualEvidenceMaxBBoxNormalizedImageSpaceKeypointL1Delta:F2}` `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_required_x_reduction_to_threshold)}`");
            builder.AppendLine($"- candidate vs ref framing deltas: avg height `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_avg_bbox_height_ratio_delta)}`, avg width `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_avg_bbox_width_ratio_delta)}`, center X range `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_center_x_range_ratio_delta)}`, max bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_max_bottom_gap_ratio_delta)}`, avg bright area `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_avg_bright_area_ratio_delta)}`");
            builder.AppendLine($"- candidate vs current-clip ref framing deltas: avg height `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_current_clip_avg_bbox_height_ratio_delta)}`, avg width `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_current_clip_avg_bbox_width_ratio_delta)}`, center X range `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_current_clip_center_x_range_ratio_delta)}`, max bottom gap `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_current_clip_max_bottom_gap_ratio_delta)}`, avg bright area `{FormatQualityFloat(frameRoleDiagnostics.candidate_vs_reference_current_clip_avg_bright_area_ratio_delta)}`");
            builder.AppendLine($"- candidate screenshot basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_screenshot_frame_metrics_basis)}");
            builder.AppendLine($"- candidate screenshot timing basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_screenshot_sample_timing_basis)}");
            builder.AppendLine($"- candidate/ref time-matched basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_framing_metric_basis)}");
            builder.AppendLine($"- candidate/ref image-space limb span basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_image_space_limb_span_basis)}");
            builder.AppendLine($"- candidate/ref image-space limb band basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_image_space_limb_band_basis)}");
            builder.AppendLine($"- candidate/ref silhouette profile basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_profile_basis)}");
            builder.AppendLine($"- candidate/ref silhouette landmark basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_silhouette_landmark_basis)}");
            builder.AppendLine($"- candidate/ref image-space keypoint basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_image_space_keypoint_basis)}");
            builder.AppendLine($"- candidate/ref bbox-normalized keypoint basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_basis)}");
            builder.AppendLine($"- candidate/ref non-hair bbox-normalized keypoint basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_basis)}");
            builder.AppendLine($"- candidate/ref crop-safe basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_crop_safe_basis)}");
            builder.AppendLine($"- candidate/ref keypoint-local crop-safe basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_keypoint_local_crop_safe_basis)}");
            builder.AppendLine($"- candidate/ref non-hair keypoint-local crop-safe basis: {EscapeMarkdown(frameRoleDiagnostics.candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_basis)}");
            builder.AppendLine($"- basis: {EscapeMarkdown(frameRoleDiagnostics.reference_mp4_analysis_metric_basis)}");
            builder.AppendLine();

            builder.AppendLine("## Results");
            builder.AppendLine();
            builder.AppendLine("| job | scene | target | success | session | csv | frames | vmd |");
            builder.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (CaptureResult result in Results)
            {
                builder.AppendLine(
                    $"| {EscapeMarkdown(result.jobDisplayName)} | {EscapeMarkdown(result.sceneName)} | {EscapeMarkdown(result.targetName)} | {result.success} | " +
                    $"`{EscapeMarkdown(result.comparisonSessionId)}` | `{EscapeMarkdown(result.comparisonMetricsCsvPath)}` | " +
                    $"`{EscapeMarkdown(result.comparisonFrameFolderPath)}` | `{EscapeMarkdown(result.vmdPath)}` |");
            }

            CaptureResult[] effectiveSettingsResults = Results
                .Where(result => result.hasFBXVmdPipelineEffectiveSettings)
                .ToArray();
            if (effectiveSettingsResults.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Main Scene Effective Settings");
                builder.AppendLine();
                builder.AppendLine("| job | foot local rot | full-body pose | body rot | lower segment | foot yaw | post-set endpoint | pre-set endpoint | evaluator X/Z | arm swing | sleeve | visual twist |");
                builder.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (CaptureResult result in effectiveSettingsResults)
                {
                    builder.AppendLine(
                        $"| {EscapeMarkdown(result.jobDisplayName)} | " +
                        $"{FormatEnabledWeight(result.ShouldUseManualAnimatorFootLocalRotationReference, result.manualAnimatorFootLocalRotationReferenceWeight)} | " +
                        $"{FormatEnabledWeight(result.ShouldUseManualAnimatorFullBodyPoseReference, result.manualAnimatorFullBodyPoseReferenceWeight)} | " +
                        $"{FormatEnabledWeight(result.ShouldUseManualAnimatorBodyRotationReference, result.manualAnimatorBodyRotationReferenceWeight)} | " +
                        $"{FormatEnabledWeightCap(result.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, result.manualAnimatorLowerBodySegmentDirectionReferenceWeight, result.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle)} | " +
                        $"{FormatEnabledWeightCap(result.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference, result.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, result.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle)} | " +
                        $"{FormatEnabledWeightCapScaleBlendGate(result.usePostSetHumanPoseRightEndpointPositionReference, result.postSetHumanPoseRightEndpointPositionReferenceWeight, result.postSetHumanPoseRightEndpointPositionReferenceMaxOffset, result.postSetHumanPoseRightEndpointPositionReferencePositiveZScale, result.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, result.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart, result.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd)} | " +
                        $"{FormatEnabledWeightCapScaleBlendGate(result.usePreSetHumanPoseRightEndpointPositionReference, result.preSetHumanPoseRightEndpointPositionReferenceWeight, result.preSetHumanPoseRightEndpointPositionReferenceMaxOffset, result.preSetHumanPoseRightEndpointPositionReferencePositiveZScale, result.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, result.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart, result.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd)} | " +
                        $"{FormatEvaluatorXzReferenceSettings(result)} | " +
                        $"{FormatArmSwingSettings(result)} | " +
                        $"{result.enableYybArmSleeveAnchorCorrection} | " +
                        $"{result.enableYybArmVisualTwistCorrection} |");
                }
            }

            SummarySampleOrderingDiagnostic[] sampleOrderingDiagnostics = BuildSampleOrderingDiagnostics();
            if (sampleOrderingDiagnostics.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Sample Ordering Diagnostics");
                builder.AppendLine();
                builder.AppendLine("| job | scene | rows | first reason | first recorderFrame | first engine frame | recorder span | engine span | first clip time | first grounding step | first step/max | first step at max | grounding clamp delta | grounding smooth delta | finish recorderFrame | finish engine frame |");
                builder.AppendLine("|---|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|");
                foreach (SummarySampleOrderingDiagnostic diagnostic in sampleOrderingDiagnostics)
                {
                    builder.AppendLine(
                        $"| {EscapeMarkdown(diagnostic.job_mode)} | {EscapeMarkdown(diagnostic.scene_name)} | {diagnostic.metric_row_count} | " +
                        $"{EscapeMarkdown(diagnostic.first_metric_reason)} | {diagnostic.first_metric_recorder_frame} | " +
                        $"{diagnostic.first_metric_engine_frame_count} | {diagnostic.recording_metric_recorder_frame_span} | " +
                        $"{diagnostic.recording_metric_engine_frame_span} | {FormatQualityFloat(diagnostic.first_metric_animation_clip_time)} | " +
                        $"{FormatQualityFloat(diagnostic.first_metric_grounding_vertical_step_last)} | " +
                        $"{FormatQualityFloat(diagnostic.first_metric_grounding_vertical_step_to_max_ratio)} | " +
                        $"{diagnostic.first_metric_grounding_vertical_step_at_max_step} | " +
                        $"{diagnostic.recording_grounding_step_clamp_delta} | {diagnostic.recording_grounding_smoothed_delta} | " +
                        $"{diagnostic.finish_metric_recorder_frame} | {diagnostic.finish_metric_engine_frame_count} |");
                }
            }

            SummaryCandidateArtifactSelection selectedCandidate = BuildCandidateArtifactSelection(frameQualitySummaries);
            if (selectedCandidate != null && !string.IsNullOrWhiteSpace(selectedCandidate.selected_candidate_vmd_path))
            {
                builder.AppendLine();
                builder.AppendLine("## Selected Candidate Artifact");
                builder.AppendLine();
                builder.AppendLine("| selected role | output role | status | acceptance artifact | metrics | vmd | manifest | files | raw status | corrected status | preserves raw diagnostic | basis |");
                builder.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
                builder.AppendLine(
                    $"| {EscapeMarkdown(selectedCandidate.selected_candidate_role)} | {EscapeMarkdown(selectedCandidate.selected_candidate_output_role)} | " +
                    $"{EscapeMarkdown(selectedCandidate.selected_candidate_status)} | {selectedCandidate.selected_candidate_is_acceptance_artifact} | " +
                    $"`{EscapeMarkdown(selectedCandidate.selected_candidate_metrics_csv)}` | " +
                    $"`{EscapeMarkdown(selectedCandidate.selected_candidate_vmd_path)}` | " +
                    $"`{EscapeMarkdown(selectedCandidate.selected_candidate_manifest_path)}` | " +
                    $"vmd={selectedCandidate.selected_candidate_vmd_exists}, metrics={selectedCandidate.selected_candidate_metrics_exists}, manifest={selectedCandidate.selected_candidate_manifest_exists}, rawVmdDiff={selectedCandidate.selected_candidate_differs_from_raw_vmd}, rawMetricsDiff={selectedCandidate.selected_candidate_differs_from_raw_metrics} | " +
                    $"{EscapeMarkdown(selectedCandidate.raw_candidate_status)} | {EscapeMarkdown(selectedCandidate.corrected_candidate_status)} | " +
                    $"{selectedCandidate.selected_candidate_preserves_raw_diagnostic} | " +
                    $"{EscapeMarkdown(selectedCandidate.selected_candidate_acceptance_basis)}; {EscapeMarkdown(selectedCandidate.selection_basis)} |");
            }

            if (frameQualitySummaries.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Frame Quality Gate");
                builder.AppendLine();
                builder.AppendLine("| baseline | candidate | evaluation | status | mmd | compared frames | foot min Y | root delta | center step | local foot IK min Y | effective foot IK min Y | metrics | mmd screenshot | mmd report | vmd | reason |");
                builder.AppendLine("|---|---|---|---|---:|---:|---:|---:|---:|---:|---|---|---|---|---|---|");
                foreach (MotionComparisonFrameQualitySummary summary in frameQualitySummaries)
                {
                    builder.AppendLine(
                        $"| {EscapeMarkdown(summary.baseline_label)} | {EscapeMarkdown(summary.candidate_label)} | {EscapeMarkdown(summary.frame_quality_evaluation_role)} | {EscapeMarkdown(summary.status)} | " +
                        $"{EscapeMarkdown(summary.mmd_result_status)} | {summary.compared_frames} | {FormatQualityFloat(summary.min_candidate_foot_bottom_y)} | " +
                        $"{FormatQualityFloat(summary.max_same_frame_root_position_delta)} | {FormatQualityFloat(summary.max_candidate_vmd_center_step)} | " +
                        $"{FormatQualityFloat(summary.min_candidate_vmd_foot_ik_y)} | {FormatQualityFloat(summary.min_candidate_vmd_effective_foot_ik_y)} | " +
                        $"`{EscapeMarkdown(summary.candidate_metrics_csv)}` | " +
                        $"`{EscapeMarkdown(summary.mmd_after_play_screenshot_path)}` | `{EscapeMarkdown(summary.mmd_report_path)}` | " +
                        $"`{EscapeMarkdown(summary.candidate_vmd_path)}` | " +
                        $"{EscapeMarkdown(summary.status_reason)} |");
                }
            }

            if (Failures.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Failures");
                builder.AppendLine();
                foreach (string failure in Failures)
                {
                    builder.AppendLine($"- {EscapeMarkdown(failure)}");
                }
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }
    }
}
#endif
