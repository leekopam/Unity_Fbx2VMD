using System;
using System.IO;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public sealed class ReferenceVideoDiagnosticsReaderTests
    {
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                $"fbx2vmd-reference-video-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
        }

        [Test]
        public void Given_ValidAnalysisFiles_When_Reading_Then_MapsVideoAndFrameMetrics()
        {
            string analysisPath = Path.Combine(_temporaryDirectory, "analysis.json");
            string metricsPath = Path.Combine(_temporaryDirectory, "metrics.json");
            File.WriteAllText(
                analysisPath,
                "{\"schema\":\"analysis.v1\",\"extractedFrameCount\":24," +
                "\"video\":{\"width\":1920,\"height\":1080,\"avg_frame_rate\":\"30/1\"," +
                "\"stream_duration\":\"31.5\",\"nb_frames\":\"945\"}}");
            File.WriteAllText(
                metricsPath,
                "{\"schema\":\"metrics.v1\",\"sampleCount\":1,\"extractedFrameCount\":24," +
                "\"avgBBoxHeightRatio\":0.75,\"avgBBoxWidthRatio\":0.5," +
                "\"centerXRangeRatio\":0.2,\"maxBottomGapRatio\":0.1," +
                "\"avgBrightAreaRatio\":0.3,\"rows\":[{\"seconds\":3.5," +
                "\"framePath\":\"frame.png\",\"bboxHeightRatio\":0.8}]}");

            object result = InvokeRead(analysisPath, metricsPath);

            Assert.That(Get<bool>(result, "AnalysisFileExists"), Is.True);
            Assert.That(Get<string>(result, "AnalysisSchema"), Is.EqualTo("analysis.v1"));
            Assert.That(Get<int>(result, "VideoWidth"), Is.EqualTo(1920));
            Assert.That(Get<float>(result, "StreamDurationSeconds"), Is.EqualTo(31.5f));
            Assert.That(Get<int>(result, "TotalVideoFrames"), Is.EqualTo(945));
            Assert.That(Get<bool>(result, "FrameMetricsFileExists"), Is.True);
            Assert.That(Get<string>(result, "FrameMetricsSchema"), Is.EqualTo("metrics.v1"));
            Assert.That(Get<int>(result, "FrameMetricsSampleCount"), Is.EqualTo(1));
            Assert.That(Get<Array>(result, "FrameMetricRows"), Has.Length.EqualTo(1));
        }

        [Test]
        public void Given_MissingAnalysisFiles_When_Reading_Then_ReportsAbsenceWithoutError()
        {
            object result = InvokeRead(
                Path.Combine(_temporaryDirectory, "missing-analysis.json"),
                Path.Combine(_temporaryDirectory, "missing-metrics.json"));

            Assert.That(Get<bool>(result, "AnalysisFileExists"), Is.False);
            Assert.That(Get<bool>(result, "FrameMetricsFileExists"), Is.False);
            Assert.That(Get<string>(result, "AnalysisError"), Is.Empty);
            Assert.That(Get<string>(result, "FrameMetricsError"), Is.Empty);
            Assert.That(Get<Array>(result, "FrameMetricRows"), Is.Empty);
        }

        private static object InvokeRead(string analysisPath, string metricsPath)
        {
            Type readerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceVideoDiagnosticsReader",
                throwOnError: false);
            Assert.That(readerType, Is.Not.Null, "reference 영상 진단 파일 판독 경계가 필요합니다.");

            MethodInfo readMethod = readerType.GetMethod(
                "Read",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(readMethod, Is.Not.Null);
            return readMethod.Invoke(null, new object[] { analysisPath, metricsPath });
        }

        private static T Get<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(instance);
        }
    }
}
