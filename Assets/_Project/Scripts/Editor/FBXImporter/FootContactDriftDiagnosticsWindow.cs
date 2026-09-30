using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 소스·직접·보정 3-리그를 프레임별로 탐색해 하체 접촉 드리프트를 측정하는
    /// 진단 창임. HumanoidLowerBodyPlaybackQualityTests와 동일한 메트릭을
    /// EditorWindow로 재현해 어느 런에서 드리프트가 생기는지 보여줌.
    /// 측정만 수행하고 보정 로직은 수정하지 않음.
    /// </summary>
    public class FootContactDriftDiagnosticsWindow : EditorWindow
    {
        private const float ContactHeightMarginMeters = 0.03f;
        private const float ContactSpeedLimitMetersPerSecond = 0.15f;
        private const int MinimumContactFrameCount = 6;
        private const float GateMeters = 0.01f;
        private const string DefaultClipPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string DefaultTargetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";

        private string _clipPath = DefaultClipPath;
        private string _targetPath = DefaultTargetPath;
        private int _firstFrame;
        private int _lastFrame = -1;
        private Vector2 _scroll;
        private readonly List<string> _reportLines = new List<string>();
        private string _message = "";
        private bool _running;

        private sealed class RunResult
        {
            internal bool IsLeft;
            internal int Start, End;
            internal float SourceDrift, DirectDrift, CorrectedDrift;
        }

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

        private delegate bool SeekDelegate(float timeSeconds);

        private void Run()
        {
            _reportLines.Clear();
            _running = true;
            var runs = new List<RunResult>();
            GameObject sourceTarget = null, directTarget = null, correctedTarget = null;
            object sourceController = null, directController = null, correctedController = null;
            try
            {
                AnimationClip clip = LoadHumanoidClip(_clipPath);
                GameObject sourceAsset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(_clipPath);
                if (sourceAsset == null)
                    throw new InvalidOperationException($"클립 FBX를 찾지 못했습니다: {_clipPath}");

                sourceTarget = InstantiateAsset(_clipPath, "Drift Source");
                directTarget = InstantiateAsset(_targetPath, "Drift Direct");
                correctedTarget = InstantiateAsset(_targetPath, "Drift Corrected");
                Animator sourceAnimator = RequireHumanoid(sourceTarget);
                Animator directAnimator = RequireHumanoid(directTarget);
                Animator correctedAnimator = RequireHumanoid(correctedTarget);
                sourceController = CreateController();
                directController = CreateController();
                correctedController = CreateController();
                Invoke(sourceController, "Prepare", sourceAnimator, clip);
                Invoke(directController, "Prepare", directAnimator, clip);
                Invoke(correctedController, "PrepareWithArmDirectionReference",
                    correctedAnimator, clip, sourceAsset);
                SeekDelegate sourceSeek = CreateSeekDelegate(sourceController);
                SeekDelegate directSeek = CreateSeekDelegate(directController);
                SeekDelegate correctedSeek = CreateSeekDelegate(correctedController);

                float frameRate = clip.frameRate > 0f ? clip.frameRate : 60f;
                int clipLast = Mathf.CeilToInt(clip.length * frameRate);
                int last = _lastFrame >= 0 ? Mathf.Min(_lastFrame, clipLast) : clipLast;
                if (_firstFrame < 0 || last <= _firstFrame)
                    throw new InvalidOperationException("프레임 범위가 비어 있습니다.");

                Transform[] src = FootBones(sourceAnimator);
                Transform[] dir = FootBones(directAnimator);
                Transform[] cor = FootBones(correctedAnimator);
                int count = last - _firstFrame + 1;
                var sourceL = new Vector3[count]; var sourceR = new Vector3[count];
                var directL = new Vector3[count]; var directR = new Vector3[count];
                var correctL = new Vector3[count]; var correctR = new Vector3[count];

                for (int frame = _firstFrame; frame <= last; frame++)
                {
                    float time = Mathf.Min(frame / frameRate, clip.length);
                    if (!sourceSeek(time) || !directSeek(time) || !correctedSeek(time))
                        throw new InvalidOperationException($"{frame}프레임 탐색 실패");
                    int i = frame - _firstFrame;
                    sourceL[i] = ContactPoint(src, true);
                    sourceR[i] = ContactPoint(src, false);
                    directL[i] = ContactPoint(dir, true);
                    directR[i] = ContactPoint(dir, false);
                    correctL[i] = ContactPoint(cor, true);
                    correctR[i] = ContactPoint(cor, false);
                    if (i % 120 == 0 &&
                        EditorUtility.DisplayCancelableProgressBar("드리프트 측정",
                            $"프레임 {frame}", (float)i / count))
                    {
                        _message = "측정이 취소되었습니다.";
                        return;
                    }
                }

                ReportSide(runs, true, sourceL, directL, correctL, frameRate);
                ReportSide(runs, false, sourceR, directR, correctR, frameRate);
                float worst = runs.Count == 0 ? 0f :
                    runs.Max(r => r.CorrectedDrift - r.SourceDrift);
                _reportLines.Add(worst <= GateMeters
                    ? $"게이트 통과: 최대 추가 드리프트 {worst:F4}m ≤ {GateMeters}m"
                    : $"게이트 초과: 최대 추가 드리프트 {worst:F4}m > {GateMeters}m");
                WriteCsv(runs);
            }
            catch (Exception error)
            {
                _message = $"측정 실패: {error.Message}";
            }
            finally
            {
                DisposeController(sourceController);
                DisposeController(directController);
                DisposeController(correctedController);
                if (sourceTarget != null) DestroyImmediate(sourceTarget);
                if (directTarget != null) DestroyImmediate(directTarget);
                if (correctedTarget != null) DestroyImmediate(correctedTarget);
                EditorUtility.ClearProgressBar();
                _running = false;
            }
        }

        private static Transform[] FootBones(Animator animator)
        {
            return new[]
            {
                RequireBone(animator, HumanBodyBones.LeftFoot),
                RequireBone(animator, HumanBodyBones.LeftToes),
                RequireBone(animator, HumanBodyBones.RightFoot),
                RequireBone(animator, HumanBodyBones.RightToes),
            };
        }

        private static Vector3 ContactPoint(Transform[] bones, bool isLeft) =>
            (bones[isLeft ? 0 : 2].position + bones[isLeft ? 1 : 3].position) * 0.5f;

        private void ReportSide(List<RunResult> runs, bool isLeft,
            Vector3[] source, Vector3[] direct, Vector3[] corrected, float frameRate)
        {
            string side = isLeft ? "왼발" : "오른발";
            bool[] contact = DetectStableContactFrames(source, frameRate);
            int start = -1;
            for (int i = 0; i <= contact.Length; i++)
            {
                bool isContact = i < contact.Length && contact[i];
                if (isContact && start < 0) { start = i; continue; }
                if (isContact || start < 0) continue;
                int end = i - 1;
                if (end - start + 1 >= MinimumContactFrameCount)
                {
                    var run = new RunResult
                    {
                        IsLeft = isLeft,
                        Start = start + _firstFrame,
                        End = end + _firstFrame,
                        SourceDrift = HorizontalDistance(source[start], source[end]),
                        DirectDrift = HorizontalDistance(direct[start], direct[end]),
                        CorrectedDrift = HorizontalDistance(corrected[start], corrected[end]),
                    };
                    runs.Add(run);
                    float add = run.CorrectedDrift - run.SourceDrift;
                    _reportLines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} {1}-{2}: src={3:F4} dir={4:F4} corr={5:F4} 추가={6:F4}m{7}",
                        side, run.Start, run.End,
                        run.SourceDrift, run.DirectDrift, run.CorrectedDrift,
                        add, add > GateMeters ? " ← 게이트 초과" : ""));
                }
                start = -1;
            }
        }

        private void WriteCsv(List<RunResult> runs)
        {
            if (runs.Count == 0)
            {
                _message = "측정된 접촉 런이 없습니다.";
                return;
            }
            string directory = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                "Docs", "Workflow", "Local", "drift-diagnostics");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,
                $"{Path.GetFileNameWithoutExtension(_clipPath)}-drift-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(
                    "side,start,end,source_drift_m,direct_drift_m,corrected_drift_m," +
                    "additional_m,gate_exceeded");
                foreach (RunResult run in runs)
                {
                    float add = run.CorrectedDrift - run.SourceDrift;
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6},{7}",
                        run.IsLeft ? "left" : "right", run.Start, run.End,
                        run.SourceDrift, run.DirectDrift, run.CorrectedDrift,
                        add, add > GateMeters ? 1 : 0));
                }
            }
            _message = $"드리프트 {runs.Count}런 저장: {path}";
        }

        // ↓ 품질 테스트의 FootContactMetrics와 동일한 지표 — 변경 시 테스트와 함께 유지함.
        private static bool[] DetectStableContactFrames(Vector3[] points, float frameRate)
        {
            float[] sortedHeights = points.Select(p => p.y).OrderBy(v => v).ToArray();
            int percentileIndex = Mathf.Clamp(
                Mathf.CeilToInt(sortedHeights.Length * 0.02f) - 1,
                0, sortedHeights.Length - 1);
            float contactHeight = sortedHeights[percentileIndex] + ContactHeightMarginMeters;
            var candidates = new bool[points.Length];
            var stable = new bool[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                float speed = i == 0 ? 0f :
                    HorizontalDistance(points[i - 1], points[i]) * frameRate;
                candidates[i] = points[i].y <= contactHeight &&
                    speed <= ContactSpeedLimitMetersPerSecond;
                stable[i] = candidates[i] &&
                    CenteredSpeed(points, i, frameRate) <= ContactSpeedLimitMetersPerSecond;
            }
            var result = new bool[points.Length];
            int candidateStart = -1;
            for (int i = 0; i <= candidates.Length; i++)
            {
                bool isCandidate = i < candidates.Length && candidates[i];
                if (isCandidate && candidateStart < 0) { candidateStart = i; continue; }
                if (isCandidate || candidateStart < 0) continue;
                int firstStable = -1, lastStable = -1;
                for (int j = candidateStart; j <= i - 1; j++)
                {
                    if (!stable[j]) continue;
                    if (firstStable < 0) firstStable = j;
                    lastStable = j;
                }
                if (firstStable >= 0)
                {
                    int contactStart = Mathf.Max(candidateStart, firstStable - 1);
                    int contactEnd = Mathf.Min(i - 1, lastStable + 1);
                    for (int j = contactStart; j <= contactEnd; j++) result[j] = true;
                }
                candidateStart = -1;
            }
            return result;
        }

        private static float CenteredSpeed(Vector3[] points, int index, float frameRate)
        {
            if (points.Length <= 1) return 0f;
            if (index == 0)
                return Vector3.Distance(points[0], points[1]) * frameRate;
            if (index == points.Length - 1)
                return Vector3.Distance(points[index - 1], points[index]) * frameRate;
            return Vector3.Distance(points[index - 1], points[index + 1]) *
                frameRate * 0.5f;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static GameObject InstantiateAsset(string path, string instanceName)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                throw new InvalidOperationException($"asset을 찾을 수 없습니다: {path}");
            GameObject instance = UnityEngine.Object.Instantiate(asset);
            instance.name = instanceName;
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.SetActive(true);
            return instance;
        }

        private static Animator RequireHumanoid(GameObject root)
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException(
                    $"{root.name}에 유효한 Humanoid Avatar가 필요합니다.");
            return animator;
        }

        private static Transform RequireBone(Animator animator, HumanBodyBones bone)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null)
                throw new InvalidOperationException($"{bone} 본이 없습니다.");
            return t;
        }

        private static AnimationClip LoadHumanoidClip(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c =>
                    !c.name.StartsWith("__", StringComparison.Ordinal) && c.humanMotion);
            if (clip == null)
                throw new InvalidOperationException($"Humanoid 클립이 없습니다: {path}");
            return clip;
        }

        // 재생 컨트롤러는 본체 어셈블리의 internal — 리플렉션으로만 구동함.
        private static object CreateController()
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController");
            if (type == null)
                throw new InvalidOperationException(
                    "HumanoidMotionPlaybackController를 찾지 못했습니다.");
            return Activator.CreateInstance(type, nonPublic: true);
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null)
                throw new InvalidOperationException($"{method} 메서드가 없습니다.");
            return info.Invoke(target, args);
        }

        private static SeekDelegate CreateSeekDelegate(object controller)
        {
            MethodInfo method = controller.GetType().GetMethod("Seek",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
                throw new InvalidOperationException("Seek 메서드가 없습니다.");
            return (SeekDelegate)method.CreateDelegate(
                typeof(SeekDelegate), controller);
        }

        private static void DisposeController(object controller)
        {
            if (controller == null) return;
            try { Invoke(controller, "Dispose"); }
            catch { /* 정리 중 예외는 삼킴 */ }
        }
    }
}
