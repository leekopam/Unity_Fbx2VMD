using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public sealed class ReferenceVideoClipCoverageCalculatorTests
    {
        [Test]
        public void Given_RowsAcrossClipRange_When_Calculating_Then_UsesOnlyMatchingSamples()
        {
            Array rows = CreateRows(
                CreateRow(9f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f),
                CreateRow(10f, 0.6f, 0.4f, 0.45f, 0.2f, 0.3f),
                CreateRow(12f, 0.8f, 0.6f, 0.55f, 0.1f, 0.5f),
                CreateRow(15f, 1f, 0.8f, 0.65f, 0.3f, 0.7f),
                CreateRow(16f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f));

            object result = InvokeCalculate(rows, 10f, 5f);

            Assert.That(Get<int>(result, "SampleCount"), Is.EqualTo(3));
            Assert.That(Get<float[]>(result, "SampleSeconds"), Is.EqualTo(new[] { 0f, 2f, 5f }));
            Assert.That(Get<float>(result, "SampleCoverageRatio"), Is.EqualTo(1f));
            Assert.That(Get<float>(result, "SampleGapSeconds"), Is.EqualTo(0f));
            Assert.That(Get<float>(result, "AverageBBoxHeightRatio"), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(Get<float>(result, "CenterXRangeRatio"), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(Get<Array>(result, "Rows"), Has.Length.EqualTo(3));
        }

        [Test]
        public void Given_NoRowsInsideClip_When_Calculating_Then_ReportsFullGapAndNaNMetrics()
        {
            Array rows = CreateRows(CreateRow(3f, 0.5f, 0.4f, 0.5f, 0.1f, 0.2f));

            object result = InvokeCalculate(rows, 10f, 5f);

            Assert.That(Get<int>(result, "SampleCount"), Is.EqualTo(0));
            Assert.That(Get<float>(result, "SampleGapSeconds"), Is.EqualTo(5f));
            Assert.That(Get<float>(result, "AverageBBoxHeightRatio"), Is.NaN);
            Assert.That(Get<Array>(result, "Rows"), Is.Empty);
        }

        private static object CreateRow(
            float seconds,
            float bboxHeight,
            float bboxWidth,
            float centerX,
            float bottomGap,
            float brightArea)
        {
            Type rowType = GetRuntimeType("Fbx2Vmd.FBXImporter.ReferenceMp4FrameMetricRow");
            object row = Activator.CreateInstance(rowType, nonPublic: true);
            rowType.GetField("seconds").SetValue(row, seconds);
            rowType.GetField("bboxHeightRatio").SetValue(row, bboxHeight);
            rowType.GetField("bboxWidthRatio").SetValue(row, bboxWidth);
            rowType.GetField("centerXRatio").SetValue(row, centerX);
            rowType.GetField("bottomGapRatio").SetValue(row, bottomGap);
            rowType.GetField("brightAreaRatio").SetValue(row, brightArea);
            return row;
        }

        private static Array CreateRows(params object[] rows)
        {
            Type rowType = GetRuntimeType("Fbx2Vmd.FBXImporter.ReferenceMp4FrameMetricRow");
            Array result = Array.CreateInstance(rowType, rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                result.SetValue(rows[i], i);
            }

            return result;
        }

        private static object InvokeCalculate(Array rows, float startSeconds, float durationSeconds)
        {
            Type calculatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceVideoClipCoverageCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null, "reference clip coverage 계산 경계가 필요합니다.");

            MethodInfo method = calculatorType.GetMethod(
                "Calculate",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, new object[] { rows, startSeconds, durationSeconds });
        }

        private static Type GetRuntimeType(string fullName)
        {
            return typeof(FBXVmdPipeline).Assembly.GetType(fullName, throwOnError: true);
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
