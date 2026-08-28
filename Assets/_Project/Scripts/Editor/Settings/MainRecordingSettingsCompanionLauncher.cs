using System;
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
        private static bool hasAutoLaunchedWebSettingsForCurrentPlayMode;

        internal static void RegisterEditorPlayModeCallback()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void OpenMainRecordingSettings()
        {
            OpenMainRecordingSettingsWithLauncher(MainRecordingSettingsCompanionProcessLauncher.Launch);
        }

        private static void OpenMainRecordingSettingsWithLauncher(
            Action<MainRecordingSettingsLaunchPlan> launcher)
        {
            MainRecordingSettingsLaunchPlan plan = CreateDefaultLaunchPlan();
            try
            {
                (launcher ?? MainRecordingSettingsCompanionProcessLauncher.Launch)(plan);
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

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                hasAutoLaunchedWebSettingsForCurrentPlayMode = false;
                return;
            }

            TryAutoLaunchWebSettingsForPlayMode(
                SceneManager.GetActiveScene().path,
                Application.isBatchMode,
                state);
        }

        private static bool ShouldAutoLaunchWebSettingsForPlayMode(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState)
        {
            return !isBatchMode &&
                   playModeState == PlayModeStateChange.EnteredPlayMode &&
                   ShouldOpenForScene(scenePath);
        }

        private static bool TryAutoLaunchWebSettingsForPlayMode(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState)
        {
            return TryAutoLaunchWebSettingsForPlayModeWithLauncher(
                scenePath,
                isBatchMode,
                playModeState,
                OpenMainRecordingSettings);
        }

        private static bool TryAutoLaunchWebSettingsForPlayModeWithLauncher(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState,
            Action openSettings)
        {
            if (!ShouldAutoLaunchWebSettingsForPlayMode(scenePath, isBatchMode, playModeState) ||
                hasAutoLaunchedWebSettingsForCurrentPlayMode)
            {
                return false;
            }

            hasAutoLaunchedWebSettingsForCurrentPlayMode = true;
            (openSettings ?? OpenMainRecordingSettings)();
            return true;
        }

        private static string GetMainRecordingScenePathForTests()
        {
            return MainRecordingScenePath;
        }

        private static bool CanLaunchWebSettingsForTests()
        {
            return CanLaunchWebSettings();
        }

        private static void OpenMainRecordingSettingsForTests(
            Action<MainRecordingSettingsLaunchPlan> launcher)
        {
            OpenMainRecordingSettingsWithLauncher(launcher);
        }

        private static bool ShouldAutoLaunchWebSettingsForPlayModeForTests(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState)
        {
            return ShouldAutoLaunchWebSettingsForPlayMode(scenePath, isBatchMode, playModeState);
        }

        private static bool TryAutoLaunchWebSettingsForPlayModeForTests(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState,
            Action openSettings)
        {
            return TryAutoLaunchWebSettingsForPlayModeWithLauncher(
                scenePath,
                isBatchMode,
                playModeState,
                openSettings);
        }

        private static void ResetAutoLaunchWebSettingsForTests()
        {
            hasAutoLaunchedWebSettingsForCurrentPlayMode = false;
        }

        private static bool CanLaunchWebSettings()
        {
            return MainRecordingSettingsCompanionProcessLauncher.HasRequiredCompanionFiles(ElectronAppRoot);
        }

        private static string NormalizeScenePath(string scenePath)
        {
            return string.IsNullOrWhiteSpace(scenePath)
                ? string.Empty
                : scenePath.Replace('\\', '/').Trim();
        }
    }
}
