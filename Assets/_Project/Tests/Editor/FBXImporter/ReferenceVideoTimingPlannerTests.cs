using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class ReferenceVideoTimingPlannerTests
    {
        [Test]
        public void Given_DisabledTiming_When_Building_Then_UsesCandidateClipTiming()
        {
            object plan = BuildPlan(
                referenceClipLengthSeconds: 60f,
                requestedDurationSeconds: 10f,
                segmentName: "head",
                enabled: false,
                knownReferenceDurationSeconds: 200f);

            Assert.That(Read<bool>(plan, "Enabled"), Is.False);
            Assert.That(Read<bool>(plan, "HasCandidateTimingOverride"), Is.False);
            Assert.That(Read<float>(plan, "ReferenceVideoStartSeconds"), Is.EqualTo(0f));
            Assert.That(Read<float>(plan, "CandidateClipStartSeconds"), Is.EqualTo(0f));
            Assert.That(Read<float>(plan, "CandidateClipSecondsPerReferenceSecond"), Is.EqualTo(1f));
            Assert.That(Read<float>(plan, "ReferenceDurationSeconds"), Is.EqualTo(60f));
        }

        [Test]
        public void Given_EnabledTailTiming_When_Building_Then_MapsReferenceWindowToCandidateClip()
        {
            object plan = BuildPlan(
                referenceClipLengthSeconds: 100f,
                requestedDurationSeconds: 10f,
                segmentName: "tail",
                enabled: true,
                knownReferenceDurationSeconds: 200f);

            Assert.That(Read<bool>(plan, "Enabled"), Is.True);
            Assert.That(Read<bool>(plan, "HasCandidateTimingOverride"), Is.True);
            Assert.That(Read<float>(plan, "ReferenceVideoStartSeconds"), Is.EqualTo(190f));
            Assert.That(Read<float>(plan, "CandidateClipStartSeconds"), Is.EqualTo(95f));
            Assert.That(Read<float>(plan, "CandidateClipSecondsPerReferenceSecond"), Is.EqualTo(0.5f));
            Assert.That(Read<float>(plan, "ReferenceDurationSeconds"), Is.EqualTo(200f));
        }

        [Test]
        public void Given_InvalidReferenceDuration_When_Building_Then_FallsBackWithoutOverride()
        {
            object plan = BuildPlan(
                referenceClipLengthSeconds: 20f,
                requestedDurationSeconds: 5f,
                segmentName: "middle",
                enabled: true,
                knownReferenceDurationSeconds: float.NaN);

            Assert.That(Read<bool>(plan, "Enabled"), Is.False);
            Assert.That(Read<bool>(plan, "HasCandidateTimingOverride"), Is.False);
            Assert.That(Read<float>(plan, "ReferenceVideoStartSeconds"), Is.EqualTo(7.5f));
            Assert.That(Read<float>(plan, "CandidateClipStartSeconds"), Is.EqualTo(7.5f));
            Assert.That(Read<float>(plan, "ReferenceDurationSeconds"), Is.EqualTo(20f));
        }

        private static object BuildPlan(
            float referenceClipLengthSeconds,
            float requestedDurationSeconds,
            string segmentName,
            bool enabled,
            float knownReferenceDurationSeconds)
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type plannerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceVideoTimingPlanner",
                throwOnError: false);
            Assert.That(plannerType, Is.Not.Null, "모델 중립 참조 영상 시간 계획 경계가 필요합니다.");
            MethodInfo build = plannerType.GetMethod(
                "Build",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null);

            Type segmentPlannerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCaptureSegmentPlanner",
                throwOnError: true);
            MethodInfo resolveSegment = segmentPlannerType.GetMethod(
                "ResolveSegment",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object segment = resolveSegment.Invoke(null, new object[] { segmentName });

            return build.Invoke(
                null,
                new[]
                {
                    (object)referenceClipLengthSeconds,
                    requestedDurationSeconds,
                    segment,
                    enabled,
                    knownReferenceDurationSeconds
                });
        }

        private static T Read<T>(object plan, string memberName)
        {
            PropertyInfo property = plan.GetType().GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(plan);
        }
    }
}
