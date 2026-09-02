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
            var muscleOutputTests = new ManualPoseReferenceApplierMuscleOutputTests();
            var endpointTests = new RetargetingEndpointDiagnosticsTests();
            var muscleReferenceTests = new RetargetingMuscleReferencePolicyTests();

            RunTest(results, nameof(muscleReferenceTests.Given_MuscleReferencePolicyOwner_When_InspectingResponsibilities_Then_OwnsPurePolicies),
                muscleReferenceTests.Given_MuscleReferencePolicyOwner_When_InspectingResponsibilities_Then_OwnsPurePolicies);
            RunTest(results, nameof(muscleReferenceTests.Given_FullBodyReferenceEnabledWithoutFingerMuscles_When_DeterminingPoseReferenceUse_Then_UsesReference),
                muscleReferenceTests.Given_FullBodyReferenceEnabledWithoutFingerMuscles_When_DeterminingPoseReferenceUse_Then_UsesReference);
            RunTest(results, nameof(muscleReferenceTests.Given_FingerReferenceEnabledWithFingerMuscles_When_DeterminingPoseReferenceUse_Then_UsesReference),
                muscleReferenceTests.Given_FingerReferenceEnabledWithFingerMuscles_When_DeterminingPoseReferenceUse_Then_UsesReference);
            RunTest(results, nameof(muscleReferenceTests.Given_NoReferenceSource_When_DeterminingPoseReferenceUse_Then_DoesNotUseReference),
                muscleReferenceTests.Given_NoReferenceSource_When_DeterminingPoseReferenceUse_Then_DoesNotUseReference);
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
            RunTest(results, nameof(muscleReferenceTests.Given_ForearmStretchMuscle_When_CheckingHumanoidReferenceUse_Then_DoesNotUseReference),
                muscleReferenceTests.Given_ForearmStretchMuscle_When_CheckingHumanoidReferenceUse_Then_DoesNotUseReference);
            RunTest(results, nameof(muscleReferenceTests.Given_LeftUpperArmTwistMuscle_When_CheckingHumanoidReferenceUse_Then_DoesNotUseReference),
                muscleReferenceTests.Given_LeftUpperArmTwistMuscle_When_CheckingHumanoidReferenceUse_Then_DoesNotUseReference);
            RunTest(results, nameof(muscleReferenceTests.Given_RightUpperArmTwistMuscle_When_CheckingHumanoidReferenceUse_Then_UsesReference),
                muscleReferenceTests.Given_RightUpperArmTwistMuscle_When_CheckingHumanoidReferenceUse_Then_UsesReference);
            RunTest(results, nameof(muscleReferenceTests.Given_UpperArmTwistPoseInput_When_TransformingInput_Then_FlipsLeftTwistSignOnly),
                muscleReferenceTests.Given_UpperArmTwistPoseInput_When_TransformingInput_Then_FlipsLeftTwistSignOnly);
            RunTest(results, nameof(muscleReferenceTests.Given_LeftArmTwistInputOpposesBoundedReference_When_AligningInput_Then_FlipsSignOnly),
                muscleReferenceTests.Given_LeftArmTwistInputOpposesBoundedReference_When_AligningInput_Then_FlipsSignOnly);
            RunTest(results, nameof(muscleReferenceTests.Given_LeftArmTwistInputOpposesOverrangeReference_When_AligningInput_Then_KeepsLiveInput),
                muscleReferenceTests.Given_LeftArmTwistInputOpposesOverrangeReference_When_AligningInput_Then_KeepsLiveInput);
            RunTest(results, nameof(muscleReferenceTests.Given_RightArmTwistInputSharesModerateOverrangeReferenceSign_When_AligningInput_Then_FlipsSignOnly),
                muscleReferenceTests.Given_RightArmTwistInputSharesModerateOverrangeReferenceSign_When_AligningInput_Then_FlipsSignOnly);
            RunTest(results, nameof(muscleReferenceTests.Given_RightArmTwistInputSharesLowerOverrangeReferenceSign_When_AligningInput_Then_KeepsLiveInput),
                muscleReferenceTests.Given_RightArmTwistInputSharesLowerOverrangeReferenceSign_When_AligningInput_Then_KeepsLiveInput);
            RunTest(results, nameof(muscleReferenceTests.Given_RightUpperArmTwistReferenceIsModeratelyOverrange_When_CheckingValueUse_Then_DoesNotUseReference),
                muscleReferenceTests.Given_RightUpperArmTwistReferenceIsModeratelyOverrange_When_CheckingValueUse_Then_DoesNotUseReference);
            RunTest(results, nameof(muscleReferenceTests.Given_RightUpperArmTwistReferenceIsBounded_When_CheckingValueUse_Then_UsesReference),
                muscleReferenceTests.Given_RightUpperArmTwistReferenceIsBounded_When_CheckingValueUse_Then_UsesReference);
            RunTest(results, nameof(muscleReferenceTests.Given_NonFiniteReferenceValue_When_CheckingValueUse_Then_DoesNotUseReference),
                muscleReferenceTests.Given_NonFiniteReferenceValue_When_CheckingValueUse_Then_DoesNotUseReference);
            RunTest(results, nameof(muscleReferenceTests.Given_InvalidMuscleIndex_When_ApplyingPolicies_Then_PreservesExistingFallbacks),
                muscleReferenceTests.Given_InvalidMuscleIndex_When_ApplyingPolicies_Then_PreservesExistingFallbacks);
            RunTest(results, nameof(tests.Given_FootHipsAlignedResidualYawCorrection_When_TargetDirectionDiffers_Then_LimitsYawOnlyRotation),
                tests.Given_FootHipsAlignedResidualYawCorrection_When_TargetDirectionDiffers_Then_LimitsYawOnlyRotation);
            RunTest(results, nameof(tests.Given_OneFootResidualAlreadyInsideGate_When_ResolvingYawMaxAngle_Then_ProtectsPassingSide),
                tests.Given_OneFootResidualAlreadyInsideGate_When_ResolvingYawMaxAngle_Then_ProtectsPassingSide);
            RunTest(results, nameof(endpointTests.Given_HipsLocalReferenceWouldIncreaseEndpointTargetGap_When_CheckingTargetGapGuard_Then_RejectsCandidate),
                endpointTests.Given_HipsLocalReferenceWouldIncreaseEndpointTargetGap_When_CheckingTargetGapGuard_Then_RejectsCandidate);
            RunTest(results, nameof(endpointTests.Given_HipsLocalReferencePreservesEndpointTargetGap_When_CheckingTargetGapGuard_Then_KeepsCandidate),
                endpointTests.Given_HipsLocalReferencePreservesEndpointTargetGap_When_CheckingTargetGapGuard_Then_KeepsCandidate);
            RunTest(results, nameof(endpointTests.Given_EndpointTargetGapAtAllowedIncrease_When_CheckingTargetGapGuard_Then_KeepsCandidate),
                endpointTests.Given_EndpointTargetGapAtAllowedIncrease_When_CheckingTargetGapGuard_Then_KeepsCandidate);
            RunTest(results, nameof(endpointTests.Given_NegativeAllowedIncrease_When_CheckingTargetGapGuard_Then_ClampsAllowanceToZero),
                endpointTests.Given_NegativeAllowedIncrease_When_CheckingTargetGapGuard_Then_ClampsAllowanceToZero);
            RunTest(results, nameof(endpointTests.Given_NonFiniteEndpointPosition_When_CheckingTargetGapGuard_Then_FailsOpen),
                endpointTests.Given_NonFiniteEndpointPosition_When_CheckingTargetGapGuard_Then_FailsOpen);
            RunTest(results, nameof(endpointTests.Given_OnlyEndpointHeightChanges_When_CheckingTargetGapGuard_Then_IgnoresY),
                endpointTests.Given_OnlyEndpointHeightChanges_When_CheckingTargetGapGuard_Then_IgnoresY);
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
            RunTest(results, nameof(endpointTests.Given_RetargetEndpointStagesWithFirstJump_When_AttributingStage_Then_ReportsExactlyFirstStageDelta),
                endpointTests.Given_RetargetEndpointStagesWithFirstJump_When_AttributingStage_Then_ReportsExactlyFirstStageDelta);
            RunTest(results, nameof(endpointTests.Given_RetargetEndpointStagesWithinTolerance_When_AttributingStage_Then_ReturnsNoAttribution),
                endpointTests.Given_RetargetEndpointStagesWithinTolerance_When_AttributingStage_Then_ReturnsNoAttribution);
            RunTest(results, nameof(endpointTests.Given_StagePositionJumpAtThreshold_When_FindingFirstJump_Then_ReturnsNoJump),
                endpointTests.Given_StagePositionJumpAtThreshold_When_FindingFirstJump_Then_ReturnsNoJump);
            RunTest(results, nameof(endpointTests.Given_NonFiniteStagePositionBeforeValidJump_When_FindingFirstJump_Then_SkipsInvalidPairs),
                endpointTests.Given_NonFiniteStagePositionBeforeValidJump_When_FindingFirstJump_Then_SkipsInvalidPairs);
            RunTest(results, nameof(endpointTests.Given_NegativeStageJumpThreshold_When_FindingFirstJump_Then_ClampsThresholdToZero),
                endpointTests.Given_NegativeStageJumpThreshold_When_FindingFirstJump_Then_ClampsThresholdToZero);
            RunTest(results, nameof(endpointTests.Given_MismatchedStagePositionInputs_When_FindingFirstJump_Then_ReturnsNoJump),
                endpointTests.Given_MismatchedStagePositionInputs_When_FindingFirstJump_Then_ReturnsNoJump);
            RunTest(results, nameof(endpointTests.Given_EndpointDiagnosticCalculation_When_CheckingOwnership_Then_UsesDedicatedType),
                endpointTests.Given_EndpointDiagnosticCalculation_When_CheckingOwnership_Then_UsesDedicatedType);
            RunTest(results, nameof(tests.Given_RetargetEndpointStageAttributionDiagnostics_When_InspectingRetargeter_Then_ExposesReadableProperties),
                tests.Given_RetargetEndpointStageAttributionDiagnostics_When_InspectingRetargeter_Then_ExposesReadableProperties);
            RunTest(results, nameof(muscleReferenceTests.Given_LegTwistOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsOnlyLegInOutAndTwist),
                muscleReferenceTests.Given_LegTwistOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsOnlyLegInOutAndTwist);
            RunTest(results, nameof(muscleReferenceTests.Given_RightArmOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsOnlyRightArmChain),
                muscleReferenceTests.Given_RightArmOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsOnlyRightArmChain);
            RunTest(results, nameof(muscleReferenceTests.Given_LeftArmOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsOnlyLeftArmChain),
                muscleReferenceTests.Given_LeftArmOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsOnlyLeftArmChain);
            RunTest(results, nameof(muscleReferenceTests.Given_RightSleeveChainOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsSpineAndRightSleeveChain),
                muscleReferenceTests.Given_RightSleeveChainOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsSpineAndRightSleeveChain);
            RunTest(results, nameof(muscleOutputTests.Given_BoundedMuscleOutputReference_When_OutputDriftsFromInput_Then_BlendsTowardInputWithinLimit),
                muscleOutputTests.Given_BoundedMuscleOutputReference_When_OutputDriftsFromInput_Then_BlendsTowardInputWithinLimit);
            RunTest(results, nameof(muscleOutputTests.Given_NonFiniteMuscleValues_When_CalculatingBoundedOutputReference_Then_PreservesFallbackPolicy),
                muscleOutputTests.Given_NonFiniteMuscleValues_When_CalculatingBoundedOutputReference_Then_PreservesFallbackPolicy);
            RunTest(results, nameof(muscleOutputTests.Given_ValidMuscleIndex_When_ApplyingBoundedReference_Then_UpdatesOnlySelectedMuscle),
                muscleOutputTests.Given_ValidMuscleIndex_When_ApplyingBoundedReference_Then_UpdatesOnlySelectedMuscle);
            RunTest(results, nameof(muscleOutputTests.Given_InvalidOrUnchangedMuscle_When_ApplyingBoundedReference_Then_PreservesOutput),
                muscleOutputTests.Given_InvalidOrUnchangedMuscle_When_ApplyingBoundedReference_Then_PreservesOutput);
            RunTest(results, nameof(muscleOutputTests.Given_BoundedMuscleOutputCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier),
                muscleOutputTests.Given_BoundedMuscleOutputCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier);
            RunTest(results, nameof(manualPoseTests.Given_LowerBodySegmentDirectionReference_When_CalculatingCorrection_Then_RotatesTowardReferenceDirection),
                manualPoseTests.Given_LowerBodySegmentDirectionReference_When_CalculatingCorrection_Then_RotatesTowardReferenceDirection);
            RunTest(results, nameof(manualPoseTests.Given_LowerBodySegmentDirectionMaxAngle_When_CalculatingCorrection_Then_ClampsBeforeWeight),
                manualPoseTests.Given_LowerBodySegmentDirectionMaxAngle_When_CalculatingCorrection_Then_ClampsBeforeWeight);
            RunTest(results, nameof(manualPoseTests.Given_LowerBodySegmentDirectionAxisScale_When_CalculatingCorrection_Then_RemovesXzAxisContribution),
                manualPoseTests.Given_LowerBodySegmentDirectionAxisScale_When_CalculatingCorrection_Then_RemovesXzAxisContribution);
            RunTest(results, nameof(manualPoseTests.Given_LowerBodySegmentDirectionAxisScaleRemovesCorrection_When_CalculatingCorrection_Then_KeepsRotation),
                manualPoseTests.Given_LowerBodySegmentDirectionAxisScaleRemovesCorrection_When_CalculatingCorrection_Then_KeepsRotation);
            RunTest(results, nameof(manualPoseTests.Given_ZeroSegmentDirectionWeight_When_CalculatingCorrection_Then_KeepsRotation),
                manualPoseTests.Given_ZeroSegmentDirectionWeight_When_CalculatingCorrection_Then_KeepsRotation);
            RunTest(results, nameof(manualPoseTests.Given_NonFiniteSegmentDirection_When_CalculatingCorrection_Then_KeepsRotation),
                manualPoseTests.Given_NonFiniteSegmentDirection_When_CalculatingCorrection_Then_KeepsRotation);
            RunTest(results, nameof(manualPoseTests.Given_SegmentDirectionCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier),
                manualPoseTests.Given_SegmentDirectionCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier);
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
