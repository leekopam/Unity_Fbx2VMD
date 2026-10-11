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
            // 명령·결과·런타임 상태는 동시 기록자(폴러/런타임) 소유이므로
            // 캐시 문서의 구형 값이 아니라 저장 직전 파일 값을 유지한다.
            PreserveCommandChannel(store, current);
            updateDocument?.Invoke(current);
            store.Save(current);
        }

        internal bool TrySaveImportFbxCommand(
            string settingsFilePathOverride,
            Action<MainRecordingSettingsDocument> updateDocument)
        {
            MainRecordingSettingsStore store = CreateStore(settingsFilePathOverride);
            MainRecordingSettingsDocument current = CurrentDocument;
            PreserveCommandChannel(store, current);
            // 소비 전 명령이 이미 대기 중이면 덮어쓰지 않고 거절한다.
            if (current.pendingCommand != null && current.pendingCommand.IsPending())
            {
                return false;
            }

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

        private static void PreserveCommandChannel(
            MainRecordingSettingsStore store, MainRecordingSettingsDocument current)
        {
            MainRecordingSettingsDocument fresh = store.LoadOrCreateDefault();
            current.pendingCommand = fresh.pendingCommand;
            current.lastCommandResult = fresh.lastCommandResult;
            current.runtimeState = fresh.runtimeState;
        }

        private static MainRecordingSettingsStore CreateStore(string settingsFilePathOverride)
        {
            return new MainRecordingSettingsStore(settingsFilePathOverride);
        }
    }
}
