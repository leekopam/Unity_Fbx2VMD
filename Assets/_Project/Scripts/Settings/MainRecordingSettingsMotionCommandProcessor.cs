using System;

namespace Fbx2Vmd.Settings
{
    internal sealed class MainRecordingSettingsMotionCommandProcessor
    {
        private MainRecordingSettingsCommandResult lastResult;

        internal bool TryProcess(
            MainRecordingSettingsDocument document,
            bool shouldExecute,
            bool shouldClearSkippedCommand,
            bool isRecording,
            Func<string, bool> tryExecute,
            Action<MainRecordingSettingsDocument> persist,
            out MainRecordingSettingsActionResult result)
        {
            result = default;
            MainRecordingSettingsCommandEnvelope command = document?.pendingCommand;
            if (command == null || !command.IsPending() || !IsMotionAction(command.action))
            {
                return false;
            }

            if (!shouldExecute && !shouldClearSkippedCommand)
            {
                result = MainRecordingSettingsActionResult.Success("모션 명령 실행을 보류했습니다.");
                return true;
            }

            if (lastResult == null || lastResult.commandId != command.commandId)
            {
                lastResult = Execute(command, shouldExecute, isRecording, tryExecute);
            }

            document.lastCommandResult = lastResult;
            document.pendingCommand = new MainRecordingSettingsCommandEnvelope();
            result = lastResult.state == MainRecordingSettingsCommandResult.StateSuccess
                ? MainRecordingSettingsActionResult.Success(lastResult.message)
                : MainRecordingSettingsActionResult.Failure(lastResult.message);
            persist?.Invoke(document);
            return true;
        }

        private static bool IsMotionAction(string action)
        {
            return action == MainRecordingSettingsCommandEnvelope.PlayImportedMotionAction ||
                   action == MainRecordingSettingsCommandEnvelope.PauseImportedMotionAction ||
                   action == MainRecordingSettingsCommandEnvelope.StopImportedMotionAction ||
                   action == MainRecordingSettingsCommandEnvelope.EnableNativeSkinningCorrectionAction ||
                   action == MainRecordingSettingsCommandEnvelope.DisableNativeSkinningCorrectionAction;
        }

        private static MainRecordingSettingsCommandResult Execute(
            MainRecordingSettingsCommandEnvelope command,
            bool shouldExecute,
            bool isRecording,
            Func<string, bool> tryExecute)
        {
            string failure;
            if (!shouldExecute)
            {
                failure = "초기 설정의 이전 모션 명령을 취소했습니다. 다시 요청해 주세요.";
            }
            else if (isRecording)
            {
                failure = "녹화 중에는 모션 재생 상태를 변경할 수 없습니다.";
            }
            else if (tryExecute == null)
            {
                failure = "모션 명령을 적용할 FBXVmdPipeline을 찾지 못했습니다.";
            }
            else
            {
                try
                {
                    if (tryExecute(command.action))
                    {
                        // 재생 요청은 보정 준비를 먼저 시작할 수 있으므로 재생 완료로 표시하지 않음.
                        return MainRecordingSettingsCommandResult.Success(
                            command.commandId, command.action, "모션 명령을 수락했습니다.");
                    }
                    failure = "현재 모션 상태에서 요청을 실행할 수 없습니다.";
                }
                catch (Exception exception)
                {
                    failure = $"모션 명령 처리에 실패했습니다: {exception.Message}";
                }
            }

            return MainRecordingSettingsCommandResult.Failure(command.commandId, command.action, failure);
        }
    }
}
