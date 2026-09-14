using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 현재 자세의 밑창 점들이 지면 위에 놓이도록 발목 목표의 최소 상향 이동을 계산함.
    /// </summary>
    internal static class HumanoidSoleGroundConstraint
    {
        internal static bool TryLiftAbovePlane(
            Vector3 anklePosition,
            Quaternion footRotation,
            Vector3[] footLocalSolePoints,
            float uniformScale,
            Vector3 planePoint,
            Vector3 planeNormal,
            out Vector3 constrainedPosition,
            out float liftDistance)
        {
            constrainedPosition = anklePosition;
            liftDistance = 0f;
            float rotationLength = Quaternion.Dot(footRotation, footRotation);
            float normalLength = planeNormal.sqrMagnitude;
            if (footLocalSolePoints == null || footLocalSolePoints.Length == 0 ||
                !IsFinite(anklePosition) || !IsFinite(planePoint) ||
                !IsFinite(rotationLength) || Mathf.Abs(rotationLength - 1f) > 0.0001f ||
                !IsFinite(normalLength) || Mathf.Abs(normalLength - 1f) > 0.0001f ||
                planeNormal.y <= 0f || !IsFinite(uniformScale) || uniformScale <= 0f)
            {
                return false;
            }

            Quaternion rotation = footRotation.normalized;
            Vector3 normal = planeNormal.normalized;
            float minimumDistance = float.PositiveInfinity;
            for (int index = 0; index < footLocalSolePoints.Length; index++)
            {
                Vector3 point = footLocalSolePoints[index];
                if (!IsFinite(point))
                    return false;

                // 발가락 변형이 반영된 현재 로컬 점에 목표 회전과 배율을 한 번씩 적용함.
                Vector3 worldPoint = anklePosition + rotation * (point * uniformScale);
                float distance = Vector3.Dot(worldPoint - planePoint, normal);
                if (!IsFinite(worldPoint) || !IsFinite(distance))
                    return false;

                minimumDistance = Mathf.Min(minimumDistance, distance);
            }

            if (minimumDistance >= 0f)
                return true;

            Vector3 liftedPosition = anklePosition - normal * minimumDistance;
            if (!IsFinite(liftedPosition))
                return false;

            // 지지 피벗과 충돌할 수 있으므로 이동량을 돌려주고 접촉 성공으로 간주하지 않음.
            constrainedPosition = liftedPosition;
            liftDistance = -minimumDistance;
            return true;
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
