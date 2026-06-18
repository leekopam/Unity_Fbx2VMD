using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Member_Han.Modules.Graphics
{
    public static class MainRecordingSettingsRuntimeLauncher
    {
        public const string SettingsFolderName = "MainRecordingSettings";
        public const string SettingsExecutableFileName = "Unity_Fbx2VMD_Settings.exe";

        private const string SettingsPathArgumentName = "--settings-path";
        private const string ElectronRunAsNodeEnvironmentVariableName = "ELECTRON_RUN_AS_NODE";

        private static readonly Func<MainRecordingSettingsLaunchPlan, Process> DefaultLaunchProcess =
            LaunchProcess;
        private static Func<MainRecordingSettingsLaunchPlan, Process> launchProcess = DefaultLaunchProcess;
        private static Process startedProcess;

        public static bool ShouldAutoLaunchForPlayer(bool requestedOpen, bool isEditor, bool isBatchMode)
        {
            return requestedOpen && !isEditor && !isBatchMode;
        }

        public static bool ShouldOpenGameViewPopupFallback(
            bool requestedOpen,
            bool isEditor,
            bool isBatchMode,
            bool launchSucceeded)
        {
            return requestedOpen && !isEditor && !isBatchMode && !launchSucceeded;
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
            if (IsStartedSettingsProcessRunning())
            {
                return MainRecordingSettingsActionResult.Success("Web 설정창이 이미 실행 중입니다.");
            }

            MainRecordingSettingsLaunchPlan plan = CreateLaunchPlan(playerExecutableDirectory, settingsPath);
            if (!Directory.Exists(plan.WorkingDirectory))
            {
                return MainRecordingSettingsActionResult.Failure(
                    $"Web 설정창 폴더를 찾을 수 없습니다: {plan.WorkingDirectory}");
            }

            if (!File.Exists(plan.ExecutablePath))
            {
                return MainRecordingSettingsActionResult.Failure(
                    $"Web 설정창 실행 파일을 찾을 수 없습니다: {plan.ExecutablePath}");
            }

            try
            {
                startedProcess = launchProcess(plan);
                if (startedProcess == null)
                {
                    return MainRecordingSettingsActionResult.Failure("Web 설정창 프로세스 시작 결과가 비어 있습니다.");
                }

                return MainRecordingSettingsActionResult.Success("Web 설정창을 실행했습니다.");
            }
            catch (Exception exception)
            {
                return MainRecordingSettingsActionResult.Failure(
                    $"Web 설정창 실행에 실패했습니다: {exception.Message}");
            }
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
            Process process = startedProcess;
            startedProcess = null;
            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainRecordingSettingsRuntimeLauncher] Web 설정창 종료 요청에 실패했습니다. " +
                    exception.Message);
            }
            finally
            {
                process.Dispose();
            }
        }

        private static bool IsStartedSettingsProcessRunning()
        {
            return IsProcessRunning(startedProcess);
        }

        private static bool IsProcessRunning(Process process)
        {
            if (process == null)
            {
                return false;
            }

            try
            {
                return !process.HasExited;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Process LaunchProcess(MainRecordingSettingsLaunchPlan plan)
        {
            return Process.Start(CreateProcessStartInfo(plan));
        }

        private static ProcessStartInfo CreateProcessStartInfo(MainRecordingSettingsLaunchPlan plan)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = plan.ExecutablePath,
                Arguments = plan.Arguments,
                WorkingDirectory = plan.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            startInfo.Environment[MainRecordingSettingsPathResolver.EnvironmentVariableName] = plan.SettingsPath;
            startInfo.Environment.Remove(ElectronRunAsNodeEnvironmentVariableName);

            return startInfo;
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

        private static MainRecordingSettingsLaunchPlan CreateLaunchPlanForTests(
            string playerExecutableDirectory,
            string settingsPath)
        {
            return CreateLaunchPlan(playerExecutableDirectory, settingsPath);
        }

        private static bool ShouldAutoLaunchForPlayerForTests(
            bool requestedOpen,
            bool isEditor,
            bool isBatchMode)
        {
            return ShouldAutoLaunchForPlayer(requestedOpen, isEditor, isBatchMode);
        }

        private static bool ShouldOpenGameViewPopupFallbackForTests(
            bool requestedOpen,
            bool isEditor,
            bool isBatchMode,
            bool launchSucceeded)
        {
            return ShouldOpenGameViewPopupFallback(requestedOpen, isEditor, isBatchMode, launchSucceeded);
        }

        private static void SetLaunchProcessForTests(Func<MainRecordingSettingsLaunchPlan, Process> launcher)
        {
            launchProcess = launcher ?? DefaultLaunchProcess;
        }

        private static void ResetLaunchProcessForTests()
        {
            launchProcess = DefaultLaunchProcess;
            startedProcess = null;
        }

        private static bool IsStartedSettingsProcessRunningForTests()
        {
            return IsStartedSettingsProcessRunning();
        }

        private static ProcessStartInfo CreateProcessStartInfoForTests(MainRecordingSettingsLaunchPlan plan)
        {
            return CreateProcessStartInfo(plan);
        }
    }
}
