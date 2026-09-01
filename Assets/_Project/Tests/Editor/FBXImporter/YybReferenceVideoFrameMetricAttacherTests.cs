using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class YybReferenceVideoFrameMetricAttacherTests
    {
        [Test]
        public void Given_MissingFrameImage_When_Attaching_Then_PreservesCoverageRowsAndDefaultAverages()
        {
            object diagnostics = Create("VisualComparisonFrameRoleDiagnosticsData");
            SetField(diagnostics, "reference_mp4_current_clip_duration_seconds", 5f);
            object coverage = Create("ReferenceVideoClipCoverageData");
            object row = Create("ReferenceMp4FrameMetricRow");
            SetField(row, "framePath", "missing/reference-frame.png");
            SetProperty(coverage, "SampleCount", 1);
            SetProperty(coverage, "Rows", CreateArray(row));

            Attach(diagnostics, coverage, Path.GetTempPath());

            ICollection rows = (ICollection)diagnostics.GetType()
                .GetField("referenceMp4CurrentClipRows")
                .GetValue(diagnostics);
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(
                GetField<float>(diagnostics, "reference_mp4_current_clip_avg_upper_limb_span_ratio"),
                Is.Zero);
            Assert.That(
                GetField<float>(diagnostics, "reference_mp4_current_clip_avg_lower_limb_span_ratio"),
                Is.Zero);
        }

        [Test]
        public void Given_NullCoverage_When_Attaching_Then_DoesNotChangeDiagnostics()
        {
            object diagnostics = Create("VisualComparisonFrameRoleDiagnosticsData");
            SetField(diagnostics, "reference_mp4_current_clip_duration_seconds", 5f);

            Attach(diagnostics, null, Path.GetTempPath());

            ICollection rows = (ICollection)diagnostics.GetType()
                .GetField("referenceMp4CurrentClipRows")
                .GetValue(diagnostics);
            Assert.That(rows.Count, Is.EqualTo(0));
        }

        [Test]
        public void Given_ExtractedAttacher_When_CheckingBuilder_Then_RowImageEnrichmentIsDelegated()
        {
            BindingFlags privateStatic = BindingFlags.NonPublic | BindingFlags.Static;
            MethodInfo method = typeof(YybVisualComparisonBatchRunner).GetMethod(
                "AttachReferenceMp4CurrentClipCoverage",
                privateStatic);
            string builderPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "YybVisualComparisonFrameRoleDiagnosticsBuilder.cs");
            string source = File.ReadAllText(builderPath);

            Assert.That(method, Is.Null);
            Assert.That(
                Count(source, "YybReferenceVideoFrameMetricAttacher.Attach("),
                Is.EqualTo(1));
        }

        private static void Attach(object diagnostics, object coverage, string projectRoot)
        {
            Type attacherType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybReferenceVideoFrameMetricAttacher",
                throwOnError: false);
            Assert.That(attacherType, Is.Not.Null, "YYB 참조 영상 frame metric attacher 타입이 필요합니다.");

            MethodInfo method = attacherType.GetMethod(
                "Attach",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new[] { diagnostics, coverage, projectRoot });
        }

        private static object Create(string typeName)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter." + typeName,
                throwOnError: true);
            return Activator.CreateInstance(type, nonPublic: true);
        }

        private static Array CreateArray(object item)
        {
            Array array = Array.CreateInstance(item.GetType(), 1);
            array.SetValue(item, 0);
            return array;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName).SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            return (T)target.GetType().GetField(fieldName).GetValue(target);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            target.GetType().GetProperty(propertyName).SetValue(target, value);
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
