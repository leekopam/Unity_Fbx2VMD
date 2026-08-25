namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonRuntimeValueNormalizer
    {
        internal static float NormalizePositive(float value, float fallbackValue)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f
                ? fallbackValue
                : value;
        }

        internal static float NormalizeFinite(float value, float fallbackValue)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallbackValue
                : value;
        }
    }
}
