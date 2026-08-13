using System.IO;
using System.Linq;
using Fbx2Vmd.Build;
using NUnit.Framework;

namespace Tests.Editor.Settings
{
    public class MainRecordingSettingsBuildReleaseTests
    {
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string SubManualScenePath = "Assets/_Project/Scene/Sub_Manual.unity";
        private const string FbxImportCaptureScenePath = "Assets/_Project/Scene/FbxImport_Capture.unity";
        private const string ReleaseSmokeScriptPath = "Docs/Workflow/Tools/Local/scripts/harness/build_main_recording_release_smoke.ps1";
        private const string ElectronPackageScriptPath = "Assets/_Project/Tools/MainRecordingSettings/scripts/packageElectronRelease.mjs";
        private const string ElectronPackageJsonPath = "Assets/_Project/Tools/MainRecordingSettings/package.json";


        [Test]
        public void Given_ReleaseBuildRunner_When_InspectingContract_Then_MainAndElectronSettingsOutputsAreSeparate()
        {
            string[] mainScenes = MainRecordingReleaseBuildRunner.MainScenePaths
                .Select(NormalizePath)
                .ToArray();

            Assert.That(mainScenes, Is.EqualTo(new[]
            {
                MainAutoScenePath,
                MainRecordingScenePath,
                SubManualScenePath,
                FbxImportCaptureScenePath,
            }));
            Assert.That(NormalizePath(MainRecordingReleaseBuildRunner.MainExecutablePath), Is.EqualTo("Builds/Local/MainRecordingRelease/Unity_Fbx2VMD.exe"));
            Assert.That(NormalizePath(MainRecordingReleaseBuildRunner.SettingsPackageDirectory), Is.EqualTo("Builds/Local/MainRecordingRelease/MainRecordingSettings"));
            Assert.That(NormalizePath(MainRecordingReleaseBuildRunner.SettingsExecutablePath), Is.EqualTo("Builds/Local/MainRecordingRelease/MainRecordingSettings/Unity_Fbx2VMD_Settings.exe"));
            Assert.That(NormalizePath(MainRecordingReleaseBuildRunner.SettingsResourcesAppPath), Is.EqualTo("Builds/Local/MainRecordingRelease/MainRecordingSettings/resources/app"));
            Assert.That(NormalizePath(MainRecordingReleaseBuildRunner.SettingsAppArchivePath), Is.EqualTo("Builds/Local/MainRecordingRelease/MainRecordingSettings/resources/app.asar"));
        }

        [Test]
        public void Given_ElectronPackageReleaseScript_When_InspectingContents_Then_ProducesSelfContainedSettingsApp()
        {
            Assert.That(File.Exists(ElectronPackageScriptPath), Is.True, "Electron release package script must exist.");
            Assert.That(File.Exists(ElectronPackageJsonPath), Is.True, "Electron package.json must exist.");

            string script = File.ReadAllText(ElectronPackageScriptPath);
            string packageJson = File.ReadAllText(ElectronPackageJsonPath);

            Assert.That(packageJson, Does.Contain("\"package:release\""));
            Assert.That(script, Does.Contain("Unity_Fbx2VMD_Settings.exe"));
            Assert.That(script, Does.Contain("resourcesAppPath"));
            Assert.That(script, Does.Contain("app.asar"));
            Assert.That(script, Does.Contain("node_modules/ws"));
            Assert.That(script, Does.Not.Contain("electron-packager"));
        }

        [Test]
        public void Given_RuntimeSettingsSourceFiles_When_InspectingContents_Then_UnityEditorIsNotReferenced()
        {
            string[] runtimeSourceFiles =
            {
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsActionResult.cs",
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsActions.cs",
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsCompanionController.cs",
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsDocument.cs",
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsLaunchPlan.cs",
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsPathResolver.cs",
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsBootstrap.cs",
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsLauncher.cs",
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsState.cs",
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsStore.cs",
                "Assets/_Project/Scripts/Settings/RecordingSetting.cs",
            };

            foreach (string sourceFile in runtimeSourceFiles)
            {
                Assert.That(File.Exists(sourceFile), Is.True, sourceFile);

                string contents = File.ReadAllText(sourceFile);
                Assert.That(contents, Does.Not.Contain("UnityEditor"), sourceFile);
            }
        }

        [Test]
        public void Given_ReleaseSmokeScript_When_InspectingContents_Then_InvokesBuildRunnerAndChecksTwoOutputs()
        {
            Assert.That(File.Exists(ReleaseSmokeScriptPath), Is.True, "Release smoke script must exist.");

            string script = File.ReadAllText(ReleaseSmokeScriptPath);
            Assert.That(script, Does.Contain("Fbx2Vmd.Build.EditorTools.MainRecordingReleaseBuildRunner.BuildWindowsSmoke"));
            Assert.That(script, Does.Contain("Unity_Fbx2VMD.exe"));
            Assert.That(script, Does.Contain("Unity_Fbx2VMD_Settings.exe"));
            Assert.That(script, Does.Contain("MainRecordingSettings"));
            Assert.That(script, Does.Contain("main_exe_exists"));
            Assert.That(script, Does.Contain("settings_exe_exists"));
            Assert.That(script, Does.Contain("settings_app_resources_exist"));
            Assert.That(script, Does.Contain("settings_app_archive_exists"));
            Assert.That(script, Does.Contain("settings_package_mode"));
            Assert.That(script, Does.Contain("player_autostart_settings_process_started"));
            Assert.That(script, Does.Contain("settings_http_health_status"));
            Assert.That(script, Does.Contain("settings_websocket_status_messages"));
            Assert.That(script, Does.Contain("settings_process_exited_after_player_close"));
        }

        [Test]
        public void Given_ReleaseBuildRunner_When_InspectingContract_Then_RemovesStaleRootSettingsCompanionOutput()
        {
            Assert.That(
                NormalizePath(MainRecordingReleaseBuildRunner.StaleRootSettingsExecutablePath),
                Is.EqualTo("Builds/Local/MainRecordingRelease/Unity_Fbx2VMD_Settings.exe"));
            Assert.That(
                NormalizePath(MainRecordingReleaseBuildRunner.StaleRootSettingsDataDirectory),
                Is.EqualTo("Builds/Local/MainRecordingRelease/Unity_Fbx2VMD_Settings_Data"));

            string source = File.ReadAllText(
                "Assets/_Project/Scripts/Build/Editor/MainRecordingReleaseBuildRunner.cs");
            Assert.That(source, Does.Contain("DeleteStaleRootSettingsCompanionOutputs"));
        }

        [Test]
        public void Given_GitIgnore_When_InspectingReleaseBuildOutput_Then_BuildsDirectoryIsIgnored()
        {
            string gitIgnore = File.ReadAllText(".gitignore");

            Assert.That(gitIgnore, Does.Contain("/Builds/"));
        }

        [Test]
        public void Given_ProjectDependencies_When_InspectingMainRecordingSettingsScope_Then_DoesNotAddPackageManagerClothDependencies()
        {
            string manifest = File.ReadAllText("Packages/manifest.json");
            string packageLock = File.ReadAllText("Packages/packages-lock.json");

            Assert.That(manifest, Does.Not.Contain("magica").IgnoreCase);
            Assert.That(packageLock, Does.Not.Contain("magica").IgnoreCase);
        }

        [Test]
        public void Given_GitIgnore_When_InspectingLocalLargeAssets_Then_LocalOnlyFoldersAreIgnored()
        {
            string gitIgnore = File.ReadAllText(".gitignore");

            Assert.That(gitIgnore, Does.Contain("/tmp/"));
            Assert.That(gitIgnore, Does.Contain("/Docs/ref/"));
            Assert.That(gitIgnore, Does.Contain("/Assets/GUIPack-Clean&Minimalist/"));
            Assert.That(gitIgnore, Does.Contain("/Assets/GUIPack-Clean&Minimalist.meta"));
            Assert.That(gitIgnore, Does.Contain("/Assets/UI/GUIPack-Clean&Minimalist/"));
            Assert.That(gitIgnore, Does.Contain("/Assets/UI/GUIPack-Clean&Minimalist.meta"));
            Assert.That(gitIgnore, Does.Contain("/Assets/MagicaCloth2/"));
            Assert.That(gitIgnore, Does.Contain("/Assets/MagicaCloth2.meta"));
            Assert.That(gitIgnore, Does.Contain("/Assets/_Project/Plugins/MagicaCloth2/"));
            Assert.That(gitIgnore, Does.Contain("/Assets/_Project/Plugins/MagicaCloth2.meta"));
        }


        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/');
        }
    }
}
