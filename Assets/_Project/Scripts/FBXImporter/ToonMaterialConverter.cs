using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 씬·런타임에 존재하는 머티리얼을 툰 프리셋 복제본으로 교체하는 공용 코어.
    /// 에셋 저장·Undo 없이 인스턴스만 만드므로 런타임(VRM 로드 등)에서도 쓸 수 있다.
    /// </summary>
    public static class ToonMaterialConverter
    {
        /// <summary>
        /// root 아래 모든 렌더러의 머티리얼을 툰 변환본으로 교체한다.
        /// 반환값은 툰으로 교체된 슬롯 수다. 반환된 딕셔너리는 원본→변환본 매핑이다.
        /// </summary>
        public static int ApplyTo(GameObject root, Dictionary<Material, Material> converted = null)
        {
            if (root == null)
            {
                return 0;
            }

            var cache = converted ?? new Dictionary<Material, Material>();
            int slotCount = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material source = slots[i];
                    if (source == null || IsToonMaterial(source))
                    {
                        continue;
                    }

                    if (!cache.TryGetValue(source, out Material toon))
                    {
                        toon = CreateToonVariant(source);
                        if (toon == null)
                        {
                            continue;
                        }

                        cache[source] = toon;
                    }

                    slots[i] = toon;
                    changed = true;
                    slotCount++;
                }

                if (changed)
                {
                    renderer.sharedMaterials = slots;
                }
            }

            return slotCount;
        }

        /// <summary>
        /// 원본 머티리얼 하나를 툰 프리셋 복제본으로 변환한다.
        /// 텍스처와 기본 색을 이어받는다. 변환 불가(프리셋 없음)면 null.
        /// </summary>
        public static Material CreateToonVariant(Material source)
        {
            if (source == null || IsToonMaterial(source))
            {
                return null;
            }

            ToonMaterialRole surface = ResolveSurface(source);
            ToonMaterialRole role =
                ToonMaterialRoleResolver.ResolveCharacterRole(source.name) ?? surface;
            // 알파가 있는 피부·눈 슬롯은 불투명 템플릿에 올리면 알파가 유실된다.
            if (surface != ToonMaterialRole.Opaque &&
                (role == ToonMaterialRole.Skin || role == ToonMaterialRole.Eye))
            {
                role = surface;
            }

            if (!ToonMaterialLibrary.TryInstantiate(role, surface, out Material toon))
            {
                return null;
            }

            toon.name = source.name;
            if (source.HasProperty("_MainTex") && source.mainTexture != null)
            {
                toon.mainTexture = source.mainTexture;
            }

            if (source.HasProperty("_Color") && toon.HasProperty("_Color"))
            {
                toon.color = source.color;
            }

            // 소스 셰이더별 파라미터(음영색·아웃라인색 등)를 있으면 이식한다.
            ToonSourcePropertyMapper.Apply(source, toon);

            return toon;
        }

        public static bool IsToonMaterial(Material material)
        {
            return material != null
                && material.shader != null
                && material.shader.name.Contains("lilToon");
        }

        // MMD 투명 변형은 셰이더명으로, MToon은 _BlendMode(0 불투명/1 컷아웃/2 투명)로 판정한다.
        private static ToonMaterialRole ResolveSurface(Material source)
        {
            if (source.HasProperty("_BlendMode"))
            {
                int blendMode = (int)source.GetFloat("_BlendMode");
                if (blendMode == 2)
                {
                    return ToonMaterialRole.Transparent;
                }
                if (blendMode == 1)
                {
                    return ToonMaterialRole.Cutout;
                }
            }

            bool hasAlpha = source.shader != null
                && source.shader.name.Contains("Transparent");
            return hasAlpha ? ToonMaterialRole.Cutout : ToonMaterialRole.Opaque;
        }
    }
}
