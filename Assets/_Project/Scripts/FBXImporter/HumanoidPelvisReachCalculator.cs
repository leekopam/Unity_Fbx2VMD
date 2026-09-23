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
                // 지지 강도는 발 목표 보간에 이미 반영됨. 다시 감쇠를 줄이면 부분 착지에서 무릎이 거의 펴짐.
                double damped = original + IntegrateDamping(requested, dampingThresholdRadians) -
                    IntegrateDamping(original, dampingThresholdRadians);
                reach = Math.Sqrt(Math.Max(0d, upper * upper + lower * lower - 2d * upper * lower * Math.Cos(damped)));
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
            if (!TryCalculateFootOffset(left, up, candidate, out float leftShift) ||
                !TryCalculateFootOffset(right, up, candidate, out float rightShift) ||
                !CanReach(left, up, candidate, leftShift) ||
                !CanReach(right, up, candidate, rightShift))
                return false;

            offset = candidate;
            footOffsets = new Vector2(leftShift, rightShift);
            return true;
        }

        private static bool TryIntersect(Leg leg, Vector3 up, ref double minimum, ref double maximum)
        {
            if (!TryGetRelativeHeightInterval(leg, up, out double low, out double high))
                return false;

            // 발의 골반 추종을 필수 이동으로 묶지 않고 원래 추종량과 0 사이에서 허용함.
            // 도달 가능한 발 목표를 지지 강도로 다시 나누어 몸을 급히 내리는 것을 막음.
            if (low <= 0d)
                low = leg.SupportWeight > 0f
                    ? Math.Max(low / leg.SupportWeight, low - leg.SoleClearance)
                    : low - leg.SoleClearance;
            if (high >= 0d)
                high = leg.SupportWeight > 0f ? high / leg.SupportWeight : double.PositiveInfinity;

            minimum = Math.Max(minimum, low);
            maximum = Math.Min(maximum, high);
            return minimum <= maximum;
        }

        private static bool TryGetRelativeHeightInterval(Leg leg, Vector3 up,
            out double minimum, out double maximum)
        {
            minimum = maximum = 0d;
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
            minimum = -vertical - height;
            maximum = vertical - height;
            return true;
        }

        private static bool TryCalculateFootOffset(Leg leg, Vector3 up, float offset, out float footOffset)
        {
            footOffset = 0f;
            if (!TryGetRelativeHeightInterval(leg, up, out double minimum, out double maximum))
                return false;

            double preferred = Mathf.Max((1f - leg.SupportWeight) * offset, -leg.SoleClearance);
            double reachable = Math.Max(offset - maximum, Math.Min(preferred, offset - minimum));
            // 경계의 float 반올림으로 지지 발이 움직이거나 밑창 여유를 넘지 않게 함.
            footOffset = (float)Math.Max(Math.Min(0d, preferred), Math.Min(Math.Max(0d, preferred), reachable));
            return IsFinite(footOffset);
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
