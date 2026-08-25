using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonCsvMetricReaderTests
    {
        [Test]
        public void Given_SimpleCsvLine_When_Splitting_Then_PreservesColumnOrder()
        {
            string[] values = (string[])Invoke("SplitLine", "reason,frameCount,1.25");

            Assert.That(values, Is.EqualTo(new[] { "reason", "frameCount", "1.25" }));
        }

        [Test]
        public void Given_DuplicateHeaders_When_BuildingIndexMap_Then_FirstColumnWins()
        {
            Dictionary<string, int> indices = (Dictionary<string, int>)Invoke(
                "BuildIndexMap",
                (object)new[] { "frame", "reason", "frame" });

            Assert.That(indices["frame"], Is.EqualTo(0));
            Assert.That(indices["reason"], Is.EqualTo(1));
        }

        [TestCase("PATH", 2)]
        [TestCase("missing", -1)]
        public void Given_Headers_When_FindingIndex_Then_IgnoresCaseAndReportsMissing(
            string headerName,
            int expected)
        {
            int result = (int)Invoke(
                "FindHeaderIndex",
                new[] { "view", "frame", "path" },
                headerName);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Given_CsvRow_When_ReadingValues_Then_UsesInvariantParsingAndExistingFallbacks()
        {
            string[] row = { "finish", "12", "1.25", "invalid" };
            Dictionary<string, int> indices = new Dictionary<string, int>
            {
                { "reason", 0 },
                { "frame", 1 },
                { "time", 2 },
                { "invalid", 3 },
            };

            Assert.That(Invoke("ReadString", row, indices, "reason"), Is.EqualTo("finish"));
            Assert.That(Invoke("ReadInt", row, indices, "frame"), Is.EqualTo(12));
            Assert.That(Invoke("ReadFloat", row, indices, "time"), Is.EqualTo(1.25f));
            Assert.That(Invoke("ReadInt", row, indices, "missing"), Is.EqualTo(0));
            Assert.That((float)Invoke("ReadFloat", row, indices, "invalid"), Is.NaN);
        }

        private static object Invoke(string methodName, params object[] arguments)
        {
            Type readerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCsvMetricReader",
                throwOnError: false);
            Assert.That(readerType, Is.Not.Null, "모델 중립 비교 CSV 판독 경계가 필요합니다.");

            MethodInfo method = readerType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, arguments);
        }
    }
}
