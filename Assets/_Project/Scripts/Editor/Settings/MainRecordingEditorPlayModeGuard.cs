using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fbx2Vmd.Settings.EditorTools
{
    [InitializeOnLoad]
    internal static class MainRecordingEditorPlayModeGuard
    {
        private const string GuardPolicy =
            "Main_Recoding Editor Play Mode sets UNITY_BURST_DISABLE_COMPILATION=1 and requests one clean script compilation to avoid Burst direct-call initializer failures; Unity Playmode tint is neutralized while Play Mode is active.";
        private static readonly MainRecordingEditorBurstCompilationSession burstCompilationSession =
            new MainRecordingEditorBurstCompilationSession();
        private static readonly MainRecordingEditorPlayModeTintController playModeTintController =
            new MainRecordingEditorPlayModeTintController();

        static MainRecordingEditorPlayModeGuard()
        {
            if (ShouldMaintainEditorPlayModeGuard(
                    SceneManager.GetActiveScene().path,
                    Application.isBatchMode))
            {
                ApplyBeforeMainRecordingPlayMode();
            }

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            MainRecordingSettingsCompanionLauncher.RegisterEditorPlayModeCallback();
            EditorApplication.update -= MaintainEditModeGuard;
            EditorApplication.update += MaintainEditModeGuard;
        }

        private static void MaintainEditModeGuard()
        {
            if (ShouldMaintainEditorPlayModeGuard(
                    SceneManager.GetActiveScene().path,
                    Application.isBatchMode))
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
                return;
            }

            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
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
            bool isBatchMode)
        {
            return !isBatchMode &&
                   MainRecordingSettingsCompanionLauncher.ShouldOpenForScene(scenePath);
        }

        private static void ApplyBeforeMainRecordingPlayMode()
        {
            burstCompilationSession.ApplyForScene(
                SceneManager.GetActiveScene().path,
                Application.isBatchMode);
            playModeTintController.ApplyNeutralTint();
        }

        private static void RestoreEditorPlayModeState()
        {
            burstCompilationSession.Restore();
            playModeTintController.RestoreTint();
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
            bool isBatchMode)
        {
            return ShouldMaintainEditorPlayModeGuard(scenePath, isBatchMode);
        }

        private static string GetEditorPlayModeGuardPolicyForTests()
        {
            return GuardPolicy;
        }

    }
}
