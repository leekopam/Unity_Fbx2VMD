using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 엄지 베이스 helper의 동기화와 안정화 목표 회전을 값 입력만으로 계산함.
    /// </summary>
    internal static class ThumbBaseHelperRotationCalculator
    {
        internal static Quaternion CalculateBaseRotation(
            Quaternion helperInitialRotation,
            Quaternion sourceRotation,
            bool hasSourceInitialRotation,
            Quaternion sourceInitialRotation,
            bool syncEnabled,
            float syncWeight,
            Quaternion deltaAxisRemap,
            Quaternion targetRotationOffset,
            bool stabilizePalm,
            float palmWeight)
        {
            Quaternion targetRotation = helperInitialRotation;
            if (syncEnabled && syncWeight > 0f)
            {
                targetRotation = sourceRotation;
                if (hasSourceInitialRotation)
                {
                    Quaternion sourceDelta = Quaternion.Inverse(sourceInitialRotation) * sourceRotation;
                    if (deltaAxisRemap != Quaternion.identity)
                    {
                        sourceDelta = deltaAxisRemap * sourceDelta * Quaternion.Inverse(deltaAxisRemap);
                    }

                    targetRotation = helperInitialRotation * sourceDelta;
                }

                if (syncWeight < 0.999f)
                {
                    targetRotation = Quaternion.Slerp(helperInitialRotation, targetRotation, syncWeight);
                }
            }

            // rig별 정적 회전 offset은 동기화된 목표 회전 뒤에 적용함.
            if (targetRotationOffset != Quaternion.identity)
            {
                targetRotation *= targetRotationOffset;
            }

            // 스킨용 helper의 손바닥 기준 자세는 정적 offset 적용 뒤에 안정화함.
            if (stabilizePalm && palmWeight > 0f)
            {
                targetRotation = Quaternion.Slerp(targetRotation, helperInitialRotation, palmWeight);
            }

            return targetRotation;
        }

        internal static Quaternion FinalizeRotation(
            Quaternion helperInitialRotation,
            Quaternion targetRotation,
            bool stabilizeWebbing,
            float webbingWeight,
            float helperMaxAngle,
            bool stabilizePalm,
            float palmWeight,
            float palmMaxAngle,
            float webbingMaxAngle)
        {
            if (stabilizeWebbing && webbingWeight > 0f)
            {
                targetRotation = Quaternion.Slerp(targetRotation, helperInitialRotation, webbingWeight);
            }

            float maximumAngle = Mathf.Clamp(helperMaxAngle, 0f, 45f);
            if (stabilizePalm && palmWeight > 0f)
            {
                maximumAngle = Mathf.Min(maximumAngle, Mathf.Clamp(palmMaxAngle, 0f, 45f));
            }

            if (stabilizeWebbing && webbingWeight > 0f)
            {
                maximumAngle = Mathf.Min(maximumAngle, Mathf.Clamp(webbingMaxAngle, 0f, 45f));
            }

            if (maximumAngle <= 0.001f)
            {
                return helperInitialRotation;
            }

            return Quaternion.Angle(helperInitialRotation, targetRotation) > maximumAngle
                ? Quaternion.RotateTowards(helperInitialRotation, targetRotation, maximumAngle)
                : targetRotation;
        }
    }
}
