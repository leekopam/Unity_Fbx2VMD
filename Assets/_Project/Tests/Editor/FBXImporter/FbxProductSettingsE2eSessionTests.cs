using System;
using System.IO;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.Settings;
using Fbx2Vmd.CharacterLibrary;
using NUnit.Framework;
using UnityEditor;

namespace Tests.Editor.FBXImporter
{
    public sealed class FbxProductSettingsE2eSessionTests
    {
        private const string LaunchKey = "Fbx2Vmd.MainRecordingSettings.EditorLaunched";
        private string _root;
        private string _runId;
        private string _settingsEnvironment;
        private string _libraryEnvironment;
        private bool _reloadEnabled;
        private EnterPlayModeOptions _reloadOptions;
        private bool _launched;
        private string _journal;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), nameof(FbxProductSettingsE2eSessionTests), Guid.NewGuid().ToString("N"));
            _runId = Guid.NewGuid().ToString("D");
            _settingsEnvironment = Environment.GetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName);
            _libraryEnvironment = Environment.GetEnvironmentVariable(CharacterLibraryPathResolver.EnvironmentVariableName);
            _reloadEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _reloadOptions = EditorSettings.enterPlayModeOptions;
            _launched = SessionState.GetBool(LaunchKey, false);
            _journal = SessionState.GetString("Fbx2Vmd.ProductSettingsE2e.Journal", string.Empty);
            if (!string.IsNullOrEmpty(_journal))
                Assert.Ignore("실제 시험 세션의 복원이 끝난 뒤 실행해야 합니다.");
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "original-settings.json"), "{\"captureWidth\":3840}");
            File.WriteAllText(Path.Combine(_root, "original-library.json"), "{\"entries\":[]}");
            Environment.SetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName, Path.Combine(_root, "original-settings.json"));
            Environment.SetEnvironmentVariable(CharacterLibraryPathResolver.EnvironmentVariableName, Path.Combine(_root, "original-library.json"));
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName, _settingsEnvironment);
            Environment.SetEnvironmentVariable(CharacterLibraryPathResolver.EnvironmentVariableName, _libraryEnvironment);
            EditorSettings.enterPlayModeOptionsEnabled = _reloadEnabled;
            EditorSettings.enterPlayModeOptions = _reloadOptions;
            SessionState.SetBool(LaunchKey, _launched);
            SessionState.SetString("Fbx2Vmd.ProductSettingsE2e.Journal", _journal);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void EnvironmentChanges_CancelOnlyPendingCompanionStartup()
        {
            Type launcher = Type.GetType("Fbx2Vmd.Settings.EditorTools.MainRecordingSettingsCompanionLauncher, Assembly-CSharp-Editor", true);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(launcher.TypeHandle);
            var startup = (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                typeof(EditorApplication.CallbackFunction), launcher.GetMethod("TryAutoOpenOnEditorStartup", BindingFlags.Static | BindingFlags.NonPublic));
            EditorApplication.CallbackFunction unrelated = () => { };
            EditorApplication.delayCall -= startup;
            EditorApplication.delayCall += startup;
            EditorApplication.delayCall += unrelated;
            try
            {
                Invoke("Prepare", _runId);
                Assert.That(Array.IndexOf(EditorApplication.delayCall.GetInvocationList(), startup), Is.EqualTo(-1));
                Assert.That(Array.IndexOf(EditorApplication.delayCall.GetInvocationList(), unrelated), Is.GreaterThanOrEqualTo(0));
                // Play 종료 재로드가 초기화 콜백을 다시 등록한 상황을 재현함.
                EditorApplication.delayCall += startup;
                Invoke("Restore", _runId);
                Assert.That(Array.IndexOf(EditorApplication.delayCall.GetInvocationList(), startup), Is.EqualTo(-1));
                Assert.That(Array.IndexOf(EditorApplication.delayCall.GetInvocationList(), unrelated), Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                EditorApplication.delayCall -= startup;
                EditorApplication.delayCall -= unrelated;
            }
        }

        [Test]
        public void PrepareAndRestore_PreserveOriginalFilesAndAllowSameRunRetry()
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            string manifest = Invoke("Prepare", _runId);
            Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.False);
            string settings = Environment.GetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName);
            Assert.That(File.ReadAllText(settings), Does.Contain("\"openSettingsOnStart\": false"));
            File.Delete(manifest);
            Assert.That(Invoke("Prepare", _runId), Is.EqualTo(manifest));
            Assert.That(File.Exists(manifest), Is.True);
            Assert.Throws<TargetInvocationException>(() => Invoke("Prepare", Guid.NewGuid().ToString("D")));
            Assert.That(Invoke("Restore", _runId), Is.EqualTo(manifest));
            Assert.That(Invoke("Restore", _runId), Is.EqualTo(manifest));
            Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.True);
            Assert.That(Environment.GetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName),
                Is.EqualTo(Path.Combine(_root, "original-settings.json")));
            Assert.That(File.ReadAllText(Path.Combine(_root, "original-settings.json")), Is.EqualTo("{\"captureWidth\":3840}"));
            Assert.That(File.ReadAllText(Path.Combine(_root, "original-library.json")), Is.EqualTo("{\"entries\":[]}"));
        }

        [Test]
        public void Restore_LeavesExternalEnvironmentChangeAndStillRestoresOtherState()
        {
            Invoke("Prepare", _runId);
            string applied = Environment.GetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName);
            string external = Path.Combine(_root, "external.json");
            Environment.SetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName, external);
            Assert.Throws<TargetInvocationException>(() => Invoke("Restore", _runId));
            Assert.That(Environment.GetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName), Is.EqualTo(external));
            Assert.That(Environment.GetEnvironmentVariable(CharacterLibraryPathResolver.EnvironmentVariableName),
                Is.EqualTo(Path.Combine(_root, "original-library.json")));
            Environment.SetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName, applied);
            Invoke("Restore", _runId);
        }

        [Test]
        public void Restore_LeavesExternallyChangedReloadOptions()
        {
            Invoke("Prepare", _runId);
            var external = (EnterPlayModeOptions)((int)_reloadOptions ^ 1);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = external;
            Assert.Throws<TargetInvocationException>(() => Invoke("Restore", _runId));
            Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(external));
            Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.True);
            EditorSettings.enterPlayModeOptions = _reloadOptions;
            Invoke("Restore", _runId);
        }

        [Test]
        public void Restore_LeavesExternallyEnabledReloadOptions()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            Invoke("Prepare", _runId);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            Assert.Throws<TargetInvocationException>(() => Invoke("Restore", _runId));
            Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.True);
            EditorSettings.enterPlayModeOptionsEnabled = false;
            Invoke("Restore", _runId);
        }

        [Test]
        public void Prepare_RejectsPendingUserCommandBeforeChangingEnvironment()
        {
            string settings = Path.Combine(_root, "original-settings.json");
            File.WriteAllText(settings, "{\"pendingCommand\":{\"commandId\":\"pending-user-command\"}}");
            Assert.Throws<TargetInvocationException>(() => Invoke("Prepare", _runId));
            Assert.That(Environment.GetEnvironmentVariable(MainRecordingSettingsPathResolver.EnvironmentVariableName), Is.EqualTo(settings));
            Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.EqualTo(_reloadEnabled));
        }

        [Test]
        public void Restore_ReportsChangedOriginalFileWithoutOverwritingIt()
        {
            Invoke("Prepare", _runId);
            string settings = Path.Combine(_root, "original-settings.json");
            File.WriteAllText(settings, "changed");
            Assert.Throws<TargetInvocationException>(() => Invoke("Restore", _runId));
            Assert.That(File.ReadAllText(settings), Is.EqualTo("changed"));
            Assert.Throws<TargetInvocationException>(() => Invoke("Prepare", Guid.NewGuid().ToString("D")));
        }

        private string Invoke(string method, string runId)
        {
            Type type = typeof(FbxPlaybackSmokeRunner).Assembly.GetType("Fbx2Vmd.FBXImporter.FbxProductSettingsE2eSession", true);
            return (string)type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { _root, runId });
        }
    }
}
