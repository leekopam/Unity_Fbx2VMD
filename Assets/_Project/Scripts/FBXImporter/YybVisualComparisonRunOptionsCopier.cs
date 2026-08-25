using System;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonRunOptionsCopier
    {
        public static void Copy(
            YybVisualComparisonRunOptions source,
            YybVisualComparisonRunOptions destination)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            VisualComparisonRunOptionsCopier.Copy(source, destination);
            destination.enableYybArmSwingLimitRuntimeOverride = source.enableYybArmSwingLimitRuntimeOverride;
            destination.yybArmSwingLimitWeight = source.yybArmSwingLimitWeight;
            destination.yybArmSwingMaxDownDot = source.yybArmSwingMaxDownDot;
            destination.yybArmSwingMinHandHorizontalRatio = source.yybArmSwingMinHandHorizontalRatio;
            destination.yybArmSwingMaxHandBelowShoulderRatio = source.yybArmSwingMaxHandBelowShoulderRatio;
            destination.yybArmSwingHorizontalReachLimitWeight = source.yybArmSwingHorizontalReachLimitWeight;
            destination.yybArmSwingMaxHandHorizontalReachRatio = source.yybArmSwingMaxHandHorizontalReachRatio;
            destination.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio = source.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
            destination.yybArmSwingHorizontalReachMinElbowAngleAfterApply = source.yybArmSwingHorizontalReachMinElbowAngleAfterApply;
            destination.yybArmSwingRaisedPoseHorizontalReachLimitWeight = source.yybArmSwingRaisedPoseHorizontalReachLimitWeight;
            destination.yybArmSwingRaisedPoseMinUpperArmDownDot = source.yybArmSwingRaisedPoseMinUpperArmDownDot;
            destination.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio = source.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
            destination.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio = source.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
            destination.enableYybArmDirectionRetargetRuntimeOverride = source.enableYybArmDirectionRetargetRuntimeOverride;
            destination.yybArmDirectionUpperArmWeight = source.yybArmDirectionUpperArmWeight;
            destination.yybArmDirectionForearmWeight = source.yybArmDirectionForearmWeight;
            destination.yybArmDirectionUpperArmMaxDegrees = source.yybArmDirectionUpperArmMaxDegrees;
            destination.yybArmDirectionForearmMaxDegrees = source.yybArmDirectionForearmMaxDegrees;
            destination.yybArmDirectionLeftSideWeightScale = source.yybArmDirectionLeftSideWeightScale;
            destination.yybArmDirectionRightSideWeightScale = source.yybArmDirectionRightSideWeightScale;
            destination.overrideYybArmSleeveAnchorRuntimeSettings = source.overrideYybArmSleeveAnchorRuntimeSettings;
            destination.enableYybArmSleeveAnchorRuntimeOverride = source.enableYybArmSleeveAnchorRuntimeOverride;
            destination.yybArmSleeveAnchorInfluence = source.yybArmSleeveAnchorInfluence;
            destination.yybArmShoulderCapAnchorInfluence = source.yybArmShoulderCapAnchorInfluence;
            destination.yybArmSleeveAnchorMaxDegrees = source.yybArmSleeveAnchorMaxDegrees;
            destination.overrideYybArmVisualTwistRuntimeSettings = source.overrideYybArmVisualTwistRuntimeSettings;
            destination.enableYybArmVisualTwistRuntimeOverride = source.enableYybArmVisualTwistRuntimeOverride;
            destination.yybArmVisualUpperArmInfluence = source.yybArmVisualUpperArmInfluence;
            destination.yybArmVisualForearmInfluence = source.yybArmVisualForearmInfluence;
            destination.yybArmVisualUpperArmMaxDegrees = source.yybArmVisualUpperArmMaxDegrees;
            destination.yybArmVisualForearmMaxDegrees = source.yybArmVisualForearmMaxDegrees;
            destination.enableYybRightSleeveSilhouetteOffsetRuntimeOverride = source.enableYybRightSleeveSilhouetteOffsetRuntimeOverride;
            destination.yybRightSleeveSilhouetteLocalOffsetX = source.yybRightSleeveSilhouetteLocalOffsetX;
            destination.yybRightSleeveSilhouetteLocalOffsetFrameGateStart = source.yybRightSleeveSilhouetteLocalOffsetFrameGateStart;
            destination.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd = source.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd;
        }
    }
}
