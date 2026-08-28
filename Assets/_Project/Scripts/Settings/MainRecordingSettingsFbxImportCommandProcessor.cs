using System;

namespace Fbx2Vmd.Settings
{
    internal sealed class MainRecordingSettingsFbxImportCommandProcessor
    {
        private string lastHandledCommandId = string.Empty;

        internal bool TryProcess(
            MainRecordingSettingsDocument document,
            bool shouldStartFbxImport,
            bool shouldClearSkippedCommand,
            Func<string, bool> fileExists,
            Func<string, bool> tryStartFbxImport,
            Action<MainRecordingSettingsDocument> persistConsumedDocument,
            out MainRecordingSettingsActionResult result)
        {
            result = default;
            MainRecordingSettingsCommandEnvelope command = document?.pendingCommand;
            if (command == null || !command.IsImportFbxCommand())
            {
                return false;
            }

            if (string.Equals(lastHandledCommandId, command.commandId, StringComparison.Ordinal))
            {
                ConsumeCommand(document, persistConsumedDocument);
                result = MainRecordingSettingsActionResult.Success("공유 설정 명령을 이미 처리했습니다.");
                return true;
            }

            if (!shouldStartFbxImport)
            {
                if (shouldClearSkippedCommand)
                {
                    ConsumeCommand(document, persistConsumedDocument);
                }

                result = MainRecordingSettingsActionResult.Success(
                    "Skipped pending FBX import command during initial settings load.");
                return true;
            }

            string commandFbxPath = string.IsNullOrWhiteSpace(command.fbxPath)
                ? string.Empty
                : command.fbxPath.Trim();
            if (string.IsNullOrEmpty(commandFbxPath))
            {
                ConsumeCommand(document, persistConsumedDocument);
                result = MainRecordingSettingsActionResult.Failure("FBX 명령 경로가 비어 있습니다.");
                return true;
            }

            if (tryStartFbxImport == null)
            {
                ConsumeCommand(document, persistConsumedDocument);
                result = MainRecordingSettingsActionResult.Failure(
                    "FBX 명령을 적용할 FBXVmdPipeline를 찾지 못했습니다.");
                return true;
            }

            if (!fileExists(commandFbxPath))
            {
                ConsumeCommand(document, persistConsumedDocument);
                result = MainRecordingSettingsActionResult.Failure(
                    $"FBX 파일을 찾을 수 없습니다: {commandFbxPath}");
                return true;
            }

            if (!tryStartFbxImport(commandFbxPath))
            {
                ConsumeCommand(document, persistConsumedDocument);
                result = MainRecordingSettingsActionResult.Failure("FBX 가져오기를 시작하지 못했습니다.");
                return true;
            }

            lastHandledCommandId = command.commandId;
            ConsumeCommand(document, persistConsumedDocument);
            result = MainRecordingSettingsActionResult.Success("FBX 가져오기를 시작했습니다.");
            return true;
        }

        private static void ConsumeCommand(
            MainRecordingSettingsDocument document,
            Action<MainRecordingSettingsDocument> persistConsumedDocument)
        {
            document.pendingCommand = new MainRecordingSettingsCommandEnvelope();
            persistConsumedDocument?.Invoke(document);
        }
    }
}
