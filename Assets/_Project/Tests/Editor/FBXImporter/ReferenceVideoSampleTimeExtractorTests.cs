using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class ReferenceVideoSampleTimeExtractorTests
    {
        [Test]
        public void Given_UnorderedRows_When_Extracting_Then_ReturnsSortedLocalTimesInsideClip()
        {
            Array rows = CreateRows(15f, 9f, 12f, 10f, float.NaN, float.PositiveInfinity);

            float[] sampleTimes = InvokeExtract(rows, 10f, 5f);

            Assert.That(sampleTimes, Is.EqualTo(new[] { 0f, 2f, 5f }));
        }

        [Test]
        public void Given_RowsWithinBoundaryEpsilon_When_Extracting_Then_ClampsToClipBounds()
        {
            Array rows = CreateRows(9.99995f, 15.00005f);

            float[] sampleTimes = InvokeExtract(rows, 10f, 5f);

            Assert.That(sampleTimes, Is.EqualTo(new[] { 0f, 5f }));
        }

        [Test]
        public void Given_ZeroRequestedDuration_When_Extracting_Then_UsesMinimumDiagnosticWindow()
        {
            Array rows = CreateRows(3.05f);

            float[] sampleTimes = InvokeExtract(rows, 3f, 0f);

            Assert.That(sampleTimes, Has.Length.EqualTo(1));
            Assert.That(sampleTimes[0], Is.EqualTo(0.05f).Within(0.0001f));
        }

        private static float[] InvokeExtract(Array rows, float startSeconds, float durationSeconds)
        {
            Type extractorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceVideoSampleTimeExtractor",
                throwOnError: false);
            Assert.That(extractorType, Is.Not.Null, "모델 중립 참조 영상 샘플 추출 경계가 필요합니다.");
            MethodInfo extract = extractorType.GetMethod(
                "ExtractLocalSeconds",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(extract, Is.Not.Null);
            return (float[])extract.Invoke(
                null,
                new object[] { rows, startSeconds, durationSeconds });
        }

        private static Array CreateRows(params float[] seconds)
        {
            Type rowType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceMp4FrameMetricRow",
                throwOnError: true);
            Array rows = Array.CreateInstance(rowType, seconds.Length);
            for (int index = 0; index < seconds.Length; index++)
            {
                object row = Activator.CreateInstance(rowType, nonPublic: true);
                rowType.GetField("seconds").SetValue(row, seconds[index]);
                rows.SetValue(row, index);
            }

            return rows;
        }
    }
}
