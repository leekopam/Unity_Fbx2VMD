using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Humanoid 팔 품질 진단에 필요한 방향·Twist·근접 거리를 계산함.
    /// </summary>
    internal static class HumanoidArmQualityMetricCalculator
    {
        private const float Epsilon = 0.000001f;

        internal static bool TryCalculateDirectionErrorDegrees(
            Vector3 expectedDirection,
            Vector3 actualDirection,
            out float errorDegrees)
        {
            errorDegrees = 0f;
            if (!IsUsableDirection(expectedDirection) ||
                !IsUsableDirection(actualDirection))
            {
                return false;
            }

            errorDegrees = Vector3.Angle(expectedDirection, actualDirection);
            if (IsFinite(errorDegrees))
            {
                return true;
            }

            errorDegrees = 0f;
            return false;
        }

        internal static bool TryCalculateTwistAngleDegrees(
            Quaternion rotationDelta,
            Vector3 twistAxis,
            out float angleDegrees)
        {
            angleDegrees = 0f;
            if (!IsFinite(rotationDelta) || !IsUsableDirection(twistAxis))
            {
                return false;
            }

            float rotationMagnitudeSquared =
                rotationDelta.x * rotationDelta.x +
                rotationDelta.y * rotationDelta.y +
                rotationDelta.z * rotationDelta.z +
                rotationDelta.w * rotationDelta.w;
            if (rotationMagnitudeSquared <= Epsilon)
            {
                return false;
            }

            float inverseRotationMagnitude = 1f / Mathf.Sqrt(rotationMagnitudeSquared);
            Quaternion normalizedRotation = new Quaternion(
                rotationDelta.x * inverseRotationMagnitude,
                rotationDelta.y * inverseRotationMagnitude,
                rotationDelta.z * inverseRotationMagnitude,
                rotationDelta.w * inverseRotationMagnitude);
            Vector3 axis = twistAxis.normalized;
            Vector3 rotationVector = new Vector3(
                normalizedRotation.x,
                normalizedRotation.y,
                normalizedRotation.z);
            Vector3 projectedRotation = axis * Vector3.Dot(rotationVector, axis);
            Quaternion twist = new Quaternion(
                projectedRotation.x,
                projectedRotation.y,
                projectedRotation.z,
                normalizedRotation.w);

            float twistMagnitudeSquared =
                twist.x * twist.x + twist.y * twist.y +
                twist.z * twist.z + twist.w * twist.w;
            if (twistMagnitudeSquared <= Epsilon)
            {
                return false;
            }

            float inverseTwistMagnitude = 1f / Mathf.Sqrt(twistMagnitudeSquared);
            twist = new Quaternion(
                twist.x * inverseTwistMagnitude,
                twist.y * inverseTwistMagnitude,
                twist.z * inverseTwistMagnitude,
                twist.w * inverseTwistMagnitude);
            angleDegrees = Quaternion.Angle(Quaternion.identity, twist);
            if (IsFinite(angleDegrees))
            {
                return true;
            }

            angleDegrees = 0f;
            return false;
        }

        internal static bool TryCalculateSegmentDistance(
            Vector3 firstStart,
            Vector3 firstEnd,
            Vector3 secondStart,
            Vector3 secondEnd,
            out float distance)
        {
            distance = 0f;
            if (!IsFinite(firstStart) || !IsFinite(firstEnd) ||
                !IsFinite(secondStart) || !IsFinite(secondEnd))
            {
                return false;
            }

            Vector3 firstDelta = firstEnd - firstStart;
            Vector3 secondDelta = secondEnd - secondStart;
            float firstLengthSquared = firstDelta.sqrMagnitude;
            float secondLengthSquared = secondDelta.sqrMagnitude;

            if (firstLengthSquared <= Epsilon && secondLengthSquared <= Epsilon)
            {
                distance = Vector3.Distance(firstStart, secondStart);
                return true;
            }

            if (firstLengthSquared <= Epsilon)
            {
                distance = CalculatePointSegmentDistance(
                    firstStart,
                    secondStart,
                    secondEnd);
                return true;
            }

            if (secondLengthSquared <= Epsilon)
            {
                distance = CalculatePointSegmentDistance(
                    secondStart,
                    firstStart,
                    firstEnd);
                return true;
            }

            Vector3 startDelta = firstStart - secondStart;
            float firstSecondDot = Vector3.Dot(firstDelta, secondDelta);
            float firstStartDot = Vector3.Dot(firstDelta, startDelta);
            float secondStartDot = Vector3.Dot(secondDelta, startDelta);
            float denominator =
                firstLengthSquared * secondLengthSquared -
                firstSecondDot * firstSecondDot;

            float firstParameter = 0f;
            if (denominator > Epsilon)
            {
                firstParameter = Mathf.Clamp01(
                    (firstSecondDot * secondStartDot -
                     firstStartDot * secondLengthSquared) /
                    denominator);
            }

            float secondParameter = Mathf.Clamp01(
                (firstSecondDot * firstParameter + secondStartDot) /
                secondLengthSquared);
            firstParameter = Mathf.Clamp01(
                (firstSecondDot * secondParameter - firstStartDot) /
                firstLengthSquared);

            Vector3 firstClosest = firstStart + firstDelta * firstParameter;
            Vector3 secondClosest = secondStart + secondDelta * secondParameter;
            distance = Vector3.Distance(firstClosest, secondClosest);
            return IsFinite(distance);
        }

        private static float CalculatePointSegmentDistance(
            Vector3 point,
            Vector3 segmentStart,
            Vector3 segmentEnd)
        {
            Vector3 segment = segmentEnd - segmentStart;
            float parameter = Mathf.Clamp01(
                Vector3.Dot(point - segmentStart, segment) / segment.sqrMagnitude);
            return Vector3.Distance(point, segmentStart + segment * parameter);
        }

        private static bool IsUsableDirection(Vector3 value)
        {
            return IsFinite(value) && value.sqrMagnitude > Epsilon;
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
