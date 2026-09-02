using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.FBXImporter
{
    public static class MmdExportAcceptanceTestBatchRunner
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

            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.MainAutoScene_UsesMmdSafeYybExportDefaults),
                () => new MmdExportSafetyDefaultsTests().MainAutoScene_UsesMmdSafeYybExportDefaults());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.YybMmdExportProductionPrefab_UsesAcceptedRuntimeVisualRecoveryDefaults),
                () => new MmdExportSafetyDefaultsTests().YybMmdExportProductionPrefab_UsesAcceptedRuntimeVisualRecoveryDefaults());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.YybMmdExportManualReferencePrefab_StaysClampOnlyBaseline),
                () => new MmdExportSafetyDefaultsTests().YybMmdExportManualReferencePrefab_StaysClampOnlyBaseline());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.MainSceneRootMotionPolicy_KeepsMainAutoStationaryAndMainRecordingMovingRootCarrier),
                () => new MmdExportSafetyDefaultsTests().MainSceneRootMotionPolicy_KeepsMainAutoStationaryAndMainRecordingMovingRootCarrier());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.MainRecordingRootMotionPolicy_EnablesMovingRootCarrierForNaturalMotion),
                () => new MmdExportSafetyDefaultsTests().MainRecordingRootMotionPolicy_EnablesMovingRootCarrierForNaturalMotion());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.MainScenes_KeepFinalIkFootGroundingExperimentDisabledByDefault),
                () => new MmdExportSafetyDefaultsTests().MainScenes_KeepFinalIkFootGroundingExperimentDisabledByDefault());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.MainScenes_PreserveRegressionSafeRetargetDefaultsForYybPlayback),
                () => new MmdExportSafetyDefaultsTests().MainScenes_PreserveRegressionSafeRetargetDefaultsForYybPlayback());
            RunTest(results, typeof(HumanoidThumbDeformationGuardSmokeRiskTests).FullName + "." +
                nameof(HumanoidThumbDeformationGuardSmokeRiskTests.Given_ManualThumbOverrideSpreadExceedsSceneCap_When_ResolvingVisualLengthLimit_Then_KeepsConfiguredSmokeCap),
                () => new HumanoidThumbDeformationGuardSmokeRiskTests().Given_ManualThumbOverrideSpreadExceedsSceneCap_When_ResolvingVisualLengthLimit_Then_KeepsConfiguredSmokeCap());
            RunTest(results, typeof(HumanoidThumbDeformationGuardSmokeRiskTests).FullName + "." +
                nameof(HumanoidThumbDeformationGuardSmokeRiskTests.Given_ManualThumbProjectionRiskExceedsSmokeLimit_When_CheckingPreserveBypass_Then_BypassesManualReferencePreserve),
                () => new HumanoidThumbDeformationGuardSmokeRiskTests().Given_ManualThumbProjectionRiskExceedsSmokeLimit_When_CheckingPreserveBypass_Then_BypassesManualReferencePreserve());
            RunTest(results, typeof(FBXConversionCoordinatorTargetTests).FullName + "." +
                nameof(FBXConversionCoordinatorTargetTests.Given_FinalIkFootGroundingExperimentEnabled_When_ConfiguringTarget_Then_UsesBipedGrounderWithoutVrik),
                () => new FBXConversionCoordinatorTargetTests().Given_FinalIkFootGroundingExperimentEnabled_When_ConfiguringTarget_Then_UsesBipedGrounderWithoutVrik());
            RunTest(results, typeof(FBXConversionCoordinatorTargetTests).FullName + "." +
                nameof(FBXConversionCoordinatorTargetTests.Given_FinalIkFootGroundingExperimentWasEnabled_When_DisabledAndReconfigured_Then_DisablesAllFinalIkFootSolvers),
                () => new FBXConversionCoordinatorTargetTests().Given_FinalIkFootGroundingExperimentWasEnabled_When_DisabledAndReconfigured_Then_DisablesAllFinalIkFootSolvers());
            RunTest(results, typeof(FinalIkFootGroundingRuntimeOverrideApplierTests).FullName + "." +
                nameof(FinalIkFootGroundingRuntimeOverrideApplierTests.Given_FinalIkFootGroundingRuntimeOverride_When_Disabled_Then_CleansExistingFootSolversForBaseline),
                () => new FinalIkFootGroundingRuntimeOverrideApplierTests().Given_FinalIkFootGroundingRuntimeOverride_When_Disabled_Then_CleansExistingFootSolversForBaseline());
            RunTest(results, typeof(ManualPoseReferenceRuntimeOverrideApplierTests).FullName + "." +
                nameof(ManualPoseReferenceRuntimeOverrideApplierTests.Given_GenericCharacterPipeline_When_TogglingFootLocalRotation_Then_ChangesOnlyFootRotationSettings),
                () => new ManualPoseReferenceRuntimeOverrideApplierTests().Given_GenericCharacterPipeline_When_TogglingFootLocalRotation_Then_ChangesOnlyFootRotationSettings());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.MainSceneRuntimeOverrides_LowerBodyForceOffOptionsDisablePromotedSceneDefaults),
                () => new MmdExportSafetyDefaultsTests().MainSceneRuntimeOverrides_LowerBodyForceOffOptionsDisablePromotedSceneDefaults());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.MainSceneRuntimeOverrides_LegChainSegmentDetailOptionsPreservePromotedSceneDefaults),
                () => new MmdExportSafetyDefaultsTests().MainSceneRuntimeOverrides_LegChainSegmentDetailOptionsPreservePromotedSceneDefaults());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.MainSceneRuntimeOverrides_FullBodyForceOffOptionsDisablePromotedSceneDefaults),
                () => new MmdExportSafetyDefaultsTests().MainSceneRuntimeOverrides_FullBodyForceOffOptionsDisablePromotedSceneDefaults());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.MainSceneRuntimeOverrides_FullBodyPoseMaskOptionsKeepRuntimeScopeIsolated),
                () => new MmdExportSafetyDefaultsTests().MainSceneRuntimeOverrides_FullBodyPoseMaskOptionsKeepRuntimeScopeIsolated());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.MainSceneRuntimeOverrides_SetHumanPoseRightLegTwistOutputKeepsRuntimeScopeIsolated),
                () => new MmdExportSafetyDefaultsTests().MainSceneRuntimeOverrides_SetHumanPoseRightLegTwistOutputKeepsRuntimeScopeIsolated());
            RunTest(results, typeof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests).FullName + "." +
                nameof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests.Given_ManualAnimatorFootToToesSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyToeSegments),
                () => new ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests().Given_ManualAnimatorFootToToesSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyToeSegments());
            RunTest(results, typeof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests).FullName + "." +
                nameof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests.Given_ManualAnimatorLegChainSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyRequestedSegments),
                () => new ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests().Given_ManualAnimatorLegChainSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyRequestedSegments());
            RunTest(results, typeof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests).FullName + "." +
                nameof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests.Given_RightLowerLegToFootSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyRightSide),
                () => new ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests().Given_RightLowerLegToFootSegmentDirectionRuntimeOverride_When_Applied_Then_LimitsOnlyRightSide());
            RunTest(results, typeof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests).FullName + "." +
                nameof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests.Given_RightLowerLegToFootAxisAwareRuntimeOverride_When_Applied_Then_ScalesOnlyRightAxisXzContribution),
                () => new ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests().Given_RightLowerLegToFootAxisAwareRuntimeOverride_When_Applied_Then_ScalesOnlyRightAxisXzContribution());
            RunTest(results, typeof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests).FullName + "." +
                nameof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests.Given_RightLowerLegToFootSoftBlendRuntimeOverride_When_Applied_Then_ScalesOnlyRightCorrectionWeight),
                () => new ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests().Given_RightLowerLegToFootSoftBlendRuntimeOverride_When_Applied_Then_ScalesOnlyRightCorrectionWeight());
            RunTest(results, typeof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests).FullName + "." +
                nameof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests.Given_RightLowerLegToFootFrameGatedRuntimeOverride_When_Applied_Then_GatesOnlyRightCapWindow),
                () => new ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests().Given_RightLowerLegToFootFrameGatedRuntimeOverride_When_Applied_Then_GatesOnlyRightCapWindow());
            RunTest(results, typeof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests).FullName + "." +
                nameof(ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests.Given_RightLowerLegToFootEndpointBlendRuntimeOverride_When_Applied_Then_ScalesOnlyRightEndpointDriftCompensation),
                () => new ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests().Given_RightLowerLegToFootEndpointBlendRuntimeOverride_When_Applied_Then_ScalesOnlyRightEndpointDriftCompensation());
            RunTest(results, typeof(HumanPoseEndpointRuntimeOverrideApplierTests).FullName + "." +
                nameof(HumanPoseEndpointRuntimeOverrideApplierTests.Given_PostSetEndpointSettings_When_Applied_Then_ClampsAndScopesValues),
                () => new HumanPoseEndpointRuntimeOverrideApplierTests().Given_PostSetEndpointSettings_When_Applied_Then_ClampsAndScopesValues());
            RunTest(results, typeof(HumanPoseEndpointRuntimeOverrideApplierTests).FullName + "." +
                nameof(HumanPoseEndpointRuntimeOverrideApplierTests.Given_PostSetEndpointSettings_When_Disabled_Then_ClearsConditionalValuesAndPreservesCaps),
                () => new HumanPoseEndpointRuntimeOverrideApplierTests().Given_PostSetEndpointSettings_When_Disabled_Then_ClearsConditionalValuesAndPreservesCaps());
            RunTest(results, typeof(RetargetingEndpointDiagnosticsTests).FullName + "." +
                nameof(RetargetingEndpointDiagnosticsTests.Given_PostSetHumanPoseEndpointPositiveZScale_When_CalculatingDesiredFootPosition_Then_ScalesOnlyPositiveZCarrier),
                () => new RetargetingEndpointDiagnosticsTests().Given_PostSetHumanPoseEndpointPositiveZScale_When_CalculatingDesiredFootPosition_Then_ScalesOnlyPositiveZCarrier());
            RunTest(results, typeof(RetargetingEndpointDiagnosticsTests).FullName + "." +
                nameof(RetargetingEndpointDiagnosticsTests.Given_PostSetHumanPoseEndpointPositiveZScale_When_CorrectionExceedsCap_Then_DoesNotIncreaseBaselineClampedX),
                () => new RetargetingEndpointDiagnosticsTests().Given_PostSetHumanPoseEndpointPositiveZScale_When_CorrectionExceedsCap_Then_DoesNotIncreaseBaselineClampedX());
            RunTest(results, typeof(RetargetingEndpointDiagnosticsTests).FullName + "." +
                nameof(RetargetingEndpointDiagnosticsTests.Given_PostSetHumanPoseEndpointToesBlend_When_RecalculatingDirection_Then_CanUseFootOnlyOrFootToesAverage),
                () => new RetargetingEndpointDiagnosticsTests().Given_PostSetHumanPoseEndpointToesBlend_When_RecalculatingDirection_Then_CanUseFootOnlyOrFootToesAverage());
            RunTest(results, typeof(HumanPoseEndpointRuntimeOverrideApplierTests).FullName + "." +
                nameof(HumanPoseEndpointRuntimeOverrideApplierTests.Given_PreSetEndpointSettings_When_Applied_Then_ClampsValuesAndScopesChanges),
                () => new HumanPoseEndpointRuntimeOverrideApplierTests().Given_PreSetEndpointSettings_When_Applied_Then_ClampsValuesAndScopesChanges());
            RunTest(results, typeof(HumanPoseEndpointRuntimeOverrideApplierTests).FullName + "." +
                nameof(HumanPoseEndpointRuntimeOverrideApplierTests.Given_PreSetEndpointFlags_When_Applied_Then_PreservesIndependentMappings),
                () => new HumanPoseEndpointRuntimeOverrideApplierTests().Given_PreSetEndpointFlags_When_Applied_Then_PreservesIndependentMappings());
            RunTest(results, typeof(HumanPoseEndpointRuntimeOverrideApplierTests).FullName + "." +
                nameof(HumanPoseEndpointRuntimeOverrideApplierTests.Given_PreSetEndpointSettings_When_Disabled_Then_ClearsConditionalValuesAndPreservesCaps),
                () => new HumanPoseEndpointRuntimeOverrideApplierTests().Given_PreSetEndpointSettings_When_Disabled_Then_ClearsConditionalValuesAndPreservesCaps());
            RunTest(results, typeof(RetargetingRuntimeOverrideApplierTests).FullName + "." +
                nameof(RetargetingRuntimeOverrideApplierTests.Given_TargetHumanoidBonePositionLock_When_Toggled_Then_ChangesOnlySkeletonBasisLock),
                () => new RetargetingRuntimeOverrideApplierTests().Given_TargetHumanoidBonePositionLock_When_Toggled_Then_ChangesOnlySkeletonBasisLock());
            RunTest(results, typeof(ManualPoseReferenceRuntimeOverrideApplierTests).FullName + "." +
                nameof(ManualPoseReferenceRuntimeOverrideApplierTests.Given_GenericCharacterPipeline_When_ApplyingBodyPositionXz_Then_ClampsAndScopesSettings),
                () => new ManualPoseReferenceRuntimeOverrideApplierTests().Given_GenericCharacterPipeline_When_ApplyingBodyPositionXz_Then_ClampsAndScopesSettings());
            RunTest(results, typeof(VmdIkDeltaGuardRuntimeOverrideApplierTests).FullName + "." +
                nameof(VmdIkDeltaGuardRuntimeOverrideApplierTests.Given_RecoveryDebtThreshold_When_Applying_Then_SetsDebtRecoveryWindow),
                () => new VmdIkDeltaGuardRuntimeOverrideApplierTests().Given_RecoveryDebtThreshold_When_Applying_Then_SetsDebtRecoveryWindow());
            RunTest(results, typeof(VmdIkDeltaGuardRuntimeOverrideApplierTests).FullName + "." +
                nameof(VmdIkDeltaGuardRuntimeOverrideApplierTests.Given_RecoveryHoldFrames_When_Applying_Then_SetsHoldWindow),
                () => new VmdIkDeltaGuardRuntimeOverrideApplierTests().Given_RecoveryHoldFrames_When_Applying_Then_SetsHoldWindow());
            var referenceFrameCountResolverTests = new ReferenceFrameCountResolverTests();
            RunTest(results, typeof(ReferenceFrameCountResolverTests).FullName + "." +
                nameof(ReferenceFrameCountResolverTests.Given_KnownReferenceProfile_When_ClipAndRequestCoverDuration_Then_UsesKnownFrameCount),
                referenceFrameCountResolverTests.Given_KnownReferenceProfile_When_ClipAndRequestCoverDuration_Then_UsesKnownFrameCount);
            RunTest(results, typeof(ReferenceFrameCountResolverTests).FullName + "." +
                nameof(ReferenceFrameCountResolverTests.Given_CandidateFrameCountDiffersFromReference_When_ResolvingSummaryTarget_Then_KeepsReferenceTarget),
                referenceFrameCountResolverTests.Given_CandidateFrameCountDiffersFromReference_When_ResolvingSummaryTarget_Then_KeepsReferenceTarget);
            RunTest(results, typeof(ReferenceFrameCountResolverTests).FullName + "." +
                nameof(ReferenceFrameCountResolverTests.Given_CandidateFrameCountIsUnavailable_When_ResolvingSummaryTarget_Then_KeepsReferenceTarget),
                referenceFrameCountResolverTests.Given_CandidateFrameCountIsUnavailable_When_ResolvingSummaryTarget_Then_KeepsReferenceTarget);
            RunTest(results, typeof(ReferenceFrameCountResolverTests).FullName + "." +
                nameof(ReferenceFrameCountResolverTests.Given_SummaryTargetPolicy_When_InspectingRunner_Then_PureCalculationOverloadIsAbsent),
                referenceFrameCountResolverTests.Given_SummaryTargetPolicy_When_InspectingRunner_Then_PureCalculationOverloadIsAbsent);
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.Given_ProjectFbxExists_When_ResolvingYybReferenceClipPath_Then_UsesProjectReferenceBeforeControlledImport),
                () => new MmdExportSafetyDefaultsTests().Given_ProjectFbxExists_When_ResolvingYybReferenceClipPath_Then_UsesProjectReferenceBeforeControlledImport());
            RunTest(results, typeof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests).FullName + "." +
                nameof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests.Given_FrameCounts_When_BuildingSummaryFrameRoleDiagnostics_Then_SeparatesReferenceTargetFromRecordedBaselines),
                () => new YybVisualComparisonFrameRoleDiagnosticsBuilderTests().Given_FrameCounts_When_BuildingSummaryFrameRoleDiagnostics_Then_SeparatesReferenceTargetFromRecordedBaselines());
            RunTest(results, typeof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests).FullName + "." +
                nameof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests.Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesCandidateFramingToReferenceMp4),
                () => new YybVisualComparisonFrameRoleDiagnosticsBuilderTests().Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesCandidateFramingToReferenceMp4());
            RunTest(results, typeof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests).FullName + "." +
                nameof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests.Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ReportsCandidateTimingCoverageAgainstReferenceSamples),
                () => new YybVisualComparisonFrameRoleDiagnosticsBuilderTests().Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ReportsCandidateTimingCoverageAgainstReferenceSamples());
            RunTest(results, typeof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests).FullName + "." +
                nameof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests.Given_TailSegmentStart_When_BuildingSummaryFrameRoleDiagnostics_Then_UsesMatchingReferenceMp4Window),
                () => new YybVisualComparisonFrameRoleDiagnosticsBuilderTests().Given_TailSegmentStart_When_BuildingSummaryFrameRoleDiagnostics_Then_UsesMatchingReferenceMp4Window());
            RunTest(results, typeof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests).FullName + "." +
                nameof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests.Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesTimeMatchedCandidateAndReferenceFraming),
                () => new YybVisualComparisonFrameRoleDiagnosticsBuilderTests().Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesTimeMatchedCandidateAndReferenceFraming());
            RunTest(results, typeof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests).FullName + "." +
                nameof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests.Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesBandedImageSpaceLimbSpans),
                () => new YybVisualComparisonFrameRoleDiagnosticsBuilderTests().Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesBandedImageSpaceLimbSpans());
            RunTest(results, typeof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests).FullName + "." +
                nameof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests.Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesSilhouetteProfileLimbSpans),
                () => new YybVisualComparisonFrameRoleDiagnosticsBuilderTests().Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesSilhouetteProfileLimbSpans());
            RunTest(results, typeof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests).FullName + "." +
                nameof(YybVisualComparisonFrameRoleDiagnosticsBuilderTests.Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesDeterministicImageSpaceKeypoints),
                () => new YybVisualComparisonFrameRoleDiagnosticsBuilderTests().Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesDeterministicImageSpaceKeypoints());
            RunTest(results, typeof(YybScreenshotNonHairDiagnosticsTests).FullName + "." +
                nameof(YybScreenshotNonHairDiagnosticsTests.Given_HairLikeSilhouetteExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints),
                () => new YybScreenshotNonHairDiagnosticsTests().Given_HairLikeSilhouetteExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints());
            RunTest(results, typeof(YybScreenshotNonHairDiagnosticsTests).FullName + "." +
                nameof(YybScreenshotNonHairDiagnosticsTests.Given_DarkTealHairShadowExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints),
                () => new YybScreenshotNonHairDiagnosticsTests().Given_DarkTealHairShadowExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints());
            RunTest(results, typeof(VisualComparisonCandidateArtifactSelectorTests).FullName + "." +
                nameof(VisualComparisonCandidateArtifactSelectorTests.Given_RawCandidateFailsAndCorrectedCandidatePasses_When_BuildingCandidateArtifactSelection_Then_SelectsCorrectedWithoutHidingRaw),
                () => new VisualComparisonCandidateArtifactSelectorTests().Given_RawCandidateFailsAndCorrectedCandidatePasses_When_BuildingCandidateArtifactSelection_Then_SelectsCorrectedWithoutHidingRaw());
            RunTest(results, typeof(VisualComparisonCandidateArtifactSelectorTests).FullName + "." +
                nameof(VisualComparisonCandidateArtifactSelectorTests.Given_SelectedCorrectedCandidateFilesExist_When_BuildingCandidateArtifactSelection_Then_MarksFinalExportAcceptanceArtifact),
                () => new VisualComparisonCandidateArtifactSelectorTests().Given_SelectedCorrectedCandidateFilesExist_When_BuildingCandidateArtifactSelection_Then_MarksFinalExportAcceptanceArtifact());
            RunTest(results, typeof(VisualComparisonCandidateArtifactSelectorTests).FullName + "." +
                nameof(VisualComparisonCandidateArtifactSelectorTests.Given_CorrectedMetricsPassAndVmdIsRawCopy_When_BuildingCandidateArtifactSelection_Then_KeepsDiagnosticOnly),
                () => new VisualComparisonCandidateArtifactSelectorTests().Given_CorrectedMetricsPassAndVmdIsRawCopy_When_BuildingCandidateArtifactSelection_Then_KeepsDiagnosticOnly());
            RunTest(results, typeof(VisualComparisonCandidateArtifactSelectorTests).FullName + "." +
                nameof(VisualComparisonCandidateArtifactSelectorTests.Given_SelectedCorrectedCandidateManifestIsMissing_When_BuildingCandidateArtifactSelection_Then_WritesManifestAndMarksAcceptanceArtifact),
                () => new VisualComparisonCandidateArtifactSelectorTests().Given_SelectedCorrectedCandidateManifestIsMissing_When_BuildingCandidateArtifactSelection_Then_WritesManifestAndMarksAcceptanceArtifact());
            RunTest(results, typeof(VisualComparisonCandidateArtifactSelectorTests).FullName + "." +
                nameof(VisualComparisonCandidateArtifactSelectorTests.Given_RawMainAutoCandidatePasses_When_BuildingCandidateArtifactSelection_Then_MarksRawExportAcceptanceArtifact),
                () => new VisualComparisonCandidateArtifactSelectorTests().Given_RawMainAutoCandidatePasses_When_BuildingCandidateArtifactSelection_Then_MarksRawExportAcceptanceArtifact());
            RunTest(results, typeof(VisualComparisonCandidateArtifactSelectorTests).FullName + "." +
                nameof(VisualComparisonCandidateArtifactSelectorTests.Given_MainRecordingAndMainAutoSummaries_When_BuildingCandidateArtifactSelection_Then_SelectsMainAutoAcceptanceArtifact),
                () => new VisualComparisonCandidateArtifactSelectorTests().Given_MainRecordingAndMainAutoSummaries_When_BuildingCandidateArtifactSelection_Then_SelectsMainAutoAcceptanceArtifact());
            RunTest(results, typeof(VisualComparisonFrameQualityFailurePolicyTests).FullName + "." +
                nameof(VisualComparisonFrameQualityFailurePolicyTests.Given_FrameQualitySummaryFails_When_BuildingCompletionFailures_Then_PromotesToRunFailure),
                () => new VisualComparisonFrameQualityFailurePolicyTests().Given_FrameQualitySummaryFails_When_BuildingCompletionFailures_Then_PromotesToRunFailure());
            RunTest(results, typeof(YybVisualComparisonReferenceAlignmentPolicyTests).FullName + "." +
                nameof(YybVisualComparisonReferenceAlignmentPolicyTests.Given_RawVerticalResidualHasReferenceAlignedCorrectedPass_When_BuildingCompletionFailures_Then_KeepsRawDiagnosticOnly),
                () => new YybVisualComparisonReferenceAlignmentPolicyTests().Given_RawVerticalResidualHasReferenceAlignedCorrectedPass_When_BuildingCompletionFailures_Then_KeepsRawDiagnosticOnly());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.Given_CaptureModes_When_CheckingSummaryCandidateMode_Then_IncludesBothMainScenes),
                () => new MmdExportSafetyDefaultsTests().Given_CaptureModes_When_CheckingSummaryCandidateMode_Then_IncludesBothMainScenes());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.Given_MainSceneCandidateModes_When_ResolvingIntegratedVerticalSolveRole_Then_ReplayAndMainAutoUseSeparateRoles),
                () => new MmdExportSafetyDefaultsTests().Given_MainSceneCandidateModes_When_ResolvingIntegratedVerticalSolveRole_Then_ReplayAndMainAutoUseSeparateRoles());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.Given_MainSceneCandidateFailedButHasMetricsAndVmd_When_CheckingFrameQualityEligibility_Then_KeepsDiagnosticCandidate),
                () => new MmdExportSafetyDefaultsTests().Given_MainSceneCandidateFailedButHasMetricsAndVmd_When_CheckingFrameQualityEligibility_Then_KeepsDiagnosticCandidate());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.Given_MainRecordingSmokeFailedButVmdExists_When_BuildingStableCandidate_Then_CopiesVmdAndKeepsFailure),
                () => new MmdExportSafetyDefaultsTests().Given_MainRecordingSmokeFailedButVmdExists_When_BuildingStableCandidate_Then_CopiesVmdAndKeepsFailure());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.Given_HeadWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock),
                () => new MmdExportSafetyDefaultsTests().Given_HeadWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock());
            RunTest(results, typeof(MmdExportSafetyDefaultsTests).FullName + "." +
                nameof(MmdExportSafetyDefaultsTests.Given_NonZeroClipWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock),
                () => new MmdExportSafetyDefaultsTests().Given_NonZeroClipWindowProbe_When_ResolvingSampleClock_Then_KeepsAnimationClipClock());

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
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(MmdExportAcceptanceTestBatchRunner).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

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
