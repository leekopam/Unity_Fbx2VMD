using System;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.Settings
{
    public static class MainRecordingSettingsPathResolver
    {
        public const string EnvironmentVariableName = "UNITY_FBX2VMD_MAIN_RECORDING_SETTINGS_PATH";
        public const string AppFolderName = "Unity_Fbx2VMD";
        public const string FeatureFolderName = "MainRecordingSettings";
        public const string SettingsFileName = "main-recording-settings.json";

        public static string ResolveSettingsFilePath(
            string explicitPath = null,
            string environmentOverridePath = null,
            string localAppDataRoot = null,
            string persistentDataRoot = null,
            bool readProcessEnvironment = true)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                return explicitPath.Trim();
            }

            if (!string.IsNullOrWhiteSpace(environmentOverridePath))
            {
                return environmentOverridePath.Trim();
            }

            if (readProcessEnvironment)
            {
                string processOverridePath = Environment.GetEnvironmentVariable(EnvironmentVariableName);
                if (!string.IsNullOrWhiteSpace(processOverridePath))
                {
                    return processOverridePath.Trim();
                }
            }

            string root = localAppDataRoot;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                root = persistentDataRoot;
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                root = Application.persistentDataPath;
            }

            return Path.Combine(root, AppFolderName, FeatureFolderName, SettingsFileName);
        }
    }
}
