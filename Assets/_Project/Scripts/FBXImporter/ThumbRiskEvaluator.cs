using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public static class ThumbRiskEvaluator
    {
        public static float RiskAbove(float value, float warningValue, float fullRiskValue)
        {
            if (!IsFinite(value) || !IsFinite(warningValue) || !IsFinite(fullRiskValue))
            {
                return float.NaN;
            }

            if (fullRiskValue <= warningValue)
            {
                return value > warningValue ? 1f : 0f;
            }

            if (value <= warningValue)
            {
                return 0f;
            }

            return Mathf.Clamp01((value - warningValue) / (fullRiskValue - warningValue));
        }

        public static float RiskOutsideRange(float value, float minValue, float maxValue, float fullRiskDistance)
        {
            if (!IsFinite(value) || !IsFinite(minValue) || !IsFinite(maxValue) || !IsFinite(fullRiskDistance))
            {
                return float.NaN;
            }

            if (value < minValue)
            {
                return RiskAbove(minValue - value, 0f, fullRiskDistance);
            }

            if (value > maxValue)
            {
                return RiskAbove(value - maxValue, 0f, fullRiskDistance);
            }

            return 0f;
        }

        public static float MaxFinite(params float[] values)
        {
            if (values == null || values.Length == 0)
            {
                return float.NaN;
            }

            float maxValue = float.NaN;
            foreach (float value in values)
            {
                if (!IsFinite(value))
                {
                    continue;
                }

                if (!IsFinite(maxValue) || value > maxValue)
                {
                    maxValue = value;
                }
            }

            return maxValue;
        }

        public static float CalculateThumbWebbingPoseRisk(
            float spreadRisk,
            float projectionRisk,
            float helperDistanceRisk,
            float helperRotationRisk)
        {
            return MaxFinite(spreadRisk, projectionRisk, helperDistanceRisk, helperRotationRisk);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
