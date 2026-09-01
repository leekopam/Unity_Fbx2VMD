using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.FBXImporter
{
    public static class PoseSpaceRetargeterGroundingVerticalStepTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-PoseSpaceRetargeterGroundingVerticalStep.xml");
            }

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var results = new List<TestResultRecord>();
            var tests = new PoseSpaceRetargeterGroundingVerticalStepTests();

            RunTest(results, nameof(tests.Given_FiniteGroundingHeights_When_CalculatingAdjustment_Then_ReturnsTargetMinusContact),
                tests.Given_FiniteGroundingHeights_When_CalculatingAdjustment_Then_ReturnsTargetMinusContact);
            RunTest(results, nameof(tests.Given_NonFiniteGroundingAdjustment_When_CalculatingAdjustment_Then_ReturnsFalse),
                tests.Given_NonFiniteGroundingAdjustment_When_CalculatingAdjustment_Then_ReturnsFalse);
            RunTest(results, nameof(tests.Given_InitialGrounding_When_CalculatingVerticalStep_Then_AppliesFullAdjustment),
                tests.Given_InitialGrounding_When_CalculatingVerticalStep_Then_AppliesFullAdjustment);
            RunTest(results, nameof(tests.Given_InitializedAdjustmentInsideDeadZone_When_CalculatingVerticalStep_Then_SkipsStep),
                tests.Given_InitializedAdjustmentInsideDeadZone_When_CalculatingVerticalStep_Then_SkipsStep);
            RunTest(results, nameof(tests.Given_InitializedSmoothCorrection_When_CalculatingVerticalStep_Then_SubtractsDeadZoneAndClamps),
                tests.Given_InitializedSmoothCorrection_When_CalculatingVerticalStep_Then_SubtractsDeadZoneAndClamps);
            RunTest(results, nameof(tests.Given_DirectionReversal_When_CalculatingVerticalStep_Then_UsesReducedClampLimit),
                tests.Given_DirectionReversal_When_CalculatingVerticalStep_Then_UsesReducedClampLimit);
            RunTest(results, nameof(tests.Given_GroundingCalculation_When_CheckingOwnership_Then_UsesGroundingStabilizer),
                tests.Given_GroundingCalculation_When_CheckingOwnership_Then_UsesGroundingStabilizer);
            RunTest(results, nameof(tests.Given_PrewarmGroundingDiagnostics_When_ResettingPlaybackStabilityMetrics_Then_ResetsCountersWithoutClearingSettledState),
                tests.Given_PrewarmGroundingDiagnostics_When_ResettingPlaybackStabilityMetrics_Then_ResetsCountersWithoutClearingSettledState);
            RunTest(results, nameof(tests.Given_ManualReferenceFootLift_When_CalculatingGroundingTarget_Then_AddsPositiveLift),
                tests.Given_ManualReferenceFootLift_When_CalculatingGroundingTarget_Then_AddsPositiveLift);
            RunTest(results, nameof(tests.Given_ManualReferenceFootDropsBelowRest_When_CalculatingGroundingTarget_Then_DoesNotPushBelowFloor),
                tests.Given_ManualReferenceFootDropsBelowRest_When_CalculatingGroundingTarget_Then_DoesNotPushBelowFloor);
            RunTest(results, nameof(tests.Given_ManualReferenceFootLiftExceedsCap_When_CalculatingGroundingTarget_Then_ClampsLift),
                tests.Given_ManualReferenceFootLiftExceedsCap_When_CalculatingGroundingTarget_Then_ClampsLift);
            RunTest(results, nameof(tests.Given_ManualReferenceFootLiftAndZeroMaxLift_When_CalculatingGroundingTarget_Then_TreatsLiftAsUnlimited),
                tests.Given_ManualReferenceFootLiftAndZeroMaxLift_When_CalculatingGroundingTarget_Then_TreatsLiftAsUnlimited);
            RunTest(results, nameof(tests.Given_ManualReferenceFootLiftAndWeightAboveOne_When_CalculatingGroundingTarget_Then_ClampsWeight),
                tests.Given_ManualReferenceFootLiftAndWeightAboveOne_When_CalculatingGroundingTarget_Then_ClampsWeight);
            RunTest(results, nameof(tests.Given_NonFiniteManualReferenceFootHeight_When_CalculatingGroundingTarget_Then_ReturnsBaseTarget),
                tests.Given_NonFiniteManualReferenceFootHeight_When_CalculatingGroundingTarget_Then_ReturnsBaseTarget);

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

            Console.WriteLine($"PoseSpaceRetargeterGroundingVerticalStep tests completed; results written to {resultPath}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static void RunTest(List<TestResultRecord> results, string methodName, TestDelegate action)
        {
            string name = typeof(PoseSpaceRetargeterGroundingVerticalStepTests).FullName + "." + methodName;
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
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(PoseSpaceRetargeterGroundingVerticalStepTests).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

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
