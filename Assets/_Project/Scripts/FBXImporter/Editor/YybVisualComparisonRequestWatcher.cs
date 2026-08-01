#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Modules.FBXImporter.EditorTools
{
    [InitializeOnLoad]
    public static class YybVisualComparisonRequestWatcher
    {
        private const string RuntimeDirectory = "Docs/Workflow/Local/runtime";
        private const string RequestFileName = "yyb_visual_compare_request.json";
        private const string StatusFileName = "yyb_visual_compare_status.json";
        private const string BootMarkerFileName = "yyb_visual_compare_watcher_boot.txt";
        private const string TraceFileName = "yyb_visual_compare_watcher_trace.log";
        private const string AwaitingCompletionSessionKey = "Fbx2Vmd.YybVisualComparison.WatcherAwaitingCompletion";
        private const string ActiveRequestIdSessionKey = "Fbx2Vmd.YybVisualComparison.WatcherActiveRequestId";
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
        private static readonly string ProjectRoot;
        private static readonly string RequestPath;
        private static readonly string StatusPath;
        private static readonly string TracePath;
        private static bool _awaitingCompletion;
        private static string _activeRequestId = string.Empty;
        private static DateTime _nextPollUtc = DateTime.MinValue;

        static YybVisualComparisonRequestWatcher()
        {
            ProjectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string runtimeDirectoryPath = Path.Combine(ProjectRoot, RuntimeDirectory);
            RequestPath = Path.Combine(runtimeDirectoryPath, RequestFileName);
            StatusPath = Path.Combine(runtimeDirectoryPath, StatusFileName);
            TracePath = Path.Combine(runtimeDirectoryPath, TraceFileName);
            Directory.CreateDirectory(runtimeDirectoryPath);
            File.WriteAllText(
                Path.Combine(runtimeDirectoryPath, BootMarkerFileName),
                DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
            AppendTrace($"loaded request={RequestPath} status={StatusPath}");

            _activeRequestId = SessionState.GetString(ActiveRequestIdSessionKey, string.Empty);
            _awaitingCompletion = SessionState.GetBool(AwaitingCompletionSessionKey, false) &&
                                  !string.IsNullOrWhiteSpace(_activeRequestId);

            if (_awaitingCompletion &&
                !YybVisualComparisonBatchRunner.IsRunning &&
                !YybVisualComparisonBatchRunner.HasPersistedRunState())
            {
                AppendTrace($"clearing stale watcher state activeRequestId={_activeRequestId}");
                ClearAwaitingCompletionState();
            }

            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            if (_awaitingCompletion)
            {
                YybVisualComparisonBatchRunner.RunCompleted -= HandleRunCompleted;
                YybVisualComparisonBatchRunner.RunCompleted += HandleRunCompleted;
                AppendTrace($"resubscribed activeRequestId={_activeRequestId}");
                if (!File.Exists(StatusPath))
                {
                    WriteStatus(new StatusEnvelope
                    {
                        request_id = _activeRequestId,
                        status = "running",
                        updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                        message = "watcher resumed after domain reload",
                        passed = false,
                        failures = Array.Empty<string>()
                    });
                }
            }
            Debug.Log("[YybVisualComparisonRequestWatcher] loaded");
        }

        [Serializable]
        private sealed class RequestEnvelope
        {
            public string request_id;
            public string requested_at;
            public string fbx_file;
            public float duration_seconds = 31f;
            public bool finger_closeups;
            public float mmd_ik_delta_guard_limit_vmd = float.NaN;
            public float mmd_ik_delta_guard_recovery_trigger_vmd = float.NaN;
            public float mmd_ik_delta_guard_recovery_debt_vmd = float.NaN;
            public int mmd_ik_delta_guard_recovery_hold_frames;
            public bool final_ik_foot_grounding_enabled;
            public bool manual_animator_foot_local_rotation_enabled;
            public bool manual_animator_foot_local_rotation_disabled;
            public bool manual_animator_full_body_pose_enabled;
            public bool manual_animator_full_body_pose_disabled;
            public float manual_animator_full_body_pose_weight =
                DefaultManualAnimatorFullBodyPoseReferenceWeight;
            public bool manual_animator_full_body_pose_exclude_lower_body_muscles;
            public bool manual_animator_full_body_pose_lower_body_muscles_only;
            public bool manual_animator_full_body_pose_leg_twist_muscles_only;
            public bool manual_animator_full_body_pose_right_arm_muscles_only;
            public bool manual_animator_full_body_pose_left_arm_muscles_only;
            public bool manual_animator_full_body_pose_right_sleeve_chain_muscles_only;
            public float manual_animator_full_body_pose_frame_gate_start;
            public float manual_animator_full_body_pose_frame_gate_end;
            public bool set_human_pose_right_leg_twist_output_enabled;
            public float set_human_pose_right_leg_twist_output_weight =
                DefaultSetHumanPoseRightLegTwistOutputReferenceWeight;
            public float set_human_pose_right_leg_twist_output_max_delta =
                DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta;
            public bool manual_animator_body_rotation_enabled;
            public bool manual_animator_body_rotation_disabled;
            public float manual_animator_body_rotation_weight =
                DefaultManualAnimatorBodyRotationReferenceWeight;
            public bool manual_animator_hand_local_rotation_enabled;
            public bool manual_animator_thumb_local_rotation_enabled;
            public bool manual_animator_hand_palm_frame_enabled;
            public float manual_animator_hand_palm_frame_weight = DefaultManualAnimatorHandPalmFrameWeight;
            public bool retarget_pose_visual_spike_smoothing_override;
            public bool retarget_pose_visual_spike_smoothing_enabled = true;
            public float retarget_pose_visual_spike_current_weight =
                DefaultRetargetPoseVisualSpikeCurrentWeight;
            public float retarget_pose_visual_spike_forearm_stretch_clamp_max_offset =
                DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset;
            public bool retarget_arm_stretch_clamp_enabled;
            public float retarget_arm_stretch_muscle_limit = DefaultRetargetArmStretchMuscleLimit;
            public bool yyb_arm_swing_limit_enabled;
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
            public bool yyb_arm_direction_retarget_enabled;
            public float yyb_arm_direction_upper_arm_weight = DefaultYybArmDirectionUpperArmWeight;
            public float yyb_arm_direction_forearm_weight = DefaultYybArmDirectionForearmWeight;
            public float yyb_arm_direction_upper_arm_max_degrees = DefaultYybArmDirectionUpperArmMaxDegrees;
            public float yyb_arm_direction_forearm_max_degrees = DefaultYybArmDirectionForearmMaxDegrees;
            public float yyb_arm_direction_left_side_weight_scale =
                DefaultYybArmDirectionLeftSideWeightScale;
            public float yyb_arm_direction_right_side_weight_scale =
                DefaultYybArmDirectionRightSideWeightScale;
            public bool yyb_arm_sleeve_anchor_override;
            public bool yyb_arm_sleeve_anchor_enabled = true;
            public float yyb_arm_sleeve_anchor_influence = DefaultYybArmSleeveAnchorInfluence;
            public float yyb_arm_shoulder_cap_anchor_influence = DefaultYybArmShoulderCapAnchorInfluence;
            public float yyb_arm_sleeve_anchor_max_degrees = DefaultYybArmSleeveAnchorMaxDegrees;
            public bool yyb_arm_visual_twist_override;
            public bool yyb_arm_visual_twist_enabled = true;
            public float yyb_arm_visual_upper_arm_influence = DefaultYybArmVisualUpperArmInfluence;
            public float yyb_arm_visual_forearm_influence = DefaultYybArmVisualForearmInfluence;
            public float yyb_arm_visual_upper_arm_max_degrees = DefaultYybArmVisualUpperArmMaxDegrees;
            public float yyb_arm_visual_forearm_max_degrees = DefaultYybArmVisualForearmMaxDegrees;
            public bool manual_animator_lower_body_segment_direction_enabled;
            public bool manual_animator_lower_body_segment_direction_disabled;
            public float manual_animator_lower_body_segment_direction_weight =
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight;
            public float manual_animator_lower_body_segment_direction_max_angle =
                DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            public bool manual_animator_upper_leg_to_lower_leg_segment_direction_disabled;
            public float manual_animator_upper_leg_to_lower_leg_segment_direction_max_angle =
                DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            public bool manual_animator_lower_leg_to_foot_segment_direction_disabled;
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
            public bool manual_animator_foot_to_toes_segment_direction_disabled;
            public float manual_animator_foot_to_toes_segment_direction_max_angle =
                DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            public bool manual_animator_foot_hips_aligned_residual_yaw_enabled;
            public bool manual_animator_foot_hips_aligned_residual_yaw_disabled;
            public float manual_animator_foot_hips_aligned_residual_yaw_weight =
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            public float manual_animator_foot_hips_aligned_residual_yaw_max_angle =
                DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            public bool post_set_human_pose_right_endpoint_position_enabled;
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
            public bool post_set_human_pose_endpoint_position_use_left_side;
            public bool pre_set_human_pose_right_endpoint_position_enabled;
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
            public bool pre_set_human_pose_endpoint_position_use_left_side;
            public bool pre_set_human_pose_endpoint_position_use_ghost_current_basis;
            public bool pre_set_human_pose_endpoint_position_invert_body_position_x;
            public bool pre_set_human_pose_endpoint_position_invert_body_position_z;
            public bool post_set_human_pose_right_foot_evaluator_xz_reference_enabled;
            public float post_set_human_pose_right_foot_evaluator_xz_reference_target_magnitude =
                DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            public bool manual_animator_biped_ik_foot_position_enabled;
            public float manual_animator_biped_ik_foot_position_weight =
                DefaultManualAnimatorBipedIkFootPositionReferenceWeight;
            public float manual_animator_biped_ik_foot_position_max_offset =
                DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset;
            public bool manual_animator_hips_local_position_enabled;
            public float manual_animator_hips_local_position_weight =
                DefaultManualAnimatorHipsLocalPositionReferenceWeight;
            public float manual_animator_hips_local_position_max_offset =
                DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset;
            public bool manual_animator_body_position_xz_enabled;
            public float manual_animator_body_position_xz_weight =
                DefaultManualAnimatorBodyPositionXzReferenceWeight;
            public float manual_animator_body_position_xz_max_offset =
                DefaultManualAnimatorBodyPositionXzReferenceMaxOffset;
            public float manual_animator_body_position_xz_frame_gate_start;
            public float manual_animator_body_position_xz_frame_gate_end;
            public float manual_animator_body_position_xz_frame_gate_blend_frames;
            public float manual_animator_body_position_xz_axis_x_scale = 1f;
            public float manual_animator_body_position_xz_axis_z_scale = 1f;
            public bool yyb_right_sleeve_silhouette_offset_enabled;
            public float yyb_right_sleeve_silhouette_local_offset_x;
            public float yyb_right_sleeve_silhouette_local_offset_frame_gate_start;
            public float yyb_right_sleeve_silhouette_local_offset_frame_gate_end;
            public bool retarget_body_position_xz_root_motion_enabled;
            public bool target_humanoid_bone_position_lock_disabled;
            public bool vmd_playback_probe_enabled;
            public bool vmd_playback_probe_apply_ik_targets;
            public bool reference_mmd_timing_enabled;
            public string segment = "head";
            public int diagnostic_capture_width_override;
            public int diagnostic_capture_height_override;
            public float diagnostic_screenshot_padding_override = float.NaN;
            public float diagnostic_screenshot_vertical_viewport_center_override = float.NaN;
        }

        [Serializable]
        private sealed class StatusEnvelope
        {
            public string request_id;
            public string status;
            public string updated_at;
            public string message;
            public bool passed;
            public string session_id;
            public string summary_json_path;
            public string summary_markdown_path;
            public string latest_summary_json_path;
            public string latest_summary_markdown_path;
            public int total_jobs;
            public int success_jobs;
            public string[] failures;
        }

        private static void Poll()
        {
            bool runnerRunning = YybVisualComparisonBatchRunner.IsRunning;
            bool hasPersistedRunState = YybVisualComparisonBatchRunner.HasPersistedRunState();

            if (_awaitingCompletion &&
                !runnerRunning &&
                hasPersistedRunState &&
                !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                AppendTrace($"requesting runner resume activeRequestId={_activeRequestId}");
                bool resumed = YybVisualComparisonBatchRunner.TryResumePersistedRun();
                runnerRunning = YybVisualComparisonBatchRunner.IsRunning;
                hasPersistedRunState = YybVisualComparisonBatchRunner.HasPersistedRunState();
                AppendTrace(
                    $"runner resume result activeRequestId={_activeRequestId} " +
                    $"resumed={resumed} runnerRunning={runnerRunning} persisted={hasPersistedRunState}");
            }

            if (_awaitingCompletion &&
                !runnerRunning &&
                !hasPersistedRunState)
            {
                AppendTrace($"clearing live stale watcher state activeRequestId={_activeRequestId}");
                ClearAwaitingCompletionState();
            }

            if (DateTime.UtcNow < _nextPollUtc)
            {
                return;
            }

            _nextPollUtc = DateTime.UtcNow.AddSeconds(1);

            if (_awaitingCompletion || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!File.Exists(RequestPath))
            {
                return;
            }

            RequestEnvelope request;
            try
            {
                Debug.Log("[YybVisualComparisonRequestWatcher] request detected");
                request = JsonUtility.FromJson<RequestEnvelope>(File.ReadAllText(RequestPath));
            }
            catch (Exception ex)
            {
                TryDeleteRequestFile();
                WriteStatus(new StatusEnvelope
                {
                    request_id = string.Empty,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    message = $"request read failed: {ex.Message}",
                    passed = false,
                    failures = new[] { ex.Message }
                });
                return;
            }

            if (request == null)
            {
                TryDeleteRequestFile();
                WriteStatus(new StatusEnvelope
                {
                    request_id = string.Empty,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    message = "request payload is null",
                    passed = false,
                    failures = new[] { "request payload is null" }
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(request.request_id))
            {
                request.request_id = Guid.NewGuid().ToString("N");
                PersistRequest(request);
            }

            StatusEnvelope existingStatus = TryReadStatus();
            if (existingStatus != null &&
                string.Equals(existingStatus.request_id, request.request_id, StringComparison.Ordinal))
            {
                if (string.Equals(existingStatus.status, "completed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(existingStatus.status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    AppendTrace($"dropping settled request={request.request_id} state={existingStatus.status}");
                    TryDeleteRequestFile();
                    return;
                }

                if (string.Equals(existingStatus.status, "running", StringComparison.OrdinalIgnoreCase) &&
                    !YybVisualComparisonBatchRunner.IsRunning &&
                    !YybVisualComparisonBatchRunner.HasPersistedRunState())
                {
                    AppendTrace($"restarting orphaned request={request.request_id}");
                }
            }

            if (YybVisualComparisonBatchRunner.IsRunning)
            {
                WriteStatus(new StatusEnvelope
                {
                    request_id = request.request_id,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    message = "YYB visual comparison is already running",
                    passed = false,
                    failures = new[] { "runner already active" }
                });
                return;
            }

            _awaitingCompletion = true;
            _activeRequestId = request.request_id;
            SessionState.SetBool(AwaitingCompletionSessionKey, true);
            SessionState.SetString(ActiveRequestIdSessionKey, _activeRequestId);
            YybVisualComparisonBatchRunner.RunCompleted -= HandleRunCompleted;
            YybVisualComparisonBatchRunner.RunCompleted += HandleRunCompleted;
            AppendTrace($"starting request={_activeRequestId} fbx={request.fbx_file}");
            AppendTrace(
                $"request evaluatorXz={request.post_set_human_pose_right_foot_evaluator_xz_reference_enabled} " +
                $"target={request.post_set_human_pose_right_foot_evaluator_xz_reference_target_magnitude}");

            WriteStatus(new StatusEnvelope
            {
                request_id = request.request_id,
                status = "running",
                updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                message = $"started fbx={request.fbx_file}",
                passed = false,
                failures = Array.Empty<string>()
            });

            try
            {
                YybVisualComparisonBatchRunner.RunWithOptions(
                    request.fbx_file,
                    request.duration_seconds,
                    request.finger_closeups,
                    enableRecorderParentFrameIkOffsetsWhenCenterParented: true,
                    mmdIkDeltaGuardLimitOverrideVmd: request.mmd_ik_delta_guard_limit_vmd,
                    mmdIkDeltaGuardRecoveryTriggerVmd: request.mmd_ik_delta_guard_recovery_trigger_vmd,
                    mmdIkDeltaGuardRecoveryDebtThresholdVmd: request.mmd_ik_delta_guard_recovery_debt_vmd,
                    mmdIkDeltaGuardRecoveryHoldFrames: request.mmd_ik_delta_guard_recovery_hold_frames,
                    enableFinalIkFootGroundingRuntimeOverride: request.final_ik_foot_grounding_enabled,
                    enableManualAnimatorFootLocalRotationRuntimeOverride: request.manual_animator_foot_local_rotation_enabled,
                    disableManualAnimatorFootLocalRotationRuntimeOverride: request.manual_animator_foot_local_rotation_disabled,
                    enableManualAnimatorFullBodyPoseRuntimeOverride: request.manual_animator_full_body_pose_enabled,
                    disableManualAnimatorFullBodyPoseRuntimeOverride: request.manual_animator_full_body_pose_disabled,
                    manualAnimatorFullBodyPoseReferenceWeight: Mathf.Clamp01(NormalizePositiveFloat(
                        request.manual_animator_full_body_pose_weight,
                        DefaultManualAnimatorFullBodyPoseReferenceWeight)),
                    manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride:
                        request.manual_animator_full_body_pose_exclude_lower_body_muscles,
                    manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride:
                        request.manual_animator_full_body_pose_lower_body_muscles_only,
                    manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride:
                        request.manual_animator_full_body_pose_leg_twist_muscles_only,
                    manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride:
                        request.manual_animator_full_body_pose_right_arm_muscles_only,
                    manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride:
                        request.manual_animator_full_body_pose_left_arm_muscles_only,
                    manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride:
                        request.manual_animator_full_body_pose_right_sleeve_chain_muscles_only,
                    manualAnimatorFullBodyPoseReferenceFrameGateStart: Mathf.Max(
                        0f,
                        NormalizeFiniteFloat(
                            request.manual_animator_full_body_pose_frame_gate_start,
                            0f)),
                    manualAnimatorFullBodyPoseReferenceFrameGateEnd: Mathf.Max(
                        0f,
                        NormalizeFiniteFloat(
                            request.manual_animator_full_body_pose_frame_gate_end,
                            0f)),
                    enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride:
                        request.set_human_pose_right_leg_twist_output_enabled,
                    setHumanPoseRightLegTwistOutputReferenceWeight: Mathf.Clamp01(NormalizeFiniteFloat(
                        request.set_human_pose_right_leg_twist_output_weight,
                        DefaultSetHumanPoseRightLegTwistOutputReferenceWeight)),
                    setHumanPoseRightLegTwistOutputReferenceMaxDelta: Mathf.Max(0f, NormalizeFiniteFloat(
                        request.set_human_pose_right_leg_twist_output_max_delta,
                        DefaultSetHumanPoseRightLegTwistOutputReferenceMaxDelta)),
                    enableManualAnimatorBodyRotationRuntimeOverride: request.manual_animator_body_rotation_enabled,
                    disableManualAnimatorBodyRotationRuntimeOverride: request.manual_animator_body_rotation_disabled,
                    manualAnimatorBodyRotationReferenceWeight: NormalizePositiveFloat(
                        request.manual_animator_body_rotation_weight,
                        DefaultManualAnimatorBodyRotationReferenceWeight),
                    enableManualAnimatorHandLocalRotationRuntimeOverride:
                        request.manual_animator_hand_local_rotation_enabled,
                    enableManualAnimatorThumbLocalRotationRuntimeOverride:
                        request.manual_animator_thumb_local_rotation_enabled,
                    enableManualAnimatorHandPalmFrameRuntimeOverride:
                        request.manual_animator_hand_palm_frame_enabled,
                    manualAnimatorHandPalmFrameWeight: Mathf.Clamp01(NormalizePositiveFloat(
                        request.manual_animator_hand_palm_frame_weight,
                        DefaultManualAnimatorHandPalmFrameWeight)),
                    overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings:
                        request.retarget_pose_visual_spike_smoothing_override,
                    enableRetargetPoseVisualSpikeSmoothingRuntimeOverride:
                        request.retarget_pose_visual_spike_smoothing_enabled,
                    retargetPoseVisualSpikeCurrentWeight: Mathf.Clamp(
                        NormalizePositiveFloat(
                            request.retarget_pose_visual_spike_current_weight,
                            DefaultRetargetPoseVisualSpikeCurrentWeight),
                        0.1f,
                        1f),
                    retargetPoseVisualSpikeForearmStretchClampMaxOffset: Mathf.Clamp01(
                        NormalizePositiveFloat(
                            request.retarget_pose_visual_spike_forearm_stretch_clamp_max_offset,
                            DefaultRetargetPoseVisualSpikeForearmStretchClampMaxOffset)),
                    enableRetargetArmStretchClampRuntimeOverride:
                        request.retarget_arm_stretch_clamp_enabled,
                    retargetArmStretchMuscleLimit: Mathf.Clamp(
                        NormalizePositiveFloat(
                            request.retarget_arm_stretch_muscle_limit,
                            DefaultRetargetArmStretchMuscleLimit),
                        0f,
                        DefaultRetargetArmStretchMuscleLimit),
                    enableYybArmSwingLimitRuntimeOverride: request.yyb_arm_swing_limit_enabled,
                    yybArmSwingLimitWeight: Mathf.Clamp01(NormalizePositiveFloat(
                        request.yyb_arm_swing_limit_weight,
                        DefaultYybArmSwingLimitWeight)),
                    yybArmSwingMaxDownDot: Mathf.Clamp01(NormalizePositiveFloat(
                        request.yyb_arm_swing_max_down_dot,
                        DefaultYybArmSwingMaxDownDot)),
                    yybArmSwingMinHandHorizontalRatio: Mathf.Clamp(
                        NormalizePositiveFloat(
                            request.yyb_arm_swing_min_hand_horizontal_ratio,
                            DefaultYybArmSwingMinHandHorizontalRatio),
                        0f,
                        1.5f),
                    yybArmSwingMaxHandBelowShoulderRatio: Mathf.Clamp(
                        NormalizePositiveFloat(
                            request.yyb_arm_swing_max_hand_below_shoulder_ratio,
                            DefaultYybArmSwingMaxHandBelowShoulderRatio),
                        0f,
                        1.5f),
                    yybArmSwingHorizontalReachLimitWeight: Mathf.Clamp01(NormalizePositiveFloat(
                        request.yyb_arm_swing_horizontal_reach_limit_weight,
                        DefaultYybArmSwingHorizontalReachLimitWeight)),
                    yybArmSwingMaxHandHorizontalReachRatio: Mathf.Clamp(
                        NormalizePositiveFloat(
                            request.yyb_arm_swing_max_hand_horizontal_reach_ratio,
                            DefaultYybArmSwingMaxHandHorizontalReachRatio),
                        0f,
                        1.5f),
                    yybArmSwingHorizontalReachMaxHandBelowShoulderRatio: Mathf.Clamp(
                        NormalizePositiveFloat(
                            request.yyb_arm_swing_horizontal_reach_max_hand_below_shoulder_ratio,
                            DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio),
                        0f,
                        1.5f),
                    yybArmSwingHorizontalReachMinElbowAngleAfterApply: Mathf.Clamp(
                        NormalizePositiveFloat(
                            request.yyb_arm_swing_horizontal_reach_min_elbow_angle_after_apply,
                            DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply),
                        0f,
                        180f),
                    yybArmSwingRaisedPoseHorizontalReachLimitWeight: Mathf.Clamp01(NormalizePositiveFloat(
                        request.yyb_arm_swing_raised_pose_horizontal_reach_limit_weight,
                        DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight)),
                    yybArmSwingRaisedPoseMinUpperArmDownDot: Mathf.Clamp01(NormalizePositiveFloat(
                        request.yyb_arm_swing_raised_pose_min_upper_arm_down_dot,
                        DefaultYybArmSwingRaisedPoseMinUpperArmDownDot)),
                    yybArmSwingRaisedPoseMaxHandBelowShoulderRatio: Mathf.Clamp(
                        NormalizePositiveFloat(
                            request.yyb_arm_swing_raised_pose_max_hand_below_shoulder_ratio,
                            DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio),
                        0f,
                        1.5f),
                    yybArmSwingRaisedPoseMaxHandHorizontalReachRatio: Mathf.Clamp(
                        NormalizePositiveFloat(
                            request.yyb_arm_swing_raised_pose_max_hand_horizontal_reach_ratio,
                            DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio),
                        0f,
                        1.5f),
                    enableYybArmDirectionRetargetRuntimeOverride: request.yyb_arm_direction_retarget_enabled,
                    yybArmDirectionUpperArmWeight: Mathf.Clamp01(NormalizePositiveFloat(
                        request.yyb_arm_direction_upper_arm_weight,
                        DefaultYybArmDirectionUpperArmWeight)),
                    yybArmDirectionForearmWeight: Mathf.Clamp01(NormalizePositiveFloat(
                        request.yyb_arm_direction_forearm_weight,
                        DefaultYybArmDirectionForearmWeight)),
                    yybArmDirectionUpperArmMaxDegrees: Mathf.Clamp(
                        NormalizePositiveFloat(
                            request.yyb_arm_direction_upper_arm_max_degrees,
                            DefaultYybArmDirectionUpperArmMaxDegrees),
                        0f,
                        120f),
                    yybArmDirectionForearmMaxDegrees: Mathf.Clamp(
                        NormalizePositiveFloat(
                            request.yyb_arm_direction_forearm_max_degrees,
                            DefaultYybArmDirectionForearmMaxDegrees),
                        0f,
                        120f),
                    yybArmDirectionLeftSideWeightScale: Mathf.Clamp01(NormalizeFiniteFloat(
                        request.yyb_arm_direction_left_side_weight_scale,
                        DefaultYybArmDirectionLeftSideWeightScale)),
                    yybArmDirectionRightSideWeightScale: Mathf.Clamp01(NormalizeFiniteFloat(
                        request.yyb_arm_direction_right_side_weight_scale,
                        DefaultYybArmDirectionRightSideWeightScale)),
                    overrideYybArmSleeveAnchorRuntimeSettings: request.yyb_arm_sleeve_anchor_override,
                    enableYybArmSleeveAnchorRuntimeOverride: request.yyb_arm_sleeve_anchor_enabled,
                    yybArmSleeveAnchorInfluence: Mathf.Clamp01(NormalizeFiniteFloat(
                        request.yyb_arm_sleeve_anchor_influence,
                        DefaultYybArmSleeveAnchorInfluence)),
                    yybArmShoulderCapAnchorInfluence: Mathf.Clamp01(NormalizeFiniteFloat(
                        request.yyb_arm_shoulder_cap_anchor_influence,
                        DefaultYybArmShoulderCapAnchorInfluence)),
                    yybArmSleeveAnchorMaxDegrees: Mathf.Clamp(
                        NormalizeFiniteFloat(
                            request.yyb_arm_sleeve_anchor_max_degrees,
                            DefaultYybArmSleeveAnchorMaxDegrees),
                        0f,
                        120f),
                    overrideYybArmVisualTwistRuntimeSettings: request.yyb_arm_visual_twist_override,
                    enableYybArmVisualTwistRuntimeOverride: request.yyb_arm_visual_twist_enabled,
                    yybArmVisualUpperArmInfluence: Mathf.Clamp01(NormalizeFiniteFloat(
                        request.yyb_arm_visual_upper_arm_influence,
                        DefaultYybArmVisualUpperArmInfluence)),
                    yybArmVisualForearmInfluence: Mathf.Clamp01(NormalizeFiniteFloat(
                        request.yyb_arm_visual_forearm_influence,
                        DefaultYybArmVisualForearmInfluence)),
                    yybArmVisualUpperArmMaxDegrees: Mathf.Clamp(
                        NormalizeFiniteFloat(
                            request.yyb_arm_visual_upper_arm_max_degrees,
                            DefaultYybArmVisualUpperArmMaxDegrees),
                        0f,
                        120f),
                    yybArmVisualForearmMaxDegrees: Mathf.Clamp(
                        NormalizeFiniteFloat(
                            request.yyb_arm_visual_forearm_max_degrees,
                            DefaultYybArmVisualForearmMaxDegrees),
                        0f,
                        120f),
                    enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride: request.manual_animator_lower_body_segment_direction_enabled,
                    disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride:
                        request.manual_animator_lower_body_segment_direction_disabled,
                    manualAnimatorLowerBodySegmentDirectionReferenceWeight: NormalizePositiveFloat(
                        request.manual_animator_lower_body_segment_direction_weight,
                        DefaultManualAnimatorLowerBodySegmentDirectionReferenceWeight),
                    manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle: NormalizePositiveFloat(
                        request.manual_animator_lower_body_segment_direction_max_angle,
                        DefaultManualAnimatorLowerBodySegmentDirectionReferenceMaxAngle),
                    disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride:
                        request.manual_animator_upper_leg_to_lower_leg_segment_direction_disabled,
                    manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle: NormalizePositiveFloat(
                        request.manual_animator_upper_leg_to_lower_leg_segment_direction_max_angle,
                        DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle),
                    disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride:
                        request.manual_animator_lower_leg_to_foot_segment_direction_disabled,
                    manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle: NormalizePositiveFloat(
                        request.manual_animator_lower_leg_to_foot_segment_direction_max_angle,
                        DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle),
                    manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle: NormalizePositiveFloat(
                        request.manual_animator_left_lower_leg_to_foot_segment_direction_max_angle,
                        DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle),
                    manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle: NormalizePositiveFloat(
                        request.manual_animator_right_lower_leg_to_foot_segment_direction_max_angle,
                        DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle),
                    manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.manual_animator_right_lower_leg_to_foot_segment_direction_axis_xz_scale,
                            DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale)),
                    manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.manual_animator_right_lower_leg_to_foot_segment_direction_blend_weight,
                            DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight)),
                    manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart: NormalizePositiveFloat(
                        request.manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_start,
                        DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart),
                    manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd: NormalizePositiveFloat(
                        request.manual_animator_right_lower_leg_to_foot_segment_direction_frame_gate_end,
                        DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd),
                    manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.manual_animator_right_lower_leg_to_foot_segment_direction_endpoint_blend_weight,
                            DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight)),
                    disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride:
                        request.manual_animator_foot_to_toes_segment_direction_disabled,
                    manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle: NormalizePositiveFloat(
                        request.manual_animator_foot_to_toes_segment_direction_max_angle,
                        DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle),
                    enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride:
                        request.manual_animator_foot_hips_aligned_residual_yaw_enabled,
                    disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride:
                        request.manual_animator_foot_hips_aligned_residual_yaw_disabled,
                    manualAnimatorFootHipsAlignedResidualYawReferenceWeight: NormalizePositiveFloat(
                        request.manual_animator_foot_hips_aligned_residual_yaw_weight,
                        DefaultManualAnimatorFootHipsAlignedResidualYawReferenceWeight),
                    manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle: NormalizePositiveFloat(
                        request.manual_animator_foot_hips_aligned_residual_yaw_max_angle,
                        DefaultManualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle),
                    enablePostSetHumanPoseRightEndpointPositionRuntimeOverride:
                        request.post_set_human_pose_right_endpoint_position_enabled,
                    postSetHumanPoseRightEndpointPositionReferenceWeight: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.post_set_human_pose_right_endpoint_position_weight,
                            DefaultPostSetHumanPoseRightEndpointPositionReferenceWeight)),
                    postSetHumanPoseRightEndpointPositionReferenceMaxOffset: NormalizePositiveFloat(
                        request.post_set_human_pose_right_endpoint_position_max_offset,
                        DefaultPostSetHumanPoseRightEndpointPositionReferenceMaxOffset),
                    postSetHumanPoseRightEndpointPositionReferencePositiveZScale: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.post_set_human_pose_right_endpoint_position_positive_z_scale,
                            DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale)),
                    postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.post_set_human_pose_right_endpoint_position_toes_blend_weight,
                            DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight)),
                    postSetHumanPoseRightEndpointPositionReferenceFrameGateStart: NormalizePositiveFloat(
                        request.post_set_human_pose_right_endpoint_position_frame_gate_start,
                        DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart),
                    postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd: NormalizePositiveFloat(
                        request.post_set_human_pose_right_endpoint_position_frame_gate_end,
                        DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd),
                    postSetHumanPoseEndpointPositionUseLeftSide:
                        request.post_set_human_pose_endpoint_position_use_left_side,
                    enablePreSetHumanPoseRightEndpointPositionRuntimeOverride:
                        request.pre_set_human_pose_right_endpoint_position_enabled,
                    preSetHumanPoseRightEndpointPositionReferenceWeight: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.pre_set_human_pose_right_endpoint_position_weight,
                            DefaultPreSetHumanPoseRightEndpointPositionReferenceWeight)),
                    preSetHumanPoseRightEndpointPositionReferenceMaxOffset: NormalizePositiveFloat(
                        request.pre_set_human_pose_right_endpoint_position_max_offset,
                        DefaultPreSetHumanPoseRightEndpointPositionReferenceMaxOffset),
                    preSetHumanPoseRightEndpointPositionReferencePositiveZScale: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.pre_set_human_pose_right_endpoint_position_positive_z_scale,
                            DefaultPreSetHumanPoseRightEndpointPositionReferencePositiveZScale)),
                    preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.pre_set_human_pose_right_endpoint_position_toes_blend_weight,
                            DefaultPreSetHumanPoseRightEndpointPositionReferenceToesBlendWeight)),
                    preSetHumanPoseRightEndpointPositionReferenceFrameGateStart: NormalizePositiveFloat(
                        request.pre_set_human_pose_right_endpoint_position_frame_gate_start,
                        DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateStart),
                    preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd: NormalizePositiveFloat(
                        request.pre_set_human_pose_right_endpoint_position_frame_gate_end,
                        DefaultPreSetHumanPoseRightEndpointPositionReferenceFrameGateEnd),
                    preSetHumanPoseEndpointPositionUseLeftSide:
                        request.pre_set_human_pose_endpoint_position_use_left_side,
                    preSetHumanPoseEndpointPositionUseGhostCurrentBasis:
                        request.pre_set_human_pose_endpoint_position_use_ghost_current_basis,
                    preSetHumanPoseEndpointPositionInvertBodyPositionX:
                        request.pre_set_human_pose_endpoint_position_invert_body_position_x,
                    preSetHumanPoseEndpointPositionInvertBodyPositionZ:
                        request.pre_set_human_pose_endpoint_position_invert_body_position_z,
                    usePostSetHumanPoseRightFootEvaluatorXzReference:
                        request.post_set_human_pose_right_foot_evaluator_xz_reference_enabled,
                    postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude: NormalizePositiveFloat(
                        request.post_set_human_pose_right_foot_evaluator_xz_reference_target_magnitude,
                        DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude),
                    enableManualAnimatorBipedIkFootPositionRuntimeOverride: request.manual_animator_biped_ik_foot_position_enabled,
                    manualAnimatorBipedIkFootPositionReferenceWeight: NormalizePositiveFloat(
                        request.manual_animator_biped_ik_foot_position_weight,
                        DefaultManualAnimatorBipedIkFootPositionReferenceWeight),
                    manualAnimatorBipedIkFootPositionReferenceMaxOffset: NormalizePositiveFloat(
                        request.manual_animator_biped_ik_foot_position_max_offset,
                        DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset),
                    enableManualAnimatorHipsLocalPositionRuntimeOverride:
                        request.manual_animator_hips_local_position_enabled,
                    manualAnimatorHipsLocalPositionReferenceWeight: NormalizePositiveFloat(
                        request.manual_animator_hips_local_position_weight,
                        DefaultManualAnimatorHipsLocalPositionReferenceWeight),
                    manualAnimatorHipsLocalPositionReferenceMaxOffset: NormalizePositiveFloat(
                        request.manual_animator_hips_local_position_max_offset,
                        DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset),
                    enableManualAnimatorBodyPositionXzRuntimeOverride:
                        request.manual_animator_body_position_xz_enabled,
                    manualAnimatorBodyPositionXzReferenceWeight: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.manual_animator_body_position_xz_weight,
                            DefaultManualAnimatorBodyPositionXzReferenceWeight)),
                    manualAnimatorBodyPositionXzReferenceMaxOffset: NormalizePositiveFloat(
                        request.manual_animator_body_position_xz_max_offset,
                        DefaultManualAnimatorBodyPositionXzReferenceMaxOffset),
                    manualAnimatorBodyPositionXzReferenceFrameGateStart: NormalizePositiveFloat(
                        request.manual_animator_body_position_xz_frame_gate_start,
                        0f),
                    manualAnimatorBodyPositionXzReferenceFrameGateEnd: NormalizePositiveFloat(
                        request.manual_animator_body_position_xz_frame_gate_end,
                        0f),
                    manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames: NormalizePositiveFloat(
                        request.manual_animator_body_position_xz_frame_gate_blend_frames,
                        DefaultManualAnimatorBodyPositionXzReferenceFrameGateBlendFrames),
                    manualAnimatorBodyPositionXzReferenceAxisXScale: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.manual_animator_body_position_xz_axis_x_scale,
                            1f)),
                    manualAnimatorBodyPositionXzReferenceAxisZScale: Mathf.Clamp01(
                        NormalizeFiniteFloat(
                            request.manual_animator_body_position_xz_axis_z_scale,
                            1f)),
                    enableYybRightSleeveSilhouetteOffsetRuntimeOverride:
                        request.yyb_right_sleeve_silhouette_offset_enabled,
                    yybRightSleeveSilhouetteLocalOffsetX: Mathf.Clamp(
                        NormalizeFiniteFloat(
                            request.yyb_right_sleeve_silhouette_local_offset_x,
                            0f),
                        -0.2f,
                        0.2f),
                    yybRightSleeveSilhouetteLocalOffsetFrameGateStart: Mathf.Clamp(
                        NormalizeFiniteFloat(
                            request.yyb_right_sleeve_silhouette_local_offset_frame_gate_start,
                            0f),
                        0f,
                        6000f),
                    yybRightSleeveSilhouetteLocalOffsetFrameGateEnd: Mathf.Clamp(
                        NormalizeFiniteFloat(
                            request.yyb_right_sleeve_silhouette_local_offset_frame_gate_end,
                            0f),
                        0f,
                        6000f),
                    enableRetargetBodyPositionXzRootMotionRuntimeOverride:
                        request.retarget_body_position_xz_root_motion_enabled,
                    disableTargetHumanoidBonePositionLockRuntimeOverride:
                        request.target_humanoid_bone_position_lock_disabled,
                    enableVmdPlaybackProbeRuntimeOverride: request.vmd_playback_probe_enabled,
                    applyVmdPlaybackProbeIkTargetsRuntimeOverride: request.vmd_playback_probe_apply_ik_targets,
                    editorDiagnosticSmokeSegmentName: request.segment,
                    enableReferenceMmdTimingRuntimeOverride: request.reference_mmd_timing_enabled,
                    diagnosticCaptureWidthOverride: Mathf.Max(0, request.diagnostic_capture_width_override),
                    diagnosticCaptureHeightOverride: Mathf.Max(0, request.diagnostic_capture_height_override),
                    diagnosticScreenshotPaddingOverride: NormalizePositiveFloat(
                        request.diagnostic_screenshot_padding_override,
                        float.NaN),
                    diagnosticScreenshotVerticalViewportCenterOverride: NormalizeFiniteFloat(
                        request.diagnostic_screenshot_vertical_viewport_center_override,
                        float.NaN));
            }
            catch (Exception ex)
            {
                YybVisualComparisonBatchRunner.RunCompleted -= HandleRunCompleted;
                ClearAwaitingCompletionState();
                TryDeleteRequestFile();
                WriteStatus(new StatusEnvelope
                {
                    request_id = request.request_id,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    message = ex.Message,
                    passed = false,
                    failures = new[] { ex.Message }
                });
            }
        }

        private static void HandleRunCompleted(YybVisualComparisonBatchRunner.RunCompletionInfo info)
        {
            string completedRequestId = _activeRequestId;
            YybVisualComparisonBatchRunner.RunCompleted -= HandleRunCompleted;
            ClearAwaitingCompletionState();
            AppendTrace($"completed request={completedRequestId} passed={info.passed} session={info.sessionId}");
            TryDeleteRequestFile();

            WriteStatus(new StatusEnvelope
            {
                request_id = completedRequestId,
                status = info.passed ? "completed" : "failed",
                updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                message = info.passed ? "comparison finished" : "comparison finished with failures",
                passed = info.passed,
                session_id = info.sessionId,
                summary_json_path = info.summaryJsonPath,
                summary_markdown_path = info.summaryMarkdownPath,
                latest_summary_json_path = info.latestSummaryJsonPath,
                latest_summary_markdown_path = info.latestSummaryMarkdownPath,
                total_jobs = info.totalJobs,
                success_jobs = info.successJobs,
                failures = info.failures ?? Array.Empty<string>()
            });
        }

        private static void ClearAwaitingCompletionState()
        {
            _awaitingCompletion = false;
            _activeRequestId = string.Empty;
            SessionState.SetBool(AwaitingCompletionSessionKey, false);
            SessionState.EraseString(ActiveRequestIdSessionKey);
        }

        private static void WriteStatus(StatusEnvelope status)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatusPath) ?? ProjectRoot);
            string json = JsonUtility.ToJson(status, true);
            File.WriteAllText(StatusPath, json);
            AppendTrace($"status request={status.request_id} state={status.status} path={StatusPath}");
        }

        private static void PersistRequest(RequestEnvelope request)
        {
            if (request == null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(RequestPath) ?? ProjectRoot);
            string json = JsonUtility.ToJson(request, true);
            File.WriteAllText(RequestPath, json);
        }

        private static StatusEnvelope TryReadStatus()
        {
            if (!File.Exists(StatusPath))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<StatusEnvelope>(File.ReadAllText(StatusPath));
            }
            catch
            {
                return null;
            }
        }

        private static void TryDeleteRequestFile()
        {
            try
            {
                if (File.Exists(RequestPath))
                {
                    File.Delete(RequestPath);
                }
            }
            catch (Exception ex)
            {
                AppendTrace($"request delete skipped path={RequestPath} reason={ex.Message}");
            }
        }

        private static float NormalizePositiveFloat(float value, float fallbackValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return fallbackValue;
            }

            return value;
        }

        private static float NormalizeFiniteFloat(float value, float fallbackValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallbackValue;
            }

            return value;
        }

        private static void AppendTrace(string message)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TracePath) ?? ProjectRoot);
            File.AppendAllText(
                TracePath,
                $"{DateTime.Now.ToString("o", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}");
        }
    }
}
#endif

