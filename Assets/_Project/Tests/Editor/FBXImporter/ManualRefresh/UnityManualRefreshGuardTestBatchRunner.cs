using NUnit.Framework;
using System;
using System.IO;
using System.Security;
using UnityEditor;

namespace Tests.Editor.FBXImporter.ManualRefresh
{
    public static class UnityManualRefreshGuardTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-UnityManualRefreshGuard.xml");
            }

            string testName = typeof(UnityManualRefreshGuardTests).FullName + "." +
                nameof(UnityManualRefreshGuardTests.Given_MixedPaths_When_GetExistingAssetPaths_Then_NormalizesUnityAssetPaths);
            DateTimeOffset start = DateTimeOffset.UtcNow;
            string failure = null;

            try
            {
                var tests = new UnityManualRefreshGuardTests();
                tests.Given_MixedPaths_When_GetExistingAssetPaths_Then_NormalizesUnityAssetPaths();
            }
            catch (Exception ex)
            {
                failure = ex.ToString();
            }

            double duration = Math.Max(0.001, (DateTimeOffset.UtcNow - start).TotalSeconds);
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllText(resultPath, BuildXml(testName, duration, failure));

            if (failure == null)
            {
                Console.WriteLine($"UnityManualRefreshGuardTests passed; results written to {resultPath}");
                EditorApplication.Exit(0);
            }
            else
            {
                Console.Error.WriteLine(failure);
                EditorApplication.Exit(1);
            }
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

        private static string BuildXml(string testName, double duration, string failure)
        {
            string escapedName = SecurityElement.Escape(testName);
            string escapedFailure = SecurityElement.Escape(failure ?? string.Empty);
            string result = failure == null ? "Passed" : "Failed";
            string failureNode = failure == null ? string.Empty : $"<failure><message>{escapedFailure}</message></failure>";

            return
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                $"<test-run testcasecount=\"1\" result=\"{result}\" total=\"1\" passed=\"{(failure == null ? 1 : 0)}\" failed=\"{(failure == null ? 0 : 1)}\">\n" +
                $"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(UnityManualRefreshGuardTests).FullName)}\" result=\"{result}\" total=\"1\" passed=\"{(failure == null ? 1 : 0)}\" failed=\"{(failure == null ? 0 : 1)}\">\n" +
                $"    <test-case name=\"{escapedName}\" fullname=\"{escapedName}\" result=\"{result}\" duration=\"{duration:0.000}\">{failureNode}</test-case>\n" +
                "  </test-suite>\n" +
                "</test-run>\n";
        }
    }
}
