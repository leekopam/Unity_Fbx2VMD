using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 엄지 자세 위험도를 값 입력만으로 계산함.
    /// </summary>
    internal static class ThumbPoseRiskCalculator
    {
        private const float HelperDistanceDeltaWarning = 0.003f;
        private const float HelperDistanceDeltaFullRisk = 0.008f;
        private const float HelperRotationWarning = 28f;
        private const float HelperRotationFullRisk = 70f;
        private const float WebbingRotationWarning = 18f;
        private const float WebbingRotationFullRisk = 45f;
        private const float ReferenceFrameSpreadDeviationToleranceDegrees = 1.5f;
        private const float ReferenceFrameProjectionDeviationTolerance = 0.015f;

        internal static float FindMaximumFinite(params float[] values)
        {
            if (values == null || values.Length == 0)
            {
                return float.NaN;
            }

            float maximum = float.NaN;
            foreach (float value in values)
            {
                if (!IsFinite(value))
                {
                    continue;
                }

                if (!IsFinite(maximum) || value > maximum)
                {
                    maximum = value;
                }
            }

            return maximum;
        }

        internal static float CalculateAboveThreshold(
            float value,
            float warningThreshold,
            float fullRiskThreshold)
        {
            if (!IsFinite(value) || !IsFinite(warningThreshold) || !IsFinite(fullRiskThreshold))
            {
                return float.NaN;
            }

            if (fullRiskThreshold <= warningThreshold)
            {
                return value > warningThreshold ? 1f : 0f;
            }

            if (value <= warningThreshold)
            {
                return 0f;
            }

            return Mathf.Clamp01((value - warningThreshold) / (fullRiskThreshold - warningThreshold));
        }

        internal static float CalculateOutsideRange(
            float value,
            float minimum,
            float maximum,
            float fullRiskDistance)
        {
            if (!IsFinite(value) || !IsFinite(minimum) || !IsFinite(maximum) || !IsFinite(fullRiskDistance))
            {
                return float.NaN;
            }

            if (value < minimum)
            {
                return CalculateAboveThreshold(minimum - value, 0f, fullRiskDistance);
            }

            if (value > maximum)
            {
                return CalculateAboveThreshold(value - maximum, 0f, fullRiskDistance);
            }

            return 0f;
        }

        internal static float CalculateReferenceFrameDeviation(
            float spreadAngle,
            float projection,
            float referenceSpreadAngle,
            float referenceProjection)
        {
            float spreadDeviation = Mathf.Max(
                0f,
                Mathf.Abs(spreadAngle - referenceSpreadAngle) - ReferenceFrameSpreadDeviationToleranceDegrees);
            float projectionDeviation = Mathf.Max(
                0f,
                Mathf.Abs(projection - referenceProjection) - ReferenceFrameProjectionDeviationTolerance);
            return spreadDeviation + projectionDeviation * 100f;
        }

        internal static bool TryCalculateHelperRelationshipRisk(
            float currentDistance,
            float initialDistance,
            float rotationDelta,
            float spreadRisk,
            float projectionRisk,
            out float helperDistanceRisk,
            out float helperRotationRisk,
            out float webbingRisk)
        {
            helperDistanceRisk = float.NaN;
            helperRotationRisk = float.NaN;
            webbingRisk = float.NaN;

            if (IsFinite(currentDistance) && IsFinite(initialDistance))
            {
                helperDistanceRisk = CalculateAboveThreshold(
                    Mathf.Abs(currentDistance - initialDistance),
                    HelperDistanceDeltaWarning,
                    HelperDistanceDeltaFullRisk);
            }

            if (IsFinite(rotationDelta))
            {
                helperRotationRisk = CalculateAboveThreshold(
                    rotationDelta,
                    HelperRotationWarning,
                    HelperRotationFullRisk);
                webbingRisk = FindMaximumFinite(
                    spreadRisk,
                    projectionRisk,
                    helperDistanceRisk,
                    CalculateAboveThreshold(
                        rotationDelta,
                        WebbingRotationWarning,
                        WebbingRotationFullRisk));
            }

            return !float.IsNaN(FindMaximumFinite(
                helperDistanceRisk,
                helperRotationRisk,
                webbingRisk));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
