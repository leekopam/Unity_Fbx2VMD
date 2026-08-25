using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonProfileDeltaCalculatorTests
    {
        [Test]
        public void Given_FiniteAndNonFiniteValues_When_Calculating_Then_UsesFinitePairsOnly()
        {
            float[] candidate = { 0.1f, float.NaN, 0.7f, 0.5f };
            float[] reference = { 0.3f, 0.4f, 0.2f };

            bool success = TryCalculate(candidate, reference, out object delta);

            Assert.That(success, Is.True);
            Assert.That(ReadProperty<int>(delta, "ComparedValueCount"), Is.EqualTo(2));
            Assert.That(ReadProperty<float>(delta, "MeanAbsoluteDelta"), Is.EqualTo(0.35f).Within(0.000001f));
            Assert.That(ReadProperty<float>(delta, "MaxAbsoluteDelta"), Is.EqualTo(0.5f).Within(0.000001f));
        }

        [Test]
        public void Given_NoFinitePairs_When_Calculating_Then_ReturnsFalse()
        {
            bool success = TryCalculate(
                new[] { float.NaN, float.PositiveInfinity },
                new[] { 0f, 1f },
                out _);

            Assert.That(success, Is.False);
        }

        [Test]
        public void Given_NullProfile_When_Calculating_Then_ReturnsFalse()
        {
            bool success = TryCalculate(null, Array.Empty<float>(), out _);

            Assert.That(success, Is.False);
        }

        [Test]
        public void Given_SingleValue_When_CalculatingPaired_Then_ReturnsFalse()
        {
            bool success = TryCalculatePaired(new[] { 0.1f }, new[] { 0.2f }, out _);

            Assert.That(success, Is.False);
        }

        [Test]
        public void Given_OneFiniteValueInPair_When_CalculatingPaired_Then_PreservesFiniteCount()
        {
            bool success = TryCalculatePaired(
                new[] { 0.1f, float.NaN },
                new[] { 0.4f, 0.7f },
                out object delta);

            Assert.That(success, Is.True);
            Assert.That(ReadProperty<int>(delta, "ComparedValueCount"), Is.EqualTo(1));
            Assert.That(ReadProperty<float>(delta, "MeanAbsoluteDelta"), Is.EqualTo(0.3f).Within(0.000001f));
            Assert.That(ReadProperty<float>(delta, "MaxAbsoluteDelta"), Is.EqualTo(0.3f).Within(0.000001f));
        }

        [Test]
        public void Given_ExtractedCalculator_When_CheckingAnalyzer_Then_ProfileDeltaImplementationIsRemoved()
        {
            Type analyzerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybScreenshotDiagnosticAnalyzer",
                throwOnError: true);
            MethodInfo legacyMethod = analyzerType.GetMethod(
                "TryComputeSilhouetteProfileDelta",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo legacyEndpointMethod = analyzerType.GetMethod(
                "TryComputeSilhouetteEndpointDelta",
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
            Assert.That(legacyEndpointMethod, Is.Null);
            Assert.That(
                Count(source, "VisualComparisonProfileDeltaCalculator.TryCalculate("),
                Is.EqualTo(1));
            Assert.That(
                Count(source, "VisualComparisonProfileDeltaCalculator.TryCalculatePaired("),
                Is.EqualTo(1));
        }

        private static bool TryCalculate(float[] candidate, float[] reference, out object delta)
        {
            return TryInvoke("TryCalculate", candidate, reference, out delta);
        }

        private static bool TryCalculatePaired(float[] candidate, float[] reference, out object delta)
        {
            return TryInvoke("TryCalculatePaired", candidate, reference, out delta);
        }

        private static bool TryInvoke(
            string methodName,
            float[] candidate,
            float[] reference,
            out object delta)
        {
            Type calculatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonProfileDeltaCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null, "모델 중립 profile delta 계산 타입이 필요합니다.");

            MethodInfo method = calculatorType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");

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
