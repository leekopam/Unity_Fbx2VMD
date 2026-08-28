using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings.EditorTools
{
    public static class GraphicSettingSceneInstaller
    {
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string ManualRecordButtonName = "MMD_Record_Button";
        private const string DefaultPostProcessResourcesPath =
            "Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset";

        [MenuItem("Tools/Graphics/Install Graphic Setting In Main_recoding")]
        public static void InstallMainRecording()
        {
            var scene = EditorSceneManager.OpenScene(MainRecordingScenePath);
            GraphicSetting setting = EnsureInActiveScene();
            setting.ApplyNow();
            GameViewScaleController.TryApply(setting.GameViewScaleMode);
            BackgroundColorSetting backgroundSetting = setting.GetComponent<BackgroundColorSetting>();
            backgroundSetting?.ApplyNow();
            GraphicTextureImportEditorController.Apply(setting);
            GraphicMaterialShaderEditorController.Apply(setting);
            Selection.activeObject = setting.gameObject;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"GraphicSetting 설치 완료: {setting.gameObject.name}");
        }

        public static GraphicSetting EnsureInActiveScene()
        {
            GameObject root = ResolveSettingRoot();
            if (root == null)
            {
                root = new GameObject("Setting");
            }
            else if (root.name != "Setting")
            {
                root.name = "Setting";
                EditorUtility.SetDirty(root);
            }

            GraphicSetting setting = root.GetComponent<GraphicSetting>();
            if (setting == null)
            {
                setting = root.AddComponent<GraphicSetting>();
            }

            BackgroundColorSetting backgroundSetting = EnsureComponent<BackgroundColorSetting>(root);
            RecordingSetting recodingSetting = EnsureComponent<RecordingSetting>(root);
            RemoveLegacyGraphicSettingChild(root);
            ConfigureDefaults(setting, backgroundSetting, recodingSetting);
            return setting;
        }

        private static GameObject ResolveSettingRoot()
        {
            GameObject root = GameObject.Find("Setting");
            if (root != null)
            {
                return root;
            }

            return GameObject.Find("SettingManager");
        }

        private static T EnsureComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static void RemoveLegacyGraphicSettingChild(GameObject root)
        {
            Transform child = root.transform.Find("Graphic Setting");
            if (child == null || child.GetComponent<GraphicSetting>() == null || child.childCount > 0)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        private static void ConfigureDefaults(
            GraphicSetting setting,
            BackgroundColorSetting backgroundSetting,
            RecordingSetting recodingSetting)
        {
            UniversalRenderPipelineAsset pipelineAsset = ResolveUniversalRenderPipelineAsset();
            Camera mainCamera = Camera.main;
            GameObject targetModelRoot = ResolveTargetModelRoot(recodingSetting);
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
            ConfigureRecordingControls(recodingSetting);
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

        private static void ConfigureRecordingControls(RecordingSetting recodingSetting)
        {
            if (recodingSetting == null)
            {
                return;
            }

            FBXVmdPipeline fileManager = recodingSetting.RecordingFBXVmdPipeline;
            Button button = ResolveManualRecordButton();
            HumanoidSampleCode controller = ResolveRecordingController(fileManager);
            var serialized = new SerializedObject(recodingSetting);
            serialized.FindProperty("recordingFBXVmdPipeline").objectReferenceValue = fileManager;
            serialized.FindProperty("manualRecordButton").objectReferenceValue = button;
            SetObjectReference(serialized, "recordingController", controller);
            SetBool(serialized, "enableRecordingDiagnostics", fileManager != null && fileManager.enableRecordingDiagnostics);
            SetBool(
                serialized,
                "useDeterministicCaptureFramerateForDiagnostics",
                fileManager != null && fileManager.useDeterministicCaptureFramerateForDiagnostics);
            SetBool(serialized, "enableDiagnosticFingerCloseups", fileManager == null || fileManager.enableDiagnosticFingerCloseups);
            SetEnum(serialized, "recordingCaptureQuality", (int)RecordingCaptureQualityPreset.Uhd4K);
            SetInt(serialized, "customRecordingCaptureWidth", 3840);
            SetInt(serialized, "customRecordingCaptureHeight", 2160);
            SetBool(serialized, "applyDiagnosticsToFBXVmdPipelineOnAwake", true);
            SetObjectReference(serialized, "settingsPopup", null);
            SetBool(serialized, "openSettingsPopupOnStart", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ManualRecordingButtonBindingApplier.Apply(button, recodingSetting, fileManager);
        }

        private static HumanoidSampleCode ResolveRecordingController(FBXVmdPipeline fileManager)
        {
            if (fileManager != null && fileManager.targetCharacter != null)
            {
                HumanoidSampleCode controller = fileManager.targetCharacter.GetComponent<HumanoidSampleCode>();
                if (controller != null)
                {
                    return controller;
                }
            }

            return null;
        }

        private static void SetObjectReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetEnum(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.enumValueIndex = value;
            }
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static Button ResolveManualRecordButton()
        {
            GameObject buttonObject = GameObject.Find(ManualRecordButtonName);
            return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
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

        private static GameObject ResolveTargetModelRoot(RecordingSetting recodingSetting)
        {
            FBXVmdPipeline fileManager = recodingSetting != null
                ? recodingSetting.RecordingFBXVmdPipeline
                : null;
            return fileManager != null ? fileManager.targetCharacter : null;
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
