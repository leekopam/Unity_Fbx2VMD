#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class VisualComparisonCandidateFrameMetricAccumulator
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<VisualComparisonCandidateFrameSample> samples =
            new List<VisualComparisonCandidateFrameSample>();
        private int nonblankCount;
        private int limbSpanCount;
        private float sumBBoxHeightRatio;
        private float sumBBoxWidthRatio;
        private float sumUpperLimbSpanRatio;
        private float sumLowerLimbSpanRatio;
        private float sumBrightAreaRatio;
        private float maxBottomGapRatio;
        private float maxTopGapRatio;
        private float minCenterX = float.PositiveInfinity;
        private float maxCenterX = float.NegativeInfinity;

        internal void AddFrame(int recorderFrame, VisualComparisonCandidateFrameMetric metric)
        {
            if (metric == null)
            {
                throw new ArgumentNullException(nameof(metric));
            }

            samples.Add(new VisualComparisonCandidateFrameSample(recorderFrame, metric));
            sumBBoxHeightRatio += metric.BBoxHeightRatio;
            sumBBoxWidthRatio += metric.BBoxWidthRatio;
            sumBrightAreaRatio += metric.BrightAreaRatio;
            maxBottomGapRatio = Math.Max(maxBottomGapRatio, metric.BottomGapRatio);
            maxTopGapRatio = Math.Max(maxTopGapRatio, metric.TopGapRatio);

            if (IsFinite(metric.UpperLimbSpanRatio) && IsFinite(metric.LowerLimbSpanRatio))
            {
                sumUpperLimbSpanRatio += metric.UpperLimbSpanRatio;
                sumLowerLimbSpanRatio += metric.LowerLimbSpanRatio;
                limbSpanCount++;
            }

            if (!metric.HasBrightPixels)
            {
                return;
            }

            nonblankCount++;
            minCenterX = Math.Min(minCenterX, metric.CenterX);
            maxCenterX = Math.Max(maxCenterX, metric.CenterX);
        }

        internal void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                errors.Add(error);
            }
        }

        internal VisualComparisonCandidateFrameMetricSummary Build()
        {
            int sampleCount = samples.Count;
            return new VisualComparisonCandidateFrameMetricSummary(
                sampleCount,
                nonblankCount,
                DivideOrNaN(sumBBoxHeightRatio, sampleCount),
                DivideOrNaN(sumBBoxWidthRatio, sampleCount),
                DivideOrNaN(sumUpperLimbSpanRatio, limbSpanCount),
                DivideOrNaN(sumLowerLimbSpanRatio, limbSpanCount),
                nonblankCount > 0 ? maxCenterX - minCenterX : float.NaN,
                sampleCount > 0 ? maxBottomGapRatio : float.NaN,
                sampleCount > 0 ? maxTopGapRatio : float.NaN,
                DivideOrNaN(sumBrightAreaRatio, sampleCount),
                samples.ToArray(),
                string.Join("; ", errors));
        }

        private static float DivideOrNaN(float sum, int count)
        {
            return count > 0 ? sum / count : float.NaN;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal sealed class VisualComparisonCandidateFrameMetricSummary
    {
        internal VisualComparisonCandidateFrameMetricSummary(
            int sampleCount,
            int nonblankCount,
            float avgBBoxHeightRatio,
            float avgBBoxWidthRatio,
            float avgUpperLimbSpanRatio,
            float avgLowerLimbSpanRatio,
            float centerXRangeRatio,
            float maxBottomGapRatio,
            float maxTopGapRatio,
            float avgBrightAreaRatio,
            IReadOnlyList<VisualComparisonCandidateFrameSample> samples,
            string error)
        {
            SampleCount = sampleCount;
            NonblankCount = nonblankCount;
            AvgBBoxHeightRatio = avgBBoxHeightRatio;
            AvgBBoxWidthRatio = avgBBoxWidthRatio;
            AvgUpperLimbSpanRatio = avgUpperLimbSpanRatio;
            AvgLowerLimbSpanRatio = avgLowerLimbSpanRatio;
            CenterXRangeRatio = centerXRangeRatio;
            MaxBottomGapRatio = maxBottomGapRatio;
            MaxTopGapRatio = maxTopGapRatio;
            AvgBrightAreaRatio = avgBrightAreaRatio;
            Samples = samples ?? Array.Empty<VisualComparisonCandidateFrameSample>();
            Error = error ?? string.Empty;
        }

        internal int SampleCount { get; }
        internal int NonblankCount { get; }
        internal float AvgBBoxHeightRatio { get; }
        internal float AvgBBoxWidthRatio { get; }
        internal float AvgUpperLimbSpanRatio { get; }
        internal float AvgLowerLimbSpanRatio { get; }
        internal float CenterXRangeRatio { get; }
        internal float MaxBottomGapRatio { get; }
        internal float MaxTopGapRatio { get; }
        internal float AvgBrightAreaRatio { get; }
        internal IReadOnlyList<VisualComparisonCandidateFrameSample> Samples { get; }
        internal string Error { get; }
    }
}
#endif
