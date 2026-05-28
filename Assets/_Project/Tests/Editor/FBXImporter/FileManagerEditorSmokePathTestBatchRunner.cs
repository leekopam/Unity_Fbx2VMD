using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.FBXImporter
{
    public static class FileManagerEditorSmokePathTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-FileManagerEditorSmokePath.xml");
            }

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var results = new List<TestResultRecord>();
            var tests = new FileManagerEditorSmokePathTests();

            RunTest(results, nameof(tests.Given_ControlledFileExists_When_ResolvingEditorSmokeFbxPath_Then_UsesControlledPath),
                tests.Given_ControlledFileExists_When_ResolvingEditorSmokeFbxPath_Then_UsesControlledPath);
            RunTest(results, nameof(tests.Given_ControlledMissingAndProjectFbxExists_When_ResolvingEditorSmokeFbxPath_Then_UsesProjectFallback),
                tests.Given_ControlledMissingAndProjectFbxExists_When_ResolvingEditorSmokeFbxPath_Then_UsesProjectFallback);
            RunTest(results, nameof(tests.Given_NoCandidateExists_When_ResolvingEditorSmokeFbxPath_Then_ReturnsControlledCandidate),
                tests.Given_NoCandidateExists_When_ResolvingEditorSmokeFbxPath_Then_ReturnsControlledCandidate);
            RunTest(results, nameof(tests.Given_UppercaseFbxExtension_When_ResolvingEditorSmokeFbxPath_Then_PreservesFileName),
                tests.Given_UppercaseFbxExtension_When_ResolvingEditorSmokeFbxPath_Then_PreservesFileName);
            RunTest(results, nameof(tests.Given_PathLikeInput_When_ResolvingEditorSmokeFbxPath_Then_UsesOnlyFileNameForFallback),
                tests.Given_PathLikeInput_When_ResolvingEditorSmokeFbxPath_Then_UsesOnlyFileNameForFallback);
            RunTest(results, nameof(tests.Given_ProjectSourceHasHumanoidClip_When_ResolvingEditorHumanoidReferencePath_Then_UsesSourcePath),
                tests.Given_ProjectSourceHasHumanoidClip_When_ResolvingEditorHumanoidReferencePath_Then_UsesSourcePath);
            RunTest(results, nameof(tests.Given_ControlledSourceAndManualClipExists_When_ResolvingEditorHumanoidReferencePath_Then_UsesControlledSourcePath),
                tests.Given_ControlledSourceAndManualClipExists_When_ResolvingEditorHumanoidReferencePath_Then_UsesControlledSourcePath);
            RunTest(results, nameof(tests.Given_ControlledSourceOnlyHasClip_When_ResolvingEditorHumanoidReferencePath_Then_FallsBackToControlledSourcePath),
                tests.Given_ControlledSourceOnlyHasClip_When_ResolvingEditorHumanoidReferencePath_Then_FallsBackToControlledSourcePath);
            RunTest(results, nameof(tests.Given_ImportedPathHasOnlyHumanoidClip_When_ResolvingEditorHumanoidReferencePath_Then_UsesImportedPath),
                tests.Given_ImportedPathHasOnlyHumanoidClip_When_ResolvingEditorHumanoidReferencePath_Then_UsesImportedPath);
            RunTest(results, nameof(tests.Given_NoHumanoidClipCandidate_When_ResolvingEditorHumanoidReferencePath_Then_ReturnsEmptyPath),
                tests.Given_NoHumanoidClipCandidate_When_ResolvingEditorHumanoidReferencePath_Then_ReturnsEmptyPath);
            RunTest(results, nameof(tests.Given_ControlledSourceAlreadyInImportFolder_When_DecidingImportSettings_Then_PreservesExistingImporter),
                tests.Given_ControlledSourceAlreadyInImportFolder_When_DecidingImportSettings_Then_PreservesExistingImporter);
            RunTest(results, nameof(tests.Given_ExternalSourceCopiedToControlledImportFolder_When_DecidingImportSettings_Then_ConfiguresCopiedImporter),
                tests.Given_ExternalSourceCopiedToControlledImportFolder_When_DecidingImportSettings_Then_ConfiguresCopiedImporter);
            RunTest(results, nameof(tests.Given_CaptureOnlyModeWithoutEditorSmoke_When_DecidingRecordingMode_Then_SkipsVmdRecording),
                tests.Given_CaptureOnlyModeWithoutEditorSmoke_When_DecidingRecordingMode_Then_SkipsVmdRecording);
            RunTest(results, nameof(tests.Given_CaptureOnlyModeWithEditorSmoke_When_DecidingRecordingMode_Then_AllowsDiagnosticVmdRecording),
                tests.Given_CaptureOnlyModeWithEditorSmoke_When_DecidingRecordingMode_Then_AllowsDiagnosticVmdRecording);
            RunTest(results, nameof(tests.Given_VmdMode_When_DecidingRecordingMode_Then_StartsVmdRecording),
                tests.Given_VmdMode_When_DecidingRecordingMode_Then_StartsVmdRecording);
            RunTest(results, nameof(tests.Given_ProjectArtifactPath_When_MakingProjectRelativePath_Then_ReturnsSlashSeparatedRelativePath),
                tests.Given_ProjectArtifactPath_When_MakingProjectRelativePath_Then_ReturnsSlashSeparatedRelativePath);
            RunTest(results, nameof(tests.Given_OutsidePathWithSharedPrefix_When_MakingProjectRelativePath_Then_ReturnsNormalizedOriginalPath),
                tests.Given_OutsidePathWithSharedPrefix_When_MakingProjectRelativePath_Then_ReturnsNormalizedOriginalPath);
            RunTest(results, nameof(tests.Given_EmptyPath_When_MakingProjectRelativePath_Then_ReturnsEmptyPath),
                tests.Given_EmptyPath_When_MakingProjectRelativePath_Then_ReturnsEmptyPath);
            RunTest(results, nameof(tests.Given_SatisfactionFullClip_When_CalculatingReferenceTiming_Then_Matches6000FrameYybReference),
                tests.Given_SatisfactionFullClip_When_CalculatingReferenceTiming_Then_Matches6000FrameYybReference);
            RunTest(results, nameof(tests.Given_FullEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_Matches6000FrameYybReference),
                tests.Given_FullEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_Matches6000FrameYybReference);
            RunTest(results, nameof(tests.Given_ShortEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsRequestedSmokeWindow),
                tests.Given_ShortEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsRequestedSmokeWindow);
            RunTest(results, nameof(tests.Given_LongRetargetPrewarmConfigured_When_ResolvingPrewarmFrameCount_Then_DoesNotCapAtLegacyTenFrames),
                tests.Given_LongRetargetPrewarmConfigured_When_ResolvingPrewarmFrameCount_Then_DoesNotCapAtLegacyTenFrames);
            RunTest(results, nameof(tests.Given_NonSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsDefaultClipTiming),
                tests.Given_NonSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsDefaultClipTiming);

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

            Console.WriteLine($"FileManagerEditorSmokePath tests completed; results written to {resultPath}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static void RunTest(List<TestResultRecord> results, string methodName, TestDelegate action)
        {
            string name = typeof(FileManagerEditorSmokePathTests).FullName + "." + methodName;
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
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(FileManagerEditorSmokePathTests).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

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
