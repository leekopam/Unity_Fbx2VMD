using System;
using UnityEngine;

namespace Fbx2Vmd.Settings
{
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
}
