using System.Globalization;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonSummaryValueFormatter
    {
        internal static string FormatFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? "n/a"
                : value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        internal static string FormatEnabledWeight(bool enabled, float weight)
        {
            return $"{enabled}/{FormatFloat(weight)}";
        }

        internal static string FormatEnabledWeightCap(bool enabled, float weight, float cap)
        {
            return $"{FormatEnabledWeight(enabled, weight)}/{FormatFloat(cap)}";
        }

        internal static string FormatEnabledWeightCapScale(
            bool enabled,
            float weight,
            float cap,
            float scale)
        {
            return $"{FormatEnabledWeightCap(enabled, weight, cap)}/{FormatFloat(scale)}";
        }

        internal static string FormatEnabledWeightCapScaleGate(
            bool enabled,
            float weight,
            float cap,
            float scale,
            float frameGateStart,
            float frameGateEnd)
        {
            return $"{FormatEnabledWeightCapScale(enabled, weight, cap, scale)}/" +
                $"{FormatFloat(frameGateStart)}-{FormatFloat(frameGateEnd)}";
        }

        internal static string FormatEnabledWeightCapScaleBlendGate(
            bool enabled,
            float weight,
            float cap,
            float scale,
            float blend,
            float frameGateStart,
            float frameGateEnd)
        {
            return $"{FormatEnabledWeightCapScale(enabled, weight, cap, scale)}/" +
                $"blend:{FormatFloat(blend)}/" +
                $"{FormatFloat(frameGateStart)}-{FormatFloat(frameGateEnd)}";
        }
    }
}
