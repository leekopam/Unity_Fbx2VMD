using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings
{
    internal static class KoreanUiTextFallback
    {
        private const string FallbackObjectName = "KoreanTextFallback";
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
        private static bool warnedMissingKoreanUiFont;

        internal static void Apply(TextMeshProUGUI label)
        {
            if (label == null)
            {
                return;
            }

            if (!ContainsKorean(label.text) || FontAssetSupportsText(label.font, label.text))
            {
                RestoreTmpLabel(label);
                return;
            }

            if (TryEnableLegacyKoreanText(label))
            {
                return;
            }

            TMP_FontAsset koreanFont = GetOrCreateKoreanUiFont();
            if (koreanFont == null || !FontAssetSupportsText(koreanFont, label.text))
            {
                if (!warnedMissingKoreanUiFont)
                {
                    warnedMissingKoreanUiFont = true;
                    Debug.LogWarning("[KoreanUiTextFallback] 한글 UI 폰트를 찾지 못했습니다. OS 한글 폰트 설치 상태를 확인하세요.");
                }

                return;
            }

            RestoreTmpLabel(label);
            label.font = koreanFont;
            label.SetAllDirty();
        }

        internal static bool IsReadable(TextMeshProUGUI label)
        {
            if (label == null)
            {
                return true;
            }

            if (!ContainsKorean(label.text) || FontAssetSupportsText(label.font, label.text))
            {
                return label.enabled;
            }

            Transform fallback = label.transform.Find(FallbackObjectName);
            Text fallbackText = fallback != null ? fallback.GetComponent<Text>() : null;
            return !label.enabled && fallbackText != null && fallbackText.enabled && fallbackText.font != null;
        }

        private static void RestoreTmpLabel(TextMeshProUGUI label)
        {
            label.enabled = true;

            Transform fallback = label.transform.Find(FallbackObjectName);
            Text fallbackText = fallback != null ? fallback.GetComponent<Text>() : null;
            if (fallbackText != null)
            {
                fallbackText.enabled = false;
            }
        }

        private static bool TryEnableLegacyKoreanText(TextMeshProUGUI label)
        {
            Font legacyFont = GetOrCreateKoreanLegacyUiFont();
            if (legacyFont == null)
            {
                return false;
            }

            Transform existing = label.transform.Find(FallbackObjectName);
            GameObject fallbackObject = existing != null
                ? existing.gameObject
                : new GameObject(FallbackObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            fallbackObject.transform.SetParent(label.transform, false);

            RectTransform fallbackRect = fallbackObject.GetComponent<RectTransform>();
            fallbackRect.anchorMin = Vector2.zero;
            fallbackRect.anchorMax = Vector2.one;
            fallbackRect.offsetMin = Vector2.zero;
            fallbackRect.offsetMax = Vector2.zero;
            fallbackRect.localScale = Vector3.one;

            Text fallbackText = fallbackObject.GetComponent<Text>();
            fallbackText.text = label.text;
            fallbackText.font = legacyFont;
            fallbackText.fontSize = Mathf.Max(1, Mathf.RoundToInt(label.fontSize));
            fallbackText.fontStyle = (label.fontStyle & FontStyles.Bold) == FontStyles.Bold
                ? FontStyle.Bold
                : FontStyle.Normal;
            fallbackText.alignment = ConvertToLegacyAlignment(label.alignment);
            fallbackText.color = label.color;
            fallbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
            fallbackText.verticalOverflow = VerticalWrapMode.Truncate;
            fallbackText.raycastTarget = false;
            fallbackText.supportRichText = true;
            fallbackText.enabled = true;

            label.enabled = false;
            return true;
        }

        private static TextAnchor ConvertToLegacyAlignment(TextAlignmentOptions alignment)
        {
            switch (alignment)
            {
                case TextAlignmentOptions.Center:
                    return TextAnchor.MiddleCenter;
                case TextAlignmentOptions.MidlineRight:
                    return TextAnchor.MiddleRight;
                case TextAlignmentOptions.TopLeft:
                    return TextAnchor.UpperLeft;
                case TextAlignmentOptions.MidlineLeft:
                default:
                    return TextAnchor.MiddleLeft;
            }
        }

        private static TMP_FontAsset GetOrCreateKoreanUiFont()
        {
            if (cachedKoreanUiFont != null)
            {
                return cachedKoreanUiFont;
            }

            Font osFont = Font.CreateDynamicFontFromOSFont(KoreanUiFontNames, 32);
            if (osFont == null)
            {
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(osFont);
            if (fontAsset == null)
            {
                return null;
            }

            fontAsset.name = "Main Recording Runtime Korean UI Font";
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.TryAddCharacters(KoreanUiTextSample, out _);
            if (!FontAssetSupportsText(fontAsset, KoreanUiTextSample))
            {
                return null;
            }

            cachedKoreanUiFont = fontAsset;
            return cachedKoreanUiFont;
        }

        private static Font GetOrCreateKoreanLegacyUiFont()
        {
            if (cachedKoreanLegacyUiFont != null)
            {
                return cachedKoreanLegacyUiFont;
            }

            Font osFont = Font.CreateDynamicFontFromOSFont(KoreanUiFontNames, 32);
            if (osFont != null)
            {
                cachedKoreanLegacyUiFont = osFont;
            }

            return cachedKoreanLegacyUiFont;
        }

        private static bool FontAssetSupportsText(TMP_FontAsset fontAsset, string text)
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

        private static bool ContainsKorean(string text)
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
