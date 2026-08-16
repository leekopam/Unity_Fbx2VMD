using System;

namespace Fbx2Vmd.FBXImporter
{
    public sealed class RetargetingSettings
    {
        public bool ShouldUseLegacyPoseSpaceFacingCorrection { get; }
        public float HeightOffset { get; }
        public float MovementScaleMultiplier { get; }
        public bool ShouldPreserveFbxRootRotation { get; }
        public bool ShouldPreserveRetargetBodyPosition { get; }
        public bool ShouldUseRetargetBodyPositionXZRootMotion { get; }
        public bool ShouldUseEditorHumanoidRootTranslationReference { get; }
        public float editorHumanoidRootTranslationWeight { get; }
        public float editorHumanoidRootTranslationCurrentWeight { get; }
        public bool ShouldStabilizeGroundedFootXZ { get; }
        public float GroundedFootLockWeight { get; }
        public float MaxGroundedFootLockStep { get; }
        public bool clampRetargetMusclesToHumanRange { get; }
        public bool enableAnatomicalArmGuard { get; }
        public float ArmStretchMuscleLimit { get; }
        public bool clampRetargetArmStretchMuscles { get; }
        public float UpperArmTwistMuscleLimit { get; }
        public float LowerArmTwistMuscleLimit { get; }
        public bool enableThumbAnatomicalGuard { get; }
        public float ThumbStretchMin { get; }
        public float ThumbStretchMax { get; }
        public float EffectiveThumbStretchOffset { get; }
        public bool preserveManualFingerReferenceThumbMuscles { get; }
        public bool ShouldUseManualAnimatorFullBodyPoseReference { get; }
        public float manualAnimatorFullBodyPoseReferenceWeight { get; }
        public bool ShouldExcludeManualAnimatorFullBodyLowerMuscles { get; }
        public bool ShouldApplyManualAnimatorFullBodyLowerMusclesOnly { get; }
        public bool ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly { get; }
        public bool manualAnimatorFullBodyPoseRightArmMusclesOnly { get; }
        public bool manualAnimatorFullBodyPoseLeftArmMusclesOnly { get; }
        public bool manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly { get; }
        public float manualAnimatorFullBodyPoseFrameGateStart { get; }
        public float manualAnimatorFullBodyPoseFrameGateEnd { get; }
        public bool ShouldUseSetHumanPoseRightLegTwistOutputReference { get; }
        public float setHumanPoseRightLegTwistOutputReferenceWeight { get; }
        public float setHumanPoseRightLegTwistOutputReferenceMaxDelta { get; }
        public bool useManualAnimatorThumbLocalRotationReference { get; }
        public bool useManualAnimatorHandLocalRotationReference { get; }
        public bool useManualAnimatorThumbSegmentDirectionReference { get; }
        public float manualAnimatorThumbSegmentDirectionWeight { get; }
        public bool useManualAnimatorThumbHandDirectionReference { get; }
        public float manualAnimatorThumbHandDirectionWeight { get; }
        public bool useManualAnimatorHandPalmFrameReference { get; }
        public float manualAnimatorHandPalmFrameWeight { get; }
        public bool useManualAnimatorThumbBasePositionReference { get; }
        public bool ShouldUseManualAnimatorHipsLocalPositionReference { get; }
        public bool ShouldUseManualAnimatorBodyRotationReference { get; }
        public float manualAnimatorBodyRotationReferenceWeight { get; }
        public bool ShouldUseManualAnimatorBodyPositionYReference { get; }
        public bool ShouldUseManualAnimatorBodyPositionXzReference { get; }
        public float manualAnimatorBodyPositionXzReferenceWeight { get; }
        public float manualAnimatorBodyPositionXzReferenceMaxOffset { get; }
        public float manualAnimatorBodyPositionXzReferenceFrameGateStart { get; }
        public float manualAnimatorBodyPositionXzReferenceFrameGateEnd { get; }
        public float manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames { get; }
        public float manualAnimatorBodyPositionXzReferenceAxisXScale { get; }
        public float manualAnimatorBodyPositionXzReferenceAxisZScale { get; }
        public float manualAnimatorHipsLocalPositionWeight { get; }
        public float manualAnimatorHipsLocalPositionMaxOffset { get; }
        public bool ShouldUseManualAnimatorFootHeightGroundingReference { get; }
        public float manualAnimatorFootHeightGroundingReferenceWeight { get; }
        public float manualAnimatorFootHeightGroundingReferenceMaxLift { get; }
        public bool ShouldUseManualAnimatorFootLocalRotationReference { get; }
        public float manualAnimatorFootLocalRotationReferenceWeight { get; }
        public bool ShouldUseManualAnimatorLowerBodySegmentDirectionReference { get; }
        public float manualAnimatorLowerBodySegmentDirectionReferenceWeight { get; }
        public float manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle { get; }
        public bool ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference { get; }
        public float manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle { get; }
        public bool ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference { get; }
        public float manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle { get; }
        public float manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle { get; }
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle { get; }
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale { get; }
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight { get; }
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart { get; }
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd { get; }
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight { get; }
        public bool ShouldDisableManualAnimatorFootToToesSegmentDirectionReference { get; }
        public float manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle { get; }
        public bool ShouldUseManualAnimatorFootHipsAlignedResidualYawReference { get; }
        public float manualAnimatorFootHipsAlignedResidualYawReferenceWeight { get; }
        public float manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle { get; }
        public bool useManualAnimatorBipedIkFootPositionReference { get; }
        public float manualAnimatorBipedIkFootPositionReferenceWeight { get; }
        public float manualAnimatorBipedIkFootPositionReferenceMaxOffset { get; }
        public bool usePostSetHumanPoseRightEndpointPositionReference { get; }
        public float postSetHumanPoseRightEndpointPositionReferenceWeight { get; }
        public float postSetHumanPoseRightEndpointPositionReferenceMaxOffset { get; }
        public float postSetHumanPoseRightEndpointPositionReferencePositiveZScale { get; }
        public float postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight { get; }
        public float postSetHumanPoseRightEndpointPositionReferenceFrameGateStart { get; }
        public float postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd { get; }
        public bool ShouldUseLeftSideForPostSetHumanPoseEndpointPosition { get; }
        public bool usePostSetHumanPoseRightFootEvaluatorXzReference { get; }
        public float postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude { get; }
        public bool usePreSetHumanPoseRightEndpointPositionReference { get; }
        public float preSetHumanPoseRightEndpointPositionReferenceWeight { get; }
        public float preSetHumanPoseRightEndpointPositionReferenceMaxOffset { get; }
        public float preSetHumanPoseRightEndpointPositionReferencePositiveZScale { get; }
        public float preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight { get; }
        public float preSetHumanPoseRightEndpointPositionReferenceFrameGateStart { get; }
        public float preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd { get; }
        public bool ShouldUseLeftSideForPreSetHumanPoseEndpointPosition { get; }
        public bool preSetHumanPoseEndpointPositionUseGhostCurrentBasis { get; }
        public bool ShouldInvertPreSetHumanPoseEndpointPositionBodyX { get; }
        public bool ShouldInvertPreSetHumanPoseEndpointPositionBodyZ { get; }
        public float manualAnimatorThumbBasePositionWeight { get; }
        public float manualAnimatorThumbBasePositionMaxOffset { get; }
        public float ThumbSpreadMin { get; }
        public float ThumbSpreadMax { get; }
        public bool logThumbAnatomicalGuardCorrections { get; }
        public bool EffectiveThumbLocalRotationGuard { get; }
        public float EffectiveThumbProximalMaxLocalAngle { get; }
        public float ThumbIntermediateMaxLocalAngle { get; }
        public float ThumbDistalMaxLocalAngle { get; }
        public bool logThumbLocalRotationGuardCorrections { get; }
        public bool clampRetargetRootDeltaSpikes { get; }
        public float MaxRetargetRootDeltaPerFrame { get; }
        public bool logRetargetRootDeltaSpikes { get; }
        public bool clampRetargetHipsLocalPositionSpikes { get; }
        public float MaxRetargetHipsLocalPositionDeltaPerFrame { get; }
        public bool smoothRetargetGrounding { get; }
        public float MaxGroundingVerticalStepPerFrame { get; }
        public float GroundingSmoothing { get; }
        public float GroundingDeadZone { get; }
        public bool FreezeRootYAfterInitialGrounding { get; }
        public bool clampRetargetVisualClipStep { get; }
        public float RetargetVisualClipFrameRate { get; }
        public bool smoothRetargetPoseOnVisualStepSpike { get; }
        public float RetargetPoseVisualSpikeCurrentWeight { get; }
        public float RetargetPoseVisualSpikeForearmStretchClampMaxOffset { get; }
        public float RetargetPoseVisualMuscleDeltaThreshold { get; }
        public bool rejectRendererGroundingOutliers { get; }
        public float MaxRendererFootGroundingSeparation { get; }
        public bool smoothLateVisualGroundingCorrection { get; }
        public float LateVisualGroundingSnapThreshold { get; }
        public float LateVisualGroundingSmoothing { get; }
        public float MaxLateVisualGroundingStepPerFrame { get; }
        public bool ShouldLockTargetHumanoidBonePositions { get; }

        private RetargetingSettings(FBXVmdPipeline pipeline)
        {
            ShouldUseLegacyPoseSpaceFacingCorrection = pipeline.ShouldUseLegacyPoseSpaceFacingCorrection;
            HeightOffset = pipeline.HeightOffset;
            MovementScaleMultiplier = pipeline.MovementScaleMultiplier;
            ShouldPreserveFbxRootRotation = pipeline.ShouldPreserveFbxRootRotation;
            ShouldPreserveRetargetBodyPosition = pipeline.ShouldPreserveRetargetBodyPosition;
            ShouldUseRetargetBodyPositionXZRootMotion = pipeline.ShouldUseRetargetBodyPositionXZRootMotion;
            ShouldUseEditorHumanoidRootTranslationReference = pipeline.ShouldUseEditorHumanoidRootTranslationReference;
            editorHumanoidRootTranslationWeight = pipeline.editorHumanoidRootTranslationWeight;
            editorHumanoidRootTranslationCurrentWeight = pipeline.editorHumanoidRootTranslationCurrentWeight;
            ShouldStabilizeGroundedFootXZ = pipeline.ShouldStabilizeGroundedFootXZ;
            GroundedFootLockWeight = pipeline.GroundedFootLockWeight;
            MaxGroundedFootLockStep = pipeline.MaxGroundedFootLockStep;
            clampRetargetMusclesToHumanRange = pipeline.clampRetargetMusclesToHumanRange;
            enableAnatomicalArmGuard = pipeline.enableAnatomicalArmGuard;
            ArmStretchMuscleLimit = pipeline.ArmStretchMuscleLimit;
            clampRetargetArmStretchMuscles = pipeline.clampRetargetArmStretchMuscles;
            UpperArmTwistMuscleLimit = pipeline.UpperArmTwistMuscleLimit;
            LowerArmTwistMuscleLimit = pipeline.LowerArmTwistMuscleLimit;
            enableThumbAnatomicalGuard = pipeline.enableThumbAnatomicalGuard;
            ThumbStretchMin = pipeline.ThumbStretchMin;
            ThumbStretchMax = pipeline.ThumbStretchMax;
            EffectiveThumbStretchOffset = pipeline.EffectiveThumbStretchOffset;
            preserveManualFingerReferenceThumbMuscles = pipeline.preserveManualFingerReferenceThumbMuscles;
            ShouldUseManualAnimatorFullBodyPoseReference = pipeline.ShouldUseManualAnimatorFullBodyPoseReference;
            manualAnimatorFullBodyPoseReferenceWeight = pipeline.manualAnimatorFullBodyPoseReferenceWeight;
            ShouldExcludeManualAnimatorFullBodyLowerMuscles = pipeline.ShouldExcludeManualAnimatorFullBodyLowerMuscles;
            ShouldApplyManualAnimatorFullBodyLowerMusclesOnly = pipeline.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly;
            ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly = pipeline.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly;
            manualAnimatorFullBodyPoseRightArmMusclesOnly = pipeline.manualAnimatorFullBodyPoseRightArmMusclesOnly;
            manualAnimatorFullBodyPoseLeftArmMusclesOnly = pipeline.manualAnimatorFullBodyPoseLeftArmMusclesOnly;
            manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly = pipeline.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly;
            manualAnimatorFullBodyPoseFrameGateStart = pipeline.manualAnimatorFullBodyPoseFrameGateStart;
            manualAnimatorFullBodyPoseFrameGateEnd = pipeline.manualAnimatorFullBodyPoseFrameGateEnd;
            ShouldUseSetHumanPoseRightLegTwistOutputReference = pipeline.ShouldUseSetHumanPoseRightLegTwistOutputReference;
            setHumanPoseRightLegTwistOutputReferenceWeight = pipeline.setHumanPoseRightLegTwistOutputReferenceWeight;
            setHumanPoseRightLegTwistOutputReferenceMaxDelta = pipeline.setHumanPoseRightLegTwistOutputReferenceMaxDelta;
            useManualAnimatorThumbLocalRotationReference = pipeline.useManualAnimatorThumbLocalRotationReference;
            useManualAnimatorHandLocalRotationReference = pipeline.useManualAnimatorHandLocalRotationReference;
            useManualAnimatorThumbSegmentDirectionReference = pipeline.useManualAnimatorThumbSegmentDirectionReference;
            manualAnimatorThumbSegmentDirectionWeight = pipeline.manualAnimatorThumbSegmentDirectionWeight;
            useManualAnimatorThumbHandDirectionReference = pipeline.useManualAnimatorThumbHandDirectionReference;
            manualAnimatorThumbHandDirectionWeight = pipeline.manualAnimatorThumbHandDirectionWeight;
            useManualAnimatorHandPalmFrameReference = pipeline.useManualAnimatorHandPalmFrameReference;
            manualAnimatorHandPalmFrameWeight = pipeline.manualAnimatorHandPalmFrameWeight;
            useManualAnimatorThumbBasePositionReference = pipeline.useManualAnimatorThumbBasePositionReference;
            ShouldUseManualAnimatorHipsLocalPositionReference = pipeline.ShouldUseManualAnimatorHipsLocalPositionReference;
            ShouldUseManualAnimatorBodyRotationReference = pipeline.ShouldUseManualAnimatorBodyRotationReference;
            manualAnimatorBodyRotationReferenceWeight = pipeline.manualAnimatorBodyRotationReferenceWeight;
            ShouldUseManualAnimatorBodyPositionYReference = pipeline.ShouldUseManualAnimatorBodyPositionYReference;
            ShouldUseManualAnimatorBodyPositionXzReference = pipeline.ShouldUseManualAnimatorBodyPositionXzReference;
            manualAnimatorBodyPositionXzReferenceWeight = pipeline.manualAnimatorBodyPositionXzReferenceWeight;
            manualAnimatorBodyPositionXzReferenceMaxOffset = pipeline.manualAnimatorBodyPositionXzReferenceMaxOffset;
            manualAnimatorBodyPositionXzReferenceFrameGateStart = pipeline.manualAnimatorBodyPositionXzReferenceFrameGateStart;
            manualAnimatorBodyPositionXzReferenceFrameGateEnd = pipeline.manualAnimatorBodyPositionXzReferenceFrameGateEnd;
            manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = pipeline.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
            manualAnimatorBodyPositionXzReferenceAxisXScale = pipeline.manualAnimatorBodyPositionXzReferenceAxisXScale;
            manualAnimatorBodyPositionXzReferenceAxisZScale = pipeline.manualAnimatorBodyPositionXzReferenceAxisZScale;
            manualAnimatorHipsLocalPositionWeight = pipeline.manualAnimatorHipsLocalPositionWeight;
            manualAnimatorHipsLocalPositionMaxOffset = pipeline.manualAnimatorHipsLocalPositionMaxOffset;
            ShouldUseManualAnimatorFootHeightGroundingReference = pipeline.ShouldUseManualAnimatorFootHeightGroundingReference;
            manualAnimatorFootHeightGroundingReferenceWeight = pipeline.manualAnimatorFootHeightGroundingReferenceWeight;
            manualAnimatorFootHeightGroundingReferenceMaxLift = pipeline.manualAnimatorFootHeightGroundingReferenceMaxLift;
            ShouldUseManualAnimatorFootLocalRotationReference = pipeline.ShouldUseManualAnimatorFootLocalRotationReference;
            manualAnimatorFootLocalRotationReferenceWeight = pipeline.manualAnimatorFootLocalRotationReferenceWeight;
            ShouldUseManualAnimatorLowerBodySegmentDirectionReference = pipeline.ShouldUseManualAnimatorLowerBodySegmentDirectionReference;
            manualAnimatorLowerBodySegmentDirectionReferenceWeight = pipeline.manualAnimatorLowerBodySegmentDirectionReferenceWeight;
            manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = pipeline.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference = pipeline.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference;
            manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = pipeline.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference = pipeline.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference;
            manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = pipeline.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = pipeline.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            ShouldDisableManualAnimatorFootToToesSegmentDirectionReference = pipeline.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference;
            manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = pipeline.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            ShouldUseManualAnimatorFootHipsAlignedResidualYawReference = pipeline.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference;
            manualAnimatorFootHipsAlignedResidualYawReferenceWeight = pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = pipeline.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            useManualAnimatorBipedIkFootPositionReference = pipeline.useManualAnimatorBipedIkFootPositionReference;
            manualAnimatorBipedIkFootPositionReferenceWeight = pipeline.manualAnimatorBipedIkFootPositionReferenceWeight;
            manualAnimatorBipedIkFootPositionReferenceMaxOffset = pipeline.manualAnimatorBipedIkFootPositionReferenceMaxOffset;
            usePostSetHumanPoseRightEndpointPositionReference = pipeline.usePostSetHumanPoseRightEndpointPositionReference;
            postSetHumanPoseRightEndpointPositionReferenceWeight = pipeline.postSetHumanPoseRightEndpointPositionReferenceWeight;
            postSetHumanPoseRightEndpointPositionReferenceMaxOffset = pipeline.postSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            postSetHumanPoseRightEndpointPositionReferencePositiveZScale = pipeline.postSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = pipeline.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = pipeline.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = pipeline.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            ShouldUseLeftSideForPostSetHumanPoseEndpointPosition = pipeline.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition;
            usePostSetHumanPoseRightFootEvaluatorXzReference = pipeline.usePostSetHumanPoseRightFootEvaluatorXzReference;
            postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = pipeline.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            usePreSetHumanPoseRightEndpointPositionReference = pipeline.usePreSetHumanPoseRightEndpointPositionReference;
            preSetHumanPoseRightEndpointPositionReferenceWeight = pipeline.preSetHumanPoseRightEndpointPositionReferenceWeight;
            preSetHumanPoseRightEndpointPositionReferenceMaxOffset = pipeline.preSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            preSetHumanPoseRightEndpointPositionReferencePositiveZScale = pipeline.preSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = pipeline.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = pipeline.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = pipeline.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            ShouldUseLeftSideForPreSetHumanPoseEndpointPosition = pipeline.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
            preSetHumanPoseEndpointPositionUseGhostCurrentBasis = pipeline.preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            ShouldInvertPreSetHumanPoseEndpointPositionBodyX = pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyX;
            ShouldInvertPreSetHumanPoseEndpointPositionBodyZ = pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ;
            manualAnimatorThumbBasePositionWeight = pipeline.manualAnimatorThumbBasePositionWeight;
            manualAnimatorThumbBasePositionMaxOffset = pipeline.manualAnimatorThumbBasePositionMaxOffset;
            ThumbSpreadMin = pipeline.ThumbSpreadMin;
            ThumbSpreadMax = pipeline.ThumbSpreadMax;
            logThumbAnatomicalGuardCorrections = pipeline.logThumbAnatomicalGuardCorrections;
            EffectiveThumbLocalRotationGuard = pipeline.EffectiveThumbLocalRotationGuard;
            EffectiveThumbProximalMaxLocalAngle = pipeline.EffectiveThumbProximalMaxLocalAngle;
            ThumbIntermediateMaxLocalAngle = pipeline.ThumbIntermediateMaxLocalAngle;
            ThumbDistalMaxLocalAngle = pipeline.ThumbDistalMaxLocalAngle;
            logThumbLocalRotationGuardCorrections = pipeline.logThumbLocalRotationGuardCorrections;
            clampRetargetRootDeltaSpikes = pipeline.clampRetargetRootDeltaSpikes;
            MaxRetargetRootDeltaPerFrame = pipeline.MaxRetargetRootDeltaPerFrame;
            logRetargetRootDeltaSpikes = pipeline.logRetargetRootDeltaSpikes;
            clampRetargetHipsLocalPositionSpikes = pipeline.clampRetargetHipsLocalPositionSpikes;
            MaxRetargetHipsLocalPositionDeltaPerFrame = pipeline.MaxRetargetHipsLocalPositionDeltaPerFrame;
            smoothRetargetGrounding = pipeline.smoothRetargetGrounding;
            MaxGroundingVerticalStepPerFrame = pipeline.MaxGroundingVerticalStepPerFrame;
            GroundingSmoothing = pipeline.GroundingSmoothing;
            GroundingDeadZone = pipeline.GroundingDeadZone;
            FreezeRootYAfterInitialGrounding = pipeline.FreezeRootYAfterInitialGrounding;
            clampRetargetVisualClipStep = pipeline.clampRetargetVisualClipStep;
            RetargetVisualClipFrameRate = pipeline.RetargetVisualClipFrameRate;
            smoothRetargetPoseOnVisualStepSpike = pipeline.smoothRetargetPoseOnVisualStepSpike;
            RetargetPoseVisualSpikeCurrentWeight = pipeline.RetargetPoseVisualSpikeCurrentWeight;
            RetargetPoseVisualSpikeForearmStretchClampMaxOffset = pipeline.RetargetPoseVisualSpikeForearmStretchClampMaxOffset;
            RetargetPoseVisualMuscleDeltaThreshold = pipeline.RetargetPoseVisualMuscleDeltaThreshold;
            rejectRendererGroundingOutliers = pipeline.rejectRendererGroundingOutliers;
            MaxRendererFootGroundingSeparation = pipeline.MaxRendererFootGroundingSeparation;
            smoothLateVisualGroundingCorrection = pipeline.smoothLateVisualGroundingCorrection;
            LateVisualGroundingSnapThreshold = pipeline.LateVisualGroundingSnapThreshold;
            LateVisualGroundingSmoothing = pipeline.LateVisualGroundingSmoothing;
            MaxLateVisualGroundingStepPerFrame = pipeline.MaxLateVisualGroundingStepPerFrame;
            ShouldLockTargetHumanoidBonePositions = pipeline.ShouldLockTargetHumanoidBonePositions;
        }

        public static RetargetingSettings CreateSnapshot(FBXVmdPipeline pipeline)
        {
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));
            return new RetargetingSettings(pipeline);
        }
    }
}
