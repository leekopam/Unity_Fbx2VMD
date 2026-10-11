namespace Fbx2Vmd.Settings
{
    internal static class MainRecordingSettingsDocumentNormalizer
    {
        internal static MainRecordingSettingsDocument Normalize(
            MainRecordingSettingsDocument document)
        {
            if (document == null)
            {
                return new MainRecordingSettingsDocument();
            }

            if (document.schemaVersion <= 0)
            {
                document.schemaVersion = 1;
            }

            if (document.captureWidth <= 0)
            {
                document.captureWidth = 1920;
            }

            if (document.captureHeight <= 0)
            {
                document.captureHeight = 1080;
            }

            if (document.updatedAtUtc == null)
            {
                document.updatedAtUtc = string.Empty;
            }

            if (document.fbxPath == null)
            {
                document.fbxPath = string.Empty;
            }

            if (document.characterModelPath == null)
            {
                document.characterModelPath = string.Empty;
            }

            if (document.runtimeState == null)
            {
                document.runtimeState = new MainRecordingSettingsState();
            }

            document.runtimeState.Normalize();

            if (document.pendingCommand == null)
            {
                document.pendingCommand = new MainRecordingSettingsCommandEnvelope();
            }

            if (document.pendingCommand.commandId == null)
            {
                document.pendingCommand.commandId = string.Empty;
            }

            if (document.pendingCommand.action == null)
            {
                document.pendingCommand.action = string.Empty;
            }

            if (document.pendingCommand.fbxPath == null)
            {
                document.pendingCommand.fbxPath = string.Empty;
            }

            if (document.pendingCommand.characterId == null)
            {
                document.pendingCommand.characterId = string.Empty;
            }

            if (document.pendingCommand.presetId == null)
            {
                document.pendingCommand.presetId = string.Empty;
            }

            if (document.pendingCommand.path == null)
            {
                document.pendingCommand.path = string.Empty;
            }

            if (document.pendingCommand.paramsJson == null)
            {
                document.pendingCommand.paramsJson = string.Empty;
            }

            if (document.pendingCommand.requestedAtUtc == null)
            {
                document.pendingCommand.requestedAtUtc = string.Empty;
            }

            if (document.lastCommandResult == null)
            {
                document.lastCommandResult = new MainRecordingSettingsCommandResult();
            }

            if (document.lastCommandResult.commandId == null)
            {
                document.lastCommandResult.commandId = string.Empty;
            }

            if (document.lastCommandResult.action == null)
            {
                document.lastCommandResult.action = string.Empty;
            }

            if (document.lastCommandResult.state == null)
            {
                document.lastCommandResult.state = string.Empty;
            }

            if (document.lastCommandResult.message == null)
            {
                document.lastCommandResult.message = string.Empty;
            }

            if (document.lastCommandResult.finishedAtUtc == null)
            {
                document.lastCommandResult.finishedAtUtc = string.Empty;
            }

            return document;
        }
    }
}
