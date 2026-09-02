using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 리타게팅 근육 기준의 적용 대상과 입력 부호를 판정함.
    /// </summary>
    internal static class RetargetingMuscleReferencePolicy
    {
        private const float UpperArmTwistReferenceSignMagnitudeTolerance = 0.35f;
        private const float UpperArmTwistOverrangeReferenceSignMagnitudeTolerance = 1.5f;
        private const float UpperArmTwistReferenceSignMaxAbs = 2.25f;
        private const float RightUpperArmTwistReferenceSignMinAbs = 2f;

        internal static bool ShouldUsePoseReference(
            bool enableFingerPoseReference,
            bool enableFullBodyPoseReference,
            int fingerReferenceMuscleCount)
        {
            return enableFullBodyPoseReference ||
                (enableFingerPoseReference && fingerReferenceMuscleCount > 0);
        }

        internal static bool ShouldUseHumanoidMuscleReference(int muscleIndex)
        {
            if (!TryGetNormalizedMuscleName(muscleIndex, out string normalized))
            {
                return false;
            }

            if (IsLeftUpperArmTwistMuscle(normalized))
            {
                return false;
            }

            if (IsForearmStretchMuscle(normalized))
            {
                return false;
            }

            return normalized.Contains("shoulder") ||
                   normalized.Contains("arm") ||
                   normalized.Contains("forearm") ||
                   normalized.Contains("hand") ||
                   normalized.Contains("thumb") ||
                   normalized.Contains("index") ||
                   normalized.Contains("middle") ||
                   normalized.Contains("ring") ||
                   normalized.Contains("little");
        }

        internal static bool ShouldPreserveHumanoidMuscleDuringVisualSmoothing(
            int muscleIndex,
            bool useHumanoidMuscleReference,
            bool hasHumanoidMuscleReferenceCurve)
        {
            return useHumanoidMuscleReference &&
                hasHumanoidMuscleReferenceCurve &&
                ShouldUseHumanoidMuscleReference(muscleIndex);
        }

        internal static bool IsForearmStretchMuscle(int muscleIndex)
        {
            return TryGetNormalizedMuscleName(muscleIndex, out string normalized) &&
                IsForearmStretchMuscle(normalized);
        }

        internal static bool ShouldApplyHumanoidMuscleReferenceValue(int muscleIndex, float referenceValue)
        {
            if (!ShouldUseHumanoidMuscleReference(muscleIndex) || !IsFinite(referenceValue))
            {
                return false;
            }

            string normalized = NormalizeMuscleName(HumanTrait.MuscleName[muscleIndex]);
            return !IsRightUpperArmTwistMuscle(normalized) || Mathf.Abs(referenceValue) <= 1f;
        }

        internal static float TransformPoseInputValue(int muscleIndex, float value)
        {
            if (!TryGetNormalizedMuscleName(muscleIndex, out string normalized))
            {
                return value;
            }

            return IsLeftUpperArmTwistMuscle(normalized) ? -value : value;
        }

        internal static float AlignPoseInputWithReference(int muscleIndex, float value, float referenceValue)
        {
            if (!TryGetNormalizedMuscleName(muscleIndex, out string normalized))
            {
                return value;
            }

            bool isLeftUpperArmTwist = IsLeftUpperArmTwistMuscle(normalized);
            bool isRightUpperArmTwist = IsRightUpperArmTwistMuscle(normalized);
            if ((!isLeftUpperArmTwist && !isRightUpperArmTwist) ||
                !IsFinite(value) ||
                !IsFinite(referenceValue) ||
                Mathf.Approximately(value, 0f) ||
                Mathf.Approximately(referenceValue, 0f))
            {
                return value;
            }

            float absReference = Mathf.Abs(referenceValue);
            float magnitudeTolerance = absReference <= 1f
                ? UpperArmTwistReferenceSignMagnitudeTolerance
                : UpperArmTwistOverrangeReferenceSignMagnitudeTolerance;
            if (absReference > UpperArmTwistReferenceSignMaxAbs ||
                Mathf.Abs(Mathf.Abs(value) - absReference) > magnitudeTolerance)
            {
                return value;
            }

            if (isLeftUpperArmTwist && Mathf.Sign(value) != Mathf.Sign(referenceValue))
            {
                return -value;
            }

            if (isRightUpperArmTwist &&
                absReference >= RightUpperArmTwistReferenceSignMinAbs &&
                Mathf.Sign(value) == Mathf.Sign(referenceValue))
            {
                return -value;
            }

            return value;
        }

        internal static bool ShouldApplyManualFullBodyMuscle(
            int muscleIndex,
            bool rightSleeveChainOnly,
            bool rightArmOnly,
            bool leftArmOnly,
            bool legTwistOnly,
            bool lowerBodyOnly,
            bool excludeLowerBody)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return true;
            }

            string muscleName = HumanTrait.MuscleName[muscleIndex];
            if (rightSleeveChainOnly)
            {
                return IsRightSleeveChainMuscle(muscleName);
            }

            if (rightArmOnly)
            {
                return IsRightArmMuscle(muscleName);
            }

            if (leftArmOnly)
            {
                return IsLeftArmMuscle(muscleName);
            }

            bool isLowerBody = IsLowerBodyMuscle(muscleName);
            if (legTwistOnly)
            {
                return IsLegTwistOrInOutMuscle(muscleName);
            }

            if (lowerBodyOnly)
            {
                return isLowerBody;
            }

            return !excludeLowerBody || !isLowerBody;
        }

        internal static bool IsFingerMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeMuscleName(muscleName);
            return normalized.Contains("thumb") ||
                   normalized.Contains("index") ||
                   normalized.Contains("middle") ||
                   normalized.Contains("ring") ||
                   normalized.Contains("little");
        }

        internal static string NormalizeMuscleName(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return string.Empty;
            }

            string normalized = muscleName
                .Replace(" ", "")
                .Replace(".", "")
                .Replace("-", "")
                .Replace("_", "")
                .ToLowerInvariant();
            return normalized.Replace("lefthandthumb", "leftthumb")
                .Replace("lefthandindex", "leftindex")
                .Replace("lefthandmiddle", "leftmiddle")
                .Replace("lefthandring", "leftring")
                .Replace("lefthandlittle", "leftlittle")
                .Replace("righthandthumb", "rightthumb")
                .Replace("righthandindex", "rightindex")
                .Replace("righthandmiddle", "rightmiddle")
                .Replace("righthandring", "rightring")
                .Replace("righthandlittle", "rightlittle");
        }

        internal static int FindHumanMuscleIndex(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return -1;
            }

            string normalizedInput = NormalizeMuscleName(muscleName);
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string humanMuscleName = HumanTrait.MuscleName[i];
                if (string.Equals(humanMuscleName, muscleName, System.StringComparison.Ordinal) ||
                    string.Equals(
                        NormalizeMuscleName(humanMuscleName),
                        normalizedInput,
                        System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryGetNormalizedMuscleName(int muscleIndex, out string normalized)
        {
            normalized = string.Empty;
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return false;
            }

            normalized = NormalizeMuscleName(HumanTrait.MuscleName[muscleIndex]);
            return true;
        }

        private static bool IsForearmStretchMuscle(string normalizedMuscleName)
        {
            return normalizedMuscleName.Contains("forearm") &&
                normalizedMuscleName.Contains("stretch");
        }

        private static bool IsLowerBodyMuscle(string muscleName)
        {
            string normalized = NormalizeMuscleName(muscleName);
            return normalized.Contains("upperleg") ||
                   normalized.Contains("lowerleg") ||
                   normalized.Contains("foot") ||
                   normalized.Contains("toes");
        }

        private static bool IsLegTwistOrInOutMuscle(string muscleName)
        {
            string normalized = NormalizeMuscleName(muscleName);
            bool isLeg = normalized.Contains("upperleg") ||
                         normalized.Contains("lowerleg") ||
                         normalized.Contains("foot");
            return isLeg &&
                (normalized.Contains("inout") || normalized.Contains("twist"));
        }

        private static bool IsRightArmMuscle(string muscleName)
        {
            return IsArmMuscle(muscleName, "right");
        }

        private static bool IsLeftArmMuscle(string muscleName)
        {
            return IsArmMuscle(muscleName, "left");
        }

        private static bool IsArmMuscle(string muscleName, string side)
        {
            string normalized = NormalizeMuscleName(muscleName);
            if (!normalized.Contains(side) || ContainsFingerName(normalized))
            {
                return false;
            }

            return normalized.Contains("shoulder") ||
                   normalized.Contains("arm") ||
                   normalized.Contains("forearm");
        }

        private static bool IsRightSleeveChainMuscle(string muscleName)
        {
            string normalized = NormalizeMuscleName(muscleName);
            if (normalized.Contains("spine") ||
                normalized.Contains("chest") ||
                normalized.Contains("upperchest"))
            {
                return true;
            }

            if (!normalized.Contains("right") ||
                ContainsFingerName(normalized) ||
                normalized.Contains("hand"))
            {
                return false;
            }

            return normalized.Contains("shoulder") ||
                   normalized.Contains("arm") ||
                   normalized.Contains("forearm");
        }

        private static bool ContainsFingerName(string normalizedMuscleName)
        {
            return normalizedMuscleName.Contains("thumb") ||
                   normalizedMuscleName.Contains("index") ||
                   normalizedMuscleName.Contains("middle") ||
                   normalizedMuscleName.Contains("ring") ||
                   normalizedMuscleName.Contains("little");
        }

        private static bool IsLeftUpperArmTwistMuscle(string normalizedMuscleName)
        {
            return !string.IsNullOrEmpty(normalizedMuscleName) &&
                normalizedMuscleName.Contains("left") &&
                normalizedMuscleName.Contains("arm") &&
                normalizedMuscleName.Contains("twist") &&
                !normalizedMuscleName.Contains("forearm");
        }

        private static bool IsRightUpperArmTwistMuscle(string normalizedMuscleName)
        {
            return !string.IsNullOrEmpty(normalizedMuscleName) &&
                normalizedMuscleName.Contains("right") &&
                normalizedMuscleName.Contains("arm") &&
                normalizedMuscleName.Contains("twist") &&
                !normalizedMuscleName.Contains("forearm");
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
