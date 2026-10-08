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
    /// 사람 확정 표식(human-labels.csv)과 추정기 출력을 비교해 지지·미끄럼
    /// 임계를 그리드 탐색하는 캘리브레이션 진입점임.
    /// `-executeMethod Fbx2Vmd.FBXImporter.FbxIntentCalibration.Calibrate`로 실행.
    /// 지표 계산은 순수 로직으로 분리해 EditMode 테스트가 가능하게 함.
    /// </summary>
    internal static class FbxIntentCalibration
    {
        internal sealed class FrameMetrics
        {
            internal int TruePositives, FalsePositives, FalseNegatives;
            internal double Precision =>
                TruePositives + FalsePositives == 0 ? 1.0 :
                (double)TruePositives / (TruePositives + FalsePositives);
            internal double Recall =>
                TruePositives + FalseNegatives == 0 ? 1.0 :
                (double)TruePositives / (TruePositives + FalseNegatives);
            internal double F1
            {
                get
                {
                    double sum = Precision + Recall;
                    return sum <= 0.0 ? 0.0 : 2.0 * Precision * Recall / sum;
                }
            }
        }

        /// <summary>표식 한 줄의 프레임 구간과 지지 여부 — 본체 internal 타입을 넘기지
        /// 않으려고 에디터 측에서 단순 튜플로 받음.</summary>
        internal readonly struct LabelSpan
        {
            internal LabelSpan(int start, int endInclusive, bool? isSupport)
            {
                Start = start;
                EndInclusive = endInclusive;
                IsSupport = isSupport;
            }

            internal int Start { get; }
            internal int EndInclusive { get; }
            internal bool? IsSupport { get; }
        }

        /// <summary>
        /// 표식 구간 안에서만 프레임 단위로 지지 분류를 채점함.
        /// 표식 밖 프레임은 평가에서 제외해 정답 없는 구간이 점수를 오염시키지 않게 함.
        /// </summary>
        internal static FrameMetrics ComputeFrameMetrics(
            bool[] predictedSupport,
            IReadOnlyList<LabelSpan> labels)
        {
            var metrics = new FrameMetrics();
            foreach (LabelSpan label in labels)
            {
                if (!label.IsSupport.HasValue) continue;
                int start = Mathf.Max(0, label.Start);
                int end = Mathf.Min(predictedSupport.Length - 1,
                    label.EndInclusive);
                for (int index = start; index <= end; index++)
                {
                    if (label.IsSupport.Value)
                    {
                        if (predictedSupport[index]) metrics.TruePositives++;
                        else metrics.FalseNegatives++;
                    }
                    else if (predictedSupport[index])
                    {
                        metrics.FalsePositives++;
                    }
                }
            }
            return metrics;
        }

        // 배치 진입점: -clipPath, -sourceKey(라벨 짝짓기용, 기본=파일명), -outputDir.
        public static void Calibrate()
        {
            string outputDir = Path.Combine(
                Directory.GetCurrentDirectory(), "Docs", "Workflow", "Local",
                "intent-calibration", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            try
            {
                string clipPath = GetArgument("-clipPath") ??
                    FbxDiagnostics.DefaultClipPath;
                string sourceKey = GetArgument("-sourceKey") ??
                    Path.GetFileName(clipPath);
                outputDir = GetArgument("-outputDir") ?? outputDir;
                Directory.CreateDirectory(outputDir);

                string result = CalibrateClip(clipPath, sourceKey, outputDir);
                Debug.Log($"[FbxIntentCalibration] {result}");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(outputDir);
                File.WriteAllText(Path.Combine(outputDir, "calibration.json"),
                    "{\"error\":\"" +
                    FbxDiagnostics.EscapeJson(error.Message) + "\"}");
                Debug.LogError($"[FbxIntentCalibration] 실패: {error.Message}");
                EditorApplication.Exit(2);
            }
        }

        /// <summary>
        /// 클립 발 궤적을 샘플링하고 사람 표식과 비교해 임계 그리드 탐색을 수행함.
        /// 결과는 calibration.json에 기록됨.
        /// </summary>
        internal static string CalibrateClip(
            string clipPath, string sourceKey, string outputDir)
        {
            Array samples = SampleClipContacts(clipPath,
                out float frameRate, out float humanScale);
            object labelSet = LoadLabelSet(sourceKey);
            int labelRowCount = CountLabels(labelSet);
            if (labelRowCount == 0)
            {
                WriteResult(outputDir, sourceKey, 0, 0, new List<string>(),
                    "no_labels");
                return $"표식 없음({sourceKey}) — 캘리브레이션 불가";
            }

            Type estimatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentEstimator", true);
            Type tuningType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentEstimator+Tuning",
                true);
            // Estimate에는 Tuning 없는 4인자 오버로드도 있으므로 이름과
            // 마지막 인자 타입으로 Tuning 오버로드를 정확히 고름.
            MethodInfo estimate = estimatorType.GetMethods(
                    BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "Estimate") return false;
                    ParameterInfo[] parameters = m.GetParameters();
                    return parameters.Length == 5 &&
                        parameters[4].ParameterType == tuningType;
                });
            if (estimate == null)
            {
                throw new InvalidOperationException(
                    "HumanoidFootContactIntentEstimator.Estimate의 Tuning 오버로드를 찾지 못했습니다.");
            }

            float[] supportSpeeds = { 0.03f, 0.05f, 0.08f };
            float[] slideDisplacements = { 0.015f, 0.02f, 0.03f };
            float[] consistencies = { 0.4f, 0.5f, 0.6f };
            var rows = new List<string>();
            foreach (float supportSpeed in supportSpeeds)
            foreach (float slideDisplacement in slideDisplacements)
            foreach (float consistency in consistencies)
            {
                object tuning = Activator.CreateInstance(tuningType,
                    BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { 0.015f, supportSpeed, 0.05f, 0.2f,
                        slideDisplacement, consistency }, null);
                object estimateResult;
                try
                {
                    // 표식은 정답지이므로 추정기 입력으로 넘기지 않음.
                    // 주입하면 ApplyHumanLabels가 예측을 표식 값으로 덮어써
                    // 모든 튜닝 조합의 F1이 1.0이 되어 비교가 무의미해짐.
                    estimateResult = estimate.Invoke(null,
                        new object[] { samples, frameRate, humanScale,
                            null, tuning });
                }
                // 리플렉션 호출 예외는 언랩해 실제 추정 실패 원인을 남김.
                catch (TargetInvocationException error)
                {
                    throw new InvalidOperationException(
                        $"의도 추정 실패: {error.InnerException?.Message ?? error.Message}",
                        error.InnerException);
                }
                FrameMetrics metrics = ScoreEstimate(
                    estimateResult, labelSet, samples.Length);
                rows.Add(string.Format(CultureInfo.InvariantCulture,
                    "{{\"support_speed\":{0},\"slide_displacement\":{1}," +
                    "\"slide_consistency\":{2},\"precision\":{3:F4}," +
                    "\"recall\":{4:F4},\"f1\":{5:F4},\"tp\":{6},\"fp\":{7}," +
                    "\"fn\":{8}}}",
                    supportSpeed, slideDisplacement, consistency,
                    metrics.Precision, metrics.Recall, metrics.F1,
                    metrics.TruePositives, metrics.FalsePositives,
                    metrics.FalseNegatives));
            }

            string status = labelRowCount < 20 ? "insufficient_labels" : "ok";
            WriteResult(outputDir, sourceKey, labelRowCount, samples.Length,
                rows, status);
            return $"표식 {labelRowCount}행, 그리드 {rows.Count}조합 평가" +
                (status == "insufficient_labels"
                    ? " — 표식 부족으로 탐색 결과는 참고용"
                    : "");
        }

        // 예측 의도 구간을 발별 프레임 지지 마스크로 펼친 뒤 해당 발 표식과 비교해
        // 프레임 단위 정밀도·재현율·F1을 합산함.
        private static FrameMetrics ScoreEstimate(
            object estimate, object labelSet, int frameCount)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic;
            var combined = new FrameMetrics();
            foreach (string side in new[] { "Left", "Right" })
            {
                var sideMask = new bool[frameCount];
                foreach (object intent in (System.Collections.IEnumerable)
                    estimate.GetType().GetProperty(side, flags)
                        .GetValue(estimate))
                {
                    int start = (int)intent.GetType()
                        .GetProperty("StartFrame", flags).GetValue(intent);
                    int endExclusive = (int)intent.GetType()
                        .GetProperty("EndFrameExclusive", flags)
                        .GetValue(intent);
                    for (int i = Mathf.Max(0, start);
                        i < Mathf.Min(frameCount, endExclusive); i++)
                    {
                        sideMask[i] = true;
                    }
                }
                var sideLabels = new List<LabelSpan>();
                foreach (object label in (System.Collections.IEnumerable)
                    labelSet.GetType().GetProperty(side, flags)
                        .GetValue(labelSet))
                {
                    Type labelType = label.GetType();
                    sideLabels.Add(new LabelSpan(
                        (int)labelType.GetProperty("StartFrame", flags)
                            .GetValue(label),
                        (int)labelType.GetProperty("EndFrameInclusive", flags)
                            .GetValue(label),
                        (bool?)labelType.GetProperty("IsSupport", flags)
                            .GetValue(label)));
                }
                FrameMetrics sideMetrics =
                    ComputeFrameMetrics(sideMask, sideLabels);
                combined.TruePositives += sideMetrics.TruePositives;
                combined.FalsePositives += sideMetrics.FalsePositives;
                combined.FalseNegatives += sideMetrics.FalseNegatives;
            }
            return combined;
        }

        /// <summary>
        /// 클립별 의도 추정 커버리지를 덤프함. 게이트 초과 클립에서
        /// Confident 의도가 얼마나 생성되는지 satisfaction_2와 비교하는 용도.
        /// `-executeMethod Fbx2Vmd.FBXImporter.FbxIntentCalibration.DumpIntents`
        /// 인자: -clipList(; 또는 | 구분), -outputDir
        /// </summary>
        public static void DumpIntents()
        {
            string outputDir = Path.Combine(
                Directory.GetCurrentDirectory(), "Docs", "Workflow", "Local",
                "intent-coverage", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            try
            {
                string list = GetArgument("-clipList");
                string[] clipPaths = string.IsNullOrEmpty(list)
                    ? new[] { GetArgument("-clipPath") ??
                        FbxDiagnostics.DefaultClipPath }
                    : list.Split(new[] { ';', '|' },
                            StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim()).ToArray();
                outputDir = GetArgument("-outputDir") ?? outputDir;
                Directory.CreateDirectory(outputDir);

                var lines = new List<string> {
                    "clip,frames,left_confident_frames,left_uncertain_frames," +
                    "left_intents,right_confident_frames,right_uncertain_frames," +
                    "right_intents,uncertain_spans," +
                    "left_contact_frames,left_covered_contact_frames," +
                    "right_contact_frames,right_covered_contact_frames" };
                foreach (string clipPath in clipPaths)
                {
                    Array samples = SampleClipContacts(clipPath,
                        out float frameRate, out float humanScale);
                    object estimate = InvokeEstimate(samples, frameRate,
                        humanScale, LoadLabelSet(Path.GetFileName(clipPath)));
                    string row = SummarizeIntents(clipPath, samples.Length,
                        estimate, samples, frameRate);
                    lines.Add(row);
                    Debug.Log($"[FbxIntentCalibration] {row}");
                }
                File.WriteAllLines(Path.Combine(outputDir,
                    "intent-coverage.csv"), lines, new UTF8Encoding(false));
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(outputDir);
                File.WriteAllText(Path.Combine(outputDir, "error.txt"),
                    error.ToString());
                Debug.LogError($"[FbxIntentCalibration] 커버리지 실패: {error.Message}");
                EditorApplication.Exit(2);
            }
        }

        // 라벨을 반영한(프로덕션과 동일한) 기본 튜닝 추정을 리플렉션으로 호출함.
        private static object InvokeEstimate(Array samples, float frameRate,
            float humanScale, object labelSet)
        {
            Type estimatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentEstimator", true);
            MethodInfo estimate = estimatorType.GetMethods(
                    BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "Estimate" && m.GetParameters().Length == 4);
            if (estimate == null)
                throw new InvalidOperationException("Estimate 4인자 오버로드가 없습니다.");
            try
            {
                return estimate.Invoke(null,
                    new object[] { samples, frameRate, humanScale, labelSet });
            }
            catch (TargetInvocationException error)
            {
                throw new InvalidOperationException(
                    $"의도 추정 실패: {error.InnerException?.Message ?? error.Message}",
                    error.InnerException);
            }
        }

        // 추정 결과를 발별 Confident/Uncertain 프레임 수와 구간 수로 요약하고,
        // 품질 지표가 보는 안정 접촉 프레임 중 Confident 의도로 덮인 비율도 셈.
        private static string SummarizeIntents(string clipPath, int frameCount,
            object estimate, Array samples, float frameRate)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic;
            var counts = new List<int>();
            int uncertainSpanTotal = 0;
            foreach (string side in new[] { "Left", "Right" })
            {
                int confident = 0, uncertain = 0, intentCount = 0;
                var intents = (System.Collections.IEnumerable)estimate.GetType()
                    .GetProperty(side, flags).GetValue(estimate);
                foreach (object intent in intents)
                {
                    intentCount++;
                    Type type = intent.GetType();
                    int start = Mathf.Max(0, (int)type
                        .GetProperty("StartFrame", flags).GetValue(intent));
                    int end = Mathf.Min(frameCount, (int)type
                        .GetProperty("EndFrameExclusive", flags).GetValue(intent));
                    string certainty = type.GetProperty("Certainty", flags)
                        .GetValue(intent).ToString();
                    int length = Mathf.Max(0, end - start);
                    if (certainty == "Confident") confident += length;
                    else uncertain += length;
                }
                counts.Add(confident);
                counts.Add(uncertain);
                counts.Add(intentCount);
                var spans = (System.Collections.IEnumerable)estimate.GetType()
                    .GetProperty(side + "UncertainSpans", flags)
                    .GetValue(estimate);
                uncertainSpanTotal += spans?.Cast<object>().Count() ?? 0;
            }

            // 접촉 프레임(드리프트 지표 기준) 중 Confident 의도 커버 비율.
            // 커버리지가 낮으면 보정이 Free 추종으로 흘러 게이트가 터짐.
            var contactCoverage = new List<int>();
            foreach (string side in new[] { "Left", "Right" })
            {
                bool[] contact = FbxDiagnostics.DetectStableContactFrames(
                    Midpoints(samples, side == "Left"), frameRate);
                var confidentMask = new bool[frameCount];
                var intents = (System.Collections.IEnumerable)estimate.GetType()
                    .GetProperty(side, flags).GetValue(estimate);
                foreach (object intent in intents)
                {
                    Type type = intent.GetType();
                    if (type.GetProperty("Certainty", flags).GetValue(intent)
                            .ToString() != "Confident")
                        continue;
                    int start = Mathf.Max(0, (int)type
                        .GetProperty("StartFrame", flags).GetValue(intent));
                    int end = Mathf.Min(frameCount, (int)type
                        .GetProperty("EndFrameExclusive", flags).GetValue(intent));
                    for (int i = start; i < end; i++) confidentMask[i] = true;
                }
                int contactFrames = 0, covered = 0;
                for (int i = 0; i < Mathf.Min(contact.Length, frameCount); i++)
                {
                    if (!contact[i]) continue;
                    contactFrames++;
                    if (confidentMask[i]) covered++;
                }
                contactCoverage.Add(contactFrames);
                contactCoverage.Add(covered);
            }
            return string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                Path.GetFileName(clipPath), frameCount,
                counts[0], counts[1], counts[2],
                counts[3], counts[4], counts[5], uncertainSpanTotal,
                contactCoverage[0], contactCoverage[1],
                contactCoverage[2], contactCoverage[3]);
        }

        // 표본 배열에서 해당 발의 (foot+toes)/2 중점 궤적을 추출함.
        // 품질 지표와 같은 접촉점 정의를 씀.
        private static Vector3[] Midpoints(Array samples, bool isLeft)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic;
            var points = new Vector3[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                object sample = samples.GetValue(i);
                Type type = sample.GetType();
                Vector3 foot = (Vector3)type.GetProperty(
                    isLeft ? "LeftFoot" : "RightFoot", flags)
                    .GetValue(sample);
                Vector3 toes = (Vector3)type.GetProperty(
                    isLeft ? "LeftToes" : "RightToes", flags)
                    .GetValue(sample);
                points[i] = (foot + toes) * 0.5f;
            }
            return points;
        }

        private static int CountLabels(object labelSet)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic;
            var property = labelSet.GetType()
                .GetProperty("RowCount", flags);
            if (property != null) return (int)property.GetValue(labelSet);
            int count = 0;
            foreach (string side in new[] { "Left", "Right" })
            {
                foreach (object _ in (System.Collections.IEnumerable)
                    labelSet.GetType().GetProperty(side, flags)
                        .GetValue(labelSet))
                {
                    count++;
                }
            }
            return count;
        }

        // 라벨 스토어는 본체 어셈블리의 internal — 리플렉션으로 로드함.
        private static object LoadLabelSet(string sourceKey)
        {
            Type storeType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentLabelStore",
                true);
            return storeType.GetMethod("Load",
                    BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { sourceKey });
        }

        // 클립의 양발 발목·발끝 위치를 프레임별로 샘플링해 표본 배열을 만듦.
        private static Array SampleClipContacts(
            string clipPath, out float frameRate, out float humanScale)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__",
                    StringComparison.Ordinal) && c.humanMotion);
            if (clip == null)
                throw new InvalidOperationException(
                    $"Humanoid 클립이 없습니다: {clipPath}");
            GameObject asset =
                AssetDatabase.LoadAssetAtPath<GameObject>(clipPath);
            if (asset == null)
                throw new InvalidOperationException(
                    $"클립 FBX를 찾지 못했습니다: {clipPath}");
            GameObject instance = UnityEngine.Object.Instantiate(asset);
            instance.hideFlags = HideFlags.HideAndDontSave;
            object controller = null;
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null ||
                    !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException("Humanoid Avatar가 필요합니다.");

                var flags = BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic;
                Type controllerType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController", true);
                controller = Activator.CreateInstance(controllerType,
                    nonPublic: true);
                controllerType.GetMethod("Prepare", flags)
                    .Invoke(controller, new object[] { animator, clip });
                MethodInfo seek = controllerType.GetMethod("Seek", flags);
                Transform lf = RequireBone(animator, HumanBodyBones.LeftFoot);
                Transform lt = RequireBone(animator, HumanBodyBones.LeftToes);
                Transform rf = RequireBone(animator, HumanBodyBones.RightFoot);
                Transform rt = RequireBone(animator, HumanBodyBones.RightToes);

                frameRate = clip.frameRate > 0f ? clip.frameRate : 60f;
                int total = Mathf.CeilToInt(clip.length * frameRate) + 1;
                Type sampleType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.HumanoidFootContactSample", true);
                Array samples = Array.CreateInstance(sampleType, total);
                for (int frame = 0; frame < total; frame++)
                {
                    float time = Mathf.Min(frame / frameRate, clip.length);
                    if (!(bool)seek.Invoke(controller, new object[] { time }))
                        throw new InvalidOperationException(
                            $"{frame}프레임 탐색 실패");
                    samples.SetValue(Activator.CreateInstance(sampleType,
                        flags, null,
                        new object[] { lf.position, lt.position,
                            rf.position, rt.position }, null), frame);
                }
                // 임계는 키 비율에 선형 스케일되므로 프로덕션과 같은 소스
                // 아바타 humanScale을 써야 탐색 결과가 그대로 이식됨.
                humanScale = animator.humanScale;
                return samples;
            }
            finally
            {
                if (controller != null)
                {
                    try
                    {
                        controller.GetType().GetMethod("Dispose",
                            BindingFlags.Instance | BindingFlags.Public |
                            BindingFlags.NonPublic)?.Invoke(controller, null);
                    }
                    catch { /* 정리 중 예외는 삼킴 */ }
                }
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Transform RequireBone(Animator animator,
            HumanBodyBones bone)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null)
                throw new InvalidOperationException($"{bone} 본이 없습니다.");
            return t;
        }

        private static void WriteResult(string outputDir, string sourceKey,
            int labelRows, int frameCount, List<string> rows, string status)
        {
            var json = new StringBuilder();
            json.Append("{\"source\":\"")
                .Append(FbxDiagnostics.EscapeJson(sourceKey))
                .Append("\",\"label_rows\":").Append(labelRows)
                .Append(",\"frames\":").Append(frameCount)
                .Append(",\"status\":\"").Append(status)
                .Append("\",\"grid\":[")
                .Append(string.Join(",", rows))
                .Append("]}");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "calibration.json"),
                json.ToString());
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
    }
}
