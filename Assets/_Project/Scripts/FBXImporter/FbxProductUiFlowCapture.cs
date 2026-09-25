#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FileSystem;
using Fbx2Vmd.Settings;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class FbxProductUiFlowCapture : IDisposable
    {
        private const string InputFileName = "Snake Hip Hop Dance.fbx";
        private enum Phase
        {
            WaitingForResolution, BeforeImport, Importing, ImportReady, Preparing,
            PlayingCaptured, Paused, PausedCaptured, Recording, RecordingCaptured,
            RecordingStoppedCaptured, StoppedCaptured, WaitingForErrorResolution,
            Error, ErrorCaptured, Finished
        }

        private sealed class SelectedFileBrowser : IFileBrowserService
        {
            internal string Path;
            public string[] OpenFilePanel(string title, string directory, string extension,
                bool multiselect) => new[] { Path };
        }

        private readonly FBXVmdPipeline _pipeline;
        private readonly string _directory;
        private readonly string _requestId;
        private readonly SelectedFileBrowser _browser = new SelectedFileBrowser();
        private readonly List<object> _events = new List<object>();
        private readonly DateTime _startedUtc = DateTime.UtcNow;
        private readonly bool _originalAutoRecord;
        private readonly RecordingDiagnosticsSettings _originalRecordingSettings;
        private readonly EditorWindow _gameView;
        private readonly PropertyInfo _selectedSizeIndex;
        private readonly MethodInfo _setCustomResolution;
        private readonly int _originalSizeIndex;
        private readonly int _screenWidth;
        private readonly int _screenHeight;
        private Phase _phase;
        private bool _capturedPreparation;
        private string _videoPath = string.Empty;
        private DateTime _nextActionUtc;

        private FbxProductUiFlowCapture(FBXVmdPipeline pipeline, string directory,
            string requestId, int screenWidth, int screenHeight)
        {
            _pipeline = pipeline;
            _directory = directory;
            _requestId = requestId;
            _originalAutoRecord = pipeline.ShouldRecordVmdAfterImport;
            _originalRecordingSettings = pipeline.DiagnosticsSettings;
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            _gameView = gameViewType != null
                ? Resources.FindObjectsOfTypeAll(gameViewType).FirstOrDefault() as EditorWindow
                : null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic;
            _selectedSizeIndex = gameViewType?.GetProperty("selectedSizeIndex", flags);
            _setCustomResolution = gameViewType?.GetMethod("SetCustomResolution", flags);
            if (_gameView == null || _selectedSizeIndex == null || _setCustomResolution == null)
                throw new InvalidOperationException("Game View 크기 설정을 찾지 못했습니다.");
            _originalSizeIndex = (int)_selectedSizeIndex.GetValue(_gameView);
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            StatePath = System.IO.Path.Combine(directory, "state.json");
        }

        internal string StatePath { get; }
        internal bool IsFinished => _phase == Phase.Finished;
        internal bool HasEvidence { get; private set; }
        internal string FailureStage { get; private set; } = string.Empty;
        internal string FailureMessage { get; private set; } = string.Empty;

        internal static bool TryStart(FBXVmdPipeline pipeline, string requestId, string runId,
            int screenWidth, int screenHeight,
            out FbxProductUiFlowCapture capture, out string message)
        {
            capture = null;
            message = string.Empty;
            if (pipeline == null || !Guid.TryParse(requestId, out Guid requestGuid) ||
                !Guid.TryParse(runId, out Guid runGuid) || !EditorApplication.isPlaying ||
                screenWidth < 320 || screenWidth > 3840 ||
                screenHeight < 320 || screenHeight > 3840 ||
                SceneManager.GetActiveScene().path != "Assets/_Project/Scene/Main_Auto.unity" ||
                pipeline.IsProcessing || pipeline.HasPreparedImportedMotion ||
                Time.captureFramerate != 0 || EventSystem.current == null ||
                !TryFindButton("FBX_Button", out _))
            {
                message = "F15는 저장된 Main_Auto Play와 활성 FBX 버튼·EventSystem이 필요합니다.";
                return false;
            }

            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string input = System.IO.Path.Combine(root, "Assets", "Resources", "Import_FBX",
                InputFileName);
            if (!File.Exists(input))
            {
                message = "F15 입력 FBX가 없습니다.";
                return false;
            }

            string directory = System.IO.Path.Combine(root, "Docs", "Workflow", "Local",
                "evidence", "boogle", "product-ui", runGuid.ToString("D"),
                requestGuid.ToString("D"));
            try
            {
                Directory.CreateDirectory(directory);
                capture = new FbxProductUiFlowCapture(pipeline, directory,
                    requestGuid.ToString("D"), screenWidth, screenHeight);
                // UI 수동 녹화와 자동 VMD 출력을 분리해 기존 사용자 파일을 보호함.
                pipeline.ShouldRecordVmdAfterImport = false;
                capture.SetGameViewResolution();
                capture._browser.Path = input;
                capture._nextActionUtc = DateTime.UtcNow.AddMilliseconds(150);
                capture._phase = Phase.WaitingForResolution;
                return true;
            }
            catch (Exception error)
            {
                if (capture != null)
                {
                    capture._pipeline.ShouldRecordVmdAfterImport = capture._originalAutoRecord;
                    capture.RestoreGameViewResolution();
                    capture = null;
                }
                message = $"F15 시작 실패: {error.Message}";
                return false;
            }
        }

        internal void Poll()
        {
            if (IsFinished) return;
            try
            {
                if (!EditorApplication.isPlaying ||
                    SceneManager.GetActiveScene().path != "Assets/_Project/Scene/Main_Auto.unity" ||
                    (Time.captureFramerate != 0 && !_pipeline.IsImportedMotionRecording))
                    throw new InvalidOperationException("Play·씬·프레임률 상태가 변경되었습니다.");
                if (DateTime.UtcNow - _startedUtc > TimeSpan.FromMinutes(5))
                    throw new TimeoutException("F15 UI 흐름 대기 시간이 초과되었습니다.");
                if (_pipeline.SessionState == FBXVmdPipeline.FBXSessionState.Failed &&
                    _phase != Phase.Error && _phase != Phase.ErrorCaptured)
                {
                    Record("product_error", true);
                    Finish("product", _pipeline.LastSessionMessage);
                    return;
                }

                switch (_phase)
                {
                    case Phase.WaitingForResolution:
                        if (Screen.width != _screenWidth || Screen.height != _screenHeight) return;
                        Record("before_import", true);
                        _nextActionUtc = DateTime.UtcNow.AddMilliseconds(150);
                        _phase = Phase.BeforeImport;
                        break;
                    case Phase.BeforeImport:
                        if (DateTime.UtcNow < _nextActionUtc ||
                            _pipeline.ImportController == null) return;
                        Click("FBX_Button", _browser.Path);
                        _phase = Phase.Importing;
                        break;
                    case Phase.Importing:
                        if (!_pipeline.HasPreparedImportedMotion ||
                            _pipeline.SessionState != FBXVmdPipeline.FBXSessionState.Ready) return;
                        Record("import_ready", true);
                        _nextActionUtc = DateTime.UtcNow.AddMilliseconds(150);
                        _phase = Phase.ImportReady;
                        break;
                    case Phase.ImportReady:
                        if (DateTime.UtcNow < _nextActionUtc) return;
                        Click("FBX_PlayPause_Button");
                        _phase = Phase.Preparing;
                        break;
                    case Phase.Preparing:
                        if (_pipeline.IsPreparingImportedMotionCorrection && !_capturedPreparation)
                        {
                            Record("correction_preparing", true);
                            _capturedPreparation = true;
                        }
                        if (!_pipeline.IsImportedMotionPlaying ||
                            !HasButtonLabel("FBX_PlayPause_Button", "일시정지")) return;
                        Record("playing", true);
                        _nextActionUtc = DateTime.UtcNow.AddMilliseconds(150);
                        _phase = Phase.PlayingCaptured;
                        break;
                    case Phase.PlayingCaptured:
                        if (DateTime.UtcNow < _nextActionUtc) return;
                        Click("FBX_PlayPause_Button");
                        _phase = Phase.Paused;
                        break;
                    case Phase.Paused:
                        if (_pipeline.SessionState != FBXVmdPipeline.FBXSessionState.PreviewPaused)
                            return;
                        Record("paused", true);
                        _nextActionUtc = DateTime.UtcNow.AddMilliseconds(150);
                        _phase = Phase.PausedCaptured;
                        break;
                    case Phase.PausedCaptured:
                        if (DateTime.UtcNow < _nextActionUtc) return;
                        string recordings = System.IO.Path.Combine(
                            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                            "Recordings");
                        for (int offset = 0; offset <= 2; offset++)
                        {
                            string name = $"Snake Hip Hop Dance_{DateTime.Now.AddSeconds(offset):yyyyMMdd_HHmmss}.mp4";
                            if (File.Exists(System.IO.Path.Combine(recordings, name)))
                                throw new InvalidOperationException("동일 시각의 기존 녹화 파일이 있습니다.");
                        }
                        _pipeline.DiagnosticsSettings = new RecordingDiagnosticsSettings(
                            _originalRecordingSettings.EnableRecordingDiagnostics,
                            _originalRecordingSettings.UseDeterministicCaptureFramerateForDiagnostics,
                            _originalRecordingSettings.EnableDiagnosticFingerCloseups,
                            RecordingCaptureQualityPreset.Custom,
                            _screenWidth,
                            _screenHeight);
                        Click("FBX_Record_Button");
                        _phase = Phase.Recording;
                        break;
                    case Phase.Recording:
                        if (!_pipeline.IsImportedMotionRecording ||
                            !HasButtonLabel("FBX_Record_Button", "녹화 중지")) return;
                        Record("recording", true);
                        _nextActionUtc = DateTime.UtcNow.AddMilliseconds(150);
                        _phase = Phase.RecordingCaptured;
                        break;
                    case Phase.RecordingCaptured:
                        if (DateTime.UtcNow < _nextActionUtc) return;
                        Click("FBX_Record_Button");
                        _videoPath = _pipeline.LastSessionMessage;
                        Record("recording_stopped", true);
                        _nextActionUtc = DateTime.UtcNow.AddMilliseconds(150);
                        _phase = Phase.RecordingStoppedCaptured;
                        break;
                    case Phase.RecordingStoppedCaptured:
                        if (DateTime.UtcNow < _nextActionUtc) return;
                        Click("FBX_Stop_Button");
                        Record("stopped", true);
                        _nextActionUtc = DateTime.UtcNow.AddMilliseconds(150);
                        _phase = Phase.StoppedCaptured;
                        break;
                    case Phase.StoppedCaptured:
                        if (DateTime.UtcNow < _nextActionUtc) return;
                        SetGameViewResolution();
                        _phase = Phase.WaitingForErrorResolution;
                        break;
                    case Phase.WaitingForErrorResolution:
                        if (Screen.width != _screenWidth || Screen.height != _screenHeight) return;
                        string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                        string missing = System.IO.Path.Combine(root, "Assets", "Resources",
                            "Import_FBX", $"__e2e_missing_{_requestId}.fbx");
                        if (File.Exists(missing))
                            throw new InvalidOperationException("오류 입력 경로가 이미 존재합니다.");
                        Click("FBX_Button", missing);
                        _phase = Phase.Error;
                        break;
                    case Phase.Error:
                        if (_pipeline.IsProcessing) return;
                        if (_pipeline.SessionState != FBXVmdPipeline.FBXSessionState.Failed)
                            throw new InvalidOperationException("잘못된 입력의 오류 상태가 표시되지 않았습니다.");
                        Record("invalid_input_error", true);
                        _nextActionUtc = DateTime.UtcNow.AddMilliseconds(150);
                        _phase = Phase.ErrorCaptured;
                        break;
                    case Phase.ErrorCaptured:
                        if (DateTime.UtcNow < _nextActionUtc) return;
                        Finish(string.Empty, string.Empty);
                        break;
                }
            }
            catch (TimeoutException error) { Finish("timeout", error.Message); }
            catch (Exception error) { Finish("ui_capture", error.ToString()); }
        }

        private void Click(string buttonName, string selectedPath = null)
        {
            if (!TryFindButton(buttonName, out Button button) ||
                !button.gameObject.activeInHierarchy || !button.interactable)
                throw new InvalidOperationException($"{buttonName} 버튼을 누를 수 없습니다.");
            if (selectedPath == null)
            {
                if (!ExecuteEvents.Execute(button.gameObject,
                    new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler))
                    throw new InvalidOperationException($"{buttonName} 클릭 이벤트가 없습니다.");
                return;
            }

            FBXImportController original = _pipeline.ImportController;
            FieldInfo field = typeof(FBXVmdPipeline).GetField("_importController",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (original == null || field == null)
                throw new InvalidOperationException("FBX 파일 선택 컨트롤러를 찾지 못했습니다.");
            _browser.Path = selectedPath;
            try
            {
                field.SetValue(_pipeline, new FBXImportController(_pipeline, _browser,
                    new AssimpFBXImporter().ImportAsync));
                if (!ExecuteEvents.Execute(button.gameObject,
                    new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler))
                    throw new InvalidOperationException("FBX 버튼 클릭 이벤트가 없습니다.");
            }
            finally { field.SetValue(_pipeline, original); }
        }

        private void Record(string step, bool captureImage)
        {
            Button[] buttons = new[] { "FBX_Button", "FBX_PlayPause_Button",
                "FBX_Record_Button", "FBX_Stop_Button" }
                .Select(name => TryFindButton(name, out Button button) ? button : null).ToArray();
            HumanoidSampleCode progress = _pipeline.targetCharacter != null
                ? _pipeline.targetCharacter.GetComponentInChildren<HumanoidSampleCode>(true) : null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            TMP_Text progressText = progress != null ? typeof(HumanoidSampleCode)
                .GetField("_progressText", flags)?.GetValue(progress) as TMP_Text : null;
            Text fallbackText = progress != null ? typeof(HumanoidSampleCode)
                .GetField("_progressFallbackText", flags)?.GetValue(progress) as Text : null;
            Slider progressSlider = progress != null ? typeof(HumanoidSampleCode)
                .GetField("_progressSlider", flags)?.GetValue(progress) as Slider : null;
            string imagePath = string.Empty;
            if (captureImage)
            {
                string when = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff",
                    CultureInfo.InvariantCulture);
                imagePath = System.IO.Path.Combine(_directory,
                    $"when-{when}_where-Main_Auto_who-auto_what-{step}_why-F15_how-GameView.png");
                ScreenCapture.CaptureScreenshot(imagePath);
            }
            _events.Add(new
            {
                step,
                session_state = _pipeline.SessionState.ToString(),
                session_message = _pipeline.LastSessionMessage,
                is_processing = _pipeline.IsProcessing,
                is_playing = _pipeline.IsImportedMotionPlaying,
                is_recording = _pipeline.IsImportedMotionRecording,
                correction_processed_frames = _pipeline.ImportedMotionCorrectionProcessedFrameCount,
                buttons = buttons.Select((button, index) => new
                {
                    name = new[] { "FBX_Button", "FBX_PlayPause_Button",
                        "FBX_Record_Button", "FBX_Stop_Button" }[index],
                    active = button != null && button.gameObject.activeInHierarchy,
                    interactable = button != null && button.interactable,
                    label = button != null ? (button.GetComponentInChildren<TMP_Text>(true)?.text ??
                        button.GetComponentInChildren<Text>(true)?.text ?? string.Empty) : string.Empty
                }).ToArray(),
                progress_text = fallbackText != null && fallbackText.enabled
                    ? fallbackText.text : progressText != null ? progressText.text : string.Empty,
                progress = progressSlider != null ? (float?)progressSlider.value : null,
                screen_width = Screen.width,
                screen_height = Screen.height,
                game_view_path = imagePath
            });
        }

        private void Finish(string stage, string message)
        {
            if (IsFinished) return;
            FailureStage = stage;
            FailureMessage = message;
            try
            {
                HasEvidence = string.IsNullOrEmpty(stage) && _events.Count >= 6;
                File.WriteAllText(StatePath, JsonConvert.SerializeObject(new
                {
                    status = HasEvidence ? "manual_review_required" : "failed",
                    failure_stage = stage,
                    failure_message = message,
                    input = InputFileName,
                    model = _pipeline.targetCharacter != null
                        ? _pipeline.targetCharacter.name : string.Empty,
                    auto_vmd_recording_suppressed = true,
                    requested_screen_width = _screenWidth,
                    requested_screen_height = _screenHeight,
                    video_result_message = _videoPath,
                    events = _events
                }, Formatting.Indented));
            }
            catch (Exception error)
            {
                HasEvidence = false;
                FailureStage = "evidence";
                FailureMessage = error.ToString();
            }
            finally
            {
                if (_pipeline.IsImportedMotionRecording)
                    _pipeline.TryStopImportedMotionRecording();
                _pipeline.DiagnosticsSettings = _originalRecordingSettings;
                _pipeline.ShouldRecordVmdAfterImport = _originalAutoRecord;
                RestoreGameViewResolution();
                _phase = Phase.Finished;
            }
        }

        private void SetGameViewResolution()
        {
            if (Screen.width == _screenWidth && Screen.height == _screenHeight)
                return;
            _setCustomResolution.Invoke(_gameView,
                new object[] { new Vector2(_screenWidth, _screenHeight),
                    $"F15 {_screenWidth}x{_screenHeight}" });
        }

        private void RestoreGameViewResolution()
        {
            _selectedSizeIndex.SetValue(_gameView, _originalSizeIndex);
        }

        public void Dispose()
        {
            if (!IsFinished) Finish("cancelled", "F15 수집이 종료되었습니다.");
        }

        private static bool TryFindButton(string name, out Button button)
        {
            GameObject gameObject = GameObject.Find(name);
            button = gameObject != null ? gameObject.GetComponent<Button>() : null;
            return button != null;
        }

        private static bool HasButtonLabel(string name, string label)
        {
            return TryFindButton(name, out Button button) &&
                (button.GetComponentInChildren<TMP_Text>(true)?.text ??
                    button.GetComponentInChildren<Text>(true)?.text) == label;
        }
    }
}
#endif
