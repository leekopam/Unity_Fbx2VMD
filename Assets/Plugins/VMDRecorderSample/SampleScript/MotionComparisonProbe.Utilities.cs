using System;
using System.Collections.Generic;
using UnityEngine;

public partial class MotionComparisonProbe
{
    private static float NormalizeScreenshotPadding(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return DefaultScreenshotPadding;
        }

        return Mathf.Clamp(value, MinScreenshotPadding, MaxScreenshotPadding);
    }

    private static float NormalizeScreenshotVerticalViewportCenter(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return DefaultScreenshotVerticalViewportCenter;
        }

        return Mathf.Clamp(value, MinScreenshotVerticalViewportCenter, MaxScreenshotVerticalViewportCenter);
    }

    private static float[] NormalizeSampleTimes(IEnumerable<float> customSampleTimes)
    {
        if (customSampleTimes == null)
        {
            return (float[])DefaultSampleTimes.Clone();
        }

        List<float> normalized = new List<float>();
        foreach (float sampleTime in customSampleTimes)
        {
            if (float.IsNaN(sampleTime) || float.IsInfinity(sampleTime) || sampleTime < 0f)
            {
                continue;
            }

            normalized.Add(sampleTime);
        }

        if (normalized.Count == 0)
        {
            return (float[])DefaultSampleTimes.Clone();
        }

        normalized.Sort();
        List<float> deduplicated = new List<float>(normalized.Count);
        for (int i = 0; i < normalized.Count; i++)
        {
            if (deduplicated.Count > 0 && Mathf.Abs(deduplicated[deduplicated.Count - 1] - normalized[i]) <= 0.0001f)
            {
                continue;
            }

            deduplicated.Add(normalized[i]);
        }

        return deduplicated.ToArray();
    }

    internal static float ResolveDiagnosticSampleClock(
        bool sampleByAnimationClipTime,
        bool recorderUsesCaptureFramerate,
        float[] configuredSampleTimes,
        int recorderFrame,
        float animationClipTime,
        float elapsedFallback)
    {
        if (!sampleByAnimationClipTime)
        {
            return elapsedFallback;
        }

        if (float.IsNaN(animationClipTime) || float.IsInfinity(animationClipTime))
        {
            return elapsedFallback;
        }

        if (elapsedFallback > 0.25f && animationClipTime <= 0.0001f)
        {
            return elapsedFallback;
        }

        return animationClipTime;
    }
}
