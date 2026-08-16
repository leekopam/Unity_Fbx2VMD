internal static class MotionComparisonProbeScreenshotIndexCsvHeaderFormatter
{
    private const string ScreenshotIndexCsvHeader = "label,scene,reason,recorderFrame,view,path";

    internal static string Build()
    {
        return ScreenshotIndexCsvHeader;
    }
}
