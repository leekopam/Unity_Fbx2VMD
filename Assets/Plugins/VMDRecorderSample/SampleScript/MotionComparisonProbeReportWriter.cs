using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public readonly struct MotionComparisonProbeScreenshotIndexRow
{
    public readonly string ComparisonLabel;
    public readonly string SceneName;
    public readonly string Reason;
    public readonly int RecorderFrame;
    public readonly string ViewName;
    public readonly string RelativePath;

    public MotionComparisonProbeScreenshotIndexRow(
        string comparisonLabel,
        string sceneName,
        string reason,
        int recorderFrame,
        string viewName,
        string relativePath)
    {
        ComparisonLabel = comparisonLabel ?? "";
        SceneName = sceneName ?? "";
        Reason = reason ?? "";
        RecorderFrame = recorderFrame;
        ViewName = viewName ?? "";
        RelativePath = relativePath ?? "";
    }
}

public readonly struct MotionComparisonProbeFrameSessionIndexData
{
    public readonly string SessionId;
    public readonly string SessionManifestRelativePath;
    public readonly string MetricsCsvRelativePath;
    public readonly string FrameIndexCsvRelativePath;

    public MotionComparisonProbeFrameSessionIndexData(
        string sessionId,
        string sessionManifestRelativePath,
        string metricsCsvRelativePath,
        string frameIndexCsvRelativePath)
    {
        SessionId = sessionId ?? "";
        SessionManifestRelativePath = sessionManifestRelativePath ?? "";
        MetricsCsvRelativePath = metricsCsvRelativePath ?? "";
        FrameIndexCsvRelativePath = frameIndexCsvRelativePath ?? "";
    }
}

public readonly struct MotionComparisonProbeSessionManifestData
{
    public readonly string SessionId;
    public readonly string ComparisonLabel;
    public readonly string SceneName;
    public readonly string StateReason;
    public readonly string CreatedAt;
    public readonly string UpdatedAt;
    public readonly bool ScreenshotsEnabled;
    public readonly string SampleClock;
    public readonly string SampleTimes;
    public readonly bool YybDiagnosticOnlyMetrics;

    public readonly int RiskEvaluationFrameCount;
    public readonly int LeftThumbCoreCoverageFrameCount;
    public readonly int RightThumbCoreCoverageFrameCount;
    public readonly bool LeftThumbHelperCoverageRequired;
    public readonly bool RightThumbHelperCoverageRequired;
    public readonly int LeftThumbHelperCoverageFrameCount;
    public readonly int RightThumbHelperCoverageFrameCount;

    public readonly float MaxGenericThumbAnatomyRisk;
    public readonly string MaxGenericThumbAnatomyRiskReason;
    public readonly float MaxGenericThumbAnatomyRiskClipTime;
    public readonly int MaxGenericThumbAnatomyRiskRecorderFrame;
    public readonly float MaxThumbSpreadRisk;
    public readonly float MaxThumbProjectionRisk;
    public readonly float MaxThumbHelperSeparationRisk;
    public readonly float MaxThumbWebbingRisk;

    public readonly float MaxYybDeformationRisk;
    public readonly string MaxYybDeformationRiskReason;
    public readonly float MaxYybDeformationRiskClipTime;
    public readonly int MaxYybDeformationRiskRecorderFrame;

    public readonly float LeftThumbProjectionGuardWeight;
    public readonly float RightThumbProjectionGuardWeight;
    public readonly float LeftThumbIndexSpreadGuardWeight;
    public readonly float RightThumbIndexSpreadGuardWeight;
    public readonly float LeftThumbSegmentStraightenGuardWeight;
    public readonly float RightThumbSegmentStraightenGuardWeight;

    public readonly string MetricsCsvRelativePath;
    public readonly string FrameFolderRelativePath;
    public readonly string FrameIndexCsvRelativePath;
    public readonly string FrameSessionIndexRelativePath;

    public MotionComparisonProbeSessionManifestData(
        string sessionId,
        string comparisonLabel,
        string sceneName,
        string stateReason,
        string createdAt,
        string updatedAt,
        bool screenshotsEnabled,
        string sampleClock,
        string sampleTimes,
        bool yybDiagnosticOnlyMetrics,
        int riskEvaluationFrameCount,
        int leftThumbCoreCoverageFrameCount,
        int rightThumbCoreCoverageFrameCount,
        bool leftThumbHelperCoverageRequired,
        bool rightThumbHelperCoverageRequired,
        int leftThumbHelperCoverageFrameCount,
        int rightThumbHelperCoverageFrameCount,
        float maxGenericThumbAnatomyRisk,
        string maxGenericThumbAnatomyRiskReason,
        float maxGenericThumbAnatomyRiskClipTime,
        int maxGenericThumbAnatomyRiskRecorderFrame,
        float maxThumbSpreadRisk,
        float maxThumbProjectionRisk,
        float maxThumbHelperSeparationRisk,
        float maxThumbWebbingRisk,
        float maxYybDeformationRisk,
        string maxYybDeformationRiskReason,
        float maxYybDeformationRiskClipTime,
        int maxYybDeformationRiskRecorderFrame,
        float leftThumbProjectionGuardWeight,
        float rightThumbProjectionGuardWeight,
        float leftThumbIndexSpreadGuardWeight,
        float rightThumbIndexSpreadGuardWeight,
        float leftThumbSegmentStraightenGuardWeight,
        float rightThumbSegmentStraightenGuardWeight,
        string metricsCsvRelativePath,
        string frameFolderRelativePath,
        string frameIndexCsvRelativePath,
        string frameSessionIndexRelativePath)
    {
        SessionId = sessionId ?? "";
        ComparisonLabel = comparisonLabel ?? "";
        SceneName = sceneName ?? "";
        StateReason = stateReason ?? "";
        CreatedAt = createdAt ?? "";
        UpdatedAt = updatedAt ?? "";
        ScreenshotsEnabled = screenshotsEnabled;
        SampleClock = sampleClock ?? "";
        SampleTimes = sampleTimes ?? "";
        YybDiagnosticOnlyMetrics = yybDiagnosticOnlyMetrics;
        RiskEvaluationFrameCount = riskEvaluationFrameCount;
        LeftThumbCoreCoverageFrameCount = leftThumbCoreCoverageFrameCount;
        RightThumbCoreCoverageFrameCount = rightThumbCoreCoverageFrameCount;
        LeftThumbHelperCoverageRequired = leftThumbHelperCoverageRequired;
        RightThumbHelperCoverageRequired = rightThumbHelperCoverageRequired;
        LeftThumbHelperCoverageFrameCount = leftThumbHelperCoverageFrameCount;
        RightThumbHelperCoverageFrameCount = rightThumbHelperCoverageFrameCount;
        MaxGenericThumbAnatomyRisk = maxGenericThumbAnatomyRisk;
        MaxGenericThumbAnatomyRiskReason = maxGenericThumbAnatomyRiskReason ?? "";
        MaxGenericThumbAnatomyRiskClipTime = maxGenericThumbAnatomyRiskClipTime;
        MaxGenericThumbAnatomyRiskRecorderFrame = maxGenericThumbAnatomyRiskRecorderFrame;
        MaxThumbSpreadRisk = maxThumbSpreadRisk;
        MaxThumbProjectionRisk = maxThumbProjectionRisk;
        MaxThumbHelperSeparationRisk = maxThumbHelperSeparationRisk;
        MaxThumbWebbingRisk = maxThumbWebbingRisk;
        MaxYybDeformationRisk = maxYybDeformationRisk;
        MaxYybDeformationRiskReason = maxYybDeformationRiskReason ?? "";
        MaxYybDeformationRiskClipTime = maxYybDeformationRiskClipTime;
        MaxYybDeformationRiskRecorderFrame = maxYybDeformationRiskRecorderFrame;
        LeftThumbProjectionGuardWeight = leftThumbProjectionGuardWeight;
        RightThumbProjectionGuardWeight = rightThumbProjectionGuardWeight;
        LeftThumbIndexSpreadGuardWeight = leftThumbIndexSpreadGuardWeight;
        RightThumbIndexSpreadGuardWeight = rightThumbIndexSpreadGuardWeight;
        LeftThumbSegmentStraightenGuardWeight = leftThumbSegmentStraightenGuardWeight;
        RightThumbSegmentStraightenGuardWeight = rightThumbSegmentStraightenGuardWeight;
        MetricsCsvRelativePath = metricsCsvRelativePath ?? "";
        FrameFolderRelativePath = frameFolderRelativePath ?? "";
        FrameIndexCsvRelativePath = frameIndexCsvRelativePath ?? "";
        FrameSessionIndexRelativePath = frameSessionIndexRelativePath ?? "";
    }

    public MotionComparisonProbeSessionManifestData(
        string sessionId,
        string comparisonLabel,
        string sceneName,
        string stateReason,
        string createdAt,
        string updatedAt,
        bool screenshotsEnabled,
        string sampleClock,
        string sampleTimes,
        bool yybDiagnosticOnlyMetrics,
        int riskEvaluationFrameCount,
        int leftThumbCoreCoverageFrameCount,
        int rightThumbCoreCoverageFrameCount,
        bool leftThumbHelperCoverageRequired,
        bool rightThumbHelperCoverageRequired,
        int leftThumbHelperCoverageFrameCount,
        int rightThumbHelperCoverageFrameCount,
        float maxGenericThumbAnatomyRisk,
        string maxGenericThumbAnatomyRiskReason,
        float maxGenericThumbAnatomyRiskClipTime,
        int maxGenericThumbAnatomyRiskRecorderFrame,
        float maxThumbSpreadRisk,
        float maxThumbProjectionRisk,
        float maxThumbHelperSeparationRisk,
        float maxThumbWebbingRisk,
        float maxYybDeformationRisk,
        string maxYybDeformationRiskReason,
        float maxYybDeformationRiskClipTime,
        int maxYybDeformationRiskRecorderFrame,
        float leftThumbProjectionGuardWeight,
        float rightThumbProjectionGuardWeight,
        float leftThumbIndexSpreadGuardWeight,
        float rightThumbIndexSpreadGuardWeight,
        float leftThumbSegmentStraightenGuardWeight,
        float rightThumbSegmentStraightenGuardWeight,
        MotionComparisonProbeSessionManifestOutputPaths artifactPaths)
        : this(
            sessionId,
            comparisonLabel,
            sceneName,
            stateReason,
            createdAt,
            updatedAt,
            screenshotsEnabled,
            sampleClock,
            sampleTimes,
            yybDiagnosticOnlyMetrics,
            riskEvaluationFrameCount,
            leftThumbCoreCoverageFrameCount,
            rightThumbCoreCoverageFrameCount,
            leftThumbHelperCoverageRequired,
            rightThumbHelperCoverageRequired,
            leftThumbHelperCoverageFrameCount,
            rightThumbHelperCoverageFrameCount,
            maxGenericThumbAnatomyRisk,
            maxGenericThumbAnatomyRiskReason,
            maxGenericThumbAnatomyRiskClipTime,
            maxGenericThumbAnatomyRiskRecorderFrame,
            maxThumbSpreadRisk,
            maxThumbProjectionRisk,
            maxThumbHelperSeparationRisk,
            maxThumbWebbingRisk,
            maxYybDeformationRisk,
            maxYybDeformationRiskReason,
            maxYybDeformationRiskClipTime,
            maxYybDeformationRiskRecorderFrame,
            leftThumbProjectionGuardWeight,
            rightThumbProjectionGuardWeight,
            leftThumbIndexSpreadGuardWeight,
            rightThumbIndexSpreadGuardWeight,
            leftThumbSegmentStraightenGuardWeight,
            rightThumbSegmentStraightenGuardWeight,
            artifactPaths.MetricsCsvRelativePath,
            artifactPaths.FrameFolderRelativePath,
            artifactPaths.FrameIndexCsvRelativePath,
            artifactPaths.FrameSessionIndexRelativePath)
    {
    }
}

[Serializable]
public sealed class MotionComparisonFrameQualitySummary
{
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

[Serializable]
public sealed class VerticalSolvePrimaryExportPromotion
{
    public string raw_metrics_csv;
    public string raw_vmd_path;
    public string raw_diagnostic_metrics_csv;
    public string raw_diagnostic_vmd_path;
    public string corrected_metrics_csv;
    public string corrected_vmd_path;
    public string integrated_manifest_path;
    public long promoted_vmd_bytes;
}

[Serializable]
internal sealed class MmdAutomationReportForSummary
{
    public string status;
    public string finished_at;
    public MmdAutomationConfigForSummary config;
    public MmdAutomationArtifactsForSummary artifacts;
    public MmdAutomationStepForSummary[] steps;
}

[Serializable]
internal sealed class MmdAutomationConfigForSummary
{
    public string motion_vmd;
}

[Serializable]
internal sealed class MmdAutomationArtifactsForSummary
{
    public string run_dir;
    public string report_path;
    public string screenshots_dir;
}

[Serializable]
internal sealed class MmdAutomationStepForSummary
{
    public string name;
    public string status;
    public string play_state_screenshot;
}

public static class MotionComparisonProbeReportWriter
{
    private const float QualityFloorTolerance = -0.001f;
    private const float QualityTeleportStepThreshold = 0.12f;
    private const float QualitySameFrameHipsYWarnThreshold = 0.04f;
    private const float QualitySameFrameHipsYFailThreshold = QualityTeleportStepThreshold;
    private const float QualitySameFrameFootBottomYWarnThreshold = 0.035f;
    private const float QualitySameFrameFootBottomYFailThreshold = 0.05f;
    private const float QualitySameFrameFootXzWarnThreshold = 0.05f;
    private const float QualitySameFrameFootXzFailThreshold = QualityTeleportStepThreshold;
    private const float QualityYybDeformationRiskFailThreshold = 0.35f;
    private const float QualityYybSleeveThicknessRiskFailThreshold = 0.35f;
    private const float QualityLimbMotionSignalThreshold = 0.005f;
    private const float QualityStationaryLimbRootTravelFailThreshold = 0.01f;
    private const float QualitySameFrameLimbPoseDeltaFailThreshold = 1.0f;
    private const float QualityGuardNormalizedHumanMuscleLimit = 1.0f;
    private const float QualityGuardNormalizedUpperArmTwistMuscleLimit = 0.75f;
    private const float QualityGuardNormalizedForearmTwistMuscleLimit = 0.5f;
    private const int QualityMetricFrameMatchTolerance = 1;
    private const float VerticalSolvePrototypeMaxCorrectionY = 0.08f;
    private const float VerticalSolveArtifactMaxCorrectionY = 0.52f;
    private const float VerticalSolvePostprocessSafetyMarginY = 0.0005f;
    private const float HorizontalFootSolvePostprocessSafetyMarginXZ = 0.001f;
    private const uint VerticalSolveVisibleIkCarrierSearchFrameWindow = 5u;
    private static readonly string[] VerticalSolveCorrectionDiagnosticColumns =
    {
        "verticalSolveCorrectionHipsY",
        "verticalSolveCorrectionFootBottomY",
        "verticalSolveCorrectionLeftFootX",
        "verticalSolveCorrectionLeftFootZ",
        "verticalSolveCorrectionRightFootX",
        "verticalSolveCorrectionRightFootZ",
        "verticalSolveHorizontalFootTargetMagnitude",
        "verticalSolveLeftFootNormalizedDeltaX",
        "verticalSolveLeftFootNormalizedDeltaZ",
        "verticalSolveLeftFootNormalizedMagnitude",
        "verticalSolveRightFootNormalizedDeltaX",
        "verticalSolveRightFootNormalizedDeltaZ",
        "verticalSolveRightFootNormalizedMagnitude",
        "verticalSolveCorrectionSource"
    };

    private const string MetricsCsvHeader = "label,scene,reason,elapsed,timeSinceLevelLoad,frameCount,recorderFrame,animationTimeSource,animationClipName,animationClipTime,animationClipLength,animationNormalizedTime,rootX,rootY,rootZ,rootYaw,retargetRootDeltaLast,retargetRootDeltaMax,retargetRootDeltaSkippedCount,retargetPoseRootDeltaLast,retargetPoseRootDeltaMax,retargetPoseRootClampCount,retargetGroundingAdjustmentLast,retargetGroundingAdjustmentMax,retargetGroundingStepClampCount,retargetGroundingSmoothedCount,retargetGroundingVerticalStepLast,retargetGroundingVerticalStepMax,retargetGroundingInitialVerticalStep,retargetGroundingVerticalStepAfterInitialMax,retargetGroundingTargetY,retargetGroundingLowestFootBottomY,retargetGroundingMaxStepPerFrame,retargetGroundingLastStepToMaxStepRatio,retargetGroundingLastStepAtMaxStep,retargetRecordingStartRootY,retargetRecordingStartBodyPositionY,retargetRecordingStartHipsLocalY,retargetRecordingStartHipsY,retargetRecordingStartHipsReferenceBeforeLocalY,retargetRecordingStartHipsReferenceAfterLocalY,retargetRecordingStartHipsReferenceDeltaY,retargetRecordingStartHipsReferenceFlipDetected,retargetRecordingStartHipsReferenceStage,retargetPoseInputLeftShoulderFrontBackMuscle,retargetAfterEditorMuscleReferenceLeftShoulderFrontBackMuscle,retargetAfterClampPoseMusclesLeftShoulderFrontBackMuscle,retargetAfterAnatomicalArmGuardLeftShoulderFrontBackMuscle,retargetAfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle,retargetSetHumanPoseInputLeftShoulderFrontBackMuscle,retargetSetHumanPoseOutputLeftShoulderFrontBackMuscle,retargetSetHumanPoseLeftShoulderFrontBackDelta,retargetPoseInputLeftArmTwistMuscle,retargetAfterEditorMuscleReferenceLeftArmTwistMuscle,retargetAfterClampPoseMusclesLeftArmTwistMuscle,retargetAfterAnatomicalArmGuardLeftArmTwistMuscle,retargetAfterVisualSpikeSmoothingLeftArmTwistMuscle,retargetSetHumanPoseInputLeftArmTwistMuscle,retargetSetHumanPoseOutputLeftArmTwistMuscle,retargetSetHumanPoseLeftArmTwistDelta,retargetPoseInputLeftForearmStretchMuscle,retargetAfterEditorMuscleReferenceLeftForearmStretchMuscle,retargetAfterClampPoseMusclesLeftForearmStretchMuscle,retargetAfterAnatomicalArmGuardLeftForearmStretchMuscle,retargetAfterVisualSpikeSmoothingLeftForearmStretchMuscle,retargetSetHumanPoseInputLeftForearmStretchMuscle,retargetSetHumanPoseOutputLeftForearmStretchMuscle,retargetSetHumanPoseLeftForearmStretchDelta,retargetPoseInputRightForearmStretchMuscle,retargetAfterEditorMuscleReferenceRightForearmStretchMuscle,retargetAfterClampPoseMusclesRightForearmStretchMuscle,retargetAfterAnatomicalArmGuardRightForearmStretchMuscle,retargetAfterVisualSpikeSmoothingRightForearmStretchMuscle,retargetSetHumanPoseInputRightForearmStretchMuscle,retargetSetHumanPoseOutputRightForearmStretchMuscle,retargetSetHumanPoseRightForearmStretchDelta,retargetPoseInputRightArmTwistMuscle,retargetAfterEditorMuscleReferenceRightArmTwistMuscle,retargetAfterClampPoseMusclesRightArmTwistMuscle,retargetAfterAnatomicalArmGuardRightArmTwistMuscle,retargetAfterVisualSpikeSmoothingRightArmTwistMuscle,retargetSetHumanPoseInputRightArmTwistMuscle,retargetSetHumanPoseOutputRightArmTwistMuscle,retargetSetHumanPoseRightArmTwistDelta,retargetSetHumanPoseInputLeftUpperLegFrontBackMuscle,retargetSetHumanPoseOutputLeftUpperLegFrontBackMuscle,retargetSetHumanPoseLeftUpperLegFrontBackDelta,retargetSetHumanPoseInputRightUpperLegFrontBackMuscle,retargetSetHumanPoseOutputRightUpperLegFrontBackMuscle,retargetSetHumanPoseRightUpperLegFrontBackDelta,retargetSetHumanPoseInputLeftLowerLegStretchMuscle,retargetSetHumanPoseOutputLeftLowerLegStretchMuscle,retargetSetHumanPoseLeftLowerLegStretchDelta,retargetSetHumanPoseInputRightLowerLegStretchMuscle,retargetSetHumanPoseOutputRightLowerLegStretchMuscle,retargetSetHumanPoseRightLowerLegStretchDelta,retargetSetHumanPoseInputLeftFootUpDownMuscle,retargetSetHumanPoseOutputLeftFootUpDownMuscle,retargetSetHumanPoseLeftFootUpDownDelta,retargetSetHumanPoseInputRightFootUpDownMuscle,retargetSetHumanPoseOutputRightFootUpDownMuscle,retargetSetHumanPoseRightFootUpDownDelta,bodyPositionY,hipsLocalY,retargetFootHeightReferenceLift,hipsX,hipsZ,hipsY,lowestFootY,lowestFootBottomY,leftFootX,leftFootZ,rightFootX,rightFootZ,meshBoundsMinY,meshBoundsMaxY,footBottomGroundGap,meshBoundsGroundGap,cameraFacingDot,maxScaleDelta,leftUpperArmScale,rightUpperArmScale,leftUpperLegScale,rightUpperLegScale,leftArmLength,rightArmLength,leftLegLength,rightLegLength,leftElbowAngle,rightElbowAngle,leftKneeAngle,rightKneeAngle,leftElbowBendForward,rightElbowBendForward,leftKneeBendForward,rightKneeBendForward,leftElbowBendOffsetForward,rightElbowBendOffsetForward,leftKneeBendOffsetForward,rightKneeBendOffsetForward,leftUpperArmDownDot,rightUpperArmDownDot,leftHandHorizontalRatio,rightHandHorizontalRatio,leftHandBelowShoulderRatio,rightHandBelowShoulderRatio,leftHandTorsoSignedClearance,rightHandTorsoSignedClearance,minHandTorsoSignedClearance,handTorsoPenetrationRisk,leftShoulderDownUpMuscle,leftShoulderFrontBackMuscle,leftArmDownUpMuscle,leftArmFrontBackMuscle,leftArmTwistMuscle,armSwingGuardLeftApplied,armSwingGuardLeftHorizontalReachApplied,armSwingGuardLeftRaisedReachApplied,armSwingGuardLeftForearmStretchBefore,armSwingGuardLeftForearmStretchAfter,armSwingGuardLeftForearmStretchDelta,leftForearmStretchMuscle,leftForearmTwistMuscle,rightShoulderDownUpMuscle,rightShoulderFrontBackMuscle,rightArmDownUpMuscle,rightArmFrontBackMuscle,rightArmTwistMuscle,armSwingGuardRightApplied,armSwingGuardRightHorizontalReachApplied,armSwingGuardRightRaisedReachApplied,armSwingGuardRightForearmStretchBefore,armSwingGuardRightForearmStretchAfter,armSwingGuardRightForearmStretchDelta,rightForearmStretchMuscle,rightForearmTwistMuscle,leftThumb1StretchMuscle,leftThumbSpreadMuscle,leftIndex1StretchMuscle,leftIndexSpreadMuscle,leftMiddle1StretchMuscle,leftMiddleSpreadMuscle,leftRing1StretchMuscle,leftRingSpreadMuscle,leftLittle1StretchMuscle,leftLittleSpreadMuscle,rightThumb1StretchMuscle,rightThumbSpreadMuscle,rightIndex1StretchMuscle,rightIndexSpreadMuscle,rightMiddle1StretchMuscle,rightMiddleSpreadMuscle,rightRing1StretchMuscle,rightRingSpreadMuscle,rightLittle1StretchMuscle,rightLittleSpreadMuscle,spineLocalEuler,chestLocalEuler,upperChestLocalEuler,leftShoulderLocalEuler,rightShoulderLocalEuler,leftUpperArmLocalEuler,rightUpperArmLocalEuler,leftLowerArmLocalEuler,rightLowerArmLocalEuler,leftHandLocalEuler,rightHandLocalEuler,leftThumbProximalLocalEuler,leftIndexProximalLocalEuler,leftMiddleProximalLocalEuler,leftRingProximalLocalEuler,leftLittleProximalLocalEuler,rightThumbProximalLocalEuler,rightIndexProximalLocalEuler,rightMiddleProximalLocalEuler,rightRingProximalLocalEuler,rightLittleProximalLocalEuler";
    private const string YybDiagnosticMetricsCsvHeader = "leftThumbIndexSpreadAngle,rightThumbIndexSpreadAngle,leftThumbPalmProjection,rightThumbPalmProjection,leftThumbSpreadRisk,rightThumbSpreadRisk,leftThumbProjectionRisk,rightThumbProjectionRisk,leftThumbHelperSourceDistance,rightThumbHelperSourceDistance,leftThumbHelperSourceDistanceDelta,rightThumbHelperSourceDistanceDelta,leftThumbHelperSourceRotationDelta,rightThumbHelperSourceRotationDelta,leftThumbHelperSeparationRisk,rightThumbHelperSeparationRisk,leftWebbingRisk,rightWebbingRisk,leftArmTwistRisk,rightArmTwistRisk,leftSleeveAnchorRisk,rightSleeveAnchorRisk,leftSleeveAnchorDistance,rightSleeveAnchorDistance,leftSleeveThicknessRatio,rightSleeveThicknessRatio,leftSleeveThicknessRisk,rightSleeveThicknessRisk,leftYybDeformationRisk,rightYybDeformationRisk,yybMaxDeformationRisk,thumbGuardManualReferenceConfigured,thumbGuardManualReferenceActive,thumbGuardPoseShapingSuppressed,thumbGuardLeftPoseShapingSuppressed,thumbGuardRightPoseShapingSuppressed,thumbGuardProjectionWeight,thumbGuardLeftProjectionWeight,thumbGuardRightProjectionWeight,thumbGuardIndexSpreadWeight,thumbGuardLeftIndexSpreadWeight,thumbGuardRightIndexSpreadWeight,thumbGuardSegmentStraightenWeight,thumbGuardLeftSegmentStraightenWeight,thumbGuardRightSegmentStraightenWeight,thumbGuardLeftProjectionCorrectionApplyCount,thumbGuardRightProjectionCorrectionApplyCount,thumbGuardLeftProjectionCorrectionPreserveCount,thumbGuardRightProjectionCorrectionPreserveCount,thumbGuardLeftSegmentStraightenApplyCount,thumbGuardRightSegmentStraightenApplyCount,thumbGuardLeftSegmentStraightenPreserveCount,thumbGuardRightSegmentStraightenPreserveCount,thumbGuardLeftLocalRotationGuardClampCount,thumbGuardRightLocalRotationGuardClampCount,thumbGuardLeftLocalRotationGuardPreserveCount,thumbGuardRightLocalRotationGuardPreserveCount,thumbGuardLeftLocalRotationGuardCurrentRisk,thumbGuardRightLocalRotationGuardCurrentRisk,thumbGuardLeftLocalRotationGuardLimitedRisk,thumbGuardRightLocalRotationGuardLimitedRisk,thumbGuardLeftWorldRotationSuppressCompetingOverride,thumbGuardRightWorldRotationSuppressCompetingOverride,thumbGuardLeftWorldRotationKeepDetachedHelperOverride,thumbGuardRightWorldRotationKeepDetachedHelperOverride,thumbGuardLeftWorldRotationCurrentReferenceFrameDeviation,thumbGuardRightWorldRotationCurrentReferenceFrameDeviation,thumbGuardLeftWorldRotationCandidateReferenceFrameDeviation,thumbGuardRightWorldRotationCandidateReferenceFrameDeviation,thumbGuardLeftProximalWorldRotationPreserveReason,thumbGuardRightProximalWorldRotationPreserveReason,thumbGuardLeftIntermediateWorldRotationPreserveReason,thumbGuardRightIntermediateWorldRotationPreserveReason,thumbGuardLeftProximalWorldRotationCurrentReferenceAngle,thumbGuardRightProximalWorldRotationCurrentReferenceAngle,thumbGuardLeftIntermediateWorldRotationCurrentReferenceAngle,thumbGuardRightIntermediateWorldRotationCurrentReferenceAngle,thumbGuardLeftProximalWorldRotationCandidateReferenceAngle,thumbGuardRightProximalWorldRotationCandidateReferenceAngle,thumbGuardLeftIntermediateWorldRotationCandidateReferenceAngle,thumbGuardRightIntermediateWorldRotationCandidateReferenceAngle,thumbGuardLeftProximalWorldRotationPreserveCurrentRisk,thumbGuardRightProximalWorldRotationPreserveCurrentRisk,thumbGuardLeftIntermediateWorldRotationPreserveCurrentRisk,thumbGuardRightIntermediateWorldRotationPreserveCurrentRisk,thumbGuardLeftProximalWorldRotationPreserveLimitedRisk,thumbGuardRightProximalWorldRotationPreserveLimitedRisk,thumbGuardLeftIntermediateWorldRotationPreserveLimitedRisk,thumbGuardRightIntermediateWorldRotationPreserveLimitedRisk,thumbGuardHelperSyncEnabled,thumbGuardHelperPositionSyncEnabled,thumbGuardHelperSyncWeight,thumbGuardHelperMaxLocalAngle,thumbGuardPalmStabilizeEnabled,thumbGuardPalmStabilizeWeight,thumbGuardPalmStabilizeMaxLocalAngle,thumbGuardWebbingStabilizeEnabled,thumbGuardWebbingStabilizeWeight,thumbGuardWebbingMaxLocalAngle,thumbGuardWebbingMaxPositionOffset";
    private const string RetargetEndpointStageDiagnosticsCsvHeader =
        "retargetStageGhostLeftFootWorldX,retargetStageGhostLeftFootWorldZ,retargetStageGhostLeftToesWorldX,retargetStageGhostLeftToesWorldZ," +
        "retargetStageGhostRightFootWorldX,retargetStageGhostRightFootWorldZ,retargetStageGhostRightToesWorldX,retargetStageGhostRightToesWorldZ," +
        "retargetStageAfterSetHumanPoseLeftFootWorldX,retargetStageAfterSetHumanPoseLeftFootWorldZ,retargetStageAfterSetHumanPoseLeftToesWorldX,retargetStageAfterSetHumanPoseLeftToesWorldZ," +
        "retargetStageAfterSetHumanPoseRightFootWorldX,retargetStageAfterSetHumanPoseRightFootWorldZ,retargetStageAfterSetHumanPoseRightToesWorldX,retargetStageAfterSetHumanPoseRightToesWorldZ," +
        "retargetStageAfterManualReferencesLeftFootWorldX,retargetStageAfterManualReferencesLeftFootWorldZ,retargetStageAfterManualReferencesLeftToesWorldX,retargetStageAfterManualReferencesLeftToesWorldZ," +
        "retargetStageAfterManualReferencesRightFootWorldX,retargetStageAfterManualReferencesRightFootWorldZ,retargetStageAfterManualReferencesRightToesWorldX,retargetStageAfterManualReferencesRightToesWorldZ," +
        "retargetStageAfterRootRestoreLeftFootWorldX,retargetStageAfterRootRestoreLeftFootWorldZ,retargetStageAfterRootRestoreLeftToesWorldX,retargetStageAfterRootRestoreLeftToesWorldZ," +
        "retargetStageAfterRootRestoreRightFootWorldX,retargetStageAfterRootRestoreRightFootWorldZ,retargetStageAfterRootRestoreRightToesWorldX,retargetStageAfterRootRestoreRightToesWorldZ," +
        "retargetStageAfterRootDeltaLeftFootWorldX,retargetStageAfterRootDeltaLeftFootWorldZ,retargetStageAfterRootDeltaLeftToesWorldX,retargetStageAfterRootDeltaLeftToesWorldZ," +
        "retargetStageAfterRootDeltaRightFootWorldX,retargetStageAfterRootDeltaRightFootWorldZ,retargetStageAfterRootDeltaRightToesWorldX,retargetStageAfterRootDeltaRightToesWorldZ," +
        "retargetStageAfterGroundingLeftFootWorldX,retargetStageAfterGroundingLeftFootWorldZ,retargetStageAfterGroundingLeftToesWorldX,retargetStageAfterGroundingLeftToesWorldZ," +
        "retargetStageAfterGroundingRightFootWorldX,retargetStageAfterGroundingRightFootWorldZ,retargetStageAfterGroundingRightToesWorldX,retargetStageAfterGroundingRightToesWorldZ," +
        "retargetStageAfterBipedIKLeftFootWorldX,retargetStageAfterBipedIKLeftFootWorldZ,retargetStageAfterBipedIKLeftToesWorldX,retargetStageAfterBipedIKLeftToesWorldZ," +
        "retargetStageAfterBipedIKRightFootWorldX,retargetStageAfterBipedIKRightFootWorldZ,retargetStageAfterBipedIKRightToesWorldX,retargetStageAfterBipedIKRightToesWorldZ," +
        "retargetStageAfterLateVisualGroundingLeftFootWorldX,retargetStageAfterLateVisualGroundingLeftFootWorldZ,retargetStageAfterLateVisualGroundingLeftToesWorldX,retargetStageAfterLateVisualGroundingLeftToesWorldZ," +
        "retargetStageAfterLateVisualGroundingRightFootWorldX,retargetStageAfterLateVisualGroundingRightFootWorldZ,retargetStageAfterLateVisualGroundingRightToesWorldX,retargetStageAfterLateVisualGroundingRightToesWorldZ";
    private const string LowerBodyPostPoseDiagnosticsCsvHeader =
        "retargetEditorFootLocalRotationLeftFootXzDelta,retargetEditorFootLocalRotationRightFootXzDelta," +
        "retargetEditorLowerBodySegmentDirectionLeftFootXzDelta,retargetEditorLowerBodySegmentDirectionRightFootXzDelta," +
        "retargetEditorLowerBodySegmentDirectionMaxCorrectionSegment,retargetEditorLowerBodySegmentDirectionMaxCorrectionAngle," +
        "retargetEditorLowerBodySegmentDirectionMaxPreAngle,retargetEditorLowerBodySegmentDirectionMaxPostAngle," +
        "retargetEditorLowerBodySegmentDirectionMaxCorrectionAxisX,retargetEditorLowerBodySegmentDirectionMaxCorrectionAxisY,retargetEditorLowerBodySegmentDirectionMaxCorrectionAxisZ," +
        "retargetEditorLowerBodySegmentDirectionMaxReferenceDirectionX,retargetEditorLowerBodySegmentDirectionMaxReferenceDirectionY,retargetEditorLowerBodySegmentDirectionMaxReferenceDirectionZ," +
        "retargetEditorLowerBodySegmentDirectionMaxPreDirectionX,retargetEditorLowerBodySegmentDirectionMaxPreDirectionY,retargetEditorLowerBodySegmentDirectionMaxPreDirectionZ," +
        "retargetEditorLowerBodySegmentDirectionMaxPostDirectionX,retargetEditorLowerBodySegmentDirectionMaxPostDirectionY,retargetEditorLowerBodySegmentDirectionMaxPostDirectionZ," +
        "retargetEditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle,retargetEditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle," +
        "retargetEditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle,retargetEditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle," +
        "retargetEditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle,retargetEditorLowerBodySegmentDirectionRightFootToesCorrectionAngle," +
        "retargetEditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle,retargetEditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle," +
        "retargetEditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle,retargetEditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle," +
        "retargetEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX,retargetEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY,retargetEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ," +
        "retargetEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX,retargetEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY,retargetEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ," +
        "retargetEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX,retargetEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY,retargetEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ," +
        "retargetEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX,retargetEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY,retargetEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ," +
        "retargetEditorLowerBodySegmentDirectionRightFootToToesPreDirectionX,retargetEditorLowerBodySegmentDirectionRightFootToToesPreDirectionY,retargetEditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ," +
        "retargetEditorLowerBodySegmentDirectionRightFootToToesPostDirectionX,retargetEditorLowerBodySegmentDirectionRightFootToToesPostDirectionY,retargetEditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ," +
        "retargetEditorLowerBodySegmentDirectionLeftLowerLegWorldX,retargetEditorLowerBodySegmentDirectionLeftLowerLegWorldY,retargetEditorLowerBodySegmentDirectionLeftLowerLegWorldZ," +
        "retargetEditorLowerBodySegmentDirectionLeftFootWorldX,retargetEditorLowerBodySegmentDirectionLeftFootWorldY,retargetEditorLowerBodySegmentDirectionLeftFootWorldZ," +
        "retargetEditorLowerBodySegmentDirectionLeftToesWorldX,retargetEditorLowerBodySegmentDirectionLeftToesWorldY,retargetEditorLowerBodySegmentDirectionLeftToesWorldZ," +
        "retargetEditorLowerBodySegmentDirectionRightLowerLegWorldX,retargetEditorLowerBodySegmentDirectionRightLowerLegWorldY,retargetEditorLowerBodySegmentDirectionRightLowerLegWorldZ," +
        "retargetEditorLowerBodySegmentDirectionRightFootWorldX,retargetEditorLowerBodySegmentDirectionRightFootWorldY,retargetEditorLowerBodySegmentDirectionRightFootWorldZ," +
        "retargetEditorLowerBodySegmentDirectionRightToesWorldX,retargetEditorLowerBodySegmentDirectionRightToesWorldY,retargetEditorLowerBodySegmentDirectionRightToesWorldZ," +
        "retargetEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX,retargetEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY,retargetEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ," +
        "retargetEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX,retargetEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY,retargetEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ," +
        "retargetEditorLowerBodySegmentDirectionLeftFootForwardX,retargetEditorLowerBodySegmentDirectionLeftFootForwardY,retargetEditorLowerBodySegmentDirectionLeftFootForwardZ," +
        "retargetEditorLowerBodySegmentDirectionLeftFootUpX,retargetEditorLowerBodySegmentDirectionLeftFootUpY,retargetEditorLowerBodySegmentDirectionLeftFootUpZ," +
        "retargetEditorLowerBodySegmentDirectionRightFootForwardX,retargetEditorLowerBodySegmentDirectionRightFootForwardY,retargetEditorLowerBodySegmentDirectionRightFootForwardZ," +
        "retargetEditorLowerBodySegmentDirectionRightFootUpX,retargetEditorLowerBodySegmentDirectionRightFootUpY,retargetEditorLowerBodySegmentDirectionRightFootUpZ," +
        "retargetEditorFootHipsAlignedResidualYawLeftFootXzDelta,retargetEditorFootHipsAlignedResidualYawRightFootXzDelta," +
        "retargetPostSetRightEndpointDesiredFootWorldX,retargetPostSetRightEndpointDesiredFootWorldZ," +
        "retargetPostSetRightEndpointDesiredToesWorldX,retargetPostSetRightEndpointDesiredToesWorldZ," +
        "retargetPostSetRightEndpointCurrentFootWorldX,retargetPostSetRightEndpointCurrentFootWorldZ," +
        "retargetPostSetRightEndpointCurrentToesWorldX,retargetPostSetRightEndpointCurrentToesWorldZ," +
        "retargetPostSetRightEndpointDeltaBeforeClampX,retargetPostSetRightEndpointDeltaBeforeClampZ," +
        "retargetPostSetRightEndpointDeltaAfterClampX,retargetPostSetRightEndpointDeltaAfterClampZ," +
        "retargetPostSetRightEndpointDeltaAfterPositiveZScaleX,retargetPostSetRightEndpointDeltaAfterPositiveZScaleZ," +
        "retargetPostSetRightEndpointCorrectionX,retargetPostSetRightEndpointCorrectionZ," +
        "retargetPostSetRightEndpointNextFootWorldX,retargetPostSetRightEndpointNextFootWorldZ," +
        "retargetPostSetRightEndpointMaxYawAngle,retargetPostSetRightEndpointYawCorrectionAngle," +
        "retargetPostSetRightEndpointUpperLegRotationDeltaAngle,retargetPostSetRightEndpointApplied," +
        "retargetPostSetRightEndpointEvaluatorXzReferenceEnabled," +
        "retargetPostSetRightEndpointEvaluatorXzFirstOffsetX,retargetPostSetRightEndpointEvaluatorXzFirstOffsetZ," +
        "retargetPostSetRightEndpointEvaluatorXzNormalizedDeltaX,retargetPostSetRightEndpointEvaluatorXzNormalizedDeltaZ," +
        "retargetPostSetRightEndpointEvaluatorXzNormalizedMagnitude," +
        "retargetPostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaX,retargetPostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaZ," +
        "retargetPostSetRightEndpointEvaluatorXzTargetMagnitude";
    private const string SetHumanPoseBodyDiagnosticsCsvHeader =
        "retargetSetHumanPoseInputBodyPositionX,retargetSetHumanPoseInputBodyPositionY,retargetSetHumanPoseInputBodyPositionZ," +
        "retargetSetHumanPoseOutputBodyPositionX,retargetSetHumanPoseOutputBodyPositionY,retargetSetHumanPoseOutputBodyPositionZ," +
        "retargetSetHumanPoseBodyPositionDeltaX,retargetSetHumanPoseBodyPositionDeltaZ," +
        "retargetSetHumanPoseBodyPositionDeltaXZ,retargetSetHumanPoseInputBodyRotationYaw,retargetSetHumanPoseOutputBodyRotationYaw," +
        "retargetSetHumanPoseBodyRotationDeltaAngle";
    private const string SetHumanPosePreSolveBasisDiagnosticsCsvHeader =
        "retargetSetHumanPosePreSolveGhostRootWorldX,retargetSetHumanPosePreSolveGhostRootWorldY,retargetSetHumanPosePreSolveGhostRootWorldZ,retargetSetHumanPosePreSolveGhostRootYaw," +
        "retargetSetHumanPosePreSolveTargetRootWorldX,retargetSetHumanPosePreSolveTargetRootWorldY,retargetSetHumanPosePreSolveTargetRootWorldZ,retargetSetHumanPosePreSolveTargetRootYaw," +
        "retargetSetHumanPosePreSolveTargetHipsWorldX,retargetSetHumanPosePreSolveTargetHipsWorldY,retargetSetHumanPosePreSolveTargetHipsWorldZ," +
        "retargetSetHumanPosePreSolveTargetHipsLocalX,retargetSetHumanPosePreSolveTargetHipsLocalY,retargetSetHumanPosePreSolveTargetHipsLocalZ," +
        "retargetSetHumanPosePreSolveBodyPositionX,retargetSetHumanPosePreSolveBodyPositionY,retargetSetHumanPosePreSolveBodyPositionZ,retargetSetHumanPosePreSolveBodyRotationYaw," +
        "retargetPreSetHumanPoseEndpointBodyPositionBeforeX,retargetPreSetHumanPoseEndpointBodyPositionBeforeZ," +
        "retargetPreSetHumanPoseEndpointBodyPositionAfterX,retargetPreSetHumanPoseEndpointBodyPositionAfterZ," +
        "retargetPreSetHumanPoseEndpointBodyPositionDeltaX,retargetPreSetHumanPoseEndpointBodyPositionDeltaZ,retargetPreSetHumanPoseEndpointBodyPositionDeltaMagnitudeXZ," +
        "retargetSetHumanPoseRealizedLeftFootDeltaX,retargetSetHumanPoseRealizedLeftFootDeltaZ,retargetSetHumanPoseRealizedLeftFootDeltaMagnitudeXZ," +
        "retargetSetHumanPoseLeftFootResponseXPerBodyPositionX,retargetSetHumanPoseLeftFootResponseZPerBodyPositionX," +
        "retargetSetHumanPoseLeftFootResponseXPerBodyPositionZ,retargetSetHumanPoseLeftFootResponseZPerBodyPositionZ," +
        "retargetSetHumanPoseRealizedRightFootDeltaX,retargetSetHumanPoseRealizedRightFootDeltaZ,retargetSetHumanPoseRealizedRightFootDeltaMagnitudeXZ," +
        "retargetSetHumanPoseRightFootResponseXPerBodyPositionX,retargetSetHumanPoseRightFootResponseZPerBodyPositionX," +
        "retargetSetHumanPoseRightFootResponseXPerBodyPositionZ,retargetSetHumanPoseRightFootResponseZPerBodyPositionZ," +
        "retargetSetHumanPoseRightFootResponseXPerSetHumanPoseBodyPositionDeltaX,retargetSetHumanPoseRightFootResponseZPerSetHumanPoseBodyPositionDeltaX," +
        "retargetSetHumanPoseRightFootResponseXPerSetHumanPoseBodyPositionDeltaZ,retargetSetHumanPoseRightFootResponseZPerSetHumanPoseBodyPositionDeltaZ," +
        "retargetSetHumanPosePreSolveGhostLeftFootWorldX,retargetSetHumanPosePreSolveGhostLeftFootWorldZ," +
        "retargetSetHumanPosePreSolveGhostLeftToesWorldX,retargetSetHumanPosePreSolveGhostLeftToesWorldZ," +
        "retargetSetHumanPosePreSolveCurrentLeftFootWorldX,retargetSetHumanPosePreSolveCurrentLeftFootWorldZ," +
        "retargetSetHumanPosePreSolveCurrentLeftToesWorldX,retargetSetHumanPosePreSolveCurrentLeftToesWorldZ," +
        "retargetSetHumanPosePreSolveTargetLeftFootWorldX,retargetSetHumanPosePreSolveTargetLeftFootWorldZ," +
        "retargetSetHumanPosePreSolveTargetLeftToesWorldX,retargetSetHumanPosePreSolveTargetLeftToesWorldZ," +
        "retargetSetHumanPosePreSolveGhostRightFootWorldX,retargetSetHumanPosePreSolveGhostRightFootWorldZ," +
        "retargetSetHumanPosePreSolveGhostRightToesWorldX,retargetSetHumanPosePreSolveGhostRightToesWorldZ," +
        "retargetSetHumanPosePreSolveCurrentRightFootWorldX,retargetSetHumanPosePreSolveCurrentRightFootWorldZ," +
        "retargetSetHumanPosePreSolveCurrentRightToesWorldX,retargetSetHumanPosePreSolveCurrentRightToesWorldZ," +
        "retargetSetHumanPosePreSolveTargetRightFootWorldX,retargetSetHumanPosePreSolveTargetRightFootWorldZ," +
        "retargetSetHumanPosePreSolveTargetRightToesWorldX,retargetSetHumanPosePreSolveTargetRightToesWorldZ";
    private const string SetHumanPoseExtendedInputDiagnosticsCsvHeader =
        "retargetSetHumanPoseInputSpineFrontBackMuscle,retargetSetHumanPoseInputSpineLeftRightMuscle,retargetSetHumanPoseInputSpineTwistLeftRightMuscle," +
        "retargetSetHumanPoseInputChestFrontBackMuscle,retargetSetHumanPoseInputChestLeftRightMuscle,retargetSetHumanPoseInputChestTwistLeftRightMuscle," +
        "retargetSetHumanPoseInputUpperChestFrontBackMuscle,retargetSetHumanPoseInputUpperChestLeftRightMuscle,retargetSetHumanPoseInputUpperChestTwistLeftRightMuscle," +
        "retargetSetHumanPoseInputLeftUpperLegInOutMuscle,retargetSetHumanPoseInputRightUpperLegInOutMuscle," +
        "retargetSetHumanPoseInputLeftUpperLegTwistInOutMuscle,retargetSetHumanPoseInputRightUpperLegTwistInOutMuscle," +
        "retargetSetHumanPoseInputLeftLowerLegTwistInOutMuscle,retargetSetHumanPoseInputRightLowerLegTwistInOutMuscle," +
        "retargetSetHumanPoseInputLeftFootTwistInOutMuscle,retargetSetHumanPoseInputRightFootTwistInOutMuscle," +
        "retargetSetHumanPoseInputLeftToesUpDownMuscle,retargetSetHumanPoseInputRightToesUpDownMuscle";
    private const string SetHumanPoseRightLegOutputDiagnosticsCsvHeader =
        "retargetSetHumanPoseOutputRightUpperLegInOutMuscle,retargetSetHumanPoseRightUpperLegInOutDelta," +
        "retargetSetHumanPoseOutputRightUpperLegTwistInOutMuscle,retargetSetHumanPoseRightUpperLegTwistInOutDelta," +
        "retargetSetHumanPoseOutputRightLowerLegTwistInOutMuscle,retargetSetHumanPoseRightLowerLegTwistInOutDelta," +
        "retargetSetHumanPoseOutputRightFootTwistInOutMuscle,retargetSetHumanPoseRightFootTwistInOutDelta," +
        "retargetSetHumanPoseOutputRightToesUpDownMuscle,retargetSetHumanPoseRightToesUpDownDelta";
    private const string SessionManifestArtifactsHeading = "## \uc0b0\ucd9c\ubb3c";
    private const string SessionManifestArtifactsTableHeader = "| \uc5ed\ud560 | \uacbd\ub85c |";
    private const string SessionManifestArtifactsTableSeparator = "|---|---|";

    public static void AppendScreenshotIndexRow(string indexFilePath, MotionComparisonProbeScreenshotIndexRow row)
    {
        if (string.IsNullOrEmpty(indexFilePath))
        {
            return;
        }

        EnsureParentDirectoryExists(indexFilePath);
        File.AppendAllText(indexFilePath, BuildScreenshotIndexRowCsvLine(row) + Environment.NewLine, Encoding.UTF8);
    }

    public static void WriteScreenshotIndexCsvHeader(string indexFilePath)
    {
        if (string.IsNullOrEmpty(indexFilePath))
        {
            return;
        }

        EnsureParentDirectoryExists(indexFilePath);
        File.WriteAllText(indexFilePath, MotionComparisonProbeScreenshotIndexCsvHeaderFormatter.Build() + Environment.NewLine, Encoding.UTF8);
    }

    internal static string BuildScreenshotIndexRowCsvLine(MotionComparisonProbeScreenshotIndexRow row)
    {
        return string.Join(",",
            EscapeCsv(row.ComparisonLabel),
            EscapeCsv(row.SceneName),
            EscapeCsv(row.Reason),
            FormatCsvInt(row.RecorderFrame),
            EscapeCsv(row.ViewName),
            EscapeCsv(row.RelativePath));
    }

    public static void WriteFrameSessionIndexMarkdown(string filePath, MotionComparisonProbeFrameSessionIndexData data)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        EnsureParentDirectoryExists(filePath);
        File.WriteAllText(filePath, BuildFrameSessionIndexMarkdown(data), Encoding.UTF8);
    }

    public static void WriteScreenshotSessionFiles(
        string screenshotIndexPath,
        string frameSessionIndexPath,
        MotionComparisonProbeFrameSessionIndexData frameSessionIndexData)
    {
        WriteScreenshotIndexCsvHeader(screenshotIndexPath);
        WriteFrameSessionIndexMarkdown(frameSessionIndexPath, frameSessionIndexData);
    }

    internal static string BuildComparisonLabel(string currentLabel, string labelOverride, string gameObjectName)
    {
        string label = !string.IsNullOrWhiteSpace(labelOverride)
            ? labelOverride
            : !string.IsNullOrWhiteSpace(currentLabel)
                ? currentLabel
                : gameObjectName;
        return SanitizeFileName(label);
    }

    internal static string BuildCaptureCameraObjectName(string comparisonLabel)
    {
        string label = string.IsNullOrWhiteSpace(comparisonLabel) ? "motion_comparison" : comparisonLabel;
        return $"MotionComparisonCapture_{label}";
    }

    internal static string BuildRetargeterLegacyAnimationTimeSourceLabel()
    {
        return "retargeterLegacy";
    }

    internal static string BuildRetargeterLegacyRecorderFrameAnimationTimeSourceLabel()
    {
        return "retargeterLegacyRecorderFrame";
    }

    internal static string BuildAnimatorStateAnimationTimeSourceLabel()
    {
        return "animatorState";
    }

    internal static string BuildUnknownAnimationTimeSourceLabel()
    {
        return "";
    }

    internal static string SanitizeFileName(string fileName)
    {
        string cleanName = string.IsNullOrWhiteSpace(fileName) ? "motion_comparison" : fileName.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            cleanName = cleanName.Replace(invalidChar, '_');
        }

        return cleanName.Replace(' ', '_');
    }

    internal static string BuildFrameSessionIndexMarkdown(MotionComparisonProbeFrameSessionIndexData data)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# 비교 프레임 세션 연결");
        builder.AppendLine();
        builder.AppendLine($"- session id: `{EscapeMarkdown(data.SessionId)}`");
        builder.AppendLine($"- session manifest: `{EscapeMarkdown(data.SessionManifestRelativePath)}`");
        builder.AppendLine($"- metrics csv: `{EscapeMarkdown(data.MetricsCsvRelativePath)}`");
        builder.AppendLine($"- frame index: `{EscapeMarkdown(data.FrameIndexCsvRelativePath)}`");
        builder.AppendLine();
        builder.AppendLine("이 파일은 `ComparisonFrames`에 분리 저장된 PNG가 어떤 CSV 로그와 같은 실행에서 생성됐는지 추적하기 위한 역참조다.");
        return builder.ToString();
    }

    public static void WriteMetricsCsvHeader(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        EnsureParentDirectoryExists(filePath);
        File.WriteAllText(filePath, BuildMetricsCsvHeader() + Environment.NewLine, Encoding.UTF8);
    }

    public static void AppendMetricsCsvLine(string filePath, string csvLine)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        EnsureParentDirectoryExists(filePath);
        File.AppendAllText(filePath, csvLine + Environment.NewLine, Encoding.UTF8);
    }

    public static bool WriteScreenshotPngBytes(string filePath, byte[] pngBytes)
    {
        if (string.IsNullOrEmpty(filePath) || pngBytes == null || pngBytes.Length == 0)
        {
            return false;
        }

        EnsureParentDirectoryExists(filePath);
        File.WriteAllBytes(filePath, pngBytes);
        return File.Exists(filePath);
    }

    private static void EnsureParentDirectoryExists(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public static bool WriteNonBlankScreenshotPng(string filePath, Texture2D texture)
    {
        if (IsScreenshotTextureBlank(texture))
        {
            return false;
        }

        return WriteScreenshotPngBytes(filePath, texture.EncodeToPNG());
    }

    public static bool IsScreenshotTextureBlank(Texture2D texture)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0)
        {
            return true;
        }

        Color32 first = texture.GetPixel(0, 0);
        int samples = 0;
        for (int y = 0; y < texture.height; y += Mathf.Max(1, texture.height / 12))
        {
            for (int x = 0; x < texture.width; x += Mathf.Max(1, texture.width / 12))
            {
                Color32 current = texture.GetPixel(x, y);
                int delta =
                    Mathf.Abs(current.r - first.r) +
                    Mathf.Abs(current.g - first.g) +
                    Mathf.Abs(current.b - first.b);
                if (delta > 8)
                {
                    return false;
                }

                samples++;
            }
        }

        return samples > 0;
    }

    internal static string FormatMetricsCsvText(string value)
    {
        return EscapeCsv(value);
    }

    internal static string FormatMetricsCsvFloat(float value)
    {
        return IsFinite(value)
            ? value.ToString("0.######", CultureInfo.InvariantCulture)
            : "";
    }

    internal static string FormatCsvInt(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    internal static string BuildTransformPairKey(string label, int firstInstanceId, int secondInstanceId)
    {
        return string.Join(":",
            label ?? "",
            FormatCsvInt(firstInstanceId),
            FormatCsvInt(secondInstanceId));
    }

    internal static string BuildTransformPairKey(string label, Transform first, Transform second)
    {
        return BuildTransformPairKey(
            label,
            first != null ? first.GetInstanceID() : 0,
            second != null ? second.GetInstanceID() : 0);
    }

    internal static string BuildThumbHelperDistancePairKeyLabel(bool isRightSide)
    {
        return isRightSide ? "thumb-helper-distance-right" : "thumb-helper-distance-left";
    }

    internal static string BuildThumbHelperRotationPairKeyLabel(bool isRightSide)
    {
        return isRightSide ? "thumb-helper-rotation-right" : "thumb-helper-rotation-left";
    }

    internal static string BuildExplicitThumbBaseSourceCacheKey(bool isRightSide)
    {
        return isRightSide ? "thumb-explicit-source-right" : "thumb-explicit-source-left";
    }

    internal static string BuildThumbBaseHelperCacheKey(bool isRightSide)
    {
        return isRightSide ? "thumb-helper-right" : "thumb-helper-left";
    }

    internal static string BuildThumbBaseSourceCacheKey(bool isRightSide)
    {
        return isRightSide ? "thumb-source-right" : "thumb-source-left";
    }

    internal static string BuildDiagnosticTransformSideToken(bool isRightSide)
    {
        return isRightSide ? "right" : "left";
    }

    internal static bool MatchesDiagnosticTransformSide(string transformName, bool isRightSide)
    {
        return NormalizeDiagnosticTransformName(transformName)
            .Contains(BuildDiagnosticTransformSideToken(isRightSide));
    }

    internal static bool MatchesActiveThumbBaseSourceTransformName(string transformName, bool isRightSide)
    {
        string normalizedName = NormalizeDiagnosticTransformName(transformName);
        return MatchesDiagnosticTransformSide(normalizedName, isRightSide) &&
            MatchesActiveThumbBaseSourceName(normalizedName);
    }

    internal static bool MatchesDetachedThumbBaseHelperTransformName(string transformName, bool isRightSide)
    {
        string normalizedName = NormalizeDiagnosticTransformName(transformName);
        return MatchesDiagnosticTransformSide(normalizedName, isRightSide) &&
            MatchesDetachedThumbBaseHelperName(normalizedName);
    }

    internal static string NormalizeDiagnosticTransformName(string value)
    {
        return string.IsNullOrEmpty(value) ? "" : value.ToLowerInvariant();
    }

    internal static bool MatchesYybModelName(string value)
    {
        return NormalizeDiagnosticTransformName(value).Contains("yyb");
    }

    internal static bool MatchesAmbiguousThumbExtraTransformCandidateName(string transformName)
    {
        string normalizedName = NormalizeDiagnosticTransformName(transformName);
        if (string.IsNullOrEmpty(normalizedName) ||
            !normalizedName.Contains("thumb") ||
            normalizedName.Contains("ghost") ||
            MatchesActiveThumbBaseSourceName(normalizedName))
        {
            return false;
        }

        return !ContainsStandardThumbSegmentName(normalizedName);
    }

    internal static bool MatchesDetachedThumbBaseHelperName(string transformName)
    {
        string normalizedName = NormalizeDiagnosticTransformName(transformName);
        if (string.IsNullOrEmpty(normalizedName) ||
            normalizedName.Contains("!") ||
            normalizedName.Contains("ghost") ||
            normalizedName.Contains("thumb0m"))
        {
            return false;
        }

        return MatchesThumbBaseName(normalizedName);
    }

    internal static bool MatchesActiveThumbBaseSourceName(string transformName)
    {
        string normalizedName = NormalizeDiagnosticTransformName(transformName);
        return !string.IsNullOrEmpty(normalizedName) &&
            normalizedName.Contains("thumb0m") &&
            !normalizedName.Contains("ghost") &&
            !normalizedName.Contains("thumb1") &&
            !normalizedName.Contains("thumb2") &&
            !normalizedName.Contains("thumbtip");
    }

    internal static bool MatchesThumbBaseName(string transformName)
    {
        string normalizedName = NormalizeDiagnosticTransformName(transformName);
        return !string.IsNullOrEmpty(normalizedName) &&
            normalizedName.Contains("thumb0") &&
            !normalizedName.Contains("thumb1") &&
            !normalizedName.Contains("thumb2") &&
            !normalizedName.Contains("thumbtip");
    }

    private static bool ContainsStandardThumbSegmentName(string normalizedName)
    {
        return normalizedName.Contains("thumb1") ||
            normalizedName.Contains("thumb2") ||
            normalizedName.Contains("thumb3") ||
            normalizedName.Contains("proximal") ||
            normalizedName.Contains("intermediate") ||
            normalizedName.Contains("distal") ||
            normalizedName.Contains("thumbtip");
    }

    internal static string BuildSleeveAnchorRotationPairKeyLabel(bool isRightSide)
    {
        return isRightSide ? "sleeve-anchor-rotation-right" : "sleeve-anchor-rotation-left";
    }

    internal static string BuildSleeveThicknessPairKeyLabel(bool isRightSide)
    {
        return isRightSide ? "sleeve-thickness-right" : "sleeve-thickness-left";
    }

    internal static string BuildSleeveAnchorTransformNameSuffix(bool isRightSide)
    {
        return isRightSide ? "joint_RightArmM" : "joint_LeftArmM";
    }

    internal static string BuildSleeveAnchorTransformCacheKey(bool isRightSide)
    {
        return "sleeve-anchor-" + BuildSleeveAnchorTransformNameSuffix(isRightSide);
    }

    internal static bool MatchesTransformNameSuffix(string transformName, string targetName)
    {
        if (string.IsNullOrEmpty(transformName) || string.IsNullOrEmpty(targetName))
        {
            return false;
        }

        return transformName == targetName ||
            transformName.EndsWith("." + targetName, StringComparison.Ordinal) ||
            transformName.EndsWith(targetName, StringComparison.Ordinal);
    }

    internal static bool MatchesSleeveAnchorTransformName(string transformName, bool isRightSide)
    {
        return MatchesTransformNameSuffix(transformName, BuildSleeveAnchorTransformNameSuffix(isRightSide));
    }

    internal static string BuildAnimatorMissingWarningMessage()
    {
        return "[MotionComparisonProbe] Animator is missing; comparison sampling cannot start.";
    }

    internal static string BuildNonZeroRecorderFrameStartWarningMessage(int recorderFrame)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[MotionComparisonProbe] comparison sampling started at recorderFrame={0}. Use only sessions that start at frame 0 for Main/Sub motion comparison.",
            recorderFrame);
    }

    internal static string BuildMissingHumanoidArmMusclesWarningMessage()
    {
        return "[MotionComparisonProbe] some Humanoid arm muscle indices were not found; matching CSV values will be blank.";
    }

    internal static string FormatMetricsCsvInt(int value)
    {
        return FormatCsvInt(value);
    }

    internal static string FormatMetricsCsvVector(Vector3 value)
    {
        return FormatMetricsCsvText(
            $"{FormatMetricsCsvFloat(value.x)}|{FormatMetricsCsvFloat(value.y)}|{FormatMetricsCsvFloat(value.z)}");
    }

    internal static string BuildMetricsCsvLine(params string[] formattedValues)
    {
        return string.Join(",", formattedValues ?? Array.Empty<string>());
    }

    internal static string BuildMetricsCsvHeader()
    {
        return MetricsCsvHeader + "," + YybDiagnosticMetricsCsvHeader + "," + RetargetEndpointStageDiagnosticsCsvHeader + "," + LowerBodyPostPoseDiagnosticsCsvHeader + "," + SetHumanPoseBodyDiagnosticsCsvHeader + "," + SetHumanPosePreSolveBasisDiagnosticsCsvHeader + "," + SetHumanPoseExtendedInputDiagnosticsCsvHeader + "," + SetHumanPoseRightLegOutputDiagnosticsCsvHeader;
    }

    public static MotionComparisonFrameQualitySummary BuildFrameQualitySummary(
        string baselineLabel,
        string baselineMetricsCsvPath,
        string candidateLabel,
        string candidateMetricsCsvPath,
        string candidateVmdPath,
        int baselineRecordedFrameCount,
        int candidateRecordedFrameCount,
        int targetFrameCount)
    {
        return BuildFrameQualitySummary(
            baselineLabel,
            baselineMetricsCsvPath,
            candidateLabel,
            candidateMetricsCsvPath,
            candidateVmdPath,
            baselineRecordedFrameCount,
            candidateRecordedFrameCount,
            targetFrameCount,
            evaluateVerticalSolvePostprocess: true);
    }

    private static MotionComparisonFrameQualitySummary BuildFrameQualitySummary(
        string baselineLabel,
        string baselineMetricsCsvPath,
        string candidateLabel,
        string candidateMetricsCsvPath,
        string candidateVmdPath,
        int baselineRecordedFrameCount,
        int candidateRecordedFrameCount,
        int targetFrameCount,
        bool evaluateVerticalSolvePostprocess)
    {
        MetricsCsvData baseline = ReadMetricsCsvData(baselineMetricsCsvPath);
        MetricsCsvData candidate = ReadMetricsCsvData(candidateMetricsCsvPath);
        VmdQualityMetrics vmd = ReadVmdQualityMetrics(candidateVmdPath);
        bool hasCandidateFirstFrame = candidate.TryGetFirstFrame(out MetricsCsvFrame candidateFirstFrame);

        MotionComparisonFrameQualitySummary summary = new MotionComparisonFrameQualitySummary
        {
            baseline_label = baselineLabel ?? "",
            candidate_label = candidateLabel ?? "",
            baseline_metrics_csv = baselineMetricsCsvPath ?? "",
            candidate_metrics_csv = candidateMetricsCsvPath ?? "",
            frame_quality_evaluation_role = "raw_candidate_metrics",
            frame_quality_evaluation_basis = "measured from the unmodified candidate metrics CSV; postprocess artifacts are reported separately",
            candidate_vmd_path = candidateVmdPath ?? "",
            mmd_result_status = "not_run",
            mmd_report_path = "",
            mmd_run_dir = "",
            mmd_after_play_screenshot_path = "",
            mmd_finished_at = "",
            status = "no_metrics",
            status_reason = "",
            baseline_metric_frames = baseline.Frames.Count,
            candidate_metric_frames = candidate.Frames.Count,
            compared_frames = 0,
            missing_baseline_frames = 0,
            missing_candidate_frames = 0,
            baseline_recorded_frame_count = baselineRecordedFrameCount,
            candidate_recorded_frame_count = candidateRecordedFrameCount,
            target_frame_count = targetFrameCount,
            baseline_frame_count_delta_from_target = targetFrameCount > 0 ? baselineRecordedFrameCount - targetFrameCount : 0,
            candidate_frame_count_delta_from_target = targetFrameCount > 0 ? candidateRecordedFrameCount - targetFrameCount : 0,
            candidate_below_floor_metric_frames = candidate.BelowFloorFrameCount,
            floor_contact_gate_status = "not_evaluated",
            floor_contact_gate_status_reason = "",
            floor_contact_corrected_diagnostic_status = "not_evaluated",
            floor_contact_corrected_diagnostic_status_reason = "",
            candidate_root_step_spike_frames = candidate.RootStepSpikeFrameCount,
            candidate_yyb_deformation_risk_column_present = candidate.HasYybMaxDeformationRiskColumn,
            candidate_yyb_deformation_risk_frame_count = candidate.YybDeformationRiskFrameCount,
            candidate_yyb_deformation_risk_missing_frames = candidate.YybDeformationRiskMissingFrameCount,
            candidate_yyb_max_deformation_risk = candidate.MaxYybDeformationRisk,
            candidate_yyb_sleeve_thickness_risk_column_present = candidate.HasYybSleeveThicknessRiskColumns,
            candidate_yyb_sleeve_thickness_risk_frame_count = candidate.YybSleeveThicknessRiskFrameCount,
            candidate_yyb_sleeve_thickness_risk_missing_frames = candidate.YybSleeveThicknessRiskMissingFrameCount,
            candidate_yyb_max_sleeve_thickness_risk = candidate.MaxYybSleeveThicknessRisk,
            candidate_vmd_bone_frames = vmd.BoneFrameCount,
            candidate_vmd_max_bone_frame_index = vmd.MaxBoneFrameIndex,
            candidate_vmd_center_spike_frames = vmd.CenterSpikeFrameCount,
            candidate_vmd_foot_ik_spike_frames = vmd.FootIkSpikeFrameCount,
            candidate_arm_motion_frames = candidate.ArmMotionFrameCount,
            candidate_leg_motion_frames = candidate.LegMotionFrameCount,
            candidate_arm_motion_root_travel = candidate.ArmMotionRootTravel,
            candidate_leg_motion_root_travel = candidate.LegMotionRootTravel,
            candidate_limb_motion_root_travel = candidate.LimbMotionRootTravel,
            max_candidate_limb_motion_root_step = candidate.MaxLimbMotionRootStep,
            max_same_frame_arm_pose_delta = float.NaN,
            max_same_frame_leg_pose_delta = float.NaN,
            max_same_frame_limb_pose_delta = float.NaN,
            max_same_frame_limb_pose_delta_recorder_frame = -1,
            max_same_frame_limb_pose_delta_candidate_recorder_frame = -1,
            max_same_frame_limb_pose_delta_source = "",
            max_same_frame_guard_normalized_arm_pose_delta = float.NaN,
            max_same_frame_guard_normalized_limb_pose_delta = float.NaN,
            max_same_frame_limb_pose_gate_delta = float.NaN,
            max_same_frame_limb_pose_gate_delta_recorder_frame = -1,
            max_same_frame_limb_pose_gate_delta_candidate_recorder_frame = -1,
            max_same_frame_limb_pose_gate_delta_source = "",
            max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range = float.NaN,
            max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range_recorder_frame = -1,
            max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range_candidate_recorder_frame = -1,
            max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range_source = "",
            raw_limb_pose_delta_excess_over_guard_normalized = float.NaN,
            raw_limb_pose_delta_saturation_basis = "",
            pre_retarget_start_compared_frames = 0,
            pre_retarget_start_max_same_frame_arm_pose_delta = float.NaN,
            pre_retarget_start_max_same_frame_limb_pose_delta = float.NaN,
            pre_retarget_start_max_same_frame_guard_normalized_arm_pose_delta = float.NaN,
            pre_retarget_start_max_same_frame_guard_normalized_limb_pose_delta = float.NaN,
            pre_retarget_start_max_same_frame_limb_pose_delta_recorder_frame = -1,
            pre_retarget_start_max_same_frame_limb_pose_delta_candidate_recorder_frame = -1,
            pre_retarget_start_evaluation_basis = "reason=start samples are captured before retarget LateUpdate stage diagnostics and are reported as pre-retarget start diagnostics outside the stationary naturalness gate",
            min_baseline_foot_bottom_y = baseline.MinFootBottomY,
            min_candidate_foot_bottom_y = candidate.MinFootBottomY,
            min_candidate_foot_bottom_ground_gap = candidate.MinFootBottomGroundGap,
            max_same_frame_root_position_delta = float.NaN,
            max_same_frame_root_y_delta = float.NaN,
            max_same_frame_hips_y_delta = float.NaN,
            max_same_frame_hips_y_delta_recorder_frame = -1,
            max_same_frame_hips_y_delta_candidate_recorder_frame = -1,
            max_same_frame_body_position_y_delta = float.NaN,
            max_same_frame_hips_local_y_delta = float.NaN,
            max_same_frame_hips_xz_delta = float.NaN,
            max_same_frame_hips_xz_delta_recorder_frame = -1,
            max_same_frame_hips_xz_delta_candidate_recorder_frame = -1,
            max_same_frame_grounding_vertical_step_delta = float.NaN,
            max_same_frame_foot_height_reference_lift_delta = float.NaN,
            max_same_frame_candidate_grounding_vertical_step_change = float.NaN,
            max_same_frame_candidate_foot_height_reference_lift_change = float.NaN,
            max_same_frame_hips_y_delta_root_y_component = float.NaN,
            max_same_frame_hips_y_delta_body_position_y_component = float.NaN,
            max_same_frame_hips_y_delta_hips_local_y_component = float.NaN,
            max_same_frame_hips_y_delta_foot_bottom_y_delta_at_frame = float.NaN,
            max_same_frame_foot_bottom_y_delta = float.NaN,
            max_same_frame_foot_bottom_y_delta_recorder_frame = -1,
            max_same_frame_foot_bottom_y_delta_candidate_recorder_frame = -1,
            max_same_frame_left_foot_xz_delta = float.NaN,
            max_same_frame_right_foot_xz_delta = float.NaN,
            max_same_frame_foot_xz_delta = float.NaN,
            max_same_frame_foot_xz_delta_recorder_frame = -1,
            max_same_frame_foot_xz_delta_candidate_recorder_frame = -1,
            max_same_frame_foot_xz_delta_side = "",
            max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range = float.NaN,
            max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range_recorder_frame = -1,
            max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range_candidate_recorder_frame = -1,
            max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range_side = "",
            max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range = float.NaN,
            max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range_recorder_frame = -1,
            max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range_candidate_recorder_frame = -1,
            max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range_side = "",
            max_same_frame_foot_xz_delta_after_hips_xz_alignment = float.NaN,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_x = float.NaN,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_z = float.NaN,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_angle_degrees = float.NaN,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_recorder_frame = -1,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_candidate_recorder_frame = -1,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_side = "",
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range = float.NaN,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range_recorder_frame = -1,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range_candidate_recorder_frame = -1,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range_side = "",
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range = float.NaN,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range_recorder_frame = -1,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range_candidate_recorder_frame = -1,
            max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range_side = "",
            vertical_solve_prototype_status = "not_evaluated",
            vertical_solve_prototype_status_reason = "",
            vertical_solve_prototype_basis = "metrics-stage prototype only; applies bounded per-frame Hips/foot vertical deltas without changing live retarget output",
            vertical_solve_prototype_max_same_frame_hips_y_delta = float.NaN,
            vertical_solve_prototype_max_same_frame_foot_bottom_y_delta = float.NaN,
            vertical_solve_prototype_max_same_frame_root_position_delta = float.NaN,
            vertical_solve_prototype_below_floor_metric_frames = candidate.BelowFloorFrameCount,
            vertical_solve_prototype_target_frame_count = targetFrameCount,
            vertical_solve_prototype_candidate_recorded_frame_count = candidateRecordedFrameCount,
            vertical_solve_prototype_hips_correction_recorder_frame = -1,
            vertical_solve_prototype_hips_correction_candidate_recorder_frame = -1,
            vertical_solve_prototype_hips_correction_y = float.NaN,
            vertical_solve_prototype_foot_correction_recorder_frame = -1,
            vertical_solve_prototype_foot_correction_candidate_recorder_frame = -1,
            vertical_solve_prototype_foot_correction_y = float.NaN,
            vertical_solve_postprocess_metrics_csv = BuildVerticalSolvePostprocessMetricsCsvPath(candidateMetricsCsvPath),
            vertical_solve_postprocess_status = "not_evaluated",
            vertical_solve_postprocess_status_reason = "",
            vertical_solve_postprocess_basis = "metrics-stage postprocess artifact; applies bounded vertical and horizontal foot carrier deltas while original frame_quality status remains measured from the unmodified candidate metrics",
            vertical_solve_postprocess_max_same_frame_hips_y_delta = float.NaN,
            vertical_solve_postprocess_max_same_frame_foot_bottom_y_delta = float.NaN,
            vertical_solve_postprocess_max_same_frame_root_position_delta = float.NaN,
            vertical_solve_postprocess_below_floor_metric_frames = candidate.BelowFloorFrameCount,
            vertical_solve_postprocess_target_frame_count = targetFrameCount,
            vertical_solve_postprocess_candidate_recorded_frame_count = candidateRecordedFrameCount,
            vertical_solve_postprocess_corrected_metric_frames = 0,
            vertical_solve_corrected_candidate_metrics_csv = BuildVerticalSolveCorrectedCandidateMetricsCsvPath(candidateMetricsCsvPath),
            vertical_solve_corrected_candidate_vmd_path = BuildVerticalSolveCorrectedCandidateVmdPath(candidateVmdPath),
            vertical_solve_corrected_candidate_manifest_path = BuildVerticalSolveCorrectedCandidateManifestPath(candidateMetricsCsvPath),
            vertical_solve_corrected_candidate_status = "not_evaluated",
            vertical_solve_corrected_candidate_status_reason = "",
            vertical_solve_corrected_candidate_basis = "explicit corrected candidate metrics/VMD artifact generated from the bounded vertical solve and horizontal foot carrier solve, then evaluated with the same raw frame_quality evaluator",
            vertical_solve_corrected_candidate_max_same_frame_hips_y_delta = float.NaN,
            vertical_solve_corrected_candidate_max_same_frame_foot_bottom_y_delta = float.NaN,
            vertical_solve_corrected_candidate_max_same_frame_root_position_delta = float.NaN,
            vertical_solve_corrected_candidate_below_floor_metric_frames = candidate.BelowFloorFrameCount,
            vertical_solve_corrected_candidate_target_frame_count = targetFrameCount,
            vertical_solve_corrected_candidate_recorded_frame_count = candidateRecordedFrameCount,
            vertical_solve_corrected_candidate_corrected_metric_frames = 0,
            candidate_foot_height_reference_lift_max = candidate.MaxFootHeightReferenceLift,
            candidate_first_recorded_recorder_frame = hasCandidateFirstFrame ? candidateFirstFrame.RecorderFrame : -1,
            candidate_first_recorded_root_y = hasCandidateFirstFrame ? candidateFirstFrame.RootY : float.NaN,
            candidate_first_recorded_body_position_y = hasCandidateFirstFrame ? candidateFirstFrame.BodyPositionY : float.NaN,
            candidate_first_recorded_hips_local_y = hasCandidateFirstFrame ? candidateFirstFrame.HipsLocalY : float.NaN,
            candidate_first_recorded_hips_y = hasCandidateFirstFrame ? candidateFirstFrame.HipsY : float.NaN,
            candidate_recording_start_root_y = hasCandidateFirstFrame ? candidateFirstFrame.RecordingStartRootY : float.NaN,
            candidate_recording_start_body_position_y = hasCandidateFirstFrame ? candidateFirstFrame.RecordingStartBodyPositionY : float.NaN,
            candidate_recording_start_hips_local_y = hasCandidateFirstFrame ? candidateFirstFrame.RecordingStartHipsLocalY : float.NaN,
            candidate_recording_start_hips_y = hasCandidateFirstFrame ? candidateFirstFrame.RecordingStartHipsY : float.NaN,
            candidate_recording_start_hips_reference_before_local_y = hasCandidateFirstFrame ? candidateFirstFrame.RecordingStartHipsReferenceBeforeLocalY : float.NaN,
            candidate_recording_start_hips_reference_after_local_y = hasCandidateFirstFrame ? candidateFirstFrame.RecordingStartHipsReferenceAfterLocalY : float.NaN,
            candidate_recording_start_hips_reference_delta_y = hasCandidateFirstFrame ? candidateFirstFrame.RecordingStartHipsReferenceDeltaY : float.NaN,
            candidate_recording_start_hips_reference_flip_detected = hasCandidateFirstFrame ? candidateFirstFrame.RecordingStartHipsReferenceFlipDetected : -1,
            candidate_recording_start_hips_reference_stage = hasCandidateFirstFrame ? candidateFirstFrame.RecordingStartHipsReferenceStage : "",
            same_frame_hips_y_contribution_basis = "offset-normalized same-recorderFrame deltas after subtracting the first matched sample offset",
            max_candidate_root_step = candidate.MaxRootStep,
            candidate_retarget_root_delta_max = candidate.MaxRetargetRootDelta,
            candidate_retarget_pose_delta_max = candidate.MaxRetargetPoseDelta,
            candidate_grounding_vertical_step_max = candidate.MaxGroundingVerticalStep,
            max_candidate_vmd_center_step = vmd.MaxCenterStep,
            max_candidate_vmd_foot_ik_step = vmd.MaxFootIkStep,
            min_candidate_vmd_foot_ik_y = vmd.MinFootIkY,
            min_candidate_vmd_effective_foot_ik_y = vmd.MinEffectiveFootIkY
        };

        Dictionary<int, VerticalSolveFrameCorrection> verticalSolveCorrections =
            evaluateVerticalSolvePostprocess ? new Dictionary<int, VerticalSolveFrameCorrection>() : null;
        CompareSameRecorderFrames(baseline, candidate, summary, verticalSolveCorrections);
        ApplyFrameQualityStatus(summary);
        if (evaluateVerticalSolvePostprocess)
        {
            ApplyVerticalSolvePostprocessSummary(
                summary,
                baselineLabel,
                baselineMetricsCsvPath,
                baseline,
                candidateLabel,
                candidateMetricsCsvPath,
                candidateVmdPath,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                targetFrameCount,
                verticalSolveCorrections);
        }

        return summary;
    }

    internal static bool TryBuildVerticalSolvePostprocessFrameQualitySummary(
        MotionComparisonFrameQualitySummary rawSummary,
        out MotionComparisonFrameQualitySummary postprocessedSummary)
    {
        postprocessedSummary = null;
        if (rawSummary == null ||
            string.IsNullOrWhiteSpace(rawSummary.baseline_metrics_csv) ||
            string.IsNullOrWhiteSpace(rawSummary.vertical_solve_postprocess_metrics_csv) ||
            !File.Exists(rawSummary.vertical_solve_postprocess_metrics_csv))
        {
            return false;
        }

        postprocessedSummary = BuildFrameQualitySummary(
            rawSummary.baseline_label,
            rawSummary.baseline_metrics_csv,
            BuildVerticalSolvePostprocessCandidateLabel(rawSummary.candidate_label),
            rawSummary.vertical_solve_postprocess_metrics_csv,
            rawSummary.candidate_vmd_path,
            rawSummary.baseline_recorded_frame_count,
            rawSummary.candidate_recorded_frame_count,
            rawSummary.target_frame_count,
            evaluateVerticalSolvePostprocess: false);
        postprocessedSummary.frame_quality_evaluation_role = "vertical_solve_postprocess_metrics";
        postprocessedSummary.frame_quality_evaluation_basis =
            "same frame_quality evaluator over the vertical_solve_postprocess metrics CSV; raw candidate summary remains separate";
        return true;
    }

    internal static bool TryBuildVerticalSolveCorrectedCandidateFrameQualitySummary(
        MotionComparisonFrameQualitySummary rawSummary,
        out MotionComparisonFrameQualitySummary correctedSummary)
    {
        correctedSummary = null;
        if (rawSummary == null ||
            string.IsNullOrWhiteSpace(rawSummary.baseline_metrics_csv) ||
            string.IsNullOrWhiteSpace(rawSummary.vertical_solve_corrected_candidate_metrics_csv) ||
            string.IsNullOrWhiteSpace(rawSummary.vertical_solve_corrected_candidate_vmd_path) ||
            !File.Exists(rawSummary.vertical_solve_corrected_candidate_metrics_csv) ||
            !File.Exists(rawSummary.vertical_solve_corrected_candidate_vmd_path))
        {
            return false;
        }

        correctedSummary = BuildFrameQualitySummary(
            rawSummary.baseline_label,
            rawSummary.baseline_metrics_csv,
            BuildVerticalSolveCorrectedCandidateLabel(rawSummary.candidate_label),
            rawSummary.vertical_solve_corrected_candidate_metrics_csv,
            rawSummary.vertical_solve_corrected_candidate_vmd_path,
            rawSummary.baseline_recorded_frame_count,
            rawSummary.candidate_recorded_frame_count,
            rawSummary.target_frame_count,
            evaluateVerticalSolvePostprocess: false);
        correctedSummary.frame_quality_evaluation_role = "corrected_candidate_metrics";
        correctedSummary.frame_quality_evaluation_basis =
            "same raw frame_quality evaluator over the explicit corrected candidate metrics/VMD artifact; bounded vertical and horizontal foot carrier corrections remain separate from the raw candidate summary";
        return true;
    }

    public static bool TryPromoteVerticalSolveCorrectedCandidateToPrimaryExport(
        MotionComparisonFrameQualitySummary rawSummary,
        out VerticalSolvePrimaryExportPromotion promotion)
    {
        promotion = null;
        if (rawSummary == null ||
            !string.Equals(rawSummary.vertical_solve_corrected_candidate_status, "pass", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(rawSummary.candidate_metrics_csv) ||
            string.IsNullOrWhiteSpace(rawSummary.candidate_vmd_path) ||
            string.IsNullOrWhiteSpace(rawSummary.vertical_solve_corrected_candidate_metrics_csv) ||
            string.IsNullOrWhiteSpace(rawSummary.vertical_solve_corrected_candidate_vmd_path) ||
            !File.Exists(rawSummary.candidate_metrics_csv) ||
            !File.Exists(rawSummary.candidate_vmd_path) ||
            !File.Exists(rawSummary.vertical_solve_corrected_candidate_metrics_csv) ||
            !File.Exists(rawSummary.vertical_solve_corrected_candidate_vmd_path))
        {
            return false;
        }

        string diagnosticMetricsPath = BuildVerticalSolveRawDiagnosticPath(rawSummary.candidate_metrics_csv);
        string diagnosticVmdPath = BuildVerticalSolveRawDiagnosticPath(rawSummary.candidate_vmd_path);
        string integratedManifestPath = BuildVerticalSolveIntegratedManifestPath(rawSummary.candidate_metrics_csv);
        try
        {
            if (IsCurrentVerticalSolvePrimaryExportPromotion(
                    rawSummary,
                    diagnosticMetricsPath,
                    diagnosticVmdPath,
                    integratedManifestPath))
            {
                FileInfo currentPromotedVmd = new FileInfo(rawSummary.candidate_vmd_path);
                promotion = new VerticalSolvePrimaryExportPromotion
                {
                    raw_metrics_csv = rawSummary.candidate_metrics_csv,
                    raw_vmd_path = rawSummary.candidate_vmd_path,
                    raw_diagnostic_metrics_csv = diagnosticMetricsPath,
                    raw_diagnostic_vmd_path = diagnosticVmdPath,
                    corrected_metrics_csv = rawSummary.vertical_solve_corrected_candidate_metrics_csv,
                    corrected_vmd_path = rawSummary.vertical_solve_corrected_candidate_vmd_path,
                    integrated_manifest_path = integratedManifestPath,
                    promoted_vmd_bytes = currentPromotedVmd.Exists ? currentPromotedVmd.Length : 0L
                };
                return promotion.promoted_vmd_bytes > 0L;
            }

            if (!FilesDiffer(rawSummary.candidate_metrics_csv, rawSummary.vertical_solve_corrected_candidate_metrics_csv) ||
                !FilesDiffer(rawSummary.candidate_vmd_path, rawSummary.vertical_solve_corrected_candidate_vmd_path))
            {
                return false;
            }

            EnsureParentDirectoryExists(diagnosticMetricsPath);
            EnsureParentDirectoryExists(diagnosticVmdPath);
            File.Copy(rawSummary.candidate_metrics_csv, diagnosticMetricsPath, overwrite: true);
            File.Copy(rawSummary.candidate_vmd_path, diagnosticVmdPath, overwrite: true);
            File.Copy(rawSummary.vertical_solve_corrected_candidate_metrics_csv, rawSummary.candidate_metrics_csv, overwrite: true);
            File.Copy(rawSummary.vertical_solve_corrected_candidate_vmd_path, rawSummary.candidate_vmd_path, overwrite: true);

            FileInfo promotedVmd = new FileInfo(rawSummary.candidate_vmd_path);
            promotion = new VerticalSolvePrimaryExportPromotion
            {
                raw_metrics_csv = rawSummary.candidate_metrics_csv,
                raw_vmd_path = rawSummary.candidate_vmd_path,
                raw_diagnostic_metrics_csv = diagnosticMetricsPath,
                raw_diagnostic_vmd_path = diagnosticVmdPath,
                corrected_metrics_csv = rawSummary.vertical_solve_corrected_candidate_metrics_csv,
                corrected_vmd_path = rawSummary.vertical_solve_corrected_candidate_vmd_path,
                integrated_manifest_path = integratedManifestPath,
                promoted_vmd_bytes = promotedVmd.Exists ? promotedVmd.Length : 0L
            };
            WriteVerticalSolveIntegratedPrimaryExportManifest(promotion, rawSummary);
            return promotion.promoted_vmd_bytes > 0L &&
                FilesDiffer(rawSummary.candidate_metrics_csv, diagnosticMetricsPath) &&
                FilesDiffer(rawSummary.candidate_vmd_path, diagnosticVmdPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MotionComparisonProbeReportWriter] Vertical solve primary export promotion failed: {ex.Message}");
            promotion = null;
            return false;
        }
    }

    private static bool IsCurrentVerticalSolvePrimaryExportPromotion(
        MotionComparisonFrameQualitySummary summary,
        string diagnosticMetricsPath,
        string diagnosticVmdPath,
        string integratedManifestPath)
    {
        if (summary == null ||
            string.IsNullOrWhiteSpace(diagnosticMetricsPath) ||
            string.IsNullOrWhiteSpace(diagnosticVmdPath) ||
            string.IsNullOrWhiteSpace(integratedManifestPath) ||
            !File.Exists(summary.candidate_metrics_csv) ||
            !File.Exists(summary.candidate_vmd_path) ||
            !File.Exists(diagnosticMetricsPath) ||
            !File.Exists(diagnosticVmdPath) ||
            !File.Exists(integratedManifestPath))
        {
            return false;
        }

        DateTime manifestWriteTimeUtc = File.GetLastWriteTimeUtc(integratedManifestPath);
        DateTime promotedMetricsWriteTimeUtc = File.GetLastWriteTimeUtc(summary.candidate_metrics_csv);
        DateTime promotedVmdWriteTimeUtc = File.GetLastWriteTimeUtc(summary.candidate_vmd_path);
        if (manifestWriteTimeUtc < promotedMetricsWriteTimeUtc ||
            manifestWriteTimeUtc < promotedVmdWriteTimeUtc)
        {
            return false;
        }

        return FilesDiffer(summary.candidate_metrics_csv, diagnosticMetricsPath) &&
            FilesDiffer(summary.candidate_vmd_path, diagnosticVmdPath);
    }

    public static MotionComparisonFrameQualitySummary[] BuildFrameQualityEvaluationEntries(
        MotionComparisonFrameQualitySummary rawSummary)
    {
        if (rawSummary == null)
        {
            return Array.Empty<MotionComparisonFrameQualitySummary>();
        }

        if (IsIntegratedVerticalSolvePrimaryRole(rawSummary.frame_quality_evaluation_role))
        {
            return new[] { rawSummary };
        }

        if (!TryBuildVerticalSolveCorrectedCandidateFrameQualitySummary(
                rawSummary,
                out MotionComparisonFrameQualitySummary correctedSummary))
        {
            return new[] { rawSummary };
        }

        ApplyCorrectedFloorContactDiagnosticStatus(rawSummary, correctedSummary);

        rawSummary.frame_quality_evaluation_role = "evaluation_candidate_metrics";
        rawSummary.frame_quality_evaluation_basis =
            "primary frame_quality evaluator over the unmodified candidate metrics CSV; corrected candidate artifacts remain separate evidence";
        return new[] { rawSummary, correctedSummary };
    }

    private static void ApplyCorrectedFloorContactDiagnosticStatus(
        MotionComparisonFrameQualitySummary rawSummary,
        MotionComparisonFrameQualitySummary correctedSummary)
    {
        if (rawSummary == null || correctedSummary == null)
        {
            return;
        }

        if (!string.Equals(rawSummary.floor_contact_gate_status, "fail", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(correctedSummary.floor_contact_gate_status, "pass", StringComparison.OrdinalIgnoreCase))
        {
            rawSummary.floor_contact_corrected_diagnostic_status = "diagnostic_only_effective_floor_safe";
            rawSummary.floor_contact_corrected_diagnostic_status_reason =
                "corrected candidate artifact is separate evidence; raw floor/contact gate remains a hard fail until generated output is rerun";
            return;
        }

        if (string.Equals(correctedSummary.floor_contact_gate_status, "fail", StringComparison.OrdinalIgnoreCase))
        {
            rawSummary.floor_contact_corrected_diagnostic_status = "diagnostic_only_effective_floor_unsafe";
            rawSummary.floor_contact_corrected_diagnostic_status_reason =
                "corrected candidate artifact still reports below-floor contact evidence; raw floor/contact gate remains a hard fail";
        }
    }

    private static bool IsIntegratedVerticalSolvePrimaryRole(string role)
    {
        return string.Equals(role, "main_auto_integrated_vertical_solve_metrics", StringComparison.Ordinal) ||
            string.Equals(role, "vmd_replay_integrated_vertical_solve_metrics", StringComparison.Ordinal);
    }

    private static string BuildVerticalSolvePostprocessCandidateLabel(string candidateLabel)
    {
        string label = string.IsNullOrWhiteSpace(candidateLabel) ? "candidate" : candidateLabel.Trim();
        return $"{label} vertical_solve_postprocess";
    }

    private static string BuildVerticalSolveCorrectedCandidateLabel(string candidateLabel)
    {
        string label = string.IsNullOrWhiteSpace(candidateLabel) ? "candidate" : candidateLabel.Trim();
        return $"{label} corrected_vertical_solve_candidate";
    }

    public static void AttachLatestMmdAutomationEvidence(
        MotionComparisonFrameQualitySummary summary,
        string projectRoot,
        string automationRunsRoot)
    {
        if (summary == null ||
            string.IsNullOrWhiteSpace(summary.candidate_vmd_path) ||
            string.IsNullOrWhiteSpace(projectRoot) ||
            string.IsNullOrWhiteSpace(automationRunsRoot) ||
            !Directory.Exists(automationRunsRoot))
        {
            return;
        }

        string candidateVmdPath = NormalizeComparablePath(summary.candidate_vmd_path, projectRoot);
        if (string.IsNullOrEmpty(candidateVmdPath))
        {
            return;
        }

        bool hasCandidateVmdWriteTime = File.Exists(candidateVmdPath);
        DateTime candidateVmdWriteTimeUtc = hasCandidateVmdWriteTime
            ? File.GetLastWriteTimeUtc(candidateVmdPath)
            : DateTime.MinValue;
        string bestReportPath = "";
        MmdAutomationReportForSummary bestReport = null;
        DateTime bestWriteTimeUtc = DateTime.MinValue;
        foreach (string reportPath in Directory.GetFiles(automationRunsRoot, "report.json", SearchOption.AllDirectories))
        {
            if (!TryReadMatchingMmdAutomationReport(reportPath, candidateVmdPath, projectRoot, out MmdAutomationReportForSummary report))
            {
                continue;
            }

            DateTime writeTimeUtc = File.GetLastWriteTimeUtc(reportPath);
            if (hasCandidateVmdWriteTime && writeTimeUtc < candidateVmdWriteTimeUtc)
            {
                continue;
            }

            if (bestReport == null || writeTimeUtc >= bestWriteTimeUtc)
            {
                bestReport = report;
                bestReportPath = reportPath;
                bestWriteTimeUtc = writeTimeUtc;
            }
        }

        if (bestReport == null)
        {
            return;
        }

        string reportDirectory = string.IsNullOrWhiteSpace(bestReportPath) ? "" : Path.GetDirectoryName(bestReportPath);
        string runDir = bestReport.artifacts != null && !string.IsNullOrWhiteSpace(bestReport.artifacts.run_dir)
            ? MotionComparisonProbeOutputPaths.ResolveMmdReportDirectoryPath(
                bestReport.artifacts.run_dir,
                projectRoot,
                reportDirectory)
            : reportDirectory;
        string reportPathFromReport = bestReport.artifacts != null && !string.IsNullOrWhiteSpace(bestReport.artifacts.report_path)
            ? bestReport.artifacts.report_path
            : bestReportPath;

        summary.mmd_result_status = string.IsNullOrWhiteSpace(bestReport.status) ? "unknown" : bestReport.status;
        summary.mmd_report_path = MotionComparisonProbeOutputPaths.MakeProjectRootRelativePath(projectRoot, reportPathFromReport);
        summary.mmd_run_dir = MotionComparisonProbeOutputPaths.MakeProjectRootRelativePath(projectRoot, runDir);
        summary.mmd_after_play_screenshot_path = MotionComparisonProbeOutputPaths.MakeProjectRootRelativePath(
            projectRoot,
            FindMmdAfterPlayScreenshot(bestReport, bestReportPath, projectRoot));
        summary.mmd_finished_at = bestReport.finished_at ?? "";
    }

    private static bool TryReadMatchingMmdAutomationReport(
        string reportPath,
        string candidateVmdPath,
        string projectRoot,
        out MmdAutomationReportForSummary report)
    {
        report = null;
        if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
        {
            return false;
        }

        try
        {
            report = JsonUtility.FromJson<MmdAutomationReportForSummary>(File.ReadAllText(reportPath, Encoding.UTF8));
        }
        catch
        {
            report = null;
            return false;
        }

        string motionVmdPath = report != null && report.config != null ? report.config.motion_vmd : "";
        return !string.IsNullOrEmpty(motionVmdPath) &&
               string.Equals(
                   NormalizeComparablePath(motionVmdPath, projectRoot),
                   candidateVmdPath,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string FindMmdAfterPlayScreenshot(
        MmdAutomationReportForSummary report,
        string reportPath,
        string projectRoot)
    {
        string reportDirectory = string.IsNullOrWhiteSpace(reportPath) ? "" : Path.GetDirectoryName(reportPath);
        if (report != null && report.steps != null)
        {
            foreach (MmdAutomationStepForSummary step in report.steps)
            {
                if (step == null ||
                    !string.Equals(step.name, "play", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(step.play_state_screenshot))
                {
                    continue;
                }

                string playScreenshot = MotionComparisonProbeOutputPaths.ResolveMmdReportArtifactPath(
                    step.play_state_screenshot,
                    projectRoot,
                    reportDirectory);
                string modelScreenshot = MotionComparisonProbeOutputPaths.BuildMmdModelScreenshotPath(playScreenshot);
                if (File.Exists(modelScreenshot))
                {
                    return modelScreenshot;
                }

                if (File.Exists(playScreenshot))
                {
                    return playScreenshot;
                }
            }
        }

        string screenshotsDir = report != null && report.artifacts != null
            ? report.artifacts.screenshots_dir
            : "";
        if (string.IsNullOrWhiteSpace(screenshotsDir) && !string.IsNullOrWhiteSpace(reportPath))
        {
            screenshotsDir = string.IsNullOrEmpty(reportDirectory) ? "" : Path.Combine(reportDirectory, "screenshots");
        }
        else
        {
            screenshotsDir = MotionComparisonProbeOutputPaths.ResolveMmdReportArtifactPath(
                screenshotsDir,
                projectRoot,
                reportDirectory);
        }

        if (!string.IsNullOrWhiteSpace(screenshotsDir))
        {
            string modelScreenshot = MotionComparisonProbeOutputPaths.BuildMmdAfterPlayModelScreenshotPath(screenshotsDir);
            if (File.Exists(modelScreenshot))
            {
                return modelScreenshot;
            }

            string fullScreenshot = MotionComparisonProbeOutputPaths.BuildMmdAfterPlayFullScreenshotPath(screenshotsDir);
            if (File.Exists(fullScreenshot))
            {
                return fullScreenshot;
            }
        }

        return "";
    }

    private static string NormalizeComparablePath(string path, string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        string normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (!Path.IsPathRooted(normalized) && !string.IsNullOrWhiteSpace(projectRoot))
        {
            normalized = Path.Combine(projectRoot, normalized);
        }

        try
        {
            normalized = Path.GetFullPath(normalized);
        }
        catch
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }

        return normalized.Replace('\\', '/').TrimEnd('/');
    }

    private static void CompareSameRecorderFrames(
        MetricsCsvData baseline,
        MetricsCsvData candidate,
        MotionComparisonFrameQualitySummary summary,
        IDictionary<int, VerticalSolveFrameCorrection> verticalSolveCorrections = null)
    {
        List<int> baselineFrames = new List<int>(baseline.Frames.Keys);
        baselineFrames.Sort();
        HashSet<int> matchedCandidateFrames = new HashSet<int>();
        bool hasRootOffset = false;
        float rootOffsetX = 0f;
        float rootOffsetY = 0f;
        float rootOffsetZ = 0f;
        bool hasHipsXzOffset = false;
        float hipsXOffset = 0f;
        float hipsZOffset = 0f;
        bool hasLeftFootXzOffset = false;
        float leftFootXOffset = 0f;
        float leftFootZOffset = 0f;
        bool hasRightFootXzOffset = false;
        float rightFootXOffset = 0f;
        float rightFootZOffset = 0f;
        bool hasVerticalOffset = false;
        float hipsYOffset = 0f;
        float footBottomYOffset = 0f;
        bool hasRootYOffset = false;
        float rootYOffset = 0f;
        bool hasBodyPositionYOffset = false;
        float bodyPositionYOffset = 0f;
        bool hasHipsLocalYOffset = false;
        float hipsLocalYOffset = 0f;
        bool hasGroundingVerticalStepOffset = false;
        float groundingVerticalStepOffset = 0f;
        bool hasFootHeightReferenceLiftOffset = false;
        float footHeightReferenceLiftOffset = 0f;
        bool hasCandidateGroundingVerticalStepOffset = false;
        float candidateGroundingVerticalStepOffset = 0f;
        bool hasCandidateFootHeightReferenceLiftOffset = false;
        float candidateFootHeightReferenceLiftOffset = 0f;
        float maxRawHipsYDelta = float.NaN;
        float maxRawFootBottomYDelta = float.NaN;
        float maxOffsetNormalizedHipsYDelta = float.NaN;
        float maxOffsetNormalizedFootBottomYDelta = float.NaN;
        float maxPrototypeHipsYDelta = float.NaN;
        float maxPrototypeFootBottomYDelta = float.NaN;
        float maxPrototypeHipsCorrectionMagnitude = float.NaN;
        float maxPrototypeFootCorrectionMagnitude = float.NaN;
        float maxOffsetNormalizedHipsYRootYComponent = float.NaN;
        float maxOffsetNormalizedHipsYBodyPositionYComponent = float.NaN;
        float maxOffsetNormalizedHipsYHipsLocalYComponent = float.NaN;
        float maxOffsetNormalizedHipsYFootBottomYDeltaAtFrame = float.NaN;
        int maxRawHipsYDeltaRecorderFrame = -1;
        int maxRawHipsYDeltaCandidateRecorderFrame = -1;
        int maxOffsetNormalizedHipsYDeltaRecorderFrame = -1;
        int maxOffsetNormalizedHipsYDeltaCandidateRecorderFrame = -1;
        int maxOffsetNormalizedFootBottomYDeltaRecorderFrame = -1;
        int maxOffsetNormalizedFootBottomYDeltaCandidateRecorderFrame = -1;
        int maxPrototypeHipsCorrectionRecorderFrame = -1;
        int maxPrototypeHipsCorrectionCandidateRecorderFrame = -1;
        int maxPrototypeFootCorrectionRecorderFrame = -1;
        int maxPrototypeFootCorrectionCandidateRecorderFrame = -1;
        float maxPrototypeHipsCorrection = float.NaN;
        float maxPrototypeFootCorrection = float.NaN;
        int rawLimbPoseDeltaSaturatedFrameCount = 0;
        float maxRawLimbPoseDeltaExcessOverGuardNormalized = float.NaN;
        foreach (int frame in baselineFrames)
        {
            MetricsCsvFrame baselineFrame = baseline.Frames[frame];
            if (!TryGetComparisonCandidateFrame(
                    candidate,
                    baselineFrame,
                    matchedCandidateFrames,
                    out int candidateRecorderFrame,
                    out MetricsCsvFrame candidateFrame))
            {
                summary.missing_candidate_frames++;
                continue;
            }

            matchedCandidateFrames.Add(candidateRecorderFrame);
            summary.compared_frames++;
            float rootDeltaX = candidateFrame.RootX - baselineFrame.RootX;
            float rootDeltaY = candidateFrame.RootY - baselineFrame.RootY;
            float rootDeltaZ = candidateFrame.RootZ - baselineFrame.RootZ;
            if (!hasRootOffset && IsFinite(rootDeltaX) && IsFinite(rootDeltaY) && IsFinite(rootDeltaZ))
            {
                rootOffsetX = rootDeltaX;
                rootOffsetY = rootDeltaY;
                rootOffsetZ = rootDeltaZ;
                hasRootOffset = true;
            }

            summary.max_same_frame_root_position_delta = MaxFinite(
                summary.max_same_frame_root_position_delta,
                Distance(
                    0f,
                    0f,
                    0f,
                    rootDeltaX - rootOffsetX,
                    rootDeltaY - rootOffsetY,
                    rootDeltaZ - rootOffsetZ));
            float hipsDeltaX = candidateFrame.HipsX - baselineFrame.HipsX;
            float hipsDeltaZ = candidateFrame.HipsZ - baselineFrame.HipsZ;
            if (!hasHipsXzOffset && IsFinite(hipsDeltaX) && IsFinite(hipsDeltaZ))
            {
                hipsXOffset = hipsDeltaX;
                hipsZOffset = hipsDeltaZ;
                hasHipsXzOffset = true;
            }

            float normalizedHipsDeltaX = hasHipsXzOffset && IsFinite(hipsDeltaX)
                ? hipsDeltaX - hipsXOffset
                : float.NaN;
            float normalizedHipsDeltaZ = hasHipsXzOffset && IsFinite(hipsDeltaZ)
                ? hipsDeltaZ - hipsZOffset
                : float.NaN;
            float hipsXzDelta = hasHipsXzOffset
                ? Distance(0f, 0f, 0f, normalizedHipsDeltaX, 0f, normalizedHipsDeltaZ)
                : float.NaN;
            UpdateMaxHipsXzDelta(
                summary,
                hipsXzDelta,
                frame,
                candidateRecorderFrame);

            float leftFootDeltaX = candidateFrame.LeftFootX - baselineFrame.LeftFootX;
            float leftFootDeltaZ = candidateFrame.LeftFootZ - baselineFrame.LeftFootZ;
            if (!hasLeftFootXzOffset && IsFinite(leftFootDeltaX) && IsFinite(leftFootDeltaZ))
            {
                leftFootXOffset = leftFootDeltaX;
                leftFootZOffset = leftFootDeltaZ;
                hasLeftFootXzOffset = true;
            }

            float leftFootXzDelta = hasLeftFootXzOffset
                ? Distance(0f, 0f, 0f, leftFootDeltaX - leftFootXOffset, 0f, leftFootDeltaZ - leftFootZOffset)
                : float.NaN;
            float leftFootPostprocessCorrectionX = 0f;
            float leftFootPostprocessCorrectionZ = 0f;
            float leftFootPostprocessInputX = float.NaN;
            float leftFootPostprocessInputZ = float.NaN;
            if (IsFinite(leftFootXzDelta))
            {
                float normalizedLeftFootDeltaX = leftFootDeltaX - leftFootXOffset;
                float normalizedLeftFootDeltaZ = leftFootDeltaZ - leftFootZOffset;
                if (TryResolveHorizontalFootPostprocessCorrection(
                    summary,
                    normalizedLeftFootDeltaX,
                    normalizedLeftFootDeltaZ,
                    candidateRecorderFrame,
                    out leftFootPostprocessCorrectionX,
                    out leftFootPostprocessCorrectionZ))
                {
                    leftFootPostprocessInputX = normalizedLeftFootDeltaX;
                    leftFootPostprocessInputZ = normalizedLeftFootDeltaZ;
                }

                summary.max_same_frame_left_foot_xz_delta =
                    MaxFinite(summary.max_same_frame_left_foot_xz_delta, leftFootXzDelta);
                UpdateMaxFootXzDelta(
                    summary,
                    "left",
                    leftFootXzDelta,
                    frame,
                    candidateRecorderFrame);
                UpdateMaxHipsAlignedFootXzDelta(
                    summary,
                    "left",
                    leftFootDeltaX - leftFootXOffset,
                    leftFootDeltaZ - leftFootZOffset,
                    normalizedHipsDeltaX,
                    normalizedHipsDeltaZ,
                    frame,
                    candidateRecorderFrame);
            }

            float rightFootDeltaX = candidateFrame.RightFootX - baselineFrame.RightFootX;
            float rightFootDeltaZ = candidateFrame.RightFootZ - baselineFrame.RightFootZ;
            if (!hasRightFootXzOffset && IsFinite(rightFootDeltaX) && IsFinite(rightFootDeltaZ))
            {
                rightFootXOffset = rightFootDeltaX;
                rightFootZOffset = rightFootDeltaZ;
                hasRightFootXzOffset = true;
            }

            float rightFootXzDelta = hasRightFootXzOffset
                ? Distance(0f, 0f, 0f, rightFootDeltaX - rightFootXOffset, 0f, rightFootDeltaZ - rightFootZOffset)
                : float.NaN;
            float rightFootPostprocessCorrectionX = 0f;
            float rightFootPostprocessCorrectionZ = 0f;
            float rightFootPostprocessInputX = float.NaN;
            float rightFootPostprocessInputZ = float.NaN;
            if (IsFinite(rightFootXzDelta))
            {
                float normalizedRightFootDeltaX = rightFootDeltaX - rightFootXOffset;
                float normalizedRightFootDeltaZ = rightFootDeltaZ - rightFootZOffset;
                if (TryResolveHorizontalFootPostprocessCorrection(
                    summary,
                    normalizedRightFootDeltaX,
                    normalizedRightFootDeltaZ,
                    candidateRecorderFrame,
                    out rightFootPostprocessCorrectionX,
                    out rightFootPostprocessCorrectionZ))
                {
                    rightFootPostprocessInputX = normalizedRightFootDeltaX;
                    rightFootPostprocessInputZ = normalizedRightFootDeltaZ;
                }

                summary.max_same_frame_right_foot_xz_delta =
                    MaxFinite(summary.max_same_frame_right_foot_xz_delta, rightFootXzDelta);
                UpdateMaxFootXzDelta(
                    summary,
                    "right",
                    rightFootXzDelta,
                    frame,
                    candidateRecorderFrame);
                UpdateMaxHipsAlignedFootXzDelta(
                    summary,
                    "right",
                    rightFootDeltaX - rightFootXOffset,
                    rightFootDeltaZ - rightFootZOffset,
                    normalizedHipsDeltaX,
                    normalizedHipsDeltaZ,
                    frame,
                    candidateRecorderFrame);
            }

            float armPoseDelta = CalculateArmMotionSignal(baselineFrame, candidateFrame);
            float guardNormalizedArmPoseDelta = CalculateGuardNormalizedArmMotionSignal(baselineFrame, candidateFrame);
            float legPoseDelta = CalculateLegMotionSignal(baselineFrame, candidateFrame);
            float limbPoseDelta = MaxFinite(armPoseDelta, legPoseDelta);
            float guardNormalizedLimbPoseDelta = MaxFinite(guardNormalizedArmPoseDelta, legPoseDelta);
            if (IsPreRetargetStartComparisonSample(baselineFrame, candidateFrame))
            {
                summary.pre_retarget_start_compared_frames++;
                summary.pre_retarget_start_max_same_frame_arm_pose_delta = MaxFinite(
                    summary.pre_retarget_start_max_same_frame_arm_pose_delta,
                    armPoseDelta);
                summary.pre_retarget_start_max_same_frame_guard_normalized_arm_pose_delta = MaxFinite(
                    summary.pre_retarget_start_max_same_frame_guard_normalized_arm_pose_delta,
                    guardNormalizedArmPoseDelta);
                summary.pre_retarget_start_max_same_frame_guard_normalized_limb_pose_delta = MaxFinite(
                    summary.pre_retarget_start_max_same_frame_guard_normalized_limb_pose_delta,
                    guardNormalizedLimbPoseDelta);
                UpdateMaxFiniteWithFrame(
                    limbPoseDelta,
                    frame,
                    candidateRecorderFrame,
                    ref summary.pre_retarget_start_max_same_frame_limb_pose_delta,
                    ref summary.pre_retarget_start_max_same_frame_limb_pose_delta_recorder_frame,
                    ref summary.pre_retarget_start_max_same_frame_limb_pose_delta_candidate_recorder_frame);
            }
            else
            {
                summary.max_same_frame_arm_pose_delta = MaxFinite(summary.max_same_frame_arm_pose_delta, armPoseDelta);
                summary.max_same_frame_leg_pose_delta = MaxFinite(summary.max_same_frame_leg_pose_delta, legPoseDelta);
                UpdateMaxFiniteWithFrameAndSource(
                    limbPoseDelta,
                    frame,
                    candidateRecorderFrame,
                    ResolveLimbPoseDeltaSource(armPoseDelta, legPoseDelta, "arm", "leg"),
                    ref summary.max_same_frame_limb_pose_delta,
                    ref summary.max_same_frame_limb_pose_delta_recorder_frame,
                    ref summary.max_same_frame_limb_pose_delta_candidate_recorder_frame,
                    ref summary.max_same_frame_limb_pose_delta_source);
                summary.max_same_frame_guard_normalized_arm_pose_delta = MaxFinite(
                    summary.max_same_frame_guard_normalized_arm_pose_delta,
                    guardNormalizedArmPoseDelta);
                summary.max_same_frame_guard_normalized_limb_pose_delta = MaxFinite(
                    summary.max_same_frame_guard_normalized_limb_pose_delta,
                    guardNormalizedLimbPoseDelta);
                string gateDeltaSource = ResolveLimbPoseDeltaSource(
                    guardNormalizedArmPoseDelta,
                    legPoseDelta,
                    "guard-normalized-arm",
                    "leg");
                UpdateMaxFiniteWithFrameAndSource(
                    guardNormalizedLimbPoseDelta,
                    frame,
                    candidateRecorderFrame,
                    gateDeltaSource,
                    ref summary.max_same_frame_limb_pose_gate_delta,
                    ref summary.max_same_frame_limb_pose_gate_delta_recorder_frame,
                    ref summary.max_same_frame_limb_pose_gate_delta_candidate_recorder_frame,
                    ref summary.max_same_frame_limb_pose_gate_delta_source);
                if (IsWithinCandidateVmdFrameRange(summary, candidateRecorderFrame))
                {
                    UpdateMaxFiniteWithFrameAndSource(
                        guardNormalizedLimbPoseDelta,
                        frame,
                        candidateRecorderFrame,
                        gateDeltaSource,
                        ref summary.max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range,
                        ref summary.max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range_recorder_frame,
                        ref summary.max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range_candidate_recorder_frame,
                        ref summary.max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range_source);
                }
                if (ExceedsThreshold(limbPoseDelta, QualitySameFrameLimbPoseDeltaFailThreshold) &&
                    !ExceedsThreshold(guardNormalizedLimbPoseDelta, QualitySameFrameLimbPoseDeltaFailThreshold))
                {
                    rawLimbPoseDeltaSaturatedFrameCount++;
                    maxRawLimbPoseDeltaExcessOverGuardNormalized = MaxFinite(
                        maxRawLimbPoseDeltaExcessOverGuardNormalized,
                        limbPoseDelta - guardNormalizedLimbPoseDelta);
                }
            }

            UpdateOffsetNormalizedDelta(
                baselineFrame.RootY,
                candidateFrame.RootY,
                ref hasRootYOffset,
                ref rootYOffset,
                ref summary.max_same_frame_root_y_delta);
            UpdateOffsetNormalizedDelta(
                baselineFrame.BodyPositionY,
                candidateFrame.BodyPositionY,
                ref hasBodyPositionYOffset,
                ref bodyPositionYOffset,
                ref summary.max_same_frame_body_position_y_delta);
            UpdateOffsetNormalizedDelta(
                baselineFrame.HipsLocalY,
                candidateFrame.HipsLocalY,
                ref hasHipsLocalYOffset,
                ref hipsLocalYOffset,
                ref summary.max_same_frame_hips_local_y_delta);
            UpdateOffsetNormalizedDelta(
                baselineFrame.GroundingVerticalStepLast,
                candidateFrame.GroundingVerticalStepLast,
                ref hasGroundingVerticalStepOffset,
                ref groundingVerticalStepOffset,
                ref summary.max_same_frame_grounding_vertical_step_delta);
            UpdateOffsetNormalizedDelta(
                baselineFrame.FootHeightReferenceLift,
                candidateFrame.FootHeightReferenceLift,
                ref hasFootHeightReferenceLiftOffset,
                ref footHeightReferenceLiftOffset,
                ref summary.max_same_frame_foot_height_reference_lift_delta);
            UpdateSingleOffsetNormalizedDelta(
                candidateFrame.GroundingVerticalStepLast,
                ref hasCandidateGroundingVerticalStepOffset,
                ref candidateGroundingVerticalStepOffset,
                ref summary.max_same_frame_candidate_grounding_vertical_step_change);
            UpdateSingleOffsetNormalizedDelta(
                candidateFrame.FootHeightReferenceLift,
                ref hasCandidateFootHeightReferenceLiftOffset,
                ref candidateFootHeightReferenceLiftOffset,
                ref summary.max_same_frame_candidate_foot_height_reference_lift_change);

            float hipsYDelta = candidateFrame.HipsY - baselineFrame.HipsY;
            float footBottomYDelta = candidateFrame.LowestFootBottomY - baselineFrame.LowestFootBottomY;
            UpdateMaxFiniteWithFrame(
                Math.Abs(hipsYDelta),
                frame,
                candidateRecorderFrame,
                ref maxRawHipsYDelta,
                ref maxRawHipsYDeltaRecorderFrame,
                ref maxRawHipsYDeltaCandidateRecorderFrame);
            maxRawFootBottomYDelta = MaxFinite(maxRawFootBottomYDelta, Math.Abs(footBottomYDelta));
            if (!hasVerticalOffset && IsFinite(hipsYDelta) && IsFinite(footBottomYDelta))
            {
                hipsYOffset = hipsYDelta;
                footBottomYOffset = footBottomYDelta;
                hasVerticalOffset = true;
            }

            float normalizedHipsYDelta = IsFinite(hipsYDelta) && hasVerticalOffset
                ? hipsYDelta - hipsYOffset
                : float.NaN;
            float normalizedFootBottomYDelta = IsFinite(footBottomYDelta) && hasVerticalOffset
                ? footBottomYDelta - footBottomYOffset
                : float.NaN;
            float prototypeHipsCorrection = ResolveBoundedVerticalSolveCorrection(
                normalizedHipsYDelta,
                QualitySameFrameHipsYWarnThreshold,
                VerticalSolvePrototypeMaxCorrectionY);
            float prototypeFootCorrection = ResolveBoundedVerticalSolveCorrection(
                normalizedFootBottomYDelta,
                QualitySameFrameFootBottomYWarnThreshold,
                VerticalSolvePrototypeMaxCorrectionY);
            prototypeFootCorrection = ClampFootVerticalSolveCorrectionToFloor(
                prototypeFootCorrection,
                candidateFrame,
                VerticalSolvePrototypeMaxCorrectionY);
            float prototypeHipsYDelta = IsFinite(normalizedHipsYDelta) && IsFinite(prototypeHipsCorrection)
                ? normalizedHipsYDelta + prototypeHipsCorrection
                : float.NaN;
            float prototypeFootBottomYDelta = IsFinite(normalizedFootBottomYDelta) && IsFinite(prototypeFootCorrection)
                ? normalizedFootBottomYDelta + prototypeFootCorrection
                : float.NaN;
            if (verticalSolveCorrections != null &&
                (HasNonZeroCorrection(leftFootPostprocessCorrectionX) ||
                 HasNonZeroCorrection(leftFootPostprocessCorrectionZ) ||
                 HasNonZeroCorrection(rightFootPostprocessCorrectionX) ||
                 HasNonZeroCorrection(rightFootPostprocessCorrectionZ) ||
                 (IsFinite(prototypeHipsCorrection) && IsFinite(prototypeFootCorrection))))
            {
                float postprocessHipsCorrection = 0f;
                float postprocessFootCorrection = 0f;
                if (IsFinite(prototypeHipsCorrection) && IsFinite(prototypeFootCorrection))
                {
                    postprocessHipsCorrection = ResolveBoundedVerticalSolveCorrection(
                        normalizedHipsYDelta,
                        Mathf.Max(0f, QualitySameFrameHipsYWarnThreshold - VerticalSolvePostprocessSafetyMarginY),
                        VerticalSolveArtifactMaxCorrectionY);
                    postprocessFootCorrection = ResolveBoundedVerticalSolveCorrection(
                        normalizedFootBottomYDelta,
                        Mathf.Max(0f, QualitySameFrameFootBottomYWarnThreshold - VerticalSolvePostprocessSafetyMarginY),
                        VerticalSolveArtifactMaxCorrectionY);
                    postprocessFootCorrection = ClampFootVerticalSolveCorrectionToFloor(
                        postprocessFootCorrection,
                        candidateFrame,
                        VerticalSolveArtifactMaxCorrectionY);
                }

                verticalSolveCorrections[candidateRecorderFrame] =
                    new VerticalSolveFrameCorrection(
                        postprocessHipsCorrection,
                        postprocessFootCorrection,
                        leftFootPostprocessCorrectionX,
                        leftFootPostprocessCorrectionZ,
                        rightFootPostprocessCorrectionX,
                        rightFootPostprocessCorrectionZ,
                        leftFootPostprocessInputX,
                        leftFootPostprocessInputZ,
                        rightFootPostprocessInputX,
                        rightFootPostprocessInputZ,
                        ResolveHorizontalFootPostprocessTargetMagnitude());
            }

            maxPrototypeHipsYDelta = MaxFinite(maxPrototypeHipsYDelta, Math.Abs(prototypeHipsYDelta));
            maxPrototypeFootBottomYDelta = MaxFinite(maxPrototypeFootBottomYDelta, Math.Abs(prototypeFootBottomYDelta));
            UpdatePrototypeCorrection(
                prototypeHipsCorrection,
                frame,
                candidateRecorderFrame,
                ref maxPrototypeHipsCorrectionMagnitude,
                ref maxPrototypeHipsCorrection,
                ref maxPrototypeHipsCorrectionRecorderFrame,
                ref maxPrototypeHipsCorrectionCandidateRecorderFrame);
            UpdatePrototypeCorrection(
                prototypeFootCorrection,
                frame,
                candidateRecorderFrame,
                ref maxPrototypeFootCorrectionMagnitude,
                ref maxPrototypeFootCorrection,
                ref maxPrototypeFootCorrectionRecorderFrame,
                ref maxPrototypeFootCorrectionCandidateRecorderFrame);
            float absNormalizedHipsYDelta = Math.Abs(normalizedHipsYDelta);
            if (IsFinite(absNormalizedHipsYDelta) &&
                (!IsFinite(maxOffsetNormalizedHipsYDelta) || absNormalizedHipsYDelta > maxOffsetNormalizedHipsYDelta))
            {
                maxOffsetNormalizedHipsYDelta = absNormalizedHipsYDelta;
                maxOffsetNormalizedHipsYDeltaRecorderFrame = frame;
                maxOffsetNormalizedHipsYDeltaCandidateRecorderFrame = candidateRecorderFrame;
                maxOffsetNormalizedHipsYRootYComponent = hasRootYOffset && IsFinite(rootDeltaY)
                    ? rootDeltaY - rootYOffset
                    : float.NaN;
                maxOffsetNormalizedHipsYBodyPositionYComponent = hasBodyPositionYOffset &&
                                                                  IsFinite(baselineFrame.BodyPositionY) &&
                                                                  IsFinite(candidateFrame.BodyPositionY)
                    ? candidateFrame.BodyPositionY - baselineFrame.BodyPositionY - bodyPositionYOffset
                    : float.NaN;
                maxOffsetNormalizedHipsYHipsLocalYComponent = hasHipsLocalYOffset &&
                                                              IsFinite(baselineFrame.HipsLocalY) &&
                                                              IsFinite(candidateFrame.HipsLocalY)
                    ? candidateFrame.HipsLocalY - baselineFrame.HipsLocalY - hipsLocalYOffset
                    : float.NaN;
                maxOffsetNormalizedHipsYFootBottomYDeltaAtFrame = normalizedFootBottomYDelta;
            }

            float absNormalizedFootBottomYDelta = Math.Abs(normalizedFootBottomYDelta);
            if (IsFinite(absNormalizedFootBottomYDelta) &&
                (!IsFinite(maxOffsetNormalizedFootBottomYDelta) || absNormalizedFootBottomYDelta > maxOffsetNormalizedFootBottomYDelta))
            {
                maxOffsetNormalizedFootBottomYDelta = absNormalizedFootBottomYDelta;
                maxOffsetNormalizedFootBottomYDeltaRecorderFrame = frame;
                maxOffsetNormalizedFootBottomYDeltaCandidateRecorderFrame = candidateRecorderFrame;
            }
        }

        bool canUseVerticalOffset = summary.compared_frames > 1 && hasVerticalOffset;
        summary.max_same_frame_hips_y_delta = canUseVerticalOffset
            ? maxOffsetNormalizedHipsYDelta
            : maxRawHipsYDelta;
        summary.max_same_frame_hips_y_delta_recorder_frame = canUseVerticalOffset
            ? maxOffsetNormalizedHipsYDeltaRecorderFrame
            : maxRawHipsYDeltaRecorderFrame;
        summary.max_same_frame_hips_y_delta_candidate_recorder_frame = canUseVerticalOffset
            ? maxOffsetNormalizedHipsYDeltaCandidateRecorderFrame
            : maxRawHipsYDeltaCandidateRecorderFrame;
        summary.max_same_frame_hips_y_delta_root_y_component = canUseVerticalOffset
            ? maxOffsetNormalizedHipsYRootYComponent
            : float.NaN;
        summary.max_same_frame_hips_y_delta_body_position_y_component = canUseVerticalOffset
            ? maxOffsetNormalizedHipsYBodyPositionYComponent
            : float.NaN;
        summary.max_same_frame_hips_y_delta_hips_local_y_component = canUseVerticalOffset
            ? maxOffsetNormalizedHipsYHipsLocalYComponent
            : float.NaN;
        summary.max_same_frame_hips_y_delta_foot_bottom_y_delta_at_frame = canUseVerticalOffset
            ? maxOffsetNormalizedHipsYFootBottomYDeltaAtFrame
            : float.NaN;
        summary.max_same_frame_foot_bottom_y_delta = canUseVerticalOffset
            ? maxOffsetNormalizedFootBottomYDelta
            : maxRawFootBottomYDelta;
        summary.max_same_frame_foot_bottom_y_delta_recorder_frame = canUseVerticalOffset
            ? maxOffsetNormalizedFootBottomYDeltaRecorderFrame
            : -1;
        summary.max_same_frame_foot_bottom_y_delta_candidate_recorder_frame = canUseVerticalOffset
            ? maxOffsetNormalizedFootBottomYDeltaCandidateRecorderFrame
            : -1;
        summary.vertical_solve_prototype_max_same_frame_hips_y_delta = canUseVerticalOffset
            ? maxPrototypeHipsYDelta
            : maxRawHipsYDelta;
        summary.vertical_solve_prototype_max_same_frame_foot_bottom_y_delta = canUseVerticalOffset
            ? maxPrototypeFootBottomYDelta
            : maxRawFootBottomYDelta;
        summary.vertical_solve_prototype_max_same_frame_root_position_delta =
            summary.max_same_frame_root_position_delta;
        summary.vertical_solve_prototype_below_floor_metric_frames = candidate.BelowFloorFrameCount;
        summary.vertical_solve_prototype_hips_correction_recorder_frame = canUseVerticalOffset
            ? maxPrototypeHipsCorrectionRecorderFrame
            : -1;
        summary.vertical_solve_prototype_hips_correction_candidate_recorder_frame = canUseVerticalOffset
            ? maxPrototypeHipsCorrectionCandidateRecorderFrame
            : -1;
        summary.vertical_solve_prototype_hips_correction_y = canUseVerticalOffset
            ? maxPrototypeHipsCorrection
            : float.NaN;
        summary.vertical_solve_prototype_foot_correction_recorder_frame = canUseVerticalOffset
            ? maxPrototypeFootCorrectionRecorderFrame
            : -1;
        summary.vertical_solve_prototype_foot_correction_candidate_recorder_frame = canUseVerticalOffset
            ? maxPrototypeFootCorrectionCandidateRecorderFrame
            : -1;
        summary.vertical_solve_prototype_foot_correction_y = canUseVerticalOffset
            ? maxPrototypeFootCorrection
            : float.NaN;
        summary.raw_limb_pose_delta_saturated_frame_count = rawLimbPoseDeltaSaturatedFrameCount;
        summary.raw_limb_pose_delta_excess_over_guard_normalized =
            maxRawLimbPoseDeltaExcessOverGuardNormalized;
        summary.raw_limb_pose_delta_saturation_basis = rawLimbPoseDeltaSaturatedFrameCount > 0
            ? "raw limb pose delta exceeded threshold while guard-normalized limb pose delta stayed within threshold; this remains a diagnostic for saturated humanoid muscle decomposition and is not promoted as the visible-safe limb naturalness gate"
            : "";

        foreach (int frame in candidate.Frames.Keys)
        {
            if (!matchedCandidateFrames.Contains(frame))
            {
                summary.missing_baseline_frames++;
            }
        }

        ApplyVerticalSolvePrototypeStatus(summary);
    }

    private static void ApplyVerticalSolvePostprocessSummary(
        MotionComparisonFrameQualitySummary summary,
        string baselineLabel,
        string baselineMetricsCsvPath,
        MetricsCsvData baseline,
        string candidateLabel,
        string candidateMetricsCsvPath,
        string candidateVmdPath,
        int baselineRecordedFrameCount,
        int candidateRecordedFrameCount,
        int targetFrameCount,
        IReadOnlyDictionary<int, VerticalSolveFrameCorrection> verticalSolveCorrections)
    {
        if (summary == null)
        {
            return;
        }

        string outputPath = summary.vertical_solve_corrected_candidate_metrics_csv;
        if (!TryWriteVerticalSolvePostprocessMetricsCsv(
                candidateMetricsCsvPath,
                outputPath,
                verticalSolveCorrections,
                out int correctedRows))
        {
            summary.vertical_solve_postprocess_status = "not_evaluated";
            summary.vertical_solve_postprocess_status_reason = "postprocess metrics csv was not written";
            summary.vertical_solve_corrected_candidate_status = "not_evaluated";
            summary.vertical_solve_corrected_candidate_status_reason = "corrected candidate metrics csv was not written";
            return;
        }

        CopyCorrectedMetricsToLegacyPostprocessPath(outputPath, summary.vertical_solve_postprocess_metrics_csv);
        bool hasCorrectedVmd = TryWriteVerticalSolveCorrectedCandidateVmdArtifact(
            candidateVmdPath,
            summary.vertical_solve_corrected_candidate_vmd_path,
            verticalSolveCorrections,
            out int correctedVmdFrameCount,
            out int safetyLimitedVmdFrameCount,
            out long correctedVmdBytes,
            out List<VerticalSolveVmdSafetyLimitDetail> correctedVmdSafetyLimitDetails);
        string correctedVmdPathForEvaluation = hasCorrectedVmd
            ? summary.vertical_solve_corrected_candidate_vmd_path
            : candidateVmdPath;
        WriteVerticalSolveCorrectedCandidateManifest(
            summary.vertical_solve_corrected_candidate_manifest_path,
            candidateMetricsCsvPath,
            candidateVmdPath,
            outputPath,
            hasCorrectedVmd ? summary.vertical_solve_corrected_candidate_vmd_path : "",
            correctedRows,
            correctedVmdFrameCount,
            safetyLimitedVmdFrameCount,
            correctedVmdBytes,
            correctedVmdSafetyLimitDetails);

        MotionComparisonFrameQualitySummary postprocessed = BuildFrameQualitySummary(
            baselineLabel,
            baselineMetricsCsvPath,
            candidateLabel,
            outputPath,
            correctedVmdPathForEvaluation,
            baselineRecordedFrameCount,
            candidateRecordedFrameCount,
            targetFrameCount,
            evaluateVerticalSolvePostprocess: false);
        summary.vertical_solve_postprocess_status = postprocessed.status;
        summary.vertical_solve_postprocess_status_reason = string.Equals(postprocessed.status, "pass", StringComparison.Ordinal)
            ? "postprocessed frame-specific vertical solve stayed within thresholds"
            : postprocessed.status_reason;
        summary.vertical_solve_postprocess_max_same_frame_hips_y_delta =
            postprocessed.max_same_frame_hips_y_delta;
        summary.vertical_solve_postprocess_max_same_frame_foot_bottom_y_delta =
            postprocessed.max_same_frame_foot_bottom_y_delta;
        summary.vertical_solve_postprocess_max_same_frame_root_position_delta =
            postprocessed.max_same_frame_root_position_delta;
        summary.vertical_solve_postprocess_below_floor_metric_frames =
            postprocessed.candidate_below_floor_metric_frames;
        summary.vertical_solve_postprocess_target_frame_count = postprocessed.target_frame_count;
        summary.vertical_solve_postprocess_candidate_recorded_frame_count =
            postprocessed.candidate_recorded_frame_count;
        summary.vertical_solve_postprocess_corrected_metric_frames = correctedRows;
        summary.vertical_solve_corrected_candidate_status = postprocessed.status;
        summary.vertical_solve_corrected_candidate_status_reason = string.Equals(postprocessed.status, "pass", StringComparison.Ordinal)
            ? "corrected candidate metrics artifact stayed within thresholds under the raw frame_quality evaluator"
            : postprocessed.status_reason;
        summary.vertical_solve_corrected_candidate_max_same_frame_hips_y_delta =
            postprocessed.max_same_frame_hips_y_delta;
        summary.vertical_solve_corrected_candidate_max_same_frame_foot_bottom_y_delta =
            postprocessed.max_same_frame_foot_bottom_y_delta;
        summary.vertical_solve_corrected_candidate_max_same_frame_root_position_delta =
            postprocessed.max_same_frame_root_position_delta;
        summary.vertical_solve_corrected_candidate_below_floor_metric_frames =
            postprocessed.candidate_below_floor_metric_frames;
        summary.vertical_solve_corrected_candidate_target_frame_count = postprocessed.target_frame_count;
        summary.vertical_solve_corrected_candidate_recorded_frame_count =
            postprocessed.candidate_recorded_frame_count;
        summary.vertical_solve_corrected_candidate_corrected_metric_frames = correctedRows;
    }

    private static void CopyCorrectedMetricsToLegacyPostprocessPath(string correctedPath, string legacyPath)
    {
        if (string.IsNullOrWhiteSpace(correctedPath) ||
            string.IsNullOrWhiteSpace(legacyPath) ||
            string.Equals(correctedPath, legacyPath, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(correctedPath))
        {
            return;
        }

        EnsureParentDirectoryExists(legacyPath);
        File.Copy(correctedPath, legacyPath, overwrite: true);
    }

    private static string BuildVerticalSolvePostprocessMetricsCsvPath(string candidateMetricsCsvPath)
    {
        if (string.IsNullOrWhiteSpace(candidateMetricsCsvPath))
        {
            return "";
        }

        string directory = Path.GetDirectoryName(candidateMetricsCsvPath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(candidateMetricsCsvPath);
        string extension = Path.GetExtension(candidateMetricsCsvPath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".csv";
        }

        string postprocessFileName = $"{fileNameWithoutExtension}.vertical_solve_postprocess{extension}";
        return string.IsNullOrEmpty(directory)
            ? postprocessFileName
            : Path.Combine(directory, postprocessFileName);
    }

    private static string BuildVerticalSolveCorrectedCandidateMetricsCsvPath(string candidateMetricsCsvPath)
    {
        if (string.IsNullOrWhiteSpace(candidateMetricsCsvPath))
        {
            return "";
        }

        string directory = Path.GetDirectoryName(candidateMetricsCsvPath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(candidateMetricsCsvPath);
        string extension = Path.GetExtension(candidateMetricsCsvPath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".csv";
        }

        string correctedFileName = $"{fileNameWithoutExtension}.corrected_vertical_solve_candidate{extension}";
        return string.IsNullOrEmpty(directory)
            ? correctedFileName
            : Path.Combine(directory, correctedFileName);
    }

    private static string BuildVerticalSolveCorrectedCandidateVmdPath(string candidateVmdPath)
    {
        if (string.IsNullOrWhiteSpace(candidateVmdPath))
        {
            return "";
        }

        string directory = Path.GetDirectoryName(candidateVmdPath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(candidateVmdPath);
        string extension = Path.GetExtension(candidateVmdPath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".vmd";
        }

        string correctedFileName = $"{fileNameWithoutExtension}.corrected_vertical_solve_candidate{extension}";
        return string.IsNullOrEmpty(directory)
            ? correctedFileName
            : Path.Combine(directory, correctedFileName);
    }

    private static string BuildVerticalSolveCorrectedCandidateManifestPath(string candidateMetricsCsvPath)
    {
        if (string.IsNullOrWhiteSpace(candidateMetricsCsvPath))
        {
            return "";
        }

        string directory = Path.GetDirectoryName(candidateMetricsCsvPath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(candidateMetricsCsvPath);
        string manifestFileName = $"{fileNameWithoutExtension}.corrected_vertical_solve_candidate.json";
        return string.IsNullOrEmpty(directory)
            ? manifestFileName
            : Path.Combine(directory, manifestFileName);
    }

    private static string BuildVerticalSolveRawDiagnosticPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        string directory = Path.GetDirectoryName(path);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".txt";
        }

        string diagnosticFileName = $"{fileNameWithoutExtension}.raw_vertical_solve_diagnostic{extension}";
        return string.IsNullOrEmpty(directory)
            ? diagnosticFileName
            : Path.Combine(directory, diagnosticFileName);
    }

    private static string BuildVerticalSolveIntegratedManifestPath(string candidateMetricsCsvPath)
    {
        if (string.IsNullOrWhiteSpace(candidateMetricsCsvPath))
        {
            return "";
        }

        string directory = Path.GetDirectoryName(candidateMetricsCsvPath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(candidateMetricsCsvPath);
        string manifestFileName = $"{fileNameWithoutExtension}.integrated_vertical_solve_primary_export.json";
        return string.IsNullOrEmpty(directory)
            ? manifestFileName
            : Path.Combine(directory, manifestFileName);
    }

    private static void WriteVerticalSolveIntegratedPrimaryExportManifest(
        VerticalSolvePrimaryExportPromotion promotion,
        MotionComparisonFrameQualitySummary rawSummary)
    {
        if (promotion == null || string.IsNullOrWhiteSpace(promotion.integrated_manifest_path))
        {
            return;
        }

        EnsureParentDirectoryExists(promotion.integrated_manifest_path);
        string json =
            "{\n" +
            "  \"artifact_role\": \"integrated_vertical_solve_primary_export\",\n" +
            "  \"generated_at\": \"" + DateTime.Now.ToString("o", CultureInfo.InvariantCulture) + "\",\n" +
            "  \"raw_metrics_csv\": \"" + JsonEscape(promotion.raw_metrics_csv) + "\",\n" +
            "  \"raw_vmd_path\": \"" + JsonEscape(promotion.raw_vmd_path) + "\",\n" +
            "  \"raw_diagnostic_metrics_csv\": \"" + JsonEscape(promotion.raw_diagnostic_metrics_csv) + "\",\n" +
            "  \"raw_diagnostic_vmd_path\": \"" + JsonEscape(promotion.raw_diagnostic_vmd_path) + "\",\n" +
            "  \"corrected_metrics_csv\": \"" + JsonEscape(promotion.corrected_metrics_csv) + "\",\n" +
            "  \"corrected_vmd_path\": \"" + JsonEscape(promotion.corrected_vmd_path) + "\",\n" +
            "  \"promoted_vmd_bytes\": " + promotion.promoted_vmd_bytes.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "  \"corrected_metric_rows\": " + rawSummary.vertical_solve_corrected_candidate_corrected_metric_frames.ToString(CultureInfo.InvariantCulture) + "\n" +
            "}\n";
        File.WriteAllText(promotion.integrated_manifest_path, json, Encoding.UTF8);
    }

    private static string JsonEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static bool FilesDiffer(string leftPath, string rightPath)
    {
        if (string.IsNullOrWhiteSpace(leftPath) ||
            string.IsNullOrWhiteSpace(rightPath) ||
            !File.Exists(leftPath) ||
            !File.Exists(rightPath))
        {
            return false;
        }

        try
        {
            if (string.Equals(
                    Path.GetFullPath(leftPath),
                    Path.GetFullPath(rightPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        catch
        {
            if (string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        FileInfo left = new FileInfo(leftPath);
        FileInfo right = new FileInfo(rightPath);
        if (left.Length != right.Length)
        {
            return true;
        }

        const int bufferSize = 81920;
        byte[] leftBuffer = new byte[bufferSize];
        byte[] rightBuffer = new byte[bufferSize];
        using (FileStream leftStream = File.OpenRead(leftPath))
        using (FileStream rightStream = File.OpenRead(rightPath))
        {
            while (true)
            {
                int leftRead = leftStream.Read(leftBuffer, 0, leftBuffer.Length);
                int rightRead = rightStream.Read(rightBuffer, 0, rightBuffer.Length);
                if (leftRead != rightRead)
                {
                    return true;
                }

                if (leftRead == 0)
                {
                    return false;
                }

                for (int i = 0; i < leftRead; i++)
                {
                    if (leftBuffer[i] != rightBuffer[i])
                    {
                        return true;
                    }
                }
            }
        }
    }

    private static bool TryWriteVerticalSolveCorrectedCandidateVmdArtifact(
        string sourceVmdPath,
        string outputVmdPath,
        IReadOnlyDictionary<int, VerticalSolveFrameCorrection> verticalSolveCorrections,
        out int correctedFrameCount,
        out int safetyLimitedFrameCount,
        out long fileSizeBytes,
        out List<VerticalSolveVmdSafetyLimitDetail> safetyLimitDetails)
    {
        correctedFrameCount = 0;
        safetyLimitedFrameCount = 0;
        fileSizeBytes = 0L;
        safetyLimitDetails = new List<VerticalSolveVmdSafetyLimitDetail>();
        if (string.IsNullOrWhiteSpace(sourceVmdPath) ||
            string.IsNullOrWhiteSpace(outputVmdPath) ||
            verticalSolveCorrections == null ||
            verticalSolveCorrections.Count == 0 ||
            !File.Exists(sourceVmdPath))
        {
            return false;
        }

        byte[] bytes = File.ReadAllBytes(sourceVmdPath);
        const int headerLength = 30 + 20;
        const int countLength = 4;
        const int boneFrameSize = 111;
        if (bytes.Length < headerLength + countLength)
        {
            return false;
        }

        uint boneFrameCount = BitConverter.ToUInt32(bytes, headerLength);
        int offset = headerLength + countLength;
        List<VmdRewriteFrame> rewriteFrames = new List<VmdRewriteFrame>();
        for (uint index = 0; index < boneFrameCount && offset + boneFrameSize <= bytes.Length; index++, offset += boneFrameSize)
        {
            string boneName = ReadPaddedShiftJis(bytes, offset, 15);
            uint frame = BitConverter.ToUInt32(bytes, offset + 15);
            bool isCenterCarrier = IsCenterCarrierBoneName(boneName);
            bool isFootIkCarrier = IsFootIkCarrierBoneName(boneName);
            bool isFootIkBone = IsFootIkBoneName(boneName);
            if (!isCenterCarrier && !isFootIkBone)
            {
                continue;
            }

            float x = BitConverter.ToSingle(bytes, offset + 19);
            float y = BitConverter.ToSingle(bytes, offset + 23);
            float z = BitConverter.ToSingle(bytes, offset + 27);
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
            {
                continue;
            }

            if (!TryGetFootIkSide(boneName, out string side))
            {
                TryGetToeIkSide(boneName, out side);
            }
            rewriteFrames.Add(new VmdRewriteFrame(
                offset,
                frame,
                boneName,
                side,
                x,
                y,
                z,
                isCenterCarrier,
                isFootIkCarrier));
        }

        Dictionary<uint, int> centerCarrierCountByFrame = CountCenterCarriersByFrame(rewriteFrames);
        Dictionary<uint, float> minEffectiveFootIkYByFrame = BuildMinEffectiveFootIkYByFrame(rewriteFrames);
        Dictionary<string, List<VmdRewriteFrame>> carrierFramesByBone = BuildCarrierFramesByBone(rewriteFrames);
        List<VmdIkStateFrame> ikStateFrames = ReadVmdIkStateFrames(bytes, offset);
        foreach (List<VmdRewriteFrame> frames in carrierFramesByBone.Values)
        {
            frames.Sort((left, right) => left.Frame.CompareTo(right.Frame));
            for (int i = 0; i < frames.Count; i++)
            {
                VmdRewriteFrame frame = frames[i];
                if (frame.Frame > int.MaxValue ||
                    !verticalSolveCorrections.TryGetValue((int)frame.Frame, out VerticalSolveFrameCorrection correction))
                {
                    continue;
                }

                float requestedDeltaY = ResolveRequestedCorrectedVmdDeltaY(
                    frame,
                    correction,
                    centerCarrierCountByFrame,
                    minEffectiveFootIkYByFrame);
                ResolveRequestedCorrectedVmdDeltaXZ(
                    frame,
                    correction,
                    out float requestedDeltaX,
                    out float requestedDeltaZ);
                if ((!IsFinite(requestedDeltaY) || Math.Abs(requestedDeltaY) <= 0f) &&
                    (!IsFinite(requestedDeltaX) || Math.Abs(requestedDeltaX) <= 0f) &&
                    (!IsFinite(requestedDeltaZ) || Math.Abs(requestedDeltaZ) <= 0f))
                {
                    continue;
                }

                requestedDeltaY = ClampCenterCarrierDeltaToFloor(
                    frame,
                    requestedDeltaY,
                    centerCarrierCountByFrame,
                    minEffectiveFootIkYByFrame);
                VmdRewriteFrame writeFrame = frame;
                int writeFrameIndex = i;
                if (TryResolveVisibleFootIkCarrierFrame(
                        frames,
                        i,
                        ikStateFrames,
                        requestedDeltaX,
                        requestedDeltaY,
                        requestedDeltaZ,
                        out int visibleFrameIndex))
                {
                    writeFrameIndex = visibleFrameIndex;
                    writeFrame = frames[writeFrameIndex];
                }

                if (writeFrameIndex == i &&
                    IsDisabledFootIkHorizontalCarrierFrame(frame, ikStateFrames, requestedDeltaX, requestedDeltaY, requestedDeltaZ))
                {
                    safetyLimitedFrameCount++;
                    safetyLimitDetails.Add(VerticalSolveVmdSafetyLimitDetail.Create(
                        frame,
                        "ik_disabled_no_visible_carrier",
                        requestedDeltaX,
                        requestedDeltaY,
                        requestedDeltaZ,
                        0f,
                        0f,
                        0f));
                    continue;
                }

                if (!TryClampVmdCarrierDeltasToStepSafety(
                        writeFrame,
                        writeFrameIndex > 0 ? frames[writeFrameIndex - 1] : null,
                        writeFrameIndex + 1 < frames.Count ? frames[writeFrameIndex + 1] : null,
                        ikStateFrames,
                        requestedDeltaX,
                        requestedDeltaY,
                        requestedDeltaZ,
                        out float safeDeltaX,
                        out float safeDeltaY,
                        out float safeDeltaZ))
                {
                    safetyLimitedFrameCount++;
                    safetyLimitDetails.Add(VerticalSolveVmdSafetyLimitDetail.Create(
                        writeFrame,
                        "step_safety_rejected",
                        requestedDeltaX,
                        requestedDeltaY,
                        requestedDeltaZ,
                        0f,
                        0f,
                        0f));
                    continue;
                }

                if (!HasNonZeroCorrection(safeDeltaX) &&
                    !HasNonZeroCorrection(safeDeltaY) &&
                    !HasNonZeroCorrection(safeDeltaZ))
                {
                    safetyLimitedFrameCount++;
                    safetyLimitDetails.Add(VerticalSolveVmdSafetyLimitDetail.Create(
                        writeFrame,
                        "step_safety_zeroed",
                        requestedDeltaX,
                        requestedDeltaY,
                        requestedDeltaZ,
                        safeDeltaX,
                        safeDeltaY,
                        safeDeltaZ));
                    continue;
                }

                if (Math.Abs(safeDeltaX - requestedDeltaX) > 0.000001f ||
                    Math.Abs(safeDeltaY - requestedDeltaY) > 0.000001f ||
                    Math.Abs(safeDeltaZ - requestedDeltaZ) > 0.000001f)
                {
                    safetyLimitedFrameCount++;
                    safetyLimitDetails.Add(VerticalSolveVmdSafetyLimitDetail.Create(
                        writeFrame,
                        "step_safety_scaled",
                        requestedDeltaX,
                        requestedDeltaY,
                        requestedDeltaZ,
                        safeDeltaX,
                        safeDeltaY,
                        safeDeltaZ));
                }

                if (HasNonZeroCorrection(safeDeltaX))
                {
                    int xOffset = writeFrame.Offset + 19;
                    byte[] xBytes = BitConverter.GetBytes(writeFrame.X + safeDeltaX);
                    Buffer.BlockCopy(xBytes, 0, bytes, xOffset, xBytes.Length);
                }

                if (HasNonZeroCorrection(safeDeltaY))
                {
                    int yOffset = writeFrame.Offset + 23;
                    byte[] yBytes = BitConverter.GetBytes(writeFrame.Y + safeDeltaY);
                    Buffer.BlockCopy(yBytes, 0, bytes, yOffset, yBytes.Length);
                }

                if (HasNonZeroCorrection(safeDeltaZ))
                {
                    int zOffset = writeFrame.Offset + 27;
                    byte[] zBytes = BitConverter.GetBytes(writeFrame.Z + safeDeltaZ);
                    Buffer.BlockCopy(zBytes, 0, bytes, zOffset, zBytes.Length);
                }

                correctedFrameCount++;
            }
        }

        EnsureParentDirectoryExists(outputVmdPath);
        if (correctedFrameCount > 0)
        {
            File.WriteAllBytes(outputVmdPath, bytes);
        }
        else
        {
            File.Copy(sourceVmdPath, outputVmdPath, overwrite: true);
        }

        FileInfo fileInfo = new FileInfo(outputVmdPath);
        fileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0L;
        return fileSizeBytes > 0L;
    }

    private static float ResolveRequestedCorrectedVmdDeltaY(
        VmdRewriteFrame frame,
        VerticalSolveFrameCorrection correction,
        IReadOnlyDictionary<uint, int> centerCarrierCountByFrame,
        IReadOnlyDictionary<uint, float> minEffectiveFootIkYByFrame)
    {
        if (frame.IsCenterCarrier)
        {
            float deltaY = correction.HipsY;
            if (!IsFinite(deltaY))
            {
                return 0f;
            }

            if (deltaY < 0f &&
                minEffectiveFootIkYByFrame != null &&
                minEffectiveFootIkYByFrame.TryGetValue(frame.Frame, out float minEffectiveFootIkY) &&
                IsFinite(minEffectiveFootIkY))
            {
                deltaY = Math.Max(deltaY, ResolveVerticalSolveFloorSafeY() - minEffectiveFootIkY);
            }

            int carrierCount = centerCarrierCountByFrame != null &&
                centerCarrierCountByFrame.TryGetValue(frame.Frame, out int count) &&
                count > 0
                    ? count
                    : 1;
            return deltaY / carrierCount;
        }

        if (!frame.IsFootIkCarrier || !IsFinite(correction.FootBottomY))
        {
            return 0f;
        }

        float footDeltaY = correction.FootBottomY;
        if (footDeltaY < 0f &&
            minEffectiveFootIkYByFrame != null &&
            minEffectiveFootIkYByFrame.TryGetValue(frame.Frame, out float minEffectiveFootIkYForFoot) &&
            IsFinite(minEffectiveFootIkYForFoot))
        {
            float pairedCenterDeltaY = IsFinite(correction.HipsY) && correction.HipsY < 0f ? correction.HipsY : 0f;
            float effectiveYAfterCenterSolve = minEffectiveFootIkYForFoot + pairedCenterDeltaY;
            footDeltaY = Math.Max(footDeltaY, ResolveVerticalSolveFloorSafeY() - effectiveYAfterCenterSolve);
        }

        return footDeltaY;
    }

    private static void ResolveRequestedCorrectedVmdDeltaXZ(
        VmdRewriteFrame frame,
        VerticalSolveFrameCorrection correction,
        out float deltaX,
        out float deltaZ)
    {
        deltaX = 0f;
        deltaZ = 0f;
        if (frame == null ||
            !frame.IsFootIkCarrier ||
            string.IsNullOrEmpty(frame.Side))
        {
            return;
        }

        if (string.Equals(frame.Side, "left", StringComparison.Ordinal))
        {
            deltaX = correction.LeftFootX;
            deltaZ = correction.LeftFootZ;
            return;
        }

        if (string.Equals(frame.Side, "right", StringComparison.Ordinal))
        {
            deltaX = correction.RightFootX;
            deltaZ = correction.RightFootZ;
        }
    }

    private static Dictionary<uint, int> CountCenterCarriersByFrame(IReadOnlyList<VmdRewriteFrame> frames)
    {
        Dictionary<uint, int> result = new Dictionary<uint, int>();
        foreach (VmdRewriteFrame frame in frames)
        {
            if (!frame.IsCenterCarrier)
            {
                continue;
            }

            result[frame.Frame] = result.TryGetValue(frame.Frame, out int current) ? current + 1 : 1;
        }

        return result;
    }

    private static Dictionary<string, List<VmdRewriteFrame>> BuildCarrierFramesByBone(IReadOnlyList<VmdRewriteFrame> frames)
    {
        Dictionary<string, List<VmdRewriteFrame>> result = new Dictionary<string, List<VmdRewriteFrame>>(StringComparer.Ordinal);
        foreach (VmdRewriteFrame frame in frames)
        {
            if (!frame.IsCenterCarrier && !frame.IsFootIkCarrier)
            {
                continue;
            }

            if (!result.TryGetValue(frame.BoneName, out List<VmdRewriteFrame> boneFrames))
            {
                boneFrames = new List<VmdRewriteFrame>();
                result[frame.BoneName] = boneFrames;
            }

            boneFrames.Add(frame);
        }

        return result;
    }

    private static Dictionary<uint, float> BuildMinEffectiveFootIkYByFrame(IReadOnlyList<VmdRewriteFrame> frames)
    {
        Dictionary<uint, float> centerYByFrame = new Dictionary<uint, float>();
        Dictionary<string, Dictionary<uint, float>> footYBySideFrame = new Dictionary<string, Dictionary<uint, float>>(StringComparer.Ordinal);
        Dictionary<string, Dictionary<uint, float>> toeYBySideFrame = new Dictionary<string, Dictionary<uint, float>>(StringComparer.Ordinal);
        foreach (VmdRewriteFrame frame in frames)
        {
            if (frame.IsCenterCarrier)
            {
                centerYByFrame[frame.Frame] = (centerYByFrame.TryGetValue(frame.Frame, out float current) ? current : 0f) + frame.Y;
                continue;
            }

            if (string.IsNullOrEmpty(frame.Side))
            {
                continue;
            }

            Dictionary<string, Dictionary<uint, float>> lookup = frame.IsFootIkCarrier ? footYBySideFrame : toeYBySideFrame;
            if (!lookup.TryGetValue(frame.Side, out Dictionary<uint, float> sideFrames))
            {
                sideFrames = new Dictionary<uint, float>();
                lookup[frame.Side] = sideFrames;
            }

            sideFrames[frame.Frame] = frame.Y;
        }

        Dictionary<uint, float> result = new Dictionary<uint, float>();
        foreach (KeyValuePair<string, Dictionary<uint, float>> sideFrames in footYBySideFrame)
        {
            foreach (KeyValuePair<uint, float> footFrame in sideFrames.Value)
            {
                float effectiveY = (centerYByFrame.TryGetValue(footFrame.Key, out float centerY) ? centerY : 0f) + footFrame.Value;
                SetMinEffectiveFootIkY(result, footFrame.Key, effectiveY);
            }
        }

        foreach (KeyValuePair<string, Dictionary<uint, float>> sideFrames in toeYBySideFrame)
        {
            footYBySideFrame.TryGetValue(sideFrames.Key, out Dictionary<uint, float> footYByFrame);
            foreach (KeyValuePair<uint, float> toeFrame in sideFrames.Value)
            {
                float effectiveY =
                    (centerYByFrame.TryGetValue(toeFrame.Key, out float centerY) ? centerY : 0f) +
                    (footYByFrame != null && footYByFrame.TryGetValue(toeFrame.Key, out float footY) ? footY : 0f) +
                    toeFrame.Value;
                SetMinEffectiveFootIkY(result, toeFrame.Key, effectiveY);
            }
        }

        return result;
    }

    private static void SetMinEffectiveFootIkY(Dictionary<uint, float> values, uint frame, float value)
    {
        if (!IsFinite(value))
        {
            return;
        }

        values[frame] = values.TryGetValue(frame, out float current) ? Math.Min(current, value) : value;
    }

    private static float ClampVmdCarrierDeltaToStepSafety(
        VmdRewriteFrame frame,
        VmdRewriteFrame previous,
        VmdRewriteFrame next,
        float requestedDeltaY)
    {
        float targetY = frame.Y + requestedDeltaY;
        float minY = float.NegativeInfinity;
        float maxY = float.PositiveInfinity;
        if (!ExpandSafeYRangeFromNeighbor(frame, previous, ref minY, ref maxY) ||
            !ExpandSafeYRangeFromNeighbor(frame, next, ref minY, ref maxY) ||
            minY > maxY)
        {
            return 0f;
        }

        targetY = Math.Min(maxY, Math.Max(minY, targetY));
        return targetY - frame.Y;
    }

    private static float ClampCenterCarrierDeltaToFloor(
        VmdRewriteFrame frame,
        float deltaY,
        IReadOnlyDictionary<uint, int> centerCarrierCountByFrame,
        IReadOnlyDictionary<uint, float> minEffectiveFootIkYByFrame)
    {
        if (!frame.IsCenterCarrier ||
            deltaY >= 0f ||
            minEffectiveFootIkYByFrame == null ||
            !minEffectiveFootIkYByFrame.TryGetValue(frame.Frame, out float minEffectiveFootIkY) ||
            !IsFinite(minEffectiveFootIkY))
        {
            return deltaY;
        }

        int carrierCount = centerCarrierCountByFrame != null &&
            centerCarrierCountByFrame.TryGetValue(frame.Frame, out int count) &&
            count > 0
                ? count
                : 1;
        float minDeltaY = (ResolveVerticalSolveFloorSafeY() - minEffectiveFootIkY) / carrierCount;
        return Math.Max(deltaY, minDeltaY);
    }

    private static bool TryResolveVisibleFootIkCarrierFrame(
        IReadOnlyList<VmdRewriteFrame> frames,
        int sourceIndex,
        List<VmdIkStateFrame> ikStateFrames,
        float requestedDeltaX,
        float requestedDeltaY,
        float requestedDeltaZ,
        out int visibleFrameIndex)
    {
        visibleFrameIndex = sourceIndex;
        if (frames == null ||
            sourceIndex < 0 ||
            sourceIndex >= frames.Count)
        {
            return false;
        }

        VmdRewriteFrame source = frames[sourceIndex];
        if (source == null ||
            !source.IsFootIkCarrier ||
            string.IsNullOrEmpty(source.Side) ||
            IsVmdIkEnabledAtFrame(ikStateFrames, source.BoneName, source.Frame) ||
            HasNonZeroCorrection(requestedDeltaY) ||
            (!HasNonZeroCorrection(requestedDeltaX) && !HasNonZeroCorrection(requestedDeltaZ)))
        {
            return false;
        }

        int bestIndex = -1;
        uint bestDistance = uint.MaxValue;
        for (int i = 0; i < frames.Count; i++)
        {
            if (i == sourceIndex)
            {
                continue;
            }

            VmdRewriteFrame candidate = frames[i];
            if (candidate == null ||
                !candidate.IsFootIkCarrier ||
                !string.Equals(candidate.Side, source.Side, StringComparison.Ordinal) ||
                !IsVmdIkEnabledAtFrame(ikStateFrames, candidate.BoneName, candidate.Frame))
            {
                continue;
            }

            uint distance = candidate.Frame > source.Frame
                ? candidate.Frame - source.Frame
                : source.Frame - candidate.Frame;
            if (distance == 0u ||
                distance > VerticalSolveVisibleIkCarrierSearchFrameWindow ||
                distance > bestDistance)
            {
                continue;
            }

            if (!TryClampVmdCarrierDeltasToStepSafety(
                    candidate,
                    i > 0 ? frames[i - 1] : null,
                    i + 1 < frames.Count ? frames[i + 1] : null,
                    ikStateFrames,
                    requestedDeltaX,
                    requestedDeltaY,
                    requestedDeltaZ,
                    out float safeDeltaX,
                    out float safeDeltaY,
                    out float safeDeltaZ) ||
                (!HasNonZeroCorrection(safeDeltaX) &&
                 !HasNonZeroCorrection(safeDeltaY) &&
                 !HasNonZeroCorrection(safeDeltaZ)))
            {
                continue;
            }

            if (distance < bestDistance || bestIndex < 0 || candidate.Frame >= source.Frame)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        visibleFrameIndex = bestIndex;
        return true;
    }

    private static bool IsDisabledFootIkHorizontalCarrierFrame(
        VmdRewriteFrame frame,
        List<VmdIkStateFrame> ikStateFrames,
        float requestedDeltaX,
        float requestedDeltaY,
        float requestedDeltaZ)
    {
        return frame != null &&
            frame.IsFootIkCarrier &&
            !IsVmdIkEnabledAtFrame(ikStateFrames, frame.BoneName, frame.Frame) &&
            !HasNonZeroCorrection(requestedDeltaY) &&
            (HasNonZeroCorrection(requestedDeltaX) || HasNonZeroCorrection(requestedDeltaZ));
    }

    private static float ResolveVerticalSolveFloorSafeY()
    {
        return QualityFloorTolerance + VerticalSolvePostprocessSafetyMarginY;
    }

    private static bool ExpandSafeYRangeFromNeighbor(
        VmdRewriteFrame frame,
        VmdRewriteFrame neighbor,
        ref float minY,
        ref float maxY)
    {
        if (neighbor == null)
        {
            return true;
        }

        float limit = Math.Max(0f, QualityTeleportStepThreshold - VerticalSolvePostprocessSafetyMarginY);
        float dx = frame.X - neighbor.X;
        float dz = frame.Z - neighbor.Z;
        float horizontalDistanceSquared = (dx * dx) + (dz * dz);
        float allowedYSquared = (limit * limit) - horizontalDistanceSquared;
        if (allowedYSquared < 0f)
        {
            return false;
        }

        float allowedY = (float)Math.Sqrt(allowedYSquared);
        minY = Math.Max(minY, neighbor.Y - allowedY);
        maxY = Math.Min(maxY, neighbor.Y + allowedY);
        return true;
    }

    private static bool TryClampVmdCarrierDeltasToStepSafety(
        VmdRewriteFrame frame,
        VmdRewriteFrame previous,
        VmdRewriteFrame next,
        List<VmdIkStateFrame> ikStateFrames,
        float requestedDeltaX,
        float requestedDeltaY,
        float requestedDeltaZ,
        out float safeDeltaX,
        out float safeDeltaY,
        out float safeDeltaZ)
    {
        VmdRewriteFrame previousForSafety = ShouldCheckPreviousVmdCarrierStep(frame, ikStateFrames) ? previous : null;
        VmdRewriteFrame nextForSafety = ShouldCheckNextVmdCarrierStep(next, ikStateFrames) ? next : null;
        return TryClampVmdCarrierDeltasToStepSafety(
            frame,
            previousForSafety,
            nextForSafety,
            requestedDeltaX,
            requestedDeltaY,
            requestedDeltaZ,
            out safeDeltaX,
            out safeDeltaY,
            out safeDeltaZ);
    }

    private static bool ShouldCheckPreviousVmdCarrierStep(VmdRewriteFrame frame, List<VmdIkStateFrame> ikStateFrames)
    {
        return frame == null ||
            !frame.IsFootIkCarrier ||
            IsVmdIkEnabledAtFrame(ikStateFrames, frame.BoneName, frame.Frame);
    }

    private static bool ShouldCheckNextVmdCarrierStep(VmdRewriteFrame next, List<VmdIkStateFrame> ikStateFrames)
    {
        return next == null ||
            !next.IsFootIkCarrier ||
            IsVmdIkEnabledAtFrame(ikStateFrames, next.BoneName, next.Frame);
    }

    private static bool TryClampVmdCarrierDeltasToStepSafety(
        VmdRewriteFrame frame,
        VmdRewriteFrame previous,
        VmdRewriteFrame next,
        float requestedDeltaX,
        float requestedDeltaY,
        float requestedDeltaZ,
        out float safeDeltaX,
        out float safeDeltaY,
        out float safeDeltaZ)
    {
        safeDeltaX = 0f;
        safeDeltaY = 0f;
        safeDeltaZ = 0f;
        if (frame == null ||
            !IsFinite(requestedDeltaX) ||
            !IsFinite(requestedDeltaY) ||
            !IsFinite(requestedDeltaZ))
        {
            return false;
        }

        if (IsVmdCarrierStepSafe(frame, previous, next, requestedDeltaX, requestedDeltaY, requestedDeltaZ))
        {
            safeDeltaX = requestedDeltaX;
            safeDeltaY = requestedDeltaY;
            safeDeltaZ = requestedDeltaZ;
            return true;
        }

        float low = 0f;
        float high = 1f;
        for (int i = 0; i < 16; i++)
        {
            float scale = (low + high) * 0.5f;
            if (IsVmdCarrierStepSafe(
                    frame,
                    previous,
                    next,
                    requestedDeltaX * scale,
                    requestedDeltaY * scale,
                    requestedDeltaZ * scale))
            {
                low = scale;
            }
            else
            {
                high = scale;
            }
        }

        if (low <= 0f)
        {
            return false;
        }

        safeDeltaX = requestedDeltaX * low;
        safeDeltaY = requestedDeltaY * low;
        safeDeltaZ = requestedDeltaZ * low;
        return true;
    }

    private static bool IsVmdCarrierStepSafe(
        VmdRewriteFrame frame,
        VmdRewriteFrame previous,
        VmdRewriteFrame next,
        float deltaX,
        float deltaY,
        float deltaZ)
    {
        return IsVmdCarrierStepSafe(frame, previous, deltaX, deltaY, deltaZ) &&
            IsVmdCarrierStepSafe(frame, next, deltaX, deltaY, deltaZ);
    }

    private static bool IsVmdCarrierStepSafe(
        VmdRewriteFrame frame,
        VmdRewriteFrame neighbor,
        float deltaX,
        float deltaY,
        float deltaZ)
    {
        if (neighbor == null)
        {
            return true;
        }

        float limit = Math.Max(0f, QualityTeleportStepThreshold - VerticalSolvePostprocessSafetyMarginY);
        float dx = frame.X + deltaX - neighbor.X;
        float dy = frame.Y + deltaY - neighbor.Y;
        float dz = frame.Z + deltaZ - neighbor.Z;
        return Distance(0f, 0f, 0f, dx, dy, dz) <= limit;
    }

    private static void WriteVerticalSolveCorrectedCandidateManifest(
        string manifestPath,
        string rawCandidateMetricsCsvPath,
        string rawCandidateVmdPath,
        string correctedCandidateMetricsCsvPath,
        string correctedCandidateVmdPath,
        int correctedRows,
        int correctedVmdChangedFrames,
        int correctedVmdSafetyLimitedFrames,
        long correctedVmdBytes,
        IReadOnlyList<VerticalSolveVmdSafetyLimitDetail> correctedVmdSafetyLimitDetails)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return;
        }

        EnsureParentDirectoryExists(manifestPath);
        string json =
            "{" +
            "\"artifact_role\":\"corrected_vertical_solve_candidate\"," +
            "\"generated_at\":\"" + EscapeJson(DateTime.Now.ToString("o", CultureInfo.InvariantCulture)) + "\"," +
            "\"raw_candidate_metrics_csv\":\"" + EscapeJson(rawCandidateMetricsCsvPath) + "\"," +
            "\"raw_candidate_vmd_path\":\"" + EscapeJson(rawCandidateVmdPath) + "\"," +
            "\"corrected_candidate_metrics_csv\":\"" + EscapeJson(correctedCandidateMetricsCsvPath) + "\"," +
            "\"corrected_candidate_vmd_path\":\"" + EscapeJson(correctedCandidateVmdPath) + "\"," +
            "\"corrected_metric_rows\":" + correctedRows.ToString(CultureInfo.InvariantCulture) + "," +
            "\"corrected_vmd_changed_frames\":" + correctedVmdChangedFrames.ToString(CultureInfo.InvariantCulture) + "," +
            "\"corrected_vmd_safety_limited_frames\":" + correctedVmdSafetyLimitedFrames.ToString(CultureInfo.InvariantCulture) + "," +
            "\"corrected_vmd_safety_limited_frame_details\":" + BuildVerticalSolveVmdSafetyLimitDetailsJson(correctedVmdSafetyLimitDetails) + "," +
            "\"corrected_vmd_bytes\":" + correctedVmdBytes.ToString(CultureInfo.InvariantCulture) + "," +
            "\"frame_quality_evaluator\":\"raw_frame_quality_evaluator\"" +
            "}";
        File.WriteAllText(manifestPath, json, Encoding.UTF8);
    }

    private static string BuildVerticalSolveVmdSafetyLimitDetailsJson(
        IReadOnlyList<VerticalSolveVmdSafetyLimitDetail> details)
    {
        if (details == null || details.Count == 0)
        {
            return "[]";
        }

        StringBuilder builder = new StringBuilder();
        builder.Append('[');
        for (int i = 0; i < details.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            VerticalSolveVmdSafetyLimitDetail detail = details[i];
            builder.Append('{');
            builder.Append("\"frame\":").Append(detail.Frame.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"bone\":\"").Append(EscapeJson(detail.BoneName)).Append("\",");
            builder.Append("\"side\":\"").Append(EscapeJson(detail.Side)).Append("\",");
            builder.Append("\"reason\":\"").Append(EscapeJson(detail.Reason)).Append("\",");
            builder.Append("\"requested_delta_x\":").Append(FormatJsonFloat(detail.RequestedDeltaX)).Append(',');
            builder.Append("\"requested_delta_y\":").Append(FormatJsonFloat(detail.RequestedDeltaY)).Append(',');
            builder.Append("\"requested_delta_z\":").Append(FormatJsonFloat(detail.RequestedDeltaZ)).Append(',');
            builder.Append("\"safe_delta_x\":").Append(FormatJsonFloat(detail.SafeDeltaX)).Append(',');
            builder.Append("\"safe_delta_y\":").Append(FormatJsonFloat(detail.SafeDeltaY)).Append(',');
            builder.Append("\"safe_delta_z\":").Append(FormatJsonFloat(detail.SafeDeltaZ));
            builder.Append('}');
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static string FormatJsonFloat(float value)
    {
        return IsFinite(value)
            ? value.ToString("G9", CultureInfo.InvariantCulture)
            : "null";
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static bool TryWriteVerticalSolvePostprocessMetricsCsv(
        string candidateMetricsCsvPath,
        string outputPath,
        IReadOnlyDictionary<int, VerticalSolveFrameCorrection> verticalSolveCorrections,
        out int correctedRows)
    {
        correctedRows = 0;
        if (string.IsNullOrWhiteSpace(candidateMetricsCsvPath) ||
            string.IsNullOrWhiteSpace(outputPath) ||
            verticalSolveCorrections == null ||
            verticalSolveCorrections.Count == 0 ||
            !File.Exists(candidateMetricsCsvPath))
        {
            return false;
        }

        string[] lines = File.ReadAllLines(candidateMetricsCsvPath, Encoding.UTF8);
        if (lines.Length <= 1)
        {
            return false;
        }

        string[] headers = AppendMissingCsvColumns(
            SplitCsvLine(lines[0]),
            VerticalSolveCorrectionDiagnosticColumns);
        Dictionary<string, int> columns = BuildColumnLookup(headers);
        if (!columns.ContainsKey("recorderFrame") ||
            !columns.ContainsKey("hipsY") ||
            !columns.ContainsKey("lowestFootBottomY"))
        {
            return false;
        }

        var output = new List<string>
        {
            BuildCsvLine(headers, headers.Length)
        };
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                output.Add(line);
                continue;
            }

            string[] values = SplitCsvLine(line);
            if (values.Length < headers.Length)
            {
                Array.Resize(ref values, headers.Length);
                for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
                {
                    values[valueIndex] = values[valueIndex] ?? "";
                }
            }

            if (TryReadInt(values, columns, "recorderFrame", out int recorderFrame) &&
                verticalSolveCorrections.TryGetValue(recorderFrame, out VerticalSolveFrameCorrection correction))
            {
                bool changed = false;
                changed |= TryApplyMetricsCsvFloatDelta(values, columns, "hipsY", correction.HipsY);
                changed |= TryApplyMetricsCsvFloatDelta(values, columns, "lowestFootBottomY", correction.FootBottomY);
                changed |= TryApplyMetricsCsvFloatDelta(values, columns, "lowestFootY", correction.FootBottomY);
                changed |= TryApplyMetricsCsvFloatDelta(values, columns, "footBottomGroundGap", correction.FootBottomY);
                changed |= TryApplyMetricsCsvFloatDelta(values, columns, "leftFootX", correction.LeftFootX);
                changed |= TryApplyMetricsCsvFloatDelta(values, columns, "leftFootZ", correction.LeftFootZ);
                changed |= TryApplyMetricsCsvFloatDelta(values, columns, "rightFootX", correction.RightFootX);
                changed |= TryApplyMetricsCsvFloatDelta(values, columns, "rightFootZ", correction.RightFootZ);
                WriteVerticalSolveCorrectionDiagnostics(values, columns, correction);
                if (changed)
                {
                    correctedRows++;
                }
            }

            output.Add(BuildCsvLine(values, headers.Length));
        }

        EnsureParentDirectoryExists(outputPath);
        File.WriteAllLines(outputPath, output, Encoding.UTF8);
        return correctedRows > 0 && File.Exists(outputPath);
    }

    private static string[] AppendMissingCsvColumns(string[] headers, string[] additionalColumns)
    {
        var output = new List<string>(headers ?? Array.Empty<string>());
        var seen = new HashSet<string>(output, StringComparer.Ordinal);
        if (additionalColumns == null)
        {
            return output.ToArray();
        }

        for (int i = 0; i < additionalColumns.Length; i++)
        {
            string column = additionalColumns[i];
            if (!string.IsNullOrWhiteSpace(column) && seen.Add(column))
            {
                output.Add(column);
            }
        }

        return output.ToArray();
    }

    private static void WriteVerticalSolveCorrectionDiagnostics(
        string[] values,
        Dictionary<string, int> columns,
        VerticalSolveFrameCorrection correction)
    {
        SetMetricsCsvFloat(values, columns, "verticalSolveCorrectionHipsY", correction.HipsY);
        SetMetricsCsvFloat(values, columns, "verticalSolveCorrectionFootBottomY", correction.FootBottomY);
        SetMetricsCsvFloat(values, columns, "verticalSolveCorrectionLeftFootX", correction.LeftFootX);
        SetMetricsCsvFloat(values, columns, "verticalSolveCorrectionLeftFootZ", correction.LeftFootZ);
        SetMetricsCsvFloat(values, columns, "verticalSolveCorrectionRightFootX", correction.RightFootX);
        SetMetricsCsvFloat(values, columns, "verticalSolveCorrectionRightFootZ", correction.RightFootZ);
        SetMetricsCsvFloat(values, columns, "verticalSolveHorizontalFootTargetMagnitude", correction.HorizontalFootTargetMagnitude);
        SetMetricsCsvFloat(values, columns, "verticalSolveLeftFootNormalizedDeltaX", correction.LeftFootNormalizedDeltaX);
        SetMetricsCsvFloat(values, columns, "verticalSolveLeftFootNormalizedDeltaZ", correction.LeftFootNormalizedDeltaZ);
        SetMetricsCsvFloat(
            values,
            columns,
            "verticalSolveLeftFootNormalizedMagnitude",
            CalculateHorizontalMagnitude(correction.LeftFootNormalizedDeltaX, correction.LeftFootNormalizedDeltaZ));
        SetMetricsCsvFloat(values, columns, "verticalSolveRightFootNormalizedDeltaX", correction.RightFootNormalizedDeltaX);
        SetMetricsCsvFloat(values, columns, "verticalSolveRightFootNormalizedDeltaZ", correction.RightFootNormalizedDeltaZ);
        SetMetricsCsvFloat(
            values,
            columns,
            "verticalSolveRightFootNormalizedMagnitude",
            CalculateHorizontalMagnitude(correction.RightFootNormalizedDeltaX, correction.RightFootNormalizedDeltaZ));
        SetMetricsCsvString(values, columns, "verticalSolveCorrectionSource", ResolveVerticalSolveCorrectionSource(correction));
    }

    private static float CalculateHorizontalMagnitude(float x, float z)
    {
        return IsFinite(x) && IsFinite(z)
            ? Distance(0f, 0f, 0f, x, 0f, z)
            : float.NaN;
    }

    private static string ResolveVerticalSolveCorrectionSource(VerticalSolveFrameCorrection correction)
    {
        bool hasVertical = HasNonZeroCorrection(correction.HipsY) || HasNonZeroCorrection(correction.FootBottomY);
        bool hasHorizontal = HasNonZeroCorrection(correction.LeftFootX) ||
            HasNonZeroCorrection(correction.LeftFootZ) ||
            HasNonZeroCorrection(correction.RightFootX) ||
            HasNonZeroCorrection(correction.RightFootZ);
        if (hasVertical && hasHorizontal)
        {
            return "vertical_and_horizontal_foot_xz";
        }

        if (hasHorizontal)
        {
            return "horizontal_foot_xz";
        }

        if (hasVertical)
        {
            return "vertical";
        }

        return "";
    }

    private static void SetMetricsCsvFloat(
        string[] values,
        Dictionary<string, int> columns,
        string columnName,
        float value)
    {
        if (!columns.TryGetValue(columnName, out int index) ||
            index < 0 ||
            index >= values.Length)
        {
            return;
        }

        values[index] = IsFinite(value) ? FormatMetricsCsvFloat(value) : "";
    }

    private static void SetMetricsCsvString(
        string[] values,
        Dictionary<string, int> columns,
        string columnName,
        string value)
    {
        if (!columns.TryGetValue(columnName, out int index) ||
            index < 0 ||
            index >= values.Length)
        {
            return;
        }

        values[index] = value ?? "";
    }

    private static bool TryApplyMetricsCsvFloatDelta(
        string[] values,
        Dictionary<string, int> columns,
        string columnName,
        float delta)
    {
        if (!IsFinite(delta) || Math.Abs(delta) <= 0f ||
            !columns.TryGetValue(columnName, out int index) ||
            index < 0 ||
            index >= values.Length)
        {
            return false;
        }

        if (!float.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
            !IsFinite(value))
        {
            return false;
        }

        values[index] = FormatMetricsCsvFloat(value + delta);
        return true;
    }

    private static string BuildCsvLine(string[] values, int columnCount)
    {
        int safeCount = Math.Max(0, columnCount);
        var escaped = new string[safeCount];
        for (int i = 0; i < safeCount; i++)
        {
            string value = i < (values?.Length ?? 0) ? values[i] : "";
            escaped[i] = EscapeCsv(value ?? "");
        }

        return string.Join(",", escaped);
    }

    private static bool TryGetComparisonCandidateFrame(
        MetricsCsvData candidate,
        MetricsCsvFrame baselineFrame,
        HashSet<int> matchedCandidateFrames,
        out int candidateRecorderFrame,
        out MetricsCsvFrame candidateFrame)
    {
        int baselineRecorderFrame = baselineFrame.RecorderFrame;
        if (TryGetUnmatchedCandidateFrame(
                candidate,
                baselineRecorderFrame,
                matchedCandidateFrames,
                out candidateRecorderFrame,
                out candidateFrame))
        {
            return true;
        }

        for (int offset = 1; offset <= QualityMetricFrameMatchTolerance; offset++)
        {
            if (TryGetUnmatchedCandidateFrame(
                    candidate,
                    baselineRecorderFrame - offset,
                    matchedCandidateFrames,
                    out candidateRecorderFrame,
                    out candidateFrame) ||
                TryGetUnmatchedCandidateFrame(
                    candidate,
                    baselineRecorderFrame + offset,
                    matchedCandidateFrames,
                    out candidateRecorderFrame,
                    out candidateFrame))
            {
                return true;
            }
        }

        if (TryGetUnmatchedTerminalReasonCandidateFrame(
                candidate,
                baselineFrame.Reason,
                matchedCandidateFrames,
                out candidateRecorderFrame,
                out candidateFrame))
        {
            return true;
        }

        candidateRecorderFrame = 0;
        candidateFrame = default;
        return false;
    }

    private static bool TryGetUnmatchedCandidateFrame(
        MetricsCsvData candidate,
        int recorderFrame,
        HashSet<int> matchedCandidateFrames,
        out int candidateRecorderFrame,
        out MetricsCsvFrame candidateFrame)
    {
        if (!matchedCandidateFrames.Contains(recorderFrame) &&
            candidate.Frames.TryGetValue(recorderFrame, out candidateFrame))
        {
            candidateRecorderFrame = recorderFrame;
            return true;
        }

        candidateRecorderFrame = 0;
        candidateFrame = default;
        return false;
    }

    private static bool TryGetUnmatchedTerminalReasonCandidateFrame(
        MetricsCsvData candidate,
        string baselineReason,
        HashSet<int> matchedCandidateFrames,
        out int candidateRecorderFrame,
        out MetricsCsvFrame candidateFrame)
    {
        candidateRecorderFrame = 0;
        candidateFrame = default;
        if (!IsTerminalComparisonSampleReason(baselineReason))
        {
            return false;
        }

        bool found = false;
        foreach (MetricsCsvFrame frame in candidate.Frames.Values)
        {
            if (matchedCandidateFrames.Contains(frame.RecorderFrame) ||
                !IsTerminalComparisonSampleReason(frame.Reason))
            {
                continue;
            }

            if (!found || frame.RecorderFrame > candidateRecorderFrame)
            {
                candidateRecorderFrame = frame.RecorderFrame;
                candidateFrame = frame;
                found = true;
            }
        }

        return found;
    }

    private static bool IsTerminalComparisonSampleReason(string reason)
    {
        return string.Equals((reason ?? "").Trim(), "finish", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPreRetargetStartComparisonSample(MetricsCsvFrame baselineFrame, MetricsCsvFrame candidateFrame)
    {
        return IsSamplingStartReason(baselineFrame.Reason) &&
            IsSamplingStartReason(candidateFrame.Reason);
    }

    private static bool IsSamplingStartReason(string reason)
    {
        return string.Equals((reason ?? "").Trim(), "start", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyFrameQualityStatus(
        MotionComparisonFrameQualitySummary summary,
        bool allowSameFrameRootPositionDelta = false,
        bool allowRelativeFootBottomDelta = false)
    {
        List<string> reasons = new List<string>();
        List<string> allowedNotes = new List<string>();
        bool fail = false;
        bool warn = false;
        if (summary.baseline_metric_frames == 0 || summary.candidate_metric_frames == 0)
        {
            reasons.Add("missing metrics csv rows");
            warn = true;
        }

        if (summary.compared_frames == 0)
        {
            reasons.Add("no same-recorderFrame metric samples");
            warn = true;
        }

        float floorCheckFootIkY = IsFinite(summary.min_candidate_vmd_effective_foot_ik_y)
            ? summary.min_candidate_vmd_effective_foot_ik_y
            : summary.min_candidate_vmd_foot_ik_y;
        bool floorContactFailed = summary.candidate_below_floor_metric_frames > 0 || IsBelowFloor(floorCheckFootIkY);
        summary.floor_contact_gate_status = floorContactFailed ? "fail" : "pass";
        summary.floor_contact_gate_status_reason = floorContactFailed
            ? "below-floor foot/IK sample detected"
            : "candidate foot/contact samples stayed above floor";
        if (floorContactFailed)
        {
            reasons.Add(summary.floor_contact_gate_status_reason);
            fail = true;
        }

        if (IsYybFrameQualityCandidate(summary))
        {
            if (!summary.candidate_yyb_deformation_risk_column_present ||
                summary.candidate_yyb_deformation_risk_missing_frames > 0)
            {
                reasons.Add("YYB deformation risk diagnostic missing");
                fail = true;
            }

            if (ExceedsThreshold(summary.candidate_yyb_max_deformation_risk, QualityYybDeformationRiskFailThreshold))
            {
                reasons.Add("YYB deformation risk threshold exceeded");
                fail = true;
            }

            if (!summary.candidate_yyb_sleeve_thickness_risk_column_present ||
                summary.candidate_yyb_sleeve_thickness_risk_missing_frames > 0)
            {
                reasons.Add("YYB sleeve thickness diagnostic missing");
                fail = true;
            }

            if (ExceedsThreshold(summary.candidate_yyb_max_sleeve_thickness_risk, QualityYybSleeveThicknessRiskFailThreshold))
            {
                reasons.Add("YYB sleeve thickness risk threshold exceeded");
                fail = true;
            }
        }

        bool hasRetargetRootDelta = IsTeleportStep(summary.candidate_retarget_root_delta_max);
        if (summary.candidate_root_step_spike_frames > 0 ||
            summary.candidate_vmd_center_spike_frames > 0 ||
            summary.candidate_vmd_foot_ik_spike_frames > 0 ||
            hasRetargetRootDelta ||
            IsTeleportStep(summary.candidate_retarget_pose_delta_max))
        {
            reasons.Add("one-frame root/center/IK teleport threshold exceeded");
            fail = true;
        }

        bool allowMovingRootPathDelta = allowSameFrameRootPositionDelta || IsMainRecordingMovingRootCandidate(summary);
        if (IsTeleportStep(summary.max_same_frame_root_position_delta))
        {
            if (allowMovingRootPathDelta)
            {
                allowedNotes.Add("intentional moving-root stage path delta");
            }
            else
            {
                reasons.Add("same-frame root position delta threshold exceeded");
                fail = true;
            }
        }

        if (IsMainRecordingStationaryPreviewCandidate(summary) &&
            !allowMovingRootPathDelta &&
            ExceedsThreshold(summary.candidate_limb_motion_root_travel, QualityStationaryLimbRootTravelFailThreshold))
        {
            reasons.Add("stationary preview limb-motion root travel threshold exceeded");
            fail = true;
        }

        float overallLimbPoseGateDelta = GetOverallSameFrameLimbPoseDeltaForGate(summary);
        float limbPoseGateDelta = GetSameFrameLimbPoseDeltaForGate(summary);
        if (ExceedsThreshold(limbPoseGateDelta, QualitySameFrameLimbPoseDeltaFailThreshold))
        {
            reasons.Add("same-frame limb pose delta threshold exceeded");
            fail = true;
        }
        else if (ExceedsThreshold(overallLimbPoseGateDelta, QualitySameFrameLimbPoseDeltaFailThreshold))
        {
            allowedNotes.Add("post-vmd limb pose delta");
        }

        if (ExceedsThreshold(summary.max_same_frame_hips_y_delta, QualitySameFrameHipsYFailThreshold))
        {
            reasons.Add("same-frame hips Y delta fail threshold exceeded");
            fail = true;
        }
        else if (ExceedsThreshold(summary.max_same_frame_hips_y_delta, QualitySameFrameHipsYWarnThreshold))
        {
            reasons.Add("same-frame hips Y delta warning threshold exceeded");
            warn = true;
        }

        if (ExceedsThreshold(summary.max_same_frame_foot_bottom_y_delta, QualitySameFrameFootBottomYFailThreshold))
        {
            if (allowRelativeFootBottomDelta)
            {
                allowedNotes.Add("relative foot-bottom delta");
            }
            else
            {
                reasons.Add("same-frame foot bottom Y delta fail threshold exceeded");
                fail = true;
            }
        }
        else if (ExceedsThreshold(summary.max_same_frame_foot_bottom_y_delta, QualitySameFrameFootBottomYWarnThreshold))
        {
            if (allowRelativeFootBottomDelta)
            {
                allowedNotes.Add("relative foot-bottom delta");
            }
            else
            {
                reasons.Add("same-frame foot bottom Y delta warning threshold exceeded");
                warn = true;
            }
        }

        float sameFrameFootXzGateDelta = GetSameFrameFootXzDeltaForGate(summary);
        if (ExceedsThreshold(sameFrameFootXzGateDelta, QualitySameFrameFootXzFailThreshold))
        {
            reasons.Add("same-frame foot XZ delta fail threshold exceeded");
            fail = true;
        }
        else if (ExceedsThreshold(sameFrameFootXzGateDelta, QualitySameFrameFootXzWarnThreshold))
        {
            reasons.Add("same-frame foot XZ delta warning threshold exceeded");
            warn = true;
        }

        if (reasons.Count == 0 &&
            (summary.missing_baseline_frames > 0 || summary.missing_candidate_frames > 0 || summary.candidate_vmd_bone_frames == 0))
        {
            reasons.Add("partial evidence only");
            fail = true;
        }

        if (reasons.Count == 0)
        {
            summary.status = "pass";
            summary.status_reason = allowedNotes.Count > 0
                ? "same-frame Unity metrics and VMD export checks stayed within thresholds; " +
                  string.Join(", ", allowedNotes.ToArray()) +
                  " reported but excluded from the stationary-root gate"
                : "same-frame Unity metrics and VMD export checks stayed within thresholds";
            return;
        }

        summary.status = fail ? "fail" : warn ? "warn" : "pass";
        summary.status_reason = string.Join("; ", reasons.ToArray());
    }

    private static float GetSameFrameFootXzDeltaForGate(MotionComparisonFrameQualitySummary summary)
    {
        if (summary != null &&
            summary.candidate_vmd_max_bone_frame_index >= 0 &&
            IsFinite(summary.max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range))
        {
            return summary.max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range;
        }

        return summary != null ? summary.max_same_frame_foot_xz_delta : float.NaN;
    }

    private static float GetSameFrameLimbPoseDeltaForGate(MotionComparisonFrameQualitySummary summary)
    {
        if (summary != null &&
            summary.candidate_vmd_max_bone_frame_index >= 0 &&
            IsFinite(summary.max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range))
        {
            return summary.max_same_frame_limb_pose_gate_delta_within_candidate_vmd_frame_range;
        }

        return GetOverallSameFrameLimbPoseDeltaForGate(summary);
    }

    private static float GetOverallSameFrameLimbPoseDeltaForGate(MotionComparisonFrameQualitySummary summary)
    {
        if (summary == null)
        {
            return float.NaN;
        }

        return IsFinite(summary.max_same_frame_limb_pose_gate_delta)
            ? summary.max_same_frame_limb_pose_gate_delta
            : IsFinite(summary.max_same_frame_guard_normalized_limb_pose_delta)
            ? summary.max_same_frame_guard_normalized_limb_pose_delta
            : summary.max_same_frame_limb_pose_delta;
    }

    private static bool IsWithinCandidateVmdFrameRange(
        MotionComparisonFrameQualitySummary summary,
        int candidateRecorderFrame)
    {
        return summary == null ||
            summary.candidate_vmd_max_bone_frame_index < 0 ||
            candidateRecorderFrame <= summary.candidate_vmd_max_bone_frame_index;
    }

    private static bool IsYybFrameQualityCandidate(MotionComparisonFrameQualitySummary summary)
    {
        return summary != null &&
            (MatchesYybModelName(summary.candidate_label) ||
             MatchesYybModelName(Path.GetFileNameWithoutExtension(summary.candidate_metrics_csv)));
    }

    private static bool IsMainRecordingStationaryPreviewCandidate(MotionComparisonFrameQualitySummary summary)
    {
        if (summary == null)
        {
            return false;
        }

        string label = NormalizeDiagnosticTransformName(summary.candidate_label);
        string metricsPath = NormalizeDiagnosticTransformName(summary.candidate_metrics_csv);
        bool isMainRecording =
            label.Contains("main_recoding") ||
            label.Contains("main_recording") ||
            metricsPath.Contains("main_recoding") ||
            metricsPath.Contains("main-recording") ||
            metricsPath.Contains("main_recording");
        bool isReplay = label.Contains("vmd replay") || metricsPath.Contains("vmd-replay");
        return isMainRecording && !isReplay;
    }

    private static bool IsMainRecordingMovingRootCandidate(MotionComparisonFrameQualitySummary summary)
    {
        if (summary == null)
        {
            return false;
        }

        string label = NormalizeDiagnosticTransformName(summary.candidate_label);
        string metricsPath = NormalizeDiagnosticTransformName(summary.candidate_metrics_csv);
        return label.Contains("main_recoding") ||
            label.Contains("main_recording") ||
            metricsPath.Contains("main_recoding") ||
            metricsPath.Contains("main-recording") ||
            metricsPath.Contains("main_recording");
    }

    private static void ApplyVerticalSolvePrototypeStatus(MotionComparisonFrameQualitySummary summary)
    {
        List<string> reasons = new List<string>();
        bool fail = false;
        bool warn = false;
        if (summary.baseline_metric_frames == 0 || summary.candidate_metric_frames == 0)
        {
            reasons.Add("projected solve has missing metrics csv rows");
            warn = true;
        }

        if (summary.compared_frames == 0)
        {
            reasons.Add("projected solve has no same-recorderFrame metric samples");
            warn = true;
        }

        float floorCheckFootIkY = IsFinite(summary.min_candidate_vmd_effective_foot_ik_y)
            ? summary.min_candidate_vmd_effective_foot_ik_y
            : summary.min_candidate_vmd_foot_ik_y;
        if (summary.vertical_solve_prototype_below_floor_metric_frames > 0 || IsBelowFloor(floorCheckFootIkY))
        {
            reasons.Add("projected solve would keep a below-floor foot/IK sample");
            fail = true;
        }

        if (summary.candidate_root_step_spike_frames > 0 ||
            summary.candidate_vmd_center_spike_frames > 0 ||
            summary.candidate_vmd_foot_ik_spike_frames > 0 ||
            IsTeleportStep(summary.candidate_retarget_root_delta_max) ||
            IsTeleportStep(summary.candidate_retarget_pose_delta_max))
        {
            reasons.Add("projected solve would keep one-frame root/center/IK teleport threshold exceeded");
            fail = true;
        }

        if (IsTeleportStep(summary.vertical_solve_prototype_max_same_frame_root_position_delta))
        {
            reasons.Add("projected solve would keep same-frame root position delta threshold exceeded");
            fail = true;
        }

        if (ExceedsThreshold(
                summary.vertical_solve_prototype_max_same_frame_hips_y_delta,
                QualitySameFrameHipsYFailThreshold))
        {
            reasons.Add("projected solve would keep same-frame hips Y delta fail threshold exceeded");
            fail = true;
        }
        else if (ExceedsThreshold(
                     summary.vertical_solve_prototype_max_same_frame_hips_y_delta,
                     QualitySameFrameHipsYWarnThreshold))
        {
            reasons.Add("projected solve would keep same-frame hips Y delta warning threshold exceeded");
            warn = true;
        }

        if (ExceedsThreshold(
                summary.vertical_solve_prototype_max_same_frame_foot_bottom_y_delta,
                QualitySameFrameFootBottomYFailThreshold))
        {
            reasons.Add("projected solve would keep same-frame foot bottom Y delta fail threshold exceeded");
            fail = true;
        }
        else if (ExceedsThreshold(
                     summary.vertical_solve_prototype_max_same_frame_foot_bottom_y_delta,
                     QualitySameFrameFootBottomYWarnThreshold))
        {
            reasons.Add("projected solve would keep same-frame foot bottom Y delta warning threshold exceeded");
            warn = true;
        }

        if (reasons.Count == 0)
        {
            summary.vertical_solve_prototype_status = "pass";
            summary.vertical_solve_prototype_status_reason =
                "projected frame-specific vertical solve stayed within thresholds";
            return;
        }

        summary.vertical_solve_prototype_status = fail ? "fail" : warn ? "warn" : "pass";
        summary.vertical_solve_prototype_status_reason = string.Join("; ", reasons.ToArray());
    }

    private static MetricsCsvData ReadMetricsCsvData(string path)
    {
        MetricsCsvData data = new MetricsCsvData();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return data;
        }

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length <= 1)
        {
            return data;
        }

        string[] headers = SplitCsvLine(lines[0]);
        Dictionary<string, int> columns = BuildColumnLookup(headers);
        data.HasYybMaxDeformationRiskColumn = columns.ContainsKey("yybMaxDeformationRisk");
        data.HasYybSleeveThicknessRiskColumns =
            columns.ContainsKey("leftSleeveThicknessRisk") &&
            columns.ContainsKey("rightSleeveThicknessRisk");
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[lineIndex]))
            {
                continue;
            }

            string[] values = SplitCsvLine(lines[lineIndex]);
            if (!TryReadInt(values, columns, "recorderFrame", out int recorderFrame) || recorderFrame < 0)
            {
                continue;
            }

            MetricsCsvFrame frame = new MetricsCsvFrame
            {
                Reason = ReadString(values, columns, "reason"),
                RecorderFrame = recorderFrame,
                RootX = ReadFloat(values, columns, "rootX"),
                RootY = ReadFloat(values, columns, "rootY"),
                RootZ = ReadFloat(values, columns, "rootZ"),
                RecordingStartRootY = ReadFloat(values, columns, "retargetRecordingStartRootY"),
                RecordingStartBodyPositionY = ReadFloat(values, columns, "retargetRecordingStartBodyPositionY"),
                RecordingStartHipsLocalY = ReadFloat(values, columns, "retargetRecordingStartHipsLocalY"),
                RecordingStartHipsY = ReadFloat(values, columns, "retargetRecordingStartHipsY"),
                RecordingStartHipsReferenceBeforeLocalY = ReadFloat(values, columns, "retargetRecordingStartHipsReferenceBeforeLocalY"),
                RecordingStartHipsReferenceAfterLocalY = ReadFloat(values, columns, "retargetRecordingStartHipsReferenceAfterLocalY"),
                RecordingStartHipsReferenceDeltaY = ReadFloat(values, columns, "retargetRecordingStartHipsReferenceDeltaY"),
                RecordingStartHipsReferenceFlipDetected = ReadInt(values, columns, "retargetRecordingStartHipsReferenceFlipDetected", -1),
                RecordingStartHipsReferenceStage = ReadString(values, columns, "retargetRecordingStartHipsReferenceStage"),
                BodyPositionY = ReadFloat(values, columns, "bodyPositionY"),
                HipsLocalY = ReadFloat(values, columns, "hipsLocalY"),
                GroundingVerticalStepLast = ReadFloat(values, columns, "retargetGroundingVerticalStepLast"),
                FootHeightReferenceLift = ReadFloat(values, columns, "retargetFootHeightReferenceLift"),
                HipsX = ReadFloat(values, columns, "hipsX"),
                HipsZ = ReadFloat(values, columns, "hipsZ"),
                HipsY = ReadFloat(values, columns, "hipsY"),
                LowestFootBottomY = ReadFloat(values, columns, "lowestFootBottomY"),
                FootBottomGroundGap = ReadFloat(values, columns, "footBottomGroundGap"),
                LeftFootX = ReadFloat(values, columns, "leftFootX"),
                LeftFootZ = ReadFloat(values, columns, "leftFootZ"),
                RightFootX = ReadFloat(values, columns, "rightFootX"),
                RightFootZ = ReadFloat(values, columns, "rightFootZ"),
                RetargetRootDeltaMax = ReadFloat(values, columns, "retargetRootDeltaMax"),
                RetargetPoseDeltaMax = ReadFloat(values, columns, "retargetPoseRootDeltaMax"),
                GroundingVerticalStepMax = ReadFloat(values, columns, "retargetGroundingVerticalStepMax"),
                LeftShoulderDownUpMuscle = ReadFloat(values, columns, "leftShoulderDownUpMuscle"),
                LeftShoulderFrontBackMuscle = ReadFloat(values, columns, "leftShoulderFrontBackMuscle"),
                LeftArmDownUpMuscle = ReadFloat(values, columns, "leftArmDownUpMuscle"),
                LeftArmFrontBackMuscle = ReadFloat(values, columns, "leftArmFrontBackMuscle"),
                LeftArmTwistMuscle = ReadFloat(values, columns, "leftArmTwistMuscle"),
                LeftForearmStretchMuscle = ReadFloat(values, columns, "leftForearmStretchMuscle"),
                LeftForearmTwistMuscle = ReadFloat(values, columns, "leftForearmTwistMuscle"),
                RightShoulderDownUpMuscle = ReadFloat(values, columns, "rightShoulderDownUpMuscle"),
                RightShoulderFrontBackMuscle = ReadFloat(values, columns, "rightShoulderFrontBackMuscle"),
                RightArmDownUpMuscle = ReadFloat(values, columns, "rightArmDownUpMuscle"),
                RightArmFrontBackMuscle = ReadFloat(values, columns, "rightArmFrontBackMuscle"),
                RightArmTwistMuscle = ReadFloat(values, columns, "rightArmTwistMuscle"),
                RightForearmStretchMuscle = ReadFloat(values, columns, "rightForearmStretchMuscle"),
                RightForearmTwistMuscle = ReadFloat(values, columns, "rightForearmTwistMuscle"),
                LeftElbowAngle = ReadFloat(values, columns, "leftElbowAngle"),
                RightElbowAngle = ReadFloat(values, columns, "rightElbowAngle"),
                LeftKneeAngle = ReadFloat(values, columns, "leftKneeAngle"),
                RightKneeAngle = ReadFloat(values, columns, "rightKneeAngle"),
                LeftHandHorizontalRatio = ReadFloat(values, columns, "leftHandHorizontalRatio"),
                RightHandHorizontalRatio = ReadFloat(values, columns, "rightHandHorizontalRatio"),
                LeftHandBelowShoulderRatio = ReadFloat(values, columns, "leftHandBelowShoulderRatio"),
                RightHandBelowShoulderRatio = ReadFloat(values, columns, "rightHandBelowShoulderRatio"),
                LeftHandTorsoSignedClearance = ReadFloat(values, columns, "leftHandTorsoSignedClearance"),
                RightHandTorsoSignedClearance = ReadFloat(values, columns, "rightHandTorsoSignedClearance"),
                YybMaxDeformationRisk = ReadFloat(values, columns, "yybMaxDeformationRisk"),
                LeftSleeveThicknessRisk = ReadFloat(values, columns, "leftSleeveThicknessRisk"),
                RightSleeveThicknessRisk = ReadFloat(values, columns, "rightSleeveThicknessRisk")
            };
            data.Frames[recorderFrame] = frame;
        }

        data.Recalculate();
        return data;
    }

    private static VmdQualityMetrics ReadVmdQualityMetrics(string path)
    {
        VmdQualityMetrics metrics = new VmdQualityMetrics
        {
            MaxBoneFrameIndex = -1,
            MaxCenterStep = float.NaN,
            MaxFootIkStep = float.NaN,
            MinFootIkY = float.NaN,
            MinEffectiveFootIkY = float.NaN
        };
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return metrics;
        }

        byte[] bytes = File.ReadAllBytes(path);
        const int headerLength = 30 + 20;
        const int countLength = 4;
        const int boneFrameSize = 111;
        if (bytes.Length < headerLength + countLength)
        {
            return metrics;
        }

        uint boneFrameCount = BitConverter.ToUInt32(bytes, headerLength);
        int offset = headerLength + countLength;
        Dictionary<string, List<VmdPositionFrame>> centerFrames = new Dictionary<string, List<VmdPositionFrame>>(StringComparer.Ordinal);
        Dictionary<string, List<VmdPositionFrame>> footIkFrames = new Dictionary<string, List<VmdPositionFrame>>(StringComparer.Ordinal);
        Dictionary<string, List<VmdPositionFrame>> footIkFramesBySide = new Dictionary<string, List<VmdPositionFrame>>(StringComparer.Ordinal);
        Dictionary<string, List<VmdPositionFrame>> toeIkFramesBySide = new Dictionary<string, List<VmdPositionFrame>>(StringComparer.Ordinal);
        for (uint index = 0; index < boneFrameCount && offset + boneFrameSize <= bytes.Length; index++, offset += boneFrameSize)
        {
            string boneName = ReadPaddedShiftJis(bytes, offset, 15);
            uint frame = BitConverter.ToUInt32(bytes, offset + 15);
            if (frame <= int.MaxValue)
            {
                metrics.MaxBoneFrameIndex = Math.Max(metrics.MaxBoneFrameIndex, (int)frame);
            }

            float x = BitConverter.ToSingle(bytes, offset + 19);
            float y = BitConverter.ToSingle(bytes, offset + 23);
            float z = BitConverter.ToSingle(bytes, offset + 27);
            VmdPositionFrame position = new VmdPositionFrame(frame, x, y, z);

            if (IsCenterCarrierBoneName(boneName))
            {
                AddVmdFrame(centerFrames, boneName, position);
            }

            if (IsFootIkBoneName(boneName))
            {
                AddVmdFrame(footIkFrames, boneName, position);
                metrics.MinFootIkY = MinFinite(metrics.MinFootIkY, y);
                if (TryGetFootIkSide(boneName, out string footSide))
                {
                    AddVmdFrame(footIkFramesBySide, footSide, position);
                }
                else if (TryGetToeIkSide(boneName, out string toeSide))
                {
                    AddVmdFrame(toeIkFramesBySide, toeSide, position);
                }
            }
        }

        List<VmdIkStateFrame> ikStateFrames = ReadVmdIkStateFrames(bytes, offset);
        metrics.BoneFrameCount = (int)Math.Min(boneFrameCount, int.MaxValue);
        metrics.MaxCenterStep = CalculateMaxVmdStep(centerFrames, out metrics.CenterSpikeFrameCount);
        metrics.MaxFootIkStep = CalculateMaxVmdStep(
            footIkFrames,
            out metrics.FootIkSpikeFrameCount,
            (boneName, frameIndex) => IsVmdIkEnabledAtFrame(ikStateFrames, boneName, frameIndex));
        metrics.MinEffectiveFootIkY = CalculateMinEffectiveFootIkY(centerFrames, footIkFramesBySide, toeIkFramesBySide);
        return metrics;
    }

    private static float CalculateMinEffectiveFootIkY(
        Dictionary<string, List<VmdPositionFrame>> centerFrames,
        Dictionary<string, List<VmdPositionFrame>> footIkFramesBySide,
        Dictionary<string, List<VmdPositionFrame>> toeIkFramesBySide)
    {
        Dictionary<uint, float> centerYByFrame = BuildCenterYByFrame(centerFrames);
        Dictionary<string, Dictionary<uint, float>> footYBySideFrame = BuildSideFrameYLookup(footIkFramesBySide);
        float minEffectiveY = float.NaN;

        foreach (KeyValuePair<string, List<VmdPositionFrame>> sideFrames in footIkFramesBySide)
        {
            foreach (VmdPositionFrame frame in sideFrames.Value)
            {
                float centerY = TryGetFrameY(centerYByFrame, frame.Frame, out float value) ? value : 0f;
                minEffectiveY = MinFinite(minEffectiveY, centerY + frame.Y);
            }
        }

        foreach (KeyValuePair<string, List<VmdPositionFrame>> sideFrames in toeIkFramesBySide)
        {
            footYBySideFrame.TryGetValue(sideFrames.Key, out Dictionary<uint, float> footYByFrame);
            foreach (VmdPositionFrame frame in sideFrames.Value)
            {
                float centerY = TryGetFrameY(centerYByFrame, frame.Frame, out float value) ? value : 0f;
                float footY = footYByFrame != null && TryGetFrameY(footYByFrame, frame.Frame, out float footValue)
                    ? footValue
                    : 0f;
                minEffectiveY = MinFinite(minEffectiveY, centerY + footY + frame.Y);
            }
        }

        return minEffectiveY;
    }

    private static Dictionary<uint, float> BuildCenterYByFrame(Dictionary<string, List<VmdPositionFrame>> centerFrames)
    {
        Dictionary<uint, float> result = new Dictionary<uint, float>();
        foreach (List<VmdPositionFrame> frames in centerFrames.Values)
        {
            foreach (VmdPositionFrame frame in frames)
            {
                if (!IsFinite(frame.Y))
                {
                    continue;
                }

                result[frame.Frame] = (result.TryGetValue(frame.Frame, out float current) ? current : 0f) + frame.Y;
            }
        }

        return result;
    }

    private static Dictionary<string, Dictionary<uint, float>> BuildSideFrameYLookup(Dictionary<string, List<VmdPositionFrame>> framesBySide)
    {
        Dictionary<string, Dictionary<uint, float>> result = new Dictionary<string, Dictionary<uint, float>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<VmdPositionFrame>> sideFrames in framesBySide)
        {
            Dictionary<uint, float> byFrame = new Dictionary<uint, float>();
            foreach (VmdPositionFrame frame in sideFrames.Value)
            {
                if (IsFinite(frame.Y))
                {
                    byFrame[frame.Frame] = frame.Y;
                }
            }

            result[sideFrames.Key] = byFrame;
        }

        return result;
    }

    private static bool TryGetFrameY(Dictionary<uint, float> frames, uint frame, out float y)
    {
        y = 0f;
        return frames != null && frames.TryGetValue(frame, out y) && IsFinite(y);
    }

    private static void AddVmdFrame(Dictionary<string, List<VmdPositionFrame>> framesByBone, string boneName, VmdPositionFrame frame)
    {
        string key = boneName ?? "";
        if (!framesByBone.TryGetValue(key, out List<VmdPositionFrame> frames))
        {
            frames = new List<VmdPositionFrame>();
            framesByBone[key] = frames;
        }

        frames.Add(frame);
    }

    private static float CalculateMaxVmdStep(
        Dictionary<string, List<VmdPositionFrame>> framesByBone,
        out int spikeFrameCount,
        Func<string, uint, bool> shouldCountSpike = null)
    {
        spikeFrameCount = 0;
        float maxStep = float.NaN;
        foreach (KeyValuePair<string, List<VmdPositionFrame>> framesByBoneEntry in framesByBone)
        {
            List<VmdPositionFrame> frames = framesByBoneEntry.Value;
            frames.Sort((left, right) => left.Frame.CompareTo(right.Frame));
            for (int i = 1; i < frames.Count; i++)
            {
                float step = Distance(
                    frames[i - 1].X,
                    frames[i - 1].Y,
                    frames[i - 1].Z,
                    frames[i].X,
                    frames[i].Y,
                    frames[i].Z);
                maxStep = MaxFinite(maxStep, step);
                if (IsTeleportStep(step) &&
                    (shouldCountSpike == null || shouldCountSpike(framesByBoneEntry.Key, frames[i].Frame)))
                {
                    spikeFrameCount++;
                }
            }
        }

        return maxStep;
    }

    private static List<VmdIkStateFrame> ReadVmdIkStateFrames(byte[] bytes, int offset)
    {
        List<VmdIkStateFrame> frames = new List<VmdIkStateFrame>();
        if (bytes == null || offset < 0 || offset >= bytes.Length)
        {
            return frames;
        }

        if (!TrySkipVmdSection(bytes, ref offset, 23) ||
            !TrySkipVmdSection(bytes, ref offset, 61) ||
            !TrySkipVmdSection(bytes, ref offset, 28) ||
            !TrySkipVmdSection(bytes, ref offset, 9) ||
            !TryReadUInt32(bytes, ref offset, out uint displayFrameCount))
        {
            return frames;
        }

        for (uint displayIndex = 0; displayIndex < displayFrameCount; displayIndex++)
        {
            if (!TryReadUInt32(bytes, ref offset, out uint frameIndex) ||
                !TryReadByte(bytes, ref offset, out _) ||
                !TryReadUInt32(bytes, ref offset, out uint ikCount))
            {
                return frames;
            }

            bool leftFootEnabled = true;
            bool leftToeEnabled = true;
            bool rightFootEnabled = true;
            bool rightToeEnabled = true;
            for (uint ikIndex = 0; ikIndex < ikCount; ikIndex++)
            {
                if (offset + 21 > bytes.Length)
                {
                    return frames;
                }

                string ikName = ReadPaddedShiftJis(bytes, offset, 20);
                offset += 20;
                if (!TryReadByte(bytes, ref offset, out byte enabledByte))
                {
                    return frames;
                }

                bool enabled = enabledByte != 0;
                if (string.Equals(ikName, "\u5de6\u8db3\uff29\uff2b", StringComparison.Ordinal) ||
                    string.Equals(ikName, "LeftFootIK", StringComparison.Ordinal))
                {
                    leftFootEnabled = enabled;
                }
                else if (string.Equals(ikName, "\u5de6\u3064\u307e\u5148\uff29\uff2b", StringComparison.Ordinal) ||
                    string.Equals(ikName, "LeftToeIK", StringComparison.Ordinal))
                {
                    leftToeEnabled = enabled;
                }
                else if (string.Equals(ikName, "\u53f3\u8db3\uff29\uff2b", StringComparison.Ordinal) ||
                    string.Equals(ikName, "RightFootIK", StringComparison.Ordinal))
                {
                    rightFootEnabled = enabled;
                }
                else if (string.Equals(ikName, "\u53f3\u3064\u307e\u5148\uff29\uff2b", StringComparison.Ordinal) ||
                    string.Equals(ikName, "RightToeIK", StringComparison.Ordinal))
                {
                    rightToeEnabled = enabled;
                }
            }

            frames.Add(new VmdIkStateFrame(
                frameIndex,
                leftFootEnabled,
                leftToeEnabled,
                rightFootEnabled,
                rightToeEnabled));
        }

        frames.Sort((left, right) => left.Frame.CompareTo(right.Frame));
        return frames;
    }

    private static bool TrySkipVmdSection(byte[] bytes, ref int offset, int bytesPerFrame)
    {
        if (!TryReadUInt32(bytes, ref offset, out uint frameCount))
        {
            return false;
        }

        long nextOffset = (long)offset + (long)frameCount * bytesPerFrame;
        if (nextOffset > bytes.Length || nextOffset > int.MaxValue)
        {
            return false;
        }

        offset = (int)nextOffset;
        return true;
    }

    private static bool TryResolveHorizontalFootPostprocessCorrection(
        MotionComparisonFrameQualitySummary summary,
        float normalizedFootDeltaX,
        float normalizedFootDeltaZ,
        int candidateRecorderFrame,
        out float correctionX,
        out float correctionZ)
    {
        correctionX = 0f;
        correctionZ = 0f;
        if (!IsFinite(normalizedFootDeltaX) ||
            !IsFinite(normalizedFootDeltaZ) ||
            !TryGetCandidateVmdFrameRangeBucket(summary, candidateRecorderFrame, out bool withinFrameRange) ||
            !withinFrameRange)
        {
            return false;
        }

        float magnitude = Distance(0f, 0f, 0f, normalizedFootDeltaX, 0f, normalizedFootDeltaZ);
        float targetMagnitude = ResolveHorizontalFootPostprocessTargetMagnitude();
        if (!IsFinite(magnitude) || magnitude <= targetMagnitude || magnitude <= 0f)
        {
            return false;
        }

        float scale = targetMagnitude / magnitude;
        correctionX = (normalizedFootDeltaX * scale) - normalizedFootDeltaX;
        correctionZ = (normalizedFootDeltaZ * scale) - normalizedFootDeltaZ;
        return HasNonZeroCorrection(correctionX) || HasNonZeroCorrection(correctionZ);
    }

    private static float ResolveHorizontalFootPostprocessTargetMagnitude()
    {
        return Mathf.Max(0f, QualitySameFrameFootXzWarnThreshold - HorizontalFootSolvePostprocessSafetyMarginXZ);
    }

    private static bool HasNonZeroCorrection(float value)
    {
        return IsFinite(value) && Math.Abs(value) > 0.000001f;
    }

    private static bool TryReadUInt32(byte[] bytes, ref int offset, out uint value)
    {
        value = 0;
        if (bytes == null || offset < 0 || offset + 4 > bytes.Length)
        {
            return false;
        }

        value = BitConverter.ToUInt32(bytes, offset);
        offset += 4;
        return true;
    }

    private static bool TryReadByte(byte[] bytes, ref int offset, out byte value)
    {
        value = 0;
        if (bytes == null || offset < 0 || offset >= bytes.Length)
        {
            return false;
        }

        value = bytes[offset];
        offset++;
        return true;
    }

    private static bool IsVmdIkEnabledAtFrame(
        List<VmdIkStateFrame> ikStateFrames,
        string boneName,
        uint frameIndex)
    {
        if (ikStateFrames == null || ikStateFrames.Count == 0)
        {
            return true;
        }

        VmdIkStateFrame active = default;
        bool hasActive = false;
        foreach (VmdIkStateFrame stateFrame in ikStateFrames)
        {
            if (stateFrame.Frame > frameIndex)
            {
                break;
            }

            active = stateFrame;
            hasActive = true;
        }

        if (!hasActive)
        {
            return true;
        }

        if (TryGetFootIkSide(boneName, out string footSide))
        {
            return string.Equals(footSide, "left", StringComparison.Ordinal)
                ? active.LeftFootEnabled
                : active.RightFootEnabled;
        }

        if (TryGetToeIkSide(boneName, out string toeSide))
        {
            return string.Equals(toeSide, "left", StringComparison.Ordinal)
                ? active.LeftToeEnabled
                : active.RightToeEnabled;
        }

        return true;
    }

    private static Dictionary<string, int> BuildColumnLookup(string[] headers)
    {
        Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            string header = headers[i] ?? "";
            if (!columns.ContainsKey(header))
            {
                columns.Add(header, i);
            }
        }

        return columns;
    }

    private static string[] SplitCsvLine(string line)
    {
        List<string> values = new List<string>();
        StringBuilder current = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < (line?.Length ?? 0); i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private static bool TryReadInt(string[] values, Dictionary<string, int> columns, string columnName, out int value)
    {
        value = 0;
        if (!columns.TryGetValue(columnName, out int index) || index < 0 || index >= values.Length)
        {
            return false;
        }

        return int.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static int ReadInt(string[] values, Dictionary<string, int> columns, string columnName, int fallback)
    {
        return TryReadInt(values, columns, columnName, out int value) ? value : fallback;
    }

    private static float ReadFloat(string[] values, Dictionary<string, int> columns, string columnName)
    {
        if (!columns.TryGetValue(columnName, out int index) || index < 0 || index >= values.Length)
        {
            return float.NaN;
        }

        return float.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : float.NaN;
    }

    private static string ReadString(string[] values, Dictionary<string, int> columns, string columnName)
    {
        if (!columns.TryGetValue(columnName, out int index) || index < 0 || index >= values.Length)
        {
            return "";
        }

        return values[index] ?? "";
    }

    private static string ReadPaddedShiftJis(byte[] bytes, int offset, int length)
    {
        int end = offset;
        int maxEnd = Math.Min(bytes.Length, offset + length);
        while (end < maxEnd && bytes[end] != 0)
        {
            end++;
        }

        if (end <= offset)
        {
            return "";
        }

        try
        {
            return Encoding.GetEncoding("shift_jis").GetString(bytes, offset, end - offset);
        }
        catch
        {
            return Encoding.UTF8.GetString(bytes, offset, end - offset);
        }
    }

    private static bool IsCenterCarrierBoneName(string boneName)
    {
        return string.Equals(boneName, "Center", StringComparison.Ordinal) ||
            string.Equals(boneName, "Groove", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u30bb\u30f3\u30bf\u30fc", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u30b0\u30eb\u30fc\u30d6", StringComparison.Ordinal);
    }

    private static bool IsFootIkBoneName(string boneName)
    {
        return string.Equals(boneName, "LeftFootIK", StringComparison.Ordinal) ||
            string.Equals(boneName, "RightFootIK", StringComparison.Ordinal) ||
            string.Equals(boneName, "LeftToeIK", StringComparison.Ordinal) ||
            string.Equals(boneName, "RightToeIK", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u5de6\u8db3\uff29\uff2b", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u53f3\u8db3\uff29\uff2b", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u5de6\u3064\u307e\u5148\uff29\uff2b", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u53f3\u3064\u307e\u5148\uff29\uff2b", StringComparison.Ordinal);
    }

    private static bool IsFootIkCarrierBoneName(string boneName)
    {
        return string.Equals(boneName, "LeftFootIK", StringComparison.Ordinal) ||
            string.Equals(boneName, "RightFootIK", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u5de6\u8db3\uff29\uff2b", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u53f3\u8db3\uff29\uff2b", StringComparison.Ordinal);
    }

    private static bool TryGetFootIkSide(string boneName, out string side)
    {
        if (string.Equals(boneName, "LeftFootIK", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u5de6\u8db3\uff29\uff2b", StringComparison.Ordinal))
        {
            side = "left";
            return true;
        }

        if (string.Equals(boneName, "RightFootIK", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u53f3\u8db3\uff29\uff2b", StringComparison.Ordinal))
        {
            side = "right";
            return true;
        }

        side = "";
        return false;
    }

    private static bool TryGetToeIkSide(string boneName, out string side)
    {
        if (string.Equals(boneName, "LeftToeIK", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u5de6\u3064\u307e\u5148\uff29\uff2b", StringComparison.Ordinal))
        {
            side = "left";
            return true;
        }

        if (string.Equals(boneName, "RightToeIK", StringComparison.Ordinal) ||
            string.Equals(boneName, "\u53f3\u3064\u307e\u5148\uff29\uff2b", StringComparison.Ordinal))
        {
            side = "right";
            return true;
        }

        side = "";
        return false;
    }

    private static float Distance(
        float ax,
        float ay,
        float az,
        float bx,
        float by,
        float bz)
    {
        if (!IsFinite(ax) || !IsFinite(ay) || !IsFinite(az) || !IsFinite(bx) || !IsFinite(by) || !IsFinite(bz))
        {
            return float.NaN;
        }

        double x = bx - ax;
        double y = by - ay;
        double z = bz - az;
        return (float)Math.Sqrt((x * x) + (y * y) + (z * z));
    }

    private static float AbsDelta(float a, float b)
    {
        return IsFinite(a) && IsFinite(b) ? Math.Abs(a - b) : float.NaN;
    }

    private static float MaxFinite(float current, float candidate)
    {
        if (!IsFinite(candidate))
        {
            return current;
        }

        return !IsFinite(current) || candidate > current ? candidate : current;
    }

    private static void UpdateMaxFiniteWithFrame(
        float candidate,
        int baselineRecorderFrame,
        int candidateRecorderFrame,
        ref float current,
        ref int currentBaselineRecorderFrame,
        ref int currentCandidateRecorderFrame)
    {
        if (!IsFinite(candidate))
        {
            return;
        }

        if (!IsFinite(current) || candidate > current)
        {
            current = candidate;
            currentBaselineRecorderFrame = baselineRecorderFrame;
            currentCandidateRecorderFrame = candidateRecorderFrame;
        }
    }

    private static void UpdateMaxFiniteWithFrameAndSource(
        float candidate,
        int baselineRecorderFrame,
        int candidateRecorderFrame,
        string source,
        ref float current,
        ref int currentBaselineRecorderFrame,
        ref int currentCandidateRecorderFrame,
        ref string currentSource)
    {
        if (!IsFinite(candidate))
        {
            return;
        }

        if (!IsFinite(current) || candidate > current)
        {
            current = candidate;
            currentBaselineRecorderFrame = baselineRecorderFrame;
            currentCandidateRecorderFrame = candidateRecorderFrame;
            currentSource = source ?? "";
        }
    }

    private static void UpdateMaxFootXzDelta(
        MotionComparisonFrameQualitySummary summary,
        string side,
        float candidate,
        int baselineRecorderFrame,
        int candidateRecorderFrame)
    {
        if (summary == null || !IsFinite(candidate))
        {
            return;
        }

        if (!IsFinite(summary.max_same_frame_foot_xz_delta) ||
            candidate > summary.max_same_frame_foot_xz_delta)
        {
            summary.max_same_frame_foot_xz_delta = candidate;
            summary.max_same_frame_foot_xz_delta_recorder_frame = baselineRecorderFrame;
            summary.max_same_frame_foot_xz_delta_candidate_recorder_frame = candidateRecorderFrame;
            summary.max_same_frame_foot_xz_delta_side = side ?? "";
        }

        UpdateMaxFootXzDeltaByVmdFrameRange(
            summary,
            side,
            candidate,
            baselineRecorderFrame,
            candidateRecorderFrame);
    }

    private static void UpdateMaxFootXzDeltaByVmdFrameRange(
        MotionComparisonFrameQualitySummary summary,
        string side,
        float candidate,
        int baselineRecorderFrame,
        int candidateRecorderFrame)
    {
        if (!TryGetCandidateVmdFrameRangeBucket(summary, candidateRecorderFrame, out bool withinFrameRange))
        {
            return;
        }

        if (withinFrameRange)
        {
            UpdateMaxFootXzDeltaRangeFields(
                side,
                candidate,
                baselineRecorderFrame,
                candidateRecorderFrame,
                ref summary.max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range,
                ref summary.max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range_recorder_frame,
                ref summary.max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range_candidate_recorder_frame,
                ref summary.max_same_frame_foot_xz_delta_within_candidate_vmd_frame_range_side);
            return;
        }

        UpdateMaxFootXzDeltaRangeFields(
            side,
            candidate,
            baselineRecorderFrame,
            candidateRecorderFrame,
            ref summary.max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range,
            ref summary.max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range_recorder_frame,
            ref summary.max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range_candidate_recorder_frame,
            ref summary.max_same_frame_foot_xz_delta_outside_candidate_vmd_frame_range_side);
    }

    private static void UpdateMaxFootXzDeltaRangeFields(
        string side,
        float candidate,
        int baselineRecorderFrame,
        int candidateRecorderFrame,
        ref float current,
        ref int currentBaselineRecorderFrame,
        ref int currentCandidateRecorderFrame,
        ref string currentSide)
    {
        if (!IsFinite(candidate))
        {
            return;
        }

        if (!IsFinite(current) || candidate > current)
        {
            current = candidate;
            currentBaselineRecorderFrame = baselineRecorderFrame;
            currentCandidateRecorderFrame = candidateRecorderFrame;
            currentSide = side ?? "";
        }
    }

    private static bool TryGetCandidateVmdFrameRangeBucket(
        MotionComparisonFrameQualitySummary summary,
        int candidateRecorderFrame,
        out bool withinFrameRange)
    {
        withinFrameRange = false;
        if (summary == null ||
            summary.candidate_vmd_max_bone_frame_index < 0 ||
            candidateRecorderFrame < 0)
        {
            return false;
        }

        withinFrameRange = candidateRecorderFrame <= summary.candidate_vmd_max_bone_frame_index;
        return true;
    }

    private static void UpdateMaxHipsXzDelta(
        MotionComparisonFrameQualitySummary summary,
        float candidate,
        int baselineRecorderFrame,
        int candidateRecorderFrame)
    {
        if (summary == null || !IsFinite(candidate))
        {
            return;
        }

        if (!IsFinite(summary.max_same_frame_hips_xz_delta) ||
            candidate > summary.max_same_frame_hips_xz_delta)
        {
            summary.max_same_frame_hips_xz_delta = candidate;
            summary.max_same_frame_hips_xz_delta_recorder_frame = baselineRecorderFrame;
            summary.max_same_frame_hips_xz_delta_candidate_recorder_frame = candidateRecorderFrame;
        }
    }

    private static void UpdateMaxHipsAlignedFootXzDelta(
        MotionComparisonFrameQualitySummary summary,
        string side,
        float normalizedFootDeltaX,
        float normalizedFootDeltaZ,
        float normalizedHipsDeltaX,
        float normalizedHipsDeltaZ,
        int baselineRecorderFrame,
        int candidateRecorderFrame)
    {
        if (summary == null ||
            !IsFinite(normalizedFootDeltaX) ||
            !IsFinite(normalizedFootDeltaZ) ||
            !IsFinite(normalizedHipsDeltaX) ||
            !IsFinite(normalizedHipsDeltaZ))
        {
            return;
        }

        float residualX = normalizedFootDeltaX - normalizedHipsDeltaX;
        float residualZ = normalizedFootDeltaZ - normalizedHipsDeltaZ;
        float candidate = Distance(
            0f,
            0f,
            0f,
            residualX,
            0f,
            residualZ);
        if (!IsFinite(summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment) ||
            candidate > summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment)
        {
            summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment = candidate;
            summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_x = residualX;
            summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_z = residualZ;
            summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_angle_degrees =
                Mathf.Atan2(residualZ, residualX) * Mathf.Rad2Deg;
            summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_recorder_frame = baselineRecorderFrame;
            summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_candidate_recorder_frame = candidateRecorderFrame;
            summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_side = side ?? "";
        }

        UpdateMaxHipsAlignedFootXzDeltaByVmdFrameRange(
            summary,
            side,
            candidate,
            baselineRecorderFrame,
            candidateRecorderFrame);
    }

    private static void UpdateMaxHipsAlignedFootXzDeltaByVmdFrameRange(
        MotionComparisonFrameQualitySummary summary,
        string side,
        float candidate,
        int baselineRecorderFrame,
        int candidateRecorderFrame)
    {
        if (!TryGetCandidateVmdFrameRangeBucket(summary, candidateRecorderFrame, out bool withinFrameRange))
        {
            return;
        }

        if (withinFrameRange)
        {
            UpdateMaxFootXzDeltaRangeFields(
                side,
                candidate,
                baselineRecorderFrame,
                candidateRecorderFrame,
                ref summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range,
                ref summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range_recorder_frame,
                ref summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range_candidate_recorder_frame,
                ref summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_within_candidate_vmd_frame_range_side);
            return;
        }

        UpdateMaxFootXzDeltaRangeFields(
            side,
            candidate,
            baselineRecorderFrame,
            candidateRecorderFrame,
            ref summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range,
            ref summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range_recorder_frame,
            ref summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range_candidate_recorder_frame,
            ref summary.max_same_frame_foot_xz_delta_after_hips_xz_alignment_outside_candidate_vmd_frame_range_side);
    }

    private static void UpdateOffsetNormalizedDelta(
        float baselineValue,
        float candidateValue,
        ref bool hasOffset,
        ref float offset,
        ref float maxDelta)
    {
        if (!IsFinite(baselineValue) || !IsFinite(candidateValue))
        {
            return;
        }

        float delta = candidateValue - baselineValue;
        if (!hasOffset)
        {
            offset = delta;
            hasOffset = true;
        }

        maxDelta = MaxFinite(maxDelta, Math.Abs(delta - offset));
    }

    private static void UpdateSingleOffsetNormalizedDelta(
        float value,
        ref bool hasOffset,
        ref float offset,
        ref float maxDelta)
    {
        if (!IsFinite(value))
        {
            return;
        }

        if (!hasOffset)
        {
            offset = value;
            hasOffset = true;
        }

        maxDelta = MaxFinite(maxDelta, Math.Abs(value - offset));
    }

    private static float ResolveBoundedVerticalSolveCorrection(
        float normalizedDelta,
        float targetThreshold,
        float maxCorrectionMagnitude)
    {
        if (!IsFinite(normalizedDelta) || !IsFinite(targetThreshold) || !IsFinite(maxCorrectionMagnitude) ||
            targetThreshold < 0f || maxCorrectionMagnitude < 0f)
        {
            return float.NaN;
        }

        float magnitude = Math.Abs(normalizedDelta);
        float excess = magnitude - targetThreshold;
        if (excess <= 0f)
        {
            return 0f;
        }

        float correctionMagnitude = Math.Min(excess, maxCorrectionMagnitude);
        return normalizedDelta > 0f ? -correctionMagnitude : correctionMagnitude;
    }

    private static float ClampFootVerticalSolveCorrectionToFloor(
        float correction,
        MetricsCsvFrame candidateFrame,
        float maxCorrectionMagnitude)
    {
        if (!IsFinite(correction))
        {
            return float.NaN;
        }

        float maxMagnitude = IsFinite(maxCorrectionMagnitude)
            ? Math.Max(0f, maxCorrectionMagnitude)
            : float.PositiveInfinity;
        if (correction >= 0f)
        {
            float floorLift = ResolveFootVerticalSolveFloorLift(candidateFrame);
            float floorSafeCorrection = IsFinite(floorLift)
                ? Math.Max(correction, floorLift)
                : correction;
            return Math.Min(floorSafeCorrection, maxMagnitude);
        }

        float maxLowering = float.PositiveInfinity;
        if (IsFinite(candidateFrame.LowestFootBottomY))
        {
            maxLowering = Math.Min(maxLowering, candidateFrame.LowestFootBottomY - QualityFloorTolerance);
        }

        if (IsFinite(candidateFrame.FootBottomGroundGap))
        {
            maxLowering = Math.Min(maxLowering, candidateFrame.FootBottomGroundGap - QualityFloorTolerance);
        }

        if (!IsFinite(maxLowering))
        {
            return correction;
        }

        float allowedLowering = Math.Max(0f, maxLowering);
        return Math.Max(correction, -allowedLowering);
    }

    private static float ResolveFootVerticalSolveFloorLift(MetricsCsvFrame candidateFrame)
    {
        float minFootFloorValue = float.NaN;
        if (IsFinite(candidateFrame.LowestFootBottomY))
        {
            minFootFloorValue = candidateFrame.LowestFootBottomY;
        }

        if (IsFinite(candidateFrame.FootBottomGroundGap))
        {
            minFootFloorValue = IsFinite(minFootFloorValue)
                ? Math.Min(minFootFloorValue, candidateFrame.FootBottomGroundGap)
                : candidateFrame.FootBottomGroundGap;
        }

        if (!IsFinite(minFootFloorValue))
        {
            return float.NaN;
        }

        return Math.Max(0f, ResolveVerticalSolveFloorSafeY() - minFootFloorValue);
    }

    private static void UpdatePrototypeCorrection(
        float correction,
        int baselineRecorderFrame,
        int candidateRecorderFrame,
        ref float maxCorrectionMagnitude,
        ref float maxCorrection,
        ref int maxCorrectionBaselineRecorderFrame,
        ref int maxCorrectionCandidateRecorderFrame)
    {
        if (!IsFinite(correction))
        {
            return;
        }

        float magnitude = Math.Abs(correction);
        if (magnitude <= 0f)
        {
            return;
        }

        if (!IsFinite(maxCorrectionMagnitude) || magnitude > maxCorrectionMagnitude)
        {
            maxCorrectionMagnitude = magnitude;
            maxCorrection = correction;
            maxCorrectionBaselineRecorderFrame = baselineRecorderFrame;
            maxCorrectionCandidateRecorderFrame = candidateRecorderFrame;
        }
    }

    private static float MinFinite(float current, float candidate)
    {
        if (!IsFinite(candidate))
        {
            return current;
        }

        return !IsFinite(current) || candidate < current ? candidate : current;
    }

    private static bool IsBelowFloor(float value)
    {
        return IsFinite(value) && value < QualityFloorTolerance;
    }

    private static bool IsTeleportStep(float value)
    {
        return IsFinite(value) && value > QualityTeleportStepThreshold;
    }

    private static bool ExceedsThreshold(float value, float threshold)
    {
        return IsFinite(value) && value > threshold;
    }

    internal static string FormatSampleTimes(float[] sampleTimes)
    {
        if (sampleTimes == null || sampleTimes.Length == 0)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < sampleTimes.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(sampleTimes[i].ToString("0.###", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    internal static string BuildSessionStartedReason()
    {
        return "started";
    }

    internal static string BuildSamplingStartReason()
    {
        return "start";
    }

    internal static string BuildSamplingStopReason()
    {
        return "stop";
    }

    internal static string BuildSamplingDefaultReason()
    {
        return "sample";
    }

    internal static string BuildSamplingDisabledReason()
    {
        return "disabled";
    }

    internal static string BuildRealtimeRiskEvaluationReason()
    {
        return "realtime";
    }

    internal static string BuildSampleTimeReason(float sampleTime)
    {
        return $"t{sampleTime.ToString("0.###", CultureInfo.InvariantCulture)}";
    }

    internal static string BuildSampleLogMessage(
        string comparisonLabel,
        string reason,
        float elapsed,
        float animationClipTime,
        int recorderFrame,
        float hipsY,
        float cameraFacingDot,
        float maxScaleDelta,
        float yybRisk)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[MotionComparisonProbe] {0} {1} t={2:F2}s clip={3:F3}s frame={4} hipsY={5:F3} facing={6:F3} scaleDelta={7:F4} yybRisk={8:F3}",
            comparisonLabel ?? "",
            reason ?? "",
            elapsed,
            animationClipTime,
            recorderFrame,
            hipsY,
            cameraFacingDot,
            maxScaleDelta,
            yybRisk);
    }

    internal static string BuildScreenshotBoundsUnavailableWarningMessage(string comparisonLabel, string reason)
    {
        return $"[MotionComparisonProbe] screenshot skipped: render bounds unavailable label={comparisonLabel ?? ""} reason={reason ?? ""}";
    }

    internal static string BuildScreenshotBlankWarningMessage(string path)
    {
        return $"[MotionComparisonProbe] screenshot render produced blank/no evidence: {path ?? ""}";
    }

    internal static string BuildSampleClockLabel(bool sampleByAnimationClipTime)
    {
        return sampleByAnimationClipTime ? "animationClipTime" : "elapsed";
    }

    internal static string BuildSessionStamp(DateTime timestamp)
    {
        return timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    }

    internal static string BuildSessionUpdatedAt(DateTime timestamp)
    {
        return timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public static void WriteSessionManifestMarkdown(string filePath, MotionComparisonProbeSessionManifestData data)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        EnsureParentDirectoryExists(filePath);
        File.WriteAllText(filePath, BuildSessionManifestMarkdown(data), Encoding.UTF8);
    }

    internal static string BuildExportedVmdArtifactRow(string vmdRelativePath, int frameCount, long fileSizeBytes)
    {
        string rowSuffix = fileSizeBytes > 0 || frameCount > 0
            ? $" (frames={frameCount}, bytes={fileSizeBytes})"
            : string.Empty;
        return $"| exported vmd | `{EscapeMarkdown(vmdRelativePath)}`{rowSuffix} |";
    }

    public static void TryAppendExportedVmdToSessionManifest(
        string sessionManifestPath,
        string vmdRelativePath,
        int frameCount,
        long fileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(sessionManifestPath) ||
            string.IsNullOrWhiteSpace(vmdRelativePath) ||
            !File.Exists(sessionManifestPath))
        {
            return;
        }

        string artifactRow = BuildExportedVmdArtifactRow(vmdRelativePath, frameCount, fileSizeBytes);

        string[] lines = File.ReadAllLines(sessionManifestPath, Encoding.UTF8);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("| exported vmd |", StringComparison.Ordinal))
            {
                lines[i] = artifactRow;
                File.WriteAllLines(sessionManifestPath, lines, Encoding.UTF8);
                return;
            }
        }

        int artifactsHeadingIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.Equals(lines[i].Trim(), SessionManifestArtifactsHeading, StringComparison.Ordinal))
            {
                artifactsHeadingIndex = i;
                break;
            }
        }

        if (artifactsHeadingIndex < 0)
        {
            File.AppendAllText(
                sessionManifestPath,
                Environment.NewLine + SessionManifestArtifactsHeading + Environment.NewLine + Environment.NewLine +
                SessionManifestArtifactsTableHeader + Environment.NewLine +
                SessionManifestArtifactsTableSeparator + Environment.NewLine +
                artifactRow + Environment.NewLine,
                Encoding.UTF8);
            return;
        }

        int tableHeaderIndex = -1;
        for (int i = artifactsHeadingIndex + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith(SessionManifestArtifactsTableHeader, StringComparison.Ordinal))
            {
                tableHeaderIndex = i;
                break;
            }

            if (lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }
        }

        if (tableHeaderIndex < 0)
        {
            var repaired = new List<string>(lines.Length + 4);
            for (int i = 0; i <= artifactsHeadingIndex; i++)
            {
                repaired.Add(lines[i]);
            }

            repaired.Add("");
            repaired.Add(SessionManifestArtifactsTableHeader);
            repaired.Add(SessionManifestArtifactsTableSeparator);
            repaired.Add(artifactRow);
            for (int i = artifactsHeadingIndex + 1; i < lines.Length; i++)
            {
                if (i == artifactsHeadingIndex + 1 && string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                repaired.Add(lines[i]);
            }

            File.WriteAllLines(sessionManifestPath, repaired, Encoding.UTF8);
            return;
        }

        int insertIndex = lines.Length;
        for (int i = tableHeaderIndex + 2; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                insertIndex = i;
                break;
            }
        }

        var updated = new string[lines.Length + 1];
        Array.Copy(lines, 0, updated, 0, insertIndex);
        updated[insertIndex] = artifactRow;
        Array.Copy(lines, insertIndex, updated, insertIndex + 1, lines.Length - insertIndex);
        File.WriteAllLines(sessionManifestPath, updated, Encoding.UTF8);
    }

    internal static string BuildSessionManifestMarkdown(MotionComparisonProbeSessionManifestData data)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# MotionComparisonProbe 세션");
        builder.AppendLine();
        builder.AppendLine($"- session id: `{EscapeMarkdown(data.SessionId)}`");
        builder.AppendLine($"- label: `{EscapeMarkdown(data.ComparisonLabel)}`");
        builder.AppendLine($"- scene: `{EscapeMarkdown(data.SceneName)}`");
        builder.AppendLine($"- last state/reason: `{EscapeMarkdown(data.StateReason)}`");
        builder.AppendLine($"- created at: `{EscapeMarkdown(data.CreatedAt)}`");
        builder.AppendLine($"- updated at: `{EscapeMarkdown(data.UpdatedAt)}`");
        builder.AppendLine($"- screenshots enabled: `{data.ScreenshotsEnabled}`");
        builder.AppendLine($"- sample clock: `{data.SampleClock}`");
        builder.AppendLine($"- sample times: `{EscapeMarkdown(data.SampleTimes)}`");
        builder.AppendLine($"- yyb diagnostic only metrics: `{data.YybDiagnosticOnlyMetrics}`");
        builder.AppendLine();
        builder.AppendLine("## 엄지 리스크 요약");
        builder.AppendLine();
        builder.AppendLine($"- risk diagnostics enabled: `{data.YybDiagnosticOnlyMetrics}`");
        builder.AppendLine($"- risk evaluation frames: `{data.RiskEvaluationFrameCount}`");
        builder.AppendLine($"- left thumb core coverage frames: `{data.LeftThumbCoreCoverageFrameCount}`");
        builder.AppendLine($"- right thumb core coverage frames: `{data.RightThumbCoreCoverageFrameCount}`");
        builder.AppendLine($"- left thumb helper coverage required: `{data.LeftThumbHelperCoverageRequired}`");
        builder.AppendLine($"- right thumb helper coverage required: `{data.RightThumbHelperCoverageRequired}`");
        builder.AppendLine($"- left thumb helper coverage frames: `{data.LeftThumbHelperCoverageFrameCount}`");
        builder.AppendLine($"- right thumb helper coverage frames: `{data.RightThumbHelperCoverageFrameCount}`");
        builder.AppendLine($"- max generic thumb anatomy risk: `{FormatManifestFloat(data.MaxGenericThumbAnatomyRisk)}`");
        builder.AppendLine($"- max generic thumb anatomy risk reason: `{EscapeMarkdown(data.MaxGenericThumbAnatomyRiskReason)}`");
        builder.AppendLine($"- max generic thumb anatomy risk clip time: `{FormatManifestFloat(data.MaxGenericThumbAnatomyRiskClipTime)}`");
        builder.AppendLine($"- max generic thumb anatomy risk recorder frame: `{data.MaxGenericThumbAnatomyRiskRecorderFrame}`");
        builder.AppendLine($"- max thumb spread risk: `{FormatManifestFloat(data.MaxThumbSpreadRisk)}`");
        builder.AppendLine($"- max thumb projection risk: `{FormatManifestFloat(data.MaxThumbProjectionRisk)}`");
        builder.AppendLine($"- max thumb helper separation risk: `{FormatManifestFloat(data.MaxThumbHelperSeparationRisk)}`");
        builder.AppendLine($"- max thumb webbing risk: `{FormatManifestFloat(data.MaxThumbWebbingRisk)}`");
        builder.AppendLine($"- max yyb deformation risk: `{FormatManifestFloat(data.MaxYybDeformationRisk)}`");
        builder.AppendLine($"- max yyb deformation risk reason: `{EscapeMarkdown(data.MaxYybDeformationRiskReason)}`");
        builder.AppendLine($"- max yyb deformation risk clip time: `{FormatManifestFloat(data.MaxYybDeformationRiskClipTime)}`");
        builder.AppendLine($"- max yyb deformation risk recorder frame: `{data.MaxYybDeformationRiskRecorderFrame}`");
        builder.AppendLine($"- left thumb projection guard weight: `{FormatManifestFloat(data.LeftThumbProjectionGuardWeight)}`");
        builder.AppendLine($"- right thumb projection guard weight: `{FormatManifestFloat(data.RightThumbProjectionGuardWeight)}`");
        builder.AppendLine($"- left thumb index-spread guard weight: `{FormatManifestFloat(data.LeftThumbIndexSpreadGuardWeight)}`");
        builder.AppendLine($"- right thumb index-spread guard weight: `{FormatManifestFloat(data.RightThumbIndexSpreadGuardWeight)}`");
        builder.AppendLine($"- left thumb segment-straighten guard weight: `{FormatManifestFloat(data.LeftThumbSegmentStraightenGuardWeight)}`");
        builder.AppendLine($"- right thumb segment-straighten guard weight: `{FormatManifestFloat(data.RightThumbSegmentStraightenGuardWeight)}`");
        builder.AppendLine();
        builder.AppendLine(SessionManifestArtifactsHeading);
        builder.AppendLine();
        builder.AppendLine(SessionManifestArtifactsTableHeader);
        builder.AppendLine(SessionManifestArtifactsTableSeparator);
        builder.AppendLine($"| metrics csv | `{EscapeMarkdown(data.MetricsCsvRelativePath)}` |");
        builder.AppendLine($"| frame folder | `{EscapeMarkdown(data.FrameFolderRelativePath)}` |");
        builder.AppendLine($"| frame index csv | `{EscapeMarkdown(data.FrameIndexCsvRelativePath)}` |");
        builder.AppendLine($"| frame session index | `{EscapeMarkdown(data.FrameSessionIndexRelativePath)}` |");
        builder.AppendLine();
        builder.AppendLine("## 사용 방법");
        builder.AppendLine();
        builder.AppendLine("- 이 `index.md`를 세션 기준점으로 사용한다.");
        builder.AppendLine("- CSV 로그와 PNG 프레임은 기존 폴더 구조를 유지하되, 이 파일과 프레임 폴더의 `session_index.md`로 서로 연결한다.");
        builder.AppendLine("- 분석 문서, contact sheet, 비교 이미지를 추가로 만들면 이 세션 폴더 또는 이 manifest에 경로를 추가한다.");
        builder.AppendLine();
        return builder.ToString();
    }

    private sealed class MetricsCsvData
    {
        public readonly Dictionary<int, MetricsCsvFrame> Frames = new Dictionary<int, MetricsCsvFrame>();
        public bool HasYybMaxDeformationRiskColumn;
        public bool HasYybSleeveThicknessRiskColumns;
        public int BelowFloorFrameCount;
        public int RootStepSpikeFrameCount;
        public int YybDeformationRiskFrameCount;
        public int YybDeformationRiskMissingFrameCount;
        public int YybSleeveThicknessRiskFrameCount;
        public int YybSleeveThicknessRiskMissingFrameCount;
        public int ArmMotionFrameCount;
        public int LegMotionFrameCount;
        public float MinFootBottomY = float.NaN;
        public float MinFootBottomGroundGap = float.NaN;
        public float MaxRootStep = float.NaN;
        public float ArmMotionRootTravel;
        public float LegMotionRootTravel;
        public float LimbMotionRootTravel;
        public float MaxLimbMotionRootStep;
        public float MaxRetargetRootDelta = float.NaN;
        public float MaxRetargetPoseDelta = float.NaN;
        public float MaxGroundingVerticalStep = float.NaN;
        public float MaxFootHeightReferenceLift = float.NaN;
        public float MaxYybDeformationRisk = float.NaN;
        public float MaxYybSleeveThicknessRisk = float.NaN;

        public bool TryGetFirstFrame(out MetricsCsvFrame firstFrame)
        {
            firstFrame = default;
            bool found = false;
            foreach (MetricsCsvFrame frame in Frames.Values)
            {
                if (!found || frame.RecorderFrame < firstFrame.RecorderFrame)
                {
                    firstFrame = frame;
                    found = true;
                }
            }

            return found;
        }

        public void Recalculate()
        {
            BelowFloorFrameCount = 0;
            RootStepSpikeFrameCount = 0;
            YybDeformationRiskFrameCount = 0;
            YybDeformationRiskMissingFrameCount = 0;
            YybSleeveThicknessRiskFrameCount = 0;
            YybSleeveThicknessRiskMissingFrameCount = 0;
            ArmMotionFrameCount = 0;
            LegMotionFrameCount = 0;
            MinFootBottomY = float.NaN;
            MinFootBottomGroundGap = float.NaN;
            MaxRootStep = float.NaN;
            ArmMotionRootTravel = 0f;
            LegMotionRootTravel = 0f;
            LimbMotionRootTravel = 0f;
            MaxLimbMotionRootStep = 0f;
            MaxRetargetRootDelta = float.NaN;
            MaxRetargetPoseDelta = float.NaN;
            MaxGroundingVerticalStep = float.NaN;
            MaxFootHeightReferenceLift = float.NaN;
            MaxYybDeformationRisk = float.NaN;
            MaxYybSleeveThicknessRisk = float.NaN;

            List<MetricsCsvFrame> frames = new List<MetricsCsvFrame>(Frames.Values);
            frames.Sort((left, right) => left.RecorderFrame.CompareTo(right.RecorderFrame));
            MetricsCsvFrame previous = default;
            bool hasPrevious = false;
            for (int i = 0; i < frames.Count; i++)
            {
                MetricsCsvFrame frame = frames[i];
                MinFootBottomY = MinFinite(MinFootBottomY, frame.LowestFootBottomY);
                MinFootBottomGroundGap = MinFinite(MinFootBottomGroundGap, frame.FootBottomGroundGap);
                MaxRetargetRootDelta = MaxFinite(MaxRetargetRootDelta, frame.RetargetRootDeltaMax);
                MaxRetargetPoseDelta = MaxFinite(MaxRetargetPoseDelta, frame.RetargetPoseDeltaMax);
                MaxGroundingVerticalStep = MaxFinite(MaxGroundingVerticalStep, frame.GroundingVerticalStepMax);
                MaxFootHeightReferenceLift = MaxFinite(MaxFootHeightReferenceLift, frame.FootHeightReferenceLift);
                if (HasYybMaxDeformationRiskColumn)
                {
                    if (IsFinite(frame.YybMaxDeformationRisk))
                    {
                        YybDeformationRiskFrameCount++;
                        MaxYybDeformationRisk = MaxFinite(MaxYybDeformationRisk, frame.YybMaxDeformationRisk);
                    }
                    else
                    {
                        YybDeformationRiskMissingFrameCount++;
                    }
                }
                else
                {
                    YybDeformationRiskMissingFrameCount++;
                }

                if (HasYybSleeveThicknessRiskColumns)
                {
                    bool hasLeftSleeveRisk = IsFinite(frame.LeftSleeveThicknessRisk);
                    bool hasRightSleeveRisk = IsFinite(frame.RightSleeveThicknessRisk);
                    if (hasLeftSleeveRisk && hasRightSleeveRisk)
                    {
                        YybSleeveThicknessRiskFrameCount++;
                        MaxYybSleeveThicknessRisk = MaxFinite(
                            MaxFinite(MaxYybSleeveThicknessRisk, frame.LeftSleeveThicknessRisk),
                            frame.RightSleeveThicknessRisk);
                    }
                    else
                    {
                        YybSleeveThicknessRiskMissingFrameCount++;
                    }
                }
                else
                {
                    YybSleeveThicknessRiskMissingFrameCount++;
                }

                if (IsBelowFloor(frame.LowestFootBottomY) || IsBelowFloor(frame.FootBottomGroundGap))
                {
                    BelowFloorFrameCount++;
                }

                if (hasPrevious && frame.RecorderFrame - previous.RecorderFrame == 1)
                {
                    float rootStep = Distance(
                        previous.RootX,
                        previous.RootY,
                        previous.RootZ,
                        frame.RootX,
                        frame.RootY,
                        frame.RootZ);
                    MaxRootStep = MaxFinite(MaxRootStep, rootStep);
                    if (IsTeleportStep(rootStep))
                    {
                        RootStepSpikeFrameCount++;
                    }
                }

                if (hasPrevious)
                {
                    float sampledRootStep = Distance(
                        previous.RootX,
                        previous.RootY,
                        previous.RootZ,
                        frame.RootX,
                        frame.RootY,
                        frame.RootZ);
                    float armMotionSignal = CalculateArmMotionSignal(previous, frame);
                    float legMotionSignal = CalculateLegMotionSignal(previous, frame);
                    bool hasArmMotion = ExceedsThreshold(armMotionSignal, QualityLimbMotionSignalThreshold);
                    bool hasLegMotion = ExceedsThreshold(legMotionSignal, QualityLimbMotionSignalThreshold);

                    if (hasArmMotion)
                    {
                        ArmMotionFrameCount++;
                        if (IsFinite(sampledRootStep))
                        {
                            ArmMotionRootTravel += sampledRootStep;
                        }
                    }

                    if (hasLegMotion)
                    {
                        LegMotionFrameCount++;
                        if (IsFinite(sampledRootStep))
                        {
                            LegMotionRootTravel += sampledRootStep;
                        }
                    }

                    if ((hasArmMotion || hasLegMotion) && IsFinite(sampledRootStep))
                    {
                        LimbMotionRootTravel += sampledRootStep;
                        MaxLimbMotionRootStep = MaxFinite(MaxLimbMotionRootStep, sampledRootStep);
                    }
                }

                previous = frame;
                hasPrevious = true;
            }
        }
    }

    private static float CalculateArmMotionSignal(MetricsCsvFrame previous, MetricsCsvFrame current)
    {
        float max = float.NaN;
        max = MaxAbsDelta(max, previous.LeftShoulderDownUpMuscle, current.LeftShoulderDownUpMuscle);
        max = MaxAbsDelta(max, previous.LeftShoulderFrontBackMuscle, current.LeftShoulderFrontBackMuscle);
        max = MaxAbsDelta(max, previous.LeftArmDownUpMuscle, current.LeftArmDownUpMuscle);
        max = MaxAbsDelta(max, previous.LeftArmFrontBackMuscle, current.LeftArmFrontBackMuscle);
        max = MaxAbsDelta(max, previous.LeftArmTwistMuscle, current.LeftArmTwistMuscle);
        max = MaxAbsDelta(max, previous.LeftForearmStretchMuscle, current.LeftForearmStretchMuscle);
        max = MaxAbsDelta(max, previous.LeftForearmTwistMuscle, current.LeftForearmTwistMuscle);
        max = MaxAbsDelta(max, previous.RightShoulderDownUpMuscle, current.RightShoulderDownUpMuscle);
        max = MaxAbsDelta(max, previous.RightShoulderFrontBackMuscle, current.RightShoulderFrontBackMuscle);
        max = MaxAbsDelta(max, previous.RightArmDownUpMuscle, current.RightArmDownUpMuscle);
        max = MaxAbsDelta(max, previous.RightArmFrontBackMuscle, current.RightArmFrontBackMuscle);
        max = MaxAbsDelta(max, previous.RightArmTwistMuscle, current.RightArmTwistMuscle);
        max = MaxAbsDelta(max, previous.RightForearmStretchMuscle, current.RightForearmStretchMuscle);
        max = MaxAbsDelta(max, previous.RightForearmTwistMuscle, current.RightForearmTwistMuscle);
        max = MaxNormalizedAngleDelta(max, previous.LeftElbowAngle, current.LeftElbowAngle);
        max = MaxNormalizedAngleDelta(max, previous.RightElbowAngle, current.RightElbowAngle);
        max = MaxAbsDelta(max, previous.LeftHandHorizontalRatio, current.LeftHandHorizontalRatio);
        max = MaxAbsDelta(max, previous.RightHandHorizontalRatio, current.RightHandHorizontalRatio);
        max = MaxAbsDelta(max, previous.LeftHandBelowShoulderRatio, current.LeftHandBelowShoulderRatio);
        max = MaxAbsDelta(max, previous.RightHandBelowShoulderRatio, current.RightHandBelowShoulderRatio);
        max = MaxAbsDelta(max, previous.LeftHandTorsoSignedClearance, current.LeftHandTorsoSignedClearance);
        max = MaxAbsDelta(max, previous.RightHandTorsoSignedClearance, current.RightHandTorsoSignedClearance);
        return max;
    }

    private static float CalculateGuardNormalizedArmMotionSignal(MetricsCsvFrame previous, MetricsCsvFrame current)
    {
        float max = float.NaN;
        max = MaxGuardNormalizedHumanMuscleDelta(max, previous.LeftShoulderDownUpMuscle, current.LeftShoulderDownUpMuscle);
        max = MaxGuardNormalizedHumanMuscleDelta(max, previous.LeftShoulderFrontBackMuscle, current.LeftShoulderFrontBackMuscle);
        max = MaxGuardNormalizedHumanMuscleDelta(max, previous.LeftArmDownUpMuscle, current.LeftArmDownUpMuscle);
        max = MaxGuardNormalizedHumanMuscleDelta(max, previous.LeftArmFrontBackMuscle, current.LeftArmFrontBackMuscle);
        max = MaxGuardNormalizedUpperArmTwistDelta(max, previous.LeftArmTwistMuscle, current.LeftArmTwistMuscle);
        max = MaxGuardNormalizedHumanMuscleDelta(max, previous.LeftForearmStretchMuscle, current.LeftForearmStretchMuscle);
        max = MaxGuardNormalizedForearmTwistDelta(max, previous.LeftForearmTwistMuscle, current.LeftForearmTwistMuscle);
        max = MaxGuardNormalizedHumanMuscleDelta(max, previous.RightShoulderDownUpMuscle, current.RightShoulderDownUpMuscle);
        max = MaxGuardNormalizedHumanMuscleDelta(max, previous.RightShoulderFrontBackMuscle, current.RightShoulderFrontBackMuscle);
        max = MaxGuardNormalizedHumanMuscleDelta(max, previous.RightArmDownUpMuscle, current.RightArmDownUpMuscle);
        max = MaxGuardNormalizedHumanMuscleDelta(max, previous.RightArmFrontBackMuscle, current.RightArmFrontBackMuscle);
        max = MaxGuardNormalizedUpperArmTwistDelta(max, previous.RightArmTwistMuscle, current.RightArmTwistMuscle);
        max = MaxGuardNormalizedHumanMuscleDelta(max, previous.RightForearmStretchMuscle, current.RightForearmStretchMuscle);
        max = MaxGuardNormalizedForearmTwistDelta(max, previous.RightForearmTwistMuscle, current.RightForearmTwistMuscle);
        max = MaxNormalizedAngleDelta(max, previous.LeftElbowAngle, current.LeftElbowAngle);
        max = MaxNormalizedAngleDelta(max, previous.RightElbowAngle, current.RightElbowAngle);
        max = MaxAbsDelta(max, previous.LeftHandHorizontalRatio, current.LeftHandHorizontalRatio);
        max = MaxAbsDelta(max, previous.RightHandHorizontalRatio, current.RightHandHorizontalRatio);
        max = MaxAbsDelta(max, previous.LeftHandBelowShoulderRatio, current.LeftHandBelowShoulderRatio);
        max = MaxAbsDelta(max, previous.RightHandBelowShoulderRatio, current.RightHandBelowShoulderRatio);
        max = MaxAbsDelta(max, previous.LeftHandTorsoSignedClearance, current.LeftHandTorsoSignedClearance);
        max = MaxAbsDelta(max, previous.RightHandTorsoSignedClearance, current.RightHandTorsoSignedClearance);
        return max;
    }

    private static float CalculateLegMotionSignal(MetricsCsvFrame previous, MetricsCsvFrame current)
    {
        float max = float.NaN;
        max = MaxNormalizedAngleDelta(max, previous.LeftKneeAngle, current.LeftKneeAngle);
        max = MaxNormalizedAngleDelta(max, previous.RightKneeAngle, current.RightKneeAngle);
        return max;
    }

    private static string ResolveLimbPoseDeltaSource(
        float armPoseDelta,
        float legPoseDelta,
        string armSource,
        string legSource)
    {
        bool hasArm = IsFinite(armPoseDelta);
        bool hasLeg = IsFinite(legPoseDelta);
        if (hasArm && (!hasLeg || armPoseDelta >= legPoseDelta))
        {
            return armSource;
        }

        if (hasLeg)
        {
            return legSource;
        }

        return "";
    }

    private static float MaxAbsDelta(float currentMax, float previous, float current)
    {
        if (!IsFinite(previous) || !IsFinite(current))
        {
            return currentMax;
        }

        return MaxFinite(currentMax, Math.Abs(current - previous));
    }

    private static float MaxGuardNormalizedHumanMuscleDelta(float currentMax, float previous, float current)
    {
        return MaxGuardNormalizedMuscleDelta(currentMax, previous, current, QualityGuardNormalizedHumanMuscleLimit);
    }

    private static float MaxGuardNormalizedUpperArmTwistDelta(float currentMax, float previous, float current)
    {
        return MaxGuardNormalizedMuscleDelta(currentMax, previous, current, QualityGuardNormalizedUpperArmTwistMuscleLimit);
    }

    private static float MaxGuardNormalizedForearmTwistDelta(float currentMax, float previous, float current)
    {
        return MaxGuardNormalizedMuscleDelta(currentMax, previous, current, QualityGuardNormalizedForearmTwistMuscleLimit);
    }

    private static float MaxGuardNormalizedMuscleDelta(float currentMax, float previous, float current, float limit)
    {
        if (!IsFinite(previous) || !IsFinite(current))
        {
            return currentMax;
        }

        float normalizedPrevious = Clamp(previous, -limit, limit);
        float normalizedCurrent = Clamp(current, -limit, limit);
        return MaxFinite(currentMax, Math.Abs(normalizedCurrent - normalizedPrevious));
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static float MaxNormalizedAngleDelta(float currentMax, float previous, float current)
    {
        if (!IsFinite(previous) || !IsFinite(current))
        {
            return currentMax;
        }

        return MaxFinite(currentMax, Math.Abs(current - previous) / 180f);
    }


    private struct MetricsCsvFrame
    {
        public string Reason;
        public int RecorderFrame;
        public float RootX;
        public float RootY;
        public float RootZ;
        public float RecordingStartRootY;
        public float RecordingStartBodyPositionY;
        public float RecordingStartHipsLocalY;
        public float RecordingStartHipsY;
        public float RecordingStartHipsReferenceBeforeLocalY;
        public float RecordingStartHipsReferenceAfterLocalY;
        public float RecordingStartHipsReferenceDeltaY;
        public int RecordingStartHipsReferenceFlipDetected;
        public string RecordingStartHipsReferenceStage;
        public float BodyPositionY;
        public float HipsLocalY;
        public float GroundingVerticalStepLast;
        public float FootHeightReferenceLift;
        public float HipsX;
        public float HipsZ;
        public float HipsY;
        public float LowestFootBottomY;
        public float FootBottomGroundGap;
        public float LeftFootX;
        public float LeftFootZ;
        public float RightFootX;
        public float RightFootZ;
        public float RetargetRootDeltaMax;
        public float RetargetPoseDeltaMax;
        public float GroundingVerticalStepMax;
        public float LeftShoulderDownUpMuscle;
        public float LeftShoulderFrontBackMuscle;
        public float LeftArmDownUpMuscle;
        public float LeftArmFrontBackMuscle;
        public float LeftArmTwistMuscle;
        public float LeftForearmStretchMuscle;
        public float LeftForearmTwistMuscle;
        public float RightShoulderDownUpMuscle;
        public float RightShoulderFrontBackMuscle;
        public float RightArmDownUpMuscle;
        public float RightArmFrontBackMuscle;
        public float RightArmTwistMuscle;
        public float RightForearmStretchMuscle;
        public float RightForearmTwistMuscle;
        public float LeftElbowAngle;
        public float RightElbowAngle;
        public float LeftKneeAngle;
        public float RightKneeAngle;
        public float LeftHandHorizontalRatio;
        public float RightHandHorizontalRatio;
        public float LeftHandBelowShoulderRatio;
        public float RightHandBelowShoulderRatio;
        public float LeftHandTorsoSignedClearance;
        public float RightHandTorsoSignedClearance;
        public float YybMaxDeformationRisk;
        public float LeftSleeveThicknessRisk;
        public float RightSleeveThicknessRisk;
    }

    private readonly struct VerticalSolveFrameCorrection
    {
        public readonly float HipsY;
        public readonly float FootBottomY;
        public readonly float LeftFootX;
        public readonly float LeftFootZ;
        public readonly float RightFootX;
        public readonly float RightFootZ;
        public readonly float LeftFootNormalizedDeltaX;
        public readonly float LeftFootNormalizedDeltaZ;
        public readonly float RightFootNormalizedDeltaX;
        public readonly float RightFootNormalizedDeltaZ;
        public readonly float HorizontalFootTargetMagnitude;

        public VerticalSolveFrameCorrection(float hipsY, float footBottomY)
            : this(
                hipsY,
                footBottomY,
                0f,
                0f,
                0f,
                0f,
                float.NaN,
                float.NaN,
                float.NaN,
                float.NaN,
                float.NaN)
        {
        }

        public VerticalSolveFrameCorrection(
            float hipsY,
            float footBottomY,
            float leftFootX,
            float leftFootZ,
            float rightFootX,
            float rightFootZ)
            : this(
                hipsY,
                footBottomY,
                leftFootX,
                leftFootZ,
                rightFootX,
                rightFootZ,
                float.NaN,
                float.NaN,
                float.NaN,
                float.NaN,
                HasNonZeroCorrection(leftFootX) ||
                HasNonZeroCorrection(leftFootZ) ||
                HasNonZeroCorrection(rightFootX) ||
                HasNonZeroCorrection(rightFootZ)
                    ? ResolveHorizontalFootPostprocessTargetMagnitude()
                    : float.NaN)
        {
        }

        public VerticalSolveFrameCorrection(
            float hipsY,
            float footBottomY,
            float leftFootX,
            float leftFootZ,
            float rightFootX,
            float rightFootZ,
            float leftFootNormalizedDeltaX,
            float leftFootNormalizedDeltaZ,
            float rightFootNormalizedDeltaX,
            float rightFootNormalizedDeltaZ,
            float horizontalFootTargetMagnitude)
        {
            HipsY = hipsY;
            FootBottomY = footBottomY;
            LeftFootX = leftFootX;
            LeftFootZ = leftFootZ;
            RightFootX = rightFootX;
            RightFootZ = rightFootZ;
            LeftFootNormalizedDeltaX = leftFootNormalizedDeltaX;
            LeftFootNormalizedDeltaZ = leftFootNormalizedDeltaZ;
            RightFootNormalizedDeltaX = rightFootNormalizedDeltaX;
            RightFootNormalizedDeltaZ = rightFootNormalizedDeltaZ;
            HorizontalFootTargetMagnitude = horizontalFootTargetMagnitude;
        }
    }

    private sealed class VmdRewriteFrame
    {
        public readonly int Offset;
        public readonly uint Frame;
        public readonly string BoneName;
        public readonly string Side;
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly bool IsCenterCarrier;
        public readonly bool IsFootIkCarrier;

        public VmdRewriteFrame(
            int offset,
            uint frame,
            string boneName,
            string side,
            float x,
            float y,
            float z,
            bool isCenterCarrier,
            bool isFootIkCarrier)
        {
            Offset = offset;
            Frame = frame;
            BoneName = boneName ?? "";
            Side = side ?? "";
            X = x;
            Y = y;
            Z = z;
            IsCenterCarrier = isCenterCarrier;
            IsFootIkCarrier = isFootIkCarrier;
        }
    }

    private readonly struct VerticalSolveVmdSafetyLimitDetail
    {
        public readonly uint Frame;
        public readonly string BoneName;
        public readonly string Side;
        public readonly string Reason;
        public readonly float RequestedDeltaX;
        public readonly float RequestedDeltaY;
        public readonly float RequestedDeltaZ;
        public readonly float SafeDeltaX;
        public readonly float SafeDeltaY;
        public readonly float SafeDeltaZ;

        private VerticalSolveVmdSafetyLimitDetail(
            uint frame,
            string boneName,
            string side,
            string reason,
            float requestedDeltaX,
            float requestedDeltaY,
            float requestedDeltaZ,
            float safeDeltaX,
            float safeDeltaY,
            float safeDeltaZ)
        {
            Frame = frame;
            BoneName = boneName ?? string.Empty;
            Side = side ?? string.Empty;
            Reason = reason ?? string.Empty;
            RequestedDeltaX = requestedDeltaX;
            RequestedDeltaY = requestedDeltaY;
            RequestedDeltaZ = requestedDeltaZ;
            SafeDeltaX = safeDeltaX;
            SafeDeltaY = safeDeltaY;
            SafeDeltaZ = safeDeltaZ;
        }

        public static VerticalSolveVmdSafetyLimitDetail Create(
            VmdRewriteFrame frame,
            string reason,
            float requestedDeltaX,
            float requestedDeltaY,
            float requestedDeltaZ,
            float safeDeltaX,
            float safeDeltaY,
            float safeDeltaZ)
        {
            return new VerticalSolveVmdSafetyLimitDetail(
                frame != null ? frame.Frame : 0u,
                frame != null ? frame.BoneName : string.Empty,
                frame != null ? frame.Side : string.Empty,
                reason,
                requestedDeltaX,
                requestedDeltaY,
                requestedDeltaZ,
                safeDeltaX,
                safeDeltaY,
                safeDeltaZ);
        }
    }

    private struct VmdPositionFrame
    {
        public readonly uint Frame;
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public VmdPositionFrame(uint frame, float x, float y, float z)
        {
            Frame = frame;
            X = x;
            Y = y;
            Z = z;
        }
    }

    private struct VmdIkStateFrame
    {
        public readonly uint Frame;
        public readonly bool LeftFootEnabled;
        public readonly bool LeftToeEnabled;
        public readonly bool RightFootEnabled;
        public readonly bool RightToeEnabled;

        public VmdIkStateFrame(
            uint frame,
            bool leftFootEnabled,
            bool leftToeEnabled,
            bool rightFootEnabled,
            bool rightToeEnabled)
        {
            Frame = frame;
            LeftFootEnabled = leftFootEnabled;
            LeftToeEnabled = leftToeEnabled;
            RightFootEnabled = rightFootEnabled;
            RightToeEnabled = rightToeEnabled;
        }
    }

    private struct VmdQualityMetrics
    {
        public int BoneFrameCount;
        public int MaxBoneFrameIndex;
        public int CenterSpikeFrameCount;
        public int FootIkSpikeFrameCount;
        public float MaxCenterStep;
        public float MaxFootIkStep;
        public float MinFootIkY;
        public float MinEffectiveFootIkY;
    }

    private static string EscapeMarkdown(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value.Replace("`", "'").Replace("|", "\\|");
    }

    private static string FormatManifestFloat(float value)
    {
        return IsFinite(value)
            ? value.ToString("0.###", CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        string escaped = value.Replace("\"", "\"\"");
        return escaped.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? $"\"{escaped}\"" : escaped;
    }
}

public static class MotionComparisonProbeSessionManifestPatcher
{
    public static void TryAppendExportedVmdToSessionManifest(
        string sessionManifestPath,
        string vmdRelativePath,
        int frameCount,
        long fileSizeBytes)
    {
        MotionComparisonProbeReportWriter.TryAppendExportedVmdToSessionManifest(
            sessionManifestPath,
            vmdRelativePath,
            frameCount,
            fileSizeBytes);
    }
}
