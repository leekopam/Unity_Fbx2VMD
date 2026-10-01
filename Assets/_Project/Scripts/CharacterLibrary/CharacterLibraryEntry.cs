using System;
using System.Collections.Generic;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 캐릭터 라이브러리에 등록된 VRM 캐릭터 1개의 영속 데이터.
    /// id는 경로와 무관한 안정 식별자이고 sourcePath는 갱신 가능한 힌트다.
    /// </summary>
    [Serializable]
    public sealed class CharacterLibraryEntry
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string sourcePath = string.Empty;
        public long fileSize;
        public string lastWriteTimeUtc = string.Empty;
        public string contentHash = string.Empty;
        public string vrmSpecVersion = string.Empty;
        public CharacterLibraryEntryMeta meta = new CharacterLibraryEntryMeta();
        public string thumbnailFileName = string.Empty;
        public CharacterLibraryEntryCompatibility compatibility = new CharacterLibraryEntryCompatibility();
        public string defaultPresetId = string.Empty;
        public List<string> presetIds = new List<string>();
        public string registeredAtUtc = string.Empty;
        public string lastUsedAtUtc = string.Empty;
        public string status = CharacterLibraryEntryStatus.Ready;
        public string lastError = string.Empty;
    }

    /// <summary>
    /// VRM 메타데이터 중 라이브러리 표시에 필요한 안전한 부분집합.
    /// JsonUtility 대응을 위해 열거형은 문자열로 저장한다.
    /// </summary>
    [Serializable]
    public sealed class CharacterLibraryEntryMeta
    {
        public string title = string.Empty;
        public string version = string.Empty;
        public string author = string.Empty;
        public string contactInformation = string.Empty;
        public string reference = string.Empty;
        public string licenseType = string.Empty;
        public string allowedUser = string.Empty;
        public string commercialUsage = string.Empty;
        public string otherLicenseUrl = string.Empty;
    }

    /// <summary>
    /// 마지막으로 확인한 호환성 판정 결과.
    /// </summary>
    [Serializable]
    public sealed class CharacterLibraryEntryCompatibility
    {
        public bool humanoidValid;
        public string checkedAtUtc = string.Empty;
        public string univrmVersion = string.Empty;
    }

    /// <summary>
    /// 엔트리 상태 문자열. 신규 값 추가 시 Normalizer의 화이트리스트도 갱신한다.
    /// </summary>
    public static class CharacterLibraryEntryStatus
    {
        public const string Ready = "ready";
        public const string Missing = "missing";
        public const string Changed = "changed";
        public const string Error = "error";

        internal static bool IsKnown(string status)
        {
            return status == Ready || status == Missing || status == Changed || status == Error;
        }
    }
}
