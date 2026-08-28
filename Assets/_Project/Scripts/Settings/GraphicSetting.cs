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
            msaaSampleCount =
                GraphicRenderPipelineSettingsApplier.NormalizeMsaaSampleCount(msaaSampleCount);
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
                GraphicAntiAliasingPlan plan = new GraphicAntiAliasingPlan(
                    antiAliasing,
                    smaaQuality,
                    enableCameraPostProcessing,
                    enableCameraMsaa,
                    msaaSampleCount);
                GraphicCameraSettingsApplier.Apply(
                    camera,
                    pipelineAsset != null,
                    plan,
                    ResolveBuiltInPostProcessResources);
            }

            GraphicRenderPipelineSettingsApplier.Apply(
                pipelineAsset,
                renderScale,
                enableCameraMsaa,
                msaaSampleCount);
        }

        public GraphicTextureImportPlan CreateTextureImportPlan()
        {
            return GraphicTextureImportPlanResolver.Resolve(
                textureResolution,
                textureImportProfile);
        }

        public GraphicMaterialShaderPlan CreateMaterialShaderPlan()
        {
            return GraphicMaterialShaderPlanResolver.Resolve(
                modelEdgeAndAlpha,
                materialShaderProfile);
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
            renderScale = GraphicRenderScalePresetResolver.Resolve(
                renderSharpness,
                hasRenderScaleTarget,
                renderScale);
        }

        private void ApplyAntiAliasingPreset()
        {
            GraphicAntiAliasingPlan customPlan = new GraphicAntiAliasingPlan(
                antiAliasing,
                smaaQuality,
                enableCameraPostProcessing,
                enableCameraMsaa,
                msaaSampleCount);
            GraphicAntiAliasingPlan plan = GraphicAntiAliasingPresetResolver.Resolve(
                antiAliasingPreset,
                customPlan);

            antiAliasing = plan.AntiAliasing;
            smaaQuality = plan.SmaaQuality;
            enableCameraPostProcessing = plan.EnableCameraPostProcessing;
            enableCameraMsaa = plan.EnableCameraMsaa;
            msaaSampleCount = plan.MsaaSampleCount;
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
    }
}
