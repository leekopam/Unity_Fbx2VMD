#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 영상 녹화 경로의 e2e 증거 수집기. FBX 임포트→녹화 시작→정지→산출물 파일을
    /// 실제 Recorder 출력으로 검증해 구간 녹화·선두 정지 스킵·녹화 중 임포트 차단을 판정함.
    /// 시나리오는 요청 파라미터로 구성하고, 완성된 mp4 파일 크기와 실제 캡처 틱 수를 기록함.
    /// </summary>
    internal sealed class FbxMotionRecordingEvidenceCapture : IDisposable
    {
        private enum Phase
        {
            Importing, Starting, Recording, Stopping, Finished
        }

        private const float ImportBlockedWallSeconds = 0.5f;
        private const int FileStablePollsRequired = 2;
        private const float FileStablePollIntervalSeconds = 1f;

        private readonly FBXVmdPipeline _pipeline;
        private readonly string _directory;
        private readonly string _requestId;
        private readonly string _inputFileName;
        private readonly bool _skipLeadingStill;
        private readonly bool _rangeEnabled;
        private readonly int _rangeStartFrame;
        private readonly int _rangeEndFrame;
        // 0 이하: 레코더 자동 정지(구간 끝)까지 대기. 양수: 실시간 경과 후 수동 정지.
        private readonly float _stopAfterSeconds;
        private readonly bool _tryImportDuringRecording;
        private readonly bool _originalAutoRecord;
        // 증거 수집이 강제한 직렬화 녹화 옵션의 원본 — 같은 Play 세션의 후속
        // 녹화 경로가 구간·출력 폴더를 상속받지 않게 종료 시 복원함.
        private readonly bool _originalSkipLeadingStill;
        private readonly bool _originalRangeEnabled;
        private readonly int _originalRangeStartFrame;
        private readonly int _originalRangeEndFrame;
        private readonly string _originalOutputFolder;
        private readonly DateTime _startedUtc = DateTime.UtcNow;
        private readonly List<string> _events = new List<string>();
        private Phase _phase = Phase.Importing;
        private bool _importAttempted;
        private bool _importRejected;
        private string _importRejectMessage = string.Empty;
        private DateTime _recordingStartedUtc;
        private string _outputPath = string.Empty;
        private long _lastObservedSize = -1;
        private int _stableSizePolls;
        private DateTime _nextFilePollUtc;
        private float _resolvedStartSeconds;
        private float _resolvedEndSeconds;

        private FbxMotionRecordingEvidenceCapture(
            FBXVmdPipeline pipeline,
            string directory,
            string requestId,
            FbxPlaybackSmokeAutomationRequest request)
        {
            _pipeline = pipeline;
            _directory = directory;
            _requestId = requestId;
            _inputFileName = request.fbx_file;
            _skipLeadingStill = request.recording_skip_still >= 0
                ? request.recording_skip_still > 0
                : pipeline.motionVideoSkipLeadingStill;
            _rangeEnabled = request.recording_range_enabled;
            _rangeStartFrame = request.recording_start_frame;
            _rangeEndFrame = request.recording_end_frame;
            _stopAfterSeconds = request.recording_stop_after_seconds;
            _tryImportDuringRecording = request.recording_try_import_during;
            _originalAutoRecord = pipeline.ShouldRecordVmdAfterImport;
            _originalSkipLeadingStill = pipeline.motionVideoSkipLeadingStill;
            _originalRangeEnabled = pipeline.motionVideoRangeEnabled;
            _originalRangeStartFrame = pipeline.motionVideoRangeStartFrame;
            _originalRangeEndFrame = pipeline.motionVideoRangeEndFrame;
            _originalOutputFolder = pipeline.motionVideoOutputFolder;
            StatePath = Path.Combine(directory, "state.json");
        }

        internal string StatePath { get; }
        internal bool IsFinished => _phase == Phase.Finished;
        internal bool HasEvidence { get; private set; }
        internal string FailureStage { get; private set; } = string.Empty;
        internal string FailureMessage { get; private set; } = string.Empty;
        internal string OutputPath => _outputPath;

        internal static bool TryStart(
            FBXVmdPipeline pipeline,
            FbxPlaybackSmokeAutomationRequest request,
            out FbxMotionRecordingEvidenceCapture capture,
            out string message)
        {
            capture = null;
            message = string.Empty;
            if (pipeline == null || request == null ||
                !Guid.TryParse(request.request_id, out Guid requestGuid) ||
                !Guid.TryParse(request.run_id, out Guid runGuid) ||
                !EditorApplication.isPlaying ||
                !IsSupportedScenePath(SceneManager.GetActiveScene().path) ||
                pipeline.IsProcessing || pipeline.HasPreparedImportedMotion ||
                pipeline.IsImportedMotionRecording || Time.captureFramerate != 0)
            {
                message = "영상 녹화 증거는 Main_Auto/Main_Recoding의 비녹화 Play 상태와 유효한 실행 ID가 필요합니다.";
                return false;
            }

            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            if (string.IsNullOrWhiteSpace(request.fbx_file) ||
                Path.GetFileName(request.fbx_file) != request.fbx_file)
            {
                message = "입력 FBX 파일명이 유효하지 않습니다.";
                return false;
            }
            string input = Path.Combine(
                root, "Assets", "Resources", "Import_FBX", request.fbx_file);
            if (!File.Exists(input))
            {
                message = $"입력 FBX가 없습니다: {request.fbx_file}";
                return false;
            }

            // 산출물이 증거 폴더에 모이게 해 attempt 산출물 수집을 단순하게 유지함.
            string directory = Path.Combine(root, "Docs", "Workflow", "Local",
                "evidence", "boogle", "motion-recording",
                runGuid.ToString("D"), requestGuid.ToString("D"));
            try
            {
                Directory.CreateDirectory(directory);
                capture = new FbxMotionRecordingEvidenceCapture(
                    pipeline, directory, requestGuid.ToString("D"), request);
                capture._events.Add($"input={request.fbx_file}");
                // VMD 자동 변환이 붙으면 임포트가 수 분 걸리므로 꺼둠 — Play 종료 시 원복됨.
                pipeline.ShouldRecordVmdAfterImport = false;
                if (!pipeline.TryStartFbxImportFromSharedSettings(input))
                {
                    message = "제품 FBX 가져오기 요청이 거부되었습니다.";
                    capture = null;
                    return false;
                }
                return true;
            }
            catch (Exception error)
            {
                // 임포트 시작이 throw하면 Finish/Dispose가 안 불리므로 여기서 원복함.
                if (capture != null)
                {
                    pipeline.ShouldRecordVmdAfterImport = capture._originalAutoRecord;
                }
                capture = null;
                message = $"영상 녹화 증거 시작 실패: {error.Message}";
                return false;
            }
        }

        internal void Poll()
        {
            if (IsFinished) return;
            try
            {
                if (!EditorApplication.isPlaying || _pipeline == null ||
                    !IsSupportedScenePath(SceneManager.GetActiveScene().path))
                {
                    throw new InvalidOperationException("Play·씬 상태가 변경되었습니다.");
                }
                // Recorder의 프레임 정확도 모드는 captureFramerate를 쓰므로 녹화 중이 아닐 때만 검사함.
                if (Time.captureFramerate != 0 && !_pipeline.IsImportedMotionRecording)
                {
                    throw new InvalidOperationException("captureFramerate가 녹화 외 경로로 잔류했습니다.");
                }
                if (DateTime.UtcNow - _startedUtc > TimeSpan.FromMinutes(10))
                    throw new TimeoutException("영상 녹화 증거 수집 시간이 초과되었습니다.");
                if (_pipeline.SessionState == FBXVmdPipeline.FBXSessionState.Failed)
                {
                    Finish(_phase.ToString(), _pipeline.LastSessionMessage);
                    return;
                }

                switch (_phase)
                {
                    case Phase.Importing:
                        PollImporting();
                        break;
                    case Phase.Starting:
                        PollStarting();
                        break;
                    case Phase.Recording:
                        PollRecording();
                        break;
                    case Phase.Stopping:
                        PollStopping();
                        break;
                }
            }
            catch (TimeoutException error) { Finish("timeout", error.Message); }
            catch (Exception error) { Finish(_phase.ToString(), error.ToString()); }
        }

        private void PollImporting()
        {
            if (_pipeline.IsProcessing || !_pipeline.HasPreparedImportedMotion ||
                _pipeline.SessionState != FBXVmdPipeline.FBXSessionState.Ready) return;
            _events.Add("import_ready");
            _pipeline.SetMotionVideoRecordingOptionsForEvidence(
                _skipLeadingStill, _rangeEnabled,
                _rangeStartFrame, _rangeEndFrame, _directory);
            if (!_pipeline.TryStartImportedMotionRecording())
            {
                Finish("recording_start", _pipeline.LastSessionMessage);
                return;
            }
            _phase = Phase.Starting;
        }

        private void PollStarting()
        {
            // 보정 준비가 큐에 들어간 동안은 준비 완료까지 대기함.
            if (_pipeline.IsPreparingImportedMotionCorrection) return;
            if (!_pipeline.IsImportedMotionRecording)
            {
                // 즉시 시작·준비 큐 둘 다 실패하면 세션 메시지에 거절 사유가 남음.
                Finish("recording_start", _pipeline.LastSessionMessage);
                return;
            }
            HumanoidMotionRecordingController controller = _pipeline.MotionRecordingController;
            _resolvedStartSeconds = controller != null ? controller.RangeStartSeconds : 0f;
            _resolvedEndSeconds = controller != null ? controller.RangeEndSeconds : 0f;
            // 레코더가 정지 시 출력 경로를 지울 수 있으므로 녹화 중에 미리 확보함.
            _outputPath = controller != null ? controller.OutputFilePath ?? string.Empty : string.Empty;
            _recordingStartedUtc = DateTime.UtcNow;
            _events.Add(
                $"recording_started start={_resolvedStartSeconds:F2}s " +
                $"end={_resolvedEndSeconds:F2}s");
            _phase = Phase.Recording;
        }

        private void PollRecording()
        {
            if (!_pipeline.IsImportedMotionRecording)
            {
                // 구간 녹화의 자동 정지 경로.
                _events.Add("recording_auto_stopped");
                _phase = Phase.Stopping;
                return;
            }

            double elapsed = (DateTime.UtcNow - _recordingStartedUtc).TotalSeconds;
            if (_tryImportDuringRecording && !_importAttempted &&
                elapsed >= ImportBlockedWallSeconds)
            {
                _importAttempted = true;
                AttemptBlockedImport();
            }

            if (_stopAfterSeconds > 0f && elapsed >= _stopAfterSeconds)
            {
                _events.Add($"manual_stop elapsed={elapsed:F1}s");
                _pipeline.TryStopImportedMotionRecording();
                _phase = Phase.Stopping;
            }
        }

        // 녹화 중 재임포트가 거부되는지 검증 — 거부 메시지를 증거에 남김.
        private void AttemptBlockedImport()
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string blockedPath = Path.Combine(
                root, "Assets", "Resources", "Import_FBX",
                $"__e2e_blocked_{_requestId}.fbx");
            _pipeline.ProcessFBXAsync(blockedPath);
            _importRejectMessage = _pipeline.LastSessionMessage ?? string.Empty;
            _importRejected = _pipeline.SessionState ==
                FBXVmdPipeline.FBXSessionState.Recording &&
                _importRejectMessage.IndexOf("녹화 중에는", StringComparison.Ordinal) >= 0;
            _events.Add(
                $"import_attempt rejected={_importRejected} message={_importRejectMessage}");
        }

        private void PollStopping()
        {
            // Recorder 정리는 비동기 — 파일 크기가 연속 두 번 안정될 때까지 대기함.
            if (string.IsNullOrEmpty(_outputPath))
            {
                HumanoidMotionRecordingController controller = _pipeline.MotionRecordingController;
                _outputPath = controller != null ? controller.OutputFilePath ?? string.Empty : string.Empty;
            }
            if (string.IsNullOrEmpty(_outputPath) || DateTime.UtcNow < _nextFilePollUtc) return;
            _nextFilePollUtc = DateTime.UtcNow.AddSeconds(FileStablePollIntervalSeconds);
            long size = File.Exists(_outputPath) ? new FileInfo(_outputPath).Length : -1;
            if (size == _lastObservedSize)
            {
                _stableSizePolls++;
            }
            else
            {
                _stableSizePolls = 0;
                _lastObservedSize = size;
            }
            if (_stableSizePolls < FileStablePollsRequired) return;
            WriteEvidence();
            Finish(string.Empty, string.Empty);
        }

        private void WriteEvidence()
        {
            HumanoidMotionRecordingController controller = _pipeline.MotionRecordingController;
            int recordedTicks = controller != null ? controller.RecordedPoseTicks : 0;
            float clipFrameRate = _pipeline.ImportedMotionFrameRate;
            float clipLength = _pipeline.ImportedMotionClipLengthSeconds;
            // 기대 틱 수 = 구간 길이(초) × 영상 fps. 시작 자세는 TryStart의 Seek가
            // 적용해 틱 집계에서 빠지고, 마지막 틱이 종료 자세를 평가하므로 총 캡처
            // 프레임은 틱 수 + 1이다.
            int expectedTicks = _rangeEnabled && _resolvedEndSeconds > _resolvedStartSeconds
                ? Mathf.RoundToInt(
                    (_resolvedEndSeconds - _resolvedStartSeconds) *
                    (float)_pipeline.motionVideoFrameRate)
                : -1;
            var evidence = new MotionRecordingEvidence
            {
                request_id = _requestId,
                fbx_file = _inputFileName,
                skip_leading_still = _skipLeadingStill,
                range_enabled = _rangeEnabled,
                range_start_frame = _rangeStartFrame,
                range_end_frame = _rangeEndFrame,
                resolved_start_seconds = _resolvedStartSeconds,
                resolved_end_seconds = _resolvedEndSeconds,
                recorded_pose_ticks = recordedTicks,
                expected_pose_ticks = expectedTicks,
                output_path = _outputPath,
                output_exists = !string.IsNullOrEmpty(_outputPath) && File.Exists(_outputPath),
                output_file_size_bytes = File.Exists(_outputPath)
                    ? new FileInfo(_outputPath).Length : 0,
                clip_length_seconds = clipLength,
                clip_frame_rate = clipFrameRate,
                video_frame_rate = (float)_pipeline.motionVideoFrameRate,
                record_wall_seconds =
                    (float)(DateTime.UtcNow - _recordingStartedUtc).TotalSeconds,
                import_attempted = _importAttempted,
                import_rejected = _importRejected,
                import_reject_message = _importRejectMessage,
                session_state_final = _pipeline.SessionState.ToString(),
                still_recording_final = _pipeline.IsImportedMotionRecording,
                events = _events.ToArray()
            };
            File.WriteAllText(StatePath, JsonConvert.SerializeObject(evidence, Formatting.Indented));
            HasEvidence = true;
        }

        private void Finish(string stage, string message)
        {
            RestorePipelineOptions();
            if (!HasEvidence)
            {
                FailureStage = stage;
                FailureMessage = message;
            }
            _phase = Phase.Finished;
        }

        public void Dispose()
        {
            RestorePipelineOptions();
            if (_pipeline.IsImportedMotionRecording)
            {
                _pipeline.TryStopImportedMotionRecording();
            }
        }

        // 강제한 녹화 옵션·자동 변환 플래그를 원본으로 되돌려 같은 Play 세션의
        // 후속 녹화가 증거 수집 설정을 상속받지 않게 함.
        private void RestorePipelineOptions()
        {
            _pipeline.ShouldRecordVmdAfterImport = _originalAutoRecord;
            _pipeline.SetMotionVideoRecordingOptionsForEvidence(
                _originalSkipLeadingStill, _originalRangeEnabled,
                _originalRangeStartFrame, _originalRangeEndFrame,
                _originalOutputFolder);
        }

        private static bool IsSupportedScenePath(string path)
        {
            return path == "Assets/_Project/Scene/Main_Auto.unity" ||
                path == "Assets/_Project/Scene/Main_Recoding.unity";
        }

        [Serializable]
        private sealed class MotionRecordingEvidence
        {
            public string request_id;
            public string fbx_file;
            public bool skip_leading_still;
            public bool range_enabled;
            public int range_start_frame;
            public int range_end_frame;
            public float resolved_start_seconds;
            public float resolved_end_seconds;
            public int recorded_pose_ticks;
            public int expected_pose_ticks;
            public string output_path;
            public bool output_exists;
            public long output_file_size_bytes;
            public float clip_length_seconds;
            public float clip_frame_rate;
            public float video_frame_rate;
            public float record_wall_seconds;
            public bool import_attempted;
            public bool import_rejected;
            public string import_reject_message;
            public string session_state_final;
            public bool still_recording_final;
            public string[] events;
        }
    }
}
#endif
