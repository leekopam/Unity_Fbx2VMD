using System;
using System.IO;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Settings.EditorTools
{
    /// <summary>
    /// 공유 설정 문서의 pendingCommand를 에디트 모드에서도 폴링·소비한다.
    /// Play 전용 런타임 소비자(RecordingSetting.Update)와 달리 캐릭터 라이브러리
    /// 명령군만 처리하며, ImportFbx는 런타임 경로에 남겨둔다.
    /// </summary>
    [InitializeOnLoad]
    internal static class MainRecordingSettingsEditorCommandPoller
    {
        private const double PollIntervalSeconds = 1.0;

        private static DateTime lastWriteTimeUtc = DateTime.MinValue;
        private static double nextPollAt;
        private static string lastHandledCommandId = string.Empty;
        private static bool dispatching;
        // 비동기 실행이 끝날 때까지 유지되는 in-flight 플래그.
        // 실행 중에 큐된 명령은 소비하지 않고 완료 후 순차 처리한다.
        private static bool running;

        static MainRecordingSettingsEditorCommandPoller()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (Application.isBatchMode || EditorApplication.isCompiling || dispatching || running)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < nextPollAt)
            {
                return;
            }

            nextPollAt = EditorApplication.timeSinceStartup + PollIntervalSeconds;

            string path = MainRecordingSettingsPathResolver.ResolveSettingsFilePath();
            if (!File.Exists(path))
            {
                return;
            }

            DateTime writeTimeUtc = File.GetLastWriteTimeUtc(path);
            if (writeTimeUtc <= lastWriteTimeUtc)
            {
                return;
            }

            MainRecordingSettingsDocument document;
            try
            {
                document = new MainRecordingSettingsStore().LoadOrCreateDefault();
            }
            catch (Exception exception)
            {
                // 읽기 실패 시 mtime을 소비하지 않아 다음 폴에서 재시도한다.
                Debug.LogWarning(
                    $"[MainRecordingSettingsEditorCommandPoller] 설정 문서 읽기 실패: {exception.Message}");
                return;
            }

            lastWriteTimeUtc = writeTimeUtc;

            MainRecordingSettingsCommandEnvelope command = document?.pendingCommand;
            if (command == null || !command.IsPending() ||
                !MainRecordingSettingsCharacterCommandProcessor.IsCharacterAction(command.action))
            {
                return;
            }

            var store = new MainRecordingSettingsStore();
            if (string.Equals(lastHandledCommandId, command.commandId, StringComparison.Ordinal))
            {
                document.pendingCommand = new MainRecordingSettingsCommandEnvelope();
                SaveQuietly(store, document);
                return;
            }

            lastHandledCommandId = command.commandId;
            dispatching = true;
            running = true;
            try
            {
                document.pendingCommand = new MainRecordingSettingsCommandEnvelope();
                document.lastCommandResult = MainRecordingSettingsCommandResult.Running(
                    command.commandId, command.action, "명령을 처리 중입니다.");
                SaveQuietly(store, document);
                _ = ExecuteAndReportAsync(store, command);
            }
            finally
            {
                dispatching = false;
            }
        }

        private static async System.Threading.Tasks.Task ExecuteAndReportAsync(
            MainRecordingSettingsStore store,
            MainRecordingSettingsCommandEnvelope command)
        {
            try
            {
                await ExecuteAndWriteResultAsync(store, command);
            }
            finally
            {
                running = false;
            }
        }

        private static async System.Threading.Tasks.Task ExecuteAndWriteResultAsync(
            MainRecordingSettingsStore store,
            MainRecordingSettingsCommandEnvelope command)
        {
            MainRecordingSettingsCommandResult result;
            try
            {
                string message = await MainRecordingSettingsCharacterCommandProcessor
                    .ExecuteAsync(command)
                    .ConfigureAwait(true);
                result = MainRecordingSettingsCommandResult.Success(
                    command.commandId, command.action, message);
            }
            catch (Exception exception)
            {
                result = MainRecordingSettingsCommandResult.Failure(
                    command.commandId, command.action, exception.Message);
            }

            try
            {
                MainRecordingSettingsDocument document = store.LoadOrCreateDefault();
                document.lastCommandResult = result;
                store.Save(document);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MainRecordingSettingsEditorCommandPoller] 명령 결과 기록 실패: {exception.Message}");
            }
        }

        private static void SaveQuietly(
            MainRecordingSettingsStore store,
            MainRecordingSettingsDocument document)
        {
            try
            {
                store.Save(document);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MainRecordingSettingsEditorCommandPoller] 설정 문서 저장 실패: {exception.Message}");
            }
        }
    }
}
