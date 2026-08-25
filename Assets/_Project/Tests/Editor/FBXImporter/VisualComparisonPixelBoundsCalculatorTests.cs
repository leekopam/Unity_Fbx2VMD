using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonPixelBoundsCalculatorTests
    {
        [Test]
        public void Given_MatchingPixels_When_Calculating_Then_ReturnsCountAndBounds()
        {
            Color32[] pixels =
            {
                Pixel(0), Pixel(1), Pixel(0),
                Pixel(1), Pixel(0), Pixel(1)
            };

            bool success = TryCalculate(
                pixels,
                width: 3,
                height: 2,
                pixel => pixel.r > 0,
                out object bounds);

            Assert.That(success, Is.True);
            Assert.That(ReadProperty<bool>(bounds, "HasMatches"), Is.True);
            Assert.That(ReadProperty<int>(bounds, "MatchedPixelCount"), Is.EqualTo(3));
            Assert.That(ReadProperty<int>(bounds, "MinX"), Is.EqualTo(0));
            Assert.That(ReadProperty<int>(bounds, "MaxX"), Is.EqualTo(2));
            Assert.That(ReadProperty<int>(bounds, "MinY"), Is.EqualTo(0));
            Assert.That(ReadProperty<int>(bounds, "MaxY"), Is.EqualTo(1));
        }

        [Test]
        public void Given_NoMatchingPixels_When_Calculating_Then_ReturnsEmptyBounds()
        {
            Color32[] pixels = { Pixel(0), Pixel(0), Pixel(0), Pixel(0) };

            bool success = TryCalculate(
                pixels,
                width: 2,
                height: 2,
                pixel => pixel.r > 0,
                out object bounds);

            Assert.That(success, Is.True);
            Assert.That(ReadProperty<bool>(bounds, "HasMatches"), Is.False);
            Assert.That(ReadProperty<int>(bounds, "MatchedPixelCount"), Is.Zero);
        }

        [Test]
        public void Given_InvalidPixelDimensions_When_Calculating_Then_ReturnsFalse()
        {
            bool success = TryCalculate(
                new[] { Pixel(1) },
                width: 2,
                height: 2,
                pixel => pixel.r > 0,
                out _);

            Assert.That(success, Is.False);
        }

        [Test]
        public void Given_ExtractedCalculator_When_CheckingAnalyzer_Then_UsesItForPixelBounds()
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
                Count(source, "VisualComparisonPixelBoundsCalculator.TryCalculate("),
                Is.EqualTo(2));
            Assert.That(source, Does.Not.Contain("brightPixelCount++;"));
        }

        private static bool TryCalculate(
            Color32[] pixels,
            int width,
            int height,
            Func<Color32, bool> predicate,
            out object bounds)
        {
            Type calculatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonPixelBoundsCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null, "모델 중립 픽셀 경계 계산 타입이 필요합니다.");

            MethodInfo method = calculatorType.GetMethod(
                "TryCalculate",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] arguments = { pixels, width, height, predicate, null };
            bool success = (bool)method.Invoke(null, arguments);
            bounds = arguments[4];
            return success;
        }

        private static Color32 Pixel(byte value)
        {
            return new Color32(value, value, value, 255);
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
