using UnityEngine;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 라이브러리에서 로드된 캐릭터 루트에 붙는 표식.
    /// 도메인 리로드로 세션 참조가 유실돼도 씬에서 라이브러리 캐릭터를 식별할 수 있다.
    /// </summary>
    public sealed class CharacterLibraryInstance : MonoBehaviour
    {
        public string entryId = string.Empty;

        /// <summary>
        /// 스왑 전에 파이프라인이 가리키던 씬 소유 캐릭터.
        /// static 대신 직렬화 필드로 두어 도메인 리로드·씬 재직렬화 후에도 복원 경로가 살아난다.
        /// </summary>
        public GameObject previousSceneTarget;
    }
}
