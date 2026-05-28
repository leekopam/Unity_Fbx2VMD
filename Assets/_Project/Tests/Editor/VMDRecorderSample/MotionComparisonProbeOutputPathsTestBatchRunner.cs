using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.VMDRecorderSample
{
    public static class MotionComparisonProbeOutputPathsTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-MotionComparisonProbeOutputPaths.xml");
            }

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var results = new List<TestResultRecord>();
            var tests = new MotionComparisonProbeOutputPathsTests();

            RunTest(results, nameof(tests.Given_DataPath_When_GetProjectRootFromDataPath_Then_ReturnsParent),
                tests.Given_DataPath_When_GetProjectRootFromDataPath_Then_ReturnsParent);
            RunTest(results, nameof(tests.Given_DataPath_When_GetOrCreateFolderFromDataPath_Then_CreatesFolder),
                tests.Given_DataPath_When_GetOrCreateFolderFromDataPath_Then_CreatesFolder);
            RunTest(results, nameof(tests.Given_ProjectPath_When_MakeProjectRelativePath_Then_UsesForwardSlashes),
                tests.Given_ProjectPath_When_MakeProjectRelativePath_Then_UsesForwardSlashes);
            RunTest(results, nameof(tests.Given_SiblingRootPrefixPath_When_MakeProjectRelativePath_Then_KeepsAbsolutePath),
                tests.Given_SiblingRootPrefixPath_When_MakeProjectRelativePath_Then_KeepsAbsolutePath);
            RunTest(results, nameof(tests.Given_ProjectRootPath_When_MakeProjectRootRelativePath_Then_UsesForwardSlashes),
                tests.Given_ProjectRootPath_When_MakeProjectRootRelativePath_Then_UsesForwardSlashes);
            RunTest(results, nameof(tests.Given_ExistingOutputPath_When_BuildUniqueOutputPath_Then_AddsNumericSuffix),
                tests.Given_ExistingOutputPath_When_BuildUniqueOutputPath_Then_AddsNumericSuffix);
            RunTest(results, nameof(tests.Given_EvidencePathLimits_When_ShortenEvidenceBaseName_Then_ReservesExtensionAndLeafFileName),
                tests.Given_EvidencePathLimits_When_ShortenEvidenceBaseName_Then_ReservesExtensionAndLeafFileName);
            RunTest(results, nameof(tests.Given_DataPath_When_BuildComparisonOutputRoots_Then_ReturnsLogAndSessionRoots),
                tests.Given_DataPath_When_BuildComparisonOutputRoots_Then_ReturnsLogAndSessionRoots);
            RunTest(results, nameof(tests.Given_SessionId_When_BuildSessionOutputPaths_Then_ReturnsUniqueFolderAndManifestPath),
                tests.Given_SessionId_When_BuildSessionOutputPaths_Then_ReturnsUniqueFolderAndManifestPath);
            RunTest(results, nameof(tests.Given_SessionIdWithoutManifestFileName_When_BuildSessionOutputPaths_Then_UsesDefaultSessionManifestName),
                tests.Given_SessionIdWithoutManifestFileName_When_BuildSessionOutputPaths_Then_UsesDefaultSessionManifestName);
            RunTest(results, nameof(tests.Given_SessionStamp_When_BuildScreenshotOutputPaths_Then_ReturnsUniqueFolderAndIndexPaths),
                tests.Given_SessionStamp_When_BuildScreenshotOutputPaths_Then_ReturnsUniqueFolderAndIndexPaths);
            RunTest(results, nameof(tests.Given_ScreenshotSessionInputs_When_BuildScreenshotSessionOutputPaths_Then_CentralizesFrameFolderAndSessionIndexData),
                tests.Given_ScreenshotSessionInputs_When_BuildScreenshotSessionOutputPaths_Then_CentralizesFrameFolderAndSessionIndexData);
            RunTest(results, nameof(tests.Given_SessionArtifactInputs_When_BuildSessionArtifactOutputPaths_Then_CentralizesSessionAndScreenshotArtifacts),
                tests.Given_SessionArtifactInputs_When_BuildSessionArtifactOutputPaths_Then_CentralizesSessionAndScreenshotArtifacts);
            RunTest(results, nameof(tests.Given_ScreenshotFolderAndFileName_When_BuildScreenshotPngPath_Then_ReturnsCombinedPathWithoutCreatingFolder),
                tests.Given_ScreenshotFolderAndFileName_When_BuildScreenshotPngPath_Then_ReturnsCombinedPathWithoutCreatingFolder);
            RunTest(results, nameof(tests.Given_ScreenshotFileNameInputs_When_BuildScreenshotPngFileName_Then_SanitizesReasonAndKeepsFormat),
                tests.Given_ScreenshotFileNameInputs_When_BuildScreenshotPngFileName_Then_SanitizesReasonAndKeepsFormat);
            RunTest(results, nameof(tests.Given_ScreenshotFileNamePartsWithInvalidChars_When_BuildScreenshotPngFileName_Then_SanitizesAllDynamicParts),
                tests.Given_ScreenshotFileNamePartsWithInvalidChars_When_BuildScreenshotPngFileName_Then_SanitizesAllDynamicParts);
            RunTest(results, nameof(tests.Given_ScreenshotFrameNumbers_When_BuildScreenshotFrameName_Then_UsesRecorderFrameOrFallback),
                tests.Given_ScreenshotFrameNumbers_When_BuildScreenshotFrameName_Then_UsesRecorderFrameOrFallback);
            RunTest(results, nameof(tests.Given_ScreenshotViewInputs_When_BuildScreenshotViewNames_Then_UsesStableTokens),
                tests.Given_ScreenshotViewInputs_When_BuildScreenshotViewNames_Then_UsesStableTokens);
            RunTest(results, nameof(tests.Given_ScreenshotSampleNameInputs_When_BuildScreenshotCaptureNames_Then_CentralizesFrameAndViewTokens),
                tests.Given_ScreenshotSampleNameInputs_When_BuildScreenshotCaptureNames_Then_CentralizesFrameAndViewTokens);
            RunTest(results, nameof(tests.Given_EvidenceBaseNameInputs_When_BuildEvidenceBaseName_Then_SanitizesAndOrdersSegments),
                tests.Given_EvidenceBaseNameInputs_When_BuildEvidenceBaseName_Then_SanitizesAndOrdersSegments);
            RunTest(results, nameof(tests.Given_SessionIdentityInputs_When_BuildEvidenceNames_Then_UsesReportPurposeSegments),
                tests.Given_SessionIdentityInputs_When_BuildEvidenceNames_Then_UsesReportPurposeSegments);
            RunTest(results, nameof(tests.Given_SamplingSessionInputs_When_BuildSamplingSessionOutputPaths_Then_CentralizesEvidenceCsvAndSessionNames),
                tests.Given_SamplingSessionInputs_When_BuildSamplingSessionOutputPaths_Then_CentralizesEvidenceCsvAndSessionNames);
            RunTest(results, nameof(tests.Given_ReportArtifactFileNames_When_BuildNames_Then_UsesStableSessionAndCsvNames),
                tests.Given_ReportArtifactFileNames_When_BuildNames_Then_UsesStableSessionAndCsvNames);
            RunTest(results, nameof(tests.Given_ComparisonOutputFolderAndMetricsFileName_When_BuildMetricsCsvOutputPath_Then_ReturnsUniquePath),
                tests.Given_ComparisonOutputFolderAndMetricsFileName_When_BuildMetricsCsvOutputPath_Then_ReturnsUniquePath);
            RunTest(results, nameof(tests.Given_SessionManifestArtifactPaths_When_BuildSessionManifestOutputPaths_Then_UsesProjectRelativeForwardSlashPaths),
                tests.Given_SessionManifestArtifactPaths_When_BuildSessionManifestOutputPaths_Then_UsesProjectRelativeForwardSlashPaths);
            RunTest(results, nameof(tests.Given_FrameSessionIndexPaths_When_BuildFrameSessionIndexData_Then_UsesProjectRelativeForwardSlashPaths),
                tests.Given_FrameSessionIndexPaths_When_BuildFrameSessionIndexData_Then_UsesProjectRelativeForwardSlashPaths);
            RunTest(results, nameof(tests.Given_ScreenshotIndexInputs_When_BuildScreenshotIndexRow_Then_UsesProjectRelativeForwardSlashPath),
                tests.Given_ScreenshotIndexInputs_When_BuildScreenshotIndexRow_Then_UsesProjectRelativeForwardSlashPath);
            RunTest(results, nameof(tests.Given_ScreenshotCaptureInputs_When_BuildScreenshotCaptureOutputPaths_Then_CentralizesFilePathAndIndexRow),
                tests.Given_ScreenshotCaptureInputs_When_BuildScreenshotCaptureOutputPaths_Then_CentralizesFilePathAndIndexRow);
            RunTest(results, nameof(tests.Given_MmdAfterPlayScreenshotPath_When_BuildMmdModelScreenshotPath_Then_AppendsModelSuffix),
                tests.Given_MmdAfterPlayScreenshotPath_When_BuildMmdModelScreenshotPath_Then_AppendsModelSuffix);
            RunTest(results, nameof(tests.Given_MmdScreenshotsDirectory_When_BuildMmdAfterPlayScreenshotPaths_Then_ReturnsKnownFallbackFiles),
                tests.Given_MmdScreenshotsDirectory_When_BuildMmdAfterPlayScreenshotPaths_Then_ReturnsKnownFallbackFiles);
            RunTest(results, nameof(tests.Given_ReportRelativeArtifactPath_When_ResolveMmdReportArtifactPath_Then_PrefersReportDirectory),
                tests.Given_ReportRelativeArtifactPath_When_ResolveMmdReportArtifactPath_Then_PrefersReportDirectory);
            RunTest(results, nameof(tests.Given_ReportRelativeDirectoryPath_When_ResolveMmdReportDirectoryPath_Then_PrefersReportDirectory),
                tests.Given_ReportRelativeDirectoryPath_When_ResolveMmdReportDirectoryPath_Then_PrefersReportDirectory);
            RunTest(results, nameof(tests.Given_FolderPath_When_EnsureDirectoryExists_Then_CreatesFolder),
                tests.Given_FolderPath_When_EnsureDirectoryExists_Then_CreatesFolder);
            RunTest(results, nameof(tests.Given_ProbeSource_When_CheckedForDirectIoApis_Then_PathIoResponsibilityStaysInHelpers),
                tests.Given_ProbeSource_When_CheckedForDirectIoApis_Then_PathIoResponsibilityStaysInHelpers);
            RunTest(results, nameof(tests.Given_ProbeSource_When_CheckedForScreenshotCapturePathHelpers_Then_UsesConsolidatedCapturePathHelper),
                tests.Given_ProbeSource_When_CheckedForScreenshotCapturePathHelpers_Then_UsesConsolidatedCapturePathHelper);
            RunTest(results, nameof(tests.Given_ProbeSource_When_CheckedForScreenshotSessionPathHelpers_Then_UsesConsolidatedScreenshotSessionPathHelper),
                tests.Given_ProbeSource_When_CheckedForScreenshotSessionPathHelpers_Then_UsesConsolidatedScreenshotSessionPathHelper);
            RunTest(results, nameof(tests.Given_ProbeSource_When_CheckedForScreenshotCaptureNameHelpers_Then_UsesConsolidatedNameHelper),
                tests.Given_ProbeSource_When_CheckedForScreenshotCaptureNameHelpers_Then_UsesConsolidatedNameHelper);
            RunTest(results, nameof(tests.Given_ProbeSource_When_CheckedForSessionOutputPathHelpers_Then_UsesDefaultManifestNameBehindSessionOutputHelper),
                tests.Given_ProbeSource_When_CheckedForSessionOutputPathHelpers_Then_UsesDefaultManifestNameBehindSessionOutputHelper);
            RunTest(results, nameof(tests.Given_ProbeSource_When_CheckedForSessionManifestArtifactPaths_Then_DoesNotUnpackRelativePathFields),
                tests.Given_ProbeSource_When_CheckedForSessionManifestArtifactPaths_Then_DoesNotUnpackRelativePathFields);
            RunTest(results, nameof(tests.Given_ProbeSource_When_CheckedForSamplingStartupPathHelpers_Then_UsesConsolidatedSessionPathHelper),
                tests.Given_ProbeSource_When_CheckedForSamplingStartupPathHelpers_Then_UsesConsolidatedSessionPathHelper);

            double duration = Math.Max(0.001, (DateTimeOffset.UtcNow - start).TotalSeconds);
            string resultDirectory = Path.GetDirectoryName(resultPath);
            if (!string.IsNullOrEmpty(resultDirectory))
            {
                Directory.CreateDirectory(resultDirectory);
            }

            File.WriteAllText(resultPath, BuildXml(results, duration));

            int failed = 0;
            foreach (TestResultRecord result in results)
            {
                if (result.Failure != null)
                {
                    failed++;
                    Console.Error.WriteLine(result.Failure);
                }
            }

            Console.WriteLine($"MotionComparisonProbeOutputPaths tests completed; results written to {resultPath}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static void RunTest(List<TestResultRecord> results, string methodName, TestDelegate action)
        {
            string name = typeof(MotionComparisonProbeOutputPathsTests).FullName + "." + methodName;
            DateTimeOffset start = DateTimeOffset.UtcNow;
            string failure = null;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex.ToString();
            }

            results.Add(new TestResultRecord(name, Math.Max(0.001, (DateTimeOffset.UtcNow - start).TotalSeconds), failure));
        }

        private static string GetArgumentValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static string BuildXml(IReadOnlyList<TestResultRecord> results, double duration)
        {
            int failed = 0;
            foreach (TestResultRecord result in results)
            {
                if (result.Failure != null)
                {
                    failed++;
                }
            }

            int passed = results.Count - failed;
            string runResult = failed == 0 ? "Passed" : "Failed";
            var writer = new System.Text.StringBuilder();
            writer.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            writer.AppendLine($"<test-run testcasecount=\"{results.Count}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\" duration=\"{duration:0.000}\">");
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(MotionComparisonProbeOutputPathsTests).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

            foreach (TestResultRecord result in results)
            {
                string testResult = result.Failure == null ? "Passed" : "Failed";
                string failureNode = result.Failure == null
                    ? string.Empty
                    : $"<failure><message>{SecurityElement.Escape(result.Failure)}</message></failure>";
                string escapedName = SecurityElement.Escape(result.Name);
                writer.AppendLine($"    <test-case name=\"{escapedName}\" fullname=\"{escapedName}\" result=\"{testResult}\" duration=\"{result.Duration:0.000}\">{failureNode}</test-case>");
            }

            writer.AppendLine("  </test-suite>");
            writer.AppendLine("</test-run>");
            return writer.ToString();
        }

        private sealed class TestResultRecord
        {
            public TestResultRecord(string name, double duration, string failure)
            {
                Name = name;
                Duration = duration;
                Failure = failure;
            }

            public string Name { get; }
            public double Duration { get; }
            public string Failure { get; }
        }
    }
}
