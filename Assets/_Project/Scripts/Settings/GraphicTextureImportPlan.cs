using UnityEngine;

namespace Fbx2Vmd.Settings
{
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
}
