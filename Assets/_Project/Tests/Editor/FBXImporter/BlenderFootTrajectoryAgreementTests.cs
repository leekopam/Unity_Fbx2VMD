using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    /// <summary>
    /// Blender가 FBX 원본에서 독립 샘플링한 발 궤적과 Unity 쪽 원본 궤적이
    /// 같은 접지 의도 구간을 만들어내는지 대조한다. Blender 덤프는
    /// Tools/BlenderFootDump/dump_foot_trajectory.py로 생성한다.
    /// </summary>
    public class BlenderFootTrajectoryAgreementTests
    {
        private const string ClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string DumpDirectory = "Artifacts/blender";
        // 프레임 단위 접촉 라벨의 최소 합의율 — 기존 FootSupportContactAgreement와 같은 기준.
        private const float MinimumContactF1 = 0.7f;
        // 정규화한 발 높이 궤적의 허용 RMS 오차(humanScale 단위).
        private const float MaxNormalizedHeightRms = 0.08f;

        private delegate bool SeekDelegate(float timeSeconds);

        [Test]
        public void Given_BlenderDump_When_ComparingIntents_Then_IntervalBoundariesAgree()
        {
            RequireFixtures(out string dumpPath);
            EnsureHumanoidClipImport();

            BlenderDump dump = LoadDump(dumpPath);
            GameObject sourceTarget = InstantiateAsset(ClipAssetPath, "Blender Agreement Source");
            object sourceController = CreateController();
            try
            {
                AnimationClip clip = LoadHumanoidClip();
                Animator animator = RequireHumanoidAnimator(sourceTarget);
                Invoke(sourceController, "Prepare", animator, clip);
                SeekDelegate seek = CreateSeekDelegate(sourceController);

                float frameRate = clip.frameRate > 0f ? clip.frameRate : 60f;
                int frameCount = Mathf.CeilToInt(clip.length * frameRate) + 1;
                var unitySamples = SampleUnityTrajectory(
                    seek, animator, frameCount, frameRate, clip.length);
                var blenderSamples = ResampleBlenderTrajectory(
                    dump, frameCount, frameRate, clip.length);

                object unityEstimate = RunEstimator(
                    unitySamples, frameRate, animator.humanScale);
                object blenderEstimate = RunEstimator(
                    blenderSamples, frameRate, dump.HumanScale);

                float heightRms = NormalizedHeightRms(
                    unitySamples, blenderSamples, animator.humanScale);

                Debug.Log(
                    "[BlenderFootAgreement] " +
                    $"clip={dump.SourceFile}, frames={frameCount}, fps={frameRate}, " +
                    $"unityScale={animator.humanScale:F4}, " +
                    $"blenderScale={dump.HumanScale:F4}, up={dump.UpAxis}, " +
                    $"heightRms={heightRms:F4}, " +
                    $"unityIntents={IntentSummary(unityEstimate)}, " +
                    $"blenderIntents={IntentSummary(blenderEstimate)}");

                Assert.That(heightRms, Is.LessThanOrEqualTo(MaxNormalizedHeightRms),
                    "Blender와 Unity가 샘플링한 발 높이 궤적이 다릅니다. " +
                    "임포터 해석 차이 또는 좌표 매핑 오류를 확인하세요.");

                foreach (bool isLeft in new[] { true, false })
                {
                    string side = isLeft ? "Left" : "Right";
                    string sideLabel = isLeft ? "왼발" : "오른발";
                    bool[] unityContact = Rasterize(
                        IntentIntervals(unityEstimate, side), frameCount);
                    bool[] blenderContact = Rasterize(
                        IntentIntervals(blenderEstimate, side), frameCount);
                    float f1 = ContactF1(unityContact, blenderContact,
                        out int unityCount, out int blenderCount, out int both);
                    Debug.Log(
                        $"[BlenderFootAgreement] {sideLabel} F1={f1:F4} " +
                        $"unity={unityCount} blender={blenderCount} both={both}");
                    Assert.That(f1, Is.GreaterThanOrEqualTo(MinimumContactF1),
                        $"{sideLabel} 접촉 라벨이 Blender 정답지와 다릅니다. " +
                        $"F1={f1:F4}, unity={unityCount}, blender={blenderCount}, " +
                        $"both={both}, " +
                        $"unity구간={string.Join(",", IntentIntervals(unityEstimate, side).Select(s => $"[{s.x},{s.y})"))}, " +
                        $"blender구간={string.Join(",", IntentIntervals(blenderEstimate, side).Select(s => $"[{s.x},{s.y})"))}");
                }
            }
            finally
            {
                DisposeController(sourceController);
                UnityEngine.Object.DestroyImmediate(sourceTarget);
            }
        }

        private static bool[] Rasterize(List<Vector2Int> spans, int frameCount)
        {
            var labels = new bool[frameCount];
            foreach (Vector2Int span in spans)
            {
                int start = Mathf.Max(0, span.x);
                int end = Mathf.Min(frameCount, span.y);
                for (int index = start; index < end; index++)
                {
                    labels[index] = true;
                }
            }
            return labels;
        }

        private static float ContactF1(
            bool[] predicted, bool[] reference,
            out int predictedCount, out int referenceCount, out int bothCount)
        {
            predictedCount = predicted.Count(v => v);
            referenceCount = reference.Count(v => v);
            bothCount = predicted.Where((v, i) => v && reference[i]).Count();
            if (predictedCount + referenceCount == 0)
            {
                return 1f;
            }
            return 2f * bothCount / (predictedCount + referenceCount);
        }

        private void RequireFixtures(out string dumpPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath) == null)
            {
                Assert.Ignore($"로컬 FBX fixture를 찾을 수 없습니다: {ClipAssetPath}");
            }
            string clipName = Path.GetFileNameWithoutExtension(ClipAssetPath);
            dumpPath = Environment.GetEnvironmentVariable("FBX2VMD_BLENDER_DUMP");
            if (string.IsNullOrEmpty(dumpPath))
            {
                dumpPath = Path.Combine(
                    Directory.GetCurrentDirectory(), DumpDirectory,
                    clipName + ".feet.json");
            }
            if (!File.Exists(dumpPath))
            {
                Assert.Ignore(
                    $"Blender 덤프가 없습니다: {dumpPath}\n" +
                    "생성: blender -b --factory-startup " +
                    "-P Tools/BlenderFootDump/dump_foot_trajectory.py -- " +
                    $"--input \"{ClipAssetPath}\" --output \"{dumpPath}\"");
            }
        }

        private sealed class BlenderDump
        {
            internal string SourceFile;
            internal string UpAxis;
            internal float HumanHeight;
            internal float HumanScale;
            internal float[] Times;
            internal Vector3[] LeftFoot;
            internal Vector3[] LeftToes;
            internal Vector3[] RightFoot;
            internal Vector3[] RightToes;
        }

        [Serializable]
        private class DumpDocument
        {
            public string source_file;
            public string up_axis;
            public float human_height;
            public DumpSample[] samples;
        }

        [Serializable]
        private class DumpSample
        {
            public float t;
            public float[] lf;
            public float[] lt;
            public float[] rf;
            public float[] rt;
        }

        private static BlenderDump LoadDump(string path)
        {
            DumpDocument doc = JsonUtility.FromJson<DumpDocument>(File.ReadAllText(path));
            Assert.That(doc, Is.Not.Null, $"Blender 덤프 JSON을 읽지 못했습니다: {path}");
            Assert.That(doc.samples, Is.Not.Null.And.Length.GreaterThan(1),
                "Blender 덤프에 표본이 없습니다.");
            int upIndex = "xyz".IndexOf(doc.up_axis ?? "y");
            Assert.That(upIndex, Is.GreaterThanOrEqualTo(0),
                $"알 수 없는 up_axis입니다: {doc.up_axis}");

            var dump = new BlenderDump
            {
                SourceFile = doc.source_file,
                UpAxis = doc.up_axis,
                HumanHeight = doc.human_height,
                // estimator의 humanScale은 "표준 인체(~1.6)가 데이터 단위로 몇인가"다.
                // cm 단위 덤프(키≈155)면 ≈97, m 단위면 ≈1이 된다.
                HumanScale = doc.human_height / 1.6f,
                Times = new float[doc.samples.Length],
                LeftFoot = new Vector3[doc.samples.Length],
                LeftToes = new Vector3[doc.samples.Length],
                RightFoot = new Vector3[doc.samples.Length],
                RightToes = new Vector3[doc.samples.Length],
            };
            for (int index = 0; index < doc.samples.Length; index++)
            {
                DumpSample sample = doc.samples[index];
                dump.Times[index] = sample.t;
                dump.LeftFoot[index] = ToUnity(sample.lf, upIndex);
                dump.LeftToes[index] = ToUnity(sample.lt, upIndex);
                dump.RightFoot[index] = ToUnity(sample.rf, upIndex);
                dump.RightToes[index] = ToUnity(sample.rt, upIndex);
            }
            return dump;
        }

        private static Vector3 ToUnity(float[] blender, int upIndex)
        {
            // 상방 축을 y로 올리고 나머지 두 축을 x/z로 배치한다.
            // 높이·속도·수평 이동량만 쓰이므로 수평 축의 좌우 대칭은 무의미하다.
            switch (upIndex)
            {
                case 0: return new Vector3(blender[1], blender[0], blender[2]);
                case 1: return new Vector3(blender[0], blender[1], blender[2]);
                default: return new Vector3(blender[0], blender[2], blender[1]);
            }
        }

        private List<Vector3[]> SampleUnityTrajectory(
            SeekDelegate seek, Animator animator, int frameCount,
            float frameRate, float clipLength)
        {
            Transform leftFoot = RequireBone(animator, HumanBodyBones.LeftFoot);
            Transform leftToes = RequireBone(animator, HumanBodyBones.LeftToes);
            Transform rightFoot = RequireBone(animator, HumanBodyBones.RightFoot);
            Transform rightToes = RequireBone(animator, HumanBodyBones.RightToes);
            var result = new List<Vector3[]>(frameCount);
            for (int index = 0; index < frameCount; index++)
            {
                float timeSeconds = Mathf.Min(index / frameRate, clipLength);
                Assert.That(seek(timeSeconds), Is.True);
                result.Add(new[]
                {
                    leftFoot.position, leftToes.position,
                    rightFoot.position, rightToes.position
                });
            }
            return result;
        }

        private List<Vector3[]> ResampleBlenderTrajectory(
            BlenderDump dump, int frameCount, float frameRate, float clipLength)
        {
            // 클립 길이가 크게 어긋나면 fps 해석이 다른 것이므로 경고만 남긴다.
            float dumpDuration = dump.Times[dump.Times.Length - 1];
            if (Mathf.Abs(dumpDuration - clipLength) > clipLength * 0.05f)
            {
                Debug.LogWarning(
                    $"[BlenderFootAgreement] 길이 불일치: dump={dumpDuration:F2}s " +
                    $"clip={clipLength:F2}s. 시간 정렬이 어긋날 수 있습니다.");
            }
            var result = new List<Vector3[]>(frameCount);
            int cursor = 0;
            for (int index = 0; index < frameCount; index++)
            {
                float t = Mathf.Min(index / frameRate, clipLength);
                while (cursor + 1 < dump.Times.Length - 1 && dump.Times[cursor + 1] < t)
                {
                    cursor++;
                }
                int next = Mathf.Min(cursor + 1, dump.Times.Length - 1);
                float span = dump.Times[next] - dump.Times[cursor];
                float blend = span > 0f ? Mathf.Clamp01((t - dump.Times[cursor]) / span) : 0f;
                result.Add(new[]
                {
                    Vector3.LerpUnclamped(dump.LeftFoot[cursor], dump.LeftFoot[next], blend),
                    Vector3.LerpUnclamped(dump.LeftToes[cursor], dump.LeftToes[next], blend),
                    Vector3.LerpUnclamped(dump.RightFoot[cursor], dump.RightFoot[next], blend),
                    Vector3.LerpUnclamped(dump.RightToes[cursor], dump.RightToes[next], blend),
                });
            }
            return result;
        }

        private static object RunEstimator(
            List<Vector3[]> trajectories, float frameRate, float humanScale)
        {
            Type sampleType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactSample", true);
            Type estimatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentEstimator", true);
            ConstructorInfo ctor = sampleType.GetConstructors(
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .First(c => c.GetParameters().Length == 4);
            var samples = (System.Collections.IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(sampleType));
            foreach (Vector3[] frame in trajectories)
            {
                samples.Add(ctor.Invoke(new object[]
                {
                    frame[0], frame[1], frame[2], frame[3]
                }));
            }
            // Estimate에는 Tuning 오버로드가 있어 매개변수 수로 구분한다.
            return estimatorType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .First(m => m.Name == "Estimate" && m.GetParameters().Length == 4)
                .Invoke(null, new object[] { samples, frameRate, humanScale, null });
        }

        private static List<Vector2Int> IntentIntervals(object estimate, string side)
        {
            var list = new List<Vector2Int>();
            foreach (object intent in (System.Collections.IEnumerable)Prop(estimate, side))
            {
                list.Add(new Vector2Int(
                    (int)Prop(intent, "StartFrame"),
                    (int)Prop(intent, "EndFrameExclusive")));
            }
            return list;
        }

        private static string IntentSummary(object estimate)
        {
            string Format(string side) =>
                side + "=" + string.Join(";",
                    IntentIntervals(estimate, side).Select(s => $"[{s.x},{s.y})"));
            return Format("Left") + " " + Format("Right");
        }

        private static object Prop(object target, string name)
        {
            return target.GetType().GetProperty(
                    name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static float NormalizedHeightRms(
            List<Vector3[]> unity, List<Vector3[]> blender, float unityScale)
        {
            // 채널별로 floor(10분위)를 빼고 자체 범위로 나눠 단위·배율 차이를 없앤다.
            double sumSquares = 0;
            int count = 0;
            for (int channel = 0; channel < 4; channel++)
            {
                var unityHeights = unity.Select(f => f[channel].y).ToList();
                var blenderHeights = blender.Select(f => f[channel].y).ToList();
                float floorUnity = Percentile(unityHeights, 0.1f);
                float floorBlender = Percentile(blenderHeights, 0.1f);
                float rangeUnity = Percentile(unityHeights, 0.99f) - floorUnity;
                float rangeBlender = Percentile(blenderHeights, 0.99f) - floorBlender;
                // 양쪽 모두 거의 움직이지 않는 채널은 정규화 노이즈가 커지므로 건너뛴다.
                if (rangeUnity < unityScale * 0.01f && rangeBlender < unityScale * 0.01f)
                {
                    continue;
                }
                rangeUnity = Mathf.Max(rangeUnity, 0.0001f);
                rangeBlender = Mathf.Max(rangeBlender, 0.0001f);
                for (int index = 0; index < unity.Count; index++)
                {
                    float a = (unityHeights[index] - floorUnity) / rangeUnity;
                    float b = (blenderHeights[index] - floorBlender) / rangeBlender;
                    sumSquares += (a - b) * (a - b);
                    count++;
                }
            }
            return count == 0 ? 0f : (float)Math.Sqrt(sumSquares / count);
        }

        private static float Percentile(List<float> values, float percentile)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int index = Mathf.Clamp(
                Mathf.FloorToInt(sorted.Count * percentile), 0, sorted.Count - 1);
            return sorted[index];
        }

        private static void EnsureHumanoidClipImport()
        {
            Type configuratorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator",
                throwOnError: false);
            MethodInfo method = configuratorType?.GetMethod(
                "EnsureHumanoid",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                "로컬 FBX fixture를 Humanoid로 준비하는 설정기가 필요합니다.");
            method.Invoke(null, new object[] { ClipAssetPath });
        }

        private static GameObject InstantiateAsset(string path, string instanceName)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(asset, Is.Not.Null, $"모델 asset을 찾을 수 없습니다: {path}");
            GameObject instance = UnityEngine.Object.Instantiate(asset);
            instance.name = instanceName;
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.SetActive(true);
            return instance;
        }

        private static Animator RequireHumanoidAnimator(GameObject root)
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, $"{root.name}에 Animator가 필요합니다.");
            Assert.That(animator.avatar, Is.Not.Null, $"{root.name}에 Avatar가 필요합니다.");
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True,
                $"{root.name}은 유효한 Humanoid Avatar를 사용해야 합니다.");
            return animator;
        }

        private static AnimationClip LoadHumanoidClip()
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
            Assert.That(clip, Is.Not.Null,
                $"Humanoid clip을 찾을 수 없습니다: {ClipAssetPath}");
            return clip;
        }

        private static Transform RequireBone(Animator animator, HumanBodyBones bone)
        {
            Transform transform = animator.GetBoneTransform(bone);
            Assert.That(transform, Is.Not.Null, $"{bone} Humanoid 본이 필요합니다.");
            return transform;
        }

        private static object CreateController()
        {
            Type controllerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController",
                throwOnError: false);
            Assert.That(controllerType, Is.Not.Null);
            return Activator.CreateInstance(controllerType, nonPublic: true);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static SeekDelegate CreateSeekDelegate(object controller)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "Seek",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (SeekDelegate)method.CreateDelegate(typeof(SeekDelegate), controller);
        }

        private static void DisposeController(object controller)
        {
            if (controller != null)
            {
                Invoke(controller, "Dispose");
            }
        }
    }
}
