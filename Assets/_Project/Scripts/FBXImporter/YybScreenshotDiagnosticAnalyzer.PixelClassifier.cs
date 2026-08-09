#if UNITY_EDITOR
using UnityEngine;
using static Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner;

namespace Fbx2Vmd.FBXImporter
{
    internal static partial class YybScreenshotDiagnosticAnalyzer
    {
        private static bool IsKeypointAffectedByVerticalFrameEdge(
            int keypointIndex,
            int bandCount,
            float bottomGapRatio,
            float topGapRatio)
        {
            bool bottomTouched = IsFiniteMetric(bottomGapRatio) &&
                                 bottomGapRatio <= ReferenceAlignedVisualEvidenceEndpointPixelTolerance;
            bool topTouched = IsFiniteMetric(topGapRatio) &&
                              topGapRatio <= ReferenceAlignedVisualEvidenceEndpointPixelTolerance;
            if (!bottomTouched && !topTouched)
            {
                return false;
            }

            if (keypointIndex == 0)
            {
                return bottomTouched;
            }

            if (keypointIndex == 1)
            {
                return topTouched;
            }

            int bandEndpointIndex = keypointIndex - 2;
            if (bandEndpointIndex < 0 || bandCount <= 0)
            {
                return bottomTouched || topTouched;
            }

            int bandIndex = bandEndpointIndex / 2;
            return (bottomTouched && bandIndex == 0) ||
                   (topTouched && bandIndex >= bandCount - 1);
        }

        private static bool IsCandidateBrightPixel(Color32 pixel)
        {
            float luminance =
                ((pixel.r * 0.2126f) + (pixel.g * 0.7152f) + (pixel.b * 0.0722f)) / 255f;
            return pixel.a > CandidateScreenshotOpaqueAlphaThreshold &&
                   luminance > CandidateScreenshotBrightLuminanceThreshold;
        }

        private static bool IsCandidateNonHairBrightPixel(Color32 pixel)
        {
            return IsCandidateBrightPixel(pixel) && !IsCandidateHairLikePixel(pixel);
        }

        private static bool IsCandidateHairLikePixel(Color32 pixel)
        {
            return IsCandidateCyanTealHairLikePixel(pixel) ||
                   IsCandidateDarkTealHairShadowPixel(pixel);
        }

        private static bool IsCandidateCyanTealHairLikePixel(Color32 pixel)
        {
            if (!IsCandidateBrightPixel(pixel))
            {
                return false;
            }

            return pixel.g >= 90 &&
                   pixel.b >= 90 &&
                   pixel.r <= 170 &&
                   pixel.g >= pixel.r * 1.15f &&
                   pixel.b >= pixel.r * 1.10f &&
                   Mathf.Abs(pixel.g - pixel.b) <= 100;
        }

        private static bool IsCandidateDarkTealHairShadowPixel(Color32 pixel)
        {
            if (!IsCandidateBrightPixel(pixel))
            {
                return false;
            }

            return pixel.r <= 80 &&
                   pixel.g >= 25 &&
                   pixel.b >= 25 &&
                   pixel.g >= pixel.r * 1.35f &&
                   pixel.b >= pixel.r * 1.35f &&
                   Mathf.Abs(pixel.g - pixel.b) <= 80;
        }
    }
}
#endif
