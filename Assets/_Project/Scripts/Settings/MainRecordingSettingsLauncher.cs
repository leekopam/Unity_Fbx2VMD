using System;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.Settings
{
    public static class MainRecordingSettingsLauncher
    {
        public const string SettingsFolderName = "MainRecordingSettings";
        public const string SettingsExecutableFileName = "Unity_Fbx2VMD_Settings.exe";

        private const string SettingsPathArgumentName = "--settings-path";
        private static readonly MainRecordingSettingsPlayerProcessSession playerProcessSession =
            new MainRecordingSettingsPlayerProcessSession();

        public static bool ShouldAutoLaunchForPlayer(bool requestedOpen, bool isEditor, bool isBatchMode)
        {
            return requestedOpen && !isEditor && !isBatchMode;
        }

        public static MainRecordingSettingsActionResult TryLaunchForPlayer(
            bool requestedOpen,
            string settingsFilePathOverride = null)
        {
            if (!ShouldAutoLaunchForPlayer(requestedOpen, Application.isEditor, Application.isBatchMode))
            {
                return MainRecordingSettingsActionResult.Success("Player 설정창 자동 실행 대상이 아닙니다.");
            }

            string settingsPath = MainRecordingSettingsPathResolver.ResolveSettingsFilePath(settingsFilePathOverride);
            return TryLaunch(ResolvePlayerExecutableDirectory(), settingsPath);
        }

        public static MainRecordingSettingsActionResult TryLaunch(string playerExecutableDirectory, string settingsPath)
        {
            return playerProcessSession.TryLaunch(
                () => CreateLaunchPlan(playerExecutableDirectory, settingsPath));
        }

        public static MainRecordingSettingsLaunchPlan CreateLaunchPlan(
            string playerExecutableDirectory,
            string settingsPath)
        {
            string normalizedPlayerDirectory = string.IsNullOrWhiteSpace(playerExecutableDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : playerExecutableDirectory.Trim();
            string settingsDirectory = Path.Combine(normalizedPlayerDirectory, SettingsFolderName);
            string executablePath = Path.Combine(settingsDirectory, SettingsExecutableFileName);

            return new MainRecordingSettingsLaunchPlan(
                settingsDirectory,
                executablePath,
                SettingsPathArgumentName + " " + QuoteArgument(settingsPath),
                settingsPath);
        }

        public static void CloseStartedProcessQuietly()
        {
            playerProcessSession.CloseQuietly();
        }

        public static bool IsSettingsProcessRunning()
        {
            return playerProcessSession.IsRunning();
        }

        private static string ResolvePlayerExecutableDirectory()
        {
            string dataPath = Application.dataPath;
            if (!string.IsNullOrWhiteSpace(dataPath))
            {
                string fullDataPath = Path.GetFullPath(dataPath);
                string trimmedDataPath = fullDataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dataFolderName = Path.GetFileName(trimmedDataPath);
                if (dataFolderName.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
                {
                    DirectoryInfo parent = Directory.GetParent(trimmedDataPath);
                    if (parent != null)
                    {
                        return parent.FullName;
                    }
                }
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string QuoteArgument(string value)
        {
            string safeValue = value ?? string.Empty;
            return "\"" + safeValue.Replace("\"", "\\\"") + "\"";
        }
    }
}
