using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.FBXImporter
{
    public static class PoseSpaceRetargeterRootMotionDeltaTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-PoseSpaceRetargeterRootMotionDelta.xml");
            }

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var results = new List<TestResultRecord>();
            var tests = new PoseSpaceRetargeterRootMotionDeltaTests();

            RunTest(results, nameof(tests.Given_RootMotionGuardOwnsRootDelta_When_CheckingPoseSpaceRetargeterContract_Then_DoesNotKeepDuplicateHelpers),
                tests.Given_RootMotionGuardOwnsRootDelta_When_CheckingPoseSpaceRetargeterContract_Then_DoesNotKeepDuplicateHelpers);
            RunTest(results, nameof(tests.Given_FiniteInputsWithoutBodyRootPolicy_When_CalculatingRootMotionDelta_Then_CombinesScaledGhostAndEditorDelta),
                tests.Given_FiniteInputsWithoutBodyRootPolicy_When_CalculatingRootMotionDelta_Then_CombinesScaledGhostAndEditorDelta);
            RunTest(results, nameof(tests.Given_ZeroMovementScale_When_CalculatingRootMotionDelta_Then_SuppressesGhostEditorAndBodyRootSources),
                tests.Given_ZeroMovementScale_When_CalculatingRootMotionDelta_Then_SuppressesGhostEditorAndBodyRootSources);
            RunTest(results, nameof(tests.Given_MainRecordingMovingRootPolicy_When_CalculatingRootMotionDelta_Then_PreservesBodyRootSourceWithoutLegacyDoubleCount),
                tests.Given_MainRecordingMovingRootPolicy_When_CalculatingRootMotionDelta_Then_PreservesBodyRootSourceWithoutLegacyDoubleCount);
            RunTest(results, nameof(tests.Given_MainRecordingMovingRootPolicyAndMovementScale_When_CalculatingRootMotionDelta_Then_ScalesBodyRootSourceOnly),
                tests.Given_MainRecordingMovingRootPolicyAndMovementScale_When_CalculatingRootMotionDelta_Then_ScalesBodyRootSourceOnly);
            RunTest(results, nameof(tests.Given_NonFiniteInput_When_CalculatingRootMotionDelta_Then_ReturnsZeroAndReportsNaN),
                tests.Given_NonFiniteInput_When_CalculatingRootMotionDelta_Then_ReturnsZeroAndReportsNaN);
            RunTest(results, nameof(tests.Given_DeltaExceedsLimitAndClampEnabled_When_CalculatingRootMotionDelta_Then_LimitsDeltaAndReportsSpike),
                tests.Given_DeltaExceedsLimitAndClampEnabled_When_CalculatingRootMotionDelta_Then_LimitsDeltaAndReportsSpike);
            RunTest(results, nameof(tests.Given_DeltaExceedsLimitAndClampEnabled_When_CalculatingRootMotionDelta_Then_KeepsLimitedMovement),
                tests.Given_DeltaExceedsLimitAndClampEnabled_When_CalculatingRootMotionDelta_Then_KeepsLimitedMovement);
            RunTest(results, nameof(tests.Given_DeltaExceedsLimitAndClampDisabled_When_CalculatingRootMotionDelta_Then_KeepsDelta),
                tests.Given_DeltaExceedsLimitAndClampDisabled_When_CalculatingRootMotionDelta_Then_KeepsDelta);
            RunTest(results, nameof(tests.Given_ZeroMovementScaleMultiplier_When_Normalizing_Then_AllowsStationaryRootMotion),
                tests.Given_ZeroMovementScaleMultiplier_When_Normalizing_Then_AllowsStationaryRootMotion);
            RunTest(results, nameof(tests.Given_FirstEditorDelta_When_CalculatingReferenceDelta_Then_AppliesWeightAndStartsSmoothing),
                tests.Given_FirstEditorDelta_When_CalculatingReferenceDelta_Then_AppliesWeightAndStartsSmoothing);
            RunTest(results, nameof(tests.Given_PreviousSmoothedDelta_When_CalculatingReferenceDelta_Then_BlendsTowardWeightedDelta),
                tests.Given_PreviousSmoothedDelta_When_CalculatingReferenceDelta_Then_BlendsTowardWeightedDelta);
            RunTest(results, nameof(tests.Given_GhostAlreadyMovedInXZ_When_CalculatingReferenceDelta_Then_SkipsAndKeepsSmoothingState),
                tests.Given_GhostAlreadyMovedInXZ_When_CalculatingReferenceDelta_Then_SkipsAndKeepsSmoothingState);
            RunTest(results, nameof(tests.Given_NonFiniteEditorDelta_When_CalculatingReferenceDelta_Then_SkipsAndKeepsSmoothingState),
                tests.Given_NonFiniteEditorDelta_When_CalculatingReferenceDelta_Then_SkipsAndKeepsSmoothingState);
            RunTest(results, nameof(tests.Given_FinitePoseAndManualBodyReference_When_SelectingBodyRootMotionSource_Then_PrefersPosePosition),
                tests.Given_FinitePoseAndManualBodyReference_When_SelectingBodyRootMotionSource_Then_PrefersPosePosition);
            RunTest(results, nameof(tests.Given_NonFinitePoseAndFiniteManualReference_When_SelectingBodyRootMotionSource_Then_UsesManualReference),
                tests.Given_NonFinitePoseAndFiniteManualReference_When_SelectingBodyRootMotionSource_Then_UsesManualReference);
            RunTest(results, nameof(tests.Given_NonFinitePoseAndManualReferencePreferenceDisabled_When_SelectingBodyRootMotionSource_Then_KeepsPoseFallback),
                tests.Given_NonFinitePoseAndManualReferencePreferenceDisabled_When_SelectingBodyRootMotionSource_Then_KeepsPoseFallback);
            RunTest(results, nameof(tests.Given_NonFinitePoseAndNonFiniteManualReference_When_SelectingBodyRootMotionSource_Then_KeepsPoseFallback),
                tests.Given_NonFinitePoseAndNonFiniteManualReference_When_SelectingBodyRootMotionSource_Then_KeepsPoseFallback);
            RunTest(results, nameof(tests.Given_NonFinitePoseAndUnavailableManualReference_When_SelectingBodyRootMotionSource_Then_KeepsPoseFallback),
                tests.Given_NonFinitePoseAndUnavailableManualReference_When_SelectingBodyRootMotionSource_Then_KeepsPoseFallback);

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

            Console.WriteLine($"PoseSpaceRetargeterRootMotionDelta tests completed; results written to {resultPath}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static void RunTest(List<TestResultRecord> results, string methodName, TestDelegate action)
        {
            string name = typeof(PoseSpaceRetargeterRootMotionDeltaTests).FullName + "." + methodName;
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
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(PoseSpaceRetargeterRootMotionDeltaTests).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

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
