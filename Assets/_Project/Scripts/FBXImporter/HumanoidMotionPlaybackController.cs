using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal enum HumanoidMotionPlaybackState
    {
        Empty,
        Ready,
        Playing,
        Paused
    }

    /// <summary>
    /// Humanoid 모션의 명시적 재생 상태와 시간을 관리함.
    /// </summary>
    internal sealed class HumanoidMotionPlaybackController : IDisposable
    {
        private readonly NativeHumanoidAnimationPlayer _player =
            new NativeHumanoidAnimationPlayer();
        private readonly HumanoidPoseFrameEditor _poseFrameEditor =
            new HumanoidPoseFrameEditor();
        private readonly HumanoidArmSupportPoseApplier _armSupportPoseApplier =
            new HumanoidArmSupportPoseApplier();
        private HumanoidPoseCorrectionDocument _poseCorrectionDocument;
#if UNITY_EDITOR
        private readonly EditorHumanoidPoseReferencePlayer _poseReferencePlayer =
            new EditorHumanoidPoseReferencePlayer();
        private readonly EditorHumanoidFootContactStabilizer _footContactStabilizer =
            new EditorHumanoidFootContactStabilizer();
        private readonly EditorHumanoidGroundResponse _groundResponse =
            new EditorHumanoidGroundResponse();
        private bool _isGroundResponseEnabled;
        private EditorHumanoidFootGrounding _footGrounding;
        private HumanoidFootRotationBinding _leftFootRotation;
        private HumanoidFootRotationBinding _rightFootRotation;
        private Quaternion _sourceToTargetRotation = Quaternion.identity;
        internal bool HasFootRotationReference => _leftFootRotation != null &&
            _rightFootRotation != null && _poseReferencePlayer.HasFootRotationReference;
        internal HumanoidFootGroundingStatus LastGroundingStatus { get; private set; } =
            HumanoidFootGroundingStatus.Disabled;
#endif

        internal HumanoidMotionPlaybackState State { get; private set; } =
            HumanoidMotionPlaybackState.Empty;

        internal float CurrentTimeSeconds { get; private set; }

        internal float ClipLengthSeconds { get; private set; }

        internal float ClipFrameRate { get; private set; }
        internal string ClipName { get; private set; } = string.Empty;

        internal int CurrentFrameIndex =>
            HumanoidMotionFrameCalculator.CalculateFrameIndex(
                CurrentTimeSeconds,
                ClipLengthSeconds,
                ClipFrameRate);

        internal int LastFrameIndex =>
            HumanoidMotionFrameCalculator.CalculateLastFrameIndex(
                ClipLengthSeconds,
                ClipFrameRate);

        internal bool IsPrepared => State != HumanoidMotionPlaybackState.Empty;

        internal void Prepare(Animator targetAnimator, AnimationClip clip)
        {
            PrepareCore(targetAnimator, clip, null);
        }

#if UNITY_EDITOR
        internal void SetGroundResponseEnabled(bool isEnabled)
        {
            _isGroundResponseEnabled = isEnabled;
            if (IsPrepared)
                EvaluateCurrentPoseWithCorrection();
        }

        internal void PrepareWithArmDirectionReference(
            Animator targetAnimator,
            AnimationClip clip,
            GameObject sourceModelAsset)
        {
            if (sourceModelAsset == null)
            {
                throw new ArgumentNullException(nameof(sourceModelAsset));
            }

            PrepareCore(
                targetAnimator,
                clip,
                () => InitializeEditorReferenceCorrections(
                    targetAnimator,
                    clip,
                    sourceModelAsset));
        }
#endif

        private void PrepareCore(
            Animator targetAnimator,
            AnimationClip clip,
            Action initializeCanonicalPoseReference)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            Dispose();

            try
            {
#if UNITY_EDITOR
                // 최초 원본 평가 전에 기준 밑창과 Avatar 굽힘 방향을 확보함.
                if (initializeCanonicalPoseReference != null)
                {
                    InitializeFootRotationBindings(targetAnimator);
                    EditorHumanoidFootGrounding.TryCreate(targetAnimator, out _footGrounding);
                }
#endif
                _armSupportPoseApplier.Initialize(targetAnimator, clip);
                _player.Initialize(targetAnimator, clip);
#if UNITY_EDITOR
                _groundResponse.Initialize(targetAnimator);
#endif
                ClipLengthSeconds = Mathf.Max(0f, clip.length);
                ClipFrameRate = HumanoidMotionFrameCalculator.NormalizeFrameRate(
                    clip.frameRate);
                ClipName = clip.name;
                CurrentTimeSeconds = 0f;
                _player.EvaluateAt(CurrentTimeSeconds);
                _poseFrameEditor.Initialize(targetAnimator);
                initializeCanonicalPoseReference?.Invoke();
                if (!EvaluateCurrentPoseWithCorrection())
                {
                    throw new InvalidOperationException("Humanoid 첫 프레임 보정을 적용하지 못했습니다.");
                }
                State = HumanoidMotionPlaybackState.Ready;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool Play()
        {
            if (!IsPrepared || State == HumanoidMotionPlaybackState.Playing)
            {
                return false;
            }

            if (CurrentTimeSeconds >= ClipLengthSeconds)
            {
                CurrentTimeSeconds = 0f;
            }

            EvaluateCurrentPoseWithCorrection();
            State = HumanoidMotionPlaybackState.Playing;
            return true;
        }

        internal bool Pause()
        {
            if (State != HumanoidMotionPlaybackState.Playing)
            {
                return false;
            }

            State = HumanoidMotionPlaybackState.Paused;
            return true;
        }

        internal bool Stop()
        {
            if (!IsPrepared)
            {
                return false;
            }

            CurrentTimeSeconds = 0f;
            EvaluateCurrentPoseWithCorrection();
            State = HumanoidMotionPlaybackState.Ready;
            return true;
        }

        internal bool Seek(float timeSeconds)
        {
            if (!IsPrepared)
            {
                return false;
            }

            ValidateTime(timeSeconds, nameof(timeSeconds));
            CurrentTimeSeconds = Mathf.Clamp(timeSeconds, 0f, ClipLengthSeconds);
            EvaluateCurrentPoseWithCorrection();
            return true;
        }

        internal bool SeekFrame(int frameIndex)
        {
            if (!IsPrepared)
            {
                return false;
            }

            return Seek(HumanoidMotionFrameCalculator.CalculateTimeSeconds(
                frameIndex,
                ClipLengthSeconds,
                ClipFrameRate));
        }

        internal bool TryCaptureCurrentPose(out HumanPose pose)
        {
            pose = default;
            return IsPrepared && _poseFrameEditor.TryCapture(out pose);
        }

#if UNITY_EDITOR
        internal bool TryCaptureCurrentFootSurface(out HumanoidFootGroundingSnapshot left,
            out HumanoidFootGroundingSnapshot right)
        {
            left = null;
            right = null;
            return IsPrepared &&
                (LastGroundingStatus == HumanoidFootGroundingStatus.Applied ||
                 LastGroundingStatus == HumanoidFootGroundingStatus.NoGround) &&
                _footGrounding != null && _footGrounding.TryCaptureCurrentSurface(out left, out right);
        }
#endif

        internal bool TryPreviewPoseCorrection(
            HumanoidPoseCorrectionDocument document)
        {
            if (!IsPrepared || document == null)
            {
                return false;
            }

            _poseCorrectionDocument = document;
            return EvaluateCurrentPoseWithCorrection();
        }

        internal bool RestoreCurrentPose()
        {
            if (!IsPrepared)
            {
                return false;
            }

#if UNITY_EDITOR
            _footGrounding?.RestoreAppliedPose();
            RestoreFootRotationBindings();
#endif
            _player.EvaluateAt(CurrentTimeSeconds);
            if (!TryCaptureFootBendNormals(
                    out Vector3 leftBendNormal,
                    out Vector3 rightBendNormal))
            {
                return false;
            }

            bool isApplied = TryApplyArmDirectionCorrection();
#if UNITY_EDITOR
            ApplyFootRotationReference(CurrentTimeSeconds);
#endif
            _armSupportPoseApplier.Apply();
            return isApplied && TryApplyFootContactStabilization(
                leftBendNormal,
                rightBendNormal);
        }

        internal void Tick(float deltaTimeSeconds)
        {
            ValidateTime(deltaTimeSeconds, nameof(deltaTimeSeconds));
            if (State != HumanoidMotionPlaybackState.Playing)
            {
                return;
            }

            CurrentTimeSeconds = Mathf.Min(
                CurrentTimeSeconds + deltaTimeSeconds,
                ClipLengthSeconds);
            EvaluateCurrentPoseWithCorrection();

            if (CurrentTimeSeconds >= ClipLengthSeconds)
            {
                State = HumanoidMotionPlaybackState.Ready;
            }
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            _isGroundResponseEnabled = false;
            LastGroundingStatus = HumanoidFootGroundingStatus.Disabled;
            _footGrounding?.Dispose();
            _footGrounding = null;
            RestoreFootRotationBindings();
            _leftFootRotation = _rightFootRotation = null;
            _sourceToTargetRotation = Quaternion.identity;
            _groundResponse.Clear();
            _footContactStabilizer.Clear();
            _poseReferencePlayer.Dispose();
#endif
            _poseFrameEditor.Dispose();
            _player.Dispose();
            _armSupportPoseApplier.Dispose();
            CurrentTimeSeconds = 0f;
            ClipLengthSeconds = 0f;
            ClipFrameRate = 0f;
            ClipName = string.Empty;
            _poseCorrectionDocument = null;
            State = HumanoidMotionPlaybackState.Empty;
        }

        private bool EvaluateCurrentPoseWithCorrection()
        {
            // 원본 자세와 상체 보정 뒤에 발 접촉을 마지막으로 고정함.
#if UNITY_EDITOR
            _footGrounding?.RestoreAppliedPose();
            RestoreFootRotationBindings();
#endif
            _player.EvaluateAt(CurrentTimeSeconds);
            if (!TryCaptureFootBendNormals(
                    out Vector3 leftBendNormal,
                    out Vector3 rightBendNormal))
            {
                return false;
            }

            if (!TryApplyArmDirectionCorrection())
            {
                return false;
            }

            int frameIndex = CurrentFrameIndex;
#if UNITY_EDITOR
            ApplyFootRotationReference(CurrentTimeSeconds);
#endif
            bool isApplied = _poseCorrectionDocument == null ||
                !_poseCorrectionDocument.HasFrameCorrection(frameIndex) ||
                _poseFrameEditor.TryApply(
                    _poseCorrectionDocument,
                    frameIndex);
            _armSupportPoseApplier.Apply();
            return isApplied && TryApplyFootContactStabilization(
                leftBendNormal,
                rightBendNormal);
        }

        private bool TryApplyArmDirectionCorrection()
        {
#if UNITY_EDITOR
            if (!_poseReferencePlayer.IsInitialized)
            {
                return true;
            }

            if (!_poseReferencePlayer.TryEvaluateArmDirectionsAt(
                    CurrentTimeSeconds,
                    out HumanoidArmDirectionReference reference))
            {
                return false;
            }

            return _poseFrameEditor.TryApplyArmDirectionReference(
                reference,
                out _);
#else
            return true;
#endif
        }

        private bool TryCaptureFootBendNormals(
            out Vector3 leftBendNormal,
            out Vector3 rightBendNormal)
        {
#if UNITY_EDITOR
            return _footContactStabilizer.TryCaptureBendNormals(
                out leftBendNormal,
                out rightBendNormal);
#else
            leftBendNormal = Vector3.zero;
            rightBendNormal = Vector3.zero;
            return true;
#endif
        }

        private bool TryApplyFootContactStabilization(
            Vector3 leftBendNormal,
            Vector3 rightBendNormal)
        {
#if UNITY_EDITOR
            LastGroundingStatus = _isGroundResponseEnabled
                ? HumanoidFootGroundingStatus.Unavailable : HumanoidFootGroundingStatus.Disabled;
            if (_isGroundResponseEnabled && _footGrounding != null && _footGrounding.IsPrepared)
            {
                if (_footGrounding.TryApply(CurrentTimeSeconds, _groundResponse))
                {
                    LastGroundingStatus = _footGrounding.HasGround
                        ? HumanoidFootGroundingStatus.Applied : HumanoidFootGroundingStatus.NoGround;
                    return true;
                }
                LastGroundingStatus = HumanoidFootGroundingStatus.Fallback;
            }
            bool isApplied = _footContactStabilizer.TryApply(
                CurrentTimeSeconds,
                leftBendNormal,
                rightBendNormal);
            if (isApplied && _isGroundResponseEnabled)
                _groundResponse.Apply(leftBendNormal, rightBendNormal);
            return isApplied;
#else
            return true;
#endif
        }

#if UNITY_EDITOR
        private void InitializeFootRotationBindings(Animator animator)
        {
            if (animator == null || !animator.isHuman)
                return;
            _sourceToTargetRotation = animator.transform.rotation;
            HumanoidFootRotationBinding.TryCreate(animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                animator.GetBoneTransform(HumanBodyBones.LeftToes), animator.transform.up, out _leftFootRotation);
            HumanoidFootRotationBinding.TryCreate(animator.GetBoneTransform(HumanBodyBones.RightFoot),
                animator.GetBoneTransform(HumanBodyBones.RightToes), animator.transform.up, out _rightFootRotation);
        }

        private void RestoreFootRotationBindings()
        {
            _leftFootRotation?.RestoreAppliedRotation();
            _rightFootRotation?.RestoreAppliedRotation();
        }

        private void ApplyFootRotationReference(float timeSeconds)
        {
            if (!HasFootRotationReference)
                return;
            if (_poseReferencePlayer.TryEvaluateFootFramesAt(timeSeconds, out var left, out var right) &&
                _leftFootRotation.TryApplyWorldFrame(_sourceToTargetRotation * left) &&
                _rightFootRotation.TryApplyWorldFrame(_sourceToTargetRotation * right))
                return;
            RestoreFootRotationBindings();
            throw new InvalidOperationException("준비된 발 회전 기준을 적용하지 못했습니다.");
        }

        private void InitializeEditorReferenceCorrections(
            Animator targetAnimator,
            AnimationClip clip,
            GameObject sourceModelAsset)
        {
            _poseReferencePlayer.InitializeFromSourceModel(sourceModelAsset, clip);
            _sourceToTargetRotation *= Quaternion.Inverse(_poseReferencePlayer.InitialRootRotation);
            _footContactStabilizer.Initialize(
                targetAnimator,
                clip,
                _player.EvaluateAt,
                _poseReferencePlayer);
            if (_footGrounding != null)
                _footGrounding.TryPrepare(_footContactStabilizer.SourceSamples,
                    _footContactStabilizer.SourceHumanScale, clip, EvaluateEditorContactReference);
        }

        private void EvaluateEditorContactReference(float timeSeconds)
        {
            _footGrounding?.RestoreAppliedPose();
            RestoreFootRotationBindings();
            _player.EvaluateAt(timeSeconds);
            if (!_poseReferencePlayer.TryEvaluateArmDirectionsAt(timeSeconds, out var reference) ||
                !_poseFrameEditor.TryApplyArmDirectionReference(reference, out _))
                throw new InvalidOperationException("접지 시작 자세의 상체 기준을 평가하지 못했습니다.");
            ApplyFootRotationReference(timeSeconds);
            _armSupportPoseApplier.Apply();
        }
#endif

        private static void ValidateTime(float timeSeconds, string parameterName)
        {
            if (float.IsNaN(timeSeconds) ||
                float.IsInfinity(timeSeconds) ||
                timeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
