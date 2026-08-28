using System;
using System.Collections.Generic;
using System.Reflection;
using Fbx2Vmd.Settings;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tests.Editor.Settings
{
    internal static class MainRecordingSettingsPopupTestInspector
    {
        private const string KoreanUiTextFallbackTypeName =
            "Fbx2Vmd.Settings.KoreanUiTextFallback, Assembly-CSharp";

        internal static int CountCardButtons(MainRecordingSettingsPopup popup)
        {
            EnsureBuilt(popup);
            FieldInfo field = typeof(MainRecordingSettingsPopup).GetField(
                "cardButtons",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return ((List<Button>)field.GetValue(popup)).Count;
        }

        internal static void OffsetAnchoredPosition(
            MainRecordingSettingsPopup popup,
            Vector2 delta)
        {
            EnsureBuilt(popup);
            RectTransform rectTransform = popup.GetComponent<RectTransform>();
            Assert.That(rectTransform, Is.Not.Null);
            rectTransform.anchoredPosition += delta;
        }

        internal static void ApplyDrag(MainRecordingSettingsPopup popup, Vector2 delta)
        {
            EventSystem eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();
            GameObject ownedEventSystemObject = null;
            if (eventSystem == null)
            {
                ownedEventSystemObject = new GameObject("Runtime Popup Drag EventSystem");
                eventSystem = ownedEventSystemObject.AddComponent<EventSystem>();
            }

            try
            {
                var beginEvent = new PointerEventData(eventSystem)
                {
                    position = new Vector2(32f, 48f),
                };
                popup.OnBeginDrag(beginEvent);

                var dragEvent = new PointerEventData(eventSystem)
                {
                    position = beginEvent.position + delta,
                };
                popup.OnDrag(dragEvent);
            }
            finally
            {
                if (ownedEventSystemObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedEventSystemObject);
                }
            }
        }

        internal static bool HasReadableKoreanText(MainRecordingSettingsPopup popup)
        {
            EnsureBuilt(popup);
            Type fallbackType = Type.GetType(KoreanUiTextFallbackTypeName);
            Assert.That(fallbackType, Is.Not.Null, KoreanUiTextFallbackTypeName);
            MethodInfo isReadableMethod = fallbackType.GetMethod(
                "IsReadable",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(isReadableMethod, Is.Not.Null);

            TextMeshProUGUI[] labels = popup.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI label in labels)
            {
                if (!(bool)isReadableMethod.Invoke(null, new object[] { label }))
                {
                    return false;
                }
            }

            return true;
        }

        internal static string[] GetVisibleText(MainRecordingSettingsPopup popup)
        {
            EnsureBuilt(popup);
            var texts = new List<string>();
            TextMeshProUGUI[] textMeshLabels =
                popup.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI label in textMeshLabels)
            {
                texts.Add(label.text);
            }

            Text[] legacyLabels = popup.GetComponentsInChildren<Text>(true);
            foreach (Text label in legacyLabels)
            {
                texts.Add(label.text);
            }

            return texts.ToArray();
        }

        private static void EnsureBuilt(MainRecordingSettingsPopup popup)
        {
            MethodInfo ensureBuiltMethod = typeof(MainRecordingSettingsPopup).GetMethod(
                "EnsureBuilt",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(ensureBuiltMethod, Is.Not.Null);
            ensureBuiltMethod.Invoke(popup, null);
        }
    }
}
