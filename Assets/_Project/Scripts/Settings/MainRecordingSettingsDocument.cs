using System;

namespace Fbx2Vmd.Settings
{
    [Serializable]
    public sealed class MainRecordingSettingsDocument
    {
        public int schemaVersion = 1;
        public string updatedAtUtc = string.Empty;
        public string fbxPath = string.Empty;
        public string characterModelPath = string.Empty;
        public int captureWidth = 1920;
        public int captureHeight = 1080;
        public bool openSettingsOnStart = true;
        public MainRecordingSettingsRuntimeState runtimeState = new MainRecordingSettingsRuntimeState();
        public MainRecordingSettingsCommandEnvelope pendingCommand = new MainRecordingSettingsCommandEnvelope();
    }

    [Serializable]
    public sealed class MainRecordingSettingsCommandEnvelope
    {
        public const string ImportFbxAction = "ImportFbx";

        public string commandId = string.Empty;
        public string action = string.Empty;
        public string fbxPath = string.Empty;
        public string requestedAtUtc = string.Empty;

        public bool IsImportFbxCommand()
        {
            return string.Equals(action, ImportFbxAction, StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(commandId);
        }
    }
}
