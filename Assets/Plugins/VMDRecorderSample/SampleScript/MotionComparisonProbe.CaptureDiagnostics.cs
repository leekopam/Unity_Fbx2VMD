using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class MotionComparisonProbe
{
    private PoseMetrics CaptureMetrics(string reason)
    {
        Transform root = _animator.transform;
        Transform hips = GetBone(HumanBodyBones.Hips);
        Transform leftFoot = GetBone(HumanBodyBones.LeftFoot);
        Transform rightFoot = GetBone(HumanBodyBones.RightFoot);
        Vector3 leftFootPosition = leftFoot != null ? leftFoot.position : EmptyVector();
        Vector3 rightFootPosition = rightFoot != null ? rightFoot.position : EmptyVector();

        float lowestFootY = float.NaN;
        if (leftFoot != null && rightFoot != null)
        {
            lowestFootY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
        }
        else if (leftFoot != null)
        {
            lowestFootY = leftFoot.position.y;
        }
        else if (rightFoot != null)
        {
            lowestFootY = rightFoot.position.y;
        }

        ArmMuscleMetrics armMuscles = CaptureArmMuscles();
        FingerMetrics fingers = CaptureFingerMetrics();
        AnimationTimeMetrics animationTime = CaptureAnimationTimeMetrics();
        RootSpikeMetrics rootSpikeMetrics = CaptureRootSpikeMetrics();
        float lowestFootBottomY = float.IsNaN(lowestFootY) ? float.NaN : lowestFootY - DiagnosticFootRadius;
        float groundY = float.IsNaN(rootSpikeMetrics.LastGroundingTargetY) ? 0f : rootSpikeMetrics.LastGroundingTargetY;
        float bodyPositionY = CaptureBodyPositionY();
        float hipsLocalY = hips != null ? hips.localPosition.y : float.NaN;
        Vector3 hipsPosition = hips != null ? hips.position : EmptyVector();
        float meshBoundsMinY = float.NaN;
        float meshBoundsMaxY = float.NaN;
        if (TryGetRendererBounds(out Bounds rendererBounds))
        {
            meshBoundsMinY = rendererBounds.min.y;
            meshBoundsMaxY = rendererBounds.max.y;
        }

        ThumbGuardDiagnostics thumbGuardDiagnostics = CaptureThumbGuardDiagnostics();
        ArmSwingGuardDiagnostics armSwingGuardDiagnostics = CaptureArmSwingGuardDiagnostics();
        YybDiagnosticMetrics yybDiagnostics = captureYybDiagnosticOnlyMetrics
            ? CaptureYybDiagnosticMetrics(armMuscles)
            : YybDiagnosticMetrics.Empty;
        HandTorsoClearanceMetrics handTorsoClearance = CaptureHandTorsoClearanceMetrics(root);

        return new PoseMetrics
        {
            Label = comparisonLabel,
            Scene = SceneManager.GetActiveScene().name,
            Reason = reason,
            Elapsed = Time.time - _startTime,
            TimeSinceLevelLoad = Time.timeSinceLevelLoad,
            FrameCount = Time.frameCount,
            RecorderFrame = _recorder != null ? _recorder.FrameNumber : -1,
            AnimationTimeSource = animationTime.Source,
            AnimationClipName = animationTime.ClipName,
            AnimationClipTime = animationTime.ClipTime,
            AnimationClipLength = animationTime.ClipLength,
            AnimationNormalizedTime = animationTime.NormalizedTime,
            RootPosition = root.position,
            RootYaw = root.eulerAngles.y,
            RootSpike = rootSpikeMetrics,
            BodyPositionY = bodyPositionY,
            HipsLocalY = hipsLocalY,
            HipsPosition = hipsPosition,
            HipsY = hips != null ? hips.position.y : float.NaN,
            LowestFootY = lowestFootY,
            LowestFootBottomY = lowestFootBottomY,
            LeftFootPosition = leftFootPosition,
            RightFootPosition = rightFootPosition,
            MeshBoundsMinY = meshBoundsMinY,
            MeshBoundsMaxY = meshBoundsMaxY,
            FootBottomGroundGap = float.IsNaN(lowestFootBottomY) ? float.NaN : lowestFootBottomY - groundY,
            MeshBoundsGroundGap = float.IsNaN(meshBoundsMinY) ? float.NaN : meshBoundsMinY - groundY,
            CameraFacingDot = CalculateCameraFacingDot(root),
            MaxScaleDelta = CalculateMaxScaleDelta(),
            LeftUpperArmScale = GetLocalScale(HumanBodyBones.LeftUpperArm),
            RightUpperArmScale = GetLocalScale(HumanBodyBones.RightUpperArm),
            LeftUpperLegScale = GetLocalScale(HumanBodyBones.LeftUpperLeg),
            RightUpperLegScale = GetLocalScale(HumanBodyBones.RightUpperLeg),
            SpineLocalEuler = GetLocalEuler(HumanBodyBones.Spine),
            ChestLocalEuler = GetLocalEuler(HumanBodyBones.Chest),
            UpperChestLocalEuler = GetLocalEuler(HumanBodyBones.UpperChest),
            LeftShoulderLocalEuler = GetLocalEuler(HumanBodyBones.LeftShoulder),
            RightShoulderLocalEuler = GetLocalEuler(HumanBodyBones.RightShoulder),
            LeftUpperArmLocalEuler = GetLocalEuler(HumanBodyBones.LeftUpperArm),
            RightUpperArmLocalEuler = GetLocalEuler(HumanBodyBones.RightUpperArm),
            LeftLowerArmLocalEuler = GetLocalEuler(HumanBodyBones.LeftLowerArm),
            RightLowerArmLocalEuler = GetLocalEuler(HumanBodyBones.RightLowerArm),
            LeftHandLocalEuler = GetLocalEuler(HumanBodyBones.LeftHand),
            RightHandLocalEuler = GetLocalEuler(HumanBodyBones.RightHand),
            LeftThumbProximalLocalEuler = GetLocalEuler(HumanBodyBones.LeftThumbProximal),
            LeftIndexProximalLocalEuler = GetLocalEuler(HumanBodyBones.LeftIndexProximal),
            LeftMiddleProximalLocalEuler = GetLocalEuler(HumanBodyBones.LeftMiddleProximal),
            LeftRingProximalLocalEuler = GetLocalEuler(HumanBodyBones.LeftRingProximal),
            LeftLittleProximalLocalEuler = GetLocalEuler(HumanBodyBones.LeftLittleProximal),
            RightThumbProximalLocalEuler = GetLocalEuler(HumanBodyBones.RightThumbProximal),
            RightIndexProximalLocalEuler = GetLocalEuler(HumanBodyBones.RightIndexProximal),
            RightMiddleProximalLocalEuler = GetLocalEuler(HumanBodyBones.RightMiddleProximal),
            RightRingProximalLocalEuler = GetLocalEuler(HumanBodyBones.RightRingProximal),
            RightLittleProximalLocalEuler = GetLocalEuler(HumanBodyBones.RightLittleProximal),
            LeftArmLength = CalculateChainLength(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            RightArmLength = CalculateChainLength(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            LeftLegLength = CalculateChainLength(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            RightLegLength = CalculateChainLength(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            LeftElbowAngle = CalculateJointAngle(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            RightElbowAngle = CalculateJointAngle(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            LeftKneeAngle = CalculateJointAngle(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            RightKneeAngle = CalculateJointAngle(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            LeftElbowBendForward = CalculateBendForwardDot(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, root),
            RightElbowBendForward = CalculateBendForwardDot(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, root),
            LeftKneeBendForward = CalculateBendForwardDot(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, root),
            RightKneeBendForward = CalculateBendForwardDot(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, root),
            LeftElbowBendOffsetForward = CalculateBendOffsetForwardDot(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, root),
            RightElbowBendOffsetForward = CalculateBendOffsetForwardDot(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, root),
            LeftKneeBendOffsetForward = CalculateBendOffsetForwardDot(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, root),
            RightKneeBendOffsetForward = CalculateBendOffsetForwardDot(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, root),
            LeftUpperArmDownDot = CalculateUpperArmDownDot(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, root),
            RightUpperArmDownDot = CalculateUpperArmDownDot(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, root),
            LeftHandHorizontalRatio = CalculateHandHorizontalRatio(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, root),
            RightHandHorizontalRatio = CalculateHandHorizontalRatio(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, root),
            LeftHandBelowShoulderRatio = CalculateHandBelowShoulderRatio(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, root),
            RightHandBelowShoulderRatio = CalculateHandBelowShoulderRatio(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, root),
            LeftHandTorsoSignedClearance = handTorsoClearance.LeftSignedClearance,
            RightHandTorsoSignedClearance = handTorsoClearance.RightSignedClearance,
            MinHandTorsoSignedClearance = handTorsoClearance.MinSignedClearance,
            HandTorsoPenetrationRisk = handTorsoClearance.PenetrationRisk,
            LeftShoulderDownUpMuscle = armMuscles.LeftShoulderDownUp,
            LeftShoulderFrontBackMuscle = armMuscles.LeftShoulderFrontBack,
            LeftArmDownUpMuscle = armMuscles.LeftArmDownUp,
            LeftArmFrontBackMuscle = armMuscles.LeftArmFrontBack,
            LeftArmTwistMuscle = armMuscles.LeftArmTwist,
            LeftForearmStretchMuscle = armMuscles.LeftForearmStretch,
            LeftForearmTwistMuscle = armMuscles.LeftForearmTwist,
            RightShoulderDownUpMuscle = armMuscles.RightShoulderDownUp,
            RightShoulderFrontBackMuscle = armMuscles.RightShoulderFrontBack,
            RightArmDownUpMuscle = armMuscles.RightArmDownUp,
            RightArmFrontBackMuscle = armMuscles.RightArmFrontBack,
            RightArmTwistMuscle = armMuscles.RightArmTwist,
            RightForearmStretchMuscle = armMuscles.RightForearmStretch,
            RightForearmTwistMuscle = armMuscles.RightForearmTwist,
            ArmSwingGuard = armSwingGuardDiagnostics,
            LeftThumb1StretchMuscle = fingers.LeftThumb1Stretch,
            LeftThumbSpreadMuscle = fingers.LeftThumbSpread,
            LeftIndex1StretchMuscle = fingers.LeftIndex1Stretch,
            LeftIndexSpreadMuscle = fingers.LeftIndexSpread,
            LeftMiddle1StretchMuscle = fingers.LeftMiddle1Stretch,
            LeftMiddleSpreadMuscle = fingers.LeftMiddleSpread,
            LeftRing1StretchMuscle = fingers.LeftRing1Stretch,
            LeftRingSpreadMuscle = fingers.LeftRingSpread,
            LeftLittle1StretchMuscle = fingers.LeftLittle1Stretch,
            LeftLittleSpreadMuscle = fingers.LeftLittleSpread,
            RightThumb1StretchMuscle = fingers.RightThumb1Stretch,
            RightThumbSpreadMuscle = fingers.RightThumbSpread,
            RightIndex1StretchMuscle = fingers.RightIndex1Stretch,
            RightIndexSpreadMuscle = fingers.RightIndexSpread,
            RightMiddle1StretchMuscle = fingers.RightMiddle1Stretch,
            RightMiddleSpreadMuscle = fingers.RightMiddleSpread,
            RightRing1StretchMuscle = fingers.RightRing1Stretch,
            RightRingSpreadMuscle = fingers.RightRingSpread,
            RightLittle1StretchMuscle = fingers.RightLittle1Stretch,
            RightLittleSpreadMuscle = fingers.RightLittleSpread,
            ThumbGuard = thumbGuardDiagnostics,
            YybDiagnostics = yybDiagnostics
        };
    }

    private float CaptureBodyPositionY()
    {
        if (_animator == null || _animator.avatar == null || !_animator.avatar.isHuman)
        {
            return float.NaN;
        }

        HumanPoseHandler handler = null;
        try
        {
            handler = new HumanPoseHandler(_animator.avatar, _animator.transform);
            HumanPose pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            return pose.bodyPosition.y;
        }
        catch
        {
            return float.NaN;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    private AnimationTimeMetrics CaptureAnimationTimeMetrics()
    {
        if (TryCaptureRetargeterAnimationTime(out AnimationTimeMetrics retargeterMetrics))
        {
            return retargeterMetrics;
        }

        if (TryCaptureAnimatorAnimationTime(out AnimationTimeMetrics animatorMetrics))
        {
            return animatorMetrics;
        }

        return AnimationTimeMetrics.Empty;
    }

    private bool TryCaptureRetargeterAnimationTime(out AnimationTimeMetrics metrics)
    {
        metrics = AnimationTimeMetrics.Empty;

        Component retargeter = FindRetargeterForCurrentAnimator();
        if (retargeter == null)
        {
            return false;
        }

        FieldInfo legacyAnimationField = retargeter.GetType().GetField("_legacyAnim", BindingFlags.Instance | BindingFlags.NonPublic);
        Animation legacyAnimation = legacyAnimationField != null ? legacyAnimationField.GetValue(retargeter) as Animation : null;
        if (legacyAnimation == null || legacyAnimation.clip == null)
        {
            return false;
        }

        AnimationClip clip = legacyAnimation.clip;
        AnimationState state = legacyAnimation[PoseSpaceRetargeterLegacyClipStateName] ?? legacyAnimation[clip.name];
        float clipLength = state != null ? state.length : clip.length;
        if (clipLength <= 0f || float.IsNaN(clipLength) || float.IsInfinity(clipLength))
        {
            return false;
        }

        float clipTime = state != null ? state.time : 0f;
        string source = MotionComparisonProbeReportWriter.BuildRetargeterLegacyAnimationTimeSourceLabel();
        if (clipTime <= 0.0001f && _recorder != null && _recorder.FrameNumber > 0)
        {
            clipTime = _recorder.FrameNumber / 30f;
            source = MotionComparisonProbeReportWriter.BuildRetargeterLegacyRecorderFrameAnimationTimeSourceLabel();
        }

        clipTime = Mathf.Clamp(clipTime, 0f, clipLength);

        metrics = new AnimationTimeMetrics
        {
            Source = source,
            ClipName = clip.name,
            ClipTime = clipTime,
            ClipLength = clipLength,
            NormalizedTime = clipLength > 0f ? clipTime / clipLength : float.NaN
        };
        return true;
    }

    private Component FindRetargeterForCurrentAnimator()
    {
        Component fallback = null;
        int retargeterCount = 0;
        Component[] components = UnityEngine.Object.FindObjectsOfType<Component>();
        foreach (Component component in components)
        {
            if (component == null || component.GetType().Name != "PoseSpaceRetargeter")
            {
                continue;
            }

            retargeterCount++;
            fallback ??= component;

            FieldInfo targetAnimatorField = component.GetType().GetField("targetAnimator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Animator targetAnimator = targetAnimatorField != null ? targetAnimatorField.GetValue(component) as Animator : null;
            if (targetAnimator == _animator)
            {
                return component;
            }
        }

        return retargeterCount == 1 ? fallback : null;
    }

    private bool TryCaptureAnimatorAnimationTime(out AnimationTimeMetrics metrics)
    {
        metrics = AnimationTimeMetrics.Empty;
        if (_animator == null || !_animator.isInitialized)
        {
            return false;
        }

        const int layerIndex = 0;
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (float.IsNaN(stateInfo.normalizedTime) || float.IsInfinity(stateInfo.normalizedTime))
        {
            return false;
        }

        AnimationClip clip = ResolveCurrentAnimatorClip(layerIndex);
        float clipLength = clip != null ? clip.length : stateInfo.length;
        if (clipLength <= 0f || float.IsNaN(clipLength) || float.IsInfinity(clipLength))
        {
            return false;
        }

        float rawClipTime = stateInfo.normalizedTime * clipLength;
        float clipTime = stateInfo.loop
            ? Mathf.Repeat(rawClipTime, clipLength)
            : Mathf.Clamp(rawClipTime, 0f, clipLength);

        metrics = new AnimationTimeMetrics
        {
            Source = MotionComparisonProbeReportWriter.BuildAnimatorStateAnimationTimeSourceLabel(),
            ClipName = clip != null ? clip.name : "",
            ClipTime = clipTime,
            ClipLength = clipLength,
            NormalizedTime = stateInfo.normalizedTime
        };
        return true;
    }

    private RootSpikeMetrics CaptureRootSpikeMetrics()
    {
        Component retargeter = FindRetargeterForCurrentAnimator();
        if (retargeter == null)
        {
            return RootSpikeMetrics.Empty;
        }

        Type type = retargeter.GetType();
        float lastGroundingVerticalStep = ReadFloatProperty(type, retargeter, "LastGroundingVerticalStep");
        float groundingMaxStepPerFrame = ReadFloatMember(type, retargeter, "maxGroundingVerticalStepPerFrame");
        return new RootSpikeMetrics
        {
            LastRootDeltaMagnitude = ReadFloatProperty(type, retargeter, "LastRootDeltaMagnitude"),
            MaxRootDeltaMagnitude = ReadFloatProperty(type, retargeter, "MaxRootDeltaMagnitude"),
            RootDeltaSpikeSkippedCount = ReadIntProperty(type, retargeter, "RootDeltaSpikeSkippedCount"),
            LastRootPositionPoseDeltaMagnitude = ReadFloatProperty(type, retargeter, "LastRootPositionPoseDeltaMagnitude"),
            MaxRootPositionPoseDeltaMagnitude = ReadFloatProperty(type, retargeter, "MaxRootPositionPoseDeltaMagnitude"),
            RootPositionSpikeClampedCount = ReadIntProperty(type, retargeter, "RootPositionSpikeClampedCount"),
            LastGroundingAdjustment = ReadFloatProperty(type, retargeter, "LastGroundingAdjustment"),
            MaxGroundingAdjustment = ReadFloatProperty(type, retargeter, "MaxGroundingAdjustment"),
            GroundingStepClampedCount = ReadIntProperty(type, retargeter, "GroundingStepClampedCount"),
            GroundingSmoothedCount = ReadIntProperty(type, retargeter, "GroundingSmoothedCount"),
            LastGroundingVerticalStep = lastGroundingVerticalStep,
            MaxGroundingVerticalStep = ReadFloatProperty(type, retargeter, "MaxGroundingVerticalStep"),
            InitialGroundingVerticalStep = ReadFloatProperty(type, retargeter, "InitialGroundingVerticalStep"),
            MaxGroundingVerticalStepAfterInitial = ReadFloatProperty(type, retargeter, "MaxGroundingVerticalStepAfterInitial"),
            LastGroundingTargetY = ReadFloatProperty(type, retargeter, "LastGroundingTargetY"),
            LastGroundingLowestFootBottomY = ReadFloatProperty(type, retargeter, "LastGroundingLowestFootBottomY"),
            FootHeightReferenceLift = ReadFloatProperty(type, retargeter, "LastEditorFootHeightGroundingReferenceLift"),
            RecordingStartRootY = ReadFloatProperty(type, retargeter, "RecordingStartRootY"),
            RecordingStartBodyPositionY = ReadFloatProperty(type, retargeter, "RecordingStartBodyPositionY"),
            RecordingStartHipsLocalY = ReadFloatProperty(type, retargeter, "RecordingStartHipsLocalY"),
            RecordingStartHipsY = ReadFloatProperty(type, retargeter, "RecordingStartHipsY"),
            RecordingStartHipsReferenceBeforeLocalY = ReadFloatProperty(type, retargeter, "RecordingStartHipsReferenceBeforeLocalY"),
            RecordingStartHipsReferenceAfterLocalY = ReadFloatProperty(type, retargeter, "RecordingStartHipsReferenceAfterLocalY"),
            RecordingStartHipsReferenceDeltaY = ReadFloatProperty(type, retargeter, "RecordingStartHipsReferenceDeltaY"),
            RecordingStartHipsReferenceFlipDetected = ReadIntProperty(type, retargeter, "RecordingStartHipsReferenceFlipDetected"),
            RecordingStartHipsReferenceStage = ReadStringProperty(type, retargeter, "RecordingStartHipsReferenceStage"),
            PoseInputLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastPoseInputLeftShoulderFrontBackMuscle"),
            AfterEditorMuscleReferenceLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastAfterEditorMuscleReferenceLeftShoulderFrontBackMuscle"),
            AfterClampPoseMusclesLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastAfterClampPoseMusclesLeftShoulderFrontBackMuscle"),
            AfterAnatomicalArmGuardLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastAfterAnatomicalArmGuardLeftShoulderFrontBackMuscle"),
            AfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastAfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle"),
            SetHumanPoseInputLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftShoulderFrontBackMuscle"),
            SetHumanPoseOutputLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftShoulderFrontBackMuscle"),
            SetHumanPoseLeftShoulderFrontBackDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftShoulderFrontBackDelta"),
            PoseInputLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastPoseInputLeftArmTwistMuscle"),
            AfterEditorMuscleReferenceLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterEditorMuscleReferenceLeftArmTwistMuscle"),
            AfterClampPoseMusclesLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterClampPoseMusclesLeftArmTwistMuscle"),
            AfterAnatomicalArmGuardLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterAnatomicalArmGuardLeftArmTwistMuscle"),
            AfterVisualSpikeSmoothingLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterVisualSpikeSmoothingLeftArmTwistMuscle"),
            SetHumanPoseInputLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftArmTwistMuscle"),
            SetHumanPoseOutputLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftArmTwistMuscle"),
            SetHumanPoseLeftArmTwistDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftArmTwistDelta"),
            PoseInputLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastPoseInputLeftForearmStretchMuscle"),
            AfterEditorMuscleReferenceLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterEditorMuscleReferenceLeftForearmStretchMuscle"),
            AfterClampPoseMusclesLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterClampPoseMusclesLeftForearmStretchMuscle"),
            AfterAnatomicalArmGuardLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterAnatomicalArmGuardLeftForearmStretchMuscle"),
            AfterVisualSpikeSmoothingLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterVisualSpikeSmoothingLeftForearmStretchMuscle"),
            SetHumanPoseInputLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftForearmStretchMuscle"),
            SetHumanPoseOutputLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftForearmStretchMuscle"),
            SetHumanPoseLeftForearmStretchDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftForearmStretchDelta"),
            PoseInputRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastPoseInputRightForearmStretchMuscle"),
            AfterEditorMuscleReferenceRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterEditorMuscleReferenceRightForearmStretchMuscle"),
            AfterClampPoseMusclesRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterClampPoseMusclesRightForearmStretchMuscle"),
            AfterAnatomicalArmGuardRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterAnatomicalArmGuardRightForearmStretchMuscle"),
            AfterVisualSpikeSmoothingRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterVisualSpikeSmoothingRightForearmStretchMuscle"),
            SetHumanPoseInputRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightForearmStretchMuscle"),
            SetHumanPoseOutputRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightForearmStretchMuscle"),
            SetHumanPoseRightForearmStretchDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightForearmStretchDelta"),
            PoseInputRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastPoseInputRightArmTwistMuscle"),
            AfterEditorMuscleReferenceRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterEditorMuscleReferenceRightArmTwistMuscle"),
            AfterClampPoseMusclesRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterClampPoseMusclesRightArmTwistMuscle"),
            AfterAnatomicalArmGuardRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterAnatomicalArmGuardRightArmTwistMuscle"),
            AfterVisualSpikeSmoothingRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterVisualSpikeSmoothingRightArmTwistMuscle"),
            SetHumanPoseInputRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightArmTwistMuscle"),
            SetHumanPoseOutputRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightArmTwistMuscle"),
            SetHumanPoseRightArmTwistDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightArmTwistDelta"),
            SetHumanPoseInputLeftUpperLegFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftUpperLegFrontBackMuscle"),
            SetHumanPoseOutputLeftUpperLegFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftUpperLegFrontBackMuscle"),
            SetHumanPoseLeftUpperLegFrontBackDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftUpperLegFrontBackDelta"),
            SetHumanPoseInputRightUpperLegFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightUpperLegFrontBackMuscle"),
            SetHumanPoseOutputRightUpperLegFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightUpperLegFrontBackMuscle"),
            SetHumanPoseRightUpperLegFrontBackDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightUpperLegFrontBackDelta"),
            SetHumanPoseInputLeftLowerLegStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftLowerLegStretchMuscle"),
            SetHumanPoseOutputLeftLowerLegStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftLowerLegStretchMuscle"),
            SetHumanPoseLeftLowerLegStretchDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftLowerLegStretchDelta"),
            SetHumanPoseInputRightLowerLegStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightLowerLegStretchMuscle"),
            SetHumanPoseOutputRightLowerLegStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightLowerLegStretchMuscle"),
            SetHumanPoseRightLowerLegStretchDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightLowerLegStretchDelta"),
            SetHumanPoseInputLeftFootUpDownMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftFootUpDownMuscle"),
            SetHumanPoseOutputLeftFootUpDownMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftFootUpDownMuscle"),
            SetHumanPoseLeftFootUpDownDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftFootUpDownDelta"),
            SetHumanPoseInputRightFootUpDownMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightFootUpDownMuscle"),
            SetHumanPoseOutputRightFootUpDownMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightFootUpDownMuscle"),
            SetHumanPoseRightFootUpDownDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightFootUpDownDelta"),
            SetHumanPoseInputBodyPositionX = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputBodyPositionX"),
            SetHumanPoseInputBodyPositionY = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputBodyPositionY"),
            SetHumanPoseInputBodyPositionZ = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputBodyPositionZ"),
            SetHumanPoseOutputBodyPositionX = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputBodyPositionX"),
            SetHumanPoseOutputBodyPositionY = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputBodyPositionY"),
            SetHumanPoseOutputBodyPositionZ = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputBodyPositionZ"),
            SetHumanPoseBodyPositionDeltaXZ = ReadFloatProperty(type, retargeter, "LastSetHumanPoseBodyPositionDeltaXZ"),
            SetHumanPoseInputBodyRotationYaw = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputBodyRotationYaw"),
            SetHumanPoseOutputBodyRotationYaw = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputBodyRotationYaw"),
            SetHumanPoseBodyRotationDeltaAngle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseBodyRotationDeltaAngle"),
            SetHumanPosePreSolveGhostRootWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostRootWorldX"),
            SetHumanPosePreSolveGhostRootWorldY = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostRootWorldY"),
            SetHumanPosePreSolveGhostRootWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostRootWorldZ"),
            SetHumanPosePreSolveGhostRootYaw = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostRootYaw"),
            SetHumanPosePreSolveTargetRootWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetRootWorldX"),
            SetHumanPosePreSolveTargetRootWorldY = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetRootWorldY"),
            SetHumanPosePreSolveTargetRootWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetRootWorldZ"),
            SetHumanPosePreSolveTargetRootYaw = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetRootYaw"),
            SetHumanPosePreSolveTargetHipsWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetHipsWorldX"),
            SetHumanPosePreSolveTargetHipsWorldY = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetHipsWorldY"),
            SetHumanPosePreSolveTargetHipsWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetHipsWorldZ"),
            SetHumanPosePreSolveTargetHipsLocalX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetHipsLocalX"),
            SetHumanPosePreSolveTargetHipsLocalY = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetHipsLocalY"),
            SetHumanPosePreSolveTargetHipsLocalZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetHipsLocalZ"),
            SetHumanPosePreSolveBodyPositionX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveBodyPositionX"),
            SetHumanPosePreSolveBodyPositionY = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveBodyPositionY"),
            SetHumanPosePreSolveBodyPositionZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveBodyPositionZ"),
            SetHumanPosePreSolveBodyRotationYaw = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveBodyRotationYaw"),
            PreSetHumanPoseEndpointBodyPositionBeforeX = ReadFloatProperty(type, retargeter, "LastPreSetHumanPoseEndpointBodyPositionBeforeX"),
            PreSetHumanPoseEndpointBodyPositionBeforeZ = ReadFloatProperty(type, retargeter, "LastPreSetHumanPoseEndpointBodyPositionBeforeZ"),
            PreSetHumanPoseEndpointBodyPositionAfterX = ReadFloatProperty(type, retargeter, "LastPreSetHumanPoseEndpointBodyPositionAfterX"),
            PreSetHumanPoseEndpointBodyPositionAfterZ = ReadFloatProperty(type, retargeter, "LastPreSetHumanPoseEndpointBodyPositionAfterZ"),
            PreSetHumanPoseEndpointBodyPositionDeltaX = ReadFloatProperty(type, retargeter, "LastPreSetHumanPoseEndpointBodyPositionDeltaX"),
            PreSetHumanPoseEndpointBodyPositionDeltaZ = ReadFloatProperty(type, retargeter, "LastPreSetHumanPoseEndpointBodyPositionDeltaZ"),
            PreSetHumanPoseEndpointBodyPositionDeltaMagnitudeXZ = ReadFloatProperty(type, retargeter, "LastPreSetHumanPoseEndpointBodyPositionDeltaMagnitudeXZ"),
            SetHumanPosePreSolveGhostLeftFootWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostLeftFootWorldX"),
            SetHumanPosePreSolveGhostLeftFootWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostLeftFootWorldZ"),
            SetHumanPosePreSolveGhostLeftToesWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostLeftToesWorldX"),
            SetHumanPosePreSolveGhostLeftToesWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostLeftToesWorldZ"),
            SetHumanPosePreSolveCurrentLeftFootWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveCurrentLeftFootWorldX"),
            SetHumanPosePreSolveCurrentLeftFootWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveCurrentLeftFootWorldZ"),
            SetHumanPosePreSolveCurrentLeftToesWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveCurrentLeftToesWorldX"),
            SetHumanPosePreSolveCurrentLeftToesWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveCurrentLeftToesWorldZ"),
            SetHumanPosePreSolveTargetLeftFootWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetLeftFootWorldX"),
            SetHumanPosePreSolveTargetLeftFootWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetLeftFootWorldZ"),
            SetHumanPosePreSolveTargetLeftToesWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetLeftToesWorldX"),
            SetHumanPosePreSolveTargetLeftToesWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetLeftToesWorldZ"),
            SetHumanPosePreSolveGhostRightFootWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostRightFootWorldX"),
            SetHumanPosePreSolveGhostRightFootWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostRightFootWorldZ"),
            SetHumanPosePreSolveGhostRightToesWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostRightToesWorldX"),
            SetHumanPosePreSolveGhostRightToesWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveGhostRightToesWorldZ"),
            SetHumanPosePreSolveCurrentRightFootWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveCurrentRightFootWorldX"),
            SetHumanPosePreSolveCurrentRightFootWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveCurrentRightFootWorldZ"),
            SetHumanPosePreSolveCurrentRightToesWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveCurrentRightToesWorldX"),
            SetHumanPosePreSolveCurrentRightToesWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveCurrentRightToesWorldZ"),
            SetHumanPosePreSolveTargetRightFootWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetRightFootWorldX"),
            SetHumanPosePreSolveTargetRightFootWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetRightFootWorldZ"),
            SetHumanPosePreSolveTargetRightToesWorldX = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetRightToesWorldX"),
            SetHumanPosePreSolveTargetRightToesWorldZ = ReadFloatProperty(type, retargeter, "LastSetHumanPosePreSolveTargetRightToesWorldZ"),
            SetHumanPoseInputSpineFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputSpineFrontBackMuscle"),
            SetHumanPoseInputSpineLeftRightMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputSpineLeftRightMuscle"),
            SetHumanPoseInputSpineTwistLeftRightMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputSpineTwistLeftRightMuscle"),
            SetHumanPoseInputChestFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputChestFrontBackMuscle"),
            SetHumanPoseInputChestLeftRightMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputChestLeftRightMuscle"),
            SetHumanPoseInputChestTwistLeftRightMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputChestTwistLeftRightMuscle"),
            SetHumanPoseInputUpperChestFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputUpperChestFrontBackMuscle"),
            SetHumanPoseInputUpperChestLeftRightMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputUpperChestLeftRightMuscle"),
            SetHumanPoseInputUpperChestTwistLeftRightMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputUpperChestTwistLeftRightMuscle"),
            SetHumanPoseInputLeftUpperLegInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftUpperLegInOutMuscle"),
            SetHumanPoseInputRightUpperLegInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightUpperLegInOutMuscle"),
            SetHumanPoseInputLeftUpperLegTwistInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftUpperLegTwistInOutMuscle"),
            SetHumanPoseInputRightUpperLegTwistInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightUpperLegTwistInOutMuscle"),
            SetHumanPoseInputLeftLowerLegTwistInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftLowerLegTwistInOutMuscle"),
            SetHumanPoseInputRightLowerLegTwistInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightLowerLegTwistInOutMuscle"),
            SetHumanPoseInputLeftFootTwistInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftFootTwistInOutMuscle"),
            SetHumanPoseInputRightFootTwistInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightFootTwistInOutMuscle"),
            SetHumanPoseInputLeftToesUpDownMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftToesUpDownMuscle"),
            SetHumanPoseInputRightToesUpDownMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightToesUpDownMuscle"),
            SetHumanPoseOutputRightUpperLegInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightUpperLegInOutMuscle"),
            SetHumanPoseRightUpperLegInOutDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightUpperLegInOutDelta"),
            SetHumanPoseOutputRightUpperLegTwistInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightUpperLegTwistInOutMuscle"),
            SetHumanPoseRightUpperLegTwistInOutDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightUpperLegTwistInOutDelta"),
            SetHumanPoseOutputRightLowerLegTwistInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightLowerLegTwistInOutMuscle"),
            SetHumanPoseRightLowerLegTwistInOutDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightLowerLegTwistInOutDelta"),
            SetHumanPoseOutputRightFootTwistInOutMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightFootTwistInOutMuscle"),
            SetHumanPoseRightFootTwistInOutDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightFootTwistInOutDelta"),
            SetHumanPoseOutputRightToesUpDownMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightToesUpDownMuscle"),
            SetHumanPoseRightToesUpDownDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightToesUpDownDelta"),
            RetargetStageGhost = ReadRetargetEndpointStage(type, retargeter, "Ghost"),
            RetargetStageAfterSetHumanPose = ReadRetargetEndpointStage(type, retargeter, "AfterSetHumanPose"),
            RetargetStageAfterManualReferences = ReadRetargetEndpointStage(type, retargeter, "AfterManualReferences"),
            RetargetStageAfterRootRestore = ReadRetargetEndpointStage(type, retargeter, "AfterRootRestore"),
            RetargetStageAfterRootDelta = ReadRetargetEndpointStage(type, retargeter, "AfterRootDelta"),
            RetargetStageAfterGrounding = ReadRetargetEndpointStage(type, retargeter, "AfterGrounding"),
            RetargetStageAfterBipedIK = ReadRetargetEndpointStage(type, retargeter, "AfterBipedIK"),
            RetargetStageAfterLateVisualGrounding = ReadRetargetEndpointStage(type, retargeter, "AfterLateVisualGrounding"),
            EditorFootLocalRotationLeftFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorFootLocalRotationLeftFootXzDelta"),
            EditorFootLocalRotationRightFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorFootLocalRotationRightFootXzDelta"),
            EditorLowerBodySegmentDirectionLeftFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootXzDelta"),
            EditorLowerBodySegmentDirectionRightFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootXzDelta"),
            EditorLowerBodySegmentDirectionMaxCorrectionSegment = ReadStringProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxCorrectionSegment"),
            EditorLowerBodySegmentDirectionMaxCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxCorrectionAngle"),
            EditorLowerBodySegmentDirectionMaxPreAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPreAngle"),
            EditorLowerBodySegmentDirectionMaxPostAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPostAngle"),
            EditorLowerBodySegmentDirectionMaxCorrectionAxisX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxCorrectionAxisX"),
            EditorLowerBodySegmentDirectionMaxCorrectionAxisY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxCorrectionAxisY"),
            EditorLowerBodySegmentDirectionMaxCorrectionAxisZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxCorrectionAxisZ"),
            EditorLowerBodySegmentDirectionMaxReferenceDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxReferenceDirectionX"),
            EditorLowerBodySegmentDirectionMaxReferenceDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxReferenceDirectionY"),
            EditorLowerBodySegmentDirectionMaxReferenceDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxReferenceDirectionZ"),
            EditorLowerBodySegmentDirectionMaxPreDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPreDirectionX"),
            EditorLowerBodySegmentDirectionMaxPreDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPreDirectionY"),
            EditorLowerBodySegmentDirectionMaxPreDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPreDirectionZ"),
            EditorLowerBodySegmentDirectionMaxPostDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPostDirectionX"),
            EditorLowerBodySegmentDirectionMaxPostDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPostDirectionY"),
            EditorLowerBodySegmentDirectionMaxPostDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPostDirectionZ"),
            EditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle"),
            EditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle"),
            EditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle"),
            EditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle"),
            EditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle"),
            EditorLowerBodySegmentDirectionRightFootToesCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToesCorrectionAngle"),
            EditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle"),
            EditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle"),
            EditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle"),
            EditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle"),
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX"),
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY"),
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ"),
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX"),
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY"),
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ"),
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX"),
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY"),
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ"),
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX"),
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY"),
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ"),
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPreDirectionX"),
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPreDirectionY"),
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ"),
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPostDirectionX"),
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPostDirectionY"),
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ"),
            EditorLowerBodySegmentDirectionLeftLowerLegWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegWorldX"),
            EditorLowerBodySegmentDirectionLeftLowerLegWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegWorldY"),
            EditorLowerBodySegmentDirectionLeftLowerLegWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegWorldZ"),
            EditorLowerBodySegmentDirectionLeftFootWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootWorldX"),
            EditorLowerBodySegmentDirectionLeftFootWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootWorldY"),
            EditorLowerBodySegmentDirectionLeftFootWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootWorldZ"),
            EditorLowerBodySegmentDirectionLeftToesWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftToesWorldX"),
            EditorLowerBodySegmentDirectionLeftToesWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftToesWorldY"),
            EditorLowerBodySegmentDirectionLeftToesWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftToesWorldZ"),
            EditorLowerBodySegmentDirectionRightLowerLegWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegWorldX"),
            EditorLowerBodySegmentDirectionRightLowerLegWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegWorldY"),
            EditorLowerBodySegmentDirectionRightLowerLegWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegWorldZ"),
            EditorLowerBodySegmentDirectionRightFootWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootWorldX"),
            EditorLowerBodySegmentDirectionRightFootWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootWorldY"),
            EditorLowerBodySegmentDirectionRightFootWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootWorldZ"),
            EditorLowerBodySegmentDirectionRightToesWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightToesWorldX"),
            EditorLowerBodySegmentDirectionRightToesWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightToesWorldY"),
            EditorLowerBodySegmentDirectionRightToesWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightToesWorldZ"),
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX"),
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY"),
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ"),
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX"),
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY"),
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ"),
            EditorLowerBodySegmentDirectionLeftFootForwardX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootForwardX"),
            EditorLowerBodySegmentDirectionLeftFootForwardY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootForwardY"),
            EditorLowerBodySegmentDirectionLeftFootForwardZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootForwardZ"),
            EditorLowerBodySegmentDirectionLeftFootUpX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootUpX"),
            EditorLowerBodySegmentDirectionLeftFootUpY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootUpY"),
            EditorLowerBodySegmentDirectionLeftFootUpZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootUpZ"),
            EditorLowerBodySegmentDirectionRightFootForwardX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootForwardX"),
            EditorLowerBodySegmentDirectionRightFootForwardY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootForwardY"),
            EditorLowerBodySegmentDirectionRightFootForwardZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootForwardZ"),
            EditorLowerBodySegmentDirectionRightFootUpX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootUpX"),
            EditorLowerBodySegmentDirectionRightFootUpY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootUpY"),
            EditorLowerBodySegmentDirectionRightFootUpZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootUpZ"),
            EditorFootHipsAlignedResidualYawLeftFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorFootHipsAlignedResidualYawLeftFootXzDelta"),
            EditorFootHipsAlignedResidualYawRightFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorFootHipsAlignedResidualYawRightFootXzDelta"),
            PostSetRightEndpointDesiredFootWorldX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDesiredFootWorldX"),
            PostSetRightEndpointDesiredFootWorldZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDesiredFootWorldZ"),
            PostSetRightEndpointDesiredToesWorldX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDesiredToesWorldX"),
            PostSetRightEndpointDesiredToesWorldZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDesiredToesWorldZ"),
            PostSetRightEndpointCurrentFootWorldX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCurrentFootWorldX"),
            PostSetRightEndpointCurrentFootWorldZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCurrentFootWorldZ"),
            PostSetRightEndpointCurrentToesWorldX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCurrentToesWorldX"),
            PostSetRightEndpointCurrentToesWorldZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCurrentToesWorldZ"),
            PostSetRightEndpointDeltaBeforeClampX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaBeforeClampX"),
            PostSetRightEndpointDeltaBeforeClampZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaBeforeClampZ"),
            PostSetRightEndpointDeltaAfterClampX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaAfterClampX"),
            PostSetRightEndpointDeltaAfterClampZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaAfterClampZ"),
            PostSetRightEndpointDeltaAfterPositiveZScaleX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScaleX"),
            PostSetRightEndpointDeltaAfterPositiveZScaleZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScaleZ"),
            PostSetRightEndpointCorrectionX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCorrectionX"),
            PostSetRightEndpointCorrectionZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCorrectionZ"),
            PostSetRightEndpointNextFootWorldX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointNextFootWorldX"),
            PostSetRightEndpointNextFootWorldZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointNextFootWorldZ"),
            PostSetRightEndpointMaxYawAngle = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointMaxYawAngle"),
            PostSetRightEndpointYawCorrectionAngle = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointYawCorrectionAngle"),
            PostSetRightEndpointUpperLegRotationDeltaAngle = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointUpperLegRotationDeltaAngle"),
            PostSetRightEndpointApplied = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointApplied"),
            PostSetRightEndpointEvaluatorXzReferenceEnabled = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzReferenceEnabled"),
            PostSetRightEndpointEvaluatorXzFirstOffsetX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffsetX"),
            PostSetRightEndpointEvaluatorXzFirstOffsetZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffsetZ"),
            PostSetRightEndpointEvaluatorXzNormalizedDeltaX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDeltaX"),
            PostSetRightEndpointEvaluatorXzNormalizedDeltaZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDeltaZ"),
            PostSetRightEndpointEvaluatorXzNormalizedMagnitude = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedMagnitude"),
            PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDeltaX"),
            PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDeltaZ"),
            PostSetRightEndpointEvaluatorXzTargetMagnitude = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzTargetMagnitude"),
            GroundingMaxStepPerFrame = groundingMaxStepPerFrame,
            GroundingLastStepToMaxStepRatio = CalculateStepToMaxRatio(lastGroundingVerticalStep, groundingMaxStepPerFrame),
            GroundingLastStepAtMaxStep = IsStepAtMax(lastGroundingVerticalStep, groundingMaxStepPerFrame) ? 1 : 0
        };
    }

    private static RetargetEndpointStageMetrics ReadRetargetEndpointStage(Type type, object retargeter, string stageName)
    {
        string prefix = "LastRetargetStage" + stageName;
        return new RetargetEndpointStageMetrics
        {
            LeftFootWorldX = ReadFloatProperty(type, retargeter, prefix + "LeftFootWorldX"),
            LeftFootWorldZ = ReadFloatProperty(type, retargeter, prefix + "LeftFootWorldZ"),
            LeftToesWorldX = ReadFloatProperty(type, retargeter, prefix + "LeftToesWorldX"),
            LeftToesWorldZ = ReadFloatProperty(type, retargeter, prefix + "LeftToesWorldZ"),
            RightFootWorldX = ReadFloatProperty(type, retargeter, prefix + "RightFootWorldX"),
            RightFootWorldZ = ReadFloatProperty(type, retargeter, prefix + "RightFootWorldZ"),
            RightToesWorldX = ReadFloatProperty(type, retargeter, prefix + "RightToesWorldX"),
            RightToesWorldZ = ReadFloatProperty(type, retargeter, prefix + "RightToesWorldZ")
        };
    }

    private static float CalculateStepToMaxRatio(float step, float maxStep)
    {
        if (float.IsNaN(step) ||
            float.IsInfinity(step) ||
            float.IsNaN(maxStep) ||
            float.IsInfinity(maxStep) ||
            maxStep <= 0f)
        {
            return float.NaN;
        }

        return Mathf.Abs(step) / maxStep;
    }

    private static bool IsStepAtMax(float step, float maxStep)
    {
        float ratio = CalculateStepToMaxRatio(step, maxStep);
        return !float.IsNaN(ratio) && !float.IsInfinity(ratio) && ratio >= 0.95f;
    }

    private ThumbGuardDiagnostics CaptureThumbGuardDiagnostics()
    {
        ThumbGuardDiagnostics metrics = ThumbGuardDiagnostics.Empty;
        Component thumbGuard = FindThumbDeformationGuardForCurrentAnimator();
        if (thumbGuard == null)
        {
            return metrics;
        }

        Type guardType = thumbGuard.GetType();
        metrics.ManualThumbReferenceConfigured = ReadBoolMemberAsFloat(
            guardType,
            thumbGuard,
            "suppressPoseShapingWithManualThumbReference");
        metrics.ProjectionGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveThumbProjectionGuardWeight");
        metrics.LeftProjectionGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveLeftThumbProjectionGuardWeight");
        metrics.RightProjectionGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveRightThumbProjectionGuardWeight");
        metrics.IndexSpreadGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveThumbIndexSpreadGuardWeight");
        metrics.LeftIndexSpreadGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveLeftThumbIndexSpreadGuardWeight");
        metrics.RightIndexSpreadGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveRightThumbIndexSpreadGuardWeight");
        metrics.SegmentStraightenWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveThumbSegmentStraightenWeight");
        metrics.LeftSegmentStraightenWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveLeftThumbSegmentStraightenWeight");
        metrics.RightSegmentStraightenWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveRightThumbSegmentStraightenWeight");
        metrics.LeftProjectionCorrectionApplyCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastLeftThumbProjectionCorrectionApplyCount");
        metrics.RightProjectionCorrectionApplyCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastRightThumbProjectionCorrectionApplyCount");
        metrics.LeftProjectionCorrectionPreserveCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastLeftThumbProjectionCorrectionPreserveCount");
        metrics.RightProjectionCorrectionPreserveCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastRightThumbProjectionCorrectionPreserveCount");
        metrics.LeftSegmentStraightenApplyCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastLeftThumbSegmentStraightenApplyCount");
        metrics.RightSegmentStraightenApplyCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastRightThumbSegmentStraightenApplyCount");
        metrics.LeftSegmentStraightenPreserveCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastLeftThumbSegmentStraightenPreserveCount");
        metrics.RightSegmentStraightenPreserveCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastRightThumbSegmentStraightenPreserveCount");
        metrics.HelperSyncEnabled = ReadBoolMemberAsFloat(
            guardType,
            thumbGuard,
            "syncDetachedThumbBaseHelpers");
        metrics.HelperPositionSyncEnabled = ReadBoolMemberAsFloat(
            guardType,
            thumbGuard,
            "syncDetachedThumbBaseHelperPositions");
        metrics.HelperSyncWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "detachedThumbBaseHelperSyncWeight");
        metrics.HelperMaxLocalAngle = ReadFloatMember(
            guardType,
            thumbGuard,
            "detachedThumbBaseHelperMaxLocalAngle");
        metrics.PalmStabilizeEnabled = ReadBoolMemberAsFloat(
            guardType,
            thumbGuard,
            "stabilizeDetachedThumbBasePalm");
        metrics.PalmStabilizeWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "detachedThumbBasePalmStabilizeWeight");
        metrics.PalmStabilizeMaxLocalAngle = ReadFloatMember(
            guardType,
            thumbGuard,
            "detachedThumbBasePalmMaxLocalAngle");
        metrics.WebbingStabilizeEnabled = ReadBoolMemberAsFloat(
            guardType,
            thumbGuard,
            "stabilizeThumbWebbingCrease");
        metrics.WebbingStabilizeWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "thumbWebbingCreaseStabilizeWeight");
        metrics.WebbingMaxLocalAngle = ReadFloatMember(
            guardType,
            thumbGuard,
            "thumbWebbingCreaseMaxLocalAngle");
        metrics.WebbingMaxPositionOffset = ReadFloatMember(
            guardType,
            thumbGuard,
            "thumbWebbingCreaseMaxPositionOffset");

        Component retargeter = FindRetargeterForCurrentAnimator();
        if (retargeter == null)
        {
            return metrics;
        }

        Type retargeterType = retargeter.GetType();
        metrics.ManualThumbReferenceActive = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "IsManualThumbLocalRotationReferenceActive");
        metrics.PoseShapingSuppressed = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "ShouldSuppressThumbPoseShapingGuard");
        metrics.LeftPoseShapingSuppressed = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "ShouldSuppressLeftThumbPoseShapingGuard");
        metrics.RightPoseShapingSuppressed = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "ShouldSuppressRightThumbPoseShapingGuard");
        metrics.LeftLocalRotationGuardClampCount = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbLocalRotationGuardClampCount");
        metrics.RightLocalRotationGuardClampCount = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbLocalRotationGuardClampCount");
        metrics.LeftLocalRotationGuardPreserveCount = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbLocalRotationGuardPreserveCount");
        metrics.RightLocalRotationGuardPreserveCount = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbLocalRotationGuardPreserveCount");
        metrics.LeftLocalRotationGuardCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbLocalRotationGuardCurrentRisk");
        metrics.RightLocalRotationGuardCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbLocalRotationGuardCurrentRisk");
        metrics.LeftLocalRotationGuardLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbLocalRotationGuardLimitedRisk");
        metrics.RightLocalRotationGuardLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbLocalRotationGuardLimitedRisk");
        metrics.LeftWorldRotationSuppressCompetingOverride = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbWorldRotationSuppressCompetingOverride");
        metrics.RightWorldRotationSuppressCompetingOverride = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbWorldRotationSuppressCompetingOverride");
        metrics.LeftWorldRotationKeepDetachedHelperOverride = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbWorldRotationKeepDetachedHelperOverride");
        metrics.RightWorldRotationKeepDetachedHelperOverride = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbWorldRotationKeepDetachedHelperOverride");
        metrics.LeftWorldRotationCurrentReferenceFrameDeviation = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbWorldRotationCurrentReferenceFrameDeviation");
        metrics.RightWorldRotationCurrentReferenceFrameDeviation = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbWorldRotationCurrentReferenceFrameDeviation");
        metrics.LeftWorldRotationCandidateReferenceFrameDeviation = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbWorldRotationCandidateReferenceFrameDeviation");
        metrics.RightWorldRotationCandidateReferenceFrameDeviation = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbWorldRotationCandidateReferenceFrameDeviation");
        metrics.LeftProximalWorldRotationPreserveReason = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbProximalWorldRotationPreserveReason");
        metrics.RightProximalWorldRotationPreserveReason = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbProximalWorldRotationPreserveReason");
        metrics.LeftIntermediateWorldRotationPreserveReason = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbIntermediateWorldRotationPreserveReason");
        metrics.RightIntermediateWorldRotationPreserveReason = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbIntermediateWorldRotationPreserveReason");
        metrics.LeftProximalWorldRotationCurrentReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbProximalWorldRotationCurrentReferenceAngle");
        metrics.RightProximalWorldRotationCurrentReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbProximalWorldRotationCurrentReferenceAngle");
        metrics.LeftIntermediateWorldRotationCurrentReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbIntermediateWorldRotationCurrentReferenceAngle");
        metrics.RightIntermediateWorldRotationCurrentReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbIntermediateWorldRotationCurrentReferenceAngle");
        metrics.LeftProximalWorldRotationCandidateReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbProximalWorldRotationCandidateReferenceAngle");
        metrics.RightProximalWorldRotationCandidateReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbProximalWorldRotationCandidateReferenceAngle");
        metrics.LeftIntermediateWorldRotationCandidateReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbIntermediateWorldRotationCandidateReferenceAngle");
        metrics.RightIntermediateWorldRotationCandidateReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbIntermediateWorldRotationCandidateReferenceAngle");
        metrics.LeftProximalWorldRotationPreserveCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbProximalWorldRotationPreserveCurrentRisk");
        metrics.RightProximalWorldRotationPreserveCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbProximalWorldRotationPreserveCurrentRisk");
        metrics.LeftIntermediateWorldRotationPreserveCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbIntermediateWorldRotationPreserveCurrentRisk");
        metrics.RightIntermediateWorldRotationPreserveCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbIntermediateWorldRotationPreserveCurrentRisk");
        metrics.LeftProximalWorldRotationPreserveLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbProximalWorldRotationPreserveLimitedRisk");
        metrics.RightProximalWorldRotationPreserveLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbProximalWorldRotationPreserveLimitedRisk");
        metrics.LeftIntermediateWorldRotationPreserveLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbIntermediateWorldRotationPreserveLimitedRisk");
        metrics.RightIntermediateWorldRotationPreserveLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbIntermediateWorldRotationPreserveLimitedRisk");
        return metrics;
    }

    private Component FindThumbDeformationGuardForCurrentAnimator()
    {
        if (_animator == null)
        {
            return null;
        }

        Component[] components = _animator.gameObject.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component != null && component.GetType().Name == "HumanoidThumbDeformationGuard")
            {
                return component;
            }
        }

        return null;
    }

    private ArmSwingGuardDiagnostics CaptureArmSwingGuardDiagnostics()
    {
        ArmSwingGuardDiagnostics metrics = ArmSwingGuardDiagnostics.Empty;
        Component guard = FindArmSwingLimitGuardForCurrentAnimator();
        if (guard == null)
        {
            return metrics;
        }

        Type guardType = guard.GetType();
        metrics.LeftApplied = ReadIntMemberAsFloat(guardType, guard, "LastLeftApplied");
        metrics.LeftHorizontalReachApplied = ReadIntMemberAsFloat(guardType, guard, "LastLeftHorizontalReachApplied");
        metrics.LeftRaisedReachApplied = ReadIntMemberAsFloat(guardType, guard, "LastLeftRaisedReachApplied");
        metrics.LeftForearmStretchBefore = ReadFloatMember(guardType, guard, "LastLeftForearmStretchBefore");
        metrics.LeftForearmStretchAfter = ReadFloatMember(guardType, guard, "LastLeftForearmStretchAfter");
        metrics.LeftForearmStretchDelta = ReadFloatMember(guardType, guard, "LastLeftForearmStretchDelta");
        metrics.RightApplied = ReadIntMemberAsFloat(guardType, guard, "LastRightApplied");
        metrics.RightHorizontalReachApplied = ReadIntMemberAsFloat(guardType, guard, "LastRightHorizontalReachApplied");
        metrics.RightRaisedReachApplied = ReadIntMemberAsFloat(guardType, guard, "LastRightRaisedReachApplied");
        metrics.RightForearmStretchBefore = ReadFloatMember(guardType, guard, "LastRightForearmStretchBefore");
        metrics.RightForearmStretchAfter = ReadFloatMember(guardType, guard, "LastRightForearmStretchAfter");
        metrics.RightForearmStretchDelta = ReadFloatMember(guardType, guard, "LastRightForearmStretchDelta");
        return metrics;
    }

    private Component FindArmSwingLimitGuardForCurrentAnimator()
    {
        if (_animator == null)
        {
            return null;
        }

        Component[] components = _animator.gameObject.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component != null && component.GetType().Name == "HumanoidArmSwingLimitGuard")
            {
                return component;
            }
        }

        return null;
    }

    private static float ReadFloatProperty(Type type, object instance, string propertyName)
    {
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
        {
            return float.NaN;
        }

        object value = property.GetValue(instance);
        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is double doubleValue)
        {
            return (float)doubleValue;
        }

        return float.NaN;
    }

    private static float ReadFloatMember(Type type, object instance, string memberName)
    {
        object value = ReadMemberValue(type, instance, memberName);
        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is double doubleValue)
        {
            return (float)doubleValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return float.NaN;
    }

    private static float ReadBoolMemberAsFloat(Type type, object instance, string memberName)
    {
        object value = ReadMemberValue(type, instance, memberName);
        if (value is bool boolValue)
        {
            return boolValue ? 1f : 0f;
        }

        return float.NaN;
    }

    private static float ReadIntMemberAsFloat(Type type, object instance, string memberName)
    {
        object value = ReadMemberValue(type, instance, memberName);
        if (value is int intValue)
        {
            return intValue;
        }

        return float.NaN;
    }

    private static object ReadMemberValue(Type type, object instance, string memberName)
    {
        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            return property.GetValue(instance);
        }

        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            return field.GetValue(instance);
        }

        return null;
    }

    private static int ReadIntProperty(Type type, object instance, string propertyName)
    {
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
        {
            return -1;
        }

        object value = property.GetValue(instance);
        return value is int intValue ? intValue : -1;
    }

    private static string ReadStringProperty(Type type, object instance, string propertyName)
    {
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
        {
            return "";
        }

        return property.GetValue(instance) as string ?? "";
    }

    private AnimationClip ResolveCurrentAnimatorClip(int layerIndex)
    {
        AnimationClip bestClip = null;
        float bestWeight = float.MinValue;
        AnimatorClipInfo[] clipInfos = _animator.GetCurrentAnimatorClipInfo(layerIndex);
        foreach (AnimatorClipInfo clipInfo in clipInfos)
        {
            if (clipInfo.clip == null || clipInfo.weight < bestWeight)
            {
                continue;
            }

            bestClip = clipInfo.clip;
            bestWeight = clipInfo.weight;
        }

        if (bestClip != null)
        {
            return bestClip;
        }

        RuntimeAnimatorController controller = _animator.runtimeAnimatorController;
        if (controller != null && controller.animationClips != null && controller.animationClips.Length == 1)
        {
            return controller.animationClips[0];
        }

        return null;
    }

    private void PrepareHumanPoseCapture()
    {
        DisposeHumanPoseCapture();
        _poseWarningLogged = false;

        if (_animator == null || _animator.avatar == null || !_animator.avatar.isValid || !_animator.avatar.isHuman)
        {
            return;
        }

        _poseHandler = new HumanPoseHandler(_animator.avatar, _animator.transform);
        _humanPose = new HumanPose();
    }

    private void DisposeHumanPoseCapture()
    {
        if (_poseHandler == null)
        {
            return;
        }

        _poseHandler.Dispose();
        _poseHandler = null;
    }

    private ArmMuscleMetrics CaptureArmMuscles()
    {
        ArmMuscleMetrics metrics = ArmMuscleMetrics.Empty;
        if (_poseHandler == null)
        {
            return metrics;
        }

        _poseHandler.GetHumanPose(ref _humanPose);
        if (_humanPose.muscles == null || _humanPose.muscles.Length == 0)
        {
            return metrics;
        }

        metrics.LeftShoulderDownUp = GetMuscleValue(_humanPose, "left", "shoulder", "downup");
        metrics.LeftShoulderFrontBack = GetMuscleValue(_humanPose, "left", "shoulder", "frontback");
        metrics.LeftArmDownUp = GetMuscleValue(_humanPose, "left", "arm", "downup");
        metrics.LeftArmFrontBack = GetMuscleValue(_humanPose, "left", "arm", "frontback");
        metrics.LeftArmTwist = GetMuscleValue(_humanPose, "left", "arm", "twist");
        metrics.LeftForearmStretch = GetMuscleValue(_humanPose, "left", "forearm", "stretch");
        metrics.LeftForearmTwist = GetMuscleValue(_humanPose, "left", "forearm", "twist");
        metrics.RightShoulderDownUp = GetMuscleValue(_humanPose, "right", "shoulder", "downup");
        metrics.RightShoulderFrontBack = GetMuscleValue(_humanPose, "right", "shoulder", "frontback");
        metrics.RightArmDownUp = GetMuscleValue(_humanPose, "right", "arm", "downup");
        metrics.RightArmFrontBack = GetMuscleValue(_humanPose, "right", "arm", "frontback");
        metrics.RightArmTwist = GetMuscleValue(_humanPose, "right", "arm", "twist");
        metrics.RightForearmStretch = GetMuscleValue(_humanPose, "right", "forearm", "stretch");
        metrics.RightForearmTwist = GetMuscleValue(_humanPose, "right", "forearm", "twist");
        return metrics;
    }

    private FingerMetrics CaptureFingerMetrics()
    {
        FingerMetrics metrics = FingerMetrics.Empty;
        if (_poseHandler == null)
        {
            return metrics;
        }

        _poseHandler.GetHumanPose(ref _humanPose);
        if (_humanPose.muscles == null || _humanPose.muscles.Length == 0)
        {
            return metrics;
        }

        metrics.LeftThumb1Stretch = GetMuscleValue(_humanPose, "left", "thumb", "1", "stretch");
        metrics.LeftThumbSpread = GetMuscleValue(_humanPose, "left", "thumb", "spread");
        metrics.LeftIndex1Stretch = GetMuscleValue(_humanPose, "left", "index", "1", "stretch");
        metrics.LeftIndexSpread = GetMuscleValue(_humanPose, "left", "index", "spread");
        metrics.LeftMiddle1Stretch = GetMuscleValue(_humanPose, "left", "middle", "1", "stretch");
        metrics.LeftMiddleSpread = GetMuscleValue(_humanPose, "left", "middle", "spread");
        metrics.LeftRing1Stretch = GetMuscleValue(_humanPose, "left", "ring", "1", "stretch");
        metrics.LeftRingSpread = GetMuscleValue(_humanPose, "left", "ring", "spread");
        metrics.LeftLittle1Stretch = GetMuscleValue(_humanPose, "left", "little", "1", "stretch");
        metrics.LeftLittleSpread = GetMuscleValue(_humanPose, "left", "little", "spread");
        metrics.RightThumb1Stretch = GetMuscleValue(_humanPose, "right", "thumb", "1", "stretch");
        metrics.RightThumbSpread = GetMuscleValue(_humanPose, "right", "thumb", "spread");
        metrics.RightIndex1Stretch = GetMuscleValue(_humanPose, "right", "index", "1", "stretch");
        metrics.RightIndexSpread = GetMuscleValue(_humanPose, "right", "index", "spread");
        metrics.RightMiddle1Stretch = GetMuscleValue(_humanPose, "right", "middle", "1", "stretch");
        metrics.RightMiddleSpread = GetMuscleValue(_humanPose, "right", "middle", "spread");
        metrics.RightRing1Stretch = GetMuscleValue(_humanPose, "right", "ring", "1", "stretch");
        metrics.RightRingSpread = GetMuscleValue(_humanPose, "right", "ring", "spread");
        metrics.RightLittle1Stretch = GetMuscleValue(_humanPose, "right", "little", "1", "stretch");
        metrics.RightLittleSpread = GetMuscleValue(_humanPose, "right", "little", "spread");
        return metrics;
    }

    private YybDiagnosticMetrics CaptureYybDiagnosticMetrics(ArmMuscleMetrics armMuscles)
    {
        YybSideDiagnosticMetrics left = CaptureYybSideDiagnosticMetrics(false);
        YybSideDiagnosticMetrics right = CaptureYybSideDiagnosticMetrics(true);

        if (!_isYybDiagnosticTarget)
        {
            left.ClearYybOnlyRiskScores();
            right.ClearYybOnlyRiskScores();
            return new YybDiagnosticMetrics
            {
                Left = left,
                Right = right,
                MaxDeformationRisk = float.NaN
            };
        }

        left.ArmTwistRisk = CalculateArmTwistRisk(armMuscles.LeftArmTwist, armMuscles.LeftForearmTwist);
        right.ArmTwistRisk = CalculateArmTwistRisk(armMuscles.RightArmTwist, armMuscles.RightForearmTwist);
        left.SleeveAnchorRisk = CalculateSleeveAnchorRisk(false);
        right.SleeveAnchorRisk = CalculateSleeveAnchorRisk(true);
        left.SleeveThicknessRisk = CalculateSleeveThicknessRisk(false, out left.SleeveAnchorDistance, out left.SleeveThicknessRatio);
        right.SleeveThicknessRisk = CalculateSleeveThicknessRisk(true, out right.SleeveAnchorDistance, out right.SleeveThicknessRatio);
        float leftArmSleeveRisk = MaxFinite(
            CalculateArmSleeveDeformationRisk(left.ArmTwistRisk, left.SleeveAnchorRisk),
            left.SleeveThicknessRisk);
        float rightArmSleeveRisk = MaxFinite(
            CalculateArmSleeveDeformationRisk(right.ArmTwistRisk, right.SleeveAnchorRisk),
            right.SleeveThicknessRisk);
        left.DeformationRisk = MaxFinite(
            left.ThumbSpreadRisk,
            left.ThumbProjectionRisk,
            left.ThumbHelperSeparationRisk,
            left.WebbingRisk,
            leftArmSleeveRisk);
        right.DeformationRisk = MaxFinite(
            right.ThumbSpreadRisk,
            right.ThumbProjectionRisk,
            right.ThumbHelperSeparationRisk,
            right.WebbingRisk,
            rightArmSleeveRisk);

        return new YybDiagnosticMetrics
        {
            Left = left,
            Right = right,
            MaxDeformationRisk = MaxFinite(left.DeformationRisk, right.DeformationRisk)
        };
    }

    private YybSideDiagnosticMetrics CaptureYybSideDiagnosticMetrics(bool isRightSide)
    {
        YybSideDiagnosticMetrics metrics = YybSideDiagnosticMetrics.Empty;
        metrics.HelperCoverageRequired = RequiresExplicitThumbBaseHelperCoverage(isRightSide);

        if (TryCalculateThumbAndIndexDirections(isRightSide, out Vector3 thumbDirection, out Vector3 indexDirection))
        {
            metrics.ThumbDirectionAvailable = true;
            metrics.ThumbIndexSpreadAngle = Vector3.Angle(thumbDirection, indexDirection);
            metrics.ThumbSpreadRisk = RiskAbove(
                metrics.ThumbIndexSpreadAngle,
                DiagnosticThumbIndexMaxSpreadAngle,
                DiagnosticThumbIndexFullRiskAngle);

            if (TryBuildDiagnosticPalmFrame(isRightSide, out _, out Vector3 palmNormal, out _))
            {
                metrics.PalmFrameAvailable = true;
                metrics.ThumbPalmProjection = Vector3.Dot(thumbDirection, palmNormal);
                metrics.ThumbProjectionRisk = RiskOutsideRange(
                    metrics.ThumbPalmProjection,
                    DiagnosticThumbPalmProjectionMin,
                    DiagnosticThumbPalmProjectionMax,
                    1f);
            }
        }

        if (TryResolveExplicitThumbBaseHelperRelationship(isRightSide, out Transform helper, out Transform source))
        {
            metrics.HelperRelationshipAvailable = true;
            string distanceKey = MotionComparisonProbeReportWriter.BuildTransformPairKey(
                MotionComparisonProbeReportWriter.BuildThumbHelperDistancePairKeyLabel(isRightSide), helper, source);
            metrics.ThumbHelperSourceDistanceDelta = CalculateDistanceDeltaFromInitial(helper, source, distanceKey, out float distance);
            metrics.ThumbHelperSourceDistance = distance;

            string rotationKey = MotionComparisonProbeReportWriter.BuildTransformPairKey(
                MotionComparisonProbeReportWriter.BuildThumbHelperRotationPairKeyLabel(isRightSide), source, helper);
            metrics.ThumbHelperSourceRotationDelta = CalculateRelativeRotationDeltaFromInitial(source, helper, rotationKey);

            metrics.ThumbHelperSeparationRisk = MaxFinite(
                RiskAbove(
                    metrics.ThumbHelperSourceDistanceDelta,
                    DiagnosticThumbHelperDistanceDeltaWarning,
                    DiagnosticThumbHelperDistanceDeltaFullRisk),
                RiskAbove(
                    metrics.ThumbHelperSourceRotationDelta,
                    DiagnosticThumbHelperRotationWarning,
                    DiagnosticThumbHelperRotationFullRisk));
        }

        metrics.WebbingRisk = MaxFinite(
            metrics.ThumbProjectionRisk,
            metrics.ThumbSpreadRisk,
            RiskAbove(
                metrics.ThumbHelperSourceDistanceDelta,
                DiagnosticThumbHelperDistanceDeltaWarning,
                DiagnosticThumbHelperDistanceDeltaFullRisk),
            RiskAbove(
                metrics.ThumbHelperSourceRotationDelta,
                DiagnosticThumbWebbingRotationWarning,
                DiagnosticThumbWebbingRotationFullRisk));

        return metrics;
    }

    private bool TryResolveExplicitThumbBaseHelperRelationship(
        bool isRightSide,
        out Transform helper,
        out Transform source)
    {
        helper = null;
        source = null;

        if (!TryFindExplicitThumbBaseSource(isRightSide, out Transform explicitSource))
        {
            return false;
        }

        if (!TryFindThumbBaseHelperCandidate(isRightSide, out Transform helperCandidate))
        {
            return false;
        }

        if (helperCandidate == explicitSource)
        {
            return false;
        }

        helper = helperCandidate;
        source = explicitSource;
        return true;
    }

    private bool RequiresExplicitThumbBaseHelperCoverage(bool isRightSide)
    {
        return TryFindThumbBaseHelperCandidate(isRightSide, out _);
    }

    private bool TryFindExplicitThumbBaseSource(bool isRightSide, out Transform source)
    {
        source = FindDiagnosticTransform(MotionComparisonProbeReportWriter.BuildExplicitThumbBaseSourceCacheKey(isRightSide), candidate =>
            MotionComparisonProbeReportWriter.MatchesActiveThumbBaseSourceTransformName(candidate.name, isRightSide));
        return source != null;
    }

    private bool TryFindThumbBaseHelperCandidate(bool isRightSide, out Transform helper)
    {
        helper = FindThumbBaseHelper(isRightSide);
        if (helper != null)
        {
            return true;
        }

        Transform hand = GetBone(isRightSide ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        if (hand == null)
        {
            return false;
        }

        Transform thumbProximal = GetBone(isRightSide ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
        Transform thumbIntermediate = GetBone(isRightSide ? HumanBodyBones.RightThumbIntermediate : HumanBodyBones.LeftThumbIntermediate);
        Transform thumbDistal = GetBone(isRightSide ? HumanBodyBones.RightThumbDistal : HumanBodyBones.LeftThumbDistal);
        Transform explicitSource = null;
        TryFindExplicitThumbBaseSource(isRightSide, out explicitSource);

        float bestDistance = float.PositiveInfinity;
        foreach (Transform candidate in hand.GetComponentsInChildren<Transform>(true))
        {
            if (!IsAmbiguousThumbExtraTransformCandidate(candidate, hand, thumbProximal, thumbIntermediate, thumbDistal))
            {
                continue;
            }

            float distance = explicitSource != null
                ? (candidate.position - explicitSource.position).sqrMagnitude
                : thumbProximal != null
                    ? (candidate.position - thumbProximal.position).sqrMagnitude
                    : (candidate.position - hand.position).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            helper = candidate;
        }

        return helper != null;
    }

    private bool IsAmbiguousThumbExtraTransformCandidate(
        Transform candidate,
        Transform hand,
        Transform thumbProximal,
        Transform thumbIntermediate,
        Transform thumbDistal)
    {
        if (candidate == null || candidate == hand || candidate == thumbProximal || candidate == thumbIntermediate || candidate == thumbDistal)
        {
            return false;
        }

        if (!MotionComparisonProbeReportWriter.MatchesAmbiguousThumbExtraTransformCandidateName(candidate.name))
        {
            return false;
        }

        if (IsAncestorWithinHand(candidate, thumbProximal, hand) ||
            IsAncestorWithinHand(candidate, thumbIntermediate, hand) ||
            IsAncestorWithinHand(candidate, thumbDistal, hand) ||
            IsAncestorWithinHand(thumbProximal, candidate, hand) ||
            IsAncestorWithinHand(thumbIntermediate, candidate, hand) ||
            IsAncestorWithinHand(thumbDistal, candidate, hand))
        {
            return false;
        }

        return true;
    }

    private static bool IsAncestorWithinHand(Transform ancestor, Transform descendant, Transform hand)
    {
        if (ancestor == null || descendant == null || hand == null || ancestor == descendant)
        {
            return false;
        }

        Transform current = descendant.parent;
        while (current != null)
        {
            if (current == ancestor)
            {
                return true;
            }

            if (current == hand)
            {
                break;
            }

            current = current.parent;
        }

        return false;
    }

    private void UpdateRealtimeRiskSummary()
    {
        AnimationTimeMetrics animationTime = CaptureAnimationTimeMetrics();
        int recorderFrame = _recorder != null ? _recorder.FrameNumber : -1;
        YybDiagnosticMetrics diagnostics = captureYybDiagnosticOnlyMetrics
            ? CaptureYybDiagnosticMetrics(CaptureArmMuscles())
            : YybDiagnosticMetrics.Empty;
        UpdateRiskSummary(
            diagnostics,
            true,
            MotionComparisonProbeReportWriter.BuildRealtimeRiskEvaluationReason(),
            animationTime.ClipTime,
            recorderFrame);
    }

    private void ResetRiskSummary()
    {
        _maxThumbSpreadRisk = float.NaN;
        _maxThumbProjectionRisk = float.NaN;
        _maxThumbHelperSeparationRisk = float.NaN;
        _maxThumbWebbingRisk = float.NaN;
        _maxGenericThumbAnatomyRisk = float.NaN;
        _maxYybDeformationRisk = float.NaN;
        _maxGenericThumbAnatomyRiskClipTime = float.NaN;
        _maxYybDeformationRiskClipTime = float.NaN;
        _maxGenericThumbAnatomyRiskRecorderFrame = -1;
        _maxYybDeformationRiskRecorderFrame = -1;
        _maxGenericThumbAnatomyRiskReason = "";
        _maxYybDeformationRiskReason = "";
        _riskEvaluationFrameCount = 0;
        _leftCoreThumbDiagnosticFrameCount = 0;
        _rightCoreThumbDiagnosticFrameCount = 0;
        _leftHelperRelationshipFrameCount = 0;
        _rightHelperRelationshipFrameCount = 0;
        _leftHelperCoverageRequired = false;
        _rightHelperCoverageRequired = false;
    }

    private void UpdateRiskSummary(
        YybDiagnosticMetrics diagnostics,
        bool countEvaluationFrame,
        string reason,
        float animationClipTime,
        int recorderFrame)
    {
        if (countEvaluationFrame)
        {
            _riskEvaluationFrameCount++;
            if (diagnostics.Left.HasCoreThumbAnatomy)
            {
                _leftCoreThumbDiagnosticFrameCount++;
            }

            if (diagnostics.Right.HasCoreThumbAnatomy)
            {
                _rightCoreThumbDiagnosticFrameCount++;
            }

            if (diagnostics.Left.HelperRelationshipAvailable)
            {
                _leftHelperRelationshipFrameCount++;
            }

            if (diagnostics.Right.HelperRelationshipAvailable)
            {
                _rightHelperRelationshipFrameCount++;
            }

            _leftHelperCoverageRequired |= diagnostics.Left.HelperCoverageRequired;
            _rightHelperCoverageRequired |= diagnostics.Right.HelperCoverageRequired;
        }

        float genericThumbRisk = MaxFinite(
            diagnostics.Left.ThumbSpreadRisk,
            diagnostics.Right.ThumbSpreadRisk,
            diagnostics.Left.ThumbProjectionRisk,
            diagnostics.Right.ThumbProjectionRisk,
            diagnostics.Left.ThumbHelperSeparationRisk,
            diagnostics.Right.ThumbHelperSeparationRisk,
            diagnostics.Left.WebbingRisk,
            diagnostics.Right.WebbingRisk);

        _maxThumbSpreadRisk = MaxFinite(
            _maxThumbSpreadRisk,
            diagnostics.Left.ThumbSpreadRisk,
            diagnostics.Right.ThumbSpreadRisk);
        _maxThumbProjectionRisk = MaxFinite(
            _maxThumbProjectionRisk,
            diagnostics.Left.ThumbProjectionRisk,
            diagnostics.Right.ThumbProjectionRisk);
        _maxThumbHelperSeparationRisk = MaxFinite(
            _maxThumbHelperSeparationRisk,
            diagnostics.Left.ThumbHelperSeparationRisk,
            diagnostics.Right.ThumbHelperSeparationRisk);
        _maxThumbWebbingRisk = MaxFinite(
            _maxThumbWebbingRisk,
            diagnostics.Left.WebbingRisk,
            diagnostics.Right.WebbingRisk);
        if (IsFinite(genericThumbRisk) &&
            (!IsFinite(_maxGenericThumbAnatomyRisk) || genericThumbRisk >= _maxGenericThumbAnatomyRisk))
        {
            _maxGenericThumbAnatomyRiskClipTime = animationClipTime;
            _maxGenericThumbAnatomyRiskRecorderFrame = recorderFrame;
            _maxGenericThumbAnatomyRiskReason = reason ?? "";
        }

        _maxGenericThumbAnatomyRisk = MaxFinite(_maxGenericThumbAnatomyRisk, genericThumbRisk);
        if (IsFinite(diagnostics.MaxDeformationRisk) &&
            (!IsFinite(_maxYybDeformationRisk) || diagnostics.MaxDeformationRisk >= _maxYybDeformationRisk))
        {
            _maxYybDeformationRiskClipTime = animationClipTime;
            _maxYybDeformationRiskRecorderFrame = recorderFrame;
            _maxYybDeformationRiskReason = reason ?? "";
        }

        _maxYybDeformationRisk = MaxFinite(_maxYybDeformationRisk, diagnostics.MaxDeformationRisk);
    }

    private bool IsYybDiagnosticTarget()
    {
        return MotionComparisonProbeReportWriter.MatchesYybModelName(gameObject.name) ||
            MotionComparisonProbeReportWriter.MatchesYybModelName(comparisonLabel);
    }

    private bool TryCalculateThumbAndIndexDirections(
        bool isRightSide,
        out Vector3 thumbDirection,
        out Vector3 indexDirection)
    {
        thumbDirection = Vector3.zero;
        indexDirection = Vector3.zero;

        Transform hand = GetBone(isRightSide ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        Transform thumbProximal = GetBone(isRightSide ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
        Transform thumbIntermediate = GetBone(isRightSide ? HumanBodyBones.RightThumbIntermediate : HumanBodyBones.LeftThumbIntermediate);
        Transform indexProximal = GetBone(isRightSide ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        Transform indexIntermediate = GetBone(isRightSide ? HumanBodyBones.RightIndexIntermediate : HumanBodyBones.LeftIndexIntermediate);

        if (thumbProximal != null && thumbIntermediate != null)
        {
            thumbDirection = thumbIntermediate.position - thumbProximal.position;
        }
        else if (hand != null && thumbProximal != null)
        {
            thumbDirection = thumbProximal.position - hand.position;
        }

        if (hand != null && indexProximal != null)
        {
            indexDirection = indexProximal.position - hand.position;
        }
        else if (indexProximal != null && indexIntermediate != null)
        {
            indexDirection = indexIntermediate.position - indexProximal.position;
        }

        return TryNormalize(thumbDirection, out thumbDirection) &&
            TryNormalize(indexDirection, out indexDirection);
    }

    private bool TryBuildDiagnosticPalmFrame(
        bool isRightSide,
        out Vector3 sideAxis,
        out Vector3 palmNormal,
        out Vector3 forwardAxis)
    {
        sideAxis = Vector3.zero;
        palmNormal = Vector3.zero;
        forwardAxis = Vector3.zero;

        Transform hand = GetBone(isRightSide ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        Transform index = GetBone(isRightSide ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        Transform middle = GetBone(isRightSide ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
        Transform little = GetBone(isRightSide ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
        if (hand == null || index == null || middle == null || little == null)
        {
            return false;
        }

        Vector3 rawSide = index.position - little.position;
        if (isRightSide)
        {
            rawSide = -rawSide;
        }

        Vector3 rawForward = ((index.position + middle.position + little.position) / 3f) - hand.position;
        return TryNormalize(rawSide, out sideAxis) &&
            TryNormalize(rawForward, out forwardAxis) &&
            TryNormalize(Vector3.Cross(sideAxis, forwardAxis), out palmNormal) &&
            TryNormalize(Vector3.Cross(palmNormal, sideAxis), out forwardAxis);
    }

    private Transform FindThumbBaseHelper(bool isRightSide)
    {
        return FindDiagnosticTransform(MotionComparisonProbeReportWriter.BuildThumbBaseHelperCacheKey(isRightSide), candidate =>
            MotionComparisonProbeReportWriter.MatchesDetachedThumbBaseHelperTransformName(candidate.name, isRightSide));
    }

    private Transform FindThumbBaseSource(bool isRightSide)
    {
        Transform source = FindDiagnosticTransform(MotionComparisonProbeReportWriter.BuildThumbBaseSourceCacheKey(isRightSide), candidate =>
            MotionComparisonProbeReportWriter.MatchesActiveThumbBaseSourceTransformName(candidate.name, isRightSide));

        if (source != null)
        {
            return source;
        }

        return GetBone(isRightSide ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
    }

    private Transform FindSleeveAnchor(bool isRightSide)
    {
        return FindDiagnosticTransform(MotionComparisonProbeReportWriter.BuildSleeveAnchorTransformCacheKey(isRightSide), candidate =>
            MotionComparisonProbeReportWriter.MatchesSleeveAnchorTransformName(candidate.name, isRightSide));
    }

    private Transform FindDiagnosticTransform(string cacheKey, Func<Transform, bool> predicate)
    {
        if (_animator == null || _animator.gameObject == null || string.IsNullOrEmpty(cacheKey) || predicate == null)
        {
            return null;
        }

        if (_diagnosticTransformCache.TryGetValue(cacheKey, out Transform cachedTransform))
        {
            return cachedTransform;
        }

        foreach (Transform candidate in _animator.gameObject.GetComponentsInChildren<Transform>(true))
        {
            if (candidate != null && predicate(candidate))
            {
                _diagnosticTransformCache[cacheKey] = candidate;
                return candidate;
            }
        }

        _diagnosticTransformCache[cacheKey] = null;
        return null;
    }

    private float CalculateArmTwistRisk(float armTwistMuscle, float forearmTwistMuscle)
    {
        return MaxFinite(
            RiskMagnitude(
                armTwistMuscle,
                DiagnosticArmTwistWarningMuscle,
                DiagnosticArmTwistFullRiskMuscle),
            RiskMagnitude(
                forearmTwistMuscle,
                DiagnosticArmTwistWarningMuscle,
                DiagnosticArmTwistFullRiskMuscle));
    }

    private static float CalculateArmSleeveDeformationRisk(float armTwistRisk, float sleeveAnchorRisk)
    {
        if (!IsFinite(armTwistRisk))
        {
            return sleeveAnchorRisk;
        }

        if (!IsFinite(sleeveAnchorRisk))
        {
            return armTwistRisk;
        }

        return Mathf.Clamp01(sleeveAnchorRisk + (sleeveAnchorRisk * armTwistRisk));
    }

    private float CalculateSleeveAnchorRisk(bool isRightSide)
    {
        Transform source = GetBone(isRightSide ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        Transform anchor = FindSleeveAnchor(isRightSide);
        if (source == null || anchor == null)
        {
            return float.NaN;
        }

        string key = MotionComparisonProbeReportWriter.BuildTransformPairKey(
            MotionComparisonProbeReportWriter.BuildSleeveAnchorRotationPairKeyLabel(isRightSide), source, anchor);
        float rotationDelta = CalculateRelativeRotationDeltaFromInitial(source, anchor, key);
        return RiskAbove(
            rotationDelta,
            DiagnosticSleeveAnchorWarningDegrees,
            DiagnosticSleeveAnchorFullRiskDegrees);
    }

    private float CalculateSleeveThicknessRisk(bool isRightSide, out float distance, out float thicknessRatio)
    {
        distance = float.NaN;
        thicknessRatio = float.NaN;

        Transform source = GetBone(isRightSide ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
        Transform anchor = FindSleeveAnchor(isRightSide);
        if (source == null || anchor == null)
        {
            return float.NaN;
        }

        string key = MotionComparisonProbeReportWriter.BuildTransformPairKey(
            MotionComparisonProbeReportWriter.BuildSleeveThicknessPairKeyLabel(isRightSide), source, anchor);
        thicknessRatio = CalculateDistanceRatioFromInitial(source, anchor, key, out distance);
        return RiskBelow(
            thicknessRatio,
            DiagnosticSleeveThicknessWarningRatio,
            DiagnosticSleeveThicknessFullRiskRatio);
    }

    private float CalculateDistanceDeltaFromInitial(Transform a, Transform b, string key, out float distance)
    {
        distance = float.NaN;
        if (a == null || b == null || string.IsNullOrEmpty(key))
        {
            return float.NaN;
        }

        distance = Vector3.Distance(a.position, b.position);
        if (!IsFinite(distance))
        {
            return float.NaN;
        }

        if (!_diagnosticInitialDistances.TryGetValue(key, out float initialDistance))
        {
            _diagnosticInitialDistances[key] = distance;
            return 0f;
        }

        return Mathf.Abs(distance - initialDistance);
    }

    private float CalculateDistanceRatioFromInitial(Transform a, Transform b, string key, out float distance)
    {
        distance = float.NaN;
        if (a == null || b == null || string.IsNullOrEmpty(key))
        {
            return float.NaN;
        }

        distance = Vector3.Distance(a.position, b.position);
        if (!IsFinite(distance))
        {
            return float.NaN;
        }

        if (!_diagnosticInitialDistances.TryGetValue(key, out float initialDistance))
        {
            _diagnosticInitialDistances[key] = distance;
            return 1f;
        }

        if (!IsFinite(initialDistance) || initialDistance <= 0.000001f)
        {
            return float.NaN;
        }

        return distance / initialDistance;
    }

    private float CalculateRelativeRotationDeltaFromInitial(Transform source, Transform target, string key)
    {
        if (source == null || target == null || string.IsNullOrEmpty(key))
        {
            return float.NaN;
        }

        Quaternion relativeRotation = Quaternion.Inverse(source.rotation) * target.rotation;
        if (!IsFinite(relativeRotation))
        {
            return float.NaN;
        }

        if (!_diagnosticInitialRelativeRotations.TryGetValue(key, out Quaternion initialRelativeRotation))
        {
            _diagnosticInitialRelativeRotations[key] = relativeRotation;
            return 0f;
        }

        return Quaternion.Angle(initialRelativeRotation, relativeRotation);
    }

    private void ResetDiagnosticBaselines()
    {
        _diagnosticTransformCache.Clear();
        _diagnosticInitialDistances.Clear();
        _diagnosticInitialRelativeRotations.Clear();
    }

    private static bool TryNormalize(Vector3 value, out Vector3 normalized)
    {
        normalized = Vector3.zero;
        if (!IsFinite(value) || value.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        normalized = value.normalized;
        return IsFinite(normalized);
    }

    private static float RiskAbove(float value, float warningValue, float fullRiskValue)
    {
        if (!IsFinite(value))
        {
            return float.NaN;
        }

        if (value <= warningValue)
        {
            return 0f;
        }

        if (fullRiskValue <= warningValue)
        {
            return 1f;
        }

        return Mathf.Clamp01((value - warningValue) / (fullRiskValue - warningValue));
    }

    private static float RiskBelow(float value, float warningValue, float fullRiskValue)
    {
        if (!IsFinite(value))
        {
            return float.NaN;
        }

        if (value >= warningValue)
        {
            return 0f;
        }

        if (warningValue <= fullRiskValue)
        {
            return 1f;
        }

        return Mathf.Clamp01((warningValue - value) / (warningValue - fullRiskValue));
    }

    private static float RiskMagnitude(float value, float warningValue, float fullRiskValue)
    {
        return RiskAbove(Mathf.Abs(value), warningValue, fullRiskValue);
    }

    private static float RiskOutsideRange(float value, float minValue, float maxValue, float fullRiskDistance)
    {
        if (!IsFinite(value))
        {
            return float.NaN;
        }

        if (value < minValue)
        {
            return RiskAbove(minValue - value, 0f, fullRiskDistance);
        }

        if (value > maxValue)
        {
            return RiskAbove(value - maxValue, 0f, fullRiskDistance);
        }

        return 0f;
    }

    private static float MaxFinite(params float[] values)
    {
        float result = float.NaN;
        if (values == null)
        {
            return result;
        }

        foreach (float value in values)
        {
            if (!IsFinite(value))
            {
                continue;
            }

            result = IsFinite(result) ? Mathf.Max(result, value) : value;
        }

        return result;
    }

    private static float MinFinite(params float[] values)
    {
        float result = float.NaN;
        if (values == null)
        {
            return result;
        }

        foreach (float value in values)
        {
            if (!IsFinite(value))
            {
                continue;
            }

            result = IsFinite(result) ? Mathf.Min(result, value) : value;
        }

        return result;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }

    private float GetMuscleValue(HumanPose pose, params string[] tokens)
    {
        int index = FindMuscleIndex(tokens);
        if (index < 0 || pose.muscles == null || index >= pose.muscles.Length)
        {
            if (!_poseWarningLogged)
            {
                Debug.LogWarning(MotionComparisonProbeReportWriter.BuildMissingHumanoidArmMusclesWarningMessage());
                _poseWarningLogged = true;
            }

            return float.NaN;
        }

        return pose.muscles[index];
    }

    private static int FindMuscleIndex(params string[] tokens)
    {
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
        {
            string muscleName = NormalizeMuscleName(HumanTrait.MuscleName[i]);
            bool matched = true;
            foreach (string token in tokens)
            {
                if (!muscleName.Contains(NormalizeMuscleName(token)))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }

    private static string NormalizeMuscleName(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : value.Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
    }

    private float CalculateCameraFacingDot(Transform root)
    {
        if (_camera == null || root == null)
        {
            return float.NaN;
        }

        Vector3 toCamera = _camera.transform.position - root.position;
        if (toCamera.sqrMagnitude <= 0.000001f)
        {
            return float.NaN;
        }

        return Vector3.Dot(root.forward, toCamera.normalized);
    }

    private float CalculateMaxScaleDelta()
    {
        HumanBodyBones[] bones =
        {
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot
        };

        float maxDelta = 0f;
        foreach (HumanBodyBones bone in bones)
        {
            Transform target = GetBone(bone);
            if (target == null)
            {
                continue;
            }

            Vector3 scale = target.localScale;
            maxDelta = Mathf.Max(maxDelta, Mathf.Abs(scale.x - 1f), Mathf.Abs(scale.y - 1f), Mathf.Abs(scale.z - 1f));
        }

        return maxDelta;
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private Vector3 GetLocalScale(HumanBodyBones bone)
    {
        Transform target = GetBone(bone);
        return target != null ? target.localScale : EmptyVector();
    }

    private Vector3 GetLocalEuler(HumanBodyBones bone)
    {
        Transform target = GetBone(bone);
        return target != null ? NormalizeEuler(target.localEulerAngles) : EmptyVector();
    }

    private static Vector3 EmptyVector()
    {
        return new Vector3(float.NaN, float.NaN, float.NaN);
    }

    private static Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(
            NormalizeAngle(euler.x),
            NormalizeAngle(euler.y),
            NormalizeAngle(euler.z));
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    private float CalculateChainLength(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c)
    {
        Transform first = GetBone(a);
        Transform second = GetBone(b);
        Transform third = GetBone(c);
        if (first == null || second == null || third == null)
        {
            return float.NaN;
        }

        return Vector3.Distance(first.position, second.position) + Vector3.Distance(second.position, third.position);
    }

    private float CalculateJointAngle(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c)
    {
        Transform first = GetBone(a);
        Transform second = GetBone(b);
        Transform third = GetBone(c);
        if (first == null || second == null || third == null)
        {
            return float.NaN;
        }

        Vector3 upper = first.position - second.position;
        Vector3 lower = third.position - second.position;
        if (upper.sqrMagnitude <= 0.000001f || lower.sqrMagnitude <= 0.000001f)
        {
            return float.NaN;
        }

        return Vector3.Angle(upper, lower);
    }

    private float CalculateBendForwardDot(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c, Transform root)
    {
        Transform first = GetBone(a);
        Transform second = GetBone(b);
        Transform third = GetBone(c);
        if (first == null || second == null || third == null || root == null)
        {
            return float.NaN;
        }

        Vector3 upper = first.position - second.position;
        Vector3 lower = third.position - second.position;
        Vector3 normal = Vector3.Cross(upper, lower);
        if (normal.sqrMagnitude <= 0.000001f)
        {
            return float.NaN;
        }

        return Vector3.Dot(normal.normalized, root.forward);
    }

    private float CalculateBendOffsetForwardDot(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c, Transform root)
    {
        Transform first = GetBone(a);
        Transform second = GetBone(b);
        Transform third = GetBone(c);
        if (first == null || second == null || third == null || root == null)
        {
            return float.NaN;
        }

        Vector3 chain = third.position - first.position;
        if (chain.sqrMagnitude <= 0.000001f)
        {
            return float.NaN;
        }

        float t = Vector3.Dot(second.position - first.position, chain) / chain.sqrMagnitude;
        Vector3 closestPointOnChain = first.position + chain * Mathf.Clamp01(t);
        Vector3 bendOffset = second.position - closestPointOnChain;
        if (bendOffset.sqrMagnitude <= 0.000001f)
        {
            return 0f;
        }

        return Vector3.Dot(bendOffset.normalized, root.forward);
    }

    private float CalculateUpperArmDownDot(HumanBodyBones upperBone, HumanBodyBones lowerBone, Transform root)
    {
        Transform upper = GetBone(upperBone);
        Transform lower = GetBone(lowerBone);
        if (upper == null || lower == null || root == null)
        {
            return float.NaN;
        }

        Vector3 upperToLower = lower.position - upper.position;
        if (upperToLower.sqrMagnitude <= 0.000001f)
        {
            return float.NaN;
        }

        Vector3 localDirection = root.InverseTransformDirection(upperToLower.normalized);
        return Mathf.Clamp01(-localDirection.y);
    }

    private float CalculateHandHorizontalRatio(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Transform root)
    {
        if (!TryGetArmOffsetRatios(upperBone, lowerBone, handBone, root, out float horizontalRatio, out _))
        {
            return float.NaN;
        }

        return horizontalRatio;
    }

    private float CalculateHandBelowShoulderRatio(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Transform root)
    {
        if (!TryGetArmOffsetRatios(upperBone, lowerBone, handBone, root, out _, out float belowShoulderRatio))
        {
            return float.NaN;
        }

        return belowShoulderRatio;
    }

    private bool TryGetArmOffsetRatios(
        HumanBodyBones upperBone,
        HumanBodyBones lowerBone,
        HumanBodyBones handBone,
        Transform root,
        out float horizontalRatio,
        out float belowShoulderRatio)
    {
        horizontalRatio = float.NaN;
        belowShoulderRatio = float.NaN;

        Transform upper = GetBone(upperBone);
        Transform lower = GetBone(lowerBone);
        Transform hand = GetBone(handBone);
        if (upper == null || lower == null || hand == null || root == null)
        {
            return false;
        }

        float armLength = Vector3.Distance(upper.position, lower.position) +
                          Vector3.Distance(lower.position, hand.position);
        if (armLength <= 0.000001f)
        {
            return false;
        }

        Vector3 localOffset = root.InverseTransformPoint(hand.position) -
                              root.InverseTransformPoint(upper.position);
        horizontalRatio = new Vector2(localOffset.x, localOffset.z).magnitude / armLength;
        belowShoulderRatio = Mathf.Max(0f, -localOffset.y) / armLength;
        return true;
    }

    private HandTorsoClearanceMetrics CaptureHandTorsoClearanceMetrics(Transform root)
    {
        float left = CalculateHandTorsoSignedClearance(LeftFingerBones, root);
        float right = CalculateHandTorsoSignedClearance(RightFingerBones, root);
        float minClearance = MinFinite(left, right);
        float penetrationDepth = IsFinite(minClearance) ? Mathf.Max(0f, -minClearance) : float.NaN;
        return new HandTorsoClearanceMetrics
        {
            LeftSignedClearance = left,
            RightSignedClearance = right,
            MinSignedClearance = minClearance,
            PenetrationRisk = IsFinite(penetrationDepth) ? RiskAbove(penetrationDepth, 0.015f, 0.08f) : float.NaN
        };
    }

    private float CalculateHandTorsoSignedClearance(HumanBodyBones[] handBones, Transform root)
    {
        Transform hips = GetBone(HumanBodyBones.Hips);
        Transform chest = GetBone(HumanBodyBones.Chest) ?? GetBone(HumanBodyBones.UpperChest) ?? GetBone(HumanBodyBones.Spine);
        Transform leftShoulder = GetBone(HumanBodyBones.LeftShoulder) ?? GetBone(HumanBodyBones.LeftUpperArm);
        Transform rightShoulder = GetBone(HumanBodyBones.RightShoulder) ?? GetBone(HumanBodyBones.RightUpperArm);
        if (root == null || hips == null || chest == null || leftShoulder == null || rightShoulder == null || handBones == null)
        {
            return float.NaN;
        }

        Vector3 localHips = root.InverseTransformPoint(hips.position);
        Vector3 localChest = root.InverseTransformPoint(chest.position);
        float yMin = Mathf.Min(localHips.y, localChest.y);
        float yMax = Mathf.Max(localHips.y, localChest.y);
        float shoulderWidth = Vector3.Distance(
            root.InverseTransformPoint(leftShoulder.position),
            root.InverseTransformPoint(rightShoulder.position));
        float radiusX = Mathf.Max(0.05f, shoulderWidth * 0.42f);
        float radiusZ = Mathf.Max(0.035f, shoulderWidth * 0.24f);
        float signedClearance = float.NaN;

        foreach (HumanBodyBones handBone in handBones)
        {
            Transform bone = GetBone(handBone);
            if (bone == null)
            {
                continue;
            }

            Vector3 point = root.InverseTransformPoint(bone.position);
            float dy = point.y < yMin ? yMin - point.y : point.y > yMax ? point.y - yMax : 0f;
            float nx = point.x / radiusX;
            float nz = point.z / radiusZ;
            float radialClearance = (Mathf.Sqrt(nx * nx + nz * nz) - 1f) * Mathf.Min(radiusX, radiusZ);
            float pointClearance = dy > 0f
                ? Mathf.Sqrt(radialClearance * radialClearance + dy * dy)
                : radialClearance;
            signedClearance = IsFinite(signedClearance) ? Mathf.Min(signedClearance, pointClearance) : pointClearance;
        }

        return signedClearance;
    }

    private Transform GetBone(HumanBodyBones bone)
    {
        return _animator != null ? _animator.GetBoneTransform(bone) : null;
    }

    private void PrepareSessionOutput()
    {
        _sessionFolder = "";
        _sessionManifestPath = "";
        _screenshotFolder = "";
        _screenshotIndexPath = "";
        _screenshotSessionIndexPath = "";
        _nonBlankScreenshotCount = 0;

        if (string.IsNullOrWhiteSpace(_sessionId))
        {
            return;
        }

        MotionComparisonProbeSessionArtifactOutputPaths paths =
            MotionComparisonProbeOutputPaths.BuildSessionArtifactOutputPaths(
                Application.dataPath,
                _sessionStamp,
                _sessionId,
                _csvPath,
                captureSampleScreenshots);
        _sessionFolder = paths.SessionFolder;
        _sessionManifestPath = paths.SessionManifestPath;

        if (!captureSampleScreenshots || string.IsNullOrEmpty(paths.ScreenshotFolder))
        {
            return;
        }

        _screenshotFolder = paths.ScreenshotFolder;
        _screenshotIndexPath = paths.ScreenshotIndexPath;
        _screenshotSessionIndexPath = paths.ScreenshotSessionIndexPath;
        MotionComparisonProbeReportWriter.WriteScreenshotSessionFiles(
            _screenshotIndexPath,
            _screenshotSessionIndexPath,
            paths.FrameSessionIndexData);
    }

    private void CaptureSampleScreenshots(string reason, PoseMetrics metrics)
    {
        if (!captureSampleScreenshots || string.IsNullOrEmpty(_screenshotFolder))
        {
            return;
        }

        if (Application.isBatchMode)
        {
            CaptureSampleScreenshotsNow(reason, metrics);
            return;
        }

        StartCoroutine(CaptureSampleScreenshotsAtEndOfFrame(reason, metrics));
    }

    private IEnumerator CaptureSampleScreenshotsAtEndOfFrame(string reason, PoseMetrics metrics)
    {
        yield return new WaitForEndOfFrame();

        CaptureSampleScreenshotsNow(reason, metrics);
    }

    private void CaptureSampleScreenshotsNow(string reason, PoseMetrics metrics)
    {
        if (!captureSampleScreenshots || string.IsNullOrEmpty(_screenshotFolder))
        {
            return;
        }

        if (!TryCalculateRenderBounds(out Bounds bounds))
        {
            Debug.LogWarning(MotionComparisonProbeReportWriter.BuildScreenshotBoundsUnavailableWarningMessage(comparisonLabel, reason));
            return;
        }

        MotionComparisonProbeScreenshotCaptureNames captureNames =
            MotionComparisonProbeOutputPaths.BuildScreenshotCaptureNames(metrics.RecorderFrame, Time.frameCount);

        CaptureView(bounds, transform.forward, reason, captureNames.FrameName, captureNames.FrontViewName, metrics);
        CaptureView(bounds, transform.right, reason, captureNames.FrameName, captureNames.RightViewName, metrics);
        CaptureFingerCloseups(reason, captureNames, metrics);
    }

    private void CaptureView(Bounds bounds, Vector3 viewDirection, string reason, string frameName, string viewName, PoseMetrics metrics, float paddingOverride = -1f)
    {
        if (viewDirection.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Camera captureCamera = EnsureCaptureCamera();
        if (captureCamera == null)
        {
            return;
        }

        Vector3 normalizedDirection = viewDirection.normalized;
        float distance = Mathf.Max(bounds.size.magnitude * 2f, 2f);
        captureCamera.transform.position = bounds.center + normalizedDirection * distance;
        captureCamera.transform.rotation = Quaternion.LookRotation(-normalizedDirection, Vector3.up);
        captureCamera.orthographic = true;
        captureCamera.orthographicSize = CalculateOrthographicSize(bounds, captureCamera);
        if (paddingOverride > 0f)
        {
            captureCamera.orthographicSize = CalculateOrthographicSize(bounds, captureCamera, paddingOverride);
        }
        else
        {
            float verticalOffset = (0.5f - screenshotVerticalViewportCenter) * 2f * captureCamera.orthographicSize;
            captureCamera.transform.position += Vector3.up * verticalOffset;
        }

        captureCamera.nearClipPlane = 0.01f;
        captureCamera.farClipPlane = distance + bounds.size.magnitude + 10f;
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);

        MotionComparisonProbeScreenshotCaptureOutputPaths outputPaths =
            MotionComparisonProbeOutputPaths.BuildScreenshotCaptureOutputPaths(
                Application.dataPath,
                _screenshotFolder,
                comparisonLabel,
                SceneManager.GetActiveScene().name,
                reason,
                metrics.RecorderFrame,
                viewName,
                frameName);

        if (!RenderCameraToPng(captureCamera, outputPaths.ScreenshotPath))
        {
            Debug.LogWarning(MotionComparisonProbeReportWriter.BuildScreenshotBlankWarningMessage(outputPaths.ScreenshotPath));
            return;
        }

        _nonBlankScreenshotCount++;
        MotionComparisonProbeReportWriter.AppendScreenshotIndexRow(
            _screenshotIndexPath,
            outputPaths.IndexRow);
    }

    private void CaptureFingerCloseups(string reason, MotionComparisonProbeScreenshotCaptureNames captureNames, PoseMetrics metrics)
    {
        if (!captureFingerCloseups)
        {
            return;
        }

        if (TryCalculateFingerBounds(true, out Bounds leftHandBounds))
        {
            CaptureView(leftHandBounds, transform.forward, reason, captureNames.FrameName, captureNames.LeftHandFrontViewName, metrics, fingerCloseupPadding);
            CaptureView(leftHandBounds, transform.right, reason, captureNames.FrameName, captureNames.LeftHandRightViewName, metrics, fingerCloseupPadding);
        }

        if (TryCalculateFingerBounds(false, out Bounds rightHandBounds))
        {
            CaptureView(rightHandBounds, transform.forward, reason, captureNames.FrameName, captureNames.RightHandFrontViewName, metrics, fingerCloseupPadding);
            CaptureView(rightHandBounds, transform.right, reason, captureNames.FrameName, captureNames.RightHandRightViewName, metrics, fingerCloseupPadding);
        }
    }

    private Camera EnsureCaptureCamera()
    {
        if (_captureCamera != null)
        {
            return _captureCamera;
        }

        GameObject cameraObject = new GameObject(MotionComparisonProbeReportWriter.BuildCaptureCameraObjectName(comparisonLabel));
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        _captureCamera = cameraObject.AddComponent<Camera>();
        _captureCamera.enabled = false;

        _captureCamera.cullingMask = ~0;
        _captureCamera.allowHDR = _camera != null && _camera.allowHDR;
        _captureCamera.allowMSAA = _camera != null && _camera.allowMSAA;

        return _captureCamera;
    }

    private void DestroyCaptureCamera()
    {
        if (_captureCamera == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_captureCamera.gameObject);
        }
        else
        {
            DestroyImmediate(_captureCamera.gameObject);
        }

        _captureCamera = null;
    }

    private bool TryCalculateRenderBounds(out Bounds bounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        bounds = new Bounds(transform.position + Vector3.up, Vector3.one);

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null || !targetRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }

        if (hasBounds && bounds.size.sqrMagnitude > 0.000001f)
        {
            return true;
        }

        Transform hips = GetBone(HumanBodyBones.Hips);
        if (hips != null)
        {
            bounds = new Bounds(hips.position, new Vector3(1f, 2f, 1f));
            return true;
        }

        return false;
    }

    private float CalculateOrthographicSize(Bounds bounds, Camera captureCamera)
    {
        return CalculateOrthographicSize(bounds, captureCamera, screenshotPadding);
    }

    private float CalculateOrthographicSize(Bounds bounds, Camera captureCamera, float padding)
    {
        float aspect = screenshotWidth > 0 && screenshotHeight > 0
            ? (float)screenshotWidth / screenshotHeight
            : 1f;
        float verticalSize = bounds.extents.y;
        float horizontalSize = Mathf.Max(bounds.extents.x, bounds.extents.z) / Mathf.Max(aspect, 0.0001f);
        return Mathf.Max(0.08f, Mathf.Max(verticalSize, horizontalSize) * padding);
    }

    private bool TryCalculateFingerBounds(bool leftSide, out Bounds bounds)
    {
        HumanBodyBones[] bones = leftSide ? LeftFingerBones : RightFingerBones;
        bool hasBounds = false;
        bounds = new Bounds(transform.position, Vector3.one * 0.2f);

        foreach (HumanBodyBones bone in bones)
        {
            Transform boneTransform = GetBone(bone);
            if (boneTransform == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = new Bounds(boneTransform.position, Vector3.one * 0.03f);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(boneTransform.position);
            }
        }

        if (!hasBounds)
        {
            return false;
        }

        bounds.Expand(0.12f);
        return true;
    }

    private bool RenderCameraToPng(Camera captureCamera, string path)
    {
        RenderTexture previousRenderTexture = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(screenshotWidth, screenshotHeight, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = null;

        try
        {
            captureCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, captureCamera.backgroundColor);
            captureCamera.Render();

            texture = new Texture2D(screenshotWidth, screenshotHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, screenshotWidth, screenshotHeight), 0, 0);
            texture.Apply();
            return MotionComparisonProbeReportWriter.WriteNonBlankScreenshotPng(path, texture);
        }
        finally
        {
            captureCamera.targetTexture = null;
            RenderTexture.active = previousRenderTexture;
            RenderTexture.ReleaseTemporary(renderTexture);

            if (texture != null)
            {
                Destroy(texture);
            }
        }
    }

    private void WriteSessionManifest(string stateReason)
    {
        if (string.IsNullOrEmpty(_sessionManifestPath))
        {
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        string updatedAt = MotionComparisonProbeReportWriter.BuildSessionUpdatedAt(DateTime.Now);
        MotionComparisonProbeSessionManifestOutputPaths outputPaths =
            MotionComparisonProbeOutputPaths.BuildSessionManifestOutputPaths(
            Application.dataPath, _csvPath, _screenshotFolder, _screenshotIndexPath, _screenshotSessionIndexPath);
        ThumbGuardDiagnostics thumbGuardDiagnostics = CaptureThumbGuardDiagnostics();
        MotionComparisonProbeReportWriter.WriteSessionManifestMarkdown(
            _sessionManifestPath,
            new MotionComparisonProbeSessionManifestData(
                sessionId: _sessionId,
                comparisonLabel: comparisonLabel,
                sceneName: sceneName,
                stateReason: stateReason,
                createdAt: _sessionStamp,
                updatedAt: updatedAt,
                screenshotsEnabled: captureSampleScreenshots,
                sampleClock: MotionComparisonProbeReportWriter.BuildSampleClockLabel(sampleByAnimationClipTime),
                sampleTimes: MotionComparisonProbeReportWriter.FormatSampleTimes(sampleTimes),
                yybDiagnosticOnlyMetrics: captureYybDiagnosticOnlyMetrics,
                riskEvaluationFrameCount: _riskEvaluationFrameCount,
                leftThumbCoreCoverageFrameCount: _leftCoreThumbDiagnosticFrameCount,
                rightThumbCoreCoverageFrameCount: _rightCoreThumbDiagnosticFrameCount,
                leftThumbHelperCoverageRequired: _leftHelperCoverageRequired,
                rightThumbHelperCoverageRequired: _rightHelperCoverageRequired,
                leftThumbHelperCoverageFrameCount: _leftHelperRelationshipFrameCount,
                rightThumbHelperCoverageFrameCount: _rightHelperRelationshipFrameCount,
                maxGenericThumbAnatomyRisk: _maxGenericThumbAnatomyRisk,
                maxGenericThumbAnatomyRiskReason: _maxGenericThumbAnatomyRiskReason,
                maxGenericThumbAnatomyRiskClipTime: _maxGenericThumbAnatomyRiskClipTime,
                maxGenericThumbAnatomyRiskRecorderFrame: _maxGenericThumbAnatomyRiskRecorderFrame,
                maxThumbSpreadRisk: _maxThumbSpreadRisk,
                maxThumbProjectionRisk: _maxThumbProjectionRisk,
                maxThumbHelperSeparationRisk: _maxThumbHelperSeparationRisk,
                maxThumbWebbingRisk: _maxThumbWebbingRisk,
                maxYybDeformationRisk: _maxYybDeformationRisk,
                maxYybDeformationRiskReason: _maxYybDeformationRiskReason,
                maxYybDeformationRiskClipTime: _maxYybDeformationRiskClipTime,
                maxYybDeformationRiskRecorderFrame: _maxYybDeformationRiskRecorderFrame,
                leftThumbProjectionGuardWeight: thumbGuardDiagnostics.LeftProjectionGuardWeight,
                rightThumbProjectionGuardWeight: thumbGuardDiagnostics.RightProjectionGuardWeight,
                leftThumbIndexSpreadGuardWeight: thumbGuardDiagnostics.LeftIndexSpreadGuardWeight,
                rightThumbIndexSpreadGuardWeight: thumbGuardDiagnostics.RightIndexSpreadGuardWeight,
                leftThumbSegmentStraightenGuardWeight: thumbGuardDiagnostics.LeftSegmentStraightenWeight,
                rightThumbSegmentStraightenGuardWeight: thumbGuardDiagnostics.RightSegmentStraightenWeight,
                artifactPaths: outputPaths));
    }

}
