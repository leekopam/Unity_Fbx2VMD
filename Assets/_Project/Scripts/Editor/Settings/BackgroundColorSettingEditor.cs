using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Settings.EditorTools
{
    [CustomEditor(typeof(BackgroundColorSetting))]
    [CanEditMultipleObjects]
    public sealed class BackgroundColorSettingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorDrawUtility.DrawSection("대상");
            EditorDrawUtility.DrawProperty(serializedObject, "targetCamera", "대상 카메라");

            EditorGUILayout.Space(6f);
            EditorDrawUtility.DrawSection("적용");
            EditorDrawUtility.DrawProperty(serializedObject, "applyOnAwake", "실행 시작 시 자동 적용");
            EditorDrawUtility.DrawProperty(serializedObject, "applyOnValidate", "Unity OnValidate 자동 적용");

            EditorGUILayout.Space(6f);
            EditorDrawUtility.DrawSection("카메라 배경");
            EditorDrawUtility.DrawProperty(serializedObject, "applyBackgroundColor", "배경색 적용");
            EditorDrawUtility.DrawProperty(serializedObject, "backgroundColor", "배경색");

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
    }
}
