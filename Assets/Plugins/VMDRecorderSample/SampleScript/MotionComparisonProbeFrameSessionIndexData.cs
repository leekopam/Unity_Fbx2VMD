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
