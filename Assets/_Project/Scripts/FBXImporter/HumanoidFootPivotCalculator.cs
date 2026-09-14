using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 고정 접촉점과 발 로컬 피벗으로 발목 목표 자세를 계산함.
    /// </summary>
    internal static class HumanoidFootPivotCalculator
    {
        internal static bool TryCalculatePose(
            Vector3 animationPosition,
            Quaternion animationRotation,
            Vector3 footLocalPivot,
            float uniformScale,
            Vector3 worldAnchor,
            Quaternion desiredRotation,
            float contactWeight,
            out Vector3 targetPosition,
            out Quaternion targetRotation)
        {
            targetPosition = animationPosition;
            targetRotation = animationRotation;
            if (!IsFinite(animationPosition) || !IsFinite(footLocalPivot) ||
                !IsFinite(worldAnchor) || !IsUnitRotation(animationRotation) ||
                !IsUnitRotation(desiredRotation) || !IsFinite(uniformScale) ||
                uniformScale <= 0f || !IsFinite(contactWeight) ||
                contactWeight < 0f || contactWeight > 1f)
            {
                return false;
            }

            if (contactWeight == 0f)
            {
                return true;
            }

            Quaternion rotation = Quaternion.Slerp(
                animationRotation.normalized, desiredRotation.normalized, contactWeight);
            // 피벗은 스케일을 적용하지 않은 발 Transform 로컬 좌표이며 여기서 한 번만 배율을 적용함.
            Vector3 offset = rotation * (footLocalPivot * uniformScale);
            Vector3 fullContactPosition = worldAnchor - offset;
            // 회전 보간 뒤의 피벗 오프셋으로 위치를 풀어 해제 도중 다른 회전 중심으로 튀지 않게 함.
            Vector3 position = Vector3.Lerp(animationPosition, fullContactPosition, contactWeight);
            if (!IsFinite(offset) || !IsFinite(fullContactPosition) || !IsFinite(position))
            {
                return false;
            }

            targetPosition = position;
            targetRotation = rotation;
            return true;
        }

        private static bool IsUnitRotation(Quaternion rotation)
        {
            float squaredLength = Quaternion.Dot(rotation, rotation);
            return IsFinite(squaredLength) && Mathf.Abs(squaredLength - 1f) <= 0.0001f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
