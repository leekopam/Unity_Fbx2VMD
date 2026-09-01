using UnityEngine;

namespace Fbx2Vmd.Retargeting
{
    /// <summary>
    /// 접지 보정값과 수직 이동량을 입력 값만으로 계산함.
    /// </summary>
    public static class GroundingStabilizer
    {
        private const float DirectionReversalNoiseThreshold = 0.0005f;
        private const float LateVisualPenetrationRecoverySmoothing = 0.55f;
        private const float LateVisualPenetrationRecoveryMaxStep = 0.1f;

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

        public static bool TryCalculateEstimatedFootRadius(
            float leftFootY,
            float rightFootY,
            float rendererMinY,
            out float estimatedRadius)
        {
            float lowestFootY = Mathf.Min(leftFootY, rightFootY);
            estimatedRadius = lowestFootY - rendererMinY;
            if (!IsFinite(estimatedRadius))
            {
                return false;
            }

            estimatedRadius = Mathf.Clamp(estimatedRadius, 0.02f, 0.16f);
            return true;
        }

        public static bool TryCalculateLowestFootBottomY(
            float leftFootY,
            float rightFootY,
            float footRadius,
            out float lowestFootBottomY)
        {
            lowestFootBottomY = 0f;
            if (!TryCalculateFootBottomY(leftFootY, footRadius, out float leftBottom) ||
                !TryCalculateFootBottomY(rightFootY, footRadius, out float rightBottom))
            {
                return false;
            }

            lowestFootBottomY = Mathf.Min(leftBottom, rightBottom);
            return true;
        }

        public static bool TryCalculateFootBottomY(
            float footY,
            float footRadius,
            out float footBottomY)
        {
            footBottomY = footY - footRadius;
            if (!IsFinite(footBottomY))
            {
                footBottomY = 0f;
                return false;
            }

            return true;
        }

        public static bool TryCalculateGroundedFootLockRootCorrection(
            Vector3 correctionSum,
            int correctionCount,
            float groundedFootLockWeight,
            float maxGroundedFootLockStep,
            out Vector3 correction)
        {
            correction = Vector3.zero;
            if (correctionCount <= 0)
            {
                return false;
            }

            correction = correctionSum / correctionCount;
            correction.y = 0f;
            correction *= Mathf.Clamp01(groundedFootLockWeight);

            float maxStep = Mathf.Max(0.001f, maxGroundedFootLockStep);
            if (correction.magnitude > maxStep)
            {
                correction = correction.normalized * maxStep;
            }

            return IsFinite(correction) && correction.sqrMagnitude > 0.00000001f;
        }

        public static bool TryCalculateFootLockCorrection(
            float bottomY,
            Vector3 footPosition,
            float targetHeight,
            bool locked,
            Vector3 lockPosition,
            out bool nextLocked,
            out Vector3 nextLockPosition,
            out Vector3 correction)
        {
            const float contactHeight = 0.08f;
            const float releaseHeight = 0.14f;
            const float resetDistance = 0.25f;

            nextLocked = locked;
            nextLockPosition = lockPosition;
            correction = Vector3.zero;

            if (!IsFinite(bottomY))
            {
                nextLocked = false;
                return false;
            }

            if (bottomY > targetHeight + releaseHeight)
            {
                nextLocked = false;
                return false;
            }

            footPosition.y = 0f;
            if (!IsFinite(footPosition))
            {
                nextLocked = false;
                return false;
            }

            if (!locked || bottomY > targetHeight + contactHeight)
            {
                nextLockPosition = footPosition;
                nextLocked = bottomY <= targetHeight + contactHeight;
                return false;
            }

            correction = lockPosition - footPosition;
            correction.y = 0f;
            if (!IsFinite(correction))
            {
                nextLocked = false;
                return false;
            }

            if (correction.magnitude > resetDistance)
            {
                nextLockPosition = footPosition;
                correction = Vector3.zero;
            }

            return true;
        }

        /// <summary>
        /// 발 하단과 renderer 하단 중 접지 기준으로 사용할 값을 선택함.
        /// </summary>
        public static float ResolveGroundingContactBottomY(
            float lowestFootBottomY,
            bool hasRendererBounds,
            float rendererMinY,
            bool rejectRendererGroundingOutliers,
            float maxRendererFootGroundingSeparation,
            out bool rendererGroundingOutlier)
        {
            rendererGroundingOutlier = false;
            if (!hasRendererBounds)
            {
                return lowestFootBottomY;
            }

            if (!rejectRendererGroundingOutliers)
            {
                return rendererMinY;
            }

            float separation = Mathf.Abs(rendererMinY - lowestFootBottomY);
            float maxSeparation = Mathf.Max(0.02f, maxRendererFootGroundingSeparation);
            if (separation <= maxSeparation)
            {
                return rendererMinY;
            }

            rendererGroundingOutlier = true;
            return lowestFootBottomY;
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
        /// 최종 시각 접지 잔차에 초기화, 평활화와 프레임 이동 제한을 적용함.
        /// </summary>
        public static float CalculateLateVisualGroundingStep(
            float residual,
            bool smoothLateVisualGroundingCorrection,
            bool lateVisualGroundingInitialized,
            float lateVisualGroundingSnapThreshold,
            float lateVisualGroundingSmoothing,
            float maxLateVisualGroundingStepPerFrame)
        {
            if (!smoothLateVisualGroundingCorrection)
            {
                return residual;
            }

            if (!lateVisualGroundingInitialized)
            {
                return residual;
            }

            float snapThreshold = Mathf.Max(0.005f, lateVisualGroundingSnapThreshold);
            if (residual > 0.0001f && residual <= snapThreshold)
            {
                return residual;
            }

            bool isFloorPenetration = residual > 0.0001f;
            float smoothing = Mathf.Clamp01(lateVisualGroundingSmoothing);
            if (isFloorPenetration)
            {
                smoothing = Mathf.Max(smoothing, LateVisualPenetrationRecoverySmoothing);
            }

            float step = Mathf.Abs(residual) > snapThreshold
                ? residual * Mathf.Max(0.1f, smoothing)
                : residual * smoothing;
            float maxStep = Mathf.Max(0.001f, maxLateVisualGroundingStepPerFrame);
            if (isFloorPenetration)
            {
                maxStep = Mathf.Max(maxStep, LateVisualPenetrationRecoveryMaxStep);
            }

            if (Mathf.Abs(step) > maxStep)
            {
                step = Mathf.Sign(step) * maxStep;
            }

            return step;
        }

        /// <summary>
        /// 최종 시각 접지 잔차가 적용 가능한 범위인지 판정함.
        /// </summary>
        public static bool TryCalculateLateVisualGroundingEffectiveResidual(
            float residual,
            bool smoothLateVisualGroundingCorrection,
            float groundingDeadZone,
            float maxLateVisualGroundingCorrection,
            out float effectiveResidual,
            out bool exceededMaxCorrection)
        {
            effectiveResidual = 0f;
            exceededMaxCorrection = false;

            bool isPenetrationResidual = residual > 0.0001f;
            bool isFloatingResidual = residual < -0.0001f;
            bool isVisualFloorResidual = isPenetrationResidual || isFloatingResidual;
            float deadZone = Mathf.Max(0.001f, groundingDeadZone);
            float skipDeadZone = isVisualFloorResidual ? 0.001f : deadZone;
            if (Mathf.Abs(residual) <= skipDeadZone)
            {
                return false;
            }

            float maxCorrection = Mathf.Max(0.001f, maxLateVisualGroundingCorrection);
            if (Mathf.Abs(residual) > maxCorrection)
            {
                exceededMaxCorrection = true;
                return false;
            }

            effectiveResidual = residual;
            if (smoothLateVisualGroundingCorrection && deadZone > 0f && !isVisualFloorResidual)
            {
                effectiveResidual = Mathf.Sign(residual) * Mathf.Max(0f, Mathf.Abs(residual) - deadZone);
                if (Mathf.Abs(effectiveResidual) <= 0.0001f)
                {
                    effectiveResidual = 0f;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 현재 접지 이동과 반대 방향인 최종 시각 보정을 건너뛸지 판정함.
        /// </summary>
        public static bool ShouldSkipLateVisualGroundingForActiveVerticalStep(
            float residual,
            bool smoothLateVisualGroundingCorrection,
            float lastGroundingVerticalStep)
        {
            if (!smoothLateVisualGroundingCorrection ||
                !IsFinite(residual) ||
                !IsFinite(lastGroundingVerticalStep) ||
                Mathf.Abs(residual) <= 0.0005f ||
                Mathf.Abs(lastGroundingVerticalStep) <= 0.0005f)
            {
                return false;
            }

            return Mathf.Sign(residual) != Mathf.Sign(lastGroundingVerticalStep);
        }

        /// <summary>
        /// 최종 시각 접지 이동을 적용한 유한 위치를 계산함.
        /// </summary>
        public static bool TryCalculateLateVisualGroundingAppliedPosition(
            Vector3 currentPosition,
            float appliedResidual,
            out Vector3 appliedPosition)
        {
            appliedPosition = Vector3.zero;
            if (!IsFinite(currentPosition))
            {
                return false;
            }

            appliedPosition = currentPosition;
            appliedPosition.y += appliedResidual;
            if (!IsFinite(appliedPosition))
            {
                appliedPosition = Vector3.zero;
                return false;
            }

            return true;
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

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
