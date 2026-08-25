using System;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonScreenshotOverridePolicy
    {
        private const float MinimumPadding = 0.25f;
        private const float MaximumPadding = 2f;

        internal static bool HasFiniteFramingOverride(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static int NormalizeCaptureDimension(int value, int noOverrideValue)
        {
            return value > 0 ? value : noOverrideValue;
        }

        internal static float NormalizePadding(float value, float noOverrideValue)
        {
            if (!HasFiniteFramingOverride(value) || value <= 0f)
            {
                return noOverrideValue;
            }

            return Math.Max(MinimumPadding, Math.Min(MaximumPadding, value));
        }

        internal static float NormalizeVerticalViewportCenter(float value, float noOverrideValue)
        {
            if (!HasFiniteFramingOverride(value))
            {
                return noOverrideValue;
            }

            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
