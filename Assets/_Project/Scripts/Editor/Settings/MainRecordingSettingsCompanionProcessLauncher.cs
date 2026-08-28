using System;
using System.Diagnostics;
using System.IO;
using Fbx2Vmd.Settings;

namespace Fbx2Vmd.Settings.EditorTools
{
    internal static class MainRecordingSettingsCompanionProcessLauncher
    {
        internal static void Launch(MainRecordingSettingsLaunchPlan plan)
        {
            string fullWorkingDirectory = Path.GetFullPath(plan.WorkingDirectory);
            if (!Directory.Exists(fullWorkingDirectory))
            {
                throw new DirectoryNotFoundException(fullWorkingDirectory);
            }

            string executable = ResolveNpmExecutable(plan.ExecutablePath);
            Process.Start(BuildProcessStartInfo(plan, executable, fullWorkingDirectory));
        }

        internal static ProcessStartInfo BuildProcessStartInfo(
            MainRecordingSettingsLaunchPlan plan,
            string executable,
            string fullWorkingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveProcessFileName(executable),
                Arguments = ResolveProcessArguments(executable, plan.Arguments),
                WorkingDirectory = fullWorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment[MainRecordingSettingsPathResolver.EnvironmentVariableName] = plan.SettingsPath;
            return startInfo;
        }

        private static string ResolveNpmExecutable(string fallbackExecutable)
        {
            string pathCommand = FindOnPath("npm.cmd");
            if (!string.IsNullOrEmpty(pathCommand))
            {
                return pathCommand;
            }

            string standardPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "nodejs",
                "npm.cmd");
            return File.Exists(standardPath) ? standardPath : fallbackExecutable;
        }

        private static string ResolveProcessFileName(string executable)
        {
            return IsWindowsCommandScript(executable)
                ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
                : executable;
        }

        private static string ResolveProcessArguments(string executable, string arguments)
        {
            return IsWindowsCommandScript(executable)
                ? "/d /c \"\"" + executable + "\" " + arguments + "\""
                : arguments;
        }

        private static bool IsWindowsCommandScript(string executable)
        {
            return executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                   executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        }

        private static string FindOnPath(string fileName)
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                string candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }
    }
}
