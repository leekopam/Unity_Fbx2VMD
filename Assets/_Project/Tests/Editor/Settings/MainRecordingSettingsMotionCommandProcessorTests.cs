using System;
using System.IO;
using System.Reflection;
using Fbx2Vmd.Settings;
using NUnit.Framework;

namespace Tests.Editor.Settings
{
    public sealed class MainRecordingSettingsMotionCommandProcessorTests
    {
        private object processor;
        private MethodInfo process;

        [SetUp]
        public void SetUp()
        {
            Type type = Type.GetType("Fbx2Vmd.Settings.MainRecordingSettingsMotionCommandProcessor, Assembly-CSharp");
            Assert.That(type, Is.Not.Null);
            processor = Activator.CreateInstance(type, true);
            process = type.GetMethod("TryProcess", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        [TestCase("PlayImportedMotion")]
        [TestCase("PauseImportedMotion")]
        [TestCase("StopImportedMotion")]
        [TestCase("EnableNativeSkinningCorrection")]
        [TestCase("DisableNativeSkinningCorrection")]
        public void AcceptedCommand_RepeatedEnvelope_ExecutesOnce(string action)
        {
            var document = CreateDocument(action);
            int calls = 0;
            Func<string, bool> execute = received => { Assert.That(received, Is.EqualTo(action)); calls++; return true; };
            Assert.That(Process(document, execute), Is.True);
            Assert.That(document.pendingCommand.IsPending(), Is.False);
            Assert.That(document.lastCommandResult.state, Is.EqualTo("success"));
            document.pendingCommand = CreateDocument(action).pendingCommand;
            Process(document, execute);
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase("PlayImportedMotion")]
        [TestCase("PauseImportedMotion")]
        [TestCase("StopImportedMotion")]
        [TestCase("EnableNativeSkinningCorrection")]
        [TestCase("DisableNativeSkinningCorrection")]
        public void Recording_RejectsEveryMotionCommand(string action)
        {
            var document = CreateDocument(action);
            int calls = 0;
            Process(document, _ => { calls++; return true; }, isRecording: true);
            Assert.That(calls, Is.Zero);
            Assert.That(document.lastCommandResult.state, Is.EqualTo("error"));
            Assert.That(document.pendingCommand.IsPending(), Is.False);
        }

        [Test]
        public void InitialLoad_CancelsStaleCommand()
        {
            var document = CreateDocument("PlayImportedMotion");
            int calls = 0;
            Process(document, _ => { calls++; return true; }, shouldExecute: false);
            Assert.That(calls, Is.Zero);
            Assert.That(document.lastCommandResult.state, Is.EqualTo("error"));
            Assert.That(document.pendingCommand.IsPending(), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectedOrThrowingCommand_RetryDoesNotExecuteAgain(bool throws)
        {
            var document = CreateDocument("PlayImportedMotion");
            int calls = 0;
            Func<string, bool> execute = _ => { calls++; if (throws) throw new InvalidOperationException("실행 실패"); return false; };
            Process(document, execute);
            Assert.That(document.lastCommandResult.state, Is.EqualTo("error"));
            document.pendingCommand = CreateDocument("PlayImportedMotion").pendingCommand;
            Process(document, execute);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ResultSaveFails_NextPollRetriesWithoutExecutingAgain()
        {
            string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "settings.json");
            try
            {
                var store = new MainRecordingSettingsStore(path);
                store.Save(CreateDocument("PlayImportedMotion"));
                Type sessionType = Type.GetType("Fbx2Vmd.Settings.RecordingSharedSettingsFileSession, Assembly-CSharp");
                object session = Activator.CreateInstance(sessionType, BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { path }, null);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var document = (MainRecordingSettingsDocument)sessionType.GetMethod("LoadCurrent", flags).Invoke(session, null);
                int calls = 0;
                Func<string, bool> execute = _ => { calls++; return true; };
                Action<MainRecordingSettingsDocument> persist = current => sessionType.GetMethod("Save", flags).Invoke(session, new object[] { current });
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Assert.Throws<TargetInvocationException>(() => process.Invoke(processor,
                        new object[] { document, true, true, false, execute, persist, null }));
                }
                object[] loadArgs = { null };
                Assert.That(sessionType.GetMethod("TryLoadChanged", flags).Invoke(session, loadArgs), Is.EqualTo(true));
                process.Invoke(processor, new object[] { loadArgs[0], true, true, false, execute, persist, null });
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(store.LoadOrCreateDefault().pendingCommand.IsPending(), Is.False);
                Assert.That(store.LoadOrCreateDefault().lastCommandResult.state, Is.EqualTo("success"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void MotionStateWrite_DoesNotModifyOrHidePendingCommand()
        {
            string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "settings.json");
            try
            {
                var store = new MainRecordingSettingsStore(path);
                store.Save(CreateDocument("PlayImportedMotion"));
                string original = File.ReadAllText(path);
                Type sessionType = Type.GetType("Fbx2Vmd.Settings.RecordingSharedSettingsFileSession, Assembly-CSharp");
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                object session = Activator.CreateInstance(sessionType, flags, null, new object[] { path }, null);
                var state = MainRecordingSettingsState.Create("playing", DateTime.UtcNow);
                state.hasPreparedMotion = true;
                state.motionTimeSeconds = 2.5f;
                sessionType.GetMethod("WriteMotionState", flags).Invoke(session, new object[] { state });
                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
                var saved = UnityEngine.JsonUtility.FromJson<MainRecordingSettingsState>(File.ReadAllText(path + ".runtime-state.json"));
                Assert.That(saved.motionTimeSeconds, Is.EqualTo(2.5f));
                sessionType.GetMethod("WriteMotionState", flags).Invoke(session,
                    new object[] { MainRecordingSettingsState.Create("stopped", DateTime.UtcNow) });
                saved = UnityEngine.JsonUtility.FromJson<MainRecordingSettingsState>(File.ReadAllText(path + ".runtime-state.json"));
                Assert.That(saved.playMode, Is.EqualTo("stopped"));
                Assert.That(saved.hasPreparedMotion, Is.False);
                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void OtherCommand_IsNotConsumed()
        {
            var document = CreateDocument("ImportFbx");
            Assert.That(Process(document, _ => false), Is.False);
            Assert.That(document.pendingCommand.IsPending(), Is.True);
        }

        private bool Process(MainRecordingSettingsDocument document, Func<string, bool> execute,
            bool isRecording = false, bool shouldExecute = true)
        {
            bool persisted = false;
            Action<MainRecordingSettingsDocument> persist = current =>
            {
                persisted = true;
                Assert.That(current.pendingCommand.IsPending(), Is.False);
                Assert.That(current.lastCommandResult.commandId, Is.EqualTo("motion-test"));
            };
            object[] args = { document, shouldExecute, true, isRecording, execute, persist, null };
            bool handled = (bool)process.Invoke(processor, args);
            Assert.That(persisted, Is.EqualTo(handled));
            return handled;
        }

        private static MainRecordingSettingsDocument CreateDocument(string action)
        {
            return new MainRecordingSettingsDocument
            {
                pendingCommand = new MainRecordingSettingsCommandEnvelope { commandId = "motion-test", action = action }
            };
        }
    }
}
