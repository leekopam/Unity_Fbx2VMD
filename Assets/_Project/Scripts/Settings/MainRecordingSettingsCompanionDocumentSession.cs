using System;

namespace Fbx2Vmd.Settings
{
    internal sealed class MainRecordingSettingsCompanionDocumentSession
    {
        private MainRecordingSettingsDocument document;

        internal MainRecordingSettingsDocument CurrentDocument
        {
            get
            {
                return document ?? (document = new MainRecordingSettingsDocument());
            }
        }

        internal MainRecordingSettingsDocument Load(string settingsFilePathOverride)
        {
            document = CreateStore(settingsFilePathOverride).LoadOrCreateDefault();
            return document;
        }

        internal void Save(
            string settingsFilePathOverride,
            Action<MainRecordingSettingsDocument> updateDocument)
        {
            MainRecordingSettingsStore store = CreateStore(settingsFilePathOverride);
            MainRecordingSettingsDocument current = CurrentDocument;
            updateDocument?.Invoke(current);
            store.Save(current);
        }

        internal bool TrySaveImportFbxCommand(
            string settingsFilePathOverride,
            Action<MainRecordingSettingsDocument> updateDocument)
        {
            MainRecordingSettingsStore store = CreateStore(settingsFilePathOverride);
            MainRecordingSettingsDocument current = CurrentDocument;
            updateDocument?.Invoke(current);

            string fbxPath = string.IsNullOrWhiteSpace(current.fbxPath)
                ? string.Empty
                : current.fbxPath.Trim();
            if (string.IsNullOrEmpty(fbxPath))
            {
                return false;
            }

            current.pendingCommand = new MainRecordingSettingsCommandEnvelope
            {
                commandId = Guid.NewGuid().ToString("N"),
                action = MainRecordingSettingsCommandEnvelope.ImportFbxAction,
                fbxPath = fbxPath,
                requestedAtUtc = DateTime.UtcNow.ToString("O"),
            };

            store.Save(current);
            return true;
        }

        internal void ReplaceDocument(MainRecordingSettingsDocument value)
        {
            document = value ?? new MainRecordingSettingsDocument();
        }

        private static MainRecordingSettingsStore CreateStore(string settingsFilePathOverride)
        {
            return new MainRecordingSettingsStore(settingsFilePathOverride);
        }
    }
}
