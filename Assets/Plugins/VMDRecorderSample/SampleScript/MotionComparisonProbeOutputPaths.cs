using System;
using System.Globalization;
using System.IO;

internal readonly struct MotionComparisonProbeSessionOutputPaths
{
    public readonly string SessionFolder;
    public readonly string SessionManifestPath;

    public MotionComparisonProbeSessionOutputPaths(string sessionFolder, string sessionManifestPath)
    {
        SessionFolder = sessionFolder ?? "";
        SessionManifestPath = sessionManifestPath ?? "";
    }
}

internal readonly struct MotionComparisonProbeScreenshotOutputPaths
{
    public readonly string ScreenshotFolder;
    public readonly string ScreenshotIndexPath;
    public readonly string ScreenshotSessionIndexPath;

    public MotionComparisonProbeScreenshotOutputPaths(
        string screenshotFolder,
        string screenshotIndexPath,
        string screenshotSessionIndexPath)
    {
        ScreenshotFolder = screenshotFolder ?? "";
        ScreenshotIndexPath = screenshotIndexPath ?? "";
        ScreenshotSessionIndexPath = screenshotSessionIndexPath ?? "";
    }
}

internal readonly struct MotionComparisonProbeScreenshotSessionOutputPaths
{
    public readonly string ScreenshotFolder;
    public readonly string ScreenshotIndexPath;
    public readonly string ScreenshotSessionIndexPath;
    public readonly MotionComparisonProbeFrameSessionIndexData FrameSessionIndexData;

    public MotionComparisonProbeScreenshotSessionOutputPaths(
        string screenshotFolder,
        string screenshotIndexPath,
        string screenshotSessionIndexPath,
        MotionComparisonProbeFrameSessionIndexData frameSessionIndexData)
    {
        ScreenshotFolder = screenshotFolder ?? "";
        ScreenshotIndexPath = screenshotIndexPath ?? "";
        ScreenshotSessionIndexPath = screenshotSessionIndexPath ?? "";
        FrameSessionIndexData = frameSessionIndexData;
    }
}

internal readonly struct MotionComparisonProbeSessionArtifactOutputPaths
{
    public readonly string SessionFolder;
    public readonly string SessionManifestPath;
    public readonly string ScreenshotFolder;
    public readonly string ScreenshotIndexPath;
    public readonly string ScreenshotSessionIndexPath;
    public readonly MotionComparisonProbeFrameSessionIndexData FrameSessionIndexData;

    public MotionComparisonProbeSessionArtifactOutputPaths(
        string sessionFolder,
        string sessionManifestPath,
        string screenshotFolder,
        string screenshotIndexPath,
        string screenshotSessionIndexPath,
        MotionComparisonProbeFrameSessionIndexData frameSessionIndexData)
    {
        SessionFolder = sessionFolder ?? "";
        SessionManifestPath = sessionManifestPath ?? "";
        ScreenshotFolder = screenshotFolder ?? "";
        ScreenshotIndexPath = screenshotIndexPath ?? "";
        ScreenshotSessionIndexPath = screenshotSessionIndexPath ?? "";
        FrameSessionIndexData = frameSessionIndexData;
    }
}

internal readonly struct MotionComparisonProbeScreenshotCaptureOutputPaths
{
    public readonly string ScreenshotFileName;
    public readonly string ScreenshotPath;
    public readonly MotionComparisonProbeScreenshotIndexRow IndexRow;

    public MotionComparisonProbeScreenshotCaptureOutputPaths(
        string screenshotFileName,
        string screenshotPath,
        MotionComparisonProbeScreenshotIndexRow indexRow)
    {
        ScreenshotFileName = screenshotFileName ?? "";
        ScreenshotPath = screenshotPath ?? "";
        IndexRow = indexRow;
    }
}

internal readonly struct MotionComparisonProbeScreenshotCaptureNames
{
    public readonly string FrameName;
    public readonly string FrontViewName;
    public readonly string RightViewName;
    public readonly string LeftHandFrontViewName;
    public readonly string LeftHandRightViewName;
    public readonly string RightHandFrontViewName;
    public readonly string RightHandRightViewName;

    public MotionComparisonProbeScreenshotCaptureNames(
        string frameName,
        string frontViewName,
        string rightViewName,
        string leftHandFrontViewName,
        string leftHandRightViewName,
        string rightHandFrontViewName,
        string rightHandRightViewName)
    {
        FrameName = frameName ?? "";
        FrontViewName = frontViewName ?? "";
        RightViewName = rightViewName ?? "";
        LeftHandFrontViewName = leftHandFrontViewName ?? "";
        LeftHandRightViewName = leftHandRightViewName ?? "";
        RightHandFrontViewName = rightHandFrontViewName ?? "";
        RightHandRightViewName = rightHandRightViewName ?? "";
    }
}

internal readonly struct MotionComparisonProbeSessionManifestOutputPaths
{
    public readonly string MetricsCsvRelativePath;
    public readonly string FrameFolderRelativePath;
    public readonly string FrameIndexCsvRelativePath;
    public readonly string FrameSessionIndexRelativePath;

    public MotionComparisonProbeSessionManifestOutputPaths(
        string metricsCsvRelativePath,
        string frameFolderRelativePath,
        string frameIndexCsvRelativePath,
        string frameSessionIndexRelativePath)
    {
        MetricsCsvRelativePath = metricsCsvRelativePath ?? "";
        FrameFolderRelativePath = frameFolderRelativePath ?? "";
        FrameIndexCsvRelativePath = frameIndexCsvRelativePath ?? "";
        FrameSessionIndexRelativePath = frameSessionIndexRelativePath ?? "";
    }
}

internal readonly struct MotionComparisonProbeOutputRoots
{
    public readonly string ComparisonOutputFolder;
    public readonly string ComparisonSessionRootFolder;

    public MotionComparisonProbeOutputRoots(string comparisonOutputFolder, string comparisonSessionRootFolder)
    {
        ComparisonOutputFolder = comparisonOutputFolder ?? "";
        ComparisonSessionRootFolder = comparisonSessionRootFolder ?? "";
    }
}

internal readonly struct MotionComparisonProbeSamplingSessionOutputPaths
{
    public readonly string EvidenceBaseName;
    public readonly string SessionId;
    public readonly string MetricsCsvPath;

    public MotionComparisonProbeSamplingSessionOutputPaths(
        string evidenceBaseName,
        string sessionId,
        string metricsCsvPath)
    {
        EvidenceBaseName = evidenceBaseName ?? "";
        SessionId = sessionId ?? "";
        MetricsCsvPath = metricsCsvPath ?? "";
    }
}

internal static class MotionComparisonProbeOutputPaths
{
    private const string OutputDocsFolderName = "Docs";
    private const string OutputRootFolderName = "Machine_Spirit";
    private const string OutputLocalFolderName = "Local";
    private const string ComparisonFolderName = "ComparisonLogs";
    private const string ComparisonFramesFolderName = "ComparisonFrames";
    private const string ComparisonSessionsFolderName = "ComparisonSessions";
    private const int EvidenceFileNamePartMaxLength = 48;
    private const int EvidenceSafeMaxFullPathLength = 240;
    private const int EvidenceUniqueSuffixLength = 4;

    public static string GetProjectRootFromDataPath(string dataPath)
    {
        DirectoryInfo projectRoot = Directory.GetParent(dataPath);
        return projectRoot != null ? projectRoot.FullName : dataPath;
    }

    public static string GetComparisonOutputFolder(string dataPath)
    {
        return GetOrCreateFolderFromDataPath(
            dataPath,
            OutputDocsFolderName,
            OutputRootFolderName,
            OutputLocalFolderName,
            ComparisonFolderName);
    }

    public static string GetComparisonFrameRootFolder(string dataPath)
    {
        return GetOrCreateFolderFromDataPath(
            dataPath,
            OutputDocsFolderName,
            OutputRootFolderName,
            OutputLocalFolderName,
            ComparisonFramesFolderName);
    }

    public static string GetComparisonSessionRootFolder(string dataPath)
    {
        return GetOrCreateFolderFromDataPath(
            dataPath,
            OutputDocsFolderName,
            OutputRootFolderName,
            OutputLocalFolderName,
            ComparisonSessionsFolderName);
    }

    public static MotionComparisonProbeOutputRoots BuildComparisonOutputRoots(string dataPath)
    {
        return new MotionComparisonProbeOutputRoots(
            GetComparisonOutputFolder(dataPath),
            GetComparisonSessionRootFolder(dataPath));
    }

    public static MotionComparisonProbeSamplingSessionOutputPaths BuildSamplingSessionOutputPaths(
        string dataPath,
        string sessionStamp,
        string sceneName,
        string comparisonLabel)
    {
        MotionComparisonProbeOutputRoots outputRoots = BuildComparisonOutputRoots(dataPath);
        string evidenceBaseName = BuildMetricsEvidenceBaseName(sessionStamp, sceneName, comparisonLabel);
        evidenceBaseName = ShortenEvidenceBaseNameToFitFile(
            outputRoots.ComparisonOutputFolder,
            evidenceBaseName,
            BuildMetricsCsvExtension());

        string sessionId = BuildComparisonSessionIdBaseName(sessionStamp, sceneName, comparisonLabel);
        sessionId = ShortenEvidenceBaseNameToFitFolder(
            outputRoots.ComparisonSessionRootFolder,
            sessionId,
            BuildSessionManifestFileName());

        string metricsCsvPath = BuildMetricsCsvOutputPath(
            outputRoots.ComparisonOutputFolder,
            BuildMetricsCsvFileName(evidenceBaseName));

        return new MotionComparisonProbeSamplingSessionOutputPaths(
            evidenceBaseName,
            sessionId,
            metricsCsvPath);
    }

    public static MotionComparisonProbeSessionOutputPaths BuildSessionOutputPaths(
        string dataPath,
        string sessionId,
        string sessionManifestFileName = null)
    {
        string sessionFolder = BuildUniqueDirectoryPath(GetComparisonSessionRootFolder(dataPath), sessionId);
        EnsureDirectoryExists(sessionFolder);
        sessionManifestFileName = string.IsNullOrEmpty(sessionManifestFileName)
            ? BuildSessionManifestFileName()
            : sessionManifestFileName;
        string sessionManifestPath = Path.Combine(sessionFolder, sessionManifestFileName ?? "");
        return new MotionComparisonProbeSessionOutputPaths(sessionFolder, sessionManifestPath);
    }

    public static MotionComparisonProbeScreenshotOutputPaths BuildScreenshotOutputPaths(
        string dataPath,
        string sessionStamp,
        string frameSessionIndexFileName)
    {
        string screenshotFolder = BuildUniqueDirectoryPath(GetComparisonFrameRootFolder(dataPath), $"when-{sessionStamp}");
        EnsureDirectoryExists(screenshotFolder);
        string screenshotIndexPath = Path.Combine(screenshotFolder, "index.csv");
        string screenshotSessionIndexPath = Path.Combine(screenshotFolder, frameSessionIndexFileName ?? "");
        return new MotionComparisonProbeScreenshotOutputPaths(
            screenshotFolder,
            screenshotIndexPath,
            screenshotSessionIndexPath);
    }

    public static MotionComparisonProbeScreenshotSessionOutputPaths BuildScreenshotSessionOutputPaths(
        string dataPath,
        string sessionStamp,
        string sessionId,
        string sessionManifestPath,
        string metricsCsvPath)
    {
        MotionComparisonProbeScreenshotOutputPaths paths =
            BuildScreenshotOutputPaths(dataPath, sessionStamp, BuildFrameSessionIndexFileName());
        return new MotionComparisonProbeScreenshotSessionOutputPaths(
            paths.ScreenshotFolder,
            paths.ScreenshotIndexPath,
            paths.ScreenshotSessionIndexPath,
            BuildFrameSessionIndexData(
                dataPath,
                sessionId,
                sessionManifestPath,
                metricsCsvPath,
                paths.ScreenshotIndexPath));
    }

    public static MotionComparisonProbeSessionArtifactOutputPaths BuildSessionArtifactOutputPaths(
        string dataPath,
        string sessionStamp,
        string sessionId,
        string metricsCsvPath,
        bool captureSampleScreenshots)
    {
        MotionComparisonProbeSessionOutputPaths sessionPaths =
            BuildSessionOutputPaths(dataPath, sessionId);

        if (!captureSampleScreenshots)
        {
            return new MotionComparisonProbeSessionArtifactOutputPaths(
                sessionPaths.SessionFolder,
                sessionPaths.SessionManifestPath,
                "",
                "",
                "",
                default(MotionComparisonProbeFrameSessionIndexData));
        }

        MotionComparisonProbeScreenshotSessionOutputPaths screenshotPaths =
            BuildScreenshotSessionOutputPaths(
                dataPath,
                sessionStamp,
                sessionId,
                sessionPaths.SessionManifestPath,
                metricsCsvPath);
        return new MotionComparisonProbeSessionArtifactOutputPaths(
            sessionPaths.SessionFolder,
            sessionPaths.SessionManifestPath,
            screenshotPaths.ScreenshotFolder,
            screenshotPaths.ScreenshotIndexPath,
            screenshotPaths.ScreenshotSessionIndexPath,
            screenshotPaths.FrameSessionIndexData);
    }

    public static string BuildScreenshotPngPath(string screenshotFolder, string fileName)
    {
        return Path.Combine(screenshotFolder, fileName);
    }

    public static MotionComparisonProbeScreenshotCaptureOutputPaths BuildScreenshotCaptureOutputPaths(
        string dataPath,
        string screenshotFolder,
        string comparisonLabel,
        string sceneName,
        string reason,
        int recorderFrame,
        string viewName,
        string frameName)
    {
        string fileName = BuildScreenshotPngFileName(reason, viewName, frameName);
        string screenshotPath = BuildScreenshotPngPath(screenshotFolder, fileName);
        return new MotionComparisonProbeScreenshotCaptureOutputPaths(
            fileName,
            screenshotPath,
            BuildScreenshotIndexRow(
                dataPath,
                comparisonLabel,
                sceneName,
                reason,
                recorderFrame,
                viewName,
                screenshotPath));
    }

    public static string BuildScreenshotPngFileName(string reason, string viewName, string frameName)
    {
        string safeReason = ShortenPathSegmentToLength(SanitizePathSegment(reason), EvidenceFileNamePartMaxLength);
        string safeViewName = ShortenPathSegmentToLength(SanitizePathSegment(viewName), EvidenceFileNamePartMaxLength);
        string safeFrameName = ShortenPathSegmentToLength(SanitizePathSegment(frameName), EvidenceFileNamePartMaxLength);
        return $"pose_{safeReason}_rt-{safeViewName}_frame-{safeFrameName}.png";
    }

    public static string BuildScreenshotFrameName(int recorderFrame, int fallbackFrameCount)
    {
        int frameNumber = recorderFrame >= 0 ? recorderFrame : fallbackFrameCount;
        return frameNumber.ToString("000000", CultureInfo.InvariantCulture);
    }

    public static string BuildSampleScreenshotViewName(bool frontView)
    {
        return frontView ? "front" : "right";
    }

    public static string BuildFingerCloseupViewName(bool leftHand, bool frontView)
    {
        string handName = leftHand ? "left-hand" : "right-hand";
        string viewName = BuildSampleScreenshotViewName(frontView);
        return $"{handName}-{viewName}";
    }

    public static MotionComparisonProbeScreenshotCaptureNames BuildScreenshotCaptureNames(
        int recorderFrame,
        int fallbackFrameCount)
    {
        return new MotionComparisonProbeScreenshotCaptureNames(
            BuildScreenshotFrameName(recorderFrame, fallbackFrameCount),
            BuildSampleScreenshotViewName(frontView: true),
            BuildSampleScreenshotViewName(frontView: false),
            BuildFingerCloseupViewName(leftHand: true, frontView: true),
            BuildFingerCloseupViewName(leftHand: true, frontView: false),
            BuildFingerCloseupViewName(leftHand: false, frontView: true),
            BuildFingerCloseupViewName(leftHand: false, frontView: false));
    }

    public static string BuildEvidenceBaseName(
        string sessionStamp,
        string sceneName,
        string comparisonLabel,
        string what,
        string why,
        string how)
    {
        return string.Join("_",
            $"when-{BuildEvidenceFileNamePart(sessionStamp)}",
            $"where-{BuildEvidenceFileNamePart(sceneName)}",
            $"who-{BuildEvidenceFileNamePart(comparisonLabel)}",
            $"what-{BuildEvidenceFileNamePart(what)}",
            $"why-{BuildEvidenceFileNamePart(why)}",
            $"how-{BuildEvidenceFileNamePart(how)}");
    }

    public static string BuildMetricsEvidenceBaseName(string sessionStamp, string sceneName, string comparisonLabel)
    {
        return BuildEvidenceBaseName(sessionStamp, sceneName, comparisonLabel, "metrics", "session", "probe");
    }

    public static string BuildComparisonSessionIdBaseName(string sessionStamp, string sceneName, string comparisonLabel)
    {
        return BuildEvidenceBaseName(sessionStamp, sceneName, comparisonLabel, "comparison-session", "motion-analysis", "probe");
    }

    public static string BuildSessionManifestFileName()
    {
        return "index.md";
    }

    public static string BuildFrameSessionIndexFileName()
    {
        return "session_index.md";
    }

    public static string BuildMetricsCsvExtension()
    {
        return ".csv";
    }

    public static string BuildMetricsCsvFileName(string evidenceBaseName)
    {
        return (evidenceBaseName ?? "") + BuildMetricsCsvExtension();
    }

    public static string BuildMmdModelScreenshotPath(string screenshotPath)
    {
        if (string.IsNullOrEmpty(screenshotPath))
        {
            return "";
        }

        string directory = Path.GetDirectoryName(screenshotPath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(screenshotPath);
        string extension = Path.GetExtension(screenshotPath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileNameWithoutExtension))
        {
            return "";
        }

        return Path.Combine(directory, fileNameWithoutExtension + "_model" + extension);
    }

    public static string BuildMmdAfterPlayModelScreenshotPath(string screenshotsDir)
    {
        return string.IsNullOrEmpty(screenshotsDir)
            ? ""
            : Path.Combine(screenshotsDir, "06_after_play_model.png");
    }

    public static string BuildMmdAfterPlayFullScreenshotPath(string screenshotsDir)
    {
        return string.IsNullOrEmpty(screenshotsDir)
            ? ""
            : Path.Combine(screenshotsDir, "06_after_play.png");
    }

    public static string ResolveMmdReportArtifactPath(string path, string projectRoot, string reportDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        string normalized = NormalizeMmdReportRelativePath(path);
        if (Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        string reportCandidate = string.IsNullOrWhiteSpace(reportDirectory)
            ? ""
            : Path.Combine(reportDirectory, normalized);
        if (!string.IsNullOrEmpty(reportCandidate) &&
            (File.Exists(reportCandidate) || Directory.Exists(reportCandidate)))
        {
            return reportCandidate;
        }

        string projectCandidate = string.IsNullOrWhiteSpace(projectRoot)
            ? ""
            : Path.Combine(projectRoot, normalized);
        if (!string.IsNullOrEmpty(projectCandidate) &&
            (File.Exists(projectCandidate) || Directory.Exists(projectCandidate)))
        {
            return projectCandidate;
        }

        return string.IsNullOrEmpty(projectCandidate) ? normalized : projectCandidate;
    }

    public static string ResolveMmdReportDirectoryPath(string path, string projectRoot, string reportDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        string normalized = NormalizeMmdReportRelativePath(path);
        if (Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        string reportCandidate = string.IsNullOrWhiteSpace(reportDirectory)
            ? ""
            : Path.GetFullPath(Path.Combine(reportDirectory, normalized));
        if (!string.IsNullOrEmpty(reportCandidate) && Directory.Exists(reportCandidate))
        {
            return reportCandidate;
        }

        string projectCandidate = string.IsNullOrWhiteSpace(projectRoot)
            ? ""
            : Path.GetFullPath(Path.Combine(projectRoot, normalized));
        if (!string.IsNullOrEmpty(projectCandidate) && Directory.Exists(projectCandidate))
        {
            return projectCandidate;
        }

        return string.IsNullOrEmpty(projectCandidate) ? normalized : projectCandidate;
    }

    public static string BuildMetricsCsvOutputPath(string comparisonOutputFolder, string metricsCsvFileName)
    {
        return BuildUniqueOutputPath(comparisonOutputFolder, metricsCsvFileName);
    }

    public static MotionComparisonProbeSessionManifestOutputPaths BuildSessionManifestOutputPaths(
        string dataPath,
        string metricsCsvPath,
        string frameFolderPath,
        string frameIndexCsvPath,
        string frameSessionIndexPath)
    {
        return new MotionComparisonProbeSessionManifestOutputPaths(
            metricsCsvRelativePath: MakeProjectRelativePath(dataPath, metricsCsvPath),
            frameFolderRelativePath: MakeProjectRelativePath(dataPath, frameFolderPath),
            frameIndexCsvRelativePath: MakeProjectRelativePath(dataPath, frameIndexCsvPath),
            frameSessionIndexRelativePath: MakeProjectRelativePath(dataPath, frameSessionIndexPath));
    }

    public static MotionComparisonProbeFrameSessionIndexData BuildFrameSessionIndexData(
        string dataPath,
        string sessionId,
        string sessionManifestPath,
        string metricsCsvPath,
        string frameIndexCsvPath)
    {
        return new MotionComparisonProbeFrameSessionIndexData(
            sessionId: sessionId,
            sessionManifestRelativePath: MakeProjectRelativePath(dataPath, sessionManifestPath),
            metricsCsvRelativePath: MakeProjectRelativePath(dataPath, metricsCsvPath),
            frameIndexCsvRelativePath: MakeProjectRelativePath(dataPath, frameIndexCsvPath));
    }

    public static MotionComparisonProbeScreenshotIndexRow BuildScreenshotIndexRow(
        string dataPath,
        string comparisonLabel,
        string sceneName,
        string reason,
        int recorderFrame,
        string viewName,
        string screenshotPath)
    {
        return new MotionComparisonProbeScreenshotIndexRow(
            comparisonLabel: comparisonLabel,
            sceneName: sceneName,
            reason: reason,
            recorderFrame: recorderFrame,
            viewName: viewName,
            relativePath: MakeProjectRelativePath(dataPath, screenshotPath));
    }

    public static void EnsureDirectoryExists(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
    }

    public static string GetOrCreateFolderFromDataPath(string dataPath, params string[] pathSegments)
    {
        string rootPath = GetProjectRootFromDataPath(dataPath);

        string folderPath = rootPath;
        if (pathSegments != null)
        {
            foreach (string segment in pathSegments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                folderPath = Path.Combine(folderPath, segment);
            }
        }

        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    public static string BuildUniqueDirectoryPath(string rootFolder, string folderName)
    {
        string safeFolderName = SanitizePathSegment(folderName);
        string candidate = Path.Combine(rootFolder, safeFolderName);
        int index = 1;

        while (Directory.Exists(candidate))
        {
            candidate = Path.Combine(rootFolder, $"{safeFolderName}_{index:000}");
            index++;
        }

        return candidate;
    }

    public static string ShortenEvidenceBaseNameToFitFile(string folderPath, string baseName, string extension)
    {
        if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(baseName))
        {
            return baseName;
        }

        string normalizedFolder = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string safeBaseName = SanitizePathSegment(baseName);
        string safeExtension = extension ?? string.Empty;
        int maxBaseNameLength = EvidenceSafeMaxFullPathLength
                                - normalizedFolder.Length
                                - 1
                                - safeExtension.Length
                                - EvidenceUniqueSuffixLength;
        maxBaseNameLength = Math.Max(12, maxBaseNameLength);
        return ShortenPathSegmentToLength(safeBaseName, maxBaseNameLength);
    }

    public static string ShortenEvidenceBaseNameToFitFolder(string rootFolder, string folderName, string leafFileName)
    {
        if (string.IsNullOrEmpty(rootFolder) || string.IsNullOrEmpty(folderName))
        {
            return folderName;
        }

        string normalizedRoot = rootFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string safeFolderName = SanitizePathSegment(folderName);
        string safeLeafName = leafFileName ?? string.Empty;
        int maxFolderNameLength = EvidenceSafeMaxFullPathLength
                                  - normalizedRoot.Length
                                  - 1
                                  - 1
                                  - safeLeafName.Length
                                  - EvidenceUniqueSuffixLength;
        maxFolderNameLength = Math.Max(12, maxFolderNameLength);
        return ShortenPathSegmentToLength(safeFolderName, maxFolderNameLength);
    }

    public static string MakeProjectRelativePath(string dataPath, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "";
        }

        string rootPath = GetProjectRootFromDataPath(dataPath);
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!IsSameOrChildPath(fullPath, fullRoot))
        {
            return path.Replace("\\", "/");
        }

        return fullPath
            .Substring(fullRoot.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace("\\", "/");
    }

    public static string MakeProjectRootRelativePath(string projectRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        string fullRoot = NormalizePathForComparison(projectRoot, "");
        string fullPath = NormalizePathForComparison(path, projectRoot);
        if (!string.IsNullOrEmpty(fullRoot) && IsSameOrChildPath(fullPath, fullRoot))
        {
            return fullPath
                .Substring(fullRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace("\\", "/");
        }

        return path.Replace("\\", "/");
    }

    private static bool IsSameOrChildPath(string fullPath, string fullRoot)
    {
        if (string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || fullPath.Length <= fullRoot.Length)
        {
            return false;
        }

        char next = fullPath[fullRoot.Length];
        return next == Path.DirectorySeparatorChar || next == Path.AltDirectorySeparatorChar;
    }

    private static string NormalizePathForComparison(string path, string rootForRelativePath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        string normalized = path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        if (!Path.IsPathRooted(normalized) && !string.IsNullOrWhiteSpace(rootForRelativePath))
        {
            normalized = Path.Combine(rootForRelativePath, normalized);
        }

        try
        {
            return Path.GetFullPath(normalized)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }

    public static string BuildUniqueOutputPath(string folderPath, string fileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string candidate = Path.Combine(folderPath, fileName);
        int index = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(folderPath, $"{baseName}_{index:000}{extension}");
            index++;
        }

        return candidate;
    }

    private static string SanitizePathSegment(string value)
    {
        string cleanName = string.IsNullOrWhiteSpace(value) ? "motion_comparison" : value.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            cleanName = cleanName.Replace(invalidChar, '_');
        }

        return cleanName.Replace(' ', '_');
    }

    private static string ShortenPathSegmentToLength(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        int safeMaxLength = Math.Max(10, maxLength);
        if (value.Length <= safeMaxLength)
        {
            return value;
        }

        const int hashLength = 8;
        int prefixLength = Math.Max(1, safeMaxLength - hashLength - 1);
        return $"{value.Substring(0, prefixLength)}_{CalculateStableHash(value):x8}";
    }

    private static string BuildEvidenceFileNamePart(string value)
    {
        return ShortenPathSegmentToLength(SanitizePathSegment(value), EvidenceFileNamePartMaxLength);
    }

    private static uint CalculateStableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        uint hash = offsetBasis;
        foreach (char character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    private static string NormalizeMmdReportRelativePath(string path)
    {
        return path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
    }
}
