using System.Collections.Generic;
using Fbx2Vmd.FBXImporter;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.EditorTools
{
    /// <summary>
    /// 씬에 배치된 기존 모델(MMD4Mecanim 등)의 머티리얼을 툰 프리셋 복제본으로 교체한다.
    /// 원본 .mat은 건드리지 않고, 원본과 같은 폴더의 Toon/ 하위에 변환본을 에셋으로 저장한다.
    /// 에디트 모드·플레이 모드·빌드 어디서도 동일하게 보이도록 영구 변환 방식이다.
    /// </summary>
    public static class SceneModelToonConverter
    {
        const string ToonSubFolderName = "Toon";
        const string FallbackOutputFolder = "Assets/ToonConverted";

        [MenuItem("Tools/Toon/선택 오브젝트에 툰 머티리얼 적용")]
        public static void ApplyToSelection()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("[Toon] 변환할 오브젝트를 먼저 선택해 주세요.");
                return;
            }

            int converted = ApplyTo(root);
            Debug.Log($"[Toon] 툰 머티리얼 변환 완료. 대상={root.name}, 변환 슬롯={converted}");
        }

        /// <summary>
        /// root 아래 모든 렌더러의 머티리얼을 툰 변환본으로 교체한다.
        /// 변환본은 원본 머티리얼 에셋 폴더의 Toon/ 하위에 저장된다.
        /// 반환값은 툰으로 교체된 슬롯 수다.
        /// </summary>
        public static int ApplyTo(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }

            var converted = new Dictionary<Material, Material>();
            int slotCount = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material source = slots[i];
                    if (source == null || ToonMaterialConverter.IsToonMaterial(source))
                    {
                        continue;
                    }

                    if (!converted.TryGetValue(source, out Material toon))
                    {
                        toon = CreateToonVariant(source);
                        if (toon == null)
                        {
                            continue;
                        }

                        toon = SaveVariantAsset(toon, source);
                        converted[source] = toon;
                    }

                    slots[i] = toon;
                    changed = true;
                    slotCount++;
                }

                if (changed)
                {
                    Undo.RecordObject(renderer, "툰 머티리얼 적용");
                    renderer.sharedMaterials = slots;
                    EditorUtility.SetDirty(renderer);
                }
            }

            AssetDatabase.SaveAssets();
            return slotCount;
        }

        /// <summary>
        /// 원본 머티리얼 하나를 툰 프리셋 복제본으로 변환한다.
        /// 텍스처와 기본 색을 이어받는다. 변환 불가(프리셋 없음)면 null.
        /// 에셋 저장은 하지 않으므로 테스트에서도 사용할 수 있다.
        /// </summary>
        public static Material CreateToonVariant(Material source)
        {
            Material toon = ToonMaterialConverter.CreateToonVariant(source);
            TryAttachSdfMask(toon, source);
            return toon;
        }

        // 캐릭터별 SDF 얼굴 그림자 마스크 자동 부착.
        // 규약: 모델 폴더 아래 "{원본 머티리얼명}_sdf" 텍스처(예: tex/sdf/face00_sdf.png).
        // Face 프리셋은 캐릭터 무관 공용이라 마스크는 여기서 슬롯별로 단다.
        private static void TryAttachSdfMask(Material toon, Material source)
        {
            if (toon == null || source == null
                || !toon.HasProperty("_ShadowStrengthMask")
                || !toon.HasProperty("_ShadowMaskType"))
            {
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            string modelRoot = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(modelRoot))
            {
                return;
            }

            // 머티리얼이 Materials/ 같은 하위 폴더에 있으므로 한 단계 위(모델 루트)를 검색 범위로 한다.
            // 단 "Assets"까지 올라가면 프로젝트 전체 탐색이 되므로 그 위로는 올리지 않는다.
            string parent = System.IO.Path.GetDirectoryName(modelRoot)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && parent != "Assets"
                && AssetDatabase.IsValidFolder(parent))
            {
                modelRoot = parent;
            }

            var nameCandidates = new List<string> { source.name };
            int dot = source.name.IndexOf('.');
            if (dot >= 0 && dot + 1 < source.name.Length)
            {
                // MMD 머티리얼명 "9.face02"에 대해 접두 없는 "face02_sdf"도 수용한다.
                nameCandidates.Add(source.name.Substring(dot + 1));
            }

            foreach (string name in nameCandidates)
            {
                foreach (string guid in AssetDatabase.FindAssets(
                             $"{name}_sdf t:Texture2D", new[] { modelRoot }))
                {
                    // FindAssets는 부분일치 — "9.face99_sdf"가 "19.face99_sdf"에도 걸리므로
                    // 파일명 정확 일치만 채택한다.
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.Equals(
                            System.IO.Path.GetFileNameWithoutExtension(assetPath),
                            name + "_sdf",
                            System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Texture2D sdf = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (sdf == null)
                    {
                        continue;
                    }

                    toon.SetTexture("_ShadowStrengthMask", sdf);
                    toon.SetFloat("_ShadowMaskType", 2f);
                    return;
                }
            }
        }

        public static bool IsToonMaterial(Material material)
        {
            return ToonMaterialConverter.IsToonMaterial(material);
        }

        // Delete+Create는 .meta를 새로 만들어 GUID가 바뀌고, 변환본을 참조하는
        // 다른 씬·프리팹이 Missing Material로 깨진다. 기존 에셋이 있으면 내용만
        // 덮어써 에셋 ID를 유지한다. 반환값은 실제로 씬에 달아야 할 에셋이다.
        private static Material SaveVariantAsset(Material toon, Material source)
        {
            string folder = ResolveOutputFolder(source);
            string path = $"{folder}/Toon_{toon.name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(toon, path);
                return toon;
            }

            EditorUtility.CopySerialized(toon, existing);
            // CopySerialized는 m_Name까지 덮어쓰므로 파일명("Toon_*.mat")과 동기화한다.
            existing.name = $"Toon_{toon.name}";
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(toon);
            return existing;
        }

        private static string ResolveOutputFolder(Material source)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string folder = string.IsNullOrEmpty(sourcePath)
                ? FallbackOutputFolder
                : System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') + "/" + ToonSubFolderName;

            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    folder = FallbackOutputFolder;
                    parent = "Assets";
                }

                AssetDatabase.CreateFolder(parent, ToonSubFolderName);
                folder = parent + "/" + ToonSubFolderName;
            }

            return folder;
        }
    }
}
