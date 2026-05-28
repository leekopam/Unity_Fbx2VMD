using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.FBXImporter
{
    public static class PoseSpaceRetargeterGroundedFootLockCorrectionTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-PoseSpaceRetargeterGroundedFootLockCorrection.xml");
            }

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var results = new List<TestResultRecord>();
            var tests = new PoseSpaceRetargeterGroundedFootLockCorrectionTests();

            RunTest(results, nameof(tests.Given_NoFootCorrections_When_CalculatingRootCorrection_Then_ReturnsFalse),
                tests.Given_NoFootCorrections_When_CalculatingRootCorrection_Then_ReturnsFalse);
            RunTest(results, nameof(tests.Given_WeightedAverageWithinMaxStep_When_CalculatingRootCorrection_Then_DropsYAndAppliesWeight),
                tests.Given_WeightedAverageWithinMaxStep_When_CalculatingRootCorrection_Then_DropsYAndAppliesWeight);
            RunTest(results, nameof(tests.Given_CorrectionExceedsMaxStep_When_CalculatingRootCorrection_Then_ClampsMagnitude),
                tests.Given_CorrectionExceedsMaxStep_When_CalculatingRootCorrection_Then_ClampsMagnitude);
            RunTest(results, nameof(tests.Given_TinyCorrection_When_CalculatingRootCorrection_Then_ReturnsFalse),
                tests.Given_TinyCorrection_When_CalculatingRootCorrection_Then_ReturnsFalse);
            RunTest(results, nameof(tests.Given_NonFiniteCorrection_When_CalculatingRootCorrection_Then_ReturnsFalse),
                tests.Given_NonFiniteCorrection_When_CalculatingRootCorrection_Then_ReturnsFalse);
            RunTest(results, nameof(tests.Given_UnlockedFootInsideContact_When_CalculatingFootLockCorrection_Then_StartsLockWithoutCorrection),
                tests.Given_UnlockedFootInsideContact_When_CalculatingFootLockCorrection_Then_StartsLockWithoutCorrection);
            RunTest(results, nameof(tests.Given_LockedFootStillGrounded_When_CalculatingFootLockCorrection_Then_ReturnsPlanarCorrection),
                tests.Given_LockedFootStillGrounded_When_CalculatingFootLockCorrection_Then_ReturnsPlanarCorrection);
            RunTest(results, nameof(tests.Given_LockedFootAboveReleaseHeight_When_CalculatingFootLockCorrection_Then_UnlocksWithoutCorrection),
                tests.Given_LockedFootAboveReleaseHeight_When_CalculatingFootLockCorrection_Then_UnlocksWithoutCorrection);
            RunTest(results, nameof(tests.Given_LockedFootCorrectionExceedsResetDistance_When_CalculatingFootLockCorrection_Then_ResetsLockAndAccumulatesZero),
                tests.Given_LockedFootCorrectionExceedsResetDistance_When_CalculatingFootLockCorrection_Then_ResetsLockAndAccumulatesZero);

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

            Console.WriteLine($"PoseSpaceRetargeterGroundedFootLockCorrection tests completed; results written to {resultPath}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static void RunTest(List<TestResultRecord> results, string methodName, TestDelegate action)
        {
            string name = typeof(PoseSpaceRetargeterGroundedFootLockCorrectionTests).FullName + "." + methodName;
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
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(PoseSpaceRetargeterGroundedFootLockCorrectionTests).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

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
