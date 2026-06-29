using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.VMDRecorderSample
{
    public static class VmdFileWriterTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-VmdFileWriter.xml");
            }

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var results = new List<TestResultRecord>();
            var tests = new VmdFileWriterTests();

            RunTest(results, nameof(tests.Given_MinimalFrames_When_WritingVmd_Then_HeaderAndKeyframeCountMatch),
                tests.Given_MinimalFrames_When_WritingVmd_Then_HeaderAndKeyframeCountMatch);
            RunTest(results, nameof(tests.Given_KeyReductionDoesNotDivideFrameCount_When_WritingVmd_Then_AllFramesAreWritten),
                tests.Given_KeyReductionDoesNotDivideFrameCount_When_WritingVmd_Then_AllFramesAreWritten);
            RunTest(results, nameof(tests.Given_CenterAsParentWithoutGrooveRouting_When_WritingVmd_Then_HumanoidCenterStaysOnCenterBone),
                tests.Given_CenterAsParentWithoutGrooveRouting_When_WritingVmd_Then_HumanoidCenterStaysOnCenterBone);
            RunTest(results, nameof(tests.Given_ReducedExport_When_WritingMotionCarrierBones_Then_RootCenterAndIkKeepEveryFrame),
                tests.Given_ReducedExport_When_WritingMotionCarrierBones_Then_RootCenterAndIkKeepEveryFrame);
            RunTest(results, nameof(tests.Given_BoneFrames_When_WritingVmd_Then_InterpolationIsLinearNotZeroed),
                tests.Given_BoneFrames_When_WritingVmd_Then_InterpolationIsLinearNotZeroed);
            RunTest(results, nameof(tests.Given_BottomCenter_When_HipsTilts_Then_CenterKeepsHipsHorizontalPosition),
                tests.Given_BottomCenter_When_HipsTilts_Then_CenterKeepsHipsHorizontalPosition);
            RunTest(results, nameof(tests.Given_MmdFootIkExportFloorGuard_When_PositionIsBelowFloor_Then_YIsClamped),
                tests.Given_MmdFootIkExportFloorGuard_When_PositionIsBelowFloor_Then_YIsClamped);
            RunTest(results, nameof(tests.Given_MmdFootIkExportFloorGuardDisabled_When_PositionIsBelowFloor_Then_YIsPreserved),
                tests.Given_MmdFootIkExportFloorGuardDisabled_When_PositionIsBelowFloor_Then_YIsPreserved);
            RunTest(results, nameof(tests.Given_DefaultRecorderSettings_When_MmdFloorGuardExists_Then_AllExportOffsetsStayNeutral),
                tests.Given_DefaultRecorderSettings_When_MmdFloorGuardExists_Then_AllExportOffsetsStayNeutral);
            RunTest(results, nameof(tests.Given_FootIkEffectiveYBelowFloor_When_ApplyingCenterFloorLift_Then_OnlyCenterYIsRaised),
                tests.Given_FootIkEffectiveYBelowFloor_When_ApplyingCenterFloorLift_Then_OnlyCenterYIsRaised);
            RunTest(results, nameof(tests.Given_FootIkFloorLiftSpike_When_ApplyingCenterFloorLift_Then_CenterYDeltaIsSmoothedWithoutMovingFeet),
                tests.Given_FootIkFloorLiftSpike_When_ApplyingCenterFloorLift_Then_CenterYDeltaIsSmoothedWithoutMovingFeet);
            RunTest(results, nameof(tests.Given_FootIkFloorLiftAndCenterXZMotion_When_ApplyingCenterFloorLift_Then_TotalCenterDeltaStaysWithinLimit),
                tests.Given_FootIkFloorLiftAndCenterXZMotion_When_ApplyingCenterFloorLift_Then_TotalCenterDeltaStaysWithinLimit);
            RunTest(results, nameof(tests.Given_FootIkExportOffset_When_ApplyingFootGuard_Then_OffsetIsAppliedBeforeClamp),
                tests.Given_FootIkExportOffset_When_ApplyingFootGuard_Then_OffsetIsAppliedBeforeClamp);
            RunTest(results, nameof(tests.Given_ToeIkLocalPosition_When_FootIkHasExportOffset_Then_ToeDoesNotReceiveExtraLift),
                tests.Given_ToeIkLocalPosition_When_FootIkHasExportOffset_Then_ToeDoesNotReceiveExtraLift);
            RunTest(results, nameof(tests.Given_ParentRootBelowFloor_When_FootIkIsLocallyOnFloor_Then_EffectiveYIsClamped),
                tests.Given_ParentRootBelowFloor_When_FootIkIsLocallyOnFloor_Then_EffectiveYIsClamped);
            RunTest(results, nameof(tests.Given_ToeIkLocalPosition_When_ParentFootIkMakesEffectiveYBelowFloor_Then_ToeEffectiveYIsClamped),
                tests.Given_ToeIkLocalPosition_When_ParentFootIkMakesEffectiveYBelowFloor_Then_ToeEffectiveYIsClamped);
            RunTest(results, nameof(tests.Given_MmdIkExportDeltaSpike_When_ClampingExportPositions_Then_LimitsEveryFrameStep),
                tests.Given_MmdIkExportDeltaSpike_When_ClampingExportPositions_Then_LimitsEveryFrameStep);
            RunTest(results, nameof(tests.Given_MmdIkExportRecoveryTrigger_When_RawStepIsLarge_Then_UsesRecoveryLimitWithoutExceedingIt),
                tests.Given_MmdIkExportRecoveryTrigger_When_RawStepIsLarge_Then_UsesRecoveryLimitWithoutExceedingIt);
            RunTest(results, nameof(tests.Given_MmdIkExportRecoveryDebt_When_LagDebtIsLarge_Then_UsesRecoveryLimitWithoutExceedingIt),
                tests.Given_MmdIkExportRecoveryDebt_When_LagDebtIsLarge_Then_UsesRecoveryLimitWithoutExceedingIt);
            RunTest(results, nameof(tests.Given_MmdIkExportRecoveryHold_When_RawStepTriggers_Then_KeepsRecoveryLimitForTriggerInclusiveHoldWindow),
                tests.Given_MmdIkExportRecoveryHold_When_RawStepTriggers_Then_KeepsRecoveryLimitForTriggerInclusiveHoldWindow);
            RunTest(results, nameof(tests.Given_LargeFootIkExportStep_When_BuildingDynamicIkFrames_Then_DisablesThatSideUntilStable),
                tests.Given_LargeFootIkExportStep_When_BuildingDynamicIkFrames_Then_DisablesThatSideUntilStable);
            RunTest(results, nameof(tests.Given_DynamicIkFrames_When_WritingVmd_Then_IKFooterPreservesPerFrameStates),
                tests.Given_DynamicIkFrames_When_WritingVmd_Then_IKFooterPreservesPerFrameStates);
            RunTest(results, nameof(tests.Given_MmdCenterExportDeltaSpike_When_ClampingExportPositions_Then_LimitsEveryFrameStep),
                tests.Given_MmdCenterExportDeltaSpike_When_ClampingExportPositions_Then_LimitsEveryFrameStep);
            RunTest(results, nameof(tests.Given_IkClampAndCenterLift_When_ApplyingExportSafetyGuards_Then_ClampRunsBeforeFloorLift),
                tests.Given_IkClampAndCenterLift_When_ApplyingExportSafetyGuards_Then_ClampRunsBeforeFloorLift);
            RunTest(results, nameof(tests.Given_NeckAndHeadBones_When_WritingVmd_Then_ExportNamesFollowEnumBoneIdentity),
                tests.Given_NeckAndHeadBones_When_WritingVmd_Then_ExportNamesFollowEnumBoneIdentity);
            RunTest(results, nameof(tests.Given_MmdLowerBodyBone_When_WritingVmd_Then_HipsRotationCarrierCanBeExported),
                tests.Given_MmdLowerBodyBone_When_WritingVmd_Then_HipsRotationCarrierCanBeExported);
            RunTest(results, nameof(tests.Given_MmdCenterAndLowerBody_When_SelectingRotationCarrier_Then_CenterIsIdentityAndLowerBodyKeepsHipsRotation),
                tests.Given_MmdCenterAndLowerBody_When_SelectingRotationCarrier_Then_CenterIsIdentityAndLowerBodyKeepsHipsRotation);
            RunTest(results, nameof(tests.Given_GhostBoneUnderRotatedParent_When_CapturingRotationDiagnostic_Then_ExporterUsesGhostLocalRotation),
                tests.Given_GhostBoneUnderRotatedParent_When_CapturingRotationDiagnostic_Then_ExporterUsesGhostLocalRotation);
            RunTest(results, nameof(tests.Given_GhostBoneWithNonIdentityRestParent_When_CapturingRotationDiagnostic_Then_ReportsSourceLocalDeltaResidual),
                tests.Given_GhostBoneWithNonIdentityRestParent_When_CapturingRotationDiagnostic_Then_ReportsSourceLocalDeltaResidual);
            RunTest(results, nameof(tests.Given_GhostBoneWithNonIdentityRestParent_When_CapturingRotationDiagnostic_Then_ParentRestBasisCorrectionMatchesSourceDelta),
                tests.Given_GhostBoneWithNonIdentityRestParent_When_CapturingRotationDiagnostic_Then_ParentRestBasisCorrectionMatchesSourceDelta);
            RunTest(results, nameof(tests.Given_GhostBoneWithNonIdentityRestParent_When_CapturingRotationDiagnostic_Then_ExportRotationUsesParentRestBasisCorrection),
                tests.Given_GhostBoneWithNonIdentityRestParent_When_CapturingRotationDiagnostic_Then_ExportRotationUsesParentRestBasisCorrection);
            RunTest(results, nameof(tests.Given_ExportRotationDiagnostics_When_BuildingCsv_Then_WorstResidualFramesAreReported),
                tests.Given_ExportRotationDiagnostics_When_BuildingCsv_Then_WorstResidualFramesAreReported);
            RunTest(results, nameof(tests.Given_ExportRotationDiagnosticSamples_When_BuildingCsv_Then_PerFrameRowsAreReported),
                tests.Given_ExportRotationDiagnosticSamples_When_BuildingCsv_Then_PerFrameRowsAreReported);
            RunTest(results, nameof(tests.Given_ExportIkSourceDiagnostics_When_BuildingCsv_Then_PerFrameRowsAreReported),
                tests.Given_ExportIkSourceDiagnostics_When_BuildingCsv_Then_PerFrameRowsAreReported);
            RunTest(results, nameof(tests.Given_MovingModelRootNode_When_ResolvingFootIkRootReference_Then_UsesMovingRoot),
                tests.Given_MovingModelRootNode_When_ResolvingFootIkRootReference_Then_UsesMovingRoot);

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

            Console.WriteLine($"VmdFileWriter tests completed; results written to {resultPath}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static void RunTest(List<TestResultRecord> results, string methodName, TestDelegate action)
        {
            string name = typeof(VmdFileWriterTests).FullName + "." + methodName;
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
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(VmdFileWriterTests).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

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
