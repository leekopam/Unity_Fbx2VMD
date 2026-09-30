using System.Collections.Generic;
using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// MC2 공식 프리셋(JSON)을 베이스라인으로 ClothSerializeData에 적용한다.
    /// 프리셋은 Resources/PhysicsPresets 아래 TextAsset으로 두어 런타임 구축과
    /// 플레이어 빌드에서도 동일하게 동작한다. ImportJson은 파라미터 블록의
    /// 값형만 덮어쓰므로 콜라이더/루트 본/렌더러 같은 런타임 목록은 보존된다.
    /// </summary>
    public static class ClothPresetLibrary
    {
        public const string ResourceRoot = "PhysicsPresets/";

        public const string FrontHair = "MC2_Preset_FrontHair";
        public const string LongHair = "MC2_Preset_LongHair";
        public const string ShortHair = "MC2_Preset_ShortHair";
        public const string Accessory = "MC2_Preset_Accessory";
        public const string Skirt = "MC2_Preset_Skirt";

        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        /// <summary>
        /// 프리셋을 sdata에 적용한다. 프리셋이 없거나 파싱 실패 시 false를 반환하고
        /// sdata는 변경하지 않는다(호출 측이 수작업 경로로 폴백).
        /// </summary>
        public static bool TryImport(ClothSerializeData sdata, string presetName)
        {
            if (sdata == null || string.IsNullOrEmpty(presetName))
                return false;
            if (!cache.TryGetValue(presetName, out string json))
            {
                var asset = Resources.Load<TextAsset>(ResourceRoot + presetName);
                if (asset == null)
                    return false;
                json = asset.text;
                cache[presetName] = json;
            }
            return sdata.ImportJson(json);
        }

        /// <summary>에디터 테스트·디버그용 캐시 초기화.</summary>
        public static void ClearCache() => cache.Clear();
    }
}
