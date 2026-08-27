using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.Editor.Settings
{
    public static class GraphicSettingTestBatchRunner
    {
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string GraphicSettingTypeName = "Fbx2Vmd.Settings.GraphicSetting, Assembly-CSharp";
        private const string BackgroundColorSettingTypeName = "BackgroundColorSetting, Assembly-CSharp";
        private const string YybRootName = "YYB Hatsune Miku";
        private const float ComparisonCameraViewportHeight = 0.56f;
        private const float ComparisonCameraViewportWidth = 0.82f;
        private const float ComparisonCameraViewportCenterY = 0.28f;
        private const float ComparisonCameraAspect = 16f / 9f;
        private const float ComparisonCameraDepth = 39f;
        private static string pendingCapturePath;
        private static double pendingCaptureStartTime;
        private static int pendingCapturePlayFrames;

        public static void Run()
        {
            string resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-GraphicSetting.xml");
            var results = new List<TestResult>();
            var tests = new GraphicSettingTests();

            RunTest(results, nameof(GraphicSettingTests.Given_InspectorProfile_When_ApplyNow_Then_AppliesCameraAndUrpQualitySettings),
                tests.Given_InspectorProfile_When_ApplyNow_Then_AppliesCameraAndUrpQualitySettings);
            RunTest(results, nameof(GraphicSettingTests.Given_BackgroundApplyDisabled_When_ApplyNow_Then_PreservesCameraBackground),
                tests.Given_BackgroundApplyDisabled_When_ApplyNow_Then_PreservesCameraBackground);
            RunTest(results, nameof(GraphicSettingTests.Given_TextureImportProfile_When_CreatePlan_Then_UsesMmdFriendlyDefaults),
                tests.Given_TextureImportProfile_When_CreatePlan_Then_UsesMmdFriendlyDefaults);
            RunTest(results, nameof(GraphicSettingTests.Given_SimpleTextureQualityPreset_When_CreateTextureImportPlan_Then_UsesHighResolutionPreset),
                tests.Given_SimpleTextureQualityPreset_When_CreateTextureImportPlan_Then_UsesHighResolutionPreset);
            RunTest(results, nameof(GraphicSettingTests.Given_SimpleQualityPresets_When_ApplyNow_Then_MapsReadablePresetsToDetailedSettings),
                tests.Given_SimpleQualityPresets_When_ApplyNow_Then_MapsReadablePresetsToDetailedSettings);
            RunTest(results, nameof(GraphicSettingTests.Given_SimpleModelEdgeQualityPreset_When_CreateMaterialShaderPlan_Then_UsesFineOutlinePreset),
                tests.Given_SimpleModelEdgeQualityPreset_When_CreateMaterialShaderPlan_Then_UsesFineOutlinePreset);
            RunTest(results, nameof(GraphicSettingTests.GraphicSettingInspectorSchema_HasTabbedCategoriesForFocusedInspector),
                tests.GraphicSettingInspectorSchema_HasTabbedCategoriesForFocusedInspector);
            RunTest(results, nameof(GraphicSettingTests.GraphicSettingEditorTypes_AreOwnedByResponsibilityNamedFiles),
                tests.GraphicSettingEditorTypes_AreOwnedByResponsibilityNamedFiles);
            RunTest(results, nameof(GraphicSettingTests.GraphicSettingRuntimeTypes_AreOwnedByTypeNamedFiles),
                tests.GraphicSettingRuntimeTypes_AreOwnedByTypeNamedFiles);
            RunTest(results, nameof(GraphicSettingTests.Given_MaterialShaderProfile_When_AppliedToYybMaterial_Then_AdjustsSupportedOutlineAndReportsSkippedUnsupportedProperties),
                tests.Given_MaterialShaderProfile_When_AppliedToYybMaterial_Then_AdjustsSupportedOutlineAndReportsSkippedUnsupportedProperties);
            RunTest(results, nameof(GraphicSettingTests.Given_MaterialShaderProfile_When_AppliedToCutoutShader_Then_AdjustsCutoffAndSurfaceBlend),
                tests.Given_MaterialShaderProfile_When_AppliedToCutoutShader_Then_AdjustsCutoffAndSurfaceBlend);
            RunTest(results, nameof(GraphicSettingTests.Given_MaterialShaderProfile_When_ShaderLacksProperties_Then_SkipsUnsupportedSettings),
                tests.Given_MaterialShaderProfile_When_ShaderLacksProperties_Then_SkipsUnsupportedSettings);
            RunTest(results, nameof(GraphicSettingTests.Given_BuiltInPipelineProfile_When_ApplyNow_Then_ConfiguresPostProcessLayerAntialiasing),
                tests.Given_BuiltInPipelineProfile_When_ApplyNow_Then_ConfiguresPostProcessLayerAntialiasing);
            RunTest(results, nameof(GraphicSettingTests.MainRecordingScene_HasGraphicSettingOnRootSettingObjectForYybQualityControl),
                tests.MainRecordingScene_HasGraphicSettingOnRootSettingObjectForYybQualityControl);
            RunTest(results, nameof(GraphicSettingTests.MainAutoScene_DoesNotCarryGraphicSettingQualityControls),
                tests.MainAutoScene_DoesNotCarryGraphicSettingQualityControls);
            RunTest(results, nameof(GraphicSettingTests.MainRecordingScene_InstallerEnsuresActualGameViewQualityPath),
                tests.MainRecordingScene_InstallerEnsuresActualGameViewQualityPath);
            RunTest(results, nameof(GraphicSettingTests.MainRecordingScene_ActualGameViewZoomIsReappliedFromSceneSetting),
                tests.MainRecordingScene_ActualGameViewZoomIsReappliedFromSceneSetting);
            RunTest(results, nameof(GraphicSettingTests.MainRecordingScene_ActualGameViewZoomDriftIsReappliedFromOneXSetting),
                tests.MainRecordingScene_ActualGameViewZoomDriftIsReappliedFromOneXSetting);
            RunTest(results, nameof(GraphicSettingTests.MainRecordingScene_UsesOnlyMainCameraForGameViewComparison),
                tests.MainRecordingScene_UsesOnlyMainCameraForGameViewComparison);
            RunTest(results, nameof(GraphicSettingTests.MainRecordingScene_MainCameraUsesReferenceMp4BlackBackground),
                tests.MainRecordingScene_MainCameraUsesReferenceMp4BlackBackground);
            RunTest(results, nameof(GraphicSettingTests.MainRecordingScene_MainCameraFramesYybRendererBoundsForGameView),
                tests.MainRecordingScene_MainCameraFramesYybRendererBoundsForGameView);
            RunTest(results, nameof(GraphicSettingTests.MainRecordingScene_MainCameraMatchesReferenceMp4FullBodyFraming),
                tests.MainRecordingScene_MainCameraMatchesReferenceMp4FullBodyFraming);
            RunTest(results, nameof(GraphicSettingTests.MainRecordingScene_MainCameraRendersYybPixelsForComparisonCapture),
                tests.MainRecordingScene_MainCameraRendersYybPixelsForComparisonCapture);

            WriteResult(resultPath, results);

            int failed = 0;
            foreach (TestResult result in results)
            {
                if (result.Failure != null)
                {
                    failed++;
                }
            }

            UnityEngine.Debug.Log($"GraphicSettingTestBatchRunner wrote {resultPath} failed={failed}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        public static void CaptureMainAutoGameViewEvidence()
        {
            CaptureMainRecordingGameViewEvidence();
        }

        public static void CaptureMainRecordingGameViewEvidence()
        {
            try
            {
                EditorSceneManager.OpenScene(MainRecordingScenePath);
                ApplyMainRecordingGraphicSetting();

                string sessionId = Environment.GetEnvironmentVariable("YYB_VISUAL_QUALITY_SESSION_ID");
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    sessionId = $"when-{DateTime.Now:yyyyMMdd-HHmmss}_where-Main_recoding_who-yyb_what-visual-quality_how-editor-batch";
                }

                string folder = Path.GetFullPath(Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Docs/Workflow/Local/VisualQualitySessions",
                    sessionId));
                Directory.CreateDirectory(folder);

                pendingCapturePath = Path.Combine(folder, "Main_recoding_GameView_3840x2160.png");
                pendingCaptureStartTime = EditorApplication.timeSinceStartup;
                pendingCapturePlayFrames = 0;

                bool canUsePlayModeCapture =
                    !Application.isBatchMode ||
                    SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
                if (!canUsePlayModeCapture)
                {
                    CaptureMainRecordingGameViewEvidenceNow();
                    return;
                }

                EditorApplication.update -= CaptureMainRecordingGameViewEvidenceUpdate;
                EditorApplication.update += CaptureMainRecordingGameViewEvidenceUpdate;

                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.EnterPlaymode();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.update -= CaptureMainRecordingGameViewEvidenceUpdate;
                EditorApplication.Exit(1);
            }
        }

        private static void RunTest(List<TestResult> results, string name, TestDelegate test)
        {
            DateTime started = DateTime.UtcNow;
            try
            {
                test();
                results.Add(new TestResult(name, null, DateTime.UtcNow - started));
            }
            catch (Exception ex)
            {
                Exception failure = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                results.Add(new TestResult(name, failure, DateTime.UtcNow - started));
            }
        }

        private static void ApplyMainRecordingGraphicSetting()
        {
            Type graphicSettingType = Type.GetType(GraphicSettingTypeName);
            if (graphicSettingType == null)
            {
                throw new InvalidOperationException($"{GraphicSettingTypeName} must exist.");
            }

            GameObject settingRoot = GameObject.Find("Setting");
            Component setting = settingRoot != null ? settingRoot.GetComponent(graphicSettingType) : null;
            if (setting == null)
            {
                throw new InvalidOperationException("Main_recoding Setting object must have GraphicSetting.");
            }

            MethodInfo applyNow = graphicSettingType.GetMethod(
                "ApplyNow",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (applyNow == null)
            {
                throw new InvalidOperationException("GraphicSetting.ApplyNow must exist.");
            }

            applyNow.Invoke(setting, null);

            Type backgroundSettingType = Type.GetType(BackgroundColorSettingTypeName);
            Component backgroundSetting = backgroundSettingType != null ? settingRoot.GetComponent(backgroundSettingType) : null;
            MethodInfo applyBackground = backgroundSettingType?.GetMethod(
                "ApplyNow",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (backgroundSetting != null && applyBackground != null)
            {
                applyBackground.Invoke(backgroundSetting, null);
            }
        }

        private static void CaptureMainRecordingGameViewEvidenceUpdate()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - pendingCaptureStartTime > 45.0)
                {
                    throw new TimeoutException("Timed out waiting for Play Mode GameView capture.");
                }

                if (!EditorApplication.isPlaying)
                {
                    return;
                }

                pendingCapturePlayFrames++;
                if (pendingCapturePlayFrames < 45)
                {
                    return;
                }

                ApplyMainRecordingGraphicSetting();
                Camera camera = Camera.main;
                if (camera == null)
                {
                    throw new InvalidOperationException("Main_recoding has no MainCamera-tagged camera.");
                }

                CaptureMainRecordingGameViewEvidenceNow();
                EditorApplication.update -= CaptureMainRecordingGameViewEvidenceUpdate;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.update -= CaptureMainRecordingGameViewEvidenceUpdate;
                EditorApplication.Exit(1);
            }
        }

        private static void CaptureMainRecordingGameViewEvidenceNow()
        {
            ApplyMainRecordingGraphicSetting();
            Camera camera = Camera.main;
            if (camera == null)
            {
                throw new InvalidOperationException("Main_recoding has no MainCamera-tagged camera.");
            }

            FrameCameraForCurrentYyb(camera);
            RenderCameraToPng(camera, pendingCapturePath, 3840, 2160);
            Debug.Log($"GraphicSetting capture wrote {pendingCapturePath}");
            EditorApplication.Exit(0);
        }

        private static void FrameCameraForCurrentYyb(Camera camera)
        {
            GameObject yybRoot = GameObject.Find(YybRootName);
            if (camera == null || yybRoot == null || !TryGetVisibleRendererBounds(yybRoot, out Bounds bounds))
            {
                return;
            }

            Vector3 focus = bounds.center;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(
                bounds.extents.y / ComparisonCameraViewportHeight,
                bounds.extents.x / (ComparisonCameraAspect * ComparisonCameraViewportWidth));
            float cameraY = focus.y - (ComparisonCameraViewportCenterY - 0.5f) * 2f * camera.orthographicSize;
            camera.transform.position = new Vector3(focus.x, cameraY, focus.z + ComparisonCameraDepth);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = ComparisonCameraDepth + bounds.extents.z + 100f;
            camera.useOcclusionCulling = false;
            Debug.Log(
                $"GraphicSetting capture framed {YybRootName}: center={bounds.center}, size={bounds.size}, " +
                $"cameraPosition={camera.transform.position}, orthographicSize={camera.orthographicSize}");
        }

        private static bool TryGetVisibleRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!TryGetRendererWorldBounds(renderer, out Bounds rendererBounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasBounds && bounds.size.sqrMagnitude > 0.000001f;
        }

        private static bool TryGetRendererWorldBounds(Renderer renderer, out Bounds bounds)
        {
            if (renderer is SkinnedMeshRenderer skinnedRenderer && skinnedRenderer.sharedMesh != null)
            {
                var bakedMesh = new Mesh();
                try
                {
                    skinnedRenderer.BakeMesh(bakedMesh);
                    bounds = TransformBounds(skinnedRenderer.transform.localToWorldMatrix, bakedMesh.bounds);
                    return bounds.size.sqrMagnitude > 0.000001f;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bakedMesh);
                }
            }

            bounds = renderer.bounds;
            return bounds.size.sqrMagnitude > 0.000001f;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds localBounds)
        {
            Vector3 center = localBounds.center;
            Vector3 extents = localBounds.extents;
            var worldBounds = new Bounds(matrix.MultiplyPoint3x4(center), Vector3.zero);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        worldBounds.Encapsulate(matrix.MultiplyPoint3x4(corner));
                    }
                }
            }

            return worldBounds;
        }

        private static void RenderCameraToPng(Camera camera, string path, int width, int height)
        {
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            Behaviour postProcessLayer = camera.GetComponent("PostProcessLayer") as Behaviour;
            bool previousPostProcessLayerEnabled = postProcessLayer != null && postProcessLayer.enabled;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                if (postProcessLayer != null)
                {
                    postProcessLayer.enabled = false;
                }

                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (postProcessLayer != null)
                {
                    postProcessLayer.enabled = previousPostProcessLayerEnabled;
                }

                UnityEngine.Object.DestroyImmediate(texture);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static void WriteResult(string path, List<TestResult> results)
        {
            int failed = 0;
            foreach (TestResult result in results)
            {
                if (result.Failure != null)
                {
                    failed++;
                }
            }

            int passed = results.Count - failed;
            using (var writer = new StreamWriter(path, false))
            {
                writer.WriteLine($"<test-run testcasecount=\"{results.Count}\" result=\"{(failed == 0 ? "Passed" : "Failed")}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");
                writer.WriteLine("  <test-suite type=\"TestFixture\" name=\"GraphicSettingTests\" result=\"" + (failed == 0 ? "Passed" : "Failed") + "\">");
                foreach (TestResult result in results)
                {
                    string duration = result.Duration.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);
                    if (result.Failure == null)
                    {
                        writer.WriteLine($"    <test-case name=\"{Escape(result.Name)}\" result=\"Passed\" duration=\"{duration}\" />");
                        continue;
                    }

                    writer.WriteLine($"    <test-case name=\"{Escape(result.Name)}\" result=\"Failed\" duration=\"{duration}\">");
                    writer.WriteLine("      <failure>");
                    writer.WriteLine($"        <message>{Escape(result.Failure.Message)}</message>");
                    writer.WriteLine($"        <stack-trace>{Escape(result.Failure.ToString())}</stack-trace>");
                    writer.WriteLine("      </failure>");
                    writer.WriteLine("    </test-case>");
                }

                writer.WriteLine("  </test-suite>");
                writer.WriteLine("</test-run>");
            }
        }

        private static string Escape(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private sealed class TestResult
        {
            public TestResult(string name, Exception failure, TimeSpan duration)
            {
                Name = name;
                Failure = failure;
                Duration = duration;
            }

            public string Name { get; }
            public Exception Failure { get; }
            public TimeSpan Duration { get; }
        }
    }
}
