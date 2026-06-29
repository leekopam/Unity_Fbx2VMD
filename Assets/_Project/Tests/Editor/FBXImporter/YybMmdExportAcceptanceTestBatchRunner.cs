using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.FBXImporter
{
    public static class YybMmdExportAcceptanceTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-YybMmdExportAcceptance.xml");
            }

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var results = new List<TestResultRecord>();

            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.MainAutoScene_UsesMmdSafeYybExportDefaults),
                () => new YybMmdExportSafetyDefaultsTests().MainAutoScene_UsesMmdSafeYybExportDefaults());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.YybMmdExportProductionPrefab_UsesAcceptedRuntimeVisualRecoveryDefaults),
                () => new YybMmdExportSafetyDefaultsTests().YybMmdExportProductionPrefab_UsesAcceptedRuntimeVisualRecoveryDefaults());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.YybMmdExportManualReferencePrefab_StaysClampOnlyBaseline),
                () => new YybMmdExportSafetyDefaultsTests().YybMmdExportManualReferencePrefab_StaysClampOnlyBaseline());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.MainSceneRootMotionPolicy_KeepsMainAutoAndMainRecordingStationaryRootCarrier),
                () => new YybMmdExportSafetyDefaultsTests().MainSceneRootMotionPolicy_KeepsMainAutoAndMainRecordingStationaryRootCarrier());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.MainRecordingRootMotionPolicy_DisablesPreviewRootCarrierForLimbIsolation),
                () => new YybMmdExportSafetyDefaultsTests().MainRecordingRootMotionPolicy_DisablesPreviewRootCarrierForLimbIsolation());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.MainScenes_KeepFinalIkFootGroundingExperimentDisabledByDefault),
                () => new YybMmdExportSafetyDefaultsTests().MainScenes_KeepFinalIkFootGroundingExperimentDisabledByDefault());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.MainScenes_PreserveRegressionSafeRetargetDefaultsForYybPlayback),
                () => new YybMmdExportSafetyDefaultsTests().MainScenes_PreserveRegressionSafeRetargetDefaultsForYybPlayback());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_ManualThumbOverrideSpreadExceedsSceneCap_When_ResolvingVisualLengthLimit_Then_KeepsConfiguredSmokeCap),
                () => new YybMmdExportSafetyDefaultsTests().Given_ManualThumbOverrideSpreadExceedsSceneCap_When_ResolvingVisualLengthLimit_Then_KeepsConfiguredSmokeCap());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_ManualThumbProjectionRiskExceedsSmokeLimit_When_CheckingPreserveBypass_Then_BypassesManualReferencePreserve),
                () => new YybMmdExportSafetyDefaultsTests().Given_ManualThumbProjectionRiskExceedsSmokeLimit_When_CheckingPreserveBypass_Then_BypassesManualReferencePreserve());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_FinalIkFootGroundingExperimentEnabled_When_ConfiguringTarget_Then_UsesBipedGrounderWithoutVrik),
                () => new YybMmdExportSafetyDefaultsTests().Given_FinalIkFootGroundingExperimentEnabled_When_ConfiguringTarget_Then_UsesBipedGrounderWithoutVrik());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_FinalIkFootGroundingExperimentWasEnabled_When_DisabledAndReconfigured_Then_DisablesAllFinalIkFootSolvers),
                () => new YybMmdExportSafetyDefaultsTests().Given_FinalIkFootGroundingExperimentWasEnabled_When_DisabledAndReconfigured_Then_DisablesAllFinalIkFootSolvers());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_FinalIkFootGroundingRuntimeOverride_When_Disabled_Then_CleansExistingFootSolversForBaseline),
                () => new YybMmdExportSafetyDefaultsTests().Given_FinalIkFootGroundingRuntimeOverride_When_Disabled_Then_CleansExistingFootSolversForBaseline());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_ManualAnimatorFootLocalRotationRuntimeOverride_When_Toggled_Then_OnlyChangesReferenceSwitchAndWeight),
                () => new YybMmdExportSafetyDefaultsTests().Given_ManualAnimatorFootLocalRotationRuntimeOverride_When_Toggled_Then_OnlyChangesReferenceSwitchAndWeight());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.MainSceneRuntimeOverrides_LowerBodyForceOffOptionsDisablePromotedSceneDefaults),
                () => new YybMmdExportSafetyDefaultsTests().MainSceneRuntimeOverrides_LowerBodyForceOffOptionsDisablePromotedSceneDefaults());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.MainSceneRuntimeOverrides_LegChainSegmentDetailOptionsPreservePromotedSceneDefaults),
                () => new YybMmdExportSafetyDefaultsTests().MainSceneRuntimeOverrides_LegChainSegmentDetailOptionsPreservePromotedSceneDefaults());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.MainSceneRuntimeOverrides_FullBodyForceOffOptionsDisablePromotedSceneDefaults),
                () => new YybMmdExportSafetyDefaultsTests().MainSceneRuntimeOverrides_FullBodyForceOffOptionsDisablePromotedSceneDefaults());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.MainSceneRuntimeOverrides_FullBodyPoseMaskOptionsKeepRuntimeScopeIsolated),
                () => new YybMmdExportSafetyDefaultsTests().MainSceneRuntimeOverrides_FullBodyPoseMaskOptionsKeepRuntimeScopeIsolated());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_ManualAnimatorFootToToesSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyToeSegments),
                () => new YybMmdExportSafetyDefaultsTests().Given_ManualAnimatorFootToToesSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyToeSegments());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_ManualAnimatorLegChainSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyRequestedSegments),
                () => new YybMmdExportSafetyDefaultsTests().Given_ManualAnimatorLegChainSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyRequestedSegments());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_RightLowerLegToFootSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyRightSide),
                () => new YybMmdExportSafetyDefaultsTests().Given_RightLowerLegToFootSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyRightSide());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_RightLowerLegToFootAxisAwareRuntimeOverride_When_Applied_Then_ScalesOnlyRightAxisXzContribution),
                () => new YybMmdExportSafetyDefaultsTests().Given_RightLowerLegToFootAxisAwareRuntimeOverride_When_Applied_Then_ScalesOnlyRightAxisXzContribution());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_RightLowerLegToFootSoftBlendRuntimeOverride_When_Applied_Then_ScalesOnlyRightCorrectionWeight),
                () => new YybMmdExportSafetyDefaultsTests().Given_RightLowerLegToFootSoftBlendRuntimeOverride_When_Applied_Then_ScalesOnlyRightCorrectionWeight());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_RightLowerLegToFootFrameGatedRuntimeOverride_When_Applied_Then_GatesOnlyRightCapWindow),
                () => new YybMmdExportSafetyDefaultsTests().Given_RightLowerLegToFootFrameGatedRuntimeOverride_When_Applied_Then_GatesOnlyRightCapWindow());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_RightLowerLegToFootEndpointBlendRuntimeOverride_When_Applied_Then_ScalesOnlyRightEndpointDriftCompensation),
                () => new YybMmdExportSafetyDefaultsTests().Given_RightLowerLegToFootEndpointBlendRuntimeOverride_When_Applied_Then_ScalesOnlyRightEndpointDriftCompensation());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_PostSetHumanPoseEndpointRuntimeOverride_When_Applied_Then_OnlyChangesEndpointClampSwitchAndCaps),
                () => new YybMmdExportSafetyDefaultsTests().Given_PostSetHumanPoseEndpointRuntimeOverride_When_Applied_Then_OnlyChangesEndpointClampSwitchAndCaps());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_PostSetHumanPoseEndpointPositiveZScaleRuntimeOverride_When_Applied_Then_ScalesOnlyPositiveZCarrier),
                () => new YybMmdExportSafetyDefaultsTests().Given_PostSetHumanPoseEndpointPositiveZScaleRuntimeOverride_When_Applied_Then_ScalesOnlyPositiveZCarrier());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_PostSetHumanPoseEndpointFrameGatedRuntimeOverride_When_Applied_Then_PreservesDiagnosticWindow),
                () => new YybMmdExportSafetyDefaultsTests().Given_PostSetHumanPoseEndpointFrameGatedRuntimeOverride_When_Applied_Then_PreservesDiagnosticWindow());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_PostSetHumanPoseEndpointPositiveZScale_When_CorrectionExceedsCap_Then_DoesNotIncreaseBaselineClampedX),
                () => new YybMmdExportSafetyDefaultsTests().Given_PostSetHumanPoseEndpointPositiveZScale_When_CorrectionExceedsCap_Then_DoesNotIncreaseBaselineClampedX());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_PostSetHumanPoseEndpointToesBlend_When_RecalculatingDirection_Then_CanUseFootOnlyOrFootToesAverage),
                () => new YybMmdExportSafetyDefaultsTests().Given_PostSetHumanPoseEndpointToesBlend_When_RecalculatingDirection_Then_CanUseFootOnlyOrFootToesAverage());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_RuntimeMmdIkDeltaRecoveryDebtOverride_When_ApplyingToRecorder_Then_SetsDebtRecoveryWindow),
                () => new YybMmdExportSafetyDefaultsTests().Given_RuntimeMmdIkDeltaRecoveryDebtOverride_When_ApplyingToRecorder_Then_SetsDebtRecoveryWindow());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_RuntimeMmdIkDeltaRecoveryHoldOverride_When_ApplyingToRecorder_Then_SetsHoldWindow),
                () => new YybMmdExportSafetyDefaultsTests().Given_RuntimeMmdIkDeltaRecoveryHoldOverride_When_ApplyingToRecorder_Then_SetsHoldWindow());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_ProjectFbxExists_When_ResolvingYybReferenceClipPath_Then_UsesProjectReferenceBeforeControlledImport),
                () => new YybMmdExportSafetyDefaultsTests().Given_ProjectFbxExists_When_ResolvingYybReferenceClipPath_Then_UsesProjectReferenceBeforeControlledImport());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_FrameCounts_When_BuildingSummaryFrameRoleDiagnostics_Then_SeparatesReferenceTargetFromRecordedBaselines),
                () => new YybMmdExportSafetyDefaultsTests().Given_FrameCounts_When_BuildingSummaryFrameRoleDiagnostics_Then_SeparatesReferenceTargetFromRecordedBaselines());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesCandidateFramingToReferenceMp4),
                () => new YybMmdExportSafetyDefaultsTests().Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesCandidateFramingToReferenceMp4());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ReportsCandidateTimingCoverageAgainstReferenceSamples),
                () => new YybMmdExportSafetyDefaultsTests().Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ReportsCandidateTimingCoverageAgainstReferenceSamples());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_TailSegmentStart_When_BuildingSummaryFrameRoleDiagnostics_Then_UsesMatchingReferenceMp4Window),
                () => new YybMmdExportSafetyDefaultsTests().Given_TailSegmentStart_When_BuildingSummaryFrameRoleDiagnostics_Then_UsesMatchingReferenceMp4Window());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesTimeMatchedCandidateAndReferenceFraming),
                () => new YybMmdExportSafetyDefaultsTests().Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesTimeMatchedCandidateAndReferenceFraming());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesBandedImageSpaceLimbSpans),
                () => new YybMmdExportSafetyDefaultsTests().Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesBandedImageSpaceLimbSpans());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesSilhouetteProfileLimbSpans),
                () => new YybMmdExportSafetyDefaultsTests().Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesSilhouetteProfileLimbSpans());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesDeterministicImageSpaceKeypoints),
                () => new YybMmdExportSafetyDefaultsTests().Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesDeterministicImageSpaceKeypoints());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_HairLikeSilhouetteExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints),
                () => new YybMmdExportSafetyDefaultsTests().Given_HairLikeSilhouetteExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_DarkTealHairShadowExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints),
                () => new YybMmdExportSafetyDefaultsTests().Given_DarkTealHairShadowExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_RawCandidateFailsAndCorrectedCandidatePasses_When_BuildingCandidateArtifactSelection_Then_SelectsCorrectedWithoutHidingRaw),
                () => new YybMmdExportSafetyDefaultsTests().Given_RawCandidateFailsAndCorrectedCandidatePasses_When_BuildingCandidateArtifactSelection_Then_SelectsCorrectedWithoutHidingRaw());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_SelectedCorrectedCandidateFilesExist_When_BuildingCandidateArtifactSelection_Then_MarksFinalExportAcceptanceArtifact),
                () => new YybMmdExportSafetyDefaultsTests().Given_SelectedCorrectedCandidateFilesExist_When_BuildingCandidateArtifactSelection_Then_MarksFinalExportAcceptanceArtifact());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_CorrectedMetricsPassAndVmdIsRawCopy_When_BuildingCandidateArtifactSelection_Then_KeepsDiagnosticOnly),
                () => new YybMmdExportSafetyDefaultsTests().Given_CorrectedMetricsPassAndVmdIsRawCopy_When_BuildingCandidateArtifactSelection_Then_KeepsDiagnosticOnly());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_SelectedCorrectedCandidateManifestIsMissing_When_BuildingCandidateArtifactSelection_Then_WritesManifestAndMarksAcceptanceArtifact),
                () => new YybMmdExportSafetyDefaultsTests().Given_SelectedCorrectedCandidateManifestIsMissing_When_BuildingCandidateArtifactSelection_Then_WritesManifestAndMarksAcceptanceArtifact());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_RawMainAutoCandidatePasses_When_BuildingCandidateArtifactSelection_Then_MarksRawExportAcceptanceArtifact),
                () => new YybMmdExportSafetyDefaultsTests().Given_RawMainAutoCandidatePasses_When_BuildingCandidateArtifactSelection_Then_MarksRawExportAcceptanceArtifact());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_MainRecordingAndMainAutoSummaries_When_BuildingCandidateArtifactSelection_Then_SelectsMainAutoAcceptanceArtifact),
                () => new YybMmdExportSafetyDefaultsTests().Given_MainRecordingAndMainAutoSummaries_When_BuildingCandidateArtifactSelection_Then_SelectsMainAutoAcceptanceArtifact());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_FrameQualitySummaryFails_When_BuildingCompletionFailures_Then_PromotesToRunFailure),
                () => new YybMmdExportSafetyDefaultsTests().Given_FrameQualitySummaryFails_When_BuildingCompletionFailures_Then_PromotesToRunFailure());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_RawVerticalResidualHasReferenceAlignedCorrectedPass_When_BuildingCompletionFailures_Then_KeepsRawDiagnosticOnly),
                () => new YybMmdExportSafetyDefaultsTests().Given_RawVerticalResidualHasReferenceAlignedCorrectedPass_When_BuildingCompletionFailures_Then_KeepsRawDiagnosticOnly());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_CaptureModes_When_CheckingSummaryCandidateMode_Then_IncludesBothMainScenes),
                () => new YybMmdExportSafetyDefaultsTests().Given_CaptureModes_When_CheckingSummaryCandidateMode_Then_IncludesBothMainScenes());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_MainSceneCandidateModes_When_ResolvingIntegratedVerticalSolveRole_Then_ReplayAndMainAutoUseSeparateRoles),
                () => new YybMmdExportSafetyDefaultsTests().Given_MainSceneCandidateModes_When_ResolvingIntegratedVerticalSolveRole_Then_ReplayAndMainAutoUseSeparateRoles());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_MainSceneCandidateFailedButHasMetricsAndVmd_When_CheckingFrameQualityEligibility_Then_KeepsDiagnosticCandidate),
                () => new YybMmdExportSafetyDefaultsTests().Given_MainSceneCandidateFailedButHasMetricsAndVmd_When_CheckingFrameQualityEligibility_Then_KeepsDiagnosticCandidate());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_MainRecordingSmokeFailedButVmdExists_When_BuildingStableCandidate_Then_CopiesVmdAndKeepsFailure),
                () => new YybMmdExportSafetyDefaultsTests().Given_MainRecordingSmokeFailedButVmdExists_When_BuildingStableCandidate_Then_CopiesVmdAndKeepsFailure());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_HeadWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock),
                () => new YybMmdExportSafetyDefaultsTests().Given_HeadWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock());
            RunTest(results, typeof(YybMmdExportSafetyDefaultsTests).FullName + "." +
                nameof(YybMmdExportSafetyDefaultsTests.Given_NonZeroClipWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock),
                () => new YybMmdExportSafetyDefaultsTests().Given_NonZeroClipWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock());

            var namingTests = new VmdNamingContractTests();
            RunTest(results, typeof(VmdNamingContractTests).FullName + "." +
                nameof(VmdNamingContractTests.Given_ExportedYybVmd_When_ReadingBoneNames_Then_MmdStandardNamesAreWritten),
                namingTests.Given_ExportedYybVmd_When_ReadingBoneNames_Then_MmdStandardNamesAreWritten);

            double duration = Math.Max(0.001, (DateTimeOffset.UtcNow - start).TotalSeconds);
            string resultDirectory = Path.GetDirectoryName(resultPath);
            if (!string.IsNullOrEmpty(resultDirectory))
            {
                Directory.CreateDirectory(resultDirectory);
            }
            File.WriteAllText(resultPath, BuildXml(results, duration));

            int failed = 0;
            foreach (var result in results)
            {
                if (result.Failure != null)
                {
                    failed++;
                    Console.Error.WriteLine(result.Failure);
                }
            }

            Console.WriteLine($"YybMmdExportAcceptance tests completed; results written to {resultPath}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static void RunTest(List<TestResultRecord> results, string name, TestDelegate action)
        {
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
            foreach (var result in results)
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
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(YybMmdExportAcceptanceTestBatchRunner).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

            foreach (var result in results)
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
