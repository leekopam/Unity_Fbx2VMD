using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 하체 접촉 궤적 드리프트를 분해 측정하는 진단 창임.
    /// HumanoidLowerBodyPlaybackQualityTests의 접촉 런 지표와 동일한 측정을
    /// 임의 클립·모델·프레임 범위로 수행하고, 최악 런과 의도 구간 분단을 함께 출력함.
    /// 다른 세션의 하체 보정 원인 분석 참고 도구로만 씀.
    /// </summary>
    internal sealed class FootContactDriftDiagnosticsWindow : EditorWindow
    {
        private const string DefaultClipPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string DefaultModelPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        // 품질 테스트와 동일한 접촉 판정 상수임. 지표 비교가 맞으려면 함께 움직여야 함.
        private const float ContactHeightMarginMeters = 0.03f;
        private const float ContactSpeedLimitMetersPerSecond = 0.15f;
        private const int MinimumContactFrameCount = 6;
        private const float AdditionalDriftGateMeters = 0.01f;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        private string _clipPath = DefaultClipPath;
        private string _modelPath = DefaultModelPath;
        private int _startFrame;
        private int _endFrame = -1;
        private bool _measureDirect = true;
        private bool _measureCorrected = true;
        private bool _disableComponents = true;
        private string _csvPath = "";
        private string _report = "";
        private Vector2 _scroll;

        [MenuItem("Tools/FBXImporter/하체 접촉 드리프트 진단")]
        private static void Open()
        {
            var window = GetWindow<FootContactDriftDiagnosticsWindow>(
                utility: false,
                title: "하체 접촉 드리프트");
            window.minSize = new Vector2(460f, 420f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("측정 대상", EditorStyles.boldLabel);
            _clipPath = EditorGUILayout.TextField("소스 클립 FBX", _clipPath);
            _modelPath = EditorGUILayout.TextField("대상 모델 FBX", _modelPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                _startFrame = EditorGUILayout.IntField("시작 프레임", _startFrame);
                _endFrame = EditorGUILayout.IntField("끝 프레임(-1=전체)", _endFrame);
            }

            EditorGUILayout.LabelField("측정 구성", EditorStyles.boldLabel);
            _measureDirect = EditorGUILayout.ToggleLeft("직접 리타깃(보정 없음)도 측정", _measureDirect);
            _measureCorrected = EditorGUILayout.ToggleLeft("접촉 안정화 보정본 측정", _measureCorrected);
            _disableComponents = EditorGUILayout.ToggleLeft("인스턴스 컴포넌트 비활성화", _disableComponents);
            _csvPath = EditorGUILayout.TextField("CSV 출력 경로(비우면 생략)", _csvPath);

            using (new EditorGUI.DisabledScope(!_measureDirect && !_measureCorrected))
            {
                if (GUILayout.Button("측정 실행"))
                {
                    RunMeasurement();
                }
            }

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RunMeasurement()
        {
            _report = "측정 중...";
            Repaint();
            var instances = new List<GameObject>();
            var controllers = new List<object>();
            try
            {
                EnsureHumanoidClipImport(_clipPath);
                AnimationClip clip = LoadHumanoidClip(_clipPath);
                GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(_clipPath);
                if (clip == null || sourceAsset == null)
                {
                    _report = $"Humanoid 클립을 찾지 못함: {_clipPath}";
                    return;
                }

                float frameRate = clip.frameRate > 0f ? clip.frameRate : 60f;
                int lastFrame = Mathf.CeilToInt(clip.length * frameRate);
                int end = _endFrame < 0 ? lastFrame : Mathf.Min(_endFrame, lastFrame);
                int start = Mathf.Clamp(_startFrame, 0, end);

                var sourceRig = CreateRig(_clipPath, "진단 소스", instances);
                object sourceController = CreateController();
                controllers.Add(sourceController);
                Invoke(sourceController, "Prepare", sourceRig.Animator, clip);

                FootRig directRig = null;
                object directController = null;
                if (_measureDirect)
                {
                    directRig = CreateRig(_modelPath, "진단 직접", instances);
                    directController = CreateController();
                    controllers.Add(directController);
                    Invoke(directController, "Prepare", directRig.Animator, clip);
                }

                FootRig correctedRig = null;
                object correctedController = null;
                if (_measureCorrected)
                {
                    correctedRig = CreateRig(_modelPath, "진단 보정", instances);
                    correctedController = CreateController();
                    controllers.Add(correctedController);
                    Invoke(correctedController, "PrepareWithArmDirectionReference",
                        correctedRig.Animator, clip, sourceAsset);
                }

                int count = end - start + 1;
                var sourceLeft = new List<Vector3>(count);
                var sourceRight = new List<Vector3>(count);
                var directLeft = new List<Vector3>(count);
                var directRight = new List<Vector3>(count);
                var correctedLeft = new List<Vector3>(count);
                var correctedRight = new List<Vector3>(count);
                var frames = new List<int>(count);

                for (int frame = start; frame <= end; frame++)
                {
                    float timeSeconds = Mathf.Min(frame / frameRate, clip.length);
                    if (EditorUtility.DisplayCancelableProgressBar(
                        "하체 접촉 드리프트 측정",
                        $"frame {frame}/{end}",
                        (frame - start) / (float)Mathf.Max(1, count)))
                    {
                        _report = "사용자가 측정을 취소함.";
                        return;
                    }

                    Invoke(sourceController, "Seek", timeSeconds);
                    if (directController != null)
                    {
                        Invoke(directController, "Seek", timeSeconds);
                    }
                    if (correctedController != null)
                    {
                        Invoke(correctedController, "Seek", timeSeconds);
                    }

                    frames.Add(frame);
                    sourceLeft.Add(sourceRig.ContactPoint(isLeft: true));
                    sourceRight.Add(sourceRig.ContactPoint(isLeft: false));
                    if (directRig != null)
                    {
                        directLeft.Add(directRig.ContactPoint(isLeft: true));
                        directRight.Add(directRig.ContactPoint(isLeft: false));
                    }
                    if (correctedRig != null)
                    {
                        correctedLeft.Add(correctedRig.ContactPoint(isLeft: true));
                        correctedRight.Add(correctedRig.ContactPoint(isLeft: false));
                    }
                }

                var report = new StringBuilder();
                report.AppendLine($"클립={clip.name} fps={frameRate:F2} 프레임 {start}-{end} ({count}프레임)");
                report.AppendLine($"게이트={AdditionalDriftGateMeters:F4}m (품질 테스트와 동일)");
                report.AppendLine();
                AppendFootReport(report, "왼발", sourceLeft, directLeft, correctedLeft, frameRate, frames);
                AppendFootReport(report, "오른발", sourceRight, directRight, correctedRight, frameRate, frames);
                AppendIntentReport(report, correctedController, frames[0], frames[frames.Count - 1]);
                if (!string.IsNullOrEmpty(_csvPath))
                {
                    WriteCsv(frames, sourceLeft, sourceRight, directLeft, directRight,
                        correctedLeft, correctedRight);
                    report.AppendLine($"CSV 기록: {_csvPath}");
                }

                _report = report.ToString();
            }
            catch (Exception exception)
            {
                _report = "측정 실패: " + exception;
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                foreach (object controller in controllers)
                {
                    (controller as IDisposable)?.Dispose();
                }
                foreach (GameObject instance in instances)
                {
                    if (instance != null)
                    {
                        DestroyImmediate(instance);
                    }
                }
            }
        }

        private FootRig CreateRig(string modelPath, string name, List<GameObject> instances)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"모델 asset을 찾지 못함: {modelPath}");
            }

            GameObject instance = Instantiate(asset);
            instance.name = name;
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.SetActive(true);
            instances.Add(instance);
            if (_disableComponents)
            {
                foreach (MonoBehaviour script in instance.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    script.enabled = false;
                }
            }

            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                throw new InvalidOperationException($"유효한 Humanoid Avatar가 필요함: {modelPath}");
            }

            animator.enabled = true;
            return new FootRig(animator);
        }

        private static object CreateController()
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController", throwOnError: true);
            return Activator.CreateInstance(type, nonPublic: true);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, Flags);
            if (method == null)
            {
                throw new InvalidOperationException($"{methodName} 메서드가 필요합니다.");
            }

            return method.Invoke(target, arguments);
        }

        private static void EnsureHumanoidClipImport(string clipPath)
        {
            Type configurator = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator", throwOnError: false);
            MethodInfo method = configurator?.GetMethod("EnsureHumanoid", Flags);
            method?.Invoke(null, new object[] { clipPath });
        }

        private static AnimationClip LoadHumanoidClip(string clipPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(clipPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
        }

        private static void AppendFootReport(
            StringBuilder report,
            string label,
            IReadOnlyList<Vector3> source,
            IReadOnlyList<Vector3> direct,
            IReadOnlyList<Vector3> corrected,
            float frameRate,
            IReadOnlyList<int> frames)
        {
            bool[] contact = DetectStableContactFrames(source, frameRate);
            var runs = CollectRuns(contact, MinimumContactFrameCount);
            report.AppendLine($"[{label}] 접촉 런 {runs.Count}개");
            var rows = new List<(int s, int e, float src, float dir, float cor)>();
            foreach ((int runStart, int runEnd) in runs)
            {
                float sourceDrift = HorizontalDistance(source[runStart], source[runEnd]);
                float directDrift = direct != null && direct.Count == source.Count
                    ? HorizontalDistance(direct[runStart], direct[runEnd])
                    : float.NaN;
                float correctedDrift = corrected != null && corrected.Count == source.Count
                    ? HorizontalDistance(corrected[runStart], corrected[runEnd])
                    : float.NaN;
                rows.Add((frames[runStart], frames[runEnd], sourceDrift, directDrift, correctedDrift));
            }

            foreach ((int s, int e, float src, float dir, float cor) in rows
                .OrderByDescending(row => Mathf.Max(
                    float.IsNaN(row.dir) ? float.NegativeInfinity : row.dir - row.src,
                    float.IsNaN(row.cor) ? float.NegativeInfinity : row.cor - row.src))
                .Take(8))
            {
                report.AppendLine(
                    $"  런 {s}-{e}: src={src:F4} " +
                    $"{(float.IsNaN(dir) ? "" : $"direct={dir:F4}(+{dir - src:F4}) ")}" +
                    $"{(float.IsNaN(cor) ? "" : $"corr={cor:F4}(+{cor - src:F4})")}" +
                    $"{(!float.IsNaN(cor) && cor - src > AdditionalDriftGateMeters ? "  ←게이트 초과" : "")}");
            }

            float maxAdditional = rows.Count == 0
                ? 0f
                : rows.Max(row => float.IsNaN(row.cor) ? 0f : row.cor - row.src);
            report.AppendLine($"  최대 추가 드리프트(corr): {maxAdditional:F4}m");
            report.AppendLine();
        }

        private static void AppendIntentReport(
            StringBuilder report,
            object correctedController,
            int firstFrame,
            int lastFrame)
        {
            if (correctedController == null)
            {
                return;
            }

            // 진단 전용 — 안정화기의 의도 구간 분단을 그대로 노출해 핀 섬 잔차 분석에 씀.
            FieldInfo field = correctedController.GetType().GetField(
                "_footContactStabilizer", Flags);
            object stabilizer = field?.GetValue(correctedController);
            object estimate = stabilizer?.GetType()
                .GetProperty("IntentEstimate", Flags)?.GetValue(stabilizer);
            if (estimate == null)
            {
                report.AppendLine("의도 추정 없음(보정본 미초기화)");
                return;
            }

            report.AppendLine("[의도 구간 — 핀은 Confident·Plant 구간에만 부여됨]");
            AppendIntentSide(report, "왼발", estimate, "Left", "LeftUncertainSpans", firstFrame, lastFrame);
            AppendIntentSide(report, "오른발", estimate, "Right", "RightUncertainSpans", firstFrame, lastFrame);
        }

        private static void AppendIntentSide(
            StringBuilder report,
            string label,
            object estimate,
            string listProperty,
            string uncertainProperty,
            int firstFrame,
            int lastFrame)
        {
            Type estimateType = estimate.GetType();
            var intents = estimateType.GetProperty(listProperty, Flags)
                ?.GetValue(estimate) as System.Collections.IEnumerable;
            if (intents != null)
            {
                foreach (object intent in intents)
                {
                    Type intentType = intent.GetType();
                    int start = (int)intentType.GetProperty("StartFrame", Flags).GetValue(intent);
                    int end = (int)intentType.GetProperty("EndFrameExclusive", Flags).GetValue(intent);
                    if (end < firstFrame || start > lastFrame)
                    {
                        continue;
                    }

                    object mode = intentType.GetProperty("Mode", Flags).GetValue(intent);
                    object certainty = intentType.GetProperty("Certainty", Flags).GetValue(intent);
                    bool startsAtClipStart = (bool)intentType
                        .GetProperty("StartsAtClipStart", Flags).GetValue(intent);
                    report.AppendLine(
                        $"  {label} {start}-{end}: {mode} {certainty}" +
                        $"{(startsAtClipStart ? " (클립 시작 지지)" : "")}");
                }
            }

            var uncertain = estimateType.GetProperty(uncertainProperty, Flags)
                ?.GetValue(estimate) as System.Collections.IEnumerable;
            if (uncertain == null)
            {
                return;
            }

            foreach (object span in uncertain)
            {
                Vector2Int range = (Vector2Int)span;
                if (range.y < firstFrame || range.x > lastFrame)
                {
                    continue;
                }

                report.AppendLine($"  {label} 불확실 {range.x}-{range.y} (사람 표식 대상)");
            }
        }

        private void WriteCsv(
            IReadOnlyList<int> frames,
            IReadOnlyList<Vector3> sourceLeft,
            IReadOnlyList<Vector3> sourceRight,
            IReadOnlyList<Vector3> directLeft,
            IReadOnlyList<Vector3> directRight,
            IReadOnlyList<Vector3> correctedLeft,
            IReadOnlyList<Vector3> correctedRight)
        {
            var builder = new StringBuilder();
            builder.AppendLine("frame,src_lx,src_ly,src_lz,src_rx,src_ry,src_rz," +
                "dir_lx,dir_ly,dir_lz,dir_rx,dir_ry,dir_rz," +
                "cor_lx,cor_ly,cor_lz,cor_rx,cor_ry,cor_rz");
            for (int index = 0; index < frames.Count; index++)
            {
                builder.Append(frames[index]);
                AppendPoint(builder, sourceLeft[index]);
                AppendPoint(builder, sourceRight[index]);
                AppendPoint(builder, directLeft.Count == frames.Count ? directLeft[index] : (Vector3?)null);
                AppendPoint(builder, directRight.Count == frames.Count ? directRight[index] : (Vector3?)null);
                AppendPoint(builder, correctedLeft.Count == frames.Count ? correctedLeft[index] : (Vector3?)null);
                AppendPoint(builder, correctedRight.Count == frames.Count ? correctedRight[index] : (Vector3?)null);
                builder.Append('\n');
            }

            string directory = System.IO.Path.GetDirectoryName(_csvPath);
            if (!string.IsNullOrEmpty(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            System.IO.File.WriteAllText(_csvPath, builder.ToString());
        }

        private static void AppendPoint(StringBuilder builder, Vector3? point)
        {
            builder.Append(point.HasValue
                ? $",{point.Value.x:F6},{point.Value.y:F6},{point.Value.z:F6}"
                : ",,,");
        }

        private static List<(int start, int end)> CollectRuns(bool[] contact, int minimumLength)
        {
            var runs = new List<(int start, int end)>();
            int start = -1;
            for (int index = 0; index <= contact.Length; index++)
            {
                bool isContact = index < contact.Length && contact[index];
                if (isContact && start < 0)
                {
                    start = index;
                    continue;
                }
                if (!isContact && start >= 0)
                {
                    if (index - start >= minimumLength)
                    {
                        runs.Add((start, index - 1));
                    }
                    start = -1;
                }
            }

            return runs;
        }

        private static bool[] DetectStableContactFrames(
            IReadOnlyList<Vector3> points,
            float frameRate)
        {
            float[] sortedHeights = points
                .Select(point => point.y)
                .OrderBy(value => value)
                .ToArray();
            int percentileIndex = Mathf.Clamp(
                Mathf.CeilToInt(sortedHeights.Length * 0.02f) - 1,
                0,
                sortedHeights.Length - 1);
            float contactHeight = sortedHeights[percentileIndex] + ContactHeightMarginMeters;

            var candidates = new bool[points.Count];
            var stable = new bool[points.Count];
            for (int index = 0; index < points.Count; index++)
            {
                float horizontalSpeed = index == 0
                    ? 0f
                    : HorizontalDistance(points[index - 1], points[index]) * frameRate;
                candidates[index] = points[index].y <= contactHeight &&
                    horizontalSpeed <= ContactSpeedLimitMetersPerSecond;
                stable[index] = candidates[index] &&
                    CenteredSpeed(points, index, frameRate) <= ContactSpeedLimitMetersPerSecond;
            }

            var result = new bool[points.Count];
            int start = -1;
            for (int index = 0; index <= candidates.Length; index++)
            {
                bool isCandidate = index < candidates.Length && candidates[index];
                if (isCandidate && start < 0)
                {
                    start = index;
                    continue;
                }
                if (!isCandidate && start >= 0)
                {
                    int firstStable = -1;
                    int lastStable = -1;
                    for (int inner = start; inner <= index - 1; inner++)
                    {
                        if (!stable[inner])
                        {
                            continue;
                        }
                        if (firstStable < 0)
                        {
                            firstStable = inner;
                        }
                        lastStable = inner;
                    }
                    if (firstStable >= 0)
                    {
                        // 품질 테스트와 동일하게 안정 구간 양끝 1프레임까지 접촉으로 표시함.
                        int contactStart = Mathf.Max(start, firstStable - 1);
                        int contactEnd = Mathf.Min(index - 1, lastStable + 1);
                        for (int inner = contactStart; inner <= contactEnd; inner++)
                        {
                            result[inner] = true;
                        }
                    }
                    start = -1;
                }
            }

            return result;
        }

        private static float CenteredSpeed(
            IReadOnlyList<Vector3> points,
            int index,
            float frameRate)
        {
            if (points.Count <= 1)
            {
                return 0f;
            }
            if (index == 0)
            {
                return Vector3.Distance(points[0], points[1]) * frameRate;
            }
            if (index == points.Count - 1)
            {
                return Vector3.Distance(points[index - 1], points[index]) * frameRate;
            }

            return Vector3.Distance(points[index - 1], points[index + 1]) *
                frameRate * 0.5f;
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private sealed class FootRig
        {
            private readonly Transform _leftFoot;
            private readonly Transform _leftToes;
            private readonly Transform _rightFoot;
            private readonly Transform _rightToes;

            internal FootRig(Animator animator)
            {
                Animator = animator;
                _leftFoot = RequireBone(animator, HumanBodyBones.LeftFoot);
                _leftToes = RequireBone(animator, HumanBodyBones.LeftToes);
                _rightFoot = RequireBone(animator, HumanBodyBones.RightFoot);
                _rightToes = RequireBone(animator, HumanBodyBones.RightToes);
            }

            internal Animator Animator { get; }

            internal Vector3 ContactPoint(bool isLeft)
            {
                Transform foot = isLeft ? _leftFoot : _rightFoot;
                Transform toes = isLeft ? _leftToes : _rightToes;
                return (foot.position + toes.position) * 0.5f;
            }

            private static Transform RequireBone(Animator animator, HumanBodyBones bone)
            {
                Transform transform = animator.GetBoneTransform(bone);
                if (transform == null)
                {
                    throw new InvalidOperationException($"{bone} Humanoid 본이 필요합니다.");
                }

                return transform;
            }
        }
    }
}
