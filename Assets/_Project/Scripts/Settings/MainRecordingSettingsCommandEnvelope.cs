using System;

namespace Fbx2Vmd.Settings
{
    [Serializable]
    public sealed class MainRecordingSettingsCommandEnvelope
    {
        public const string ImportFbxAction = "ImportFbx";
        public const string LoadCharacterAction = "LoadCharacter";
        public const string ApplyPresetAction = "ApplyPreset";
        public const string SavePresetAction = "SavePreset";
        public const string RestoreSceneCharacterAction = "RestoreSceneCharacter";
        public const string RegisterVrmAction = "RegisterVrm";
        public const string RemoveCharacterAction = "RemoveCharacter";
        public const string RefreshCharacterAction = "RefreshCharacter";
        public const string ReconnectCharacterAction = "ReconnectCharacter";
        public const string SetDefaultPresetAction = "SetDefaultPreset";
        public const string DeletePresetAction = "DeletePreset";
        public const string RenamePresetAction = "RenamePreset";
        public const string CancelLoadAction = "CancelLoad";
        public const string CopyPresetAction = "CopyPreset";
        public const string UpdatePresetAction = "UpdatePreset";

        public string commandId = string.Empty;
        public string action = string.Empty;
        public string fbxPath = string.Empty;
        public string characterId = string.Empty;
        public string presetId = string.Empty;
        public string path = string.Empty;
        public string requestedAtUtc = string.Empty;
        /// <summary>액션별 추가 파라미터(JSON 문자열). JsonUtility가 Dictionary를
        /// 직렬화하지 못하므로 문자열로 운반하고 소비 측에서 파싱한다.</summary>
        public string paramsJson = string.Empty;

        public bool IsImportFbxCommand()
        {
            return string.Equals(action, ImportFbxAction, StringComparison.Ordinal) &&
                   !string.IsNullOrWhiteSpace(commandId);
        }

        public bool IsPending()
        {
            return !string.IsNullOrWhiteSpace(commandId) &&
                   !string.IsNullOrWhiteSpace(action);
        }
    }
}
