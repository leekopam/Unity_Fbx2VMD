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
        public MainRecordingSettingsState runtimeState = new MainRecordingSettingsState();
        public MainRecordingSettingsCommandEnvelope pendingCommand = new MainRecordingSettingsCommandEnvelope();
        /// <summary>마지막으로 소비된 명령의 결과. Electron이 폴링해 상태를 표시한다.</summary>
        public MainRecordingSettingsCommandResult lastCommandResult = new MainRecordingSettingsCommandResult();
    }
}
