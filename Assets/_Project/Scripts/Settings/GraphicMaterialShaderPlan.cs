using UnityEngine;

namespace Fbx2Vmd.Settings
{
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
}
