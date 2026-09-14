using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 보정 전 연속 궤적에서 뒤쪽·앞쪽 지지 가중치를 사전 계산함.
    /// </summary>
    internal static class HumanoidFootSupportWeightCalculator
    {
        private const float ReleaseThresholdRatio = 1.25f;

        internal static bool TryBuild(
            Vector3[] rearPoints,
            Vector3[] frontPoints,
            float frameRate,
            Vector2 referenceHeights,
            float heightTolerance,
            float speedLimit,
            float minimumSupportSeconds,
            float blendSeconds,
            out Vector2[] weights)
        {
            weights = Array.Empty<Vector2>();
            if (rearPoints == null || frontPoints == null || rearPoints.Length < 2 ||
                rearPoints.Length != frontPoints.Length || !IsPositive(frameRate) ||
                !IsPositive(heightTolerance) || !IsPositive(speedLimit) ||
                !IsPositive(minimumSupportSeconds) || !IsPositive(blendSeconds) ||
                !IsFinite(referenceHeights.x) || !IsFinite(referenceHeights.y) ||
                !IsFinite(referenceHeights.x + heightTolerance * ReleaseThresholdRatio) ||
                !IsFinite(referenceHeights.y + heightTolerance * ReleaseThresholdRatio) ||
                !IsFinite(speedLimit * ReleaseThresholdRatio))
            {
                return false;
            }

            for (int i = 0; i < rearPoints.Length; i++)
            {
                if (!IsFinite(rearPoints[i]) || !IsFinite(frontPoints[i]))
                    return false;
            }

            float minimumIntervals = frameRate * minimumSupportSeconds;
            if (minimumIntervals >= rearPoints.Length)
            {
                weights = new Vector2[rearPoints.Length];
                return true;
            }

            // 표본 사이 경과시간을 기준으로 하므로 60Hz의 0.1초 지지에는 7개 시점이 필요함.
            int minimumFrames = Mathf.CeilToInt(minimumIntervals) + 1;
            float blendFrames = (float)Math.Min(rearPoints.Length, (double)frameRate * blendSeconds);
            var result = new Vector2[rearPoints.Length];
            if (!TryCalculateChannel(rearPoints, frameRate, referenceHeights.x,
                    heightTolerance, speedLimit, minimumFrames, blendFrames, 0, result) ||
                !TryCalculateChannel(frontPoints, frameRate, referenceHeights.y,
                    heightTolerance, speedLimit, minimumFrames, blendFrames, 1, result))
            {
                return false;
            }

            weights = result;
            return true;
        }

        private static bool TryCalculateChannel(
            Vector3[] points, float frameRate, float referenceHeight,
            float heightTolerance, float speedLimit, int minimumFrames,
            float blendFrames, int channel, Vector2[] result)
        {
            int start = -1;
            for (int i = 0; i <= points.Length; i++)
            {
                bool isSupported = false;
                if (i < points.Length)
                {
                    // 왕복 움직임의 중앙 차분이 0이 되어도 정지로 오인하지 않도록 양쪽 속도를 확인함.
                    float speed = Mathf.Max(
                        Vector3.Distance(points[Mathf.Max(0, i - 1)], points[i]),
                        Vector3.Distance(points[i], points[Mathf.Min(points.Length - 1, i + 1)])) * frameRate;
                    if (!IsFinite(speed))
                        return false;

                    float thresholdRatio = start >= 0 ? ReleaseThresholdRatio : 1f;
                    isSupported = points[i].y <= referenceHeight + heightTolerance * thresholdRatio &&
                        speed <= speedLimit * thresholdRatio;
                }

                if (isSupported)
                {
                    if (start < 0)
                        start = i;
                    continue;
                }

                if (start < 0)
                    continue;

                if (i - start >= minimumFrames)
                {
                    for (int j = start; j < i; j++)
                    {
                        // 구간 밖 공중 자세를 잡아두지 않도록 진입·해제 혼합을 지지 구간 안에 둠.
                        float enter = start == 0 ? 1f : (j - start + 1f) / blendFrames;
                        float leave = i == points.Length ? 1f : (i - j) / blendFrames;
                        float weight = Mathf.Clamp01(Mathf.Min(enter, leave));
                        result[j][channel] = weight * weight * (3f - 2f * weight);
                    }
                }
                start = -1;
            }
            return true;
        }

        private static bool IsPositive(float value) => IsFinite(value) && value > 0f;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
