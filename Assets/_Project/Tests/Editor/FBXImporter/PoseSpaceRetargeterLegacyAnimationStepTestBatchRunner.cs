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
