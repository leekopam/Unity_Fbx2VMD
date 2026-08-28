using System;

namespace Fbx2Vmd.Settings
{
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
