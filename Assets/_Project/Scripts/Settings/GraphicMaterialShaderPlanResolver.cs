namespace Fbx2Vmd.Settings
{
    internal static class GraphicMaterialShaderPlanResolver
    {
        internal static GraphicMaterialShaderPlan Resolve(
            GraphicSettingQualityPreset preset,
            GraphicMaterialShaderProfile customProfile)
        {
            switch (preset)
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
                    return (customProfile ?? new GraphicMaterialShaderProfile()).CreatePlan();
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
    }
}
