using UnityEngine;

namespace Fbx2Vmd.Retargeting
{
    /// <summary>
    /// 접지 보정값과 수직 이동량을 입력 값만으로 계산함.
    /// </summary>
    public static class GroundingStabilizer
    {
        private const float DirectionReversalNoiseThreshold = 0.0005f;

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
        /// 수직 보정량에 사각 구간, 평활화와 최대 이동량 제한을 적용함.
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
            if (!IsFinite(previousGroundingVerticalStep) ||
                Mathf.Abs(verticalStep) <= DirectionReversalNoiseThreshold ||
                Mathf.Abs(previousGroundingVerticalStep) <= DirectionReversalNoiseThreshold)
            {
                return false;
            }

            return Mathf.Sign(verticalStep) != Mathf.Sign(previousGroundingVerticalStep);
        }

        // 현재 Unity의 .NET 런타임에서 사용할 수 있는 유한값 판정을 유지함.
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
