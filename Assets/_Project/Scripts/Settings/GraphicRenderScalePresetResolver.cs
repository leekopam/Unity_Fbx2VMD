namespace Fbx2Vmd.Settings
{
    internal static class GraphicRenderScalePresetResolver
    {
        internal static float Resolve(
            GraphicSettingQualityPreset preset,
            bool hasRenderScaleTarget,
            float customRenderScale)
        {
            if (!hasRenderScaleTarget && preset != GraphicSettingQualityPreset.Custom)
            {
                return 1.0f;
            }

            switch (preset)
            {
                case GraphicSettingQualityPreset.Performance:
                    return 1.0f;
                case GraphicSettingQualityPreset.Balanced:
                    return 1.25f;
                case GraphicSettingQualityPreset.Quality:
                    return 1.5f;
                default:
                    return customRenderScale;
            }
        }
    }
}
