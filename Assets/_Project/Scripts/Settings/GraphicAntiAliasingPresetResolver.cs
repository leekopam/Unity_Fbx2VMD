using UnityEngine.Rendering.Universal;

namespace Fbx2Vmd.Settings
{
    internal static class GraphicAntiAliasingPresetResolver
    {
        internal static GraphicAntiAliasingPlan Resolve(
            GraphicSettingQualityPreset preset,
            GraphicAntiAliasingPlan customPlan)
        {
            switch (preset)
            {
                case GraphicSettingQualityPreset.Performance:
                    return new GraphicAntiAliasingPlan(
                        GraphicAntiAliasingMode.FXAA,
                        AntialiasingQuality.Low,
                        true,
                        true,
                        2);
                case GraphicSettingQualityPreset.Balanced:
                    return new GraphicAntiAliasingPlan(
                        GraphicAntiAliasingMode.SMAA,
                        AntialiasingQuality.Medium,
                        true,
                        true,
                        4);
                case GraphicSettingQualityPreset.Quality:
                    return new GraphicAntiAliasingPlan(
                        GraphicAntiAliasingMode.SMAA,
                        AntialiasingQuality.High,
                        true,
                        true,
                        8);
                default:
                    return customPlan;
            }
        }
    }
}
