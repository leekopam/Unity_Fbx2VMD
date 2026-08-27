using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering.Universal;

namespace Fbx2Vmd.Settings
{
    [DisallowMultipleComponent]
    public sealed class GraphicSetting : MonoBehaviour
    {
#if UNITY_EDITOR
        private const string DefaultPostProcessResourcesPath =
            "Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset";
#endif

        [Header("Targets")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private UniversalRenderPipelineAsset targetRenderPipelineAsset;
        [SerializeField] private PostProcessResources builtInPostProcessResources;

        [Header("Apply")]
        [SerializeField] private bool applyOnAwake = true;
        [SerializeField] private bool applyOnValidate;

        [Header("Simple Presets")]
        [SerializeField] private GraphicSettingQualityPreset textureResolution = GraphicSettingQualityPreset.Balanced;
        [SerializeField] private GraphicSettingQualityPreset antiAliasingPreset = GraphicSettingQualityPreset.Quality;
        [SerializeField] private GraphicSettingQualityPreset renderSharpness = GraphicSettingQualityPreset.Balanced;
        [SerializeField] private GraphicSettingQualityPreset modelEdgeAndAlpha = GraphicSettingQualityPreset.Balanced;

        [Header("Anti Aliasing")]
        [SerializeField] private GraphicAntiAliasingMode antiAliasing = GraphicAntiAliasingMode.SMAA;
        [SerializeField] private AntialiasingQuality smaaQuality = AntialiasingQuality.High;
        [SerializeField] private bool enableCameraPostProcessing = true;
        [SerializeField] private bool enableCameraMsaa = true;
        [SerializeField] private int msaaSampleCount = 8;
        [SerializeField, Range(0.1f, 2.0f)] private float renderScale = 1.0f;

        [Header("Editor GameView")]
        [SerializeField] private GraphicGameViewScaleMode gameViewScaleMode = GraphicGameViewScaleMode.OneX;

        [Header("Texture Import")]
        [SerializeField] private GraphicTextureImportProfile textureImportProfile = new GraphicTextureImportProfile();
        [SerializeField] private GameObject[] textureSourceRoots = Array.Empty<GameObject>();
        [SerializeField] private Texture2D[] textureImportTargets = Array.Empty<Texture2D>();
        [SerializeField] private string[] textureAssetFolders = Array.Empty<string>();

        [Header("Material Shader")]
        [SerializeField] private GraphicMaterialShaderProfile materialShaderProfile = new GraphicMaterialShaderProfile();
        [SerializeField] private GameObject[] materialSourceRoots = Array.Empty<GameObject>();
        [SerializeField] private Material[] materialShaderTargets = Array.Empty<Material>();
        [SerializeField] private string[] materialAssetFolders = Array.Empty<string>();

        public Camera TargetCamera => targetCamera;
        public UniversalRenderPipelineAsset TargetRenderPipelineAsset => targetRenderPipelineAsset;
        public GraphicTextureImportProfile TextureImportProfile => textureImportProfile;
        public GameObject[] TextureSourceRoots => textureSourceRoots;
        public Texture2D[] TextureImportTargets => textureImportTargets;
        public string[] TextureAssetFolders => textureAssetFolders;
        public GraphicMaterialShaderProfile MaterialShaderProfile => materialShaderProfile;
        public GameObject[] MaterialSourceRoots => materialSourceRoots;
        public Material[] MaterialShaderTargets => materialShaderTargets;
        public string[] MaterialAssetFolders => materialAssetFolders;
        public GraphicGameViewScaleMode GameViewScaleMode => gameViewScaleMode;

        private void Reset()
        {
            targetCamera = Camera.main;
            targetRenderPipelineAsset = ResolveCurrentUniversalRenderPipelineAsset();
            builtInPostProcessResources = ResolveDefaultPostProcessResources();
        }

        private void Awake()
        {
            if (applyOnAwake)
            {
                ApplyNow();
            }
        }

        private void OnValidate()
        {
            msaaSampleCount = NormalizeMsaaSampleCount(msaaSampleCount);
            renderScale = Mathf.Clamp(renderScale, 0.1f, 2.0f);
            if (builtInPostProcessResources == null)
            {
                builtInPostProcessResources = ResolveDefaultPostProcessResources();
            }

            if (materialShaderProfile == null)
            {
                materialShaderProfile = new GraphicMaterialShaderProfile();
            }

            ApplySimplePresetValues();

            if (applyOnValidate && !Application.isPlaying)
            {
                ApplyNow();
            }
        }

        public void ApplyNow()
        {
            UniversalRenderPipelineAsset pipelineAsset = ResolveTargetPipelineAsset();
            ApplySimplePresetValues(pipelineAsset != null);

            Camera camera = ResolveTargetCamera();
            if (camera != null)
            {
                ApplyCameraSettings(camera, pipelineAsset != null);
            }

            if (pipelineAsset != null)
            {
                pipelineAsset.msaaSampleCount = NormalizeMsaaSampleCount(msaaSampleCount);
                pipelineAsset.renderScale = Mathf.Clamp(renderScale, 0.1f, 2.0f);
                return;
            }

            QualitySettings.antiAliasing = enableCameraMsaa ? NormalizeMsaaSampleCount(msaaSampleCount) : 0;
        }

        public GraphicTextureImportPlan CreateTextureImportPlan()
        {
            switch (textureResolution)
            {
                case GraphicSettingQualityPreset.Performance:
                    return new GraphicTextureImportPlan(
                        FilterMode.Bilinear,
                        4,
                        2048,
                        GraphicTextureCompressionPreference.HighQuality,
                        true);
                case GraphicSettingQualityPreset.Quality:
                    return new GraphicTextureImportPlan(
                        FilterMode.Trilinear,
                        16,
                        8192,
                        GraphicTextureCompressionPreference.None,
                        true);
                case GraphicSettingQualityPreset.Custom:
                    return textureImportProfile != null
                        ? textureImportProfile.CreatePlan()
                        : new GraphicTextureImportProfile().CreatePlan();
                default:
                    return new GraphicTextureImportPlan(
                        FilterMode.Trilinear,
                        8,
                        4096,
                        GraphicTextureCompressionPreference.HighQuality,
                        true);
            }
        }

        public GraphicMaterialShaderPlan CreateMaterialShaderPlan()
        {
            switch (modelEdgeAndAlpha)
            {
                case GraphicSettingQualityPreset.Performance:
                    return new GraphicMaterialShaderPlan(
                        false,
                        0f,
                        0f,
                        true,
                        0.35f,
                        GraphicMaterialSurfaceMode.Keep,
                        false);
                case GraphicSettingQualityPreset.Quality:
                    return new GraphicMaterialShaderPlan(
                        true,
                        0.00025f,
                        0.0002f,
                        true,
                        0.35f,
                        GraphicMaterialSurfaceMode.Keep,
                        true);
                case GraphicSettingQualityPreset.Custom:
                    return materialShaderProfile != null
                        ? materialShaderProfile.CreatePlan()
                        : new GraphicMaterialShaderProfile().CreatePlan();
                default:
                    return new GraphicMaterialShaderPlan(
                        true,
                        0.0005f,
                        0.00025f,
                        true,
                        0.35f,
                        GraphicMaterialSurfaceMode.Keep,
                        true);
            }
        }

        public GraphicMaterialShaderApplyResult ApplyMaterialShaderSettings()
        {
            if (materialShaderProfile == null)
            {
                materialShaderProfile = new GraphicMaterialShaderProfile();
            }

            GraphicMaterialShaderApplyResult result = GraphicMaterialShaderController.Apply(
                CreateMaterialShaderPlan(),
                CollectMaterialShaderTargets());
            Debug.Log($"GraphicSetting 모델 머티리얼 설정 적용: {result}");
            return result;
        }

        private IEnumerable<Material> CollectMaterialShaderTargets()
        {
            foreach (Material material in materialShaderTargets)
            {
                yield return material;
            }

            foreach (GameObject root in materialSourceRoots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        yield return material;
                    }
                }
            }
        }

        private void ApplySimplePresetValues()
        {
            ApplySimplePresetValues(ResolveTargetPipelineAsset() != null);
        }

        private void ApplySimplePresetValues(bool hasRenderScaleTarget)
        {
            ApplyAntiAliasingPreset();
            ApplyRenderSharpnessPreset(hasRenderScaleTarget);
        }

        private void ApplyAntiAliasingPreset()
        {
            switch (antiAliasingPreset)
            {
                case GraphicSettingQualityPreset.Performance:
                    antiAliasing = GraphicAntiAliasingMode.FXAA;
                    smaaQuality = AntialiasingQuality.Low;
                    enableCameraPostProcessing = true;
                    enableCameraMsaa = true;
                    msaaSampleCount = 2;
                    break;
                case GraphicSettingQualityPreset.Balanced:
                    antiAliasing = GraphicAntiAliasingMode.SMAA;
                    smaaQuality = AntialiasingQuality.Medium;
                    enableCameraPostProcessing = true;
                    enableCameraMsaa = true;
                    msaaSampleCount = 4;
                    break;
                case GraphicSettingQualityPreset.Quality:
                    antiAliasing = GraphicAntiAliasingMode.SMAA;
                    smaaQuality = AntialiasingQuality.High;
                    enableCameraPostProcessing = true;
                    enableCameraMsaa = true;
                    msaaSampleCount = 8;
                    break;
            }
        }

        private void ApplyRenderSharpnessPreset(bool hasRenderScaleTarget)
        {
            if (!hasRenderScaleTarget && renderSharpness != GraphicSettingQualityPreset.Custom)
            {
                renderScale = 1.0f;
                return;
            }

            switch (renderSharpness)
            {
                case GraphicSettingQualityPreset.Performance:
                    renderScale = 1.0f;
                    break;
                case GraphicSettingQualityPreset.Balanced:
                    renderScale = 1.25f;
                    break;
                case GraphicSettingQualityPreset.Quality:
                    renderScale = 1.5f;
                    break;
            }
        }

        private void ApplyCameraSettings(Camera camera, bool useUniversalRenderPipeline)
        {
            camera.allowMSAA = enableCameraMsaa;

            if (!useUniversalRenderPipeline)
            {
                ApplyBuiltInPostProcessSettings(camera);
                return;
            }

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = enableCameraPostProcessing;
            cameraData.antialiasing = ToUrpAntialiasingMode(antiAliasing);
            cameraData.antialiasingQuality = smaaQuality;
        }

        private void ApplyBuiltInPostProcessSettings(Camera camera)
        {
            PostProcessLayer layer = camera.GetComponent<PostProcessLayer>();
            if (layer == null)
            {
                layer = camera.gameObject.AddComponent<PostProcessLayer>();
            }

            layer.enabled = enableCameraPostProcessing && antiAliasing != GraphicAntiAliasingMode.Off;
            layer.volumeLayer = ~0;
            layer.antialiasingMode = ToBuiltInAntialiasingMode(antiAliasing);
            layer.subpixelMorphologicalAntialiasing.quality = ToBuiltInSmaaQuality(smaaQuality);

            PostProcessResources resources = ResolveBuiltInPostProcessResources();
            if (resources != null)
            {
                layer.Init(resources);
            }
        }

        private PostProcessResources ResolveBuiltInPostProcessResources()
        {
            return builtInPostProcessResources != null
                ? builtInPostProcessResources
                : ResolveDefaultPostProcessResources();
        }

        private Camera ResolveTargetCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            targetCamera = Camera.main;
            return targetCamera;
        }

        private UniversalRenderPipelineAsset ResolveTargetPipelineAsset()
        {
            if (targetRenderPipelineAsset != null)
            {
                return targetRenderPipelineAsset;
            }

            targetRenderPipelineAsset = ResolveCurrentUniversalRenderPipelineAsset();
            return targetRenderPipelineAsset;
        }

        private static UniversalRenderPipelineAsset ResolveCurrentUniversalRenderPipelineAsset()
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

        private static PostProcessResources ResolveDefaultPostProcessResources()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<PostProcessResources>(DefaultPostProcessResourcesPath);
#else
            return Resources.Load<PostProcessResources>("PostProcessResources");
#endif
        }

        private static AntialiasingMode ToUrpAntialiasingMode(GraphicAntiAliasingMode mode)
        {
            switch (mode)
            {
                case GraphicAntiAliasingMode.FXAA:
                    return AntialiasingMode.FastApproximateAntialiasing;
                case GraphicAntiAliasingMode.SMAA:
                    return AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                case GraphicAntiAliasingMode.TAA:
                    return AntialiasingMode.TemporalAntiAliasing;
                default:
                    return AntialiasingMode.None;
            }
        }

        private static PostProcessLayer.Antialiasing ToBuiltInAntialiasingMode(GraphicAntiAliasingMode mode)
        {
            switch (mode)
            {
                case GraphicAntiAliasingMode.FXAA:
                    return PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
                case GraphicAntiAliasingMode.SMAA:
                    return PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
                case GraphicAntiAliasingMode.TAA:
                    return PostProcessLayer.Antialiasing.TemporalAntialiasing;
                default:
                    return PostProcessLayer.Antialiasing.None;
            }
        }

        private static SubpixelMorphologicalAntialiasing.Quality ToBuiltInSmaaQuality(AntialiasingQuality quality)
        {
            switch (quality)
            {
                case AntialiasingQuality.Low:
                    return SubpixelMorphologicalAntialiasing.Quality.Low;
                case AntialiasingQuality.Medium:
                    return SubpixelMorphologicalAntialiasing.Quality.Medium;
                default:
                    return SubpixelMorphologicalAntialiasing.Quality.High;
            }
        }

        private static int NormalizeMsaaSampleCount(int samples)
        {
            if (samples >= 8)
            {
                return 8;
            }

            if (samples >= 4)
            {
                return 4;
            }

            if (samples >= 2)
            {
                return 2;
            }

            return 1;
        }
    }
}
