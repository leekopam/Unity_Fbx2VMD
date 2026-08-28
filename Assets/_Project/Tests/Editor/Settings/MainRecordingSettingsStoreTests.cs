using Fbx2Vmd.Settings;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.Settings
{
    public class MainRecordingSettingsStoreTests
    {
        [Test]
        public void Given_DefaultSettingsDocument_When_InspectingSchema_Then_KeepsBuildReleaseContract()
        {
            var document = new MainRecordingSettingsDocument();

            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.updatedAtUtc, Is.EqualTo(string.Empty));
            Assert.That(document.fbxPath, Is.EqualTo(string.Empty));
            Assert.That(document.characterModelPath, Is.EqualTo(string.Empty));
            Assert.That(document.captureWidth, Is.EqualTo(1920));
            Assert.That(document.captureHeight, Is.EqualTo(1080));
            Assert.That(document.openSettingsOnStart, Is.True);
        }

        [Test]
        public void Given_DefaultSettingsDocument_When_InspectingCommandEnvelope_Then_KeepsImportCommandContractEmpty()
        {
            var document = new MainRecordingSettingsDocument();
            object command = GetFieldValue(document, "pendingCommand");

            Assert.That(command, Is.Not.Null);
            Assert.That(GetFieldValue<string>(command, "commandId"), Is.EqualTo(string.Empty));
            Assert.That(GetFieldValue<string>(command, "action"), Is.EqualTo(string.Empty));
            Assert.That(GetFieldValue<string>(command, "fbxPath"), Is.EqualTo(string.Empty));
            Assert.That(GetFieldValue<string>(command, "requestedAtUtc"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Given_DefaultSettingsDocument_When_InspectingRuntimeState_Then_PlayModeIsStopped()
        {
            var document = new MainRecordingSettingsDocument();
            object runtimeState = GetFieldValue(document, "runtimeState");

            Assert.That(runtimeState, Is.Not.Null);
            Assert.That(GetFieldValue<string>(runtimeState, "playMode"), Is.EqualTo("stopped"));
            Assert.That(GetFieldValue<string>(runtimeState, "updatedAtUtc"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Given_SettingsStore_When_InspectingNormalizationOwnership_Then_DelegatesToIoIndependentDocumentNormalizer()
        {
            const string storeSourcePath =
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsStore.cs";
            const string normalizerSourcePath =
                "Assets/_Project/Scripts/Settings/MainRecordingSettingsDocumentNormalizer.cs";

            Assert.That(File.Exists(normalizerSourcePath), Is.True, normalizerSourcePath);

            string storeSource = File.ReadAllText(storeSourcePath);
            string normalizerSource = File.ReadAllText(normalizerSourcePath);

            Assert.That(
                storeSource,
                Does.Contain("MainRecordingSettingsDocumentNormalizer.Normalize("));
            Assert.That(
                storeSource,
                Does.Not.Contain("private static MainRecordingSettingsDocument NormalizeDocument("));
            Assert.That(normalizerSource, Does.Not.Contain("File."));
            Assert.That(normalizerSource, Does.Not.Contain("JsonUtility"));
            Assert.That(normalizerSource, Does.Not.Contain("DateTime.UtcNow"));
            Assert.That(normalizerSource, Does.Not.Contain("UnityEngine"));
            Assert.That(normalizerSource, Does.Not.Contain("interface "));
        }

        [Test]
        public void Given_InvalidSettingsDocument_When_Normalizing_Then_RestoresEveryDocumentInvariant()
        {
            var document = new MainRecordingSettingsDocument
            {
                schemaVersion = 0,
                updatedAtUtc = null,
                fbxPath = null,
                characterModelPath = null,
                captureWidth = 0,
                captureHeight = -1,
                openSettingsOnStart = false,
                runtimeState = new MainRecordingSettingsState
                {
                    playMode = "paused",
                    updatedAtUtc = "invalid",
                },
                pendingCommand = new MainRecordingSettingsCommandEnvelope
                {
                    commandId = null,
                    action = null,
                    fbxPath = null,
                    requestedAtUtc = null,
                },
            };

            MainRecordingSettingsDocument normalized = NormalizeDocument(document);

            Assert.That(normalized, Is.SameAs(document));
            Assert.That(normalized.schemaVersion, Is.EqualTo(1));
            Assert.That(normalized.captureWidth, Is.EqualTo(1920));
            Assert.That(normalized.captureHeight, Is.EqualTo(1080));
            Assert.That(normalized.updatedAtUtc, Is.EqualTo(string.Empty));
            Assert.That(normalized.fbxPath, Is.EqualTo(string.Empty));
            Assert.That(normalized.characterModelPath, Is.EqualTo(string.Empty));
            Assert.That(normalized.openSettingsOnStart, Is.False);
            Assert.That(normalized.runtimeState.playMode, Is.EqualTo(MainRecordingSettingsState.Stopped));
            Assert.That(normalized.runtimeState.updatedAtUtc, Is.EqualTo(string.Empty));
            Assert.That(normalized.pendingCommand.commandId, Is.EqualTo(string.Empty));
            Assert.That(normalized.pendingCommand.action, Is.EqualTo(string.Empty));
            Assert.That(normalized.pendingCommand.fbxPath, Is.EqualTo(string.Empty));
            Assert.That(normalized.pendingCommand.requestedAtUtc, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Given_MissingNestedSettings_When_Normalizing_Then_CreatesDefaultNestedObjects()
        {
            var missingNestedDocument = new MainRecordingSettingsDocument
            {
                runtimeState = null,
                pendingCommand = null,
            };
            MainRecordingSettingsDocument normalized = NormalizeDocument(missingNestedDocument);

            Assert.That(normalized, Is.SameAs(missingNestedDocument));
            Assert.That(normalized.runtimeState, Is.Not.Null);
            Assert.That(normalized.pendingCommand, Is.Not.Null);
        }

        [Test]
        public void Given_NullSettingsDocument_When_Normalizing_Then_ReturnsCompleteDefault()
        {
            MainRecordingSettingsDocument normalized = NormalizeDocument(null);

            Assert.That(normalized, Is.Not.Null);
            Assert.That(normalized.schemaVersion, Is.EqualTo(1));
            Assert.That(normalized.captureWidth, Is.EqualTo(1920));
            Assert.That(normalized.captureHeight, Is.EqualTo(1080));
            Assert.That(normalized.runtimeState, Is.Not.Null);
            Assert.That(normalized.pendingCommand, Is.Not.Null);
        }

        [Test]
        public void Given_NoOverride_When_ResolvingSettingsPath_Then_UsesStableLocalAppDataJsonFile()
        {
            string localAppData = Path.Combine("C:", "Users", "Tester", "AppData", "Local");

            string path = MainRecordingSettingsPathResolver.ResolveSettingsFilePath(
                localAppDataRoot: localAppData,
                persistentDataRoot: Path.Combine("Fallback", "Persistent"),
                readProcessEnvironment: false);

            Assert.That(path, Is.EqualTo(Path.Combine(
                localAppData,
                "Unity_Fbx2VMD",
                "MainRecordingSettings",
                "main-recording-settings.json")));
        }

        [Test]
        public void Given_Overrides_When_ResolvingSettingsPath_Then_ExplicitPathWinsOverEnvironmentPath()
        {
            string explicitPath = Path.Combine("D:", "settings", "explicit.json");
            string environmentPath = Path.Combine("D:", "settings", "environment.json");

            Assert.That(
                MainRecordingSettingsPathResolver.ResolveSettingsFilePath(
                    explicitPath,
                    environmentPath,
                    localAppDataRoot: Path.Combine("D:", "local"),
                    persistentDataRoot: Path.Combine("D:", "persistent"),
                    readProcessEnvironment: false),
                Is.EqualTo(explicitPath));

            Assert.That(
                MainRecordingSettingsPathResolver.ResolveSettingsFilePath(
                    environmentOverridePath: environmentPath,
                    localAppDataRoot: Path.Combine("D:", "local"),
                    persistentDataRoot: Path.Combine("D:", "persistent"),
                    readProcessEnvironment: false),
                Is.EqualTo(environmentPath));
        }

        [Test]
        public void Given_MissingSettingsFile_When_Loading_Then_ReturnsDefaultDocumentWithoutCreatingFile()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "missing", "settings.json");

            var store = new MainRecordingSettingsStore(path);
            MainRecordingSettingsDocument document = store.LoadOrCreateDefault();

            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.captureWidth, Is.EqualTo(1920));
            Assert.That(document.captureHeight, Is.EqualTo(1080));
            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public void Given_MissingSettingsFile_When_ResolvingLastWriteTime_Then_ReturnsMinimumValue()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "missing", "settings.json");
            var store = new MainRecordingSettingsStore(path);

            DateTime writeTimeUtc = ResolveLastWriteTimeUtc(store);

            Assert.That(writeTimeUtc, Is.EqualTo(DateTime.MinValue));
        }

        [Test]
        public void Given_ExistingSettingsFile_When_ResolvingLastWriteTime_Then_ReturnsFileTimestamp()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "main-recording-settings.json");
            File.WriteAllText(path, "{}");
            DateTime expectedWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            var store = new MainRecordingSettingsStore(path);

            DateTime writeTimeUtc = ResolveLastWriteTimeUtc(store);

            Assert.That(writeTimeUtc, Is.EqualTo(expectedWriteTimeUtc));
        }

        [Test]
        public void Given_SettingsDocument_When_SavingAndLoading_Then_RoundTripsJsonAndCreatesFolder()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "nested", "main-recording-settings.json");
            var store = new MainRecordingSettingsStore(path);
            var document = new MainRecordingSettingsDocument
            {
                fbxPath = "D:/motions/satisfaction_2.fbx",
                characterModelPath = "D:/models/yyb.prefab",
                captureWidth = 3840,
                captureHeight = 2160,
                openSettingsOnStart = false,
            };

            store.Save(document);
            MainRecordingSettingsDocument loaded = store.LoadOrCreateDefault();

            Assert.That(Directory.Exists(Path.GetDirectoryName(path)), Is.True);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.ReadAllText(path), Does.Contain("\"schemaVersion\""));
            Assert.That(loaded.schemaVersion, Is.EqualTo(1));
            Assert.That(loaded.fbxPath, Is.EqualTo(document.fbxPath));
            Assert.That(loaded.characterModelPath, Is.EqualTo(document.characterModelPath));
            Assert.That(loaded.captureWidth, Is.EqualTo(3840));
            Assert.That(loaded.captureHeight, Is.EqualTo(2160));
            Assert.That(loaded.openSettingsOnStart, Is.False);
            Assert.That(loaded.updatedAtUtc, Is.Not.Empty);
        }

        [Test]
        public void Given_SettingsFileWithInvalidRuntimeState_When_Loading_Then_NormalizesPlayModeToStopped()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "main-recording-settings.json");
            File.WriteAllText(path, "{\"schemaVersion\":1,\"runtimeState\":{\"playMode\":\"paused\",\"updatedAtUtc\":100}}");

            var store = new MainRecordingSettingsStore(path);
            MainRecordingSettingsDocument loaded = store.LoadOrCreateDefault();
            object runtimeState = GetFieldValue(loaded, "runtimeState");

            Assert.That(GetFieldValue<string>(runtimeState, "playMode"), Is.EqualTo("stopped"));
            Assert.That(GetFieldValue<string>(runtimeState, "updatedAtUtc"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Given_CorruptSettingsFile_When_Loading_Then_BacksUpCorruptFileAndReturnsDefaultDocument()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "main-recording-settings.json");
            File.WriteAllText(path, "{ corrupt json");

            var store = new MainRecordingSettingsStore(path);
            MainRecordingSettingsDocument loaded = store.LoadOrCreateDefault();
            string[] backups = Directory.GetFiles(folder, "main-recording-settings.json.corrupt-*");

            Assert.That(loaded.schemaVersion, Is.EqualTo(1));
            Assert.That(loaded.captureWidth, Is.EqualTo(1920));
            Assert.That(File.Exists(path), Is.False);
            Assert.That(backups, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(backups[0]), Is.EqualTo("{ corrupt json"));
        }

        [Test]
        public void Given_ExistingSettingsFile_When_Saving_Then_ReplacesFileWithoutLeavingTempFile()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "main-recording-settings.json");
            File.WriteAllText(path, "{\"schemaVersion\":1,\"fbxPath\":\"old\"}");

            var store = new MainRecordingSettingsStore(path);
            store.Save(new MainRecordingSettingsDocument { fbxPath = "new" });

            Assert.That(File.ReadAllText(path), Does.Contain("\"new\""));
            Assert.That(Directory.GetFiles(folder, "main-recording-settings.json.tmp-*"), Is.Empty);
        }

        private static string CreateTempFolder()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "UnityFbx2Vmd-MainRecordingSettingsStoreTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static DateTime ResolveLastWriteTimeUtc(MainRecordingSettingsStore store)
        {
            MethodInfo method = typeof(MainRecordingSettingsStore).GetMethod(
                "ResolveLastWriteTimeUtc",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "MainRecordingSettingsStore must own settings file timestamp resolution.");
            return (DateTime)method.Invoke(store, null);
        }

        private static MainRecordingSettingsDocument NormalizeDocument(
            MainRecordingSettingsDocument document)
        {
            Type normalizerType = typeof(MainRecordingSettingsStore).Assembly.GetType(
                "Fbx2Vmd.Settings.MainRecordingSettingsDocumentNormalizer");
            Assert.That(normalizerType, Is.Not.Null);

            MethodInfo normalizeMethod = normalizerType.GetMethod(
                "Normalize",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(normalizeMethod, Is.Not.Null);

            return (MainRecordingSettingsDocument)normalizeMethod.Invoke(
                null,
                new object[] { document });
        }

        private static object GetFieldValue(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null, $"{instance.GetType().FullName}.{fieldName} must exist.");
            return field.GetValue(instance);
        }

        private static T GetFieldValue<T>(object instance, string fieldName)
        {
            return (T)GetFieldValue(instance, fieldName);
        }
    }
}
