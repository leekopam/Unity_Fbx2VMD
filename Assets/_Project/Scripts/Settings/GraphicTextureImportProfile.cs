using System;
using UnityEngine;

namespace Fbx2Vmd.Settings
{
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
}
