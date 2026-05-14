using System;
using System.Globalization;
using System.IO;
using System.Text;

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
}

internal static class MotionComparisonProbeReportWriter
{
    public static void AppendScreenshotIndexRow(string indexFilePath, MotionComparisonProbeScreenshotIndexRow row)
    {
        if (string.IsNullOrEmpty(indexFilePath))
        {
            return;
        }

        File.AppendAllText(indexFilePath, BuildScreenshotIndexRowCsvLine(row) + Environment.NewLine, Encoding.UTF8);
    }

    internal static string BuildScreenshotIndexRowCsvLine(MotionComparisonProbeScreenshotIndexRow row)
    {
        return string.Join(",",
            EscapeCsv(row.ComparisonLabel),
            EscapeCsv(row.SceneName),
            EscapeCsv(row.Reason),
            row.RecorderFrame.ToString(CultureInfo.InvariantCulture),
            EscapeCsv(row.ViewName),
            EscapeCsv(row.RelativePath));
    }

    public static void WriteFrameSessionIndexMarkdown(string filePath, MotionComparisonProbeFrameSessionIndexData data)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        File.WriteAllText(filePath, BuildFrameSessionIndexMarkdown(data), Encoding.UTF8);
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

    public static void WriteSessionManifestMarkdown(string filePath, MotionComparisonProbeSessionManifestData data)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        File.WriteAllText(filePath, BuildSessionManifestMarkdown(data), Encoding.UTF8);
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
        builder.AppendLine("## 산출물");
        builder.AppendLine();
        builder.AppendLine("| 역할 | 경로 |");
        builder.AppendLine("|---|---|");
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

