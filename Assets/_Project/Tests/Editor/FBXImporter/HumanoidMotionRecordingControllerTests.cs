using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.Recording;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidMotionRecordingControllerTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
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

        [TestCase(24f)]
        [TestCase(60f)]
        public void Given_Recording_When_FrameDurationVaries_Then_UsesOutputFrameTimes(float frameRate)
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(playback, recorder);
            try
            {
                Assert.That((bool)Invoke(controller, "TryStart", CreateSettings(frameRate), null), Is.True);
                Invoke(controller, "Tick", 0f);
                Invoke(controller, "Tick", 0f);
                Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"), Is.Zero);

                float[] durations = { 0.5f, 0.001f, 0.12f, 0.033f };
                for (int index = 0; index < durations.Length; index++)
                {
                    Assert.That((bool)Invoke(controller, "Tick", durations[index]), Is.False);
                    Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                        Is.EqualTo((index + 1) / frameRate).Within(TimeTolerance));
                }

                Invoke(controller, "Stop");
                Invoke(controller, "TryStart", CreateSettings(frameRate), null);
                Invoke(controller, "Tick", 0.2f);
                Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                    Is.EqualTo(1f / frameRate).Within(TimeTolerance), "재녹화는 첫 표본부터 시작해야 합니다.");
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [TestCase(1f, 0f)]
        [TestCase(60f, 1f / 60f)]
        [TestCase(60f, 0.04f)]
        public void Given_Recording_When_LastPoseIsReached_Then_StopsAfterItsRenderOpportunity(
            float frameRate, float testLength)
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(playback, recorder);
            try
            {
                if (testLength > 0f)
                {
                    // 짧은 종료 경계만 평가하여 float 반올림과 부분 프레임을 재현함.
                    playback.GetType().GetProperty("ClipLengthSeconds",
                        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(playback, testLength);
                }
                Invoke(controller, "TryStart", CreateSettings(frameRate), null);
                float length = ReadProperty<float>(playback, "ClipLengthSeconds");
                int lastSample = Mathf.CeilToInt(length * frameRate);
                for (int sample = 1; sample <= lastSample; sample++)
                {
                    Assert.That((bool)Invoke(controller, "Tick", 1f), Is.False);
                    Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                        Is.EqualTo(Mathf.Min(sample / frameRate, length)).Within(TimeTolerance));
                }
                Assert.That(recorder.IsRecording, Is.True, "마지막 자세의 렌더 전에 녹화를 종료하면 안 됩니다.");
                Invoke(controller, "Tick", 0f);
                Assert.That(recorder.IsRecording, Is.True);
                Assert.That((bool)Invoke(controller, "Tick", 1f), Is.True);
                Assert.That(recorder.IsRecording, Is.False);
                Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"), Is.Zero);
                Assert.That(recorder.StopCount, Is.EqualTo(1));
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_MovFormat_When_RecordingStartsAndStops_Then_AlphaHiddenRenderersAreRestored()
        {
            GameObject target = InstantiateTarget();
            GameObject hidden = CreateHiddenRendererTarget(out Renderer hiddenRenderer);
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(
                playback, recorder, new[] { hiddenRenderer });

            try
            {
                bool started = (bool)Invoke(
                    controller,
                    "TryStart",
                    CreateSettings(format: MotionVideoFileFormat.MovProRes),
                    null);

                Assert.That(started, Is.True);
                Assert.That(hiddenRenderer.enabled, Is.False,
                    "MOV 녹화 중에는 숨김 대상 렌더러가 꺼져야 합니다.");

                Invoke(controller, "Stop");
                Assert.That(hiddenRenderer.enabled, Is.True,
                    "녹화 종료 후에는 숨김 대상 렌더러가 복원되어야 합니다.");
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(hidden);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_Mp4Format_When_RecordingStarts_Then_AlphaHiddenRenderersStayEnabled()
        {
            GameObject target = InstantiateTarget();
            GameObject hidden = CreateHiddenRendererTarget(out Renderer hiddenRenderer);
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(
                playback, recorder, new[] { hiddenRenderer });

            try
            {
                bool started = (bool)Invoke(
                    controller,
                    "TryStart",
                    CreateSettings(format: MotionVideoFileFormat.Mp4),
                    null);

                Assert.That(started, Is.True);
                Assert.That(hiddenRenderer.enabled, Is.True,
                    "MP4 녹화에서는 숨김 대상 렌더러가 유지되어야 합니다.");

                Invoke(controller, "Stop");
                Assert.That(hiddenRenderer.enabled, Is.True);
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(hidden);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_MovRecording_When_DisposingController_Then_AlphaHiddenRenderersAreRestored()
        {
            GameObject target = InstantiateTarget();
            GameObject hidden = CreateHiddenRendererTarget(out Renderer hiddenRenderer);
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(
                playback, recorder, new[] { hiddenRenderer });

            try
            {
                bool started = (bool)Invoke(
                    controller,
                    "TryStart",
                    CreateSettings(format: MotionVideoFileFormat.MovProRes),
                    null);

                Assert.That(started, Is.True);
                Assert.That(hiddenRenderer.enabled, Is.False);

                Invoke(controller, "Dispose");
                Assert.That(hiddenRenderer.enabled, Is.True,
                    "컨트롤러 해제 시에도 숨김 대상 렌더러가 복원되어야 합니다.");
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(hidden);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_MainAutoScene_When_ReadingAlphaHiddenObjects_Then_ContainsScenePlaneRenderer()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(MainAutoScenePath, OpenSceneMode.Single);

            try
            {
                var pipeline = UnityEngine.Object.FindObjectOfType<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();
                Assert.That(pipeline, Is.Not.Null,
                    "Main_Auto 씬에서 영상 녹화 파이프라인을 찾아야 합니다.");

                GameObject[] hiddenObjects = pipeline.alphaRecordingHiddenObjects;
                Assert.That(hiddenObjects, Is.Not.Null.And.Not.Empty,
                    "MOV 녹화 시 숨길 오브젝트가 씬에 지정되어야 합니다.");
                Assert.That(
                    hiddenObjects.Any(candidate => candidate != null && candidate.name == "Plane"),
                    Is.True,
                    "숨김 대상에 바닥 Plane이 포함되어야 합니다.");

                MethodInfo collectMethod = pipeline.GetType().GetMethod(
                    "CollectAlphaHiddenRenderers",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(collectMethod, Is.Not.Null);
                var renderers = (Renderer[])collectMethod.Invoke(pipeline, null);
                Assert.That(renderers, Is.Not.Null.And.Not.Empty);
                Assert.That(
                    renderers.Any(renderer =>
                        renderer != null && renderer.gameObject.name == "Plane"),
                    Is.True,
                    "숨김 대상 수집에서 Plane 렌더러가 반환되어야 합니다.");
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousScenePath))
                {
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                }
            }
        }

        private static GameObject CreateHiddenRendererTarget(out Renderer renderer)
        {
            var target = new GameObject("AlphaHidden", typeof(MeshRenderer));
            target.hideFlags = HideFlags.HideAndDontSave;
            renderer = target.GetComponent<MeshRenderer>();
            return target;
        }

        private static MotionVideoRecordingSettings CreateSettings(
            float frameRate = 60f,
            MotionVideoFileFormat format = MotionVideoFileFormat.Mp4)
        {
            return new MotionVideoRecordingSettings(
                "motion", 1920, 1080, frameRate, format: format);
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

        private static object CreateRecordingController(
            object playback,
            IMotionVideoRecorder recorder,
            Renderer[] alphaHiddenRenderers = null)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionRecordingController",
                throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "재생과 영상 녹화 시작 순서를 관리하는 전용 컨트롤러가 필요합니다.");
            object[] arguments = alphaHiddenRenderers == null
                ? new[] { playback, recorder }
                : new object[] { playback, recorder, alphaHiddenRenderers };
            return Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: arguments,
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
