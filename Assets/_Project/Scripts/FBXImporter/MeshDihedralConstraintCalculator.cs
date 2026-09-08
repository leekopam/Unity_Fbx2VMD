using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 서로 이웃한 두 삼각형의 급접힘을 제한하는 위치 보정값을 계산함.
    /// </summary>
    internal static class MeshDihedralConstraintCalculator
    {
        private const float MinimumEdgeLength = 0.000001f;
        private const float MinimumNormalSquaredMagnitude = 0.0000000000000001f;

        internal static bool TryCalculateAngleDegrees(
            Vector3 opposite0,
            Vector3 opposite1,
            Vector3 edge0,
            Vector3 edge1,
            out float angleDegrees)
        {
            angleDegrees = 0f;
            if (!TryCalculateUnitNormals(
                    opposite0,
                    opposite1,
                    edge0,
                    edge1,
                    out Vector3 normal0,
                    out Vector3 normal1))
            {
                return false;
            }

            angleDegrees = Mathf.Acos(Mathf.Clamp(
                Vector3.Dot(normal0, normal1),
                -1f,
                1f)) * Mathf.Rad2Deg;
            return IsFinite(angleDegrees);
        }

        internal static bool TryCalculateCorrections(
            Vector3 opposite0,
            Vector3 opposite1,
            Vector3 edge0,
            Vector3 edge1,
            float maximumAngleDegrees,
            float stiffness,
            out Vector3 opposite0Correction,
            out Vector3 opposite1Correction,
            out Vector3 edge0Correction,
            out Vector3 edge1Correction)
        {
            opposite0Correction = Vector3.zero;
            opposite1Correction = Vector3.zero;
            edge0Correction = Vector3.zero;
            edge1Correction = Vector3.zero;

            if (!AreFinite(opposite0, opposite1, edge0, edge1) ||
                !IsFinite(maximumAngleDegrees) ||
                !IsFinite(stiffness) ||
                maximumAngleDegrees < 0f ||
                maximumAngleDegrees > 180f ||
                stiffness <= 0f)
            {
                return false;
            }

            Vector3 sharedEdge = edge1 - edge0;
            float sharedEdgeLength = sharedEdge.magnitude;
            if (sharedEdgeLength < MinimumEdgeLength)
            {
                return false;
            }

            Vector3 unscaledNormal0 = Vector3.Cross(
                edge0 - opposite0,
                edge1 - opposite0);
            Vector3 unscaledNormal1 = Vector3.Cross(
                edge1 - opposite1,
                edge0 - opposite1);
            float normal0SquaredMagnitude = unscaledNormal0.sqrMagnitude;
            float normal1SquaredMagnitude = unscaledNormal1.sqrMagnitude;
            if (normal0SquaredMagnitude < MinimumNormalSquaredMagnitude ||
                normal1SquaredMagnitude < MinimumNormalSquaredMagnitude)
            {
                return false;
            }

            Vector3 gradient0 = sharedEdgeLength *
                (unscaledNormal0 / normal0SquaredMagnitude);
            Vector3 gradient1 = sharedEdgeLength *
                (unscaledNormal1 / normal1SquaredMagnitude);
            float inverseEdgeLength = 1f / sharedEdgeLength;
            Vector3 gradient2 =
                Vector3.Dot(opposite0 - edge1, sharedEdge) * inverseEdgeLength *
                (unscaledNormal0 / normal0SquaredMagnitude) +
                Vector3.Dot(opposite1 - edge1, sharedEdge) * inverseEdgeLength *
                (unscaledNormal1 / normal1SquaredMagnitude);
            Vector3 gradient3 =
                Vector3.Dot(edge0 - opposite0, sharedEdge) * inverseEdgeLength *
                (unscaledNormal0 / normal0SquaredMagnitude) +
                Vector3.Dot(edge0 - opposite1, sharedEdge) * inverseEdgeLength *
                (unscaledNormal1 / normal1SquaredMagnitude);

            Vector3 unitNormal0 = unscaledNormal0 /
                Mathf.Sqrt(normal0SquaredMagnitude);
            Vector3 unitNormal1 = unscaledNormal1 /
                Mathf.Sqrt(normal1SquaredMagnitude);
            float currentAngleRadians = Mathf.Acos(Mathf.Clamp(
                Vector3.Dot(unitNormal0, unitNormal1),
                -1f,
                1f));
            float maximumAngleRadians = maximumAngleDegrees * Mathf.Deg2Rad;
            if (!IsFinite(currentAngleRadians) ||
                currentAngleRadians <= maximumAngleRadians)
            {
                return false;
            }

            float gradientSquaredSum =
                gradient0.sqrMagnitude +
                gradient1.sqrMagnitude +
                gradient2.sqrMagnitude +
                gradient3.sqrMagnitude;
            if (!IsFinite(gradientSquaredSum) ||
                gradientSquaredSum < MinimumNormalSquaredMagnitude)
            {
                return false;
            }

            float lambda =
                (currentAngleRadians - maximumAngleRadians) /
                gradientSquaredSum * Mathf.Clamp01(stiffness);
            if (Vector3.Dot(Vector3.Cross(unitNormal0, unitNormal1), sharedEdge) > 0f)
            {
                lambda = -lambda;
            }

            opposite0Correction = -lambda * gradient0;
            opposite1Correction = -lambda * gradient1;
            edge0Correction = -lambda * gradient2;
            edge1Correction = -lambda * gradient3;
            if (AreFinite(
                    opposite0Correction,
                    opposite1Correction,
                    edge0Correction,
                    edge1Correction))
            {
                return true;
            }

            opposite0Correction = Vector3.zero;
            opposite1Correction = Vector3.zero;
            edge0Correction = Vector3.zero;
            edge1Correction = Vector3.zero;
            return false;
        }

        private static bool TryCalculateUnitNormals(
            Vector3 opposite0,
            Vector3 opposite1,
            Vector3 edge0,
            Vector3 edge1,
            out Vector3 normal0,
            out Vector3 normal1)
        {
            normal0 = Vector3.zero;
            normal1 = Vector3.zero;
            if (!AreFinite(opposite0, opposite1, edge0, edge1))
            {
                return false;
            }

            Vector3 candidate0 = Vector3.Cross(
                edge0 - opposite0,
                edge1 - opposite0);
            Vector3 candidate1 = Vector3.Cross(
                edge1 - opposite1,
                edge0 - opposite1);
            if (candidate0.sqrMagnitude < MinimumNormalSquaredMagnitude ||
                candidate1.sqrMagnitude < MinimumNormalSquaredMagnitude)
            {
                return false;
            }

            normal0 = candidate0 / Mathf.Sqrt(candidate0.sqrMagnitude);
            normal1 = candidate1 / Mathf.Sqrt(candidate1.sqrMagnitude);
            return true;
        }

        private static bool AreFinite(
            Vector3 value0,
            Vector3 value1,
            Vector3 value2,
            Vector3 value3)
        {
            return IsFinite(value0) &&
                   IsFinite(value1) &&
                   IsFinite(value2) &&
                   IsFinite(value3);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
