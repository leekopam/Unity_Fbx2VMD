using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.Recording;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidMotionRecordingControllerTests
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
                throwOnError: true);
            MethodInfo method = configuratorType.GetMethod(
                "EnsureHumanoid",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { ClipAssetPath });
        }

        [Test]
        public void Given_PreparedMotion_When_StartingRecording_Then_RewindsBeforeRecorderStartAndPlays()
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(playback, recorder);

            try
            {
                Invoke(playback, "Play");
                Invoke(playback, "Tick", 0.5f);
                recorder.BeforeStart = () =>
                    ReadProperty<float>(playback, "CurrentTimeSeconds") == 0f &&
                    ReadProperty(playback, "State").ToString() == "Ready";

                bool started = (bool)Invoke(
                    controller,
                    "TryStart",
                    CreateSettings(),
                    null);

                Assert.That(started, Is.True);
                Assert.That(recorder.Calls, Is.EqualTo(new[] { "prepare", "start" }));
                Assert.That(recorder.WasReadyAtStart, Is.True,
                    "인코더 시작 전 모션이 0초 Ready 상태여야 합니다.");
                Assert.That(ReadProperty(playback, "State").ToString(), Is.EqualTo("Playing"));
                Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                    Is.EqualTo(0f).Within(TimeTolerance));
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Given_RecorderFailure_When_StartingRecording_Then_MotionRemainsReadyAtZero(
            bool failDuringPrepare)
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe
            {
                PrepareSucceeds = !failDuringPrepare,
                StartSucceeds = failDuringPrepare
            };
            object controller = CreateRecordingController(playback, recorder);

            try
            {
                Invoke(playback, "Play");
                Invoke(playback, "Tick", 0.5f);

                object[] arguments = { CreateSettings(), null };
                bool started = (bool)Invoke(controller, "TryStart", arguments);

                Assert.That(started, Is.False);
                Assert.That(arguments[1], Is.Not.Null.And.Not.Empty);
                Assert.That(ReadProperty(playback, "State").ToString(), Is.EqualTo("Ready"));
                Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                    Is.EqualTo(0f).Within(TimeTolerance));
                Assert.That(recorder.IsRecording, Is.False);
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_ActiveRecording_When_Stopping_Then_StopsRecorderAndRewindsMotion()
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(playback, recorder);

            try
            {
                Invoke(controller, "TryStart", CreateSettings(), null);
                Invoke(playback, "Tick", 0.5f);

                Assert.That((bool)Invoke(controller, "Stop"), Is.True);

                Assert.That(recorder.StopCount, Is.EqualTo(1));
                Assert.That(ReadProperty(playback, "State").ToString(), Is.EqualTo("Ready"));
                Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                    Is.EqualTo(0f).Within(TimeTolerance));
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_ActiveRecording_When_PlaybackCompletes_Then_StopsAndRewindsAutomatically()
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(playback, recorder);

            try
            {
                Invoke(controller, "TryStart", CreateSettings(), null);
                float clipLength = ReadProperty<float>(playback, "ClipLengthSeconds");
                Invoke(playback, "Tick", clipLength);

                Assert.That((bool)Invoke(controller, "StopWhenPlaybackCompletes"), Is.True);
                Assert.That(recorder.IsRecording, Is.False);
                Assert.That(ReadProperty(playback, "State").ToString(), Is.EqualTo("Ready"));
                Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                    Is.EqualTo(0f).Within(TimeTolerance));
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static MotionVideoRecordingSettings CreateSettings()
        {
            return new MotionVideoRecordingSettings("motion", 1920, 1080, 60f);
        }

        private static object CreatePlaybackController(GameObject target)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController",
                throwOnError: true);
            object controller = Activator.CreateInstance(type, nonPublic: true);
            Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());
            return controller;
        }

        private static object CreateRecordingController(object playback, IMotionVideoRecorder recorder)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionRecordingController",
                throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "재생과 영상 녹화 시작 순서를 관리하는 전용 컨트롤러가 필요합니다.");
            return Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new[] { playback, recorder },
                culture: null);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static void DisposeIfPresent(object target)
        {
            if (target != null)
            {
                Invoke(target, "Dispose");
            }
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

        private static GameObject InstantiateTarget()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TargetAssetPath);
            Assert.That(source, Is.Not.Null);
            GameObject target = UnityEngine.Object.Instantiate(source);
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }

        private static Animator RequireHumanoidAnimator(GameObject target)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman,
                Is.True);
            return animator;
        }

        private static AnimationClip LoadHumanoidClip()
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
            Assert.That(clip, Is.Not.Null);
            return clip;
        }

        private sealed class RecorderProbe : IMotionVideoRecorder
        {
            public bool PrepareSucceeds { get; set; } = true;
            public bool StartSucceeds { get; set; } = true;
            public Func<bool> BeforeStart { get; set; }
            public System.Collections.Generic.List<string> Calls { get; } =
                new System.Collections.Generic.List<string>();
            public bool WasReadyAtStart { get; private set; }
            public int StopCount { get; private set; }
            public bool IsRecording { get; private set; }
            public string OutputFilePath => "recording.mp4";

            public bool TryPrepare(
                MotionVideoRecordingSettings settings,
                out string errorMessage)
            {
                Calls.Add("prepare");
                errorMessage = PrepareSucceeds ? string.Empty : "prepare failed";
                return PrepareSucceeds;
            }

            public bool TryStart(out string errorMessage)
            {
                Calls.Add("start");
                WasReadyAtStart = BeforeStart?.Invoke() ?? true;
                IsRecording = StartSucceeds;
                errorMessage = StartSucceeds ? string.Empty : "start failed";
                return StartSucceeds;
            }

            public void Stop()
            {
                StopCount++;
                IsRecording = false;
            }

            public void Dispose()
            {
                IsRecording = false;
            }
        }
    }
}
