namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonFrameGeometryCalculator
    {
        private const float FrameEdgePixelTolerance = 0.001f;

        internal static bool IsFiniteMetric(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static float ResolveFrameTopGapRatio(
            float bottomGapRatio,
            float boundingBoxHeightRatio)
        {
            if (!IsFiniteMetric(bottomGapRatio) || !IsFiniteMetric(boundingBoxHeightRatio))
            {
                return float.NaN;
            }

            return System.Math.Max(0f, 1f - bottomGapRatio - boundingBoxHeightRatio);
        }

        internal static bool IsFrameEdgeTouched(float bottomGapRatio, float topGapRatio)
        {
            return (IsFiniteMetric(bottomGapRatio) &&
                    bottomGapRatio <= FrameEdgePixelTolerance) ||
                   (IsFiniteMetric(topGapRatio) &&
                    topGapRatio <= FrameEdgePixelTolerance);
        }
    }
}
