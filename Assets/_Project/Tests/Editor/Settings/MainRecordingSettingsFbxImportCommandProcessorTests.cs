using System;
using System.IO;
using System.Reflection;
using Fbx2Vmd.Settings;
using NUnit.Framework;

namespace Tests.Editor.Settings
{
    public sealed class MainRecordingSettingsFbxImportCommandProcessorTests
    {
        private const string ProcessorTypeName =
            "Fbx2Vmd.Settings.MainRecordingSettingsFbxImportCommandProcessor, Assembly-CSharp";
        private const string ProcessorSourcePath =
            "Assets/_Project/Scripts/Settings/MainRecordingSettingsFbxImportCommandProcessor.cs";
        private const string RecordingSettingSourcePath =
            "Assets/_Project/Scripts/Settings/RecordingSetting.cs";

        [Test]
        public void Given_SameImportCommandTwice_When_Processing_Then_StartsOnceAndConsumesEachEnvelope()
        {
            Type processorType = Type.GetType(ProcessorTypeName);
            Assert.That(processorType, Is.Not.Null, ProcessorTypeName);
            object processor = Activator.CreateInstance(processorType, true);
            MethodInfo tryProcessMethod = processorType.GetMethod(
                "TryProcess",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(tryProcessMethod, Is.Not.Null);

            int fileExistsCount = 0;
            int startCount = 0;
            int persistCount = 0;
            Func<string, bool> fileExists = path =>
            {
                fileExistsCount++;
                return path == "D:/motion/sample.fbx";
            };
            Func<string, bool> tryStartFbxImport = path =>
            {
                startCount++;
                return path == "D:/motion/sample.fbx";
            };
            Action<MainRecordingSettingsDocument> persistConsumedDocument = document =>
            {
                persistCount++;
                Assert.That(document.pendingCommand.commandId, Is.EqualTo(string.Empty));
                Assert.That(document.pendingCommand.action, Is.EqualTo(string.Empty));
                Assert.That(document.pendingCommand.fbxPath, Is.EqualTo(string.Empty));
            };

            var document = new MainRecordingSettingsDocument();
            SetImportCommand(document, "cmd-repeat", " D:/motion/sample.fbx ");
            bool firstHandled = InvokeTryProcess(
                processor,
                tryProcessMethod,
                document,
                true,
                false,
                fileExists,
                tryStartFbxImport,
                persistConsumedDocument,
                out MainRecordingSettingsActionResult firstResult);

            SetImportCommand(document, "cmd-repeat", "D:/motion/sample.fbx");
            bool secondHandled = InvokeTryProcess(
                processor,
                tryProcessMethod,
                document,
                true,
                false,
                fileExists,
                tryStartFbxImport,
                persistConsumedDocument,
                out MainRecordingSettingsActionResult secondResult);

            Assert.That(firstHandled, Is.True);
            Assert.That(secondHandled, Is.True);
            Assert.That(firstResult.Succeeded, Is.True);
            Assert.That(secondResult.Succeeded, Is.True);
            Assert.That(secondResult.UserMessage, Does.Contain("이미 처리"));
            Assert.That(fileExistsCount, Is.EqualTo(1));
            Assert.That(startCount, Is.EqualTo(1));
            Assert.That(persistCount, Is.EqualTo(2));
        }

        [Test]
        public void Given_CommandProcessor_When_InspectingOwnership_Then_RecordingSettingOnlyComposesDependencies()
        {
            Assert.That(File.Exists(ProcessorSourcePath), Is.True, ProcessorSourcePath);
            string processorSource = File.ReadAllText(ProcessorSourcePath);
            string recordingSettingSource = File.ReadAllText(RecordingSettingSourcePath);

            Assert.That(processorSource,
                Does.Contain("internal sealed class MainRecordingSettingsFbxImportCommandProcessor"));
            Assert.That(processorSource, Does.Contain("bool TryProcess("));
            Assert.That(processorSource, Does.Not.Contain("UnityEngine"));
            Assert.That(processorSource, Does.Not.Contain("System.IO"));
            Assert.That(processorSource, Does.Not.Contain("MainRecordingSettingsStore"));
            Assert.That(processorSource, Does.Not.Contain("using Fbx2Vmd.FBXImporter"));

            Assert.That(recordingSettingSource,
                Does.Not.Contain("private bool TryApplyPendingSharedSettingsCommand("));
            Assert.That(recordingSettingSource,
                Does.Not.Contain("private void ClearPendingSharedSettingsCommand("));
            Assert.That(recordingSettingSource, Does.Not.Contain("File.Exists(commandFbxPath)"));
            Assert.That(recordingSettingSource, Does.Not.Contain("lastHandledSharedSettingsCommandId"));
            Assert.That(recordingSettingSource, Does.Not.Contain("lastAppliedSharedSettingsFbxPath"));
            Assert.That(recordingSettingSource,
                Does.Contain("MainRecordingSettingsFbxImportCommandProcessor"));
            Assert.That(recordingSettingSource,
                Does.Contain("PersistConsumedSharedSettingsDocumentQuietly"));
        }

        private static bool InvokeTryProcess(
            object processor,
            MethodInfo tryProcessMethod,
            MainRecordingSettingsDocument document,
            bool shouldStartFbxImport,
            bool shouldClearSkippedCommand,
            Func<string, bool> fileExists,
            Func<string, bool> tryStartFbxImport,
            Action<MainRecordingSettingsDocument> persistConsumedDocument,
            out MainRecordingSettingsActionResult result)
        {
            object[] arguments =
            {
                document,
                shouldStartFbxImport,
                shouldClearSkippedCommand,
                fileExists,
                tryStartFbxImport,
                persistConsumedDocument,
                default(MainRecordingSettingsActionResult),
            };
            bool handled = (bool)tryProcessMethod.Invoke(processor, arguments);
            result = (MainRecordingSettingsActionResult)arguments[6];
            return handled;
        }

        private static void SetImportCommand(
            MainRecordingSettingsDocument document,
            string commandId,
            string fbxPath)
        {
            document.pendingCommand = new MainRecordingSettingsCommandEnvelope
            {
                commandId = commandId,
                action = MainRecordingSettingsCommandEnvelope.ImportFbxAction,
                fbxPath = fbxPath,
                requestedAtUtc = DateTime.UtcNow.ToString("O"),
            };
        }
    }
}
