using NUnit.Framework;

namespace Tests.Editor.VMDRecorderSample
{
    public class HumanoidRecordingSessionTests
    {
        [Test]
        public void Given_FrameBasedSession_When_Tick_Then_UsesFrameNumberForProgressAndFinish()
        {
            var session = new HumanoidRecordingSession(recordingFrameRate: 30f);
            session.Start(totalDurationSeconds: 10f, targetFrameCount: 300, finishByRecordedFrameCount: true);

            HumanoidRecordingTick tick = session.Tick(deltaTimeSeconds: 0.1f, recordedFrameNumber: 150);

            Assert.That(tick.DisplayTimeSeconds, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(tick.Progress01, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(tick.ShouldFinish, Is.False);

            tick = session.Tick(deltaTimeSeconds: 0.1f, recordedFrameNumber: 300);
            Assert.That(tick.ShouldFinish, Is.True);
        }

        [Test]
        public void Given_TimeBasedSession_When_Tick_Then_UsesTimerForProgressAndFinish()
        {
            var session = new HumanoidRecordingSession(recordingFrameRate: 30f);
            session.Start(totalDurationSeconds: 2f, targetFrameCount: 0, finishByRecordedFrameCount: true);

            HumanoidRecordingTick tick = session.Tick(deltaTimeSeconds: 1f, recordedFrameNumber: 100);
            Assert.That(tick.DisplayTimeSeconds, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(tick.Progress01, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(tick.ShouldFinish, Is.False);

            tick = session.Tick(deltaTimeSeconds: 1f, recordedFrameNumber: 200);
            Assert.That(tick.ShouldFinish, Is.True);
        }
    }
}

