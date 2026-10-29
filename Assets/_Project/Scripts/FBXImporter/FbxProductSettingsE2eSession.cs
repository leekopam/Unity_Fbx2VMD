#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Fbx2Vmd.CharacterLibrary;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public static class FbxProductSettingsE2eSession
    {
        public static event Action EnvironmentChanging;
        private const string JournalKey = "Fbx2Vmd.ProductSettingsE2e.Journal";
        // 기존 companion 자동 실행 계약의 세션 키를 보존하여 시험 창 중복 실행을 막음.
        private const string CompanionLaunchKey = "Fbx2Vmd.MainRecordingSettings.EditorLaunched";

        [Serializable]
        private sealed class Journal
        {
            public string runId;
            public string phase;
            public string settingsPath;
            public string libraryPath;
            public string originalSettingsEnvironment;
            public string originalLibraryEnvironment;
            public string originalSettingsPath;
            public string originalLibraryPath;
            public string settingsHash;
            public string libraryHash;
            public bool reloadOptionsEnabled;
            public int reloadOptions;
            public bool companionLaunched;
        }

        internal static string Prepare(string projectRoot, string runId)
        {
            RequireEditMode();
            string runDirectory = GetRunDirectory(projectRoot, runId);
            string journalPath = GetJournalPath(projectRoot);
            if (File.Exists(journalPath))
            {
                Journal previous = ReadJournal(journalPath, runId);
                if (previous.phase == "prepared" &&
                    Environment.GetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName) == previous.settingsPath &&
                    Environment.GetEnvironmentVariable(CharacterLibraryPathResolver.EnvironmentVariableName) == previous.libraryPath &&
                    !EditorSettings.enterPlayModeOptionsEnabled &&
                    EditorSettings.enterPlayModeOptions == EnterPlayModeOptions.None &&
                    SessionState.GetBool(CompanionLaunchKey, false) &&
                    File.Exists(previous.settingsPath) && File.Exists(previous.libraryPath))
                {
                    string previousManifest = Path.Combine(runDirectory, "session.json");
                    File.WriteAllText(previousManifest, JsonUtility.ToJson(previous, true));
                    return previousManifest;
                }
                throw new InvalidOperationException("미복원 시험 세션을 먼저 복원해야 합니다.");
            }

            if (Directory.Exists(runDirectory))
                throw new InvalidOperationException("이미 사용한 시험 ID입니다. 새 ID를 사용해야 합니다.");
            var journal = new Journal
            {
                runId = Guid.Parse(runId).ToString("D"),
                phase = "preparing",
                settingsPath = Path.Combine(runDirectory, "settings.json"),
                libraryPath = Path.Combine(runDirectory, "library.json"),
                originalSettingsEnvironment = Environment.GetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName),
                originalLibraryEnvironment = Environment.GetEnvironmentVariable(CharacterLibraryPathResolver.EnvironmentVariableName),
                originalSettingsPath = MainRecordingSettingsPathResolver.ResolveSettingsFilePath(),
                originalLibraryPath = CharacterLibraryPathResolver.ResolveLibraryFilePath(),
                reloadOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled,
                reloadOptions = (int)EditorSettings.enterPlayModeOptions,
                companionLaunched = SessionState.GetBool(CompanionLaunchKey, false)
            };
            journal.settingsHash = GetHash(journal.originalSettingsPath);
            journal.libraryHash = GetHash(journal.originalLibraryPath);
            if (File.Exists(journal.originalSettingsPath))
            {
                var document = JsonUtility.FromJson<MainRecordingSettingsDocument>(File.ReadAllText(journal.originalSettingsPath));
                if (document?.pendingCommand != null && !string.IsNullOrWhiteSpace(document.pendingCommand.commandId))
                    throw new InvalidOperationException("기존 설정창 명령이 대기 중이므로 시험을 준비할 수 없습니다.");
            }
            Directory.CreateDirectory(runDirectory);
            // 전역 상태 변경 전에 디스크 기록을 확정하여 도메인 재로드·중단 후에도 복구함.
            SaveJournal(journalPath, journal);
            File.WriteAllText(journal.settingsPath, JsonUtility.ToJson(
                new MainRecordingSettingsDocument { openSettingsOnStart = false }, true));
            File.WriteAllText(journal.libraryPath, JsonUtility.ToJson(new CharacterLibraryDocument(), true));
            EnvironmentChanging?.Invoke();
            SessionState.SetBool(CompanionLaunchKey, true);
            Environment.SetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName, journal.settingsPath);
            Environment.SetEnvironmentVariable(CharacterLibraryPathResolver.EnvironmentVariableName, journal.libraryPath);
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
            journal.phase = "prepared";
            SaveJournal(journalPath, journal);
            string manifestPath = Path.Combine(runDirectory, "session.json");
            File.WriteAllText(manifestPath, JsonUtility.ToJson(journal, true));
            return manifestPath;
        }

        internal static string Restore(string projectRoot, string runId)
        {
            RequireEditMode();
            string manifestPath = Path.Combine(GetRunDirectory(projectRoot, runId), "session.json");
            string journalPath = GetJournalPath(projectRoot);
            if (!File.Exists(journalPath))
            {
                if (File.Exists(manifestPath) && ReadJournal(manifestPath, runId).phase == "restored")
                    return manifestPath;
                throw new InvalidOperationException("복원할 시험 세션 기록이 없습니다.");
            }
            Journal journal = ReadJournal(journalPath, runId);
            var failures = new List<string>();
            EnvironmentChanging?.Invoke();
            RestoreEnvironment(MainRecordingSettingsPathResolver.EnvironmentVariableName,
                journal.settingsPath, journal.originalSettingsEnvironment, failures);
            RestoreEnvironment(CharacterLibraryPathResolver.EnvironmentVariableName,
                journal.libraryPath, journal.originalLibraryEnvironment, failures);
            bool enabled = EditorSettings.enterPlayModeOptionsEnabled;
            int options = (int)EditorSettings.enterPlayModeOptions;
            bool matchesOriginal = enabled == journal.reloadOptionsEnabled && options == journal.reloadOptions;
            bool matchesApplied = !enabled && options == (int)EnterPlayModeOptions.None;
            bool isInterruptedPreparation = journal.phase == "preparing" && !enabled && options == journal.reloadOptions;
            if (!matchesOriginal && !matchesApplied && !isInterruptedPreparation)
                failures.Add("재로드 옵션이 외부에서 변경되어 원복하지 않았습니다.");
            else
            {
                EditorSettings.enterPlayModeOptionsEnabled = journal.reloadOptionsEnabled;
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)journal.reloadOptions;
            }
            if (SessionState.GetBool(CompanionLaunchKey, false) || !journal.companionLaunched)
                SessionState.SetBool(CompanionLaunchKey, journal.companionLaunched);
            else
                failures.Add("자동 설정창 세션 값이 외부에서 변경되어 원복하지 않았습니다.");
            if (GetHash(journal.originalSettingsPath) != journal.settingsHash)
                failures.Add("원래 설정 파일의 내용이 변경되었습니다.");
            if (GetHash(journal.originalLibraryPath) != journal.libraryHash)
                failures.Add("원래 캐릭터 라이브러리의 내용이 변경되었습니다.");
            journal.phase = failures.Count == 0 ? "restored" : "restore_failed";
            SaveJournal(journalPath, journal);
            File.WriteAllText(manifestPath, JsonUtility.ToJson(journal, true));
            if (failures.Count != 0)
                throw new InvalidOperationException(string.Join("\n", failures));
            File.Delete(journalPath);
            SessionState.EraseString(JournalKey);
            return manifestPath;
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("시험 준비·복원은 재로드가 끝난 Edit 모드에서만 가능합니다.");
        }

        private static void RestoreEnvironment(string name, string applied, string original, List<string> failures)
        {
            string current = Environment.GetEnvironmentVariable(name);
            if (current == applied || string.Equals(current ?? string.Empty, original ?? string.Empty, StringComparison.Ordinal))
                Environment.SetEnvironmentVariable(name, string.IsNullOrEmpty(original) ? null : original);
            else
                failures.Add(name + " 값이 외부에서 변경되어 원복하지 않았습니다.");
        }

        private static string GetRunDirectory(string projectRoot, string runId)
        {
            if (!Guid.TryParse(runId, out Guid id) || id == Guid.Empty)
                throw new ArgumentException("유효한 시험 run ID가 필요합니다.", nameof(runId));
            return Path.Combine(projectRoot, "Docs", "Workflow", "Local", "evidence", "product-settings-e2e", id.ToString("D"));
        }

        private static string GetJournalPath(string projectRoot) =>
            Path.Combine(projectRoot, "Docs", "Workflow", "Local", "runtime", "product-settings-e2e-session.json");

        private static Journal ReadJournal(string path, string runId)
        {
            Journal journal = JsonUtility.FromJson<Journal>(File.ReadAllText(path));
            if (journal == null || journal.runId != Guid.Parse(runId).ToString("D"))
                throw new InvalidOperationException("다른 시험 세션의 복구 기록이 존재합니다.");
            return journal;
        }

        private static void SaveJournal(string path, Journal journal)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string json = JsonUtility.ToJson(journal, true);
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, json);
            if (File.Exists(path)) File.Replace(temporaryPath, path, null);
            else File.Move(temporaryPath, path);
            SessionState.SetString(JournalKey, json);
        }

        private static string GetHash(string path)
        {
            if (!File.Exists(path)) return "missing";
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
#endif
