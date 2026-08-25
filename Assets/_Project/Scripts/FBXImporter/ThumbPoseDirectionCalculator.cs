using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 엄지 방향의 벌어짐과 손바닥 투영 보정을 값 입력만으로 계산함.
    /// </summary>
    internal static class ThumbPoseDirectionCalculator
    {
        internal static bool TryCalculateCorrectedDirection(
            Vector3 sourceDirection,
            Vector3 indexDirection,
            bool hasIndexDirection,
            Vector3 sideAxis,
            Vector3 palmNormal,
            Vector3 forwardAxis,
            float minimumPalmNormal,
            float maximumPalmNormal,
            float maximumSpreadAngle,
            float indexSpreadWeight,
            float projectionWeight,
            out Vector3 correctedDirection)
        {
            Vector3 targetDirection = sourceDirection;
            float correctionWeight = 0f;

            // 엄지가 검지에서 과하게 벌어진 경우에만 검지 방향으로 당김.
            bool ApplySpreadConstraint(ref Vector3 candidateDirection)
            {
                if (!hasIndexDirection ||
                    indexSpreadWeight <= 0f ||
                    maximumSpreadAngle >= 89.999f)
                {
                    return false;
                }

                float spreadAngle = Vector3.Angle(candidateDirection, indexDirection);
                if (spreadAngle <= maximumSpreadAngle + 0.001f)
                {
                    return false;
                }

                Vector3 spreadCorrectedDirection = Vector3.RotateTowards(
                    candidateDirection,
                    indexDirection,
                    (spreadAngle - maximumSpreadAngle) * Mathf.Deg2Rad,
                    0f);
                if (!TryNormalize(spreadCorrectedDirection, out spreadCorrectedDirection))
                {
                    return false;
                }

                candidateDirection = spreadCorrectedDirection;
                correctionWeight = Mathf.Max(correctionWeight, Mathf.Clamp01(indexSpreadWeight));
                return true;
            }

            // 손바닥 좌표계의 법선 성분만 허용 범위로 제한함.
            bool ApplyProjectionConstraint(ref Vector3 candidateDirection)
            {
                if (projectionWeight <= 0f)
                {
                    return false;
                }

                float side = Vector3.Dot(candidateDirection, sideAxis);
                float normal = Vector3.Dot(candidateDirection, palmNormal);
                float forward = Vector3.Dot(candidateDirection, forwardAxis);
                float clampedNormal = Mathf.Clamp(normal, minimumPalmNormal, maximumPalmNormal);
                if (Mathf.Abs(clampedNormal - normal) <= 0.001f)
                {
                    return false;
                }

                Vector3 projectionCorrectedDirection =
                    sideAxis * side +
                    palmNormal * clampedNormal +
                    forwardAxis * forward;
                if (!TryNormalize(projectionCorrectedDirection, out projectionCorrectedDirection))
                {
                    return false;
                }

                candidateDirection = projectionCorrectedDirection;
                correctionWeight = Mathf.Max(correctionWeight, Mathf.Clamp01(projectionWeight));
                return true;
            }

            // 두 제한이 서로를 다시 위반할 수 있어 기존 순서로 세 번까지 수렴시킴.
            for (int pass = 0; pass < 3; pass++)
            {
                bool changed = false;
                changed |= ApplySpreadConstraint(ref targetDirection);
                changed |= ApplyProjectionConstraint(ref targetDirection);
                changed |= ApplySpreadConstraint(ref targetDirection);
                if (!TryNormalize(targetDirection, out targetDirection))
                {
                    correctedDirection = sourceDirection;
                    return false;
                }

                if (!changed)
                {
                    break;
                }
            }

            if (correctionWeight <= 0f)
            {
                correctedDirection = sourceDirection;
                return false;
            }

            correctedDirection = Vector3.Slerp(sourceDirection, targetDirection, correctionWeight);
            if (!TryNormalize(correctedDirection, out correctedDirection) ||
                Vector3.Angle(sourceDirection, correctedDirection) <= 0.1f)
            {
                correctedDirection = sourceDirection;
                return false;
            }

            return true;
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value) || value.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            normalized = value.normalized;
            return IsFinite(normalized);
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
