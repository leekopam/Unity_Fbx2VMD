using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

internal readonly struct MotionComparisonProbeScreenshotIndexRow
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

internal readonly struct MotionComparisonProbeFrameSessionIndexData
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

internal readonly struct MotionComparisonProbeSessionManifestData
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
internal sealed class MotionComparisonFrameQualitySummary
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
    public int candidate_vmd_center_spike_frames;
    public int candidate_vmd_foot_ik_spike_frames;
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
internal sealed class VerticalSolvePrimaryExportPromotion
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

internal static class MotionComparisonProbeReportWriter
{
    private const float QualityFloorTolerance = -0.001f;
    private const float QualityTeleportStepThreshold = 0.12f;
    private const float QualitySameFrameHipsYWarnThreshold = 0.04f;
    private const float QualitySameFrameHipsYFailThreshold = QualityTeleportStepThreshold;
    private const float QualitySameFrameFootBottomYWarnThreshold = 0.035f;
    private const float QualitySameFrameFootBottomYFailThreshold = 0.05f;
    private const float QualityYybDeformationRiskFailThreshold = 0.35f;
    private const float QualityYybSleeveThicknessRiskFailThreshold = 0.35f;
    private const int QualityMetricFrameMatchTolerance = 1;
    private const float VerticalSolvePrototypeMaxCorrectionY = 0.08f;
    private const float VerticalSolveArtifactMaxCorrectionY = 0.085f;
    private const float VerticalSolvePostprocessSafetyMarginY = 0.0005f;
    private const string MainRecordingMovingRootEvaluationRole = "main_recording_moving_root_metrics";
    private const string ScreenshotIndexCsvHeader = "label,scene,reason,recorderFrame,view,path";
    private const string MetricsCsvHeader = "label,scene,reason,elapsed,timeSinceLevelLoad,frameCount,recorderFrame,animationTimeSource,animationClipName,animationClipTime,animationClipLength,animationNormalizedTime,rootX,rootY,rootZ,rootYaw,retargetRootDeltaLast,retargetRootDeltaMax,retargetRootDeltaSkippedCount,retargetPoseRootDeltaLast,retargetPoseRootDeltaMax,retargetPoseRootClampCount,retargetGroundingAdjustmentLast,retargetGroundingAdjustmentMax,retargetGroundingStepClampCount,retargetGroundingSmoothedCount,retargetGroundingVerticalStepLast,retargetGroundingVerticalStepMax,retargetGroundingInitialVerticalStep,retargetGroundingVerticalStepAfterInitialMax,retargetGroundingTargetY,retargetGroundingLowestFootBottomY,retargetGroundingMaxStepPerFrame,retargetGroundingLastStepToMaxStepRatio,retargetGroundingLastStepAtMaxStep,retargetRecordingStartRootY,retargetRecordingStartBodyPositionY,retargetRecordingStartHipsLocalY,retargetRecordingStartHipsY,retargetRecordingStartHipsReferenceBeforeLocalY,retargetRecordingStartHipsReferenceAfterLocalY,retargetRecordingStartHipsReferenceDeltaY,retargetRecordingStartHipsReferenceFlipDetected,retargetRecordingStartHipsReferenceStage,bodyPositionY,hipsLocalY,retargetFootHeightReferenceLift,hipsY,lowestFootY,lowestFootBottomY,meshBoundsMinY,meshBoundsMaxY,footBottomGroundGap,meshBoundsGroundGap,cameraFacingDot,maxScaleDelta,leftUpperArmScale,rightUpperArmScale,leftUpperLegScale,rightUpperLegScale,leftArmLength,rightArmLength,leftLegLength,rightLegLength,leftElbowAngle,rightElbowAngle,leftKneeAngle,rightKneeAngle,leftElbowBendForward,rightElbowBendForward,leftKneeBendForward,rightKneeBendForward,leftElbowBendOffsetForward,rightElbowBendOffsetForward,leftKneeBendOffsetForward,rightKneeBendOffsetForward,leftUpperArmDownDot,rightUpperArmDownDot,leftHandHorizontalRatio,rightHandHorizontalRatio,leftHandBelowShoulderRatio,rightHandBelowShoulderRatio,leftHandTorsoSignedClearance,rightHandTorsoSignedClearance,minHandTorsoSignedClearance,handTorsoPenetrationRisk,leftShoulderDownUpMuscle,leftShoulderFrontBackMuscle,leftArmDownUpMuscle,leftArmFrontBackMuscle,leftArmTwistMuscle,leftForearmStretchMuscle,leftForearmTwistMuscle,rightShoulderDownUpMuscle,rightShoulderFrontBackMuscle,rightArmDownUpMuscle,rightArmFrontBackMuscle,rightArmTwistMuscle,rightForearmStretchMuscle,rightForearmTwistMuscle,leftThumb1StretchMuscle,leftThumbSpreadMuscle,leftIndex1StretchMuscle,leftIndexSpreadMuscle,leftMiddle1StretchMuscle,leftMiddleSpreadMuscle,leftRing1StretchMuscle,leftRingSpreadMuscle,leftLittle1StretchMuscle,leftLittleSpreadMuscle,rightThumb1StretchMuscle,rightThumbSpreadMuscle,rightIndex1StretchMuscle,rightIndexSpreadMuscle,rightMiddle1StretchMuscle,rightMiddleSpreadMuscle,rightRing1StretchMuscle,rightRingSpreadMuscle,rightLittle1StretchMuscle,rightLittleSpreadMuscle,spineLocalEuler,chestLocalEuler,upperChestLocalEuler,leftShoulderLocalEuler,rightShoulderLocalEuler,leftUpperArmLocalEuler,rightUpperArmLocalEuler,leftLowerArmLocalEuler,rightLowerArmLocalEuler,leftHandLocalEuler,rightHandLocalEuler,leftThumbProximalLocalEuler,leftIndexProximalLocalEuler,leftMiddleProximalLocalEuler,leftRingProximalLocalEuler,leftLittleProximalLocalEuler,rightThumbProximalLocalEuler,rightIndexProximalLocalEuler,rightMiddleProximalLocalEuler,rightRingProximalLocalEuler,rightLittleProximalLocalEuler";
    private const string YybDiagnosticMetricsCsvHeader = "leftThumbIndexSpreadAngle,rightThumbIndexSpreadAngle,leftThumbPalmProjection,rightThumbPalmProjection,leftThumbSpreadRisk,rightThumbSpreadRisk,leftThumbProjectionRisk,rightThumbProjectionRisk,leftThumbHelperSourceDistance,rightThumbHelperSourceDistance,leftThumbHelperSourceDistanceDelta,rightThumbHelperSourceDistanceDelta,leftThumbHelperSourceRotationDelta,rightThumbHelperSourceRotationDelta,leftThumbHelperSeparationRisk,rightThumbHelperSeparationRisk,leftWebbingRisk,rightWebbingRisk,leftArmTwistRisk,rightArmTwistRisk,leftSleeveAnchorRisk,rightSleeveAnchorRisk,leftSleeveAnchorDistance,rightSleeveAnchorDistance,leftSleeveThicknessRatio,rightSleeveThicknessRatio,leftSleeveThicknessRisk,rightSleeveThicknessRisk,leftYybDeformationRisk,rightYybDeformationRisk,yybMaxDeformationRisk,thumbGuardManualReferenceConfigured,thumbGuardManualReferenceActive,thumbGuardPoseShapingSuppressed,thumbGuardLeftPoseShapingSuppressed,thumbGuardRightPoseShapingSuppressed,thumbGuardProjectionWeight,thumbGuardLeftProjectionWeight,thumbGuardRightProjectionWeight,thumbGuardIndexSpreadWeight,thumbGuardLeftIndexSpreadWeight,thumbGuardRightIndexSpreadWeight,thumbGuardSegmentStraightenWeight,thumbGuardLeftSegmentStraightenWeight,thumbGuardRightSegmentStraightenWeight,thumbGuardLeftProjectionCorrectionApplyCount,thumbGuardRightProjectionCorrectionApplyCount,thumbGuardLeftProjectionCorrectionPreserveCount,thumbGuardRightProjectionCorrectionPreserveCount,thumbGuardLeftSegmentStraightenApplyCount,thumbGuardRightSegmentStraightenApplyCount,thumbGuardLeftSegmentStraightenPreserveCount,thumbGuardRightSegmentStraightenPreserveCount,thumbGuardLeftLocalRotationGuardClampCount,thumbGuardRightLocalRotationGuardClampCount,thumbGuardLeftLocalRotationGuardPreserveCount,thumbGuardRightLocalRotationGuardPreserveCount,thumbGuardLeftLocalRotationGuardCurrentRisk,thumbGuardRightLocalRotationGuardCurrentRisk,thumbGuardLeftLocalRotationGuardLimitedRisk,thumbGuardRightLocalRotationGuardLimitedRisk,thumbGuardLeftWorldRotationSuppressCompetingOverride,thumbGuardRightWorldRotationSuppressCompetingOverride,thumbGuardLeftWorldRotationKeepDetachedHelperOverride,thumbGuardRightWorldRotationKeepDetachedHelperOverride,thumbGuardLeftWorldRotationCurrentReferenceFrameDeviation,thumbGuardRightWorldRotationCurrentReferenceFrameDeviation,thumbGuardLeftWorldRotationCandidateReferenceFrameDeviation,thumbGuardRightWorldRotationCandidateReferenceFrameDeviation,thumbGuardLeftProximalWorldRotationPreserveReason,thumbGuardRightProximalWorldRotationPreserveReason,thumbGuardLeftIntermediateWorldRotationPreserveReason,thumbGuardRightIntermediateWorldRotationPreserveReason,thumbGuardLeftProximalWorldRotationCurrentReferenceAngle,thumbGuardRightProximalWorldRotationCurrentReferenceAngle,thumbGuardLeftIntermediateWorldRotationCurrentReferenceAngle,thumbGuardRightIntermediateWorldRotationCurrentReferenceAngle,thumbGuardLeftProximalWorldRotationCandidateReferenceAngle,thumbGuardRightProximalWorldRotationCandidateReferenceAngle,thumbGuardLeftIntermediateWorldRotationCandidateReferenceAngle,thumbGuardRightIntermediateWorldRotationCandidateReferenceAngle,thumbGuardLeftProximalWorldRotationPreserveCurrentRisk,thumbGuardRightProximalWorldRotationPreserveCurrentRisk,thumbGuardLeftIntermediateWorldRotationPreserveCurrentRisk,thumbGuardRightIntermediateWorldRotationPreserveCurrentRisk,thumbGuardLeftProximalWorldRotationPreserveLimitedRisk,thumbGuardRightProximalWorldRotationPreserveLimitedRisk,thumbGuardLeftIntermediateWorldRotationPreserveLimitedRisk,thumbGuardRightIntermediateWorldRotationPreserveLimitedRisk,thumbGuardHelperSyncEnabled,thumbGuardHelperPositionSyncEnabled,thumbGuardHelperSyncWeight,thumbGuardHelperMaxLocalAngle,thumbGuardPalmStabilizeEnabled,thumbGuardPalmStabilizeWeight,thumbGuardPalmStabilizeMaxLocalAngle,thumbGuardWebbingStabilizeEnabled,thumbGuardWebbingStabilizeWeight,thumbGuardWebbingMaxLocalAngle,thumbGuardWebbingMaxPositionOffset";
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
        File.WriteAllText(indexFilePath, BuildScreenshotIndexCsvHeader() + Environment.NewLine, Encoding.UTF8);
    }

    internal static string BuildScreenshotIndexCsvHeader()
    {
        return ScreenshotIndexCsvHeader;
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
        return MetricsCsvHeader + "," + YybDiagnosticMetricsCsvHeader;
    }

    internal static MotionComparisonFrameQualitySummary BuildFrameQualitySummary(
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
            candidate_vmd_center_spike_frames = vmd.CenterSpikeFrameCount,
            candidate_vmd_foot_ik_spike_frames = vmd.FootIkSpikeFrameCount,
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
            vertical_solve_postprocess_basis = "metrics-stage postprocess artifact; original frame_quality status remains measured from the unmodified candidate metrics",
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
            vertical_solve_corrected_candidate_basis = "explicit corrected candidate metrics/VMD artifact generated from the bounded vertical solve and evaluated with the same raw frame_quality evaluator",
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
            "same raw frame_quality evaluator over the explicit corrected candidate metrics/VMD artifact; original raw candidate summary remains separate";
        return true;
    }

    internal static bool TryPromoteVerticalSolveCorrectedCandidateToPrimaryExport(
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
            return promotion.promoted_vmd_bytes > 0L;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MotionComparisonProbeReportWriter] Vertical solve primary export promotion failed: {ex.Message}");
            promotion = null;
            return false;
        }
    }

    internal static MotionComparisonFrameQualitySummary[] BuildFrameQualityEvaluationEntries(
        MotionComparisonFrameQualitySummary rawSummary)
    {
        if (rawSummary == null)
        {
            return Array.Empty<MotionComparisonFrameQualitySummary>();
        }

        if (string.Equals(
                rawSummary.frame_quality_evaluation_role,
                MainRecordingMovingRootEvaluationRole,
                StringComparison.Ordinal))
        {
            return new[] { rawSummary };
        }

        if (string.Equals(
                rawSummary.frame_quality_evaluation_role,
                "main_auto_integrated_vertical_solve_metrics",
                StringComparison.Ordinal))
        {
            return new[] { rawSummary };
        }

        if (!TryBuildVerticalSolveCorrectedCandidateFrameQualitySummary(
                rawSummary,
                out MotionComparisonFrameQualitySummary correctedSummary))
        {
            return new[] { rawSummary };
        }

        rawSummary.frame_quality_evaluation_role = "evaluation_candidate_metrics";
        rawSummary.frame_quality_evaluation_basis =
            "primary frame_quality evaluator over the unmodified candidate metrics CSV; corrected candidate artifacts remain separate evidence";
        return new[] { rawSummary, correctedSummary };
    }

    internal static void MarkIntentionalMovingRootStageMotion(MotionComparisonFrameQualitySummary summary)
    {
        if (summary == null)
        {
            return;
        }

        summary.frame_quality_evaluation_role = MainRecordingMovingRootEvaluationRole;
        summary.frame_quality_evaluation_basis =
            "Main_recoding stage preview intentionally follows FBX X/Z root motion; same-frame root path and relative foot-bottom deltas are reported but excluded from the stationary-root gate, while retarget root, root-step/VMD center/IK spikes, below-floor, and hips gates remain enforced";
        ApplyFrameQualityStatus(
            summary,
            allowSameFrameRootPositionDelta: true,
            allowRelativeFootBottomDelta: true);
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

    internal static void AttachLatestMmdAutomationEvidence(
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
            prototypeFootCorrection = ClampFootVerticalSolveCorrectionToFloor(prototypeFootCorrection, candidateFrame);
            float prototypeHipsYDelta = IsFinite(normalizedHipsYDelta) && IsFinite(prototypeHipsCorrection)
                ? normalizedHipsYDelta + prototypeHipsCorrection
                : float.NaN;
            float prototypeFootBottomYDelta = IsFinite(normalizedFootBottomYDelta) && IsFinite(prototypeFootCorrection)
                ? normalizedFootBottomYDelta + prototypeFootCorrection
                : float.NaN;
            if (verticalSolveCorrections != null &&
                IsFinite(prototypeHipsCorrection) &&
                IsFinite(prototypeFootCorrection))
            {
                float postprocessHipsCorrection = ResolveBoundedVerticalSolveCorrection(
                    normalizedHipsYDelta,
                    Mathf.Max(0f, QualitySameFrameHipsYWarnThreshold - VerticalSolvePostprocessSafetyMarginY),
                    VerticalSolveArtifactMaxCorrectionY);
                float postprocessFootCorrection = ResolveBoundedVerticalSolveCorrection(
                    normalizedFootBottomYDelta,
                    Mathf.Max(0f, QualitySameFrameFootBottomYWarnThreshold - VerticalSolvePostprocessSafetyMarginY),
                    VerticalSolveArtifactMaxCorrectionY);
                postprocessFootCorrection = ClampFootVerticalSolveCorrectionToFloor(
                    postprocessFootCorrection,
                    candidateFrame);
                verticalSolveCorrections[candidateRecorderFrame] =
                    new VerticalSolveFrameCorrection(postprocessHipsCorrection, postprocessFootCorrection);
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
            out long correctedVmdBytes);
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
            correctedVmdBytes);

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

    private static bool TryWriteVerticalSolveCorrectedCandidateVmdArtifact(
        string sourceVmdPath,
        string outputVmdPath,
        IReadOnlyDictionary<int, VerticalSolveFrameCorrection> verticalSolveCorrections,
        out int correctedFrameCount,
        out int safetyLimitedFrameCount,
        out long fileSizeBytes)
    {
        correctedFrameCount = 0;
        safetyLimitedFrameCount = 0;
        fileSizeBytes = 0L;
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
                if (!IsFinite(requestedDeltaY) || Math.Abs(requestedDeltaY) <= 0f)
                {
                    continue;
                }

                float safeDeltaY = ClampVmdCarrierDeltaToStepSafety(
                    frame,
                    i > 0 ? frames[i - 1] : null,
                    i + 1 < frames.Count ? frames[i + 1] : null,
                    requestedDeltaY);
                safeDeltaY = ClampCenterCarrierDeltaToFloor(
                    frame,
                    safeDeltaY,
                    centerCarrierCountByFrame,
                    minEffectiveFootIkYByFrame);
                if (!IsFinite(safeDeltaY) || Math.Abs(safeDeltaY) <= 0f)
                {
                    safetyLimitedFrameCount++;
                    continue;
                }

                if (Math.Abs(safeDeltaY - requestedDeltaY) > 0.000001f)
                {
                    safetyLimitedFrameCount++;
                }

                int yOffset = frame.Offset + 23;
                byte[] yBytes = BitConverter.GetBytes(frame.Y + safeDeltaY);
                Buffer.BlockCopy(yBytes, 0, bytes, yOffset, yBytes.Length);
                correctedFrameCount++;
            }
        }

        if (correctedFrameCount <= 0)
        {
            return false;
        }

        EnsureParentDirectoryExists(outputVmdPath);
        File.WriteAllBytes(outputVmdPath, bytes);
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

    private static void WriteVerticalSolveCorrectedCandidateManifest(
        string manifestPath,
        string rawCandidateMetricsCsvPath,
        string rawCandidateVmdPath,
        string correctedCandidateMetricsCsvPath,
        string correctedCandidateVmdPath,
        int correctedRows,
        int correctedVmdChangedFrames,
        int correctedVmdSafetyLimitedFrames,
        long correctedVmdBytes)
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
            "\"corrected_vmd_bytes\":" + correctedVmdBytes.ToString(CultureInfo.InvariantCulture) + "," +
            "\"frame_quality_evaluator\":\"raw_frame_quality_evaluator\"" +
            "}";
        File.WriteAllText(manifestPath, json, Encoding.UTF8);
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

        string[] headers = SplitCsvLine(lines[0]);
        Dictionary<string, int> columns = BuildColumnLookup(headers);
        if (!columns.ContainsKey("recorderFrame") ||
            !columns.ContainsKey("hipsY") ||
            !columns.ContainsKey("lowestFootBottomY"))
        {
            return false;
        }

        var output = new List<string>
        {
            lines[0]
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
        if (summary.candidate_below_floor_metric_frames > 0 || IsBelowFloor(floorCheckFootIkY))
        {
            reasons.Add("below-floor foot/IK sample detected");
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

        if (IsTeleportStep(summary.max_same_frame_root_position_delta))
        {
            if (allowSameFrameRootPositionDelta)
            {
                allowedNotes.Add("intentional moving-root stage path delta");
            }
            else
            {
                reasons.Add("same-frame root position delta threshold exceeded");
                fail = true;
            }
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

    private static bool IsYybFrameQualityCandidate(MotionComparisonFrameQualitySummary summary)
    {
        return summary != null &&
            (MatchesYybModelName(summary.candidate_label) ||
             MatchesYybModelName(Path.GetFileNameWithoutExtension(summary.candidate_metrics_csv)));
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
                HipsY = ReadFloat(values, columns, "hipsY"),
                LowestFootBottomY = ReadFloat(values, columns, "lowestFootBottomY"),
                FootBottomGroundGap = ReadFloat(values, columns, "footBottomGroundGap"),
                RetargetRootDeltaMax = ReadFloat(values, columns, "retargetRootDeltaMax"),
                RetargetPoseDeltaMax = ReadFloat(values, columns, "retargetPoseRootDeltaMax"),
                GroundingVerticalStepMax = ReadFloat(values, columns, "retargetGroundingVerticalStepMax"),
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

        metrics.BoneFrameCount = (int)Math.Min(boneFrameCount, int.MaxValue);
        metrics.MaxCenterStep = CalculateMaxVmdStep(centerFrames, out metrics.CenterSpikeFrameCount);
        metrics.MaxFootIkStep = CalculateMaxVmdStep(footIkFrames, out metrics.FootIkSpikeFrameCount);
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

    private static float CalculateMaxVmdStep(Dictionary<string, List<VmdPositionFrame>> framesByBone, out int spikeFrameCount)
    {
        spikeFrameCount = 0;
        float maxStep = float.NaN;
        foreach (List<VmdPositionFrame> frames in framesByBone.Values)
        {
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
                if (IsTeleportStep(step))
                {
                    spikeFrameCount++;
                }
            }
        }

        return maxStep;
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

    private static float ClampFootVerticalSolveCorrectionToFloor(float correction, MetricsCsvFrame candidateFrame)
    {
        if (!IsFinite(correction))
        {
            return float.NaN;
        }

        if (correction >= 0f)
        {
            return correction;
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
        public float MinFootBottomY = float.NaN;
        public float MinFootBottomGroundGap = float.NaN;
        public float MaxRootStep = float.NaN;
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
            MinFootBottomY = float.NaN;
            MinFootBottomGroundGap = float.NaN;
            MaxRootStep = float.NaN;
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

                previous = frame;
                hasPrevious = true;
            }
        }
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
        public float HipsY;
        public float LowestFootBottomY;
        public float FootBottomGroundGap;
        public float RetargetRootDeltaMax;
        public float RetargetPoseDeltaMax;
        public float GroundingVerticalStepMax;
        public float YybMaxDeformationRisk;
        public float LeftSleeveThicknessRisk;
        public float RightSleeveThicknessRisk;
    }

    private readonly struct VerticalSolveFrameCorrection
    {
        public readonly float HipsY;
        public readonly float FootBottomY;

        public VerticalSolveFrameCorrection(float hipsY, float footBottomY)
        {
            HipsY = hipsY;
            FootBottomY = footBottomY;
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

    private struct VmdQualityMetrics
    {
        public int BoneFrameCount;
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
