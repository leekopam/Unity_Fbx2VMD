using System.IO;

namespace Fbx2Vmd.Settings
{
    public readonly struct MainRecordingSettingsLaunchPlan
    {
        public MainRecordingSettingsLaunchPlan(
            string workingDirectory,
            string executablePath,
            string arguments,
            string settingsPath)
        {
            WorkingDirectory = workingDirectory;
            ExecutablePath = executablePath;
            Arguments = arguments;
            SettingsPath = settingsPath;
        }

        public string WorkingDirectory { get; }
        public string ExecutablePath { get; }
        public string ExecutableName => string.IsNullOrWhiteSpace(ExecutablePath)
            ? string.Empty
            : Path.GetFileName(ExecutablePath);
        public string Arguments { get; }
        public string SettingsPath { get; }
    }
}
