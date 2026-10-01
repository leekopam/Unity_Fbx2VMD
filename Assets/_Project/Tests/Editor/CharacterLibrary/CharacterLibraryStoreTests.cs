using Fbx2Vmd.CharacterLibrary;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.CharacterLibrary
{
    public class CharacterLibraryStoreTests
    {
        [Test]
        public void Given_DefaultDocument_When_InspectingSchema_Then_KeepsContract()
        {
            var document = new CharacterLibraryDocument();

            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.updatedAtUtc, Is.EqualTo(string.Empty));
            Assert.That(document.activeCharacterId, Is.EqualTo(string.Empty));
            Assert.That(document.entries, Is.Not.Null);
            Assert.That(document.entries, Is.Empty);
        }

        [Test]
        public void Given_NullDocument_When_Normalizing_Then_ReturnsCompleteDefault()
        {
            CharacterLibraryDocument normalized = NormalizeDocument(null);

            Assert.That(normalized, Is.Not.Null);
            Assert.That(normalized.schemaVersion, Is.EqualTo(1));
            Assert.That(normalized.entries, Is.Not.Null);
        }

        [Test]
        public void Given_InvalidDocument_When_Normalizing_Then_RestoresInvariants()
        {
            var document = new CharacterLibraryDocument
            {
                schemaVersion = 0,
                updatedAtUtc = null,
                activeCharacterId = null,
                entries = null,
            };

            CharacterLibraryDocument normalized = NormalizeDocument(document);

            Assert.That(normalized, Is.SameAs(document));
            Assert.That(normalized.schemaVersion, Is.EqualTo(1));
            Assert.That(normalized.updatedAtUtc, Is.EqualTo(string.Empty));
            Assert.That(normalized.activeCharacterId, Is.EqualTo(string.Empty));
            Assert.That(normalized.entries, Is.Not.Null);
        }

        [Test]
        public void Given_EntryWithNullFields_When_Normalizing_Then_FillsDefaults()
        {
            var document = new CharacterLibraryDocument
            {
                entries = new System.Collections.Generic.List<CharacterLibraryEntry>
                {
                    new CharacterLibraryEntry
                    {
                        id = null,
                        displayName = null,
                        sourcePath = null,
                        status = "bogus",
                        meta = null,
                        compatibility = null,
                        presetIds = null,
                    },
                },
            };

            CharacterLibraryDocument normalized = NormalizeDocument(document);
            CharacterLibraryEntry entry = normalized.entries[0];

            Assert.That(entry.id, Is.Not.Empty, "빈 id는 정규화 시 새 GUID가 부여된다");
            Assert.That(entry.displayName, Is.EqualTo(string.Empty));
            Assert.That(entry.status, Is.EqualTo(CharacterLibraryEntryStatus.Ready));
            Assert.That(entry.meta, Is.Not.Null);
            Assert.That(entry.compatibility, Is.Not.Null);
            Assert.That(entry.presetIds, Is.Not.Null);
        }

        [Test]
        public void Given_DuplicateEntryIds_When_Normalizing_Then_KeepsFirstOnly()
        {
            var document = new CharacterLibraryDocument
            {
                entries = new System.Collections.Generic.List<CharacterLibraryEntry>
                {
                    new CharacterLibraryEntry { id = "aaa", displayName = "first" },
                    new CharacterLibraryEntry { id = "bbb", displayName = "second" },
                    new CharacterLibraryEntry { id = "aaa", displayName = "dupe" },
                },
            };

            CharacterLibraryDocument normalized = NormalizeDocument(document);

            Assert.That(normalized.entries.Count, Is.EqualTo(2));
            Assert.That(normalized.entries[0].displayName, Is.EqualTo("first"));
            Assert.That(normalized.entries[1].displayName, Is.EqualTo("second"));
        }

        [Test]
        public void Given_NoOverride_When_ResolvingLibraryPath_Then_UsesStableLocalAppDataJsonFile()
        {
            string localAppData = Path.Combine("C:", "Users", "Tester", "AppData", "Local");

            string path = CharacterLibraryPathResolver.ResolveLibraryFilePath(
                localAppDataRoot: localAppData,
                persistentDataRoot: Path.Combine("Fallback", "Persistent"),
                readProcessEnvironment: false);

            Assert.That(path, Is.EqualTo(Path.Combine(
                localAppData,
                "Unity_Fbx2VMD",
                "CharacterLibrary",
                "characters.json")));
        }

        [Test]
        public void Given_Overrides_When_ResolvingLibraryPath_Then_ExplicitPathWins()
        {
            string explicitPath = Path.Combine("D:", "library", "explicit.json");
            string environmentPath = Path.Combine("D:", "library", "environment.json");

            Assert.That(
                CharacterLibraryPathResolver.ResolveLibraryFilePath(
                    explicitPath,
                    environmentPath,
                    localAppDataRoot: Path.Combine("D:", "local"),
                    persistentDataRoot: Path.Combine("D:", "persistent"),
                    readProcessEnvironment: false),
                Is.EqualTo(explicitPath));

            Assert.That(
                CharacterLibraryPathResolver.ResolveLibraryFilePath(
                    environmentOverridePath: environmentPath,
                    localAppDataRoot: Path.Combine("D:", "local"),
                    persistentDataRoot: Path.Combine("D:", "persistent"),
                    readProcessEnvironment: false),
                Is.EqualTo(environmentPath));
        }

        [Test]
        public void Given_FileNameWithTraversal_When_ResolvingThumbnailPath_Then_RejectsIt()
        {
            string libraryFile = Path.Combine("C:", "lib", "characters.json");

            Assert.That(
                CharacterLibraryPathResolver.ResolveThumbnailPath(libraryFile, "..\\evil.png"),
                Is.EqualTo(string.Empty));
            Assert.That(
                CharacterLibraryPathResolver.ResolveThumbnailPath(libraryFile, "sub\\a.png"),
                Is.EqualTo(string.Empty));
            Assert.That(
                CharacterLibraryPathResolver.ResolveThumbnailPath(libraryFile, "ok.png"),
                Is.EqualTo(Path.Combine("C:", "lib", "thumbnails", "ok.png")));
        }

        [Test]
        public void Given_MissingLibraryFile_When_Loading_Then_ReturnsDefaultWithoutCreatingFile()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "missing", "characters.json");

            var store = new CharacterLibraryStore(path);
            CharacterLibraryDocument document = store.LoadOrCreateDefault();

            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.entries, Is.Empty);
            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public void Given_LibraryDocument_When_SavingAndLoading_Then_RoundTripsJsonAndCreatesFolder()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "nested", "characters.json");
            var store = new CharacterLibraryStore(path);
            var document = new CharacterLibraryDocument
            {
                activeCharacterId = "abc",
            };
            document.entries.Add(new CharacterLibraryEntry
            {
                id = "abc",
                displayName = "테스트 캐릭터",
                sourcePath = "D:\\models\\test.vrm",
                contentHash = "sha256:0123456789abcdef",
                vrmSpecVersion = "0.0",
                meta = new CharacterLibraryEntryMeta { title = "테스트", author = "작가" },
            });

            store.Save(document);
            CharacterLibraryDocument loaded = store.LoadOrCreateDefault();

            Assert.That(File.Exists(path), Is.True);
            Assert.That(loaded.activeCharacterId, Is.EqualTo("abc"));
            Assert.That(loaded.entries.Count, Is.EqualTo(1));
            Assert.That(loaded.entries[0].displayName, Is.EqualTo("테스트 캐릭터"));
            Assert.That(loaded.entries[0].meta.author, Is.EqualTo("작가"));
            Assert.That(loaded.updatedAtUtc, Is.Not.Empty);
        }

        [Test]
        public void Given_CorruptLibraryFile_When_Loading_Then_BacksUpAndReturnsDefault()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "characters.json");
            File.WriteAllText(path, "{ corrupt json");

            var store = new CharacterLibraryStore(path);
            CharacterLibraryDocument loaded = store.LoadOrCreateDefault();
            string[] backups = Directory.GetFiles(folder, "characters.json.corrupt-*");

            Assert.That(loaded.schemaVersion, Is.EqualTo(1));
            Assert.That(File.Exists(path), Is.False);
            Assert.That(backups, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(backups[0]), Is.EqualTo("{ corrupt json"));
        }

        [Test]
        public void Given_ValidJsonOfWrongType_When_Loading_Then_TreatsAsCorruptAndBacksUp()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "characters.json");
            // 문법은 유효하지만 문서 타입이 아닌 JSON도 손상으로 간주해 백업해야 한다.
            File.WriteAllText(path, "[]");

            var store = new CharacterLibraryStore(path);
            CharacterLibraryDocument loaded = store.LoadOrCreateDefault();

            Assert.That(loaded.schemaVersion, Is.EqualTo(1));
            Assert.That(File.Exists(path), Is.False);
            Assert.That(
                Directory.GetFiles(folder, "characters.json.corrupt-*"),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void Given_EmptyLibraryFile_When_Loading_Then_TreatsAsCorruptAndBacksUp()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "characters.json");
            File.WriteAllText(path, "   ");

            var store = new CharacterLibraryStore(path);
            CharacterLibraryDocument loaded = store.LoadOrCreateDefault();

            Assert.That(loaded.schemaVersion, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(folder, "characters.json.corrupt-*"),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void Given_ExistingLibraryFile_When_Saving_Then_ReplacesWithoutLeavingTempFile()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "characters.json");
            File.WriteAllText(path, "{\"schemaVersion\":1}");

            var store = new CharacterLibraryStore(path);
            store.Save(new CharacterLibraryDocument());

            Assert.That(File.ReadAllText(path), Does.Contain("\"schemaVersion\""));
            Assert.That(Directory.GetFiles(folder, "characters.json.tmp-*"), Is.Empty);
        }

        [Test]
        public void Given_SameContentDifferentPaths_When_Hashing_Then_IdenticalHash()
        {
            string folder = CreateTempFolder();
            string pathA = Path.Combine(folder, "a.vrm");
            string pathB = Path.Combine(folder, "b.vrm");
            File.WriteAllBytes(pathA, new byte[] { 1, 2, 3, 4, 5 });
            File.WriteAllBytes(pathB, new byte[] { 1, 2, 3, 4, 5 });

            string hashA = InvokeComputeContentHash(pathA);
            string hashB = InvokeComputeContentHash(pathB);

            Assert.That(hashA, Does.StartWith("sha256:"));
            Assert.That(hashA, Is.EqualTo(hashB));
        }

        [Test]
        public void Given_DifferentContent_When_Hashing_Then_DifferentHash()
        {
            string folder = CreateTempFolder();
            string pathA = Path.Combine(folder, "a.vrm");
            string pathB = Path.Combine(folder, "b.vrm");
            File.WriteAllBytes(pathA, new byte[] { 1, 2, 3 });
            File.WriteAllBytes(pathB, new byte[] { 9, 9, 9 });

            Assert.That(
                InvokeComputeContentHash(pathA),
                Is.Not.EqualTo(InvokeComputeContentHash(pathB)));
        }

        private static string CreateTempFolder()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "UnityFbx2Vmd-CharacterLibraryTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static CharacterLibraryDocument NormalizeDocument(CharacterLibraryDocument document)
        {
            Type normalizerType = typeof(CharacterLibraryStore).Assembly.GetType(
                "Fbx2Vmd.CharacterLibrary.CharacterLibraryDocumentNormalizer");
            Assert.That(normalizerType, Is.Not.Null);

            MethodInfo normalizeMethod = normalizerType.GetMethod(
                "Normalize",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(normalizeMethod, Is.Not.Null);

            return (CharacterLibraryDocument)normalizeMethod.Invoke(
                null, new object[] { document });
        }

        private static string InvokeComputeContentHash(string path)
        {
            Type hasherType = typeof(CharacterLibraryStore).Assembly.GetType(
                "Fbx2Vmd.CharacterLibrary.CharacterFileHasher");
            Assert.That(hasherType, Is.Not.Null);

            MethodInfo method = hasherType.GetMethod(
                "ComputeContentHash",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            return (string)method.Invoke(null, new object[] { path });
        }
    }
}
