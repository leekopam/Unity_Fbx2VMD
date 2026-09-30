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
                if (i < points.Length &&
                    !EvaluateSupport(points, i, frameRate, referenceHeight, heightTolerance,
                        speedLimit, start >= 0 ? ReleaseThresholdRatio : 1f, out isSupported))
                    return false;

                if (isSupported)
                {
                    if (start < 0)
                        start = i;
                    continue;
                }

                if (start < 0)
                    continue;

                // 런 도중의 짧은 공백도 발끝이 지면 높이에 머물면 실제 이륙이 아닌
                // 속도 지터로 보고 지지를 이어감. 이륙은 반드시 높이 상승을 동반함.
                if (i < points.Length)
                {
                    int resume = i;
                    bool resumeSupported = false;
                    while (resume < points.Length && resume - i < minimumFrames &&
                        points[resume].y <= referenceHeight + heightTolerance * ReleaseThresholdRatio)
                    {
                        if (!EvaluateSupport(points, resume, frameRate, referenceHeight,
                                heightTolerance, speedLimit, ReleaseThresholdRatio,
                                out resumeSupported))
                            return false;
                        if (resumeSupported)
                            break;
                        resume++;
                    }
                    if (resumeSupported && resume - i < minimumFrames)
                    {
                        i = resume - 1;
                        continue;
                    }
                }

                if (i - start >= minimumFrames)
                {
                    for (int j = start; j < i; j++)
                    {
                        // 진입 혼합만 지지 구간 안에 둠. 해제를 구간 안에서 시작하면
                        // 발이 아직 지면에 붙은 채로 원시 리타깃으로 복귀해 팝이 됨.
                        float enter = start == 0 ? 1f : (j - start + 1f) / blendFrames;
                        float weight = Mathf.Clamp01(enter);
                        float smoothed = weight * weight * (3f - 2f * weight);
                        if (smoothed > result[j][channel])
                            result[j][channel] = smoothed;
                    }

                    // 해제 블렌드는 런 종료 뒤(실제 리프트오프 구간)에 배치해
                    // 복귀 동작이 발이 뜨는 움직임에 겹치게 함. 뒤따르는 지지 런의
                    // 진입 혼합과 겹치면 더 큰 값을 유지해 가중치가 움푹 꺼지지 않게 함.
                    int releaseEnd = Mathf.Min(points.Length, i + Mathf.CeilToInt(blendFrames));
                    for (int j = i; j < releaseEnd; j++)
                    {
                        float tail = Mathf.Clamp01((releaseEnd - j) / blendFrames);
                        float smoothed = tail * tail * (3f - 2f * tail);
                        if (smoothed > result[j][channel])
                            result[j][channel] = smoothed;
                    }
                }
                start = -1;
            }
            return true;
        }

        // 왕복 움직임의 중앙 차분이 0이 되어도 정지로 오인하지 않도록 양쪽 속도를 확인함.
        private static bool EvaluateSupport(Vector3[] points, int i, float frameRate,
            float referenceHeight, float heightTolerance, float speedLimit,
            float thresholdRatio, out bool supported)
        {
            supported = false;
            float speed = Mathf.Max(
                Vector3.Distance(points[Mathf.Max(0, i - 1)], points[i]),
                Vector3.Distance(points[i], points[Mathf.Min(points.Length - 1, i + 1)])) * frameRate;
            if (!IsFinite(speed))
                return false;
            supported = points[i].y <= referenceHeight + heightTolerance * thresholdRatio &&
                speed <= speedLimit * thresholdRatio;
            return true;
        }

        private static bool IsPositive(float value) => IsFinite(value) && value > 0f;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
