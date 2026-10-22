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
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
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
        public void Given_RecorderNotRecording_When_Tick_Then_DoesNotAdvanceMotion()
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(playback, recorder);
            try
            {
                Invoke(playback, "Play");

                Assert.That((bool)Invoke(controller, "Tick", 0.5f), Is.False);
                Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                    Is.EqualTo(0f).Within(TimeTolerance),
                    "인코더가 녹화 중이 아닐 때 Tick은 모션을 진행하면 안 됩니다 " +
                    "(중복 프레임 원천 차단).");
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_ActiveRecording_When_Ticks_Then_AdvancesExactlyOneOutputFramePerTick()
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(playback, recorder);
            try
            {
                Invoke(controller, "TryStart", CreateSettings(frameRate: 60f), null);

                for (int index = 0; index < 5; index++)
                {
                    // 프레임별 진행량은 입력 dt와 무관하게 정확히 1/60초여야 함(캡처↔모션 1:1).
                    Invoke(controller, "Tick", (index + 1) * 0.3f);
                    Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                        Is.EqualTo((index + 1) / 60f).Within(TimeTolerance),
                        $"틱 {index + 1}은 정확히 {(index + 1)}번째 출력 프레임 자세여야 합니다.");
                }
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_RangeStart_When_StartingRecording_Then_FirstCapturedPoseIsRangeStart()
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(playback, recorder);

            try
            {
                const float rangeStart = 1.5f;
                recorder.BeforeStart = () =>
                    Mathf.Abs(ReadProperty<float>(playback, "CurrentTimeSeconds") - rangeStart) <
                    TimeTolerance &&
                    ReadProperty(playback, "State").ToString() == "Ready";

                bool started = (bool)Invoke(
                    controller,
                    "TryStart",
                    CreateSettings(startTimeSeconds: rangeStart),
                    null);

                Assert.That(started, Is.True);
                Assert.That(recorder.WasReadyAtStart, Is.True,
                    "인코더 시작 전에 모션이 구간 시작 자세로 이동해 있어야 합니다.");
                Assert.That(ReadProperty(playback, "State").ToString(), Is.EqualTo("Playing"));
                Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                    Is.EqualTo(rangeStart).Within(TimeTolerance));
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_RangeEnd_When_Ticking_Then_StopsAfterEndPoseRenderOpportunity()
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(playback, recorder);

            try
            {
                const float frameRate = 60f;
                const float rangeStart = 1.0f;
                // 출력 3프레임 뒤에 끝나는 구간 — 시작~종료 자세가 모두 캡처 대상임.
                const float rangeEnd = rangeStart + 3f / frameRate;
                Assert.That((bool)Invoke(
                    controller,
                    "TryStart",
                    CreateSettings(
                        frameRate,
                        startTimeSeconds: rangeStart,
                        endTimeSeconds: rangeEnd),
                    null), Is.True);

                for (int step = 1; step <= 3; step++)
                {
                    Assert.That((bool)Invoke(controller, "Tick", 1f), Is.False);
                    Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                        Is.EqualTo(rangeStart + step / frameRate).Within(TimeTolerance));
                }

                Assert.That(recorder.IsRecording, Is.True,
                    "종료 자세의 렌더 기회 전에 녹화를 종료하면 안 됩니다.");
                Assert.That((bool)Invoke(controller, "Tick", 1f), Is.True);
                Assert.That(recorder.IsRecording, Is.False);
                Assert.That(recorder.StopCount, Is.EqualTo(1));
                Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"), Is.Zero);
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [TestCase(5f, 5f)]
        [TestCase(5f, 4f)]
        public void Given_InvalidRange_When_StartingRecording_Then_FailsWithoutRecorderStart(
            float startTimeSeconds,
            float endTimeSeconds)
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(playback, recorder);

            try
            {
                object[] arguments =
                {
                    CreateSettings(
                        startTimeSeconds: startTimeSeconds,
                        endTimeSeconds: endTimeSeconds),
                    null
                };
                bool started = (bool)Invoke(controller, "TryStart", arguments);

                Assert.That(started, Is.False);
                Assert.That(arguments[1], Is.Not.Null.And.Not.Empty);
                Assert.That(recorder.Calls, Is.Empty,
                    "무효 구간은 인코더 준비 자체를 시작하면 안 됩니다.");
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
        public void Given_RangeEndBeyondClip_When_StartingRecording_Then_RecordsToClipEnd()
        {
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);
            var recorder = new RecorderProbe();
            object controller = CreateRecordingController(playback, recorder);

            try
            {
                float clipLength = ReadProperty<float>(playback, "ClipLengthSeconds");
                Assert.That((bool)Invoke(
                    controller,
                    "TryStart",
                    CreateSettings(
                        startTimeSeconds: 1f,
                        endTimeSeconds: clipLength + 100f),
                    null), Is.True);

                Assert.That((bool)Invoke(controller, "Tick", 1f), Is.False);
                Assert.That(ReadProperty<float>(playback, "CurrentTimeSeconds"),
                    Is.EqualTo(1f + 1f / 60f).Within(TimeTolerance));
            }
            finally
            {
                DisposeIfPresent(controller);
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_ClipWithLeadingStill_When_LocatingMotionStart_Then_ReturnsPositiveTime()
        {
            AnimationClip clip = LoadHumanoidClip();
            bool found = TryLocateMotionStart(clip, out float motionStart);

            TestContext.WriteLine(
                $"[LeadingStill] clip={clip.name}, detectedMotionStart={motionStart:F3}s, " +
                $"clipLength={clip.length:F3}s");
            Assert.That(found, Is.True, "satisfaction_2 클립은 선두 정지 구간이 있어야 합니다.");
            // 실측 프로파일 기준 안무 시작은 약 7초 — 감지가 엉뚱한 시각을 반환하지 않는지 확인함.
            Assert.That(motionStart, Is.GreaterThan(1f).And.LessThan(30f));
        }

        [Test]
        public void Given_ConstantCurveClip_When_LocatingMotionStart_Then_ReturnsFalse()
        {
            var clip = new AnimationClip { frameRate = 30f };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(6f, 0f)));

            bool found = TryLocateMotionStart(clip, out _);

            Assert.That(found, Is.False, "전 구간 정지 클립은 건너뛸 구간이 없어야 합니다.");
            UnityEngine.Object.DestroyImmediate(clip);
        }

        [Test]
        public void Given_StillThenMovingClip_When_LocatingMotionStart_Then_DetectsStillEnd()
        {
            var clip = new AnimationClip { frameRate = 30f };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(2f, 0f),
                    new Keyframe(2.3f, 5f),
                    new Keyframe(6f, 5f)));

            bool found = TryLocateMotionStart(clip, out float motionStart);

            // 2.1~2.3s에서 활동 임계를 연속 3회 넘기므로 상승 직전인 ~2.0s를 정지 끝으로 반환함.
            Assert.That(found, Is.True);
            Assert.That(motionStart, Is.EqualTo(2f).Within(0.15f));
            UnityEngine.Object.DestroyImmediate(clip);
        }

        [Test]
        public void Given_ImmediatelyMovingClip_When_LocatingMotionStart_Then_ReturnsFalse()
        {
            var clip = new AnimationClip { frameRate = 30f };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 5f)));

            bool found = TryLocateMotionStart(clip, out _);

            Assert.That(found, Is.False,
                "처음부터 움직이는 클립은 건너뛸 준비 구간이 없어야 합니다.");
            UnityEngine.Object.DestroyImmediate(clip);
        }

        [Test]
        public void Given_MotionThenStillThenMotionClip_When_LocatingMotionStart_Then_ReturnsFalse()
        {
            // 선두 트랜지언트가 아니라 실제 움직임(0.8s) 뒤의 중간 홀드는
            // 선두 정지 구간이 아니므로 스킵하지 않아야 함 — 격리 리뷰 F1 회귀.
            var clip = new AnimationClip { frameRate = 30f };
            foreach (string axis in new[] { "x", "y", "z" })
            {
                clip.SetCurve(
                    string.Empty,
                    typeof(Transform),
                    $"localPosition.{axis}",
                    new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.8f, 4f),
                        new Keyframe(1.5f, 4f),
                        new Keyframe(1.8f, 7f)));
            }

            bool found = TryLocateMotionStart(clip, out _);

            Assert.That(found, Is.False,
                "움직임-정지-움직임 클립의 중간 홀드를 선두 준비 구간으로 오인하면 안 됩니다.");
            UnityEngine.Object.DestroyImmediate(clip);
        }

        [Test]
        public void Given_ShortTransientThenStillThenMotionClip_When_LocatingMotionStart_Then_DetectsStillEnd()
        {
            // 짧은 정착 트랜지언트(≤0.6s) 뒤의 정지 구간은 선두 준비 구간으로 인정해야 함.
            var clip = new AnimationClip { frameRate = 30f };
            foreach (string axis in new[] { "x", "y", "z" })
            {
                clip.SetCurve(
                    string.Empty,
                    typeof(Transform),
                    $"localPosition.{axis}",
                    new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.3f, 3f),
                        new Keyframe(1.0f, 3f),
                        new Keyframe(1.3f, 6f)));
            }

            bool found = TryLocateMotionStart(clip, out float motionStart);

            Assert.That(found, Is.True);
            Assert.That(motionStart, Is.EqualTo(1f).Within(0.25f));
            UnityEngine.Object.DestroyImmediate(clip);
        }

        [Test]
        public void Given_PipelineRangeSettings_When_ResolvingRange_Then_ConvertsMotionFramesToSeconds()
        {
            GameObject host = new GameObject("RangeResolveTest", typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline));
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);

            try
            {
                var pipeline = host.GetComponent<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();
                SetField(pipeline, "_humanoidMotionPlaybackController", playback);
                SetField(pipeline, "_motionVideoRangeEnabled", true);
                SetField(pipeline, "_motionVideoRangeStartFrame", 30);
                SetField(pipeline, "_motionVideoRangeEndFrame", 90);

                object[] arguments = { 0f, 0f, null };
                bool resolved = (bool)Invoke(
                    pipeline,
                    "TryResolveMotionVideoRangeSeconds",
                    arguments);

                float clipFrameRate = ReadProperty<float>(playback, "ClipFrameRate");
                Assert.That(resolved, Is.True, (string)arguments[2]);
                Assert.That((float)arguments[0],
                    Is.EqualTo(30f / clipFrameRate).Within(TimeTolerance));
                Assert.That((float)arguments[1],
                    Is.EqualTo(90f / clipFrameRate).Within(TimeTolerance));
            }
            finally
            {
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Given_PipelineRangeWithReversedFrames_When_ResolvingRange_Then_FailsWithMessage()
        {
            GameObject host = new GameObject("RangeResolveTest", typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline));
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);

            try
            {
                var pipeline = host.GetComponent<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();
                SetField(pipeline, "_humanoidMotionPlaybackController", playback);
                SetField(pipeline, "_motionVideoRangeEnabled", true);
                SetField(pipeline, "_motionVideoRangeStartFrame", 90);
                SetField(pipeline, "_motionVideoRangeEndFrame", 30);

                object[] arguments = { 0f, 0f, null };
                bool resolved = (bool)Invoke(
                    pipeline,
                    "TryResolveMotionVideoRangeSeconds",
                    arguments);

                Assert.That(resolved, Is.False);
                Assert.That(arguments[2], Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Given_PipelineSkipStill_When_ResolvingRange_Then_StartsAtDetectedMotion()
        {
            GameObject host = new GameObject("RangeResolveTest", typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline));
            GameObject target = InstantiateTarget();
            object playback = CreatePlaybackController(target);

            try
            {
                var pipeline = host.GetComponent<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();
                SetField(pipeline, "_humanoidMotionPlaybackController", playback);
                // 구간 녹화 없이 선두 정지 스킵만 켠 기본 조합을 재현함.
                SetField(pipeline, "_motionVideoRangeEnabled", false);
                SetField(pipeline, "_motionVideoSkipLeadingStill", true);

                object[] arguments = { 0f, 0f, null };
                bool resolved = (bool)Invoke(
                    pipeline,
                    "TryResolveMotionVideoRangeSeconds",
                    arguments);

                float clipLength = ReadProperty<float>(playback, "ClipLengthSeconds");
                Assert.That(resolved, Is.True, (string)arguments[2]);
                Assert.That((float)arguments[0],
                    Is.GreaterThan(0f).And.LessThan(clipLength),
                    "선두 정지 감지가 동작하면 시작 시각이 0보다 커야 합니다.");
            }
            finally
            {
                DisposeIfPresent(playback);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static bool TryLocateMotionStart(AnimationClip clip, out float motionStart)
        {
            Type locatorType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionLeadingStillLocator",
                throwOnError: true);
            MethodInfo method = locatorType.GetMethod(
                "TryFindMotionStartTime",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { clip, 0f };
            bool found = (bool)method.Invoke(null, arguments);
            motionStart = (float)arguments[1];
            return found;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            field.SetValue(target, value);
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
        public void Given_WebmFormat_When_RecordingStartsAndStops_Then_AlphaHiddenRenderersAreRestored()
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
                    CreateSettings(format: MotionVideoFileFormat.WebM),
                    null);

                Assert.That(started, Is.True);
                Assert.That(hiddenRenderer.enabled, Is.False,
                    "WebM 알파 녹화 중에는 숨김 대상 렌더러가 꺼져야 합니다.");

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
        public void Given_MainAutoScene_When_ReadingAlphaHiddenObjects_Then_ContainsScenePlaneRenderer() =>
            AssertSceneWiresPlaneToAlphaHiddenObjects(MainAutoScenePath, "Main_Auto");

        [Test]
        public void Given_MainRecodingScene_When_ReadingAlphaHiddenObjects_Then_ContainsScenePlaneRenderer() =>
            AssertSceneWiresPlaneToAlphaHiddenObjects(MainRecordingScenePath, "Main_Recoding");

        private static void AssertSceneWiresPlaneToAlphaHiddenObjects(string scenePath, string sceneLabel)
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            try
            {
                var pipeline = UnityEngine.Object.FindFirstObjectByType<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();
                Assert.That(pipeline, Is.Not.Null,
                    $"{sceneLabel} 씬에서 영상 녹화 파이프라인을 찾아야 합니다.");

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
            MotionVideoFileFormat format = MotionVideoFileFormat.Mp4,
            float startTimeSeconds = 0f,
            float endTimeSeconds = 0f)
        {
            return new MotionVideoRecordingSettings(
                "motion",
                1920,
                1080,
                frameRate,
                format: format,
                startTimeSeconds: startTimeSeconds,
                endTimeSeconds: endTimeSeconds);
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
