using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonKeypointDeltaCalculatorTests
    {
        [Test]
        public void Given_FiniteAndNonFiniteKeypoints_When_Calculating_Then_UsesCompleteFinitePairsOnly()
        {
            float[] candidate = { 0.1f, 0.2f, float.NaN, 0.4f, 0.8f, 0.1f };
            float[] reference = { 0.4f, 0.3f, 0.2f, 0.5f, 0.3f, 0.4f };

            bool success = TryCalculate(candidate, reference, out object delta);

            Assert.That(success, Is.True);
            Assert.That(ReadProperty<int>(delta, "ComparedKeypointCount"), Is.EqualTo(2));
            Assert.That(ReadProperty<float>(delta, "MeanL1Delta"), Is.EqualTo(0.6f).Within(0.000001f));
            Assert.That(ReadProperty<float>(delta, "MaxL1Delta"), Is.EqualTo(0.8f).Within(0.000001f));
        }

        [Test]
        public void Given_SingleCoordinate_When_Calculating_Then_ReturnsFalse()
        {
            bool success = TryCalculate(new[] { 0.1f }, new[] { 0.2f }, out _);

            Assert.That(success, Is.False);
        }

        [Test]
        public void Given_NullKeypoints_When_Calculating_Then_ReturnsFalse()
        {
            bool success = TryCalculate(null, Array.Empty<float>(), out _);

            Assert.That(success, Is.False);
        }

        [Test]
        public void Given_ExtractedCalculator_When_CheckingAnalyzer_Then_KeypointDeltaImplementationIsRemoved()
        {
            Type analyzerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybScreenshotDiagnosticAnalyzer",
                throwOnError: true);
            MethodInfo legacyMethod = analyzerType.GetMethod(
                "TryComputeImageSpaceKeypointDelta",
                BindingFlags.Static | BindingFlags.NonPublic);
            string analyzerPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "YybScreenshotDiagnosticAnalyzer.cs");
            string source = File.ReadAllText(analyzerPath);

            Assert.That(legacyMethod, Is.Null);
            Assert.That(
                Count(source, "VisualComparisonKeypointDeltaCalculator.TryCalculate("),
                Is.EqualTo(1));
        }

        private static bool TryCalculate(float[] candidate, float[] reference, out object delta)
        {
            Type calculatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonKeypointDeltaCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null, "모델 중립 keypoint delta 계산 타입이 필요합니다.");

            MethodInfo method = calculatorType.GetMethod(
                "TryCalculate",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] arguments = { candidate, reference, null };
            bool success = (bool)method.Invoke(null, arguments);
            delta = arguments[2];
            return success;
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return (T)property.GetValue(target);
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
