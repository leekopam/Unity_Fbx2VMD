using System.Collections.Generic;
using Fbx2Vmd.FBXImporter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 편집 모드에서 씬 저장 직전에 라이브러리 캐릭터를 원래 씬 캐릭터로 되돌린다.
    /// 런타임 생성 Mesh/Material/Texture는 직렬화되지 않으므로, 저장 시
    /// 깨진 계층이 .unity에 남는 것을 막기 위한 안전장치다.
    /// </summary>
    [InitializeOnLoad]
    internal static class CharacterLibrarySceneSaveGuard
    {
        static CharacterLibrarySceneSaveGuard()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            if (Application.isPlaying)
            {
                return;
            }

            List<GameObject> roots = CharacterLibrarySceneBinding.FindAllLibraryRoots(scene);
            if (roots.Count == 0)
            {
                return;
            }

            FBXVmdPipeline pipeline = null;
            for (int i = 0; i < roots.Count; i++)
            {
                GameObject root = roots[i];
                if (root == null || root.scene != scene)
                {
                    continue;
                }

                if (pipeline == null)
                {
                    pipeline = Object.FindObjectOfType<FBXVmdPipeline>(true);
                }

                // 파이프라인이 가리키는 루트는 이전 캐릭터로 복원하고,
                // 나머지 누수 루트는 그냥 제거해 어느 것도 저장되지 않게 한다.
                if (pipeline != null && pipeline.targetCharacter != null &&
                    pipeline.targetCharacter.transform.root == root.transform)
                {
                    // 저장 콜백 안에서는 유휴 가드 재초기화(변환 변경)를 하지 않는다.
                    CharacterLibrarySceneBinding.Restore(pipeline, out _, rebindGuard: false);
                    // 매니저를 거치지 않는 복원이므로 내부 상태를 정리하게 한다.
                    if (CharacterLibraryManager.Current != null)
                    {
                        CharacterLibraryManager.Current.NotifyExternalRestore();
                    }
                }
                else
                {
                    // 파이프라인이 없거나 다른 캐릭터를 가리켜도, 비활성 보존된
                    // 이전 씬 캐릭터가 그대로 저장되지 않게 되살린다.
                    var marker = root.GetComponent<CharacterLibraryInstance>();
                    GameObject previous = marker != null ? marker.previousSceneTarget : null;
                    if (previous != null)
                    {
                        previous.SetActive(true);
                    }
                    CharacterLibrarySceneBinding.DestroyLibraryRoot(root);
                }
            }
        }
    }
}
