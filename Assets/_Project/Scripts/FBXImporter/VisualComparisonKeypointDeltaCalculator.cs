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

    internal static class VisualComparisonKeypointDeltaCalculator
    {
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
    }
}
#endif
