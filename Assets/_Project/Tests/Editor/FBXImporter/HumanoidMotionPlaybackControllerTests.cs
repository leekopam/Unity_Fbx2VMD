using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidMotionPlaybackControllerTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const float TimeTolerance = 0.0001f;

        [OneTimeSetUp]
        public void EnsureHumanoidClipImport()
        {
            Type configuratorType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator",
                throwOnError: false);
            Assert.That(configuratorType, Is.Not.Null,
                "FBX 기준 입력을 Humanoid로 준비하는 Editor 설정기가 필요합니다.");

            MethodInfo method = configuratorType.GetMethod(
                "EnsureHumanoid",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "EnsureHumanoid 메서드가 필요합니다.");
            method.Invoke(null, new object[] { ClipAssetPath });
        }

        [Test]
        public void Given_PreparedClip_When_TickingWithoutPlay_Then_RemainsReadyAtFirstFrame()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Ready"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0f).Within(TimeTolerance));

                Invoke(controller, "Tick", 1f);

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Ready"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0f).Within(TimeTolerance),
                    "임포트 직후에는 사용자가 재생하기 전까지 시간이 진행되면 안 됩니다.");
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_ReadyClip_When_PlayingAndTicking_Then_AdvancesExactlyOnce()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());
                Assert.That((bool)Invoke(controller, "Play"), Is.True);

                Invoke(controller, "Tick", 0.5f);

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Playing"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0.5f).Within(TimeTolerance));
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_PlayingClip_When_PausingOnce_Then_HoldsUntilSingleResume()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());
                Invoke(controller, "Play");
                Invoke(controller, "Tick", 0.5f);

                Assert.That((bool)Invoke(controller, "Pause"), Is.True);
                Invoke(controller, "Tick", 1f);

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Paused"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0.5f).Within(TimeTolerance));

                Assert.That((bool)Invoke(controller, "Play"), Is.True);
                Invoke(controller, "Tick", 0.25f);

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Playing"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0.75f).Within(TimeTolerance),
                    "일시정지는 한 번의 재생 명령으로 해제되어야 합니다.");
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_PlayingClip_When_Stopping_Then_ReturnsToReadyFirstFrame()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());
                Invoke(controller, "Play");
                Invoke(controller, "Tick", 0.5f);

                Assert.That((bool)Invoke(controller, "Stop"), Is.True);

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Ready"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0f).Within(TimeTolerance));
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_PreparedClip_When_SeekingFrame_Then_EvaluatesExactFrameWithoutPlaying()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());
                float frameRate = ReadProperty<float>(controller, "ClipFrameRate");
                int lastFrameIndex = ReadProperty<int>(controller, "LastFrameIndex");
                int requestedFrameIndex = Math.Min(30, lastFrameIndex);

                Assert.That(frameRate, Is.GreaterThan(0f));
                Assert.That((bool)Invoke(controller, "SeekFrame", requestedFrameIndex), Is.True);
                Assert.That(ReadProperty<int>(controller, "CurrentFrameIndex"),
                    Is.EqualTo(requestedFrameIndex));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(requestedFrameIndex / frameRate).Within(TimeTolerance));
                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Ready"),
                    "프레임 이동만으로 모션이 자동 재생되면 안 됩니다.");
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_SourceHumanoidPose_When_PreparingCorrection_Then_ReducesArmErrorWithoutGeometryChanges()
        {
            GameObject baselineTarget = InstantiateTarget();
            GameObject correctedTarget = InstantiateTarget();
            object baselineController = CreateController();
            object correctedController = CreateController();
            object referencePlayer = CreateEditorReferencePlayer();

            try
            {
                AnimationClip clip = LoadHumanoidClip();
                GameObject sourceModel = LoadSourceModel();
                const float sampleTime = 31.1675f;

                Invoke(referencePlayer, "InitializeFromSourceModel", sourceModel, clip);
                HumanPose sourcePose = SampleReferencePose(referencePlayer, sampleTime);

                Animator baselineAnimator = RequireHumanoidAnimator(baselineTarget);
                Invoke(baselineController, "Prepare", baselineAnimator, clip);
                Invoke(baselineController, "Seek", sampleTime);
                HumanPose baselinePose = CaptureCurrentPose(baselineController);
                Transform[] baselineBones = CaptureHumanoidBones(baselineAnimator);

                Animator correctedAnimator = RequireHumanoidAnimator(correctedTarget);
                Invoke(
                    correctedController,
                    "PrepareWithCanonicalPoseReference",
                    correctedAnimator,
                    clip,
                    sourceModel);
                Invoke(correctedController, "Seek", sampleTime);
                HumanPose correctedPose = CaptureCurrentPose(correctedController);
                Transform[] correctedBones = CaptureHumanoidBones(correctedAnimator);

                float baselineError = CalculateArmMuscleMeanError(sourcePose, baselinePose);
                float correctedError = CalculateArmMuscleMeanError(sourcePose, correctedPose);
                Assert.That(correctedError, Is.LessThan(baselineError * 0.8f),
                    "표준 자세 보정은 직접 Humanoid 재생보다 팔 muscle 오차를 줄여야 합니다.");
                AssertEquivalentGeometry(baselineBones, correctedBones);
            }
            finally
            {
                DisposeController(baselineController);
                DisposeController(correctedController);
                DisposeController(referencePlayer);
                UnityEngine.Object.DestroyImmediate(baselineTarget);
                UnityEngine.Object.DestroyImmediate(correctedTarget);
            }
        }

        private static object CreateController()
        {
            Type controllerType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController",
                throwOnError: false);
            Assert.That(controllerType, Is.Not.Null,
                "명시적 재생 상태를 관리하는 모델 중립 컨트롤러가 필요합니다.");
            return Activator.CreateInstance(controllerType, nonPublic: true);
        }

        private static object CreateEditorReferencePlayer()
        {
            Type playerType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidPoseReferencePlayer",
                throwOnError: false);
            Assert.That(playerType, Is.Not.Null,
                "에디터 Humanoid 기준 자세 재생기가 필요합니다.");
            return Activator.CreateInstance(playerType, nonPublic: true);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static object ReadProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return property.GetValue(target);
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            return (T)ReadProperty(target, propertyName);
        }

        private static void DisposeController(object controller)
        {
            if (controller != null)
            {
                Invoke(controller, "Dispose");
            }
        }

        private static HumanPose CaptureCurrentPose(object controller)
        {
            object[] arguments = { null };
            Assert.That(
                (bool)Invoke(controller, "TryCaptureCurrentPose", arguments),
                Is.True);
            return (HumanPose)arguments[0];
        }

        private static HumanPose SampleReferencePose(object referencePlayer, float timeSeconds)
        {
            var pose = new HumanPose();
            object[] arguments = { timeSeconds, pose };
            Assert.That(
                (bool)Invoke(referencePlayer, "TryEvaluateAt", arguments),
                Is.True);
            return (HumanPose)arguments[1];
        }

        private static float CalculateArmMuscleMeanError(
            HumanPose sourcePose,
            HumanPose targetPose)
        {
            int[] armIndices = Enumerable.Range(0, HumanTrait.MuscleCount)
                .Where(index =>
                    HumanTrait.MuscleName[index].Contains("Arm") ||
                    HumanTrait.MuscleName[index].Contains("Forearm"))
                .ToArray();
            return armIndices.Average(index => Mathf.Abs(
                Mathf.Clamp(sourcePose.muscles[index], -1f, 1f) -
                targetPose.muscles[index]));
        }

        private static Transform[] CaptureHumanoidBones(Animator animator)
        {
            return Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                .Select(index => animator.GetBoneTransform((HumanBodyBones)index))
                .Where(bone => bone != null)
                .ToArray();
        }

        private static void AssertEquivalentGeometry(
            Transform[] expectedBones,
            Transform[] actualBones)
        {
            Assert.That(actualBones.Length, Is.EqualTo(expectedBones.Length));
            for (int index = 0; index < expectedBones.Length; index++)
            {
                Assert.That(
                    Vector3.Distance(
                        actualBones[index].localPosition,
                        expectedBones[index].localPosition),
                    Is.LessThanOrEqualTo(TimeTolerance),
                    $"{actualBones[index].name} localPosition이 보정으로 바뀌면 안 됩니다.");
                Assert.That(
                    Vector3.Distance(
                        actualBones[index].localScale,
                        expectedBones[index].localScale),
                    Is.LessThanOrEqualTo(TimeTolerance),
                    $"{actualBones[index].name} localScale이 보정으로 바뀌면 안 됩니다.");
            }
        }

        private static GameObject InstantiateTarget()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TargetAssetPath);
            Assert.That(source, Is.Not.Null, $"기준 모델을 찾을 수 없습니다: {TargetAssetPath}");

            GameObject target = UnityEngine.Object.Instantiate(source);
            target.name = "Humanoid Motion Playback Controller Target";
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }

        private static Animator RequireHumanoidAnimator(GameObject target)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, "기준 모델에 Animator가 필요합니다.");
            Assert.That(animator.avatar, Is.Not.Null, "기준 모델에 Avatar가 필요합니다.");
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True,
                "기준 모델은 유효한 Humanoid Avatar를 사용해야 합니다.");
            return animator;
        }

        private static AnimationClip LoadHumanoidClip()
        {
            AnimationClip clip = AssetDatabase
                .LoadAllAssetsAtPath(ClipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
            Assert.That(clip, Is.Not.Null, $"Humanoid 클립을 찾을 수 없습니다: {ClipAssetPath}");
            return clip;
        }

        private static GameObject LoadSourceModel()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath);
            Assert.That(source, Is.Not.Null,
                $"Humanoid 기준 모델을 찾을 수 없습니다: {ClipAssetPath}");
            return source;
        }
    }
}
