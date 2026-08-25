using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 엄지 localRotation 제한 결과를 값 입력만으로 계산함.
    /// </summary>
    internal static class ThumbLocalRotationCalculator
    {
        private const float OvershootRatio = 0.35f;
        private const float HardOvershootDegrees = 8f;

        internal static bool TryCalculateCorrection(
            Quaternion initialRotation,
            Quaternion rawRotation,
            Quaternion currentDisplayedRotation,
            Quaternion limitSpaceOffset,
            float softLimit,
            out Quaternion correctedRotation)
        {
            correctedRotation = rawRotation;
            if (softLimit <= 0f)
            {
                if (Quaternion.Angle(initialRotation, rawRotation) <= 0.001f)
                {
                    return false;
                }

                correctedRotation = initialRotation;
                return true;
            }

            Quaternion baselineRotation = initialRotation * limitSpaceOffset;
            Quaternion currentRotation = rawRotation * limitSpaceOffset;
            float angle = Quaternion.Angle(baselineRotation, currentRotation);
            if (angle <= softLimit)
            {
                return Quaternion.Angle(currentDisplayedRotation, rawRotation) > 0.001f;
            }

            Quaternion limitedRotation = LimitLocalRotation(
                baselineRotation,
                currentRotation,
                softLimit);
            correctedRotation = limitedRotation * Quaternion.Inverse(limitSpaceOffset);
            return true;
        }

        private static Quaternion LimitLocalRotation(
            Quaternion initialRotation,
            Quaternion currentRotation,
            float softLimit)
        {
            float angle = Quaternion.Angle(initialRotation, currentRotation);
            float hardLimit = softLimit + HardOvershootDegrees;
            float targetAngle = Mathf.Min(
                hardLimit,
                softLimit + (angle - softLimit) * OvershootRatio);
            return Quaternion.RotateTowards(initialRotation, currentRotation, targetAngle);
        }
    }
}
