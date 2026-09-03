using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.FBXImporter
{
    public static class FBXVmdPipelineEditorSmokePathTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-FBXVmdPipelineEditorSmokePath.xml");
            }

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var results = new List<TestResultRecord>();
            var tests = new FBXVmdPipelineEditorSmokePathTests();

            RunTest(results, nameof(tests.Given_FBXVmdPipelineEditor_When_InspectingSourceOwnership_Then_LivesWithFbxImporterEditorCode),
                tests.Given_FBXVmdPipelineEditor_When_InspectingSourceOwnership_Then_LivesWithFbxImporterEditorCode);
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
            RunTest(results, nameof(tests.Given_ControlledSourceAndProjectClipExists_When_ResolvingEditorHumanoidReferencePath_Then_UsesProjectFallbackPath),
                tests.Given_ControlledSourceAndProjectClipExists_When_ResolvingEditorHumanoidReferencePath_Then_UsesProjectFallbackPath);
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
            RunTest(results, nameof(tests.Given_SatisfactionFullClipAndReferenceTimingDisabled_When_CalculatingReferenceTiming_Then_KeepsNormalPlayback),
                tests.Given_SatisfactionFullClipAndReferenceTimingDisabled_When_CalculatingReferenceTiming_Then_KeepsNormalPlayback);
            RunTest(results, nameof(tests.Given_CustomVmdPlaybackSpeed_When_ResolvingRecordingLength_Then_PreservesFullClipAtThatSpeed),
                tests.Given_CustomVmdPlaybackSpeed_When_ResolvingRecordingLength_Then_PreservesFullClipAtThatSpeed);
            RunTest(results, nameof(tests.Given_GhostHasNoRenderers_When_ShowGhostModelIsEnabled_Then_UsesSkeletonDebugFallback),
                tests.Given_GhostHasNoRenderers_When_ShowGhostModelIsEnabled_Then_UsesSkeletonDebugFallback);
            RunTest(results, nameof(tests.Given_GhostHasRenderers_When_ShowGhostModelIsEnabled_Then_StillUsesSkeletonDebugVisibilityAid),
                tests.Given_GhostHasRenderers_When_ShowGhostModelIsEnabled_Then_StillUsesSkeletonDebugVisibilityAid);
            RunTest(results, nameof(tests.Given_GhostSkeletonDebugRendererInitializedBeforeAnimator_When_AnimatorIsAdded_Then_ReacquiresAnimatorForSceneViewLines),
                tests.Given_GhostSkeletonDebugRendererInitializedBeforeAnimator_When_AnimatorIsAdded_Then_ReacquiresAnimatorForSceneViewLines);
            RunTest(results, nameof(tests.Given_GhostContainerScaleIsSmall_When_DrawingDebugSkeleton_Then_CompensatesLinePositionsForSceneVisibility),
                tests.Given_GhostContainerScaleIsSmall_When_DrawingDebugSkeleton_Then_CompensatesLinePositionsForSceneVisibility);
            RunTest(results, nameof(tests.Given_FullEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_Matches6000FrameYybReference),
                tests.Given_FullEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_Matches6000FrameYybReference);
            RunTest(results, nameof(tests.Given_ShortEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsRequestedSmokeWindow),
                tests.Given_ShortEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsRequestedSmokeWindow);
            RunTest(results, nameof(tests.Given_LongRetargetPrewarmConfigured_When_ResolvingPrewarmFrameCount_Then_DoesNotCapAtLegacyTenFrames),
                tests.Given_LongRetargetPrewarmConfigured_When_ResolvingPrewarmFrameCount_Then_DoesNotCapAtLegacyTenFrames);
            RunTest(results, nameof(tests.Given_PreviewOnlyImport_When_ResolvingPrewarmFrameCount_Then_DoesNotHoldStartPoseAcrossVisibleFrames),
                tests.Given_PreviewOnlyImport_When_ResolvingPrewarmFrameCount_Then_DoesNotHoldStartPoseAcrossVisibleFrames);
            RunTest(results, nameof(tests.Given_PreviewOnlyImport_When_ResolvingPrewarmVisibleYieldFrames_Then_DoesNotRenderExtraStartFrame),
                tests.Given_PreviewOnlyImport_When_ResolvingPrewarmVisibleYieldFrames_Then_DoesNotRenderExtraStartFrame);
            RunTest(results, nameof(tests.Given_PreviewOnlyImport_When_ResolvingStartDelay_Then_DoesNotHoldInitialPoseBeforePlayback),
                tests.Given_PreviewOnlyImport_When_ResolvingStartDelay_Then_DoesNotHoldInitialPoseBeforePlayback);
            RunTest(results, nameof(tests.Given_NonSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsDefaultClipTiming),
                tests.Given_NonSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsDefaultClipTiming);
            RunTest(results, nameof(tests.Given_EditorSmokeCaptureOverride_When_NormalizingResolution_Then_AllowsUhd4KAndRejectsInvalidInput),
                tests.Given_EditorSmokeCaptureOverride_When_NormalizingResolution_Then_AllowsUhd4KAndRejectsInvalidInput);
            RunTest(results, nameof(tests.Given_MainSceneFullRegressionEvidenceCommand_When_InspectingRunnerPlan_Then_UsesFullClipUhd4KAndHeadMiddleTailSamples),
                tests.Given_MainSceneFullRegressionEvidenceCommand_When_InspectingRunnerPlan_Then_UsesFullClipUhd4KAndHeadMiddleTailSamples);
            RunTest(results, nameof(tests.Given_MainRecordingFullRegressionEvidenceCommand_When_InspectingRunnerPlan_Then_UsesSatisfaction2Fbx),
                tests.Given_MainRecordingFullRegressionEvidenceCommand_When_InspectingRunnerPlan_Then_UsesSatisfaction2Fbx);
            RunTest(results, nameof(tests.Given_EditorSmokeSettingsSnapshot_When_CapturingAndClearing_Then_RestoresAllRuntimeSettings),
                tests.Given_EditorSmokeSettingsSnapshot_When_CapturingAndClearing_Then_RestoresAllRuntimeSettings);
            RunTest(results, nameof(tests.Given_RendererIsolationFailure_When_ApplyingEditorSmokeResult_Then_ConvertsSuccessToFailure),
                tests.Given_RendererIsolationFailure_When_ApplyingEditorSmokeResult_Then_ConvertsSuccessToFailure);
            RunTest(results, nameof(tests.Given_EditorSmokeSettingsSnapshot_When_StartDelayIsNaN_Then_RestoresNaNExactly),
                tests.Given_EditorSmokeSettingsSnapshot_When_StartDelayIsNaN_Then_RestoresNaNExactly);
            RunTest(results, nameof(tests.Given_EditorSmokeSettingsSnapshot_When_ClearRunsWithoutActiveSnapshot_Then_DoesNotChangeSettings),
                tests.Given_EditorSmokeSettingsSnapshot_When_ClearRunsWithoutActiveSnapshot_Then_DoesNotChangeSettings);

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

            Console.WriteLine($"FBXVmdPipelineEditorSmokePath tests completed; results written to {resultPath}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static void RunTest(List<TestResultRecord> results, string methodName, TestDelegate action)
        {
            string name = typeof(FBXVmdPipelineEditorSmokePathTests).FullName + "." + methodName;
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
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(FBXVmdPipelineEditorSmokePathTests).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

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
