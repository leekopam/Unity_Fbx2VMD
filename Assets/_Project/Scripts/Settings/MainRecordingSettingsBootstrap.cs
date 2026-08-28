using System;
using UnityEngine;

namespace Fbx2Vmd.Settings
{
    public static class MainRecordingSettingsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoLaunchForPlayerStartup()
        {
            Application.quitting -= MainRecordingSettingsLauncher.CloseStartedProcessQuietly;
            Application.quitting += MainRecordingSettingsLauncher.CloseStartedProcessQuietly;

            string settingsPath = MainRecordingSettingsPathResolver.ResolveSettingsFilePath();
            bool openSettingsOnStart = ResolveOpenSettingsOnStart(settingsPath);
            if (!MainRecordingSettingsLauncher.ShouldAutoLaunchForPlayer(
                    openSettingsOnStart,
                    Application.isEditor,
                    Application.isBatchMode))
            {
                return;
            }

            MainRecordingSettingsActionResult result =
                MainRecordingSettingsLauncher.TryLaunchForPlayer(true, settingsPath);
            if (!result.Succeeded)
            {
                Debug.LogWarning($"[MainRecordingSettingsBootstrap] {result.UserMessage}");
            }
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
                    "[MainRecordingSettingsBootstrap] 공유 설정을 읽지 못해 기본 자동 실행 정책을 사용합니다. " +
                    exception.Message);
                return true;
            }
        }

    }
}
