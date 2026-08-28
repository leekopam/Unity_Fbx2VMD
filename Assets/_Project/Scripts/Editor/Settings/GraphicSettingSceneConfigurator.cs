using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering.Universal;

namespace Fbx2Vmd.Settings.EditorTools
{
    internal static class GraphicSettingSceneConfigurator
    {
        private const string DefaultPostProcessResourcesPath =
            "Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset";

        internal static void Configure(
            GraphicSetting setting,
            BackgroundColorSetting backgroundSetting,
            RecordingSetting recordingSetting)
        {
            UniversalRenderPipelineAsset pipelineAsset = ResolveUniversalRenderPipelineAsset();
            Camera mainCamera = Camera.main;
            GameObject targetModelRoot = ResolveTargetModelRoot(recordingSetting);
            var serialized = new SerializedObject(setting);
            serialized.FindProperty("targetCamera").objectReferenceValue = mainCamera;
            serialized.FindProperty("targetRenderPipelineAsset").objectReferenceValue = pipelineAsset;
            serialized.FindProperty("builtInPostProcessResources").objectReferenceValue =
                ResolvePostProcessResources();
            serialized.FindProperty("applyOnAwake").boolValue = true;
            serialized.FindProperty("applyOnValidate").boolValue = false;
            serialized.FindProperty("textureResolution").enumValueIndex = (int)GraphicSettingQualityPreset.Quality;
            serialized.FindProperty("antiAliasingPreset").enumValueIndex = (int)GraphicSettingQualityPreset.Quality;
            serialized.FindProperty("renderSharpness").enumValueIndex = (int)GraphicSettingQualityPreset.Quality;
            serialized.FindProperty("modelEdgeAndAlpha").enumValueIndex = (int)GraphicSettingQualityPreset.Quality;
            serialized.FindProperty("gameViewScaleMode").enumValueIndex = (int)GraphicGameViewScaleMode.OneX;
            serialized.FindProperty("antiAliasing").enumValueIndex = (int)GraphicAntiAliasingMode.SMAA;
            serialized.FindProperty("smaaQuality").enumValueIndex = (int)AntialiasingQuality.High;
            serialized.FindProperty("enableCameraPostProcessing").boolValue = true;
            serialized.FindProperty("enableCameraMsaa").boolValue = true;
            serialized.FindProperty("msaaSampleCount").intValue = 8;
            serialized.FindProperty("renderScale").floatValue = pipelineAsset == null ? 1.0f : 1.5f;
            ConfigureTextureImportProfile(serialized.FindProperty("textureImportProfile"));
            ConfigureMaterialShaderProfile(serialized.FindProperty("materialShaderProfile"));
            ConfigureObjectArray(serialized.FindProperty("textureSourceRoots"), targetModelRoot);
            ConfigureObjectArray(serialized.FindProperty("materialSourceRoots"), targetModelRoot);
            ConfigureStringArray(serialized.FindProperty("textureAssetFolders"), null);
            ConfigureStringArray(serialized.FindProperty("materialAssetFolders"), null);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ConfigureBackgroundColor(backgroundSetting, mainCamera);
            RecordingSettingSceneConfigurator.Configure(recordingSetting);
            GraphicSettingCameraFramingApplier.ApplyDefaultFraming(mainCamera, targetModelRoot);
        }

        private static void ConfigureBackgroundColor(BackgroundColorSetting backgroundSetting, Camera mainCamera)
        {
            if (backgroundSetting == null)
            {
                return;
            }

            var serialized = new SerializedObject(backgroundSetting);
            serialized.FindProperty("targetCamera").objectReferenceValue = mainCamera;
            serialized.FindProperty("applyOnAwake").boolValue = true;
            serialized.FindProperty("applyOnValidate").boolValue = false;
            serialized.FindProperty("applyBackgroundColor").boolValue = true;
            serialized.FindProperty("backgroundColor").colorValue = Color.black;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTextureImportProfile(SerializedProperty property)
        {
            if (property == null)
            {
                return;
            }

            property.FindPropertyRelative("filterMode").enumValueIndex = (int)FilterMode.Trilinear;
            property.FindPropertyRelative("anisoLevel").intValue = 16;
            property.FindPropertyRelative("maxTextureSize").intValue = 8192;
            property.FindPropertyRelative("compression").enumValueIndex = (int)GraphicTextureCompressionPreference.None;
            property.FindPropertyRelative("alphaIsTransparency").boolValue = true;
        }

        private static void ConfigureMaterialShaderProfile(SerializedProperty property)
        {
            if (property == null)
            {
                return;
            }

            property.FindPropertyRelative("applyOutline").boolValue = true;
            property.FindPropertyRelative("outlineScale").floatValue = 0.00025f;
            property.FindPropertyRelative("outlineSize").floatValue = 0.0002f;
            property.FindPropertyRelative("applyAlphaCutoff").boolValue = true;
            property.FindPropertyRelative("alphaCutoff").floatValue = 0.35f;
            property.FindPropertyRelative("surfaceMode").enumValueIndex = (int)GraphicMaterialSurfaceMode.Keep;
            property.FindPropertyRelative("enableAlphaToCoverage").boolValue = true;
        }

        private static void ConfigureObjectArray(SerializedProperty property, UnityEngine.Object value)
        {
            if (property == null)
            {
                return;
            }

            property.arraySize = value == null ? 0 : 1;
            if (value != null)
            {
                property.GetArrayElementAtIndex(0).objectReferenceValue = value;
            }
        }

        private static void ConfigureStringArray(SerializedProperty property, string value)
        {
            if (property == null)
            {
                return;
            }

            property.arraySize = string.IsNullOrEmpty(value) ? 0 : 1;
            if (!string.IsNullOrEmpty(value))
            {
                property.GetArrayElementAtIndex(0).stringValue = value;
            }
        }

        private static GameObject ResolveTargetModelRoot(RecordingSetting recordingSetting)
        {
            FBXVmdPipeline pipeline = recordingSetting != null
                ? recordingSetting.RecordingFBXVmdPipeline
                : null;
            return pipeline != null ? pipeline.targetCharacter : null;
        }

        private static UniversalRenderPipelineAsset ResolveUniversalRenderPipelineAsset()
        {
            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset qualityAsset)
            {
                return qualityAsset;
            }

            if (GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset graphicsAsset)
            {
                return graphicsAsset;
            }

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultAsset)
            {
                return defaultAsset;
            }

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset currentAsset)
            {
                return currentAsset;
            }

            return null;
        }

        private static PostProcessResources ResolvePostProcessResources()
        {
            return AssetDatabase.LoadAssetAtPath<PostProcessResources>(DefaultPostProcessResourcesPath);
        }
    }
}
