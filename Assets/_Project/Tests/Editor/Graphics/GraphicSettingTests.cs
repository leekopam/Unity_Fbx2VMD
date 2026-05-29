using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Tests.Editor.Graphics
{
    public class GraphicSettingTests
    {
        private const string GraphicSettingTypeName = "Member_Han.Modules.Graphics.GraphicSetting, Assembly-CSharp";
        private const string TextureProfileTypeName = "Member_Han.Modules.Graphics.GraphicTextureImportProfile, Assembly-CSharp";
        private const string MaterialShaderProfileTypeName = "Member_Han.Modules.Graphics.GraphicMaterialShaderProfile, Assembly-CSharp";
        private const string MaterialShaderUtilityTypeName = "Member_Han.Modules.Graphics.GraphicMaterialShaderUtility, Assembly-CSharp";
        private const string InspectorSchemaTypeName = "Member_Han.Modules.Graphics.EditorTools.GraphicSettingInspectorSchema, Assembly-CSharp-Editor";
        private const string SceneInstallerTypeName = "Member_Han.Modules.Graphics.EditorTools.GraphicSettingSceneInstaller, Assembly-CSharp-Editor";
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
                SetField(setting, "applyBackgroundColor", true);
                SetField(setting, "backgroundColor", Color.gray);

                Invoke(setting, "ApplyNow");

                UniversalAdditionalCameraData additionalData = camera.GetUniversalAdditionalCameraData();
                Assert.That(camera.allowMSAA, Is.True);
                Assert.That(camera.backgroundColor, Is.EqualTo(Color.gray));
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
            Type graphicSettingType = RequireType(GraphicSettingTypeName);
            var cameraObject = new GameObject("Graphic Setting Background Preserve Camera");
            var settingObject = new GameObject("Graphic Setting Background Preserve");
            var pipelineAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.backgroundColor = Color.red;

                var setting = settingObject.AddComponent(graphicSettingType);
                SetField(setting, "targetCamera", camera);
                SetField(setting, "targetRenderPipelineAsset", pipelineAsset);
                SetField(setting, "applyBackgroundColor", false);
                SetField(setting, "backgroundColor", Color.gray);

                Invoke(setting, "ApplyNow");

                Assert.That(camera.backgroundColor, Is.EqualTo(Color.red));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineAsset);
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

            Assert.That(labels, Is.EqualTo(new[] { "품질", "대상", "텍스처", "모델", "고급" }));

            Type verifiedCategoryType = GetStaticMemberValue<Type>(schemaType, "CategoryEnumType");
            Assert.That(Enum.GetNames(verifiedCategoryType), Does.Not.Contain("Capture"));
            object verifiedPresetCategory = Enum.Parse(verifiedCategoryType, "Quality");
            string[] verifiedPresetFields = (string[])InvokeStatic(schemaType, "GetVisiblePropertyNames", verifiedPresetCategory);

            Assert.That(verifiedPresetFields, Does.Contain("textureResolution"));
            Assert.That(verifiedPresetFields, Does.Contain("antiAliasingPreset"));
            Assert.That(verifiedPresetFields, Does.Contain("renderSharpness"));
            Assert.That(verifiedPresetFields, Does.Not.Contain("targetCamera"));
            Assert.That(verifiedPresetFields, Does.Not.Contain("captureQuality"));

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
        public void MainAutoScene_HasGraphicSettingOnRootSettingObjectForInspectorControl()
        {
            Type graphicSettingType = RequireType(GraphicSettingTypeName);
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");

            GameObject settingRoot = GameObject.Find("Setting");
            Assert.That(settingRoot, Is.Not.Null, "Main_Auto scene must keep a root Setting object.");

            Component component = settingRoot.GetComponent(graphicSettingType);
            Assert.That(component, Is.Not.Null, "Setting must have GraphicSetting component so selecting Setting shows the controls.");

            var targetCamera = GetField<Camera>(component, "targetCamera");
            Assert.That(targetCamera, Is.EqualTo(Camera.main), "GraphicSetting must target the Main Camera by default.");

            var pipelineAsset = GetField<UniversalRenderPipelineAsset>(component, "targetRenderPipelineAsset");
            Assert.That(pipelineAsset, Is.Null,
                "Main_Auto currently uses the built-in render path, so URP renderScale must not be claimed as active.");

            object postProcessResources = GetMemberValue<object>(component, "builtInPostProcessResources");
            Assert.That(postProcessResources, Is.Not.Null,
                "GraphicSetting must keep Built-in Post Processing resources assigned for runtime AA.");

            Assert.That(GetField<object>(component, "antiAliasing").ToString(), Is.EqualTo("SMAA"),
                "Main_Auto Setting must apply SMAA to the actual GameView camera path.");
            Assert.That(GetField<object>(component, "smaaQuality").ToString(), Is.EqualTo("High"),
                "Main_Auto Setting must use high SMAA for YYB edge readability.");
            Assert.That(GetField<int>(component, "msaaSampleCount"), Is.EqualTo(8),
                "Main_Auto Setting must keep the quality preset at 8x MSAA.");
            Assert.That(GetField<float>(component, "renderScale"), Is.EqualTo(1.0f).Within(0.0001f),
                "Main_Auto built-in GameView path must leave URP renderScale inactive.");
            Assert.That(GetField<bool>(component, "applyBackgroundColor"), Is.True,
                "Main_Auto Setting must actively keep the neutral YYB preview background applied.");

            object materialProfile = GetMemberValue<object>(component, "materialShaderProfile");
            Assert.That(materialProfile, Is.Not.Null,
                "GraphicSetting must expose a material shader profile for MMD outline and alpha tuning.");

            var serialized = new SerializedObject(component);
            Assert.That(serialized.FindProperty("materialShaderProfile"), Is.Not.Null,
                "GraphicSetting inspector must expose the material shader profile on the selected Setting object.");
            Assert.That(serialized.FindProperty("captureQuality"), Is.Null,
                "GraphicSetting must not expose screenshot capture controls.");
            Assert.That(serialized.FindProperty("captureSuperSize"), Is.Null,
                "GraphicSetting must not own screenshot capture output settings.");
        }

        [Test]
        public void MainAutoScene_InstallerEnsuresActualGameViewQualityPath()
        {
            Type installerType = RequireType(SceneInstallerTypeName);
            Type graphicSettingType = RequireType(GraphicSettingTypeName);
            Type postProcessLayerType = RequireType(
                "UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");

            int originalAntiAliasing = QualitySettings.antiAliasing;
            UniversalRenderPipelineAsset pipelineAsset = null;
            int originalPipelineMsaa = 1;
            float originalPipelineRenderScale = 1f;

            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");
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
                    "Installer must wire the setting to the actual Main_Auto GameView camera.");

                Assert.That(GetField<object>(installed, "antiAliasingPreset").ToString(), Is.EqualTo("Quality"));
                Assert.That(GetField<object>(installed, "renderSharpness").ToString(), Is.EqualTo("Balanced"));
                Assert.That(GetField<object>(installed, "antiAliasing").ToString(), Is.EqualTo("SMAA"));
                Assert.That(GetField<object>(installed, "smaaQuality").ToString(), Is.EqualTo("High"));
                Assert.That(GetField<int>(installed, "msaaSampleCount"), Is.EqualTo(8));
                Assert.That(GetField<float>(installed, "renderScale"), Is.EqualTo(1.0f).Within(0.0001f),
                    "Installer must keep URP renderScale inactive when Main_Auto uses the built-in pipeline.");
                Assert.That(GetField<bool>(installed, "applyBackgroundColor"), Is.True);
                Assert.That(GetField<Color>(installed, "backgroundColor"),
                    Is.EqualTo(new Color(0.5f, 0.5f, 0.5f, 1f)));
                Assert.That(graphicSettingType.GetField("captureQuality", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("captureSuperSize", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("captureFolder", InstanceFields), Is.Null);
                Assert.That(graphicSettingType.GetField("captureFilePrefix", InstanceFields), Is.Null);

                pipelineAsset = GetField<UniversalRenderPipelineAsset>(installed, "targetRenderPipelineAsset");
                if (pipelineAsset != null)
                {
                    originalPipelineMsaa = pipelineAsset.msaaSampleCount;
                    originalPipelineRenderScale = pipelineAsset.renderScale;
                }

                Invoke(installed, "ApplyNow");

                Camera mainCamera = Camera.main;
                Assert.That(mainCamera, Is.Not.Null);
                Assert.That(mainCamera.allowMSAA, Is.True);
                Assert.That(mainCamera.backgroundColor, Is.EqualTo(new Color(0.5f, 0.5f, 0.5f, 1f)),
                    "Applied GameView camera background must keep YYB visible against a neutral preview color.");

                if (pipelineAsset != null)
                {
                    UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
                    Assert.That(cameraData.renderPostProcessing, Is.True);
                    Assert.That(cameraData.antialiasing,
                        Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
                    Assert.That(cameraData.antialiasingQuality, Is.EqualTo(AntialiasingQuality.High));
                    Assert.That(pipelineAsset.msaaSampleCount, Is.EqualTo(8));
                    Assert.That(pipelineAsset.renderScale, Is.EqualTo(1.25f).Within(0.0001f));
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
        public void MainAutoScene_MainCameraUsesNeutralPreviewBackgroundForYybVisibility()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");

            Camera mainCamera = Camera.main;
            Assert.That(mainCamera, Is.Not.Null, "Main_Auto scene must expose a MainCamera-tagged camera for GameView.");
            Assert.That(mainCamera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor),
                "GameView must use a stable solid background behind YYB instead of editor transparency.");
            Assert.That(mainCamera.backgroundColor, Is.EqualTo(new Color(0.5f, 0.5f, 0.5f, 1f)),
                "GameView background must be neutral gray so dark YYB hair/clothes remain readable.");
        }

        [Test]
        public void MainAutoScene_MainCameraFramesYybRendererBoundsForGameView()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scene/Main_Auto.unity");

            Camera mainCamera = Camera.main;
            Assert.That(mainCamera, Is.Not.Null, "Main_Auto scene must expose a MainCamera-tagged camera for GameView.");

            Bounds bounds = GetVisibleRendererBounds("YYB Hatsune Miku");
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            Assert.That(GeometryUtility.TestPlanesAABB(planes, bounds), Is.True,
                $"Main Camera frustum must include YYB bounds. cameraPosition={mainCamera.transform.position}, cameraRotation={mainCamera.transform.eulerAngles}, orthographic={mainCamera.orthographic}, orthographicSize={mainCamera.orthographicSize}, boundsCenter={bounds.center}, boundsSize={bounds.size}");

            Vector3 centerViewport = mainCamera.WorldToViewportPoint(bounds.center);
            Vector3 topViewport = mainCamera.WorldToViewportPoint(bounds.center + Vector3.up * bounds.extents.y);
            Vector3 bottomViewport = mainCamera.WorldToViewportPoint(bounds.center - Vector3.up * bounds.extents.y);

            Assert.That(centerViewport.z, Is.GreaterThan(0f), "YYB bounds center must be in front of the Main Camera.");
            Assert.That(centerViewport.x, Is.InRange(0.2f, 0.8f), "YYB bounds center must be horizontally framed.");
            Assert.That(centerViewport.y, Is.InRange(0.2f, 0.8f), "YYB bounds center must be vertically framed.");
            Assert.That(topViewport.y, Is.LessThanOrEqualTo(0.95f), "YYB top must stay inside the GameView frame.");
            Assert.That(bottomViewport.y, Is.GreaterThanOrEqualTo(0.05f), "YYB feet must stay inside the GameView frame.");

            float modelViewportHeight = topViewport.y - bottomViewport.y;
            Assert.That(modelViewportHeight, Is.GreaterThanOrEqualTo(0.38f),
                "YYB must occupy enough vertical GameView space to be readable at default 16:9 preview size.");
            Assert.That(modelViewportHeight, Is.LessThanOrEqualTo(0.8f),
                "YYB must keep enough vertical margin that hair and feet are not cropped in GameView.");
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

            Bounds bounds = visibleRenderers[0].bounds;
            for (int i = 1; i < visibleRenderers.Count; i++)
            {
                bounds.Encapsulate(visibleRenderers[i].bounds);
            }

            return bounds;
        }
    }
}
