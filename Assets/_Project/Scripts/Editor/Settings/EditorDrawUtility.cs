using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Settings.EditorTools
{
    // NotThreadSafe: Unity Editor GUI는 메인 스레드에서만 호출해야 함.
    /// <summary>
    /// Inspector SerializedProperty 표시를 통일함.
    /// </summary>
    public static class EditorDrawUtility
    {
        /// <summary>
        /// SerializedProperty를 찾아 Inspector에 표시함.
        /// </summary>
        public static void DrawProperty(
            SerializedObject serializedObject,
            string propertyName,
            string label,
            bool showWarningOnNull = false)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                if (showWarningOnNull)
                {
                    EditorGUILayout.HelpBox(
                        $"Inspector 필드를 찾을 수 없습니다: {propertyName}",
                        MessageType.Warning);
                }

                return;
            }

            EditorGUILayout.PropertyField(
                property,
                new GUIContent(label, property.tooltip),
                true);
        }

        /// <summary>
        /// Boolean SerializedProperty 값을 읽음.
        /// </summary>
        /// <returns>속성이 Boolean이고 true이면 true, 그 외에는 false.</returns>
        public static bool GetBool(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null
                   && property.propertyType == SerializedPropertyType.Boolean
                   && property.boolValue;
        }

        /// <summary>
        /// 굵은 라벨로 섹션 헤더를 표시함.
        /// </summary>
        public static void DrawSection(string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }
    }
}
