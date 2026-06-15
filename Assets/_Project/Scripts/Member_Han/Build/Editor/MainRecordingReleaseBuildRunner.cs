using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Member_Han.Build.EditorTools
{
    public static class MainRecordingReleaseBuildRunner
    {
        private static readonly string[] MainScenes =
        {
            "Assets/_Project/Scene/Main_Auto.unity",
            "Assets/_Project/Scene/Main_Recoding.unity",
            "Assets/_Project/Scene/Sub_Manual.unity",
            "Assets/_Project/Scene/FbxImport_Capture.unity",
        };

        private static readonly string[] CompanionScenes =
        {
            "Assets/_Project/Scene/MainRecording_SettingsCompanion.unity",
        };

        public const string OutputDirectory = "Builds/Local/MainRecordingRelease";
        public const string MainExecutablePath = OutputDirectory + "/Unity_Fbx2VMD.exe";
        public const string CompanionExecutablePath = OutputDirectory + "/Unity_Fbx2VMD_Settings.exe";

        public static string[] MainScenePaths => (string[])MainScenes.Clone();
        public static string[] CompanionScenePaths => (string[])CompanionScenes.Clone();

        public static void BuildWindowsSmoke()
        {
            BuildWindowsSmoke(BuildOptions.Development);
        }

        public static void BuildWindowsSmoke(BuildOptions options)
        {
            PrepareOutputDirectory();
            BuildPlayer("main", MainScenes, MainExecutablePath, options);
            BuildPlayer("settings", CompanionScenes, CompanionExecutablePath, options);
        }

        private static void PrepareOutputDirectory()
        {
            string fullOutputDirectory = Path.GetFullPath(OutputDirectory);
            Directory.CreateDirectory(fullOutputDirectory);
        }

        private static void BuildPlayer(string label, string[] scenes, string outputPath, BuildOptions options)
        {
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            BuildReport report = BuildPipeline.BuildPlayer(
                scenes,
                outputPath,
                BuildTarget.StandaloneWindows64,
                options);

            EnsureBuildSucceeded(label, report, outputPath);
        }

        private static void EnsureBuildSucceeded(string label, BuildReport report, string outputPath)
        {
            if (report == null)
            {
                throw new InvalidOperationException($"{label} build did not return a report.");
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{label} build failed: result={summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}");
            }

            if (!File.Exists(outputPath))
            {
                throw new FileNotFoundException($"{label} build output was not created.", outputPath);
            }

            Debug.Log(
                $"[MainRecordingReleaseBuildRunner] {label} build succeeded: " +
                $"output={outputPath}, size={summary.totalSize}, warnings={summary.totalWarnings}");
        }
    }
}
