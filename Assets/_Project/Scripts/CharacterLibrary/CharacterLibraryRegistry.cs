using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 캐릭터 라이브러리의 조회·등록·갱신을 담당하는 서비스.
    /// 스토어(영속화), 해시(식별), 메타 리더(표시 정보)를 묶는다.
    /// metaReader는 테스트에서 주입할 수 있는 IO 경계다.
    /// </summary>
    public sealed class CharacterLibraryRegistry
    {
        private readonly CharacterLibraryStore _store;
        private readonly Func<string, VrmCharacterMetaResult> _metaReader;
        // 엔트리별 프리셋 스토어를 재사용해 LastReadFailed 보호가 읽기-쓰기에 걸쳐 유지되게 한다.
        private readonly Dictionary<string, CharacterPresetStore> _presetStores =
            new Dictionary<string, CharacterPresetStore>(StringComparer.Ordinal);
        private CharacterLibraryDocument _document;
        private DateTime _knownWriteTimeUtc = DateTime.MinValue;

        public CharacterLibraryRegistry(
            string libraryFilePath = null,
            Func<string, VrmCharacterMetaResult> metaReader = null)
        {
            _store = new CharacterLibraryStore(libraryFilePath);
            _metaReader = metaReader ?? VrmCharacterMetaReader.Read;
        }

        public string LibraryFilePath => _store.LibraryFilePath;

        public IReadOnlyList<CharacterLibraryEntry> Entries => Document.entries;

        private CharacterLibraryDocument Document
        {
            get
            {
                if (_document == null)
                {
                    _document = _store.LoadOrCreateDefault();
                    _knownWriteTimeUtc = _store.ResolveLastWriteTimeUtc();
                }
                return _document;
            }
        }

        /// <summary>
        /// 다른 인스턴스(창/매니저 등)가 같은 파일을 저장했을 때 캐시를 버리고 다시 읽는다.
        /// 쓰기 전에 호출해 read-modify-write 덮어쓰기를 막는다.
        /// </summary>
        private void ReloadIfExternallyChanged()
        {
            if (_document == null)
            {
                return;
            }

            DateTime current = _store.ResolveLastWriteTimeUtc();
            if (current != _knownWriteTimeUtc)
            {
                _document = _store.LoadOrCreateDefault();
                _knownWriteTimeUtc = current;
            }
        }

        public string ActiveCharacterId => Document.activeCharacterId;

        public CharacterLibraryEntry FindById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int i = 0; i < Document.entries.Count; i++)
            {
                CharacterLibraryEntry entry = Document.entries[i];
                if (string.Equals(entry.id, id, StringComparison.Ordinal))
                {
                    return entry;
                }
            }
            return null;
        }

        public CharacterLibraryEntry FindBySourcePath(string normalizedPath)
        {
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return null;
            }

            for (int i = 0; i < Document.entries.Count; i++)
            {
                CharacterLibraryEntry entry = Document.entries[i];
                if (string.Equals(entry.sourcePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }

        /// <summary>
        /// VRM 파일을 라이브러리에 등록한다.
        /// 성공 시 엔트리, 실패 시 entry=null과 한국어 오류 메시지를 돌려준다.
        /// </summary>
        public RegisterResult Register(string sourcePath)
        {
            var result = new RegisterResult();
            ReloadIfExternallyChanged();

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath((sourcePath ?? string.Empty).Trim());
            }
            catch (Exception error)
            {
                result.error = $"경로가 유효하지 않습니다: {error.Message}";
                return result;
            }

            if (!File.Exists(normalizedPath))
            {
                result.error = $"파일이 없습니다: {normalizedPath}";
                return result;
            }

            if (!string.Equals(Path.GetExtension(normalizedPath), ".vrm", StringComparison.OrdinalIgnoreCase))
            {
                result.error = "VRM 파일(.vrm)만 등록할 수 있습니다.";
                return result;
            }

            CharacterLibraryEntry existing = FindBySourcePath(normalizedPath);
            if (existing != null)
            {
                result.error = "이미 등록된 캐릭터입니다.";
                result.entry = existing;
                return result;
            }

            if (!CharacterFileHasher.TryCaptureFileStat(
                    normalizedPath, out long fileSize, out string lastWriteTimeUtc, out string contentHash))
            {
                result.error = "파일 정보를 읽을 수 없습니다.";
                return result;
            }

            // 다른 경로에 같은 내용의 파일이 이미 등록돼 있으면 중복으로 간주한다.
            for (int i = 0; i < Document.entries.Count; i++)
            {
                if (string.Equals(Document.entries[i].contentHash, contentHash, StringComparison.Ordinal))
                {
                    result.error = "동일한 내용의 파일이 이미 등록되어 있습니다.";
                    result.entry = Document.entries[i];
                    return result;
                }
            }

            VrmCharacterMetaResult meta = _metaReader(normalizedPath);
            if (meta == null || !meta.success)
            {
                result.error = meta != null && !string.IsNullOrEmpty(meta.error)
                    ? meta.error
                    : "VRM 메타데이터를 읽을 수 없습니다.";
                return result;
            }

            var entry = new CharacterLibraryEntry
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = ResolveDisplayName(meta.meta, normalizedPath),
                sourcePath = normalizedPath,
                fileSize = fileSize,
                lastWriteTimeUtc = lastWriteTimeUtc,
                contentHash = contentHash,
                vrmSpecVersion = meta.specVersion ?? string.Empty,
                meta = meta.meta ?? new CharacterLibraryEntryMeta(),
                registeredAtUtc = DateTime.UtcNow.ToString("O"),
                status = CharacterLibraryEntryStatus.Ready,
            };

            entry.thumbnailFileName = TryWriteThumbnail(entry.id, meta.thumbnailPng);

            Document.entries.Add(entry);
            try
            {
                Save();
            }
            catch (Exception error)
            {
                // 저장 실패 시 남은 썸네일이 orphan이 되지 않게 정리하고,
                // 메모리 문서도 디스크 상태에 맞춰 롤백한다.
                DeleteThumbnailFile(entry.thumbnailFileName);
                Document.entries.Remove(entry);
                result.error = $"라이브러리를 저장할 수 없습니다: {error.Message}";
                return result;
            }
            result.entry = entry;
            return result;
        }

        /// <summary>
        /// 엔트리의 파일 존재·내용 변경 상태를 다시 확인해 status를 갱신한다.
        /// </summary>
        public void RefreshEntry(CharacterLibraryEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            ReloadIfExternallyChanged();
            entry = FindById(entry.id) ?? entry;

            if (!File.Exists(entry.sourcePath))
            {
                entry.status = CharacterLibraryEntryStatus.Missing;
                return;
            }

            if (!CharacterFileHasher.TryCaptureFileStat(
                    entry.sourcePath, out long fileSize, out string lastWriteTimeUtc, out string contentHash))
            {
                entry.status = CharacterLibraryEntryStatus.Error;
                entry.lastError = "파일 정보를 읽을 수 없습니다.";
                return;
            }

            // 내용 해시가 바뀐 경우만 Changed로 표시한다(mtime만 바뀐 터치는 무시).
            bool contentChanged = !string.Equals(entry.contentHash, contentHash, StringComparison.Ordinal);

            entry.fileSize = fileSize;
            entry.lastWriteTimeUtc = lastWriteTimeUtc;

            if (contentChanged)
            {
                entry.contentHash = contentHash;
                entry.status = CharacterLibraryEntryStatus.Changed;
            }
            else if (entry.status == CharacterLibraryEntryStatus.Missing ||
                     entry.status == CharacterLibraryEntryStatus.Error)
            {
                // 파일이 정상으로 읽히면 Missing/Error를 해소한다.
                entry.status = CharacterLibraryEntryStatus.Ready;
                entry.lastError = string.Empty;
            }
        }

        /// <summary>
        /// 파일이 이동/삭제된 엔트리를 새 경로에 연결한다. 내용 해시가 같을 때만 허용한다.
        /// </summary>
        public bool TryRelink(CharacterLibraryEntry entry, string newPath, out string error)
        {
            error = string.Empty;
            if (entry == null)
            {
                error = "엔트리가 없습니다.";
                return false;
            }

            ReloadIfExternallyChanged();
            entry = FindById(entry.id) ?? entry;

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath((newPath ?? string.Empty).Trim());
            }
            catch (Exception exception)
            {
                error = $"경로가 유효하지 않습니다: {exception.Message}";
                return false;
            }

            if (!File.Exists(normalizedPath))
            {
                error = $"파일이 없습니다: {normalizedPath}";
                return false;
            }

            if (!CharacterFileHasher.TryCaptureFileStat(
                    normalizedPath, out long fileSize, out string lastWriteTimeUtc, out string contentHash))
            {
                error = "파일 정보를 읽을 수 없습니다.";
                return false;
            }

            if (!string.Equals(entry.contentHash, contentHash, StringComparison.Ordinal))
            {
                error = "파일 내용이 등록된 캐릭터와 다릅니다.";
                return false;
            }

            entry.sourcePath = normalizedPath;
            entry.fileSize = fileSize;
            entry.lastWriteTimeUtc = lastWriteTimeUtc;
            entry.status = CharacterLibraryEntryStatus.Ready;
            entry.lastError = string.Empty;
            Save();
            return true;
        }

        public bool Remove(string id)
        {
            ReloadIfExternallyChanged();
            CharacterLibraryEntry entry = FindById(id);
            if (entry == null)
            {
                return false;
            }

            Document.entries.Remove(entry);
            if (string.Equals(Document.activeCharacterId, entry.id, StringComparison.Ordinal))
            {
                Document.activeCharacterId = string.Empty;
            }
            Save();
            DeleteThumbnailFile(entry.thumbnailFileName);
            // 캐릭터 귀속 프리셋 파일도 함께 정리한다(실패해도 제거는 유지).
            _presetStores.Remove(entry.id);
            string presetFile = ResolvePresetFilePath(entry.id);
            if (!string.IsNullOrEmpty(presetFile) && File.Exists(presetFile))
            {
                try
                {
                    File.Delete(presetFile);
                }
                catch (Exception error)
                {
                    // 프리셋 파일 삭제 실패는 엔트리 제거를 막지 않지만 흔적은 남긴다.
                    Debug.LogWarning(
                        $"[CharacterLibrary] 프리셋 파일을 삭제하지 못했습니다: " +
                        $"{presetFile} ({error.Message})");
                }
            }
            return true;
        }

        /// <summary>
        /// 마지막 사용 시각과 활성 캐릭터 표시를 갱신한다.
        /// </summary>
        public void MarkUsed(string id)
        {
            ReloadIfExternallyChanged();
            CharacterLibraryEntry entry = FindById(id);
            if (entry == null)
            {
                return;
            }

            entry.lastUsedAtUtc = DateTime.UtcNow.ToString("O");
            Document.activeCharacterId = entry.id;
            Save();
        }

        /// <summary>
        /// 활성 캐릭터 기록을 지운다. 씬 캐릭터로 복원할 때 호출해
        /// 다음 시작 시 자동 로드가 다시 실행되지 않게 한다.
        /// </summary>
        public void ClearActiveCharacter()
        {
            ReloadIfExternallyChanged();
            if (string.IsNullOrEmpty(Document.activeCharacterId))
            {
                return;
            }
            Document.activeCharacterId = string.Empty;
            Save();
        }

        /// <summary>
        /// 마지막 읽기가 IO 오류로 실패해 문서가 기본값인지.
        /// 이 상태에서 Save하면 정상 파일이 소실되므로 호출자가 먼저 확인할 수 있다.
        /// </summary>
        public bool LastReadFailed => _store.LastReadFailed;

        public void Save()
        {
            if (_store.LastReadFailed)
            {
                throw new InvalidOperationException(
                    "라이브러리 파일을 읽지 못한 상태입니다. " +
                    "기존 데이터 보호를 위해 저장을 차단했습니다.");
            }
            _store.Save(Document);
            _knownWriteTimeUtc = _store.ResolveLastWriteTimeUtc();
        }

        /// <summary>
        /// 엔트리의 프리셋 JSON 절대 경로. 없으면 빈 문자열.
        /// </summary>
        public string ResolvePresetFilePath(string entryId)
        {
            return CharacterLibraryPathResolver.ResolvePresetFilePath(LibraryFilePath, entryId);
        }

        /// <summary>
        /// 엔트리의 프리셋 스토어를 돌려준다. 경로가 비어 있으면 null.
        /// 같은 엔트리에는 같은 인스턴스를 재사용해 읽기 실패 시 저장 차단이 유지된다.
        /// </summary>
        public CharacterPresetStore OpenPresetStore(string entryId)
        {
            string path = ResolvePresetFilePath(entryId);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            if (!_presetStores.TryGetValue(entryId, out CharacterPresetStore store))
            {
                store = new CharacterPresetStore(path);
                _presetStores[entryId] = store;
            }
            return store;
        }

        /// <summary>
        /// 엔트리의 프리셋 문서를 읽는다. 파일이 없거나 읽기 실패면 빈 문서를 돌려준다.
        /// </summary>
        public CharacterPresetDocument LoadPresets(string entryId)
        {
            CharacterPresetStore store = OpenPresetStore(entryId);
            CharacterPresetDocument document = store != null
                ? store.LoadOrCreateDefault()
                : new CharacterPresetDocument();
            document.characterId = entryId ?? string.Empty;
            return document;
        }

        /// <summary>
        /// id로 프리셋 하나를 찾는다. 없으면 null.
        /// </summary>
        public CharacterPreset FindPreset(string entryId, string presetId)
        {
            if (string.IsNullOrEmpty(entryId) || string.IsNullOrEmpty(presetId))
            {
                return null;
            }

            CharacterPresetDocument document = LoadPresets(entryId);
            for (int i = 0; i < document.presets.Count; i++)
            {
                if (string.Equals(document.presets[i].id, presetId, StringComparison.Ordinal))
                {
                    return document.presets[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 엔트리의 기본 프리셋을 지정한다. 빈 presetId면 해제한다.
        /// </summary>
        public bool SetDefaultPreset(string entryId, string presetId)
        {
            ReloadIfExternallyChanged();
            CharacterLibraryEntry entry = FindById(entryId);
            if (entry == null)
            {
                return false;
            }

            entry.defaultPresetId = presetId ?? string.Empty;
            Save();
            return true;
        }

        /// <summary>
        /// 프리셋 파일과 엔트리의 presetIds를 동기화한다.
        /// 목록에 없는 defaultPresetId는 해제한다.
        /// </summary>
        public void SyncPresetIds(string entryId, IEnumerable<string> presetIds)
        {
            ReloadIfExternallyChanged();
            CharacterLibraryEntry entry = FindById(entryId);
            if (entry == null)
            {
                return;
            }

            entry.presetIds = presetIds != null
                ? new List<string>(presetIds)
                : new List<string>();
            if (!string.IsNullOrEmpty(entry.defaultPresetId) &&
                !entry.presetIds.Contains(entry.defaultPresetId))
            {
                entry.defaultPresetId = string.Empty;
            }
            Save();
        }

        /// <summary>
        /// 엔트리의 썸네일 PNG 절대 경로. 없으면 빈 문자열.
        /// </summary>
        public string ResolveThumbnailPath(CharacterLibraryEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }
            return CharacterLibraryPathResolver.ResolveThumbnailPath(
                LibraryFilePath, entry.thumbnailFileName);
        }

        private string TryWriteThumbnail(string entryId, byte[] png)
        {
            if (png == null || png.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                string directory = CharacterLibraryPathResolver.ResolveThumbnailsDirectory(LibraryFilePath);
                Directory.CreateDirectory(directory);
                string fileName = entryId + ".png";
                File.WriteAllBytes(Path.Combine(directory, fileName), png);
                return fileName;
            }
            catch (Exception)
            {
                // 썸네일 저장 실패는 등록 자체를 막지 않는다.
                return string.Empty;
            }
        }

        private void DeleteThumbnailFile(string thumbnailFileName)
        {
            string path = CharacterLibraryPathResolver.ResolveThumbnailPath(
                LibraryFilePath, thumbnailFileName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // 썸네일 삭제 실패는 레지스트리 동작을 막지 않는다.
            }
        }

        private static string ResolveDisplayName(CharacterLibraryEntryMeta meta, string path)
        {
            if (meta != null && !string.IsNullOrWhiteSpace(meta.title))
            {
                return meta.title.Trim();
            }
            return Path.GetFileNameWithoutExtension(path);
        }

        public sealed class RegisterResult
        {
            public CharacterLibraryEntry entry;
            public string error = string.Empty;
            public bool success => entry != null && string.IsNullOrEmpty(error);
        }
    }
}
