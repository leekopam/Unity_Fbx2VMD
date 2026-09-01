using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public sealed class ReferenceAlignedSampleTimePlannerTests
    {
        private static readonly float[] ProbeDefaultLocalSampleSeconds =
        {
            0f,
            3f,
            6f,
            10f,
            13.2f,
            20f,
            30f,
            60f,
            120f
        };

        [Test]
        public void Given_OverlappingLocalSamples_When_Building_Then_DeduplicatesWithinHalfFrame()
        {
            float[] samples = BuildSampleTimes(
                10f,
                20f,
                new[] { 3.2f, 13.2f },
                new[] { 3.216f },
                1f,
                30f);

            Assert.That(samples, Is.Ordered.Ascending);
            Assert.That(samples.Length, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(13.216f).Within(0.0001f));
            Assert.That(samples[1], Is.EqualTo(23.2f).Within(0.0001f));
        }

        [Test]
        public void Given_VisualCompareSegmentMiddle_When_BuildingProbeSampleTimes_Then_ShiftsSamplesToReferenceClipWindow()
        {
            float referenceClipStartSeconds = 88.39167f;
            float requestedDurationSeconds = 31f;
            float[] referenceLocalSampleSeconds =
            {
                1.6083298f,
                11.6083298f,
                21.6083298f
            };

            float[] sampleTimes = BuildSampleTimes(
                referenceClipStartSeconds,
                requestedDurationSeconds,
                ProbeDefaultLocalSampleSeconds,
                referenceLocalSampleSeconds,
                1f,
                30f);

            Assert.That(sampleTimes, Is.Ordered.Ascending);
            Assert.That(sampleTimes, Has.None.LessThan(referenceClipStartSeconds - 0.0001f));
            Assert.That(sampleTimes, Has.None.GreaterThan(referenceClipStartSeconds + requestedDurationSeconds + 0.0001f));
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + 3f);
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + 10f);
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + referenceLocalSampleSeconds[0]);
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + referenceLocalSampleSeconds[1]);
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + referenceLocalSampleSeconds[2]);
            AssertDoesNotContainTime(sampleTimes, 3f);
            AssertDoesNotContainTime(sampleTimes, 10f);
        }

        [Test]
        public void Given_ReferenceMp4SampleWithinHalfFrameOfDefaultSample_When_BuildingProbeSampleTimes_Then_DeduplicatesToSingleCaptureFrame()
        {
            float referenceClipStartSeconds = 176.78334f;
            float requestedDurationSeconds = 31f;
            float[] referenceLocalSampleSeconds =
            {
                3.2166595f,
                13.21666f,
                23.149658f
            };

            float[] sampleTimes = BuildSampleTimes(
                referenceClipStartSeconds,
                requestedDurationSeconds,
                ProbeDefaultLocalSampleSeconds,
                referenceLocalSampleSeconds,
                1f,
                30f);

            int nearThirteenSecondSamples = sampleTimes.Count(time =>
                Mathf.Abs(time - (referenceClipStartSeconds + 13.2f)) <= (0.5f / 30f) + 0.0001f ||
                Mathf.Abs(time - (referenceClipStartSeconds + 13.21666f)) <= (0.5f / 30f) + 0.0001f);

            Assert.That(nearThirteenSecondSamples, Is.EqualTo(1));
            AssertContainsTime(sampleTimes, referenceClipStartSeconds + 13.21666f);
            Assert.That(sampleTimes.Length, Is.EqualTo(9));
        }

        [Test]
        public void Given_ReferenceMmdTimingScale_When_BuildingProbeSampleTimes_Then_MapsReferenceSecondsToCandidateClipSeconds()
        {
            float candidateClipStartSeconds = 176.78334f;
            float requestedDurationSeconds = 31f;
            float candidateClipSecondsPerReferenceSecond = 207.78334f / (6001f / 30f);
            float[] referenceLocalSampleSeconds =
            {
                3.2166595f,
                13.21666f,
                23.149658f
            };

            float[] sampleTimes = BuildSampleTimes(
                candidateClipStartSeconds,
                requestedDurationSeconds,
                ProbeDefaultLocalSampleSeconds,
                referenceLocalSampleSeconds,
                candidateClipSecondsPerReferenceSecond,
                30f);

            Assert.That(sampleTimes, Is.Ordered.Ascending);
            AssertContainsTime(
                sampleTimes,
                candidateClipStartSeconds + (3.2166595f * candidateClipSecondsPerReferenceSecond));
            AssertContainsTime(
                sampleTimes,
                candidateClipStartSeconds + (23.149658f * candidateClipSecondsPerReferenceSecond));
            AssertDoesNotContainTime(sampleTimes, candidateClipStartSeconds + 3.2166595f);
            AssertDoesNotContainTime(sampleTimes, candidateClipStartSeconds + 23.149658f);
        }

        private static float[] BuildSampleTimes(
            float referenceClipStartSeconds,
            float requestedDurationSeconds,
            float[] defaultLocalSampleSeconds,
            float[] referenceLocalSampleSeconds,
            float candidateClipSecondsPerReferenceSecond,
            float frameRate)
        {
            Type plannerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceAlignedSampleTimePlanner",
                throwOnError: false);
            Assert.That(plannerType, Is.Not.Null, "모델 중립 참조 정렬 샘플 시간 계산기가 필요합니다.");

            MethodInfo buildMethod = plannerType.GetMethod(
                "Build",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(buildMethod, Is.Not.Null);

            return (float[])buildMethod.Invoke(
                null,
                new object[]
                {
                    referenceClipStartSeconds,
                    requestedDurationSeconds,
                    defaultLocalSampleSeconds,
                    referenceLocalSampleSeconds,
                    candidateClipSecondsPerReferenceSecond,
                    frameRate
                });
        }

        private static void AssertContainsTime(float[] sampleTimes, float expected)
        {
            Assert.That(
                sampleTimes.Any(value => Mathf.Abs(value - expected) <= 0.0001f),
                Is.True,
                $"Expected sample time {expected:F5} was not present. Actual: {string.Join(", ", sampleTimes.Select(value => value.ToString("F5")))}");
        }

        private static void AssertDoesNotContainTime(float[] sampleTimes, float unexpected)
        {
            Assert.That(
                sampleTimes.Any(value => Mathf.Abs(value - unexpected) <= 0.0001f),
                Is.False,
                $"Unexpected sample time {unexpected:F5} was present. Actual: {string.Join(", ", sampleTimes.Select(value => value.ToString("F5")))}");
        }
    }
}
