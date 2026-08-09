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
