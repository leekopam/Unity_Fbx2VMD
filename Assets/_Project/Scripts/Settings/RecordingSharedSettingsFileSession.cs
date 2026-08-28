using System;

namespace Fbx2Vmd.Settings
{
    internal sealed class RecordingSharedSettingsFileSession
    {
        private readonly MainRecordingSettingsStore store;
        private DateTime lastWriteTimeUtc;

        internal RecordingSharedSettingsFileSession(string settingsFilePathOverride)
        {
            store = new MainRecordingSettingsStore(settingsFilePathOverride);
            lastWriteTimeUtc = store.ResolveLastWriteTimeUtc();
        }

        internal string SettingsFilePath => store.SettingsFilePath;

        internal MainRecordingSettingsDocument LoadCurrent()
        {
            MainRecordingSettingsDocument document = store.LoadOrCreateDefault();
            lastWriteTimeUtc = store.ResolveLastWriteTimeUtc();
            return document;
        }

        internal bool TryLoadChanged(out MainRecordingSettingsDocument document)
        {
            DateTime currentWriteTimeUtc = store.ResolveLastWriteTimeUtc();
            if (currentWriteTimeUtc <= lastWriteTimeUtc)
            {
                document = null;
                return false;
            }

            document = store.LoadOrCreateDefault();
            lastWriteTimeUtc = currentWriteTimeUtc;
            return true;
        }

        internal void WriteRuntimePlayModeState(string playMode)
        {
            MainRecordingSettingsDocument document = store.LoadOrCreateDefault();
            document.runtimeState = MainRecordingSettingsState.Create(playMode, DateTime.UtcNow);
            Save(document);
        }

        internal void Save(MainRecordingSettingsDocument document)
        {
            store.Save(document);
            lastWriteTimeUtc = store.ResolveLastWriteTimeUtc();
        }
    }
}
