using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Tests.Editor.Settings
{
    public class GraphicSettingTests
    {
        private const string GraphicSettingTypeName = "Fbx2Vmd.Settings.GraphicSetting, Assembly-CSharp";
        private const string BackgroundColorSettingTypeName = "BackgroundColorSetting, Assembly-CSharp";
        private const string RecodingSettingTypeName = "RecordingSetting, Assembly-CSharp";
        private const string FBXVmdPipelineTypeName = "Fbx2Vmd.FBXImporter.FBXVmdPipeline, Assembly-CSharp";
        private const string TextureProfileTypeName = "Fbx2Vmd.Settings.GraphicTextureImportProfile, Assembly-CSharp";
        private const string TexturePlanResolverTypeName =
            "Fbx2Vmd.Settings.GraphicTextureImportPlanResolver, Assembly-CSharp";
        private const string MaterialShaderProfileTypeName = "Fbx2Vmd.Settings.GraphicMaterialShaderProfile, Assembly-CSharp";
        private const string MaterialShaderPlanResolverTypeName =
            "Fbx2Vmd.Settings.GraphicMaterialShaderPlanResolver, Assembly-CSharp";
        private const string MaterialShaderTargetCollectorTypeName =
            "Fbx2Vmd.Settings.GraphicMaterialShaderTargetCollector, Assembly-CSharp";
        private const string InspectorAutoApplyControllerTypeName =
            "Fbx2Vmd.Settings.EditorTools.GraphicSettingInspectorAutoApplyController, Assembly-CSharp-Editor";
        private const string RecordingSettingSceneConfiguratorTypeName =
            "Fbx2Vmd.Settings.EditorTools.RecordingSettingSceneConfigurator, Assembly-CSharp-Editor";
        private const string GraphicSettingSceneConfiguratorTypeName =
            "Fbx2Vmd.Settings.EditorTools.GraphicSettingSceneConfigurator, Assembly-CSharp-Editor";
        private const string RenderScalePresetResolverTypeName =
            "Fbx2Vmd.Settings.GraphicRenderScalePresetResolver, Assembly-CSharp";
        private const string AntiAliasingPlanTypeName =
            "Fbx2Vmd.Settings.GraphicAntiAliasingPlan, Assembly-CSharp";
        private const string AntiAliasingPresetResolverTypeName =
            "Fbx2Vmd.Settings.GraphicAntiAliasingPresetResolver, Assembly-CSharp";
        private const string CameraSettingsApplierTypeName =
            "Fbx2Vmd.Settings.GraphicCameraSettingsApplier, Assembly-CSharp";
        private const string RenderPipelineSettingsApplierTypeName =
            "Fbx2Vmd.Settings.GraphicRenderPipelineSettingsApplier, Assembly-CSharp";
        private const string MaterialShaderUtilityTypeName = "Fbx2Vmd.Settings.GraphicMaterialShaderController, Assembly-CSharp";
        private const string InspectorSchemaTypeName = "Fbx2Vmd.Settings.EditorTools.GraphicSettingInspectorSchema, Assembly-CSharp-Editor";
        private const string SceneInstallerTypeName = "Fbx2Vmd.Settings.EditorTools.GraphicSettingSceneInstaller, Assembly-CSharp-Editor";
        private const string CameraFramingApplierTypeName =
            "Fbx2Vmd.Settings.EditorTools.GraphicSettingCameraFramingApplier, Assembly-CSharp-Editor";
        private const string GameViewScaleAutoApplierTypeName =
            "Fbx2Vmd.Settings.EditorTools.GraphicSettingGameViewScaleAutoApplier, Assembly-CSharp-Editor";
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string ManualRecordButtonName = "MMD_Record_Button";
        private const string ManualRecordMethodName = "StartManualRecording";
        private const float ReferenceMp4ViewportCenterY = 0.28f;
        private const float ReferenceMp4ViewportHeight = 0.56f;
        private static readonly Color ReferenceMp4BackgroundColor = Color.black;
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticMethods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void Given_InspectorProfile_When_ApplyNow_Then_AppliesCameraAndUrpQualitySettings()
        {
            Type graphicSettingType = RequireType(GraphicSettingTypeName);
            var cameraObject = new GameObject("Graphic Setting Test Camera");
            var settingObject = new GameObject("Graphic Setting Test");
            var pipelineAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var setting = settingObject.AddComponent(graphicSettingType);

                SetField(setting, "targetCamera", camera);
                SetField(setting, "targetRenderPipelineAsset", pipelineAsset);
                SetField(setting, "antiAliasingPreset", ParseFieldEnum(setting, "antiAliasingPreset", "Custom"));
                SetField(setting, "renderSharpness", ParseFieldEnum(setting, "renderSharpness", "Custom"));
                SetField(setting, "antiAliasing", ParseFieldEnum(setting, "antiAliasing", "SMAA"));
                SetField(setting, "smaaQuality", ParseFieldEnum(setting, "smaaQuality", "High"));
                SetField(setting, "enableCameraPostProcessing", true);
                SetField(setting, "enableCameraMsaa", true);
                SetField(setting, "msaaSampleCount", 8);
                SetField(setting, "renderScale", 1.5f);

                Invoke(setting, "ApplyNow");

                UniversalAdditionalCameraData additionalData = camera.GetUniversalAdditionalCameraData();
                Assert.That(camera.allowMSAA, Is.True);
                Assert.That(additionalData.renderPostProcessing, Is.True);
                Assert.That(additionalData.antialiasing, Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
                Assert.That(additionalData.antialiasingQuality, Is.EqualTo(AntialiasingQuality.High));
                Assert.That(pipelineAsset.msaaSampleCount, Is.EqualTo(8));
                Assert.That(pipelineAsset.renderScale, Is.EqualTo(1.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineAsset);
                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Given_BackgroundApplyDisabled_When_ApplyNow_Then_PreservesCameraBackground()
        {
            Type backgroundSettingType = RequireType(BackgroundColorSettingTypeName);
            var cameraObject = new GameObject("Background Color Setting Preserve Camera");
            var settingObject = new GameObject("Background Color Setting Preserve");

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.backgroundColor = Color.red;

                var setting = settingObject.AddComponent(backgroundSettingType);
                SetField(setting, "targetCamera", camera);
                SetField(setting, "applyBackgroundColor", false);
                SetField(setting, "backgroundColor", Color.gray);

                Invoke(setting, "ApplyNow");

                Assert.That(camera.backgroundColor, Is.EqualTo(Color.red));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Given_TextureImportProfile_When_CreatePlan_Then_UsesMmdFriendlyDefaults()
        {
            Type profileType = RequireType(TextureProfileTypeName);
            object profile = Activator.CreateInstance(profileType);

            object plan = Invoke(profile, "CreatePlan");

            Assert.That(GetMemberValue<FilterMode>(plan, "FilterMode"), Is.EqualTo(FilterMode.Trilinear));
            Assert.That(GetMemberValue<int>(plan, "AnisoLevel"), Is.EqualTo(8));
            Assert.That(GetMemberValue<int>(plan, "MaxTextureSize"), Is.EqualTo(4096));
            Assert.That(GetMemberValue<object>(plan, "Compression").ToString(), Is.EqualTo("HighQuality"));
            Assert.That(GetMemberValue<bool>(plan, "AlphaIsTransparency"), Is.True);
        }

        [Test]
        public void Given_SimpleTextureQualityPreset_When_CreateTextureImportPlan_Then_UsesHighResolutionPreset()
        {
            Type graphicSettingType = RequireType(GraphicSettingTypeName);
            var settingObject = new GameObject("Graphic Setting Simple Texture Preset Test");

            try
            {
                var setting = settingObject.AddComponent(graphicSettingType);
                SetField(setting, "textureResolution", ParseFieldEnum(setting, "textureResolution", "Quality"));

                object plan = Invoke(setting, "CreateTextureImportPlan");

                Assert.That(GetMemberValue<FilterMode>(plan, "FilterMode"), Is.EqualTo(FilterMode.Trilinear));
                Assert.That(GetMemberValue<int>(plan, "AnisoLevel"), Is.EqualTo(16));
                Assert.That(GetMemberValue<int>(plan, "MaxTextureSize"), Is.EqualTo(8192));
                Assert.That(GetMemberValue<object>(plan, "Compression").ToString(), Is.EqualTo("None"));
                Assert.That(GetMemberValue<bool>(plan, "AlphaIsTransparency"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_GraphicSetting_When_InspectingTexturePresetOwnership_Then_DelegatesPlanResolution()
        {
            const string settingSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicSetting.cs";
            const string resolverSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicTextureImportPlanResolver.cs";

            Assert.That(File.Exists(resolverSourcePath), Is.True, resolverSourcePath);
            Assert.That(Type.GetType(TexturePlanResolverTypeName), Is.Not.Null);

            string settingSource = File.ReadAllText(settingSourcePath);
            string resolverSource = File.ReadAllText(resolverSourcePath);
            Assert.That(
                settingSource,
                Does.Contain("GraphicTextureImportPlanResolver.Resolve("));
            Assert.That(settingSource, Does.Not.Contain("switch (textureResolution)"));
            Assert.That(resolverSource, Does.Contain("switch (preset)"));
            Assert.That(resolverSource, Does.Not.Contain("MonoBehaviour"));
            Assert.That(resolverSource, Does.Not.Contain("interface "));
        }

        [TestCase("Performance", FilterMode.Bilinear, 4, 2048, "HighQuality")]
        [TestCase("Balanced", FilterMode.Trilinear, 8, 4096, "HighQuality")]
        [TestCase("Quality", FilterMode.Trilinear, 16, 8192, "None")]
        [TestCase("Custom", FilterMode.Trilinear, 8, 4096, "HighQuality")]
        public void Given_TextureQualityPreset_When_ResolvingPlan_Then_ReturnsExpectedValues(
            string presetName,
            FilterMode expectedFilterMode,
            int expectedAnisoLevel,
            int expectedMaxTextureSize,
            string expectedCompression)
        {
            Type resolverType = RequireType(TexturePlanResolverTypeName);
            Type presetType = RequireType(
                "Fbx2Vmd.Settings.GraphicSettingQualityPreset, Assembly-CSharp");
            object preset = Enum.Parse(presetType, presetName);

            object plan = InvokeStatic(resolverType, "Resolve", preset, null);

            Assert.That(
                GetMemberValue<FilterMode>(plan, "FilterMode"),
                Is.EqualTo(expectedFilterMode));
            Assert.That(GetMemberValue<int>(plan, "AnisoLevel"), Is.EqualTo(expectedAnisoLevel));
            Assert.That(GetMemberValue<int>(plan, "MaxTextureSize"), Is.EqualTo(expectedMaxTextureSize));
            Assert.That(
                GetMemberValue<object>(plan, "Compression").ToString(),
                Is.EqualTo(expectedCompression));
            Assert.That(GetMemberValue<bool>(plan, "AlphaIsTransparency"), Is.True);
        }

        [Test]
        public void Given_SimpleQualityPresets_When_ApplyNow_Then_MapsReadablePresetsToDetailedSettings()
        {
            Type graphicSettingType = RequireType(GraphicSettingTypeName);
            var cameraObject = new GameObject("Graphic Setting Simple Preset Camera");
            var settingObject = new GameObject("Graphic Setting Simple Preset Test");
            int originalAntiAliasing = QualitySettings.antiAliasing;

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var setting = settingObject.AddComponent(graphicSettingType);

                SetField(setting, "targetCamera", camera);
                SetField(setting, "targetRenderPipelineAsset", null);
                SetField(setting, "antiAliasingPreset", ParseFieldEnum(setting, "antiAliasingPreset", "Quality"));
                SetField(setting, "renderSharpness", ParseFieldEnum(setting, "renderSharpness", "Balanced"));

                Invoke(setting, "ApplyNow");

                Assert.That(QualitySettings.antiAliasing, Is.EqualTo(8));
                Assert.That(GetField<object>(setting, "antiAliasing").ToString(), Is.EqualTo("SMAA"));
                Assert.That(GetField<object>(setting, "smaaQuality").ToString(), Is.EqualTo("High"));
                Assert.That(GetField<float>(setting, "renderScale"), Is.EqualTo(1.0f).Within(0.0001f),
                    "Built-in GameView path must not claim URP renderScale without a configured URP asset.");
            }
            finally
            {
                QualitySettings.antiAliasing = originalAntiAliasing;
                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Given_GraphicSetting_When_InspectingRenderScaleOwnership_Then_DelegatesPresetResolution()
        {
            const string settingSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicSetting.cs";
            const string resolverSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicRenderScalePresetResolver.cs";

            Assert.That(File.Exists(resolverSourcePath), Is.True, resolverSourcePath);
            Assert.That(Type.GetType(RenderScalePresetResolverTypeName), Is.Not.Null);

            string settingSource = File.ReadAllText(settingSourcePath);
            string resolverSource = File.ReadAllText(resolverSourcePath);
            Assert.That(
                settingSource,
                Does.Contain("GraphicRenderScalePresetResolver.Resolve("));
            Assert.That(settingSource, Does.Not.Contain("ApplyRenderSharpnessPreset("));
            Assert.That(resolverSource, Does.Contain("switch (preset)"));
            Assert.That(resolverSource, Does.Not.Contain("MonoBehaviour"));
            Assert.That(resolverSource, Does.Not.Contain("interface "));
        }

        [TestCase("Performance", true, 1.8f, 1.0f)]
        [TestCase("Balanced", true, 1.8f, 1.25f)]
        [TestCase("Quality", true, 1.8f, 1.5f)]
        [TestCase("Custom", true, 1.8f, 1.8f)]
        [TestCase("Balanced", false, 1.8f, 1.0f)]
        [TestCase("Custom", false, 1.8f, 1.8f)]
        public void Given_RenderSharpnessPreset_When_ResolvingScale_Then_ReturnsExpectedValue(
            string presetName,
            bool hasRenderScaleTarget,
            float customRenderScale,
            float expectedRenderScale)
        {
            Type resolverType = RequireType(RenderScalePresetResolverTypeName);
            Type presetType = RequireType(
                "Fbx2Vmd.Settings.GraphicSettingQualityPreset, Assembly-CSharp");
            object preset = Enum.Parse(presetType, presetName);

            float renderScale = (float)InvokeStatic(
                resolverType,
                "Resolve",
                preset,
                hasRenderScaleTarget,
                customRenderScale);

            Assert.That(renderScale, Is.EqualTo(expectedRenderScale).Within(0.0001f));
        }

        [Test]
        public void Given_GraphicSetting_When_InspectingAntiAliasingOwnership_Then_DelegatesPresetResolution()
        {
            const string settingSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicSetting.cs";
            const string planSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicAntiAliasingPlan.cs";
            const string resolverSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicAntiAliasingPresetResolver.cs";

            Assert.That(File.Exists(planSourcePath), Is.True, planSourcePath);
            Assert.That(File.Exists(resolverSourcePath), Is.True, resolverSourcePath);
            Assert.That(Type.GetType(AntiAliasingPlanTypeName), Is.Not.Null);
            Assert.That(Type.GetType(AntiAliasingPresetResolverTypeName), Is.Not.Null);

            string settingSource = File.ReadAllText(settingSourcePath);
            string planSource = File.ReadAllText(planSourcePath);
            string resolverSource = File.ReadAllText(resolverSourcePath);
            Assert.That(
                settingSource,
                Does.Contain("GraphicAntiAliasingPresetResolver.Resolve("));
            Assert.That(settingSource, Does.Not.Contain("switch (antiAliasingPreset)"));
            Assert.That(planSource, Does.Contain("readonly struct GraphicAntiAliasingPlan"));
            Assert.That(resolverSource, Does.Contain("switch (preset)"));
            Assert.That(planSource, Does.Not.Contain("MonoBehaviour"));
            Assert.That(resolverSource, Does.Not.Contain("MonoBehaviour"));
            Assert.That(resolverSource, Does.Not.Contain("interface "));
        }

        [TestCase("Performance", "FXAA", "Low", true, true, 2)]
        [TestCase("Balanced", "SMAA", "Medium", true, true, 4)]
        [TestCase("Quality", "SMAA", "High", true, true, 8)]
        [TestCase("Custom", "TAA", "Low", false, false, 6)]
        public void Given_AntiAliasingPreset_When_ResolvingPlan_Then_ReturnsExpectedValues(
            string presetName,
            string expectedMode,
            string expectedSmaaQuality,
            bool expectedPostProcessing,
            bool expectedMsaa,
            int expectedMsaaSampleCount)
        {
            Type planType = RequireType(AntiAliasingPlanTypeName);
            Type resolverType = RequireType(AntiAliasingPresetResolverTypeName);
            Type presetType = RequireType(
                "Fbx2Vmd.Settings.GraphicSettingQualityPreset, Assembly-CSharp");
            Type modeType = RequireType(
                "Fbx2Vmd.Settings.GraphicAntiAliasingMode, Assembly-CSharp");
            object preset = Enum.Parse(presetType, presetName);
            object customMode = Enum.Parse(modeType, "TAA");
            object customPlan = Activator.CreateInstance(
                planType,
                InstanceFields,
                null,
                new object[] { customMode, AntialiasingQuality.Low, false, false, 6 },
                null);

            object plan = InvokeStatic(resolverType, "Resolve", preset, customPlan);

            Assert.That(GetMemberValue<object>(plan, "AntiAliasing").ToString(), Is.EqualTo(expectedMode));
            Assert.That(GetMemberValue<object>(plan, "SmaaQuality").ToString(), Is.EqualTo(expectedSmaaQuality));
            Assert.That(
                GetMemberValue<bool>(plan, "EnableCameraPostProcessing"),
                Is.EqualTo(expectedPostProcessing));
            Assert.That(GetMemberValue<bool>(plan, "EnableCameraMsaa"), Is.EqualTo(expectedMsaa));
            Assert.That(
                GetMemberValue<int>(plan, "MsaaSampleCount"),
                Is.EqualTo(expectedMsaaSampleCount));
        }

        [Test]
        public void Given_GraphicSetting_When_InspectingCameraWriterOwnership_Then_DelegatesToApplier()
        {
            const string settingSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicSetting.cs";
            const string applierSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicCameraSettingsApplier.cs";

            Assert.That(File.Exists(applierSourcePath), Is.True, applierSourcePath);
            Assert.That(Type.GetType(CameraSettingsApplierTypeName), Is.Not.Null);

            string settingSource = File.ReadAllText(settingSourcePath);
            string applierSource = File.ReadAllText(applierSourcePath);
            Assert.That(settingSource, Does.Contain("GraphicCameraSettingsApplier.Apply("));
            Assert.That(settingSource, Does.Not.Contain("ApplyCameraSettings("));
            Assert.That(settingSource, Does.Not.Contain("ApplyBuiltInPostProcessSettings("));
            Assert.That(settingSource, Does.Not.Contain("ToUrpAntialiasingMode("));
            Assert.That(settingSource, Does.Not.Contain("ToBuiltInAntialiasingMode("));
            Assert.That(settingSource, Does.Not.Contain("ToBuiltInSmaaQuality("));
            Assert.That(applierSource, Does.Contain("camera.allowMSAA"));
            Assert.That(applierSource, Does.Contain("GetUniversalAdditionalCameraData()"));
            Assert.That(applierSource, Does.Contain("PostProcessLayer"));
            Assert.That(applierSource, Does.Not.Contain("interface "));
        }

        [Test]
        public void Given_UrpCameraAndAntiAliasingPlan_When_ApplyingCameraSettings_Then_ConfiguresAdditionalData()
        {
            Type applierType = RequireType(CameraSettingsApplierTypeName);
            Type planType = RequireType(AntiAliasingPlanTypeName);
            Type modeType = RequireType(
                "Fbx2Vmd.Settings.GraphicAntiAliasingMode, Assembly-CSharp");
            var cameraObject = new GameObject("Graphic Camera Applier URP Test");

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                object mode = Enum.Parse(modeType, "SMAA");
                object plan = Activator.CreateInstance(
                    planType,
                    InstanceFields,
                    null,
                    new object[] { mode, AntialiasingQuality.High, true, true, 8 },
                    null);

                InvokeStatic(applierType, "Apply", camera, true, plan, null);

                UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
                Assert.That(camera.allowMSAA, Is.True);
                Assert.That(cameraData.renderPostProcessing, Is.True);
                Assert.That(
                    cameraData.antialiasing,
                    Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
                Assert.That(cameraData.antialiasingQuality, Is.EqualTo(AntialiasingQuality.High));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Given_BuiltInCameraAndAntiAliasingPlan_When_ApplyingCameraSettings_Then_ConfiguresPostProcessLayer()
        {
            Type applierType = RequireType(CameraSettingsApplierTypeName);
            Type planType = RequireType(AntiAliasingPlanTypeName);
            Type modeType = RequireType(
                "Fbx2Vmd.Settings.GraphicAntiAliasingMode, Assembly-CSharp");
            Type postProcessLayerType = RequireType(
                "UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");
            PostProcessResources resources = AssetDatabase.LoadAssetAtPath<PostProcessResources>(
                "Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
            var cameraObject = new GameObject("Graphic Camera Applier Built-in Test");

            Assert.That(resources, Is.Not.Null);
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                object mode = Enum.Parse(modeType, "SMAA");
                object plan = Activator.CreateInstance(
                    planType,
                    InstanceFields,
                    null,
                    new object[] { mode, AntialiasingQuality.Medium, true, false, 2 },
                    null);
                Func<PostProcessResources> resolveResources = () => resources;

                InvokeStatic(applierType, "Apply", camera, false, plan, resolveResources);

                Component layer = camera.GetComponent(postProcessLayerType);
                Assert.That(camera.allowMSAA, Is.False);
                Assert.That(layer, Is.Not.Null);
                Assert.That(GetMemberValue<bool>(layer, "enabled"), Is.True);
                Assert.That(
                    GetMemberValue<object>(layer, "antialiasingMode").ToString(),
                    Is.EqualTo("SubpixelMorphologicalAntialiasing"));
                object smaa = GetMemberValue<object>(layer, "subpixelMorphologicalAntialiasing");
                Assert.That(GetMemberValue<object>(smaa, "quality").ToString(), Is.EqualTo("Medium"));
                Assert.That(GetMemberValue<object>(layer, "m_Resources"), Is.EqualTo(resources));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void Given_GraphicSetting_When_InspectingRenderPipelineWriterOwnership_Then_DelegatesToApplier()
        {
            const string settingSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicSetting.cs";
            const string applierSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicRenderPipelineSettingsApplier.cs";

            Assert.That(File.Exists(applierSourcePath), Is.True, applierSourcePath);
            Assert.That(Type.GetType(RenderPipelineSettingsApplierTypeName), Is.Not.Null);

            string settingSource = File.ReadAllText(settingSourcePath);
            string applierSource = File.ReadAllText(applierSourcePath);
            Assert.That(settingSource, Does.Contain("GraphicRenderPipelineSettingsApplier.Apply("));
            Assert.That(
                settingSource,
                Does.Contain("GraphicRenderPipelineSettingsApplier.NormalizeMsaaSampleCount("));
            Assert.That(settingSource, Does.Not.Contain("pipelineAsset.msaaSampleCount ="));
            Assert.That(settingSource, Does.Not.Contain("pipelineAsset.renderScale ="));
            Assert.That(settingSource, Does.Not.Contain("QualitySettings.antiAliasing ="));
            Assert.That(settingSource, Does.Not.Contain("private static int NormalizeMsaaSampleCount("));
            Assert.That(applierSource, Does.Contain("pipelineAsset.msaaSampleCount ="));
            Assert.That(applierSource, Does.Contain("pipelineAsset.renderScale ="));
            Assert.That(applierSource, Does.Contain("QualitySettings.antiAliasing ="));
            Assert.That(applierSource, Does.Not.Contain("interface "));
        }

        [Test]
        public void Given_UrpPipelineSettings_When_Applying_Then_ConfiguresAssetWithSupportedValues()
        {
            Type applierType = RequireType(RenderPipelineSettingsApplierTypeName);
            var pipelineAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();

            try
            {
                InvokeStatic(applierType, "Apply", pipelineAsset, 3.0f, true, 6);

                Assert.That(pipelineAsset.msaaSampleCount, Is.EqualTo(4));
                Assert.That(pipelineAsset.renderScale, Is.EqualTo(2.0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineAsset);
            }
        }

        [Test]
        public void Given_BuiltInPipelineSettings_When_Applying_Then_ConfiguresGlobalMsaa()
        {
            Type applierType = RequireType(RenderPipelineSettingsApplierTypeName);
            int previousAntiAliasing = QualitySettings.antiAliasing;

            try
            {
                InvokeStatic(applierType, "Apply", null, 1.5f, true, 3);
                Assert.That(QualitySettings.antiAliasing, Is.EqualTo(2));

                InvokeStatic(applierType, "Apply", null, 1.5f, false, 8);
                Assert.That(QualitySettings.antiAliasing, Is.Zero);
            }
            finally
            {
                QualitySettings.antiAliasing = previousAntiAliasing;
            }
        }

        [TestCase(-1, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 2)]
        [TestCase(7, 4)]
        [TestCase(8, 8)]
        [TestCase(16, 8)]
        public void Given_MsaaSampleCount_When_Normalizing_Then_ReturnsSupportedValue(
            int samples,
            int expectedSamples)
        {
            Type applierType = RequireType(RenderPipelineSettingsApplierTypeName);

            int normalizedSamples = (int)InvokeStatic(
                applierType,
                "NormalizeMsaaSampleCount",
                samples);

            Assert.That(normalizedSamples, Is.EqualTo(expectedSamples));
        }

        [Test]
        public void Given_MaterialShaderPaths_When_InspectingTargetTraversal_Then_DelegateToSharedCollector()
        {
            const string settingSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicSetting.cs";
            const string editorControllerSourcePath =
                "Assets/_Project/Scripts/Editor/Settings/GraphicMaterialShaderEditorController.cs";
            const string collectorSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicMaterialShaderTargetCollector.cs";

            Assert.That(File.Exists(collectorSourcePath), Is.True, collectorSourcePath);
            Assert.That(Type.GetType(MaterialShaderTargetCollectorTypeName), Is.Not.Null);

            string settingSource = File.ReadAllText(settingSourcePath);
            string editorControllerSource = File.ReadAllText(editorControllerSourcePath);
            string collectorSource = File.ReadAllText(collectorSourcePath);
            Assert.That(settingSource, Does.Contain("GraphicMaterialShaderTargetCollector.Enumerate("));
            Assert.That(settingSource, Does.Not.Contain("CollectMaterialShaderTargets("));
            Assert.That(settingSource, Does.Not.Contain("GetComponentsInChildren<Renderer>"));
            Assert.That(
                editorControllerSource,
                Does.Contain("GraphicMaterialShaderTargetCollector.Enumerate("));
            Assert.That(editorControllerSource, Does.Not.Contain("GetComponentsInChildren<Renderer>"));
            Assert.That(collectorSource, Does.Contain("GetComponentsInChildren<Renderer>(true)"));
            Assert.That(collectorSource, Does.Not.Contain("interface "));
        }

        [Test]
        public void Given_GraphicSettingInspector_When_InspectingAutoApplyOwnership_Then_DelegatesToController()
        {
            const string inspectorSourcePath =
                "Assets/_Project/Scripts/Editor/Settings/GraphicSettingEditor.cs";
            const string controllerSourcePath =
                "Assets/_Project/Scripts/Editor/Settings/GraphicSettingInspectorAutoApplyController.cs";

            Assert.That(File.Exists(controllerSourcePath), Is.True, controllerSourcePath);
            Assert.That(Type.GetType(InspectorAutoApplyControllerTypeName), Is.Not.Null);

            string inspectorSource = File.ReadAllText(inspectorSourcePath);
            string controllerSource = File.ReadAllText(controllerSourcePath);
            Assert.That(
                inspectorSource,
                Does.Contain("GraphicSettingInspectorAutoApplyController.Schedule(setting, selectedCategory);"));
            Assert.That(inspectorSource, Does.Not.Contain("PendingAutoApplyIds"));
            Assert.That(inspectorSource, Does.Not.Contain("EditorApplication.delayCall"));
            Assert.That(inspectorSource, Does.Not.Contain("Selection.objects"));
            Assert.That(inspectorSource, Does.Not.Contain("ApplyChangedSettings("));
            Assert.That(inspectorSource, Does.Not.Contain("MarkSettingDirty("));
            Assert.That(controllerSource, Does.Contain("EditorApplication.delayCall"));
            Assert.That(controllerSource, Does.Contain("Selection.objects"));
            Assert.That(controllerSource, Does.Contain("ApplyChangedSettings("));
            Assert.That(controllerSource, Does.Contain("MarkSettingDirty("));
            Assert.That(controllerSource, Does.Not.Contain("interface "));
        }

        [Test]
        public void Given_GraphicSettingSceneConfigurator_When_InspectingRecordingDefaults_Then_DelegatesToRecordingConfigurator()
        {
            const string sceneConfiguratorSourcePath =
                "Assets/_Project/Scripts/Editor/Settings/GraphicSettingSceneConfigurator.cs";
            const string recordingConfiguratorSourcePath =
                "Assets/_Project/Scripts/Editor/Settings/RecordingSettingSceneConfigurator.cs";

            Assert.That(
                File.Exists(sceneConfiguratorSourcePath),
                Is.True,
                sceneConfiguratorSourcePath);
            Assert.That(
                File.Exists(recordingConfiguratorSourcePath),
                Is.True,
                recordingConfiguratorSourcePath);
            Assert.That(Type.GetType(RecordingSettingSceneConfiguratorTypeName), Is.Not.Null);

            string sceneConfiguratorSource = File.ReadAllText(sceneConfiguratorSourcePath);
            string recordingConfiguratorSource = File.ReadAllText(recordingConfiguratorSourcePath);
            const string backgroundCall = "ConfigureBackgroundColor(backgroundSetting, mainCamera);";
            const string recordingCall =
                "RecordingSettingSceneConfigurator.Configure(recordingSetting);";
            const string framingCall =
                "GraphicSettingCameraFramingApplier.ApplyDefaultFraming(mainCamera, targetModelRoot);";

            Assert.That(sceneConfiguratorSource, Does.Contain(recordingCall));
            Assert.That(
                sceneConfiguratorSource.IndexOf(recordingCall),
                Is.GreaterThan(sceneConfiguratorSource.IndexOf(backgroundCall)));
            Assert.That(
                sceneConfiguratorSource.IndexOf(framingCall),
                Is.GreaterThan(sceneConfiguratorSource.IndexOf(recordingCall)));
            Assert.That(sceneConfiguratorSource, Does.Not.Contain("ConfigureRecordingControls("));
            Assert.That(sceneConfiguratorSource, Does.Not.Contain("ManualRecordButtonName"));
            Assert.That(
                sceneConfiguratorSource,
                Does.Not.Contain("ManualRecordingButtonBindingApplier.Apply("));
            Assert.That(sceneConfiguratorSource, Does.Not.Contain("recodingSetting"));
            Assert.That(recordingConfiguratorSource, Does.Contain("recordingCaptureQuality"));
            Assert.That(recordingConfiguratorSource, Does.Contain("customRecordingCaptureWidth"));
            Assert.That(
                recordingConfiguratorSource,
                Does.Contain("ManualRecordingButtonBindingApplier.Apply("));
            Assert.That(recordingConfiguratorSource, Does.Not.Contain("interface "));
        }

        [Test]
        public void Given_GraphicSettingSceneInstaller_When_InspectingDefaultConfiguration_Then_DelegatesToConfigurator()
        {
            const string installerSourcePath =
                "Assets/_Project/Scripts/Editor/Settings/GraphicSettingSceneInstaller.cs";
            const string configuratorSourcePath =
                "Assets/_Project/Scripts/Editor/Settings/GraphicSettingSceneConfigurator.cs";

            Assert.That(File.Exists(configuratorSourcePath), Is.True, configuratorSourcePath);
            Assert.That(Type.GetType(GraphicSettingSceneConfiguratorTypeName), Is.Not.Null);

            string installerSource = File.ReadAllText(installerSourcePath);
            string configuratorSource = File.ReadAllText(configuratorSourcePath);
            const string configurationCall =
                "GraphicSettingSceneConfigurator.Configure(setting, backgroundSetting, recordingSetting);";

            Assert.That(installerSource, Does.Contain(configurationCall));
            Assert.That(installerSource, Does.Not.Contain("ConfigureDefaults("));
            Assert.That(installerSource, Does.Not.Contain("SerializedObject"));
            Assert.That(installerSource, Does.Not.Contain("DefaultPostProcessResourcesPath"));
            Assert.That(installerSource, Does.Not.Contain("ResolveUniversalRenderPipelineAsset("));
            Assert.That(
                configuratorSource,
                Does.Contain("ConfigureBackgroundColor(backgroundSetting, mainCamera);"));
            Assert.That(
                configuratorSource,
                Does.Contain("RecordingSettingSceneConfigurator.Configure(recordingSetting);"));
            Assert.That(
                configuratorSource,
                Does.Contain(
                    "GraphicSettingCameraFramingApplier.ApplyDefaultFraming(mainCamera, targetModelRoot);"));
            Assert.That(configuratorSource, Does.Not.Contain("interface "));
        }

        [Test]
        public void Given_ExplicitAndRootMaterials_When_EnumeratingTargets_Then_PreservesTraversalOrder()
        {
            Type collectorType = RequireType(MaterialShaderTargetCollectorTypeName);
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);

            var explicitMaterial = new Material(shader);
            var rootMaterial = new Material(shader);
            var inactiveChildMaterial = new Material(shader);
            var root = new GameObject("Graphic Material Target Root Test");
            var inactiveChild = new GameObject("Graphic Material Target Inactive Child Test");

            try
            {
                root.AddComponent<MeshRenderer>().sharedMaterials = new[] { rootMaterial };
                inactiveChild.transform.SetParent(root.transform, false);
                inactiveChild.AddComponent<MeshRenderer>().sharedMaterials =
                    new[] { inactiveChildMaterial };
                inactiveChild.SetActive(false);

                object targets = InvokeStatic(
                    collectorType,
                    "Enumerate",
                    new Material[] { explicitMaterial, null },
                    new GameObject[] { null, root });
                var materials = new List<Material>((IEnumerable<Material>)targets);

                CollectionAssert.AreEqual(
                    new Material[]
                    {
                        explicitMaterial,
                        null,
                        rootMaterial,
                        inactiveChildMaterial
                    },
                    materials);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(inactiveChildMaterial);
                UnityEngine.Object.DestroyImmediate(rootMaterial);
                UnityEngine.Object.DestroyImmediate(explicitMaterial);
            }
        }

        [Test]
        public void Given_SimpleModelEdgeQualityPreset_When_CreateMaterialShaderPlan_Then_UsesFineOutlinePreset()
        {
            Type graphicSettingType = RequireType(GraphicSettingTypeName);
            var settingObject = new GameObject("Graphic Setting Simple Material Preset Test");

            try
            {
                var setting = settingObject.AddComponent(graphicSettingType);
                SetField(setting, "modelEdgeAndAlpha", ParseFieldEnum(setting, "modelEdgeAndAlpha", "Quality"));

                object plan = Invoke(setting, "CreateMaterialShaderPlan");

                Assert.That(GetMemberValue<bool>(plan, "ApplyOutline"), Is.True);
                Assert.That(GetMemberValue<float>(plan, "OutlineScale"), Is.EqualTo(0.00025f).Within(0.00001f));
                Assert.That(GetMemberValue<float>(plan, "OutlineSize"), Is.EqualTo(0.0002f).Within(0.00001f));
                Assert.That(GetMemberValue<bool>(plan, "ApplyAlphaCutoff"), Is.True);
                Assert.That(GetMemberValue<float>(plan, "AlphaCutoff"), Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(GetMemberValue<object>(plan, "SurfaceMode").ToString(), Is.EqualTo("Keep"));
                Assert.That(GetMemberValue<bool>(plan, "EnableAlphaToCoverage"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_GraphicSetting_When_InspectingMaterialPresetOwnership_Then_DelegatesPlanResolution()
        {
            const string settingSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicSetting.cs";
            const string resolverSourcePath =
                "Assets/_Project/Scripts/Settings/GraphicMaterialShaderPlanResolver.cs";

            Assert.That(File.Exists(resolverSourcePath), Is.True, resolverSourcePath);
            Assert.That(Type.GetType(MaterialShaderPlanResolverTypeName), Is.Not.Null);

            string settingSource = File.ReadAllText(settingSourcePath);
            string resolverSource = File.ReadAllText(resolverSourcePath);
            Assert.That(
                settingSource,
                Does.Contain("GraphicMaterialShaderPlanResolver.Resolve("));
            Assert.That(settingSource, Does.Not.Contain("switch (modelEdgeAndAlpha)"));
            Assert.That(resolverSource, Does.Contain("switch (preset)"));
            Assert.That(resolverSource, Does.Not.Contain("MonoBehaviour"));
            Assert.That(resolverSource, Does.Not.Contain("interface "));
        }

        [TestCase("Performance", false, 0f, 0f, true, 0.35f, "Keep", false)]
        [TestCase("Balanced", true, 0.0005f, 0.00025f, true, 0.35f, "Keep", true)]
        [TestCase("Quality", true, 0.00025f, 0.0002f, true, 0.35f, "Keep", true)]
        [TestCase("Custom", true, 0.0005f, 0.00025f, true, 0.35f, "Keep", true)]
        public void Given_MaterialQualityPreset_When_ResolvingPlan_Then_ReturnsExpectedValues(
            string presetName,
            bool expectedApplyOutline,
            float expectedOutlineScale,
            float expectedOutlineSize,
            bool expectedApplyAlphaCutoff,
            float expectedAlphaCutoff,
            string expectedSurfaceMode,
            bool expectedAlphaToCoverage)
        {
            Type resolverType = RequireType(MaterialShaderPlanResolverTypeName);
            Type presetType = RequireType(
                "Fbx2Vmd.Settings.GraphicSettingQualityPreset, Assembly-CSharp");
            object preset = Enum.Parse(presetType, presetName);

            object plan = InvokeStatic(resolverType, "Resolve", preset, null);

            Assert.That(GetMemberValue<bool>(plan, "ApplyOutline"), Is.EqualTo(expectedApplyOutline));
            Assert.That(
                GetMemberValue<float>(plan, "OutlineScale"),
                Is.EqualTo(expectedOutlineScale).Within(0.000001f));
            Assert.That(
                GetMemberValue<float>(plan, "OutlineSize"),
                Is.EqualTo(expectedOutlineSize).Within(0.000001f));
            Assert.That(
                GetMemberValue<bool>(plan, "ApplyAlphaCutoff"),
                Is.EqualTo(expectedApplyAlphaCutoff));
            Assert.That(
                GetMemberValue<float>(plan, "AlphaCutoff"),
                Is.EqualTo(expectedAlphaCutoff).Within(0.0001f));
            Assert.That(
                GetMemberValue<object>(plan, "SurfaceMode").ToString(),
                Is.EqualTo(expectedSurfaceMode));
            Assert.That(
                GetMemberValue<bool>(plan, "EnableAlphaToCoverage"),
                Is.EqualTo(expectedAlphaToCoverage));
        }

        [Test]
        public void GraphicSettingInspectorSchema_HasTabbedCategoriesForFocusedInspector()
        {
            Type schemaType = RequireType(InspectorSchemaTypeName);
            string[] labels = GetStaticMemberValue<string[]>(schemaType, "CategoryLabels");
            Array categories = GetStaticMemberValue<Array>(schemaType, "Categories");

            Assert.That(labels, Is.EqualTo(new[] { "품질", "대상", "텍스처", "모델", "고급" }));
            Assert.That(categories.Length, Is.EqualTo(labels.Length));
            Assert.That(
                GetEnumNames(categories),
                Is.EqualTo(new[] { "Quality", "Target", "Texture", "Model", "Advanced" }));

            Type verifiedCategoryType = GetStaticMemberValue<Type>(schemaType, "CategoryEnumType");
            Assert.That(Enum.GetNames(verifiedCategoryType), Does.Not.Contain("Capture"));
            Assert.That(Enum.GetNames(verifiedCategoryType), Does.Not.Contain("Recording"));
            Assert.That(Convert.ToInt32(Enum.Parse(verifiedCategoryType, "Quality")), Is.EqualTo(0));
            Assert.That(Convert.ToInt32(Enum.Parse(verifiedCategoryType, "Target")), Is.EqualTo(1));
            Assert.That(Convert.ToInt32(Enum.Parse(verifiedCategoryType, "Recoding")), Is.EqualTo(2));
            Assert.That(Convert.ToInt32(Enum.Parse(verifiedCategoryType, "Texture")), Is.EqualTo(3));
            Assert.That(Convert.ToInt32(Enum.Parse(verifiedCategoryType, "Model")), Is.EqualTo(4));
            Assert.That(Convert.ToInt32(Enum.Parse(verifiedCategoryType, "Advanced")), Is.EqualTo(5));
            FieldInfo legacyRecodingCategory = verifiedCategoryType.GetField("Recoding");
            Assert.That(legacyRecodingCategory, Is.Not.Null);
            Assert.That(
                legacyRecodingCategory.GetCustomAttribute<ObsoleteAttribute>(),
                Is.Not.Null,
                "Recoding enum value must remain only as an obsolete compatibility member.");
            object verifiedPresetCategory = Enum.Parse(verifiedCategoryType, "Quality");
            string[] verifiedPresetFields = (string[])InvokeStatic(schemaType, "GetVisiblePropertyNames", verifiedPresetCategory);

            Assert.That(verifiedPresetFields, Does.Contain("textureResolution"));
            Assert.That(verifiedPresetFields, Does.Contain("antiAliasingPreset"));
            Assert.That(verifiedPresetFields, Does.Contain("renderSharpness"));
            Assert.That(verifiedPresetFields, Does.Not.Contain("targetCamera"));
            Assert.That(verifiedPresetFields, Does.Not.Contain("captureQuality"));
            Assert.That(verifiedPresetFields, Does.Not.Contain("manualRecordButton"));

            string[] textureLabels = (string[])InvokeStatic(schemaType, "GetPresetOptionLabels", "textureResolution");
            Assert.That(textureLabels, Is.EqualTo(new[] { "작업용 2K", "표준 4K", "검수용 원본(최대 8K)", "세부값 직접 입력" }));
            Assert.That(textureLabels, Does.Not.Contain("성능"));
            Assert.That(textureLabels, Does.Not.Contain("균형"));
            Assert.That(textureLabels, Does.Not.Contain("품질"));
            Assert.That(textureLabels, Does.Not.Contain("사용자 지정"));

            string displayName = (string)InvokeStatic(schemaType, "GetPropertyDisplayName", "targetRenderPipelineAsset");
            Assert.That(displayName, Is.EqualTo("URP 렌더 파이프라인 에셋"));

            string inspectorSource = File.ReadAllText(
                "Assets/_Project/Scripts/Editor/Settings/GraphicSettingEditor.cs");
            Assert.That(
                inspectorSource,
                Does.Contain("GraphicSettingInspectorSchema.ResolveCategoryIndex(selectedCategory)"));
            Assert.That(
                inspectorSource,
                Does.Contain("GraphicSettingInspectorSchema.ResolveCategory(selectedIndex)"));
            Assert.That(
                inspectorSource,
                Does.Not.Contain("(GraphicSettingInspectorCategory)selectedIndex"));

            for (int categoryIndex = 0; categoryIndex < categories.Length; categoryIndex++)
            {
                object category = categories.GetValue(categoryIndex);
                int resolvedIndex = (int)InvokeStatic(
                    schemaType,
                    "ResolveCategoryIndex",
                    category);
                object resolvedCategory = InvokeStatic(
                    schemaType,
                    "ResolveCategory",
                    categoryIndex);
                bool autoApplied = (bool)InvokeStatic(schemaType, "AppliesAutomatically", category);
                bool usesManualApplyButton = (bool)InvokeStatic(schemaType, "UsesManualApplyButton", category);

                Assert.That(resolvedIndex, Is.EqualTo(categoryIndex));
                Assert.That(resolvedCategory, Is.EqualTo(category));
                Assert.That(autoApplied, Is.True, $"{category} category must apply changes immediately.");
                Assert.That(usesManualApplyButton, Is.False, $"{category} category must not expose manual apply buttons.");
            }

            object legacyRecodingValue = Enum.Parse(verifiedCategoryType, "Recoding");
            Assert.That(
                InvokeStatic(schemaType, "ResolveCategoryIndex", legacyRecodingValue),
                Is.EqualTo(0));
        }

        [Test]
        public void GraphicSettingInspectorSchema_ExposesOnlyCategoriesWithVisibleProperties()
        {
            Type schemaType = RequireType(InspectorSchemaTypeName);
            Array categories = GetStaticMemberValue<Array>(schemaType, "Categories");

            foreach (object category in categories)
            {
                string[] visibleProperties = (string[])InvokeStatic(
                    schemaType,
                    "GetVisiblePropertyNames",
                    category);

                Assert.That(
                    visibleProperties,
                    Is.Not.Empty,
                    $"{category} category must expose at least one editable property.");
            }
        }

        [Test]
        public void GraphicSettingEditorTypes_AreOwnedByResponsibilityNamedFiles()
        {
            string editorSettingsDirectory = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Editor/Settings");
            string[] editorTypeNames =
            {
                "GraphicSettingInspectorSchema",
                "GraphicMaterialShaderEditorController",
                "GraphicTextureImportEditorController",
                "GraphicSettingGameViewScaleAutoApplier",
                "GameViewScaleController",
                "GraphicSettingSceneInstaller"
            };

            foreach (string typeName in editorTypeNames)
            {
                string sourcePath = Path.Combine(editorSettingsDirectory, $"{typeName}.cs");
                Assert.That(File.Exists(sourcePath), Is.True, $"{typeName} must be stored in {sourcePath}.");
                Assert.That(
                    Type.GetType($"Fbx2Vmd.Settings.EditorTools.{typeName}, Assembly-CSharp-Editor"),
                    Is.Not.Null,
                    $"{typeName} must keep the EditorTools namespace contract.");
            }

            string inspectorSource = File.ReadAllText(
                Path.Combine(editorSettingsDirectory, "GraphicSettingEditor.cs"));
            foreach (string typeName in editorTypeNames)
            {
                Assert.That(
                    inspectorSource,
                    Does.Not.Contain($"class {typeName}"),
                    $"GraphicSettingEditor.cs must not own {typeName}.");
            }
        }

        [Test]
        public void Given_GenericVisibleRenderers_When_ApplyingDefaultFraming_Then_UsesDedicatedCameraBoundary()
        {
            const string applierSourcePath =
                "Assets/_Project/Scripts/Editor/Settings/GraphicSettingCameraFramingApplier.cs";
            const string sceneInstallerSourcePath =
                "Assets/_Project/Scripts/Editor/Settings/GraphicSettingSceneInstaller.cs";

            Assert.That(File.Exists(applierSourcePath), Is.True, applierSourcePath);
            string sceneInstallerSource = File.ReadAllText(sceneInstallerSourcePath);
            Assert.That(sceneInstallerSource, Does.Not.Contain("TryGetVisibleRendererBounds"));
            Assert.That(sceneInstallerSource, Does.Not.Contain("TryGetRendererWorldBounds"));

            Type applierType = RequireType(CameraFramingApplierTypeName);
            var cameraObject = new GameObject("Graphic Setting Camera Framing Test");
            var targetRoot = new GameObject("Generic Camera Framing Target");
            GameObject firstTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject secondTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject disabledTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);

            try
            {
                firstTarget.transform.SetParent(targetRoot.transform);
                firstTarget.transform.position = new Vector3(-2f, 1f, 0f);
                firstTarget.transform.localScale = new Vector3(2f, 4f, 1f);
                secondTarget.transform.SetParent(targetRoot.transform);
                secondTarget.transform.position = new Vector3(4f, 2f, 1f);
                secondTarget.transform.localScale = new Vector3(1f, 2f, 2f);
                Bounds expectedBounds = firstTarget.GetComponent<Renderer>().bounds;
                expectedBounds.Encapsulate(secondTarget.GetComponent<Renderer>().bounds);
                secondTarget.SetActive(false);
                disabledTarget.transform.SetParent(targetRoot.transform);
                disabledTarget.transform.position = new Vector3(100f, 100f, 100f);
                disabledTarget.GetComponent<Renderer>().enabled = false;

                var camera = cameraObject.AddComponent<Camera>();
                InvokeStatic(applierType, "ApplyDefaultFraming", camera, targetRoot);

                float expectedSize = Mathf.Max(
                    expectedBounds.extents.y / 0.56f,
                    expectedBounds.extents.x / ((16f / 9f) * 0.82f));
                float expectedY = expectedBounds.center.y - (0.28f - 0.5f) * 2f * expectedSize;

                Assert.That(camera.orthographic, Is.True);
                Assert.That(camera.orthographicSize, Is.EqualTo(expectedSize).Within(0.0001f));
                Assert.That(camera.transform.position.x, Is.EqualTo(expectedBounds.center.x).Within(0.0001f));
                Assert.That(camera.transform.position.y, Is.EqualTo(expectedY).Within(0.0001f));
                Assert.That(camera.transform.position.z, Is.EqualTo(expectedBounds.center.z + 39f).Within(0.0001f));
                Assert.That(Vector3.Dot(camera.transform.forward, Vector3.back), Is.GreaterThan(0.999f));
                Assert.That(camera.nearClipPlane, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(camera.farClipPlane, Is.EqualTo(139f + expectedBounds.extents.z).Within(0.0001f));
                Assert.That(camera.useOcclusionCulling, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetRoot);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void GraphicSettingRuntimeTypes_AreOwnedByTypeNamedFiles()
        {
            string settingsDirectory = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Settings");
            string[] runtimeTypeNames =
            {
                "GraphicAntiAliasingMode",
                "GraphicGameViewScaleMode",
                "GraphicTextureCompressionPreference",
                "GraphicSettingQualityPreset",
                "GraphicMaterialSurfaceMode",
                "GraphicTextureImportPlan",
                "GraphicMaterialShaderPlan",
                "GraphicMaterialShaderApplyResult",
                "GraphicTextureImportProfile",
                "GraphicMaterialShaderProfile",
                "GraphicMaterialShaderController"
            };

            foreach (string typeName in runtimeTypeNames)
            {
                string sourcePath = Path.Combine(settingsDirectory, $"{typeName}.cs");
                Assert.That(File.Exists(sourcePath), Is.True, $"{typeName} must be stored in {sourcePath}.");
                Assert.That(
                    Type.GetType($"Fbx2Vmd.Settings.{typeName}, Assembly-CSharp"),
                    Is.Not.Null,
                    $"{typeName} must keep the runtime Settings namespace contract.");
            }

            string settingSource = File.ReadAllText(
                Path.Combine(settingsDirectory, "GraphicSetting.cs"));
            string[] extractedTypeDeclarations =
            {
                "enum GraphicAntiAliasingMode",
                "enum GraphicGameViewScaleMode",
                "enum GraphicTextureCompressionPreference",
                "enum GraphicSettingQualityPreset",
                "enum GraphicMaterialSurfaceMode",
                "readonly struct GraphicTextureImportPlan",
                "readonly struct GraphicMaterialShaderPlan",
                "readonly struct GraphicMaterialShaderApplyResult",
                "sealed class GraphicTextureImportProfile",
                "sealed class GraphicMaterialShaderProfile",
                "static class GraphicMaterialShaderController"
            };

            foreach (string declaration in extractedTypeDeclarations)
            {
                Assert.That(
                    settingSource,
                    Does.Not.Contain(declaration),
                    $"GraphicSetting.cs must not own {declaration}.");
            }
        }

        [Test]
        public void Given_MaterialShaderProfile_When_AppliedToYybMaterial_Then_AdjustsSupportedOutlineAndReportsSkippedUnsupportedProperties()
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Model/YYB Hatsune Miku_default/Materials/0.body01.mat");
            Assert.That(source, Is.Not.Null, "YYB material fixture must exist.");

            var material = new Material(source);
            try
            {
                material.SetFloat("_EdgeScale", 0.001f);
                material.SetFloat("_EdgeSize", 0.001f);

                Type profileType = RequireType(MaterialShaderProfileTypeName);
                Type utilityType = RequireType(MaterialShaderUtilityTypeName);
                object profile = Activator.CreateInstance(profileType);

                SetField(profile, "applyOutline", true);
                SetField(profile, "outlineScale", 0.00025f);
                SetField(profile, "outlineSize", 0.0002f);
                SetField(profile, "applyAlphaCutoff", true);
                SetField(profile, "alphaCutoff", 0.35f);
                SetField(profile, "surfaceMode", ParseFieldEnum(profile, "surfaceMode", "Cutout"));
                SetField(profile, "enableAlphaToCoverage", true);

                object plan = Invoke(profile, "CreatePlan");
                object result = InvokeStatic(utilityType, "Apply", plan, new[] { material });

                Assert.That(material.GetFloat("_EdgeScale"), Is.EqualTo(0.00025f).Within(0.00001f));
                Assert.That(material.GetFloat("_EdgeSize"), Is.EqualTo(0.0002f).Within(0.00001f));
                Assert.That(GetMemberValue<int>(result, "ProcessedMaterials"), Is.EqualTo(1));
                Assert.That(GetMemberValue<int>(result, "ChangedMaterials"), Is.EqualTo(1));
                Assert.That(GetMemberValue<int>(result, "ChangedProperties"), Is.EqualTo(2));
                Assert.That(GetMemberValue<int>(result, "SkippedMaterials"), Is.EqualTo(0));
                Assert.That(GetMemberValue<int>(result, "SkippedProperties"), Is.GreaterThanOrEqualTo(6));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Given_MaterialShaderProfile_When_AppliedToCutoutShader_Then_AdjustsCutoffAndSurfaceBlend()
        {
            Material material = new Material(RequireShader("PronamaChan/Standard(DoubleSide)"));
            try
            {
                Type profileType = RequireType(MaterialShaderProfileTypeName);
                Type utilityType = RequireType(MaterialShaderUtilityTypeName);
                object profile = Activator.CreateInstance(profileType);

                SetField(profile, "applyOutline", false);
                SetField(profile, "applyAlphaCutoff", true);
                SetField(profile, "alphaCutoff", 0.35f);
                SetField(profile, "surfaceMode", ParseFieldEnum(profile, "surfaceMode", "Cutout"));
                SetField(profile, "enableAlphaToCoverage", false);

                object plan = Invoke(profile, "CreatePlan");
                object result = InvokeStatic(utilityType, "Apply", plan, new[] { material });

                Assert.That(material.GetFloat("_Cutoff"), Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(material.GetFloat("_Mode"), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(material.GetFloat("_SrcBlend"), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(material.GetFloat("_DstBlend"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.AlphaTest));
                Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.True);
                Assert.That(GetMemberValue<int>(result, "ChangedMaterials"), Is.EqualTo(1));
                Assert.That(GetMemberValue<int>(result, "ChangedProperties"), Is.GreaterThanOrEqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Given_MaterialShaderProfile_When_ShaderLacksProperties_Then_SkipsUnsupportedSettings()
        {
            Material material = new Material(RequireShader("Unlit/Color"));
            try
            {
                Type profileType = RequireType(MaterialShaderProfileTypeName);
                Type utilityType = RequireType(MaterialShaderUtilityTypeName);
                object profile = Activator.CreateInstance(profileType);

                SetField(profile, "applyOutline", true);
                SetField(profile, "applyAlphaCutoff", true);
                SetField(profile, "surfaceMode", ParseFieldEnum(profile, "surfaceMode", "Cutout"));
                SetField(profile, "enableAlphaToCoverage", true);

                object plan = Invoke(profile, "CreatePlan");
                object result = InvokeStatic(utilityType, "Apply", plan, new[] { material });

                Assert.That(GetMemberValue<int>(result, "ProcessedMaterials"), Is.EqualTo(1));
                Assert.That(GetMemberValue<int>(result, "ChangedMaterials"), Is.EqualTo(0));
                Assert.That(GetMemberValue<int>(result, "SkippedProperties"), Is.GreaterThanOrEqualTo(4));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Given_BuiltInPipelineProfile_When_ApplyNow_Then_ConfiguresPostProcessLayerAntialiasing()
        {
            Type graphicSettingType = RequireType(GraphicSettingTypeName);
            Type postProcessLayerType = RequireType(
                "UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");
            var cameraObject = new GameObject("Built-in Graphic Setting Test Camera");
            var settingObject = new GameObject("Built-in Graphic Setting Test");
            int originalAntiAliasing = QualitySettings.antiAliasing;

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var setting = settingObject.AddComponent(graphicSettingType);

                SetField(setting, "targetCamera", camera);
                SetField(setting, "targetRenderPipelineAsset", null);
                SetField(setting, "antiAliasingPreset", ParseFieldEnum(setting, "antiAliasingPreset", "Custom"));
                SetField(setting, "antiAliasing", ParseFieldEnum(setting, "antiAliasing", "FXAA"));
                SetField(setting, "enableCameraPostProcessing", true);
                SetField(setting, "enableCameraMsaa", originalAntiAliasing > 0);
                SetField(setting, "msaaSampleCount", Mathf.Max(1, originalAntiAliasing));

                Invoke(setting, "ApplyNow");

                Component layer = camera.GetComponent(postProcessLayerType);
                Assert.That(layer, Is.Not.Null, "Built-in pipeline AA must add/configure PostProcessLayer.");
                Assert.That(GetMemberValue<object>(layer, "antialiasingMode").ToString(),
                    Is.EqualTo("FastApproximateAntialiasing"));
                Assert.That(GetMemberValue<object>(layer, "m_Resources"),
                    Is.Not.Null, "Built-in PostProcessLayer must have the package resources asset assigned.");
            }
            finally
            {
                QualitySettings.antiAliasing = originalAntiAliasing;
                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void MainRecordingScene_HasGraphicSettingOnRootSettingObjectForYybQualityControl()
        {
            Type graphicSettingType = RequireType(GraphicSettingTypeName);
            Type backgroundSettingType = RequireType(BackgroundColorSettingTypeName);
            Type recodingSettingType = RequireType(RecodingSettingTypeName);
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            GameObject settingRoot = GameObject.Find("Setting");
            Assert.That(settingRoot, Is.Not.Null, "Main_recoding scene must keep a root Setting object.");

            Component component = settingRoot.GetComponent(graphicSettingType);
            Assert.That(component, Is.Not.Null, "Setting must have GraphicSetting component so selecting Setting shows the controls.");
            Component backgroundSetting = settingRoot.GetComponent(backgroundSettingType);
            Assert.That(backgroundSetting, Is.Not.Null, "Setting must have BackgroundColorSetting for GameView background control.");
            Component recodingSetting = settingRoot.GetComponent(recodingSettingType);
            Assert.That(recodingSetting, Is.Not.Null, "Setting must have RecordingSetting for manual recording control.");

            var targetCamera = GetField<Camera>(component, "targetCamera");
            Assert.That(targetCamera, Is.EqualTo(Camera.main), "GraphicSetting must target the Main Camera by default.");
            Assert.That(GetField<Camera>(backgroundSetting, "targetCamera"), Is.EqualTo(Camera.main),
                "BackgroundColorSetting must target the Main Camera by default.");

            var pipelineAsset = GetField<UniversalRenderPipelineAsset>(component, "targetRenderPipelineAsset");
            Assert.That(pipelineAsset, Is.Null,
                "Main_recoding currently uses the built-in render path, so URP renderScale must not be claimed as active.");

            object postProcessResources = GetMemberValue<object>(component, "builtInPostProcessResources");
            Assert.That(postProcessResources, Is.Not.Null,
                "GraphicSetting must keep Built-in Post Processing resources assigned for runtime AA.");

            Assert.That(GetField<object>(component, "textureResolution").ToString(), Is.EqualTo("Quality"),
                "Main_recoding must use the 8K/no-compression import preset for YYB visual quality work.");
            Assert.That(GetField<object>(component, "antiAliasingPreset").ToString(), Is.EqualTo("Quality"),
                "Main_recoding must use the high AA preset for YYB edge readability.");
            Assert.That(GetField<object>(component, "renderSharpness").ToString(), Is.EqualTo("Quality"),
                "Main_recoding must store the high render sharpness preset even when the built-in path keeps renderScale inactive.");
            Assert.That(GetField<object>(component, "modelEdgeAndAlpha").ToString(), Is.EqualTo("Quality"),
                "Main_recoding must use the fine outline/alpha preset for YYB material tuning.");
            Assert.That(GetField<object>(component, "gameViewScaleMode").ToString(), Is.EqualTo("OneX"),
                "Main_recoding must default GameView display to 1x so editor zoom does not exaggerate pixelation.");
            Assert.That(GetField<object>(component, "antiAliasing").ToString(), Is.EqualTo("SMAA"),
                "Main_recoding Setting must apply SMAA to the actual GameView camera path.");
            Assert.That(GetField<object>(component, "smaaQuality").ToString(), Is.EqualTo("High"),
                "Main_recoding Setting must use high SMAA for YYB edge readability.");
            Assert.That(GetField<int>(component, "msaaSampleCount"), Is.EqualTo(8),
                "Main_recoding Setting must keep the quality preset at 8x MSAA.");
            Assert.That(GetField<float>(component, "renderScale"), Is.EqualTo(1.0f).Within(0.0001f),
                "Main_recoding built-in GameView path must leave URP renderScale inactive.");
            Assert.That(GetField<bool>(backgroundSetting, "applyBackgroundColor"), Is.True,
                "BackgroundColorSetting must actively keep the neutral YYB preview background applied.");
            Assert.That(GetField<Color>(backgroundSetting, "backgroundColor"), Is.EqualTo(ReferenceMp4BackgroundColor),
                "BackgroundColorSetting must own the reference mp4 background color.");

            Component fileManager = UnityEngine.Object.FindObjectOfType(RequireType(FBXVmdPipelineTypeName)) as Component;
            Assert.That(fileManager, Is.Not.Null, "Main_recoding scene must keep FBXVmdPipeline for recording control.");
            Component recordingController = ResolveRecordingController(fileManager);
            Assert.That(recordingController, Is.Not.Null, "Main_recoding target must keep HumanoidSampleCode for manual recording.");
            GameObject manualRecordButtonObject = GameObject.Find(ManualRecordButtonName);
            Assert.That(manualRecordButtonObject, Is.Not.Null, "Main_recoding must expose a manual MMD recording button.");
            Button manualRecordButton = manualRecordButtonObject.GetComponent<Button>();
            Assert.That(manualRecordButton, Is.Not.Null, "Manual MMD recording button must use Unity UI Button.");
            Assert.That(GetField<Component>(recodingSetting, "recordingFBXVmdPipeline"), Is.EqualTo(fileManager),
                "RecordingSetting must show which FBXVmdPipeline owns the recording flow.");
            Assert.That(GetField<Button>(recodingSetting, "manualRecordButton"), Is.EqualTo(manualRecordButton),
                "RecordingSetting must show the manual recording button assignment.");
            Assert.That(GetField<Component>(recodingSetting, "recordingController"), Is.EqualTo(recordingController),
                "RecordingSetting must show which HumanoidSampleCode receives manual recording.");
            Assert.That(HasPersistentCall(manualRecordButton, recodingSetting, ManualRecordMethodName), Is.True,
                "Manual recording button must call RecordingSetting.StartManualRecording on the selected Setting object.");

            object materialProfile = GetMemberValue<object>(component, "materialShaderProfile");
            Assert.That(materialProfile, Is.Not.Null,
                "GraphicSetting must expose a material shader profile for MMD outline and alpha tuning.");

            AssertRecordingTargetQualitySources(component, fileManager);

            var serialized = new SerializedObject(component);
            Assert.That(serialized.FindProperty("materialShaderProfile"), Is.Not.Null,
                "GraphicSetting inspector must expose the material shader profile on the selected Setting object.");
            Assert.That(serialized.FindProperty("captureQuality"), Is.Null,
                "GraphicSetting must not expose screenshot capture controls.");
            Assert.That(serialized.FindProperty("captureSuperSize"), Is.Null,
                "GraphicSetting must not own screenshot capture output settings.");
            Assert.That(serialized.FindProperty("recordingCaptureQuality"), Is.Null,
                "GraphicSetting must not own recording capture quality settings.");
            Assert.That(serialized.FindProperty("previewRenderTexture"), Is.Null,
                "GraphicSetting must not own recording preview render textures.");
            Assert.That(serialized.FindProperty("applyBackgroundColor"), Is.Null,
                "GraphicSetting must not own background color controls.");
            Assert.That(serialized.FindProperty("backgroundColor"), Is.Null,
                "GraphicSetting must not own the background color value.");
            Assert.That(serialized.FindProperty("recordingFBXVmdPipeline"), Is.Null,
                "GraphicSetting must not own recording FBXVmdPipeline assignment.");
            Assert.That(serialized.FindProperty("manualRecordButton"), Is.Null,
                "GraphicSetting must not own recording button assignment.");
        }

        [Test]
        public void MainAutoScene_DoesNotCarryGraphicSettingQualityControls()
        {
            Type graphicSettingType = RequireType(GraphicSettingTypeName);
            EditorSceneManager.OpenScene(MainAutoScenePath);

            Component component = UnityEngine.Object.FindObjectOfType(graphicSettingType) as Component;

            Assert.That(component, Is.Null,
                "Main_Auto must stay focused on VMD generation and must not carry the Main_recoding visual quality controls.");
        }

        [Test]
        public void MainRecordingScene_InstallerEnsuresActualGameViewQualityPath()
        {
            Type installerType = RequireType(SceneInstallerTypeName);
            Type graphicSettingType = RequireType(GraphicSettingTypeName);
            Type backgroundSettingType = RequireType(BackgroundColorSettingTypeName);
            Type recodingSettingType = RequireType(RecodingSettingTypeName);
            Type postProcessLayerType = RequireType(
                "UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");

            int originalAntiAliasing = QualitySettings.antiAliasing;
            UniversalRenderPipelineAsset pipelineAsset = null;
            int originalPipelineMsaa = 1;
            float originalPipelineRenderScale = 1f;

            EditorSceneManager.OpenScene(MainRecordingScenePath);
            try
            {
                object installed = InvokeStatic(installerType, "EnsureInActiveScene");
                Assert.That(installed, Is.Not.Null, "Installer must return the applied GraphicSetting component.");
                Assert.That(installed.GetType(), Is.EqualTo(graphicSettingType));

                var component = (Component)installed;
                Assert.That(component.gameObject.name, Is.EqualTo("Setting"),
                    "Installer must keep GraphicSetting on the root Setting object selected by the user.");
                Assert.That(component.transform.parent, Is.Null,
                    "Installer must not hide the GameView controls under a child object.");
                Assert.That(GetField<Camera>(installed, "targetCamera"), Is.EqualTo(Camera.main),
                    "Installer must wire the setting to the actual Main_recoding GameView camera.");

                Assert.That(GetField<object>(installed, "textureResolution").ToString(), Is.EqualTo("Quality"));
                Assert.That(GetField<object>(installed, "antiAliasingPreset").ToString(), Is.EqualTo("Quality"));
                Assert.That(GetField<object>(installed, "renderSharpness").ToString(), Is.EqualTo("Quality"));
                Assert.That(GetField<object>(installed, "modelEdgeAndAlpha").ToString(), Is.EqualTo("Quality"));
                Assert.That(GetField<object>(installed, "gameViewScaleMode").ToString(), Is.EqualTo("OneX"));
                Assert.That(GetField<object>(installed, "antiAliasing").ToString(), Is.EqualTo("SMAA"));
                Assert.That(GetField<object>(installed, "smaaQuality").ToString(), Is.EqualTo("High"));
                Assert.That(GetField<int>(installed, "msaaSampleCount"), Is.EqualTo(8));
                Assert.That(GetField<float>(installed, "renderScale"), Is.EqualTo(1.0f).Within(0.0001f),
                    "Installer must keep URP renderScale inactive when Main_Auto uses the built-in pipeline.");
                Component backgroundSetting = component.GetComponent(backgroundSettingType);
                Assert.That(backgroundSetting, Is.Not.Null, "Installer must keep BackgroundColorSetting on Setting.");
                Assert.That(GetField<Camera>(backgroundSetting, "targetCamera"), Is.EqualTo(Camera.main));
                Assert.That(GetField<bool>(backgroundSetting, "applyBackgroundColor"), Is.True);
                Assert.That(GetField<Color>(backgroundSetting, "backgroundColor"),
                    Is.EqualTo(ReferenceMp4BackgroundColor));
                Component recodingSetting = component.GetComponent(recodingSettingType);
                Assert.That(recodingSetting, Is.Not.Null, "Installer must keep RecordingSetting on Setting.");
                Component fileManager = UnityEngine.Object.FindObjectOfType(RequireType(FBXVmdPipelineTypeName)) as Component;
                Component recordingController = ResolveRecordingController(fileManager);
                Button manualRecordButton = GameObject.Find(ManualRecordButtonName)?.GetComponent<Button>();
                Assert.That(fileManager, Is.Not.Null);
                Assert.That(recordingController, Is.Not.Null);
                Assert.That(manualRecordButton, Is.Not.Null);
                Assert.That(GetField<Component>(recodingSetting, "recordingFBXVmdPipeline"), Is.EqualTo(fileManager));
                Assert.That(GetField<Button>(recodingSetting, "manualRecordButton"), Is.EqualTo(manualRecordButton));
                Assert.That(GetField<Component>(recodingSetting, "recordingController"), Is.EqualTo(recordingController));
                Assert.That(HasPersistentCall(manualRecordButton, recodingSetting, ManualRecordMethodName), Is.True);
                AssertRecordingTargetQualitySources(installed, fileManager);
                Assert.That(graphicSettingType.GetField("captureQuality", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("captureSuperSize", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("captureFolder", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("captureFilePrefix", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("recordingCaptureQuality", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("previewRenderTexture", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("applyBackgroundColor", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("backgroundColor", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("recordingFBXVmdPipeline", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("manualRecordButton", InstanceFields), Is.Null);

                pipelineAsset = GetField<UniversalRenderPipelineAsset>(installed, "targetRenderPipelineAsset");
                if (pipelineAsset != null)
                {
                    originalPipelineMsaa = pipelineAsset.msaaSampleCount;
                    originalPipelineRenderScale = pipelineAsset.renderScale;
                }

                Invoke(installed, "ApplyNow");
                Invoke(backgroundSetting, "ApplyNow");

                Camera mainCamera = Camera.main;
                Assert.That(mainCamera, Is.Not.Null);
                Assert.That(mainCamera.allowMSAA, Is.True);
                Assert.That(mainCamera.backgroundColor, Is.EqualTo(ReferenceMp4BackgroundColor),
                    "Applied GameView camera background must match the black reference mp4 background.");

                if (pipelineAsset != null)
                {
                    UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
                    Assert.That(cameraData.renderPostProcessing, Is.True);
                    Assert.That(cameraData.antialiasing,
                        Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
                    Assert.That(cameraData.antialiasingQuality, Is.EqualTo(AntialiasingQuality.High));
                    Assert.That(pipelineAsset.msaaSampleCount, Is.EqualTo(8));
                    Assert.That(pipelineAsset.renderScale, Is.EqualTo(1.5f).Within(0.0001f));
                }
                else
                {
                    Component layer = mainCamera.GetComponent(postProcessLayerType);
                    Assert.That(layer, Is.Not.Null,
                        "Built-in rendering path must still configure post-processing antialiasing.");
                    Assert.That(GetMemberValue<object>(layer, "antialiasingMode").ToString(),
                        Is.EqualTo("SubpixelMorphologicalAntialiasing"));
                    Assert.That(GetMemberValue<object>(layer, "m_Resources"), Is.Not.Null);
                    Assert.That(QualitySettings.antiAliasing, Is.EqualTo(8));
                }
            }
            finally
            {
                QualitySettings.antiAliasing = originalAntiAliasing;
                if (pipelineAsset != null)
                {
                    pipelineAsset.msaaSampleCount = originalPipelineMsaa;
                    pipelineAsset.renderScale = originalPipelineRenderScale;
                }
            }
        }

        [Test]
        public void MainRecordingScene_ActualGameViewZoomIsReappliedFromSceneSetting()
        {
            Type autoApplierType = RequireType(GameViewScaleAutoApplierTypeName);
            EditorSceneManager.OpenScene(MainRecordingScenePath);
            EditorWindow gameView = RequireGameViewWindow();

            SetGameViewZoomScale(gameView, new Vector2(5f, 5f));
            Vector2 before = GetGameViewZoomScale(gameView);
            Assert.That(before.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(before.y, Is.EqualTo(5f).Within(0.001f));

            bool applied = (bool)InvokeStatic(autoApplierType, "ApplyActiveSceneSettingGameViewScale");
            Vector2 after = GetGameViewZoomScale(gameView);

            Assert.That(applied, Is.True, "Scene setting must apply to the already-open GameView UI.");
            Assert.That(after.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(after.y, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void MainRecordingScene_ActualGameViewZoomDriftIsReappliedFromOneXSetting()
        {
            Type autoApplierType = RequireType(GameViewScaleAutoApplierTypeName);
            EditorSceneManager.OpenScene(MainRecordingScenePath);
            EditorWindow gameView = RequireGameViewWindow();

            SetGameViewZoomScale(gameView, new Vector2(5f, 5f));
            Vector2 before = GetGameViewZoomScale(gameView);
            Assert.That(before.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(before.y, Is.EqualTo(5f).Within(0.001f));

            bool applied = (bool)InvokeStatic(autoApplierType, "ApplyActiveSceneSettingGameViewScaleIfDrifted");
            Vector2 after = GetGameViewZoomScale(gameView);

            Assert.That(applied, Is.True, "OneX setting must reapply when the already-open GameView drifts back to a zoomed scale.");
            Assert.That(after.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(after.y, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void MainRecordingScene_UsesOnlyMainCameraForGameViewComparison()
        {
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            Camera mainCamera = Camera.main;
            Assert.That(mainCamera, Is.Not.Null, "Main_recoding scene must expose a MainCamera-tagged camera for GameView.");

            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            Assert.That(cameras, Has.Length.EqualTo(1),
                "Main_recoding must not keep temporary comparison cameras; ref mp4 comparison must use the existing Main Camera.");
            Assert.That(cameras[0], Is.EqualTo(mainCamera));
        }

        [Test]
        public void MainRecordingScene_MainCameraUsesReferenceMp4BlackBackground()
        {
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            Camera mainCamera = Camera.main;
            Assert.That(mainCamera, Is.Not.Null, "Main_recoding scene must expose a MainCamera-tagged camera for GameView.");
            Assert.That(mainCamera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor),
                "GameView must use a stable solid background behind YYB instead of editor transparency.");
            Assert.That(mainCamera.backgroundColor, Is.EqualTo(ReferenceMp4BackgroundColor),
                "GameView background must match the black background sampled from the reference mp4.");
        }

        [Test]
        public void MainRecordingScene_MainCameraFramesYybRendererBoundsForGameView()
        {
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            Camera mainCamera = Camera.main;
            Assert.That(mainCamera, Is.Not.Null, "Main_recoding scene must expose a MainCamera-tagged camera for GameView.");

            Bounds bounds = GetVisibleRendererBounds(RequireRecordingTargetRoot());
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            Assert.That(GeometryUtility.TestPlanesAABB(planes, bounds), Is.True,
                $"Main Camera frustum must include YYB bounds. cameraPosition={mainCamera.transform.position}, cameraRotation={mainCamera.transform.eulerAngles}, orthographic={mainCamera.orthographic}, orthographicSize={mainCamera.orthographicSize}, boundsCenter={bounds.center}, boundsSize={bounds.size}");

            Vector3 centerViewport = mainCamera.WorldToViewportPoint(bounds.center);
            Vector3 topViewport = mainCamera.WorldToViewportPoint(bounds.center + Vector3.up * bounds.extents.y);
            Vector3 bottomViewport = mainCamera.WorldToViewportPoint(bounds.center - Vector3.up * bounds.extents.y);

            Assert.That(centerViewport.z, Is.GreaterThan(0f), "YYB bounds center must be in front of the Main Camera.");
            Assert.That(centerViewport.x, Is.InRange(0.2f, 0.8f), "YYB bounds center must be horizontally framed.");
            Assert.That(centerViewport.y, Is.InRange(0.2f, 0.8f), "YYB bounds center must be vertically framed.");
            Assert.That(topViewport.y, Is.LessThanOrEqualTo(0.72f), "YYB top must stay inside the GameView frame with the mp4-style lower full-body anchor.");
            Assert.That(bottomViewport.y, Is.GreaterThanOrEqualTo(-0.005f), "YYB feet must stay inside the GameView frame while matching the mp4 lower anchor.");

            float modelViewportHeight = topViewport.y - bottomViewport.y;
            Assert.That(modelViewportHeight, Is.GreaterThanOrEqualTo(0.38f),
                "YYB must occupy enough vertical GameView space to be readable at default 16:9 preview size.");
            Assert.That(modelViewportHeight, Is.LessThanOrEqualTo(0.8f),
                "YYB must keep enough vertical margin that hair and feet are not cropped in GameView.");
        }

        [Test]
        public void MainRecordingScene_MainCameraMatchesReferenceMp4FullBodyFraming()
        {
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            Camera mainCamera = Camera.main;
            Assert.That(mainCamera, Is.Not.Null, "Main_recoding scene must expose a MainCamera-tagged camera for GameView.");

            Bounds bounds = GetVisibleRendererBounds(RequireRecordingTargetRoot());
            Vector3 centerViewport = mainCamera.WorldToViewportPoint(bounds.center);
            Vector3 topViewport = mainCamera.WorldToViewportPoint(bounds.center + Vector3.up * bounds.extents.y);
            Vector3 bottomViewport = mainCamera.WorldToViewportPoint(bounds.center - Vector3.up * bounds.extents.y);
            float modelViewportHeight = topViewport.y - bottomViewport.y;

            Assert.That(centerViewport.x, Is.InRange(0.47f, 0.53f),
                "YYB must be horizontally centered for side-by-side mp4/Unity frame comparison.");
            Assert.That(centerViewport.y, Is.InRange(ReferenceMp4ViewportCenterY - 0.04f, ReferenceMp4ViewportCenterY + 0.04f),
                "YYB vertical framing must match the reference mp4 full-body lower anchor.");
            Assert.That(modelViewportHeight, Is.InRange(ReferenceMp4ViewportHeight - 0.04f, ReferenceMp4ViewportHeight + 0.04f),
                "YYB bbox height must match the reference mp4 full-body scale.");
        }

        [Test]
        public void MainRecordingScene_MainCameraRendersYybPixelsForComparisonCapture()
        {
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            Camera mainCamera = Camera.main;
            Assert.That(mainCamera, Is.Not.Null, "Main_recoding scene must expose a MainCamera-tagged camera for GameView.");

            Bounds bounds = GetVisibleRendererBounds(RequireRecordingTargetRoot());
            if (Application.isBatchMode)
            {
                Debug.Log(
                    "Skipping Main Camera pixel render assertion because Unity batchmode can produce an empty camera buffer; " +
                    "the live Unity Test Runner covers this assertion with a real GameView graphics context.");
                return;
            }

            int nonUniformPixels = CountPixelsDifferentFromCorner(mainCamera, 512, 288);
            Assert.That(nonUniformPixels, Is.GreaterThan(500),
                $"Main Camera render must contain visible YYB pixels for mp4/Unity comparison. cameraPosition={mainCamera.transform.position}, cameraRotation={mainCamera.transform.eulerAngles}, orthographic={mainCamera.orthographic}, orthographicSize={mainCamera.orthographicSize}, boundsCenter={bounds.center}, boundsSize={bounds.size}, nonUniformPixels={nonUniformPixels}");
        }

        private static void AssertRecordingTargetQualitySources(object component, Component fileManager)
        {
            GameObject targetRoot = RequireRecordingTargetRoot(fileManager);
            Assert.That(GetField<GameObject[]>(component, "textureSourceRoots"),
                Is.EqualTo(new[] { targetRoot }));
            Assert.That(GetField<GameObject[]>(component, "materialSourceRoots"),
                Is.EqualTo(new[] { targetRoot }));
            Assert.That(GetField<string[]>(component, "textureAssetFolders"), Is.Empty);
            Assert.That(GetField<string[]>(component, "materialAssetFolders"), Is.Empty);
        }

        private static GameObject RequireRecordingTargetRoot(Component fileManager = null)
        {
            Component resolvedFileManager = fileManager ??
                UnityEngine.Object.FindObjectOfType(RequireType(FBXVmdPipelineTypeName)) as Component;
            Assert.That(resolvedFileManager, Is.Not.Null, "Expected an active FBXVmdPipeline.");

            GameObject targetRoot = GetMemberValue<GameObject>(resolvedFileManager, "targetCharacter");
            Assert.That(targetRoot, Is.Not.Null, "Expected the recording pipeline target model.");
            return targetRoot;
        }

        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName);
            Assert.That(type, Is.Not.Null, $"{typeName} must exist.");
            return type;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceFields);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceFields);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{fieldName}'.");
            return (T)field.GetValue(target);
        }

        private static object ParseFieldEnum(object target, string fieldName, string value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceFields);
            Assert.That(field, Is.Not.Null, $"Expected enum field '{fieldName}'.");
            return Enum.Parse(field.FieldType, value);
        }

        private static T GetMemberValue<T>(object target, string memberName)
        {
            PropertyInfo property = target.GetType().GetProperty(memberName, InstanceFields);
            if (property != null)
            {
                return (T)property.GetValue(target);
            }

            FieldInfo field = target.GetType().GetField(memberName, InstanceFields);
            Assert.That(field, Is.Not.Null, $"Expected field or property '{memberName}'.");
            return (T)field.GetValue(target);
        }

        private static T GetStaticMemberValue<T>(Type type, string memberName)
        {
            PropertyInfo property = type.GetProperty(memberName, StaticMembers);
            if (property != null)
            {
                return (T)property.GetValue(null);
            }

            FieldInfo field = type.GetField(memberName, StaticMembers);
            Assert.That(field, Is.Not.Null, $"Expected static field or property '{memberName}'.");
            return (T)field.GetValue(null);
        }

        private static string[] GetEnumNames(Array values)
        {
            var names = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                names[index] = values.GetValue(index).ToString();
            }

            return names;
        }

        private static object Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, InstanceFields);
            Assert.That(method, Is.Not.Null, $"Expected method '{methodName}'.");
            return method.Invoke(target, null);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethod(methodName, StaticMethods);
            Assert.That(method, Is.Not.Null, $"Expected static method '{methodName}'.");
            return method.Invoke(null, arguments);
        }

        private static bool HasPersistentCall(Button button, UnityEngine.Object target, string methodName)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == target &&
                    button.onClick.GetPersistentMethodName(i) == methodName)
                {
                    return true;
                }
            }

            return false;
        }

        private static Component ResolveRecordingController(Component fileManager)
        {
            if (fileManager == null)
            {
                return null;
            }

            GameObject targetCharacter = GetMemberValue<GameObject>(fileManager, "targetCharacter");
            return targetCharacter != null ? targetCharacter.GetComponent<HumanoidSampleCode>() : null;
        }

        private static EditorWindow RequireGameViewWindow()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            Assert.That(gameViewType, Is.Not.Null, "Expected UnityEditor.GameView type.");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            Assert.That(gameView, Is.Not.Null, "Expected an open GameView window.");
            gameView.Show();
            return gameView;
        }

        private static Vector2 GetGameViewZoomScale(EditorWindow gameView)
        {
            object zoomArea = GetGameViewZoomArea(gameView);
            FieldInfo scaleField = zoomArea.GetType().GetField("m_Scale", InstanceFields);
            Assert.That(scaleField, Is.Not.Null, "Expected GameView zoom area scale field.");
            return (Vector2)scaleField.GetValue(zoomArea);
        }

        private static void SetGameViewZoomScale(EditorWindow gameView, Vector2 scale)
        {
            object zoomArea = GetGameViewZoomArea(gameView);
            FieldInfo scaleField = zoomArea.GetType().GetField("m_Scale", InstanceFields);
            Assert.That(scaleField, Is.Not.Null, "Expected GameView zoom area scale field.");
            scaleField.SetValue(zoomArea, scale);
            gameView.Repaint();
        }

        private static object GetGameViewZoomArea(EditorWindow gameView)
        {
            FieldInfo zoomAreaField = gameView.GetType().GetField("m_ZoomArea", InstanceFields);
            Assert.That(zoomAreaField, Is.Not.Null, "Expected GameView zoom area field.");
            object zoomArea = zoomAreaField.GetValue(gameView);
            Assert.That(zoomArea, Is.Not.Null, "Expected GameView zoom area instance.");
            return zoomArea;
        }

        private static Shader RequireShader(string name)
        {
            Shader shader = Shader.Find(name);
            Assert.That(shader, Is.Not.Null, $"Expected shader '{name}' to exist.");
            return shader;
        }

        private static Bounds GetVisibleRendererBounds(GameObject root)
        {
            Assert.That(root, Is.Not.Null, "Expected the recording target model.");

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            var visibleRenderers = new List<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    visibleRenderers.Add(renderer);
                }
            }

            Assert.That(visibleRenderers.Count, Is.GreaterThan(0),
                $"Expected visible renderers under '{root.name}'.");

            Assert.That(TryGetRendererWorldBounds(visibleRenderers[0], out Bounds bounds), Is.True,
                $"Expected renderer bounds under '{root.name}'.");
            for (int i = 1; i < visibleRenderers.Count; i++)
            {
                if (TryGetRendererWorldBounds(visibleRenderers[i], out Bounds rendererBounds))
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return bounds;
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

        private static int CountPixelsDifferentFromCorner(Camera camera, int width, int height)
        {
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                Color32[] pixels = texture.GetPixels32();
                Color32 background = pixels.Length > 0 ? pixels[0] : new Color32(0, 0, 0, 0);
                int nonUniformPixels = 0;
                foreach (Color32 pixel in pixels)
                {
                    int delta =
                        Math.Abs(pixel.r - background.r) +
                        Math.Abs(pixel.g - background.g) +
                        Math.Abs(pixel.b - background.b) +
                        Math.Abs(pixel.a - background.a);
                    if (delta > 24)
                    {
                        nonUniformPixels++;
                    }
                }

                return nonUniformPixels;
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
    }
}
