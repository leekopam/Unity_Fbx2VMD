using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.FileSystem;
using Fbx2Vmd.Settings;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Editor.Settings
{
    public class RecordingSettingEditorTests
    {
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string SettingsLauncherTypeName =
            "Fbx2Vmd.Settings.EditorTools.MainRecordingSettingsCompanionLauncher, Assembly-CSharp-Editor";
        private const string LayoutSpecTypeName =
            "Fbx2Vmd.Settings.MainRecordingSettingsLayoutSpec, Assembly-CSharp";
        private const string RuntimePopupTypeName =
            "Fbx2Vmd.Settings.MainRecordingSettingsPopup, Assembly-CSharp";
        private const string KoreanUiTextFallbackTypeName =
            "Fbx2Vmd.Settings.KoreanUiTextFallback, Assembly-CSharp";
        private const string CompanionControllerTypeName =
            "Fbx2Vmd.Settings.MainRecordingSettingsCompanionController, Assembly-CSharp";
        private const string EditorPlayModeGuardTypeName =
            "Fbx2Vmd.Settings.EditorTools.MainRecordingEditorPlayModeGuard, Assembly-CSharp-Editor";
        private const string RuntimeLauncherTypeName =
            "Fbx2Vmd.Settings.MainRecordingSettingsLauncher, Assembly-CSharp";
        private const string RuntimeBootstrapTypeName =
            "Fbx2Vmd.Settings.MainRecordingSettingsBootstrap, Assembly-CSharp";
        private const string SharedSettingsFileSessionTypeName =
            "Fbx2Vmd.Settings.RecordingSharedSettingsFileSession, Assembly-CSharp";

        [Test]
        public void Given_RecordingSetting_When_InspectingSharedSettingsFileState_Then_DelegatesToFileSession()
        {
            const string recordingSettingSourcePath =
                "Assets/_Project/Scripts/Settings/RecordingSetting.cs";
            const string sessionSourcePath =
                "Assets/_Project/Scripts/Settings/RecordingSharedSettingsFileSession.cs";

            Assert.That(File.Exists(sessionSourcePath), Is.True, sessionSourcePath);
            Assert.That(Type.GetType(SharedSettingsFileSessionTypeName), Is.Not.Null);

            string recordingSettingSource = File.ReadAllText(recordingSettingSourcePath);
            string sessionSource = File.ReadAllText(sessionSourcePath);

            Assert.That(
                recordingSettingSource,
                Does.Contain("private RecordingSharedSettingsFileSession sharedSettingsFileSession;"));
            Assert.That(recordingSettingSource, Does.Contain("sharedSettingsFileSession.LoadCurrent()"));
            Assert.That(recordingSettingSource, Does.Contain("sharedSettingsFileSession.TryLoadChanged("));
            Assert.That(recordingSettingSource, Does.Not.Contain("private MainRecordingSettingsStore"));
            Assert.That(recordingSettingSource, Does.Not.Contain("lastSharedSettingsWriteTimeUtc"));
            Assert.That(recordingSettingSource, Does.Not.Contain("DateTime.UtcNow"));
            Assert.That(recordingSettingSource, Does.Not.Contain("EnsureSharedSettingsStore("));
            Assert.That(sessionSource, Does.Contain("new MainRecordingSettingsStore("));
            Assert.That(sessionSource, Does.Contain("bool TryLoadChanged("));
            Assert.That(sessionSource, Does.Contain("void WriteRuntimePlayModeState("));
            Assert.That(sessionSource, Does.Not.Contain("MonoBehaviour"));
            Assert.That(sessionSource, Does.Not.Contain("interface "));
        }

        [Test]
        public void Given_RecordingSetting_When_InspectingDiagnosticsMapping_Then_UsesDiagnosticsSettingsValue()
        {
            const string diagnosticsSettingsSourcePath =
                "Assets/_Project/Scripts/Settings/RecordingDiagnosticsSettings.cs";
            const string recordingSettingSourcePath =
                "Assets/_Project/Scripts/Settings/RecordingSetting.cs";
            const string pipelineSourcePath =
                "Assets/_Project/Scripts/FBXImporter/FBXVmdPipeline.cs";
            const string diagnosticsSettingsTypeName =
                "Fbx2Vmd.Settings.RecordingDiagnosticsSettings, Assembly-CSharp";

            Assert.That(File.Exists(diagnosticsSettingsSourcePath), Is.True, diagnosticsSettingsSourcePath);
            Assert.That(Type.GetType(diagnosticsSettingsTypeName), Is.Not.Null, diagnosticsSettingsTypeName);

            string diagnosticsSettingsSource = File.ReadAllText(diagnosticsSettingsSourcePath);
            string recordingSettingSource = File.ReadAllText(recordingSettingSourcePath);
            string pipelineSource = File.ReadAllText(pipelineSourcePath);

            Assert.That(diagnosticsSettingsSource, Does.Contain("public readonly struct RecordingDiagnosticsSettings"));
            Assert.That(diagnosticsSettingsSource, Does.Not.Contain("MonoBehaviour"));
            Assert.That(diagnosticsSettingsSource, Does.Not.Contain("interface "));
            Assert.That(pipelineSource, Does.Contain("public RecordingDiagnosticsSettings DiagnosticsSettings"));
            Assert.That(
                recordingSettingSource,
                Does.Contain("fileManager.DiagnosticsSettings = CreateDiagnosticsSettings();"));
            Assert.That(
                recordingSettingSource,
                Does.Contain("ApplyDiagnosticsSettings(fileManager.DiagnosticsSettings);"));
            Assert.That(recordingSettingSource, Does.Not.Contain("fileManager.enableRecordingDiagnostics"));
            Assert.That(
                recordingSettingSource,
                Does.Not.Contain("fileManager.useDeterministicCaptureFramerateForDiagnostics"));
            Assert.That(recordingSettingSource, Does.Not.Contain("fileManager.enableDiagnosticFingerCloseups"));
            Assert.That(recordingSettingSource, Does.Not.Contain("fileManager.recordingCaptureQuality"));
            Assert.That(recordingSettingSource, Does.Not.Contain("fileManager.customRecordingCaptureWidth"));
            Assert.That(recordingSettingSource, Does.Not.Contain("fileManager.customRecordingCaptureHeight"));
        }

        [Test]
        public void Given_RecordingDiagnosticsSettings_When_AssignedToPipeline_Then_RoundTripsAllValues()
        {
            var pipelineObject = new GameObject("Recording Diagnostics Settings Pipeline Test");

            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                var expected = new RecordingDiagnosticsSettings(
                    enableRecordingDiagnostics: true,
                    useDeterministicCaptureFramerateForDiagnostics: true,
                    enableDiagnosticFingerCloseups: false,
                    captureQuality: RecordingCaptureQualityPreset.Custom,
                    customCaptureWidth: 2560,
                    customCaptureHeight: 1440);

                pipeline.DiagnosticsSettings = expected;
                RecordingDiagnosticsSettings actual = pipeline.DiagnosticsSettings;

                Assert.That(actual.EnableRecordingDiagnostics, Is.True);
                Assert.That(actual.UseDeterministicCaptureFramerateForDiagnostics, Is.True);
                Assert.That(actual.EnableDiagnosticFingerCloseups, Is.False);
                Assert.That(actual.CaptureQuality, Is.EqualTo(RecordingCaptureQualityPreset.Custom));
                Assert.That(actual.CustomCaptureWidth, Is.EqualTo(2560));
                Assert.That(actual.CustomCaptureHeight, Is.EqualTo(1440));

                RecordingCaptureResolutionPlan capturePlan = actual.CreateCaptureResolutionPlan();
                Assert.That(capturePlan.Width, Is.EqualTo(2560));
                Assert.That(capturePlan.Height, Is.EqualTo(1440));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_RecordingSetting_When_ApplyingDiagnostics_Then_TransfersAllValuesToPipeline()
        {
            var pipelineObject = new GameObject("Recording Diagnostics Pipeline Transfer Test");
            var settingObject = new GameObject("Recording Diagnostics Setting Transfer Test");

            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                var recordingSetting = settingObject.AddComponent<RecordingSetting>();
                SetField(recordingSetting, "recordingFBXVmdPipeline", pipeline);
                SetField(recordingSetting, "enableRecordingDiagnostics", true);
                SetField(recordingSetting, "useDeterministicCaptureFramerateForDiagnostics", true);
                SetField(recordingSetting, "enableDiagnosticFingerCloseups", false);
                SetField(recordingSetting, "recordingCaptureQuality", RecordingCaptureQualityPreset.Custom);
                SetField(recordingSetting, "customRecordingCaptureWidth", 3200);
                SetField(recordingSetting, "customRecordingCaptureHeight", 1800);

                recordingSetting.ApplyDiagnosticsToFBXVmdPipeline();
                RecordingDiagnosticsSettings actual = pipeline.DiagnosticsSettings;

                Assert.That(actual.EnableRecordingDiagnostics, Is.True);
                Assert.That(actual.UseDeterministicCaptureFramerateForDiagnostics, Is.True);
                Assert.That(actual.EnableDiagnosticFingerCloseups, Is.False);
                Assert.That(actual.CaptureQuality, Is.EqualTo(RecordingCaptureQualityPreset.Custom));
                Assert.That(actual.CustomCaptureWidth, Is.EqualTo(3200));
                Assert.That(actual.CustomCaptureHeight, Is.EqualTo(1800));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_MultipleRecordingSettings_When_OverridingFbxImportStarter_Then_KeepsInstanceIsolation()
        {
            var firstSettingObject = new GameObject("First RecordingSetting Import Starter Test");
            var secondSettingObject = new GameObject("Second RecordingSetting Import Starter Test");

            try
            {
                var firstSetting = firstSettingObject.AddComponent<RecordingSetting>();
                var secondSetting = secondSettingObject.AddComponent<RecordingSetting>();
                PropertyInfo starterProperty = typeof(RecordingSetting).GetProperty(
                    nameof(RecordingSetting.SharedSettingsFbxImportStarterForTests),
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
                Assert.That(starterProperty, Is.Not.Null);

                Func<FBXVmdPipeline, string, bool> firstStarter = (_, _) => true;
                Func<FBXVmdPipeline, string, bool> secondStarter = (_, _) => false;
                starterProperty.SetValue(firstSetting, firstStarter);
                starterProperty.SetValue(secondSetting, secondStarter);

                Assert.That(starterProperty.GetValue(firstSetting), Is.SameAs(firstStarter));
                Assert.That(starterProperty.GetValue(secondSetting), Is.SameAs(secondStarter));
                Assert.That(starterProperty.GetMethod.IsStatic, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstSettingObject);
                UnityEngine.Object.DestroyImmediate(secondSettingObject);
            }
        }

        [Test]
        public void Given_MissingFBXVmdPipeline_When_ApplyingSharedSettingsWithFbxPath_Then_ReturnsUserMessage()
        {
            var document = new MainRecordingSettingsDocument
            {
                fbxPath = "D:/motion/sample.fbx",
                captureWidth = 1920,
                captureHeight = 1080,
            };

            MainRecordingSettingsActionResult result =
                MainRecordingSettingsActions.ApplySharedSettings(document, null, null, false);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.UserMessage, Does.Contain("FBX"));
        }

        [Test]
        public void Given_RecodingSetting_When_LoadingSharedSettings_Then_AppliesCaptureAndPopupSettings()
        {
            string path = CreateTempSettingsPath();
            var store = new MainRecordingSettingsStore(path);
            store.Save(new MainRecordingSettingsDocument
            {
                captureWidth = 1280,
                captureHeight = 720,
                openSettingsOnStart = false,
            });

            var fileManagerObject = new GameObject("Shared Settings FBXVmdPipeline Test");
            var settingObject = new GameObject("Shared Settings RecordingSetting Test");

            try
            {
                var fileManager = fileManagerObject.AddComponent<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();
                var recodingSetting = settingObject.AddComponent<RecordingSetting>();
                SetField(recodingSetting, "recordingFBXVmdPipeline", fileManager);

                MainRecordingSettingsActionResult result =
                    recodingSetting.LoadSharedSettingsFromPathForTests(path);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(GetField<bool>(recodingSetting, "openSettingsPopupOnStart"), Is.False);
                Assert.That(GetField<object>(recodingSetting, "recordingCaptureQuality").ToString(), Is.EqualTo("Custom"));
                Assert.That(GetField<int>(recodingSetting, "customRecordingCaptureWidth"), Is.EqualTo(1280));
                Assert.That(GetField<int>(recodingSetting, "customRecordingCaptureHeight"), Is.EqualTo(720));
                Assert.That(fileManager.recordingCaptureQuality, Is.EqualTo(RecordingCaptureQualityPreset.Custom));
                Assert.That(fileManager.customRecordingCaptureWidth, Is.EqualTo(1280));
                Assert.That(fileManager.customRecordingCaptureHeight, Is.EqualTo(720));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(fileManagerObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_ApplyingSharedSettingsWithMissingFbxFile_Then_ReturnsFailure()
        {
            string missingFbxPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.fbx");
            var fileManagerObject = new GameObject("Shared Settings Missing FBX FBXVmdPipeline Test");
            var settingObject = new GameObject("Shared Settings Missing FBX RecordingSetting Test");

            try
            {
                var fileManager = fileManagerObject.AddComponent<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();
                var recodingSetting = settingObject.AddComponent<RecordingSetting>();
                SetField(recodingSetting, "recordingFBXVmdPipeline", fileManager);

                var document = new MainRecordingSettingsDocument
                {
                    fbxPath = missingFbxPath,
                    captureWidth = 1920,
                    captureHeight = 1080,
                };
                SetImportCommand(document, "cmd-missing-fbx", missingFbxPath);

                MainRecordingSettingsActionResult result =
                    recodingSetting.ApplySharedSettingsDocument(document, fileManager);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.UserMessage, Does.Contain("FBX"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(fileManagerObject);
            }
        }

        [Test]
        public void Given_RecordingSetting_When_ApplyingDocumentWithoutImportCommand_Then_DoesNotStartImport()
        {
            var fileManagerObject = new GameObject("Shared Settings Duplicate FBX FBXVmdPipeline Test");
            var settingObject = new GameObject("Shared Settings Duplicate FBX RecordingSetting Test");

            try
            {
                var fileManager = fileManagerObject.AddComponent<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();
                var recodingSetting = settingObject.AddComponent<RecordingSetting>();
                SetField(recodingSetting, "recordingFBXVmdPipeline", fileManager);
                int startCount = 0;
                recodingSetting.SharedSettingsFbxImportStarterForTests = (_, _) =>
                {
                    startCount++;
                    return true;
                };

                var document = new MainRecordingSettingsDocument
                {
                    fbxPath = "D:/motion/stored-only.fbx",
                    captureWidth = 1920,
                    captureHeight = 1080,
                };

                MainRecordingSettingsActionResult result =
                    recodingSetting.ApplySharedSettingsDocument(document, fileManager);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(startCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(fileManagerObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_ApplyingExistingNewSharedFbxPath_Then_StartsImportOnceAndSkipsDuplicate()
        {
            string tempFbxPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.fbx");
            var fileManagerObject = new GameObject("Shared Settings Positive FBX FBXVmdPipeline Test");
            var settingObject = new GameObject("Shared Settings Positive FBX RecordingSetting Test");
            try
            {
                File.WriteAllBytes(tempFbxPath, Array.Empty<byte>());

                var fileManager = fileManagerObject.AddComponent<FBXVmdPipeline>();
                var recodingSetting = settingObject.AddComponent<RecordingSetting>();
                SetField(recodingSetting, "recordingFBXVmdPipeline", fileManager);

                int startCount = 0;
                FBXVmdPipeline startedManager = null;
                string startedPath = string.Empty;
                recodingSetting.SharedSettingsFbxImportStarterForTests = (manager, path) =>
                {
                    startCount++;
                    startedManager = manager;
                    startedPath = path;
                    return true;
                };

                var document = new MainRecordingSettingsDocument
                {
                    fbxPath = tempFbxPath,
                    captureWidth = 1920,
                    captureHeight = 1080,
                };
                SetImportCommand(document, "cmd-1", tempFbxPath);

                MainRecordingSettingsActionResult firstResult =
                    recodingSetting.ApplySharedSettingsDocument(document, fileManager);
                SetImportCommand(document, "cmd-1", tempFbxPath);
                MainRecordingSettingsActionResult secondResult =
                    recodingSetting.ApplySharedSettingsDocument(document, fileManager);

                Assert.That(firstResult.Succeeded, Is.True);
                Assert.That(secondResult.Succeeded, Is.True);
                Assert.That(startCount, Is.EqualTo(1));
                Assert.That(startedManager, Is.SameAs(fileManager));
                Assert.That(startedPath, Is.EqualTo(tempFbxPath));
                Assert.That(secondResult.UserMessage, Does.Contain("이미 처리"));
            }
            finally
            {
                if (File.Exists(tempFbxPath))
                {
                    File.Delete(tempFbxPath);
                }

                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(fileManagerObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_ImportCommandIdChangesForSameFbx_Then_StartsImportAgain()
        {
            string tempFbxPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.fbx");
            var fileManagerObject = new GameObject("Shared Settings Command FBX FBXVmdPipeline Test");
            var settingObject = new GameObject("Shared Settings Command FBX RecordingSetting Test");
            try
            {
                File.WriteAllBytes(tempFbxPath, Array.Empty<byte>());

                var fileManager = fileManagerObject.AddComponent<FBXVmdPipeline>();
                var recodingSetting = settingObject.AddComponent<RecordingSetting>();
                SetField(recodingSetting, "recordingFBXVmdPipeline", fileManager);

                int startCount = 0;
                recodingSetting.SharedSettingsFbxImportStarterForTests = (manager, path) =>
                {
                    startCount++;
                    return manager == fileManager && path == tempFbxPath;
                };

                var document = new MainRecordingSettingsDocument
                {
                    fbxPath = tempFbxPath,
                    captureWidth = 1920,
                    captureHeight = 1080,
                };

                SetImportCommand(document, "cmd-1", tempFbxPath);
                MainRecordingSettingsActionResult firstResult =
                    recodingSetting.ApplySharedSettingsDocument(document, fileManager);

                SetImportCommand(document, "cmd-2", tempFbxPath);
                MainRecordingSettingsActionResult secondResult =
                    recodingSetting.ApplySharedSettingsDocument(document, fileManager);
                SetImportCommand(document, "cmd-2", tempFbxPath);
                MainRecordingSettingsActionResult duplicateResult =
                    recodingSetting.ApplySharedSettingsDocument(document, fileManager);

                Assert.That(firstResult.Succeeded, Is.True);
                Assert.That(secondResult.Succeeded, Is.True);
                Assert.That(duplicateResult.Succeeded, Is.True);
                Assert.That(startCount, Is.EqualTo(2));
                Assert.That(duplicateResult.UserMessage, Does.Contain("이미 처리"));
            }
            finally
            {
                if (File.Exists(tempFbxPath))
                {
                    File.Delete(tempFbxPath);
                }

                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(fileManagerObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_LoadingStoredFbxPathWithoutImportCommand_Then_DoesNotStartImport()
        {
            string tempFbxPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.fbx");
            string settingsPath = CreateTempSettingsPath();
            var store = new MainRecordingSettingsStore(settingsPath);
            var fileManagerObject = new GameObject("Stored FBX Path FBXVmdPipeline Test");
            var settingObject = new GameObject("Stored FBX Path RecordingSetting Test");
            try
            {
                File.WriteAllBytes(tempFbxPath, Array.Empty<byte>());
                store.Save(new MainRecordingSettingsDocument
                {
                    fbxPath = tempFbxPath,
                    captureWidth = 1920,
                    captureHeight = 1080,
                });

                var fileManager = fileManagerObject.AddComponent<FBXVmdPipeline>();
                var recodingSetting = settingObject.AddComponent<RecordingSetting>();
                SetField(recodingSetting, "recordingFBXVmdPipeline", fileManager);

                int startCount = 0;
                recodingSetting.SharedSettingsFbxImportStarterForTests = (_, _) =>
                {
                    startCount++;
                    return true;
                };

                MainRecordingSettingsActionResult result =
                    recodingSetting.LoadSharedSettingsFromPathForTests(settingsPath);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(startCount, Is.EqualTo(0));
            }
            finally
            {
                if (File.Exists(tempFbxPath))
                {
                    File.Delete(tempFbxPath);
                }

                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(fileManagerObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_LoadingStaleImportCommandOnPlayStart_Then_ClearsCommandWithoutStartingImport()
        {
            string tempFbxPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.fbx");
            string settingsPath = CreateTempSettingsPath();
            var store = new MainRecordingSettingsStore(settingsPath);
            var fileManagerObject = new GameObject("Stale Import Command FBXVmdPipeline Test");
            var settingObject = new GameObject("Stale Import Command RecordingSetting Test");
            try
            {
                File.WriteAllBytes(tempFbxPath, Array.Empty<byte>());
                var document = new MainRecordingSettingsDocument
                {
                    fbxPath = tempFbxPath,
                    captureWidth = 1920,
                    captureHeight = 1080,
                };
                SetImportCommand(document, "cmd-stale-on-play-start", tempFbxPath);
                store.Save(document);

                var fileManager = fileManagerObject.AddComponent<FBXVmdPipeline>();
                var recodingSetting = settingObject.AddComponent<RecordingSetting>();
                SetField(recodingSetting, "recordingFBXVmdPipeline", fileManager);

                int startCount = 0;
                recodingSetting.SharedSettingsFbxImportStarterForTests = (_, _) =>
                {
                    startCount++;
                    return true;
                };

                MainRecordingSettingsActionResult result =
                    recodingSetting.LoadSharedSettingsFromPathForTests(settingsPath);
                MainRecordingSettingsDocument loadedDocument = store.LoadOrCreateDefault();
                object loadedCommand = GetField<MainRecordingSettingsCommandEnvelope>(
                    loadedDocument,
                    "pendingCommand");

                Assert.That(result.Succeeded, Is.True);
                Assert.That(startCount, Is.EqualTo(0),
                    "Play start must not replay a command that was already present in the settings file.");
                Assert.That(GetField<string>(loadedCommand, "commandId"), Is.EqualTo(string.Empty));
                Assert.That(GetField<string>(loadedCommand, "action"), Is.EqualTo(string.Empty));
                Assert.That(GetField<string>(loadedCommand, "fbxPath"), Is.EqualTo(string.Empty));
            }
            finally
            {
                if (File.Exists(tempFbxPath))
                {
                    File.Delete(tempFbxPath);
                }

                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(fileManagerObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_ImportCommandHasEmptyPath_Then_DoesNotFallbackToStoredFbxPath()
        {
            string tempFbxPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.fbx");
            var fileManagerObject = new GameObject("Empty Command Path FBXVmdPipeline Test");
            var settingObject = new GameObject("Empty Command Path RecordingSetting Test");
            try
            {
                File.WriteAllBytes(tempFbxPath, Array.Empty<byte>());

                var fileManager = fileManagerObject.AddComponent<FBXVmdPipeline>();
                var recodingSetting = settingObject.AddComponent<RecordingSetting>();
                SetField(recodingSetting, "recordingFBXVmdPipeline", fileManager);

                int startCount = 0;
                recodingSetting.SharedSettingsFbxImportStarterForTests = (_, _) =>
                {
                    startCount++;
                    return true;
                };

                var document = new MainRecordingSettingsDocument
                {
                    fbxPath = tempFbxPath,
                    captureWidth = 1920,
                    captureHeight = 1080,
                };
                SetImportCommand(document, "cmd-empty-path", string.Empty);

                MainRecordingSettingsActionResult result =
                    recodingSetting.ApplySharedSettingsDocument(document, fileManager);
                object consumedCommand = GetField<MainRecordingSettingsCommandEnvelope>(
                    document,
                    "pendingCommand");

                Assert.That(result.Succeeded, Is.False);
                Assert.That(startCount, Is.EqualTo(0),
                    "An import command must carry its own FBX path and must not reuse a stored document path.");
                Assert.That(GetField<string>(consumedCommand, "commandId"), Is.EqualTo(string.Empty));
                Assert.That(GetField<string>(consumedCommand, "action"), Is.EqualTo(string.Empty));
                Assert.That(GetField<string>(consumedCommand, "fbxPath"), Is.EqualTo(string.Empty));
            }
            finally
            {
                if (File.Exists(tempFbxPath))
                {
                    File.Delete(tempFbxPath);
                }

                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(fileManagerObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_ImportCommandIsConsumed_Then_ClearsPendingCommandBeforeNextPlay()
        {
            string tempFbxPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.fbx");
            string settingsPath = CreateTempSettingsPath();
            var store = new MainRecordingSettingsStore(settingsPath);
            var firstFBXVmdPipelineObject = new GameObject("Consumed Command First FBXVmdPipeline Test");
            var firstSettingObject = new GameObject("Consumed Command First RecordingSetting Test");
            var secondFBXVmdPipelineObject = new GameObject("Consumed Command Second FBXVmdPipeline Test");
            var secondSettingObject = new GameObject("Consumed Command Second RecordingSetting Test");
            try
            {
                File.WriteAllBytes(tempFbxPath, Array.Empty<byte>());
                store.Save(new MainRecordingSettingsDocument
                {
                    fbxPath = tempFbxPath,
                    captureWidth = 1920,
                    captureHeight = 1080,
                });

                int startCount = 0;
                var firstFBXVmdPipeline = firstFBXVmdPipelineObject.AddComponent<FBXVmdPipeline>();
                var firstRecodingSetting = firstSettingObject.AddComponent<RecordingSetting>();
                SetField(firstRecodingSetting, "recordingFBXVmdPipeline", firstFBXVmdPipeline);
                firstRecodingSetting.SharedSettingsFbxImportStarterForTests = (_, path) =>
                {
                    startCount++;
                    return path == tempFbxPath;
                };

                MainRecordingSettingsActionResult initialResult =
                    firstRecodingSetting.LoadSharedSettingsFromPathForTests(settingsPath);
                Assert.That(initialResult.Succeeded, Is.True);
                Assert.That(startCount, Is.EqualTo(0));

                var document = new MainRecordingSettingsDocument
                {
                    fbxPath = tempFbxPath,
                    captureWidth = 1920,
                    captureHeight = 1080,
                };
                SetImportCommand(document, "cmd-stale-guard", tempFbxPath);
                store.Save(document);
                File.SetLastWriteTimeUtc(settingsPath, DateTime.UtcNow.AddMinutes(1));

                MainRecordingSettingsActionResult firstResult =
                    firstRecodingSetting.PollSharedSettingsIfChanged();
                MainRecordingSettingsDocument consumedDocument = store.LoadOrCreateDefault();
                object consumedCommand = GetField<MainRecordingSettingsCommandEnvelope>(
                    consumedDocument,
                    "pendingCommand");

                Assert.That(firstResult.Succeeded, Is.True);
                Assert.That(startCount, Is.EqualTo(1));
                Assert.That(GetField<string>(consumedCommand, "commandId"), Is.EqualTo(string.Empty));
                Assert.That(GetField<string>(consumedCommand, "action"), Is.EqualTo(string.Empty));
                Assert.That(GetField<string>(consumedCommand, "fbxPath"), Is.EqualTo(string.Empty));

                var secondFBXVmdPipeline = secondFBXVmdPipelineObject.AddComponent<FBXVmdPipeline>();
                var secondRecodingSetting = secondSettingObject.AddComponent<RecordingSetting>();
                SetField(secondRecodingSetting, "recordingFBXVmdPipeline", secondFBXVmdPipeline);

                MainRecordingSettingsActionResult secondResult =
                    secondRecodingSetting.LoadSharedSettingsFromPathForTests(settingsPath);

                Assert.That(secondResult.Succeeded, Is.True);
                Assert.That(startCount, Is.EqualTo(1));
            }
            finally
            {
                if (File.Exists(tempFbxPath))
                {
                    File.Delete(tempFbxPath);
                }

                UnityEngine.Object.DestroyImmediate(firstSettingObject);
                UnityEngine.Object.DestroyImmediate(firstFBXVmdPipelineObject);
                UnityEngine.Object.DestroyImmediate(secondSettingObject);
                UnityEngine.Object.DestroyImmediate(secondFBXVmdPipelineObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_SettingsFileChanges_Then_PollingAppliesUpdatedDocument()
        {
            string path = CreateTempSettingsPath();
            var store = new MainRecordingSettingsStore(path);
            store.Save(new MainRecordingSettingsDocument
            {
                captureWidth = 1280,
                captureHeight = 720,
                openSettingsOnStart = false,
            });

            var settingObject = new GameObject("Shared Settings Polling RecordingSetting Test");

            try
            {
                var recodingSetting = settingObject.AddComponent<RecordingSetting>();
                recodingSetting.LoadSharedSettingsFromPathForTests(path);

                store.Save(new MainRecordingSettingsDocument
                {
                    captureWidth = 2560,
                    captureHeight = 1440,
                    openSettingsOnStart = true,
                });
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

                MainRecordingSettingsActionResult result = recodingSetting.PollSharedSettingsIfChanged();

                Assert.That(result.Succeeded, Is.True);
                Assert.That(GetField<bool>(recodingSetting, "openSettingsPopupOnStart"), Is.True);
                Assert.That(GetField<int>(recodingSetting, "customRecordingCaptureWidth"), Is.EqualTo(2560));
                Assert.That(GetField<int>(recodingSetting, "customRecordingCaptureHeight"), Is.EqualTo(1440));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_WritingRuntimePlayModeState_Then_SharedSettingsFileReflectsPlayState()
        {
            string path = CreateTempSettingsPath();
            var store = new MainRecordingSettingsStore(path);
            store.Save(new MainRecordingSettingsDocument
            {
                fbxPath = "D:/motions/satisfaction_2.fbx",
                captureWidth = 2560,
                captureHeight = 1440,
                openSettingsOnStart = false,
            });

            var settingObject = new GameObject("Runtime State Writer RecordingSetting Test");

            try
            {
                var recodingSetting = settingObject.AddComponent<RecordingSetting>();
                recodingSetting.LoadSharedSettingsFromPathForTests(path);

                MainRecordingSettingsActionResult playingResult =
                    InvokeInstance<MainRecordingSettingsActionResult>(
                        recodingSetting,
                        "WriteRuntimePlayModeStateForTests",
                        "playing");
                MainRecordingSettingsDocument playingDocument = store.LoadOrCreateDefault();
                object playingState = GetField<object>(playingDocument, "runtimeState");

                Assert.That(playingResult.Succeeded, Is.True);
                Assert.That(GetField<string>(playingState, "playMode"), Is.EqualTo("playing"));
                Assert.That(GetField<string>(playingState, "updatedAtUtc"), Is.Not.Empty);
                Assert.That(playingDocument.fbxPath, Is.EqualTo("D:/motions/satisfaction_2.fbx"));
                Assert.That(playingDocument.captureWidth, Is.EqualTo(2560));
                Assert.That(playingDocument.captureHeight, Is.EqualTo(1440));
                Assert.That(playingDocument.openSettingsOnStart, Is.False);

                MainRecordingSettingsActionResult stoppedResult =
                    InvokeInstance<MainRecordingSettingsActionResult>(
                        recodingSetting,
                        "WriteRuntimePlayModeStateForTests",
                        "stopped");
                MainRecordingSettingsDocument stoppedDocument = store.LoadOrCreateDefault();
                object stoppedState = GetField<object>(stoppedDocument, "runtimeState");

                Assert.That(stoppedResult.Succeeded, Is.True);
                Assert.That(GetField<string>(stoppedState, "playMode"), Is.EqualTo("stopped"));
                Assert.That(GetField<string>(stoppedState, "updatedAtUtc"), Is.Not.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_CreateEditor_Then_UsesRecordingInspector()
        {
            var settingObject = new GameObject("Recoding Setting Editor Test");

            try
            {
                var recodingSetting = settingObject.AddComponent<RecordingSetting>();
                UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(recodingSetting);

                try
                {
                    Assert.That(editor.GetType().FullName,
                        Is.EqualTo("Fbx2Vmd.Settings.EditorTools.RecordingSettingEditor"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_BackgroundColorSetting_When_CreateEditor_Then_UsesKoreanInspector()
        {
            var settingObject = new GameObject("Background Color Setting Editor Test");

            try
            {
                var backgroundColorSetting = settingObject.AddComponent<BackgroundColorSetting>();
                UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(backgroundColorSetting);

                try
                {
                    Assert.That(editor.GetType().FullName,
                        Is.EqualTo("Fbx2Vmd.Settings.EditorTools.BackgroundColorSettingEditor"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_EditorDrawUtility_When_InspectingOwnership_Then_UsesInspectorEditorBoundary()
        {
            const string sourcePath =
                "Assets/_Project/Scripts/Editor/Inspector/EditorDrawUtility.cs";
            const string legacySourcePath =
                "Assets/_Project/Scripts/Editor/Settings/EditorDrawUtility.cs";
            Type utilityType = Type.GetType(
                "Fbx2Vmd.EditorTools.EditorDrawUtility, Assembly-CSharp-Editor");

            Assert.That(File.Exists(sourcePath), Is.True, sourcePath);
            Assert.That(File.Exists(legacySourcePath), Is.False, legacySourcePath);
            Assert.That(utilityType, Is.Not.Null);
            Assert.That(utilityType.Assembly.GetName().Name, Is.EqualTo("Assembly-CSharp-Editor"));
            Assert.That(
                Type.GetType("Fbx2Vmd.Settings.EditorTools.EditorDrawUtility, Assembly-CSharp-Editor"),
                Is.Null);
        }

        [Test]
        public void Given_SettingFields_When_InspectingAttributes_Then_UsesKoreanLabels()
        {
            AssertHeader<BackgroundColorSetting>("targetCamera", "대상");
            AssertInspectorName<BackgroundColorSetting>("targetCamera", "대상 카메라");
            AssertHeader<BackgroundColorSetting>("applyOnAwake", "적용");
            AssertInspectorName<BackgroundColorSetting>("applyOnAwake", "실행 시작 시 자동 적용");
            AssertInspectorName<BackgroundColorSetting>("applyOnValidate", "Unity OnValidate 자동 적용");
            AssertHeader<BackgroundColorSetting>("applyBackgroundColor", "카메라 배경");
            AssertInspectorName<BackgroundColorSetting>("applyBackgroundColor", "배경색 적용");
            AssertInspectorName<BackgroundColorSetting>("backgroundColor", "배경색");

            AssertHeader<RecordingSetting>("recordingFBXVmdPipeline", "수동 녹화");
            AssertInspectorName<RecordingSetting>("recordingFBXVmdPipeline", "녹화 FBXVmdPipeline");
            AssertInspectorName<RecordingSetting>("manualRecordButton", "수동 녹화 버튼");
            AssertInspectorName<RecordingSetting>("recordingController", "녹화 대상");
            AssertHeader<RecordingSetting>("enableRecordingDiagnostics", "화면 녹화 진단");
            AssertInspectorName<RecordingSetting>("enableRecordingDiagnostics", "녹화 진단/캡처 사용");
            AssertInspectorName<RecordingSetting>(
                "useDeterministicCaptureFramerateForDiagnostics",
                "테스트용 30fps 시간 고정");
            AssertInspectorName<RecordingSetting>("enableDiagnosticFingerCloseups", "손 close-up 캡처");
            AssertInspectorName<RecordingSetting>("applyDiagnosticsToFBXVmdPipelineOnAwake", "실행 시작 시 FBXVmdPipeline에 적용");
            AssertHeader<RecordingSetting>("settingsPopup", "설정 팝업");
            AssertInspectorName<RecordingSetting>("settingsPopup", "런타임 설정 팝업");
            AssertInspectorName<RecordingSetting>("openSettingsPopupOnStart", "시작 시 설정 팝업 열기");
        }

        [Test]
        public void Given_PublicSettingsLaunchPolicies_When_InspectingTestSurface_Then_HaveNoForwardingWrappers()
        {
            Type runtimeLauncherType = RequireType(RuntimeLauncherTypeName);
            Type bootstrapType = RequireType(RuntimeBootstrapTypeName);
            Type editorLauncherType = RequireType(SettingsLauncherTypeName);
            const BindingFlags StaticMembers =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            Assert.That(runtimeLauncherType.GetMethod("CreateLaunchPlanForTests", StaticMembers), Is.Null);
            Assert.That(runtimeLauncherType.GetMethod("ShouldAutoLaunchForPlayerForTests", StaticMembers), Is.Null);
            Assert.That(runtimeLauncherType.GetMethod("ShouldOpenGameViewPopupFallbackForTests", StaticMembers), Is.Null);
            Assert.That(bootstrapType.GetMethod("ShouldAutoLaunchOnPlayerStartupForTests", StaticMembers), Is.Null);
            Assert.That(editorLauncherType.GetMethod("GetMenuPathForTests", StaticMembers), Is.Null);
            Assert.That(editorLauncherType.GetMethod("GetEditorSurfacePolicyForTests", StaticMembers), Is.Null);
            Assert.That(editorLauncherType.GetMethod("CreateDefaultLaunchPlanForTests", StaticMembers), Is.Null);

            const string bootstrapSourcePath =
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsBootstrap.cs";
            string bootstrapSource = File.ReadAllText(bootstrapSourcePath);
            Assert.That(
                bootstrapSource,
                Does.Contain("MainRecordingSettingsLauncher.ShouldAutoLaunchForPlayer("));
            Assert.That(bootstrapSource, Does.Not.Contain("ShouldAutoLaunchOnPlayerStartup("));
        }

        [Test]
        public void Given_PublicSettingsOperations_When_InspectingTestSurface_Then_HaveNoForwardingWrappers()
        {
            Type companionControllerType = RequireType(CompanionControllerTypeName);
            Type runtimeLauncherType = RequireType(RuntimeLauncherTypeName);
            const BindingFlags InstanceMembers =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            const BindingFlags StaticMembers =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            Assert.That(
                typeof(MainRecordingSettingsActions).GetMethod("ApplyForTests", StaticMembers),
                Is.Null);
            Assert.That(
                typeof(RecordingSetting).GetMethod("PollSharedSettingsForTests", InstanceMembers),
                Is.Null);
            Assert.That(
                companionControllerType.GetMethod("SaveCurrentDocumentForTests", InstanceMembers),
                Is.Null);
            Assert.That(
                companionControllerType.GetMethod("GetStatusMessageForTests", InstanceMembers),
                Is.Null);
            Assert.That(
                runtimeLauncherType.GetMethod("CreateProcessStartInfoForTests", StaticMembers),
                Is.Null);
        }

        [Test]
        public void Given_SettingsLauncherType_When_InspectingMetadata_Then_UsesElectronCompanionForMainRecording()
        {
            Type launcherType = RequireType(SettingsLauncherTypeName);

            Assert.That(typeof(EditorWindow).IsAssignableFrom(launcherType), Is.False);
            Assert.That(GetStaticMemberValue<string>(launcherType, "MenuPath"),
                Is.EqualTo("Tools/Graphics/Open Main_recording Settings"));
            Assert.That(
                InvokeStatic<string>(launcherType, "GetMainRecordingScenePathForTests"),
                Is.EqualTo(MainRecordingScenePath));
            Assert.That(File.Exists(MainRecordingScenePath), Is.True);
            Assert.That(InvokeStatic<bool>(launcherType, "ShouldOpenForScene", MainRecordingScenePath), Is.True);
            Assert.That(InvokeStatic<bool>(launcherType, "ShouldOpenForScene", MainAutoScenePath), Is.False);
            Assert.That(MainRecordingSettingsSurfacePolicy.EditorSurfacePolicy, Does.Contain("Electron"));
            Assert.That(MainRecordingSettingsSurfacePolicy.EditorSurfacePolicy, Does.Contain("Web UI"));
            Assert.That(InvokeStatic<bool>(launcherType, "CanLaunchWebSettingsForTests"), Is.True);
        }

        [Test]
        public void Given_SettingsLauncher_When_InspectingLaunchPlan_Then_UsesElectronAssetsAndSharedSettings()
        {
            Type launcherType = RequireType(SettingsLauncherTypeName);

            object plan = InvokeStatic<object>(launcherType, "CreateDefaultLaunchPlan");

            Assert.That(GetMemberValue<string>(plan, "WorkingDirectory"),
                Is.EqualTo("Assets/_Project/Tools/MainRecordingSettings"));
            Assert.That(GetMemberValue<string>(plan, "ExecutableName"), Is.EqualTo("npm"));
            Assert.That(GetMemberValue<string>(plan, "Arguments"), Is.EqualTo("run start:prod"));
            Assert.That(GetMemberValue<string>(plan, "SettingsPath"),
                Does.EndWith("main-recording-settings.json"));
        }

        [Test]
        public void Given_PlayerExecutableDirectory_When_CreatingSettingsLaunchPlan_Then_UsesPackagedSettingsExe()
        {
            MainRecordingSettingsLaunchPlan plan = MainRecordingSettingsLauncher.CreateLaunchPlan(
                "D:/Builds/Local/MainRecordingRelease",
                "D:/Data/main-recording-settings.json");

            Assert.That(
                GetMemberValue<string>(plan, "ExecutablePath").Replace('\\', '/'),
                Is.EqualTo("D:/Builds/Local/MainRecordingRelease/MainRecordingSettings/Unity_Fbx2VMD_Settings.exe"));
            Assert.That(
                GetMemberValue<string>(plan, "WorkingDirectory").Replace('\\', '/'),
                Is.EqualTo("D:/Builds/Local/MainRecordingRelease/MainRecordingSettings"));
            Assert.That(
                GetMemberValue<string>(plan, "SettingsPath").Replace('\\', '/'),
                Is.EqualTo("D:/Data/main-recording-settings.json"));
            Assert.That(GetMemberValue<string>(plan, "Arguments"), Does.Contain("--settings-path"));
            Assert.That(GetMemberValue<string>(plan, "Arguments"), Does.Contain("main-recording-settings.json"));
        }

        [Test]
        public void Given_PlayerLaunchPlan_When_CreatingProcessStartInfo_Then_DoesNotInheritElectronRunAsNode()
        {
            Type runtimeLauncherType = RequireType(RuntimeLauncherTypeName);
            string originalElectronRunAsNode = Environment.GetEnvironmentVariable(
                "ELECTRON_RUN_AS_NODE",
                EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", "1", EnvironmentVariableTarget.Process);

            try
            {
                MainRecordingSettingsLaunchPlan plan = MainRecordingSettingsLauncher.CreateLaunchPlan(
                    "D:/Builds/Local/MainRecordingRelease",
                    "D:/Data/main-recording-settings.json");

                var startInfo = InvokeStatic<ProcessStartInfo>(
                    runtimeLauncherType,
                    "CreateProcessStartInfo",
                    plan);

                Assert.That(startInfo.Environment.ContainsKey("ELECTRON_RUN_AS_NODE"), Is.False);
                Assert.That(
                    startInfo.Environment[MainRecordingSettingsPathResolver.EnvironmentVariableName].Replace('\\', '/'),
                    Is.EqualTo("D:/Data/main-recording-settings.json"));
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "ELECTRON_RUN_AS_NODE",
                    originalElectronRunAsNode,
                    EnvironmentVariableTarget.Process);
            }
        }

        [Test]
        public void Given_PlayerRuntimePolicy_When_CheckingAutoLaunch_Then_UsesExternalSettingsOnlyForNonBatchPlayer()
        {
            Assert.That(
                MainRecordingSettingsLauncher.ShouldAutoLaunchForPlayer(true, false, false),
                Is.True);
            Assert.That(
                MainRecordingSettingsLauncher.ShouldAutoLaunchForPlayer(true, true, false),
                Is.False);
            Assert.That(
                MainRecordingSettingsLauncher.ShouldAutoLaunchForPlayer(true, false, true),
                Is.False);
            Assert.That(
                MainRecordingSettingsLauncher.ShouldAutoLaunchForPlayer(false, false, false),
                Is.False);
        }

        [Test]
        public void Given_PlayerSettingsStartup_When_InspectingOwnership_Then_BootstrapOwnsLaunchAndRecordingSettingOwnsFallback()
        {
            const string bootstrapSourcePath =
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsBootstrap.cs";
            const string recordingSettingSourcePath =
                "Assets/_Project/Scripts/Settings/RecordingSetting.cs";

            string bootstrapSource = File.ReadAllText(bootstrapSourcePath);
            string recordingSettingSource = File.ReadAllText(recordingSettingSourcePath);

            Assert.That(
                bootstrapSource,
                Does.Contain("MainRecordingSettingsLauncher.TryLaunchForPlayer("));
            Assert.That(
                recordingSettingSource,
                Does.Not.Contain("MainRecordingSettingsLauncher.ShouldAutoLaunchForPlayer("));
            Assert.That(
                recordingSettingSource,
                Does.Not.Contain("MainRecordingSettingsLauncher.TryLaunchForPlayer("));
            Assert.That(
                recordingSettingSource,
                Does.Contain("MainRecordingSettingsLauncher.IsSettingsProcessRunning()"));
            Assert.That(
                recordingSettingSource,
                Does.Contain("MainRecordingSettingsSurfacePolicy.ShouldOpenRuntimePopupFallback("));
        }

        [Test]
        public void Given_PlayerRuntimeLaunchResult_When_LaunchSucceeds_Then_GameViewPopupStaysFallbackOnly()
        {
            Assert.That(
                MainRecordingSettingsSurfacePolicy.ShouldOpenRuntimePopupFallback(true, false, false, true),
                Is.False);
            Assert.That(
                MainRecordingSettingsSurfacePolicy.ShouldOpenRuntimePopupFallback(true, false, false, false),
                Is.True);
            Assert.That(
                MainRecordingSettingsSurfacePolicy.ShouldOpenRuntimePopupFallback(true, true, false, false),
                Is.False);
        }

        [Test]
        public void Given_PlayerStartup_When_FirstBuildSceneHasNoRecodingSetting_Then_RuntimeBootstrapRunsBeforeSceneLoad()
        {
            Type bootstrapType = RequireType(RuntimeBootstrapTypeName);
            MethodInfo method = bootstrapType.GetMethod(
                "AutoLaunchForPlayerStartup",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] attributes = method.GetCustomAttributes(
                typeof(RuntimeInitializeOnLoadMethodAttribute),
                false);
            Assert.That(attributes.Length, Is.EqualTo(1));
            var attribute = (RuntimeInitializeOnLoadMethodAttribute)attributes[0];
            Assert.That(attribute.loadType, Is.EqualTo(RuntimeInitializeLoadType.BeforeSceneLoad));

            Assert.That(
                MainRecordingSettingsLauncher.ShouldAutoLaunchForPlayer(true, false, false),
                Is.True);
            Assert.That(
                MainRecordingSettingsLauncher.ShouldAutoLaunchForPlayer(true, true, false),
                Is.False);
            Assert.That(
                MainRecordingSettingsLauncher.ShouldAutoLaunchForPlayer(true, false, true),
                Is.False);

            EditorSceneManager.OpenScene(MainAutoScenePath);
            Assert.That(
                UnityEngine.Object.FindObjectOfType<RecordingSetting>(),
                Is.Null,
                "The first Player scene is Main_Auto, so Player startup settings launch must not depend on RecordingSetting.Start().");
        }

        [Test]
        public void Given_RuntimeSettingsProcessAlreadyStarted_When_TryLaunchRunsAgain_Then_SkipsDuplicateProcess()
        {
            Type runtimeLauncherType = RequireType(RuntimeLauncherTypeName);
            const BindingFlags StaticMembers =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] mutableStaticDelegates = Array.FindAll(
                runtimeLauncherType.GetFields(StaticMembers),
                field => typeof(Delegate).IsAssignableFrom(field.FieldType) && !field.IsInitOnly);

            Assert.That(mutableStaticDelegates, Is.Empty);
            Assert.That(runtimeLauncherType.GetMethod("SetLaunchProcessForTests", StaticMembers), Is.Null);
            Assert.That(runtimeLauncherType.GetMethod("ResetLaunchProcessForTests", StaticMembers), Is.Null);
            SetStaticField(runtimeLauncherType, "startedProcess", Process.GetCurrentProcess());

            try
            {
                MainRecordingSettingsActionResult result =
                    InvokeStatic<MainRecordingSettingsActionResult>(
                        runtimeLauncherType,
                        "TryLaunch",
                        "D:/Missing/MainRecordingRelease",
                        "D:/Data/main-recording-settings.json");

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.UserMessage, Does.Contain("이미 실행 중"));
            }
            finally
            {
                SetStaticField(runtimeLauncherType, "startedProcess", null);
            }
        }

        [Test]
        public void Given_MainRecordingScene_When_EnteringEditorPlayMode_Then_AutoLaunchesWebSettings()
        {
            Type launcherType = RequireType(SettingsLauncherTypeName);

            Assert.That(
                InvokeStatic<bool>(
                    launcherType,
                    "ShouldAutoLaunchWebSettingsForPlayModeForTests",
                    MainRecordingScenePath,
                    false,
                    PlayModeStateChange.EnteredPlayMode),
                Is.True);
            Assert.That(
                InvokeStatic<bool>(
                    launcherType,
                    "ShouldAutoLaunchWebSettingsForPlayModeForTests",
                    MainAutoScenePath,
                    false,
                    PlayModeStateChange.EnteredPlayMode),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    launcherType,
                    "ShouldAutoLaunchWebSettingsForPlayModeForTests",
                    MainRecordingScenePath,
                    true,
                    PlayModeStateChange.EnteredPlayMode),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    launcherType,
                    "ShouldAutoLaunchWebSettingsForPlayModeForTests",
                    MainRecordingScenePath,
                    false,
                    PlayModeStateChange.ExitingEditMode),
                Is.False);
        }

        [Test]
        public void Given_EditorPlayModeAutoLaunch_When_InspectingOwnership_Then_BelongsToCompanionLauncher()
        {
            Type launcherType = RequireType(SettingsLauncherTypeName);
            Type guardType = RequireType(EditorPlayModeGuardTypeName);
            const BindingFlags StaticMembers =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            string[] autoLaunchMemberNames =
            {
                "ShouldAutoLaunchWebSettingsForPlayModeForTests",
                "TryAutoLaunchWebSettingsForPlayModeForTests",
                "ResetAutoLaunchWebSettingsForTests",
            };

            Assert.That(
                launcherType.GetField("hasAutoLaunchedWebSettingsForCurrentPlayMode", StaticMembers),
                Is.Not.Null);
            Assert.That(
                guardType.GetField("hasAutoLaunchedWebSettingsForCurrentPlayMode", StaticMembers),
                Is.Null);

            foreach (string memberName in autoLaunchMemberNames)
            {
                Assert.That(launcherType.GetMethod(memberName, StaticMembers), Is.Not.Null, memberName);
                Assert.That(guardType.GetMethod(memberName, StaticMembers), Is.Null, memberName);
            }

            string launcherSource = File.ReadAllText(
                "Assets/_Project/Scripts/Editor/Settings/MainRecordingSettingsCompanionLauncher.cs");
            string guardSource = File.ReadAllText(
                "Assets/_Project/Scripts/Editor/Settings/MainRecordingEditorPlayModeGuard.cs");
            Assert.That(
                launcherSource,
                Does.Contain("EditorApplication.playModeStateChanged += OnPlayModeStateChanged;"));
            Assert.That(guardSource, Does.Not.Contain("TryAutoLaunchWebSettingsForPlayMode"));
            int companionCallbackUnsubscribeIndex = launcherSource.IndexOf(
                "EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;",
                StringComparison.Ordinal);
            int companionCallbackSubscribeIndex = launcherSource.IndexOf(
                "EditorApplication.playModeStateChanged += OnPlayModeStateChanged;",
                StringComparison.Ordinal);
            int guardCallbackRegistrationIndex = guardSource.IndexOf(
                "EditorApplication.playModeStateChanged += OnPlayModeStateChanged;",
                StringComparison.Ordinal);
            int companionCallbackRegistrationIndex = guardSource.IndexOf(
                "MainRecordingSettingsCompanionLauncher.RegisterEditorPlayModeCallback();",
                StringComparison.Ordinal);
            Assert.That(companionCallbackUnsubscribeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                companionCallbackSubscribeIndex,
                Is.GreaterThan(companionCallbackUnsubscribeIndex),
                "Companion callback은 중복 구독을 제거한 뒤 다시 등록해야 합니다.");
            Assert.That(guardCallbackRegistrationIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                companionCallbackRegistrationIndex,
                Is.GreaterThan(guardCallbackRegistrationIndex),
                "Editor 보호 적용 callback이 Companion 자동 실행 callback보다 먼저 등록돼야 합니다.");
        }

        [Test]
        public void Given_CompanionAutoLaunchAlreadyRan_When_PlayModeExits_Then_AllowsNextSessionLaunch()
        {
            Type launcherType = RequireType(SettingsLauncherTypeName);
            int launchCount = 0;
            Action openSettings = () => launchCount++;

            InvokeStatic<object>(launcherType, "ResetAutoLaunchWebSettingsForTests");

            try
            {
                Assert.That(
                    InvokeStatic<bool>(
                        launcherType,
                        "TryAutoLaunchWebSettingsForPlayModeForTests",
                        MainRecordingScenePath,
                        false,
                        PlayModeStateChange.EnteredPlayMode,
                        openSettings),
                    Is.True);

                InvokeStatic<object>(launcherType, "OnPlayModeStateChanged", PlayModeStateChange.ExitingPlayMode);

                Assert.That(
                    InvokeStatic<bool>(
                        launcherType,
                        "TryAutoLaunchWebSettingsForPlayModeForTests",
                        MainRecordingScenePath,
                        false,
                        PlayModeStateChange.EnteredPlayMode,
                        openSettings),
                    Is.True);

                InvokeStatic<object>(launcherType, "OnPlayModeStateChanged", PlayModeStateChange.EnteredEditMode);

                Assert.That(
                    InvokeStatic<bool>(
                        launcherType,
                        "TryAutoLaunchWebSettingsForPlayModeForTests",
                        MainRecordingScenePath,
                        false,
                        PlayModeStateChange.EnteredPlayMode,
                        openSettings),
                    Is.True);
                Assert.That(launchCount, Is.EqualTo(3));
            }
            finally
            {
                InvokeStatic<object>(launcherType, "ResetAutoLaunchWebSettingsForTests");
            }
        }

        [Test]
        public void Given_SettingsLauncher_When_InspectingTestSeam_Then_UsesCallScopedDependency()
        {
            Type launcherType = RequireType(SettingsLauncherTypeName);
            const BindingFlags StaticMembers =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo[] mutableStaticDelegates = Array.FindAll(
                launcherType.GetFields(StaticMembers),
                field => typeof(Delegate).IsAssignableFrom(field.FieldType) && !field.IsInitOnly);
            MethodInfo scopedLaunchMethod = launcherType.GetMethod(
                "OpenMainRecordingSettingsForTests",
                StaticMembers);

            Assert.That(mutableStaticDelegates, Is.Empty);
            Assert.That(launcherType.GetMethod("SetLaunchWebSettingsForTests", StaticMembers), Is.Null);
            Assert.That(launcherType.GetMethod("ResetLaunchWebSettingsForTests", StaticMembers), Is.Null);
            Assert.That(scopedLaunchMethod, Is.Not.Null);
            Assert.That(scopedLaunchMethod.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(
                scopedLaunchMethod.GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(Action<MainRecordingSettingsLaunchPlan>)));
        }

        [Test]
        public void Given_MainRecordingScene_When_AutoLaunchingEditorPlayMode_Then_InvokesWebSettingsLauncherOnce()
        {
            Type launcherType = RequireType(SettingsLauncherTypeName);
            int launchCount = 0;
            string settingsPath = string.Empty;
            Action<MainRecordingSettingsLaunchPlan> launcher = plan =>
            {
                launchCount++;
                settingsPath = plan.SettingsPath;
            };
            Action openSettings = () =>
                InvokeStatic<object>(launcherType, "OpenMainRecordingSettingsForTests", launcher);

            InvokeStatic<object>(launcherType, "ResetAutoLaunchWebSettingsForTests");

            try
            {
                bool firstLaunch = InvokeStatic<bool>(
                    launcherType,
                    "TryAutoLaunchWebSettingsForPlayModeForTests",
                    MainRecordingScenePath,
                    false,
                    PlayModeStateChange.EnteredPlayMode,
                    openSettings);
                bool duplicateLaunch = InvokeStatic<bool>(
                    launcherType,
                    "TryAutoLaunchWebSettingsForPlayModeForTests",
                    MainRecordingScenePath,
                    false,
                    PlayModeStateChange.EnteredPlayMode,
                    openSettings);

                Assert.That(firstLaunch, Is.True);
                Assert.That(duplicateLaunch, Is.False);
                Assert.That(launchCount, Is.EqualTo(1));
                Assert.That(settingsPath, Does.EndWith("main-recording-settings.json"));
            }
            finally
            {
                InvokeStatic<object>(launcherType, "ResetAutoLaunchWebSettingsForTests");
            }
        }

        [Test]
        public void Given_EditorPlayModeGuardMaintainPolicy_When_InspectingSignature_Then_HasNoUnusedPlayModeArgument()
        {
            Type guardType = RequireType(EditorPlayModeGuardTypeName);
            const BindingFlags StaticMembers =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo maintainPolicy = guardType.GetMethod(
                "ShouldMaintainEditorPlayModeGuardForTests",
                StaticMembers);

            Assert.That(maintainPolicy, Is.Not.Null);
            Assert.That(maintainPolicy.GetParameters(), Has.Length.EqualTo(2));
            Assert.That(maintainPolicy.GetParameters()[0].Name, Is.EqualTo("scenePath"));
            Assert.That(maintainPolicy.GetParameters()[1].Name, Is.EqualTo("isBatchMode"));
        }

        [Test]
        public void Given_MainRecordingScene_When_PreparingEditorPlayMode_Then_DisablesBurstDirectCallsAndNeutralizesEditorTint()
        {
            Type guardType = RequireType(EditorPlayModeGuardTypeName);

            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldApplyEditorPlayModeGuardForTests",
                    MainRecordingScenePath,
                    false,
                    PlayModeStateChange.ExitingEditMode),
                Is.True);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldApplyEditorPlayModeGuardForTests",
                    MainAutoScenePath,
                    false,
                    PlayModeStateChange.ExitingEditMode),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldApplyEditorPlayModeGuardForTests",
                    MainRecordingScenePath,
                    true,
                    PlayModeStateChange.ExitingEditMode),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldApplyEditorPlayModeGuardForTests",
                    MainRecordingScenePath,
                    false,
                    PlayModeStateChange.EnteredPlayMode),
                Is.True,
                "The neutral Playmode tint must be re-applied after domain reload finishes and Play Mode is entered.");
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldMaintainEditorPlayModeGuardForTests",
                    MainRecordingScenePath,
                    false),
                Is.True,
                "Burst direct-call IL postprocessing must remain disabled for the Main Recording scene before and during Play Mode.");
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldMaintainEditorPlayModeGuardForTests",
                    MainAutoScenePath,
                    false),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldMaintainEditorPlayModeGuardForTests",
                    MainRecordingScenePath,
                    true),
                Is.False);
            Assert.That(InvokeStatic<bool>(guardType, "CanReflectBurstCompilerOptionsForTests"), Is.True);
            Assert.That(
                InvokeStatic<string>(guardType, "GetBurstDisableEnvironmentVariableNameForTests"),
                Is.EqualTo("UNITY_BURST_DISABLE_COMPILATION"));
            Assert.That(
                InvokeStatic<bool>(guardType, "IsBurstDisableEnvironmentValueForTests", "1"),
                Is.True);
            Assert.That(
                InvokeStatic<bool>(guardType, "IsBurstDisableEnvironmentValueForTests", "0"),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldRequestBurstDisableCleanCompilationForTests",
                    null,
                    false,
                    false),
                Is.True);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldRequestBurstDisableCleanCompilationForTests",
                    "1",
                    false,
                    false),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldRequestBurstDisableCleanCompilationForTests",
                    null,
                    true,
                    false),
                Is.False);
            Assert.That(
                InvokeStatic<bool>(
                    guardType,
                    "ShouldRequestBurstDisableCleanCompilationForTests",
                    null,
                    false,
                    true),
                Is.False);
            Assert.That(InvokeStatic<bool>(guardType, "CanReflectPlayModeTintForTests"), Is.True);
            Assert.That(
                InvokeStatic<bool>(guardType, "IsNeutralPlayModeTintForTests", new Color(1f, 1f, 1f, 1f)),
                Is.True);
            Assert.That(
                InvokeStatic<bool>(guardType, "IsNeutralPlayModeTintForTests", new Color(0.8f, 0.8f, 0.8f, 1f)),
                Is.False);
            Assert.That(
                InvokeStatic<string>(guardType, "GetEditorPlayModeGuardPolicyForTests"),
                Does.Contain("UNITY_BURST_DISABLE_COMPILATION"));
        }

        [Test]
        public void Given_MainRecordingScene_When_RestoringEditorPlayModeState_Then_RestoresBurstAndEnvironment()
        {
            Type guardType = RequireType(EditorPlayModeGuardTypeName);
            string environmentVariableName =
                InvokeStatic<string>(guardType, "GetBurstDisableEnvironmentVariableNameForTests");
            string originalEnvironmentValue =
                Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process);
            bool originalBurstCompilation = InvokeStatic<bool>(guardType, "GetCurrentBurstCompilationForTests");

            try
            {
                InvokeStatic<object>(guardType, "RestoreEditorPlayModeState");
                EditorSceneManager.OpenScene(MainRecordingScenePath);
                Environment.SetEnvironmentVariable(environmentVariableName, null, EnvironmentVariableTarget.Process);
                InvokeStatic<bool>(guardType, "ApplyBurstCompilationForTests", true);
                bool savedBurstCompilation = InvokeStatic<bool>(guardType, "GetCurrentBurstCompilationForTests");

                InvokeStatic<object>(guardType, "ApplyBeforeMainRecordingPlayMode");

                Assert.That(InvokeStatic<bool>(guardType, "GetCurrentBurstCompilationForTests"), Is.False);
                Assert.That(
                    Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process),
                    Is.EqualTo("1"));

                InvokeStatic<object>(guardType, "RestoreEditorPlayModeState");

                Assert.That(
                    InvokeStatic<bool>(guardType, "GetCurrentBurstCompilationForTests"),
                    Is.EqualTo(savedBurstCompilation));
                Assert.That(
                    Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process),
                    Is.Null);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    environmentVariableName,
                    originalEnvironmentValue,
                    EnvironmentVariableTarget.Process);
                InvokeStatic<bool>(guardType, "ApplyBurstCompilationForTests", originalBurstCompilation);
            }
        }

        [Test]
        public void Given_WebSettingsSource_When_InspectingReferenceShell_Then_MatchesElectronFirstScreenContract()
        {
            Type layoutSpecType = RequireType(LayoutSpecTypeName);
            string webRoot = "Assets/_Project/Tools/MainRecordingSettings";
            string indexPath = Path.Combine(webRoot, "build", "index.html");
            string stylePath = Path.Combine(webRoot, "build", "styles.css");

            Assert.That(GetStaticMemberValue<int>(layoutSpecType, "ReferenceWidth"), Is.EqualTo(1265));
            Assert.That(GetStaticMemberValue<int>(layoutSpecType, "ReferenceHeight"), Is.EqualTo(675));
            Assert.That(GetStaticMemberValue<float>(layoutSpecType, "RailWidth"), Is.EqualTo(56f));
            Assert.That(GetStaticMemberValue<float>(layoutSpecType, "SidebarWidth"), Is.EqualTo(249f));
            Assert.That(GetStaticMemberValue<float>(layoutSpecType, "CardWidth"), Is.EqualTo(672f));
            Assert.That(GetStaticMemberValue<float>(layoutSpecType, "CardHeight"), Is.EqualTo(192f));
            Assert.That(
                GetStaticMemberValue<float>(layoutSpecType, "CardButtonWidth"),
                Is.GreaterThanOrEqualTo(128f),
                "The 'FBX 가져오기' button must be wide enough to avoid clipping at the default 1.25x display scale.");

            Assert.That(File.Exists(indexPath), Is.True, indexPath);
            Assert.That(File.Exists(stylePath), Is.True, stylePath);
            string index = File.ReadAllText(indexPath);
            string styles = File.ReadAllText(stylePath);

            Assert.That(index, Does.Contain("class=\"rail\""));
            Assert.That(index, Does.Contain("class=\"sidebar\""));
            Assert.That(index, Does.Contain("id=\"statusBadge\""));
            Assert.That(index, Does.Contain("FBX 파일 임포트"));
            Assert.That(index, Does.Contain("FBX 가져오기"));
            Assert.That(styles, Does.Contain("--reference-width: 1265px"));
            Assert.That(styles, Does.Contain("--reference-height: 675px"));
        }

        [Test]
        public void Given_EditorSettingsSource_When_InspectingLegacyFile_Then_RemovesImguiEditorWindowSurface()
        {
            string oldWindowPath =
                "Assets/_Project/Scripts/Settings/Editor/MainRecordingSettingsWindow.cs";
            string launcherPath =
                "Assets/_Project/Scripts/Editor/Settings/MainRecordingSettingsCompanionLauncher.cs";
            string guardPath =
                "Assets/_Project/Scripts/Editor/Settings/MainRecordingEditorPlayModeGuard.cs";

            Assert.That(File.Exists(launcherPath), Is.True, "The menu entry must live in the companion launcher.");
            Assert.That(File.Exists(guardPath), Is.True, "The Play Mode guard must be split from the old IMGUI window.");
            string launcherSource = File.ReadAllText(launcherPath);
            string legacyAutoOpenEditorWindowMethodName =
                "ShouldAutoOpenEditorWindow" + "ForPlayModeForTests";
            Assert.That(launcherSource, Does.Not.Contain(legacyAutoOpenEditorWindowMethodName));
            if (!File.Exists(oldWindowPath))
            {
                return;
            }

            string oldWindowSource = File.ReadAllText(oldWindowPath);
            Assert.That(oldWindowSource, Does.Not.Contain("EditorWindow"));
            Assert.That(oldWindowSource, Does.Not.Contain("OnGUI"));
            Assert.That(oldWindowSource, Does.Not.Contain("DrawRail"));
            Assert.That(oldWindowSource, Does.Not.Contain("DrawSidebar"));
            Assert.That(oldWindowSource, Does.Not.Contain("DrawMainArea"));
            Assert.That(oldWindowSource, Does.Not.Contain("DrawCard"));
            Assert.That(oldWindowSource, Does.Not.Contain("MainRecordingSettingsWindowPlayModeAutoOpener"));
        }

        [Test]
        public void Given_EditorSettingsSource_When_InspectingVisibleLogText_Then_KeepsKoreanReadable()
        {
            string launcherPath =
                "Assets/_Project/Scripts/Editor/Settings/MainRecordingSettingsCompanionLauncher.cs";
            string guardPath =
                "Assets/_Project/Scripts/Editor/Settings/MainRecordingEditorPlayModeGuard.cs";

            string launcherSource = ReadUtf8Source(launcherPath);
            string guardSource = ReadUtf8Source(guardPath);

            Assert.That(HasUtf8Bom(launcherPath), Is.True, launcherPath);
            Assert.That(HasUtf8Bom(guardPath), Is.True, guardPath);
            Assert.That(launcherSource, Does.Contain("Web 설정창 실행에 실패했습니다."));
            Assert.That(
                guardSource,
                Does.Contain(
                    "Main_Recoding Play 준비를 위해 Burst direct-call 컴파일을 비활성화하고 스크립트 clean compile을 요청했습니다."));
            AssertSourceDoesNotContainReplacementCharacters(launcherPath, launcherSource);
            AssertSourceDoesNotContainReplacementCharacters(guardPath, guardSource);
        }

        [Test]
        public void Given_WebSettingsSource_When_InspectingVisibleText_Then_RemovesLegacyEditorWindowPlaceholders()
        {
            string indexPath =
                "Assets/_Project/Tools/MainRecordingSettings/build/index.html";

            Assert.That(File.Exists(indexPath), Is.True, indexPath);
            string index = File.ReadAllText(indexPath);

            Assert.That(index, Does.Not.Contain("공유 설정을 불러왔습니다."));
            Assert.That(index, Does.Not.Contain("공유 설정을 저장했습니다."));
            Assert.That(index, Does.Not.Contain("공유 설정 저장"));
            Assert.That(index, Does.Not.Contain("캐릭터"));
            Assert.That(index, Does.Not.Contain("Character 1 (비활성화)"));
            Assert.That(index, Does.Not.Contain("Character 1 (Inactive)"));
            Assert.That(index.IndexOf('\uFFFD'), Is.EqualTo(-1));
            Assert.That(index, Does.Not.Contain("??"));
        }

        [Test]
        public void Given_CompanionController_When_LoadingAndSaving_Then_UsesSharedSettingsStore()
        {
            Type controllerType = RequireType(CompanionControllerTypeName);
            string path = CreateTempSettingsPath();
            var gameObject = new GameObject("Main Recording Settings Companion Controller Test");

            try
            {
                var controller = gameObject.AddComponent(controllerType);

                MainRecordingSettingsDocument loaded =
                    InvokeInstance<MainRecordingSettingsDocument>(controller, "LoadFromPathForTests", path);

                Assert.That(loaded.schemaVersion, Is.EqualTo(1));
                Assert.That(loaded.captureWidth, Is.EqualTo(1920));
                Assert.That(InvokeInstance<bool>(controller, "IsSaveButtonEnabledForTests"), Is.True);

                var document = new MainRecordingSettingsDocument
                {
                    fbxPath = "D:/motions/editor-roundtrip.fbx",
                    characterModelPath = "D:/models/future-character.vrm",
                    captureWidth = 2560,
                    captureHeight = 1440,
                    openSettingsOnStart = false,
                };

                InvokeInstance<object>(controller, "SetDocumentForTests", document);
                Assert.That(InvokeInstance<bool>(controller, "SaveSettings"), Is.True);

                var store = new MainRecordingSettingsStore(path);
                MainRecordingSettingsDocument roundTrip = store.LoadOrCreateDefault();
                Assert.That(roundTrip.fbxPath, Is.EqualTo(document.fbxPath));
                Assert.That(roundTrip.characterModelPath, Is.EqualTo(document.characterModelPath));
                Assert.That(roundTrip.captureWidth, Is.EqualTo(2560));
                Assert.That(roundTrip.captureHeight, Is.EqualTo(1440));
                Assert.That(roundTrip.openSettingsOnStart, Is.False);
                Assert.That(GetMemberValue<string>(controller, "StatusMessage"), Does.Contain("저장"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Given_MainRecordingScene_When_InspectingOnboardingActions_Then_BasicSetupCanReachFbxImporter()
        {
            EditorSceneManager.OpenScene(MainRecordingScenePath);
            RecordingSetting recodingSetting = UnityEngine.Object.FindObjectOfType<RecordingSetting>();

            Assert.That(
                MainRecordingSettingsActions.CanExecute(
                    MainRecordingSettingsActionType.ImportFbx,
                    recodingSetting),
                Is.True);
        }

        [Test]
        public void Given_MainRecordingScene_When_ExecutingImportFbxBeforeFBXVmdPipelineAwake_Then_InitializesFileBrowserAndCancelsSafely()
        {
            EditorSceneManager.OpenScene(MainRecordingScenePath);
            FBXVmdPipeline fileManager = UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();
            Assert.That(fileManager, Is.Not.Null);

            var fakeFileBrowser = new CancelFileBrowserService();
            Func<IFileBrowserService> originalFactory =
                GetStaticMemberValue<Func<IFileBrowserService>>(typeof(FBXVmdPipeline), "fileBrowserServiceFactory");

            try
            {
                SetStaticField(
                    typeof(FBXVmdPipeline),
                    "fileBrowserServiceFactory",
                    new Func<IFileBrowserService>(() => fakeFileBrowser));
                SetField(fileManager, "_fileBrowserService", null);
                SetField(fileManager, "_fbxImporter", null);

                bool executed = false;
                Assert.DoesNotThrow(() =>
                {
                    executed = MainRecordingSettingsActions.Execute(
                        MainRecordingSettingsActionType.ImportFbx,
                        null,
                        fileManager);
                });

                Assert.That(executed, Is.True);
                Assert.That(fakeFileBrowser.OpenFilePanelCallCount, Is.EqualTo(1));
                Assert.That(fakeFileBrowser.LastTitle, Is.EqualTo("Import FBX"));
                Assert.That(fakeFileBrowser.LastExtension, Is.EqualTo("fbx"));
                Assert.That(fakeFileBrowser.LastMultiselect, Is.False);
            }
            finally
            {
                SetStaticField(typeof(FBXVmdPipeline), "fileBrowserServiceFactory", originalFactory);
            }
        }

        [Test]
        public void Given_MainRecordingScene_When_ResolvingSettingsActionComponents_Then_UsesSplitSceneComponents()
        {
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            GameObject root = GameObject.Find("Setting");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponent<GraphicSetting>(), Is.Not.Null);
            Assert.That(root.GetComponent<BackgroundColorSetting>(), Is.Not.Null);
            Assert.That(root.GetComponent<RecordingSetting>(), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
        }

        [Test]
        public void Given_RuntimeSettingsKoreanFallback_When_CheckingOwnership_Then_UsesSharedConcreteType()
        {
            const string fallbackPath =
                "Assets/_Project/Scripts/Settings/KoreanUiTextFallback.cs";
            const string popupPath =
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsPopup.cs";
            const string companionPath =
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsCompanionController.cs";

            Assert.That(File.Exists(fallbackPath), Is.True, fallbackPath);

            string fallbackSource = File.ReadAllText(fallbackPath);
            string popupSource = File.ReadAllText(popupPath);
            string companionSource = File.ReadAllText(companionPath);

            Assert.That(fallbackSource, Does.Contain("internal static class KoreanUiTextFallback"));
            Assert.That(popupSource, Does.Contain("KoreanUiTextFallback.Apply("));
            Assert.That(companionSource, Does.Contain("KoreanUiTextFallback.Apply("));
            Assert.That(popupSource, Does.Not.Contain("private static void ApplyReadableKoreanFont"));
            Assert.That(companionSource, Does.Not.Contain("private static void ApplyReadableKoreanFont"));
        }

        [Test]
        public void Given_RuntimePopupElementConstruction_When_CheckingOwnership_Then_UsesDedicatedBuilder()
        {
            const string popupPath =
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsPopup.cs";
            const string builderPath =
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsElementBuilder.cs";

            Assert.That(File.Exists(builderPath), Is.True, builderPath);

            string popupSource = File.ReadAllText(popupPath);
            string builderSource = File.ReadAllText(builderPath);

            Assert.That(builderSource, Does.Contain("internal sealed class MainRecordingSettingsElementBuilder"));
            Assert.That(popupSource, Does.Contain("new MainRecordingSettingsElementBuilder("));
            Assert.That(popupSource, Does.Not.Contain("private Button CreateButton("));
            Assert.That(popupSource, Does.Not.Contain("private Image CreateImage("));
            Assert.That(popupSource, Does.Not.Contain("private TextMeshProUGUI CreateText("));
            Assert.That(popupSource, Does.Not.Contain("private RectTransform CreateRectTransform("));
        }

        [Test]
        public void Given_RuntimePopupNotification_When_ShowingKoreanMessage_Then_RemainsReadable()
        {
            var popupObject = new GameObject("Runtime Popup Korean Notification Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();

                InvokeInstance<object>(popup, "ShowNotification", "설정을 준비 중입니다.");

                Assert.That(popup.HasReadableKoreanTextForTests(), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_LegacyKoreanFallback_When_TextBecomesNonKorean_Then_RestoresTmpLabel()
        {
            Type fallbackType = RequireType(KoreanUiTextFallbackTypeName);
            var labelObject = new GameObject("Korean Fallback State Test", typeof(RectTransform));
            var fallbackObject = new GameObject(
                "KoreanTextFallback",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Text));

            try
            {
                var label = labelObject.AddComponent<TextMeshProUGUI>();
                UnityEngine.UI.Text fallbackText = fallbackObject.GetComponent<UnityEngine.UI.Text>();
                fallbackObject.transform.SetParent(labelObject.transform, false);
                label.text = "Ready";
                label.enabled = false;
                fallbackText.enabled = true;

                InvokeStatic<object>(fallbackType, "Apply", label);

                Assert.That(label.enabled, Is.True);
                Assert.That(fallbackText.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(labelObject);
            }
        }

        [Test]
        public void Given_BuiltRuntimePopupWithLostRuntimeCache_When_EnsuringAgain_Then_ReusesGeneratedHierarchy()
        {
            var popupObject = new GameObject("Runtime Popup Reentry Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Transform generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                int initialGeneratedChildCount = generatedContent.childCount;
                Transform closeButtonTransform = generatedContent.Find("CloseButton");
                Assert.That(closeButtonTransform, Is.Not.Null);

                UnityEngine.UI.Button closeButton = closeButtonTransform.GetComponent<UnityEngine.UI.Button>();
                Assert.That(closeButton, Is.Not.Null);
                closeButton.onClick.RemoveAllListeners();

                popup.ApplyDragDeltaForTests(new Vector2(96f, -32f));
                Vector2 draggedPosition = popupObject.GetComponent<RectTransform>().anchoredPosition;

                System.Collections.Generic.List<UnityEngine.UI.Button> cardButtons =
                    GetField<System.Collections.Generic.List<UnityEngine.UI.Button>>(popup, "cardButtons");
                foreach (UnityEngine.UI.Button cardButton in cardButtons)
                {
                    cardButton.onClick.RemoveAllListeners();
                }

                ResetRuntimePopupCache(popup);

                popup.Open();

                generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                Assert.That(generatedContent.childCount, Is.EqualTo(initialGeneratedChildCount));
                Assert.That(CountDirectChildrenNamed(popupObject.transform, "GeneratedContent"), Is.EqualTo(1));
                Assert.That(CountDirectChildrenNamed(generatedContent, "Page"), Is.EqualTo(1));
                Assert.That(CountDirectChildrenNamed(generatedContent, "Rail"), Is.EqualTo(1));
                Assert.That(CountDirectChildrenNamed(generatedContent, "MainViewport"), Is.EqualTo(1));
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Assert.That(popupObject.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(draggedPosition));

                closeButton.onClick.Invoke();
                Assert.That(popup.IsOpen, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_PartialRuntimePopupHierarchy_When_EnsuringAgain_Then_RebuildsOwnedElementsOnce()
        {
            var popupObject = new GameObject("Runtime Popup Partial Hierarchy Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Transform generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                int completeGeneratedChildCount = generatedContent.childCount;
                var extensionObject = new GameObject("Runtime Popup Extension", typeof(RectTransform));
                extensionObject.transform.SetParent(popupObject.transform, false);

                InvokeInstance<object>(popup, "ShowNotification", "부분 계층 복구 메시지");
                popup.ApplyDragDeltaForTests(new Vector2(72f, -24f));
                Vector2 draggedPosition = popupObject.GetComponent<RectTransform>().anchoredPosition;
                UnityEngine.Object.DestroyImmediate(generatedContent.Find("Rail").gameObject);

                popup.Open();

                generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                Assert.That(generatedContent.childCount, Is.EqualTo(completeGeneratedChildCount));
                Assert.That(CountDirectChildrenNamed(popupObject.transform, "GeneratedContent"), Is.EqualTo(1));
                Assert.That(CountDirectChildrenNamed(generatedContent, "Page"), Is.EqualTo(1));
                Assert.That(CountDirectChildrenNamed(generatedContent, "Rail"), Is.EqualTo(1));
                Assert.That(CountDirectChildrenNamed(generatedContent, "MainViewport"), Is.EqualTo(1));
                Assert.That(extensionObject.transform.parent, Is.EqualTo(popupObject.transform));
                Assert.That(extensionObject.activeSelf, Is.True);
                Assert.That(popupObject.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(draggedPosition));
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Assert.That(GetField<TextMeshProUGUI>(popup, "notificationText").text,
                    Is.EqualTo("부분 계층 복구 메시지"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_DamagedRuntimePopupWithReservedExtensionName_When_Rebuilding_Then_PreservesExtension()
        {
            var popupObject = new GameObject("Runtime Popup Reserved Extension Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Transform generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                var extensionObject = new GameObject("Page", typeof(RectTransform));
                extensionObject.transform.SetParent(popupObject.transform, false);
                int extensionSiblingIndex = extensionObject.transform.GetSiblingIndex();
                UnityEngine.Object.DestroyImmediate(generatedContent.Find("Rail").gameObject);

                popup.Open();

                Assert.That(extensionObject, Is.Not.Null);
                Assert.That(extensionObject.transform.parent, Is.EqualTo(popupObject.transform));
                Assert.That(extensionObject.activeSelf, Is.True);
                Assert.That(extensionObject.transform.GetSiblingIndex(), Is.EqualTo(extensionSiblingIndex));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_CompleteLegacyPopupHierarchy_When_EnsuringAgain_Then_MigratesWithoutRebuilding()
        {
            var popupObject = new GameObject("Runtime Popup Legacy Hierarchy Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Transform generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                int completeGeneratedChildCount = generatedContent.childCount;
                Transform page = generatedContent.Find("Page");
                UnityEngine.UI.Button closeButton = generatedContent
                    .Find("CloseButton")
                    .GetComponent<UnityEngine.UI.Button>();
                int externalCloseInvocationCount = 0;
                closeButton.onClick.AddListener(() => externalCloseInvocationCount++);

                InvokeInstance<object>(popup, "ShowNotification", "기존 계층 이전 메시지");
                popup.ApplyDragDeltaForTests(new Vector2(48f, -16f));
                Vector2 draggedPosition = popupObject.GetComponent<RectTransform>().anchoredPosition;
                Vector3 pageWorldPosition = page.position;

                var legacyChildren = new System.Collections.Generic.List<Transform>();
                for (int i = 0; i < generatedContent.childCount; i++)
                {
                    legacyChildren.Add(generatedContent.GetChild(i));
                }

                foreach (Transform child in legacyChildren)
                {
                    child.SetParent(popupObject.transform, true);
                }

                UnityEngine.Object.DestroyImmediate(generatedContent.gameObject);

                popup.Open();

                generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                Assert.That(CountDirectChildrenNamed(popupObject.transform, "GeneratedContent"), Is.EqualTo(1));
                Assert.That(CountDirectChildrenNamed(popupObject.transform, "Page"), Is.EqualTo(0));
                Assert.That(generatedContent.childCount, Is.EqualTo(completeGeneratedChildCount));
                Assert.That(generatedContent.Find("Page").position, Is.EqualTo(pageWorldPosition));
                Assert.That(popupObject.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(draggedPosition));
                Assert.That(GetField<TextMeshProUGUI>(popup, "notificationText").text,
                    Is.EqualTo("기존 계층 이전 메시지"));

                closeButton.onClick.Invoke();
                Assert.That(popup.IsOpen, Is.False);
                Assert.That(externalCloseInvocationCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_DuplicateRuntimePopupHierarchy_When_EnsuringAgain_Then_NormalizesOwnedElements()
        {
            var popupObject = new GameObject("Runtime Popup Duplicate Hierarchy Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Transform generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                int completeGeneratedChildCount = generatedContent.childCount;
                GameObject duplicatePage = UnityEngine.Object.Instantiate(
                    generatedContent.Find("Page").gameObject,
                    generatedContent);
                duplicatePage.name = "Page";

                popup.Open();

                generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                Assert.That(generatedContent.childCount, Is.EqualTo(completeGeneratedChildCount));
                Assert.That(CountDirectChildrenNamed(popupObject.transform, "GeneratedContent"), Is.EqualTo(1));
                Assert.That(CountDirectChildrenNamed(generatedContent, "Page"), Is.EqualTo(1));
                Assert.That(CountDirectChildrenNamed(generatedContent, "Rail"), Is.EqualTo(1));
                Assert.That(CountDirectChildrenNamed(generatedContent, "MainViewport"), Is.EqualTo(1));
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_DuplicateGeneratedContentContainer_When_EnsuringAgain_Then_NormalizesOwnedContainer()
        {
            var popupObject = new GameObject("Runtime Popup Duplicate Container Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Transform generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                GameObject duplicateContent = UnityEngine.Object.Instantiate(
                    generatedContent.gameObject,
                    popupObject.transform);
                duplicateContent.name = "GeneratedContent";

                popup.Open();

                Assert.That(CountDirectChildrenNamed(popupObject.transform, "GeneratedContent"), Is.EqualTo(1));
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_BalancedMissingAndDuplicateRuntimePopupHierarchy_When_EnsuringAgain_Then_NormalizesOwnedElements()
        {
            var popupObject = new GameObject("Runtime Popup Balanced Damage Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Transform generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                UnityEngine.Object.DestroyImmediate(generatedContent.Find("Rail").gameObject);
                GameObject duplicatePage = UnityEngine.Object.Instantiate(
                    generatedContent.Find("Page").gameObject,
                    generatedContent);
                duplicatePage.name = "Page";

                popup.Open();

                generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                Assert.That(CountDirectChildrenNamed(generatedContent, "Page"), Is.EqualTo(1));
                Assert.That(CountDirectChildrenNamed(generatedContent, "Rail"), Is.EqualTo(1));
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_RuntimePopupWithMissingRequiredComponent_When_EnsuringAgain_Then_RebuildsComponent()
        {
            var popupObject = new GameObject("Runtime Popup Component Damage Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Transform generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                UnityEngine.Object.DestroyImmediate(generatedContent.Find("Rail").GetComponent<UnityEngine.UI.Image>());

                popup.Open();

                generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                Assert.That(generatedContent.Find("Rail").GetComponent<UnityEngine.UI.Image>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_RuntimePopupWithDuplicateMainContent_When_EnsuringAgain_Then_NormalizesNestedHierarchy()
        {
            var popupObject = new GameObject("Runtime Popup Nested Duplicate Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Transform generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                Transform mainViewport = generatedContent.Find("MainViewport");
                GameObject duplicateMainContent = UnityEngine.Object.Instantiate(
                    mainViewport.Find("MainContent").gameObject,
                    mainViewport);
                duplicateMainContent.name = "MainContent";

                popup.Open();

                generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                mainViewport = generatedContent.Find("MainViewport");
                Assert.That(CountDirectChildrenNamed(mainViewport, "MainContent"), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_DamagedLegacyPopupBeforeExtension_When_Rebuilding_Then_PreservesRelativeSiblingOrder()
        {
            var popupObject = new GameObject("Runtime Popup Damaged Legacy Order Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                Transform generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                var legacyChildren = new System.Collections.Generic.List<Transform>();
                for (int i = 0; i < generatedContent.childCount; i++)
                {
                    legacyChildren.Add(generatedContent.GetChild(i));
                }

                foreach (Transform child in legacyChildren)
                {
                    child.SetParent(popupObject.transform, true);
                }

                UnityEngine.Object.DestroyImmediate(generatedContent.gameObject);
                var extensionObject = new GameObject("Runtime Popup Legacy Extension", typeof(RectTransform));
                extensionObject.transform.SetParent(popupObject.transform, false);
                UnityEngine.Object.DestroyImmediate(
                    popupObject.transform.Find("Rail").GetComponent<UnityEngine.UI.Image>());

                popup.Open();

                generatedContent = FindRuntimePopupGeneratedContent(popupObject.transform);
                Assert.That(CountDirectChildrenNamed(popupObject.transform, "GeneratedContent"), Is.EqualTo(1));
                Assert.That(generatedContent.GetSiblingIndex(), Is.LessThan(extensionObject.transform.GetSiblingIndex()));
                Assert.That(extensionObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_BuiltRuntimePopupWithLostRootCache_When_EnsuringAgain_Then_PreservesExistingListeners()
        {
            var popupObject = new GameObject("Runtime Popup Listener Preservation Test", typeof(RectTransform));

            try
            {
                var popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
                Assert.That(popup.GetCardButtonCountForTests(), Is.EqualTo(3));
                int initialChildCount = popupObject.transform.childCount;
                UnityEngine.UI.Button closeButton =
                    FindRuntimePopupGeneratedContent(popupObject.transform)
                        .Find("CloseButton")
                        .GetComponent<UnityEngine.UI.Button>();
                int externalCloseInvocationCount = 0;
                closeButton.onClick.AddListener(() => externalCloseInvocationCount++);

                SetField(popup, "panelRoot", null);
                SetField(popup, "canvasGroup", null);
                SetField(popup, "notificationText", null);

                popup.Open();
                closeButton.onClick.Invoke();

                Assert.That(popupObject.transform.childCount, Is.EqualTo(initialChildCount));
                Assert.That(popup.IsOpen, Is.False);
                Assert.That(externalCloseInvocationCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(popupObject);
            }
        }

        [Test]
        public void Given_MainRecordingScene_When_EnsuringRuntimeSettingsPopup_Then_CreatesPopupUnderUiCanvas()
        {
            Type popupType = RequireType(RuntimePopupTypeName);
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            RecordingSetting recodingSetting = UnityEngine.Object.FindObjectOfType<RecordingSetting>();
            Assert.That(recodingSetting, Is.Not.Null, "Main_recoding must keep RecordingSetting on the Setting object.");
            Assert.That(GetField<bool>(recodingSetting, "openSettingsPopupOnStart"), Is.True);

            object popup = InvokeInstance<object>(recodingSetting, "EnsureSettingsPopup");
            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.GetType(), Is.EqualTo(popupType));

            var popupComponent = (Component)popup;
            Assert.That(popupComponent.transform.parent, Is.Not.Null);
            Assert.That(popupComponent.transform.parent.name, Is.EqualTo("UI_Canvas"));
            Assert.That(GetField<Component>(recodingSetting, "settingsPopup"), Is.EqualTo(popupComponent));
            Assert.That(InvokeInstance<Vector2>(popup, "GetReferenceSizeForTests"), Is.EqualTo(new Vector2(1265f, 675f)));
            Assert.That(InvokeInstance<Vector2>(popup, "GetDisplayedSizeForTests"),
                Is.EqualTo(new Vector2(1581.25f, 843.75f)));
            Assert.That(InvokeInstance<bool>(popup, "SupportsPointerDragForTests"), Is.True);
            RectTransform popupRect = popupComponent.GetComponent<RectTransform>();
            Vector2 beforeDrag = popupRect.anchoredPosition;
            InvokeInstance<object>(popup, "ApplyDragDeltaForTests", new Vector2(120f, -48f));
            Assert.That(popupRect.anchoredPosition, Is.EqualTo(beforeDrag + new Vector2(120f, -48f)));
            Assert.That(InvokeInstance<int>(popup, "GetCardButtonCountForTests"), Is.EqualTo(3));
            Assert.That(InvokeInstance<bool>(popup, "CanResolveImportActionForTests"), Is.True);
            Assert.That(InvokeInstance<bool>(popup, "UsesCharacterVisualAssetForTests"), Is.False);
            Assert.That(InvokeInstance<bool>(popup, "IsProductionSurfaceForTests"), Is.False);
            Assert.That(InvokeInstance<bool>(popup, "HasReadableKoreanTextForTests"), Is.True);
            Assert.That(
                InvokeInstance<string[]>(popup, "GetSidebarItemLabelsForTests"),
                Is.EqualTo(new[] { "Camera 1", "Environment", "Directional Light" }));
            string[] visibleText = InvokeInstance<string[]>(popup, "GetVisibleTextForTests");
            Assert.That(visibleText, Does.Not.Contain("캐릭터"));
            Assert.That(visibleText, Does.Not.Contain("Character 1 (비활성화)"));
            Assert.That(visibleText, Does.Not.Contain("Character 1 (Inactive)"));
        }

        private static void ExpectHeadlessWindowLogsIfNeeded()
        {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return;
            }

            LogAssert.Expect(LogType.Error, "No graphic device is available to initialize the view.");
            LogAssert.Expect(LogType.Error, "No graphic device is available to show the window.");
            LogAssert.Expect(LogType.Error, "No graphic device is available to initialize the view.");
        }

        private static void AssertHeader<T>(string fieldName, string expectedHeader)
        {
            FieldInfo field = RequireField<T>(fieldName);
            HeaderAttribute attribute = field.GetCustomAttribute<HeaderAttribute>();
            Assert.That(attribute, Is.Not.Null, $"{fieldName} must expose a Korean inspector section header.");
            Assert.That(attribute.header, Is.EqualTo(expectedHeader));
        }

        private static void AssertInspectorName<T>(string fieldName, string expectedName)
        {
            FieldInfo field = RequireField<T>(fieldName);
            InspectorNameAttribute attribute = field.GetCustomAttribute<InspectorNameAttribute>();
            Assert.That(attribute, Is.Not.Null, $"{fieldName} must expose a Korean inspector label.");
            Assert.That(attribute.displayName, Is.EqualTo(expectedName));
        }

        private static FieldInfo RequireField<T>(string fieldName)
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{typeof(T).Name}.{fieldName} must exist.");
            return field;
        }

        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName);
            Assert.That(type, Is.Not.Null, $"{typeName} must exist.");
            return type;
        }

        private static T InvokeStatic<T>(Type type, string methodName, params object[] args)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} must exist.");
            return (T)method.Invoke(null, args);
        }

        private static T InvokeInstance<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{target.GetType().FullName}.{methodName} must exist.");
            return (T)method.Invoke(target, args);
        }

        private static T GetStaticMemberValue<T>(Type type, string memberName)
        {
            PropertyInfo property = type.GetProperty(
                memberName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return (T)property.GetValue(null);
            }

            FieldInfo field = type.GetField(
                memberName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{type.FullName}.{memberName} must exist.");
            return (T)field.GetValue(null);
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{instance.GetType().FullName}.{fieldName} must exist.");
            return (T)field.GetValue(instance);
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{instance.GetType().FullName}.{fieldName} must exist.");
            field.SetValue(instance, value);
        }

        private static int CountDirectChildrenNamed(Transform parent, string childName)
        {
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == childName)
                {
                    count++;
                }
            }

            return count;
        }

        private static Transform FindRuntimePopupGeneratedContent(Transform popupRoot)
        {
            Transform generatedContent = popupRoot.Find("GeneratedContent");
            return generatedContent != null ? generatedContent : popupRoot;
        }

        private static void ResetRuntimePopupCache(MainRecordingSettingsPopup popup)
        {
            GetField<System.Collections.Generic.List<UnityEngine.UI.Button>>(popup, "cardButtons").Clear();
            SetField(popup, "panelRoot", null);
            SetField(popup, "canvasGroup", null);
            SetField(popup, "notificationText", null);
        }

        private static void SetImportCommand(MainRecordingSettingsDocument document, string commandId, string fbxPath)
        {
            FieldInfo commandField = typeof(MainRecordingSettingsDocument).GetField("pendingCommand");
            Assert.That(commandField, Is.Not.Null, "MainRecordingSettingsDocument.pendingCommand must exist.");

            object command = commandField.GetValue(document);
            Assert.That(command, Is.Not.Null, "MainRecordingSettingsDocument.pendingCommand must not be null.");

            SetField(command, "commandId", commandId);
            SetField(command, "action", "ImportFbx");
            SetField(command, "fbxPath", fbxPath);
            SetField(command, "requestedAtUtc", DateTime.UtcNow.ToString("O"));
        }

        private static void SetStaticField(Type type, string fieldName, object value)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{type.FullName}.{fieldName} must exist.");
            field.SetValue(null, value);
        }

        private static void ResetSettingsWindowStyleCache(Type windowType)
        {
            SetStaticField(windowType, "titleStyle", null);
            SetStaticField(windowType, "sidebarHeaderStyle", null);
            SetStaticField(windowType, "sidebarItemStyle", null);
            SetStaticField(windowType, "sidebarInactiveItemStyle", null);
            SetStaticField(windowType, "cardTitleStyle", null);
            SetStaticField(windowType, "cardBodyStyle", null);
            SetStaticField(windowType, "cardButtonStyle", null);
            SetStaticField(windowType, "iconStyle", null);
            SetStaticField(windowType, "toolbarStyle", null);
        }

        private static T GetMemberValue<T>(object instance, string memberName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return (T)property.GetValue(instance);
            }

            FieldInfo field = instance.GetType().GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{instance.GetType().FullName}.{memberName} must exist.");
            return (T)field.GetValue(instance);
        }

        private static string CreateTempSettingsPath()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "UnityFbx2Vmd-MainRecordingSettingsEditorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "main-recording-settings.json");
        }

        private static string ReadUtf8Source(string path)
        {
            Assert.That(File.Exists(path), Is.True, path);
            return new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path)).TrimStart('\uFEFF');
        }

        private static bool HasUtf8Bom(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            return bytes.Length >= 3 &&
                   bytes[0] == 0xEF &&
                   bytes[1] == 0xBB &&
                   bytes[2] == 0xBF;
        }

        private static void AssertSourceDoesNotContainReplacementCharacters(string path, string source)
        {
            Assert.That(source.IndexOf('\uFFFD'), Is.EqualTo(-1), $"{path} contains a replacement character.");
        }

        private sealed class CancelFileBrowserService : IFileBrowserService
        {
            public int OpenFilePanelCallCount { get; private set; }
            public string LastTitle { get; private set; }
            public string LastExtension { get; private set; }
            public bool LastMultiselect { get; private set; }

            public string[] OpenFilePanel(string title, string directory, string extension, bool multiselect)
            {
                OpenFilePanelCallCount++;
                LastTitle = title;
                LastExtension = extension;
                LastMultiselect = multiselect;
                return Array.Empty<string>();
            }
        }
    }
}
