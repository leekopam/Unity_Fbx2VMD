using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Fbx2Vmd.Settings.EditorTools
{
    internal sealed class MainRecordingEditorBurstCompilationSession
    {
        private const string BurstDisableCompilationEnvironmentVariable = "UNITY_BURST_DISABLE_COMPILATION";
        private const string BurstDisableCompilationValue = "1";
        private const string BurstDisableCleanCompileSessionKey =
            "MainRecordingEditorPlayModeGuard.BurstDisableCleanCompileRequested";
        private const string BurstCleanCompileLogMessage =
            "[MainRecordingEditorPlayModeGuard] Main_Recoding Play 준비를 위해 Burst direct-call 컴파일을 비활성화하고 스크립트 clean compile을 요청했습니다.";

        private bool hasSavedBurstCompilation;
        private bool savedBurstCompilation;
        private bool hasSavedBurstDisableEnvironment;
        private string savedBurstDisableEnvironmentValue;

        internal void ApplyForScene(string scenePath, bool isBatchMode)
        {
            SaveStateBeforeApply();
            EnsureBurstDirectCallCompilationDisabledForScene(scenePath, isBatchMode);
            TrySetBurstCompilation(false);
        }

        internal void Restore()
        {
            if (hasSavedBurstCompilation)
            {
                TrySetBurstCompilation(savedBurstCompilation);
                hasSavedBurstCompilation = false;
            }

            if (!hasSavedBurstDisableEnvironment)
            {
                return;
            }

            Environment.SetEnvironmentVariable(
                BurstDisableCompilationEnvironmentVariable,
                savedBurstDisableEnvironmentValue,
                EnvironmentVariableTarget.Process);
            savedBurstDisableEnvironmentValue = null;
            hasSavedBurstDisableEnvironment = false;
        }

        private void SaveStateBeforeApply()
        {
            if (!hasSavedBurstCompilation && TryGetBurstCompilation(out bool burstCompilation))
            {
                savedBurstCompilation = burstCompilation;
                hasSavedBurstCompilation = true;
            }

            if (hasSavedBurstDisableEnvironment)
            {
                return;
            }

            savedBurstDisableEnvironmentValue = Environment.GetEnvironmentVariable(
                BurstDisableCompilationEnvironmentVariable,
                EnvironmentVariableTarget.Process);
            hasSavedBurstDisableEnvironment = true;
        }

        private static void EnsureBurstDirectCallCompilationDisabledForScene(
            string scenePath,
            bool isBatchMode)
        {
            if (isBatchMode || !MainRecordingSettingsCompanionLauncher.ShouldOpenForScene(scenePath))
            {
                return;
            }

            string environmentValue = Environment.GetEnvironmentVariable(BurstDisableCompilationEnvironmentVariable);
            bool shouldRequestCleanCompile = ShouldRequestBurstDisableCleanCompilation(
                environmentValue,
                SessionState.GetBool(BurstDisableCleanCompileSessionKey, false),
                isBatchMode);

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
            Debug.Log(BurstCleanCompileLogMessage);
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
    }
}
