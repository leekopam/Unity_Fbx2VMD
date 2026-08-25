using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonSilhouetteMetricCalculatorTests
    {
        [Test]
        public void Given_PixelArray_When_CalculatingGeometry_Then_ReturnsBoundsRatiosAndKeypoints()
        {
            Color32[] pixels = CreatePixels();

            bool success = TryCalculateGeometry(pixels, 4, 4, 4, out object geometry);

            Assert.That(success, Is.True);
            Assert.That(ReadProperty<int>(ReadProperty<object>(geometry, "Bounds"), "MatchedPixelCount"), Is.EqualTo(4));
            Assert.That(ReadProperty<float>(geometry, "BBoxHeightRatio"), Is.EqualTo(0.75f));
            Assert.That(ReadProperty<float>(geometry, "BBoxWidthRatio"), Is.EqualTo(1f));
            Assert.That(ReadProperty<float>(geometry, "CenterX"), Is.EqualTo(0.5f));
            Assert.That(ReadProperty<float>(geometry, "BottomGapRatio"), Is.Zero);
            Assert.That(ReadProperty<float>(geometry, "TopGapRatio"), Is.EqualTo(0.25f));
            Assert.That(ReadProperty<float[]>(geometry, "KeypointProfile"), Has.Length.EqualTo(20));
        }

        [Test]
        public void Given_Geometry_When_CalculatingBandMetrics_Then_ReturnsSpanAndEndpoints()
        {
            Color32[] pixels = CreatePixels();
            Assert.That(TryCalculateGeometry(pixels, 4, 4, 4, out object geometry), Is.True);

            bool success = TryCalculateBandMetrics(
                pixels,
                4,
                4,
                ReadProperty<object>(geometry, "Bounds"),
                4,
                out object metrics);

            Assert.That(success, Is.True);
            Assert.That(ReadProperty<float>(metrics, "UpperSpanRatio"), Is.EqualTo(1f));
            Assert.That(ReadProperty<float>(metrics, "LowerSpanRatio"), Is.EqualTo(0.5f));
            Assert.That(ReadProperty<float[]>(metrics, "SpanProfile"),
                Is.EqualTo(new[] { 0.5f, 0f, 1f, 0f }));
            Assert.That(ReadProperty<float[]>(metrics, "EndpointProfile")[0], Is.EqualTo(0.25f));
            Assert.That(ReadProperty<float[]>(metrics, "EndpointProfile")[1], Is.EqualTo(0.75f));
            Assert.That(ReadProperty<float[]>(metrics, "EndpointProfile")[2], Is.NaN);
        }

        [Test]
        public void Given_InvalidDimensions_When_CalculatingGeometry_Then_ReturnsFalse()
        {
            bool success = TryCalculateGeometry(Array.Empty<Color32>(), 0, 4, 4, out _);

            Assert.That(success, Is.False);
        }

        [Test]
        public void Given_ExtractedCalculator_When_CheckingAnalyzer_Then_PixelMetricImplementationsAreRemoved()
        {
            Type analyzerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybScreenshotDiagnosticAnalyzer",
                throwOnError: true);
            string[] legacyMethods =
            {
                "TryAnalyzeImageSpaceSilhouette",
                "FillBandedImageSpaceLimbSpanMetrics",
                "BuildSilhouetteSpanProfile",
                "BuildSilhouetteEndpointProfile",
                "BuildImageSpaceSilhouetteKeypointProfile",
                "AppendBBoxCenterlineEndpointKeypoint",
                "AppendKeypoint",
                "AppendMissingKeypoint",
            };
            MethodInfo[] methods = analyzerType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
            string analyzerPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "YybScreenshotDiagnosticAnalyzer.cs");
            string source = File.ReadAllText(analyzerPath);

            foreach (string legacyMethod in legacyMethods)
            {
                Assert.That(
                    Array.Exists(methods, method => method.Name == legacyMethod),
                    Is.False,
                    legacyMethod);
            }
            Assert.That(
                Count(source, "VisualComparisonSilhouetteMetricCalculator.TryCalculateGeometry("),
                Is.EqualTo(2));
            Assert.That(
                Count(source, "VisualComparisonSilhouetteMetricCalculator.TryCalculateBandMetrics("),
                Is.EqualTo(1));
        }

        private static Color32[] CreatePixels()
        {
            var pixels = new Color32[16];
            pixels[1] = new Color32(255, 255, 255, 255);
            pixels[2] = new Color32(255, 255, 255, 255);
            pixels[8] = new Color32(255, 255, 255, 255);
            pixels[11] = new Color32(255, 255, 255, 255);
            return pixels;
        }

        private static bool TryCalculateGeometry(
            Color32[] pixels,
            int width,
            int height,
            int bandCount,
            out object geometry)
        {
            Type calculatorType = GetCalculatorType();
            MethodInfo method = calculatorType.GetMethod(
                "TryCalculateGeometry",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            Func<Color32, bool> predicate = pixel => pixel.a > 0;
            object[] arguments = { pixels, width, height, bandCount, predicate, null };
            bool success = (bool)method.Invoke(null, arguments);
            geometry = arguments[5];
            return success;
        }

        private static bool TryCalculateBandMetrics(
            Color32[] pixels,
            int width,
            int height,
            object bounds,
            int bandCount,
            out object metrics)
        {
            Type calculatorType = GetCalculatorType();
            MethodInfo method = calculatorType.GetMethod(
                "TryCalculateBandMetrics",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            Func<Color32, bool> predicate = pixel => pixel.a > 0;
            object[] arguments = { pixels, width, height, bounds, bandCount, predicate, null };
            bool success = (bool)method.Invoke(null, arguments);
            metrics = arguments[6];
            return success;
        }

        private static Type GetCalculatorType()
        {
            Type calculatorType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonSilhouetteMetricCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null, "모델 중립 silhouette metric 계산 타입이 필요합니다.");
            return calculatorType;
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
