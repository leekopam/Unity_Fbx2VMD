using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Settings.EditorTools
{
    internal sealed class MainRecordingEditorPlayModeTintController
    {
        private static readonly Color NeutralPlayModeTint = Color.white;
        private bool hasSavedPlayModeTint;
        private Color savedPlayModeTint;

        internal void ApplyNeutralTint()
        {
            if (hasSavedPlayModeTint || !TryGetPlayModeTint(out Color playModeTint))
            {
                return;
            }

            savedPlayModeTint = playModeTint;
            hasSavedPlayModeTint = true;
            TrySetPlayModeTint(NeutralPlayModeTint);
        }

        internal void RestoreTint()
        {
            if (!hasSavedPlayModeTint)
            {
                return;
            }

            TrySetPlayModeTint(savedPlayModeTint);
            hasSavedPlayModeTint = false;
        }

        private static bool CanReflectPlayModeTintForTests()
        {
            return TryGetPlayModeTint(out _);
        }

        private static bool IsNeutralPlayModeTintForTests(Color color)
        {
            return Mathf.Abs(color.r - 1f) <= 0.0001f &&
                   Mathf.Abs(color.g - 1f) <= 0.0001f &&
                   Mathf.Abs(color.b - 1f) <= 0.0001f &&
                   Mathf.Abs(color.a - 1f) <= 0.0001f;
        }

        private static Color GetCurrentPlayModeTintForTests()
        {
            return TryGetPlayModeTint(out Color color) ? color : Color.clear;
        }

        private static Color ApplyPlayModeTintForTests(Color color)
        {
            if (!TryGetPlayModeTint(out Color current))
            {
                return Color.clear;
            }

            TrySetPlayModeTint(color);
            return current;
        }

        private static bool TryGetPlayModeTint(out Color color)
        {
            color = Color.clear;
            object prefColor = GetPlayModeTintPrefColor();
            PropertyInfo property = GetPrefColorProperty(prefColor);
            if (prefColor == null || property == null || property.PropertyType != typeof(Color))
            {
                return false;
            }

            color = (Color)property.GetValue(prefColor);
            return true;
        }

        private static bool TrySetPlayModeTint(Color color)
        {
            object prefColor = GetPlayModeTintPrefColor();
            PropertyInfo property = GetPrefColorProperty(prefColor);
            if (prefColor == null || property == null || property.PropertyType != typeof(Color) || !property.CanWrite)
            {
                return false;
            }

            property.SetValue(prefColor, color);
            return true;
        }

        private static object GetPlayModeTintPrefColor()
        {
            Type hostViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.HostView");
            FieldInfo field = hostViewType?.GetField(
                "kPlayModeDarken",
                BindingFlags.NonPublic | BindingFlags.Static);
            return field?.GetValue(null);
        }

        private static PropertyInfo GetPrefColorProperty(object prefColor)
        {
            return prefColor?.GetType().GetProperty(
                "Color",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }
}
