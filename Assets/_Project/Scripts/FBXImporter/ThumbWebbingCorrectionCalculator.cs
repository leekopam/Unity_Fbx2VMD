using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 엄지 webbing helper의 유효 보정 설정과 제한 위치를 값 입력만으로 계산함.
    /// </summary>
    internal static class ThumbWebbingCorrectionCalculator
    {
        internal static void CalculateEffectiveSettings(
            float configuredWeight,
            float configuredMaxLocalAngle,
            float configuredMaxPositionOffset,
            float baseMaxLocalAngle,
            float baseMaxPositionOffset,
            float poseRisk,
            float dynamicMinLocalAngle,
            float dynamicMinPositionOffset,
            out float weight,
            out float maxLocalAngle,
            out float maxPositionOffset)
        {
            configuredWeight = Mathf.Clamp01(configuredWeight);
            configuredMaxLocalAngle = Mathf.Clamp(configuredMaxLocalAngle, 0f, 45f);
            configuredMaxPositionOffset = Mathf.Clamp(configuredMaxPositionOffset, 0f, 0.02f);
            weight = 0f;
            maxLocalAngle = Mathf.Clamp(baseMaxLocalAngle, 0f, 45f);
            maxPositionOffset = Mathf.Clamp(baseMaxPositionOffset, 0f, 0.02f);

            if (!IsFinite(poseRisk) || poseRisk <= 0f)
            {
                return;
            }

            weight = Mathf.Lerp(0f, configuredWeight, poseRisk);
            maxLocalAngle = Mathf.Lerp(
                maxLocalAngle,
                Mathf.Min(configuredMaxLocalAngle, dynamicMinLocalAngle),
                poseRisk);
            maxPositionOffset = Mathf.Lerp(
                maxPositionOffset,
                Mathf.Min(configuredMaxPositionOffset, dynamicMinPositionOffset),
                poseRisk);
        }

        internal static Vector3 ConstrainPosition(
            Vector3 initialPosition,
            Vector3 targetPosition,
            float weight,
            float maxOffset)
        {
            if (weight > 0f)
            {
                targetPosition = Vector3.Lerp(targetPosition, initialPosition, weight);
            }

            maxOffset = Mathf.Clamp(maxOffset, 0f, 0.02f);
            if (maxOffset <= 0.000001f)
            {
                return initialPosition;
            }

            return initialPosition + Vector3.ClampMagnitude(
                targetPosition - initialPosition,
                maxOffset);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
