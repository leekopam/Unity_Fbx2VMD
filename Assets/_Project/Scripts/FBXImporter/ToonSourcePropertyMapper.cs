using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 소스 머티리얼 셰이더(MMD4Mecanim/MToon/Standard 등)의 의미 있는 파라미터를
    /// 툰 머티리얼로 이식한다. 없는 프로퍼티는 건너뛰므로 소스 셰이더 종류에 무관하게 동작한다.
    /// </summary>
    public static class ToonSourcePropertyMapper
    {
        public static void Apply(Material source, Material target)
        {
            if (source == null || target == null)
            {
                return;
            }

            // 음영 팔레트 — MToon 음영색은 lilToon 1단 그림자색과 의미가 같음.
            CopyColor(source, target, "_ShadeColor", "_ShadowColor");
            // MToon ShadeToony는 음영 경계 경도이므로 lilToon 블러로 역변환함.
            if (TryGetFloat(source, "_ShadeToony", out float shadeToony))
            {
                SetFloat(target, "_ShadowBlur", 1f - Mathf.Clamp01(shadeToony));
            }

            CopyFloat(source, target, "_Cutoff", "_Cutoff");

            // 아웃라인 색 — MToon _OutlineColor, MMD4Mecanim _EdgeColor.
            CopyColor(source, target, "_OutlineColor", "_OutlineColor");
            CopyColor(source, target, "_EdgeColor", "_OutlineColor");

            // 림라이트 — MToon과 lilToon이 같은 프로퍼티명을 씀.
            CopyColor(source, target, "_RimColor", "_RimColor");
            CopyFloat(source, target, "_RimFresnelPower", "_RimFresnelPower");
        }

        private static bool TryGetFloat(Material material, string propertyName, out float value)
        {
            value = 0f;
            if (material.HasProperty(propertyName))
            {
                value = material.GetFloat(propertyName);
                return true;
            }

            return false;
        }

        private static void CopyFloat(Material source, Material target, string sourceName, string targetName)
        {
            if (source.HasProperty(sourceName) && target.HasProperty(targetName))
            {
                target.SetFloat(targetName, source.GetFloat(sourceName));
            }
        }

        private static void CopyColor(Material source, Material target, string sourceName, string targetName)
        {
            if (source.HasProperty(sourceName) && target.HasProperty(targetName))
            {
                target.SetColor(targetName, source.GetColor(sourceName));
            }
        }

        private static void SetFloat(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
