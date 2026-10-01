using System;
using UnityEngine;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 캐릭터 라이브러리 JSON의 원자적 저장/복원. MainRecordingSettingsStore와 같은 계약:
    /// temp 파일에 쓰고 File.Replace로 교체하며, 손상 파일은 .corrupt- 타임스탬프로 백업한다.
    /// 파일 IO는 AtomicJsonFile에 위임한다.
    /// </summary>
    public sealed class CharacterLibraryStore
    {
        public CharacterLibraryStore(string libraryFilePath = null)
        {
            LibraryFilePath = string.IsNullOrWhiteSpace(libraryFilePath)
                ? CharacterLibraryPathResolver.ResolveLibraryFilePath()
                : libraryFilePath.Trim();
        }

        public string LibraryFilePath { get; }

        /// <summary>
        /// 마지막 읽기가 IO 오류로 실패했는지. true인 동안 Registry는
        /// 저장을 차단해 멀쩡한 파일이 빈 문서로 덮어써지는 것을 막는다.
        /// </summary>
        public bool LastReadFailed { get; private set; }

        public CharacterLibraryDocument LoadOrCreateDefault()
        {
            if (!AtomicJsonFile.TryReadAllText(LibraryFilePath, out string json, out bool ioFailed))
            {
                // 잠금·권한 등 IO 오류는 파일을 건드리지 않고 기본값만 돌려준다.
                // 이 상태에서 저장하면 정상 파일이 소실되므로 읽기 실패를 기록한다.
                LastReadFailed = ioFailed;
                return CharacterLibraryDocumentNormalizer.Normalize(null);
            }

            LastReadFailed = false;
            try
            {
                CharacterLibraryDocument document = string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonUtility.FromJson<CharacterLibraryDocument>(json);
                if (document == null)
                {
                    // 유효하지만 타입이 맞지 않는 JSON("[]" 등)도 손상으로 간주해 백업한다.
                    AtomicJsonFile.TryBackupCorrupt(LibraryFilePath);
                    return CharacterLibraryDocumentNormalizer.Normalize(null);
                }
                return CharacterLibraryDocumentNormalizer.Normalize(document);
            }
            catch (ArgumentException)
            {
                AtomicJsonFile.TryBackupCorrupt(LibraryFilePath);
                return CharacterLibraryDocumentNormalizer.Normalize(null);
            }
        }

        internal DateTime ResolveLastWriteTimeUtc()
        {
            return AtomicJsonFile.LastWriteTimeUtc(LibraryFilePath);
        }

        public void Save(CharacterLibraryDocument document)
        {
            CharacterLibraryDocument normalized =
                CharacterLibraryDocumentNormalizer.Normalize(document);
            normalized.updatedAtUtc = DateTime.UtcNow.ToString("O");

            AtomicJsonFile.SaveAtomic(
                LibraryFilePath, JsonUtility.ToJson(normalized, true));
        }
    }
}
