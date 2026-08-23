using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 시각 포즈 spike 판정과 보간·제한 값을 계산함.
    /// </summary>
    internal static class RetargetingPoseSmoothing
    {
        private const float BodyPositionVisualSpikeThreshold = 0.02f;
        private const float BodyRotationVisualSpikeThresholdDegrees = 25f;
        private const float ForearmStretchVisualClampCurrentMax = -0.65f;

        internal static bool ShouldSmoothVisualPoseSpike(
            float maxMuscleDelta,
            float bodyPositionDelta,
            float bodyRotationDelta,
            float poseVisualMuscleDeltaThreshold,
            bool legacyAnimationStepSpikeThisFrame,
            out bool muscleDeltaOnlySpike)
        {
            bool bodyPoseSpike = IsBodyPoseSpike(bodyPositionDelta, bodyRotationDelta);
            muscleDeltaOnlySpike = maxMuscleDelta > poseVisualMuscleDeltaThreshold &&
                !legacyAnimationStepSpikeThisFrame &&
                !bodyPoseSpike;

            return legacyAnimationStepSpikeThisFrame || bodyPoseSpike;
        }

        internal static float CalculateVisualPoseSpikeCurrentWeight(
            float configuredWeight,
            float bodyPositionDelta,
            float bodyRotationDelta,
            bool legacyAnimationStepSpikeThisFrame)
        {
            float currentWeight = Mathf.Clamp(configuredWeight, 0.1f, 1f);
            if (IsBodyPoseSpike(bodyPositionDelta, bodyRotationDelta))
            {
                return Mathf.Min(currentWeight, 0.1f);
            }

            return currentWeight;
        }

        internal static float BlendVisualPoseSpikeMuscle(
            float previousValue,
            float currentValue,
            float currentWeight,
            bool shouldPreserveCurrentValue,
            bool isForearmStretchMuscle,
            float forearmStretchClampMaxOffset)
        {
            if (shouldPreserveCurrentValue)
            {
                return currentValue;
            }

            float blended = Mathf.Lerp(previousValue, currentValue, currentWeight);
            return ClampForearmStretchVisualSpikeBlend(
                previousValue,
                currentValue,
                blended,
                isForearmStretchMuscle,
                forearmStretchClampMaxOffset);
        }

        private static float ClampForearmStretchVisualSpikeBlend(
            float previousValue,
            float currentValue,
            float blendedValue,
            bool isForearmStretchMuscle,
            float maxOffset)
        {
            if (maxOffset <= 0f ||
                !isForearmStretchMuscle ||
                !IsFinite(previousValue) ||
                !IsFinite(currentValue) ||
                !IsFinite(blendedValue))
            {
                return blendedValue;
            }

            if (currentValue > ForearmStretchVisualClampCurrentMax)
            {
                return blendedValue;
            }

            float safeOffset = Mathf.Clamp01(maxOffset);
            return Mathf.Clamp(
                blendedValue,
                currentValue - safeOffset,
                currentValue + safeOffset);
        }

        private static bool IsBodyPoseSpike(float bodyPositionDelta, float bodyRotationDelta)
        {
            return bodyPositionDelta > BodyPositionVisualSpikeThreshold ||
                bodyRotationDelta > BodyRotationVisualSpikeThresholdDegrees;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
