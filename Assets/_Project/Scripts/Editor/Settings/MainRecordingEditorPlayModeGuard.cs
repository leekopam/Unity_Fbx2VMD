using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fbx2Vmd.Settings.EditorTools
{
    [InitializeOnLoad]
    internal static class MainRecordingEditorPlayModeGuard
    {
        private const string BurstDisableCompilationEnvironmentVariable = "UNITY_BURST_DISABLE_COMPILATION";
        private const string BurstDisableCompilationValue = "1";
        private const string BurstDisableCleanCompileSessionKey =
            "MainRecordingEditorPlayModeGuard.BurstDisableCleanCompileRequested";
        private const string BurstCleanCompileLogMessage =
            "[MainRecordingEditorPlayModeGuard] Main_Recoding Play 준비를 위해 Burst direct-call 컴파일을 비활성화하고 스크립트 clean compile을 요청했습니다.";
        private const string GuardPolicy =
            "Main_Recoding Editor Play Mode sets UNITY_BURST_DISABLE_COMPILATION=1 and requests one clean script compilation to avoid Burst direct-call initializer failures; Unity Playmode tint is neutralized while Play Mode is active.";
        private static readonly Color NeutralPlayModeTint = Color.white;
        private static bool hasSavedPlayModeTint;
        private static Color savedPlayModeTint;
        private static bool hasSavedBurstCompilation;
        private static bool savedBurstCompilation;
        private static bool hasSavedBurstDisableEnvironment;
        private static string savedBurstDisableEnvironmentValue;
        private static bool hasAutoLaunchedWebSettingsForCurrentPlayMode;

        static MainRecordingEditorPlayModeGuard()
        {
            if (ShouldMaintainEditorPlayModeGuard(
                    SceneManager.GetActiveScene().path,
                    Application.isBatchMode,
                    EditorApplication.isPlayingOrWillChangePlaymode))
            {
                ApplyBeforeMainRecordingPlayMode();
            }

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= MaintainEditModeGuard;
            EditorApplication.update += MaintainEditModeGuard;
        }

        private static void MaintainEditModeGuard()
        {
            if (ShouldMaintainEditorPlayModeGuard(
                    SceneManager.GetActiveScene().path,
                    Application.isBatchMode,
                    EditorApplication.isPlayingOrWillChangePlaymode))
            {
                ApplyBeforeMainRecordingPlayMode();
                return;
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                RestoreEditorPlayModeState();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (ShouldApplyEditorPlayModeGuard(SceneManager.GetActiveScene().path, Application.isBatchMode, state))
            {
                ApplyBeforeMainRecordingPlayMode();
                TryAutoLaunchWebSettingsForPlayMode(SceneManager.GetActiveScene().path, Application.isBatchMode, state);
                return;
            }

            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                hasAutoLaunchedWebSettingsForCurrentPlayMode = false;
                RestoreEditorPlayModeState();
            }
        }

        private static bool ShouldApplyEditorPlayModeGuard(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState)
        {
            return !isBatchMode &&
                   (playModeState == PlayModeStateChange.ExitingEditMode ||
                    playModeState == PlayModeStateChange.EnteredPlayMode) &&
                   MainRecordingSettingsCompanionLauncher.ShouldOpenForScene(scenePath);
        }

        private static bool ShouldMaintainEditorPlayModeGuard(
            string scenePath,
            bool isBatchMode,
            bool isPlayingOrWillChangePlaymode)
        {
            return !isBatchMode &&
                   MainRecordingSettingsCompanionLauncher.ShouldOpenForScene(scenePath);
        }

        private static bool ShouldAutoLaunchWebSettingsForPlayMode(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState)
        {
            return !isBatchMode &&
                   playModeState == PlayModeStateChange.EnteredPlayMode &&
                   MainRecordingSettingsCompanionLauncher.ShouldOpenForScene(scenePath);
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
                MainRecordingSettingsCompanionLauncher.OpenMainRecordingSettings);
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
            (openSettings ?? MainRecordingSettingsCompanionLauncher.OpenMainRecordingSettings)();
            return true;
        }

        private static void ApplyBeforeMainRecordingPlayMode()
        {
            SaveEditorPlayModeStateBeforeGuard();
            EnsureBurstDirectCallCompilationDisabledForScene(SceneManager.GetActiveScene().path);
            TrySetBurstCompilation(false);

            if (!hasSavedPlayModeTint && TryGetPlayModeTint(out Color playModeTint))
            {
                savedPlayModeTint = playModeTint;
                hasSavedPlayModeTint = true;
                TrySetPlayModeTint(NeutralPlayModeTint);
            }
        }

        private static void RestoreEditorPlayModeState()
        {
            if (hasSavedBurstCompilation)
            {
                TrySetBurstCompilation(savedBurstCompilation);
                hasSavedBurstCompilation = false;
            }

            if (hasSavedBurstDisableEnvironment)
            {
                Environment.SetEnvironmentVariable(
                    BurstDisableCompilationEnvironmentVariable,
                    savedBurstDisableEnvironmentValue,
                    EnvironmentVariableTarget.Process);
                savedBurstDisableEnvironmentValue = null;
                hasSavedBurstDisableEnvironment = false;
            }

            if (hasSavedPlayModeTint)
            {
                TrySetPlayModeTint(savedPlayModeTint);
                hasSavedPlayModeTint = false;
            }
        }

        private static void SaveEditorPlayModeStateBeforeGuard()
        {
            if (!hasSavedBurstCompilation && TryGetBurstCompilation(out bool burstCompilation))
            {
                savedBurstCompilation = burstCompilation;
                hasSavedBurstCompilation = true;
            }

            if (!hasSavedBurstDisableEnvironment)
            {
                savedBurstDisableEnvironmentValue = Environment.GetEnvironmentVariable(
                    BurstDisableCompilationEnvironmentVariable,
                    EnvironmentVariableTarget.Process);
                hasSavedBurstDisableEnvironment = true;
            }
        }

        private static bool ShouldApplyEditorPlayModeGuardForTests(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState)
        {
            return ShouldApplyEditorPlayModeGuard(scenePath, isBatchMode, playModeState);
        }

        private static bool ShouldMaintainEditorPlayModeGuardForTests(
            string scenePath,
            bool isBatchMode,
            bool isPlayingOrWillChangePlaymode)
        {
            return ShouldMaintainEditorPlayModeGuard(scenePath, isBatchMode, isPlayingOrWillChangePlaymode);
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

        private static string GetBurstDisableEnvironmentVariableNameForTests()
        {
            return BurstDisableCompilationEnvironmentVariable;
        }

        private static bool IsBurstDisableEnvironmentValueForTests(string value)
        {
            return IsBurstDisableEnvironmentValue(value);
        }

        private static bool ShouldRequestBurstDisableCleanCompilationForTests(
            string environmentValue,
            bool cleanCompileAlreadyRequested,
            bool isBatchMode)
        {
            return ShouldRequestBurstDisableCleanCompilation(
                environmentValue,
                cleanCompileAlreadyRequested,
                isBatchMode);
        }

        private static string GetEditorPlayModeGuardPolicyForTests()
        {
            return GuardPolicy;
        }

        private static bool CanReflectBurstCompilerOptionsForTests()
        {
            return TryGetBurstCompilation(out _);
        }

        private static bool CanReflectPlayModeTintForTests()
        {
            return TryGetPlayModeTint(out _);
        }

        private static bool IsNeutralPlayModeTintForTests(Color color)
        {
            return Mathf.Abs(color.r - 1f) <= 0.0001f &&
                   Mathf.Abs(color.g - 1f) <= 0.0001f &&
                   Mathf.Abs(color.b - 1f) <= 0.0001f &&
                   Mathf.Abs(color.a - 1f) <= 0.0001f;
        }

        private static bool GetCurrentBurstCompilationForTests()
        {
            return TryGetBurstCompilation(out bool enabled) && enabled;
        }

        private static bool ApplyBurstCompilationForTests(bool enabled)
        {
            if (!TryGetBurstCompilation(out bool current))
            {
                return false;
            }

            TrySetBurstCompilation(enabled);
            return current;
        }

        private static Color GetCurrentPlayModeTintForTests()
        {
            return TryGetPlayModeTint(out Color color) ? color : Color.clear;
        }

        private static Color ApplyPlayModeTintForTests(Color color)
        {
            if (!TryGetPlayModeTint(out Color current))
            {
                return Color.clear;
            }

            TrySetPlayModeTint(color);
            return current;
        }

        private static void EnsureBurstDirectCallCompilationDisabledForScene(string scenePath)
        {
            if (Application.isBatchMode || !MainRecordingSettingsCompanionLauncher.ShouldOpenForScene(scenePath))
            {
                return;
            }

            string environmentValue = Environment.GetEnvironmentVariable(BurstDisableCompilationEnvironmentVariable);
            bool shouldRequestCleanCompile = ShouldRequestBurstDisableCleanCompilation(
                environmentValue,
                SessionState.GetBool(BurstDisableCleanCompileSessionKey, false),
                Application.isBatchMode);

            if (!IsBurstDisableEnvironmentValue(environmentValue))
            {
                Environment.SetEnvironmentVariable(
                    BurstDisableCompilationEnvironmentVariable,
                    BurstDisableCompilationValue,
                    EnvironmentVariableTarget.Process);
            }

            TrySetBurstCompilation(false);
            if (!shouldRequestCleanCompile)
            {
                return;
            }

            SessionState.SetBool(BurstDisableCleanCompileSessionKey, true);
            UnityEngine.Debug.Log(BurstCleanCompileLogMessage);
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }

        private static bool ShouldRequestBurstDisableCleanCompilation(
            string environmentValue,
            bool cleanCompileAlreadyRequested,
            bool isBatchMode)
        {
            return !isBatchMode &&
                   !cleanCompileAlreadyRequested &&
                   !IsBurstDisableEnvironmentValue(environmentValue);
        }

        private static bool IsBurstDisableEnvironmentValue(string value)
        {
            return !string.IsNullOrEmpty(value) && value != "0";
        }

        private static bool TryGetBurstCompilation(out bool enabled)
        {
            enabled = true;
            object options = GetBurstCompilerOptions();
            PropertyInfo property = GetBurstCompilationProperty(options);
            if (options == null || property == null || property.PropertyType != typeof(bool))
            {
                return false;
            }

            enabled = (bool)property.GetValue(options);
            return true;
        }

        private static bool TrySetBurstCompilation(bool enabled)
        {
            object options = GetBurstCompilerOptions();
            PropertyInfo property = GetBurstCompilationProperty(options);
            if (options == null || property == null || property.PropertyType != typeof(bool) || !property.CanWrite)
            {
                return false;
            }

            property.SetValue(options, enabled);
            return true;
        }

        private static object GetBurstCompilerOptions()
        {
            Type burstCompilerType = Type.GetType("Unity.Burst.BurstCompiler, Unity.Burst");
            FieldInfo optionsField = burstCompilerType?.GetField(
                "Options",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return optionsField?.GetValue(null);
        }

        private static PropertyInfo GetBurstCompilationProperty(object options)
        {
            return options?.GetType().GetProperty(
                "EnableBurstCompilation",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static bool TryGetPlayModeTint(out Color color)
        {
            color = Color.clear;
            object prefColor = GetPlayModeTintPrefColor();
            PropertyInfo property = GetPrefColorProperty(prefColor);
            if (prefColor == null || property == null || property.PropertyType != typeof(Color))
            {
                return false;
            }

            color = (Color)property.GetValue(prefColor);
            return true;
        }

        private static bool TrySetPlayModeTint(Color color)
        {
            object prefColor = GetPlayModeTintPrefColor();
            PropertyInfo property = GetPrefColorProperty(prefColor);
            if (prefColor == null || property == null || property.PropertyType != typeof(Color) || !property.CanWrite)
            {
                return false;
            }

            property.SetValue(prefColor, color);
            return true;
        }

        private static object GetPlayModeTintPrefColor()
        {
            Type hostViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.HostView");
            FieldInfo field = hostViewType?.GetField(
                "kPlayModeDarken",
                BindingFlags.NonPublic | BindingFlags.Static);
            return field?.GetValue(null);
        }

        private static PropertyInfo GetPrefColorProperty(object prefColor)
        {
            return prefColor?.GetType().GetProperty(
                "Color",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }
}
