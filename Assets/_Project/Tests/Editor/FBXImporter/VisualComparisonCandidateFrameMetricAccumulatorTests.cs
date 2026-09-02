using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonCandidateFrameMetricAccumulatorTests
    {
        private const string AccumulatorTypeName =
            "Fbx2Vmd.FBXImporter.VisualComparisonCandidateFrameMetricAccumulator";
        private const string MetricTypeName =
            "Fbx2Vmd.FBXImporter.VisualComparisonCandidateFrameMetric";

        [Test]
        public void Given_FrameMetrics_When_BuildingSummary_Then_AggregatesFramingValues()
        {
            object accumulator = CreateAccumulator();

            AddFrame(
                accumulator,
                10,
                CreateMetric(
                    hasBrightPixels: true,
                    bboxHeightRatio: 0.4f,
                    bboxWidthRatio: 0.3f,
                    centerX: 0.2f,
                    bottomGapRatio: 0.1f,
                    topGapRatio: 0.2f,
                    brightAreaRatio: 0.05f));
            AddFrame(
                accumulator,
                20,
                CreateMetric(
                    hasBrightPixels: true,
                    bboxHeightRatio: 0.8f,
                    bboxWidthRatio: 0.7f,
                    centerX: 0.7f,
                    bottomGapRatio: 0.3f,
                    topGapRatio: 0.1f,
                    brightAreaRatio: 0.15f));

            object summary = Build(accumulator);
            IList samples = GetProperty<IList>(summary, "Samples");

            Assert.That(GetProperty<int>(summary, "SampleCount"), Is.EqualTo(2));
            Assert.That(GetProperty<int>(summary, "NonblankCount"), Is.EqualTo(2));
            Assert.That(GetProperty<float>(summary, "AvgBBoxHeightRatio"), Is.EqualTo(0.6f).Within(0.000001f));
            Assert.That(GetProperty<float>(summary, "AvgBBoxWidthRatio"), Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(GetProperty<float>(summary, "CenterXRangeRatio"), Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(GetProperty<float>(summary, "MaxBottomGapRatio"), Is.EqualTo(0.3f).Within(0.000001f));
            Assert.That(GetProperty<float>(summary, "MaxTopGapRatio"), Is.EqualTo(0.2f).Within(0.000001f));
            Assert.That(GetProperty<float>(summary, "AvgBrightAreaRatio"), Is.EqualTo(0.1f).Within(0.000001f));
            Assert.That(samples.Count, Is.EqualTo(2));
            Assert.That(GetField<int>(samples[0], "RecorderFrame"), Is.EqualTo(10));
            Assert.That(GetField<int>(samples[1], "RecorderFrame"), Is.EqualTo(20));
        }

        [Test]
        public void Given_IncompleteLimbSpanPair_When_BuildingSummary_Then_ExcludesPairFromLimbAverage()
        {
            object accumulator = CreateAccumulator();
            object validMetric = CreateMetric();
            SetField(validMetric, "UpperLimbSpanRatio", 0.4f);
            SetField(validMetric, "LowerLimbSpanRatio", 0.6f);
            object incompleteMetric = CreateMetric();
            SetField(incompleteMetric, "UpperLimbSpanRatio", 0.8f);
            SetField(incompleteMetric, "LowerLimbSpanRatio", float.NaN);

            AddFrame(accumulator, 0, validMetric);
            AddFrame(accumulator, 1, incompleteMetric);

            object summary = Build(accumulator);

            Assert.That(GetProperty<float>(summary, "AvgUpperLimbSpanRatio"), Is.EqualTo(0.4f).Within(0.000001f));
            Assert.That(GetProperty<float>(summary, "AvgLowerLimbSpanRatio"), Is.EqualTo(0.6f).Within(0.000001f));
        }

        [Test]
        public void Given_NoBrightPixels_When_BuildingSummary_Then_CenterRangeRemainsUnavailable()
        {
            object accumulator = CreateAccumulator();

            AddFrame(accumulator, 0, CreateMetric(hasBrightPixels: false, centerX: 0.3f));

            object summary = Build(accumulator);

            Assert.That(GetProperty<int>(summary, "NonblankCount"), Is.Zero);
            Assert.That(GetProperty<float>(summary, "CenterXRangeRatio"), Is.NaN);
        }

        [Test]
        public void Given_AnalysisErrors_When_BuildingSummary_Then_JoinsOnlyMeaningfulMessages()
        {
            object accumulator = CreateAccumulator();

            Invoke(accumulator, "AddError", "첫 번째 실패");
            Invoke(accumulator, "AddError", "   ");
            Invoke(accumulator, "AddError", "두 번째 실패");

            object summary = Build(accumulator);

            Assert.That(GetProperty<int>(summary, "SampleCount"), Is.Zero);
            Assert.That(GetProperty<string>(summary, "Error"), Is.EqualTo("첫 번째 실패; 두 번째 실패"));
            Assert.That(GetProperty<float>(summary, "AvgBBoxHeightRatio"), Is.NaN);
        }

        private static object CreateAccumulator()
        {
            Type accumulatorType = GetRuntimeType(AccumulatorTypeName);
            Assert.That(accumulatorType, Is.Not.Null, "후보 프레임 metric 누적 계산 전용 타입이 필요합니다.");
            return Activator.CreateInstance(accumulatorType, nonPublic: true);
        }

        private static object CreateMetric(
            bool hasBrightPixels = false,
            float bboxHeightRatio = 0f,
            float bboxWidthRatio = 0f,
            float centerX = 0f,
            float bottomGapRatio = 0f,
            float topGapRatio = 0f,
            float brightAreaRatio = 0f)
        {
            object metric = Activator.CreateInstance(GetRuntimeType(MetricTypeName), nonPublic: true);
            SetField(metric, "HasBrightPixels", hasBrightPixels);
            SetField(metric, "BBoxHeightRatio", bboxHeightRatio);
            SetField(metric, "BBoxWidthRatio", bboxWidthRatio);
            SetField(metric, "CenterX", centerX);
            SetField(metric, "BottomGapRatio", bottomGapRatio);
            SetField(metric, "TopGapRatio", topGapRatio);
            SetField(metric, "BrightAreaRatio", brightAreaRatio);
            return metric;
        }

        private static void AddFrame(object accumulator, int recorderFrame, object metric)
        {
            Invoke(accumulator, "AddFrame", recorderFrame, metric);
        }

        private static object Build(object accumulator)
        {
            return Invoke(accumulator, "Build");
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return (T)property.GetValue(target);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            return (T)field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            field.SetValue(target, value);
        }

        private static Type GetRuntimeType(string typeName)
        {
            return typeof(FBXVmdPipeline).Assembly.GetType(typeName, throwOnError: false);
        }
    }
}
