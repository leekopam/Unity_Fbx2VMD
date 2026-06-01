using UnityEditor;
using UnityEngine;

namespace Member_Han.Modules.Graphics.EditorTools
{
    [CustomEditor(typeof(RecodingSetting))]
    [CanEditMultipleObjects]
    public sealed class RecodingSettingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSection("수동 녹화");
            DrawProperty("recordingFileManager", "녹화 FileManager");
            DrawProperty("manualRecordButton", "수동 녹화 버튼");
            DrawProperty("recordingController", "녹화 대상");

            EditorGUILayout.Space(6f);
            DrawSection("화면 녹화 진단");
            DrawProperty("enableRecordingDiagnostics", "녹화 진단/캡처 사용");
            DrawProperty("useDeterministicCaptureFramerateForDiagnostics", "테스트용 30fps 시간 고정");
            DrawProperty("enableDiagnosticFingerCloseups", "손 close-up 캡처");
            DrawProperty("applyDiagnosticsToFileManagerOnAwake", "실행 시작 시 FileManager에 적용");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("녹화 시작", GUILayout.Height(28f)))
                {
                    StartRecordingFromInspector();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("녹화 시작은 Play Mode에서 사용할 수 있습니다.", MessageType.Info);
            }
        }

        private void StartRecordingFromInspector()
        {
            foreach (Object targetObject in targets)
            {
                if (targetObject is RecodingSetting recodingSetting)
                {
                    recodingSetting.StartManualRecording();
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
