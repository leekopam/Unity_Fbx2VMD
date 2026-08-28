using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonSummaryValueFormatterTests
    {
        [TestCase(float.NaN, "n/a")]
        [TestCase(float.PositiveInfinity, "n/a")]
        [TestCase(1.23456789f, "1.234568")]
        public void Given_QualityFloat_When_Formatting_Then_UsesInvariantCompactValue(float value, string expected)
        {
            Assert.That(Invoke("FormatFloat", value), Is.EqualTo(expected));
        }

        [Test]
        public void Given_EnabledWeightCapScaleGate_When_Formatting_Then_PreservesSummaryLayout()
        {
            Assert.That(
                Invoke("FormatEnabledWeightCapScaleGate", true, 0.5f, 1f, 2f, 10f, 20f),
                Is.EqualTo("True/0.5/1/2/10-20"));
        }

        [Test]
        public void Given_EnabledWeightCapScaleBlendGate_When_Formatting_Then_LabelsBlendValue()
        {
            Assert.That(
                Invoke("FormatEnabledWeightCapScaleBlendGate", true, 0.5f, 1f, 2f, 0.25f, 10f, 20f),
                Is.EqualTo("True/0.5/1/2/blend:0.25/10-20"));
        }

        [Test]
        public void Given_ProbeSampleTimes_When_Formatting_Then_UsesInvariantSlashSeparatedValues()
        {
            Assert.That(
                Invoke("FormatProbeSampleTimes", new float[] { 0f, 1.2345f, 2.5f }),
                Is.EqualTo("0/1.235/2.5"));
        }

        [Test]
        public void Given_NoProbeSampleTimes_When_Formatting_Then_ReturnsNone()
        {
            Assert.That(Invoke("FormatProbeSampleTimes", Array.Empty<float>()), Is.EqualTo("none"));
            Assert.That(Invoke("FormatProbeSampleTimes", new object[] { null }), Is.EqualTo("none"));
        }

        private static object Invoke(string methodName, params object[] arguments)
        {
            Type formatterType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonSummaryValueFormatter",
                throwOnError: false);
            Assert.That(formatterType, Is.Not.Null, "모델 중립 비교 요약 값 formatter가 필요합니다.");

            MethodInfo method = formatterType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, arguments);
        }
    }
}
