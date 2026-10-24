using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 접촉 의도 추정 결과(contact-intent.json)를 보고 사람 표식을 human-labels.csv로
    /// 기록하는 창임. 보정 로직을 수정하지 않고 추정기의 정답 데이터만 생산함.
    /// 행 서식·어휘는 run-product-smoke.mjs와 HumanoidFootContactIntentLabelStore에 맞춤.
    /// </summary>
    public class FootContactIntentLabelingWindow : EditorWindow
    {
        private const string EvidenceRoot = "Docs/Workflow/Local/evidence/boogle";
        private const string IntentFileName = "contact-intent.json";
        private const string LabelsFileName = "human-labels.csv";
        private const string StateFileName = "state.json";
        // 헤더는 run-product-smoke.mjs 템플릿·validate-human-labels.mjs가 요구하는
        // 비따옴표 서식과 바이트 단위로 같아야 함(검증기가 첫 줄을 문자열 그대로 비교).
        internal const string Header =
            "from_frame,to_frame,side,contact_label,motion_label,reviewer,notes";
        // "미기록"은 미선택 상태를 뜻하는 UI 전용 값으로 파일에 기록하지 않고,
        // "불확실"은 검증기 어휘에 있는 정규 표식이다.
        private static readonly string[] ContactOptions =
            { "미기록", "공중", "앞꿈치", "뒤꿈치", "발 전체", "불확실" };
        private static readonly string[] MotionOptions =
            { "미기록", "고정", "구르기", "의도된 이동", "불확실" };

        [Serializable]
        private sealed class IntentFile
        {
            public string input;
            public float frame_rate;
            public float source_human_scale;
            public string generated_at;
            public IntentRow[] intents = new IntentRow[0];
            public SpanRow[] uncertain_spans = new SpanRow[0];
        }

        [Serializable]
        private sealed class IntentRow
        {
            public string side;
            public int start_frame;
            public int end_frame_exclusive;
            public string mode;
            public string certainty;
            public bool starts_at_clip_start;
        }

        [Serializable]
        private sealed class SpanRow
        {
            public string side;
            public int start_frame;
            public int end_frame_exclusive;
        }

        private sealed class LabelEntry
        {
            internal int From;
            internal int To;
            internal bool IsLeft = true;
            internal int Contact;
            internal int Motion;
            internal string Reviewer = "";
            internal string Notes = "";
        }

        private sealed class StoredLabel
        {
            internal int From, To;
            internal bool IsLeft;
            internal string Contact, Motion;
        }

        private Vector2 _scroll;
        private List<string> _intentFiles = new List<string>();
        private int _selectedFile;
        private IntentFile _intent;
        private readonly List<LabelEntry> _pending = new List<LabelEntry>();
        private readonly List<StoredLabel> _stored = new List<StoredLabel>();
        // Jev 자동 제안(suggested-labels.csv) — 표식 대상 선택 시 프리필용.
        private readonly Dictionary<string,
            EditorTools.FBXImporter.FootContactIntentAutoLabeler.SuggestedRow>
            _suggestions = new Dictionary<string,
                EditorTools.FBXImporter.FootContactIntentAutoLabeler.SuggestedRow>();
        // 결함 스캔 결과(foot-defects.csv) — 스팬 행 배지와 표식 요약용.
        private readonly List<EditorTools.FBXImporter.FootDefectScan.DefectEvent>
            _defects =
                new List<EditorTools.FBXImporter.FootDefectScan.DefectEvent>();
        private string _message = "";
        private int _newFrom, _newTo;
        private bool _newLeft = true;
        private int _newContact, _newMotion;
        private string _newReviewer = "", _newNotes = "";
        private bool _showIntents = true;

        // 프레임 미리보기: 자동라벨러의 격리 리그를 빌려 발 클로즈업을 그린다.
        // 씬을 건드리지 않고 API 호출도 없다.
        private EditorTools.FBXImporter.FootContactIntentAutoLabeler _previewRig;
        private string _rigInput;
        private Texture2D[] _thumbTextures;
        private int[] _thumbFrames;
        private Texture2D _previewTexture;
        private int _previewFrom, _previewTo, _scrubFrame;
        private bool _previewLeft;

        [MenuItem("Tools/FBXImporter/접촉 의도 라벨링")]
        private static void Open() =>
            GetWindow<FootContactIntentLabelingWindow>(false, "접촉 의도 라벨링");

        private void OnEnable() => RefreshFileList();

        private void OnDisable() => ReleaseRig();

        private void RefreshFileList()
        {
            _intentFiles.Clear();
            string root = Path.Combine(ProjectRoot, EvidenceRoot);
            if (Directory.Exists(root))
            {
                try
                {
                    _intentFiles.AddRange(Directory.GetFiles(
                        root, IntentFileName, SearchOption.AllDirectories));
                }
                catch (Exception error) when (error is IOException ||
                    error is UnauthorizedAccessException)
                {
                    // 열거할 수 없는 하위 폴더는 건너뛰고 나머지 파일로 계속함.
                }
            }
            _intentFiles.Sort(StringComparer.Ordinal);
            _selectedFile = 0;
            int dropped = _pending.Count;
            ReleaseRig();
            LoadSelected();
            // 새로고침은 선택을 초기화하므로 미저장 표식이 버려졌음을 알림.
            if (dropped > 0 && string.IsNullOrEmpty(_message))
                _message = $"새로고침으로 저장하지 않은 표식 {dropped}건을 버렸습니다.";
        }

        private static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

        private void LoadSelected()
        {
            _intent = null;
            _stored.Clear();
            // 다른 입력의 표식이 새 파일 폴더에 섞여 기록되지 않게 대기 행도 비움.
            _pending.Clear();
            if (_selectedFile < 0 || _selectedFile >= _intentFiles.Count) return;
            try
            {
                _intent = JsonUtility.FromJson<IntentFile>(
                    File.ReadAllText(_intentFiles[_selectedFile]));
            }
            catch (Exception error)
            {
                _message = $"의도 파일을 읽지 못했습니다: {error.Message}";
                return;
            }
            if (_intent == null) return;
            LoadStoredLabels(_intent.input);
            LoadSuggestions();
            LoadDefects();
        }

        // 결함 스캔을 다시 돌린 뒤 선택 파일을 바꾸지 않고도 최신
        // foot-defects.csv를 반영할 수 있게 분리해 둠.
        private void LoadDefects()
        {
            _defects.Clear();
            // "결함 다시 읽기" 버튼은 빈 목록 상태에서도 눌릴 수 있어 경계 가드를 둔다.
            if (_selectedFile < 0 || _selectedFile >= _intentFiles.Count) return;
            _defects.AddRange(EditorTools.FBXImporter.FootDefectScan.ReadEvents(
                Path.Combine(Path.GetDirectoryName(_intentFiles[_selectedFile]),
                    EditorTools.FBXImporter.FootDefectScan.FileName)));
        }

        // 제안 파일은 같은 폴더의 suggested-labels.csv. 없으면 프리필 없이 동작.
        private void LoadSuggestions()
        {
            _suggestions.Clear();
            string folder = Path.GetDirectoryName(_intentFiles[_selectedFile]);
            string path = Path.Combine(folder,
                EditorTools.FBXImporter.FootContactIntentAutoLabeler.SuggestedFileName);
            foreach (var row in EditorTools.FBXImporter.FootContactIntentAutoLabeler
                .ReadSuggestions(path))
            {
                _suggestions[
                    EditorTools.FBXImporter.FootContactIntentAutoLabeler.RowKey(
                        row.From, row.To, row.IsLeft)] = row;
            }
        }

        // 선택한 스팬에 Jev 제안이 있으면 표식·메모를 채워 사람이 확인만 하게 함.
        private void PrefillSuggestion()
        {
            if (_suggestions.TryGetValue(
                    EditorTools.FBXImporter.FootContactIntentAutoLabeler.RowKey(
                        _newFrom, _newTo, _newLeft),
                    out var suggestion))
            {
                ApplySuggestion(suggestion);
                return;
            }
            // 스팬이 전이점 분할로 하위 구간 제안을 받았으면 목록을 보여준다.
            var contained = new List<
                EditorTools.FBXImporter.FootContactIntentAutoLabeler.SuggestedRow>();
            foreach (var row in _suggestions.Values)
            {
                if (row.IsLeft == _newLeft &&
                    row.From >= _newFrom && row.To <= _newTo)
                {
                    contained.Add(row);
                }
            }
            if (contained.Count == 0) return;
            contained.Sort((a, b) => a.From.CompareTo(b.From));
            var parts = new List<string>();
            foreach (var row in contained)
            {
                parts.Add($"{row.From}~{row.To} {row.Contact}/" +
                    $"{row.Motion}({row.ContactConf:0.00},{row.MotionConf:0.00})");
            }
            string summary = $"jev 하위 제안 {contained.Count}건: " +
                string.Join(", ", parts);
            if (string.IsNullOrEmpty(_newNotes)) _newNotes = summary;
            else if (!_newNotes.Contains("jev")) _newNotes += " | " + summary;
            _message = "스팬이 혼합 구간이라 하위 제안이 나뉘어 있습니다. " +
                "범위를 나눠 표식하거나 불확실로 남기세요.";
        }

        private void ApplySuggestion(
            EditorTools.FBXImporter.FootContactIntentAutoLabeler.SuggestedRow
                suggestion)
        {
            int contact = Array.IndexOf(ContactOptions, suggestion.Contact);
            int motion = Array.IndexOf(MotionOptions, suggestion.Motion);
            if (contact > 0) _newContact = contact;
            if (motion > 0) _newMotion = motion;
            string note =
                $"jev 제안 접촉={suggestion.Contact}({suggestion.ContactConf:0.00}) " +
                $"동작={suggestion.Motion}({suggestion.MotionConf:0.00})";
            // 사용자가 적은 메모가 있으면 지우지 않고 덧붙이되,
            // 같은 제안을 다시 눌러도 중복 누적되지 않게 함.
            if (string.IsNullOrEmpty(_newNotes)) _newNotes = note;
            else if (!_newNotes.Contains("jev 제안")) _newNotes += " | " + note;
            _message = "jev 제안을 채웠습니다. 내용을 확인하고 표식 행을 추가하세요.";
        }

        // 라벨 스토어는 본체 어셈블리의 internal이라 리플렉션으로 읽음.
        private void LoadStoredLabels(string input)
        {
            _stored.Clear(); // 재조회 시 기존 목록과 중복되지 않게 항상 초기화
            Type storeType = Type.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentLabelStore, Assembly-CSharp");
            object set = storeType
                ?.GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, new object[] { input });
            if (set == null)
            {
                _message = "라벨 스토어를 찾지 못했습니다(Assembly-CSharp 갱신 필요).";
                return;
            }
            foreach (string prop in new[] { "Left", "Right" })
            {
                bool isLeft = prop == "Left";
                if (!(set.GetType().GetProperty(prop,
                        BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.GetValue(set) is System.Collections.IEnumerable labels))
                    continue;
                foreach (object label in labels)
                {
                    Type t = label.GetType();
                    var row = new StoredLabel
                    {
                        IsLeft = isLeft,
                        From = (int)GetProp(t, label, "StartFrame"),
                        To = (int)GetProp(t, label, "EndFrameInclusive"),
                    };
                    object isSupport = GetPropObj(t, label, "IsSupport");
                    row.Contact = isSupport == null ? "미기록"
                        : (bool)isSupport ? "지지" : "공중";
                    object mode = GetPropObj(t, label, "Mode");
                    row.Motion = mode?.ToString() ?? "미기록";
                    _stored.Add(row);
                }
            }
            _stored.Sort((a, b) => a.From != b.From
                ? a.From.CompareTo(b.From) : a.IsLeft.CompareTo(b.IsLeft));
        }

        // 속성이 없으면 0으로 숨기지 않고 API 드리프트를 명시적으로 알림.
        private static object GetPropObj(Type t, object instance, string name)
        {
            PropertyInfo property = t.GetProperty(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (property == null)
                throw new InvalidOperationException($"{t.Name}.{name} 속성이 없습니다.");
            return property.GetValue(instance);
        }

        private static int GetProp(Type t, object instance, string name) =>
            (int)GetPropObj(t, instance, name);

        private void OnGUI()
        {
            EditorGUILayout.LabelField("1. 의도 추정 결과 선택", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("목록 새로고침", GUILayout.Width(110)))
            {
                RefreshFileList();
            }
            // 스캔을 다시 돌린 뒤 파일 선택을 바꾸지 않고 최신 결함만 반영.
            if (GUILayout.Button("결함 다시 읽기", GUILayout.Width(110)))
            {
                LoadDefects();
            }
            EditorGUILayout.EndHorizontal();
            if (_intentFiles.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"{EvidenceRoot} 아래 {IntentFileName}이 없습니다. 제품 경로 실행으로 먼저 생성하세요.",
                    MessageType.Info);
                return;
            }
            int previous = _selectedFile;
            _selectedFile = Mathf.Clamp(_selectedFile, 0, _intentFiles.Count - 1);
            string[] names = _intentFiles.ConvertAll(ShortName).ToArray();
            _selectedFile = EditorGUILayout.Popup("추정 결과", _selectedFile, names);
            if (_selectedFile != previous)
            {
                // 입력이 바뀌면 대기 표식이 다른 증거 폴더에 섞이지 않게 버리고 알림.
                if (_pending.Count > 0)
                    _message =
                        $"선택 변경으로 저장하지 않은 표식 {_pending.Count}건을 버렸습니다.";
                ReleaseRig();
                LoadSelected();
            }
            if (_intent == null) return;
            EditorGUILayout.LabelField(
                $"입력: {_intent.input} · 프레임률: {_intent.frame_rate} · 생성: {_intent.generated_at}");

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSpanList();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            DrawPreview();
            EditorGUILayout.Space();
            DrawLabelEditor();
            DrawPending();
            DrawStored();
            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, MessageType.Warning);
            }
        }

        private string ShortName(string path)
        {
            string relative = path.Replace(ProjectRoot + Path.DirectorySeparatorChar, "");
            return relative.Replace('\\', '/');
        }

        private void DrawSpanList()
        {
            _showIntents = EditorGUILayout.Foldout(_showIntents,
                $"2. 구간 목록 — 의도 {_intent.intents.Length}건 · 불확실 {_intent.uncertain_spans.Length}건");
            if (!_showIntents) return;
            foreach (SpanRow span in _intent.uncertain_spans)
            {
                bool isLeft = span.side == "left";
                int defects = EditorTools.FBXImporter.FootDefectScan
                    .CountOverlapping(_defects, span.start_frame,
                        span.end_frame_exclusive - 1, isLeft);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"  [불확실] {span.side} {span.start_frame}~" +
                    $"{span.end_frame_exclusive - 1}" +
                    (defects > 0 ? $" · 결함 {defects}" : ""));
                if (GUILayout.Button("표식 대상", GUILayout.Width(80)))
                {
                    _newFrom = span.start_frame;
                    _newTo = span.end_frame_exclusive - 1;
                    _newLeft = span.side == "left";
                    PrefillSuggestion();
                    PreparePreview(span.start_frame,
                        span.end_frame_exclusive - 1, _newLeft);
                }
                EditorGUILayout.EndHorizontal();
            }
            foreach (IntentRow intent in _intent.intents)
            {
                bool isLeft = intent.side == "left";
                int defects = EditorTools.FBXImporter.FootDefectScan
                    .CountOverlapping(_defects, intent.start_frame,
                        intent.end_frame_exclusive - 1, isLeft);
                string mark = intent.certainty == "uncertain" ? "[불확실 흡수] " : "";
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"  {mark}{intent.side} {intent.start_frame}~" +
                    $"{intent.end_frame_exclusive - 1} · {intent.mode}" +
                    (defects > 0 ? $" · 결함 {defects}" : ""));
                if (GUILayout.Button("표식 대상", GUILayout.Width(80)))
                {
                    _newFrom = intent.start_frame;
                    _newTo = intent.end_frame_exclusive - 1;
                    _newLeft = intent.side == "left";
                    PrefillSuggestion();
                    PreparePreview(intent.start_frame,
                        intent.end_frame_exclusive - 1, _newLeft);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // 스팬 선택 시 발 클로즈업 썸네일을 렌더하고 슬라이더로 임의
        // 프레임을 본다. 썸네일 클릭이나 "경계로" 버튼으로 표식 범위를 잡는다.
        private void PreparePreview(int from, int to, bool isLeft)
        {
            _previewFrom = from;
            _previewTo = to;
            _previewLeft = isLeft;
            _scrubFrame = from;
            if (!EnsureRig())
            {
                ClearPreviewTextures();
                return; // 실패 원인은 EnsureRig가 _message에 기록한다.
            }
            int[] frames =
                EditorTools.FBXImporter.FootContactIntentAutoLabeler
                    .SampleFrames(from, to);
            byte[][] pngs;
            try
            {
                pngs = _previewRig.RenderPreviewSpan(from, to, isLeft);
            }
            catch (Exception error)
            {
                ClearPreviewTextures();
                _message = $"미리보기 렌더 실패({from}~{to}): {error.Message}";
                return;
            }
            ClearPreviewTextures();
            _thumbFrames = frames;
            _thumbTextures = DecodePngs(pngs);
            ShowPreviewFrame(from);
        }

        private bool EnsureRig()
        {
            string input = _intent.input;
            if (_previewRig != null && _rigInput == input) return true;
            ReleaseRig();
            _previewRig = CreateInstance<
                EditorTools.FBXImporter.FootContactIntentAutoLabeler>();
            _rigInput = input;
            if (_previewRig.SetupPreview(input, _intent.frame_rate,
                _intent.source_human_scale))
            {
                return true;
            }
            string status = _previewRig.PreviewStatus;
            ReleaseRig();
            _message = "미리보기 리그 준비 실패: " + status;
            return false;
        }

        private void ShowPreviewFrame(int frame)
        {
            if (_previewRig == null) return;
            _scrubFrame = Mathf.Clamp(frame, _previewFrom, _previewTo);
            byte[][] pngs;
            try
            {
                pngs = _previewRig.RenderPreviewSpan(_scrubFrame, _scrubFrame,
                    _previewLeft);
            }
            catch (Exception error)
            {
                _message = $"프레임 {_scrubFrame} 렌더 실패: {error.Message}";
                return;
            }
            if (_previewTexture != null) DestroyImmediate(_previewTexture);
            _previewTexture = pngs.Length > 0 ? DecodePng(pngs[0]) : null;
        }

        private static Texture2D DecodePng(byte[] png)
        {
            var texture = new Texture2D(2, 2);
            texture.LoadImage(png);
            return texture;
        }

        private static Texture2D[] DecodePngs(byte[][] pngs)
        {
            var textures = new Texture2D[pngs.Length];
            for (int i = 0; i < pngs.Length; i++)
                textures[i] = DecodePng(pngs[i]);
            return textures;
        }

        private void ClearPreviewTextures()
        {
            if (_thumbTextures != null)
            {
                foreach (Texture2D t in _thumbTextures)
                    if (t != null) DestroyImmediate(t);
            }
            _thumbTextures = null;
            _thumbFrames = null;
            if (_previewTexture != null) DestroyImmediate(_previewTexture);
            _previewTexture = null;
        }

        private void ReleaseRig()
        {
            ClearPreviewTextures();
            if (_previewRig != null)
            {
                _previewRig.EndPreview();
                DestroyImmediate(_previewRig);
                _previewRig = null;
            }
            _rigInput = null;
        }

        private void DrawPreview()
        {
            // 프레임 배열과 텍스처는 항상 같이 채워져야 한다(어긋나면 NRE).
            if (_thumbTextures == null || _thumbTextures.Length == 0 ||
                _thumbFrames == null ||
                _thumbFrames.Length != _thumbTextures.Length) return;
            EditorGUILayout.LabelField(
                $"3. 프레임 미리보기 — {(_previewLeft ? "left" : "right")} " +
                $"{_previewFrom}~{_previewTo}", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < _thumbTextures.Length; i++)
            {
                if (_thumbTextures[i] == null) continue;
                int frame = _thumbFrames[i];
                if (GUILayout.Button(_thumbTextures[i], GUIStyle.none,
                        GUILayout.Width(96), GUILayout.Height(72)))
                {
                    ShowPreviewFrame(frame);
                }
            }
            EditorGUILayout.EndHorizontal();
            int scrub = EditorGUILayout.IntSlider(
                "프레임", _scrubFrame, _previewFrom, _previewTo);
            if (scrub != _scrubFrame) ShowPreviewFrame(scrub);
            if (_previewTexture != null)
            {
                GUILayout.Label(_previewTexture, GUILayout.Width(320),
                    GUILayout.Height(240));
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("이 프레임을 시작으로", GUILayout.Width(140)))
                _newFrom = _scrubFrame;
            if (GUILayout.Button("이 프레임을 끝으로", GUILayout.Width(140)))
                _newTo = _scrubFrame;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLabelEditor()
        {
            EditorGUILayout.LabelField("4. 표식 입력", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _newFrom = EditorGUILayout.IntField("시작", _newFrom, GUILayout.Width(200));
            _newTo = EditorGUILayout.IntField("끝(포함)", _newTo, GUILayout.Width(200));
            _newLeft = GUILayout.Toggle(_newLeft, "왼발", "Button", GUILayout.Width(60));
            bool right = GUILayout.Toggle(!_newLeft, "오른발", "Button", GUILayout.Width(60));
            if (right) _newLeft = false;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            _newContact = EditorGUILayout.Popup("접촉", _newContact, ContactOptions);
            _newMotion = EditorGUILayout.Popup("동작", _newMotion, MotionOptions);
            EditorGUILayout.EndHorizontal();
            _newReviewer = EditorGUILayout.TextField("검토자", _newReviewer);
            _newNotes = EditorGUILayout.TextField("메모", _newNotes);
            string validation = ValidateEntry(_newFrom, _newTo, _newContact, _newMotion,
                _newReviewer);
            if (validation != null)
            {
                EditorGUILayout.HelpBox(validation, MessageType.None);
            }
            DrawDefectSummary();
            using (new EditorGUI.DisabledScope(validation != null))
            {
                if (GUILayout.Button("표식 행 추가"))
                {
                    _pending.Add(new LabelEntry
                    {
                        From = _newFrom, To = _newTo, IsLeft = _newLeft,
                        Contact = _newContact, Motion = _newMotion,
                        Reviewer = _newReviewer, Notes = _newNotes,
                    });
                }
            }
        }

        // 표식 범위와 겹치는 결함 이벤트를 요약해 "이 구간의 불확실이
        // 원본 결함에서 비롯된 것인지" 판단 재료를 제공한다.
        private void DrawDefectSummary()
        {
            var overlapping = new List<
                EditorTools.FBXImporter.FootDefectScan.DefectEvent>();
            foreach (var e in _defects)
            {
                if (e.IsLeft == _newLeft && e.StartFrame <= _newTo &&
                    e.EndFrame >= _newFrom)
                {
                    overlapping.Add(e);
                }
            }
            if (overlapping.Count == 0) return;
            var parts = new List<string>();
            // 이벤트가 많으면 상위 3건만 보여 상자가 에디터를 잠식하지 않게 한다.
            const int maxShown = 3;
            foreach (var e in overlapping.Take(maxShown))
            {
                parts.Add(
                    $"{EditorTools.FBXImporter.FootDefectScan.KindLabel(e.Kind)} " +
                    $"{e.StartFrame}~{e.EndFrame} " +
                    $"({e.WorstMeters * 1000f:0.0}mm@{e.PeakFrame})");
            }
            string extra = overlapping.Count > maxShown
                ? $" 외 {overlapping.Count - maxShown}건" : "";
            EditorGUILayout.HelpBox(
                $"결함 {overlapping.Count}건: " + string.Join(", ", parts) + extra,
                MessageType.Warning);
        }

        // 행 서식은 smoke 템플릿과 같은 비따옴표 CSV이며 자유 서술 셀만
        // 쉼표·따옴표·줄바꿈이 있을 때 최소 인용으로 이스케이프한다.
        internal static string FormatRow(int from, int to, bool isLeft,
            string contact, string motion, string reviewer, string notes) =>
            string.Join(",",
                from.ToString(CultureInfo.InvariantCulture),
                to.ToString(CultureInfo.InvariantCulture),
                isLeft ? "left" : "right",
                CsvCell(contact), CsvCell(motion), CsvCell(reviewer), CsvCell(notes));

        private static string CsvCell(string value) =>
            !string.IsNullOrEmpty(value) && (value.Contains(",") || value.Contains("\"") ||
                value.Contains("\n") || value.Contains("\r"))
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;

        // 검증기는 두 표식과 검토자를 모두 요구하므로 미선택("미기록")은 저장 불가.
        private static string ValidateEntry(int from, int to, int contact, int motion,
            string reviewer)
        {
            if (from < 0) return "시작 프레임이 0보다 작습니다.";
            if (to < from) return "끝 프레임이 시작보다 빠릅니다.";
            if (contact == 0 || motion == 0)
                return "접촉·동작 표식을 모두 선택해야 합니다(모르는 항목은 '불확실').";
            if (string.IsNullOrWhiteSpace(reviewer)) return "검토자를 입력하세요.";
            return null;
        }

        private void DrawPending()
        {
            EditorGUILayout.LabelField($"5. 보류 중인 표식 {_pending.Count}건", EditorStyles.boldLabel);
            for (int index = _pending.Count - 1; index >= 0; index--)
            {
                LabelEntry entry = _pending[index];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"  {entry.From}~{entry.To} {(entry.IsLeft ? "left" : "right")} · " +
                    $"{ContactOptions[entry.Contact]} · {MotionOptions[entry.Motion]}");
                if (GUILayout.Button("제거", GUILayout.Width(60)))
                {
                    _pending.RemoveAt(index);
                }
                EditorGUILayout.EndHorizontal();
            }
            using (new EditorGUI.DisabledScope(_pending.Count == 0))
            {
                if (GUILayout.Button("human-labels.csv에 저장"))
                {
                    SavePending();
                }
            }
        }

        private void SavePending()
        {
            // 표식은 같은 입력의 state.json이 있는 증거 폴더에만 써야 스토어가 읽음.
            string folder = Path.GetDirectoryName(_intentFiles[_selectedFile]);
            string statePath = Path.Combine(folder, StateFileName);
            if (!File.Exists(statePath))
            {
                try
                {
                    File.WriteAllText(statePath,
                        JsonUtility.ToJson(new StateJson { input = _intent.input }, true));
                }
                catch (Exception error)
                {
                    _message = $"state.json 기록 실패: {error.Message}";
                    return;
                }
            }
            string csvPath = Path.Combine(folder, LabelsFileName);
            try
            {
                bool needsHeader = !File.Exists(csvPath) ||
                    new FileInfo(csvPath).Length == 0;
                using (var writer = new StreamWriter(csvPath, true,
                    new System.Text.UTF8Encoding(false)))
                {
                    if (needsHeader) writer.WriteLine(Header);
                    foreach (LabelEntry entry in _pending)
                    {
                        writer.WriteLine(FormatRow(entry.From, entry.To, entry.IsLeft,
                            ContactOptions[entry.Contact], MotionOptions[entry.Motion],
                            entry.Reviewer, entry.Notes));
                    }
                }
                _message = $"{_pending.Count}건을 {csvPath}에 기록했습니다.";
                _pending.Clear();
                LoadStoredLabels(_intent.input);
            }
            catch (Exception error)
            {
                _message = $"CSV 기록 실패: {error.Message}";
            }
        }

        [Serializable]
        private sealed class StateJson
        {
            public string input;
        }

        private void DrawStored()
        {
            EditorGUILayout.LabelField(
                $"6. 누적 표식 {_stored.Count}건(스토어가 같은 입력의 모든 증거 폴더에서 읽음)",
                EditorStyles.boldLabel);
            foreach (StoredLabel label in _stored)
            {
                EditorGUILayout.LabelField(
                    $"  {label.From}~{label.To} {(label.IsLeft ? "left" : "right")} · " +
                    $"{label.Contact} · {label.Motion}");
            }
        }
    }
}
