using System;
using System.Collections.Generic;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 캐릭터 라이브러리 문서의 불변 조건을 복원하는 IO 독립 정규화기.
    /// 손상·수기 편집·구버전 문서도 안전한 형태로 만든다.
    /// </summary>
    internal static class CharacterLibraryDocumentNormalizer
    {
        internal static CharacterLibraryDocument Normalize(CharacterLibraryDocument document)
        {
            if (document == null)
            {
                return new CharacterLibraryDocument();
            }

            if (document.schemaVersion <= 0)
            {
                document.schemaVersion = 1;
            }

            if (document.updatedAtUtc == null)
            {
                document.updatedAtUtc = string.Empty;
            }

            if (document.activeCharacterId == null)
            {
                document.activeCharacterId = string.Empty;
            }

            if (document.entries == null)
            {
                document.entries = new List<CharacterLibraryEntry>();
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < document.entries.Count; i++)
            {
                CharacterLibraryEntry entry = document.entries[i];
                if (entry == null)
                {
                    document.entries.RemoveAt(i);
                    i--;
                    continue;
                }

                NormalizeEntry(entry);

                if (entry.id.Length == 0)
                {
                    // 수기 편집 등으로 id가 비어 있으면 새로 부여해 엔트리를 살린다.
                    entry.id = Guid.NewGuid().ToString("N");
                }

                if (!seenIds.Add(entry.id))
                {
                    // 동일 id 중복은 첫 항목만 유지한다.
                    document.entries.RemoveAt(i);
                    i--;
                }
            }

            return document;
        }

        private static void NormalizeEntry(CharacterLibraryEntry entry)
        {
            entry.id = NormalizeString(entry.id);
            entry.displayName = NormalizeString(entry.displayName);
            entry.sourcePath = NormalizeString(entry.sourcePath);
            entry.lastWriteTimeUtc = NormalizeString(entry.lastWriteTimeUtc);
            entry.contentHash = NormalizeString(entry.contentHash);
            entry.vrmSpecVersion = NormalizeString(entry.vrmSpecVersion);
            entry.thumbnailFileName = NormalizeString(entry.thumbnailFileName);
            entry.defaultPresetId = NormalizeString(entry.defaultPresetId);
            entry.registeredAtUtc = NormalizeString(entry.registeredAtUtc);
            entry.lastUsedAtUtc = NormalizeString(entry.lastUsedAtUtc);
            entry.lastError = NormalizeString(entry.lastError);

            if (!CharacterLibraryEntryStatus.IsKnown(entry.status))
            {
                entry.status = CharacterLibraryEntryStatus.Ready;
            }

            if (entry.meta == null)
            {
                entry.meta = new CharacterLibraryEntryMeta();
            }

            entry.meta.title = NormalizeString(entry.meta.title);
            entry.meta.version = NormalizeString(entry.meta.version);
            entry.meta.author = NormalizeString(entry.meta.author);
            entry.meta.contactInformation = NormalizeString(entry.meta.contactInformation);
            entry.meta.reference = NormalizeString(entry.meta.reference);
            entry.meta.licenseType = NormalizeString(entry.meta.licenseType);
            entry.meta.allowedUser = NormalizeString(entry.meta.allowedUser);
            entry.meta.commercialUsage = NormalizeString(entry.meta.commercialUsage);
            entry.meta.otherLicenseUrl = NormalizeString(entry.meta.otherLicenseUrl);

            if (entry.compatibility == null)
            {
                entry.compatibility = new CharacterLibraryEntryCompatibility();
            }

            entry.compatibility.checkedAtUtc = NormalizeString(entry.compatibility.checkedAtUtc);
            entry.compatibility.univrmVersion = NormalizeString(entry.compatibility.univrmVersion);

            if (entry.presetIds == null)
            {
                entry.presetIds = new List<string>();
            }
        }

        private static string NormalizeString(string value)
        {
            return value ?? string.Empty;
        }
    }
}
