using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybArmRuntimeOverrideApplier
    {
        internal static bool ApplySwingLimit(
            FBXVmdPipeline pipeline,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio,
            float horizontalReachLimitWeight,
            float maxHandHorizontalReachRatio,
            float horizontalReachMaxHandBelowShoulderRatio,
            float horizontalReachMinElbowAngleAfterApply,
            float raisedPoseHorizontalReachLimitWeight,
            float raisedPoseMinUpperArmDownDot,
            float raisedPoseMaxHandBelowShoulderRatio,
            float raisedPoseMaxHandHorizontalReachRatio)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.enableYybArmSwingLimitCorrection = enabled;
            pipeline.YybArmSwingLimitWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            pipeline.YybArmSwingMaxDownDot = Mathf.Clamp01(maxDownDot);
            pipeline.YybArmSwingMinHandHorizontalRatio = Mathf.Clamp(minHandHorizontalRatio, 0f, 1.5f);
            pipeline.YybArmSwingMaxHandBelowShoulderRatio = Mathf.Clamp(maxHandBelowShoulderRatio, 0f, 1.5f);
            pipeline.YybArmSwingHorizontalReachLimitWeight = enabled
                ? Mathf.Clamp01(horizontalReachLimitWeight)
                : 0f;
            pipeline.YybArmSwingMaxHandHorizontalReachRatio = enabled
                ? Mathf.Clamp(maxHandHorizontalReachRatio, 0f, 1.5f)
                : 0f;
            pipeline.YybArmSwingHorizontalReachMaxHandBelowShoulderRatio = enabled
                ? Mathf.Clamp(horizontalReachMaxHandBelowShoulderRatio, 0f, 1.5f)
                : 0f;
            pipeline.YybArmSwingHorizontalReachMinElbowAngleAfterApply = enabled
                ? Mathf.Clamp(horizontalReachMinElbowAngleAfterApply, 0f, 180f)
                : 0f;
            pipeline.YybArmSwingRaisedPoseHorizontalReachLimitWeight = enabled
                ? Mathf.Clamp01(raisedPoseHorizontalReachLimitWeight)
                : 0f;
            pipeline.YybArmSwingRaisedPoseMinUpperArmDownDot = Mathf.Clamp01(raisedPoseMinUpperArmDownDot);
            pipeline.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio = Mathf.Clamp(
                raisedPoseMaxHandBelowShoulderRatio,
                0f,
                1.5f);
            pipeline.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio = enabled
                ? Mathf.Clamp(raisedPoseMaxHandHorizontalReachRatio, 0f, 1.5f)
                : 0f;
            return true;
        }

        internal static bool ApplyDirection(
            FBXVmdPipeline pipeline,
            bool enabled,
            float upperArmWeight,
            float forearmWeight,
            float upperArmMaxDegrees,
            float forearmMaxDegrees,
            float leftSideWeightScale,
            float rightSideWeightScale)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.enableYybArmDirectionRetargetCorrection = enabled;
            pipeline.YybArmDirectionUpperArmWeight = enabled ? Mathf.Clamp01(upperArmWeight) : 0f;
            pipeline.YybArmDirectionForearmWeight = enabled ? Mathf.Clamp01(forearmWeight) : 0f;
            pipeline.YybArmDirectionUpperArmMaxDegrees = Mathf.Clamp(upperArmMaxDegrees, 0f, 120f);
            pipeline.YybArmDirectionForearmMaxDegrees = Mathf.Clamp(forearmMaxDegrees, 0f, 120f);
            pipeline.YybArmDirectionLeftSideWeightScale = enabled ? Mathf.Clamp01(leftSideWeightScale) : 0f;
            pipeline.YybArmDirectionRightSideWeightScale = enabled ? Mathf.Clamp01(rightSideWeightScale) : 0f;
            return true;
        }

        internal static bool ApplySleeveAnchor(
            FBXVmdPipeline pipeline,
            bool enabled,
            float sleeveInfluence,
            float shoulderCapInfluence,
            float maxDegrees)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.enableYybArmSleeveAnchorCorrection = enabled;
            pipeline.YybArmSleeveAnchorInfluence = enabled ? Mathf.Clamp01(sleeveInfluence) : 0f;
            pipeline.YybArmShoulderCapAnchorInfluence = enabled ? Mathf.Clamp01(shoulderCapInfluence) : 0f;
            pipeline.YybArmSleeveAnchorMaxDegrees = Mathf.Clamp(maxDegrees, 0f, 120f);
            return true;
        }

        internal static bool ApplyVisualTwist(
            FBXVmdPipeline pipeline,
            bool enabled,
            float upperArmInfluence,
            float forearmInfluence,
            float upperArmMaxDegrees,
            float forearmMaxDegrees)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.enableYybArmVisualTwistCorrection = enabled;
            pipeline.YybArmVisualUpperArmInfluence = enabled ? Mathf.Clamp01(upperArmInfluence) : 0f;
            pipeline.YybArmVisualForearmInfluence = enabled ? Mathf.Clamp01(forearmInfluence) : 0f;
            pipeline.YybArmVisualUpperArmMaxDegrees = Mathf.Clamp(upperArmMaxDegrees, 0f, 120f);
            pipeline.YybArmVisualForearmMaxDegrees = Mathf.Clamp(forearmMaxDegrees, 0f, 120f);
            return true;
        }

        internal static bool ApplyRightSleeveSilhouetteOffset(
            FBXVmdPipeline pipeline,
            bool enabled,
            float localOffsetX,
            float frameGateStart,
            float frameGateEnd)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.useYybRightSleeveSilhouetteLocalOffsetReference = enabled;
            pipeline.yybRightSleeveSilhouetteLocalOffsetX = Mathf.Clamp(localOffsetX, -0.2f, 0.2f);
            pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateStart = Mathf.Clamp(frameGateStart, 0f, 6000f);
            pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd = Mathf.Clamp(frameGateEnd, 0f, 6000f);
            return true;
        }
    }
}
