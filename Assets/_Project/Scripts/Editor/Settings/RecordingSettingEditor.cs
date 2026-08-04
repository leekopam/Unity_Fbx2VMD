using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Settings
{
    [CustomEditor(typeof(RecordingSetting))]
    [CanEditMultipleObjects]
    public sealed class RecordingSettingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorDrawUtility.DrawSection("수동 녹화");
            EditorDrawUtility.DrawProperty(serializedObject, "recordingFBXVmdPipeline", "녹화 FBXVmdPipeline");
            EditorDrawUtility.DrawProperty(serializedObject, "manualRecordButton", "수동 녹화 버튼");
            EditorDrawUtility.DrawProperty(serializedObject, "recordingController", "녹화 대상");

            EditorGUILayout.Space(6f);
            EditorDrawUtility.DrawSection("화면 녹화 진단");
            EditorDrawUtility.DrawProperty(serializedObject, "enableRecordingDiagnostics", "녹화 진단/캡처 사용");
            EditorDrawUtility.DrawProperty(serializedObject, "useDeterministicCaptureFramerateForDiagnostics", "테스트용 30fps 시간 고정");
            EditorDrawUtility.DrawProperty(serializedObject, "enableDiagnosticFingerCloseups", "손 close-up 캡처");
            EditorDrawUtility.DrawProperty(serializedObject, "recordingCaptureQuality", "녹화 캡처 해상도");
            EditorDrawUtility.DrawProperty(serializedObject, "customRecordingCaptureWidth", "사용자 지정 캡처 폭");
            EditorDrawUtility.DrawProperty(serializedObject, "customRecordingCaptureHeight", "사용자 지정 캡처 높이");
            EditorDrawUtility.DrawProperty(serializedObject, "applyDiagnosticsToFBXVmdPipelineOnAwake", "실행 시작 시 FBXVmdPipeline에 적용");

            EditorGUILayout.Space(6f);
            EditorDrawUtility.DrawSection("설정 팝업");
            EditorDrawUtility.DrawProperty(serializedObject, "settingsPopup", "런타임 설정 팝업");
            EditorDrawUtility.DrawProperty(serializedObject, "openSettingsPopupOnStart", "시작 시 설정 팝업 열기");

            EditorGUILayout.Space(6f);
            EditorDrawUtility.DrawSection("공유 설정 파일");
            EditorDrawUtility.DrawProperty(serializedObject, "loadSharedSettingsOnAwake", "시작 시 공유 설정 로드");
            EditorDrawUtility.DrawProperty(serializedObject, "sharedSettingsFilePathOverride", "공유 설정 파일 override");
            EditorDrawUtility.DrawProperty(serializedObject, "sharedSettingsPollingIntervalSeconds", "공유 설정 polling 간격");

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
                if (targetObject is RecordingSetting recodingSetting)
                {
                    recodingSetting.StartManualRecording();
                }
            }
        }
    }
}
