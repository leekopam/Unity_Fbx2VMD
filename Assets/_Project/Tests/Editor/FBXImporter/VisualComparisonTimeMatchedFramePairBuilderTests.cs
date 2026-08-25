using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonTimeMatchedFramePairBuilderTests
    {
        private const string ProductionNamespace = "Fbx2Vmd.FBXImporter.";

        [Test]
        public void Given_TimedSamples_When_BuildingPairs_Then_SelectsNearestValidSample()
        {
            IList referenceRows = CreateList("ReferenceMp4FrameMetricRow");
            referenceRows.Add(CreateReferenceRow(seconds: 0.7f));
            IList candidateSamples = CreateList("VisualComparisonCandidateFrameSample");
            candidateSamples.Add(CreateCandidateSample(seconds: 0.2f, hasBrightPixels: true));
            candidateSamples.Add(CreateCandidateSample(seconds: 0.8f, hasBrightPixels: true));

            Array pairs = BuildPairs(referenceRows, candidateSamples, clipStartSeconds: 0f, clipDurationSeconds: 1f);

            Assert.That(pairs.Length, Is.EqualTo(1));
            object pair = pairs.GetValue(0);
            Assert.That(ReadProperty<float>(pair, "SecondsGap"), Is.EqualTo(0.1f).Within(0.0001f));
            object selectedSample = ReadProperty<object>(pair, "CandidateSample");
            Assert.That(ReadField<float>(selectedSample, "Seconds"), Is.EqualTo(0.8f));
        }

        [Test]
        public void Given_InvalidAndEquidistantSamples_When_BuildingPairs_Then_SkipsInvalidAndKeepsFirstTie()
        {
            IList referenceRows = CreateList("ReferenceMp4FrameMetricRow");
            referenceRows.Add(CreateReferenceRow(seconds: 0.5f));
            IList candidateSamples = CreateList("VisualComparisonCandidateFrameSample");
            candidateSamples.Add(CreateCandidateSample(seconds: 0.5f, hasBrightPixels: false));
            candidateSamples.Add(CreateCandidateSample(seconds: 0.25f, hasBrightPixels: true));
            candidateSamples.Add(CreateCandidateSample(seconds: 0.75f, hasBrightPixels: true));

            Array pairs = BuildPairs(referenceRows, candidateSamples, clipStartSeconds: 0f, clipDurationSeconds: 1f);

            object selectedSample = ReadProperty<object>(pairs.GetValue(0), "CandidateSample");
            Assert.That(ReadField<float>(selectedSample, "Seconds"), Is.EqualTo(0.25f));
        }

        [Test]
        public void Given_ClipOffsetAndEdgeTouch_When_BuildingPairs_Then_ReturnsNormalizedEdgeContext()
        {
            IList referenceRows = CreateList("ReferenceMp4FrameMetricRow");
            referenceRows.Add(CreateReferenceRow(seconds: 5.5f, bottomGapRatio: 0f, bboxHeightRatio: 0.6f));
            IList candidateSamples = CreateList("VisualComparisonCandidateFrameSample");
            candidateSamples.Add(CreateCandidateSample(
                seconds: 0.5f,
                hasBrightPixels: true,
                bottomGapRatio: 0.2f,
                topGapRatio: 0.2f));

            Array pairs = BuildPairs(referenceRows, candidateSamples, clipStartSeconds: 5f, clipDurationSeconds: 1f);

            object pair = pairs.GetValue(0);
            Assert.That(ReadProperty<float>(pair, "ReferenceTopGapRatio"), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(ReadProperty<bool>(pair, "ReferenceTouchesFrameEdge"), Is.True);
            Assert.That(ReadProperty<bool>(pair, "CandidateTouchesFrameEdge"), Is.False);
            Assert.That(ReadProperty<bool>(pair, "IsCropSafe"), Is.False);
        }

        [Test]
        public void Given_EmptyInputs_When_BuildingPairs_Then_ReturnsEmptyCollection()
        {
            IList referenceRows = CreateList("ReferenceMp4FrameMetricRow");
            IList candidateSamples = CreateList("VisualComparisonCandidateFrameSample");

            Array pairs = BuildPairs(referenceRows, candidateSamples, clipStartSeconds: 0f, clipDurationSeconds: 1f);

            Assert.That(pairs, Is.Empty);
        }

        [Test]
        public void Given_ExtractedPairBuilder_When_CheckingAnalyzer_Then_NestedSearchLoopIsRemoved()
        {
            string analyzerPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "YybScreenshotDiagnosticAnalyzer.cs");
            string source = File.ReadAllText(analyzerPath);

            Assert.That(
                Count(source, "VisualComparisonTimeMatchedFramePairBuilder.Build("),
                Is.EqualTo(1));
            Assert.That(
                source,
                Does.Not.Contain("foreach (VisualComparisonCandidateFrameSample candidateSample in candidateSamples)"));
        }

        private static Array BuildPairs(
            IList referenceRows,
            IList candidateSamples,
            float clipStartSeconds,
            float clipDurationSeconds)
        {
            Type builderType = FindType("VisualComparisonTimeMatchedFramePairBuilder");
            MethodInfo method = builderType.GetMethod(
                "Build",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object result = method.Invoke(
                null,
                new object[] { referenceRows, candidateSamples, clipStartSeconds, clipDurationSeconds });
            Assert.That(result, Is.InstanceOf<Array>());
            return (Array)result;
        }

        private static object CreateReferenceRow(
            float seconds,
            float bottomGapRatio = 0.2f,
            float bboxHeightRatio = 0.5f)
        {
            object row = Activator.CreateInstance(FindType("ReferenceMp4FrameMetricRow"));
            WriteField(row, "seconds", seconds);
            WriteField(row, "bottomGapRatio", bottomGapRatio);
            WriteField(row, "bboxHeightRatio", bboxHeightRatio);
            return row;
        }

        private static object CreateCandidateSample(
            float seconds,
            bool hasBrightPixels,
            float bottomGapRatio = 0.2f,
            float topGapRatio = 0.2f)
        {
            Type metricType = FindType("VisualComparisonCandidateFrameMetric");
            object metric = Activator.CreateInstance(metricType);
            WriteField(metric, "HasBrightPixels", hasBrightPixels);
            WriteField(metric, "BottomGapRatio", bottomGapRatio);
            WriteField(metric, "TopGapRatio", topGapRatio);

            Type sampleType = FindType("VisualComparisonCandidateFrameSample");
            object sample = Activator.CreateInstance(sampleType, 0, metric);
            WriteField(sample, "Seconds", seconds);
            return sample;
        }

        private static IList CreateList(string elementTypeName)
        {
            Type elementType = FindType(elementTypeName);
            Type listType = typeof(List<>).MakeGenericType(elementType);
            return (IList)Activator.CreateInstance(listType);
        }

        private static Type FindType(string typeName)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                ProductionNamespace + typeName,
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{typeName} 타입이 필요합니다.");
            return type;
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return (T)property.GetValue(target);
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            return (T)field.GetValue(target);
        }

        private static void WriteField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            field.SetValue(target, value);
        }

        private static int Count(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
