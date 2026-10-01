using System.Collections.Generic;
using Fbx2Vmd.FBXImporter;
using UniGLTF;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 로드된 라이브러리 캐릭터와 FBXVmdPipeline.targetCharacter 사이의
    /// 교체/복원을 담당하는 세션 상태. 에디터 창과 런타임 매니저가 공용한다.
    /// 복원에 필요한 이전 타겟은 CharacterLibraryInstance 마커에 직렬화로 보관한다.
    /// </summary>
    public static class CharacterLibrarySceneBinding
    {
        /// <summary>
        /// 검증이 끝난 인스턴스를 활성 캐릭터로 커밋한다.
        /// 기존 라이브러리 캐릭터는 파괴하고, 씬 소유 캐릭터는 비활성화로 보존한다.
        /// </summary>
        public static void Commit(FBXVmdPipeline pipeline, string entryId, RuntimeGltfInstance instance)
        {
            if (pipeline == null || instance == null || instance.Root == null)
            {
                return;
            }

            GameObject newRoot = instance.Root;
            GameObject previousTarget = pipeline.targetCharacter;
            CharacterLibraryInstance previousLibrary =
                FindMarkerOn(previousTarget);

            var marker = newRoot.GetComponent<CharacterLibraryInstance>();
            if (marker == null)
            {
                marker = newRoot.AddComponent<CharacterLibraryInstance>();
            }
            marker.entryId = entryId ?? string.Empty;

            if (previousLibrary != null)
            {
                // 다른 라이브러리 캐릭터를 교체하는 경우 원본 씬 캐릭터를 계승한다.
                marker.previousSceneTarget = previousLibrary.previousSceneTarget;
            }
            else if (previousTarget != null && previousTarget != newRoot)
            {
                // 씬 소유 캐릭터는 파괴하지 않고 비활성화로 보존한다.
                previousTarget.SetActive(false);
                marker.previousSceneTarget = previousTarget;
            }

            newRoot.name = "Library Character";
            newRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.ShowMeshes();
            newRoot.SetActive(true);

            // 검증·배치가 모두 끝난 뒤에 타겟을 한 번만 교체한다.
            pipeline.targetCharacter = newRoot;
            pipeline.RebindIdlePoseGuard();

            // 같은 씬의 이전 라이브러리 캐릭터가 남아 있으면 정리한다.
            // 다른 열린 씬/프리팹 편집 단계의 마커는 건드리지 않는다.
            List<GameObject> leftovers = FindAllLibraryRoots(pipeline.gameObject.scene);
            for (int i = 0; i < leftovers.Count; i++)
            {
                if (leftovers[i] != null && leftovers[i] != newRoot)
                {
                    DestroyObject(leftovers[i]);
                }
            }
        }

        /// <summary>
        /// 라이브러리 캐릭터를 제거하고 스왑 전 씬 캐릭터로 되돌린다.
        /// 이전 타겟이 없으면 라이브러리 루트만 제거하고 false를 돌려준다.
        /// rebindGuard=false면 파이프라인의 유휴 가드 재초기화를 생략한다(씬 저장 콜백용).
        /// </summary>
        public static bool Restore(
            FBXVmdPipeline pipeline, out bool removedLibraryCharacter, bool rebindGuard = true)
        {
            GameObject libraryRoot = FindActiveLibraryRoot(pipeline);
            removedLibraryCharacter = libraryRoot != null;
            if (libraryRoot == null)
            {
                return false;
            }

            CharacterLibraryInstance marker = libraryRoot.GetComponent<CharacterLibraryInstance>();
            GameObject previous = marker != null ? marker.previousSceneTarget : null;

            // FindActiveLibraryRoot와 같은 기준: 타겟이 루트 자체이거나 그 자식이면
            // 라이브러리 캐릭터를 가리키는 것으로 본다.
            bool targetsLibrary = pipeline != null && pipeline.targetCharacter != null &&
                                  pipeline.targetCharacter.transform.root == libraryRoot.transform;
            bool targetFree = pipeline != null && pipeline.targetCharacter == null;

            if (pipeline != null && (targetsLibrary || targetFree))
            {
                pipeline.targetCharacter = previous;
                if (rebindGuard)
                {
                    pipeline.RebindIdlePoseGuard();
                }
            }

            DestroyObject(libraryRoot);

            // 파이프라인이 없거나 타겟이 비어 있거나 라이브러리를 가리키던 경우에만
            // 비활성 보존된 이전 캐릭터를 되살린다. 제3자가 다른 캐릭터를 가리키는
            // 동안에는 활성 캐릭터가 2개가 되는 것을 막기 위해 보류한다.
            bool canReactivate = previous != null &&
                                 (pipeline == null || targetFree || targetsLibrary);
            if (canReactivate)
            {
                previous.SetActive(true);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 파이프라인 씬(없으면 활성 씬)에서 라이브러리 표식이 붙은 루트를 찾는다.
        /// 파이프라인이 가리키는 루트를 우선하고, 없으면 첫 마커 루트를 돌려준다.
        /// </summary>
        public static GameObject FindActiveLibraryRoot(FBXVmdPipeline pipeline = null)
        {
            Scene scene = pipeline != null
                ? pipeline.gameObject.scene
                : SceneManager.GetActiveScene();
            List<GameObject> roots = FindAllLibraryRoots(scene);
            if (roots.Count == 0)
            {
                return null;
            }

            if (pipeline != null && pipeline.targetCharacter != null)
            {
                GameObject target = pipeline.targetCharacter;
                for (int i = 0; i < roots.Count; i++)
                {
                    if (roots[i] == target || roots[i].transform == target.transform.root)
                    {
                        return roots[i];
                    }
                }
            }

            return roots[0];
        }

        /// <summary>
        /// 지정한 씬에 존재하는 모든 라이브러리 마커 루트를 돌려준다(프리팹 에셋 제외).
        /// </summary>
        public static List<GameObject> FindAllLibraryRoots(Scene scene)
        {
            var roots = new List<GameObject>();
            CharacterLibraryInstance[] markers =
                Resources.FindObjectsOfTypeAll<CharacterLibraryInstance>();
            for (int i = 0; i < markers.Length; i++)
            {
                CharacterLibraryInstance marker = markers[i];
                if (marker == null ||
                    !marker.gameObject.scene.IsValid() ||
                    marker.gameObject.scene != scene)
                {
                    continue;
                }
                roots.Add(marker.gameObject);
            }
            return roots;
        }

        /// <summary>
        /// 라이브러리 루트를 파괴한다. 씬 저장 전 정리 훅에서도 사용한다.
        /// </summary>
        public static void DestroyLibraryRoot(GameObject root)
        {
            if (root == null || root.GetComponent<CharacterLibraryInstance>() == null)
            {
                return;
            }
            DestroyObject(root);
        }

        private static CharacterLibraryInstance FindMarkerOn(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            return target.GetComponent<CharacterLibraryInstance>() ??
                   target.GetComponentInParent<CharacterLibraryInstance>();
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
