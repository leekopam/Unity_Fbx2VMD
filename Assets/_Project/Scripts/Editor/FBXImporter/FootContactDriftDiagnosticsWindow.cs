using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 소스·직접·보정 3-리그를 프레임별로 탐색해 하체 접촉 드리프트를 측정하는
    /// 진단 창임. HumanoidLowerBodyPlaybackQualityTests와 동일한 메트릭을
    /// EditorWindow로 재현해 어느 런에서 드리프트가 생기는지 보여줌.
    /// 측정 로직은 FbxDiagnostics와 공유하고 창은 표시만 담당함.
    /// 측정만 수행하고 보정 로직은 수정하지 않음.
    /// </summary>
    public class FootContactDriftDiagnosticsWindow : EditorWindow
    {
        private string _clipPath = FbxDiagnostics.DefaultClipPath;
        private string _targetPath = FbxDiagnostics.DefaultTargetPath;
        private int _firstFrame;
        private int _lastFrame = -1;
        private Vector2 _scroll;
        private readonly List<string> _reportLines = new List<string>();
        private string _message = "";
        private bool _running;

        [MenuItem("Tools/FBXImporter/하체 접촉 드리프트 진단")]
        private static void Open() =>
            GetWindow<FootContactDriftDiagnosticsWindow>(false, "접촉 드리프트 진단");

        private void OnGUI()
        {
            _clipPath = EditorGUILayout.TextField("클립 FBX 경로", _clipPath);
            _targetPath = EditorGUILayout.TextField("대상 모델 경로", _targetPath);
            EditorGUILayout.BeginHorizontal();
            _firstFrame = EditorGUILayout.IntField("시작 프레임", _firstFrame);
            _lastFrame = EditorGUILayout.IntField("끝 프레임(-1=끝까지)", _lastFrame);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "측정은 HumanoidLowerBodyPlaybackQualityTests와 같은 3-리그·같은 지표로 " +
                "진행합니다. 전체 클립은 수 분 걸릴 수 있어 범위 지정을 권장합니다.",
                MessageType.None);
            using (new EditorGUI.DisabledScope(_running))
            {
                if (GUILayout.Button(_running ? "측정 중…" : "측정 시작"))
                {
                    Run();
                }
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (string line in _reportLines)
            {
                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, MessageType.Info);
            }
        }

        private void Run()
        {
            _reportLines.Clear();
            _running = true;
            try
            {
                FbxDiagnostics.DriftReport report =
                    FbxDiagnostics.MeasureFootContactDrift(
                        _clipPath, _targetPath, _firstFrame, _lastFrame, null,
                        (i, count, frame) =>
                            EditorUtility.DisplayCancelableProgressBar(
                                "드리프트 측정", $"프레임 {frame}",
                                (float)i / count));
                if (!report.Completed)
                {
                    _message = "측정이 취소되었습니다.";
                    return;
                }

                foreach (FbxDiagnostics.RunResult run in report.Runs)
                {
                    _reportLines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} {1}-{2}: src={3:F4} dir={4:F4} corr={5:F4} 추가={6:F4}m{7}",
                        run.IsLeft ? "왼발" : "오른발", run.Start, run.End,
                        run.SourceDrift, run.DirectDrift, run.CorrectedDrift,
                        run.Additional,
                        run.GateExceeded ? " ← 게이트 초과" : ""));
                }
                _reportLines.Add(report.GatePassed
                    ? $"게이트 통과: 최대 추가 드리프트 {report.WorstAdditional:F4}m ≤ {FbxDiagnostics.GateMeters}m"
                    : $"게이트 초과: 최대 추가 드리프트 {report.WorstAdditional:F4}m > {FbxDiagnostics.GateMeters}m");
                _message = report.Runs.Count == 0
                    ? "측정된 접촉 런이 없습니다."
                    : $"드리프트 {report.Runs.Count}런 저장: {report.CsvPath}";
            }
            catch (Exception error)
            {
                _message = $"측정 실패: {error.Message}";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _running = false;
            }
        }
    }
}
