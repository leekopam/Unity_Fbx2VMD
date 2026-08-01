using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Fbx2Vmd.Build.EditorTools
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

        public const string OutputDirectory = "Builds/Local/MainRecordingRelease";
        public const string MainExecutablePath = OutputDirectory + "/Unity_Fbx2VMD.exe";
        public const string StaleRootSettingsExecutablePath = OutputDirectory + "/Unity_Fbx2VMD_Settings.exe";
        public const string StaleRootSettingsDataDirectory = OutputDirectory + "/Unity_Fbx2VMD_Settings_Data";
        public const string SettingsPackageDirectory = OutputDirectory + "/MainRecordingSettings";
        public const string SettingsExecutablePath = SettingsPackageDirectory + "/Unity_Fbx2VMD_Settings.exe";
        public const string SettingsResourcesAppPath = SettingsPackageDirectory + "/resources/app";
        public const string SettingsAppArchivePath = SettingsPackageDirectory + "/resources/app.asar";
        public const string SettingsAppRoot = "Assets/_Project/Tools/MainRecordingSettings";
        public const string SettingsPackageScriptPath = SettingsAppRoot + "/scripts/packageElectronRelease.mjs";

        public static string[] MainScenePaths => (string[])MainScenes.Clone();

        public static void BuildWindowsSmoke()
        {
            BuildWindowsSmoke(BuildOptions.Development);
        }

        public static void BuildWindowsSmoke(BuildOptions options)
        {
            PrepareOutputDirectory();
            BuildPlayer("main", MainScenes, MainExecutablePath, options);
            PackageElectronSettingsApp();
        }

        private static void PrepareOutputDirectory()
        {
            string fullOutputDirectory = Path.GetFullPath(OutputDirectory);
            Directory.CreateDirectory(fullOutputDirectory);
            DeleteStaleRootSettingsCompanionOutputs();
        }

        private static void DeleteStaleRootSettingsCompanionOutputs()
        {
            string staleExecutablePath = Path.GetFullPath(StaleRootSettingsExecutablePath);
            if (File.Exists(staleExecutablePath))
            {
                File.Delete(staleExecutablePath);
            }

            string staleDataDirectory = Path.GetFullPath(StaleRootSettingsDataDirectory);
            if (Directory.Exists(staleDataDirectory))
            {
                Directory.Delete(staleDataDirectory, true);
            }
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

            UnityEngine.Debug.Log(
                $"[MainRecordingReleaseBuildRunner] {label} build succeeded: " +
                $"output={outputPath}, size={summary.totalSize}, warnings={summary.totalWarnings}");
        }

        private static void PackageElectronSettingsApp()
        {
            string appRoot = Path.GetFullPath(SettingsAppRoot);
            string outputPath = Path.GetFullPath(SettingsPackageDirectory);
            string scriptPath = Path.GetFullPath(SettingsPackageScriptPath);
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("Electron settings package script was not found.", scriptPath);
            }

            string output = RunProcess(
                "node",
                $"{QuoteArgument(scriptPath)} --output {QuoteArgument(outputPath)}",
                appRoot);

            if (!File.Exists(Path.GetFullPath(SettingsExecutablePath)))
            {
                throw new FileNotFoundException(
                    "Electron settings package executable was not created.",
                    Path.GetFullPath(SettingsExecutablePath));
            }

            if (!File.Exists(Path.Combine(Path.GetFullPath(SettingsResourcesAppPath), "package.json")))
            {
                throw new FileNotFoundException(
                    "Electron settings package resources/app package.json was not created.",
                    Path.Combine(Path.GetFullPath(SettingsResourcesAppPath), "package.json"));
            }

            if (!File.Exists(Path.GetFullPath(SettingsAppArchivePath)))
            {
                throw new FileNotFoundException(
                    "Electron settings package app.asar was not created.",
                    Path.GetFullPath(SettingsAppArchivePath));
            }

            UnityEngine.Debug.Log("[MainRecordingSettingsPackage] Electron settings app packaged: " + output);
        }

        private static string RunProcess(string fileName, string arguments, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                if (!process.Start())
                {
                    throw new InvalidOperationException($"Failed to start process: {fileName}");
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"{fileName} {arguments} failed with exit code {process.ExitCode}.\n{stdout}\n{stderr}");
                }

                return string.IsNullOrWhiteSpace(stderr)
                    ? stdout.Trim()
                    : (stdout + "\n" + stderr).Trim();
            }
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
