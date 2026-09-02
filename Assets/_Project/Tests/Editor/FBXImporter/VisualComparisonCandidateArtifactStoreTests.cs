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

        [Test]
        public void Given_MainRecordingStableCandidate_When_ExportIkSourceDiagnosticsExists_Then_CopiesDiagnosticsBesideStableVmd()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "StableCandidateDiagnostics_" + Guid.NewGuid().ToString("N"));
            string sourceDirectory = Path.Combine(root, "source");
            string summaryDirectory = Path.Combine(root, "summary");
            Directory.CreateDirectory(sourceDirectory);
            Directory.CreateDirectory(summaryDirectory);
            string sourceVmdPath = Path.Combine(sourceDirectory, "source.vmd");
            string sourceRotationCsvPath = Path.Combine(sourceDirectory, "source.export_rotation_diagnostics.csv");
            string sourceIkCsvPath = Path.Combine(sourceDirectory, "source.export_ik_source_samples.csv");

            try
            {
                File.WriteAllText(sourceVmdPath, "vmd");
                File.WriteAllText(sourceRotationCsvPath, "rotation");
                File.WriteAllText(sourceIkCsvPath, "ik-source");
                var sourceResult = VmdSaveResult.Ok(
                    sourceVmdPath,
                    frameCount: 3,
                    fileSizeBytes: new FileInfo(sourceVmdPath).Length,
                    exportRotationDiagnosticsCsvPath: sourceRotationCsvPath,
                    exportIkSourceDiagnosticsCsvPath: sourceIkCsvPath);

                VmdSaveResult stableResult = CopyStableCandidate(
                    sourceResult,
                    captureMode: "MainRecording",
                    summaryDirectory);

                Assert.That(Path.GetFileName(stableResult.FilePath), Is.EqualTo("vmd-rec.vmd"));
                Assert.That(File.Exists(stableResult.FilePath), Is.True);
                Assert.That(
                    Path.GetFileName(stableResult.ExportIkSourceDiagnosticsCsvPath),
                    Is.EqualTo("vmd-rec.export_ik_source_samples.csv"));
                Assert.That(File.Exists(stableResult.ExportIkSourceDiagnosticsCsvPath), Is.True);
                Assert.That(File.ReadAllText(stableResult.ExportIkSourceDiagnosticsCsvPath), Is.EqualTo("ik-source"));
                Assert.That(
                    Path.GetFileName(stableResult.ExportRotationDiagnosticsCsvPath),
                    Is.EqualTo("vmd-rec.export_rotation_diagnostics.csv"));
                Assert.That(File.Exists(stableResult.ExportRotationDiagnosticsCsvPath), Is.True);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MainRecordingSmokeFailedButVmdExists_When_BuildingStableCandidate_Then_CopiesVmdAndKeepsFailure()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "StableCandidateFailure_" + Guid.NewGuid().ToString("N"));
            string sourceDirectory = Path.Combine(root, "source");
            string summaryDirectory = Path.Combine(root, "summary");
            Directory.CreateDirectory(sourceDirectory);
            Directory.CreateDirectory(summaryDirectory);
            string sourceVmdPath = Path.Combine(sourceDirectory, "source.vmd");

            try
            {
                File.WriteAllText(sourceVmdPath, "failed-but-usable-vmd");
                var sourceResult = new VmdSaveResult
                {
                    Success = false,
                    FilePath = sourceVmdPath,
                    ErrorMessage = "deformation risk 0.365 > 0.35",
                    FrameCount = 930,
                    FileSizeBytes = new FileInfo(sourceVmdPath).Length
                };

                VmdSaveResult stableResult = CopyStableCandidate(
                    sourceResult,
                    captureMode: "MainRecording",
                    summaryDirectory);

                Assert.That(stableResult.Success, Is.False);
                Assert.That(stableResult.ErrorMessage, Is.EqualTo(sourceResult.ErrorMessage));
                Assert.That(stableResult.FrameCount, Is.EqualTo(930));
                Assert.That(Path.GetFileName(stableResult.FilePath), Is.EqualTo("vmd-rec.vmd"));
                Assert.That(File.Exists(stableResult.FilePath), Is.True);
                Assert.That(stableResult.FileSizeBytes, Is.EqualTo(new FileInfo(stableResult.FilePath).Length));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_MainAutoCandidate_When_CopyingStableCandidate_Then_KeepsOriginalResult()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "MainAutoCandidate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string sourceVmdPath = Path.Combine(root, "source.vmd");
            string summaryDirectory = Path.Combine(root, "summary");

            try
            {
                File.WriteAllText(sourceVmdPath, "main-auto-vmd");
                VmdSaveResult sourceResult = VmdSaveResult.Ok(
                    sourceVmdPath,
                    frameCount: 3,
                    fileSizeBytes: new FileInfo(sourceVmdPath).Length);

                VmdSaveResult stableResult = CopyStableCandidate(
                    sourceResult,
                    captureMode: "MainAuto",
                    summaryDirectory);

                Assert.That(stableResult.Success, Is.EqualTo(sourceResult.Success));
                Assert.That(stableResult.FilePath, Is.EqualTo(sourceResult.FilePath));
                Assert.That(stableResult.FrameCount, Is.EqualTo(sourceResult.FrameCount));
                Assert.That(stableResult.FileSizeBytes, Is.EqualTo(sourceResult.FileSizeBytes));
                Assert.That(Directory.Exists(summaryDirectory), Is.False);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        private static VmdSaveResult CopyStableCandidate(
            VmdSaveResult result,
            string captureMode,
            string summaryDirectory)
        {
            Type storeType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCandidateArtifactStore",
                throwOnError: true);
            MethodInfo copyMethod = storeType.GetMethod(
                "CopyStableCandidate",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(copyMethod, Is.Not.Null);
            return (VmdSaveResult)copyMethod.Invoke(
                null,
                new object[] { result, captureMode, summaryDirectory, "visual_compare" });
        }
    }
}
