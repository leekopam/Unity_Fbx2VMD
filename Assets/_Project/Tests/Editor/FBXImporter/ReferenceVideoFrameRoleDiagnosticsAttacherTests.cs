using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class ReferenceVideoFrameRoleDiagnosticsAttacherTests
    {
        [Test]
        public void Given_MissingReferenceFiles_When_Attaching_Then_InitializesContextAndEmptyCoverage()
        {
            object diagnostics = Create("VisualComparisonFrameRoleDiagnosticsData");

            object coverage = Attach(
                diagnostics,
                8f,
                2f,
                "missing/provenance.md",
                "missing/result.json",
                "missing/metrics.json",
                "missing/contact.png",
                Path.GetTempPath(),
                "reference context",
                "metric basis");

            Assert.That(GetField<string>(diagnostics, "reference_mp4_canonical_context"), Is.EqualTo("reference context"));
            Assert.That(GetField<string>(diagnostics, "reference_mp4_analysis_metric_basis"), Is.EqualTo("metric basis"));
            Assert.That(GetField<bool>(diagnostics, "reference_mp4_provenance_evidence_exists"), Is.False);
            Assert.That(GetField<bool>(diagnostics, "reference_mp4_contact_sheet_exists"), Is.False);
            Assert.That(GetProperty<int>(coverage, "SampleCount"), Is.EqualTo(0));
        }

        [Test]
        public void Given_NullDiagnostics_When_Attaching_Then_ReturnsNull()
        {
            object coverage = Attach(
                null,
                8f,
                2f,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Path.GetTempPath(),
                string.Empty,
                string.Empty);

            Assert.That(coverage, Is.Null);
        }

        [Test]
        public void Given_ExtractedAttacher_When_CheckingBuilder_Then_FileReadAndCoverageCalculationAreDelegated()
        {
            string builderPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "YybVisualComparisonFrameRoleDiagnosticsBuilder.cs");
            string source = File.ReadAllText(builderPath);

            Assert.That(Count(source, "ReferenceVideoFrameRoleDiagnosticsAttacher.Attach("), Is.EqualTo(1));
            Assert.That(Count(source, "ReferenceVideoDiagnosticsReader.Read("), Is.EqualTo(0));
            Assert.That(Count(source, "ReferenceVideoClipCoverageCalculator.Calculate("), Is.EqualTo(0));
        }

        private static object Attach(object diagnostics, params object[] remainingArguments)
        {
            Type attacherType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceVideoFrameRoleDiagnosticsAttacher",
                throwOnError: false);
            Assert.That(attacherType, Is.Not.Null, "모델 중립 참조 영상 진단 attacher 타입이 필요합니다.");

            MethodInfo method = attacherType.GetMethod(
                "Attach",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            object[] arguments = new object[remainingArguments.Length + 1];
            arguments[0] = diagnostics;
            Array.Copy(remainingArguments, 0, arguments, 1, remainingArguments.Length);
            return method.Invoke(null, arguments);
        }

        private static object Create(string typeName)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter." + typeName,
                throwOnError: true);
            return Activator.CreateInstance(type, nonPublic: true);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            return (T)target.GetType().GetField(fieldName).GetValue(target);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
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
