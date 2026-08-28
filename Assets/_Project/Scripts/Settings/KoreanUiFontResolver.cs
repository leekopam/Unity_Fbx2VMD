using TMPro;
using UnityEngine;

namespace Fbx2Vmd.Settings
{
    internal static class KoreanUiFontResolver
    {
        private const string KoreanUiTextSample =
            "가나다FBX파일임포트선택해프로젝트로가져오고모션캡쳐설정을시작합니다공유설정을불러왔습니다저장했습니다시작시열기시네마토그래피환경캐릭터비활성화가로세로경로준비중닫기";

        private static readonly string[] KoreanUiFontNames =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Noto Sans KR",
            "Noto Sans CJK KR",
            "NanumGothic",
            "Nanum Gothic"
        };

        private static TMP_FontAsset cachedKoreanUiFont;
        private static Font cachedKoreanLegacyUiFont;

        internal static bool TryGetTmpFont(out TMP_FontAsset fontAsset)
        {
            if (cachedKoreanUiFont != null)
            {
                fontAsset = cachedKoreanUiFont;
                return true;
            }

            Font osFont = Font.CreateDynamicFontFromOSFont(KoreanUiFontNames, 32);
            if (osFont == null)
            {
                fontAsset = null;
                return false;
            }

            TMP_FontAsset createdFontAsset = TMP_FontAsset.CreateFontAsset(osFont);
            if (createdFontAsset == null)
            {
                fontAsset = null;
                return false;
            }

            createdFontAsset.name = "Main Recording Runtime Korean UI Font";
            createdFontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            createdFontAsset.TryAddCharacters(KoreanUiTextSample, out _);
            if (!SupportsText(createdFontAsset, KoreanUiTextSample))
            {
                fontAsset = null;
                return false;
            }

            cachedKoreanUiFont = createdFontAsset;
            fontAsset = cachedKoreanUiFont;
            return true;
        }

        internal static bool TryGetLegacyFont(out Font font)
        {
            if (cachedKoreanLegacyUiFont != null)
            {
                font = cachedKoreanLegacyUiFont;
                return true;
            }

            Font osFont = Font.CreateDynamicFontFromOSFont(KoreanUiFontNames, 32);
            if (osFont == null)
            {
                font = null;
                return false;
            }

            cachedKoreanLegacyUiFont = osFont;
            font = cachedKoreanLegacyUiFont;
            return true;
        }

        internal static bool SupportsText(TMP_FontAsset fontAsset, string text)
        {
            if (fontAsset == null)
            {
                return false;
            }

            foreach (char character in text)
            {
                if (IsKorean(character) && !fontAsset.HasCharacter(character, true, true))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool ContainsKorean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (char character in text)
            {
                if (IsKorean(character))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsKorean(char character)
        {
            return (character >= '\uAC00' && character <= '\uD7A3') ||
                   (character >= '\u3130' && character <= '\u318F') ||
                   (character >= '\u1100' && character <= '\u11FF');
        }
    }
}
