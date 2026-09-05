using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 팔 길이축 Twist를 추가하지 않는 최단 Swing 회전을 계산함.
    /// </summary>
    internal static class HumanoidArmSwingCorrectionCalculator
    {
        private const float DirectionEpsilon = 0.000001f;
        private const float MaximumCorrectionDegrees = 15f;

        internal static bool TryCalculate(
            Vector3 currentDirection,
            Vector3 referenceDirection,
            out Quaternion correction,
            out float errorDegrees)
        {
            correction = Quaternion.identity;
            errorDegrees = 0f;
            if (!IsFinite(currentDirection) ||
                !IsFinite(referenceDirection) ||
                currentDirection.sqrMagnitude <= DirectionEpsilon ||
                referenceDirection.sqrMagnitude <= DirectionEpsilon)
            {
                return false;
            }

            Vector3 current = currentDirection.normalized;
            Vector3 reference = referenceDirection.normalized;
            errorDegrees = Vector3.Angle(current, reference);
            if (!IsFinite(errorDegrees))
            {
                errorDegrees = 0f;
                return false;
            }

            Quaternion fullCorrection = Quaternion.FromToRotation(current, reference);
            float correctionWeight = errorDegrees <= MaximumCorrectionDegrees
                ? 1f
                : MaximumCorrectionDegrees / errorDegrees;
            correction = Quaternion.Slerp(
                Quaternion.identity,
                fullCorrection,
                correctionWeight);
            return IsFinite(correction);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
