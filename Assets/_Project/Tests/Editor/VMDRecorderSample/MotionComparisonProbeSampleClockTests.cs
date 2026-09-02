using NUnit.Framework;
using System.Reflection;

namespace Tests.Editor.VMDRecorderSample
{
    public class MotionComparisonProbeSampleClockTests
    {
        [Test]
        public void Given_HeadWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock()
        {
            MethodInfo method = typeof(MotionComparisonProbe).GetMethod(
                "ResolveDiagnosticSampleClock",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Head-window visual compare sampling must use the primed animation clock so t3/t6/t10 stay on the intended clip time.");

            float clock = (float)method.Invoke(
                null,
                new object[] { true, true, new[] { 0f, 3f, 6f }, 90, 3.025f, 3.0333333f });

            Assert.That(clock, Is.EqualTo(3.025f).Within(0.0001f));
        }

        [Test]
        public void Given_NonZeroClipWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock()
        {
            MethodInfo method = typeof(MotionComparisonProbe).GetMethod(
                "ResolveDiagnosticSampleClock",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            float clock = (float)method.Invoke(
                null,
                new object[] { true, true, new[] { 88f, 91f, 98f }, 90, 91.025f, 3.0333333f });

            Assert.That(clock, Is.EqualTo(91.025f).Within(0.0001f));
        }
    }
}
