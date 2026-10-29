using System;
using System.IO;
using UnityEngine;

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

        internal void WriteMotionState(MainRecordingSettingsState state)
        {
            state.updatedAtUtc = DateTime.UtcNow.ToString("O");
            // 명령 문서와 작성자를 분리하여 주기 상태 게시가 새 명령을 덮지 않게 함.
            string path = store.SettingsFilePath + ".runtime-state.json";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(state));
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }

        internal void Save(MainRecordingSettingsDocument document)
        {
            try
            {
                store.Save(document);
                lastWriteTimeUtc = store.ResolveLastWriteTimeUtc();
            }
            catch
            {
                // 디스크에 남은 명령을 다시 읽어 캐시된 처리 결과 저장을 재시도함.
                lastWriteTimeUtc = DateTime.MinValue;
                throw;
            }
        }
    }
}
