using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Fbx2Vmd.Settings;
using NUnit.Framework;

namespace Tests.Editor.Settings
{
    public sealed class MainRecordingSettingsCompanionProcessLauncherTests
    {
        private const string ProcessLauncherTypeName =
            "Fbx2Vmd.Settings.EditorTools.MainRecordingSettingsCompanionProcessLauncher, Assembly-CSharp-Editor";
        private const string ProcessLauncherSourcePath =
            "Assets/_Project/Scripts/Editor/Settings/MainRecordingSettingsCompanionProcessLauncher.cs";
        private const string CompanionLauncherSourcePath =
            "Assets/_Project/Scripts/Editor/Settings/MainRecordingSettingsCompanionLauncher.cs";

        [Test]
        public void Given_CompanionLauncher_When_InspectingProcessOwnership_Then_DelegatesInfrastructure()
        {
            Assert.That(File.Exists(ProcessLauncherSourcePath), Is.True, ProcessLauncherSourcePath);
            Assert.That(Type.GetType(ProcessLauncherTypeName), Is.Not.Null, ProcessLauncherTypeName);

            string companionSource = File.ReadAllText(CompanionLauncherSourcePath);
            string processSource = File.ReadAllText(ProcessLauncherSourcePath);

            Assert.That(
                companionSource,
                Does.Contain("MainRecordingSettingsCompanionProcessLauncher.Launch"));
            Assert.That(companionSource, Does.Not.Contain("Process.Start("));
            Assert.That(companionSource, Does.Not.Contain("ProcessStartInfo"));
            Assert.That(companionSource, Does.Not.Contain("ResolveNpmExecutable"));
            Assert.That(companionSource, Does.Not.Contain("FindOnPath("));
            Assert.That(processSource, Does.Contain("Process.Start("));
        }

        [Test]
        public void Given_CommandScript_When_BuildingProcessStartInfo_Then_UsesShellAndSettingsEnvironment()
        {
            Type processLauncherType = Type.GetType(ProcessLauncherTypeName);
            Assert.That(processLauncherType, Is.Not.Null, ProcessLauncherTypeName);
            MethodInfo buildStartInfo = processLauncherType.GetMethod(
                "BuildProcessStartInfo",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(buildStartInfo, Is.Not.Null);

            var plan = new MainRecordingSettingsLaunchPlan(
                "Assets/_Project/Tools/MainRecordingSettings",
                "npm",
                "run start:prod",
                "D:/Data/main-recording-settings.json");
            const string resolvedExecutable = "D:/Tools/npm.cmd";
            const string fullWorkingDirectory = "D:/Workspace/MainRecordingSettings";

            var startInfo = (ProcessStartInfo)buildStartInfo.Invoke(
                null,
                new object[] { plan, resolvedExecutable, fullWorkingDirectory });

            Assert.That(
                startInfo.FileName,
                Is.EqualTo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"));
            Assert.That(startInfo.Arguments, Does.StartWith("/d /c \"\"D:/Tools/npm.cmd\" "));
            Assert.That(startInfo.Arguments, Does.Contain("run start:prod"));
            Assert.That(startInfo.WorkingDirectory, Is.EqualTo(fullWorkingDirectory));
            Assert.That(startInfo.UseShellExecute, Is.False);
            Assert.That(startInfo.CreateNoWindow, Is.True);
            Assert.That(
                startInfo.Environment[MainRecordingSettingsPathResolver.EnvironmentVariableName],
                Is.EqualTo(plan.SettingsPath));
        }
    }
}
