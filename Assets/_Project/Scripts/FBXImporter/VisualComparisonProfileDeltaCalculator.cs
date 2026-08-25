#if UNITY_EDITOR
using System;

namespace Fbx2Vmd.FBXImporter
{
    internal readonly struct VisualComparisonProfileDelta
    {
        internal VisualComparisonProfileDelta(
            int comparedValueCount,
            float meanAbsoluteDelta,
            float maxAbsoluteDelta)
        {
            ComparedValueCount = comparedValueCount;
            MeanAbsoluteDelta = meanAbsoluteDelta;
            MaxAbsoluteDelta = maxAbsoluteDelta;
        }

        internal int ComparedValueCount { get; }

        internal float MeanAbsoluteDelta { get; }

        internal float MaxAbsoluteDelta { get; }
    }

    internal static class VisualComparisonProfileDeltaCalculator
    {
        internal static bool TryCalculatePaired(
            float[] candidateProfile,
            float[] referenceProfile,
            out VisualComparisonProfileDelta delta)
        {
            if (candidateProfile == null || referenceProfile == null ||
                Math.Min(candidateProfile.Length, referenceProfile.Length) <= 1)
            {
                delta = default;
                return false;
            }

            return TryCalculate(candidateProfile, referenceProfile, out delta);
        }

        internal static bool TryCalculate(
            float[] candidateProfile,
            float[] referenceProfile,
            out VisualComparisonProfileDelta delta)
        {
            delta = default;
            if (candidateProfile == null || referenceProfile == null)
            {
                return false;
            }

            int length = Math.Min(candidateProfile.Length, referenceProfile.Length);
            float sumDelta = 0f;
            float maxDelta = 0f;
            int finiteCount = 0;
            for (int i = 0; i < length; i++)
            {
                float candidate = candidateProfile[i];
                float reference = referenceProfile[i];
                if (float.IsNaN(candidate) || float.IsInfinity(candidate) ||
                    float.IsNaN(reference) || float.IsInfinity(reference))
                {
                    continue;
                }

                float absoluteDelta = Math.Abs(candidate - reference);
                sumDelta += absoluteDelta;
                maxDelta = Math.Max(maxDelta, absoluteDelta);
                finiteCount++;
            }

            if (finiteCount <= 0)
            {
                return false;
            }

            delta = new VisualComparisonProfileDelta(
                finiteCount,
                sumDelta / finiteCount,
                maxDelta);
            return true;
        }
    }
}
#endif
