using UnityEditor;
using UnityEngine;

namespace Member_Han.Modules.Graphics.EditorTools
{
    [CustomEditor(typeof(BackgroundColorSetting))]
    [CanEditMultipleObjects]
    public sealed class BackgroundColorSettingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSection("대상");
            DrawProperty("targetCamera", "대상 카메라");

            EditorGUILayout.Space(6f);
            DrawSection("적용");
            DrawProperty("applyOnAwake", "실행 시작 시 자동 적용");
            DrawProperty("applyOnValidate", "Unity OnValidate 자동 적용");

            EditorGUILayout.Space(6f);
            DrawSection("카메라 배경");
            DrawProperty("applyBackgroundColor", "배경색 적용");
            DrawProperty("backgroundColor", "배경색");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("배경색 적용", GUILayout.Height(28f)))
            {
                ApplyBackgroundColor();
            }
        }

        private void ApplyBackgroundColor()
        {
            foreach (Object targetObject in targets)
            {
                if (targetObject is BackgroundColorSetting backgroundColorSetting)
                {
                    backgroundColorSetting.ApplyNow();
                    EditorUtility.SetDirty(backgroundColorSetting);
                }
            }
        }

        private void DrawProperty(string propertyName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
        }

        private static void DrawSection(string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }
    }
}
