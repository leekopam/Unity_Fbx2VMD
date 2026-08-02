using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        private const string MaterialShaderProfileTypeName = "Fbx2Vmd.Settings.GraphicMaterialShaderProfile, Assembly-CSharp";
        private const string MaterialShaderUtilityTypeName = "Fbx2Vmd.Settings.GraphicMaterialShaderUtility, Assembly-CSharp";
        private const string InspectorSchemaTypeName = "Fbx2Vmd.Settings.EditorTools.GraphicSettingInspectorSchema, Assembly-CSharp-Editor";
        private const string SceneInstallerTypeName = "Fbx2Vmd.Settings.EditorTools.GraphicSettingSceneInstaller, Assembly-CSharp-Editor";
        private const string GameViewScaleAutoApplierTypeName =
            "Fbx2Vmd.Settings.EditorTools.GraphicSettingGameViewScaleAutoApplier, Assembly-CSharp-Editor";
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string YybTextureFolder = "Assets/_Project/Model/YYB Hatsune Miku_default/tex";
        private const string YybMaterialFolder = "Assets/_Project/Model/YYB Hatsune Miku_default/Materials";
        private const string YybRootName = "YYB Hatsune Miku";
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
        public void GraphicSettingInspectorSchema_HasTabbedCategoriesForFocusedInspector()
        {
            Type schemaType = RequireType(InspectorSchemaTypeName);
            string[] labels = GetStaticMemberValue<string[]>(schemaType, "CategoryLabels");

            Assert.That(labels, Is.EqualTo(new[] { "품질", "대상", "녹화", "텍스처", "모델", "고급" }));

            Type verifiedCategoryType = GetStaticMemberValue<Type>(schemaType, "CategoryEnumType");
            Assert.That(Enum.GetNames(verifiedCategoryType), Does.Not.Contain("Capture"));
            Assert.That(Enum.GetNames(verifiedCategoryType), Does.Not.Contain("Recording"));
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

            foreach (object category in Enum.GetValues(verifiedCategoryType))
            {
                bool autoApplied = (bool)InvokeStatic(schemaType, "AppliesAutomatically", category);
                bool usesManualApplyButton = (bool)InvokeStatic(schemaType, "UsesManualApplyButton", category);
                Assert.That(autoApplied, Is.True, $"{category} category must apply changes immediately.");
                Assert.That(usesManualApplyButton, Is.False, $"{category} category must not expose manual apply buttons.");
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

            AssertYybQualityTargets(component);

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
                AssertYybQualityTargets(installed);
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

            Bounds bounds = GetVisibleRendererBounds(YybRootName);
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

            Bounds bounds = GetVisibleRendererBounds(YybRootName);
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

            Bounds bounds = GetVisibleRendererBounds(YybRootName);
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

        private static void AssertYybQualityTargets(object component)
        {
            var textureSourceRoots = GetField<GameObject[]>(component, "textureSourceRoots");
            Assert.That(textureSourceRoots, Is.Empty,
                "Texture import must stay limited to the configured YYB tex folder and must not walk toon/fx/spa roots.");

            var materialSourceRoots = GetField<GameObject[]>(component, "materialSourceRoots");
            Assert.That(materialSourceRoots, Has.Length.EqualTo(1));
            Assert.That(materialSourceRoots[0], Is.Not.Null);
            Assert.That(materialSourceRoots[0].name, Is.EqualTo(YybRootName));

            Assert.That(GetField<string[]>(component, "textureAssetFolders"),
                Is.EquivalentTo(new[] { YybTextureFolder }));
            Assert.That(GetField<string[]>(component, "materialAssetFolders"),
                Is.EquivalentTo(new[] { YybMaterialFolder }));
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

        private static Bounds GetVisibleRendererBounds(string rootName)
        {
            GameObject root = GameObject.Find(rootName);
            Assert.That(root, Is.Not.Null, $"Expected scene object '{rootName}'.");

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            var visibleRenderers = new List<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    visibleRenderers.Add(renderer);
                }
            }

            Assert.That(visibleRenderers.Count, Is.GreaterThan(0), $"Expected visible renderers under '{rootName}'.");

            Assert.That(TryGetRendererWorldBounds(visibleRenderers[0], out Bounds bounds), Is.True,
                $"Expected renderer bounds under '{rootName}'.");
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
