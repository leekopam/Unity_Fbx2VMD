using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings
{
    internal sealed class MainRecordingSettingsElementBuilder
    {
        private readonly int layer;

        internal MainRecordingSettingsElementBuilder(int layer)
        {
            this.layer = layer;
        }

        internal Button CreateButton(
            string name,
            Transform parent,
            string label,
            Rect rect,
            bool interactable)
        {
            RectTransform buttonRect = CreateRectTransform(name, parent, rect);
            Image image = buttonRect.gameObject.AddComponent<Image>();
            image.color = interactable
                ? MainRecordingSettingsLayoutSpec.ButtonColor
                : MainRecordingSettingsLayoutSpec.DisabledButtonColor;
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;

            Color textColor = interactable
                ? new Color32(36, 39, 44, 255)
                : MainRecordingSettingsLayoutSpec.DisabledButtonTextColor;
            CreateText(
                name + " Text",
                buttonRect,
                label,
                16,
                textColor,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Rect(0f, 0f, rect.width, rect.height));
            return button;
        }

        internal Image CreateImage(string name, Transform parent, Rect rect, Color color)
        {
            RectTransform imageRect = CreateRectTransform(name, parent, rect);
            Image image = imageRect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        internal TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string text,
            int fontSize,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment,
            Rect rect)
        {
            RectTransform textRect = CreateRectTransform(name, parent, rect);
            TextMeshProUGUI label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            KoreanUiTextFallback.Apply(label);
            return label;
        }

        internal RectTransform CreateRectTransform(string name, Transform parent, Rect rect)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = layer;
            RectTransform transform = child.GetComponent<RectTransform>();
            transform.SetParent(parent, false);
            transform.anchorMin = new Vector2(0f, 1f);
            transform.anchorMax = new Vector2(0f, 1f);
            transform.pivot = new Vector2(0f, 1f);
            transform.anchoredPosition = new Vector2(rect.x, -rect.y);
            transform.sizeDelta = new Vector2(rect.width, rect.height);
            transform.localScale = Vector3.one;
            return transform;
        }
    }
}
