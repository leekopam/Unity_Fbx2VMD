using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings
{
    internal static class KoreanUiTextFallback
    {
        private const string FallbackObjectName = "KoreanTextFallback";
        private static bool warnedMissingKoreanUiFont;

        internal static void Apply(TextMeshProUGUI label)
        {
            if (label == null)
            {
                return;
            }

            if (!KoreanUiFontResolver.ContainsKorean(label.text) ||
                KoreanUiFontResolver.SupportsText(label.font, label.text))
            {
                RestoreTmpLabel(label);
                return;
            }

            if (TryEnableLegacyKoreanText(label))
            {
                return;
            }

            if (!KoreanUiFontResolver.TryGetTmpFont(out TMP_FontAsset koreanFont) ||
                !KoreanUiFontResolver.SupportsText(koreanFont, label.text))
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

            if (!KoreanUiFontResolver.ContainsKorean(label.text) ||
                KoreanUiFontResolver.SupportsText(label.font, label.text))
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
            if (!KoreanUiFontResolver.TryGetLegacyFont(out Font legacyFont))
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
            fallbackText.resizeTextForBestFit = label.enableAutoSizing;
            fallbackText.resizeTextMinSize = Mathf.Max(
                1,
                Mathf.RoundToInt(label.fontSizeMin));
            fallbackText.resizeTextMaxSize = Mathf.Max(
                fallbackText.resizeTextMinSize,
                Mathf.RoundToInt(label.fontSizeMax));
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

    }
}
