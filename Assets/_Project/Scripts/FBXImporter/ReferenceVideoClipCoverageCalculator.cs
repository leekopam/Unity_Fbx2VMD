using System;
using System.Collections.Generic;

namespace Fbx2Vmd.FBXImporter
{
    internal static class ReferenceVideoClipCoverageCalculator
    {
        private const float BoundaryEpsilonSeconds = 0.0001f;

        internal static ReferenceVideoClipCoverageData Calculate(
            ReferenceMp4FrameMetricRow[] rows,
            float startSeconds,
            float durationSeconds)
        {
            float safeStartSeconds = Math.Max(0f, startSeconds);
            float safeDurationSeconds = Math.Max(0f, durationSeconds);
            ReferenceVideoClipCoverageData data = new ReferenceVideoClipCoverageData
            {
                EndSeconds = safeStartSeconds + safeDurationSeconds,
                SampleGapSeconds = safeDurationSeconds
            };
            if (rows == null || safeDurationSeconds <= 0f)
            {
                return data;
            }

            float endSeconds = data.EndSeconds;
            float firstSeconds = float.PositiveInfinity;
            float lastSeconds = float.NegativeInfinity;
            float sumBBoxHeight = 0f;
            float sumBBoxWidth = 0f;
            float sumBrightArea = 0f;
            float maxBottomGap = float.NegativeInfinity;
            float minCenterX = float.PositiveInfinity;
            float maxCenterX = float.NegativeInfinity;
            List<float> sampleSeconds = new List<float>();
            List<ReferenceMp4FrameMetricRow> selectedRows =
                new List<ReferenceMp4FrameMetricRow>();

            foreach (ReferenceMp4FrameMetricRow row in rows)
            {
                if (row == null ||
                    float.IsNaN(row.seconds) ||
                    row.seconds < safeStartSeconds - BoundaryEpsilonSeconds ||
                    row.seconds > endSeconds + BoundaryEpsilonSeconds)
                {
                    continue;
                }

                float localSeconds = Math.Min(
                    safeDurationSeconds,
                    Math.Max(0f, row.seconds - safeStartSeconds));
                selectedRows.Add(row);
                sampleSeconds.Add(localSeconds);
                firstSeconds = Math.Min(firstSeconds, localSeconds);
                lastSeconds = Math.Max(lastSeconds, localSeconds);
                sumBBoxHeight += row.bboxHeightRatio;
                sumBBoxWidth += row.bboxWidthRatio;
                sumBrightArea += row.brightAreaRatio;
                maxBottomGap = Math.Max(maxBottomGap, row.bottomGapRatio);
                minCenterX = Math.Min(minCenterX, row.centerXRatio);
                maxCenterX = Math.Max(maxCenterX, row.centerXRatio);
            }

            data.Rows = selectedRows.ToArray();
            data.SampleSeconds = sampleSeconds.ToArray();
            data.SampleCount = data.Rows.Length;
            if (data.SampleCount <= 0)
            {
                return data;
            }

            data.FirstSampleSeconds = firstSeconds;
            data.LastSampleSeconds = lastSeconds;
            data.SampleCoverageRatio = Clamp01(lastSeconds / safeDurationSeconds);
            data.SampleGapSeconds = Math.Max(0f, safeDurationSeconds - lastSeconds);
            data.AverageBBoxHeightRatio = sumBBoxHeight / data.SampleCount;
            data.AverageBBoxWidthRatio = sumBBoxWidth / data.SampleCount;
            data.CenterXRangeRatio = maxCenterX - minCenterX;
            data.MaxBottomGapRatio = maxBottomGap;
            data.AverageBrightAreaRatio = sumBrightArea / data.SampleCount;
            return data;
        }

        private static float Clamp01(float value)
        {
            return Math.Min(1f, Math.Max(0f, value));
        }
    }
}
