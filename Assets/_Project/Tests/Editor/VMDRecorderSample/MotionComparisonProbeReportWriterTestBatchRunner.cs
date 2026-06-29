using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.VMDRecorderSample
{
    public static class MotionComparisonProbeReportWriterTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-MotionComparisonProbeReportWriter.xml");
            }

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var results = new List<TestResultRecord>();
            var tests = new MotionComparisonProbeReportWriterTests();

            RunTest(results, nameof(tests.Given_ScreenshotIndexRow_When_BuildCsvLine_Then_EscapesQuotesAndCommas),
                tests.Given_ScreenshotIndexRow_When_BuildCsvLine_Then_EscapesQuotesAndCommas);
            RunTest(results, nameof(tests.Given_NewMotionComparisonProbe_When_InspectingFullBodyScreenshotPadding_Then_MatchesReferenceMp4LongShotFraming),
                tests.Given_NewMotionComparisonProbe_When_InspectingFullBodyScreenshotPadding_Then_MatchesReferenceMp4LongShotFraming);
            RunTest(results, nameof(tests.Given_NewMotionComparisonProbe_When_ResettingDefaultSampleTimes_Then_CoversReferenceMp4CurrentClipSamples),
                tests.Given_NewMotionComparisonProbe_When_ResettingDefaultSampleTimes_Then_CoversReferenceMp4CurrentClipSamples);
            RunTest(results, nameof(tests.Given_MetricsCsvValues_When_FormattersRun_Then_UseInvariantCsvAndBlankInvalidNumbers),
                tests.Given_MetricsCsvValues_When_FormattersRun_Then_UseInvariantCsvAndBlankInvalidNumbers);
            RunTest(results, nameof(tests.Given_CsvInteger_When_FormatCsvInt_Then_UsesInvariantDigits),
                tests.Given_CsvInteger_When_FormatCsvInt_Then_UsesInvariantDigits);
            RunTest(results, nameof(tests.Given_TransformInstanceIds_When_BuildTransformPairKey_Then_UsesStableDelimitedInvariantIds),
                tests.Given_TransformInstanceIds_When_BuildTransformPairKey_Then_UsesStableDelimitedInvariantIds);
            RunTest(results, nameof(tests.Given_Transforms_When_BuildTransformPairKey_Then_UsesInstanceIdsAndNullFallback),
                tests.Given_Transforms_When_BuildTransformPairKey_Then_UsesInstanceIdsAndNullFallback);
            RunTest(results, nameof(tests.Given_ThumbHelperPairKeySide_When_BuildLabels_Then_UsesStableDiagnosticLabels),
                tests.Given_ThumbHelperPairKeySide_When_BuildLabels_Then_UsesStableDiagnosticLabels);
            RunTest(results, nameof(tests.Given_ThumbDiagnosticTransformSide_When_BuildCacheKeys_Then_UsesStableLookupLabels),
                tests.Given_ThumbDiagnosticTransformSide_When_BuildCacheKeys_Then_UsesStableLookupLabels);
            RunTest(results, nameof(tests.Given_DiagnosticTransformSide_When_BuildSideToken_Then_UsesStableNameLookupToken),
                tests.Given_DiagnosticTransformSide_When_BuildSideToken_Then_UsesStableNameLookupToken);
            RunTest(results, nameof(tests.Given_DiagnosticTransformNameAndSide_When_MatchesSideToken_Then_UsesStableLeftRightContainment),
                tests.Given_DiagnosticTransformNameAndSide_When_MatchesSideToken_Then_UsesStableLeftRightContainment);
            RunTest(results, nameof(tests.Given_DiagnosticTransformName_When_NormalizeName_Then_UsesInvariantLowercaseAndEmptyFallback),
                tests.Given_DiagnosticTransformName_When_NormalizeName_Then_UsesInvariantLowercaseAndEmptyFallback);
            RunTest(results, nameof(tests.Given_ModelName_When_MatchesYybModelName_Then_UsesNormalizedYybToken),
                tests.Given_ModelName_When_MatchesYybModelName_Then_UsesNormalizedYybToken);
            RunTest(results, nameof(tests.Given_ThumbDiagnosticTransformName_When_MatchNamePredicates_Then_UsesStableLookupRules),
                tests.Given_ThumbDiagnosticTransformName_When_MatchNamePredicates_Then_UsesStableLookupRules);
            RunTest(results, nameof(tests.Given_ThumbDiagnosticTransformNameAndSide_When_MatchTransformPredicates_Then_RequiresSideAndNameRules),
                tests.Given_ThumbDiagnosticTransformNameAndSide_When_MatchTransformPredicates_Then_RequiresSideAndNameRules);
            RunTest(results, nameof(tests.Given_SleeveAnchorPairKeySide_When_BuildLabel_Then_UsesStableDiagnosticLabel),
                tests.Given_SleeveAnchorPairKeySide_When_BuildLabel_Then_UsesStableDiagnosticLabel);
            RunTest(results, nameof(tests.Given_SleeveAnchorLookupSide_When_BuildLabels_Then_UsesStableTransformLookupLabels),
                tests.Given_SleeveAnchorLookupSide_When_BuildLabels_Then_UsesStableTransformLookupLabels);
            RunTest(results, nameof(tests.Given_TransformNameSuffix_When_MatchesSuffix_Then_AllowsExactAndDottedUnityNames),
                tests.Given_TransformNameSuffix_When_MatchesSuffix_Then_AllowsExactAndDottedUnityNames);
            RunTest(results, nameof(tests.Given_SleeveAnchorTransformNameAndSide_When_MatchesSleeveAnchorTransformName_Then_UsesStableSideSuffixRules),
                tests.Given_SleeveAnchorTransformNameAndSide_When_MatchesSleeveAnchorTransformName_Then_UsesStableSideSuffixRules);
            RunTest(results, nameof(tests.Given_SamplingStartupWarnings_When_BuildMessages_Then_UsesStableDiagnosticText),
                tests.Given_SamplingStartupWarnings_When_BuildMessages_Then_UsesStableDiagnosticText);
            RunTest(results, nameof(tests.Given_HumanoidArmMuscleWarning_When_BuildMessage_Then_UsesStableDiagnosticText),
                tests.Given_HumanoidArmMuscleWarning_When_BuildMessage_Then_UsesStableDiagnosticText);
            RunTest(results, nameof(tests.Given_MetricsCsvInteger_When_FormatMetricsCsvInt_Then_UsesInvariantDigits),
                tests.Given_MetricsCsvInteger_When_FormatMetricsCsvInt_Then_UsesInvariantDigits);
            RunTest(results, nameof(tests.Given_FormattedMetricsCsvValues_When_BuildMetricsCsvLine_Then_JoinsColumnsInOrder),
                tests.Given_FormattedMetricsCsvValues_When_BuildMetricsCsvLine_Then_JoinsColumnsInOrder);
            RunTest(results, nameof(tests.Given_LabelInputs_When_BuildComparisonLabel_Then_PrefersOverrideAndSanitizesFallbacks),
                tests.Given_LabelInputs_When_BuildComparisonLabel_Then_PrefersOverrideAndSanitizesFallbacks);
            RunTest(results, nameof(tests.Given_CaptureCameraLabel_When_BuildObjectName_Then_UsesStablePrefixAndFallback),
                tests.Given_CaptureCameraLabel_When_BuildObjectName_Then_UsesStablePrefixAndFallback);
            RunTest(results, nameof(tests.Given_AnimationTimeSources_When_BuildLabels_Then_UsesStableMetricsValues),
                tests.Given_AnimationTimeSources_When_BuildLabels_Then_UsesStableMetricsValues);
            RunTest(results, nameof(tests.Given_ScreenshotIndexRow_When_AppendRow_Then_WritesCsvRow),
                tests.Given_ScreenshotIndexRow_When_AppendRow_Then_WritesCsvRow);
            RunTest(results, nameof(tests.Given_ScreenshotIndexRowPathWithMissingParent_When_AppendRow_Then_CreatesParentAndWritesRow),
                tests.Given_ScreenshotIndexRowPathWithMissingParent_When_AppendRow_Then_CreatesParentAndWritesRow);
            RunTest(results, nameof(tests.Given_ReportWriterBoundary_When_CheckedForOutputPathHelpers_Then_DoesNotExposePathNamingMethods),
                tests.Given_ReportWriterBoundary_When_CheckedForOutputPathHelpers_Then_DoesNotExposePathNamingMethods);
            RunTest(results, nameof(tests.Given_FrameSessionIndexData_When_BuildMarkdown_Then_EscapesBackticksAndPipes),
                tests.Given_FrameSessionIndexData_When_BuildMarkdown_Then_EscapesBackticksAndPipes);
            RunTest(results, nameof(tests.Given_ScreenshotIndexCsvHeader_When_BuildHeader_Then_KeepsColumnOrderAndCount),
                tests.Given_ScreenshotIndexCsvHeader_When_BuildHeader_Then_KeepsColumnOrderAndCount);
            RunTest(results, nameof(tests.Given_ScreenshotIndexCsvPath_When_WriteHeader_Then_WritesHeaderLine),
                tests.Given_ScreenshotIndexCsvPath_When_WriteHeader_Then_WritesHeaderLine);
            RunTest(results, nameof(tests.Given_ScreenshotSessionFilePaths_When_WriteSessionFiles_Then_WritesCsvHeaderAndMarkdownIndex),
                tests.Given_ScreenshotSessionFilePaths_When_WriteSessionFiles_Then_WritesCsvHeaderAndMarkdownIndex);
            RunTest(results, nameof(tests.Given_ScreenshotSessionFilePathsWithMissingParent_When_WriteSessionFiles_Then_CreatesParentAndWritesFiles),
                tests.Given_ScreenshotSessionFilePathsWithMissingParent_When_WriteSessionFiles_Then_CreatesParentAndWritesFiles);
            RunTest(results, nameof(tests.Given_SampleTimes_When_FormatSampleTimes_Then_UsesInvariantCompactValues),
                tests.Given_SampleTimes_When_FormatSampleTimes_Then_UsesInvariantCompactValues);
            RunTest(results, nameof(tests.Given_SampleTime_When_BuildSampleTimeReason_Then_UsesCompactReasonLabel),
                tests.Given_SampleTime_When_BuildSampleTimeReason_Then_UsesCompactReasonLabel);
            RunTest(results, nameof(tests.Given_SamplingLifecycle_When_BuildReasonLabels_Then_UsesStableManifestAndSampleReasons),
                tests.Given_SamplingLifecycle_When_BuildReasonLabels_Then_UsesStableManifestAndSampleReasons);
            RunTest(results, nameof(tests.Given_RealtimeRiskEvaluation_When_BuildReasonLabel_Then_UsesStableValue),
                tests.Given_RealtimeRiskEvaluation_When_BuildReasonLabel_Then_UsesStableValue);
            RunTest(results, nameof(tests.Given_SampleLogValues_When_BuildSampleLogMessage_Then_UsesInvariantFixedPrecision),
                tests.Given_SampleLogValues_When_BuildSampleLogMessage_Then_UsesInvariantFixedPrecision);
            RunTest(results, nameof(tests.Given_ScreenshotWarningValues_When_BuildWarningMessages_Then_UsesStableDiagnosticText),
                tests.Given_ScreenshotWarningValues_When_BuildWarningMessages_Then_UsesStableDiagnosticText);
            RunTest(results, nameof(tests.Given_NoSampleTimes_When_FormatSampleTimes_Then_ReturnsEmptyString),
                tests.Given_NoSampleTimes_When_FormatSampleTimes_Then_ReturnsEmptyString);
            RunTest(results, nameof(tests.Given_SampleClockMode_When_BuildSampleClockLabel_Then_UsesManifestLabels),
                tests.Given_SampleClockMode_When_BuildSampleClockLabel_Then_UsesManifestLabels);
            RunTest(results, nameof(tests.Given_SessionTimestamp_When_BuildSessionTimeLabels_Then_UsesInvariantManifestFormats),
                tests.Given_SessionTimestamp_When_BuildSessionTimeLabels_Then_UsesInvariantManifestFormats);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_KeepsColumnOrderAndCount),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_KeepsColumnOrderAndCount);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesPostSetRightEndpointDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesPostSetRightEndpointDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesSetHumanPoseBodyInputDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesSetHumanPoseBodyInputDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesSetHumanPosePreSolveBasisDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesSetHumanPosePreSolveBasisDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesSetHumanPoseExtendedInputDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesSetHumanPoseExtendedInputDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesRetargetPoseStageDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesRetargetPoseStageDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesLeftArmTwistRetargetPoseStageDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesLeftArmTwistRetargetPoseStageDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesLeftForearmRetargetPoseStageDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesLeftForearmRetargetPoseStageDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesRightForearmRetargetPoseStageDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesRightForearmRetargetPoseStageDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesSetHumanPoseBoundaryDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesSetHumanPoseBoundaryDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesLeftForearmSetHumanPoseBoundaryDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesLeftForearmSetHumanPoseBoundaryDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesArmSwingGuardBoundaryDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesArmSwingGuardBoundaryDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesLowerBodyPostPoseDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesLowerBodyPostPoseDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesRetargetEndpointStageDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesRetargetEndpointStageDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesSleeveThicknessDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesSleeveThicknessDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesHipsYContributionDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesHipsYContributionDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesRecordingStartHipsBaselineDiagnostics),
                tests.Given_MetricsCsvHeader_When_BuildHeader_Then_IncludesRecordingStartHipsBaselineDiagnostics);
            RunTest(results, nameof(tests.Given_MetricsCsvPath_When_AppendLine_Then_WritesLineWithNewline),
                tests.Given_MetricsCsvPath_When_AppendLine_Then_WritesLineWithNewline);
            RunTest(results, nameof(tests.Given_MetricsCsvPathWithMissingParent_When_WriteHeaderAndAppendLine_Then_CreatesParentAndWritesLines),
                tests.Given_MetricsCsvPathWithMissingParent_When_WriteHeaderAndAppendLine_Then_CreatesParentAndWritesLines);
            RunTest(results, nameof(tests.Given_ScreenshotPngBytes_When_WriteBytes_Then_WritesFileAndReturnsTrue),
                tests.Given_ScreenshotPngBytes_When_WriteBytes_Then_WritesFileAndReturnsTrue);
            RunTest(results, nameof(tests.Given_ScreenshotPngPathWithMissingParent_When_WriteBytes_Then_CreatesParentAndWritesFile),
                tests.Given_ScreenshotPngPathWithMissingParent_When_WriteBytes_Then_CreatesParentAndWritesFile);
            RunTest(results, nameof(tests.Given_TextureSamples_When_IsScreenshotTextureBlank_Then_DetectsUniformAndVariedContent),
                tests.Given_TextureSamples_When_IsScreenshotTextureBlank_Then_DetectsUniformAndVariedContent);
            RunTest(results, nameof(tests.Given_TextureAndPath_When_WriteNonBlankScreenshotPng_Then_WritesOnlyNonBlankImage),
                tests.Given_TextureAndPath_When_WriteNonBlankScreenshotPng_Then_WritesOnlyNonBlankImage);
            RunTest(results, nameof(tests.Given_SessionManifestData_When_BuildMarkdown_Then_ContainsOutputsTable),
                tests.Given_SessionManifestData_When_BuildMarkdown_Then_ContainsOutputsTable);
            RunTest(results, nameof(tests.Given_SessionManifestPathWithMissingParent_When_WriteMarkdown_Then_CreatesParentAndWritesFile),
                tests.Given_SessionManifestPathWithMissingParent_When_WriteMarkdown_Then_CreatesParentAndWritesFile);
            RunTest(results, nameof(tests.Given_SessionManifestArtifactsHeadingWithoutTable_When_AppendExportedVmd_Then_RecreatesArtifactsTableUnderHeading),
                tests.Given_SessionManifestArtifactsHeadingWithoutTable_When_AppendExportedVmd_Then_RecreatesArtifactsTableUnderHeading);
            RunTest(results, nameof(tests.Given_ExportedVmdArtifactInputs_When_BuildRow_Then_EscapesPathAndIncludesOptionalStats),
                tests.Given_ExportedVmdArtifactInputs_When_BuildRow_Then_EscapesPathAndIncludesOptionalStats);
            RunTest(results, nameof(tests.Given_TwoMetricsCsvs_When_BuildFrameQualitySummary_Then_ComparesSameRecorderFramesAndReportsSpikes),
                tests.Given_TwoMetricsCsvs_When_BuildFrameQualitySummary_Then_ComparesSameRecorderFramesAndReportsSpikes);
            RunTest(results, nameof(tests.Given_SparseMetricsCsvs_When_BuildFrameQualitySummary_Then_DoesNotTreatSampleGapAsOneFrameTeleport),
                tests.Given_SparseMetricsCsvs_When_BuildFrameQualitySummary_Then_DoesNotTreatSampleGapAsOneFrameTeleport);
            RunTest(results, nameof(tests.Given_MetricSamplesDriftByOneRecorderFrame_When_BuildFrameQualitySummary_Then_ComparesNearestFrame),
                tests.Given_MetricSamplesDriftByOneRecorderFrame_When_BuildFrameQualitySummary_Then_ComparesNearestFrame);
            RunTest(results, nameof(tests.Given_SparseMetricsCsvsWithSameFrameRootDelta_When_BuildFrameQualitySummary_Then_FailsGate),
                tests.Given_SparseMetricsCsvsWithSameFrameRootDelta_When_BuildFrameQualitySummary_Then_FailsGate);
            RunTest(results, nameof(tests.Given_SameFrameFootXzArcDelta_When_BuildFrameQualitySummary_Then_ReportsHorizontalFootGate),
                tests.Given_SameFrameFootXzArcDelta_When_BuildFrameQualitySummary_Then_ReportsHorizontalFootGate);
            RunTest(results, nameof(tests.Given_HipsHorizontalMotionExplainsPartOfFootXzDelta_When_BuildFrameQualitySummary_Then_ReportsHipsAlignedFootResidual),
                tests.Given_HipsHorizontalMotionExplainsPartOfFootXzDelta_When_BuildFrameQualitySummary_Then_ReportsHipsAlignedFootResidual);
            RunTest(results, nameof(tests.Given_FinalFootXzSampleFallsOutsideVmdExportRange_When_BuildFrameQualitySummary_Then_UsesInsideRangeForFootXzGateAndReportsOutsideSample),
                tests.Given_FinalFootXzSampleFallsOutsideVmdExportRange_When_BuildFrameQualitySummary_Then_UsesInsideRangeForFootXzGateAndReportsOutsideSample);
            RunTest(results, nameof(tests.Given_FootXzWarningWithinVmdRange_When_BuildingEvaluationEntries_Then_CorrectedCandidateReducesFootCarrierXzBelowWarning),
                tests.Given_FootXzWarningWithinVmdRange_When_BuildingEvaluationEntries_Then_CorrectedCandidateReducesFootCarrierXzBelowWarning);
            RunTest(results, nameof(tests.Given_FootXzCorrectionFrameHasDisabledIk_When_NearbyIkFrameCanCarryDelta_Then_CorrectedVmdUsesVisibleCarrierFrame),
                tests.Given_FootXzCorrectionFrameHasDisabledIk_When_NearbyIkFrameCanCarryDelta_Then_CorrectedVmdUsesVisibleCarrierFrame);
            RunTest(results, nameof(tests.Given_YybCandidateRiskColumnWithoutFiniteValues_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate),
                tests.Given_YybCandidateRiskColumnWithoutFiniteValues_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate);
            RunTest(results, nameof(tests.Given_YybCandidateWithoutRiskColumn_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate),
                tests.Given_YybCandidateWithoutRiskColumn_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate);
            RunTest(results, nameof(tests.Given_YybCandidateSleeveThicknessRiskWithoutFiniteValues_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate),
                tests.Given_YybCandidateSleeveThicknessRiskWithoutFiniteValues_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate);
            RunTest(results, nameof(tests.Given_YybCandidateSleeveThicknessRiskExceedsThreshold_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate),
                tests.Given_YybCandidateSleeveThicknessRiskExceedsThreshold_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate);
            RunTest(results, nameof(tests.Given_YybCandidateRiskExceedsThreshold_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate),
                tests.Given_YybCandidateRiskExceedsThreshold_When_BuildFrameQualitySummary_Then_FailsDiagnosticGate);
            RunTest(results, nameof(tests.Given_MainRecordingRootPathDelta_When_BuildFrameQualitySummary_Then_FailsStationaryPreviewGate),
                tests.Given_MainRecordingRootPathDelta_When_BuildFrameQualitySummary_Then_FailsStationaryPreviewGate);
            RunTest(results, nameof(tests.Given_MainRecordingArmMotionWithSmallRootDrift_When_BuildFrameQualitySummary_Then_FailsLimbIsolationGate),
                tests.Given_MainRecordingArmMotionWithSmallRootDrift_When_BuildFrameQualitySummary_Then_FailsLimbIsolationGate);
            RunTest(results, nameof(tests.Given_MainRecordingSameFrameLimbPoseGap_When_BuildFrameQualitySummary_Then_FailsNaturalnessGate),
                tests.Given_MainRecordingSameFrameLimbPoseGap_When_BuildFrameQualitySummary_Then_FailsNaturalnessGate);
            RunTest(results, nameof(tests.Given_StartSampleHasPreRetargetArmPoseGap_When_BuildFrameQualitySummary_Then_BucketsStartOutsideNaturalnessGate),
                tests.Given_StartSampleHasPreRetargetArmPoseGap_When_BuildFrameQualitySummary_Then_BucketsStartOutsideNaturalnessGate);
            RunTest(results, nameof(tests.Given_BaselineArmTwistOutsideSafetyRange_When_BuildFrameQualitySummary_Then_GuardNormalizedNaturalnessGatePasses),
                tests.Given_BaselineArmTwistOutsideSafetyRange_When_BuildFrameQualitySummary_Then_GuardNormalizedNaturalnessGatePasses);
            RunTest(results, nameof(tests.Given_ForearmTwistFullRangeFlipWithStableVisualPose_When_BuildFrameQualitySummary_Then_GuardNormalizedNaturalnessGatePasses),
                tests.Given_ForearmTwistFullRangeFlipWithStableVisualPose_When_BuildFrameQualitySummary_Then_GuardNormalizedNaturalnessGatePasses);
            RunTest(results, nameof(tests.Given_RawForearmTwistSaturatesButGatePasses_When_BuildFrameQualitySummary_Then_ReportsRawLimbPoseSaturation),
                tests.Given_RawForearmTwistSaturatesButGatePasses_When_BuildFrameQualitySummary_Then_ReportsRawLimbPoseSaturation);
            RunTest(results, nameof(tests.Given_SafeArmTwistSignFlip_When_BuildFrameQualitySummary_Then_FailsGuardNormalizedNaturalnessGate),
                tests.Given_SafeArmTwistSignFlip_When_BuildFrameQualitySummary_Then_FailsGuardNormalizedNaturalnessGate);
            RunTest(results, nameof(tests.Given_MainRecordingRootPathDeltaWithRetargetRootSpike_When_BuildFrameQualitySummary_Then_FailsRootSpikeGate),
                tests.Given_MainRecordingRootPathDeltaWithRetargetRootSpike_When_BuildFrameQualitySummary_Then_FailsRootSpikeGate);
            RunTest(results, nameof(tests.Given_MainRecordingRootPathDeltaWithFloorFailure_When_BuildFrameQualitySummary_Then_FailsFloorAndStationaryRootGates),
                tests.Given_MainRecordingRootPathDeltaWithFloorFailure_When_BuildFrameQualitySummary_Then_FailsFloorAndStationaryRootGates);
            RunTest(results, nameof(tests.Given_ConstantSceneRootOffset_When_BuildFrameQualitySummary_Then_DoesNotFailRootDeltaGate),
                tests.Given_ConstantSceneRootOffset_When_BuildFrameQualitySummary_Then_DoesNotFailRootDeltaGate);
            RunTest(results, nameof(tests.Given_ConstantVerticalModelOffset_When_BuildFrameQualitySummary_Then_DoesNotWarnHipsOrFootGate),
                tests.Given_ConstantVerticalModelOffset_When_BuildFrameQualitySummary_Then_DoesNotWarnHipsOrFootGate);
            RunTest(results, nameof(tests.Given_RelativeVerticalDriftExceedsTolerance_When_BuildFrameQualitySummary_Then_WarnsGate),
                tests.Given_RelativeVerticalDriftExceedsTolerance_When_BuildFrameQualitySummary_Then_WarnsGate);
            RunTest(results, nameof(tests.Given_SameFrameHipsAndFootDeltasExceedWarnThreshold_When_BuildFrameQualitySummary_Then_WarnsGate),
                tests.Given_SameFrameHipsAndFootDeltasExceedWarnThreshold_When_BuildFrameQualitySummary_Then_WarnsGate);
            RunTest(results, nameof(tests.Given_HipsContributionColumns_When_BuildFrameQualitySummary_Then_ReportsOffsetNormalizedHipsYContributors),
                tests.Given_HipsContributionColumns_When_BuildFrameQualitySummary_Then_ReportsOffsetNormalizedHipsYContributors);
            RunTest(results, nameof(tests.Given_RecordingStartHipsBaselineColumns_When_BuildFrameQualitySummary_Then_ReportsCandidateStartAndFlip),
                tests.Given_RecordingStartHipsBaselineColumns_When_BuildFrameQualitySummary_Then_ReportsCandidateStartAndFlip);
            RunTest(results, nameof(tests.Given_FrameSpecificVerticalSolveCandidate_When_BuildFrameQualitySummary_Then_ReportsBoundedPrototypeWithoutChangingActualGate),
                tests.Given_FrameSpecificVerticalSolveCandidate_When_BuildFrameQualitySummary_Then_ReportsBoundedPrototypeWithoutChangingActualGate);
            RunTest(results, nameof(tests.Given_FrameSpecificVerticalSolveCandidate_When_BuildFrameQualitySummary_Then_WritesPostprocessedMetricsWithoutHidingActualGate),
                tests.Given_FrameSpecificVerticalSolveCandidate_When_BuildFrameQualitySummary_Then_WritesPostprocessedMetricsWithoutHidingActualGate);
            RunTest(results, nameof(tests.Given_FinishSamplesUseDifferentFrameCounts_When_BuildFrameQualitySummary_Then_PostprocessHasFullEvidence),
                tests.Given_FinishSamplesUseDifferentFrameCounts_When_BuildFrameQualitySummary_Then_PostprocessHasFullEvidence);
            RunTest(results, nameof(tests.Given_PostprocessMetricsArtifact_When_BuildingFrameQualitySummary_Then_ReturnsSeparatePassingEvaluationEntry),
                tests.Given_PostprocessMetricsArtifact_When_BuildingFrameQualitySummary_Then_ReturnsSeparatePassingEvaluationEntry);
            RunTest(results, nameof(tests.Given_PostprocessMetricsArtifact_When_BuildingFrameQualityEvaluationEntries_Then_PrimaryEntryKeepsRawCandidateAndPostprocessRemainsSecondary),
                tests.Given_PostprocessMetricsArtifact_When_BuildingFrameQualityEvaluationEntries_Then_PrimaryEntryKeepsRawCandidateAndPostprocessRemainsSecondary);
            RunTest(results, nameof(tests.Given_CorrectedArtifactFootDeltaNeedsMoreThanPrototypeCap_When_BuildingEvaluationEntries_Then_PostprocessUsesArtifactCap),
                tests.Given_CorrectedArtifactFootDeltaNeedsMoreThanPrototypeCap_When_BuildingEvaluationEntries_Then_PostprocessUsesArtifactCap);
            RunTest(results, nameof(tests.Given_CorrectedMetricsArtifact_When_BuildingFrameQualityEvaluationEntries_Then_EvaluatesCorrectedArtifactSeparatelyFromRawCandidate),
                tests.Given_CorrectedMetricsArtifact_When_BuildingFrameQualityEvaluationEntries_Then_EvaluatesCorrectedArtifactSeparatelyFromRawCandidate);
            RunTest(results, nameof(tests.Given_CorrectedMetricsArtifact_When_BuildingFrameQualityEvaluationEntries_Then_CorrectedEntryUsesExplicitVmdArtifactAndManifest),
                tests.Given_CorrectedMetricsArtifact_When_BuildingFrameQualityEvaluationEntries_Then_CorrectedEntryUsesExplicitVmdArtifactAndManifest);
            RunTest(results, nameof(tests.Given_CorrectedCandidatePasses_When_PromotingToPrimaryExport_Then_RewritesMainAutoPathsAndPreservesRawDiagnostics),
                tests.Given_CorrectedCandidatePasses_When_PromotingToPrimaryExport_Then_RewritesMainAutoPathsAndPreservesRawDiagnostics);
            RunTest(results, nameof(tests.Given_PrimaryExportPromotionRunsTwice_When_RawDiagnosticsExist_Then_DoesNotOverwriteRawDiagnostics),
                tests.Given_PrimaryExportPromotionRunsTwice_When_RawDiagnosticsExist_Then_DoesNotOverwriteRawDiagnostics);
            RunTest(results, nameof(tests.Given_IntegratedPrimarySummary_When_BuildingEvaluationEntries_Then_KeepsPrimaryAsOnlyAcceptanceEntry),
                tests.Given_IntegratedPrimarySummary_When_BuildingEvaluationEntries_Then_KeepsPrimaryAsOnlyAcceptanceEntry);
            RunTest(results, nameof(tests.Given_VmdReplayIntegratedPrimarySummary_When_BuildingEvaluationEntries_Then_KeepsReplayPrimaryAsOnlyDiagnosticEntry),
                tests.Given_VmdReplayIntegratedPrimarySummary_When_BuildingEvaluationEntries_Then_KeepsReplayPrimaryAsOnlyDiagnosticEntry);
            RunTest(results, nameof(tests.Given_VerticalSolveWouldCreateUnsafeVmdCarrierStep_When_BuildingEvaluationEntries_Then_CorrectedVmdStaysWithinSafetyGates),
                tests.Given_VerticalSolveWouldCreateUnsafeVmdCarrierStep_When_BuildingEvaluationEntries_Then_CorrectedVmdStaysWithinSafetyGates);
            RunTest(results, nameof(tests.Given_FootVerticalSolveWouldSinkEffectiveVmdIk_When_BuildingEvaluationEntries_Then_CorrectedVmdPreservesFloorMargin),
                tests.Given_FootVerticalSolveWouldSinkEffectiveVmdIk_When_BuildingEvaluationEntries_Then_CorrectedVmdPreservesFloorMargin);
            RunTest(results, nameof(tests.Given_CenterVerticalSolveWouldSinkToeIkEffectiveY_When_BuildingEvaluationEntries_Then_CorrectedVmdPreservesToeFloorMargin),
                tests.Given_CenterVerticalSolveWouldSinkToeIkEffectiveY_When_BuildingEvaluationEntries_Then_CorrectedVmdPreservesToeFloorMargin);
            RunTest(results, nameof(tests.Given_CenterLiftKeepsEffectiveFootIkAboveFloor_When_BuildFrameQualitySummary_Then_DoesNotFailBelowFloorGate),
                tests.Given_CenterLiftKeepsEffectiveFootIkAboveFloor_When_BuildFrameQualitySummary_Then_DoesNotFailBelowFloorGate);
            RunTest(results, nameof(tests.Given_VmdFrames_When_BuildFrameQualitySummary_Then_ReportsCenterAndFootIkExportDelta),
                tests.Given_VmdFrames_When_BuildFrameQualitySummary_Then_ReportsCenterAndFootIkExportDelta);
            RunTest(results, nameof(tests.Given_FootIkStepWhileIkIsDisabled_When_BuildFrameQualitySummary_Then_DoesNotCountVisualFootIkSpike),
                tests.Given_FootIkStepWhileIkIsDisabled_When_BuildFrameQualitySummary_Then_DoesNotCountVisualFootIkSpike);
            RunTest(results, nameof(tests.Given_MmdAutomationEvidence_When_AttachLatestMmdAutomationEvidence_Then_UpdatesStatusAndRelativePaths),
                tests.Given_MmdAutomationEvidence_When_AttachLatestMmdAutomationEvidence_Then_UpdatesStatusAndRelativePaths);
            RunTest(results, nameof(tests.Given_MmdAutomationEvidenceWithProjectRelativeArtifactPaths_When_AttachLatestMmdAutomationEvidence_Then_ResolvesScreenshotPath),
                tests.Given_MmdAutomationEvidenceWithProjectRelativeArtifactPaths_When_AttachLatestMmdAutomationEvidence_Then_ResolvesScreenshotPath);
            RunTest(results, nameof(tests.Given_MmdAutomationEvidenceWithReportRelativeRunDir_When_AttachLatestMmdAutomationEvidence_Then_ResolvesRunDirPath),
                tests.Given_MmdAutomationEvidenceWithReportRelativeRunDir_When_AttachLatestMmdAutomationEvidence_Then_ResolvesRunDirPath);
            RunTest(results, nameof(tests.Given_ReportRelativeScreenshotAlsoExistsAtProjectRoot_When_AttachLatestMmdAutomationEvidence_Then_PrefersReportDirectory),
                tests.Given_ReportRelativeScreenshotAlsoExistsAtProjectRoot_When_AttachLatestMmdAutomationEvidence_Then_PrefersReportDirectory);
            RunTest(results, nameof(tests.Given_StaleMmdAutomationEvidence_When_AttachLatestMmdAutomationEvidence_Then_LeavesSummaryAsNotRun),
                tests.Given_StaleMmdAutomationEvidence_When_AttachLatestMmdAutomationEvidence_Then_LeavesSummaryAsNotRun);

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

            Console.WriteLine($"MotionComparisonProbeReportWriter tests completed; results written to {resultPath}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static void RunTest(List<TestResultRecord> results, string methodName, TestDelegate action)
        {
            string name = typeof(MotionComparisonProbeReportWriterTests).FullName + "." + methodName;
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
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(MotionComparisonProbeReportWriterTests).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

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
