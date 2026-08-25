using System;
using System.Collections.Generic;

namespace Fbx2Vmd.FBXImporter
{
    internal static class ReferenceVideoSampleTimeExtractor
    {
        private const float BoundaryEpsilonSeconds = 0.0001f;
        private const float MinimumDurationSeconds = 0.1f;

        public static float[] ExtractLocalSeconds(
            ReferenceMp4FrameMetricRow[] rows,
            float startSeconds,
            float durationSeconds)
        {
            if (rows == null)
            {
                return Array.Empty<float>();
            }

            float safeStartSeconds = Math.Max(0f, startSeconds);
            float safeDurationSeconds = Math.Max(MinimumDurationSeconds, durationSeconds);
            float endSeconds = safeStartSeconds + safeDurationSeconds;
            List<float> localSampleSeconds = new List<float>();
            foreach (ReferenceMp4FrameMetricRow row in rows)
            {
                if (row == null ||
                    float.IsNaN(row.seconds) ||
                    float.IsInfinity(row.seconds) ||
                    row.seconds < safeStartSeconds - BoundaryEpsilonSeconds ||
                    row.seconds > endSeconds + BoundaryEpsilonSeconds)
                {
                    continue;
                }

                localSampleSeconds.Add(Math.Min(
                    safeDurationSeconds,
                    Math.Max(0f, row.seconds - safeStartSeconds)));
            }

            localSampleSeconds.Sort();
            return localSampleSeconds.ToArray();
        }
    }
}
