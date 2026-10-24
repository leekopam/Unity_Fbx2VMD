using System;
using Fbx2Vmd.Profiling;
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
        private readonly HumanoidPoseReferencePlayer _poseReferencePlayer =
            new HumanoidPoseReferencePlayer();
        private readonly HumanoidFootContactStabilizer _footContactStabilizer =
            new HumanoidFootContactStabilizer();
        private readonly HumanoidGroundResponse _groundResponse =
            new HumanoidGroundResponse();
        private readonly HumanoidFootGoalDriver _footGoalDriver =
            new HumanoidFootGoalDriver();
        private readonly HumanoidFootStepDiffuser _footStepDiffuser =
            new HumanoidFootStepDiffuser();
        private bool _isGroundResponseEnabled;
        // 실험 경로 — goal IK 드라이브가 켜져도 gate는 매 프레임 실행되고
        // 드라이버는 주장 프레임에만 gate 결과 위에 오버레이됨.
        private bool _useIkGoalFootDrive;
        private float _footGoalAlpha;
        // 소스 클립의 단발 불연속으로 정지 발이 순간이동할 때 확산 적용 여부.
        private bool _useFootStepDiffusion;
        private HumanoidFootGrounding _footGrounding;
        private HumanoidFootRotationBinding _leftFootRotation;
        private HumanoidFootRotationBinding _rightFootRotation;
        private Quaternion _sourceToTargetRotation = Quaternion.identity;
        internal bool HasFootRotationReference => _leftFootRotation != null &&
            _rightFootRotation != null && _poseReferencePlayer.HasFootRotationReference;
        internal HumanoidFootGroundingStatus LastGroundingStatus { get; private set; } =
            HumanoidFootGroundingStatus.Disabled;
        // 마지막 프레임의 게이트 입력 수치. 계측 경로가 교정 실패 원인을 프레임별로 재현함.
        internal HumanoidFootGroundingGate LastGroundingGate { get; } =
            new HumanoidFootGroundingGate();

        internal HumanoidMotionPlaybackState State { get; private set; } =
            HumanoidMotionPlaybackState.Empty;

        internal float CurrentTimeSeconds { get; private set; }

        internal float ClipLengthSeconds { get; private set; }

        internal float ClipFrameRate { get; private set; }
        internal string ClipName { get; private set; } = string.Empty;

        // 선두 정지 구간 탐지처럼 준비된 클립 자체를 읽는 경로에서 사용함.
        internal AnimationClip PreparedClip { get; private set; }

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

        internal void SetGroundResponseEnabled(bool isEnabled)
        {
            _isGroundResponseEnabled = isEnabled;
            if (IsPrepared)
                EvaluateCurrentPoseWithCorrection();
        }

        // goal IK 실험 경로 스위치. alpha=0 또는 데이터 부족이면 기존 접지 경로가 그대로 동작함.
        internal void SetIkGoalFootDrive(bool isEnabled, float alpha)
        {
            _useIkGoalFootDrive = isEnabled;
            _footGoalAlpha = Mathf.Clamp01(alpha);
            if (IsPrepared)
                EvaluateCurrentPoseWithCorrection();
        }

        internal bool IsIkGoalFootDriveActive =>
            _useIkGoalFootDrive && _footGoalAlpha > 0f && _footGoalDriver.IsInitialized;

        // 정지 발 순간이동 확산 스위치 — 파이프라인 준비 경로가 기본 활성하고
        // A/B 비교나 롤백 시 해제함.
        internal void SetFootStepDiffusion(bool isEnabled)
        {
            _useFootStepDiffusion = isEnabled;
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
                // 최초 원본 평가 전에 기준 밑창과 Avatar 굽힘 방향을 확보함.
                if (initializeCanonicalPoseReference != null)
                {
                    InitializeFootRotationBindings(targetAnimator);
                    HumanoidFootGrounding.TryCreate(targetAnimator, out _footGrounding);
                }
                _armSupportPoseApplier.Initialize(targetAnimator, clip);
                _player.Initialize(targetAnimator, clip);
                _groundResponse.Initialize(targetAnimator);
                _footStepDiffuser.Initialize(targetAnimator);
                ClipLengthSeconds = Mathf.Max(0f, clip.length);
                ClipFrameRate = HumanoidMotionFrameCalculator.NormalizeFrameRate(
                    clip.frameRate);
                ClipName = clip.name;
                PreparedClip = clip;
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

        internal bool TryCaptureCurrentFootSurface(out HumanoidFootGroundingSnapshot left,
            out HumanoidFootGroundingSnapshot right)
        {
            left = null;
            right = null;
            // 폴백 프레임도 접지 평가가 실행된 상태이므로 표면 측정값을 기록해야 한다.
            // Unavailable/Disabled는 평가 자체가 없어 측정값이 없으므로 제외한다.
            // goal 경로(evaluation_stage=="ik_goal_drive")는 TryEvaluate로 지면·앵커·
            // 가중치를 프레임별 갱신하되 gate 판정(핀 보정 목표·통과 여부)은 평가하지
            // 않으므로 gate 진단 열 해석은 stage로 구분해야 함.
            return IsPrepared &&
                (LastGroundingStatus == HumanoidFootGroundingStatus.Applied ||
                 LastGroundingStatus == HumanoidFootGroundingStatus.NoGround ||
                 LastGroundingStatus == HumanoidFootGroundingStatus.Fallback) &&
                _footGrounding != null && _footGrounding.TryCaptureCurrentSurface(out left, out right);
        }

        internal bool TryCapturePoseBeforeFootStabilization(Action capture)
        {
            if (!IsPrepared || capture == null) return false;
            try
            {
                if (!EvaluateCurrentPoseWithCorrection(applyFootStabilization: false)) return false;
                capture();
                return true;
            }
            finally
            {
                if (!EvaluateCurrentPoseWithCorrection())
                    throw new InvalidOperationException("계측 후 접지 자세를 복원하지 못했습니다.");
            }
        }

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

            _footGrounding?.RestoreAppliedPose();
            RestoreFootRotationBindings();
            _player.EvaluateAt(CurrentTimeSeconds);
            if (!TryCaptureFootBendNormals(
                    out Vector3 leftBendNormal,
                    out Vector3 rightBendNormal))
            {
                return false;
            }

            bool isApplied = TryApplyArmDirectionCorrection();
            ApplyFootRotationReference(CurrentTimeSeconds);
            _armSupportPoseApplier.Apply();
            return isApplied && TryApplyFootContactStabilization(
                leftBendNormal,
                rightBendNormal);
        }

        internal void Tick(float deltaTimeSeconds)
        {
            using var perfScope = PerfScope.Measure("HumanoidMotionPlaybackController.Tick");
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
            _isGroundResponseEnabled = false;
            _useIkGoalFootDrive = false;
            _footGoalAlpha = 0f;
            LastGroundingStatus = HumanoidFootGroundingStatus.Disabled;
            LastGroundingGate.measured = false;
            _footGrounding?.Dispose();
            _footGrounding = null;
            RestoreFootRotationBindings();
            _leftFootRotation = _rightFootRotation = null;
            _sourceToTargetRotation = Quaternion.identity;
            _groundResponse.Clear();
            _footGoalDriver.Clear();
            _footStepDiffuser.Clear();
            _useFootStepDiffusion = false;
            _footContactStabilizer.Clear();
            _poseReferencePlayer.Dispose();
            _poseFrameEditor.Dispose();
            _player.Dispose();
            _armSupportPoseApplier.Dispose();
            CurrentTimeSeconds = 0f;
            ClipLengthSeconds = 0f;
            ClipFrameRate = 0f;
            ClipName = string.Empty;
            PreparedClip = null;
            _poseCorrectionDocument = null;
            State = HumanoidMotionPlaybackState.Empty;
        }

        private bool EvaluateCurrentPoseWithCorrection(bool applyFootStabilization = true)
        {
            // 원본 자세와 상체 보정 뒤에 발 접촉을 마지막으로 고정함.
            _footGrounding?.RestoreAppliedPose();
            RestoreFootRotationBindings();
            Vector3 leftBendNormal, rightBendNormal;
            using (PerfScope.Measure("PlaybackController.PoseSample"))
            {
                _player.EvaluateAt(CurrentTimeSeconds);
                if (!TryCaptureFootBendNormals(
                        out leftBendNormal,
                        out rightBendNormal))
                {
                    return false;
                }
            }

            if (!TryApplyArmDirectionCorrection())
            {
                return false;
            }

            int frameIndex = CurrentFrameIndex;
            ApplyFootRotationReference(CurrentTimeSeconds);
            bool isApplied;
            using (PerfScope.Measure("PlaybackController.PoseFrameEdit"))
            {
                isApplied = _poseCorrectionDocument == null ||
                    !_poseCorrectionDocument.HasFrameCorrection(frameIndex) ||
                    _poseFrameEditor.TryApply(
                        _poseCorrectionDocument,
                        frameIndex);
                _armSupportPoseApplier.Apply();
            }

            if (!isApplied || !applyFootStabilization)
            {
                return isApplied;
            }

            using (PerfScope.Measure("PlaybackController.FootStabilization"))
            {
                return TryApplyFootContactStabilization(
                    leftBendNormal,
                    rightBendNormal);
            }
        }

        private bool TryApplyArmDirectionCorrection()
        {
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
        }

        private bool TryCaptureFootBendNormals(
            out Vector3 leftBendNormal,
            out Vector3 rightBendNormal)
        {
            return _footContactStabilizer.TryCaptureBendNormals(
                out leftBendNormal,
                out rightBendNormal);
        }

        private bool TryApplyFootContactStabilization(
            Vector3 leftBendNormal,
            Vector3 rightBendNormal)
        {
            bool isApplied = TryApplyFootContactStabilizationCore(
                leftBendNormal, rightBendNormal);
            // 발 계층 최종 출력의 단발 순간이동을 확산 — 모든 경로 반환부를 감쌈.
            if (_useFootStepDiffusion)
                _footStepDiffuser.TryApply(CurrentTimeSeconds, ClipFrameRate);
            return isApplied;
        }

        private bool TryApplyFootContactStabilizationCore(
            Vector3 leftBendNormal,
            Vector3 rightBendNormal)
        {
            LastGroundingStatus = _isGroundResponseEnabled
                ? HumanoidFootGroundingStatus.Unavailable : HumanoidFootGroundingStatus.Disabled;
            LastGroundingGate.measured = false;
            LastGroundingGate.evaluation_stage = "unavailable";
            LastGroundingGate.raw_pass = LastGroundingGate.held_pass = false;
            LastGroundingGate.used_physical_reach = false;
            LastGroundingGate.pelvis_offset_m = 0f;
            LastGroundingGate.pelvis_maximum_offset_m = 0f;
            LastGroundingGate.left = LastGroundingGate.right = null;
            bool goalDriverActive = IsIkGoalFootDriveActive;
            // 주장 여부는 weight 갱신 전 상태로 판정해 gate가 stabilizer 이전 자세를
            // 보는 alpha=0 순서를 유지함.
            bool driverClaims = goalDriverActive &&
                _footGoalDriver.WouldClaim(CurrentTimeSeconds);
            if (goalDriverActive)
                LastGroundingGate.evaluation_stage = driverClaims
                    ? "ik_goal_drive" : "ik_goal_abstain";
            // gate는 준비된 매 프레임에서 실행 — weight 감쇠·기권 프레임의 접지 보정
            // 공백을 막고, 주장 프레임에는 드라이버가 gate 결과 위에 오버레이함.
            bool gateApplied = false;
            if (_isGroundResponseEnabled &&
                _footGrounding != null && _footGrounding.IsPrepared)
            {
                gateApplied = _footGrounding.TryApply(CurrentTimeSeconds, _groundResponse);
                LastGroundingGate.evaluation_stage = _footGrounding.EvaluationStage;
                LastGroundingGate.measured = _footGrounding.EvaluationStage == "gate";
                LastGroundingGate.raw_pass = _footGrounding.RawGatePass;
                LastGroundingGate.held_pass = gateApplied;
                LastGroundingGate.used_physical_reach = _footGrounding.UsedPhysicalReach;
                LastGroundingGate.pelvis_offset_m = _footGrounding.PelvisOffset;
                LastGroundingGate.pelvis_maximum_offset_m = _footGrounding.PelvisMaximumOffset;
                LastGroundingGate.left = _footGrounding.LeftDiagnostic;
                LastGroundingGate.right = _footGrounding.RightDiagnostic;
                LastGroundingGate.target_error_m = _footGrounding.MaximumTargetError;
                LastGroundingGate.sole_clearance_m = _footGrounding.MinimumSoleClearance;
                LastGroundingGate.supported_contact_error_m =
                    _footGrounding.MaximumSupportedContactError;
                LastGroundingGate.supported_contact_count =
                    _footGrounding.FullySupportedContactCount;
                if (gateApplied)
                {
                    LastGroundingStatus = _footGrounding.HasGround
                        ? HumanoidFootGroundingStatus.Applied : HumanoidFootGroundingStatus.NoGround;
                    if (!driverClaims)
                    {
                        if (goalDriverActive)
                        {
                            // gate 적용 프레임도 드라이버 시간·weight는 매 프레임 갱신해
                            // 다음 pin 진입의 램프와 점프 판정이 끊기지 않게 함.
                            _footGoalDriver.TryApply(CurrentTimeSeconds, _footGoalAlpha,
                                leftBendNormal, rightBendNormal);
                        }
                        return true;
                    }
                }
                else
                {
                    LastGroundingStatus = HumanoidFootGroundingStatus.Fallback;
                }
            }
            // gate 적용 프레임은 alpha=0과 같이 stabilizer를 건너뜀.
            bool isApplied = !gateApplied && _footContactStabilizer.TryApply(
                CurrentTimeSeconds,
                leftBendNormal,
                rightBendNormal);
            if (goalDriverActive)
            {
                // pin 프레임만 IK가 목표를 주장하고 기권 구간은 gate/stabilizer 출력이 유지됨.
                _footGoalDriver.TryApply(CurrentTimeSeconds, _footGoalAlpha,
                    leftBendNormal, rightBendNormal);
                // 주장 프레임의 골반 오프셋은 gate+드라이버 누적값을 보고함.
                if (driverClaims)
                {
                    LastGroundingGate.pelvis_offset_m +=
                        _footGoalDriver.LastAppliedPelvisOffset;
                }
                // 지면 하방 침투 차단은 goal 경로에도 동일하게 적용 — 발 하단이
                // 지면 아래일 때만 작동해 pin 프레임의 IK 결과는 건드리지 않음.
                // 굽힘 법선은 드라이버의 해부학 정합 결과를 사용 — 원시 자세
                // cross는 무릎이 거의 펴진 구간에서 부호가 뒤집혀 상승 IK가
                // 무릎을 반대편에 배치함. 미solve 다리는 자세 cross 폴백으로
                // 드라이버 이후의 현재 자세 기준 방향을 유지함.
                if ((isApplied || gateApplied) && _isGroundResponseEnabled)
                    _groundResponse.Apply(
                        _footGoalDriver.LastUsedLeftBendNormal,
                        _footGoalDriver.LastUsedRightBendNormal, _footGrounding);
                // goal 경로는 gate 보정 대신 평가 전용 갱신으로 지면·지지 가중치·
                // 밑창 측정을 현재 프레임 기준으로 채움 — 스냅샷이 마지막 gate 평가나
                // 초기값을 재사용하지 않게 함. gate 판정 열은 계속 비움(measured=false).
                // 기권 프레임은 gate가 이미 진단을 채웠으므로 평가 갱신을 건너뜀.
                // grounding이 준비되지 않은 구성에서는 스냅샷을 만들 수 없으므로
                // Unavailable을 유지해 측정 불가를 정직하게 신호함.
                if (driverClaims && _footGrounding != null && _footGrounding.IsPrepared)
                {
                    bool evaluated = _footGrounding.TryEvaluate(
                        CurrentTimeSeconds, _groundResponse);
                    LastGroundingGate.evaluation_stage = evaluated
                        ? "ik_goal_drive" : $"ik_goal_{_footGrounding.EvaluationStage}";
                    LastGroundingGate.left = _footGrounding.LeftDiagnostic;
                    LastGroundingGate.right = _footGrounding.RightDiagnostic;
                    LastGroundingStatus = !evaluated
                        ? HumanoidFootGroundingStatus.Fallback
                        : _footGrounding.HasGround
                            ? HumanoidFootGroundingStatus.Applied
                            : HumanoidFootGroundingStatus.NoGround;
                }
            }
            else if (isApplied && _isGroundResponseEnabled)
            {
                _groundResponse.Apply(leftBendNormal, rightBendNormal, _footGrounding);
            }
            return isApplied || gateApplied;
        }

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
                _poseReferencePlayer,
                sourceModelAsset?.name);
            // 초기화는 항상 수행해 세터 시점과 무관하게 둠 — 적용만 플래그로 게이트함.
            _footGoalDriver.TryInitialize(
                targetAnimator, _footContactStabilizer, clip.frameRate);
            if (_footGrounding != null)
            {
                _footGrounding.TryPrepare(_footContactStabilizer.SourceSamples,
                    _footContactStabilizer.SourceHumanScale, clip, EvaluateEditorContactReference,
                    _footContactStabilizer.IntentEstimate);
                // goal 드라이버에 밑창 정점을 공유 — 발끝 본 스냅만으로는 회전된 발의
                // 밑창 정점이 지면 아래로 파고드는 침투를 막지 못함. 프로브가 없으면
                // 기존 발끝 스냅 폴백이 그대로 적용됨.
                if (_footGoalDriver.IsInitialized)
                {
                    if (_footGrounding.TryGetSoleProbeLocalOffsets(true, out Vector3[] leftSole))
                        _footGoalDriver.SetSoleProbes(true, leftSole);
                    if (_footGrounding.TryGetSoleProbeLocalOffsets(false, out Vector3[] rightSole))
                        _footGoalDriver.SetSoleProbes(false, rightSole);
                }
            }
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
