using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class HumanoidFootSupportRoleCalculator
    {
        internal static bool TryBuild(Vector2[] sourceWeights, float[] relativeHeights,
            float frameRate, float heightTolerance, float blendSeconds, out Vector2[] weights)
        {
            weights = Array.Empty<Vector2>();
            if (sourceWeights == null || relativeHeights == null || sourceWeights.Length == 0 ||
                sourceWeights.Length != relativeHeights.Length || !IsPositiveFinite(frameRate) ||
                !IsPositiveFinite(heightTolerance) || !IsPositiveFinite(blendSeconds))
                return false;

            float maximumRoleChange = (float)Math.Min(2d, 1d / frameRate / blendSeconds);
            if (maximumRoleChange <= 0f)
                return false;
            for (int i = 0; i < sourceWeights.Length; i++)
            {
                Vector2 source = sourceWeights[i];
                if (!IsFinite(relativeHeights[i]) || !IsFinite(source.x) || !IsFinite(source.y) ||
                    source.x < 0f || source.x > 1f || source.y < 0f || source.y > 1f)
                    return false;
            }

            weights = new Vector2[sourceWeights.Length];
            float role = 0f;
            bool hasRole = false;
            for (int i = 0; i < weights.Length; i++)
            {
                float whole = Mathf.Max(sourceWeights[i].x, sourceWeights[i].y);
                if (whole <= 0f)
                {
                    hasRole = false;
                    continue;
                }

                float height = relativeHeights[i];
                float rear = Mathf.Clamp01(1f - Mathf.Max(0f, -height) / heightTolerance);
                float front = Mathf.Clamp01(1f - Mathf.Max(0f, height) / heightTolerance);
                float desired = Smooth(rear) - Smooth(front);
                // 단일 역할을 완화하여 전체 지지는 보존하고, 해제 후 재진입에는 이전 역할을 남기지 않음.
                role = hasRole ? Mathf.MoveTowards(role, desired, maximumRoleChange) : desired;
                hasRole = true;
                weights[i] = whole * new Vector2(1f + Mathf.Min(0f, role), 1f - Mathf.Max(0f, role));
            }
            return true;
        }

        internal static Vector2 Interpolate(Vector2 first, Vector2 second, float blend)
        {
            float firstWhole = Mathf.Max(first.x, first.y);
            float secondWhole = Mathf.Max(second.x, second.y);
            float firstRole = firstWhole > 0f ? (first.x - first.y) / firstWhole : 0f;
            float secondRole = secondWhole > 0f ? (second.x - second.y) / secondWhole : 0f;
            if (firstWhole <= 0f) firstRole = secondRole;
            if (secondWhole <= 0f) secondRole = firstRole;
            float whole = Mathf.Lerp(firstWhole, secondWhole, blend);
            float role = Mathf.Lerp(firstRole, secondRole, blend);
            return whole * new Vector2(1f + Mathf.Min(0f, role), 1f - Mathf.Max(0f, role));
        }

        private static float Smooth(float value) => value * value * (3f - 2f * value);

        private static bool IsPositiveFinite(float value) => IsFinite(value) && value > 0f;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
