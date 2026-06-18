using System;
using UnityEngine;

namespace Member_Han.Modules.Graphics
{
    public static class MainRecordingSettingsRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoLaunchForPlayerStartup()
        {
            Application.quitting -= MainRecordingSettingsRuntimeLauncher.CloseStartedProcessQuietly;
            Application.quitting += MainRecordingSettingsRuntimeLauncher.CloseStartedProcessQuietly;

            string settingsPath = MainRecordingSettingsPathResolver.ResolveSettingsFilePath();
            bool openSettingsOnStart = ResolveOpenSettingsOnStart(settingsPath);
            if (!ShouldAutoLaunchOnPlayerStartup(
                    openSettingsOnStart,
                    Application.isEditor,
                    Application.isBatchMode))
            {
                return;
            }

            MainRecordingSettingsActionResult result =
                MainRecordingSettingsRuntimeLauncher.TryLaunchForPlayer(true, settingsPath);
            if (!result.Succeeded)
            {
                Debug.LogWarning($"[MainRecordingSettingsRuntimeBootstrap] {result.UserMessage}");
            }
        }

        private static bool ShouldAutoLaunchOnPlayerStartup(
            bool requestedOpen,
            bool isEditor,
            bool isBatchMode)
        {
            return MainRecordingSettingsRuntimeLauncher.ShouldAutoLaunchForPlayer(
                requestedOpen,
                isEditor,
                isBatchMode);
        }

        private static bool ResolveOpenSettingsOnStart(string settingsPath)
        {
            try
            {
                return new MainRecordingSettingsStore(settingsPath)
                    .LoadOrCreateDefault()
                    .openSettingsOnStart;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MainRecordingSettingsRuntimeBootstrap] 공유 설정을 읽지 못해 기본 자동 실행 정책을 사용합니다. " +
                    exception.Message);
                return true;
            }
        }

        private static bool ShouldAutoLaunchOnPlayerStartupForTests(
            bool requestedOpen,
            bool isEditor,
            bool isBatchMode)
        {
            return ShouldAutoLaunchOnPlayerStartup(requestedOpen, isEditor, isBatchMode);
        }
    }
}
