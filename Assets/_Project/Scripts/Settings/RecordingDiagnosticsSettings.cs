namespace Fbx2Vmd.Settings
{
    public readonly struct RecordingDiagnosticsSettings
    {
        public RecordingDiagnosticsSettings(
            bool enableRecordingDiagnostics,
            bool useDeterministicCaptureFramerateForDiagnostics,
            bool enableDiagnosticFingerCloseups,
            RecordingCaptureQualityPreset captureQuality,
            int customCaptureWidth,
            int customCaptureHeight)
        {
            EnableRecordingDiagnostics = enableRecordingDiagnostics;
            UseDeterministicCaptureFramerateForDiagnostics =
                useDeterministicCaptureFramerateForDiagnostics;
            EnableDiagnosticFingerCloseups = enableDiagnosticFingerCloseups;
            CaptureQuality = captureQuality;
            CustomCaptureWidth = customCaptureWidth;
            CustomCaptureHeight = customCaptureHeight;
        }

        public bool EnableRecordingDiagnostics { get; }
        public bool UseDeterministicCaptureFramerateForDiagnostics { get; }
        public bool EnableDiagnosticFingerCloseups { get; }
        public RecordingCaptureQualityPreset CaptureQuality { get; }
        public int CustomCaptureWidth { get; }
        public int CustomCaptureHeight { get; }

        public RecordingCaptureResolutionPlan CreateCaptureResolutionPlan()
        {
            return RecordingCaptureResolution.CreatePlan(
                CaptureQuality,
                CustomCaptureWidth,
                CustomCaptureHeight);
        }
    }
}
