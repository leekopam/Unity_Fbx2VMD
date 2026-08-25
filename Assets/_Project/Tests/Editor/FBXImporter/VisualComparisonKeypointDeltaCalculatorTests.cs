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
        public void Given_BoundsAndKeypoints_When_CalculatingBBoxNormalized_Then_RecordsMaxAttribution()
        {
            bool success = TryCalculateBBoxNormalized(
                new[] { 0.3f, 0.1f, 0.7f, 0.9f },
                0.5f,
                0.4f,
                0.1f,
                0.8f,
                new[] { 0.3f, 0.1f, 0.5f, 0.5f },
                0.5f,
                0.4f,
                0.1f,
                0.8f,
                out object delta);

            Assert.That(success, Is.True);
            Assert.That(ReadProperty<int>(delta, "ComparedKeypointCount"), Is.EqualTo(2));
            Assert.That(ReadProperty<float>(delta, "MeanL1Delta"), Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(ReadProperty<float>(delta, "MaxL1Delta"), Is.EqualTo(1f).Within(0.000001f));
            Assert.That(ReadProperty<int>(delta, "MaxKeypointIndex"), Is.EqualTo(1));
            Assert.That(ReadProperty<float>(delta, "MaxXDelta"), Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(ReadProperty<float>(delta, "MaxYDelta"), Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(ReadProperty<float>(delta, "MaxCandidateX"), Is.EqualTo(1f).Within(0.000001f));
            Assert.That(ReadProperty<float>(delta, "MaxCandidateY"), Is.EqualTo(1f).Within(0.000001f));
            Assert.That(ReadProperty<float>(delta, "MaxReferenceX"), Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(ReadProperty<float>(delta, "MaxReferenceY"), Is.EqualTo(0.5f).Within(0.000001f));
        }

        [Test]
        public void Given_InvalidBBoxWidth_When_CalculatingBBoxNormalized_Then_ReturnsFalse()
        {
            bool success = TryCalculateBBoxNormalized(
                new[] { 0.3f, 0.1f },
                0.5f,
                0f,
                0.1f,
                0.8f,
                new[] { 0.3f, 0.1f },
                0.5f,
                0.4f,
                0.1f,
                0.8f,
                out _);

            Assert.That(success, Is.False);
        }

        [Test]
        public void Given_IdenticalNormalizedKeypoint_When_Calculating_Then_MaxAttributionRemainsUnset()
        {
            bool success = TryCalculateBBoxNormalized(
                new[] { 0.3f, 0.1f },
                0.5f,
                0.4f,
                0.1f,
                0.8f,
                new[] { 0.3f, 0.1f },
                0.5f,
                0.4f,
                0.1f,
                0.8f,
                out object delta);

            Assert.That(success, Is.True);
            Assert.That(ReadProperty<float>(delta, "MaxL1Delta"), Is.Zero);
            Assert.That(ReadProperty<int>(delta, "MaxKeypointIndex"), Is.EqualTo(-1));
            Assert.That(ReadProperty<float>(delta, "MaxXDelta"), Is.NaN);
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
            bool hasLegacyNormalizedMethod = Array.Exists(
                analyzerType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
                method => method.Name == "TryComputeBBoxNormalizedImageSpaceKeypointDelta");
            string analyzerPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "YybScreenshotDiagnosticAnalyzer.cs");
            string source = File.ReadAllText(analyzerPath);

            Assert.That(legacyMethod, Is.Null);
            Assert.That(hasLegacyNormalizedMethod, Is.False);
            Assert.That(
                Count(source, "VisualComparisonKeypointDeltaCalculator.TryCalculate("),
                Is.EqualTo(1));
            Assert.That(
                Count(source, "VisualComparisonKeypointDeltaCalculator.TryCalculateBBoxNormalized("),
                Is.EqualTo(2));
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

        private static bool TryCalculateBBoxNormalized(
            float[] candidate,
            float candidateCenterX,
            float candidateBBoxWidth,
            float candidateBottomGap,
            float candidateBBoxHeight,
            float[] reference,
            float referenceCenterX,
            float referenceBBoxWidth,
            float referenceBottomGap,
            float referenceBBoxHeight,
            out object delta)
        {
            Type calculatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonKeypointDeltaCalculator",
                throwOnError: true);
            MethodInfo method = calculatorType.GetMethod(
                "TryCalculateBBoxNormalized",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "bbox-normalized keypoint delta 계산 메서드가 필요합니다.");

            object[] arguments =
            {
                candidate,
                candidateCenterX,
                candidateBBoxWidth,
                candidateBottomGap,
                candidateBBoxHeight,
                reference,
                referenceCenterX,
                referenceBBoxWidth,
                referenceBottomGap,
                referenceBBoxHeight,
                null,
            };
            bool success = (bool)method.Invoke(null, arguments);
            delta = arguments[10];
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
