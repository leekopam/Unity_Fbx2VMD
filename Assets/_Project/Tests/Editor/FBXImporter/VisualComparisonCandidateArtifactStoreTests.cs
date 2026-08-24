using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonCandidateArtifactStoreTests
    {
        [Test]
        public void Given_CandidateVmdAndSiblingArtifact_When_Copying_Then_PreservesResultMetadata()
        {
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonArtifactStore_" + Guid.NewGuid().ToString("N"));
            string sourceDirectory = Path.Combine(root, "source");
            string destinationDirectory = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDirectory);
            string sourceVmdPath = Path.Combine(sourceDirectory, "source.vmd");
            string sourceCsvPath = Path.Combine(sourceDirectory, "source.samples.csv");
            string destinationVmdPath = Path.Combine(destinationDirectory, "vmd-rec.vmd");

            try
            {
                File.WriteAllText(sourceVmdPath, "vmd");
                File.WriteAllText(sourceCsvPath, "samples");
                var sourceResult = VmdSaveResult.Ok(
                    sourceVmdPath,
                    frameCount: 3,
                    fileSizeBytes: new FileInfo(sourceVmdPath).Length,
                    exportRotationDiagnosticsCsvPath: sourceCsvPath);

                Type storeType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.VisualComparisonCandidateArtifactStore",
                    throwOnError: false);
                Assert.That(storeType, Is.Not.Null, "모델 중립 비교 산출물 저장 경계가 필요합니다.");

                MethodInfo copyMethod = storeType.GetMethod(
                    "Copy",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(copyMethod, Is.Not.Null);

                var copiedResult = (VmdSaveResult)copyMethod.Invoke(
                    null,
                    new object[]
                    {
                        sourceResult,
                        destinationVmdPath,
                        destinationDirectory,
                        (Func<string, string>)(value => value.Replace(' ', '_'))
                    });

                Assert.That(copiedResult.Success, Is.True);
                Assert.That(copiedResult.FrameCount, Is.EqualTo(3));
                Assert.That(copiedResult.FilePath, Is.EqualTo(destinationVmdPath));
                Assert.That(File.Exists(copiedResult.FilePath), Is.True);
                Assert.That(Path.GetFileName(copiedResult.ExportRotationDiagnosticsCsvPath), Is.EqualTo("vmd-rec.samples.csv"));
                Assert.That(File.ReadAllText(copiedResult.ExportRotationDiagnosticsCsvPath), Is.EqualTo("samples"));
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
