using System;
using Fbx2Vmd.Profiling;
using Fbx2Vmd.Recording;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 영상 인코더와 Humanoid 모션의 시작·종료 순서를 조립함.
    /// </summary>
    internal sealed class HumanoidMotionRecordingController : IDisposable
    {
        private readonly HumanoidMotionPlaybackController _playbackController;
        private readonly IMotionVideoRecorder _videoRecorder;
        private readonly Renderer[] _alphaHiddenRenderers;
        private float _recordingFrameRate;
        private long _nextRecordingFrame;
        private bool _hasReachedLastFrame;
        // 녹화 구간 — 시작 시각의 자세가 첫 캡처 프레임, 종료 시각의 자세가 마지막 캡처 프레임이 됨.
        private float _rangeStartSeconds;
        private float _rangeEndSeconds;
        // 느린 틱 계측 — 목표 프레임 간격의 2배를 넘는 틱을 스파이크로 집계함.
        private int _slowTickCount;
        private int _recordedTickCount;
        private double _maxTickMilliseconds;

        internal HumanoidMotionRecordingController(
            HumanoidMotionPlaybackController playbackController,
            IMotionVideoRecorder videoRecorder)
            : this(playbackController, videoRecorder, null)
        {
        }

        internal HumanoidMotionRecordingController(
            HumanoidMotionPlaybackController playbackController,
            IMotionVideoRecorder videoRecorder,
            Renderer[] alphaHiddenRenderers)
        {
            _playbackController = playbackController ??
                throw new ArgumentNullException(nameof(playbackController));
            _videoRecorder = videoRecorder ??
                throw new ArgumentNullException(nameof(videoRecorder));
            _alphaHiddenRenderers = alphaHiddenRenderers;
        }

        internal bool IsRecording => _videoRecorder.IsRecording;
        internal string OutputFilePath => _videoRecorder.OutputFilePath;

        // 구간 내 재생 위치를 0~1로 환산 — 녹화 중 진행률 표시에 사용함.
        internal float NormalizedRecordingProgress
        {
            get
            {
                float span = _rangeEndSeconds - _rangeStartSeconds;
                if (!IsRecording || span <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(
                    (_playbackController.CurrentTimeSeconds - _rangeStartSeconds) / span);
            }
        }

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

            // 구간 해석은 어떤 부작용도 일으키기 전에 검증함 — 무효 설정은 재생 상태를 건드리지 않음.
            _rangeStartSeconds = Mathf.Max(0f, settings.StartTimeSeconds);
            float clipLengthSeconds = _playbackController.ClipLengthSeconds;
            _rangeEndSeconds = settings.EndTimeSeconds > 0f
                ? Mathf.Min(settings.EndTimeSeconds, clipLengthSeconds)
                : clipLengthSeconds;
            if (_rangeEndSeconds - _rangeStartSeconds <= 0f)
            {
                errorMessage = "녹화 구간이 비어 있습니다. 시작/종료 프레임 설정을 확인하세요.";
                return false;
            }

            // 이전 세션 계측 잔존 방지 — 실패 경로에서도 새 세션으로 초기화함.
            _slowTickCount = 0;
            _recordedTickCount = 0;
            _maxTickMilliseconds = 0.0;

            if (!_videoRecorder.TryPrepare(settings, out errorMessage))
            {
                _playbackController.Stop();
                return false;
            }

            _playbackController.Stop();
            // 구간 녹화면 첫 캡처 프레임이 시작 자세가 되도록 녹화 시작 전에 해당 시각으로 이동함.
            if (_rangeStartSeconds > 0f &&
                !_playbackController.Seek(_rangeStartSeconds))
            {
                _videoRecorder.Stop();
                errorMessage = "녹화 구간 시작 위치로 이동하지 못했습니다.";
                return false;
            }

            if (!_videoRecorder.TryStart(out errorMessage))
            {
                _videoRecorder.Stop();
                _playbackController.Stop();
                return false;
            }

            if (_playbackController.Play())
            {
                _recordingFrameRate = settings.FrameRate;
                _nextRecordingFrame = 1;
                _hasReachedLastFrame = false;
                // 프레임 정확도 우선 녹화 — 재생이 실시간보다 느려 보이는 것은 정상 동작임.
                Debug.Log(
                    $"[FBXImport] 영상 녹화 시작됨. fps={settings.FrameRate}, " +
                    $"구간={_rangeStartSeconds:F2}~{_rangeEndSeconds:F2}s, " +
                    "프레임 정확도 모드로 재생이 느려 보일 수 있습니다.");
                // 투명 배경 녹화(MOV/WebM)일 때만 지정된 렌더러를 숨겨 캐릭터만 산출함.
                SetAlphaHidden(settings.Format.SupportsAlpha());
                return true;
            }

            _videoRecorder.Stop();
            _playbackController.Stop();
            errorMessage = "영상 녹화 시작 후 FBX 모션을 재생하지 못했습니다.";
            return false;
        }

        internal bool Tick(float simulationDeltaTimeSeconds)
        {
            using var perfScope = PerfScope.Measure("HumanoidMotionRecordingController.Tick");
            // Recorder가 첫 0자세를 기록할 때까지 시뮬레이션을 정지하므로 함께 대기함.
            if (!IsRecording || simulationDeltaTimeSeconds <= 0f)
                return false;

            // 마지막 자세를 평가한 프레임의 렌더가 끝난 다음에 되감음.
            if (_hasReachedLastFrame)
                return Stop();

            long tickStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            double timeSeconds =
                _rangeStartSeconds + _nextRecordingFrame / (double)_recordingFrameRate;
            _playbackController.Seek((float)Math.Min(timeSeconds, _rangeEndSeconds));
            _hasReachedLastFrame =
                _playbackController.CurrentTimeSeconds >= _rangeEndSeconds;
            _nextRecordingFrame++;

            double tickMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - tickStartTimestamp) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency;
            _recordedTickCount++;
            _maxTickMilliseconds = Math.Max(_maxTickMilliseconds, tickMilliseconds);
            if (tickMilliseconds > 2000.0 / _recordingFrameRate)
            {
                _slowTickCount++;
            }
            return false;
        }

        internal bool Stop()
        {
            bool hadActiveRecording = _videoRecorder.IsRecording;
            _videoRecorder.Stop();
            bool rewoundMotion = _playbackController.Stop();
            SetAlphaHidden(false);
            if (_recordedTickCount > 0)
            {
                // 녹화 중 프레임 스파이크 요약 — 끊김 원인 분석용 계측.
                Debug.Log(
                    $"[FBXImport] 녹화 틱 계측: ticks={_recordedTickCount}, " +
                    $"slowTicks(>2프레임시간)={_slowTickCount}, maxTickMs={_maxTickMilliseconds:F1}");
                // 중복 Stop 호출 시 같은 요약이 반복 출력되지 않도록 리셋함.
                _recordedTickCount = 0;
                _slowTickCount = 0;
                _maxTickMilliseconds = 0.0;
            }
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
            SetAlphaHidden(false);
            return true;
        }

        public void Dispose()
        {
            SetAlphaHidden(false);
            _videoRecorder.Dispose();
        }

        // 숨김 대상 렌더러를 알파 녹화 기간 동안만 꺼둠.
        private void SetAlphaHidden(bool hidden)
        {
            if (_alphaHiddenRenderers == null)
            {
                return;
            }

            foreach (Renderer renderer in _alphaHiddenRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = !hidden;
                }
            }
        }
    }
}
