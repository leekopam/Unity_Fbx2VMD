using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class ReferenceAlignedSampleTimePlanner
    {
        internal static float[] Build(
            float referenceClipStartSeconds,
            float requestedDurationSeconds,
            IEnumerable<float> defaultLocalSampleSeconds,
            IEnumerable<float> referenceLocalSampleSeconds,
            float candidateClipSecondsPerReferenceSecond,
            float frameRate)
        {
            float safeStart = Mathf.Max(0f, referenceClipStartSeconds);
            float safeDuration = Mathf.Max(0.1f, requestedDurationSeconds);
            float localSampleScale =
                candidateClipSecondsPerReferenceSecond <= 0f ||
                float.IsNaN(candidateClipSecondsPerReferenceSecond) ||
                float.IsInfinity(candidateClipSecondsPerReferenceSecond)
                    ? 1f
                    : candidateClipSecondsPerReferenceSecond;
            var absoluteSampleTimes = new List<float>();
            AddLocalSamples(
                absoluteSampleTimes,
                safeStart,
                safeDuration,
                defaultLocalSampleSeconds,
                localSampleScale);
            AddLocalSamples(
                absoluteSampleTimes,
                safeStart,
                safeDuration,
                referenceLocalSampleSeconds,
                localSampleScale);

            if (absoluteSampleTimes.Count <= 0)
            {
                return Array.Empty<float>();
            }

            absoluteSampleTimes.Sort();
            var deduplicated = new List<float>(absoluteSampleTimes.Count);
            float dedupeSeconds = (0.5f / Mathf.Max(0.0001f, frameRate)) + 0.0001f;
            foreach (float sampleTime in absoluteSampleTimes)
            {
                if (deduplicated.Count > 0 &&
                    Mathf.Abs(deduplicated[deduplicated.Count - 1] - sampleTime) <= dedupeSeconds)
                {
                    deduplicated[deduplicated.Count - 1] = sampleTime;
                    continue;
                }

                deduplicated.Add(sampleTime);
            }

            return deduplicated.ToArray();
        }

        private static void AddLocalSamples(
            List<float> absoluteSampleTimes,
            float referenceClipStartSeconds,
            float requestedDurationSeconds,
            IEnumerable<float> localSampleSeconds,
            float localSampleScale)
        {
            if (absoluteSampleTimes == null || localSampleSeconds == null)
            {
                return;
            }

            const float epsilonSeconds = 0.0001f;
            foreach (float localSampleSecond in localSampleSeconds)
            {
                if (float.IsNaN(localSampleSecond) ||
                    float.IsInfinity(localSampleSecond) ||
                    localSampleSecond < -epsilonSeconds ||
                    localSampleSecond > requestedDurationSeconds + epsilonSeconds)
                {
                    continue;
                }

                absoluteSampleTimes.Add(referenceClipStartSeconds + (Mathf.Clamp(
                    localSampleSecond,
                    0f,
                    requestedDurationSeconds) * Mathf.Max(0.0001f, localSampleScale)));
            }
        }
    }
}
