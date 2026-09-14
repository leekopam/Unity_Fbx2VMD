using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 양다리의 도달 범위와 밑창 여유 안에서 가장 작은 골반 이동을 계산함.
    /// </summary>
    internal static class HumanoidPelvisReachCalculator
    {
        internal readonly struct Leg
        {
            internal readonly Vector3 TargetToHip;
            internal readonly float UpperLength;
            internal readonly float LowerLength;
            internal readonly float SupportWeight;
            internal readonly float SoleClearance;
            internal readonly double MaximumReach;

            internal Leg(Vector3 targetToHip, float upperLength, float lowerLength,
                float supportWeight, float soleClearance)
                : this(targetToHip, upperLength, lowerLength, supportWeight, soleClearance,
                    (double)upperLength + lowerLength)
            {
            }

            internal Leg(Vector3 targetToHip, float upperLength, float lowerLength,
                float supportWeight, float soleClearance, double maximumReach)
            {
                // 벡터는 발목 목표에서 허벅지 시작점으로 향함.
                TargetToHip = targetToHip;
                UpperLength = upperLength;
                LowerLength = lowerLength;
                SupportWeight = supportWeight;
                SoleClearance = soleClearance;
                MaximumReach = maximumReach;
            }
        }

        internal static bool TryCalculateExtensionReach(float upperLength, float lowerLength,
            float originalKneeAngleRadians, float targetDistance, float supportWeight,
            float dampingThresholdRadians, out float maximumReach)
        {
            maximumReach = 0f;
            if (!IsFinite(upperLength) || !IsFinite(lowerLength) || upperLength <= 0f || lowerLength <= 0f ||
                !IsFinite(originalKneeAngleRadians) || originalKneeAngleRadians < 0f || originalKneeAngleRadians > Mathf.PI ||
                !IsFinite(targetDistance) || targetDistance < 0f ||
                !IsFinite(supportWeight) || supportWeight < 0f || supportWeight > 1f ||
                !IsFinite(dampingThresholdRadians) || dampingThresholdRadians <= 0f || dampingThresholdRadians >= Mathf.PI)
                return false;

            double upper = upperLength;
            double lower = lowerLength;
            double reach = upper + lower;
            if (reach > float.MaxValue)
                return false;

            double original = Math.Min(Math.PI, originalKneeAngleRadians);
            double distance = Math.Min(reach, Math.Max(Math.Abs(upper - lower), targetDistance));
            double cosine = (upper * upper + lower * lower - distance * distance) / (2d * upper * lower);
            double requested = Math.Acos(Math.Max(-1d, Math.Min(1d, cosine)));
            if (supportWeight > 0f && requested > original && requested > dampingThresholdRadians)
            {
                // 원본보다 추가로 펴지는 양만 감쇠하여 원래 곧게 편 자세와 굴곡 동작은 유지함.
                double damped = original + IntegrateDamping(requested, dampingThresholdRadians) -
                    IntegrateDamping(original, dampingThresholdRadians);
                double angle = requested + (damped - requested) * supportWeight;
                reach = Math.Sqrt(Math.Max(0d, upper * upper + lower * lower - 2d * upper * lower * Math.Cos(angle)));
            }

            maximumReach = (float)reach;
            return IsFinite(maximumReach);
        }

        private static double IntegrateDamping(double angle, double threshold)
        {
            if (angle <= threshold)
                return angle;

            // Footskate Cleanup의 신전 감쇠 다항식을 적분하여 탐색 순서에 의존하지 않게 함.
            double span = Math.PI - threshold;
            double ratio = (angle - threshold) / span;
            return threshold + span * (ratio - ratio * ratio * ratio +
                0.5d * ratio * ratio * ratio * ratio);
        }

        internal static bool TryCalculateOffset(Leg left, Leg right, Vector3 up,
            float maximumOffset, out float offset, out Vector2 footOffsets)
        {
            offset = 0f;
            footOffsets = Vector2.zero;
            if (!IsFinite(up.sqrMagnitude) || Mathf.Abs(up.sqrMagnitude - 1f) > 0.0001f ||
                up.y <= 0f || !IsFinite(maximumOffset) || maximumOffset < 0f)
                return false;

            up = up.normalized;
            double minimum = -maximumOffset;
            double maximum = maximumOffset;
            if (!TryIntersect(left, up, ref minimum, ref maximum) ||
                !TryIntersect(right, up, ref minimum, ref maximum) || minimum > maximum)
                return false;

            float candidate = (float)Math.Max(minimum, Math.Min(0d, maximum));
            float leftShift = CalculateFootOffset(left, candidate);
            float rightShift = CalculateFootOffset(right, candidate);
            if (!CanReach(left, up, candidate, leftShift) ||
                !CanReach(right, up, candidate, rightShift))
                return false;

            offset = candidate;
            footOffsets = new Vector2(leftShift, rightShift);
            return true;
        }

        private static bool TryIntersect(Leg leg, Vector3 up, ref double minimum, ref double maximum)
        {
            if (!IsFinite(leg.TargetToHip.x) || !IsFinite(leg.TargetToHip.y) ||
                !IsFinite(leg.TargetToHip.z) || !IsFinite(leg.UpperLength) ||
                !IsFinite(leg.LowerLength) || leg.UpperLength <= 0f || leg.LowerLength <= 0f ||
                !IsFinite(leg.SupportWeight) || leg.SupportWeight < 0f || leg.SupportWeight > 1f ||
                !IsFinite(leg.SoleClearance) || leg.SoleClearance < 0f ||
                double.IsNaN(leg.MaximumReach) || double.IsInfinity(leg.MaximumReach) || leg.MaximumReach <= 0d ||
                leg.MaximumReach < Math.Abs((double)leg.UpperLength - leg.LowerLength) ||
                leg.MaximumReach > ((double)leg.UpperLength + leg.LowerLength) * 1.000001d)
                return false;

            double height = (double)leg.TargetToHip.x * up.x +
                (double)leg.TargetToHip.y * up.y + (double)leg.TargetToHip.z * up.z;
            double x = leg.TargetToHip.x - height * up.x;
            double y = leg.TargetToHip.y - height * up.y;
            double z = leg.TargetToHip.z - height * up.z;
            // float 한도로 전달된 합의 반올림은 허용하되 실제 본 길이 합을 넘겨 풀지 않음.
            double reach = Math.Min(leg.MaximumReach, (double)leg.UpperLength + leg.LowerLength);
            double verticalSquared = reach * reach - (x * x + y * y + z * z);
            if (verticalSquared < 0d)
                return false;

            double vertical = Math.Sqrt(verticalSquared);
            double low = -vertical - height - leg.SoleClearance;
            double high = vertical - height - leg.SoleClearance;
            if (leg.SupportWeight == 0f)
            {
                if (height < -vertical)
                    return false;
                if (height <= vertical)
                    high = double.PositiveInfinity;
            }
            else
            {
                // 바닥에 막힌 발과 자유롭게 따라오는 발의 두 구간을 함께 역산함.
                low = Math.Max(low, (-vertical - height) / leg.SupportWeight);
                high = Math.Max(high, (vertical - height) / leg.SupportWeight);
            }

            minimum = Math.Max(minimum, low);
            maximum = Math.Min(maximum, high);
            return minimum <= maximum;
        }

        private static float CalculateFootOffset(Leg leg, float offset)
        {
            return Mathf.Max((1f - leg.SupportWeight) * offset, -leg.SoleClearance);
        }

        private static bool CanReach(Leg leg, Vector3 up, float offset, float footOffset)
        {
            Vector3 distance = leg.TargetToHip + up * (offset - footOffset);
            double length = Math.Sqrt((double)distance.x * distance.x +
                (double)distance.y * distance.y + (double)distance.z * distance.z);
            double maximum = Math.Min(leg.MaximumReach, (double)leg.UpperLength + leg.LowerLength);
            double minimum = Math.Abs((double)leg.UpperLength - leg.LowerLength);
            double tolerance = maximum * 0.000001d;
            // 최대 도달 후보가 지나치게 접힌 자세이면 채택하지 않음. 다른 후보의 존재까지 부정하지 않음.
            return IsFinite(offset) && IsFinite(footOffset) && !double.IsNaN(length) &&
                length >= minimum - tolerance && length <= maximum + tolerance;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
