using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Resources/ToonPresets 아래에 미리 만들어 둔 툰 템플릿 머티리얼을 읽어
    /// 런타임 머티리얼을 복제한다. ClothPresetLibrary와 같은 규약을 따른다.
    /// 탐색 순서: Character/{키}_{역할} → Toon_{역할} → Toon_{폴백역할}.
    /// 템플릿이 하나도 없으면 false를 반환하고 호출 측이 기존 경로로 폴백한다.
    /// 템플릿 .mat 에셋이 곧 참조이므로 빌드에서도 셰이더가 자동 포함된다.
    /// </summary>
    public static class ToonMaterialLibrary
    {
        public const string ResourceRoot = "ToonPresets/";

        static readonly Dictionary<string, Material> templateCache =
            new Dictionary<string, Material>();

        /// <summary>
        /// 역할에 맞는 템플릿을 복제해 반환한다.
        /// fallbackRole은 부위 역할(Skin/Hair/Eye) 템플릿이 없을 때 쓰는 표면 역할이다.
        /// </summary>
        public static bool TryInstantiate(
            ToonMaterialRole role,
            ToonMaterialRole fallbackRole,
            out Material material,
            string characterKey = null)
        {
            material = null;
            Material template = FindTemplate(role, fallbackRole, characterKey);
            if (template == null)
            {
                return false;
            }

            material = new Material(template);
            return true;
        }

        private static Material FindTemplate(
            ToonMaterialRole role, ToonMaterialRole fallbackRole, string characterKey)
        {
            if (!string.IsNullOrEmpty(characterKey))
            {
                Material characterTemplate = LoadTemplate(
                    $"Character/{characterKey}_{TemplateName(role)}");
                if (characterTemplate != null)
                {
                    return characterTemplate;
                }
            }

            Material roleTemplate = LoadTemplate(TemplateName(role));
            if (roleTemplate != null)
            {
                return roleTemplate;
            }

            if (fallbackRole != role)
            {
                return LoadTemplate(TemplateName(fallbackRole));
            }

            return null;
        }

        public static string TemplateName(ToonMaterialRole role)
        {
            return "Toon_" + role;
        }

        private static Material LoadTemplate(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return null;
            }

            string key = ResourceRoot + resourceName;
            if (templateCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            // null은 캐시하지 않는다 — 에셋이 나중에 생성·재임포트되면 다시 찾아야 하므로.
            Material template = Resources.Load<Material>(key);
            if (template != null)
            {
                templateCache[key] = template;
            }
            return template;
        }
    }
}
