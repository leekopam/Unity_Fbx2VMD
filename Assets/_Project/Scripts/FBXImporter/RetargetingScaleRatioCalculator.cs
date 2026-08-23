using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class RetargetingScaleRatioCalculator
    {
        internal static float CalculateSafeScaleRatio(
            float currentScaleRatio,
            bool hasAnimatorScale,
            float ghostHumanScale,
            float targetHumanScale,
            float initialGhostHipHeight,
            float initialTargetHipHeight,
            bool hasHipPositions,
            float ghostHipY,
            float targetHipY,
            out bool usedInvalidFallback)
        {
            usedInvalidFallback = false;
            float ratio = currentScaleRatio;

            if (hasAnimatorScale && ghostHumanScale > 0.0001f && targetHumanScale > 0.0001f)
            {
                ratio = targetHumanScale / ghostHumanScale;
            }
            else if (initialGhostHipHeight > 0.01f)
            {
                ratio = initialTargetHipHeight / initialGhostHipHeight;
            }
            else if (hasHipPositions && ghostHipY > 0.01f)
            {
                ratio = targetHipY / ghostHipY;
            }

            if (!IsFinite(ratio) || ratio <= 0f)
            {
                usedInvalidFallback = true;
                return 1f;
            }

            return Mathf.Clamp(ratio, 0.01f, 10f);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
