using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEditor.Events;
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
        private const string YybRootName = "YYB Hatsune Miku";
        private const string ManualRecordButtonName = "MMD_Record_Button";
        private const string RecodingSettingManualRecordMethodName = nameof(RecordingSetting.StartManualRecording);
        private const string LegacyFBXVmdPipelineManualRecordMethodName = "OnClickManualRecordButton";
        private const string YybTextureFolder = "Assets/_Project/Model/YYB Hatsune Miku_default/tex";
        private const string YybMaterialFolder = "Assets/_Project/Model/YYB Hatsune Miku_default/Materials";
        private const string DefaultPostProcessResourcesPath =
            "Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset";
        private const float DefaultComparisonCameraViewportHeight = 0.56f;
        private const float DefaultComparisonCameraViewportWidth = 0.82f;
        private const float DefaultComparisonCameraViewportCenterY = 0.28f;
        private const float DefaultComparisonCameraAspect = 16f / 9f;
        private const float DefaultComparisonCameraDepth = 39f;

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
            GameObject yybRoot = ResolveYybRoot();
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
            ConfigureObjectArray(serialized.FindProperty("textureSourceRoots"), null);
            ConfigureObjectArray(serialized.FindProperty("materialSourceRoots"), yybRoot);
            ConfigureStringArray(serialized.FindProperty("textureAssetFolders"), YybTextureFolder);
            ConfigureStringArray(serialized.FindProperty("materialAssetFolders"), YybMaterialFolder);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ConfigureBackgroundColor(backgroundSetting, mainCamera);
            ConfigureRecordingControls(recodingSetting);
            ConfigureDefaultCameraComposition(mainCamera, yybRoot);
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

            if (button == null)
            {
                return;
            }

            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                UnityEngine.Object target = button.onClick.GetPersistentTarget(i);
                string methodName = button.onClick.GetPersistentMethodName(i);
                if (target == recodingSetting ||
                    target == fileManager ||
                    methodName == RecodingSettingManualRecordMethodName ||
                    methodName == LegacyFBXVmdPipelineManualRecordMethodName)
                {
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
                }
            }

            UnityEventTools.AddPersistentListener(button.onClick, recodingSetting.StartManualRecording);
            EditorUtility.SetDirty(button);
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

        private static GameObject ResolveYybRoot()
        {
            return GameObject.Find(YybRootName);
        }

        private static void ConfigureDefaultCameraComposition(Camera camera, GameObject targetRoot)
        {
            if (camera == null || targetRoot == null)
            {
                return;
            }

            if (!TryGetVisibleRendererBounds(targetRoot, out Bounds bounds))
            {
                return;
            }

            Vector3 focus = bounds.center;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(
                bounds.extents.y / DefaultComparisonCameraViewportHeight,
                bounds.extents.x / (DefaultComparisonCameraAspect * DefaultComparisonCameraViewportWidth));
            float cameraY = focus.y - (DefaultComparisonCameraViewportCenterY - 0.5f) * 2f * camera.orthographicSize;
            camera.transform.position = new Vector3(focus.x, cameraY, focus.z + DefaultComparisonCameraDepth);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = DefaultComparisonCameraDepth + bounds.extents.z + 100f;
            camera.useOcclusionCulling = false;
        }

        private static bool TryGetVisibleRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled)
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
