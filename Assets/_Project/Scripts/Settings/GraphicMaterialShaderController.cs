using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fbx2Vmd.Settings
{
public static class GraphicMaterialShaderController
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
}
