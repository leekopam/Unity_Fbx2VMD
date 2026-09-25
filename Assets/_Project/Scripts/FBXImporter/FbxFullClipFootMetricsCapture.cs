#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 제품 재생 경로의 전체 클립을 정수 프레임마다 평가해 하체 수치를 기록함.
    /// </summary>
    internal sealed class FbxFullClipFootMetricsCapture : IDisposable
    {
        private const string DefaultInputFileName = "satisfaction_2.fbx";
        private const string ScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const int FramesPerPoll = 12;
        private enum Phase { Importing, Preparing, Scanning, Finished }

        private readonly FBXVmdPipeline _pipeline;
        private readonly string _inputFileName;
        private readonly string _caseId;
        private readonly int _frameLimit;
        private readonly bool _captureViews;
        private readonly List<string> _capturePaths = new List<string>();
        private readonly DateTime _startedUtc = DateTime.UtcNow;
        private StreamWriter _writer;
        private HumanoidMotionPlaybackController _controller;
        private Animator _animator;
        private Transform _hips;
        private readonly Transform[] _knees = new Transform[2];
        private readonly Transform[] _feet = new Transform[2];
        private readonly Transform[] _toes = new Transform[2];
        private readonly Vector3[] _previousRear = new Vector3[2];
        private readonly Vector3[] _previousFront = new Vector3[2];
        private readonly int[] _previousRearVertex = new int[2];
        private readonly int[] _previousFrontVertex = new int[2];
        private readonly string[] _previousRearRenderer = new string[2];
        private readonly string[] _previousFrontRenderer = new string[2];
        private readonly Quaternion[] _previousFootRotation = new Quaternion[2];
        private readonly bool[] _hasPrevious = new bool[2];
        private IReadOnlyList<HumanoidFootContactSample> _sourceSamples;
        private IReadOnlyList<HumanoidFootContactSample> _targetSamples;
        private float _sourceHumanScale;
        private Phase _phase = Phase.Importing;
        private int _frame;
        private int _lastFrame;
        private int _rowCount;
        private float _frameRate;
        private float _maximumTimeErrorMilliseconds;
        private float _maximumPenetrationMillimeters;
        private float _maximumSoleStepMillimeters;
        private float _maximumFootRotationStepDegrees;
        private float _maximumHipsStepMillimeters;
        private float _previousHipsY;
        private int _maximumSoleStepFrame;
        private int _maximumFootRotationStepFrame;
        private int _maximumHipsStepFrame;

        private FbxFullClipFootMetricsCapture(FBXVmdPipeline pipeline, string directory,
            string inputFileName, string caseId, int frameLimit, bool captureViews)
        {
            _pipeline = pipeline;
            _inputFileName = inputFileName;
            _caseId = caseId;
            _frameLimit = frameLimit;
            _captureViews = captureViews;
            CsvPath = Path.Combine(directory, "all-frames.csv");
            StatePath = Path.Combine(directory, "state.json");
        }

        internal string CsvPath { get; }
        internal string StatePath { get; }
        internal bool IsFinished => _phase == Phase.Finished;
        internal bool HasEvidence { get; private set; }
        internal string FailureStage { get; private set; } = string.Empty;
        internal string FailureMessage { get; private set; } = string.Empty;
        internal string CaseId => _caseId;

        internal static bool TryStart(FBXVmdPipeline pipeline, string requestId, string runId,
            out FbxFullClipFootMetricsCapture capture, out string message,
            string inputFileName = DefaultInputFileName, string caseId = "F10",
            int frameLimit = 0, bool captureViews = false)
        {
            capture = null;
            message = string.Empty;
            if (pipeline == null || !Guid.TryParse(requestId, out Guid requestGuid) ||
                !Guid.TryParse(runId, out Guid runGuid) || !EditorApplication.isPlaying ||
                SceneManager.GetActiveScene().path != ScenePath || pipeline.IsProcessing ||
                pipeline.HasPreparedImportedMotion || pipeline.IsImportedMotionRecording ||
                Time.captureFramerate != 0 || pipeline.targetCharacter == null)
            {
                message = $"{caseId}는 저장된 Main_Auto의 비녹화 Play 상태와 유효한 실행 ID가 필요합니다.";
                return false;
            }

            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            if (string.IsNullOrWhiteSpace(inputFileName) ||
                Path.GetFileName(inputFileName) != inputFileName)
            {
                message = "입력 FBX 파일명이 유효하지 않습니다.";
                return false;
            }
            string input = Path.Combine(root, "Assets", "Resources", "Import_FBX", inputFileName);
            if (!File.Exists(input))
            {
                message = $"{caseId} 입력 FBX가 없습니다: {inputFileName}";
                return false;
            }

            string directory = Path.Combine(root, "Docs", "Workflow", "Local", "evidence",
                "boogle", caseId.StartsWith("VRM_", StringComparison.Ordinal)
                    ? "vrm-character" : caseId == "F14" ? "full-clip-f14" : "full-clip",
                runGuid.ToString("D"), requestGuid.ToString("D"));
            try
            {
                Directory.CreateDirectory(directory);
                capture = new FbxFullClipFootMetricsCapture(pipeline, directory,
                    inputFileName, caseId, frameLimit, captureViews);
                if (pipeline.TryStartFbxImportFromSharedSettings(input)) return true;
                message = $"{caseId} 제품 FBX 가져오기 요청이 거부되었습니다.";
            }
            catch (Exception error) { message = $"{caseId} 시작 실패: {error.Message}"; }
            capture = null;
            return false;
        }

        internal void Poll()
        {
            if (IsFinished) return;
            try
            {
                if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath ||
                    _pipeline == null || Time.captureFramerate != 0)
                    throw new InvalidOperationException("Play·씬·프레임률 상태가 변경되었습니다.");
                if (DateTime.UtcNow - _startedUtc > TimeSpan.FromMinutes(30))
                    throw new TimeoutException($"{_caseId} 전체 프레임 수집 시간이 초과되었습니다.");
                if (_pipeline.SessionState == FBXVmdPipeline.FBXSessionState.Failed)
                {
                    string stage = _phase == Phase.Preparing
                        ? "native_skinning_preparation" : "import_or_retarget";
                    Finish(stage, _pipeline.LastSessionMessage);
                    return;
                }

                switch (_phase)
                {
                    case Phase.Importing:
                        if (_pipeline.IsProcessing || !_pipeline.HasPreparedImportedMotion ||
                            _pipeline.SessionState != FBXVmdPipeline.FBXSessionState.Ready) return;
                        if (!_pipeline.TryPlayImportedMotion())
                        {
                            Finish("playback_start", _pipeline.LastSessionMessage);
                            return;
                        }
                        _phase = Phase.Preparing;
                        break;
                    case Phase.Preparing:
                        if (_pipeline.IsPreparingImportedMotionCorrection ||
                            !_pipeline.IsImportedMotionPlaying) return;
                        if (!_pipeline.TryPauseImportedMotion())
                            throw new InvalidOperationException($"{_caseId} 준비 후 일시정지 실패");
                        Initialize();
                        _phase = Phase.Scanning;
                        break;
                    case Phase.Scanning:
                        for (int count = 0; count < FramesPerPoll && _frame <= _lastFrame; count++)
                        {
                            CaptureFrame(_frame);
                            _frame++;
                        }
                        if (_frame > _lastFrame) Finish(string.Empty, string.Empty);
                        break;
                }
            }
            catch (TimeoutException error) { Finish("timeout", error.Message); }
            catch (Exception error) { Finish("capture", error.ToString()); }
        }

        private void Initialize()
        {
            _controller = (HumanoidMotionPlaybackController)typeof(FBXVmdPipeline)
                .GetField("_humanoidMotionPlaybackController", BindingFlags.Instance |
                    BindingFlags.NonPublic)?.GetValue(_pipeline);
            _animator = _pipeline.targetCharacter.GetComponent<Animator>();
            _hips = _animator?.GetBoneTransform(HumanBodyBones.Hips);
            _knees[0] = _animator?.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _knees[1] = _animator?.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _feet[0] = _animator?.GetBoneTransform(HumanBodyBones.LeftFoot);
            _feet[1] = _animator?.GetBoneTransform(HumanBodyBones.RightFoot);
            _toes[0] = _animator?.GetBoneTransform(HumanBodyBones.LeftToes);
            _toes[1] = _animator?.GetBoneTransform(HumanBodyBones.RightToes);
            _frameRate = _pipeline.ImportedMotionFrameRate;
            _lastFrame = _frameLimit > 0
                ? Mathf.Min(_pipeline.ImportedMotionLastFrameIndex, _frameLimit - 1)
                : _pipeline.ImportedMotionLastFrameIndex;
            var stabilizer = _controller == null ? null :
                (EditorHumanoidFootContactStabilizer)typeof(HumanoidMotionPlaybackController)
                    .GetField("_footContactStabilizer", BindingFlags.Instance |
                        BindingFlags.NonPublic)?.GetValue(_controller);
            _sourceSamples = stabilizer?.SourceSamples;
            _targetSamples = stabilizer?.TargetSamples;
            _sourceHumanScale = stabilizer?.SourceHumanScale ?? 0f;
            if (_controller == null || _hips == null || _knees.Any(item => item == null) ||
                _feet.Any(item => item == null) || _toes.Any(item => item == null) ||
                !(_frameRate > 0f) || _lastFrame < 1 ||
                _sourceSamples == null || _sourceSamples.Count <= _lastFrame ||
                _targetSamples == null || _targetSamples.Count <= _lastFrame ||
                !(_sourceHumanScale > 0f))
                throw new InvalidOperationException($"{_caseId} 전체 클립 또는 하체 본이 준비되지 않았습니다.");

            _writer = new StreamWriter(CsvPath, false, new System.Text.UTF8Encoding(false));
            _writer.WriteLine("frame,time_s,time_error_ms,side,grounding_status,has_ground,support_role,rear_weight,front_weight,rear_signed_mm,front_signed_mm,minimum_signed_mm,rear_anchor_x_m,rear_anchor_y_m,rear_anchor_z_m,front_anchor_x_m,front_anchor_y_m,front_anchor_z_m,rear_point_x_m,rear_point_y_m,rear_point_z_m,front_point_x_m,front_point_y_m,front_point_z_m,rear_vertex,front_vertex,rear_step_mm,front_step_mm,foot_rotation_step_deg,foot_x_m,foot_y_m,foot_z_m,foot_pitch_deg,foot_yaw_deg,foot_roll_deg,toes_y_m,knee_y_m,hips_y_m,root_y_m,source_foot_y_m,source_toes_y_m,source_foot_speed_mps,source_toes_speed_mps,retarget_foot_y_m,retarget_toes_y_m,retarget_foot_speed_mps,retarget_toes_speed_mps");
        }

        private void CaptureFrame(int frame)
        {
            // 상태 로그 1만 건을 만들지 않도록 같은 제품 평가기의 내부 탐색만 호출함.
            if (!_controller.SeekFrame(frame) || _pipeline.ImportedMotionCurrentFrameIndex != frame ||
                !_pipeline.TryCaptureImportedMotionFootSurface(out HumanoidFootGroundingSnapshot left,
                    out HumanoidFootGroundingSnapshot right, out HumanoidFootGroundingStatus status))
                throw new InvalidOperationException($"{_caseId} {frame}프레임 탐색·접지 측정 실패");

            float time = _pipeline.ImportedMotionCurrentTimeSeconds;
            float timeError = Mathf.Abs(time - frame / _frameRate) * 1000f;
            if (!IsFinite(time) || !IsFinite(timeError) ||
                timeError > 500f / _frameRate + 0.01f)
                throw new InvalidOperationException($"F10 {frame}프레임 시간축 불일치: {timeError}ms");
            _maximumTimeErrorMilliseconds = Mathf.Max(_maximumTimeErrorMilliseconds, timeError);
            float hipsY = _hips.position.y;
            if (frame > 0)
            {
                float hipsStep = Mathf.Abs(hipsY - _previousHipsY) * 1000f;
                if (hipsStep > _maximumHipsStepMillimeters)
                { _maximumHipsStepMillimeters = hipsStep; _maximumHipsStepFrame = frame; }
            }
            _previousHipsY = hipsY;
            WriteFoot(frame, time, timeError, 0, status, left);
            WriteFoot(frame, time, timeError, 1, status, right);
            if (_captureViews && (frame == 0 || frame == _lastFrame))
                CaptureGameView(frame);
        }

        private void CaptureGameView(int frame)
        {
            string file = Path.Combine(Path.GetDirectoryName(CsvPath),
                $"frame-{frame:000000}.png");
            WriteGameViewPng(file);
            _capturePaths.Add(file);
        }

        internal static void WriteGameViewPng(string file, int width = 1280,
            int height = 720, bool excludeUi = false)
        {
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Main Camera가 없습니다.");
            var renderTexture = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            Canvas[] canvases = excludeUi
                ? UnityEngine.Object.FindObjectsOfType<Canvas>(true) : Array.Empty<Canvas>();
            bool[] canvasStates = new bool[canvases.Length];
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                for (int index = 0; index < canvases.Length; index++)
                {
                    canvasStates[index] = canvases[index].enabled;
                    canvases[index].enabled = false;
                }
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(file, texture.EncodeToPNG());
            }
            finally
            {
                for (int index = 0; index < canvases.Length; index++)
                    if (canvases[index] != null) canvases[index].enabled = canvasStates[index];
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
                UnityEngine.Object.Destroy(texture);
            }
        }

        private void WriteFoot(int frame, float time, float timeError, int side,
            HumanoidFootGroundingStatus status, HumanoidFootGroundingSnapshot foot)
        {
            if (foot == null) throw new InvalidOperationException($"F10 {frame}프레임 발 측정값 누락");
            Vector3 rear = foot.rear_point;
            Vector3 front = foot.front_point;
            Transform bone = _feet[side];
            bool rearStepValid = foot.has_ground && _hasPrevious[side] &&
                foot.rear_vertex == _previousRearVertex[side] &&
                foot.rear_renderer == _previousRearRenderer[side];
            bool frontStepValid = foot.has_ground && _hasPrevious[side] &&
                foot.front_vertex == _previousFrontVertex[side] &&
                foot.front_renderer == _previousFrontRenderer[side];
            float rearStep = rearStepValid ? Vector3.Distance(rear, _previousRear[side]) * 1000f : 0f;
            float frontStep = frontStepValid ? Vector3.Distance(front, _previousFront[side]) * 1000f : 0f;
            float rotationStep = frame > 0
                ? Quaternion.Angle(_previousFootRotation[side], bone.rotation) : 0f;
            float soleStep = Mathf.Max(rearStep, frontStep);
            if (soleStep > _maximumSoleStepMillimeters)
            { _maximumSoleStepMillimeters = soleStep; _maximumSoleStepFrame = frame; }
            if (rotationStep > _maximumFootRotationStepDegrees)
            { _maximumFootRotationStepDegrees = rotationStep; _maximumFootRotationStepFrame = frame; }
            _maximumPenetrationMillimeters = Mathf.Max(_maximumPenetrationMillimeters,
                -foot.minimum_distance_mm);
            _previousRear[side] = rear;
            _previousFront[side] = front;
            _previousRearVertex[side] = foot.rear_vertex;
            _previousFrontVertex[side] = foot.front_vertex;
            _previousRearRenderer[side] = foot.rear_renderer;
            _previousFrontRenderer[side] = foot.front_renderer;
            _previousFootRotation[side] = bone.rotation;
            _hasPrevious[side] = foot.has_ground;
            Vector3 angles = bone.localEulerAngles;
            Vector3 position = bone.position;
            HumanoidFootContactSample source = _sourceSamples[frame];
            Vector3 sourceFoot = side == 0 ? source.LeftFoot : source.RightFoot;
            Vector3 sourceToes = side == 0 ? source.LeftToes : source.RightToes;
            HumanoidFootContactSample previousSource = _sourceSamples[Mathf.Max(0, frame - 1)];
            Vector3 previousSourceFoot = side == 0 ? previousSource.LeftFoot : previousSource.RightFoot;
            Vector3 previousSourceToes = side == 0 ? previousSource.LeftToes : previousSource.RightToes;
            HumanoidFootContactSample target = _targetSamples[frame];
            Vector3 targetFoot = side == 0 ? target.LeftFoot : target.RightFoot;
            Vector3 targetToes = side == 0 ? target.LeftToes : target.RightToes;
            HumanoidFootContactSample previousTarget = _targetSamples[Mathf.Max(0, frame - 1)];
            Vector3 previousTargetFoot = side == 0 ? previousTarget.LeftFoot : previousTarget.RightFoot;
            Vector3 previousTargetToes = side == 0 ? previousTarget.LeftToes : previousTarget.RightToes;
            object[] values =
            {
                frame, time, timeError, side == 0 ? "left" : "right", status, foot.has_ground,
                foot.support_role, foot.rear_weight, foot.front_weight, foot.rear_distance_mm,
                foot.front_distance_mm, foot.minimum_distance_mm,
                foot.rear_anchor.x, foot.rear_anchor.y, foot.rear_anchor.z,
                foot.front_anchor.x, foot.front_anchor.y, foot.front_anchor.z,
                rear.x, rear.y, rear.z, front.x, front.y, front.z,
                foot.rear_vertex, foot.front_vertex,
                rearStepValid ? (object)rearStep : string.Empty,
                frontStepValid ? (object)frontStep : string.Empty, rotationStep,
                position.x, position.y, position.z, angles.x, angles.y, angles.z,
                _toes[side].position.y, _knees[side].position.y,
                _hips.position.y, _animator.transform.position.y,
                sourceFoot.y, sourceToes.y,
                frame > 0 ? (object)(Vector3.Distance(sourceFoot, previousSourceFoot) * _frameRate) : string.Empty,
                frame > 0 ? (object)(Vector3.Distance(sourceToes, previousSourceToes) * _frameRate) : string.Empty,
                targetFoot.y, targetToes.y,
                frame > 0 ? (object)(Vector3.Distance(targetFoot, previousTargetFoot) * _frameRate) : string.Empty,
                frame > 0 ? (object)(Vector3.Distance(targetToes, previousTargetToes) * _frameRate) : string.Empty
            };
            if (values.OfType<float>().Any(value => !IsFinite(value)))
                throw new InvalidOperationException($"{_caseId} {frame}프레임 비유한 하체 수치");
            _writer.WriteLine(string.Join(",", values.Select(value =>
                value is float number ? number.ToString("R", CultureInfo.InvariantCulture) : value)));
            _rowCount++;
        }

        private void Finish(string stage, string message)
        {
            if (IsFinished) return;
            FailureStage = stage;
            FailureMessage = message;
            try
            {
                _writer?.Dispose();
                _writer = null;
                HasEvidence = string.IsNullOrEmpty(stage) && _rowCount == (_lastFrame + 1) * 2 &&
                    File.Exists(CsvPath) && new FileInfo(CsvPath).Length > 100 &&
                    (!_captureViews || (_capturePaths.Count == 2 &&
                        _capturePaths.All(path => File.Exists(path) && new FileInfo(path).Length > 100)));
                int[] reviewFrames = { 166, 544, 790, 1324, 1332, 4866, 4965, 5547,
                    _maximumSoleStepFrame, _maximumFootRotationStepFrame, _maximumHipsStepFrame };
                Camera camera = Camera.main;
                File.WriteAllText(StatePath, JsonConvert.SerializeObject(new
                {
                    status = HasEvidence ? "metrics_complete_review_required" : "failed",
                    failure_stage = stage, failure_message = message,
                    scene = ScenePath, input = _inputFileName,
                    model = _pipeline?.targetCharacter != null ? _pipeline.targetCharacter.name : string.Empty,
                    native_skinning_processed_frames =
                        _pipeline?.ImportedMotionCorrectionProcessedFrameCount ?? 0,
                    native_skinning_total_frames =
                        _pipeline?.ImportedMotionCorrectionTotalFrameCount ?? 0,
                    clip_frame_rate = _frameRate, last_frame = _lastFrame,
                    frame_limit = _frameLimit, capture_paths = _capturePaths,
                    camera = camera == null ? null : new
                    {
                        position = new[] { camera.transform.position.x, camera.transform.position.y,
                            camera.transform.position.z },
                        rotation = new[] { camera.transform.rotation.x, camera.transform.rotation.y,
                            camera.transform.rotation.z, camera.transform.rotation.w },
                        camera.orthographic, camera.orthographicSize, camera.fieldOfView
                    },
                    source_human_scale = _sourceHumanScale,
                    stage_basis = "source=FBX Humanoid world; retarget=initial target pose before contact correction; sole and foot=final evaluated pose; Game View mesh not presented per frame",
                    processed_frames = _frame, row_count = _rowCount,
                    csv_path = CsvPath,
                    maximum_time_error_ms = _maximumTimeErrorMilliseconds,
                    maximum_penetration_mm = _maximumPenetrationMillimeters,
                    maximum_sole_step_mm = _maximumSoleStepMillimeters,
                    maximum_foot_rotation_step_degrees = _maximumFootRotationStepDegrees,
                    maximum_hips_step_mm = _maximumHipsStepMillimeters,
                    review_frames = reviewFrames.Where(frame => frame >= 0 && frame <= _lastFrame)
                        .Distinct().OrderBy(frame => frame).ToArray(),
                    elapsed_ms = (DateTime.UtcNow - _startedUtc).TotalMilliseconds
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
                if (_pipeline != null && (_pipeline.IsImportedMotionPlaying ||
                    _pipeline.SessionState == FBXVmdPipeline.FBXSessionState.PreviewPaused))
                    _pipeline.TryStopImportedMotion();
                _phase = Phase.Finished;
            }
        }

        public void Dispose()
        {
            if (!IsFinished) Finish("cancelled", $"{_caseId} 수집이 종료되었습니다.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
#endif
