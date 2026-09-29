using System;
using System.Collections.Generic;
using System.IO;
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
        }

        private void RefreshReports()
        {
            _reportDirectory = ProfilingReportWriter.OutputDirectory;
            _reportFiles = ProfilingReportWriter.ListReports();
            if (_leftIndex >= _reportFiles.Length) _leftIndex = 0;
            if (_rightIndex >= _reportFiles.Length) _rightIndex = -1;
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
            }
            GUILayout.Label($"출력 경로: {_reportDirectory}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

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
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            for (int i = 0; i < _reportFiles.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(_reportFiles[i]);
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
                EditorGUI.LabelField(rect, name, style);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
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
                DrawRunHeader(left, "기준");
                DrawFindings(left);
                if (right != null)
                {
                    DrawRunHeader(right, "비교");
                    DrawStageCompare(left, right);
                }
                else
                {
                    DrawStageTable(left);
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
                EditorGUILayout.LabelField(
                    $"프레임 {run.sampledFrameCount}개 | 평균 {run.sampledFrameSumMs / run.sampledFrameCount:F2}ms | 최대 {run.sampledFrameMaxMs:F2}ms",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawFindings(ProfilingRunRecord run)
        {
            if (run.analysis == null || run.analysis.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("자동 분석", EditorStyles.boldLabel);
            foreach (ProfilingFinding finding in run.analysis)
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

            DrawJevDiagnosis();
        }

        private void DrawJevDiagnosis()
        {
            JevDiagnosisFile diagnosis = LoadJevDiagnosis(_leftIndex);
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
            if (!File.Exists(jevPath))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<JevDiagnosisFile>(File.ReadAllText(jevPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] JEV 진단 로드 실패: {jevPath} - {e.Message}");
                return null;
            }
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

        private void DrawStageTable(ProfilingRunRecord run)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("스테이지", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("스테이지 / Δms / 누적ms / ΔGC(KB) / 메시지", EditorStyles.miniLabel);
            // 구 버전 리포트의 프레임 단위 샘플도 스테이지명별로 합산해 표시한다.
            foreach (ProfilingStageSample s in ProfilingRunAnalyzer.AggregateStagesByName(run.stages))
            {
                EditorGUILayout.LabelField(
                    $"{s.stage}  |  {s.deltaMs:F1}  |  {s.sinceRunStartMs:F1}  |  {s.gcDeltaBytes / 1024f:F1}  |  {s.message}",
                    _monoStyle);
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

        private void DrawStageCompare(ProfilingRunRecord left, ProfilingRunRecord right)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("스테이지 비교 (기준 Δms / 비교 Δms / 차이)", EditorStyles.boldLabel);
            var rightByStage = new Dictionary<string, ProfilingStageSample>();
            foreach (ProfilingStageSample s in ProfilingRunAnalyzer.AggregateStagesByName(right.stages))
            {
                rightByStage[s.stage] = s;
            }

            foreach (ProfilingStageSample l in ProfilingRunAnalyzer.AggregateStagesByName(left.stages))
            {
                if (!rightByStage.TryGetValue(l.stage, out ProfilingStageSample r))
                {
                    EditorGUILayout.LabelField($"{l.stage}  |  {l.deltaMs:F1}  |  -  |  비교 런에 없음", _monoStyle);
                    continue;
                }

                float diff = r.deltaMs - l.deltaMs;
                string sign = diff >= 0f ? "+" : "";
                EditorGUILayout.LabelField(
                    $"{l.stage}  |  {l.deltaMs:F1}  |  {r.deltaMs:F1}  |  {sign}{diff:F1}ms",
                    _monoStyle);
            }
        }

        private ProfilingRunRecord LoadAt(int index)
        {
            if (index < 0 || index >= _reportFiles.Length)
            {
                return null;
            }

            try
            {
                return ProfilingReportWriter.Load(_reportFiles[index]);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiling] 리포트 로드 실패: {_reportFiles[index]} - {e.Message}");
                return null;
            }
        }
    }
}
