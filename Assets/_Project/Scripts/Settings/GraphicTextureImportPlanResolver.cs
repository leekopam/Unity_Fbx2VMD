using UnityEngine;

namespace Fbx2Vmd.Settings
{
    internal static class GraphicTextureImportPlanResolver
    {
        internal static GraphicTextureImportPlan Resolve(
            GraphicSettingQualityPreset preset,
            GraphicTextureImportProfile customProfile)
        {
            switch (preset)
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
                    return (customProfile ?? new GraphicTextureImportProfile()).CreatePlan();
                default:
                    return new GraphicTextureImportPlan(
                        FilterMode.Trilinear,
                        8,
                        4096,
                        GraphicTextureCompressionPreference.HighQuality,
                        true);
            }
        }
    }
}
