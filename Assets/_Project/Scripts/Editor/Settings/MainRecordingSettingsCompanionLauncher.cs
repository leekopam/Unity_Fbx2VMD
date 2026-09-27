using System;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fbx2Vmd.Settings.EditorTools
{
    [InitializeOnLoad]
    public static class MainRecordingSettingsCompanionLauncher
    {
        public const string MenuPath = "Tools/Graphics/Open Main_recording Settings";
        public const string GeneralMenuPath = "Window/General/Main Recording Settings";

        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string ElectronAppRoot = "Assets/_Project/Tools/MainRecordingSettings";
        private const string NpmExecutableName = "npm";
        private const string NpmArguments = "run start:prod";
        private const string SessionLaunchKey = "Fbx2Vmd.MainRecordingSettings.EditorLaunched";

        static MainRecordingSettingsCompanionLauncher()
        {
            RegisterEditorPlayModeCallback();
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
            EditorApplication.delayCall += TryAutoOpenOnEditorStartup;
        }

        internal static void RegisterEditorPlayModeCallback()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        [MenuItem(GeneralMenuPath)]
        public static void OpenMainRecordingSettings()
        {
            OpenMainRecordingSettingsWithLauncher(MainRecordingSettingsCompanionProcessLauncher.Launch);
        }

        private static bool OpenMainRecordingSettingsWithLauncher(
            Action<MainRecordingSettingsLaunchPlan> launcher)
        {
            MainRecordingSettingsLaunchPlan plan = CreateDefaultLaunchPlan();
            try
            {
                (launcher ?? MainRecordingSettingsCompanionProcessLauncher.Launch)(plan);
                SessionState.SetBool(SessionLaunchKey, true);
                return true;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainRecordingSettingsCompanionLauncher] Web 설정창 실행에 실패했습니다. " +
                    exception.Message);
                return false;
            }
        }

        [MenuItem(MenuPath, true)]
        [MenuItem(GeneralMenuPath, true)]
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
            if (Application.isBatchMode)
            {
                return;
            }

            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                WriteRuntimeState(MainRecordingSettingsState.Stopped);
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                WriteRuntimeState(MainRecordingSettingsState.Playing);
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

        private static void TryAutoOpenOnEditorStartup()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            WriteRuntimeState(EditorApplication.isPlaying
                ? MainRecordingSettingsState.Playing
                : MainRecordingSettingsState.Stopped);
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                !SessionState.GetBool(SessionLaunchKey, false))
            {
                OpenMainRecordingSettings();
            }
        }

        private static void OnEditorQuitting()
        {
            if (!Application.isBatchMode)
            {
                WriteRuntimeState(MainRecordingSettingsState.Stopped);
            }
        }

        private static void WriteRuntimeState(string playMode)
        {
            try
            {
                var store = new MainRecordingSettingsStore();
                MainRecordingSettingsDocument document = store.LoadOrCreateDefault();
                document.runtimeState = MainRecordingSettingsState.Create(playMode, DateTime.UtcNow);
                store.Save(document);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainRecordingSettingsCompanionLauncher] Play 상태 기록에 실패했습니다. " +
                    exception.Message);
            }
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
                () => OpenMainRecordingSettingsWithLauncher(MainRecordingSettingsCompanionProcessLauncher.Launch));
        }

        private static bool TryAutoLaunchWebSettingsForPlayModeWithLauncher(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState,
            Func<bool> openSettings)
        {
            if (!ShouldAutoLaunchWebSettingsForPlayMode(scenePath, isBatchMode, playModeState) ||
                SessionState.GetBool(SessionLaunchKey, false))
            {
                return false;
            }

            return (openSettings ??
                (() => OpenMainRecordingSettingsWithLauncher(MainRecordingSettingsCompanionProcessLauncher.Launch)))();
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
                () =>
                {
                    openSettings();
                    SessionState.SetBool(SessionLaunchKey, true);
                    return true;
                });
        }

        private static void ResetAutoLaunchWebSettingsForTests()
        {
            SessionState.SetBool(SessionLaunchKey, false);
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
