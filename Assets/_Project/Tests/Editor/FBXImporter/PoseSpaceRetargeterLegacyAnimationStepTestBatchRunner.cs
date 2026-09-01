using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.FBXImporter
{
    public static class PoseSpaceRetargeterLegacyAnimationStepTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-PoseSpaceRetargeterLegacyAnimationStep.xml");
            }

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var results = new List<TestResultRecord>();
            var tests = new PoseSpaceRetargeterLegacyAnimationStepTests();
            var manualPoseTests = new PoseSpaceRetargeterHipsLocalPositionReferenceTests();

            RunTest(results, nameof(tests.Given_FullBodyReferenceEnabledWithoutFingerMuscles_When_DeterminingEditorPoseReferenceUse_Then_UsesReference),
                tests.Given_FullBodyReferenceEnabledWithoutFingerMuscles_When_DeterminingEditorPoseReferenceUse_Then_UsesReference);
            RunTest(results, nameof(tests.Given_BodyPositionSpike_When_DeterminingVisualPoseSmoothing_Then_SmoothsWithoutMuscleOnlySkip),
                tests.Given_BodyPositionSpike_When_DeterminingVisualPoseSmoothing_Then_SmoothsWithoutMuscleOnlySkip);
            RunTest(results, nameof(tests.Given_MainRecordingResidualBodyPositionSpike_When_DeterminingVisualPoseSmoothing_Then_Smooths),
                tests.Given_MainRecordingResidualBodyPositionSpike_When_DeterminingVisualPoseSmoothing_Then_Smooths);
            RunTest(results, nameof(tests.Given_MainRecordingHeadSpikeResidualBodyDelta_When_DeterminingVisualPoseSmoothing_Then_Smooths),
                tests.Given_MainRecordingHeadSpikeResidualBodyDelta_When_DeterminingVisualPoseSmoothing_Then_Smooths);
            RunTest(results, nameof(tests.Given_MuscleOnlySpike_When_DeterminingVisualPoseSmoothing_Then_DoesNotSmoothAndReportsMuscleOnlySkip),
                tests.Given_MuscleOnlySpike_When_DeterminingVisualPoseSmoothing_Then_DoesNotSmoothAndReportsMuscleOnlySkip);
            RunTest(results, nameof(tests.Given_LegacyAnimationStepSpike_When_DeterminingVisualPoseSmoothing_Then_Smooths),
                tests.Given_LegacyAnimationStepSpike_When_DeterminingVisualPoseSmoothing_Then_Smooths);
            RunTest(results, nameof(tests.Given_BodyPositionSpike_When_CalculatingVisualPoseSpikeWeight_Then_UsesStrongOutlierClamp),
                tests.Given_BodyPositionSpike_When_CalculatingVisualPoseSpikeWeight_Then_UsesStrongOutlierClamp);
            RunTest(results, nameof(tests.Given_MainRecordingHeadSpikeResidualBodyDelta_When_CalculatingVisualPoseSpikeWeight_Then_UsesStrongOutlierClamp),
                tests.Given_MainRecordingHeadSpikeResidualBodyDelta_When_CalculatingVisualPoseSpikeWeight_Then_UsesStrongOutlierClamp);
            RunTest(results, nameof(tests.Given_EditorReferenceShoulderMuscle_When_BlendingVisualPoseSpike_Then_PreservesCurrentReferenceValue),
                tests.Given_EditorReferenceShoulderMuscle_When_BlendingVisualPoseSpike_Then_PreservesCurrentReferenceValue);
            RunTest(results, nameof(tests.Given_RowLocalForearmStretchSpike_When_BlendingVisualPoseSpikeWithClamp_Then_LimitsBlendAroundCurrent),
                tests.Given_RowLocalForearmStretchSpike_When_BlendingVisualPoseSpikeWithClamp_Then_LimitsBlendAroundCurrent);
            RunTest(results, nameof(tests.Given_BodyPoseSpikeForearmStretchRow_When_BlendingVisualPoseSpikeWithClamp_Then_LimitsBlendAroundCurrent),
                tests.Given_BodyPoseSpikeForearmStretchRow_When_BlendingVisualPoseSpikeWithClamp_Then_LimitsBlendAroundCurrent);
            RunTest(results, nameof(tests.Given_Frame49StyleForearmValue_When_BlendingVisualPoseSpikeWithClamp_Then_KeepsDefaultSmoothing),
                tests.Given_Frame49StyleForearmValue_When_BlendingVisualPoseSpikeWithClamp_Then_KeepsDefaultSmoothing);
            RunTest(results, nameof(tests.Given_EditorReferenceForearmStretchMuscle_When_CheckingReferenceUse_Then_DoesNotUseReference),
                tests.Given_EditorReferenceForearmStretchMuscle_When_CheckingReferenceUse_Then_DoesNotUseReference);
            RunTest(results, nameof(tests.Given_EditorReferenceLeftUpperArmTwistMuscle_When_CheckingReferenceUse_Then_DoesNotUseReference),
                tests.Given_EditorReferenceLeftUpperArmTwistMuscle_When_CheckingReferenceUse_Then_DoesNotUseReference);
            RunTest(results, nameof(tests.Given_EditorReferenceRightUpperArmTwistMuscle_When_CheckingReferenceUse_Then_UsesReference),
                tests.Given_EditorReferenceRightUpperArmTwistMuscle_When_CheckingReferenceUse_Then_UsesReference);
            RunTest(results, nameof(tests.Given_UpperArmTwistPoseInput_When_TransformingRetargetInput_Then_FlipsTwistSign),
                tests.Given_UpperArmTwistPoseInput_When_TransformingRetargetInput_Then_FlipsTwistSign);
            RunTest(results, nameof(tests.Given_LeftArmTwistInputOpposesBoundedReference_When_AligningRetargetInput_Then_FlipsSignOnly),
                tests.Given_LeftArmTwistInputOpposesBoundedReference_When_AligningRetargetInput_Then_FlipsSignOnly);
            RunTest(results, nameof(tests.Given_LeftArmTwistInputOpposesOverrangeReference_When_AligningRetargetInput_Then_KeepsLiveInput),
                tests.Given_LeftArmTwistInputOpposesOverrangeReference_When_AligningRetargetInput_Then_KeepsLiveInput);
            RunTest(results, nameof(tests.Given_RightArmTwistInputSharesModerateOverrangeReferenceSign_When_AligningRetargetInput_Then_FlipsSignOnly),
                tests.Given_RightArmTwistInputSharesModerateOverrangeReferenceSign_When_AligningRetargetInput_Then_FlipsSignOnly);
            RunTest(results, nameof(tests.Given_RightArmTwistInputSharesLowerOverrangeReferenceSign_When_AligningRetargetInput_Then_KeepsLiveInput),
                tests.Given_RightArmTwistInputSharesLowerOverrangeReferenceSign_When_AligningRetargetInput_Then_KeepsLiveInput);
            RunTest(results, nameof(tests.Given_RightUpperArmTwistReferenceIsModeratelyOverrange_When_CheckingReferenceValueUse_Then_DoesNotUseReference),
                tests.Given_RightUpperArmTwistReferenceIsModeratelyOverrange_When_CheckingReferenceValueUse_Then_DoesNotUseReference);
            RunTest(results, nameof(tests.Given_RightUpperArmTwistReferenceIsBounded_When_CheckingReferenceValueUse_Then_UsesReference),
                tests.Given_RightUpperArmTwistReferenceIsBounded_When_CheckingReferenceValueUse_Then_UsesReference);
            RunTest(results, nameof(tests.Given_FootHipsAlignedResidualYawCorrection_When_TargetDirectionDiffers_Then_LimitsYawOnlyRotation),
                tests.Given_FootHipsAlignedResidualYawCorrection_When_TargetDirectionDiffers_Then_LimitsYawOnlyRotation);
            RunTest(results, nameof(tests.Given_OneFootResidualAlreadyInsideGate_When_ResolvingYawMaxAngle_Then_ProtectsPassingSide),
                tests.Given_OneFootResidualAlreadyInsideGate_When_ResolvingYawMaxAngle_Then_ProtectsPassingSide);
            RunTest(results, nameof(tests.Given_HipsLocalReferenceWouldIncreaseRightEndpointTargetGap_When_CheckingTargetGapGuard_Then_RejectsCandidate),
                tests.Given_HipsLocalReferenceWouldIncreaseRightEndpointTargetGap_When_CheckingTargetGapGuard_Then_RejectsCandidate);
            RunTest(results, nameof(tests.Given_HipsLocalReferenceDoesNotIncreaseRightEndpointTargetGap_When_CheckingTargetGapGuard_Then_KeepsCandidate),
                tests.Given_HipsLocalReferenceDoesNotIncreaseRightEndpointTargetGap_When_CheckingTargetGapGuard_Then_KeepsCandidate);
            RunTest(results, nameof(manualPoseTests.Given_ManualAnimatorBodyPositionXzReference_When_CalculatingSolverInput_Then_ClampsXzOnly),
                manualPoseTests.Given_ManualAnimatorBodyPositionXzReference_When_CalculatingSolverInput_Then_ClampsXzOnly);
            RunTest(results, nameof(manualPoseTests.Given_ManualAnimatorBodyPositionXzAxisScale_When_CalculatingSolverInput_Then_ReducesOnlyRequestedAxis),
                manualPoseTests.Given_ManualAnimatorBodyPositionXzAxisScale_When_CalculatingSolverInput_Then_ReducesOnlyRequestedAxis);
            RunTest(results, nameof(manualPoseTests.Given_LeftFootCurrentIsNegativeXPositiveZFromGhost_When_CalculatingSignCorrectedBodyPosition_Then_MovesTowardGhost),
                manualPoseTests.Given_LeftFootCurrentIsNegativeXPositiveZFromGhost_When_CalculatingSignCorrectedBodyPosition_Then_MovesTowardGhost);
            RunTest(results, nameof(manualPoseTests.Given_LeftFootRealizedZMovesOppositeIntended_When_InvertingBodyPositionZ_Then_FlipsOnlyZInput),
                manualPoseTests.Given_LeftFootRealizedZMovesOppositeIntended_When_InvertingBodyPositionZ_Then_FlipsOnlyZInput);
            RunTest(results, nameof(manualPoseTests.Given_BodyPositionXzCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier),
                manualPoseTests.Given_BodyPositionXzCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier);
            RunTest(results, nameof(manualPoseTests.Given_BodyPositionXzFrameGate_When_CalculatingWeight_Then_BlendsAtBothEdges),
                manualPoseTests.Given_BodyPositionXzFrameGate_When_CalculatingWeight_Then_BlendsAtBothEdges);
            RunTest(results, nameof(manualPoseTests.Given_InclusiveFrameGate_When_CheckingFrame_Then_PreservesDisabledInvalidAndRoundedBounds),
                manualPoseTests.Given_InclusiveFrameGate_When_CheckingFrame_Then_PreservesDisabledInvalidAndRoundedBounds);
            RunTest(results, nameof(manualPoseTests.Given_SingleFrameFallbackGate_When_EndIsInvalid_Then_UsesRoundedStartFrameOnly),
                manualPoseTests.Given_SingleFrameFallbackGate_When_EndIsInvalid_Then_UsesRoundedStartFrameOnly);
            RunTest(results, nameof(tests.Given_LeftArmTwistStageDiagnostics_When_InspectingRetargeter_Then_ExposesReadableProperties),
                tests.Given_LeftArmTwistStageDiagnostics_When_InspectingRetargeter_Then_ExposesReadableProperties);
            RunTest(results, nameof(tests.Given_RightArmTwistStageDiagnostics_When_InspectingRetargeter_Then_ExposesReadableProperties),
                tests.Given_RightArmTwistStageDiagnostics_When_InspectingRetargeter_Then_ExposesReadableProperties);
            RunTest(results, nameof(tests.Given_RetargetEndpointStagesWithFirstJump_When_AttributingStage_Then_ReportsExactlyFirstStageDelta),
                tests.Given_RetargetEndpointStagesWithFirstJump_When_AttributingStage_Then_ReportsExactlyFirstStageDelta);
            RunTest(results, nameof(tests.Given_RetargetEndpointStagesWithinTolerance_When_AttributingStage_Then_ReturnsNoAttribution),
                tests.Given_RetargetEndpointStagesWithinTolerance_When_AttributingStage_Then_ReturnsNoAttribution);
            RunTest(results, nameof(tests.Given_RetargetEndpointStageAttributionDiagnostics_When_InspectingRetargeter_Then_ExposesReadableProperties),
                tests.Given_RetargetEndpointStageAttributionDiagnostics_When_InspectingRetargeter_Then_ExposesReadableProperties);
            RunTest(results, nameof(tests.Given_LegTwistOnlyFullBodyPoseMask_When_CheckingReferenceMuscles_Then_AllowsOnlyLegInOutAndTwist),
                tests.Given_LegTwistOnlyFullBodyPoseMask_When_CheckingReferenceMuscles_Then_AllowsOnlyLegInOutAndTwist);
            RunTest(results, nameof(manualPoseTests.Given_BoundedMuscleOutputReference_When_OutputDriftsFromInput_Then_BlendsTowardInputWithinLimit),
                manualPoseTests.Given_BoundedMuscleOutputReference_When_OutputDriftsFromInput_Then_BlendsTowardInputWithinLimit);
            RunTest(results, nameof(manualPoseTests.Given_NonFiniteMuscleValues_When_CalculatingBoundedOutputReference_Then_PreservesFallbackPolicy),
                manualPoseTests.Given_NonFiniteMuscleValues_When_CalculatingBoundedOutputReference_Then_PreservesFallbackPolicy);
            RunTest(results, nameof(manualPoseTests.Given_BoundedMuscleOutputCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier),
                manualPoseTests.Given_BoundedMuscleOutputCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier);
            RunTest(results, nameof(tests.Given_PlayModeAndStalledState_When_CalculatingManualLegacyTime_Then_AdvancesByDeltaTimeAndSpeed),
                tests.Given_PlayModeAndStalledState_When_CalculatingManualLegacyTime_Then_AdvancesByDeltaTimeAndSpeed);
            RunTest(results, nameof(tests.Given_ZeroPlaybackSpeed_When_CalculatingManualLegacyTime_Then_UsesNormalPlaybackStep),
                tests.Given_ZeroPlaybackSpeed_When_CalculatingManualLegacyTime_Then_UsesNormalPlaybackStep);
            RunTest(results, nameof(tests.Given_ManualStepWouldPassClipEnd_When_CalculatingManualLegacyTime_Then_ClampsToClipLength),
                tests.Given_ManualStepWouldPassClipEnd_When_CalculatingManualLegacyTime_Then_ClampsToClipLength);
            RunTest(results, nameof(tests.Given_EditorMode_When_CalculatingManualLegacyTime_Then_DoesNotAdvance),
                tests.Given_EditorMode_When_CalculatingManualLegacyTime_Then_DoesNotAdvance);
            RunTest(results, nameof(tests.Given_CurrentTimeAlreadyAdvanced_When_CalculatingManualLegacyTime_Then_DoesNotAdvance),
                tests.Given_CurrentTimeAlreadyAdvanced_When_CalculatingManualLegacyTime_Then_DoesNotAdvance);
            RunTest(results, nameof(tests.Given_CurrentTimeLoopedBack_When_CalculatingManualLegacyTime_Then_DoesNotAdvance),
                tests.Given_CurrentTimeLoopedBack_When_CalculatingManualLegacyTime_Then_DoesNotAdvance);
            RunTest(results, nameof(tests.Given_TailSegmentWrapsToClipStart_When_CheckingLegacyEndWrap_Then_ClampsToClipEnd),
                tests.Given_TailSegmentWrapsToClipStart_When_CheckingLegacyEndWrap_Then_ClampsToClipEnd);
            RunTest(results, nameof(tests.Given_MidClipTimeJumpsBackward_When_CheckingLegacyEndWrap_Then_DoesNotClamp),
                tests.Given_MidClipTimeJumpsBackward_When_CheckingLegacyEndWrap_Then_DoesNotClamp);

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

            Console.WriteLine($"PoseSpaceRetargeterLegacyAnimationStep tests completed; results written to {resultPath}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static void RunTest(List<TestResultRecord> results, string methodName, TestDelegate action)
        {
            string name = typeof(PoseSpaceRetargeterLegacyAnimationStepTests).FullName + "." + methodName;
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
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(PoseSpaceRetargeterLegacyAnimationStepTests).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

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
