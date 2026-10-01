using System;
using System.Collections.Generic;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 캐릭터 라이브러리 JSON 문서. MainRecordingSettingsDocument와 같은 계약:
    /// schemaVersion으로 마이그레이션하고 updatedAtUtc는 저장 시 갱신된다.
    /// </summary>
    [Serializable]
    public sealed class CharacterLibraryDocument
    {
        public int schemaVersion = 1;
        public string updatedAtUtc = string.Empty;
        public string activeCharacterId = string.Empty;
        public List<CharacterLibraryEntry> entries = new List<CharacterLibraryEntry>();
    }
}
