using System;

namespace Fbx2Vmd.Settings
{
    /// <summary>명령 소비 결과 회답 — Electron이 문서를 읽어 성공/실패를 표시한다.</summary>
    [Serializable]
    public sealed class MainRecordingSettingsCommandResult
    {
        public const string StateRunning = "running";
        public const string StateSuccess = "success";
        public const string StateError = "error";

        public string commandId = string.Empty;
        public string action = string.Empty;
        public string state = string.Empty;
        public string message = string.Empty;
        public string finishedAtUtc = string.Empty;

        public static MainRecordingSettingsCommandResult Success(
            string commandId, string action, string message)
        {
            return Create(commandId, action, StateSuccess, message);
        }

        public static MainRecordingSettingsCommandResult Failure(
            string commandId, string action, string message)
        {
            return Create(commandId, action, StateError, message);
        }

        public static MainRecordingSettingsCommandResult Running(
            string commandId, string action, string message)
        {
            return Create(commandId, action, StateRunning, message);
        }

        private static MainRecordingSettingsCommandResult Create(
            string commandId, string action, string state, string message)
        {
            return new MainRecordingSettingsCommandResult
            {
                commandId = commandId ?? string.Empty,
                action = action ?? string.Empty,
                state = state ?? string.Empty,
                message = message ?? string.Empty,
                finishedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }
    }
}
