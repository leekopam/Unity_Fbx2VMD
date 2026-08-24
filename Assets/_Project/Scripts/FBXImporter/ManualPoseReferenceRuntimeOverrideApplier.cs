using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class ManualPoseReferenceRuntimeOverrideApplier
    {
        internal static bool ApplyFootLocalRotation(FBXVmdPipeline pipeline, bool enabled)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.ShouldUseManualAnimatorFootLocalRotationReference = enabled;
            pipeline.manualAnimatorFootLocalRotationReferenceWeight = enabled ? 1f : 0f;
            return true;
        }

        internal static bool ApplyFullBodyPose(
            FBXVmdPipeline pipeline,
            bool enabled,
            float weight,
            bool excludeLowerBodyMuscles,
            bool lowerBodyMusclesOnly,
            bool legTwistMusclesOnly,
            bool rightArmMusclesOnly,
            bool leftArmMusclesOnly,
            bool rightSleeveChainMusclesOnly,
            float frameGateStart,
            float frameGateEnd)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.ShouldUseManualAnimatorFullBodyPoseReference = enabled;
            pipeline.manualAnimatorFullBodyPoseReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            pipeline.ShouldExcludeManualAnimatorFullBodyLowerMuscles = enabled && excludeLowerBodyMuscles;
            pipeline.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly = enabled && lowerBodyMusclesOnly;
            pipeline.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly = enabled && legTwistMusclesOnly;
            pipeline.manualAnimatorFullBodyPoseRightArmMusclesOnly = enabled && rightArmMusclesOnly;
            pipeline.manualAnimatorFullBodyPoseLeftArmMusclesOnly = enabled && leftArmMusclesOnly;
            pipeline.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly =
                enabled && rightSleeveChainMusclesOnly;
            pipeline.manualAnimatorFullBodyPoseFrameGateStart = enabled ? Mathf.Max(0f, frameGateStart) : 0f;
            pipeline.manualAnimatorFullBodyPoseFrameGateEnd = enabled ? Mathf.Max(0f, frameGateEnd) : 0f;
            return true;
        }

        internal static bool ApplyRightLegTwistOutput(
            FBXVmdPipeline pipeline,
            bool enabled,
            float weight,
            float maxDelta)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.ShouldUseSetHumanPoseRightLegTwistOutputReference = enabled;
            pipeline.setHumanPoseRightLegTwistOutputReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            pipeline.setHumanPoseRightLegTwistOutputReferenceMaxDelta = Mathf.Max(0f, maxDelta);
            return true;
        }

        internal static bool ApplyBodyRotation(FBXVmdPipeline pipeline, bool enabled, float weight)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.ShouldUseManualAnimatorBodyRotationReference = enabled;
            pipeline.manualAnimatorBodyRotationReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            return true;
        }

        internal static bool ApplyHandLocalRotation(FBXVmdPipeline pipeline, bool enabled)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.useManualAnimatorHandLocalRotationReference = enabled;
            return true;
        }

        internal static bool ApplyThumbLocalRotation(FBXVmdPipeline pipeline, bool enabled)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.useManualAnimatorThumbLocalRotationReference = enabled;
            return true;
        }

        internal static bool ApplyHandPalmFrame(FBXVmdPipeline pipeline, bool enabled, float weight)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.useManualAnimatorHandPalmFrameReference = enabled;
            pipeline.manualAnimatorHandPalmFrameWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            return true;
        }
    }
}
