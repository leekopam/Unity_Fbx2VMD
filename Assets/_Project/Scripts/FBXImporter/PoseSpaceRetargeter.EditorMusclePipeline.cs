using UnityEngine;
using System;

namespace Fbx2Vmd.FBXImporter
{
    public partial class PoseSpaceRetargeter
    {
#if UNITY_EDITOR
        private static string ResolveFirstAnimatorStateName(RuntimeAnimatorController controller)
        {
            if (controller == null)
            {
                return "";
            }

            AnimatorOverrideController overrideController = controller as AnimatorOverrideController;
            if (overrideController != null && overrideController.runtimeAnimatorController != null)
            {
                controller = overrideController.runtimeAnimatorController;
            }

            UnityEditor.Animations.AnimatorController animatorController = controller as UnityEditor.Animations.AnimatorController;
            if (animatorController == null || animatorController.layers == null || animatorController.layers.Length == 0)
            {
                return "";
            }

            UnityEditor.Animations.ChildAnimatorState[] states = animatorController.layers[0].stateMachine.states;
            return states != null && states.Length > 0 ? states[0].state.name : "";
        }

        private static bool IsFingerMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            return normalized.Contains("thumb") ||
                   normalized.Contains("index") ||
                   normalized.Contains("middle") ||
                   normalized.Contains("ring") ||
                   normalized.Contains("little");
        }

        private static bool IsLowerBodyMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            return normalized.Contains("upperleg") ||
                   normalized.Contains("lowerleg") ||
                   normalized.Contains("foot") ||
                   normalized.Contains("toes");
        }

        private static bool IsLegTwistOrInOutMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            bool isLeg = normalized.Contains("upperleg") ||
                         normalized.Contains("lowerleg") ||
                         normalized.Contains("foot");
            if (!isLeg)
            {
                return false;
            }

            return normalized.Contains("inout") ||
                   normalized.Contains("twist");
        }

        private static bool IsRightArmPoseMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            if (!normalized.Contains("right"))
            {
                return false;
            }

            if (normalized.Contains("thumb") ||
                normalized.Contains("index") ||
                normalized.Contains("middle") ||
                normalized.Contains("ring") ||
                normalized.Contains("little"))
            {
                return false;
            }

            return normalized.Contains("shoulder") ||
                   normalized.Contains("arm") ||
                   normalized.Contains("forearm");
        }

        private static bool IsLeftArmPoseMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            if (!normalized.Contains("left"))
            {
                return false;
            }

            if (normalized.Contains("thumb") ||
                normalized.Contains("index") ||
                normalized.Contains("middle") ||
                normalized.Contains("ring") ||
                normalized.Contains("little"))
            {
                return false;
            }

            return normalized.Contains("shoulder") ||
                   normalized.Contains("arm") ||
                   normalized.Contains("forearm");
        }

        private static bool IsRightSleeveChainPoseMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            if (normalized.Contains("spine") ||
                normalized.Contains("chest") ||
                normalized.Contains("upperchest"))
            {
                return true;
            }

            if (!normalized.Contains("right"))
            {
                return false;
            }

            if (normalized.Contains("thumb") ||
                normalized.Contains("index") ||
                normalized.Contains("middle") ||
                normalized.Contains("ring") ||
                normalized.Contains("little") ||
                normalized.Contains("hand"))
            {
                return false;
            }

            return normalized.Contains("shoulder") ||
                   normalized.Contains("arm") ||
                   normalized.Contains("forearm");
        }

        private static bool ShouldUseEditorHumanoidMuscleReference(int muscleIndex)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(HumanTrait.MuscleName[muscleIndex]);
            if (IsLeftUpperArmTwistMuscle(normalized))
            {
                return false;
            }

            if (normalized.Contains("forearm") && normalized.Contains("stretch"))
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

        private static bool ShouldApplyEditorHumanoidMuscleReferenceValue(int muscleIndex, float referenceValue)
        {
            if (!ShouldUseEditorHumanoidMuscleReference(muscleIndex) || !IsFinite(referenceValue))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(HumanTrait.MuscleName[muscleIndex]);
            if (IsRightUpperArmTwistMuscle(normalized) && Mathf.Abs(referenceValue) > 1f)
            {
                return false;
            }

            return true;
        }

        private static void TransformRetargetPoseInputMuscles(ref HumanPose pose)
        {
            if (pose.muscles == null)
            {
                return;
            }

            for (int i = 0; i < pose.muscles.Length; i++)
            {
                pose.muscles[i] = TransformRetargetPoseInputMuscleValue(i, pose.muscles[i]);
            }
        }

        private static float TransformRetargetPoseInputMuscleValue(int muscleIndex, float value)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return value;
            }

            string normalized = NormalizeEditorMuscleName(HumanTrait.MuscleName[muscleIndex]);
            if (IsLeftUpperArmTwistMuscle(normalized))
            {
                return -value;
            }

            return value;
        }

        private static float AlignRetargetPoseInputWithEditorReference(int muscleIndex, float value, float referenceValue)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return value;
            }

            string normalized = NormalizeEditorMuscleName(HumanTrait.MuscleName[muscleIndex]);
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

        private static int FindHumanMuscleIndex(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return -1;
            }

            string normalizedInput = NormalizeEditorMuscleName(muscleName);
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string humanMuscleName = HumanTrait.MuscleName[i];
                if (string.Equals(humanMuscleName, muscleName, StringComparison.Ordinal) ||
                    string.Equals(NormalizeEditorMuscleName(humanMuscleName), normalizedInput, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string NormalizeEditorMuscleName(string muscleName)
        {
            string normalized = muscleName.Replace(" ", "").Replace(".", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
            normalized = normalized.Replace("lefthandthumb", "leftthumb")
                .Replace("lefthandindex", "leftindex")
                .Replace("lefthandmiddle", "leftmiddle")
                .Replace("lefthandring", "leftring")
                .Replace("lefthandlittle", "leftlittle")
                .Replace("righthandthumb", "rightthumb")
                .Replace("righthandindex", "rightindex")
                .Replace("righthandmiddle", "rightmiddle")
                .Replace("righthandring", "rightring")
                .Replace("righthandlittle", "rightlittle");
            return normalized;
        }
#endif
    }
}
