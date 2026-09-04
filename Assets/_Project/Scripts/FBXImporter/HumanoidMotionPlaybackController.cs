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
        private HumanoidPoseCorrectionDocument _poseCorrectionDocument;

        internal HumanoidMotionPlaybackState State { get; private set; } =
            HumanoidMotionPlaybackState.Empty;

        internal float CurrentTimeSeconds { get; private set; }

        internal float ClipLengthSeconds { get; private set; }

        internal float ClipFrameRate { get; private set; }

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
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            Dispose();

            try
            {
                _player.Initialize(targetAnimator, clip);
                ClipLengthSeconds = Mathf.Max(0f, clip.length);
                ClipFrameRate = HumanoidMotionFrameCalculator.NormalizeFrameRate(
                    clip.frameRate);
                CurrentTimeSeconds = 0f;
                _player.EvaluateAt(CurrentTimeSeconds);
                _poseFrameEditor.Initialize(targetAnimator);
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

            _player.EvaluateAt(CurrentTimeSeconds);
            return true;
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
            _poseFrameEditor.Dispose();
            _player.Dispose();
            CurrentTimeSeconds = 0f;
            ClipLengthSeconds = 0f;
            ClipFrameRate = 0f;
            _poseCorrectionDocument = null;
            State = HumanoidMotionPlaybackState.Empty;
        }

        private bool EvaluateCurrentPoseWithCorrection()
        {
            // 원본 clip을 먼저 평가한 뒤 현재 프레임 delta를 한 번만 적용함.
            _player.EvaluateAt(CurrentTimeSeconds);
            int frameIndex = CurrentFrameIndex;
            return _poseCorrectionDocument == null ||
                !_poseCorrectionDocument.HasFrameCorrection(frameIndex) ||
                _poseFrameEditor.TryApply(
                    _poseCorrectionDocument,
                    frameIndex);
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
