using System;
using UnityEngine;

public partial class MotionComparisonProbe
{
    private struct ArmSwingGuardDiagnostics
    {
        public float LeftApplied;
        public float LeftHorizontalReachApplied;
        public float LeftRaisedReachApplied;
        public float LeftForearmStretchBefore;
        public float LeftForearmStretchAfter;
        public float LeftForearmStretchDelta;
        public float RightApplied;
        public float RightHorizontalReachApplied;
        public float RightRaisedReachApplied;
        public float RightForearmStretchBefore;
        public float RightForearmStretchAfter;
        public float RightForearmStretchDelta;

        public static ArmSwingGuardDiagnostics Empty => new ArmSwingGuardDiagnostics
        {
            LeftApplied = float.NaN,
            LeftHorizontalReachApplied = float.NaN,
            LeftRaisedReachApplied = float.NaN,
            LeftForearmStretchBefore = float.NaN,
            LeftForearmStretchAfter = float.NaN,
            LeftForearmStretchDelta = float.NaN,
            RightApplied = float.NaN,
            RightHorizontalReachApplied = float.NaN,
            RightRaisedReachApplied = float.NaN,
            RightForearmStretchBefore = float.NaN,
            RightForearmStretchAfter = float.NaN,
            RightForearmStretchDelta = float.NaN
        };
    }
    private struct YybDiagnosticMetrics
    {
        public YybSideDiagnosticMetrics Left;
        public YybSideDiagnosticMetrics Right;
        public float MaxDeformationRisk;

        public static YybDiagnosticMetrics Empty => new YybDiagnosticMetrics
        {
            Left = YybSideDiagnosticMetrics.Empty,
            Right = YybSideDiagnosticMetrics.Empty,
            MaxDeformationRisk = float.NaN
        };
    }
    private struct ThumbGuardDiagnostics
    {
        public float ManualThumbReferenceConfigured;
        public float ManualThumbReferenceActive;
        public float PoseShapingSuppressed;
        public float LeftPoseShapingSuppressed;
        public float RightPoseShapingSuppressed;
        public float ProjectionGuardWeight;
        public float LeftProjectionGuardWeight;
        public float RightProjectionGuardWeight;
        public float IndexSpreadGuardWeight;
        public float LeftIndexSpreadGuardWeight;
        public float RightIndexSpreadGuardWeight;
        public float SegmentStraightenWeight;
        public float LeftSegmentStraightenWeight;
        public float RightSegmentStraightenWeight;
        public float LeftProjectionCorrectionApplyCount;
        public float RightProjectionCorrectionApplyCount;
        public float LeftProjectionCorrectionPreserveCount;
        public float RightProjectionCorrectionPreserveCount;
        public float LeftSegmentStraightenApplyCount;
        public float RightSegmentStraightenApplyCount;
        public float LeftSegmentStraightenPreserveCount;
        public float RightSegmentStraightenPreserveCount;
        public float LeftLocalRotationGuardClampCount;
        public float RightLocalRotationGuardClampCount;
        public float LeftLocalRotationGuardPreserveCount;
        public float RightLocalRotationGuardPreserveCount;
        public float LeftLocalRotationGuardCurrentRisk;
        public float RightLocalRotationGuardCurrentRisk;
        public float LeftLocalRotationGuardLimitedRisk;
        public float RightLocalRotationGuardLimitedRisk;
        public float LeftWorldRotationSuppressCompetingOverride;
        public float RightWorldRotationSuppressCompetingOverride;
        public float LeftWorldRotationKeepDetachedHelperOverride;
        public float RightWorldRotationKeepDetachedHelperOverride;
        public float LeftWorldRotationCurrentReferenceFrameDeviation;
        public float RightWorldRotationCurrentReferenceFrameDeviation;
        public float LeftWorldRotationCandidateReferenceFrameDeviation;
        public float RightWorldRotationCandidateReferenceFrameDeviation;
        public float LeftProximalWorldRotationPreserveReason;
        public float RightProximalWorldRotationPreserveReason;
        public float LeftIntermediateWorldRotationPreserveReason;
        public float RightIntermediateWorldRotationPreserveReason;
        public float LeftProximalWorldRotationCurrentReferenceAngle;
        public float RightProximalWorldRotationCurrentReferenceAngle;
        public float LeftIntermediateWorldRotationCurrentReferenceAngle;
        public float RightIntermediateWorldRotationCurrentReferenceAngle;
        public float LeftProximalWorldRotationCandidateReferenceAngle;
        public float RightProximalWorldRotationCandidateReferenceAngle;
        public float LeftIntermediateWorldRotationCandidateReferenceAngle;
        public float RightIntermediateWorldRotationCandidateReferenceAngle;
        public float LeftProximalWorldRotationPreserveCurrentRisk;
        public float RightProximalWorldRotationPreserveCurrentRisk;
        public float LeftIntermediateWorldRotationPreserveCurrentRisk;
        public float RightIntermediateWorldRotationPreserveCurrentRisk;
        public float LeftProximalWorldRotationPreserveLimitedRisk;
        public float RightProximalWorldRotationPreserveLimitedRisk;
        public float LeftIntermediateWorldRotationPreserveLimitedRisk;
        public float RightIntermediateWorldRotationPreserveLimitedRisk;
        public float HelperSyncEnabled;
        public float HelperPositionSyncEnabled;
        public float HelperSyncWeight;
        public float HelperMaxLocalAngle;
        public float PalmStabilizeEnabled;
        public float PalmStabilizeWeight;
        public float PalmStabilizeMaxLocalAngle;
        public float WebbingStabilizeEnabled;
        public float WebbingStabilizeWeight;
        public float WebbingMaxLocalAngle;
        public float WebbingMaxPositionOffset;

        public static ThumbGuardDiagnostics Empty => new ThumbGuardDiagnostics
        {
            ManualThumbReferenceConfigured = float.NaN,
            ManualThumbReferenceActive = float.NaN,
            PoseShapingSuppressed = float.NaN,
            LeftPoseShapingSuppressed = float.NaN,
            RightPoseShapingSuppressed = float.NaN,
            ProjectionGuardWeight = float.NaN,
            LeftProjectionGuardWeight = float.NaN,
            RightProjectionGuardWeight = float.NaN,
            IndexSpreadGuardWeight = float.NaN,
            LeftIndexSpreadGuardWeight = float.NaN,
            RightIndexSpreadGuardWeight = float.NaN,
            SegmentStraightenWeight = float.NaN,
            LeftSegmentStraightenWeight = float.NaN,
            RightSegmentStraightenWeight = float.NaN,
            LeftProjectionCorrectionApplyCount = float.NaN,
            RightProjectionCorrectionApplyCount = float.NaN,
            LeftProjectionCorrectionPreserveCount = float.NaN,
            RightProjectionCorrectionPreserveCount = float.NaN,
            LeftSegmentStraightenApplyCount = float.NaN,
            RightSegmentStraightenApplyCount = float.NaN,
            LeftSegmentStraightenPreserveCount = float.NaN,
            RightSegmentStraightenPreserveCount = float.NaN,
            LeftLocalRotationGuardClampCount = float.NaN,
            RightLocalRotationGuardClampCount = float.NaN,
            LeftLocalRotationGuardPreserveCount = float.NaN,
            RightLocalRotationGuardPreserveCount = float.NaN,
            LeftLocalRotationGuardCurrentRisk = float.NaN,
            RightLocalRotationGuardCurrentRisk = float.NaN,
            LeftLocalRotationGuardLimitedRisk = float.NaN,
            RightLocalRotationGuardLimitedRisk = float.NaN,
            LeftWorldRotationSuppressCompetingOverride = float.NaN,
            RightWorldRotationSuppressCompetingOverride = float.NaN,
            LeftWorldRotationKeepDetachedHelperOverride = float.NaN,
            RightWorldRotationKeepDetachedHelperOverride = float.NaN,
            LeftWorldRotationCurrentReferenceFrameDeviation = float.NaN,
            RightWorldRotationCurrentReferenceFrameDeviation = float.NaN,
            LeftWorldRotationCandidateReferenceFrameDeviation = float.NaN,
            RightWorldRotationCandidateReferenceFrameDeviation = float.NaN,
            LeftProximalWorldRotationPreserveReason = float.NaN,
            RightProximalWorldRotationPreserveReason = float.NaN,
            LeftIntermediateWorldRotationPreserveReason = float.NaN,
            RightIntermediateWorldRotationPreserveReason = float.NaN,
            LeftProximalWorldRotationCurrentReferenceAngle = float.NaN,
            RightProximalWorldRotationCurrentReferenceAngle = float.NaN,
            LeftIntermediateWorldRotationCurrentReferenceAngle = float.NaN,
            RightIntermediateWorldRotationCurrentReferenceAngle = float.NaN,
            LeftProximalWorldRotationCandidateReferenceAngle = float.NaN,
            RightProximalWorldRotationCandidateReferenceAngle = float.NaN,
            LeftIntermediateWorldRotationCandidateReferenceAngle = float.NaN,
            RightIntermediateWorldRotationCandidateReferenceAngle = float.NaN,
            LeftProximalWorldRotationPreserveCurrentRisk = float.NaN,
            RightProximalWorldRotationPreserveCurrentRisk = float.NaN,
            LeftIntermediateWorldRotationPreserveCurrentRisk = float.NaN,
            RightIntermediateWorldRotationPreserveCurrentRisk = float.NaN,
            LeftProximalWorldRotationPreserveLimitedRisk = float.NaN,
            RightProximalWorldRotationPreserveLimitedRisk = float.NaN,
            LeftIntermediateWorldRotationPreserveLimitedRisk = float.NaN,
            RightIntermediateWorldRotationPreserveLimitedRisk = float.NaN,
            HelperSyncEnabled = float.NaN,
            HelperPositionSyncEnabled = float.NaN,
            HelperSyncWeight = float.NaN,
            HelperMaxLocalAngle = float.NaN,
            PalmStabilizeEnabled = float.NaN,
            PalmStabilizeWeight = float.NaN,
            PalmStabilizeMaxLocalAngle = float.NaN,
            WebbingStabilizeEnabled = float.NaN,
            WebbingStabilizeWeight = float.NaN,
            WebbingMaxLocalAngle = float.NaN,
            WebbingMaxPositionOffset = float.NaN
        };
    }
    private struct RootSpikeMetrics
    {
        public float LastRootDeltaMagnitude;
        public float MaxRootDeltaMagnitude;
        public int RootDeltaSpikeSkippedCount;
        public float LastRootPositionPoseDeltaMagnitude;
        public float MaxRootPositionPoseDeltaMagnitude;
        public int RootPositionSpikeClampedCount;
        public float LastGroundingAdjustment;
        public float MaxGroundingAdjustment;
        public int GroundingStepClampedCount;
        public int GroundingSmoothedCount;
        public float LastGroundingVerticalStep;
        public float MaxGroundingVerticalStep;
        public float InitialGroundingVerticalStep;
        public float MaxGroundingVerticalStepAfterInitial;
        public float LastGroundingTargetY;
        public float LastGroundingLowestFootBottomY;
        public float FootHeightReferenceLift;
        public float RecordingStartRootY;
        public float RecordingStartBodyPositionY;
        public float RecordingStartHipsLocalY;
        public float RecordingStartHipsY;
        public float RecordingStartHipsReferenceBeforeLocalY;
        public float RecordingStartHipsReferenceAfterLocalY;
        public float RecordingStartHipsReferenceDeltaY;
        public int RecordingStartHipsReferenceFlipDetected;
        public string RecordingStartHipsReferenceStage;
        public float PoseInputLeftShoulderFrontBackMuscle;
        public float AfterEditorMuscleReferenceLeftShoulderFrontBackMuscle;
        public float AfterClampPoseMusclesLeftShoulderFrontBackMuscle;
        public float AfterAnatomicalArmGuardLeftShoulderFrontBackMuscle;
        public float AfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle;
        public float SetHumanPoseInputLeftShoulderFrontBackMuscle;
        public float SetHumanPoseOutputLeftShoulderFrontBackMuscle;
        public float SetHumanPoseLeftShoulderFrontBackDelta;
        public float PoseInputLeftArmTwistMuscle;
        public float AfterEditorMuscleReferenceLeftArmTwistMuscle;
        public float AfterClampPoseMusclesLeftArmTwistMuscle;
        public float AfterAnatomicalArmGuardLeftArmTwistMuscle;
        public float AfterVisualSpikeSmoothingLeftArmTwistMuscle;
        public float SetHumanPoseInputLeftArmTwistMuscle;
        public float SetHumanPoseOutputLeftArmTwistMuscle;
        public float SetHumanPoseLeftArmTwistDelta;
        public float PoseInputLeftForearmStretchMuscle;
        public float AfterEditorMuscleReferenceLeftForearmStretchMuscle;
        public float AfterClampPoseMusclesLeftForearmStretchMuscle;
        public float AfterAnatomicalArmGuardLeftForearmStretchMuscle;
        public float AfterVisualSpikeSmoothingLeftForearmStretchMuscle;
        public float SetHumanPoseInputLeftForearmStretchMuscle;
        public float SetHumanPoseOutputLeftForearmStretchMuscle;
        public float SetHumanPoseLeftForearmStretchDelta;
        public float PoseInputRightForearmStretchMuscle;
        public float AfterEditorMuscleReferenceRightForearmStretchMuscle;
        public float AfterClampPoseMusclesRightForearmStretchMuscle;
        public float AfterAnatomicalArmGuardRightForearmStretchMuscle;
        public float AfterVisualSpikeSmoothingRightForearmStretchMuscle;
        public float SetHumanPoseInputRightForearmStretchMuscle;
        public float SetHumanPoseOutputRightForearmStretchMuscle;
        public float SetHumanPoseRightForearmStretchDelta;
        public float PoseInputRightArmTwistMuscle;
        public float AfterEditorMuscleReferenceRightArmTwistMuscle;
        public float AfterClampPoseMusclesRightArmTwistMuscle;
        public float AfterAnatomicalArmGuardRightArmTwistMuscle;
        public float AfterVisualSpikeSmoothingRightArmTwistMuscle;
        public float SetHumanPoseInputRightArmTwistMuscle;
        public float SetHumanPoseOutputRightArmTwistMuscle;
        public float SetHumanPoseRightArmTwistDelta;
        public float SetHumanPoseInputLeftUpperLegFrontBackMuscle;
        public float SetHumanPoseOutputLeftUpperLegFrontBackMuscle;
        public float SetHumanPoseLeftUpperLegFrontBackDelta;
        public float SetHumanPoseInputRightUpperLegFrontBackMuscle;
        public float SetHumanPoseOutputRightUpperLegFrontBackMuscle;
        public float SetHumanPoseRightUpperLegFrontBackDelta;
        public float SetHumanPoseInputLeftLowerLegStretchMuscle;
        public float SetHumanPoseOutputLeftLowerLegStretchMuscle;
        public float SetHumanPoseLeftLowerLegStretchDelta;
        public float SetHumanPoseInputRightLowerLegStretchMuscle;
        public float SetHumanPoseOutputRightLowerLegStretchMuscle;
        public float SetHumanPoseRightLowerLegStretchDelta;
        public float SetHumanPoseInputLeftFootUpDownMuscle;
        public float SetHumanPoseOutputLeftFootUpDownMuscle;
        public float SetHumanPoseLeftFootUpDownDelta;
        public float SetHumanPoseInputRightFootUpDownMuscle;
        public float SetHumanPoseOutputRightFootUpDownMuscle;
        public float SetHumanPoseRightFootUpDownDelta;
        public float SetHumanPoseInputBodyPositionX;
        public float SetHumanPoseInputBodyPositionY;
        public float SetHumanPoseInputBodyPositionZ;
        public float SetHumanPoseOutputBodyPositionX;
        public float SetHumanPoseOutputBodyPositionY;
        public float SetHumanPoseOutputBodyPositionZ;
        public float SetHumanPoseBodyPositionDeltaXZ;
        public float SetHumanPoseInputBodyRotationYaw;
        public float SetHumanPoseOutputBodyRotationYaw;
        public float SetHumanPoseBodyRotationDeltaAngle;
        public float SetHumanPosePreSolveGhostRootWorldX;
        public float SetHumanPosePreSolveGhostRootWorldY;
        public float SetHumanPosePreSolveGhostRootWorldZ;
        public float SetHumanPosePreSolveGhostRootYaw;
        public float SetHumanPosePreSolveTargetRootWorldX;
        public float SetHumanPosePreSolveTargetRootWorldY;
        public float SetHumanPosePreSolveTargetRootWorldZ;
        public float SetHumanPosePreSolveTargetRootYaw;
        public float SetHumanPosePreSolveTargetHipsWorldX;
        public float SetHumanPosePreSolveTargetHipsWorldY;
        public float SetHumanPosePreSolveTargetHipsWorldZ;
        public float SetHumanPosePreSolveTargetHipsLocalX;
        public float SetHumanPosePreSolveTargetHipsLocalY;
        public float SetHumanPosePreSolveTargetHipsLocalZ;
        public float SetHumanPosePreSolveBodyPositionX;
        public float SetHumanPosePreSolveBodyPositionY;
        public float SetHumanPosePreSolveBodyPositionZ;
        public float SetHumanPosePreSolveBodyRotationYaw;
        public float PreSetHumanPoseEndpointBodyPositionBeforeX;
        public float PreSetHumanPoseEndpointBodyPositionBeforeZ;
        public float PreSetHumanPoseEndpointBodyPositionAfterX;
        public float PreSetHumanPoseEndpointBodyPositionAfterZ;
        public float PreSetHumanPoseEndpointBodyPositionDeltaX;
        public float PreSetHumanPoseEndpointBodyPositionDeltaZ;
        public float PreSetHumanPoseEndpointBodyPositionDeltaMagnitudeXZ;
        public float SetHumanPosePreSolveGhostLeftFootWorldX;
        public float SetHumanPosePreSolveGhostLeftFootWorldZ;
        public float SetHumanPosePreSolveGhostLeftToesWorldX;
        public float SetHumanPosePreSolveGhostLeftToesWorldZ;
        public float SetHumanPosePreSolveCurrentLeftFootWorldX;
        public float SetHumanPosePreSolveCurrentLeftFootWorldZ;
        public float SetHumanPosePreSolveCurrentLeftToesWorldX;
        public float SetHumanPosePreSolveCurrentLeftToesWorldZ;
        public float SetHumanPosePreSolveTargetLeftFootWorldX;
        public float SetHumanPosePreSolveTargetLeftFootWorldZ;
        public float SetHumanPosePreSolveTargetLeftToesWorldX;
        public float SetHumanPosePreSolveTargetLeftToesWorldZ;
        public float SetHumanPosePreSolveGhostRightFootWorldX;
        public float SetHumanPosePreSolveGhostRightFootWorldZ;
        public float SetHumanPosePreSolveGhostRightToesWorldX;
        public float SetHumanPosePreSolveGhostRightToesWorldZ;
        public float SetHumanPosePreSolveCurrentRightFootWorldX;
        public float SetHumanPosePreSolveCurrentRightFootWorldZ;
        public float SetHumanPosePreSolveCurrentRightToesWorldX;
        public float SetHumanPosePreSolveCurrentRightToesWorldZ;
        public float SetHumanPosePreSolveTargetRightFootWorldX;
        public float SetHumanPosePreSolveTargetRightFootWorldZ;
        public float SetHumanPosePreSolveTargetRightToesWorldX;
        public float SetHumanPosePreSolveTargetRightToesWorldZ;
        public float SetHumanPoseInputSpineFrontBackMuscle;
        public float SetHumanPoseInputSpineLeftRightMuscle;
        public float SetHumanPoseInputSpineTwistLeftRightMuscle;
        public float SetHumanPoseInputChestFrontBackMuscle;
        public float SetHumanPoseInputChestLeftRightMuscle;
        public float SetHumanPoseInputChestTwistLeftRightMuscle;
        public float SetHumanPoseInputUpperChestFrontBackMuscle;
        public float SetHumanPoseInputUpperChestLeftRightMuscle;
        public float SetHumanPoseInputUpperChestTwistLeftRightMuscle;
        public float SetHumanPoseInputLeftUpperLegInOutMuscle;
        public float SetHumanPoseInputRightUpperLegInOutMuscle;
        public float SetHumanPoseInputLeftUpperLegTwistInOutMuscle;
        public float SetHumanPoseInputRightUpperLegTwistInOutMuscle;
        public float SetHumanPoseInputLeftLowerLegTwistInOutMuscle;
        public float SetHumanPoseInputRightLowerLegTwistInOutMuscle;
        public float SetHumanPoseInputLeftFootTwistInOutMuscle;
        public float SetHumanPoseInputRightFootTwistInOutMuscle;
        public float SetHumanPoseInputLeftToesUpDownMuscle;
        public float SetHumanPoseInputRightToesUpDownMuscle;
        public float SetHumanPoseOutputRightUpperLegInOutMuscle;
        public float SetHumanPoseRightUpperLegInOutDelta;
        public float SetHumanPoseOutputRightUpperLegTwistInOutMuscle;
        public float SetHumanPoseRightUpperLegTwistInOutDelta;
        public float SetHumanPoseOutputRightLowerLegTwistInOutMuscle;
        public float SetHumanPoseRightLowerLegTwistInOutDelta;
        public float SetHumanPoseOutputRightFootTwistInOutMuscle;
        public float SetHumanPoseRightFootTwistInOutDelta;
        public float SetHumanPoseOutputRightToesUpDownMuscle;
        public float SetHumanPoseRightToesUpDownDelta;
        public RetargetEndpointStageMetrics RetargetStageGhost;
        public RetargetEndpointStageMetrics RetargetStageAfterSetHumanPose;
        public RetargetEndpointStageMetrics RetargetStageAfterManualReferences;
        public RetargetEndpointStageMetrics RetargetStageAfterRootRestore;
        public RetargetEndpointStageMetrics RetargetStageAfterRootDelta;
        public RetargetEndpointStageMetrics RetargetStageAfterGrounding;
        public RetargetEndpointStageMetrics RetargetStageAfterBipedIK;
        public RetargetEndpointStageMetrics RetargetStageAfterLateVisualGrounding;
        public float EditorFootLocalRotationLeftFootXzDelta;
        public float EditorFootLocalRotationRightFootXzDelta;
        public float EditorLowerBodySegmentDirectionLeftFootXzDelta;
        public float EditorLowerBodySegmentDirectionRightFootXzDelta;
        public string EditorLowerBodySegmentDirectionMaxCorrectionSegment;
        public float EditorLowerBodySegmentDirectionMaxCorrectionAngle;
        public float EditorLowerBodySegmentDirectionMaxPreAngle;
        public float EditorLowerBodySegmentDirectionMaxPostAngle;
        public float EditorLowerBodySegmentDirectionMaxCorrectionAxisX;
        public float EditorLowerBodySegmentDirectionMaxCorrectionAxisY;
        public float EditorLowerBodySegmentDirectionMaxCorrectionAxisZ;
        public float EditorLowerBodySegmentDirectionMaxReferenceDirectionX;
        public float EditorLowerBodySegmentDirectionMaxReferenceDirectionY;
        public float EditorLowerBodySegmentDirectionMaxReferenceDirectionZ;
        public float EditorLowerBodySegmentDirectionMaxPreDirectionX;
        public float EditorLowerBodySegmentDirectionMaxPreDirectionY;
        public float EditorLowerBodySegmentDirectionMaxPreDirectionZ;
        public float EditorLowerBodySegmentDirectionMaxPostDirectionX;
        public float EditorLowerBodySegmentDirectionMaxPostDirectionY;
        public float EditorLowerBodySegmentDirectionMaxPostDirectionZ;
        public float EditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle;
        public float EditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle;
        public float EditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle;
        public float EditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle;
        public float EditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle;
        public float EditorLowerBodySegmentDirectionRightFootToesCorrectionAngle;
        public float EditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle;
        public float EditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle;
        public float EditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle;
        public float EditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle;
        public float EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX;
        public float EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY;
        public float EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ;
        public float EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX;
        public float EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY;
        public float EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ;
        public float EditorLowerBodySegmentDirectionRightFootToToesPreDirectionX;
        public float EditorLowerBodySegmentDirectionRightFootToToesPreDirectionY;
        public float EditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ;
        public float EditorLowerBodySegmentDirectionRightFootToToesPostDirectionX;
        public float EditorLowerBodySegmentDirectionRightFootToToesPostDirectionY;
        public float EditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ;
        public float EditorLowerBodySegmentDirectionLeftLowerLegWorldX;
        public float EditorLowerBodySegmentDirectionLeftLowerLegWorldY;
        public float EditorLowerBodySegmentDirectionLeftLowerLegWorldZ;
        public float EditorLowerBodySegmentDirectionLeftFootWorldX;
        public float EditorLowerBodySegmentDirectionLeftFootWorldY;
        public float EditorLowerBodySegmentDirectionLeftFootWorldZ;
        public float EditorLowerBodySegmentDirectionLeftToesWorldX;
        public float EditorLowerBodySegmentDirectionLeftToesWorldY;
        public float EditorLowerBodySegmentDirectionLeftToesWorldZ;
        public float EditorLowerBodySegmentDirectionRightLowerLegWorldX;
        public float EditorLowerBodySegmentDirectionRightLowerLegWorldY;
        public float EditorLowerBodySegmentDirectionRightLowerLegWorldZ;
        public float EditorLowerBodySegmentDirectionRightFootWorldX;
        public float EditorLowerBodySegmentDirectionRightFootWorldY;
        public float EditorLowerBodySegmentDirectionRightFootWorldZ;
        public float EditorLowerBodySegmentDirectionRightToesWorldX;
        public float EditorLowerBodySegmentDirectionRightToesWorldY;
        public float EditorLowerBodySegmentDirectionRightToesWorldZ;
        public float EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX;
        public float EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY;
        public float EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ;
        public float EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX;
        public float EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY;
        public float EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ;
        public float EditorLowerBodySegmentDirectionLeftFootForwardX;
        public float EditorLowerBodySegmentDirectionLeftFootForwardY;
        public float EditorLowerBodySegmentDirectionLeftFootForwardZ;
        public float EditorLowerBodySegmentDirectionLeftFootUpX;
        public float EditorLowerBodySegmentDirectionLeftFootUpY;
        public float EditorLowerBodySegmentDirectionLeftFootUpZ;
        public float EditorLowerBodySegmentDirectionRightFootForwardX;
        public float EditorLowerBodySegmentDirectionRightFootForwardY;
        public float EditorLowerBodySegmentDirectionRightFootForwardZ;
        public float EditorLowerBodySegmentDirectionRightFootUpX;
        public float EditorLowerBodySegmentDirectionRightFootUpY;
        public float EditorLowerBodySegmentDirectionRightFootUpZ;
        public float EditorFootHipsAlignedResidualYawLeftFootXzDelta;
        public float EditorFootHipsAlignedResidualYawRightFootXzDelta;
        public float PostSetRightEndpointDesiredFootWorldX;
        public float PostSetRightEndpointDesiredFootWorldZ;
        public float PostSetRightEndpointDesiredToesWorldX;
        public float PostSetRightEndpointDesiredToesWorldZ;
        public float PostSetRightEndpointCurrentFootWorldX;
        public float PostSetRightEndpointCurrentFootWorldZ;
        public float PostSetRightEndpointCurrentToesWorldX;
        public float PostSetRightEndpointCurrentToesWorldZ;
        public float PostSetRightEndpointDeltaBeforeClampX;
        public float PostSetRightEndpointDeltaBeforeClampZ;
        public float PostSetRightEndpointDeltaAfterClampX;
        public float PostSetRightEndpointDeltaAfterClampZ;
        public float PostSetRightEndpointDeltaAfterPositiveZScaleX;
        public float PostSetRightEndpointDeltaAfterPositiveZScaleZ;
        public float PostSetRightEndpointCorrectionX;
        public float PostSetRightEndpointCorrectionZ;
        public float PostSetRightEndpointNextFootWorldX;
        public float PostSetRightEndpointNextFootWorldZ;
        public float PostSetRightEndpointMaxYawAngle;
        public float PostSetRightEndpointYawCorrectionAngle;
        public float PostSetRightEndpointUpperLegRotationDeltaAngle;
        public float PostSetRightEndpointApplied;
        public float PostSetRightEndpointEvaluatorXzReferenceEnabled;
        public float PostSetRightEndpointEvaluatorXzFirstOffsetX;
        public float PostSetRightEndpointEvaluatorXzFirstOffsetZ;
        public float PostSetRightEndpointEvaluatorXzNormalizedDeltaX;
        public float PostSetRightEndpointEvaluatorXzNormalizedDeltaZ;
        public float PostSetRightEndpointEvaluatorXzNormalizedMagnitude;
        public float PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaX;
        public float PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaZ;
        public float PostSetRightEndpointEvaluatorXzTargetMagnitude;
        public float GroundingMaxStepPerFrame;
        public float GroundingLastStepToMaxStepRatio;
        public int GroundingLastStepAtMaxStep;

        public static RootSpikeMetrics Empty => new RootSpikeMetrics
        {
            LastRootDeltaMagnitude = float.NaN,
            MaxRootDeltaMagnitude = float.NaN,
            RootDeltaSpikeSkippedCount = -1,
            LastRootPositionPoseDeltaMagnitude = float.NaN,
            MaxRootPositionPoseDeltaMagnitude = float.NaN,
            RootPositionSpikeClampedCount = -1,
            LastGroundingAdjustment = float.NaN,
            MaxGroundingAdjustment = float.NaN,
            GroundingStepClampedCount = -1,
            GroundingSmoothedCount = -1,
            LastGroundingVerticalStep = float.NaN,
            MaxGroundingVerticalStep = float.NaN,
            InitialGroundingVerticalStep = float.NaN,
            MaxGroundingVerticalStepAfterInitial = float.NaN,
            LastGroundingTargetY = float.NaN,
            LastGroundingLowestFootBottomY = float.NaN,
            FootHeightReferenceLift = float.NaN,
            RecordingStartRootY = float.NaN,
            RecordingStartBodyPositionY = float.NaN,
            RecordingStartHipsLocalY = float.NaN,
            RecordingStartHipsY = float.NaN,
            RecordingStartHipsReferenceBeforeLocalY = float.NaN,
            RecordingStartHipsReferenceAfterLocalY = float.NaN,
            RecordingStartHipsReferenceDeltaY = float.NaN,
            RecordingStartHipsReferenceFlipDetected = -1,
            RecordingStartHipsReferenceStage = "",
            PoseInputLeftShoulderFrontBackMuscle = float.NaN,
            AfterEditorMuscleReferenceLeftShoulderFrontBackMuscle = float.NaN,
            AfterClampPoseMusclesLeftShoulderFrontBackMuscle = float.NaN,
            AfterAnatomicalArmGuardLeftShoulderFrontBackMuscle = float.NaN,
            AfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle = float.NaN,
            SetHumanPoseInputLeftShoulderFrontBackMuscle = float.NaN,
            SetHumanPoseOutputLeftShoulderFrontBackMuscle = float.NaN,
            SetHumanPoseLeftShoulderFrontBackDelta = float.NaN,
            PoseInputLeftArmTwistMuscle = float.NaN,
            AfterEditorMuscleReferenceLeftArmTwistMuscle = float.NaN,
            AfterClampPoseMusclesLeftArmTwistMuscle = float.NaN,
            AfterAnatomicalArmGuardLeftArmTwistMuscle = float.NaN,
            AfterVisualSpikeSmoothingLeftArmTwistMuscle = float.NaN,
            SetHumanPoseInputLeftArmTwistMuscle = float.NaN,
            SetHumanPoseOutputLeftArmTwistMuscle = float.NaN,
            SetHumanPoseLeftArmTwistDelta = float.NaN,
            PoseInputLeftForearmStretchMuscle = float.NaN,
            AfterEditorMuscleReferenceLeftForearmStretchMuscle = float.NaN,
            AfterClampPoseMusclesLeftForearmStretchMuscle = float.NaN,
            AfterAnatomicalArmGuardLeftForearmStretchMuscle = float.NaN,
            AfterVisualSpikeSmoothingLeftForearmStretchMuscle = float.NaN,
            SetHumanPoseInputLeftForearmStretchMuscle = float.NaN,
            SetHumanPoseOutputLeftForearmStretchMuscle = float.NaN,
            SetHumanPoseLeftForearmStretchDelta = float.NaN,
            PoseInputRightForearmStretchMuscle = float.NaN,
            AfterEditorMuscleReferenceRightForearmStretchMuscle = float.NaN,
            AfterClampPoseMusclesRightForearmStretchMuscle = float.NaN,
            AfterAnatomicalArmGuardRightForearmStretchMuscle = float.NaN,
            AfterVisualSpikeSmoothingRightForearmStretchMuscle = float.NaN,
            SetHumanPoseInputRightForearmStretchMuscle = float.NaN,
            SetHumanPoseOutputRightForearmStretchMuscle = float.NaN,
            SetHumanPoseRightForearmStretchDelta = float.NaN,
            PoseInputRightArmTwistMuscle = float.NaN,
            AfterEditorMuscleReferenceRightArmTwistMuscle = float.NaN,
            AfterClampPoseMusclesRightArmTwistMuscle = float.NaN,
            AfterAnatomicalArmGuardRightArmTwistMuscle = float.NaN,
            AfterVisualSpikeSmoothingRightArmTwistMuscle = float.NaN,
            SetHumanPoseInputRightArmTwistMuscle = float.NaN,
            SetHumanPoseOutputRightArmTwistMuscle = float.NaN,
            SetHumanPoseRightArmTwistDelta = float.NaN,
            SetHumanPoseInputLeftUpperLegFrontBackMuscle = float.NaN,
            SetHumanPoseOutputLeftUpperLegFrontBackMuscle = float.NaN,
            SetHumanPoseLeftUpperLegFrontBackDelta = float.NaN,
            SetHumanPoseInputRightUpperLegFrontBackMuscle = float.NaN,
            SetHumanPoseOutputRightUpperLegFrontBackMuscle = float.NaN,
            SetHumanPoseRightUpperLegFrontBackDelta = float.NaN,
            SetHumanPoseInputLeftLowerLegStretchMuscle = float.NaN,
            SetHumanPoseOutputLeftLowerLegStretchMuscle = float.NaN,
            SetHumanPoseLeftLowerLegStretchDelta = float.NaN,
            SetHumanPoseInputRightLowerLegStretchMuscle = float.NaN,
            SetHumanPoseOutputRightLowerLegStretchMuscle = float.NaN,
            SetHumanPoseRightLowerLegStretchDelta = float.NaN,
            SetHumanPoseInputLeftFootUpDownMuscle = float.NaN,
            SetHumanPoseOutputLeftFootUpDownMuscle = float.NaN,
            SetHumanPoseLeftFootUpDownDelta = float.NaN,
            SetHumanPoseInputRightFootUpDownMuscle = float.NaN,
            SetHumanPoseOutputRightFootUpDownMuscle = float.NaN,
            SetHumanPoseRightFootUpDownDelta = float.NaN,
            SetHumanPoseInputBodyPositionX = float.NaN,
            SetHumanPoseInputBodyPositionY = float.NaN,
            SetHumanPoseInputBodyPositionZ = float.NaN,
            SetHumanPoseOutputBodyPositionX = float.NaN,
            SetHumanPoseOutputBodyPositionY = float.NaN,
            SetHumanPoseOutputBodyPositionZ = float.NaN,
            SetHumanPoseBodyPositionDeltaXZ = float.NaN,
            SetHumanPoseInputBodyRotationYaw = float.NaN,
            SetHumanPoseOutputBodyRotationYaw = float.NaN,
            SetHumanPoseBodyRotationDeltaAngle = float.NaN,
            SetHumanPosePreSolveGhostRootWorldX = float.NaN,
            SetHumanPosePreSolveGhostRootWorldY = float.NaN,
            SetHumanPosePreSolveGhostRootWorldZ = float.NaN,
            SetHumanPosePreSolveGhostRootYaw = float.NaN,
            SetHumanPosePreSolveTargetRootWorldX = float.NaN,
            SetHumanPosePreSolveTargetRootWorldY = float.NaN,
            SetHumanPosePreSolveTargetRootWorldZ = float.NaN,
            SetHumanPosePreSolveTargetRootYaw = float.NaN,
            SetHumanPosePreSolveTargetHipsWorldX = float.NaN,
            SetHumanPosePreSolveTargetHipsWorldY = float.NaN,
            SetHumanPosePreSolveTargetHipsWorldZ = float.NaN,
            SetHumanPosePreSolveTargetHipsLocalX = float.NaN,
            SetHumanPosePreSolveTargetHipsLocalY = float.NaN,
            SetHumanPosePreSolveTargetHipsLocalZ = float.NaN,
            SetHumanPosePreSolveBodyPositionX = float.NaN,
            SetHumanPosePreSolveBodyPositionY = float.NaN,
            SetHumanPosePreSolveBodyPositionZ = float.NaN,
            SetHumanPosePreSolveBodyRotationYaw = float.NaN,
            PreSetHumanPoseEndpointBodyPositionBeforeX = float.NaN,
            PreSetHumanPoseEndpointBodyPositionBeforeZ = float.NaN,
            PreSetHumanPoseEndpointBodyPositionAfterX = float.NaN,
            PreSetHumanPoseEndpointBodyPositionAfterZ = float.NaN,
            PreSetHumanPoseEndpointBodyPositionDeltaX = float.NaN,
            PreSetHumanPoseEndpointBodyPositionDeltaZ = float.NaN,
            PreSetHumanPoseEndpointBodyPositionDeltaMagnitudeXZ = float.NaN,
            SetHumanPosePreSolveGhostLeftFootWorldX = float.NaN,
            SetHumanPosePreSolveGhostLeftFootWorldZ = float.NaN,
            SetHumanPosePreSolveGhostLeftToesWorldX = float.NaN,
            SetHumanPosePreSolveGhostLeftToesWorldZ = float.NaN,
            SetHumanPosePreSolveCurrentLeftFootWorldX = float.NaN,
            SetHumanPosePreSolveCurrentLeftFootWorldZ = float.NaN,
            SetHumanPosePreSolveCurrentLeftToesWorldX = float.NaN,
            SetHumanPosePreSolveCurrentLeftToesWorldZ = float.NaN,
            SetHumanPosePreSolveTargetLeftFootWorldX = float.NaN,
            SetHumanPosePreSolveTargetLeftFootWorldZ = float.NaN,
            SetHumanPosePreSolveTargetLeftToesWorldX = float.NaN,
            SetHumanPosePreSolveTargetLeftToesWorldZ = float.NaN,
            SetHumanPosePreSolveGhostRightFootWorldX = float.NaN,
            SetHumanPosePreSolveGhostRightFootWorldZ = float.NaN,
            SetHumanPosePreSolveGhostRightToesWorldX = float.NaN,
            SetHumanPosePreSolveGhostRightToesWorldZ = float.NaN,
            SetHumanPosePreSolveCurrentRightFootWorldX = float.NaN,
            SetHumanPosePreSolveCurrentRightFootWorldZ = float.NaN,
            SetHumanPosePreSolveCurrentRightToesWorldX = float.NaN,
            SetHumanPosePreSolveCurrentRightToesWorldZ = float.NaN,
            SetHumanPosePreSolveTargetRightFootWorldX = float.NaN,
            SetHumanPosePreSolveTargetRightFootWorldZ = float.NaN,
            SetHumanPosePreSolveTargetRightToesWorldX = float.NaN,
            SetHumanPosePreSolveTargetRightToesWorldZ = float.NaN,
            SetHumanPoseInputSpineFrontBackMuscle = float.NaN,
            SetHumanPoseInputSpineLeftRightMuscle = float.NaN,
            SetHumanPoseInputSpineTwistLeftRightMuscle = float.NaN,
            SetHumanPoseInputChestFrontBackMuscle = float.NaN,
            SetHumanPoseInputChestLeftRightMuscle = float.NaN,
            SetHumanPoseInputChestTwistLeftRightMuscle = float.NaN,
            SetHumanPoseInputUpperChestFrontBackMuscle = float.NaN,
            SetHumanPoseInputUpperChestLeftRightMuscle = float.NaN,
            SetHumanPoseInputUpperChestTwistLeftRightMuscle = float.NaN,
            SetHumanPoseInputLeftUpperLegInOutMuscle = float.NaN,
            SetHumanPoseInputRightUpperLegInOutMuscle = float.NaN,
            SetHumanPoseInputLeftUpperLegTwistInOutMuscle = float.NaN,
            SetHumanPoseInputRightUpperLegTwistInOutMuscle = float.NaN,
            SetHumanPoseInputLeftLowerLegTwistInOutMuscle = float.NaN,
            SetHumanPoseInputRightLowerLegTwistInOutMuscle = float.NaN,
            SetHumanPoseInputLeftFootTwistInOutMuscle = float.NaN,
            SetHumanPoseInputRightFootTwistInOutMuscle = float.NaN,
            SetHumanPoseInputLeftToesUpDownMuscle = float.NaN,
            SetHumanPoseInputRightToesUpDownMuscle = float.NaN,
            SetHumanPoseOutputRightUpperLegInOutMuscle = float.NaN,
            SetHumanPoseRightUpperLegInOutDelta = float.NaN,
            SetHumanPoseOutputRightUpperLegTwistInOutMuscle = float.NaN,
            SetHumanPoseRightUpperLegTwistInOutDelta = float.NaN,
            SetHumanPoseOutputRightLowerLegTwistInOutMuscle = float.NaN,
            SetHumanPoseRightLowerLegTwistInOutDelta = float.NaN,
            SetHumanPoseOutputRightFootTwistInOutMuscle = float.NaN,
            SetHumanPoseRightFootTwistInOutDelta = float.NaN,
            SetHumanPoseOutputRightToesUpDownMuscle = float.NaN,
            SetHumanPoseRightToesUpDownDelta = float.NaN,
            RetargetStageGhost = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterSetHumanPose = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterManualReferences = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterRootRestore = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterRootDelta = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterGrounding = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterBipedIK = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterLateVisualGrounding = RetargetEndpointStageMetrics.Empty,
            EditorFootLocalRotationLeftFootXzDelta = float.NaN,
            EditorFootLocalRotationRightFootXzDelta = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootXzDelta = float.NaN,
            EditorLowerBodySegmentDirectionRightFootXzDelta = float.NaN,
            EditorLowerBodySegmentDirectionMaxCorrectionSegment = "",
            EditorLowerBodySegmentDirectionMaxCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionMaxPreAngle = float.NaN,
            EditorLowerBodySegmentDirectionMaxPostAngle = float.NaN,
            EditorLowerBodySegmentDirectionMaxCorrectionAxisX = float.NaN,
            EditorLowerBodySegmentDirectionMaxCorrectionAxisY = float.NaN,
            EditorLowerBodySegmentDirectionMaxCorrectionAxisZ = float.NaN,
            EditorLowerBodySegmentDirectionMaxReferenceDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionMaxReferenceDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionMaxReferenceDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionMaxPreDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionMaxPreDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionMaxPreDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionMaxPostDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionMaxPostDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionMaxPostDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToesCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegWorldX = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegWorldY = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootWorldX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootWorldY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftToesWorldX = float.NaN,
            EditorLowerBodySegmentDirectionLeftToesWorldY = float.NaN,
            EditorLowerBodySegmentDirectionLeftToesWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegWorldX = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegWorldY = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootWorldX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootWorldY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionRightToesWorldX = float.NaN,
            EditorLowerBodySegmentDirectionRightToesWorldY = float.NaN,
            EditorLowerBodySegmentDirectionRightToesWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootForwardX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootForwardY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootForwardZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootUpX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootUpY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootUpZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootForwardX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootForwardY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootForwardZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootUpX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootUpY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootUpZ = float.NaN,
            EditorFootHipsAlignedResidualYawLeftFootXzDelta = float.NaN,
            EditorFootHipsAlignedResidualYawRightFootXzDelta = float.NaN,
            PostSetRightEndpointDesiredFootWorldX = float.NaN,
            PostSetRightEndpointDesiredFootWorldZ = float.NaN,
            PostSetRightEndpointDesiredToesWorldX = float.NaN,
            PostSetRightEndpointDesiredToesWorldZ = float.NaN,
            PostSetRightEndpointCurrentFootWorldX = float.NaN,
            PostSetRightEndpointCurrentFootWorldZ = float.NaN,
            PostSetRightEndpointCurrentToesWorldX = float.NaN,
            PostSetRightEndpointCurrentToesWorldZ = float.NaN,
            PostSetRightEndpointDeltaBeforeClampX = float.NaN,
            PostSetRightEndpointDeltaBeforeClampZ = float.NaN,
            PostSetRightEndpointDeltaAfterClampX = float.NaN,
            PostSetRightEndpointDeltaAfterClampZ = float.NaN,
            PostSetRightEndpointDeltaAfterPositiveZScaleX = float.NaN,
            PostSetRightEndpointDeltaAfterPositiveZScaleZ = float.NaN,
            PostSetRightEndpointCorrectionX = float.NaN,
            PostSetRightEndpointCorrectionZ = float.NaN,
            PostSetRightEndpointNextFootWorldX = float.NaN,
            PostSetRightEndpointNextFootWorldZ = float.NaN,
            PostSetRightEndpointMaxYawAngle = float.NaN,
            PostSetRightEndpointYawCorrectionAngle = float.NaN,
            PostSetRightEndpointUpperLegRotationDeltaAngle = float.NaN,
            PostSetRightEndpointApplied = float.NaN,
            PostSetRightEndpointEvaluatorXzReferenceEnabled = float.NaN,
            PostSetRightEndpointEvaluatorXzFirstOffsetX = float.NaN,
            PostSetRightEndpointEvaluatorXzFirstOffsetZ = float.NaN,
            PostSetRightEndpointEvaluatorXzNormalizedDeltaX = float.NaN,
            PostSetRightEndpointEvaluatorXzNormalizedDeltaZ = float.NaN,
            PostSetRightEndpointEvaluatorXzNormalizedMagnitude = float.NaN,
            PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaX = float.NaN,
            PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaZ = float.NaN,
            PostSetRightEndpointEvaluatorXzTargetMagnitude = float.NaN,
            GroundingMaxStepPerFrame = float.NaN,
            GroundingLastStepToMaxStepRatio = float.NaN,
            GroundingLastStepAtMaxStep = -1
        };
    }
    private struct YybSideDiagnosticMetrics
    {
        public bool ThumbDirectionAvailable;
        public bool PalmFrameAvailable;
        public bool HelperCoverageRequired;
        public bool HelperRelationshipAvailable;
        public float ThumbIndexSpreadAngle;
        public float ThumbPalmProjection;
        public float ThumbSpreadRisk;
        public float ThumbProjectionRisk;
        public float ThumbHelperSourceDistance;
        public float ThumbHelperSourceDistanceDelta;
        public float ThumbHelperSourceRotationDelta;
        public float ThumbHelperSeparationRisk;
        public float WebbingRisk;
        public float ArmTwistRisk;
        public float SleeveAnchorRisk;
        public float SleeveAnchorDistance;
        public float SleeveThicknessRatio;
        public float SleeveThicknessRisk;
        public float DeformationRisk;
        public bool HasCoreThumbAnatomy => ThumbDirectionAvailable && PalmFrameAvailable;

        public static YybSideDiagnosticMetrics Empty => new YybSideDiagnosticMetrics
        {
            ThumbDirectionAvailable = false,
            PalmFrameAvailable = false,
            HelperCoverageRequired = false,
            HelperRelationshipAvailable = false,
            ThumbIndexSpreadAngle = float.NaN,
            ThumbPalmProjection = float.NaN,
            ThumbSpreadRisk = float.NaN,
            ThumbProjectionRisk = float.NaN,
            ThumbHelperSourceDistance = float.NaN,
            ThumbHelperSourceDistanceDelta = float.NaN,
            ThumbHelperSourceRotationDelta = float.NaN,
            ThumbHelperSeparationRisk = float.NaN,
            WebbingRisk = float.NaN,
            ArmTwistRisk = float.NaN,
            SleeveAnchorRisk = float.NaN,
            SleeveAnchorDistance = float.NaN,
            SleeveThicknessRatio = float.NaN,
            SleeveThicknessRisk = float.NaN,
            DeformationRisk = float.NaN
        };

        public void ClearYybOnlyRiskScores()
        {
            ArmTwistRisk = float.NaN;
            SleeveAnchorRisk = float.NaN;
            SleeveAnchorDistance = float.NaN;
            SleeveThicknessRatio = float.NaN;
            SleeveThicknessRisk = float.NaN;
            DeformationRisk = float.NaN;
        }
    }
    private struct PoseMetrics
    {
        public string Label;
        public string Scene;
        public string Reason;
        public float Elapsed;
        public float TimeSinceLevelLoad;
        public int FrameCount;
        public int RecorderFrame;
        public string AnimationTimeSource;
        public string AnimationClipName;
        public float AnimationClipTime;
        public float AnimationClipLength;
        public float AnimationNormalizedTime;
        public Vector3 RootPosition;
        public float RootYaw;
        public RootSpikeMetrics RootSpike;
        public float BodyPositionY;
        public float HipsLocalY;
        public Vector3 HipsPosition;
        public float HipsY;
        public float LowestFootY;
        public float LowestFootBottomY;
        public Vector3 LeftFootPosition;
        public Vector3 RightFootPosition;
        public float MeshBoundsMinY;
        public float MeshBoundsMaxY;
        public float FootBottomGroundGap;
        public float MeshBoundsGroundGap;
        public float CameraFacingDot;
        public float MaxScaleDelta;
        public Vector3 LeftUpperArmScale;
        public Vector3 RightUpperArmScale;
        public Vector3 LeftUpperLegScale;
        public Vector3 RightUpperLegScale;
        public Vector3 SpineLocalEuler;
        public Vector3 ChestLocalEuler;
        public Vector3 UpperChestLocalEuler;
        public Vector3 LeftShoulderLocalEuler;
        public Vector3 RightShoulderLocalEuler;
        public Vector3 LeftUpperArmLocalEuler;
        public Vector3 RightUpperArmLocalEuler;
        public Vector3 LeftLowerArmLocalEuler;
        public Vector3 RightLowerArmLocalEuler;
        public Vector3 LeftHandLocalEuler;
        public Vector3 RightHandLocalEuler;
        public Vector3 LeftThumbProximalLocalEuler;
        public Vector3 LeftIndexProximalLocalEuler;
        public Vector3 LeftMiddleProximalLocalEuler;
        public Vector3 LeftRingProximalLocalEuler;
        public Vector3 LeftLittleProximalLocalEuler;
        public Vector3 RightThumbProximalLocalEuler;
        public Vector3 RightIndexProximalLocalEuler;
        public Vector3 RightMiddleProximalLocalEuler;
        public Vector3 RightRingProximalLocalEuler;
        public Vector3 RightLittleProximalLocalEuler;
        public float LeftArmLength;
        public float RightArmLength;
        public float LeftLegLength;
        public float RightLegLength;
        public float LeftElbowAngle;
        public float RightElbowAngle;
        public float LeftKneeAngle;
        public float RightKneeAngle;
        public float LeftElbowBendForward;
        public float RightElbowBendForward;
        public float LeftKneeBendForward;
        public float RightKneeBendForward;
        public float LeftElbowBendOffsetForward;
        public float RightElbowBendOffsetForward;
        public float LeftKneeBendOffsetForward;
        public float RightKneeBendOffsetForward;
        public float LeftUpperArmDownDot;
        public float RightUpperArmDownDot;
        public float LeftHandHorizontalRatio;
        public float RightHandHorizontalRatio;
        public float LeftHandBelowShoulderRatio;
        public float RightHandBelowShoulderRatio;
        public float LeftHandTorsoSignedClearance;
        public float RightHandTorsoSignedClearance;
        public float MinHandTorsoSignedClearance;
        public float HandTorsoPenetrationRisk;
        public float LeftShoulderDownUpMuscle;
        public float LeftShoulderFrontBackMuscle;
        public float LeftArmDownUpMuscle;
        public float LeftArmFrontBackMuscle;
        public float LeftArmTwistMuscle;
        public float LeftForearmStretchMuscle;
        public float LeftForearmTwistMuscle;
        public float RightShoulderDownUpMuscle;
        public float RightShoulderFrontBackMuscle;
        public float RightArmDownUpMuscle;
        public float RightArmFrontBackMuscle;
        public float RightArmTwistMuscle;
        public float RightForearmStretchMuscle;
        public float RightForearmTwistMuscle;
        public ArmSwingGuardDiagnostics ArmSwingGuard;
        public float LeftThumb1StretchMuscle;
        public float LeftThumbSpreadMuscle;
        public float LeftIndex1StretchMuscle;
        public float LeftIndexSpreadMuscle;
        public float LeftMiddle1StretchMuscle;
        public float LeftMiddleSpreadMuscle;
        public float LeftRing1StretchMuscle;
        public float LeftRingSpreadMuscle;
        public float LeftLittle1StretchMuscle;
        public float LeftLittleSpreadMuscle;
        public float RightThumb1StretchMuscle;
        public float RightThumbSpreadMuscle;
        public float RightIndex1StretchMuscle;
        public float RightIndexSpreadMuscle;
        public float RightMiddle1StretchMuscle;
        public float RightMiddleSpreadMuscle;
        public float RightRing1StretchMuscle;
        public float RightRingSpreadMuscle;
        public float RightLittle1StretchMuscle;
        public float RightLittleSpreadMuscle;
        public ThumbGuardDiagnostics ThumbGuard;
        public YybDiagnosticMetrics YybDiagnostics;

        public string ToCsvLine()
        {
            return MotionComparisonProbeReportWriter.BuildMetricsCsvLine(
                Escape(Label),
                Escape(Scene),
                Escape(Reason),
                F(Elapsed),
                F(TimeSinceLevelLoad),
                I(FrameCount),
                I(RecorderFrame),
                Escape(AnimationTimeSource),
                Escape(AnimationClipName),
                F(AnimationClipTime),
                F(AnimationClipLength),
                F(AnimationNormalizedTime),
                F(RootPosition.x),
                F(RootPosition.y),
                F(RootPosition.z),
                F(RootYaw),
                F(RootSpike.LastRootDeltaMagnitude),
                F(RootSpike.MaxRootDeltaMagnitude),
                I(RootSpike.RootDeltaSpikeSkippedCount),
                F(RootSpike.LastRootPositionPoseDeltaMagnitude),
                F(RootSpike.MaxRootPositionPoseDeltaMagnitude),
                I(RootSpike.RootPositionSpikeClampedCount),
                F(RootSpike.LastGroundingAdjustment),
                F(RootSpike.MaxGroundingAdjustment),
                I(RootSpike.GroundingStepClampedCount),
                I(RootSpike.GroundingSmoothedCount),
                F(RootSpike.LastGroundingVerticalStep),
                F(RootSpike.MaxGroundingVerticalStep),
                F(RootSpike.InitialGroundingVerticalStep),
                F(RootSpike.MaxGroundingVerticalStepAfterInitial),
                F(RootSpike.LastGroundingTargetY),
                F(RootSpike.LastGroundingLowestFootBottomY),
                F(RootSpike.GroundingMaxStepPerFrame),
                F(RootSpike.GroundingLastStepToMaxStepRatio),
                I(RootSpike.GroundingLastStepAtMaxStep),
                F(RootSpike.RecordingStartRootY),
                F(RootSpike.RecordingStartBodyPositionY),
                F(RootSpike.RecordingStartHipsLocalY),
                F(RootSpike.RecordingStartHipsY),
                F(RootSpike.RecordingStartHipsReferenceBeforeLocalY),
                F(RootSpike.RecordingStartHipsReferenceAfterLocalY),
                F(RootSpike.RecordingStartHipsReferenceDeltaY),
                I(RootSpike.RecordingStartHipsReferenceFlipDetected),
                Escape(RootSpike.RecordingStartHipsReferenceStage),
                F(RootSpike.PoseInputLeftShoulderFrontBackMuscle),
                F(RootSpike.AfterEditorMuscleReferenceLeftShoulderFrontBackMuscle),
                F(RootSpike.AfterClampPoseMusclesLeftShoulderFrontBackMuscle),
                F(RootSpike.AfterAnatomicalArmGuardLeftShoulderFrontBackMuscle),
                F(RootSpike.AfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle),
                F(RootSpike.SetHumanPoseInputLeftShoulderFrontBackMuscle),
                F(RootSpike.SetHumanPoseOutputLeftShoulderFrontBackMuscle),
                F(RootSpike.SetHumanPoseLeftShoulderFrontBackDelta),
                F(RootSpike.PoseInputLeftArmTwistMuscle),
                F(RootSpike.AfterEditorMuscleReferenceLeftArmTwistMuscle),
                F(RootSpike.AfterClampPoseMusclesLeftArmTwistMuscle),
                F(RootSpike.AfterAnatomicalArmGuardLeftArmTwistMuscle),
                F(RootSpike.AfterVisualSpikeSmoothingLeftArmTwistMuscle),
                F(RootSpike.SetHumanPoseInputLeftArmTwistMuscle),
                F(RootSpike.SetHumanPoseOutputLeftArmTwistMuscle),
                F(RootSpike.SetHumanPoseLeftArmTwistDelta),
                F(RootSpike.PoseInputLeftForearmStretchMuscle),
                F(RootSpike.AfterEditorMuscleReferenceLeftForearmStretchMuscle),
                F(RootSpike.AfterClampPoseMusclesLeftForearmStretchMuscle),
                F(RootSpike.AfterAnatomicalArmGuardLeftForearmStretchMuscle),
                F(RootSpike.AfterVisualSpikeSmoothingLeftForearmStretchMuscle),
                F(RootSpike.SetHumanPoseInputLeftForearmStretchMuscle),
                F(RootSpike.SetHumanPoseOutputLeftForearmStretchMuscle),
                F(RootSpike.SetHumanPoseLeftForearmStretchDelta),
                F(RootSpike.PoseInputRightForearmStretchMuscle),
                F(RootSpike.AfterEditorMuscleReferenceRightForearmStretchMuscle),
                F(RootSpike.AfterClampPoseMusclesRightForearmStretchMuscle),
                F(RootSpike.AfterAnatomicalArmGuardRightForearmStretchMuscle),
                F(RootSpike.AfterVisualSpikeSmoothingRightForearmStretchMuscle),
                F(RootSpike.SetHumanPoseInputRightForearmStretchMuscle),
                F(RootSpike.SetHumanPoseOutputRightForearmStretchMuscle),
                F(RootSpike.SetHumanPoseRightForearmStretchDelta),
                F(RootSpike.PoseInputRightArmTwistMuscle),
                F(RootSpike.AfterEditorMuscleReferenceRightArmTwistMuscle),
                F(RootSpike.AfterClampPoseMusclesRightArmTwistMuscle),
                F(RootSpike.AfterAnatomicalArmGuardRightArmTwistMuscle),
                F(RootSpike.AfterVisualSpikeSmoothingRightArmTwistMuscle),
                F(RootSpike.SetHumanPoseInputRightArmTwistMuscle),
                F(RootSpike.SetHumanPoseOutputRightArmTwistMuscle),
                F(RootSpike.SetHumanPoseRightArmTwistDelta),
                F(RootSpike.SetHumanPoseInputLeftUpperLegFrontBackMuscle),
                F(RootSpike.SetHumanPoseOutputLeftUpperLegFrontBackMuscle),
                F(RootSpike.SetHumanPoseLeftUpperLegFrontBackDelta),
                F(RootSpike.SetHumanPoseInputRightUpperLegFrontBackMuscle),
                F(RootSpike.SetHumanPoseOutputRightUpperLegFrontBackMuscle),
                F(RootSpike.SetHumanPoseRightUpperLegFrontBackDelta),
                F(RootSpike.SetHumanPoseInputLeftLowerLegStretchMuscle),
                F(RootSpike.SetHumanPoseOutputLeftLowerLegStretchMuscle),
                F(RootSpike.SetHumanPoseLeftLowerLegStretchDelta),
                F(RootSpike.SetHumanPoseInputRightLowerLegStretchMuscle),
                F(RootSpike.SetHumanPoseOutputRightLowerLegStretchMuscle),
                F(RootSpike.SetHumanPoseRightLowerLegStretchDelta),
                F(RootSpike.SetHumanPoseInputLeftFootUpDownMuscle),
                F(RootSpike.SetHumanPoseOutputLeftFootUpDownMuscle),
                F(RootSpike.SetHumanPoseLeftFootUpDownDelta),
                F(RootSpike.SetHumanPoseInputRightFootUpDownMuscle),
                F(RootSpike.SetHumanPoseOutputRightFootUpDownMuscle),
                F(RootSpike.SetHumanPoseRightFootUpDownDelta),
                F(BodyPositionY),
                F(HipsLocalY),
                F(RootSpike.FootHeightReferenceLift),
                F(HipsPosition.x),
                F(HipsPosition.z),
                F(HipsY),
                F(LowestFootY),
                F(LowestFootBottomY),
                F(LeftFootPosition.x),
                F(LeftFootPosition.z),
                F(RightFootPosition.x),
                F(RightFootPosition.z),
                F(MeshBoundsMinY),
                F(MeshBoundsMaxY),
                F(FootBottomGroundGap),
                F(MeshBoundsGroundGap),
                F(CameraFacingDot),
                F(MaxScaleDelta),
                V(LeftUpperArmScale),
                V(RightUpperArmScale),
                V(LeftUpperLegScale),
                V(RightUpperLegScale),
                F(LeftArmLength),
                F(RightArmLength),
                F(LeftLegLength),
                F(RightLegLength),
                F(LeftElbowAngle),
                F(RightElbowAngle),
                F(LeftKneeAngle),
                F(RightKneeAngle),
                F(LeftElbowBendForward),
                F(RightElbowBendForward),
                F(LeftKneeBendForward),
                F(RightKneeBendForward),
                F(LeftElbowBendOffsetForward),
                F(RightElbowBendOffsetForward),
                F(LeftKneeBendOffsetForward),
                F(RightKneeBendOffsetForward),
                F(LeftUpperArmDownDot),
                F(RightUpperArmDownDot),
                F(LeftHandHorizontalRatio),
                F(RightHandHorizontalRatio),
                F(LeftHandBelowShoulderRatio),
                F(RightHandBelowShoulderRatio),
                F(LeftHandTorsoSignedClearance),
                F(RightHandTorsoSignedClearance),
                F(MinHandTorsoSignedClearance),
                F(HandTorsoPenetrationRisk),
                F(LeftShoulderDownUpMuscle),
                F(LeftShoulderFrontBackMuscle),
                F(LeftArmDownUpMuscle),
                F(LeftArmFrontBackMuscle),
                F(LeftArmTwistMuscle),
                F(ArmSwingGuard.LeftApplied),
                F(ArmSwingGuard.LeftHorizontalReachApplied),
                F(ArmSwingGuard.LeftRaisedReachApplied),
                F(ArmSwingGuard.LeftForearmStretchBefore),
                F(ArmSwingGuard.LeftForearmStretchAfter),
                F(ArmSwingGuard.LeftForearmStretchDelta),
                F(LeftForearmStretchMuscle),
                F(LeftForearmTwistMuscle),
                F(RightShoulderDownUpMuscle),
                F(RightShoulderFrontBackMuscle),
                F(RightArmDownUpMuscle),
                F(RightArmFrontBackMuscle),
                F(RightArmTwistMuscle),
                F(ArmSwingGuard.RightApplied),
                F(ArmSwingGuard.RightHorizontalReachApplied),
                F(ArmSwingGuard.RightRaisedReachApplied),
                F(ArmSwingGuard.RightForearmStretchBefore),
                F(ArmSwingGuard.RightForearmStretchAfter),
                F(ArmSwingGuard.RightForearmStretchDelta),
                F(RightForearmStretchMuscle),
                F(RightForearmTwistMuscle),
                F(LeftThumb1StretchMuscle),
                F(LeftThumbSpreadMuscle),
                F(LeftIndex1StretchMuscle),
                F(LeftIndexSpreadMuscle),
                F(LeftMiddle1StretchMuscle),
                F(LeftMiddleSpreadMuscle),
                F(LeftRing1StretchMuscle),
                F(LeftRingSpreadMuscle),
                F(LeftLittle1StretchMuscle),
                F(LeftLittleSpreadMuscle),
                F(RightThumb1StretchMuscle),
                F(RightThumbSpreadMuscle),
                F(RightIndex1StretchMuscle),
                F(RightIndexSpreadMuscle),
                F(RightMiddle1StretchMuscle),
                F(RightMiddleSpreadMuscle),
                F(RightRing1StretchMuscle),
                F(RightRingSpreadMuscle),
                F(RightLittle1StretchMuscle),
                F(RightLittleSpreadMuscle),
                V(SpineLocalEuler),
                V(ChestLocalEuler),
                V(UpperChestLocalEuler),
                V(LeftShoulderLocalEuler),
                V(RightShoulderLocalEuler),
                V(LeftUpperArmLocalEuler),
                V(RightUpperArmLocalEuler),
                V(LeftLowerArmLocalEuler),
                V(RightLowerArmLocalEuler),
                V(LeftHandLocalEuler),
                V(RightHandLocalEuler),
                V(LeftThumbProximalLocalEuler),
                V(LeftIndexProximalLocalEuler),
                V(LeftMiddleProximalLocalEuler),
                V(LeftRingProximalLocalEuler),
                V(LeftLittleProximalLocalEuler),
                V(RightThumbProximalLocalEuler),
                V(RightIndexProximalLocalEuler),
                V(RightMiddleProximalLocalEuler),
                V(RightRingProximalLocalEuler),
                V(RightLittleProximalLocalEuler),
                F(YybDiagnostics.Left.ThumbIndexSpreadAngle),
                F(YybDiagnostics.Right.ThumbIndexSpreadAngle),
                F(YybDiagnostics.Left.ThumbPalmProjection),
                F(YybDiagnostics.Right.ThumbPalmProjection),
                F(YybDiagnostics.Left.ThumbSpreadRisk),
                F(YybDiagnostics.Right.ThumbSpreadRisk),
                F(YybDiagnostics.Left.ThumbProjectionRisk),
                F(YybDiagnostics.Right.ThumbProjectionRisk),
                F(YybDiagnostics.Left.ThumbHelperSourceDistance),
                F(YybDiagnostics.Right.ThumbHelperSourceDistance),
                F(YybDiagnostics.Left.ThumbHelperSourceDistanceDelta),
                F(YybDiagnostics.Right.ThumbHelperSourceDistanceDelta),
                F(YybDiagnostics.Left.ThumbHelperSourceRotationDelta),
                F(YybDiagnostics.Right.ThumbHelperSourceRotationDelta),
                F(YybDiagnostics.Left.ThumbHelperSeparationRisk),
                F(YybDiagnostics.Right.ThumbHelperSeparationRisk),
                F(YybDiagnostics.Left.WebbingRisk),
                F(YybDiagnostics.Right.WebbingRisk),
                F(YybDiagnostics.Left.ArmTwistRisk),
                F(YybDiagnostics.Right.ArmTwistRisk),
                F(YybDiagnostics.Left.SleeveAnchorRisk),
                F(YybDiagnostics.Right.SleeveAnchorRisk),
                F(YybDiagnostics.Left.SleeveAnchorDistance),
                F(YybDiagnostics.Right.SleeveAnchorDistance),
                F(YybDiagnostics.Left.SleeveThicknessRatio),
                F(YybDiagnostics.Right.SleeveThicknessRatio),
                F(YybDiagnostics.Left.SleeveThicknessRisk),
                F(YybDiagnostics.Right.SleeveThicknessRisk),
                F(YybDiagnostics.Left.DeformationRisk),
                F(YybDiagnostics.Right.DeformationRisk),
                F(YybDiagnostics.MaxDeformationRisk),
                F(ThumbGuard.ManualThumbReferenceConfigured),
                F(ThumbGuard.ManualThumbReferenceActive),
                F(ThumbGuard.PoseShapingSuppressed),
                F(ThumbGuard.LeftPoseShapingSuppressed),
                F(ThumbGuard.RightPoseShapingSuppressed),
                F(ThumbGuard.ProjectionGuardWeight),
                F(ThumbGuard.LeftProjectionGuardWeight),
                F(ThumbGuard.RightProjectionGuardWeight),
                F(ThumbGuard.IndexSpreadGuardWeight),
                F(ThumbGuard.LeftIndexSpreadGuardWeight),
                F(ThumbGuard.RightIndexSpreadGuardWeight),
                F(ThumbGuard.SegmentStraightenWeight),
                F(ThumbGuard.LeftSegmentStraightenWeight),
                F(ThumbGuard.RightSegmentStraightenWeight),
                F(ThumbGuard.LeftProjectionCorrectionApplyCount),
                F(ThumbGuard.RightProjectionCorrectionApplyCount),
                F(ThumbGuard.LeftProjectionCorrectionPreserveCount),
                F(ThumbGuard.RightProjectionCorrectionPreserveCount),
                F(ThumbGuard.LeftSegmentStraightenApplyCount),
                F(ThumbGuard.RightSegmentStraightenApplyCount),
                F(ThumbGuard.LeftSegmentStraightenPreserveCount),
                F(ThumbGuard.RightSegmentStraightenPreserveCount),
                F(ThumbGuard.LeftLocalRotationGuardClampCount),
                F(ThumbGuard.RightLocalRotationGuardClampCount),
                F(ThumbGuard.LeftLocalRotationGuardPreserveCount),
                F(ThumbGuard.RightLocalRotationGuardPreserveCount),
                F(ThumbGuard.LeftLocalRotationGuardCurrentRisk),
                F(ThumbGuard.RightLocalRotationGuardCurrentRisk),
                F(ThumbGuard.LeftLocalRotationGuardLimitedRisk),
                F(ThumbGuard.RightLocalRotationGuardLimitedRisk),
                F(ThumbGuard.LeftWorldRotationSuppressCompetingOverride),
                F(ThumbGuard.RightWorldRotationSuppressCompetingOverride),
                F(ThumbGuard.LeftWorldRotationKeepDetachedHelperOverride),
                F(ThumbGuard.RightWorldRotationKeepDetachedHelperOverride),
                F(ThumbGuard.LeftWorldRotationCurrentReferenceFrameDeviation),
                F(ThumbGuard.RightWorldRotationCurrentReferenceFrameDeviation),
                F(ThumbGuard.LeftWorldRotationCandidateReferenceFrameDeviation),
                F(ThumbGuard.RightWorldRotationCandidateReferenceFrameDeviation),
                F(ThumbGuard.LeftProximalWorldRotationPreserveReason),
                F(ThumbGuard.RightProximalWorldRotationPreserveReason),
                F(ThumbGuard.LeftIntermediateWorldRotationPreserveReason),
                F(ThumbGuard.RightIntermediateWorldRotationPreserveReason),
                F(ThumbGuard.LeftProximalWorldRotationCurrentReferenceAngle),
                F(ThumbGuard.RightProximalWorldRotationCurrentReferenceAngle),
                F(ThumbGuard.LeftIntermediateWorldRotationCurrentReferenceAngle),
                F(ThumbGuard.RightIntermediateWorldRotationCurrentReferenceAngle),
                F(ThumbGuard.LeftProximalWorldRotationCandidateReferenceAngle),
                F(ThumbGuard.RightProximalWorldRotationCandidateReferenceAngle),
                F(ThumbGuard.LeftIntermediateWorldRotationCandidateReferenceAngle),
                F(ThumbGuard.RightIntermediateWorldRotationCandidateReferenceAngle),
                F(ThumbGuard.LeftProximalWorldRotationPreserveCurrentRisk),
                F(ThumbGuard.RightProximalWorldRotationPreserveCurrentRisk),
                F(ThumbGuard.LeftIntermediateWorldRotationPreserveCurrentRisk),
                F(ThumbGuard.RightIntermediateWorldRotationPreserveCurrentRisk),
                F(ThumbGuard.LeftProximalWorldRotationPreserveLimitedRisk),
                F(ThumbGuard.RightProximalWorldRotationPreserveLimitedRisk),
                F(ThumbGuard.LeftIntermediateWorldRotationPreserveLimitedRisk),
                F(ThumbGuard.RightIntermediateWorldRotationPreserveLimitedRisk),
                F(ThumbGuard.HelperSyncEnabled),
                F(ThumbGuard.HelperPositionSyncEnabled),
                F(ThumbGuard.HelperSyncWeight),
                F(ThumbGuard.HelperMaxLocalAngle),
                F(ThumbGuard.PalmStabilizeEnabled),
                F(ThumbGuard.PalmStabilizeWeight),
                F(ThumbGuard.PalmStabilizeMaxLocalAngle),
                F(ThumbGuard.WebbingStabilizeEnabled),
                F(ThumbGuard.WebbingStabilizeWeight),
                F(ThumbGuard.WebbingMaxLocalAngle),
                F(ThumbGuard.WebbingMaxPositionOffset),
                F(RootSpike.RetargetStageGhost.LeftFootWorldX),
                F(RootSpike.RetargetStageGhost.LeftFootWorldZ),
                F(RootSpike.RetargetStageGhost.LeftToesWorldX),
                F(RootSpike.RetargetStageGhost.LeftToesWorldZ),
                F(RootSpike.RetargetStageGhost.RightFootWorldX),
                F(RootSpike.RetargetStageGhost.RightFootWorldZ),
                F(RootSpike.RetargetStageGhost.RightToesWorldX),
                F(RootSpike.RetargetStageGhost.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterSetHumanPose.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterSetHumanPose.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX),
                F(RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterSetHumanPose.RightToesWorldX),
                F(RootSpike.RetargetStageAfterSetHumanPose.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterManualReferences.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterManualReferences.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterManualReferences.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterManualReferences.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterManualReferences.RightFootWorldX),
                F(RootSpike.RetargetStageAfterManualReferences.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterManualReferences.RightToesWorldX),
                F(RootSpike.RetargetStageAfterManualReferences.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterRootRestore.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterRootRestore.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterRootRestore.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterRootRestore.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterRootRestore.RightFootWorldX),
                F(RootSpike.RetargetStageAfterRootRestore.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterRootRestore.RightToesWorldX),
                F(RootSpike.RetargetStageAfterRootRestore.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterRootDelta.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterRootDelta.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterRootDelta.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterRootDelta.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterRootDelta.RightFootWorldX),
                F(RootSpike.RetargetStageAfterRootDelta.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterRootDelta.RightToesWorldX),
                F(RootSpike.RetargetStageAfterRootDelta.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterGrounding.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterGrounding.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterGrounding.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterGrounding.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterGrounding.RightFootWorldX),
                F(RootSpike.RetargetStageAfterGrounding.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterGrounding.RightToesWorldX),
                F(RootSpike.RetargetStageAfterGrounding.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterBipedIK.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterBipedIK.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterBipedIK.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterBipedIK.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterBipedIK.RightFootWorldX),
                F(RootSpike.RetargetStageAfterBipedIK.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterBipedIK.RightToesWorldX),
                F(RootSpike.RetargetStageAfterBipedIK.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterLateVisualGrounding.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterLateVisualGrounding.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterLateVisualGrounding.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterLateVisualGrounding.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterLateVisualGrounding.RightFootWorldX),
                F(RootSpike.RetargetStageAfterLateVisualGrounding.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterLateVisualGrounding.RightToesWorldX),
                F(RootSpike.RetargetStageAfterLateVisualGrounding.RightToesWorldZ),
                F(RootSpike.EditorFootLocalRotationLeftFootXzDelta),
                F(RootSpike.EditorFootLocalRotationRightFootXzDelta),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootXzDelta),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootXzDelta),
                Escape(RootSpike.EditorLowerBodySegmentDirectionMaxCorrectionSegment),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPreAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPostAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxCorrectionAxisX),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxCorrectionAxisY),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxCorrectionAxisZ),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxReferenceDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxReferenceDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxReferenceDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPreDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPreDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPreDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPostDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPostDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPostDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToesCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPreDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPreDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPostDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPostDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftToesWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftToesWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftToesWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightToesWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightToesWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightToesWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootForwardX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootForwardY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootForwardZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootUpX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootUpY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootUpZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootForwardX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootForwardY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootForwardZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootUpX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootUpY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootUpZ),
                F(RootSpike.EditorFootHipsAlignedResidualYawLeftFootXzDelta),
                F(RootSpike.EditorFootHipsAlignedResidualYawRightFootXzDelta),
                F(RootSpike.PostSetRightEndpointDesiredFootWorldX),
                F(RootSpike.PostSetRightEndpointDesiredFootWorldZ),
                F(RootSpike.PostSetRightEndpointDesiredToesWorldX),
                F(RootSpike.PostSetRightEndpointDesiredToesWorldZ),
                F(RootSpike.PostSetRightEndpointCurrentFootWorldX),
                F(RootSpike.PostSetRightEndpointCurrentFootWorldZ),
                F(RootSpike.PostSetRightEndpointCurrentToesWorldX),
                F(RootSpike.PostSetRightEndpointCurrentToesWorldZ),
                F(RootSpike.PostSetRightEndpointDeltaBeforeClampX),
                F(RootSpike.PostSetRightEndpointDeltaBeforeClampZ),
                F(RootSpike.PostSetRightEndpointDeltaAfterClampX),
                F(RootSpike.PostSetRightEndpointDeltaAfterClampZ),
                F(RootSpike.PostSetRightEndpointDeltaAfterPositiveZScaleX),
                F(RootSpike.PostSetRightEndpointDeltaAfterPositiveZScaleZ),
                F(RootSpike.PostSetRightEndpointCorrectionX),
                F(RootSpike.PostSetRightEndpointCorrectionZ),
                F(RootSpike.PostSetRightEndpointNextFootWorldX),
                F(RootSpike.PostSetRightEndpointNextFootWorldZ),
                F(RootSpike.PostSetRightEndpointMaxYawAngle),
                F(RootSpike.PostSetRightEndpointYawCorrectionAngle),
                F(RootSpike.PostSetRightEndpointUpperLegRotationDeltaAngle),
                F(RootSpike.PostSetRightEndpointApplied),
                F(RootSpike.PostSetRightEndpointEvaluatorXzReferenceEnabled),
                F(RootSpike.PostSetRightEndpointEvaluatorXzFirstOffsetX),
                F(RootSpike.PostSetRightEndpointEvaluatorXzFirstOffsetZ),
                F(RootSpike.PostSetRightEndpointEvaluatorXzNormalizedDeltaX),
                F(RootSpike.PostSetRightEndpointEvaluatorXzNormalizedDeltaZ),
                F(RootSpike.PostSetRightEndpointEvaluatorXzNormalizedMagnitude),
                F(RootSpike.PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaX),
                F(RootSpike.PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaZ),
                F(RootSpike.PostSetRightEndpointEvaluatorXzTargetMagnitude),
                F(RootSpike.SetHumanPoseInputBodyPositionX),
                F(RootSpike.SetHumanPoseInputBodyPositionY),
                F(RootSpike.SetHumanPoseInputBodyPositionZ),
                F(RootSpike.SetHumanPoseOutputBodyPositionX),
                F(RootSpike.SetHumanPoseOutputBodyPositionY),
                F(RootSpike.SetHumanPoseOutputBodyPositionZ),
                F(FiniteDelta(
                    RootSpike.SetHumanPoseInputBodyPositionX,
                    RootSpike.SetHumanPoseOutputBodyPositionX)),
                F(FiniteDelta(
                    RootSpike.SetHumanPoseInputBodyPositionZ,
                    RootSpike.SetHumanPoseOutputBodyPositionZ)),
                F(RootSpike.SetHumanPoseBodyPositionDeltaXZ),
                F(RootSpike.SetHumanPoseInputBodyRotationYaw),
                F(RootSpike.SetHumanPoseOutputBodyRotationYaw),
                F(RootSpike.SetHumanPoseBodyRotationDeltaAngle),
                F(RootSpike.SetHumanPosePreSolveGhostRootWorldX),
                F(RootSpike.SetHumanPosePreSolveGhostRootWorldY),
                F(RootSpike.SetHumanPosePreSolveGhostRootWorldZ),
                F(RootSpike.SetHumanPosePreSolveGhostRootYaw),
                F(RootSpike.SetHumanPosePreSolveTargetRootWorldX),
                F(RootSpike.SetHumanPosePreSolveTargetRootWorldY),
                F(RootSpike.SetHumanPosePreSolveTargetRootWorldZ),
                F(RootSpike.SetHumanPosePreSolveTargetRootYaw),
                F(RootSpike.SetHumanPosePreSolveTargetHipsWorldX),
                F(RootSpike.SetHumanPosePreSolveTargetHipsWorldY),
                F(RootSpike.SetHumanPosePreSolveTargetHipsWorldZ),
                F(RootSpike.SetHumanPosePreSolveTargetHipsLocalX),
                F(RootSpike.SetHumanPosePreSolveTargetHipsLocalY),
                F(RootSpike.SetHumanPosePreSolveTargetHipsLocalZ),
                F(RootSpike.SetHumanPosePreSolveBodyPositionX),
                F(RootSpike.SetHumanPosePreSolveBodyPositionY),
                F(RootSpike.SetHumanPosePreSolveBodyPositionZ),
                F(RootSpike.SetHumanPosePreSolveBodyRotationYaw),
                F(RootSpike.PreSetHumanPoseEndpointBodyPositionBeforeX),
                F(RootSpike.PreSetHumanPoseEndpointBodyPositionBeforeZ),
                F(RootSpike.PreSetHumanPoseEndpointBodyPositionAfterX),
                F(RootSpike.PreSetHumanPoseEndpointBodyPositionAfterZ),
                F(RootSpike.PreSetHumanPoseEndpointBodyPositionDeltaX),
                F(RootSpike.PreSetHumanPoseEndpointBodyPositionDeltaZ),
                F(RootSpike.PreSetHumanPoseEndpointBodyPositionDeltaMagnitudeXZ),
                F(FiniteXzDelta(
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldZ,
                    axis: "x")),
                F(FiniteXzDelta(
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldZ,
                    axis: "z")),
                F(FiniteXzDeltaMagnitude(
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldZ)),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldZ,
                    endpointAxis: "x",
                    RootSpike.PreSetHumanPoseEndpointBodyPositionDeltaX)),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldZ,
                    endpointAxis: "z",
                    RootSpike.PreSetHumanPoseEndpointBodyPositionDeltaX)),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldZ,
                    endpointAxis: "x",
                    RootSpike.PreSetHumanPoseEndpointBodyPositionDeltaZ)),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldZ,
                    endpointAxis: "z",
                    RootSpike.PreSetHumanPoseEndpointBodyPositionDeltaZ)),
                F(FiniteXzDelta(
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ,
                    axis: "x")),
                F(FiniteXzDelta(
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ,
                    axis: "z")),
                F(FiniteXzDeltaMagnitude(
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ)),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ,
                    endpointAxis: "x",
                    RootSpike.PreSetHumanPoseEndpointBodyPositionDeltaX)),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ,
                    endpointAxis: "z",
                    RootSpike.PreSetHumanPoseEndpointBodyPositionDeltaX)),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ,
                    endpointAxis: "x",
                    RootSpike.PreSetHumanPoseEndpointBodyPositionDeltaZ)),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ,
                    endpointAxis: "z",
                    RootSpike.PreSetHumanPoseEndpointBodyPositionDeltaZ)),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ,
                    endpointAxis: "x",
                    FiniteDelta(
                        RootSpike.SetHumanPoseInputBodyPositionX,
                        RootSpike.SetHumanPoseOutputBodyPositionX))),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ,
                    endpointAxis: "z",
                    FiniteDelta(
                        RootSpike.SetHumanPoseInputBodyPositionX,
                        RootSpike.SetHumanPoseOutputBodyPositionX))),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ,
                    endpointAxis: "x",
                    FiniteDelta(
                        RootSpike.SetHumanPoseInputBodyPositionZ,
                        RootSpike.SetHumanPoseOutputBodyPositionZ))),
                F(FiniteXzResponseRatio(
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX,
                    RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX,
                    RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ,
                    endpointAxis: "z",
                    FiniteDelta(
                        RootSpike.SetHumanPoseInputBodyPositionZ,
                        RootSpike.SetHumanPoseOutputBodyPositionZ))),
                F(RootSpike.SetHumanPosePreSolveGhostLeftFootWorldX),
                F(RootSpike.SetHumanPosePreSolveGhostLeftFootWorldZ),
                F(RootSpike.SetHumanPosePreSolveGhostLeftToesWorldX),
                F(RootSpike.SetHumanPosePreSolveGhostLeftToesWorldZ),
                F(RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldX),
                F(RootSpike.SetHumanPosePreSolveCurrentLeftFootWorldZ),
                F(RootSpike.SetHumanPosePreSolveCurrentLeftToesWorldX),
                F(RootSpike.SetHumanPosePreSolveCurrentLeftToesWorldZ),
                F(RootSpike.SetHumanPosePreSolveTargetLeftFootWorldX),
                F(RootSpike.SetHumanPosePreSolveTargetLeftFootWorldZ),
                F(RootSpike.SetHumanPosePreSolveTargetLeftToesWorldX),
                F(RootSpike.SetHumanPosePreSolveTargetLeftToesWorldZ),
                F(RootSpike.SetHumanPosePreSolveGhostRightFootWorldX),
                F(RootSpike.SetHumanPosePreSolveGhostRightFootWorldZ),
                F(RootSpike.SetHumanPosePreSolveGhostRightToesWorldX),
                F(RootSpike.SetHumanPosePreSolveGhostRightToesWorldZ),
                F(RootSpike.SetHumanPosePreSolveCurrentRightFootWorldX),
                F(RootSpike.SetHumanPosePreSolveCurrentRightFootWorldZ),
                F(RootSpike.SetHumanPosePreSolveCurrentRightToesWorldX),
                F(RootSpike.SetHumanPosePreSolveCurrentRightToesWorldZ),
                F(RootSpike.SetHumanPosePreSolveTargetRightFootWorldX),
                F(RootSpike.SetHumanPosePreSolveTargetRightFootWorldZ),
                F(RootSpike.SetHumanPosePreSolveTargetRightToesWorldX),
                F(RootSpike.SetHumanPosePreSolveTargetRightToesWorldZ),
                F(RootSpike.SetHumanPoseInputSpineFrontBackMuscle),
                F(RootSpike.SetHumanPoseInputSpineLeftRightMuscle),
                F(RootSpike.SetHumanPoseInputSpineTwistLeftRightMuscle),
                F(RootSpike.SetHumanPoseInputChestFrontBackMuscle),
                F(RootSpike.SetHumanPoseInputChestLeftRightMuscle),
                F(RootSpike.SetHumanPoseInputChestTwistLeftRightMuscle),
                F(RootSpike.SetHumanPoseInputUpperChestFrontBackMuscle),
                F(RootSpike.SetHumanPoseInputUpperChestLeftRightMuscle),
                F(RootSpike.SetHumanPoseInputUpperChestTwistLeftRightMuscle),
                F(RootSpike.SetHumanPoseInputLeftUpperLegInOutMuscle),
                F(RootSpike.SetHumanPoseInputRightUpperLegInOutMuscle),
                F(RootSpike.SetHumanPoseInputLeftUpperLegTwistInOutMuscle),
                F(RootSpike.SetHumanPoseInputRightUpperLegTwistInOutMuscle),
                F(RootSpike.SetHumanPoseInputLeftLowerLegTwistInOutMuscle),
                F(RootSpike.SetHumanPoseInputRightLowerLegTwistInOutMuscle),
                F(RootSpike.SetHumanPoseInputLeftFootTwistInOutMuscle),
                F(RootSpike.SetHumanPoseInputRightFootTwistInOutMuscle),
                F(RootSpike.SetHumanPoseInputLeftToesUpDownMuscle),
                F(RootSpike.SetHumanPoseInputRightToesUpDownMuscle),
                F(RootSpike.SetHumanPoseOutputRightUpperLegInOutMuscle),
                F(RootSpike.SetHumanPoseRightUpperLegInOutDelta),
                F(RootSpike.SetHumanPoseOutputRightUpperLegTwistInOutMuscle),
                F(RootSpike.SetHumanPoseRightUpperLegTwistInOutDelta),
                F(RootSpike.SetHumanPoseOutputRightLowerLegTwistInOutMuscle),
                F(RootSpike.SetHumanPoseRightLowerLegTwistInOutDelta),
                F(RootSpike.SetHumanPoseOutputRightFootTwistInOutMuscle),
                F(RootSpike.SetHumanPoseRightFootTwistInOutDelta),
                F(RootSpike.SetHumanPoseOutputRightToesUpDownMuscle),
                F(RootSpike.SetHumanPoseRightToesUpDownDelta));
        }

        private static float FiniteXzDelta(float beforeX, float beforeZ, float afterX, float afterZ, string axis)
        {
            if (!IsFinite(beforeX) || !IsFinite(beforeZ) || !IsFinite(afterX) || !IsFinite(afterZ))
            {
                return float.NaN;
            }

            return axis == "z" ? afterZ - beforeZ : afterX - beforeX;
        }

        private static float FiniteDelta(float before, float after)
        {
            return IsFinite(before) && IsFinite(after) ? after - before : float.NaN;
        }

        private static float FiniteXzDeltaMagnitude(float beforeX, float beforeZ, float afterX, float afterZ)
        {
            float deltaX = FiniteXzDelta(beforeX, beforeZ, afterX, afterZ, axis: "x");
            float deltaZ = FiniteXzDelta(beforeX, beforeZ, afterX, afterZ, axis: "z");
            return IsFinite(deltaX) && IsFinite(deltaZ)
                ? Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ)
                : float.NaN;
        }

        private static float FiniteXzResponseRatio(
            float beforeX,
            float beforeZ,
            float afterX,
            float afterZ,
            string endpointAxis,
            float bodyPositionDelta)
        {
            float endpointDelta = FiniteXzDelta(beforeX, beforeZ, afterX, afterZ, endpointAxis);
            if (!IsFinite(endpointDelta) ||
                !IsFinite(bodyPositionDelta) ||
                Mathf.Abs(bodyPositionDelta) <= 0.000001f)
            {
                return float.NaN;
            }

            return endpointDelta / bodyPositionDelta;
        }

        private static string F(float value)
        {
            return MotionComparisonProbeReportWriter.FormatMetricsCsvFloat(value);
        }

        private static string I(int value)
        {
            return MotionComparisonProbeReportWriter.FormatMetricsCsvInt(value);
        }

        private static string V(Vector3 value)
        {
            return MotionComparisonProbeReportWriter.FormatMetricsCsvVector(value);
        }

        private static string Escape(string value)
        {
            return MotionComparisonProbeReportWriter.FormatMetricsCsvText(value);
        }
    }
}
