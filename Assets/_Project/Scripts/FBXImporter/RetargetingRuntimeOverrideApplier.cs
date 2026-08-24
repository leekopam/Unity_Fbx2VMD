#if UNITY_EDITOR
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class RetargetingRuntimeOverrideApplier
    {
        internal static bool ApplyPoseVisualSpikeSmoothing(
            FBXVmdPipeline pipeline,
            bool enabled,
            float currentWeight,
            float forearmStretchClampMaxOffset)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.smoothRetargetPoseOnVisualStepSpike = enabled;
            pipeline.RetargetPoseVisualSpikeCurrentWeight = Mathf.Clamp(currentWeight, 0.1f, 1f);
            pipeline.RetargetPoseVisualSpikeForearmStretchClampMaxOffset =
                Mathf.Clamp01(forearmStretchClampMaxOffset);
            return true;
        }

        internal static bool ApplyArmStretchClamp(
            FBXVmdPipeline pipeline,
            bool enabled,
            float stretchLimit,
            float maximumStretchLimit)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.enableAnatomicalArmGuard = true;
            pipeline.clampRetargetArmStretchMuscles = enabled;
            pipeline.targetGuardClampAnatomicalArmMuscles = enabled;
            pipeline.targetGuardClampArmStretchMuscles = enabled;
            pipeline.ArmStretchMuscleLimit = enabled
                ? Mathf.Clamp(stretchLimit, 0f, maximumStretchLimit)
                : 0f;
            return true;
        }

        internal static bool ApplyTargetHumanoidBonePositionLock(FBXVmdPipeline pipeline, bool enabled)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.ShouldLockTargetHumanoidBonePositions = enabled;
            return true;
        }

        internal static bool ApplyBodyPositionXzRootMotion(FBXVmdPipeline pipeline, bool enabled)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.ShouldUseRetargetBodyPositionXZRootMotion = enabled;
            return true;
        }

    }
}
#endif
