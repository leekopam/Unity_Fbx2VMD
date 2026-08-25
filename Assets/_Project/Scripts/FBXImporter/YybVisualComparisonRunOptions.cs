using System;

namespace Fbx2Vmd.FBXImporter
{
    [Serializable]
    internal class YybVisualComparisonRunOptions : VisualComparisonRunOptions
    {
        public bool enableYybArmSwingLimitRuntimeOverride;
        public float yybArmSwingLimitWeight;
        public float yybArmSwingMaxDownDot;
        public float yybArmSwingMinHandHorizontalRatio;
        public float yybArmSwingMaxHandBelowShoulderRatio;
        public float yybArmSwingHorizontalReachLimitWeight;
        public float yybArmSwingMaxHandHorizontalReachRatio;
        public float yybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
        public float yybArmSwingHorizontalReachMinElbowAngleAfterApply;
        public float yybArmSwingRaisedPoseHorizontalReachLimitWeight;
        public float yybArmSwingRaisedPoseMinUpperArmDownDot;
        public float yybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
        public float yybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
        public bool enableYybArmDirectionRetargetRuntimeOverride;
        public float yybArmDirectionUpperArmWeight;
        public float yybArmDirectionForearmWeight;
        public float yybArmDirectionUpperArmMaxDegrees;
        public float yybArmDirectionForearmMaxDegrees;
        public float yybArmDirectionLeftSideWeightScale;
        public float yybArmDirectionRightSideWeightScale;
        public bool overrideYybArmSleeveAnchorRuntimeSettings;
        public bool enableYybArmSleeveAnchorRuntimeOverride;
        public float yybArmSleeveAnchorInfluence;
        public float yybArmShoulderCapAnchorInfluence;
        public float yybArmSleeveAnchorMaxDegrees;
        public bool overrideYybArmVisualTwistRuntimeSettings;
        public bool enableYybArmVisualTwistRuntimeOverride;
        public float yybArmVisualUpperArmInfluence;
        public float yybArmVisualForearmInfluence;
        public float yybArmVisualUpperArmMaxDegrees;
        public float yybArmVisualForearmMaxDegrees;
        public bool enableYybRightSleeveSilhouetteOffsetRuntimeOverride;
        public float yybRightSleeveSilhouetteLocalOffsetX;
        public float yybRightSleeveSilhouetteLocalOffsetFrameGateStart;
        public float yybRightSleeveSilhouetteLocalOffsetFrameGateEnd;
    }
}
