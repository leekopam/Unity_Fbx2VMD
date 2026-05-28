using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering.Universal;

namespace Member_Han.Modules.Graphics
{
    public enum GraphicAntiAliasingMode
    {
        Off,
        FXAA,
        SMAA,
        TAA
    }

    public enum GraphicGameViewScaleMode
    {
        Fit,
        OneX
    }

    public enum GraphicTextureCompressionPreference
    {
        Keep,
        None,
        HighQuality
    }

    public enum GraphicSettingQualityPreset
    {
        Performance,
        Balanced,
        Quality,
        Custom
    }

    public enum GraphicCaptureQualityPreset
    {
        Basic,
        HighQuality,
        UltraQuality,
        Custom
    }

    public enum GraphicMaterialSurfaceMode
    {
        Keep,
        Opaque,
        Cutout,
        Fade,
        Transparent
    }

    public readonly struct GraphicTextureImportPlan
    {
        public GraphicTextureImportPlan(
            FilterMode filterMode,
            int anisoLevel,
            int maxTextureSize,
            GraphicTextureCompressionPreference compression,
            bool alphaIsTransparency)
        {
            FilterMode = filterMode;
            AnisoLevel = Mathf.Clamp(anisoLevel, 0, 16);
            MaxTextureSize = Mathf.Clamp(maxTextureSize, 32, 16384);
            Compression = compression;
            AlphaIsTransparency = alphaIsTransparency;
        }

        public FilterMode FilterMode { get; }
        public int AnisoLevel { get; }
        public int MaxTextureSize { get; }
        public GraphicTextureCompressionPreference Compression { get; }
        public bool AlphaIsTransparency { get; }
    }

    public readonly struct GraphicMaterialShaderPlan
    {
        public GraphicMaterialShaderPlan(
            bool applyOutline,
            float outlineScale,
            float outlineSize,
            bool applyAlphaCutoff,
            float alphaCutoff,
            GraphicMaterialSurfaceMode surfaceMode,
            bool enableAlphaToCoverage)
        {
            ApplyOutline = applyOutline;
            OutlineScale = Mathf.Max(0f, outlineScale);
            OutlineSize = Mathf.Max(0f, outlineSize);
            ApplyAlphaCutoff = applyAlphaCutoff;
            AlphaCutoff = Mathf.Clamp01(alphaCutoff);
            SurfaceMode = surfaceMode;
            EnableAlphaToCoverage = enableAlphaToCoverage;
        }

        public bool ApplyOutline { get; }
        public float OutlineScale { get; }
        public float OutlineSize { get; }
        public bool ApplyAlphaCutoff { get; }
        public float AlphaCutoff { get; }
        public GraphicMaterialSurfaceMode SurfaceMode { get; }
        public bool EnableAlphaToCoverage { get; }
    }

    public readonly struct GraphicMaterialShaderApplyResult
    {
        public GraphicMaterialShaderApplyResult(
            int processedMaterials,
            int changedMaterials,
            int changedProperties,
            int skippedMaterials,
            int skippedProperties)
        {
            ProcessedMaterials = processedMaterials;
            ChangedMaterials = changedMaterials;
            ChangedProperties = changedProperties;
            SkippedMaterials = skippedMaterials;
            SkippedProperties = skippedProperties;
        }

        public int ProcessedMaterials { get; }
        public int ChangedMaterials { get; }
        public int ChangedProperties { get; }
        public int SkippedMaterials { get; }
        public int SkippedProperties { get; }

        public override string ToString()
        {
            return
                $"processed={ProcessedMaterials}, changedMaterials={ChangedMaterials}, changedProperties={ChangedProperties}, skippedMaterials={SkippedMaterials}, skippedProperties={SkippedProperties}";
        }
    }

    [Serializable]
    public sealed class GraphicTextureImportProfile
    {
        [SerializeField] private FilterMode filterMode = FilterMode.Trilinear;
        [SerializeField, Range(0, 16)] private int anisoLevel = 8;
        [SerializeField] private int maxTextureSize = 4096;
        [SerializeField] private GraphicTextureCompressionPreference compression = GraphicTextureCompressionPreference.HighQuality;
        [SerializeField] private bool alphaIsTransparency = true;

        public GraphicTextureImportPlan CreatePlan()
        {
            return new GraphicTextureImportPlan(
                filterMode,
                anisoLevel,
                maxTextureSize,
                compression,
                alphaIsTransparency);
        }
    }

    [Serializable]
    public sealed class GraphicMaterialShaderProfile
    {
        [SerializeField] private bool applyOutline = true;
        [SerializeField, UnityEngine.Min(0f)] private float outlineScale = 0.0005f;
        [SerializeField, UnityEngine.Min(0f)] private float outlineSize = 0.00025f;
        [SerializeField] private bool applyAlphaCutoff = true;
        [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.35f;
        [SerializeField] private GraphicMaterialSurfaceMode surfaceMode = GraphicMaterialSurfaceMode.Keep;
        [SerializeField] private bool enableAlphaToCoverage = true;

        public GraphicMaterialShaderPlan CreatePlan()
        {
            return new GraphicMaterialShaderPlan(
                applyOutline,
                outlineScale,
                outlineSize,
                applyAlphaCutoff,
                alphaCutoff,
                surfaceMode,
                enableAlphaToCoverage);
        }
    }

    public static class GraphicMaterialShaderUtility
    {
        private const string EdgeScaleProperty = "_EdgeScale";
        private const string EdgeSizeProperty = "_EdgeSize";
        private const string CutoffProperty = "_Cutoff";
        private const string ModeProperty = "_Mode";
        private const string SrcBlendProperty = "_SrcBlend";
        private const string DstBlendProperty = "_DstBlend";
        private const string ZWriteProperty = "_ZWrite";

        private static readonly string[] AlphaToCoverageProperties =
        {
            "_AlphaToMask",
            "_AlphaToCoverage",
            "_AlphaToMaskOn"
        };

        public static GraphicMaterialShaderApplyResult Apply(
            GraphicMaterialShaderPlan plan,
            IEnumerable<Material> materials)
        {
            if (materials == null)
            {
                return new GraphicMaterialShaderApplyResult(0, 0, 0, 0, 0);
            }

            int processedMaterials = 0;
            int changedMaterials = 0;
            int changedProperties = 0;
            int skippedMaterials = 0;
            int skippedProperties = 0;
            var seen = new HashSet<Material>();

            foreach (Material material in materials)
            {
                if (material == null || material.shader == null || !seen.Add(material))
                {
                    skippedMaterials++;
                    continue;
                }

                processedMaterials++;
                int changedBeforeMaterial = changedProperties;

                if (plan.ApplyOutline)
                {
                    SetFloatIfSupported(material, EdgeScaleProperty, plan.OutlineScale, ref changedProperties, ref skippedProperties);
                    SetFloatIfSupported(material, EdgeSizeProperty, plan.OutlineSize, ref changedProperties, ref skippedProperties);
                }

                if (plan.ApplyAlphaCutoff)
                {
                    SetFloatIfSupported(material, CutoffProperty, plan.AlphaCutoff, ref changedProperties, ref skippedProperties);
                }

                if (plan.SurfaceMode != GraphicMaterialSurfaceMode.Keep)
                {
                    ApplySurfaceMode(material, plan.SurfaceMode, ref changedProperties, ref skippedProperties);
                }

                if (plan.EnableAlphaToCoverage)
                {
                    SetAlphaToCoverageIfSupported(material, ref changedProperties, ref skippedProperties);
                }

                if (changedProperties > changedBeforeMaterial)
                {
                    changedMaterials++;
                }
            }

            return new GraphicMaterialShaderApplyResult(
                processedMaterials,
                changedMaterials,
                changedProperties,
                skippedMaterials,
                skippedProperties);
        }

        private static void ApplySurfaceMode(
            Material material,
            GraphicMaterialSurfaceMode surfaceMode,
            ref int changedProperties,
            ref int skippedProperties)
        {
            SurfaceState state = ToSurfaceState(surfaceMode);
            bool supportsSurfaceState =
                material.HasProperty(ModeProperty) ||
                material.HasProperty(SrcBlendProperty) ||
                material.HasProperty(DstBlendProperty) ||
                material.HasProperty(ZWriteProperty);

            SetFloatIfSupported(material, ModeProperty, state.Mode, ref changedProperties, ref skippedProperties);
            SetFloatIfSupported(material, SrcBlendProperty, state.SrcBlend, ref changedProperties, ref skippedProperties);
            SetFloatIfSupported(material, DstBlendProperty, state.DstBlend, ref changedProperties, ref skippedProperties);
            SetFloatIfSupported(material, ZWriteProperty, state.ZWrite, ref changedProperties, ref skippedProperties);

            if (!supportsSurfaceState)
            {
                return;
            }

            material.renderQueue = state.RenderQueue;
            material.SetOverrideTag("RenderType", state.RenderType);
            ConfigureSurfaceKeywords(material, surfaceMode);
        }

        private static void SetFloatIfSupported(
            Material material,
            string propertyName,
            float value,
            ref int changedProperties,
            ref int skippedProperties)
        {
            if (!material.HasProperty(propertyName))
            {
                skippedProperties++;
                return;
            }

            if (Mathf.Approximately(material.GetFloat(propertyName), value))
            {
                return;
            }

            material.SetFloat(propertyName, value);
            changedProperties++;
        }

        private static void SetAlphaToCoverageIfSupported(
            Material material,
            ref int changedProperties,
            ref int skippedProperties)
        {
            bool supported = false;
            foreach (string propertyName in AlphaToCoverageProperties)
            {
                if (!material.HasProperty(propertyName))
                {
                    continue;
                }

                supported = true;
                SetFloatIfSupported(material, propertyName, 1f, ref changedProperties, ref skippedProperties);
            }

            if (!supported)
            {
                skippedProperties++;
            }
        }

        private static SurfaceState ToSurfaceState(GraphicMaterialSurfaceMode surfaceMode)
        {
            switch (surfaceMode)
            {
                case GraphicMaterialSurfaceMode.Opaque:
                    return new SurfaceState(
                        0f,
                        (float)BlendMode.One,
                        (float)BlendMode.Zero,
                        1f,
                        -1,
                        string.Empty);
                case GraphicMaterialSurfaceMode.Cutout:
                    return new SurfaceState(
                        1f,
                        (float)BlendMode.One,
                        (float)BlendMode.Zero,
                        1f,
                        (int)RenderQueue.AlphaTest,
                        "TransparentCutout");
                case GraphicMaterialSurfaceMode.Fade:
                    return new SurfaceState(
                        2f,
                        (float)BlendMode.SrcAlpha,
                        (float)BlendMode.OneMinusSrcAlpha,
                        0f,
                        (int)RenderQueue.Transparent,
                        "Transparent");
                case GraphicMaterialSurfaceMode.Transparent:
                    return new SurfaceState(
                        3f,
                        (float)BlendMode.One,
                        (float)BlendMode.OneMinusSrcAlpha,
                        0f,
                        (int)RenderQueue.Transparent,
                        "Transparent");
                default:
                    return new SurfaceState(0f, 0f, 0f, 0f, -1, string.Empty);
            }
        }

        private static void ConfigureSurfaceKeywords(Material material, GraphicMaterialSurfaceMode surfaceMode)
        {
            SetKeyword(material, "_ALPHATEST_ON", surfaceMode == GraphicMaterialSurfaceMode.Cutout);
            SetKeyword(material, "_ALPHABLEND_ON", surfaceMode == GraphicMaterialSurfaceMode.Fade);
            SetKeyword(material, "_ALPHAPREMULTIPLY_ON", surfaceMode == GraphicMaterialSurfaceMode.Transparent);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
                return;
            }

            material.DisableKeyword(keyword);
        }

        private readonly struct SurfaceState
        {
            public SurfaceState(
                float mode,
                float srcBlend,
                float dstBlend,
                float zWrite,
                int renderQueue,
                string renderType)
            {
                Mode = mode;
                SrcBlend = srcBlend;
                DstBlend = dstBlend;
                ZWrite = zWrite;
                RenderQueue = renderQueue;
                RenderType = renderType;
            }

            public float Mode { get; }
            public float SrcBlend { get; }
            public float DstBlend { get; }
            public float ZWrite { get; }
            public int RenderQueue { get; }
            public string RenderType { get; }
        }
    }

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
        [SerializeField] private GraphicCaptureQualityPreset captureQuality = GraphicCaptureQualityPreset.HighQuality;

        [Header("Anti Aliasing")]
        [SerializeField] private GraphicAntiAliasingMode antiAliasing = GraphicAntiAliasingMode.SMAA;
        [SerializeField] private AntialiasingQuality smaaQuality = AntialiasingQuality.High;
        [SerializeField] private bool enableCameraPostProcessing = true;
        [SerializeField] private bool enableCameraMsaa = true;
        [SerializeField] private int msaaSampleCount = 8;
        [SerializeField, Range(0.1f, 2.0f)] private float renderScale = 1.0f;

        [Header("Capture")]
        [SerializeField] private int captureSuperSize = 2;
        [SerializeField] private string captureFolder = "Docs/Machine_Spirit/Local/GraphicsCaptures";
        [SerializeField] private string captureFilePrefix = "graphic-setting";

        [Header("Camera Background")]
        [SerializeField] private bool applyBackgroundColor;
        [SerializeField] private Color backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("Editor GameView")]
        [SerializeField] private GraphicGameViewScaleMode gameViewScaleMode = GraphicGameViewScaleMode.Fit;

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
            captureSuperSize = Mathf.Clamp(captureSuperSize, 1, 8);
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

        public string CaptureSupersampledScreenshot()
        {
            ApplySimplePresetValues(ResolveTargetPipelineAsset() != null);

            string path = BuildCapturePath(DateTime.Now);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path, Mathf.Clamp(captureSuperSize, 1, 8));
            return path;
        }

        public string BuildCapturePath(DateTime timestamp)
        {
            string safePrefix = string.IsNullOrWhiteSpace(captureFilePrefix) ? "graphic-setting" : captureFilePrefix.Trim();
            string fileName = $"{safePrefix}-{timestamp:yyyyMMdd-HHmmss}-x{Mathf.Clamp(captureSuperSize, 1, 8)}.png";
            return Path.GetFullPath(Path.Combine(captureFolder, fileName));
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

            GraphicMaterialShaderApplyResult result = GraphicMaterialShaderUtility.Apply(
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
            ApplyCaptureQualityPreset();
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

        private void ApplyCaptureQualityPreset()
        {
            switch (captureQuality)
            {
                case GraphicCaptureQualityPreset.Basic:
                    captureSuperSize = 1;
                    break;
                case GraphicCaptureQualityPreset.HighQuality:
                    captureSuperSize = 2;
                    break;
                case GraphicCaptureQualityPreset.UltraQuality:
                    captureSuperSize = 4;
                    break;
            }
        }

        private void ApplyCameraSettings(Camera camera, bool useUniversalRenderPipeline)
        {
            camera.allowMSAA = enableCameraMsaa;
            if (applyBackgroundColor)
            {
                camera.backgroundColor = backgroundColor;
            }

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
