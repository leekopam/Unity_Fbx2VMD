#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using static Fbx2Vmd.FBXImporter.VisualComparisonFrameGeometryCalculator;

namespace Fbx2Vmd.FBXImporter
{
    internal readonly struct VisualComparisonTimeMatchedFramePair
    {
        internal VisualComparisonTimeMatchedFramePair(
            ReferenceMp4FrameMetricRow referenceRow,
            VisualComparisonCandidateFrameSample candidateSample,
            float secondsGap,
            float referenceTopGapRatio,
            bool referenceTouchesFrameEdge,
            bool candidateTouchesFrameEdge)
        {
            ReferenceRow = referenceRow;
            CandidateSample = candidateSample;
            SecondsGap = secondsGap;
            ReferenceTopGapRatio = referenceTopGapRatio;
            ReferenceTouchesFrameEdge = referenceTouchesFrameEdge;
            CandidateTouchesFrameEdge = candidateTouchesFrameEdge;
        }

        internal ReferenceMp4FrameMetricRow ReferenceRow { get; }

        internal VisualComparisonCandidateFrameSample CandidateSample { get; }

        internal float SecondsGap { get; }

        internal float ReferenceTopGapRatio { get; }

        internal bool ReferenceTouchesFrameEdge { get; }

        internal bool CandidateTouchesFrameEdge { get; }

        internal bool IsCropSafe => !ReferenceTouchesFrameEdge && !CandidateTouchesFrameEdge;
    }

    internal static class VisualComparisonTimeMatchedFramePairBuilder
    {
        internal static VisualComparisonTimeMatchedFramePair[] Build(
            IReadOnlyList<ReferenceMp4FrameMetricRow> referenceRows,
            IReadOnlyList<VisualComparisonCandidateFrameSample> candidateSamples,
            float clipStartSeconds,
            float clipDurationSeconds)
        {
            if (referenceRows == null || candidateSamples == null ||
                referenceRows.Count <= 0 || candidateSamples.Count <= 0)
            {
                return Array.Empty<VisualComparisonTimeMatchedFramePair>();
            }

            float normalizedClipStartSeconds = Math.Max(0f, clipStartSeconds);
            float normalizedClipDurationSeconds = Math.Max(0f, clipDurationSeconds);
            var pairs = new List<VisualComparisonTimeMatchedFramePair>(referenceRows.Count);
            foreach (ReferenceMp4FrameMetricRow referenceRow in referenceRows)
            {
                if (referenceRow == null || float.IsNaN(referenceRow.seconds))
                {
                    continue;
                }

                float referenceLocalSeconds = Math.Min(
                    normalizedClipDurationSeconds,
                    Math.Max(0f, referenceRow.seconds - normalizedClipStartSeconds));
                VisualComparisonCandidateFrameSample nearestSample = null;
                float nearestGap = float.PositiveInfinity;
                foreach (VisualComparisonCandidateFrameSample candidateSample in candidateSamples)
                {
                    if (candidateSample == null ||
                        candidateSample.Metric == null ||
                        !candidateSample.Metric.HasBrightPixels ||
                        float.IsNaN(candidateSample.Seconds))
                    {
                        continue;
                    }

                    float gap = Math.Abs(candidateSample.Seconds - referenceLocalSeconds);
                    if (gap < nearestGap)
                    {
                        nearestGap = gap;
                        nearestSample = candidateSample;
                    }
                }

                if (nearestSample == null || float.IsInfinity(nearestGap))
                {
                    continue;
                }

                float referenceTopGapRatio = ResolveFrameTopGapRatio(
                    referenceRow.bottomGapRatio,
                    referenceRow.bboxHeightRatio);
                bool referenceTouchesFrameEdge = IsFrameEdgeTouched(
                    referenceRow.bottomGapRatio,
                    referenceTopGapRatio);
                bool candidateTouchesFrameEdge = IsFrameEdgeTouched(
                    nearestSample.Metric.BottomGapRatio,
                    nearestSample.Metric.TopGapRatio);
                pairs.Add(new VisualComparisonTimeMatchedFramePair(
                    referenceRow,
                    nearestSample,
                    nearestGap,
                    referenceTopGapRatio,
                    referenceTouchesFrameEdge,
                    candidateTouchesFrameEdge));
            }

            return pairs.Count > 0
                ? pairs.ToArray()
                : Array.Empty<VisualComparisonTimeMatchedFramePair>();
        }
    }
}
#endif
