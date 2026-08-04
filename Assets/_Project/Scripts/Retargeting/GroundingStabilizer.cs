using UnityEngine;

namespace Fbx2Vmd.Retargeting
{
    /// <summary>
    /// 접지(Grounding) 계산 유틸리티.
    /// PoseSpaceRetargeter에서 추출함.
    /// ponytail: 순수 static 계산만 — HumanPose 변형은 PoseSpaceRetargeter에 유지.
    /// </summary>
    public static class GroundingStabilizer
    {
        /// <summary>
        /// 발 접지 높이 보정값 계산. targetHeight - contactBottomY.
        /// </summary>
        public static bool TryCalculateAdjustment(
            float targetHeight,
            float contactBottomY,
            out float adjustment)
        {
            adjustment = targetHeight - contactBottomY;
            if (!IsFinite(adjustment))
            {
                adjustment = 0f;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 수직 보정 step을 dead zone, smoothing, max step 제한으로 clamp.
        /// </summary>
        public static float CalculateVerticalStep(
            float currentY,
            float adjustment,
            bool wasGroundingInitialized,
            bool smoothGrounding,
            float groundingSmoothing,
            float maxGroundingVerticalStepPerFrame,
            float groundingDeadZone,
            float previousGroundingVerticalStep,
            float groundingDirectionReversalStepScale,
            out bool skippedByDeadZone,
            out bool smoothed,
            out bool clamped)
        {
            skippedByDeadZone = false;
            smoothed = false;
            clamped = false;

            float deadZone = Mathf.Max(0f, groundingDeadZone);
            if (wasGroundingInitialized && Mathf.Abs(adjustment) <= deadZone)
            {
                skippedByDeadZone = true;
                return 0f;
            }

            float effectiveAdjustment = adjustment;
            if (wasGroundingInitialized && deadZone > 0f)
            {
                effectiveAdjustment = Mathf.Sign(adjustment) * Mathf.Max(0f, Mathf.Abs(adjustment) - deadZone);
            }

            float desiredY = currentY + effectiveAdjustment;
            float nextY = desiredY;
            if (wasGroundingInitialized && smoothGrounding)
            {
                float smoothing = Mathf.Clamp01(groundingSmoothing);
                if (smoothing < 1f)
                {
                    nextY = Mathf.Lerp(currentY, desiredY, smoothing);
                    smoothed = true;
                }

                float maxStep = Mathf.Max(0.001f, maxGroundingVerticalStepPerFrame);
                float verticalStep = nextY - currentY;
                if (IsDirectionReversal(verticalStep, previousGroundingVerticalStep))
                {
                    maxStep = Mathf.Max(0.001f, maxStep * groundingDirectionReversalStepScale);
                }

                if (Mathf.Abs(verticalStep) > maxStep)
                {
                    nextY = currentY + Mathf.Sign(verticalStep) * maxStep;
                    clamped = true;
                }
            }

            return nextY - currentY;
        }

        /// <summary>
        /// 접지 방향이 반전되었는지 확인.
        /// </summary>
        public static bool IsDirectionReversal(float verticalStep, float previousGroundingVerticalStep)
        {
            if (!IsFinite(previousGroundingVerticalStep))
            {
                return false;
            }

            return (verticalStep > 0f && previousGroundingVerticalStep < 0f) ||
                   (verticalStep < 0f && previousGroundingVerticalStep > 0f);
        }

        // ponytail: float.IsFinite polyfill for Unity's .NET runtime
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
