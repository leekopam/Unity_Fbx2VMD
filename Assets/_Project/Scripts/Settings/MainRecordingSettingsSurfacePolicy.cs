namespace Fbx2Vmd.Settings
{
    public static class MainRecordingSettingsSurfacePolicy
    {
        public const string EditorSurface = "electron web launcher";
        public const string EditorSurfacePolicy =
            "Editor settings surface launches the Electron Web UI through MainRecordingSettingsCompanionLauncher; GameView popup stays fallback/manual only.";
        public const string ProductionSurface = "electron web companion";
        public const string FallbackSurface = "runtime popup development fallback";
        public const string DeliveryPolicy =
            "Production settings surface launches the packaged Electron/Web companion beside the Player, then uses HTTP/WebSocket bridge and shared settings file.";

        public static bool ShouldAutoOpenRuntimePopup(bool requestedOpen, bool isEditor, bool isBatchMode = false)
        {
            return requestedOpen && !isEditor && !isBatchMode;
        }
    }
}
