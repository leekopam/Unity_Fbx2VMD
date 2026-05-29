using Member_Han.Modules.Graphics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Member_Han.Modules.Graphics.EditorTools
{
    public readonly struct MainRecordingSettingsWindowContext
    {
        public MainRecordingSettingsWindowContext(
            GameObject settingRoot,
            GraphicSetting graphicSetting,
            BackgroundColorSetting backgroundColorSetting,
            RecodingSetting recodingSetting,
            Camera targetCamera)
        {
            SettingRoot = settingRoot;
            GraphicSetting = graphicSetting;
            BackgroundColorSetting = backgroundColorSetting;
            RecodingSetting = recodingSetting;
            TargetCamera = targetCamera;
        }

        public GameObject SettingRoot { get; }
        public GraphicSetting GraphicSetting { get; }
        public BackgroundColorSetting BackgroundColorSetting { get; }
        public RecodingSetting RecodingSetting { get; }
        public Camera TargetCamera { get; }
        public bool IsComplete => SettingRoot != null &&
                                  GraphicSetting != null &&
                                  BackgroundColorSetting != null &&
                                  RecodingSetting != null &&
                                  TargetCamera != null;
    }

    public sealed class MainRecordingSettingsWindow : EditorWindow
    {
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_recoding.unity";
        private const string WindowTitle = "Main_recording 설정";
        private const float LabelWidth = 150f;

        private Vector2 scroll;

        [MenuItem("Tools/Graphics/Open Main_recording Settings")]
        public static MainRecordingSettingsWindow OpenForMainRecordingScene()
        {
            var window = GetWindow<MainRecordingSettingsWindow>(true, WindowTitle, true);
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(360f, 520f);
            window.Show();
            return window;
        }

        public static string GetWindowTitle()
        {
            return WindowTitle;
        }

        public static bool ShouldOpenForScene(string scenePath)
        {
            return string.Equals(NormalizeScenePath(scenePath), MainRecordingScenePath, System.StringComparison.OrdinalIgnoreCase);
        }

        public static MainRecordingSettingsWindowContext ResolveContext()
        {
            GameObject root = GameObject.Find("Setting");
            GraphicSetting graphicSetting = root != null ? root.GetComponent<GraphicSetting>() : Object.FindObjectOfType<GraphicSetting>();
            if (root == null && graphicSetting != null)
            {
                root = graphicSetting.gameObject;
            }

            BackgroundColorSetting backgroundColorSetting =
                root != null ? root.GetComponent<BackgroundColorSetting>() : Object.FindObjectOfType<BackgroundColorSetting>();
            RecodingSetting recodingSetting =
                root != null ? root.GetComponent<RecodingSetting>() : Object.FindObjectOfType<RecodingSetting>();
            Camera targetCamera = backgroundColorSetting != null && backgroundColorSetting.TargetCamera != null
                ? backgroundColorSetting.TargetCamera
                : Camera.main;

            return new MainRecordingSettingsWindowContext(
                root,
                graphicSetting,
                backgroundColorSetting,
                recodingSetting,
                targetCamera);
        }

        private static string NormalizeScenePath(string scenePath)
        {
            return string.IsNullOrWhiteSpace(scenePath)
                ? string.Empty
                : scenePath.Replace('\\', '/').Trim();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
        }

        private void OnGUI()
        {
            EditorGUIUtility.labelWidth = LabelWidth;
            DrawHeader();

            Scene activeScene = SceneManager.GetActiveScene();
            if (!ShouldOpenForScene(activeScene.path))
            {
                EditorGUILayout.HelpBox(
                    "이 설정창은 Main_recoding 씬에서만 자동으로 동작합니다.",
                    MessageType.Warning);
                if (GUILayout.Button("Main_recoding 씬 열기"))
                {
                    EditorSceneManager.OpenScene(MainRecordingScenePath);
                }

                return;
            }

            MainRecordingSettingsWindowContext context = ResolveContext();
            if (!context.IsComplete)
            {
                EditorGUILayout.HelpBox(
                    "Setting 오브젝트에 필요한 설정 컴포넌트가 부족합니다.",
                    MessageType.Error);
                if (GUILayout.Button("설정 오브젝트 보강"))
                {
                    GraphicSettingSceneInstaller.EnsureInActiveScene();
                    Repaint();
                }

                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawRecordingSection(context.RecodingSetting);
            DrawBackgroundSection(context.BackgroundColorSetting);
            DrawGraphicSection(context.GraphicSetting);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeader()
        {
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                Application.isPlaying ? "Play Mode" : "Edit Mode",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(6f);
        }

        private static void DrawRecordingSection(RecodingSetting setting)
        {
            DrawSectionTitle("녹화 설정");
            var serialized = new SerializedObject(setting);
            serialized.Update();
            DrawProperty(serialized, "recordingFileManager", "녹화 FileManager");
            DrawProperty(serialized, "manualRecordButton", "수동 녹화 버튼");
            DrawProperty(serialized, "recordingController", "녹화 대상");
            DrawProperty(serialized, "enableRecordingDiagnostics", "녹화 진단/캡처 사용");
            DrawProperty(serialized, "useDeterministicCaptureFramerateForDiagnostics", "테스트용 30fps 시간 고정");
            DrawProperty(serialized, "enableDiagnosticFingerCloseups", "손 close-up 캡처");
            bool changed = serialized.ApplyModifiedProperties();

            if (changed)
            {
                setting.ApplyDiagnosticsToFileManager();
                EditorUtility.SetDirty(setting);
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("녹화 시작"))
                {
                    setting.StartManualRecording();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("녹화 시작은 Play Mode에서 사용할 수 있습니다.", MessageType.Info);
            }
        }

        private static void DrawBackgroundSection(BackgroundColorSetting setting)
        {
            DrawSectionTitle("배경색 설정");
            var serialized = new SerializedObject(setting);
            serialized.Update();
            DrawProperty(serialized, "targetCamera", "대상 카메라");
            DrawProperty(serialized, "applyOnAwake", "실행 시작 시 자동 적용");
            DrawProperty(serialized, "applyOnValidate", "Unity OnValidate 자동 적용");
            DrawProperty(serialized, "applyBackgroundColor", "배경색 적용");
            DrawProperty(serialized, "backgroundColor", "배경색");
            bool changed = serialized.ApplyModifiedProperties();

            if (changed)
            {
                setting.ApplyNow();
                EditorUtility.SetDirty(setting);
            }

            if (GUILayout.Button("배경색 적용"))
            {
                setting.ApplyNow();
                EditorUtility.SetDirty(setting);
            }
        }

        private static void DrawGraphicSection(GraphicSetting setting)
        {
            DrawSectionTitle("GameView 품질");
            var serialized = new SerializedObject(setting);
            serialized.Update();
            DrawProperty(serialized, "textureResolution", "텍스처 Import 기준");
            DrawProperty(serialized, "antiAliasingPreset", "GameView 안티앨리어싱");
            DrawProperty(serialized, "renderSharpness", "렌더 스케일 기준");
            DrawProperty(serialized, "modelEdgeAndAlpha", "모델 윤곽선/알파 기준");
            DrawProperty(serialized, "gameViewScaleMode", "GameView 확대 표시");
            bool changed = serialized.ApplyModifiedProperties();

            if (changed)
            {
                setting.ApplyNow();
                EditorUtility.SetDirty(setting);
            }
        }

        private static void DrawSectionTitle(string label)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        private static void DrawProperty(SerializedObject serialized, string propertyName, string label)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
        }
    }

    [InitializeOnLoad]
    public static class MainRecordingSettingsWindowPlayModeHook
    {
        static MainRecordingSettingsWindowPlayModeHook()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            string scenePath = SceneManager.GetActiveScene().path;
            if (!MainRecordingSettingsWindow.ShouldOpenForScene(scenePath))
            {
                return;
            }

            EditorApplication.delayCall += () => MainRecordingSettingsWindow.OpenForMainRecordingScene();
        }
    }
}
