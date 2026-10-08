using System;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// FBX 머티리얼 이름으로 캐릭터 부위 역할을 추정한다.
    /// 순수 문자열 판정만 하므로 에디터/플레이어/테스트 어디서나 동일하다.
    /// </summary>
    public static class ToonMaterialRoleResolver
    {
        // 눈 계열이지만 눈 템플릿이 아니라 Cutout이어야 하는 이름들.
        static readonly string[] eyeExclusions =
        {
            "lash", "eyelash", "brow", "eyebrow", "まつ毛", "まつげ", "眉",
        };

        static readonly (string keyword, ToonMaterialRole role)[] table =
        {
            ("eye", ToonMaterialRole.Eye),
            ("瞳", ToonMaterialRole.Eye),
            ("目", ToonMaterialRole.Eye),
            ("hitomi", ToonMaterialRole.Eye),
            ("hair", ToonMaterialRole.Hair),
            ("髪", ToonMaterialRole.Hair),
            ("face", ToonMaterialRole.Face),
            ("顔", ToonMaterialRole.Face),
            ("skin", ToonMaterialRole.Skin),
            ("body", ToonMaterialRole.Skin),
            ("肌", ToonMaterialRole.Skin),
        };

        /// <summary>이름에서 캐릭터 역할을 추정한다. 못 찾으면 null.</summary>
        public static ToonMaterialRole? ResolveCharacterRole(string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName))
            {
                return null;
            }

            string lowered = materialName.Trim().ToLowerInvariant();
            foreach ((string keyword, ToonMaterialRole role) in table)
            {
                if (!lowered.Contains(keyword))
                {
                    continue;
                }

                // eyelash/eyebrow 계열은 눈이 아니라 알파컷 머티리얼로 본다.
                if (role == ToonMaterialRole.Eye && ContainsAny(lowered, eyeExclusions))
                {
                    return null;
                }

                return role;
            }

            return null;
        }

        private static bool ContainsAny(string haystack, string[] needles)
        {
            foreach (string needle in needles)
            {
                if (haystack.Contains(needle))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
