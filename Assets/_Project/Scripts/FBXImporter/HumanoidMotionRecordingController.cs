using System;
using Fbx2Vmd.Recording;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 영상 인코더와 Humanoid 모션의 시작·종료 순서를 조립함.
    /// </summary>
    internal sealed class HumanoidMotionRecordingController : IDisposable
    {
        private readonly HumanoidMotionPlaybackController _playbackController;
        private readonly IMotionVideoRecorder _videoRecorder;

        internal HumanoidMotionRecordingController(
            HumanoidMotionPlaybackController playbackController,
            IMotionVideoRecorder videoRecorder)
        {
            _playbackController = playbackController ??
                throw new ArgumentNullException(nameof(playbackController));
            _videoRecorder = videoRecorder ??
                throw new ArgumentNullException(nameof(videoRecorder));
        }

        internal bool IsRecording => _videoRecorder.IsRecording;
        internal string OutputFilePath => _videoRecorder.OutputFilePath;

        internal bool TryStart(
            MotionVideoRecordingSettings settings,
            out string errorMessage)
        {
            if (!_playbackController.IsPrepared)
            {
                errorMessage = "재생할 FBX 모션이 준비되지 않았습니다.";
                return false;
            }

            if (_videoRecorder.IsRecording)
            {
                errorMessage = "이미 영상 녹화 중입니다.";
                return false;
            }

            if (!_videoRecorder.TryPrepare(settings, out errorMessage))
            {
                _playbackController.Stop();
                return false;
            }

            _playbackController.Stop();
            if (!_videoRecorder.TryStart(out errorMessage))
            {
                _videoRecorder.Stop();
                _playbackController.Stop();
                return false;
            }

            if (_playbackController.Play())
            {
                return true;
            }

            _videoRecorder.Stop();
            _playbackController.Stop();
            errorMessage = "영상 녹화 시작 후 FBX 모션을 재생하지 못했습니다.";
            return false;
        }

        internal bool Stop()
        {
            bool hadActiveRecording = _videoRecorder.IsRecording;
            _videoRecorder.Stop();
            bool rewoundMotion = _playbackController.Stop();
            return hadActiveRecording || rewoundMotion;
        }

        internal bool StopWhenPlaybackCompletes()
        {
            if (!_videoRecorder.IsRecording ||
                _playbackController.State == HumanoidMotionPlaybackState.Playing)
            {
                return false;
            }

            _videoRecorder.Stop();
            _playbackController.Stop();
            return true;
        }

        public void Dispose()
        {
            _videoRecorder.Dispose();
        }
    }
}
