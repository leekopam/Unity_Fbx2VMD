using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonSummaryFileStoreTests
    {
        [Serializable]
        private sealed class SampleSummary
        {
            public string status;
        }

        [Test]
        public void Given_SummaryData_When_WritingAndCopying_Then_CreatesLatestFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonSummaryStore_" + Guid.NewGuid().ToString("N"));
            string sourcePath = Path.Combine(root, "session", "summary.json");
            string relativeTargetPath = Path.Combine("latest", "summary.json");

            try
            {
                Type storeType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.VisualComparisonSummaryFileStore",
                    throwOnError: false);
                Assert.That(storeType, Is.Not.Null, "모델 중립 비교 요약 파일 저장 경계가 필요합니다.");

                MethodInfo writeJsonMethod = storeType.GetMethod(
                    "WriteJson",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo copyLatestMethod = storeType.GetMethod(
                    "CopyLatest",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(writeJsonMethod, Is.Not.Null);
                Assert.That(copyLatestMethod, Is.Not.Null);

                writeJsonMethod.Invoke(null, new object[] { sourcePath, new SampleSummary { status = "passed" } });
                copyLatestMethod.Invoke(null, new object[] { sourcePath, root, relativeTargetPath });

                string latestPath = Path.Combine(root, relativeTargetPath);
                Assert.That(File.Exists(sourcePath), Is.True);
                Assert.That(File.Exists(latestPath), Is.True);
                Assert.That(File.ReadAllText(latestPath), Does.Contain("passed"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }
    }
}
