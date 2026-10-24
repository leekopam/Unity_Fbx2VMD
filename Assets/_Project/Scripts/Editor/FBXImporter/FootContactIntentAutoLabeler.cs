// 접촉 의도 자동 라벨 제안(Jev): 불확실·의도 스팬을 발 클로즈업 이미지로 렌더해
// TypeSafe System One(Jev)에 Choice 질의를 던지고 suggested-labels.csv로 남긴다.
// 제안은 정답이 아니며 FootContactIntentLabelingWindow에서 사람이 채택·수정한다.
// 주의: Jev 이미지 기능은 공식 문서상 dreamer-ready(실험적)다. API 키는
// 창의 EditorPrefs(이 PC 전용) 또는 TYPESAFE_API_KEY 환경변수로만 읽고
// 코드·에셋에 저장하지 않는다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fbx2Vmd.EditorTools.FBXImporter
{
    public class FootContactIntentAutoLabeler : EditorWindow
    {
        private const string IntentFileName = "contact-intent.json";
        internal const string SuggestedFileName = "suggested-labels.csv";
        internal const string SuggestedHeader =
            "from_frame,to_frame,side,contact_label,motion_label,reviewer,notes," +
            "contact_confidence,motion_confidence";
        private const string ApiUrl = "https://api.typesafe.ai/v1/systemone";
        private const string Model = "jev-latest";
        private const string ApiKeyEnv = "TYPESAFE_API_KEY";
        // 창에서 입력한 키는 EditorPrefs에만 두어 이 PC에 머물고
        // 프로젝트 파일·씬·에셋에 직렬화되지 않는다.
        private const string ApiKeyPref = "Fbx2Vmd.TypeSafeApiKey";
        // 이 값 미만의 신뢰도는 어떤 라벨이든 '불확실' 제안으로 내린다.
        internal const float MinSuggestConfidence = 0.55f;
        private const int FramesPerSpan = 6;
        private const int RenderWidth = 512;
        private const int RenderHeight = 384;
        // 이 길이 이상 스팬은 발목 높이 전이점으로 균질 하위 구간으로 나눈다.
        private const int MinSplitLenFrames = 24;
        // 2차 패스 표식: 잡 종류이자 병합 후 notes에 남는 마커.
        internal const string RequeryKind = "requery_v2";
        private const int MaxSubSpans = 6;

        private static readonly Dictionary<string, string> ContactMap =
            new Dictionary<string, string>
            {
                ["airborne"] = "공중",
                ["toes"] = "앞꿈치",
                ["heel"] = "뒤꿈치",
                ["whole"] = "발 전체",
                ["uncertain"] = "불확실",
            };
        private static readonly Dictionary<string, string> MotionMap =
            new Dictionary<string, string>
            {
                ["pinned"] = "고정",
                ["roll"] = "구르기",
                ["slide"] = "의도된 이동",
                ["uncertain"] = "불확실",
            };

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60),
        };

        private List<string> _intentFiles = new List<string>();
        private int _selectedFile;
        private int _maxSpans = 30;
        private string _apiKey = "";
        private string _status = "";
        // 라벨링 창 미리보기 등 동시 리그와 겹치지 않게 오프셋을 바꿀 수 있다.
        private Vector3 _rigOffset = new Vector3(1000f, 0f, 0f);

        private bool _running;
        private Task<string> _apiCheckTask;
        private string _apiCheckStatus = "";
        private IntentFileDto _intent;
        private string _intentPath;
        private float _humanScale = 1f;
        private int _clipFrames;
        private readonly List<SpanJob> _jobs = new List<SpanJob>();
        private int _jobIndex;
        private readonly HashSet<string> _suggestedKeys = new HashSet<string>();
        // 전이 분리 등으로 부모 스팬 안쪽에 생긴 제안 행도 잡기 위해
        // 키 집합과 별도로 행 목록을 유지한다.
        private List<SuggestedRow> _suggestedRows = new List<SuggestedRow>();
        private int[] _currentFrames;
        private Task<string> _httpTask;
        private SpanJob _current;
        private StreamWriter _writer;
        private int _done;
        private int _total;
        private int _errors;
        // 유료 응답을 복구 파일로 보존하고 정지/완료 시 원자 병합함.
        // 키 = 원본 행의 RowKey, 값 = 그 행을 대체할 CSV 줄들.
        private bool _requeryMode;
        private readonly Dictionary<string, List<string>> _requeryLines =
            new Dictionary<string, List<string>>();
        private int _requeryMerged;
        private string _csvPath;
        private string _requeryOriginal;

        // 렌더 리그: 대상 모델 + 원본 클립 직접 재생 컨트롤러 + 발 클로즈업 카메라.
        private GameObject _model;
        private IDisposable _controller;
        private Animator _animator;
        private Func<float, bool> _seek;
        private Transform _leftFoot, _leftToe, _rightFoot, _rightToe;
        private GameObject _ground;
        private GameObject _light;
        private Camera _camera;
        private RenderTexture _renderTexture;
        private Texture2D _readback;

        internal struct SpanJob
        {
            public int From;
            public int To;
            public bool IsLeft;
            public string Kind;
            // 2차 재질의에서만 참 — 발 전방 저앵글 뷰를 추가로 찍는다.
            public bool FrontView;
        }

        // 렌더된 한 프레임의 측정값. VLM은 이미지에서 거리를 못 재므로
        // 수치를 질문에 함께 넣어 "측정"이 아니라 "해석"을 시킨다.
        internal struct FrameMeasure
        {
            public int Frame;
            public float AnkleCm;
            public float ToeCm;
            public float SpeedCms;
            public string Vdir;
        }

        private struct SpanRender
        {
            public byte[][] Pngs;
            public byte[][] FrontPngs;
            public int[] Frames;
            public FrameMeasure[] Measures;
        }

        internal sealed class SuggestedRow
        {
            public int From;
            public int To;
            public bool IsLeft;
            // side 셀이 left/right 둘 다 아니면 false — 재질의 대상에서 제외한다.
            public bool SideKnown;
            public string Contact;
            public string Motion;
            public float ContactConf;
            public float MotionConf;
            public string Reviewer;
            public string Notes;
        }

        [Serializable]
        private sealed class IntentFileDto
        {
            public string input;
            public float frame_rate;
            public float source_human_scale;
            public IntentRowDto[] intents;
            public SpanRowDto[] uncertain_spans;
        }

        [Serializable]
        internal sealed class IntentRowDto
        {
            public string side;
            public int start_frame;
            public int end_frame_exclusive;
            public string mode;
            public string certainty;
        }

        [Serializable]
        private sealed class SpanRowDto
        {
            public string side;
            public int start_frame;
            public int end_frame_exclusive;
        }

        [MenuItem("Tools/FBXImporter/접촉 의도 자동 라벨 제안(Jev)")]
        private static void Open()
        {
            var window = GetWindow<FootContactIntentAutoLabeler>(
                false, "접촉 의도 자동 라벨 제안(Jev)");
            window.minSize = new Vector2(520, 180);
            window.RefreshFileList();
            window.Show();
        }

        // 도메인 리로드로 목록이 비는 것을 막기 위해 활성화 시 재조회.
        private void OnEnable()
        {
            RefreshFileList();
            _apiKey = EditorPrefs.GetString(ApiKeyPref);
        }

        private void OnDisable() => StopRun(true);

        private static string EvidenceRoot => Path.Combine(
            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
            "Docs", "Workflow", "Local", "evidence", "boogle");

        private void RefreshFileList()
        {
            _intentFiles = new List<string>();
            if (Directory.Exists(EvidenceRoot))
            {
                try
                {
                    // 실제 파일은 contact-intent/session-*/ 등 임의 깊이에 있음.
                    _intentFiles.AddRange(Directory.GetFiles(EvidenceRoot,
                        IntentFileName, SearchOption.AllDirectories));
                }
                catch (Exception error) when (error is IOException ||
                    error is UnauthorizedAccessException)
                {
                    // 열거 불가 시 목록이 비는 것으로 처리.
                }
            }
            _intentFiles.Sort(StringComparer.Ordinal);
            _selectedFile = Mathf.Clamp(_selectedFile, 0,
                Mathf.Max(0, _intentFiles.Count - 1));
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "불확실 구간을 발 클로즈업으로 렌더해 Jev에 라벨 제안을 요청합니다.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                $"결과는 {SuggestedFileName}(같은 폴더)에 쌓이고, 라벨 창에서 사람이 채택합니다.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            _selectedFile = EditorGUILayout.Popup("추정 결과", _selectedFile,
                _intentFiles.ConvertAll(ShortName).ToArray());
            if (GUILayout.Button("새로고침", GUILayout.Width(80)))
            {
                RefreshFileList();
            }
            EditorGUILayout.EndHorizontal();
            _maxSpans = Mathf.Max(0, EditorGUILayout.IntField(
                "이번 실행 최대 스팬 수(0=전부)", _maxSpans));
            EditorGUILayout.BeginHorizontal();
            _apiKey = EditorGUILayout.PasswordField("TypeSafe API 키", _apiKey);
            if (GUILayout.Button("저장", GUILayout.Width(60)))
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    EditorPrefs.DeleteKey(ApiKeyPref);
                    _status = "저장된 API 키를 지웠습니다.";
                    _apiCheckStatus = "";
                    // 진행 중이던 검증 결과가 지운 키의 것이라 늦게 덮어쓰지 않게 취소.
                    _apiCheckTask = null;
                }
                else
                {
                    EditorPrefs.SetString(ApiKeyPref, _apiKey);
                    _status = "API 키를 이 PC에 저장했습니다.";
                    _apiCheckStatus = "연결 확인 중…";
                    _apiCheckTask = CheckApiAsync(_apiKey);
                }
            }
            using (new EditorGUI.DisabledScope(
                _apiCheckTask != null || (string.IsNullOrEmpty(_apiKey) &&
                    string.IsNullOrEmpty(ResolveApiKey()))))
            {
                if (GUILayout.Button("연결 확인", GUILayout.Width(80)))
                {
                    _apiCheckStatus = "연결 확인 중…";
                    // 입력 칸의 키를 우선 검사(저장 전 시험 포함), 비어 있으면 저장값/env.
                    _apiCheckTask = CheckApiAsync(
                        !string.IsNullOrEmpty(_apiKey) ? _apiKey : ResolveApiKey());
                }
            }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_apiCheckStatus))
            {
                EditorGUILayout.HelpBox(_apiCheckStatus,
                    _apiCheckStatus.Contains("실패") ? MessageType.Warning
                    : _apiCheckStatus.StartsWith("연결됨") ? MessageType.Info
                    : MessageType.None);
            }
            EditorGUILayout.LabelField(
                "키는 이 PC에만 저장되고 프로젝트 파일·코드에 기록되지 않습니다. " +
                $"({ApiKeyEnv} 환경변수도 대체 경로로 사용)",
                EditorStyles.miniLabel);
            if (string.IsNullOrEmpty(ResolveApiKey()))
            {
                EditorGUILayout.HelpBox(
                    "API 키가 없습니다. 위 칸에 키를 넣고 저장하세요.",
                    MessageType.Warning);
            }
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_running || _intentFiles.Count == 0))
            {
                if (GUILayout.Button("제안 생성 시작"))
                {
                    StartRun();
                }
                if (GUILayout.Button("로컬 스캔(결함·동작·보정시뮬)"))
                {
                    ScanDefects();
                }
                if (GUILayout.Button("2차 재질의(앞면 뷰)"))
                {
                    StartRequery();
                }
            }
            using (new EditorGUI.DisabledScope(!_running))
            {
                if (GUILayout.Button("중단"))
                {
                    StopRun(false);
                }
            }
            if (!_running && PendingCsvPath() != null &&
                GUILayout.Button("미저장 재질의 결과 저장 재시도"))
            {
                RetryRequerySave();
            }
            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

        private string ShortName(string path)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ??
                Application.dataPath;
            return path.Replace(root + Path.DirectorySeparatorChar, "")
                .Replace('\\', '/');
        }

        private void StartRun()
        {
            if (BlockPendingRequery()) return;
            _status = "";
            _done = 0;
            _errors = 0;
            // 키가 없으면 모든 스팬이 렌더만 하고 실패하므로 시작 전 차단.
            if (string.IsNullOrEmpty(ResolveApiKey()))
            {
                _status = "API 키가 없습니다. 창에 키를 넣고 저장하세요.";
                return;
            }
            if (!TryLoadIntent()) return;

            string folder = Path.GetDirectoryName(_intentPath);
            string csvPath = Path.Combine(folder, SuggestedFileName);
            _suggestedKeys.Clear();
            _suggestedRows = ReadSuggestions(csvPath);
            foreach (SuggestedRow row in _suggestedRows)
            {
                _suggestedKeys.Add(RowKey(row.From, row.To, row.IsLeft));
            }

            _jobs.Clear();
            _jobIndex = 0;
            EnqueueSpans();
            if (_jobs.Count == 0)
            {
                _status = "제안이 필요한 스팬이 없습니다(모두 제안됨 또는 목록 비어 있음).";
                return;
            }
            _total = Mathf.Min(_maxSpans > 0 ? _maxSpans : int.MaxValue, _jobs.Count);
            _jobs.RemoveRange(_total, _jobs.Count - _total);

            if (!SetupRig())
            {
                return;
            }
            try
            {
                bool needsHeader = !File.Exists(csvPath) ||
                    new FileInfo(csvPath).Length == 0;
                _writer = new StreamWriter(csvPath, true,
                    new UTF8Encoding(false));
                if (needsHeader) _writer.WriteLine(SuggestedHeader);
            }
            catch (Exception error)
            {
                _status = $"{SuggestedFileName} 기록 실패: {error.Message}";
                TeardownRig();
                return;
            }
            _running = true;
        }

        // 2차 재질의: 접촉/동작이 불확실로 남은 제안 행만 골라 앞면 뷰를
        // 추가해 다시 묻는다. 복구 파일에 응답을 보존한 뒤 정지 시 치환함.
        private void StartRequery()
        {
            if (BlockPendingRequery()) return;
            _status = "";
            _done = 0;
            _errors = 0;
            _requeryMerged = 0;
            if (string.IsNullOrEmpty(ResolveApiKey()))
            {
                _status = "API 키가 없습니다. 창에 키를 넣고 저장하세요.";
                return;
            }
            if (!TryLoadIntent()) return;

            string folder = Path.GetDirectoryName(_intentPath);
            _csvPath = Path.Combine(folder, SuggestedFileName);
            try
            {
                _requeryOriginal = File.ReadAllText(_csvPath);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                _status = $"제안 파일 읽기 실패: {error.Message}";
                return;
            }
            _suggestedRows = ReadSuggestions(_csvPath);
            var targets = SelectRequeryRows(_suggestedRows);
            if (targets.Count == 0)
            {
                _status = "2차 재질의 대상이 없습니다 " +
                    "(불확실 행 없음 또는 모두 재질의 완료).";
                return;
            }
            _jobs.Clear();
            _jobIndex = 0;
            _suggestedKeys.Clear();
            foreach (SuggestedRow row in targets)
            {
                _jobs.Add(new SpanJob
                {
                    From = row.From, To = row.To, IsLeft = row.IsLeft,
                    Kind = RequeryKind, FrontView = true,
                });
            }
            _total = Mathf.Min(_maxSpans > 0 ? _maxSpans : int.MaxValue,
                _jobs.Count);
            _jobs.RemoveRange(_total, _jobs.Count - _total);
            if (!SetupRig()) return;
            _requeryMode = true;
            _requeryLines.Clear();
            _running = true;
        }

        private bool TryLoadIntent()
        {
            // GUI에서만 호출되지만 호출부 가드에만 의존하지 않고 경계에서 막는다.
            if (_selectedFile < 0 || _selectedFile >= _intentFiles.Count)
            {
                _status = "선택된 의도 파일이 없습니다.";
                return false;
            }
            _intentPath = _intentFiles[_selectedFile];
            try
            {
                _intent = JsonUtility.FromJson<IntentFileDto>(
                    File.ReadAllText(_intentPath));
            }
            catch (Exception error)
            {
                _status = $"의도 파일을 읽지 못했습니다: {error.Message}";
                return false;
            }
            if (_intent == null)
            {
                _status = "의도 파일 파싱 실패.";
                return false;
            }
            _humanScale = _intent.source_human_scale > 0f
                ? _intent.source_human_scale : 1f;
            return true;
        }

        // 발 결함 스캔: 전체 프레임의 발목·발끝 위치를 추정 바닥(궤적
        // 10백분위) 기준으로 판정해 결함 이벤트·보정 수요 시뮬레이션·
        // 동작 재분류를 남긴다. API·렌더 없는 로컬 계산.
        private void ScanDefects()
        {
            if (BlockPendingRequery()) return;
            _status = "";
            if (!TryLoadIntent()) return;
            if (!SetupRig(false)) return;
            try
            {
                float fps = _intent.frame_rate > 0f ? _intent.frame_rate : 60f;
                var leftFoot = new Vector3[_clipFrames];
                var leftToe = new Vector3[_clipFrames];
                var rightFoot = new Vector3[_clipFrames];
                var rightToe = new Vector3[_clipFrames];
                var simRows = new List<FootDefectScan.SimRow>(_clipFrames);
                for (int frame = 0; frame < _clipFrames; frame++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "발 로컬 스캔",
                            $"프레임 {frame}/{_clipFrames} 측정 중…",
                            (float)frame / _clipFrames))
                    {
                        _status = "로컬 스캔을 중단했습니다.";
                        return;
                    }
                    if (!SeekFrameIndex(frame, fps))
                    {
                        _status = $"프레임 {frame} 시크 실패로 스캔 중단.";
                        return;
                    }
                    leftFoot[frame] = _leftFoot.position;
                    leftToe[frame] = _leftToe != null
                        ? _leftToe.position : _leftFoot.position;
                    rightFoot[frame] = _rightFoot.position;
                    rightToe[frame] = _rightToe != null
                        ? _rightToe.position : _rightFoot.position;
                    simRows.Add(FootDefectScan.BuildSimRow(frame,
                        leftFoot[frame], leftToe[frame],
                        rightFoot[frame], rightToe[frame],
                        IsInConfidentPlant(frame, true),
                        IsInConfidentPlant(frame, false)));
                }
                var events = new List<FootDefectScan.DefectEvent>();
                events.AddRange(ScanSide(true, leftFoot, leftToe));
                events.AddRange(ScanSide(false, rightFoot, rightToe));
                string folder = Path.GetDirectoryName(_intentPath);
                FootDefectScan.WriteEvents(Path.Combine(
                    folder, FootDefectScan.FileName), events);
                FootDefectScan.WriteSimRows(Path.Combine(
                    folder, FootDefectScan.SimFileName), simRows);
                int groundedCount = 0;
                foreach (FootDefectScan.SimRow r in simRows)
                {
                    if (r.GroundWhileNoPlant) groundedCount++;
                }
                int reclassified = ReclassifyMotion(folder, fps,
                    leftFoot, leftToe, rightFoot, rightToe);
                _status = $"스캔 완료: 결함 {events.Count}건, " +
                    $"보정수요(무료 지지) {groundedCount}프레임, " +
                    $"동작 재분류 {reclassified}건 → " +
                    $"{FootDefectScan.FileName}, {FootDefectScan.SimFileName}";
            }
            catch (Exception error)
            {
                _status = $"로컬 스캔 실패: {error.Message}";
                Debug.LogException(error);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                TeardownRig();
                Repaint();
            }
        }

        // 한쪽 발의 전체 프레임을 판정해 연속 프레임 이벤트로 병합한다.
        private List<FootDefectScan.DefectEvent> ScanSide(bool isLeft,
            Vector3[] footPos, Vector3[] toePos)
        {
            float floorFoot = FootDefectScan.EstimateFloor(HeightsOf(footPos));
            float floorToe = FootDefectScan.EstimateFloor(HeightsOf(toePos));
            float supportHeight =
                _humanScale * FootDefectScan.SupportHeightPerHumanScale;
            float airborneHeight =
                _humanScale * FootDefectScan.AirborneHeightPerHumanScale;
            float penDepth =
                _humanScale * FootDefectScan.PenetrationDepthPerHumanScale;
            var flags = new List<(int frame, string kind, float lowest)>(
                footPos.Length);
            for (int frame = 0; frame < footPos.Length; frame++)
            {
                float footH = footPos[frame].y - floorFoot;
                float toeH = toePos[frame].y - floorToe;
                string kind = FootDefectScan.Classify(footH, toeH,
                    IsInConfidentPlant(frame, isLeft),
                    supportHeight, airborneHeight, penDepth);
                flags.Add((frame, kind, Mathf.Min(footH, toeH)));
            }
            return FootDefectScan.MergeEvents(flags, isLeft);
        }

        private static float[] HeightsOf(Vector3[] positions)
        {
            var heights = new float[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                heights[i] = positions[i].y;
            }
            return heights;
        }

        // Jev가 동작을 불확실로 남긴 제안 행 중 접촉이 확정된 행만
        // 프레임 측정으로 동작을 다시 정한다. 속도·롤 비율은 추정기와
        // 같은 정의. 애매한 행은 불확실 그대로 둔다(정직한 기권).
        private int ReclassifyMotion(string folder, float fps,
            Vector3[] leftFoot, Vector3[] leftToe,
            Vector3[] rightFoot, Vector3[] rightToe)
        {
            string csvPath = Path.Combine(folder, SuggestedFileName);
            if (!File.Exists(csvPath)) return 0;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(csvPath);
            }
            catch (Exception error) when (error is IOException ||
                error is UnauthorizedAccessException)
            {
                // 잠금·권한 문제가 "재분류 0건"과 구분되도록 로그를 남긴다.
                Debug.LogWarning(
                    $"제안 파일을 읽지 못해 동작 재분류를 건너뜁니다: {error.Message}");
                return 0;
            }
            float supportSpeed =
                _humanScale * FootDefectScan.SupportSpeedPerHumanScale;
            int changed = 0;
            var output = new StringBuilder();
            foreach (string line in lines)
            {
                output.AppendLine(RewriteMotionLine(line, fps,
                    leftFoot, leftToe, rightFoot, rightToe,
                    supportSpeed, ref changed));
            }
            if (changed > 0)
            {
                FootDefectScan.WriteAllTextAtomic(csvPath, output.ToString());
            }
            return changed;
        }

        // 제안 CSV 한 줄의 동작 셀을 프레임 측정으로 재분류한다.
        // 접촉이 닿음 계열이고 동작이 "불확실"인 행만 건드린다.
        internal static string RewriteMotionLine(string line, float fps,
            Vector3[] leftFoot, Vector3[] leftToe,
            Vector3[] rightFoot, Vector3[] rightToe,
            float supportSpeed, ref int changed)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith("from_frame", StringComparison.Ordinal) ||
                line.Contains("\""))
            {
                return line;
            }
            string[] cells = line.Split(',');
            if (cells.Length < 9) return line;
            if (!int.TryParse(cells[0], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int from) ||
                !int.TryParse(cells[1], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int to))
            {
                return line;
            }
            bool touching = cells[3] == "발 전체" || cells[3] == "앞꿈치" ||
                cells[3] == "뒤꿈치";
            if (!touching || cells[4] != "불확실") return line;
            // 알 수 없는 side는 어느 발로도 재분류하지 않는다.
            if (cells[2] != "left" && cells[2] != "right") return line;
            bool isLeft = cells[2] == "left";
            Vector3[] feet = isLeft ? leftFoot : rightFoot;
            Vector3[] toes = isLeft ? leftToe : rightToe;
            int pinned = 0, roll = 0, total = 0;
            int last = Mathf.Min(to, feet.Length - 1);
            for (int f = Mathf.Max(1, from); f <= last; f++)
            {
                // 발목은 추정기와 같은 3D 속도(상하 요동도 움직임이므로),
                // 발끝은 슬라이드 판정과 같은 수평 속도를 본다.
                float footSpeed = Vector3.Distance(feet[f - 1], feet[f]) * fps;
                float toesSpeedH = new Vector2(
                    toes[f].x - toes[f - 1].x,
                    toes[f].z - toes[f - 1].z).magnitude * fps;
                if (FootDefectScan.IsPinnedFrame(
                        footSpeed, toesSpeedH, supportSpeed))
                {
                    pinned++;
                }
                if (FootDefectScan.IsRollFrame(
                        FootDefectScan.RollRatio(feet[f], toes[f]),
                        toesSpeedH, supportSpeed))
                {
                    roll++;
                }
                total++;
            }
            if (total <= 0) return line;
            string motion = FootDefectScan.ClassifyMotion(
                (float)pinned / total, (float)roll / total);
            if (motion == null) return line;
            cells[4] = motion;
            // 출처를 메모에 남겨 사람이 "측정값으로 채워진 제안"과
            // jev 제안을 구분할 수 있게 한다.
            cells[6] = string.IsNullOrEmpty(cells[6])
                ? "motion=local"
                : cells[6] + "|motion=local";
            changed++;
            return string.Join(",", cells);
        }

        private bool IsInConfidentPlant(int frame, bool isLeft)
        {
            if (_intent?.intents == null) return false;
            string side = isLeft ? "left" : "right";
            foreach (IntentRowDto intent in _intent.intents)
            {
                if (intent.side == side && intent.certainty == "confident" &&
                    intent.mode == "plant" &&
                    intent.start_frame <= frame &&
                    frame < intent.end_frame_exclusive)
                {
                    return true;
                }
            }
            return false;
        }

        // 의도 행과 불확실 스팬을 모두 큐에 넣되 이미 제안된 (from,to,side)는 건너뜀.
        private void EnqueueSpans()
        {
            if (_intent.uncertain_spans != null)
            {
                foreach (SpanRowDto span in _intent.uncertain_spans)
                {
                    TryEnqueue(span.start_frame, span.end_frame_exclusive - 1,
                        span.side == "left", "uncertain_span");
                }
            }
            if (_intent.intents != null)
            {
                foreach (IntentRowDto intent in _intent.intents)
                {
                    // 확실 판정된 의도는 추정기 결과가 이미 있어 제안 비용 가치가 낮다.
                    if (intent.certainty == "confident") continue;
                    TryEnqueue(intent.start_frame, intent.end_frame_exclusive - 1,
                        intent.side == "left", $"intent:{intent.mode}");
                }
            }
        }

        private void TryEnqueue(int from, int to, bool isLeft, string kind)
        {
            // 기존 제안 행과 같은 런 내 중복(intents↔uncertain_spans 교집합) 모두 제외.
            if (to < from || !_suggestedKeys.Add(RowKey(from, to, isLeft)))
            {
                return;
            }
            _jobs.Add(new SpanJob
            {
                From = from, To = to, IsLeft = isLeft, Kind = kind,
            });
        }

        internal static string RowKey(int from, int to, bool isLeft) =>
            $"{from}:{to}:{(isLeft ? "left" : "right")}";

        // 2차 재질의 대상: 접촉 또는 동작이 불확실이고, 아직 2차 패스를
        // 거치지 않았으며, side가 left/right로 확실한 행. 불명 side는
        // 잘못된 발을 촬영해 원본 행을 오염시키므로 제외한다.
        internal static List<SuggestedRow> SelectRequeryRows(
            List<SuggestedRow> rows)
        {
            var targets = new List<SuggestedRow>();
            if (rows == null) return targets;
            var duplicateKeys = rows.GroupBy(r => RowKey(r.From, r.To, r.IsLeft))
                .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            foreach (SuggestedRow row in rows)
            {
                bool uncertain = row.Contact == "불확실" ||
                    (row.Contact != "공중" && row.Motion == "불확실");
                bool already = row.Notes != null &&
                    row.Notes.Contains(RequeryKind);
                if (uncertain && !already && row.SideKnown &&
                    row.From >= 0 && row.To >= row.From &&
                    !duplicateKeys.Contains(RowKey(row.From, row.To, row.IsLeft)))
                {
                    targets.Add(row);
                }
            }
            return targets;
        }

        // notes 뒤에 "|마커"를 붙인다. 빈 notes면 마커만 둔다.
        internal static string AppendNote(string notes, string marker) =>
            string.IsNullOrEmpty(notes) ? marker : notes + "|" + marker;

        // 제안 CSV의 본문 줄에서 (from,to,side) 키를 뽑는다.
        // 헤더·빈 줄·따옴표 셀·파싱 불가는 null을 돌려준다.
        internal static string DataLineKey(string line)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith("from_frame", StringComparison.Ordinal) ||
                line.Contains("\""))
            {
                return null;
            }
            string[] cells = line.Split(',');
            if (cells.Length < 3) return null;
            if (!int.TryParse(cells[0], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int from) ||
                !int.TryParse(cells[1], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int to) ||
                (cells[2] != "left" && cells[2] != "right"))
            {
                return null;
            }
            return RowKey(from, to, cells[2] == "left");
        }

        // 원본 CSV 줄들에 2차 재질의 결과를 행 단위로 치환한다.
        // replacements의 키에 맞는 원본 행은 새 줄들로, 나머지는 그대로.
        // 한 키는 한 번만 치환해 같은 키의 중복 원본 행이 있어도
        // 결과가 복제되지 않게 한다. 치환된 원본 행 수를 돌려준다.
        internal static int MergeRequeryLines(IEnumerable<string> lines,
            Dictionary<string, List<string>> replacements, StringBuilder output)
        {
            var pending = new Dictionary<string, List<string>>(replacements);
            int merged = 0;
            foreach (string line in lines)
            {
                string key = DataLineKey(line);
                if (key != null && pending.TryGetValue(key,
                        out List<string> repl))
                {
                    pending.Remove(key);
                    merged++;
                    foreach (string replLine in repl)
                    {
                        output.AppendLine(replLine);
                    }
                    continue;
                }
                output.AppendLine(line);
            }
            return merged;
        }

        // [from, to] 안쪽에 기존 제안 행이 하나라도 있으면 true.
        // 전이 분리 행(부모 범위보다 작은 행)도 잡아 재실행 중복을 막는다.
        internal static bool HasSuggestionInside(
            List<SuggestedRow> rows, int from, int to, bool isLeft) =>
            rows != null && rows.Any(r => r.IsLeft == isLeft &&
                r.From >= from && r.To <= to);

        private bool IsCovered(SpanJob job) =>
            HasSuggestionInside(_suggestedRows, job.From, job.To, job.IsLeft);

        // 분할된 하위 스팬 중 제안 행도 없고 이번 런에 아직 질의하지 않은
        // 것만 남긴다. 부모 안쪽에 행 일부만 있어도 나머지 하위는 남는다.
        internal static List<SpanJob> FilterFreshSubs(List<SpanJob> subs,
            List<SuggestedRow> rows, HashSet<string> keys) =>
            subs.Where(s => !HasSuggestionInside(rows,
                    s.From, s.To, s.IsLeft) &&
                keys.Add(RowKey(s.From, s.To, s.IsLeft))).ToList();

        // 불확실 스팬의 이전·다음 의도 구간을 intent 목록에서 찾아
        // 모델이 "착지 전이인가 이륙 전이인가"를 해석할 맥락을 만든다.
        // 의도 사이의 공백은 공중 프레임이다.
        private JObject ComputeBoundedBy(SpanJob job)
        {
            string side = job.IsLeft ? "left" : "right";
            FindBounds(_intent.intents, side, job.From, job.To,
                out IntentRowDto prev, out IntentRowDto next);
            var bounded = new JObject();
            bounded["prev"] = prev == null
                ? (JToken)"클립 시작"
                : new JObject
                {
                    ["label"] = prev.mode,
                    ["airborne_gap_frames"] = job.From - prev.end_frame_exclusive,
                };
            bounded["next"] = next == null
                ? (JToken)"클립 끝"
                : new JObject
                {
                    ["airborne_gap_frames"] = next.start_frame - job.To - 1,
                    ["label"] = next.mode,
                };
            return bounded;
        }

        // 같은 발의 확정(confident) 의도 구간 중 스팬 앞뒤에서 가장 가까운
        // 것을 고른다. 불확실 이웃은 추정기 추측이라 사실처럼 주입하지 않는다.
        internal static void FindBounds(IntentRowDto[] intents, string side,
            int from, int to, out IntentRowDto prev, out IntentRowDto next)
        {
            prev = null;
            next = null;
            if (intents == null) return;
            foreach (IntentRowDto intent in intents)
            {
                if (intent.side != side || intent.certainty != "confident")
                {
                    continue;
                }
                if (intent.end_frame_exclusive <= from &&
                    (prev == null ||
                        intent.end_frame_exclusive > prev.end_frame_exclusive))
                {
                    prev = intent;
                }
                if (intent.start_frame > to &&
                    (next == null ||
                        intent.start_frame < next.start_frame))
                {
                    next = intent;
                }
            }
        }

        // 리그 조립은 드리프트 진단과 같은 경로: 대상 모델에 원본 클립을 직접 재생.
        // withRender=false면 측정 전용으로 카메라·광원·간이 바닥을 만들지 않는다.
        private bool SetupRig() => SetupRig(true);

        private bool SetupRig(bool withRender)
        {
            string clipPath = ResolveClipPath(_intent.input);
            if (clipPath == null)
            {
                _status = $"클립을 찾지 못했습니다: {_intent.input}";
                return false;
            }
            AnimationClip clip = LoadHumanoidClip(clipPath);
            if (clip == null)
            {
                _status = $"휴머노이드 클립을 찾지 못했습니다: {clipPath}";
                return false;
            }
            _clipFrames = Mathf.Max(1, Mathf.RoundToInt(clip.length *
                (_intent != null && _intent.frame_rate > 0f
                    ? _intent.frame_rate : clip.frameRate)));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                global::Fbx2Vmd.FBXImporter.FbxDiagnostics.DefaultTargetPath);
            if (prefab == null)
            {
                _status = "대상 모델을 찾지 못했습니다: " +
                    global::Fbx2Vmd.FBXImporter.FbxDiagnostics.DefaultTargetPath;
                return false;
            }
            try
            {
                _model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            catch (Exception error)
            {
                _status = $"모델 인스턴스화 실패: {error.Message}";
                return false;
            }
            _model.hideFlags = HideFlags.HideAndDontSave;
            // 씬의 기존 모델과 겹쳐 렌더·씬 뷰를 오염시키지 않게 멀리 둔다.
            // 카메라가 발 위치를 따라가므로 프레임에는 이 모델만 나온다.
            _model.transform.position = _rigOffset;
            if (withRender)
            {
                // 오프셋 위치에는 씬 바닥이 없어 접촉 기준면이 사라지므로 간이 바닥을 깐다.
                _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                _ground.hideFlags = HideFlags.HideAndDontSave;
                _ground.transform.position = _rigOffset;
                _ground.transform.localScale = new Vector3(1f, 1f, 1f);
            }
            _animator = _model.GetComponent<Animator>();
            if (_animator == null || !_animator.isHuman)
            {
                _status = "대상에 휴머노이드 Animator가 없습니다.";
                TeardownRig();
                return false;
            }
            _controller = CreateController();
            string prepareError = null;
            if (_controller == null || !Prepare(_controller, _animator, clip,
                out prepareError))
            {
                _status = prepareError ?? "재생 컨트롤러 준비 실패.";
                TeardownRig();
                return false;
            }
            _seek = BuildSeek(_controller);
            if (_seek == null)
            {
                _status = "Seek 메서드를 찾지 못했습니다.";
                TeardownRig();
                return false;
            }
            _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _leftToe = _animator.GetBoneTransform(HumanBodyBones.LeftToes);
            _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _rightToe = _animator.GetBoneTransform(HumanBodyBones.RightToes);
            if (_leftFoot == null || _rightFoot == null)
            {
                _status = "발 본을 찾지 못했습니다.";
                TeardownRig();
                return false;
            }
            if (!withRender) return true;
            var cameraGo = new GameObject("JevLabelCamera")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            _camera = cameraGo.AddComponent<Camera>();
            _renderTexture = new RenderTexture(RenderWidth, RenderHeight, 24);
            _camera.targetTexture = _renderTexture;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            _camera.nearClipPlane = 0.01f;
            _camera.enabled = false; // 수동 Render만 사용
            _readback = new Texture2D(RenderWidth, RenderHeight,
                TextureFormat.RGB24, false);
            // 검은 부츠가 어두운 배경에 묻히지 않게 발 추적 보조광을 둔다.
            _light = new GameObject("JevLabelLight")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var light = _light.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 4f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;
            return true;
        }

        // 라벨링 창 미리보기용 리그 어댑터. 실행 중인 자동 제안 리그와
        // 겹치지 않게 별도 오프셋에 세우고 PNG 프레임만 돌려준다.
        internal bool SetupPreview(string clipInput, float frameRate,
            float humanScale)
        {
            _intent = new IntentFileDto { input = clipInput, frame_rate = frameRate };
            _humanScale = humanScale > 0f ? humanScale : 1f;
            _rigOffset = new Vector3(2000f, 0f, 0f);
            return SetupRig();
        }

        internal string PreviewStatus => _status;

        internal byte[][] RenderPreviewSpan(int from, int to, bool isLeft) =>
            RenderSpan(new SpanJob { From = from, To = to, IsLeft = isLeft }).Pngs;

        internal void EndPreview() => TeardownRig();

        private void TeardownRig()
        {
            _controller?.Dispose();
            _controller = null;
            if (_model != null) Object.DestroyImmediate(_model);
            _model = null;
            if (_camera != null) Object.DestroyImmediate(_camera.gameObject);
            _camera = null;
            if (_ground != null) Object.DestroyImmediate(_ground);
            _ground = null;
            if (_light != null) Object.DestroyImmediate(_light);
            _light = null;
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Object.DestroyImmediate(_renderTexture);
            }
            _renderTexture = null;
            if (_readback != null) Object.DestroyImmediate(_readback);
            _readback = null;
        }

        private void Update()
        {
            // 저장/연결 확인 버튼이 던진 키 검증 요청의 결과를 받는다.
            if (_apiCheckTask != null && _apiCheckTask.IsCompleted)
            {
                try
                {
                    _apiCheckStatus = _apiCheckTask.GetAwaiter().GetResult();
                }
                catch (Exception error)
                {
                    _apiCheckStatus = $"연결 확인 실패: {error.Message}";
                }
                _apiCheckTask = null;
                Repaint();
            }
            if (!_running) return;
            // 플레이 모드 진입 시 도메인 리로드로 리그가 파괴되므로 실행을 멈춤.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                StopRun(false, "플레이 모드 진입으로 중단했습니다.");
                return;
            }
            if (_httpTask != null)
            {
                if (!_httpTask.IsCompleted) return;
                ConsumeResult();
                return;
            }
            bool cancel = EditorUtility.DisplayCancelableProgressBar(
                "Jev 라벨 제안", $"스팬 {_done}/{_total} 처리 중…",
                _total > 0 ? (float)_done / _total : 0f);
            if (cancel)
            {
                StopRun(false);
                return;
            }
            if (_jobIndex >= _jobs.Count)
            {
                StopRun(false, $"처리 종료: 응답 {_done}건, 오류 {_errors}건.");
                Repaint();
                return;
            }
            _current = _jobs[_jobIndex++];
            // 재질의 잡은 원본 행과 범위가 같아야 병합이 성립하므로
            // 분할·커버 검사를 하지 않는다.
            bool requery = _current.Kind == RequeryKind;
            bool expanded = false;
            // 긴 스팬은 발목 높이 궤적의 전이점에서 균질 하위 구간으로 나눈다.
            // 혼합 구간에 라벨 하나를 묻는 질문 자체가 성립하지 않기 때문.
            if (!requery && _current.To - _current.From + 1 >= MinSplitLenFrames)
            {
                try
                {
                    List<SpanJob> subs = SplitSpan(_current);
                    if (subs.Count > 1)
                    {
                        // 하위 행만 CSV에 남으므로 부모는 재실행 시 다시 큐에 들어온다.
                        // 이미 제안된 하위를 걸러야 재처리·중복 기록을 막을 수 있다.
                        var fresh = FilterFreshSubs(subs,
                            _suggestedRows, _suggestedKeys);
                        if (fresh.Count == 0)
                        {
                            return;
                        }
                        _jobs.InsertRange(_jobIndex, fresh.Skip(1));
                        _total += fresh.Count - 1;
                        _current = fresh[0];
                        expanded = true;
                    }
                }
                catch (Exception error)
                {
                    Debug.LogWarning($"[JevLabeler] 스팬 분할 실패 " +
                        $"{_current.From}~{_current.To}: {error.Message}");
                }
            }
            // 분할로 확장된 스팬은 하위 단위로 이미 걸러졌다. 그 외 스팬은
            // 범위 안쪽에 제안 행이 있으면 전이 분리 등으로 처리가 끝난 것이므로
            // 중복 질의·중복 기록을 막는다. 부모 범위에 안쪽 행 일부만 있어도
            // 여기서 건너뛰면 아직 제안 없는 하위가 영구 누락된다.
            if (!expanded && !requery && IsCovered(_current)) return;
            SpanRender render;
            try
            {
                render = RenderSpan(_current);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[JevLabeler] 렌더 실패 " +
                    $"{_current.From}~{_current.To}: {error.Message}");
                _errors++;
                return;
            }
            string side = _current.IsLeft ? "left" : "right";
            _currentFrames = render.Frames;
            string body = BuildRequestJson(_intent.input, side,
                _current.From, _current.To, render.Frames, render.Pngs,
                render.Measures, ComputeBoundedBy(_current),
                render.FrontPngs);
            _httpTask = PostAsync(body);
        }

        private void ConsumeResult()
        {
            Task<string> task = _httpTask;
            _httpTask = null;
            try
            {
                List<SegmentResult> segments = ParseResponse(
                    task.GetAwaiter().GetResult(),
                    _current.From, _current.To, _currentFrames);
                if (_requeryMode)
                {
                    BufferRequeryResult(_current, segments);
                    _done++;
                    try
                    {
                        SaveRequeryCheckpoint(_csvPath, _requeryOriginal, _requeryLines);
                    }
                    catch (Exception error)
                    {
                        StopRun(false, $"결과 보존 실패로 추가 요청 중단: {error.Message}");
                    }
                    return;
                }
                // 전이로 나뉘면 경계 프레임(둘째 세그먼트 시작)을 메모에 남긴다.
                string note = _current.Kind;
                if (segments.Count > 1)
                {
                    note += $"|t={segments[1].From}";
                }
                foreach (SegmentResult seg in segments)
                {
                    _writer.WriteLine(FormatSuggestedRow(seg.From, seg.To,
                        _current.IsLeft, seg.Contact, seg.Motion,
                        "jev", note, seg.ContactConf, seg.MotionConf));
                    _suggestedKeys.Add(RowKey(seg.From, seg.To, _current.IsLeft));
                    _suggestedRows.Add(new SuggestedRow
                    {
                        From = seg.From, To = seg.To, IsLeft = _current.IsLeft,
                    });
                }
                _writer.Flush();
                _done++;
            }
            catch (Exception error)
            {
                _errors++;
                Debug.LogWarning($"[JevLabeler] 스팬 {_current.From}~" +
                    $"{_current.To} 요청 실패: {error.Message}");
                // 인증 오류는 이후 호출도 전부 실패하므로 실행을 멈춤.
                string message = error.Message ?? "";
                if (message.Contains("401") || message.Contains("403"))
                {
                    StopRun(false, "API 키 오류(401/403). TYPESAFE_API_KEY 확인.");
                    EditorUtility.ClearProgressBar();
                    Repaint();
                }
            }
        }

        // 재질의 결과는 원본 행의 메모에 마커를 붙인 치환 줄로 버퍼한다.
        // 원본 행을 찾지 못해도 마커만 달린 줄로 버퍼해 실행 자체는 살린다.
        private void BufferRequeryResult(SpanJob job,
            List<SegmentResult> segments)
        {
            SuggestedRow origin = _suggestedRows.Find(r =>
                r.IsLeft == job.IsLeft && r.From == job.From &&
                r.To == job.To);
            string note = AppendNote(origin?.Notes, RequeryKind);
            if (segments.Count > 1)
            {
                note += $"|t={segments[1].From}";
            }
            var lines = new List<string>(segments.Count);
            foreach (SegmentResult seg in segments)
            {
                lines.Add(FormatSuggestedRow(seg.From, seg.To, job.IsLeft,
                    seg.Contact, seg.Motion, "jev", note,
                    seg.ContactConf, seg.MotionConf));
            }
            _requeryLines[RowKey(job.From, job.To, job.IsLeft)] = lines;
        }

        // 원본 변경 여부를 확인한 뒤 저장하고 실패 시 응답을 보존함.
        private string FinishRequery()
        {
            _requeryMode = false;
            _requeryMerged = 0;
            if (_requeryLines.Count == 0) return "재질의 저장 대상 없음.";
            try
            {
                SaveRequeryCheckpoint(_csvPath, _requeryOriginal ??
                    File.ReadAllText(_csvPath), _requeryLines);
                _requeryMerged = ApplyRequeryCheckpoint(_csvPath);
                _requeryLines.Clear();
                _status = $"재질의 치환 {_requeryMerged}행 저장.";
            }
            catch (Exception error)
            {
                _requeryMerged = 0;
                _status = $"재질의 결과 저장 실패: {error.Message} " +
                    "미저장 결과를 보존했습니다. 저장 재시도를 사용하세요.";
                Debug.LogException(error);
            }
            return _status;
        }

        private void StopRun(bool silent, string reason = "중단했습니다.")
        {
            _running = false;
            _httpTask = null;
            _jobs.Clear();
            _writer?.Dispose();
            _writer = null;
            if (_requeryMode || _requeryLines.Count > 0)
                _status = reason + " " + FinishRequery();
            else if (!silent)
                _status = reason;
            TeardownRig();
            EditorUtility.ClearProgressBar();
            if (!silent) Repaint();
        }

        private string PendingCsvPath()
        {
            if (_requeryLines.Count > 0) return _csvPath;
            if (!string.IsNullOrEmpty(_csvPath) &&
                File.Exists(_csvPath + ".requery-pending.json")) return _csvPath;
            if (_selectedFile < 0 || _selectedFile >= _intentFiles.Count) return null;
            string path = Path.Combine(Path.GetDirectoryName(_intentFiles[_selectedFile]),
                SuggestedFileName);
            return File.Exists(path + ".requery-pending.json") ? path : null;
        }

        private bool BlockPendingRequery()
        {
            string path = PendingCsvPath();
            if (path == null) return false;
            _status = $"미저장 재질의 결과가 있습니다: {path}. 저장 재시도를 먼저 사용하세요.";
            return true;
        }

        private void RetryRequerySave()
        {
            string path = PendingCsvPath();
            if (path == null) return;
            if (_requeryLines.Count > 0)
            {
                FinishRequery();
                return;
            }
            try
            {
                _status = $"재질의 복구 {ApplyRequeryCheckpoint(path)}행 저장.";
            }
            catch (Exception error)
            {
                _status = $"재질의 복구 실패: {error.Message}. 복구 파일을 유지합니다.";
            }
        }

        private static string TextHash(string text)
        {
            using (var hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        internal static void SaveRequeryCheckpoint(string csvPath, string original,
            Dictionary<string, List<string>> replacements)
        {
            var output = new StringBuilder();
            var lines = new List<string>();
            using (var reader = new StringReader(original))
            {
                string line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
            }
            int merged = MergeRequeryLines(lines, replacements, output);
            if (merged == 0 || merged != replacements.Count)
                throw new InvalidDataException("원본 구간과 재질의 결과가 일치하지 않습니다.");
            string result = output.ToString();
            var checkpoint = new JObject
            {
                ["original_hash"] = TextHash(original),
                ["result_hash"] = TextHash(result),
                ["merged"] = merged,
                ["result"] = result,
            };
            FootDefectScan.WriteAllTextAtomic(csvPath + ".requery-pending.json",
                checkpoint.ToString());
        }

        internal static int ApplyRequeryCheckpoint(string csvPath)
        {
            string pendingPath = csvPath + ".requery-pending.json";
            JObject checkpoint = JObject.Parse(File.ReadAllText(pendingPath));
            string result = checkpoint.Value<string>("result");
            string originalHash = checkpoint.Value<string>("original_hash");
            string resultHash = checkpoint.Value<string>("result_hash");
            int merged = checkpoint.Value<int>("merged");
            if (merged <= 0 || string.IsNullOrEmpty(originalHash) ||
                string.IsNullOrEmpty(result) || !result.StartsWith(SuggestedHeader + Environment.NewLine,
                    StringComparison.Ordinal) || TextHash(result) != resultHash)
                throw new InvalidDataException("재질의 복구 파일이 손상됐습니다.");
            string current = File.ReadAllText(csvPath);
            string currentHash = TextHash(current);
            if (currentHash != resultHash)
            {
                if (currentHash != originalHash)
                    throw new IOException("실행 이후 제안 CSV가 변경됐습니다. 자동 덮어쓰기를 중단합니다.");
                // 읽은 뒤 외부 편집이 발생한 경우에도 덮어쓰지 않음.
                if (File.ReadAllText(csvPath) != current)
                    throw new IOException("저장 직전 제안 CSV가 변경됐습니다.");
                FootDefectScan.WriteAllTextAtomic(csvPath, result);
            }
            File.Delete(pendingPath);
            return merged;
        }

        // 발 클로즈업: 발목+발끝 중점을 발 축에 수직인 측면 저앵글로 촬영.
        // 카메라는 반대발이 없는 쪽에 둔다. 프레임별 측정값(높이·속도·방향)을
        // 같이 모아 질문 state로 넘긴다.
        private SpanRender RenderSpan(SpanJob job)
        {
            Transform foot = job.IsLeft ? _leftFoot : _rightFoot;
            Transform toe = job.IsLeft ? _leftToe : _rightToe;
            var pngs = new List<byte[]>();
            var frontPngs = new List<byte[]>();
            int[] frames = SampleFrames(job.From, job.To);
            var measures = new List<FrameMeasure>();
            Vector3 prevMid = Vector3.zero;
            float prevAnkleY = 0f;
            float fps = _intent.frame_rate > 0f ? _intent.frame_rate : 60f;
            foreach (int frame in frames)
            {
                if (!SeekFrameIndex(frame, fps))
                {
                    throw new InvalidOperationException(
                        $"시크 실패 프레임 {frame}");
                }
                Vector3 mid = toe != null
                    ? (foot.position + toe.position) * 0.5f
                    : foot.position;
                // 루트 모션으로 모델이 이동하므로 프레임마다 바닥을 발 아래로 옮긴다.
                if (_ground != null)
                {
                    _ground.transform.position = new Vector3(mid.x, 0f, mid.z);
                }
                // 발 축에 수직인 측면 저앵글이어야 밑창-지면 틈이 포즈와
                // 무관하게 보인다. 반대발이 없는 쪽을 택해야 지나가는
                // 다리가 시선을 가리지 않는다.
                Vector3 fwd = Vector3.ProjectOnPlane(
                    foot.forward, Vector3.up);
                Vector3 side = fwd.sqrMagnitude > 0.0001f
                    ? Vector3.Cross(Vector3.up, fwd.normalized)
                    : Vector3.right;
                Transform otherFoot = job.IsLeft ? _rightFoot : _leftFoot;
                if (otherFoot != null &&
                    Vector3.Dot(side, otherFoot.position - mid) > 0f)
                {
                    side = -side;
                }
                _camera.transform.position = mid +
                    (side * 0.32f + Vector3.up * 0.07f) * _humanScale;
                _camera.transform.LookAt(mid);
                if (_light != null)
                {
                    // 카메라-발 사이 약간 위에서 비춰 밑창-지면 경계를 밝힌다.
                    _light.transform.position =
                        _camera.transform.position +
                        Vector3.up * 0.15f * _humanScale;
                }
                pngs.Add(CaptureShot());
                if (job.FrontView)
                {
                    // 발 진행방향 정면 저앵글 — 측면에서 안 보이는 좌우 롤
                    // 틈과 발 앞쪽 접촉면을 보여준다. 발 전방이 위로 뭉개지면
                    // 반대 측면으로 대체(각도는 달라져도 정보는 보존).
                    Vector3 front = fwd.sqrMagnitude > 0.0001f
                        ? fwd.normalized : -side;
                    _camera.transform.position = mid +
                        (front * 0.32f + Vector3.up * 0.07f) * _humanScale;
                    _camera.transform.LookAt(mid);
                    if (_light != null)
                    {
                        _light.transform.position =
                            _camera.transform.position +
                            Vector3.up * 0.15f * _humanScale;
                    }
                    frontPngs.Add(CaptureShot());
                }
                // 측정: 높이는 렌더 바닥 y=0 기준 cm, 속도는 중점의 수평 이동,
                // 수직 방향은 이전 샘플 대비 발목 높이 차(±0.5cm)로 판정한다.
                // 첫 프레임은 이전이 없어 "기준"으로 둔다.
                float dyCm = (foot.position.y - prevAnkleY) * 100f;
                int gap = frame - (measures.Count > 0 ? frames[measures.Count - 1] : frame);
                float dt = gap / fps;
                float speed = dt > 0f
                    ? Vector3.ProjectOnPlane(mid - prevMid, Vector3.up).magnitude /
                        dt * 100f
                    : 0f;
                string vdir = measures.Count == 0 ? "기준"
                    : dyCm > 0.5f ? "상승"
                    : dyCm < -0.5f ? "하강" : "정지";
                measures.Add(new FrameMeasure
                {
                    Frame = frame,
                    AnkleCm = foot.position.y * 100f,
                    ToeCm = toe != null ? toe.position.y * 100f : -1f,
                    SpeedCms = speed,
                    Vdir = vdir,
                });
                prevMid = mid;
                prevAnkleY = foot.position.y;
            }
            return new SpanRender
            {
                Pngs = pngs.ToArray(),
                FrontPngs = job.FrontView ? frontPngs.ToArray() : null,
                Frames = frames,
                Measures = measures.ToArray(),
            };
        }

        // 카메라·조명 위치는 호출부가 정한다. 수동 렌더 후 PNG 바이트를 돌려준다.
        private byte[] CaptureShot()
        {
            _camera.Render();
            RenderTexture.active = _renderTexture;
            _readback.ReadPixels(
                new Rect(0, 0, RenderWidth, RenderHeight), 0, 0);
            _readback.Apply();
            RenderTexture.active = null;
            return _readback.EncodeToPNG();
        }

        private bool SeekFrameIndex(int frame, float fps)
        {
            return _seek != null && _seek(frame / fps);
        }

        // 스팬 전체를 훑어 발목 월드 높이 궤적을 만들고,
        // 극값 중간점을 경계로 하는 균질 하위 스팬 목록을 돌려준다.
        private List<SpanJob> SplitSpan(SpanJob job)
        {
            Transform foot = job.IsLeft ? _leftFoot : _rightFoot;
            int len = job.To - job.From + 1;
            var ys = new float[len];
            float fps = _intent.frame_rate > 0f ? _intent.frame_rate : 60f;
            for (int i = 0; i < len; i++)
            {
                if (!SeekFrameIndex(job.From + i, fps))
                {
                    throw new InvalidOperationException(
                        $"시크 실패 프레임 {job.From + i}");
                }
                ys[i] = foot.position.y;
            }
            // 사람 스케일 기준 2cm 미만 요동은 노이즈로 무시한다.
            float minAmp = 0.02f * _humanScale;
            int minLen = Mathf.Max(4,
                (int)(fps * 0.12f));
            List<int> offsets = FindSplitOffsets(ys, minAmp, minLen,
                MaxSubSpans);
            var subs = new List<SpanJob> { job };
            if (offsets.Count == 0) return subs;
            subs.Clear();
            int prev = job.From;
            int part = 0;
            foreach (int off in offsets)
            {
                subs.Add(new SpanJob
                {
                    From = prev, To = job.From + off - 1,
                    IsLeft = job.IsLeft,
                    Kind = $"{job.Kind}/p{++part}",
                });
                prev = job.From + off;
            }
            subs.Add(new SpanJob
            {
                From = prev, To = job.To,
                IsLeft = job.IsLeft,
                Kind = $"{job.Kind}/p{++part}",
            });
            return subs;
        }

        // 구간을 최대 FramesPerSpan등분한 시간순 프레임(중복 제거, 시작·끝 포함).
        internal static int[] SampleFrames(int from, int to)
        {
            var frames = new List<int>();
            int length = to - from;
            for (int i = 0; i < FramesPerSpan; i++)
            {
                int frame = length <= 0
                    ? from
                    : from + (int)Math.Round(length * (double)i / (FramesPerSpan - 1));
                if (!frames.Contains(frame)) frames.Add(frame);
            }
            return frames.ToArray();
        }

        private static string ResolveClipPath(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            string candidate = Path.Combine(
                "Assets", "Resources", "Import_FBX", Path.GetFileName(input));
            if (File.Exists(candidate)) return candidate;
            string[] guids = AssetDatabase.FindAssets(
                Path.GetFileNameWithoutExtension(input));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(Path.GetExtension(input),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            return null;
        }

        // 본체 어셈블리의 internal 컨트롤러를 리플렉션으로 사용(드리프트 진단과 동일).
        private static IDisposable CreateController()
        {
            Type type = Type.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController, Assembly-CSharp");
            return type == null ? null : (IDisposable)Activator.CreateInstance(type);
        }

        // Prepare는 void — Invoke가 null 반환. 실패는 내부 예외로만 전달된다.
        private static bool Prepare(IDisposable controller,
            Animator animator, AnimationClip clip, out string error)
        {
            error = null;
            MethodInfo method = controller.GetType().GetMethod("Prepare",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                error = "Prepare 메서드를 찾지 못했습니다.";
                return false;
            }
            try
            {
                method.Invoke(controller, new object[] { animator, clip });
                return true;
            }
            catch (TargetInvocationException wrapped)
            {
                error = $"Prepare 실패: {wrapped.InnerException?.Message}";
                return false;
            }
        }

        private static Func<float, bool> BuildSeek(IDisposable controller)
        {
            MethodInfo seek = controller.GetType().GetMethod("Seek",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (seek == null) return null;
            return t =>
            {
                try
                {
                    return (bool)seek.Invoke(controller, new object[] { t });
                }
                catch (TargetInvocationException wrapped)
                {
                    throw wrapped.InnerException ?? wrapped;
                }
            };
        }

        private static AnimationClip LoadHumanoidClip(string path)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith("__preview", StringComparison.Ordinal))
                {
                    return clip;
                }
            }
            return null;
        }

        // Jev 이미지 요청 본문. 자산 참조는 토큰 치환이라 접두사 충돌을 피해
        // 요청마다 고유 문자열을 쓴다.
        internal static string BuildRequestJson(string clip, string side,
            int from, int to, int[] frames, byte[][] pngFrames,
            FrameMeasure[] measures, JObject boundedBy,
            byte[][] frontPngs = null)
        {
            string prefix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var assets = new JObject();
            var state = new JObject
            {
                ["clip"] = clip,
                ["side"] = side,
                ["span_frames"] = $"{from}~{to}(inclusive)",
            };
            if (boundedBy != null)
            {
                state["bounded_by"] = boundedBy;
            }
            for (int i = 0; i < pngFrames.Length; i++)
            {
                string key = $"shot_{prefix}_{i}";
                assets[key] = new JObject
                {
                    ["type"] = "image",
                    ["format"] = "png",
                    ["data_base64"] = Convert.ToBase64String(pngFrames[i]),
                };
                state[$"frame_image_{i}"] = key;
            }
            bool hasFront = frontPngs != null &&
                frontPngs.Length == pngFrames.Length;
            if (hasFront)
            {
                for (int i = 0; i < frontPngs.Length; i++)
                {
                    string key = $"shotf_{prefix}_{i}";
                    assets[key] = new JObject
                    {
                        ["type"] = "image",
                        ["format"] = "png",
                        ["data_base64"] = Convert.ToBase64String(frontPngs[i]),
                    };
                    state[$"frame_front_image_{i}"] = key;
                }
            }
            // VLM은 이미지에서 거리를 못 재므로(SPHERE 등 문헌 확인)
            // 렌더 시 측정한 실제 수치를 넘겨 해석만 시킨다.
            if (measures != null && measures.Length > 0)
            {
                var measArr = new JArray();
                foreach (FrameMeasure m in measures)
                {
                    measArr.Add(new JObject
                    {
                        ["frame"] = m.Frame,
                        ["ankle_cm"] = Math.Round(m.AnkleCm, 1),
                        ["toe_cm"] = Math.Round(m.ToeCm, 1),
                        ["speed_cms"] = Math.Round(m.SpeedCms, 1),
                        ["vdir"] = m.Vdir,
                    });
                }
                state["measurements"] = measArr;
            }
            string sideName = side == "left" ? "왼" : "오른";
            // 전방 뷰가 있으면 두 각도를 함께 보라고 지시 — 측면에서
            // 모호했던 좌우 롤 틈과 앞쪽 접촉이 정면에서는 판별된다.
            string viewHint = hasFront
                ? $" 측면 뷰는 frame_image_, 발 정면 뷰는 frame_front_image_ " +
                  "키를 함께 참고하세요."
                : "";
            var questions = new JObject();
            // 2단계 질의: 먼저 "닿았나"(이진)를 묻고, 닿은 프레임들만
            // 부위 투표로 집계한다. 공중 프레임에 부위를 묻는
            // 불가능한 질문이 불확실을 양산하기 때문.
            for (int i = 0; i < pngFrames.Length; i++)
            {
                questions[$"touching_f{i}"] = new JObject
                {
                    ["type"] = "noul",
                    ["instructions"] =
                        $"state의 frame_image_{i} 이미지는 {sideName}발을 " +
                        $"근접 촬영한 한 프레임입니다.{viewHint} " +
                        $"measurements[{i}]의 발목·발끝 높이와 방향도 참고해 " +
                        "이 발이 지면에 닿아 있는지 판정하세요.",
                    ["criteria"] = new JObject
                    {
                        ["true"] = "발의 어느 부분이든 지면에 닿아 몸을 지지함",
                        ["false"] = "발이 지면에서 완전히 떠 있음",
                    },
                };
                questions[$"contact_f{i}"] = new JObject
                {
                    ["type"] = "choice",
                    ["instructions"] =
                        $"state의 frame_image_{i}가 가리키는 이미지는 3D 캐릭터 " +
                        $"애니메이션 {from}~{to}프레임 구간에서 {sideName}발을 " +
                        $"근접 촬영한 한 프레임입니다.{viewHint} " +
                        "이 프레임에서 발이 지면에 닿는 상태를 하나 고르세요.",
                    ["criteria"] = new JObject
                    {
                        ["airborne"] = "발이 지면에 닿지 않고 공중에 떠 있음",
                        ["toes"] = "발끝·앞꿈치만 지면에 닿아 몸을 지지함",
                        ["heel"] = "뒤꿈치만 지면에 닿아 몸을 지지함",
                        ["whole"] = "발바닥 전체가 지면에 닿아 몸을 지지함",
                        ["uncertain"] = "이미지만으로는 접촉 상태를 확정하기 어려움",
                    },
                };
            }
            // 혼합 구간은 라벨 하나가 성립하지 않으므로 전이 프레임을
            // 직접 지목하게 한다. 지목 시 전후 세그먼트로 나눠 기록한다.
            var transCriteria = new JObject
            {
                ["none"] = "구간 내내 접촉 상태가 같음",
            };
            foreach (int frame in frames)
            {
                transCriteria[frame.ToString(CultureInfo.InvariantCulture)] =
                    $"프레임 {frame}에서 접촉 상태가 바뀜";
            }
            questions["transition_frame"] = new JObject
            {
                ["type"] = "choice",
                ["instructions"] =
                    $"같은 구간의 {sideName}발입니다. measurements의 높이·방향 " +
                    "변화와 이미지를 참고해 접촉 상태(닿음↔떨어짐)가 바뀌는 " +
                    "프레임이 있으면 그 프레임을 고르세요." + viewHint,
                ["criteria"] = transCriteria,
            };
            questions["motion"] = new JObject
            {
                ["type"] = "choice",
                ["instructions"] =
                    $"같은 구간의 {sideName}발입니다. 이미지 순서대로 발의 " +
                    $"수평 이동을 보고 동작 유형을 하나 고르세요.{viewHint} " +
                    "지면에 닿은 프레임들의 이동만 의미가 있습니다.",
                ["criteria"] = new JObject
                {
                    ["pinned"] = "접촉한 발이 같은 자리에 고정되어 수평 이동이 없음",
                    ["roll"] = "뒤꿈치→앞꿈치 또는 앞꿈치→뒤꿈치로 구르듯 접촉점이 이동함",
                    ["slide"] = "발이 지면에 닿은 채 의도적으로 미끄러져 수평 이동함",
                    ["uncertain"] = "프레임만으로 동작 유형을 확정하기 어려움",
                },
            };
            var request = new JObject
            {
                ["model"] = Model,
                ["state"] = state,
                ["questions"] = questions,
                ["assets"] = assets,
            };
            return request.ToString(Newtonsoft.Json.Formatting.None);
        }

        // 제안 한 행(세그먼트). 전이 분리 시 한 스팬이 2행이 된다.
        internal sealed class SegmentResult
        {
            public int From;
            public int To;
            public string Contact;
            public string Motion;
            public float ContactConf;
            public float MotionConf;
        }

        // 신뢰도가 임계 미만이거나 모르는 선택지면 '불확실'로 내린다.
        internal static string MapLabel(string key,
            Dictionary<string, string> map, float confidence) =>
            confidence < MinSuggestConfidence ||
            !map.TryGetValue(key ?? "", out string label)
                ? "불확실" : label;

        // 발목 높이 궤적의 극값(peak/trough)을 찾아 스팬을 균질 구간으로 나눈다.
        // 경계 = 연속 극값 사이의 중간 프레임. minAmp 미만 요동은 노이즈로 무시한다.
        // 반환값은 ys 안에서 새 구간이 시작되는 인덱스 목록(0 제외, 오름차순).
        internal static List<int> FindSplitOffsets(float[] ys,
            float minAmp, int minLen, int maxParts)
        {
            var splits = new List<int>();
            int n = ys.Length;
            if (n < minLen * 2) return splits;
            // 3점 이동평균으로 스파이크성 극값을 눌러 단조 구간을 안정화한다.
            var sm = new float[n];
            for (int i = 0; i < n; i++)
            {
                float a = ys[Mathf.Max(0, i - 1)];
                float b = ys[i];
                float c = ys[Mathf.Min(n - 1, i + 1)];
                sm[i] = (a + b + c) / 3f;
            }
            // prominence >= minAmp 인 극값만 채택하는 지그재그 탐지.
            // 극값은 평탄부의 (시작, 끝) 쌍으로 잡아 경계가 전이 경사
            // 한가운데에 놓이게 한다.
            var extrema = new List<Vector2Int>();
            int dir = 0;
            float pivot = sm[0];
            int pivotStart = 0;
            int pivotEnd = 0;
            for (int i = 1; i < n; i++)
            {
                float d = sm[i] - pivot;
                if (dir >= 0 && d < -minAmp)
                {
                    extrema.Add(new Vector2Int(pivotStart, pivotEnd));
                    dir = -1;
                    pivot = sm[i];
                    pivotStart = i;
                    pivotEnd = i;
                }
                else if (dir <= 0 && d > minAmp)
                {
                    extrema.Add(new Vector2Int(pivotStart, pivotEnd));
                    dir = 1;
                    pivot = sm[i];
                    pivotStart = i;
                    pivotEnd = i;
                }
                else if (dir > 0 && sm[i] > pivot)
                {
                    pivot = sm[i];
                    pivotStart = i;
                    pivotEnd = i;
                }
                else if (dir < 0 && sm[i] < pivot)
                {
                    pivot = sm[i];
                    pivotStart = i;
                    pivotEnd = i;
                }
                else if (sm[i] == pivot)
                {
                    pivotEnd = i;
                }
            }
            extrema.Add(new Vector2Int(pivotStart, pivotEnd));
            if (extrema.Count < 2) return splits;
            // 연속 극값 사이 경사의 중간점을 경계 후보로 둔다.
            for (int i = 0; i + 1 < extrema.Count; i++)
            {
                splits.Add((extrema[i].y + extrema[i + 1].x) / 2);
            }
            // 최소 길이 미만 구간이 생기는 경계는 제거.
            var kept = new List<int>();
            int prev = 0;
            foreach (int s in splits)
            {
                if (s - prev < minLen || n - s < minLen) continue;
                prev = s;
                kept.Add(s);
            }
            // 구간 수 상한: 초과하면 경계 간격이 넓은 것만 남긴다.
            while (kept.Count + 1 > maxParts)
            {
                int mergeAt = 0;
                int best = int.MaxValue;
                for (int i = 0; i < kept.Count; i++)
                {
                    int left = i == 0 ? 0 : kept[i - 1];
                    int right = i + 1 == kept.Count ? n : kept[i + 1];
                    int span = right - left;
                    if (span < best)
                    {
                        best = span;
                        mergeAt = i;
                    }
                }
                kept.RemoveAt(mergeAt);
            }
            return kept;
        }

        // 응답을 파싱해 제안 행 1~2개를 돌려준다. touching_f*(이진)로
        // 닿음/공중을 먼저 가르고, 닿은 프레임만 부위(contact_f*) 투표로
        // 집계한다. transition_frame 지목이 유효하면 전후 세그먼트로 나눈다.
        internal static List<SegmentResult> ParseResponse(string json,
            int from, int to, int[] frames)
        {
            JObject answers = JObject.Parse(json)["answers"] as JObject;
            JObject motion = answers?["motion"] as JObject;
            if (answers == null || motion == null)
            {
                throw new InvalidDataException(
                    "응답에 contact_f*/motion 답변이 없습니다.");
            }
            var frameLabels = new List<string>();
            var frameConfs = new List<float>();
            var touched = new List<bool>();
            var touchW = new List<float>();
            for (int i = 0; ; i++)
            {
                JObject frame = answers[$"contact_f{i}"] as JObject;
                if (frame == null) break;
                float conf = frame.Value<float?>("confidence") ?? 0f;
                frameLabels.Add(MapLabel(frame.Value<string>("choice"),
                    ContactMap, conf));
                frameConfs.Add(conf);
                // noul 답변은 yes 확률이 그대로 닿음 가중치가 된다.
                // 답변이 없으면 알 수 없는 표라 집계에서 제외한다.
                JObject touch = answers[$"touching_f{i}"] as JObject;
                float? p = touch?.Value<float?>("noul");
                bool isTouch = p.HasValue && p.Value >= 0.5f;
                touched.Add(isTouch);
                touchW.Add(p.HasValue
                    ? (isTouch ? p.Value : 1f - p.Value)
                    : 0f);
            }
            if (frameLabels.Count == 0)
            {
                throw new InvalidDataException(
                    "응답에 contact_f* 답변이 없습니다.");
            }
            float motionConf = motion.Value<float?>("confidence") ?? 0f;
            string motionLabel = MapLabel(motion.Value<string>("choice"),
                MotionMap, motionConf);
            // 유효한 전이 지목이면 경계 전후로 세그먼트를 나눠 각각 집계한다.
            int boundary = ResolveTransitionBoundary(
                answers["transition_frame"] as JObject, frames,
                frameLabels.Count);
            if (boundary > 0)
            {
                int split = 0;
                while (split < frames.Length && frames[split] < boundary)
                {
                    split++;
                }
                return new List<SegmentResult>
                {
                    BuildSegment(from, boundary - 1, touched, touchW,
                        frameLabels, frameConfs, 0, split - 1,
                        motionLabel, motionConf),
                    BuildSegment(boundary, to, touched, touchW,
                        frameLabels, frameConfs, split,
                        frameLabels.Count - 1,
                        motionLabel, motionConf),
                };
            }
            return new List<SegmentResult>
            {
                BuildSegment(from, to, touched, touchW,
                    frameLabels, frameConfs, 0, frameLabels.Count - 1,
                    motionLabel, motionConf),
            };
        }

        // 지목 프레임이 샘플 프레임 안에 있고, 양쪽 세그먼트에 샘플이
        // 2개 이상 남을 때만 경계로 채택한다.
        private static int ResolveTransitionBoundary(JObject transition,
            int[] frames, int answerCount)
        {
            if (transition == null || frames == null) return 0;
            float conf = transition.Value<float?>("confidence") ?? 0f;
            if (conf < 0.6f) return 0;
            if (!int.TryParse(transition.Value<string>("choice"),
                    NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int boundary))
            {
                return 0;
            }
            // 선택지에 없는 임의 숫자는 경계로 쓰지 않는다.
            if (Array.IndexOf(frames, boundary) < 0) return 0;
            int split = 0;
            while (split < frames.Length && frames[split] < boundary)
            {
                split++;
            }
            return split >= 2 && answerCount - split >= 2 ? boundary : 0;
        }

        // 한 세그먼트의 닿음/공중 비율과 부위 투표로 라벨을 정한다.
        private static SegmentResult BuildSegment(int from, int to,
            List<bool> touched, List<float> touchW,
            List<string> labels, List<float> confs,
            int startIdx, int endIdx,
            string motionLabel, float motionConf)
        {
            float touch = 0f, air = 0f;
            for (int i = startIdx; i <= endIdx; i++)
            {
                if (touched[i]) touch += touchW[i];
                else air += touchW[i];
            }
            float total = touch + air;
            string contact;
            float share;
            if (total > 0f && air / total >= 0.6f)
            {
                contact = "공중";
                share = air / total;
            }
            else if (total > 0f && touch / total >= 0.6f)
            {
                // 닿은 프레임 중 부위 라벨만 가중 집계. 공중·불확실
                // 답은 부위 투표에서 기권으로 처리한다.
                var weights = new Dictionary<string, float>();
                float wsum = 0f;
                for (int i = startIdx; i <= endIdx; i++)
                {
                    if (!touched[i]) continue;
                    string label = labels[i];
                    if (label != "앞꿈치" && label != "뒤꿈치" &&
                        label != "발 전체") continue;
                    weights[label] = (weights.TryGetValue(label,
                        out float w) ? w : 0f) + confs[i];
                    wsum += confs[i];
                }
                string part = "불확실";
                float partShare = 0f;
                foreach (var pair in weights)
                {
                    float s = wsum > 0f ? pair.Value / wsum : 0f;
                    if (s > partShare)
                    {
                        partShare = s;
                        part = pair.Key;
                    }
                }
                if (wsum <= 0f || partShare < 0.6f)
                {
                    contact = "불확실";
                    share = touch / total;
                }
                else
                {
                    contact = part;
                    share = partShare;
                }
            }
            else
            {
                contact = "불확실";
                share = total > 0f ? Mathf.Max(touch, air) / total : 0f;
            }
            // 공중 세그먼트에는 동작 의도가 성립하지 않는다.
            bool airborne = contact == "공중";
            return new SegmentResult
            {
                From = from,
                To = to,
                Contact = contact,
                Motion = airborne ? "불확실" : motionLabel,
                ContactConf = share,
                // 공중 구간의 동작 신뢰도는 의미가 없어 라벨과 함께 비운다.
                MotionConf = airborne ? 0f : motionConf,
            };
        }

        // 창에 입력한 키(EditorPrefs)를 우선 쓰고 없으면 환경변수를 본다.
        private static string ResolveApiKey()
        {
            string pref = EditorPrefs.GetString(ApiKeyPref);
            return !string.IsNullOrEmpty(pref)
                ? pref
                : Environment.GetEnvironmentVariable(ApiKeyEnv);
        }

        // GET /v1/models — 토큰을 쓰지 않고 키 유효성만 확인하는 가벼운 경로.
        private static async Task<string> CheckApiAsync(string key)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.typesafe.ai/v1/models"))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", key);
                using (HttpResponseMessage response = await Http.SendAsync(request)
                    .ConfigureAwait(false))
                {
                    string text = await response.Content.ReadAsStringAsync()
                        .ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return $"연결 실패 HTTP {(int)response.StatusCode} — 키를 확인하세요.";
                    }
                    JArray models = JObject.Parse(text)["models"] as JArray;
                    string names = models == null ? "?"
                        : string.Join(", ", models
                            .Select(m => m["name"]?.ToString())
                            .Where(n => !string.IsNullOrEmpty(n)));
                    return $"연결됨 — 사용 가능 모델: {names}";
                }
            }
        }

        private static async Task<string> PostAsync(string body)
        {
            string key = ResolveApiKey();
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException(
                    $"API 키가 없습니다(창 입력 또는 {ApiKeyEnv}).");
            }
            using (var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", key);
                // 이미지 자산 사용에 필요한 헤더(공식 문서상 실험 기능).
                request.Headers.Add("TypeSafe-Danger-Zone-Images", "true");
                request.Content = new StringContent(body,
                    Encoding.UTF8, "application/json");
                using (HttpResponseMessage response = await Http.SendAsync(request)
                    .ConfigureAwait(false))
                {
                    string text = await response.Content.ReadAsStringAsync()
                        .ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        string excerpt = text.Length > 200
                            ? text.Substring(0, 200) : text;
                        throw new InvalidOperationException(
                            $"Jev HTTP {(int)response.StatusCode}: {excerpt}");
                    }
                    return text;
                }
            }
        }

        private static string FormatSuggestedRow(int from, int to, bool isLeft,
            string contact, string motion, string reviewer, string notes,
            float contactConf, float motionConf) =>
            string.Join(",",
                from.ToString(CultureInfo.InvariantCulture),
                to.ToString(CultureInfo.InvariantCulture),
                isLeft ? "left" : "right",
                CsvCell(contact), CsvCell(motion), CsvCell(reviewer),
                CsvCell(notes),
                contactConf.ToString("0.###", CultureInfo.InvariantCulture),
                motionConf.ToString("0.###", CultureInfo.InvariantCulture));

        private static string CsvCell(string value) =>
            !string.IsNullOrEmpty(value) && (value.Contains(",") ||
                value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;

        // 제안 CSV를 읽는다. 이 파일의 작성자가 이 클래스뿐이므로 따옴표 셀이
        // 섞이면 해당 행은 건너뛴다.
        internal static List<SuggestedRow> ReadSuggestions(string csvPath)
        {
            var rows = new List<SuggestedRow>();
            if (!File.Exists(csvPath)) return rows;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(csvPath);
            }
            catch (Exception error) when (error is IOException ||
                error is UnauthorizedAccessException)
            {
                return rows;
            }
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("from_frame", StringComparison.Ordinal))
                {
                    continue;
                }
                if (line.Contains("\"")) continue;
                string[] cells = line.Split(',');
                if (cells.Length < 7) continue;
                if (!int.TryParse(cells[0], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int from) ||
                    !int.TryParse(cells[1], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int to))
                {
                    continue;
                }
                var row = new SuggestedRow
                {
                    From = from,
                    To = to,
                    IsLeft = cells[2] == "left",
                    SideKnown = cells[2] == "left" || cells[2] == "right",
                    Contact = cells[3],
                    Motion = cells[4],
                };
                if (cells.Length > 5) row.Reviewer = cells[5];
                if (cells.Length > 6) row.Notes = cells[6];
                if (cells.Length > 7)
                {
                    float.TryParse(cells[7], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out row.ContactConf);
                }
                if (cells.Length > 8)
                {
                    float.TryParse(cells[8], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out row.MotionConf);
                }
                rows.Add(row);
            }
            return rows;
        }
    }
}
