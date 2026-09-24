#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 실제 시간 영상과 정수 프레임 접지 수치를 한 요청에 묶어 기록함.
    /// </summary>
    internal sealed class FbxFootLiveEvidenceCapture : IDisposable
    {
        private const string InputFileName = "tetoris_001.fbx";
        private const string ScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const int VideoWidth = 1280;
        private const int VideoHeight = 720;
        private static readonly int[] Starts = { 1525, 3769 };
        private static readonly int[] Ends = { 1565, 3822 };
        private static readonly BindingFlags MemberFlags = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic;

        private enum Phase { Importing, Preparing, Capturing, Finalizing, Finished }

        private sealed class FrameEntry
        {
            public int segment_start;
            public int observed_index;
            public int frame;
            public float time_seconds;
            public long wall_ms;
            public int rendered_frame_count;
        }

        private sealed class FootEntry
        {
            public int segment_start;
            public int observed_index;
            public int frame;
            public float time_seconds;
            public string side;
            public bool has_ground;
            public string support_role;
            public float[] weight;
            public float[] signed_ground_distance_mm;
            public float[][] anchor;
            public float[] source_foot;
            public float[] source_toes;
            public float[] target_foot;
            public int[][] planned_point_ids;
            public float[] planned_blend;
            public string[] fixed_renderer;
            public int[] fixed_vertex;
            public float[][] fixed_source_sole;
            public float[][] fixed_final_sole;
        }

        private readonly FBXVmdPipeline _pipeline;
        private readonly string _directory;
        private readonly string[] _videoPaths = new string[2];
        private readonly HashSet<int>[] _seen = { new HashSet<int>(), new HashSet<int>() };
        private readonly List<FrameEntry> _frames = new List<FrameEntry>();
        private readonly List<FootEntry> _liveRows = new List<FootEntry>();
        private readonly List<FootEntry> _seekRows = new List<FootEntry>();
        private readonly DateTime _startedUtc = DateTime.UtcNow;
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private readonly SkinnedMeshRenderer[,] _fixedRenderers = new SkinnedMeshRenderer[2, 2];
        private readonly int[,] _fixedVertices = new int[2, 2];
        private Phase _phase = Phase.Importing;
        private int _segment;
        private bool _segmentEndObserved;
        private DateTime _finalizingStartedUtc;
        private Camera _camera;
        private object _driver;
        private object[] _legs;
        private Transform[] _feet;
        private Dictionary<SkinnedMeshRenderer, object> _previewBySource;
        private RecorderController _recorder;
        private RecorderControllerSettings _recorderSettings;
        private MovieRecorderSettings _movieSettings;
        private Exception _renderError;

        private FbxFootLiveEvidenceCapture(FBXVmdPipeline pipeline, string directory)
        {
            _pipeline = pipeline;
            _directory = directory;
            StatePath = Path.Combine(directory, "state.json");
        }

        internal string StatePath { get; }
        internal bool IsFinished => _phase == Phase.Finished;
        internal bool HasEvidence { get; private set; }
        internal string FailureStage { get; private set; } = string.Empty;
        internal string FailureMessage { get; private set; } = string.Empty;

        internal static bool TryStart(FBXVmdPipeline pipeline, string requestId, string runId,
            out FbxFootLiveEvidenceCapture capture, out string message)
        {
            capture = null;
            message = string.Empty;
            if (pipeline == null || !Guid.TryParse(requestId, out Guid requestGuid) ||
                !Guid.TryParse(runId, out Guid runGuid) ||
                !EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath ||
                pipeline.IsProcessing || pipeline.HasPreparedImportedMotion ||
                pipeline.IsImportedMotionRecording || Time.captureFramerate != 0 ||
                Camera.main == null || pipeline.targetCharacter == null)
            {
                message = "F09는 저장된 Main_Auto의 비녹화 Play 상태와 유효한 실행 ID가 필요합니다.";
                return false;
            }

            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string input = Path.Combine(root, "Assets", "Resources", "Import_FBX", InputFileName);
            if (!File.Exists(input))
            {
                message = $"F09 입력 FBX가 없습니다: {InputFileName}";
                return false;
            }

            string directory = Path.Combine(root, "Docs", "Workflow", "Local", "evidence",
                "boogle", "foot-live", runGuid.ToString("D"), requestGuid.ToString("D"));
            try
            {
                Directory.CreateDirectory(directory);
                capture = new FbxFootLiveEvidenceCapture(pipeline, directory);
                if (pipeline.TryStartFbxImportFromSharedSettings(input)) return true;
                message = "F09 제품 FBX 가져오기 요청이 거부되었습니다.";
            }
            catch (Exception error)
            {
                message = $"F09 시작 실패: {error.Message}";
            }

            capture = null;
            return false;
        }

        internal void Poll()
        {
            if (IsFinished) return;
            try
            {
                if (_renderError != null) throw _renderError;
                if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath ||
                    _pipeline == null || Time.captureFramerate != 0)
                    throw new InvalidOperationException("Play·씬·프레임률 상태가 변경되었습니다.");
                if (DateTime.UtcNow - _startedUtc > TimeSpan.FromMinutes(20))
                    throw new TimeoutException("F09 가져오기·접지 준비·녹화 시간이 초과되었습니다.");
                if (_pipeline.SessionState == FBXVmdPipeline.FBXSessionState.Failed)
                    throw new InvalidOperationException(_pipeline.LastSessionMessage);

                switch (_phase)
                {
                    case Phase.Importing:
                        if (_pipeline.IsProcessing || !_pipeline.HasPreparedImportedMotion ||
                            _pipeline.SessionState != FBXVmdPipeline.FBXSessionState.Ready) return;
                        if (_pipeline.ImportedMotionLastFrameIndex < Ends[Ends.Length - 1] ||
                            !_pipeline.TryPlayImportedMotion())
                            throw new InvalidOperationException("F09 모션 길이 또는 재생 시작이 유효하지 않습니다.");
                        _phase = Phase.Preparing;
                        break;
                    case Phase.Preparing:
                        if (_pipeline.IsPreparingImportedMotionCorrection ||
                            !_pipeline.IsImportedMotionPlaying) return;
                        if (!_pipeline.TryPauseImportedMotion())
                            throw new InvalidOperationException("F09 준비 후 일시정지 실패");
                        InitializeSampling();
                        StartSegment(0);
                        break;
                    case Phase.Capturing:
                        if (!_segmentEndObserved &&
                            _pipeline.ImportedMotionCurrentFrameIndex > Ends[_segment] + 10)
                            throw new InvalidOperationException("F09 Game View 렌더 프레임을 관측하지 못했습니다.");
                        if (!_segmentEndObserved) return;
                        StopVideo();
                        _pipeline.TryPauseImportedMotion();
                        if (_segment + 1 < Starts.Length) StartSegment(_segment + 1);
                        else
                        {
                            CaptureSeekRows();
                            _finalizingStartedUtc = DateTime.UtcNow;
                            _phase = Phase.Finalizing;
                        }
                        break;
                    case Phase.Finalizing:
                        if (_videoPaths.All(path => File.Exists(path) && new FileInfo(path).Length > 1024))
                        {
                            Finish(string.Empty, string.Empty);
                            return;
                        }
                        if (DateTime.UtcNow - _finalizingStartedUtc > TimeSpan.FromSeconds(30))
                            throw new IOException("F09 영상 파일을 완성하지 못했습니다.");
                        break;
                }
            }
            catch (TimeoutException error) { Finish("timeout", error.Message); }
            catch (Exception error) { Finish("capture", error.ToString()); }
        }

        private void InitializeSampling()
        {
            _camera = Camera.main;
            _driver = GetField(_pipeline, "_nativeSkinningCorrectionPlaybackDriver");
            if (_camera == null || !(bool)GetProperty(_driver, "IsReady"))
                throw new InvalidOperationException("F09 카메라 또는 최종 메시가 준비되지 않았습니다.");
            object controller = GetField(_pipeline, "_humanoidMotionPlaybackController");
            object grounding = GetField(controller, "_footGrounding");
            _legs = new[] { GetField(grounding, "_left"), GetField(grounding, "_right") };
            Animator animator = _pipeline.targetCharacter.GetComponent<Animator>();
            _feet = new[] { animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                animator.GetBoneTransform(HumanBodyBones.RightFoot) };
            if (_feet.Any(foot => foot == null))
                throw new InvalidOperationException("F09 Humanoid Foot 본이 없습니다.");
            _previewBySource = ((IEnumerable)GetField(_driver, "_presenters"))
                .Cast<object>().ToDictionary(item =>
                    (SkinnedMeshRenderer)GetField(item, "_sourceRenderer"));
        }

        private void StartSegment(int index)
        {
            _segment = index;
            _segmentEndObserved = false;
            for (int side = 0; side < 2; side++)
                for (int channel = 0; channel < 2; channel++)
                    _fixedRenderers[side, channel] = null;
            if (!_pipeline.TrySeekImportedMotionFrame(Starts[index] - 1) ||
                !(bool)Invoke(_driver, "PresentCurrentFrame"))
                throw new InvalidOperationException("F09 구간 첫 프레임 탐색 실패");
            StartVideo(index);
            Camera.onPostRender += CaptureRenderedFrame;
            _phase = Phase.Capturing;
            if (!_pipeline.TryPlayImportedMotion())
                throw new InvalidOperationException("F09 실제 시간 재생 시작 실패");
        }

        private void StartVideo(int index)
        {
            string stamp = DateTime.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture);
            string name = $"when-{stamp}_where-MainAuto_who-YYB_what-{Starts[index]}-{Ends[index]}_why-F09_how-GameView";
            string output = Path.Combine(_directory, name);
            if (output.Length + 4 >= 260)
                throw new PathTooLongException("F09 영상 출력 경로가 Windows Recorder 한계를 넘습니다.");
            _videoPaths[index] = output + ".mp4";
            _recorderSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            _recorderSettings.SetRecordModeToManual();
            _recorderSettings.FrameRatePlayback = FrameRatePlayback.Variable;
            _recorderSettings.FrameRate = _pipeline.ImportedMotionFrameRate;
            _recorderSettings.CapFrameRate = false;
            _movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            _movieSettings.name = "F09 실제 시간 Game View";
            _movieSettings.Enabled = true;
            _movieSettings.EncoderSettings = new CoreEncoderSettings
            {
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                Codec = CoreEncoderSettings.OutputCodec.MP4
            };
            _movieSettings.CaptureAudio = false;
            _movieSettings.ImageInputSettings = new CameraInputSettings
            {
                Source = ImageSource.MainCamera,
                OutputWidth = VideoWidth,
                OutputHeight = VideoHeight
            };
            _movieSettings.OutputFile = output;
            _recorderSettings.AddRecorderSettings(_movieSettings);
            _recorder = new RecorderController(_recorderSettings);
            _recorder.PrepareRecording();
            if (!_recorder.StartRecording())
                throw new InvalidOperationException("F09 Game View 영상 녹화를 시작하지 못했습니다.");
        }

        private void StopVideo()
        {
            Camera.onPostRender -= CaptureRenderedFrame;
            _recorder?.StopRecording();
            _recorder = null;
            if (_movieSettings != null) UnityEngine.Object.DestroyImmediate(_movieSettings);
            if (_recorderSettings != null) UnityEngine.Object.DestroyImmediate(_recorderSettings);
            _movieSettings = null;
            _recorderSettings = null;
        }

        private void CaptureRenderedFrame(Camera rendered)
        {
            if (_phase != Phase.Capturing || rendered != _camera) return;
            try
            {
                int frame = _pipeline.ImportedMotionCurrentFrameIndex;
                if (frame >= Starts[_segment] && frame <= Ends[_segment] && _seen[_segment].Add(frame))
                {
                    float time = _pipeline.ImportedMotionCurrentTimeSeconds;
                    int observedIndex = _frames.Count;
                    _frames.Add(new FrameEntry
                    {
                        segment_start = Starts[_segment], observed_index = observedIndex,
                        frame = frame, time_seconds = time,
                        wall_ms = _clock.ElapsedMilliseconds,
                        rendered_frame_count = Time.renderedFrameCount
                    });
                    CaptureFootRows(Starts[_segment], observedIndex, frame, time, _liveRows);
                }
                if (frame >= Ends[_segment]) _segmentEndObserved = true;
            }
            catch (Exception error) { _renderError = error; }
        }

        private void CaptureSeekRows()
        {
            for (int segment = 0; segment < Starts.Length; segment++)
            {
                for (int side = 0; side < 2; side++)
                    for (int channel = 0; channel < 2; channel++)
                        _fixedRenderers[side, channel] = null;
                for (int frame = Starts[segment]; frame <= Ends[segment]; frame++)
                {
                    if (!_pipeline.TrySeekImportedMotionFrame(frame) ||
                        !(bool)Invoke(_driver, "PresentCurrentFrame") ||
                        _pipeline.ImportedMotionCurrentFrameIndex != frame)
                        throw new InvalidOperationException($"F09 {frame}프레임 탐색 불일치");
                    CaptureFootRows(Starts[segment], -1, frame,
                        _pipeline.ImportedMotionCurrentTimeSeconds, _seekRows);
                }
            }
        }

        private void CaptureFootRows(int segmentStart, int observedIndex, int frame,
            float time, List<FootEntry> rows)
        {
            if (!_pipeline.TryCaptureImportedMotionFootSurface(out HumanoidFootGroundingSnapshot left,
                    out HumanoidFootGroundingSnapshot right, out _))
                throw new InvalidOperationException($"F09 {frame}프레임 접지 표면 읽기 실패");
            var surfaces = new[] { left, right };
            var previewVertices = new Dictionary<SkinnedMeshRenderer, Vector3[]>();
            for (int side = 0; side < 2; side++)
            {
                object leg = _legs[side];
                object sampler = GetField(leg, "Sampler");
                if (!(bool)Invoke(sampler, "TrySample"))
                    throw new InvalidOperationException($"F09 {frame}프레임 밑창 읽기 실패");
                object[] points = ((IEnumerable)GetField(sampler, "_points")).Cast<object>().ToArray();
                Vector3[] world = (Vector3[])GetField(sampler, "_worldPoints");
                Vector3[][] source = (Vector3[][])GetField(leg, "_sourcePoints");
                if (_fixedRenderers[side, 0] == null)
                    SelectFixedVertices(side, points, world);
                float sourceFrame = time * _pipeline.ImportedMotionFrameRate;
                int first = Mathf.Clamp(Mathf.FloorToInt(sourceFrame), 0, source[0].Length - 1);
                int second = Mathf.Min(first + 1, source[0].Length - 1);
                float blend = Mathf.Clamp01(sourceFrame - first);
                Array contacts = (Array)GetField(leg, "_activeContacts");
                var plannedIds = new int[2][];
                var plannedBlend = new float[2];
                var shown = new float[2][];
                var sourceSole = new float[2][];
                for (int channel = 0; channel < 2; channel++)
                {
                    object contact = contacts.GetValue(channel);
                    plannedIds[channel] = new[] { (int)GetField(contact, "_firstPoint"),
                        (int)GetField(contact, "_secondPoint") };
                    plannedBlend[channel] = (float)GetField(contact, "_blend");
                    SkinnedMeshRenderer renderer = _fixedRenderers[side, channel];
                    int vertex = _fixedVertices[side, channel];
                    int pointIndex = Array.FindIndex(points, point =>
                        (SkinnedMeshRenderer)GetField(GetField(point, "Surface"), "Renderer") == renderer &&
                        (int)GetField(point, "Vertex") == vertex);
                    if (pointIndex < 0) throw new InvalidOperationException("F09 고정 밑창 신원 유실");
                    sourceSole[channel] = ToArray(world[pointIndex]);
                    if (_previewBySource.TryGetValue(renderer, out object presenter))
                    {
                        if (!(bool)GetProperty(presenter, "IsPresenting"))
                            throw new InvalidOperationException("F09 최종 표시 메시 비활성");
                        if (!previewVertices.TryGetValue(renderer, out Vector3[] vertices))
                        {
                            vertices = ((Mesh)GetProperty(presenter, "PreviewMesh")).vertices;
                            previewVertices.Add(renderer, vertices);
                        }
                        GameObject preview = (GameObject)GetField(presenter, "_previewObject");
                        shown[channel] = ToArray(preview.transform.TransformPoint(vertices[vertex]));
                    }
                    else if (renderer.enabled) shown[channel] = sourceSole[channel];
                    else throw new InvalidOperationException("F09 표시 정점 없음");
                }

                HumanoidFootGroundingSnapshot surface = surfaces[side];
                rows.Add(new FootEntry
                {
                    segment_start = segmentStart, observed_index = observedIndex,
                    frame = frame, time_seconds = time, side = side == 0 ? "left" : "right",
                    has_ground = surface.has_ground, support_role = surface.support_role,
                    weight = new[] { surface.rear_weight, surface.front_weight },
                    signed_ground_distance_mm = new[] { surface.rear_distance_mm,
                        surface.front_distance_mm, surface.minimum_distance_mm },
                    anchor = new[] { ToArray(surface.rear_anchor), ToArray(surface.front_anchor) },
                    source_foot = ToArray(Vector3.LerpUnclamped(source[0][first], source[0][second], blend)),
                    source_toes = ToArray(Vector3.LerpUnclamped(source[1][first], source[1][second], blend)),
                    target_foot = ToArray(_feet[side].position),
                    planned_point_ids = plannedIds, planned_blend = plannedBlend,
                    fixed_renderer = new[] { _fixedRenderers[side, 0].name,
                        _fixedRenderers[side, 1].name },
                    fixed_vertex = new[] { _fixedVertices[side, 0], _fixedVertices[side, 1] },
                    fixed_source_sole = sourceSole, fixed_final_sole = shown
                });
            }
        }

        private void SelectFixedVertices(int side, object[] points, Vector3[] world)
        {
            for (int channel = 0; channel < 2; channel++)
            {
                int selected = -1;
                float minimum = float.PositiveInfinity;
                for (int index = 0; index < points.Length; index++)
                    if ((bool)GetField(points[index], channel == 0 ? "IsRear" : "IsFront") &&
                        world[index].y < minimum)
                    { selected = index; minimum = world[index].y; }
                if (selected < 0) throw new InvalidOperationException("F09 고정 밑창 정점 선택 실패");
                _fixedRenderers[side, channel] = (SkinnedMeshRenderer)GetField(
                    GetField(points[selected], "Surface"), "Renderer");
                _fixedVertices[side, channel] = (int)GetField(points[selected], "Vertex");
            }
        }

        private void Finish(string stage, string message)
        {
            if (IsFinished) return;
            FailureStage = stage;
            FailureMessage = message;
            try
            {
                float clipFrameRate = _pipeline?.ImportedMotionFrameRate ?? 0f;
                float sourceScaleRatio = GetSourceScaleRatio();
                StopVideo();
                if (_pipeline != null && (_pipeline.IsImportedMotionPlaying ||
                    _pipeline.SessionState == FBXVmdPipeline.FBXSessionState.PreviewPaused))
                    _pipeline.TryStopImportedMotion();
                int[][] missing = Starts.Select((start, index) =>
                    Enumerable.Range(start, Ends[index] - start + 1)
                        .Where(frame => !_seen[index].Contains(frame)).ToArray()).ToArray();
                HasEvidence = string.IsNullOrEmpty(stage) && _videoPaths.All(path =>
                    File.Exists(path) && new FileInfo(path).Length > 1024) &&
                    _seen.All(group => group.Count > 0) && _seekRows.Count == 190;
                string state = HasEvidence
                    ? (missing.Any(group => group.Length > 0) ? "partial" : "completed")
                    : "failed";
                File.WriteAllText(StatePath, JsonConvert.SerializeObject(new
                {
                    status = state, failure_stage = stage, failure_message = message,
                    scene = ScenePath, input = InputFileName,
                    model = _pipeline?.targetCharacter != null ? _pipeline.targetCharacter.name : string.Empty,
                    clip_frame_rate = clipFrameRate,
                    source_to_target_scale = sourceScaleRatio,
                    starts = Starts, ends = Ends, missing_frames = missing,
                    video_paths = _videoPaths, live_frame_map = _frames,
                    video_mapping_verified = false,
                    live_rows = _liveRows, seek_rows = _seekRows,
                    elapsed_ms = _clock.ElapsedMilliseconds,
                    capture_framerate = Time.captureFramerate
                }, Formatting.Indented));
            }
            catch (Exception error)
            {
                FailureStage = "evidence";
                FailureMessage = error.ToString();
                HasEvidence = false;
            }
            finally { _phase = Phase.Finished; }
        }

        private float GetSourceScaleRatio()
        {
            try
            {
                object controller = GetField(_pipeline, "_humanoidMotionPlaybackController");
                object grounding = GetField(controller, "_footGrounding");
                return (float)GetField(grounding, "_sourceScaleRatio");
            }
            catch { return 0f; }
        }

        public void Dispose()
        {
            if (!IsFinished) Finish("cancelled", "F09 캡처가 종료되었습니다.");
            else StopVideo();
        }

        private static object GetField(object owner, string name) =>
            owner?.GetType().GetField(name, MemberFlags)?.GetValue(owner) ??
            throw new MissingFieldException($"F09 계측 필드 없음: {name}");

        private static object GetProperty(object owner, string name) =>
            owner?.GetType().GetProperty(name, MemberFlags)?.GetValue(owner) ??
            throw new MissingMemberException($"F09 계측 속성 없음: {name}");

        private static object Invoke(object owner, string name) =>
            owner?.GetType().GetMethod(name, MemberFlags)?.Invoke(owner, null) ??
            throw new MissingMethodException($"F09 계측 메서드 없음: {name}");

        private static float[] ToArray(Vector3 value) => new[] { value.x, value.y, value.z };
    }
}
#endif
