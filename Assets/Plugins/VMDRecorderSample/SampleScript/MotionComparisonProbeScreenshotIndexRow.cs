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
