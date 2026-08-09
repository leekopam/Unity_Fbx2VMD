using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public static partial class MotionComparisonProbeReportWriter
{
    private const string SessionManifestArtifactsHeading = "## 산출물";
    private const string SessionManifestArtifactsTableHeader = "| 역할 | 경로 |";
    private const string SessionManifestArtifactsTableSeparator = "|---|---|";

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
}
