#if UNITY_EDITOR
using System;

namespace Fbx2Vmd.FBXImporter
{
    internal readonly struct VisualComparisonKeypointDelta
    {
        internal VisualComparisonKeypointDelta(
            int comparedKeypointCount,
            float meanL1Delta,
            float maxL1Delta)
        {
            ComparedKeypointCount = comparedKeypointCount;
            MeanL1Delta = meanL1Delta;
            MaxL1Delta = maxL1Delta;
        }

        internal int ComparedKeypointCount { get; }

        internal float MeanL1Delta { get; }

        internal float MaxL1Delta { get; }
    }

    internal readonly struct VisualComparisonBBoxNormalizedKeypointDelta
    {
        internal VisualComparisonBBoxNormalizedKeypointDelta(
            int comparedKeypointCount,
            int excludedKeypointCount,
            float meanL1Delta,
            float maxL1Delta,
            int maxKeypointIndex,
            float maxXDelta,
            float maxYDelta,
            float maxCandidateX,
            float maxCandidateY,
            float maxReferenceX,
            float maxReferenceY)
        {
            ComparedKeypointCount = comparedKeypointCount;
            ExcludedKeypointCount = excludedKeypointCount;
            MeanL1Delta = meanL1Delta;
            MaxL1Delta = maxL1Delta;
            MaxKeypointIndex = maxKeypointIndex;
            MaxXDelta = maxXDelta;
            MaxYDelta = maxYDelta;
            MaxCandidateX = maxCandidateX;
            MaxCandidateY = maxCandidateY;
            MaxReferenceX = maxReferenceX;
            MaxReferenceY = maxReferenceY;
        }

        internal int ComparedKeypointCount { get; }

        internal int ExcludedKeypointCount { get; }

        internal float MeanL1Delta { get; }

        internal float MaxL1Delta { get; }

        internal int MaxKeypointIndex { get; }

        internal float MaxXDelta { get; }

        internal float MaxYDelta { get; }

        internal float MaxCandidateX { get; }

        internal float MaxCandidateY { get; }

        internal float MaxReferenceX { get; }

        internal float MaxReferenceY { get; }
    }

    internal static class VisualComparisonKeypointDeltaCalculator
    {
        internal static bool TryCalculateBBoxNormalized(
            float[] candidateKeypoints,
            float candidateCenterX,
            float candidateBBoxWidth,
            float candidateBottomGap,
            float candidateBBoxHeight,
            float[] referenceKeypoints,
            float referenceCenterX,
            float referenceBBoxWidth,
            float referenceBottomGap,
            float referenceBBoxHeight,
            out VisualComparisonBBoxNormalizedKeypointDelta delta)
        {
            return TryCalculateBBoxNormalizedCore(
                candidateKeypoints,
                candidateCenterX,
                candidateBBoxWidth,
                candidateBottomGap,
                candidateBBoxHeight,
                float.NaN,
                referenceKeypoints,
                referenceCenterX,
                referenceBBoxWidth,
                referenceBottomGap,
                referenceBBoxHeight,
                float.NaN,
                float.NaN,
                excludeVerticalFrameEdges: false,
                out delta);
        }

        internal static bool TryCalculateCropSafeBBoxNormalized(
            float[] candidateKeypoints,
            float candidateCenterX,
            float candidateBBoxWidth,
            float candidateBottomGap,
            float candidateBBoxHeight,
            float candidateTopGap,
            float[] referenceKeypoints,
            float referenceCenterX,
            float referenceBBoxWidth,
            float referenceBottomGap,
            float referenceBBoxHeight,
            float referenceTopGap,
            float endpointPixelTolerance,
            out VisualComparisonBBoxNormalizedKeypointDelta delta)
        {
            return TryCalculateBBoxNormalizedCore(
                candidateKeypoints,
                candidateCenterX,
                candidateBBoxWidth,
                candidateBottomGap,
                candidateBBoxHeight,
                candidateTopGap,
                referenceKeypoints,
                referenceCenterX,
                referenceBBoxWidth,
                referenceBottomGap,
                referenceBBoxHeight,
                referenceTopGap,
                endpointPixelTolerance,
                excludeVerticalFrameEdges: true,
                out delta);
        }

        internal static bool TryCalculate(
            float[] candidateKeypoints,
            float[] referenceKeypoints,
            out VisualComparisonKeypointDelta delta)
        {
            delta = default;
            if (candidateKeypoints == null || referenceKeypoints == null)
            {
                return false;
            }

            int length = Math.Min(candidateKeypoints.Length, referenceKeypoints.Length);
            if (length <= 1)
            {
                return false;
            }

            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteKeypointCount = 0;
            for (int i = 0; i + 1 < length; i += 2)
            {
                float candidateX = candidateKeypoints[i];
                float candidateY = candidateKeypoints[i + 1];
                float referenceX = referenceKeypoints[i];
                float referenceY = referenceKeypoints[i + 1];
                if (!IsFinite(candidateX) ||
                    !IsFinite(candidateY) ||
                    !IsFinite(referenceX) ||
                    !IsFinite(referenceY))
                {
                    continue;
                }

                float l1Delta =
                    Math.Abs(candidateX - referenceX) +
                    Math.Abs(candidateY - referenceY);
                sumDelta += l1Delta;
                maxDelta = Math.Max(maxDelta, l1Delta);
                finiteKeypointCount++;
            }

            if (finiteKeypointCount <= 0)
            {
                return false;
            }

            delta = new VisualComparisonKeypointDelta(
                finiteKeypointCount,
                sumDelta / finiteKeypointCount,
                maxDelta);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryCalculateBBoxNormalizedCore(
            float[] candidateKeypoints,
            float candidateCenterX,
            float candidateBBoxWidth,
            float candidateBottomGap,
            float candidateBBoxHeight,
            float candidateTopGap,
            float[] referenceKeypoints,
            float referenceCenterX,
            float referenceBBoxWidth,
            float referenceBottomGap,
            float referenceBBoxHeight,
            float referenceTopGap,
            float endpointPixelTolerance,
            bool excludeVerticalFrameEdges,
            out VisualComparisonBBoxNormalizedKeypointDelta delta)
        {
            delta = default;
            if (candidateKeypoints == null ||
                referenceKeypoints == null ||
                !IsFinite(candidateCenterX) ||
                !IsFinite(candidateBBoxWidth) ||
                !IsFinite(candidateBottomGap) ||
                !IsFinite(candidateBBoxHeight) ||
                !IsFinite(referenceCenterX) ||
                !IsFinite(referenceBBoxWidth) ||
                !IsFinite(referenceBottomGap) ||
                !IsFinite(referenceBBoxHeight) ||
                candidateBBoxWidth <= 0f ||
                candidateBBoxHeight <= 0f ||
                referenceBBoxWidth <= 0f ||
                referenceBBoxHeight <= 0f ||
                (excludeVerticalFrameEdges &&
                 (!IsFinite(candidateTopGap) ||
                  !IsFinite(referenceTopGap) ||
                  !IsFinite(endpointPixelTolerance))))
            {
                return false;
            }

            int length = Math.Min(candidateKeypoints.Length, referenceKeypoints.Length);
            if (length <= 1)
            {
                return false;
            }

            int totalKeypointCount = length / 2;
            int bandCount = Math.Max(0, (totalKeypointCount - 2) / 2);
            float candidateLeft = candidateCenterX - (candidateBBoxWidth * 0.5f);
            float referenceLeft = referenceCenterX - (referenceBBoxWidth * 0.5f);
            float sumDelta = 0f;
            float maxDelta = 0f;
            int maxKeypointIndex = -1;
            float maxXDelta = float.NaN;
            float maxYDelta = float.NaN;
            float maxCandidateX = float.NaN;
            float maxCandidateY = float.NaN;
            float maxReferenceX = float.NaN;
            float maxReferenceY = float.NaN;
            int finiteKeypointCount = 0;
            int excludedKeypointCount = 0;
            for (int i = 0; i + 1 < length; i += 2)
            {
                int keypointIndex = i / 2;
                float candidateX = candidateKeypoints[i];
                float candidateY = candidateKeypoints[i + 1];
                float referenceX = referenceKeypoints[i];
                float referenceY = referenceKeypoints[i + 1];
                if (!IsFinite(candidateX) ||
                    !IsFinite(candidateY) ||
                    !IsFinite(referenceX) ||
                    !IsFinite(referenceY))
                {
                    continue;
                }

                if (excludeVerticalFrameEdges &&
                    (IsKeypointAffectedByVerticalFrameEdge(
                         keypointIndex,
                         bandCount,
                         referenceBottomGap,
                         referenceTopGap,
                         endpointPixelTolerance) ||
                     IsKeypointAffectedByVerticalFrameEdge(
                         keypointIndex,
                         bandCount,
                         candidateBottomGap,
                         candidateTopGap,
                         endpointPixelTolerance)))
                {
                    excludedKeypointCount++;
                    continue;
                }

                float candidateNormalizedX = (candidateX - candidateLeft) / candidateBBoxWidth;
                float candidateNormalizedY = (candidateY - candidateBottomGap) / candidateBBoxHeight;
                float referenceNormalizedX = (referenceX - referenceLeft) / referenceBBoxWidth;
                float referenceNormalizedY = (referenceY - referenceBottomGap) / referenceBBoxHeight;
                float xDelta = Math.Abs(candidateNormalizedX - referenceNormalizedX);
                float yDelta = Math.Abs(candidateNormalizedY - referenceNormalizedY);
                float l1Delta = xDelta + yDelta;
                sumDelta += l1Delta;
                if (l1Delta > maxDelta)
                {
                    maxDelta = l1Delta;
                    maxKeypointIndex = keypointIndex;
                    maxXDelta = xDelta;
                    maxYDelta = yDelta;
                    maxCandidateX = candidateNormalizedX;
                    maxCandidateY = candidateNormalizedY;
                    maxReferenceX = referenceNormalizedX;
                    maxReferenceY = referenceNormalizedY;
                }

                finiteKeypointCount++;
            }

            delta = new VisualComparisonBBoxNormalizedKeypointDelta(
                finiteKeypointCount,
                excludedKeypointCount,
                finiteKeypointCount > 0 ? sumDelta / finiteKeypointCount : float.NaN,
                finiteKeypointCount > 0 ? maxDelta : float.NaN,
                maxKeypointIndex,
                maxXDelta,
                maxYDelta,
                maxCandidateX,
                maxCandidateY,
                maxReferenceX,
                maxReferenceY);
            return finiteKeypointCount > 0;
        }

        private static bool IsKeypointAffectedByVerticalFrameEdge(
            int keypointIndex,
            int bandCount,
            float bottomGapRatio,
            float topGapRatio,
            float endpointPixelTolerance)
        {
            bool bottomTouched = IsFinite(bottomGapRatio) &&
                                 bottomGapRatio <= endpointPixelTolerance;
            bool topTouched = IsFinite(topGapRatio) &&
                              topGapRatio <= endpointPixelTolerance;
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
    }
}
#endif
