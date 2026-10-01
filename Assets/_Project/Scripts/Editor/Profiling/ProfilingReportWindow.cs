using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Profiling.EditorTools
{
    /// <summary>
    /// Artifacts/Profiling에 누적된 런 기록을 열람·비교하는 에디터 창입니다.
    /// </summary>
    public sealed class ProfilingReportWindow : EditorWindow
    {
        public const string MenuPath = "Tools/Profiling/Run Reports";
        private const string WindowTitle = "Profiling Reports";

        private string[] _reportFiles = Array.Empty<string>();
        private string _reportDirectory = string.Empty;
        private int _leftIndex;
        private int _rightIndex = -1;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private GUIStyle _monoStyle;
        private GUIStyle _warnStyle;
        private ProfilingAnalysisThresholds _thresholds;
        private int _detailIndex = -1;
        private Dictionary<string, List<float>> _historyByStage;
        private int _historyCount;
        private string _listFilter = string.Empty;
        private long[] _fileSizes = Array.Empty<long>();
        private bool _showThresholds;
        private ProfilingAnalysisThresholds _editThresholds;
        private List<ProfilingFinding> _analysisOverride;
        private List<ProfilingFinding> _analysisOverrideRight;
        // 재분석 결과가 어느 비교 인덱스의 것인지 추적 — 대상 변경 시 다른 런의 findings가 붙지 않게 한다.
        private int _analysisOverrideRightIndex = -1;
        private int _chartSourceIndex = -1;
        private Texture2D _chartTexture;
        private float _selStart;
        private float _selEnd = 1f;
        private bool _selecting;
        private int _stageSort;
        private bool _stageSortAsc = true;

        // OnGUI 이벤트마다 디스크를 읽지 않도록 로드 결과를 캐시한다.
        private readonly Dictionary<string, ProfilingRunRecord> _recordCache =
            new Dictionary<string, ProfilingRunRecord>(StringComparer.Ordinal);
        // JEV 사이드카 캐시 — 파일 상태(존재/수정시각/크기)만 주기적으로 확인해
        // 변경된 경우에만 재로드한다. 상태 확인은 Recheck 주기로 스로틀한다.
        private readonly Dictionary<string, JevCacheEntry> _jevCache =
            new Dictionary<string, JevCacheEntry>(StringComparer.Ordinal);
        private const double JevRecheckIntervalSec = 0.5;
        // 이력 증분 스캔 상태 — 에디터 틱당 시간 예산으로 잘라 진행해 GUI 블로킹을 막는다.
        private readonly Queue<string> _historyQueue = new Queue<string>();
        private ProfilingRunRecord _historyRun;
        private string _historyForRunId;
        private Dictionary<string, List<float>> _historyPending;
        private List<ProfilingRunRecord> _historyRecords;
        private int _historyTotal;
        // 결과 필터용 경량 메타 스캔 — outcome 필터를 켰을 때만 지연 수집한다.
        private readonly Dictionary<string, string> _outcomeCache =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _knownOutcomes =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _metaScanQueue = new Queue<string>();
        private readonly HashSet<string> _metaScanQueued =
            new HashSet<string>(StringComparer.Ordinal);
        private int _metaScanTotal;
        private int _metaScanDone;
        private string _outcomeFilter = string.Empty;
        private int _dateFilter;
        private static readonly string[] DateFilterNames =
            { "기간: 전체", "기간: 24시간", "기간: 7일", "기간: 30일" };
        private const int MaxHistoryRuns = 20;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<ProfilingReportWindow>(false, WindowTitle, true);
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(720f, 420f);
            window.Show();
            window.RefreshReports();
        }

        private void OnEnable()
        {
            RefreshReports();
            EditorApplication.update += PumpBackgroundWork;
        }

        private void OnDisable()
        {
            EditorApplication.update -= PumpBackgroundWork;
            if (_chartTexture != null)
            {
                DestroyImmediate(_chartTexture);
                _chartTexture = null;
            }
        }

        private void RefreshReports()
        {
            _reportDirectory = ProfilingReportWriter.OutputDirectory;
            _reportFiles = ProfilingReportWriter.ListReports();
            if (_leftIndex >= _reportFiles.Length) _leftIndex = 0;
            if (_rightIndex >= _reportFiles.Length) _rightIndex = -1;
            _thresholds = ProfilingAnalysisThresholds.Load();
            _detailIndex = -1;
            _recordCache.Clear();
            _jevCache.Clear();
            _outcomeCache.Clear();
            _knownOutcomes.Clear();
            _metaScanQueue.Clear();
            _metaScanQueued.Clear();
            _metaScanTotal = 0;
            _metaScanDone = 0;
            _analysisOverride = null;
            _analysisOverrideRight = null;
            _analysisOverrideRightIndex = -1;
            _chartSourceIndex = -1;
            _editThresholds = null;
            CancelHistoryScan();
            _fileSizes = new long[_reportFiles.Length];
            for (int i = 0; i < _reportFiles.Length; i++)
            {
                try { _fileSizes[i] = new FileInfo(_reportFiles[i]).Length; }
                catch { _fileSizes[i] = -1; }
            }
        }

        /// <summary>이력·메타 스캔을 에디터 틱당 짧은 예산으로 조금씩 진행합니다(GUI 블로킹 방지).</summary>
        private void PumpBackgroundWork()
        {
            if (_historyQueue.Count == 0 && _metaScanQueue.Count == 0)
            {
                return;
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (_historyQueue.Count > 0 && watch.ElapsedMilliseconds < 8)
            {
                StepHistoryScan();
            }
            while (_metaScanQueue.Count > 0 && watch.ElapsedMilliseconds < 8)
            {
                StepMetaScan();
            }
            Repaint();
        }

        private void CancelHistoryScan()
        {
            _historyQueue.Clear();
            _historyByStage = null;
            _historyPending = null;
            _historyRecords = null;
            _historyRun = null;
            _historyForRunId = null;
            _historyTotal = 0;
            _historyCount = 0;
        }

        private void BeginHistoryScan(ProfilingRunRecord run)
        {
            CancelHistoryScan();
            _historyRun = run;
            _historyForRunId = run.runId;
            _historyPending = new Dictionary<string, List<float>>(StringComparer.Ordinal);
            _historyRecords = new List<ProfilingRunRecord>(MaxHistoryRuns);
            List<string> candidates = ProfilingReportWriter.GetHistoryCandidatePaths(run);
            _historyTotal = candidates.Count;
            foreach (string path in candidates)
            {
                _historyQueue.Enqueue(path);
            }
            // 후보가 없으면 즉시 완료 처리 — 펌프가 돌지 않아 "스캔 중"이 남는 것을 막는다.
            if (_historyQueue.Count == 0)
            {
                _historyByStage = _historyPending;
                _historyPending = null;
                _historyRun = null;
            }
        }

        private void StepHistoryScan()
        {
            string path = _historyQueue.Dequeue();
            try
            {
                ProfilingRunRecord past = ProfilingReportWriter.Load(path);
                if (ProfilingReportWriter.IsHistoryMatch(_historyRun, past))
                {
                    _historyRecords.Add(past);
                    _historyCount = _historyRecords.Count;
                    foreach (ProfilingStageSample s in
                        ProfilingRunAnalyzer.AggregateStagesByName(past.stages))
                    {
                        if (ProfilingRunAnalyzer.IsInteractiveStage(s.stage) ||
                            ProfilingRunAnalyzer.IsOutcomeStage(s.stage))
                        {
                            continue;
                        }
                        if (!_historyPending.TryGetValue(s.stage, out List<float> list))
                        {
                            list = new List<float>();
                            _historyPending[s.stage] = list;
                        }
                        list.Add(s.deltaMs);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 이력 파일 건너뜀: {path} - {e.Message}");
            }
            if (_historyQueue.Count == 0 || _historyRecords.Count >= MaxHistoryRuns)
            {
                _historyQueue.Clear();
                _historyByStage = _historyPending;
                _historyPending = null;
                _historyRun = null;
            }
        }

        private void EnsureMetaScan()
        {
            foreach (string path in _reportFiles)
            {
                if (!_outcomeCache.ContainsKey(path) && _metaScanQueued.Add(path))
                {
                    _metaScanQueue.Enqueue(path);
                    _metaScanTotal++;
                }
            }
        }

        private void StepMetaScan()
        {
            string path = _metaScanQueue.Dequeue();
            _metaScanQueued.Remove(path);
            if (ProfilingReportWriter.TryReadMeta(path, out string outcome, out _))
            {
                _outcomeCache[path] = outcome ?? string.Empty;
                if (!string.IsNullOrEmpty(outcome))
                {
                    _knownOutcomes.Add(outcome);
                }
            }
            else
            {
                _outcomeCache[path] = string.Empty;
            }
            _metaScanDone++;
        }

        private void OnGUI()
        {
            _monoStyle ??= new GUIStyle(EditorStyles.label) { richText = false };

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RefreshReports();
            }
            if (GUILayout.Button("HTML", EditorStyles.toolbarButton, GUILayout.Width(45)))
            {
                try
                {
                    string htmlPath = ProfilingHtmlReportWriter.WriteIndexPage();
                    Debug.Log($"[Profiling] HTML 리포트 생성: {htmlPath}");
                    EditorUtility.RevealInFinder(htmlPath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Profiling] HTML 리포트 생성 실패: {e.Message}");
                }
            }
            // 선택한 런의 스테이지 타임라인을 Chrome Trace 포맷으로보냅니다(Perfetto/chrome://tracing에서 열람).
            using (new EditorGUI.DisabledScope(_leftIndex < 0 || _leftIndex >= _reportFiles.Length))
            {
                if (GUILayout.Button("Trace", EditorStyles.toolbarButton, GUILayout.Width(45)))
                {
                    try
                    {
                        string tracePath = ProfilingTraceExporter.WriteTrace(LoadAt(_leftIndex));
                        if (string.IsNullOrEmpty(tracePath))
                        {
                            Debug.LogWarning("[Profiling] Trace보내기 실패: 선택한 런을 읽을 수 없습니다.");
                        }
                        else
                        {
                            Debug.Log($"[Profiling] Trace보내기: {tracePath}");
                            EditorUtility.RevealInFinder(tracePath);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Profiling] Trace보내기 실패: {e.Message}");
                    }
                }
                if (GUILayout.Button("Perfetto", EditorStyles.toolbarButton, GUILayout.Width(55)))
                {
                    OpenInPerfetto();
                }
            }
            if (GUILayout.Button("폴더", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                EditorUtility.RevealInFinder(_reportDirectory);
            }
            if (GUILayout.Button("베이스라인", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                BatchOps.RebuildBaselines();
            }
            if (GUILayout.Button("픽스처", EditorStyles.toolbarButton, GUILayout.Width(45)))
            {
                BatchOps.RunFixtureProfile();
            }
            if (GUILayout.Button("임계값", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _showThresholds = !_showThresholds;
            }
            GUILayout.Label($"출력 경로: {_reportDirectory}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (_showThresholds)
            {
                DrawThresholdsPanel();
            }

            if (_reportFiles.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "저장된 프로파일링 런이 없습니다. FBX 변환을 실행하면 자동으로 기록됩니다.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawRunList();
            DrawDetailPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRunList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            EditorGUILayout.LabelField("런 목록 (좌클릭=기준, 우클릭=비교 대상)", EditorStyles.miniBoldLabel);
            _listFilter = EditorGUILayout.TextField(_listFilter, EditorStyles.toolbarSearchField);
            // 결과 팝업의 선택지(_knownOutcomes)는 메타 스캔 결과에서만 채워진다.
            // 필터가 켜진 뒤에야 스캔을 시작하면 선택지가 영원히 비어 필터를 켤 수 없으므로
            // 목록을 그릴 때 항상 스캔을 진행한다(이미 읽은 파일은 건너뛰어 비용은 1회분).
            EnsureMetaScan();
            EditorGUILayout.BeginHorizontal();
            var outcomeOptions = new List<string> { "결과: 전체" };
            outcomeOptions.AddRange(_knownOutcomes.OrderBy(o => o, StringComparer.Ordinal));
            int outcomeIndex = string.IsNullOrEmpty(_outcomeFilter)
                ? 0
                : outcomeOptions.IndexOf(_outcomeFilter);
            if (outcomeIndex < 0)
            {
                outcomeIndex = 0;
            }
            int pickedOutcome = EditorGUILayout.Popup(
                outcomeIndex, outcomeOptions.ToArray(), GUILayout.Width(120));
            if (pickedOutcome != outcomeIndex)
            {
                _outcomeFilter = pickedOutcome == 0 ? string.Empty : outcomeOptions[pickedOutcome];
                if (pickedOutcome > 0)
                {
                    EnsureMetaScan();
                }
            }
            _dateFilter = EditorGUILayout.Popup(
                _dateFilter, DateFilterNames, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_outcomeFilter) && _metaScanQueue.Count > 0)
            {
                EditorGUILayout.LabelField(
                    $"결과 스캔 중… {_metaScanDone}/{_metaScanTotal}",
                    EditorStyles.miniLabel);
            }
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            for (int i = 0; i < _reportFiles.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(_reportFiles[i]);
                if (!PassesFilters(i))
                {
                    continue;
                }
                string size = _fileSizes[i] >= 0 ? $" ({FormatSize(_fileSizes[i])})" : string.Empty;
                GUIStyle style = i == _leftIndex || i == _rightIndex
                    ? EditorStyles.whiteLabel
                    : EditorStyles.label;
                Rect rect = EditorGUILayout.GetControlRect();
                var evt = Event.current;
                if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
                {
                    if (evt.button == 1) _rightIndex = i;
                    else _leftIndex = i;
                    evt.Use();
                    Repaint();
                }
                EditorGUI.LabelField(rect, name + size, style);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>텍스트·결과·기간 필터를 파일명/지연 스캔 메타 기준으로 적용합니다.</summary>
        private bool PassesFilters(int i)
        {
            string path = _reportFiles[i];
            if (!string.IsNullOrEmpty(_listFilter) &&
                Path.GetFileNameWithoutExtension(path)
                    .IndexOf(_listFilter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
            if (_dateFilter > 0)
            {
                TimeSpan span = _dateFilter == 1
                    ? TimeSpan.FromDays(1)
                    : _dateFilter == 2 ? TimeSpan.FromDays(7) : TimeSpan.FromDays(30);
                if (!ProfilingReportWriter.TryGetRunTimestampUtc(path, out DateTime utc) ||
                    DateTime.UtcNow - utc > span)
                {
                    return false;
                }
            }
            if (!string.IsNullOrEmpty(_outcomeFilter))
            {
                if (!_outcomeCache.TryGetValue(path, out string outcome))
                {
                    // 스캔 진행 중에는 아직 확인되지 않은 파일을 통과시킨다(스캔 완료 후 정확히 필터).
                    return _metaScanQueue.Count > 0;
                }
                if (!string.Equals(outcome, _outcomeFilter, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private void DrawDetailPane()
        {
            ProfilingRunRecord left = LoadAt(_leftIndex);
            ProfilingRunRecord right = _rightIndex >= 0 ? LoadAt(_rightIndex) : null;

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            if (left == null)
            {
                EditorGUILayout.LabelField("런을 선택하세요.");
            }
            else
            {
                if (_detailIndex != _leftIndex)
                {
                    _detailIndex = _leftIndex;
                    _analysisOverride = null;
                    _analysisOverrideRight = null;
                    _analysisOverrideRightIndex = -1;
                    CancelHistoryScan();
                    _chartSourceIndex = -1;
                }
                DrawRunHeader(left, "기준");
                DrawFrameChart(left);
                DrawFindings(left, true);
                DrawStageTable(left);
                if (right != null)
                {
                    EditorGUILayout.Space();
                    DrawRunHeader(right, "비교");
                    DrawFindings(right, false);
                    DrawStageCompare(left, right);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawRunHeader(ProfilingRunRecord run, string role)
        {
            EditorGUILayout.LabelField(
                $"{role}: {run.label} | 결과={run.outcome} | 총 {run.totalMs:F1}ms | GC {run.totalGcAllocBytes / 1024f:F1}KB",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"{run.startedUtc} | Unity {run.unityVersion} | {run.deviceModel} | rev={run.revision}",
                EditorStyles.miniLabel);
            if (run.sampledFrameCount > 0)
            {
                string frame = $"프레임 {run.sampledFrameCount}개 | 평균 {run.sampledFrameSumMs / run.sampledFrameCount:F2}ms | 최대 {run.sampledFrameMaxMs:F2}ms";
                if (run.frameP50Ms > 0f)
                {
                    frame += $" | p50 {run.frameP50Ms:F1} | p95 {run.frameP95Ms:F1} | p99 {run.frameP99Ms:F1}ms";
                }
                EditorGUILayout.LabelField(frame, EditorStyles.miniLabel);
            }
            if (run.gcReservedBytes > 0 || run.systemUsedBytes > 0)
            {
                EditorGUILayout.LabelField(
                    $"메모리 피크(엔진 카운터): GC Reserved {run.gcReservedBytes / (1024f * 1024f):F1}MB | System Used {run.systemUsedBytes / (1024f * 1024f):F1}MB",
                    EditorStyles.miniLabel);
            }
            // 미기록(-1) 필드는 표시하지 않는다.
            var inputParts = new List<string>(3);
            if (run.inputBytes >= 0) inputParts.Add($"입력 {run.inputBytes / (1024f * 1024f):F2}MB");
            if (run.clipLengthSec >= 0f) inputParts.Add($"클립 {run.clipLengthSec:F1}초");
            if (run.boneCount >= 0) inputParts.Add($"본/트랜스폼 {run.boneCount}개");
            if (inputParts.Count > 0)
            {
                EditorGUILayout.LabelField(string.Join(" | ", inputParts), EditorStyles.miniLabel);
            }
        }

        private void DrawFindings(ProfilingRunRecord run, bool isLeft)
        {
            // 비교 대상이 바뀌면 직전 런의 재분석 결과를 쓰지 않는다(다른 런의 findings 오표시 방지).
            var override_ = isLeft
                ? _analysisOverride
                : _rightIndex == _analysisOverrideRightIndex ? _analysisOverrideRight : null;
            var findings = override_ ?? run.analysis;
            if (findings == null || findings.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                override_ != null
                    ? "자동 분석 (현재 임계값으로 재분석)"
                    : "자동 분석", EditorStyles.boldLabel);
            _thresholds ??= ProfilingAnalysisThresholds.Load();
            EditorGUILayout.LabelField(
                $"임계값: 병목>{_thresholds.bottleneckShareOfTotal:P0}&{_thresholds.bottleneckMinAbsMs:F0}ms | " +
                $"트렌드>중앙값x{_thresholds.trendRegressionFactor:F1} 초과 & +{_thresholds.trendRegressionMinDeltaMs:F0}ms(표본≥{_thresholds.trendMinHistory}) | " +
                $"프레임>평균x{_thresholds.frameSpikeFactor:F1}&{_thresholds.frameSpikeMinMs:F0}ms | GC>{_thresholds.gcSpikeMinMb:F0}MB",
                EditorStyles.miniLabel);
            foreach (ProfilingFinding finding in findings)
            {
                var type = finding.severity == ProfilingRunAnalyzer.SeverityError
                    ? MessageType.Error
                    : finding.severity == ProfilingRunAnalyzer.SeverityWarning
                        ? MessageType.Warning
                        : MessageType.Info;
                string text = finding.detail;
                if (finding.suggestedScopes != null && finding.suggestedScopes.Count > 0)
                {
                    text += "\n계측 심화 후보: " + string.Join(", ", finding.suggestedScopes);
                }
                EditorGUILayout.HelpBox(text, type);
            }

            DrawJevDiagnosis(isLeft ? _leftIndex : _rightIndex);
        }

        /// <summary>같은 라벨·머신의 과거 런 이력을 증분 스캔합니다. 완료 전까지 부분 결과를 표시합니다.</summary>
        private void EnsureHistory(ProfilingRunRecord run)
        {
            if (_historyByStage != null || _historyPending != null)
            {
                return;
            }
            BeginHistoryScan(run);
        }

        private void DrawJevDiagnosis(int index)
        {
            JevDiagnosisFile diagnosis = LoadJevDiagnosis(index);
            if (diagnosis == null || diagnosis.entries == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"JEV 진단 ({diagnosis.status}, {diagnosis.model})", EditorStyles.boldLabel);
            foreach (JevDiagnosisEntry entry in diagnosis.entries)
            {
                var line = new System.Text.StringBuilder($"{entry.stage} [{entry.kind}]");
                if (entry.cause != null)
                {
                    line.Append($" — 원인 추정: {entry.cause.label} ({entry.cause.confidence:P0})");
                }
                if (entry.deepen != null && entry.deepen.choice != "uncertain")
                {
                    line.Append($" | 우선 계측: {entry.deepen.label} ({entry.deepen.confidence:P0})");
                }
                if (!string.IsNullOrEmpty(entry.error))
                {
                    line.Append($" | 오류: {entry.error}");
                }
                EditorGUILayout.LabelField(line.ToString(), EditorStyles.miniLabel);
            }
        }

        private JevDiagnosisFile LoadJevDiagnosis(int index)
        {
            if (index < 0 || index >= _reportFiles.Length)
            {
                return null;
            }

            // diagnose-run.mjs가 생성하는 run-*.jev.json 형제 파일을 읽는다. 없으면 표시하지 않는다.
            string jevPath = _reportFiles[index][..^".json".Length] + ".jev.json";
            double now = EditorApplication.timeSinceStartup;
            if (_jevCache.TryGetValue(jevPath, out JevCacheEntry entry) &&
                now - entry.lastCheckTime < JevRecheckIntervalSec)
            {
                return entry.diagnosis;
            }

            // 파일 상태만 비교해 교체·삭제·생성을 감지한다. 동일하면 재로드하지 않는다.
            var info = new FileInfo(jevPath);
            bool exists = info.Exists;
            long writeTicks = exists ? info.LastWriteTimeUtc.Ticks : 0L;
            long length = exists ? info.Length : 0L;

            if (entry == null)
            {
                entry = new JevCacheEntry();
                _jevCache[jevPath] = entry;
            }
            else if (exists == entry.fileExists &&
                     writeTicks == entry.writeTimeTicks &&
                     length == entry.length)
            {
                entry.lastCheckTime = now;
                return entry.diagnosis;
            }

            entry.lastCheckTime = now;
            entry.fileExists = exists;
            entry.writeTimeTicks = writeTicks;
            entry.length = length;
            entry.diagnosis = null;

            if (!exists)
            {
                return null;
            }

            try
            {
                entry.diagnosis = JsonUtility.FromJson<JevDiagnosisFile>(File.ReadAllText(jevPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] JEV 진단 로드 실패: {jevPath} - {e.Message}");
            }
            return entry.diagnosis;
        }

        /// <summary>JEV 사이드카의 캐시 항목입니다. diagnosis는 파일 없음/파싱 실패 시 null입니다.</summary>
        private sealed class JevCacheEntry
        {
            public JevDiagnosisFile diagnosis;
            public bool fileExists;
            public long writeTimeTicks;
            public long length;
            public double lastCheckTime;
        }

        [Serializable]
        private class JevDiagnosisFile
        {
            public string status;
            public string model;
            public List<JevDiagnosisEntry> entries;
        }

        [Serializable]
        private class JevDiagnosisEntry
        {
            public string kind;
            public string stage;
            public string detail;
            public JevChoice cause;
            public JevChoice deepen;
            public string error;
        }

        [Serializable]
        private class JevChoice
        {
            public string choice;
            public string label;
            public float confidence;
        }

        /// <summary>스테이지 행 데이터입니다. 이름·Δms·비중·이력 중앙값 기준 정렬/강조에 씁니다.</summary>
        private struct StageRow
        {
            public ProfilingStageSample sample;
            public float share;
            public float histMedian;
            public int histN;
        }

        private void DrawStageTable(ProfilingRunRecord run)
        {
            EnsureHistory(run);
            var stages = ProfilingRunAnalyzer.AggregateStagesByName(run.stages);
            float total = Mathf.Max(run.totalMs, 0.001f);
            _thresholds ??= ProfilingAnalysisThresholds.Load();

            // 스캔 진행 중에도 부분 결과를 표시한다.
            Dictionary<string, List<float>> histSource = _historyByStage ?? _historyPending;
            var rows = new List<StageRow>(stages.Count);
            foreach (ProfilingStageSample s in stages)
            {
                var row = new StageRow { sample = s, share = s.deltaMs / total, histMedian = -1f };
                if (histSource != null &&
                    histSource.TryGetValue(s.stage, out List<float> hist))
                {
                    row.histMedian = ProfilingRunAnalyzer.Median(hist);
                    row.histN = hist.Count;
                }
                rows.Add(row);
            }

            EditorGUILayout.Space();
            float top3 = rows.OrderByDescending(r => r.share).Take(3).Sum(r => r.share);
            string histLabel = _historyPending != null
                ? $"이력 스캔 중 {_historyRecords?.Count ?? 0}/{_historyTotal}"
                : $"이력 {_historyCount}회";
            EditorGUILayout.LabelField(
                $"스테이지 ({histLabel} | 상위3 비중 {top3 * 100f:F0}% | 헤더 클릭=정렬)",
                EditorStyles.boldLabel);

            // 정렬 헤더 — 컬럼 클릭으로 기준 전환, 같은 컬럼 재클릭 시 오름/내림 반전.
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"스테이지{SortArrow(0)}", EditorStyles.miniButtonLeft, GUILayout.Width(150))) SetStageSort(0);
            if (GUILayout.Button($"Δms{SortArrow(1)}", EditorStyles.miniButtonMid, GUILayout.Width(65))) SetStageSort(1);
            if (GUILayout.Button($"비중{SortArrow(2)}", EditorStyles.miniButtonMid, GUILayout.Width(55))) SetStageSort(2);
            if (GUILayout.Button($"호출{SortArrow(3)}", EditorStyles.miniButtonMid, GUILayout.Width(45))) SetStageSort(3);
            if (GUILayout.Button($"누적ms{SortArrow(4)}", EditorStyles.miniButtonMid, GUILayout.Width(65))) SetStageSort(4);
            if (GUILayout.Button($"이력중앙{SortArrow(5)}", EditorStyles.miniButtonRight, GUILayout.Width(105))) SetStageSort(5);
            EditorGUILayout.LabelField("ΔGC(KB) / 메시지", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            Func<StageRow, object> key = _stageSort switch
            {
                1 => r => r.sample.deltaMs,
                2 => r => r.share,
                3 => r => r.sample.calls,
                4 => r => r.sample.sinceRunStartMs,
                5 => r => r.histMedian,
                _ => r => r.sample.stage,
            };
            var sorted = _stageSortAsc ? rows.OrderBy(key) : rows.OrderByDescending(key);

            _warnStyle ??= new GUIStyle(_monoStyle);
            _warnStyle.normal.textColor = new Color(1f, 0.62f, 0.25f);

            foreach (StageRow row in sorted)
            {
                ProfilingStageSample s = row.sample;
                string hist = row.histMedian >= 0f
                    ? $"{row.histMedian:F1}(n={row.histN},{(row.histMedian > 0.01f ? $"{s.deltaMs / row.histMedian:F2}x" : "-")})"
                    : "-";
                // 분석기와 동일 조건으로 병목 후보 행을 강조한다.
                bool bottleneck = row.share >= _thresholds.bottleneckShareOfTotal &&
                                  s.deltaMs >= _thresholds.bottleneckMinAbsMs;
                EditorGUILayout.LabelField(
                    $"{s.stage}  |  {s.deltaMs:F1}  |  {row.share * 100f:F0}%  |  {s.calls}  |  {s.sinceRunStartMs:F1}  |  {hist}  |  {s.gcDeltaBytes / 1024f:F1}  |  {s.message}",
                    bottleneck ? _warnStyle : _monoStyle);
            }

            if (run.metrics != null && run.metrics.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("계측 지표", EditorStyles.boldLabel);
                foreach (ProfilingMetricSample m in run.metrics)
                {
                    EditorGUILayout.LabelField(
                        $"{m.name}  |  calls={m.calls}  |  total={m.totalMs:F1}ms  |  max={m.maxMs:F1}ms  |  GC={m.totalGcAllocBytes / 1024f:F1}KB",
                        _monoStyle);
                }
            }
        }

        private string SortArrow(int column)
        {
            return _stageSort == column ? (_stageSortAsc ? "▲" : "▼") : string.Empty;
        }

        private void SetStageSort(int column)
        {
            if (_stageSort == column) _stageSortAsc = !_stageSortAsc;
            else { _stageSort = column; _stageSortAsc = true; }
        }

        private void DrawStageCompare(ProfilingRunRecord left, ProfilingRunRecord right)
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("기준↔비교 교체", GUILayout.Width(110)))
            {
                (_leftIndex, _rightIndex) = (_rightIndex, _leftIndex);
                _detailIndex = -1;
                GUIUtility.ExitGUI();
            }

            // 입력 컨텍스트가 다르면 비교 자체가 무의미할 수 있어 먼저 표시한다.
            // 미기록(-1) 측은 "-0.00MB"처럼 음수로 나오지 않게 "미기록"으로 표기한다.
            static string FmtMb(long v) => v >= 0 ? $"{v / (1024f * 1024f):F2}MB" : "미기록";
            static string FmtSec(float v) => v >= 0f ? $"{v:F1}초" : "미기록";
            static string FmtCount(int v) => v >= 0 ? $"{v}" : "미기록";
            var ctx = new List<string>(3);
            if (left.inputBytes >= 0 || right.inputBytes >= 0)
            {
                ctx.Add($"입력 {FmtMb(left.inputBytes)} vs {FmtMb(right.inputBytes)}");
            }
            if (left.clipLengthSec >= 0f || right.clipLengthSec >= 0f)
            {
                ctx.Add($"클립 {FmtSec(left.clipLengthSec)} vs {FmtSec(right.clipLengthSec)}");
            }
            if (left.boneCount >= 0 || right.boneCount >= 0)
            {
                ctx.Add($"본 {FmtCount(left.boneCount)} vs {FmtCount(right.boneCount)}");
            }
            if (ctx.Count > 0)
            {
                EditorGUILayout.LabelField("입력 컨텍스트 (기준 vs 비교): " + string.Join(" | ", ctx), EditorStyles.miniLabel);
            }

            EditorGUILayout.LabelField("스테이지 비교 (기준 / 비교 / 차이Δms / 차이%)", EditorStyles.boldLabel);
            var rightByStage = new Dictionary<string, ProfilingStageSample>();
            foreach (ProfilingStageSample s in ProfilingRunAnalyzer.AggregateStagesByName(right.stages))
            {
                rightByStage[s.stage] = s;
            }

            _warnStyle ??= new GUIStyle(_monoStyle);
            var improveStyle = new GUIStyle(_monoStyle);
            improveStyle.normal.textColor = new Color(0.45f, 0.85f, 0.45f);
            _warnStyle.normal.textColor = new Color(1f, 0.62f, 0.25f);

            var leftStages = ProfilingRunAnalyzer.AggregateStagesByName(left.stages);
            var handled = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProfilingStageSample l in leftStages)
            {
                handled.Add(l.stage);
                if (!rightByStage.TryGetValue(l.stage, out ProfilingStageSample r))
                {
                    EditorGUILayout.LabelField($"{l.stage}  |  {l.deltaMs:F1}  |  -  |  비교 런에 없음", _monoStyle);
                    continue;
                }

                float diff = r.deltaMs - l.deltaMs;
                string pct = Mathf.Abs(l.deltaMs) > 0.01f
                    ? $"{diff / l.deltaMs * 100f:+0;-0;0}%"
                    : "-";
                // 회귀는 주황, 개선은 녹색 — 기준 런 대비 비교 런이 느려진 방향이 회귀다.
                GUIStyle style = diff > 0f ? _warnStyle : diff < 0f ? improveStyle : _monoStyle;
                EditorGUILayout.LabelField(
                    $"{l.stage}  |  {l.deltaMs:F1}  |  {r.deltaMs:F1}  |  {diff:+0.0;-0.0}ms  |  {pct}",
                    style);
            }
            // 비교 런에만 있는 스테이지도 빠뜨리지 않는다.
            foreach (ProfilingStageSample s in ProfilingRunAnalyzer.AggregateStagesByName(right.stages))
            {
                if (!handled.Contains(s.stage))
                {
                    EditorGUILayout.LabelField($"{s.stage}  |  -  |  {s.deltaMs:F1}  |  기준 런에 없음", _monoStyle);
                }
            }

            DrawFindingsDiff(left, right, _warnStyle, improveStyle);
        }

        /// <summary>기준/비교 런의 findings(kind+stage 기준) 신규·해소 목록을 비교 표시합니다.</summary>
        private void DrawFindingsDiff(
            ProfilingRunRecord left, ProfilingRunRecord right,
            GUIStyle regressionStyle, GUIStyle improveStyle)
        {
            var leftFindings = _analysisOverride ?? left.analysis;
            var rightFindings =
                (_rightIndex == _analysisOverrideRightIndex ? _analysisOverrideRight : null)
                ?? right.analysis;
            if ((leftFindings == null || leftFindings.Count == 0) &&
                (rightFindings == null || rightFindings.Count == 0))
            {
                return;
            }
            leftFindings ??= new List<ProfilingFinding>();
            rightFindings ??= new List<ProfilingFinding>();

            var leftKeys = new HashSet<string>(leftFindings.Select(FindingKey), StringComparer.Ordinal);
            var rightKeys = new HashSet<string>(rightFindings.Select(FindingKey), StringComparer.Ordinal);
            var onlyRight = rightFindings.Where(f => !leftKeys.Contains(FindingKey(f))).ToList();
            var onlyLeft = leftFindings.Where(f => !rightKeys.Contains(FindingKey(f))).ToList();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"Findings 비교 (기준 {leftFindings.Count}건 / 비교 {rightFindings.Count}건)",
                EditorStyles.boldLabel);
            if (onlyRight.Count == 0 && onlyLeft.Count == 0)
            {
                EditorGUILayout.LabelField("두 런의 findings(kind·stage 기준)가 동일합니다.", _monoStyle);
                return;
            }
            foreach (ProfilingFinding f in onlyRight)
            {
                EditorGUILayout.LabelField(
                    $"신규(비교에만) | {f.severity} {f.kind} {f.stage} — {f.detail}",
                    regressionStyle);
            }
            foreach (ProfilingFinding f in onlyLeft)
            {
                EditorGUILayout.LabelField(
                    $"해소(기준에만) | {f.severity} {f.kind} {f.stage} — {f.detail}",
                    improveStyle);
            }
        }

        private static string FindingKey(ProfilingFinding f)
        {
            return $"{f.kind}|{f.stage ?? string.Empty}";
        }

        /// <summary>프레임 타임라인 차트와 드래그 범위 집계를 표시합니다. frameSamplesMs가 없는 구 리포트는 생략합니다.</summary>
        private void DrawFrameChart(ProfilingRunRecord run)
        {
            float[] samples = run.frameSamplesMs;
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            if (_chartSourceIndex != _leftIndex)
            {
                _chartSourceIndex = _leftIndex;
                _selStart = 0f;
                _selEnd = 1f;
                if (_chartTexture != null)
                {
                    DestroyImmediate(_chartTexture);
                    _chartTexture = null;
                }
            }
            // Unity가 미사용 에셋 언로드로 텍스처 네이티브만 파괴한 경우(참조는 남는 fake-null)에도
            // 재빌드되도록 ??= 대신 UnityObject == null 비교를 사용한다.
            if (_chartTexture == null)
            {
                _chartTexture = BuildChartTexture(run);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("프레임 타임라인 (드래그=범위 선택)", EditorStyles.miniBoldLabel);
            Rect rect = GUILayoutUtility.GetRect(200f, 64f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, _chartTexture, ScaleMode.StretchToFill, false);
            if (Mathf.Abs(_selEnd - _selStart) > 0.001f && (_selStart > 0f || _selEnd < 1f))
            {
                float lo = Mathf.Min(_selStart, _selEnd), hi = Mathf.Max(_selStart, _selEnd);
                EditorGUI.DrawRect(
                    new Rect(rect.x + rect.width * lo, rect.y, rect.width * (hi - lo), rect.height),
                    new Color(1f, 1f, 1f, 0.22f));
            }

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                _selecting = true;
                _selStart = _selEnd = Mathf.Clamp01((evt.mousePosition.x - rect.x) / rect.width);
                evt.Use();
            }
            else if (_selecting && evt.type == EventType.MouseDrag)
            {
                _selEnd = Mathf.Clamp01((evt.mousePosition.x - rect.x) / rect.width);
                evt.Use();
                Repaint();
            }
            else if (_selecting && evt.type == EventType.MouseUp)
            {
                _selecting = false;
                evt.Use();
            }

            int start = Mathf.FloorToInt(Mathf.Min(_selStart, _selEnd) * samples.Length);
            int end = Mathf.CeilToInt(Mathf.Max(_selStart, _selEnd) * samples.Length);
            var stats = ProfilingRunAnalyzer.FrameStats(samples, start, end);
            EditorGUILayout.LabelField(
                stats.count == 0
                    ? "선택 구간이 비어 있습니다. 차트를 드래그해 범위를 지정하세요."
                    : $"구간 {start}~{end} 프레임 | n={stats.count} | min {stats.minMs:F1} | max {stats.maxMs:F1} | mean {stats.meanMs:F2} | p95 {stats.p95Ms:F1}ms (노란선=스파이크 기준 {_thresholds.frameSpikeMinMs:F0}ms)",
                EditorStyles.miniLabel);
        }

        /// <summary>프레임 샘플을 픽셀 막대 텍스처로 굽습니다. 스파이크는 주황, 스파이크 기준선은 노랑입니다.</summary>
        private Texture2D BuildChartTexture(ProfilingRunRecord run)
        {
            float[] samples = run.frameSamplesMs;
            _thresholds ??= ProfilingAnalysisThresholds.Load();
            int width = Mathf.Min(samples.Length, 1024);
            const int height = 64;
            float spikeLine = _thresholds.frameSpikeMinMs;
            float max = Mathf.Max(samples.Max(), spikeLine) * 1.05f;
            float mean = samples.Average();
            float spikeCut = Mathf.Max(mean * _thresholds.frameSpikeFactor, spikeLine);

            var bg = new Color(0.13f, 0.13f, 0.13f, 1f);
            var normal = new Color(0.35f, 0.65f, 0.9f, 1f);
            var spike = new Color(0.95f, 0.45f, 0.25f, 1f);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            // 버킷당 최대값으로 그려 줌아웃 상태에서도 스파이크가 보존된다.
            for (int x = 0; x < width; x++)
            {
                int i0 = (int)((long)x * samples.Length / width);
                int i1 = (int)((long)(x + 1) * samples.Length / width);
                float bucketMax = 0f;
                for (int i = i0; i < i1; i++) bucketMax = Mathf.Max(bucketMax, samples[i]);
                int h = Mathf.Clamp(Mathf.RoundToInt(bucketMax / max * (height - 2)), 1, height - 2);
                Color c = bucketMax > spikeCut ? spike : normal;
                for (int y = 0; y < h; y++)
                {
                    pixels[(height - 1 - y) * width + x] = c;
                }
            }
            int lineY = Mathf.Clamp(Mathf.RoundToInt(spikeLine / max * (height - 2)), 0, height - 2);
            for (int x = 0; x < width; x++)
            {
                pixels[(height - 1 - lineY) * width + x] = new Color(1f, 0.9f, 0.3f, 1f);
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>분석 임계값 편집 패널입니다. 저장 시 클램프 검증 후 선택 런을 즉시 재분석합니다.</summary>
        private void DrawThresholdsPanel()
        {
            _editThresholds ??= CloneThresholds(_thresholds);
            var t = _editThresholds;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("분석 임계값 편집 (저장 시 선택 런 즉시 재분석)", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("병목비중", GUILayout.Width(50));
            t.bottleneckShareOfTotal = EditorGUILayout.FloatField(t.bottleneckShareOfTotal, GUILayout.Width(45));
            EditorGUILayout.LabelField("병목최소ms", GUILayout.Width(58));
            t.bottleneckMinAbsMs = EditorGUILayout.FloatField(t.bottleneckMinAbsMs, GUILayout.Width(55));
            EditorGUILayout.LabelField("트렌드x", GUILayout.Width(45));
            t.trendRegressionFactor = EditorGUILayout.FloatField(t.trendRegressionFactor, GUILayout.Width(40));
            EditorGUILayout.LabelField("트렌드+ms", GUILayout.Width(55));
            t.trendRegressionMinDeltaMs = EditorGUILayout.FloatField(t.trendRegressionMinDeltaMs, GUILayout.Width(50));
            EditorGUILayout.LabelField("표본≥", GUILayout.Width(35));
            t.trendMinHistory = EditorGUILayout.IntField(t.trendMinHistory, GUILayout.Width(30));
            EditorGUILayout.LabelField("스파이크x", GUILayout.Width(50));
            t.frameSpikeFactor = EditorGUILayout.FloatField(t.frameSpikeFactor, GUILayout.Width(40));
            EditorGUILayout.LabelField("스파이크ms", GUILayout.Width(55));
            t.frameSpikeMinMs = EditorGUILayout.FloatField(t.frameSpikeMinMs, GUILayout.Width(45));
            EditorGUILayout.LabelField("GC mb", GUILayout.Width(38));
            t.gcSpikeMinMb = EditorGUILayout.FloatField(t.gcSpikeMinMb, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("저장", GUILayout.Width(50)))
            {
                // 경계 검증: NaN/Infinity는 기본값으로, 음수·0은 유효 하한으로 보정한다.
                t.Sanitize();
                ProfilingAnalysisThresholds.Save(t);
                _thresholds = ProfilingAnalysisThresholds.Load();
                _editThresholds = CloneThresholds(_thresholds);
                ReanalyzeSelectedRun();
                // 스파이크 기준선·색상이 구 임계값으로 굽혀 있으므로 차트를 재생성한다.
                if (_chartTexture != null)
                {
                    DestroyImmediate(_chartTexture);
                    _chartTexture = null;
                }
                _chartSourceIndex = -1;
                Debug.Log("[Profiling] 분석 임계값 저장 및 선택 런 재분석 완료");
            }
            if (GUILayout.Button("기본값", GUILayout.Width(60)))
            {
                _editThresholds = new ProfilingAnalysisThresholds();
            }
            EditorGUILayout.LabelField(ProfilingAnalysisThresholds.ConfigPath, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        /// <summary>저장된 임계값으로 기준/비교 런을 다시 분석해 findings를 교체합니다.</summary>
        private void ReanalyzeSelectedRun()
        {
            _analysisOverride = ReanalyzeWithHistory(LoadAt(_leftIndex));
            _analysisOverrideRight = _rightIndex >= 0
                ? ReanalyzeWithHistory(LoadAt(_rightIndex))
                : null;
            _analysisOverrideRightIndex =
                _analysisOverrideRight != null ? _rightIndex : -1;
        }

        private List<ProfilingFinding> ReanalyzeWithHistory(ProfilingRunRecord run)
        {
            if (run == null)
            {
                return null;
            }
            try
            {
                // 이력 증분 스캔 결과가 같은 런 것이면 재사용해 디스크 재스캔을 피한다.
                // 단 스캔 진행 중(_historyQueue 잔여)에는 부분 이력으로 분석된 findings가
                // 고정될 수 있으므로 완료된 경우에만 재사용한다.
                List<ProfilingRunRecord> history =
                    _historyRecords != null && _historyQueue.Count == 0 &&
                    string.Equals(_historyForRunId, run.runId, StringComparison.Ordinal)
                        ? _historyRecords
                        : ProfilingReportWriter.LoadMatching(run);
                return ProfilingRunAnalyzer.Analyze(run, history, _thresholds);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 재분석 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>선택 런의 트레이스를 내보내고 Perfetto UI를 엽니다.</summary>
        private void OpenInPerfetto()
        {
            try
            {
                string tracePath = ProfilingTraceExporter.WriteTrace(LoadAt(_leftIndex));
                if (string.IsNullOrEmpty(tracePath))
                {
                    Debug.LogWarning("[Profiling] Trace 내보내기 실패: 선택한 런을 읽을 수 없습니다.");
                    return;
                }
                EditorUtility.RevealInFinder(tracePath);
                Application.OpenURL("https://ui.perfetto.dev/");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] Perfetto 열기 실패: {e.Message}");
            }
        }

        private static ProfilingAnalysisThresholds CloneThresholds(ProfilingAnalysisThresholds t)
        {
            return JsonUtility.FromJson<ProfilingAnalysisThresholds>(JsonUtility.ToJson(t));
        }

        private static string FormatSize(long bytes)
        {
            return bytes >= 1024 * 1024
                ? $"{bytes / (1024f * 1024f):F1}MB"
                : $"{bytes / 1024f:F0}KB";
        }

        /// <summary>리포트를 경로별로 캐시합니다. OnGUI 이벤트마다 디스크를 읽지 않기 위함입니다.</summary>
        private ProfilingRunRecord LoadAt(int index)
        {
            if (index < 0 || index >= _reportFiles.Length)
            {
                return null;
            }

            string path = _reportFiles[index];
            if (_recordCache.TryGetValue(path, out ProfilingRunRecord cached))
            {
                return cached;
            }

            try
            {
                _recordCache[path] = ProfilingReportWriter.Load(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 리포트 로드 실패: {path} - {e.Message}");
                _recordCache[path] = null;
            }
            return _recordCache[path];
        }
    }
}
