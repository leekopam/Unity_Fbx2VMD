using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.Editor.Graphics
{
    public static class GraphicSettingTestBatchRunner
    {
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string GraphicSettingTypeName = "Member_Han.Modules.Graphics.GraphicSetting, Assembly-CSharp";
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
            RunTest(results, nameof(GraphicSettingTests.Given_MaterialShaderProfile_When_AppliedToYybMaterial_Then_AdjustsSupportedOutlineAndReportsSkippedUnsupportedProperties),
                tests.Given_MaterialShaderProfile_When_AppliedToYybMaterial_Then_AdjustsSupportedOutlineAndReportsSkippedUnsupportedProperties);
            RunTest(results, nameof(GraphicSettingTests.Given_MaterialShaderProfile_When_AppliedToCutoutShader_Then_AdjustsCutoffAndSurfaceBlend),
                tests.Given_MaterialShaderProfile_When_AppliedToCutoutShader_Then_AdjustsCutoffAndSurfaceBlend);
            RunTest(results, nameof(GraphicSettingTests.Given_MaterialShaderProfile_When_ShaderLacksProperties_Then_SkipsUnsupportedSettings),
                tests.Given_MaterialShaderProfile_When_ShaderLacksProperties_Then_SkipsUnsupportedSettings);
            RunTest(results, nameof(GraphicSettingTests.Given_BuiltInPipelineProfile_When_ApplyNow_Then_ConfiguresPostProcessLayerAntialiasing),
                tests.Given_BuiltInPipelineProfile_When_ApplyNow_Then_ConfiguresPostProcessLayerAntialiasing);
            RunTest(results, nameof(GraphicSettingTests.MainAutoScene_HasGraphicSettingOnRootSettingObjectForInspectorControl),
                tests.MainAutoScene_HasGraphicSettingOnRootSettingObjectForInspectorControl);
            RunTest(results, nameof(GraphicSettingTests.MainAutoScene_InstallerEnsuresActualGameViewQualityPath),
                tests.MainAutoScene_InstallerEnsuresActualGameViewQualityPath);
            RunTest(results, nameof(GraphicSettingTests.MainAutoScene_MainCameraUsesNeutralPreviewBackgroundForYybVisibility),
                tests.MainAutoScene_MainCameraUsesNeutralPreviewBackgroundForYybVisibility);
            RunTest(results, nameof(GraphicSettingTests.MainAutoScene_MainCameraFramesYybRendererBoundsForGameView),
                tests.MainAutoScene_MainCameraFramesYybRendererBoundsForGameView);

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
            try
            {
                EditorSceneManager.OpenScene(MainAutoScenePath);
                ApplyMainAutoGraphicSetting();

                string folder = Path.GetFullPath(Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Docs/Machine_Spirit/Local/GraphicsCaptures"));
                Directory.CreateDirectory(folder);

                pendingCapturePath = Path.Combine(folder, $"yyb-gameview-after-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                pendingCaptureStartTime = EditorApplication.timeSinceStartup;
                pendingCapturePlayFrames = 0;

                EditorApplication.update -= CaptureMainAutoGameViewEvidenceUpdate;
                EditorApplication.update += CaptureMainAutoGameViewEvidenceUpdate;

                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.EnterPlaymode();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.update -= CaptureMainAutoGameViewEvidenceUpdate;
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

        private static void ApplyMainAutoGraphicSetting()
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
                throw new InvalidOperationException("Main_Auto Setting object must have GraphicSetting.");
            }

            MethodInfo applyNow = graphicSettingType.GetMethod(
                "ApplyNow",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (applyNow == null)
            {
                throw new InvalidOperationException("GraphicSetting.ApplyNow must exist.");
            }

            applyNow.Invoke(setting, null);
        }

        private static void CaptureMainAutoGameViewEvidenceUpdate()
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

                ApplyMainAutoGraphicSetting();
                Camera camera = Camera.main;
                if (camera == null)
                {
                    throw new InvalidOperationException("Main_Auto has no MainCamera-tagged camera.");
                }

                RenderCameraToPng(camera, pendingCapturePath, 1920, 1080);
                Debug.Log($"GraphicSetting capture wrote {pendingCapturePath}");
                EditorApplication.update -= CaptureMainAutoGameViewEvidenceUpdate;
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.update -= CaptureMainAutoGameViewEvidenceUpdate;
                EditorApplication.Exit(1);
            }
        }

        private static void RenderCameraToPng(Camera camera, string path, int width, int height)
        {
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8
            };
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
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
