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
    /// 접촉 드리프트 측정의 정적 진입점임. 진단 창과 배치 실행이 같은
    /// 측정 코드를 공유하게 하려고 창의 측정 로직을 여기로 모음.
    /// `-batchmode -executeMethod Fbx2Vmd.FBXImporter.FbxDiagnostics.RunAll`로
    /// 호출하면 CSV와 summary.json을 남기고 게이트 실패 시 종료 코드 1로 끝냄.
    /// </summary>
    internal static class FbxDiagnostics
    {
        internal const float ContactHeightMarginMeters = 0.03f;
        internal const float ContactSpeedLimitMetersPerSecond = 0.15f;
        internal const int MinimumContactFrameCount = 6;
        internal const float GateMeters = 0.01f;
        internal const string DefaultClipPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        internal const string DefaultTargetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";

        internal sealed class RunResult
        {
            internal bool IsLeft;
            internal int Start, End;
            internal float SourceDrift, DirectDrift, CorrectedDrift;
            internal float Additional => CorrectedDrift - SourceDrift;
            internal bool GateExceeded => Additional > GateMeters;
        }

        internal sealed class DriftReport
        {
            internal readonly List<RunResult> Runs = new List<RunResult>();
            internal string CsvPath = "";
            internal float WorstAdditional;
            internal bool GatePassed;
            // 진행바 취소로 조기 반환된 보고서와 완료된 보고서를 구분함.
            internal bool Completed;
        }

        private delegate bool SeekDelegate(float timeSeconds);

        /// <summary>
        /// 배치 진입점. -clipPath 또는 -clipList(; 또는 | 구분)로 클립을 받고
        /// -targetPath/-firstFrame/-lastFrame/-outputDir/-offlineSolver 인자를 읽음.
        /// 클립이 여러 개면 클립별 하위 폴더에 요약을 쓰고 batch-summary.json을 집계함.
        /// 한 클립이 실패해도 나머지를 계속 측정해 회귀 세트 전체 그림을 남김.
        /// </summary>
        public static void RunAll()
        {
            string outputDir = Path.Combine(
                Directory.GetCurrentDirectory(), "Docs", "Workflow", "Local",
                "drift-diagnostics", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            try
            {
                string[] clipPaths = ParseClipList();
                string targetPath = GetArgument("-targetPath") ?? DefaultTargetPath;
                int firstFrame = ParseInt(GetArgument("-firstFrame"), 0);
                int lastFrame = ParseInt(GetArgument("-lastFrame"), -1);
                outputDir = GetArgument("-outputDir") ?? outputDir;
                // 오프라인 접촉 솔버 실험 경로를 게이트 측정에 포함할 수 있게 인자로 토글함.
                SetOfflineContactSolver(
                    ParseInt(GetArgument("-offlineSolver"), 0) != 0);
                // -dumpRuns 1이면 런별 정책·보정량 진단 CSV를 함께 남김.
                bool dumpRuns = ParseInt(GetArgument("-dumpRuns"), 0) != 0;
                // -traceStart/-traceEnd로 지정한 구간의 프레임별 궤적을 덤프함.
                int traceStart = ParseInt(GetArgument("-traceStart"), -1);
                int traceEnd = ParseInt(GetArgument("-traceEnd"), -1);

                var batch = new List<ClipResult>();
                bool allPassed = true;
                foreach (string clipPath in clipPaths)
                {
                    string clipDir = clipPaths.Length > 1
                        ? Path.Combine(outputDir,
                            Path.GetFileNameWithoutExtension(clipPath))
                        : outputDir;
                    var entry = new ClipResult { Clip = clipPath };
                    try
                    {
                        entry.Report = MeasureFootContactDrift(
                            clipPath, targetPath, firstFrame, lastFrame, clipDir,
                            diagnosticsDir: dumpRuns || traceStart >= 0
                                ? clipDir : null,
                            traceStart: traceStart, traceEnd: traceEnd);
                        WriteSummaryJson(entry.Report, clipDir, clipPath,
                            targetPath, firstFrame, lastFrame);
                        Debug.Log(entry.Report.GatePassed
                            ? $"[FbxDiagnostics] 게이트 통과: {clipPath} {entry.Report.WorstAdditional:F4}m"
                            : $"[FbxDiagnostics] 게이트 초과: {clipPath} {entry.Report.WorstAdditional:F4}m");
                        allPassed &= entry.Report.GatePassed;
                    }
                    catch (Exception clipError)
                    {
                        entry.Error = clipError.Message;
                        allPassed = false;
                        Debug.LogError(
                            $"[FbxDiagnostics] {clipPath} 측정 실패: {clipError.Message}");
                    }
                    batch.Add(entry);
                }

                if (clipPaths.Length > 1)
                {
                    WriteBatchSummaryJson(batch, outputDir, targetPath,
                        firstFrame, lastFrame);
                }
                EditorApplication.Exit(allPassed ? 0 : 1);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(outputDir);
                File.WriteAllText(Path.Combine(outputDir, "summary.json"),
                    "{\"error\":\"" + EscapeJson(error.Message) + "\"}");
                Debug.LogError($"[FbxDiagnostics] 측정 실패: {error.Message}");
                EditorApplication.Exit(2);
            }
        }

        private sealed class ClipResult
        {
            internal string Clip;
            internal DriftReport Report;
            internal string Error;
        }

        private static string[] ParseClipList()
        {
            string list = GetArgument("-clipList");
            if (string.IsNullOrEmpty(list))
                return new[] { GetArgument("-clipPath") ?? DefaultClipPath };
            return list.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .ToArray();
        }

        private static void WriteBatchSummaryJson(List<ClipResult> batch,
            string outputDir, string targetPath, int firstFrame, int lastFrame)
        {
            Directory.CreateDirectory(outputDir);
            var clipsJson = new StringBuilder();
            foreach (ClipResult entry in batch)
            {
                if (clipsJson.Length > 0) clipsJson.Append(',');
                if (entry.Report != null)
                {
                    clipsJson.Append(string.Format(CultureInfo.InvariantCulture,
                        "{{\"clip\":\"{0}\",\"worst_additional_m\":{1}," +
                        "\"gate_passed\":{2},\"runs\":{3}}}",
                        EscapeJson(entry.Clip), entry.Report.WorstAdditional,
                        entry.Report.GatePassed ? "true" : "false",
                        entry.Report.Runs.Count));
                }
                else
                {
                    clipsJson.Append(string.Format(CultureInfo.InvariantCulture,
                        "{{\"clip\":\"{0}\",\"gate_passed\":false," +
                        "\"error\":\"{1}\"}}",
                        EscapeJson(entry.Clip), EscapeJson(entry.Error)));
                }
            }
            File.WriteAllText(Path.Combine(outputDir, "batch-summary.json"),
                string.Format(CultureInfo.InvariantCulture,
                    "{{\"target\":\"{0}\",\"first_frame\":{1},\"last_frame\":{2}," +
                    "\"gate_m\":{3},\"clips\":[{4}],\"all_passed\":{5}}}",
                    EscapeJson(targetPath), firstFrame, lastFrame, GateMeters,
                    clipsJson, batch.All(e => e.Report != null && e.Report.GatePassed)
                        ? "true" : "false"));
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < args.Length; index++)
            {
                if (args[index] == name) return args[index + 1];
            }

            return null;
        }

        private static int ParseInt(string value, int fallback) =>
            int.TryParse(value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int result)
                ? result
                : fallback;

        /// <summary>
        /// 3-리그(소스·직접·보정) 프레임 탐색으로 접촉 런별 드리프트를 측정함.
        /// HumanoidLowerBodyPlaybackQualityTests와 같은 지표를 사용함.
        /// </summary>
        internal static DriftReport MeasureFootContactDrift(
            string clipPath,
            string targetPath,
            int firstFrame,
            int lastFrame,
            string outputDir,
            Func<int, int, int, bool> progress = null,
            string diagnosticsDir = null,
            int traceStart = -1, int traceEnd = -1)
        {
            var report = new DriftReport();
            GameObject sourceTarget = null, directTarget = null, correctedTarget = null;
            object sourceController = null, directController = null,
                correctedController = null;
            try
            {
                AnimationClip clip = LoadHumanoidClip(clipPath);
                GameObject sourceAsset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(clipPath);
                if (sourceAsset == null)
                    throw new InvalidOperationException(
                        $"클립 FBX를 찾지 못했습니다: {clipPath}");

                sourceTarget = InstantiateAsset(clipPath, "Drift Source");
                directTarget = InstantiateAsset(targetPath, "Drift Direct");
                correctedTarget = InstantiateAsset(targetPath, "Drift Corrected");
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
                int last = lastFrame >= 0 ? Mathf.Min(lastFrame, clipLast) : clipLast;
                if (firstFrame < 0 || last <= firstFrame)
                    throw new InvalidOperationException("프레임 범위가 비어 있습니다.");

                Transform[] src = FootBones(sourceAnimator);
                Transform[] dir = FootBones(directAnimator);
                Transform[] cor = FootBones(correctedAnimator);
                int count = last - firstFrame + 1;
                var sourceL = new Vector3[count]; var sourceR = new Vector3[count];
                var directL = new Vector3[count]; var directR = new Vector3[count];
                var correctL = new Vector3[count]; var correctR = new Vector3[count];
                // 트레이스용 — 추정기와 같은 발목/발끝 분리 표본.
                Vector3[] srcFootL = null, srcToeL = null,
                    srcFootR = null, srcToeR = null;
                if (traceStart >= 0)
                {
                    srcFootL = new Vector3[count]; srcToeL = new Vector3[count];
                    srcFootR = new Vector3[count]; srcToeR = new Vector3[count];
                }

                for (int frame = firstFrame; frame <= last; frame++)
                {
                    float time = Mathf.Min(frame / frameRate, clip.length);
                    if (!sourceSeek(time) || !directSeek(time) || !correctedSeek(time))
                        throw new InvalidOperationException($"{frame}프레임 탐색 실패");
                    int i = frame - firstFrame;
                    sourceL[i] = ContactPoint(src, true);
                    sourceR[i] = ContactPoint(src, false);
                    if (srcFootL != null)
                    {
                        srcFootL[i] = src[0].position; srcToeL[i] = src[1].position;
                        srcFootR[i] = src[2].position; srcToeR[i] = src[3].position;
                    }
                    directL[i] = ContactPoint(dir, true);
                    directR[i] = ContactPoint(dir, false);
                    correctL[i] = ContactPoint(cor, true);
                    correctR[i] = ContactPoint(cor, false);
                    if (progress != null && i % 120 == 0 &&
                        progress(i, count, frame))
                    {
                        return report;
                    }
                }

                ReportSide(report.Runs, true, sourceL, directL, correctL,
                    frameRate, firstFrame);
                ReportSide(report.Runs, false, sourceR, directR, correctR,
                    frameRate, firstFrame);
                report.WorstAdditional = report.Runs.Count == 0 ? 0f :
                    report.Runs.Max(r => r.Additional);
                report.GatePassed = report.WorstAdditional <= GateMeters;
                report.CsvPath = WriteCsv(report.Runs, clipPath, outputDir);
                if (!string.IsNullOrEmpty(diagnosticsDir))
                {
                    // 보정 컨트롤러가 살아 있는 동안 plan·의도를 꺼내 런별 원인을 남김.
                    WriteRunDiagnostics(report.Runs, correctedController,
                        sourceL, sourceR, directL, directR, correctL, correctR,
                        srcFootL, srcToeL, srcFootR, srcToeR,
                        frameRate, firstFrame, clipPath, diagnosticsDir,
                        traceStart, traceEnd);
                }
                report.Completed = true;
            }
            finally
            {
                DisposeController(sourceController);
                DisposeController(directController);
                DisposeController(correctedController);
                if (sourceTarget != null) UnityEngine.Object.DestroyImmediate(sourceTarget);
                if (directTarget != null) UnityEngine.Object.DestroyImmediate(directTarget);
                if (correctedTarget != null)
                    UnityEngine.Object.DestroyImmediate(correctedTarget);
            }

            return report;
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

        private static void ReportSide(List<RunResult> runs, bool isLeft,
            Vector3[] source, Vector3[] direct, Vector3[] corrected,
            float frameRate, int firstFrame)
        {
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
                    runs.Add(new RunResult
                    {
                        IsLeft = isLeft,
                        Start = start + firstFrame,
                        End = end + firstFrame,
                        SourceDrift = HorizontalDistance(source[start], source[end]),
                        DirectDrift = HorizontalDistance(direct[start], direct[end]),
                        CorrectedDrift =
                            HorizontalDistance(corrected[start], corrected[end]),
                    });
                }
                start = -1;
            }
        }

        internal static string WriteCsv(List<RunResult> runs, string clipPath,
            string outputDir)
        {
            if (runs.Count == 0) return "";
            string directory = string.IsNullOrEmpty(outputDir)
                ? Path.Combine(
                    Directory.GetParent(Application.dataPath)?.FullName ??
                        Application.dataPath,
                    "Docs", "Workflow", "Local", "drift-diagnostics")
                : outputDir;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,
                $"{Path.GetFileNameWithoutExtension(clipPath)}-drift-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(
                    "side,start,end,source_drift_m,direct_drift_m,corrected_drift_m," +
                    "additional_m,gate_exceeded");
                foreach (RunResult run in runs)
                {
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6},{7}",
                        run.IsLeft ? "left" : "right", run.Start, run.End,
                        run.SourceDrift, run.DirectDrift, run.CorrectedDrift,
                        run.Additional, run.GateExceeded ? 1 : 0));
                }
            }
            return path;
        }

        private static void WriteSummaryJson(DriftReport report, string outputDir,
            string clipPath, string targetPath, int firstFrame, int lastFrame)
        {
            Directory.CreateDirectory(outputDir);
            var runsJson = new StringBuilder();
            foreach (RunResult run in report.Runs)
            {
                if (runsJson.Length > 0) runsJson.Append(',');
                runsJson.Append(string.Format(CultureInfo.InvariantCulture,
                    "{{\"side\":\"{0}\",\"start\":{1},\"end\":{2}," +
                    "\"source_drift_m\":{3},\"direct_drift_m\":{4}," +
                    "\"corrected_drift_m\":{5},\"additional_m\":{6}," +
                    "\"gate_exceeded\":{7}}}",
                    run.IsLeft ? "left" : "right", run.Start, run.End,
                    run.SourceDrift, run.DirectDrift, run.CorrectedDrift,
                    run.Additional,
                    run.GateExceeded ? "true" : "false"));
            }
            File.WriteAllText(Path.Combine(outputDir, "summary.json"),
                string.Format(CultureInfo.InvariantCulture,
                    "{{\"clip\":\"{0}\",\"target\":\"{1}\",\"first_frame\":{2}," +
                    "\"last_frame\":{3},\"gate_m\":{4},\"worst_additional_m\":{5}," +
                    "\"gate_passed\":{6},\"csv\":\"{7}\",\"runs\":[{8}]}}",
                    EscapeJson(clipPath), EscapeJson(targetPath), firstFrame,
                    lastFrame, GateMeters, report.WorstAdditional,
                    report.GatePassed ? "true" : "false",
                    EscapeJson(report.CsvPath), runsJson));
        }

        // 에러 메시지의 개행·탭 같은 제어문자까지 이스케이프해 항상 유효한 JSON을 씀.
        internal static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c == '\\') builder.Append("\\\\");
                else if (c == '"') builder.Append("\\\"");
                else if (c == '\n') builder.Append("\\n");
                else if (c == '\r') builder.Append("\\r");
                else if (c == '\t') builder.Append("\\t");
                else if (c < ' ')
                {
                    builder.Append("\\u").Append(((int)c)
                        .ToString("x4", CultureInfo.InvariantCulture));
                }
                else builder.Append(c);
            }
            return builder.ToString();
        }

        // ↓ 품질 테스트의 FootContactMetrics와 동일한 지표 — 변경 시 테스트와 함께 유지함.
        internal static bool[] DetectStableContactFrames(Vector3[] points,
            float frameRate)
        {
            float[] sortedHeights = points.Select(p => p.y).OrderBy(v => v).ToArray();
            int percentileIndex = Mathf.Clamp(
                Mathf.CeilToInt(sortedHeights.Length * 0.02f) - 1,
                0, sortedHeights.Length - 1);
            float contactHeight = sortedHeights[percentileIndex] +
                ContactHeightMarginMeters;
            var candidates = new bool[points.Length];
            var stable = new bool[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                float speed = i == 0 ? 0f :
                    HorizontalDistance(points[i - 1], points[i]) * frameRate;
                candidates[i] = points[i].y <= contactHeight &&
                    speed <= ContactSpeedLimitMetersPerSecond;
                stable[i] = candidates[i] &&
                    CenteredSpeed(points, i, frameRate) <=
                    ContactSpeedLimitMetersPerSecond;
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

        private static float CenteredSpeed(Vector3[] points, int index,
            float frameRate)
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
                    !c.name.StartsWith("__", StringComparison.Ordinal) &&
                    c.humanMotion);
            if (clip == null)
                throw new InvalidOperationException(
                    $"Humanoid 클립이 없습니다: {path}");
            return clip;
        }

        // 런별로 플래너 앵커 정책 분포와 plan 보정량을 덤프해
        // 게이트 초과 원인(핀 부재·핀 차단·오정책)을 구분하게 함.
        private static void WriteRunDiagnostics(List<RunResult> runs,
            object correctedController,
            Vector3[] sourceLeft, Vector3[] sourceRight,
            Vector3[] directLeft, Vector3[] directRight,
            Vector3[] correctedLeft, Vector3[] correctedRight,
            Vector3[] srcFootLeft, Vector3[] srcToeLeft,
            Vector3[] srcFootRight, Vector3[] srcToeRight,
            float frameRate, int firstFrame,
            string clipPath, string outputDir,
            int traceStart = -1, int traceEnd = -1)
        {
            Directory.CreateDirectory(outputDir);
            string path = Path.Combine(outputDir,
                $"{Path.GetFileNameWithoutExtension(clipPath)}-run-diagnostics.csv");
            var flags = BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic;

            object stabilizer = correctedController.GetType()
                .GetField("_footContactStabilizer", flags)
                .GetValue(correctedController);
            object plan = stabilizer.GetType()
                .GetField("_plan", flags).GetValue(stabilizer);
            object intents = stabilizer.GetType()
                .GetProperty("IntentEstimate", flags).GetValue(stabilizer);
            if (plan == null) return;

            MethodInfo tryEvaluate = plan.GetType().GetMethod("TryEvaluate", flags);
            // 의도 구간을 프레임별 정책으로 펼침 — 플래너의 Rasterize와 동일 규칙.
            var leftPolicies = RasterizePolicyNames(intents, "Left", runs);
            var rightPolicies = RasterizePolicyNames(intents, "Right", runs);
            var leftUncertain = UncertainSpanMask(intents, "Left", runs);
            var rightUncertain = UncertainSpanMask(intents, "Right", runs);

            var evalArgs = new object[] { 0f, Vector3.zero, Vector3.zero };
            using (var writer = new StreamWriter(path, false,
                new UTF8Encoding(false)))
            {
                writer.WriteLine("side,start,end,additional_m," +
                    "pinned_frames,sliding_frames,uncertain_frames," +
                    "airborne_frames,mean_correction_m,max_correction_m," +
                    "mean_applied_delta_m,max_applied_delta_m");
                foreach (RunResult run in runs)
                {
                    string[] policies = run.IsLeft ? leftPolicies : rightPolicies;
                    bool[] uncertain = run.IsLeft ? leftUncertain : rightUncertain;
                    Vector3[] direct = run.IsLeft ? directLeft : directRight;
                    Vector3[] corrected = run.IsLeft ? correctedLeft : correctedRight;
                    int pinned = 0, sliding = 0, uncovered = 0;
                    int uncertainCount = 0, airborneCount = 0;
                    float sum = 0f, max = 0f;
                    float appliedSum = 0f, appliedMax = 0f;
                    for (int frame = run.Start; frame <= run.End; frame++)
                    {
                        int i = frame - firstFrame;
                        if (i < 0) continue;
                        if (i < policies.Length)
                        {
                            if (policies[i] == "Pinned") pinned++;
                            else if (policies[i] == "Sliding") sliding++;
                            else
                            {
                                uncovered++;
                                // Confident 외 프레임을 추정기 분류로 세분화함.
                                // 불확실 구간에 속하면 Uncertain, 아니면 Airborne.
                                if (i < uncertain.Length && uncertain[i])
                                    uncertainCount++;
                                else
                                    airborneCount++;
                            }
                        }
                        evalArgs[0] = frame / frameRate;
                        tryEvaluate.Invoke(plan, evalArgs);
                        float magnitude =
                            ((Vector3)evalArgs[run.IsLeft ? 1 : 2]).magnitude;
                        sum += magnitude;
                        if (magnitude > max) max = magnitude;
                        if (i < direct.Length && i < corrected.Length)
                        {
                            // plan이 출력한 보정과 실제 궤적 변화를 따로 재서
                            // 보정이 적용됐는지 여부를 구분함.
                            float appliedDelta = Vector3.Distance(
                                corrected[i], direct[i]);
                            appliedSum += appliedDelta;
                            if (appliedDelta > appliedMax) appliedMax = appliedDelta;
                        }
                    }
                    int frames = run.End - run.Start + 1;
                    float mean = frames > 0 ? sum / frames : 0f;
                    float appliedMean = frames > 0 ? appliedSum / frames : 0f;
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}",
                        run.IsLeft ? "left" : "right", run.Start, run.End,
                        run.Additional, pinned, sliding, uncertainCount,
                        airborneCount, mean, max, appliedMean, appliedMax));
                }
            }

            // -traceStart/-traceEnd 구간의 프레임별 궤적과 추정기 입력량을 덤프해
            // Free 추종 수렴 여부와 Uncertain 분류의 차단 조건을 함께 검사함.
            if (traceStart >= 0 && srcFootLeft != null)
            {
                // 추정기와 같은 바닥·임계 — 스태빌라이저의 SourceHumanScale을 재사용함.
                float humanScale = (float)Convert.ChangeType(stabilizer.GetType()
                    .GetProperty("SourceHumanScale", flags)
                    .GetValue(stabilizer), typeof(float));
                WriteTrace(outputDir, clipPath, frameRate, firstFrame,
                    traceStart, traceEnd, humanScale,
                    leftPolicies, rightPolicies,
                    leftUncertain, rightUncertain,
                    sourceLeft, sourceRight, directLeft, directRight,
                    correctedLeft, correctedRight,
                    srcFootLeft, srcToeLeft, srcFootRight, srcToeRight,
                    plan, tryEvaluate, evalArgs);
            }
        }

        private static void WriteTrace(string outputDir, string clipPath,
            float frameRate, int firstFrame, int traceStart, int traceEnd,
            float humanScale,
            string[] leftPolicies, string[] rightPolicies,
            bool[] leftUncertain, bool[] rightUncertain,
            Vector3[] sourceLeft, Vector3[] sourceRight,
            Vector3[] directLeft, Vector3[] directRight,
            Vector3[] correctedLeft, Vector3[] correctedRight,
            Vector3[] footLeft, Vector3[] toeLeft,
            Vector3[] footRight, Vector3[] toeRight,
            object plan, MethodInfo tryEvaluate, object[] evalArgs)
        {
            string tracePath = Path.Combine(outputDir,
                $"{Path.GetFileNameWithoutExtension(clipPath)}-trace.csv");
            // 추정기 상수와 같은 기준으로 프레임 분류를 재계산해 차단 항목을 노출함.
            float supportHeight = humanScale * 0.015f;
            float supportSpeed = humanScale * 0.05f;
            float floorFL = FloorY(footLeft), floorTL = FloorY(toeLeft);
            float floorFR = FloorY(footRight), floorTR = FloorY(toeRight);
            using (var tw = new StreamWriter(tracePath, false,
                new UTF8Encoding(false)))
            {
                tw.WriteLine("frame,policy_l,policy_r," +
                    "src_l_x,src_l_z,cor_l_x,cor_l_z,plan_l_x,plan_l_z," +
                    "l_foot_h,l_toe_h,l_foot_v,l_toe_v,l_class," +
                    "src_r_x,src_r_z,cor_r_x,cor_r_z,plan_r_x,plan_r_z," +
                    "r_foot_h,r_toe_h,r_foot_v,r_toe_v,r_class");
                int last = traceEnd < 0 ? firstFrame + sourceLeft.Length - 1
                    : traceEnd;
                for (int frame = traceStart; frame <= last; frame++)
                {
                    int i = frame - firstFrame;
                    if (i < 0 || i >= sourceLeft.Length) continue;
                    evalArgs[0] = frame / frameRate;
                    tryEvaluate.Invoke(plan, evalArgs);
                    Vector3 cl = (Vector3)evalArgs[1];
                    Vector3 cr = (Vector3)evalArgs[2];
                    string clsL = EstimatorClass(footLeft, toeLeft, i,
                        floorFL, floorTL, supportHeight, supportSpeed, frameRate);
                    string clsR = EstimatorClass(footRight, toeRight, i,
                        floorFR, floorTR, supportHeight, supportSpeed, frameRate);
                    tw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}," +
                        "{11},{12},{13},{14},{15},{16},{17},{18},{19}," +
                        "{20},{21},{22},{23},{24}",
                        frame,
                        i < leftPolicies.Length ? leftPolicies[i] : "-",
                        i < rightPolicies.Length ? rightPolicies[i] : "-",
                        sourceLeft[i].x, sourceLeft[i].z,
                        correctedLeft[i].x, correctedLeft[i].z,
                        cl.x, cl.z,
                        footLeft[i].y - floorFL, toeLeft[i].y - floorTL,
                        i > 0 ? Vector3.Distance(footLeft[i - 1], footLeft[i]) * frameRate : 0f,
                        i > 0 ? Vector3.Distance(toeLeft[i - 1], toeLeft[i]) * frameRate : 0f,
                        clsL,
                        sourceRight[i].x, sourceRight[i].z,
                        correctedRight[i].x, correctedRight[i].z,
                        cr.x, cr.z,
                        footRight[i].y - floorFR, toeRight[i].y - floorTR,
                        i > 0 ? Vector3.Distance(footRight[i - 1], footRight[i]) * frameRate : 0f,
                        i > 0 ? Vector3.Distance(toeRight[i - 1], toeRight[i]) * frameRate : 0f,
                        clsR));
                }
            }
        }

        // 추정기의 프레임 분류와 같은 임계로 지지 여부를 재계산함(S/A/U).
        private static string EstimatorClass(Vector3[] feet, Vector3[] toes,
            int i, float floorFoot, float floorToes,
            float supportHeight, float supportSpeed, float frameRate)
        {
            if (i <= 0) return "U";
            float footHeight = feet[i].y - floorFoot;
            float toesHeight = toes[i].y - floorToes;
            float footSpeed = Vector3.Distance(feet[i - 1], feet[i]) * frameRate;
            float toesSpeed = Vector3.Distance(toes[i - 1], toes[i]) * frameRate;
            if (footHeight <= supportHeight && toesHeight <= supportHeight &&
                footSpeed <= supportSpeed && toesSpeed <= supportSpeed) return "S";
            return "U";
        }

        private static float FloorY(Vector3[] points)
        {
            var heights = new float[points.Length];
            for (int i = 0; i < points.Length; i++) heights[i] = points[i].y;
            Array.Sort(heights);
            return heights[(int)((heights.Length - 1) * 0.1f)];
        }

        // 추정 결과의 해당 발 의도 구간을 프레임별 정책 이름 배열로 펼침.
        // Confident 구간만 Pinned/Sliding, 나머지는 Free — 플래너와 동일 규칙.
        private static string[] RasterizePolicyNames(object estimate,
            string side, List<RunResult> runs)
        {
            int frameCount = runs.Count == 0 ? 0 : runs.Max(r => r.End) + 1;
            var names = new string[Mathf.Max(1, frameCount + 1)];
            for (int i = 0; i < names.Length; i++) names[i] = "Free";
            if (estimate == null) return names;
            var flags = BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic;
            var intents = (System.Collections.IEnumerable)estimate.GetType()
                .GetProperty(side, flags)?.GetValue(estimate);
            if (intents == null) return names;
            foreach (object intent in intents)
            {
                Type type = intent.GetType();
                string certainty = type.GetProperty("Certainty", flags)
                    .GetValue(intent).ToString();
                if (certainty != "Confident") continue;
                int start = Mathf.Max(0, (int)type
                    .GetProperty("StartFrame", flags).GetValue(intent));
                int end = Mathf.Min(names.Length, (int)type
                    .GetProperty("EndFrameExclusive", flags).GetValue(intent));
                string mode = type.GetProperty("Mode", flags)
                    .GetValue(intent).ToString();
                string name = mode == "Slide" ? "Sliding" : "Pinned";
                for (int i = start; i < end; i++) names[i] = name;
            }
            return names;
        }

        // 추정 결과의 불확실 구간을 프레임 마스크로 펼침.
        // Confident도 아니고 불확실 구간도 아닌 프레임은 Airborne으로 해석 가능함.
        private static bool[] UncertainSpanMask(object estimate, string side,
            List<RunResult> runs)
        {
            int frameCount = runs.Count == 0 ? 0 : runs.Max(r => r.End) + 1;
            var mask = new bool[Mathf.Max(1, frameCount + 1)];
            if (estimate == null) return mask;
            var flags = BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic;
            var spans = (System.Collections.IEnumerable)estimate.GetType()
                .GetProperty(side + "UncertainSpans", flags)?.GetValue(estimate);
            if (spans == null) return mask;
            foreach (object span in spans)
            {
                Vector2Int range = (Vector2Int)span;
                for (int i = Mathf.Max(0, range.x);
                    i < Mathf.Min(mask.Length, range.y); i++)
                {
                    mask[i] = true;
                }
            }
            return mask;
        }

        // 접촉 솔버 선택은 본체 어셈블리의 internal 상태 — 리플렉션으로만 토글함.
        private static void SetOfflineContactSolver(bool enabled)
        {
            Type planner = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactPlanner");
            FieldInfo field = planner?.GetField("UseOfflineContactSolver",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException(
                    "HumanoidFootContactPlanner.UseOfflineContactSolver를 찾지 못했습니다.");
            field.SetValue(null, enabled);
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

        private static object Invoke(object target, string method,
            params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (info == null)
                throw new InvalidOperationException($"{method} 메서드가 없습니다.");
            try
            {
                return info.Invoke(target, args);
            }
            // 리플렉션 호출 예외는 TargetInvocationException으로 감싸지므로
            // 진단 문구가 실제 실패 원인을 보여주게 언랩함.
            catch (TargetInvocationException error)
            {
                throw new InvalidOperationException(
                    $"{method} 실패: {error.InnerException?.Message ?? error.Message}",
                    error.InnerException);
            }
        }

        private static SeekDelegate CreateSeekDelegate(object controller)
        {
            MethodInfo method = controller.GetType().GetMethod("Seek",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
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
