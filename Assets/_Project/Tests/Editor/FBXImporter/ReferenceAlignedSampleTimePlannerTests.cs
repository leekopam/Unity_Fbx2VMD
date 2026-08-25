using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class ReferenceAlignedSampleTimePlannerTests
    {
        [Test]
        public void Given_OverlappingLocalSamples_When_Building_Then_DeduplicatesWithinHalfFrame()
        {
            Type plannerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceAlignedSampleTimePlanner",
                throwOnError: false);
            Assert.That(plannerType, Is.Not.Null, "모델 중립 참조 정렬 샘플 시간 계산기가 필요합니다.");

            MethodInfo buildMethod = plannerType.GetMethod(
                "Build",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(buildMethod, Is.Not.Null);

            float[] samples = (float[])buildMethod.Invoke(
                null,
                new object[]
                {
                    10f,
                    20f,
                    new[] { 3.2f, 13.2f },
                    new[] { 3.216f },
                    1f,
                    30f
                });

            Assert.That(samples, Is.Ordered.Ascending);
            Assert.That(samples.Length, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(13.216f).Within(0.0001f));
            Assert.That(samples[1], Is.EqualTo(23.2f).Within(0.0001f));
        }
    }
}
