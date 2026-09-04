using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Humanoid 모션의 시간과 정수 프레임 사이 변환을 계산함.
    /// </summary>
    internal static class HumanoidMotionFrameCalculator
    {
        internal const float DefaultFrameRate = 60f;

        internal static float NormalizeFrameRate(float frameRate)
        {
            return IsFinite(frameRate) && frameRate > 0f
                ? frameRate
                : DefaultFrameRate;
        }

        internal static int CalculateLastFrameIndex(
            float clipLengthSeconds,
            float frameRate)
        {
            float length = NormalizeLength(clipLengthSeconds);
            double scaledFrame = length * NormalizeFrameRate(frameRate);
            if (scaledFrame >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return Mathf.Max(0, Mathf.RoundToInt((float)scaledFrame));
        }

        internal static int CalculateFrameIndex(
            float timeSeconds,
            float clipLengthSeconds,
            float frameRate)
        {
            float length = NormalizeLength(clipLengthSeconds);
            float time = IsFinite(timeSeconds)
                ? Mathf.Clamp(timeSeconds, 0f, length)
                : 0f;
            double scaledFrame = time * NormalizeFrameRate(frameRate);
            int frameIndex = scaledFrame >= int.MaxValue
                ? int.MaxValue
                : Mathf.RoundToInt((float)scaledFrame);
            return Mathf.Clamp(
                frameIndex,
                0,
                CalculateLastFrameIndex(length, frameRate));
        }

        internal static float CalculateTimeSeconds(
            int frameIndex,
            float clipLengthSeconds,
            float frameRate)
        {
            float length = NormalizeLength(clipLengthSeconds);
            int clampedFrameIndex = Mathf.Clamp(
                frameIndex,
                0,
                CalculateLastFrameIndex(length, frameRate));
            return Mathf.Min(
                clampedFrameIndex / NormalizeFrameRate(frameRate),
                length);
        }

        private static float NormalizeLength(float clipLengthSeconds)
        {
            return IsFinite(clipLengthSeconds) && clipLengthSeconds > 0f
                ? clipLengthSeconds
                : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
