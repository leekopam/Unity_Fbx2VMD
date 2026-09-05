using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Humanoid 표준 자세의 팔 오차가 큰 프레임만 점진적으로 보정함.
    /// </summary>
    internal static class HumanoidCanonicalArmPoseCorrectionPolicy
    {
        private const float ActivationError = 0.005f;
        private const float FullCorrectionError = 0.02f;

        internal static float CalculateBlendWeight(float meanError)
        {
            if (!IsFinite(meanError) || meanError <= ActivationError)
            {
                return 0f;
            }

            if (meanError >= FullCorrectionError)
            {
                return 1f;
            }

            float normalized = Mathf.InverseLerp(
                ActivationError,
                FullCorrectionError,
                meanError);
            return normalized * normalized * (3f - 2f * normalized);
        }

        internal static bool TryBlend(
            float[] sourceMuscles,
            float[] targetMuscles,
            out float[] blendedMuscles,
            out float meanError,
            out float blendWeight)
        {
            blendedMuscles = null;
            meanError = 0f;
            blendWeight = 0f;

            if (!IsValidMuscleArray(sourceMuscles) ||
                !IsValidMuscleArray(targetMuscles))
            {
                return false;
            }

            int armMuscleCount = 0;
            float errorSum = 0f;
            for (int index = 0; index < HumanTrait.MuscleCount; index++)
            {
                if (!IsArmMuscle(HumanTrait.MuscleName[index]))
                {
                    continue;
                }

                float sourceValue = Mathf.Clamp(sourceMuscles[index], -1f, 1f);
                errorSum += Mathf.Abs(targetMuscles[index] - sourceValue);
                armMuscleCount++;
            }

            if (armMuscleCount == 0)
            {
                return false;
            }

            meanError = errorSum / armMuscleCount;
            blendWeight = CalculateBlendWeight(meanError);
            blendedMuscles = (float[])targetMuscles.Clone();
            if (blendWeight <= 0f)
            {
                return true;
            }

            for (int index = 0; index < HumanTrait.MuscleCount; index++)
            {
                if (!IsArmMuscle(HumanTrait.MuscleName[index]))
                {
                    continue;
                }

                float sourceValue = Mathf.Clamp(sourceMuscles[index], -1f, 1f);
                blendedMuscles[index] = Mathf.Clamp(
                    Mathf.Lerp(targetMuscles[index], sourceValue, blendWeight),
                    -1f,
                    1f);
            }

            return true;
        }

        private static bool IsValidMuscleArray(float[] muscles)
        {
            if (muscles == null || muscles.Length < HumanTrait.MuscleCount)
            {
                return false;
            }

            for (int index = 0; index < HumanTrait.MuscleCount; index++)
            {
                if (!IsFinite(muscles[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsArmMuscle(string muscleName)
        {
            return !string.IsNullOrEmpty(muscleName) &&
                (muscleName.IndexOf("Arm", StringComparison.Ordinal) >= 0 ||
                 muscleName.IndexOf("Forearm", StringComparison.Ordinal) >= 0);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
