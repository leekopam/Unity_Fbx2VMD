using System;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonMetricCalculator
    {
        private const float GroundingStepAtMaxRatioThreshold = 0.95f;

        internal static float ResolveGroundingStepToMaxRatio(
            float reportedRatio,
            float step,
            float maxStep)
        {
            if (IsFinite(reportedRatio))
            {
                return reportedRatio;
            }

            return IsFinite(step) && IsFinite(maxStep) && maxStep > 0f
                ? Math.Abs(step) / maxStep
                : float.NaN;
        }

        internal static bool IsGroundingStepAtMax(float stepToMaxRatio)
        {
            return IsFinite(stepToMaxRatio) &&
                stepToMaxRatio >= GroundingStepAtMaxRatioThreshold;
        }

        internal static int CalculateIntSpan(int first, int finish)
        {
            return first < 0 || finish < 0
                ? -1
                : finish - first;
        }

        internal static float CalculateFloatSpan(float first, float finish)
        {
            return IsFinite(first) && IsFinite(finish)
                ? finish - first
                : float.NaN;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
