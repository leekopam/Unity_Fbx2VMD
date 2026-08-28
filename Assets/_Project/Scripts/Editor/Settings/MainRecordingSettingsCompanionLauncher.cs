using System;
using System.Diagnostics;
using System.IO;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fbx2Vmd.Settings.EditorTools
{
    public static class MainRecordingSettingsCompanionLauncher
    {
        public const string MenuPath = "Tools/Graphics/Open Main_recording Settings";

        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string ElectronAppRoot = "Assets/_Project/Tools/MainRecordingSettings";
        private const string NpmExecutableName = "npm";
        private const string NpmArguments = "run start:prod";

        [MenuItem(MenuPath)]
        public static void OpenMainRecordingSettings()
        {
            OpenMainRecordingSettingsWithLauncher(LaunchWebSettings);
        }

        private static void OpenMainRecordingSettingsWithLauncher(
            Action<MainRecordingSettingsLaunchPlan> launcher)
        {
            MainRecordingSettingsLaunchPlan plan = CreateDefaultLaunchPlan();
            try
            {
                (launcher ?? LaunchWebSettings)(plan);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainRecordingSettingsCompanionLauncher] Web 설정창 실행에 실패했습니다. " +
                    exception.Message);
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOpenMainRecordingSettings()
        {
            return CanLaunchWebSettings();
        }

        public static bool ShouldOpenForScene(string scenePath)
        {
            return string.Equals(NormalizeScenePath(scenePath), MainRecordingScenePath, StringComparison.OrdinalIgnoreCase);
        }

        public static MainRecordingSettingsLaunchPlan CreateDefaultLaunchPlan()
        {
            return new MainRecordingSettingsLaunchPlan(
                ElectronAppRoot,
                NpmExecutableName,
                NpmArguments,
                MainRecordingSettingsPathResolver.ResolveSettingsFilePath());
        }

        private static string GetMenuPathForTests()
        {
            return MenuPath;
        }

        private static string GetMainRecordingScenePathForTests()
        {
            return MainRecordingScenePath;
        }

        private static string GetEditorSurfacePolicyForTests()
        {
            return MainRecordingSettingsSurfacePolicy.EditorSurfacePolicy;
        }

        private static bool CanLaunchWebSettingsForTests()
        {
            return CanLaunchWebSettings();
        }

        private static MainRecordingSettingsLaunchPlan CreateDefaultLaunchPlanForTests()
        {
            return CreateDefaultLaunchPlan();
        }

        private static void OpenMainRecordingSettingsForTests(
            Action<MainRecordingSettingsLaunchPlan> launcher)
        {
            OpenMainRecordingSettingsWithLauncher(launcher);
        }

        private static bool CanLaunchWebSettings()
        {
            return File.Exists(Path.Combine(ElectronAppRoot, "package.json")) &&
                   File.Exists(Path.Combine(ElectronAppRoot, "electron", "main.js"));
        }

        private static void LaunchWebSettings(MainRecordingSettingsLaunchPlan plan)
        {
            string fullWorkingDirectory = Path.GetFullPath(plan.WorkingDirectory);
            if (!Directory.Exists(fullWorkingDirectory))
            {
                throw new DirectoryNotFoundException(fullWorkingDirectory);
            }

            string npmExecutable = ResolveNpmExecutable();
            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveProcessFileName(npmExecutable),
                Arguments = ResolveProcessArguments(npmExecutable, plan.Arguments),
                WorkingDirectory = fullWorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment[MainRecordingSettingsPathResolver.EnvironmentVariableName] = plan.SettingsPath;

            Process.Start(startInfo);
        }

        private static string ResolveNpmExecutable()
        {
            string pathCommand = FindOnPath("npm.cmd");
            if (!string.IsNullOrEmpty(pathCommand))
            {
                return pathCommand;
            }

            string standardPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "nodejs",
                "npm.cmd");
            return File.Exists(standardPath) ? standardPath : NpmExecutableName;
        }

        private static string ResolveProcessFileName(string executable)
        {
            return IsWindowsCommandScript(executable)
                ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
                : executable;
        }

        private static string ResolveProcessArguments(string executable, string arguments)
        {
            return IsWindowsCommandScript(executable)
                ? "/d /c \"\"" + executable + "\" " + arguments + "\""
                : arguments;
        }

        private static bool IsWindowsCommandScript(string executable)
        {
            return executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                   executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        }

        private static string FindOnPath(string fileName)
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                string candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static string NormalizeScenePath(string scenePath)
        {
            return string.IsNullOrWhiteSpace(scenePath)
                ? string.Empty
                : scenePath.Replace('\\', '/').Trim();
        }
    }
}
