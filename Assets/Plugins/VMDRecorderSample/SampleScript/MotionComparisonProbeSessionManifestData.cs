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

