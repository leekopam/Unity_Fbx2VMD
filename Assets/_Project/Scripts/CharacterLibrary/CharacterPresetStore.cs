using System;
using UnityEngine;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 캐릭터 1개의 프리셋 문서(presets/&lt;characterId&gt;.json)의 원자적 저장/복원.
    /// 계약은 CharacterLibraryStore와 동일: 원자적 교체, 손상 백업, 읽기 실패 시 저장 차단.
    /// </summary>
    public sealed class CharacterPresetStore
    {
        public CharacterPresetStore(string presetFilePath)
        {
            if (string.IsNullOrWhiteSpace(presetFilePath))
            {
                throw new ArgumentException("프리셋 파일 경로가 비어 있습니다.", nameof(presetFilePath));
            }
            FilePath = presetFilePath.Trim();
        }

        public string FilePath { get; }

        /// <summary>
        /// 마지막 읽기가 IO 오류로 실패했는지. true이면 Save가 차단된다(데이터 보호).
        /// </summary>
        public bool LastReadFailed { get; private set; }

        public CharacterPresetDocument LoadOrCreateDefault()
        {
            if (!AtomicJsonFile.TryReadAllText(FilePath, out string json, out bool ioFailed))
            {
                LastReadFailed = ioFailed;
                return CharacterPresetDocumentNormalizer.Normalize(null);
            }

            LastReadFailed = false;
            try
            {
                CharacterPresetDocument document = string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonUtility.FromJson<CharacterPresetDocument>(json);
                if (document == null)
                {
                    AtomicJsonFile.TryBackupCorrupt(FilePath);
                    return CharacterPresetDocumentNormalizer.Normalize(null);
                }
                return CharacterPresetDocumentNormalizer.Normalize(document);
            }
            catch (ArgumentException)
            {
                AtomicJsonFile.TryBackupCorrupt(FilePath);
                return CharacterPresetDocumentNormalizer.Normalize(null);
            }
        }

        public void Save(CharacterPresetDocument document)
        {
            if (LastReadFailed)
            {
                throw new InvalidOperationException(
                    "프리셋 파일을 읽지 못한 상태입니다. " +
                    "기존 데이터 보호를 위해 저장을 차단했습니다.");
            }

            CharacterPresetDocument normalized =
                CharacterPresetDocumentNormalizer.Normalize(document);
            normalized.updatedAtUtc = DateTime.UtcNow.ToString("O");
            AtomicJsonFile.SaveAtomic(FilePath, JsonUtility.ToJson(normalized, true));
        }
    }
}
