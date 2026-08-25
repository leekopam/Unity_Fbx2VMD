using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 엄지 자세 위험도를 값 입력만으로 계산함.
    /// </summary>
    internal static class ThumbPoseRiskCalculator
    {
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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
