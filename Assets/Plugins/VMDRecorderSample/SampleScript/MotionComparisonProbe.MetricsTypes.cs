using System;

public partial class MotionComparisonProbe
{
    private struct ArmMuscleMetrics
    {
        public float LeftShoulderDownUp;
        public float LeftShoulderFrontBack;
        public float LeftArmDownUp;
        public float LeftArmFrontBack;
        public float LeftArmTwist;
        public float LeftForearmStretch;
        public float LeftForearmTwist;
        public float RightShoulderDownUp;
        public float RightShoulderFrontBack;
        public float RightArmDownUp;
        public float RightArmFrontBack;
        public float RightArmTwist;
        public float RightForearmStretch;
        public float RightForearmTwist;

        public static ArmMuscleMetrics Empty => new ArmMuscleMetrics
        {
            LeftShoulderDownUp = float.NaN,
            LeftShoulderFrontBack = float.NaN,
            LeftArmDownUp = float.NaN,
            LeftArmFrontBack = float.NaN,
            LeftArmTwist = float.NaN,
            LeftForearmStretch = float.NaN,
            LeftForearmTwist = float.NaN,
            RightShoulderDownUp = float.NaN,
            RightShoulderFrontBack = float.NaN,
            RightArmDownUp = float.NaN,
            RightArmFrontBack = float.NaN,
            RightArmTwist = float.NaN,
            RightForearmStretch = float.NaN,
            RightForearmTwist = float.NaN
        };
    }

    private struct AnimationTimeMetrics
    {
        public string Source;
        public string ClipName;
        public float ClipTime;
        public float ClipLength;
        public float NormalizedTime;

        public static AnimationTimeMetrics Empty => new AnimationTimeMetrics
        {
            Source = MotionComparisonProbeReportWriter.BuildUnknownAnimationTimeSourceLabel(),
            ClipName = "",
            ClipTime = float.NaN,
            ClipLength = float.NaN,
            NormalizedTime = float.NaN
        };
    }

    private struct FingerMetrics
    {
        public float LeftThumb1Stretch;
        public float LeftThumbSpread;
        public float LeftIndex1Stretch;
        public float LeftIndexSpread;
        public float LeftMiddle1Stretch;
        public float LeftMiddleSpread;
        public float LeftRing1Stretch;
        public float LeftRingSpread;
        public float LeftLittle1Stretch;
        public float LeftLittleSpread;
        public float RightThumb1Stretch;
        public float RightThumbSpread;
        public float RightIndex1Stretch;
        public float RightIndexSpread;
        public float RightMiddle1Stretch;
        public float RightMiddleSpread;
        public float RightRing1Stretch;
        public float RightRingSpread;
        public float RightLittle1Stretch;
        public float RightLittleSpread;

        public static FingerMetrics Empty => new FingerMetrics
        {
            LeftThumb1Stretch = float.NaN,
            LeftThumbSpread = float.NaN,
            LeftIndex1Stretch = float.NaN,
            LeftIndexSpread = float.NaN,
            LeftMiddle1Stretch = float.NaN,
            LeftMiddleSpread = float.NaN,
            LeftRing1Stretch = float.NaN,
            LeftRingSpread = float.NaN,
            LeftLittle1Stretch = float.NaN,
            LeftLittleSpread = float.NaN,
            RightThumb1Stretch = float.NaN,
            RightThumbSpread = float.NaN,
            RightIndex1Stretch = float.NaN,
            RightIndexSpread = float.NaN,
            RightMiddle1Stretch = float.NaN,
            RightMiddleSpread = float.NaN,
            RightRing1Stretch = float.NaN,
            RightRingSpread = float.NaN,
            RightLittle1Stretch = float.NaN,
            RightLittleSpread = float.NaN
        };
    }

    private struct RetargetEndpointStageMetrics
    {
        public float LeftFootWorldX;
        public float LeftFootWorldZ;
        public float LeftToesWorldX;
        public float LeftToesWorldZ;
        public float RightFootWorldX;
        public float RightFootWorldZ;
        public float RightToesWorldX;
        public float RightToesWorldZ;

        public static RetargetEndpointStageMetrics Empty => new RetargetEndpointStageMetrics
        {
            LeftFootWorldX = float.NaN,
            LeftFootWorldZ = float.NaN,
            LeftToesWorldX = float.NaN,
            LeftToesWorldZ = float.NaN,
            RightFootWorldX = float.NaN,
            RightFootWorldZ = float.NaN,
            RightToesWorldX = float.NaN,
            RightToesWorldZ = float.NaN
        };
    }

    private struct HandTorsoClearanceMetrics
    {
        public float LeftSignedClearance;
        public float RightSignedClearance;
        public float MinSignedClearance;
        public float PenetrationRisk;

        public static HandTorsoClearanceMetrics Empty => new HandTorsoClearanceMetrics
        {
            LeftSignedClearance = float.NaN,
            RightSignedClearance = float.NaN,
            MinSignedClearance = float.NaN,
            PenetrationRisk = float.NaN
        };
    }
}
