using System;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Settings.EditorTools
{
    public sealed class RecordingPreviewWindow : EditorWindow
    {
        public const string MenuPath = "Tools/Graphics/Open Recording Preview";
        public const int DefaultPreviewWidth = 1920;
        public const int DefaultPreviewHeight = 1080;
        public const float DefaultMaxFps = 10f;

        private const string WindowTitle = "Recording Preview";
        private const int MinPreviewFps = 1;
        private const int MaxPreviewFps = 10;

        private Camera sourceCamera;
        private GameObject previewCameraObject;
        private Camera previewCamera;
        private RenderTexture previewTexture;
        private RecordingCaptureQualityPreset previewQuality = RecordingCaptureQualityPreset.FullHd;
        private int customPreviewWidth = DefaultPreviewWidth;
        private int customPreviewHeight = DefaultPreviewHeight;
        private float maxFps = DefaultMaxFps;
        private bool autoRefresh = true;
        private double lastRenderTime;
        private string statusMessage = "No preview rendered yet.";

        [MenuItem(MenuPath)]
        public static RecordingPreviewWindow OpenForMainCamera()
        {
            var window = GetWindow<RecordingPreviewWindow>(false, WindowTitle, true);
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(560f, 420f);
            window.Show();
            window.RefreshNow();
            return window;
        }

        public static string GetWindowTitle()
        {
            return WindowTitle;
        }

        public static RecordingCaptureResolutionPlan CreateDefaultPreviewPlan()
        {
            return RecordingCaptureResolution.CreatePlan(RecordingCaptureQualityPreset.FullHd);
        }

        public static RecordingCaptureResolutionPlan CreateManualUhdPreviewPlan()
        {
            return RecordingCaptureResolution.CreatePlan(RecordingCaptureQualityPreset.Uhd4K);
        }

        public static float GetMinimumRenderIntervalSeconds(float framesPerSecond)
        {
            float clampedFps = Mathf.Clamp(framesPerSecond, MinPreviewFps, MaxPreviewFps);
            return 1f / clampedFps;
        }

        public static bool ShouldRender(double nowSeconds, double lastRenderSeconds, float framesPerSecond)
        {
            if (lastRenderSeconds <= 0d)
            {
                return true;
            }

            return nowSeconds - lastRenderSeconds >= GetMinimumRenderIntervalSeconds(framesPerSecond);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(560f, 420f);
            EditorApplication.update -= HandleEditorUpdate;
            EditorApplication.update += HandleEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
            ReleasePreviewResources();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawPreview();
        }

        private void HandleEditorUpdate()
        {
            if (!autoRefresh)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (!ShouldRender(now, lastRenderTime, maxFps))
            {
                return;
            }

            if (RenderPreview(false))
            {
                Repaint();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                sourceCamera = (Camera)EditorGUILayout.ObjectField(
                    sourceCamera != null ? sourceCamera : ResolveSourceCamera(),
                    typeof(Camera),
                    true,
                    GUILayout.MinWidth(160f));

                previewQuality = (RecordingCaptureQualityPreset)EditorGUILayout.EnumPopup(
                    previewQuality,
                    GUILayout.Width(126f));

                using (new EditorGUI.DisabledScope(previewQuality != RecordingCaptureQualityPreset.Custom))
                {
                    customPreviewWidth = EditorGUILayout.IntField(customPreviewWidth, GUILayout.Width(60f));
                    customPreviewHeight = EditorGUILayout.IntField(customPreviewHeight, GUILayout.Width(60f));
                }

                maxFps = EditorGUILayout.Slider(maxFps, MinPreviewFps, MaxPreviewFps, GUILayout.Width(132f));
                autoRefresh = GUILayout.Toggle(autoRefresh, "Auto", EditorStyles.toolbarButton, GUILayout.Width(48f));

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    RefreshNow();
                }

                if (GUILayout.Button("4K Refresh", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                {
                    RefreshUhd4KNow();
                }
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);

            Rect rect = GUILayoutUtility.GetAspectRect(16f / 9f, GUILayout.ExpandWidth(true));
            if (previewTexture == null)
            {
                EditorGUI.DrawRect(rect, new Color32(30, 34, 38, 255));
                GUI.Label(rect, "Preview is empty.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit, false);
        }

        private void RefreshNow()
        {
            RenderPreview(true);
            Repaint();
        }

        private void RefreshUhd4KNow()
        {
            previewQuality = RecordingCaptureQualityPreset.Uhd4K;
            RenderPreview(true);
            Repaint();
        }

        private int GetPreviewTextureWidthForTests()
        {
            return previewTexture != null ? previewTexture.width : 0;
        }

        private int GetPreviewTextureHeightForTests()
        {
            return previewTexture != null ? previewTexture.height : 0;
        }

        private int CountPreviewSamplesDifferentFromCornerForTests(int columns, int rows)
        {
            if (previewTexture == null || !previewTexture.IsCreated())
            {
                return 0;
            }

            int clampedColumns = Mathf.Clamp(columns, 1, 128);
            int clampedRows = Mathf.Clamp(rows, 1, 128);
            RenderTexture previousActive = RenderTexture.active;
            var texture = new Texture2D(previewTexture.width, previewTexture.height, TextureFormat.RGBA32, false);

            try
            {
                RenderTexture.active = previewTexture;
                texture.ReadPixels(new Rect(0, 0, previewTexture.width, previewTexture.height), 0, 0, false);
                texture.Apply(false, false);

                Color32 corner = texture.GetPixel(0, 0);
                int differentSamples = 0;
                for (int row = 0; row < clampedRows; row++)
                {
                    int y = Mathf.Clamp(
                        Mathf.RoundToInt(((row + 0.5f) / clampedRows) * (texture.height - 1)),
                        0,
                        texture.height - 1);
                    for (int column = 0; column < clampedColumns; column++)
                    {
                        int x = Mathf.Clamp(
                            Mathf.RoundToInt(((column + 0.5f) / clampedColumns) * (texture.width - 1)),
                            0,
                            texture.width - 1);
                        Color32 sample = texture.GetPixel(x, y);
                        int delta =
                            Mathf.Abs(sample.r - corner.r) +
                            Mathf.Abs(sample.g - corner.g) +
                            Mathf.Abs(sample.b - corner.b) +
                            Mathf.Abs(sample.a - corner.a);
                        if (delta > 24)
                        {
                            differentSamples++;
                        }
                    }
                }

                return differentSamples;
            }
            finally
            {
                RenderTexture.active = previousActive;
                DestroyImmediate(texture);
            }
        }

        private bool RenderPreview(bool force)
        {
            double now = EditorApplication.timeSinceStartup;
            if (!force && !ShouldRender(now, lastRenderTime, maxFps))
            {
                return false;
            }

            Camera camera = sourceCamera != null ? sourceCamera : ResolveSourceCamera();
            if (camera == null)
            {
                statusMessage = "No camera found for recording preview.";
                return false;
            }

            RecordingCaptureResolutionPlan plan = CreateCurrentPlan();
            EnsurePreviewTexture(plan);
            EnsurePreviewCamera();

            try
            {
                previewCamera.CopyFrom(camera);
                previewCamera.enabled = false;
                previewCamera.targetTexture = previewTexture;
                previewCamera.Render();
                lastRenderTime = now;
                statusMessage = $"{camera.name}: {plan.Width}x{plan.Height}, max {Mathf.RoundToInt(maxFps)}fps";
                return true;
            }
            catch (Exception ex)
            {
                statusMessage = $"Preview render failed: {ex.Message}";
                Debug.LogWarning($"[RecordingPreviewWindow] {statusMessage}");
                return false;
            }
        }

        private RecordingCaptureResolutionPlan CreateCurrentPlan()
        {
            if (previewQuality == RecordingCaptureQualityPreset.Custom)
            {
                return RecordingCaptureResolution.CreateCustomPlan(customPreviewWidth, customPreviewHeight);
            }

            return RecordingCaptureResolution.CreatePlan(previewQuality);
        }

        private static Camera ResolveSourceCamera()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                return camera;
            }

            return null;
        }

        private void EnsurePreviewCamera()
        {
            if (previewCamera != null)
            {
                return;
            }

            previewCameraObject = new GameObject("Recording Preview Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            previewCamera = previewCameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.hideFlags = HideFlags.HideAndDontSave;
        }

        private void EnsurePreviewTexture(RecordingCaptureResolutionPlan plan)
        {
            if (previewTexture != null &&
                previewTexture.width == plan.Width &&
                previewTexture.height == plan.Height)
            {
                return;
            }

            ReleasePreviewTexture();
            previewTexture = new RenderTexture(plan.Width, plan.Height, 24, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = $"RecordingPreview_{plan.Width}x{plan.Height}",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            previewTexture.Create();
        }

        private void ReleasePreviewResources()
        {
            ReleasePreviewTexture();
            if (previewCameraObject != null)
            {
                DestroyImmediate(previewCameraObject);
                previewCameraObject = null;
                previewCamera = null;
            }
        }

        private void ReleasePreviewTexture()
        {
            if (previewTexture == null)
            {
                return;
            }

            previewTexture.Release();
            DestroyImmediate(previewTexture);
            previewTexture = null;
        }
    }
}
