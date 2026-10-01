using Fbx2Vmd.CharacterLibrary;
using NUnit.Framework;
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Editor.CharacterLibrary
{
    public class CharacterLibraryRegistryTests
    {
        [Test]
        public void Given_ValidVrmPath_When_Registering_Then_CreatesEntryWithStatAndMeta()
        {
            string folder = CreateTempFolder();
            string vrmPath = Path.Combine(folder, "miku.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1, 2, 3, 4 });
            var registry = CreateRegistry(folder);

            CharacterLibraryRegistry.RegisterResult result = registry.Register(vrmPath);

            Assert.That(result.success, Is.True, result.error);
            Assert.That(result.entry.id, Is.Not.Empty);
            Assert.That(result.entry.displayName, Is.EqualTo("테스트모델"));
            Assert.That(result.entry.contentHash, Does.StartWith("sha256:"));
            Assert.That(result.entry.fileSize, Is.EqualTo(4));
            Assert.That(result.entry.vrmSpecVersion, Is.EqualTo("0.0"));
            Assert.That(result.entry.status, Is.EqualTo(CharacterLibraryEntryStatus.Ready));
            Assert.That(registry.Entries.Count, Is.EqualTo(1));
        }

        [Test]
        public void Given_MissingFile_When_Registering_Then_ReturnsErrorWithoutEntry()
        {
            string folder = CreateTempFolder();
            var registry = CreateRegistry(folder);

            CharacterLibraryRegistry.RegisterResult result =
                registry.Register(Path.Combine(folder, "missing.vrm"));

            Assert.That(result.success, Is.False);
            Assert.That(result.error, Does.Contain("없습니다"));
            Assert.That(registry.Entries, Is.Empty);
        }

        [Test]
        public void Given_NonVrmFile_When_Registering_Then_RejectsExtension()
        {
            string folder = CreateTempFolder();
            string path = Path.Combine(folder, "model.fbx");
            File.WriteAllBytes(path, new byte[] { 1 });
            var registry = CreateRegistry(folder);

            CharacterLibraryRegistry.RegisterResult result = registry.Register(path);

            Assert.That(result.success, Is.False);
            Assert.That(result.error, Does.Contain(".vrm"));
        }

        [Test]
        public void Given_MetaReadFailure_When_Registering_Then_ReturnsErrorWithoutEntry()
        {
            string folder = CreateTempFolder();
            string vrmPath = Path.Combine(folder, "broken.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1, 2 });
            var registry = new CharacterLibraryRegistry(
                Path.Combine(folder, "characters.json"),
                _ => new VrmCharacterMetaResult { success = false, error = "VRM 1.0 미지원" });

            CharacterLibraryRegistry.RegisterResult result = registry.Register(vrmPath);

            Assert.That(result.success, Is.False);
            Assert.That(result.error, Is.EqualTo("VRM 1.0 미지원"));
            Assert.That(registry.Entries, Is.Empty);
        }

        [Test]
        public void Given_SamePathTwice_When_Registering_Then_RejectsDuplicate()
        {
            string folder = CreateTempFolder();
            string vrmPath = Path.Combine(folder, "miku.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1, 2, 3 });
            var registry = CreateRegistry(folder);
            registry.Register(vrmPath);

            CharacterLibraryRegistry.RegisterResult second = registry.Register(vrmPath);

            Assert.That(second.success, Is.False);
            Assert.That(second.error, Does.Contain("이미"));
            Assert.That(registry.Entries.Count, Is.EqualTo(1));
        }

        [Test]
        public void Given_SameContentDifferentPath_When_Registering_Then_RejectsDuplicateByHash()
        {
            string folder = CreateTempFolder();
            string pathA = Path.Combine(folder, "a.vrm");
            string pathB = Path.Combine(folder, "b.vrm");
            File.WriteAllBytes(pathA, new byte[] { 7, 8, 9 });
            File.WriteAllBytes(pathB, new byte[] { 7, 8, 9 });
            var registry = CreateRegistry(folder);
            registry.Register(pathA);

            CharacterLibraryRegistry.RegisterResult second = registry.Register(pathB);

            Assert.That(second.success, Is.False);
            Assert.That(second.error, Does.Contain("동일한 내용"));
            Assert.That(registry.Entries.Count, Is.EqualTo(1));
        }

        [Test]
        public void Given_RegisteredEntry_When_FileMoved_Then_StatusBecomesMissingAndRelinkRestores()
        {
            string folder = CreateTempFolder();
            string vrmPath = Path.Combine(folder, "miku.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1, 2, 3 });
            var registry = CreateRegistry(folder);
            CharacterLibraryEntry entry = registry.Register(vrmPath).entry;

            string movedPath = Path.Combine(folder, "sub", "miku.vrm");
            Directory.CreateDirectory(Path.GetDirectoryName(movedPath));
            File.Move(vrmPath, movedPath);

            registry.RefreshEntry(entry);
            Assert.That(entry.status, Is.EqualTo(CharacterLibraryEntryStatus.Missing));

            bool relinked = registry.TryRelink(entry, movedPath, out string error);
            Assert.That(relinked, Is.True, error);
            Assert.That(entry.status, Is.EqualTo(CharacterLibraryEntryStatus.Ready));
            Assert.That(entry.sourcePath, Is.EqualTo(Path.GetFullPath(movedPath)));
        }

        [Test]
        public void Given_RegisteredEntry_When_RelinkToDifferentContent_Then_Rejected()
        {
            string folder = CreateTempFolder();
            string vrmPath = Path.Combine(folder, "miku.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1, 2, 3 });
            var registry = CreateRegistry(folder);
            CharacterLibraryEntry entry = registry.Register(vrmPath).entry;

            string otherPath = Path.Combine(folder, "other.vrm");
            File.WriteAllBytes(otherPath, new byte[] { 9, 9, 9 });

            bool relinked = registry.TryRelink(entry, otherPath, out string error);

            Assert.That(relinked, Is.False);
            Assert.That(error, Does.Contain("다릅니다"));
        }

        [Test]
        public void Given_RegisteredEntry_When_FileContentChanges_Then_StatusBecomesChanged()
        {
            string folder = CreateTempFolder();
            string vrmPath = Path.Combine(folder, "miku.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1, 2, 3 });
            var registry = CreateRegistry(folder);
            CharacterLibraryEntry entry = registry.Register(vrmPath).entry;

            File.WriteAllBytes(vrmPath, new byte[] { 1, 2, 3, 4, 5 });
            registry.RefreshEntry(entry);

            Assert.That(entry.status, Is.EqualTo(CharacterLibraryEntryStatus.Changed));
            Assert.That(entry.fileSize, Is.EqualTo(5));
        }

        [Test]
        public void Given_RegisteredEntry_When_MarkUsed_Then_UpdatesLastUsedAndActive()
        {
            string folder = CreateTempFolder();
            string vrmPath = Path.Combine(folder, "miku.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1 });
            var registry = CreateRegistry(folder);
            CharacterLibraryEntry entry = registry.Register(vrmPath).entry;

            registry.MarkUsed(entry.id);

            Assert.That(entry.lastUsedAtUtc, Is.Not.Empty);
            Assert.That(registry.ActiveCharacterId, Is.EqualTo(entry.id));
        }

        [Test]
        public void Given_TwoEntries_When_RemovingActive_Then_ClearsActiveId()
        {
            string folder = CreateTempFolder();
            string pathA = Path.Combine(folder, "a.vrm");
            string pathB = Path.Combine(folder, "b.vrm");
            File.WriteAllBytes(pathA, new byte[] { 1 });
            File.WriteAllBytes(pathB, new byte[] { 2 });
            var registry = CreateRegistry(folder);
            CharacterLibraryEntry entryA = registry.Register(pathA).entry;
            registry.Register(pathB);
            registry.MarkUsed(entryA.id);

            Assert.That(registry.Remove(entryA.id), Is.True);
            Assert.That(registry.ActiveCharacterId, Is.EqualTo(string.Empty));
            Assert.That(registry.Entries.Count, Is.EqualTo(1));
        }

        [Test]
        public void Given_Registry_When_ReloadingFromDisk_Then_PreservesEntries()
        {
            string folder = CreateTempFolder();
            string libraryFile = Path.Combine(folder, "characters.json");
            string vrmPath = Path.Combine(folder, "miku.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1, 2, 3 });

            var first = CreateRegistry(folder, libraryFile);
            string id = first.Register(vrmPath).entry.id;

            var second = CreateRegistry(folder, libraryFile);

            Assert.That(second.Entries.Count, Is.EqualTo(1));
            Assert.That(second.Entries[0].id, Is.EqualTo(id));
        }

        [Test]
        public void Given_RegisteredEntry_When_OnlyTimestampChanges_Then_StaysReady()
        {
            string folder = CreateTempFolder();
            string vrmPath = Path.Combine(folder, "miku.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1, 2, 3 });
            var registry = CreateRegistry(folder);
            CharacterLibraryEntry entry = registry.Register(vrmPath).entry;

            // 내용은 같고 mtime만 다른 터치는 Changed로 표시하지 않는다.
            File.SetLastWriteTimeUtc(vrmPath, DateTime.UtcNow.AddMinutes(5));
            registry.RefreshEntry(entry);

            Assert.That(entry.status, Is.EqualTo(CharacterLibraryEntryStatus.Ready));
        }

        [Test]
        public void Given_ErrorEntry_When_FileReadable_Then_RecoversToReady()
        {
            string folder = CreateTempFolder();
            string vrmPath = Path.Combine(folder, "miku.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1, 2, 3 });
            var registry = CreateRegistry(folder);
            CharacterLibraryEntry entry = registry.Register(vrmPath).entry;
            entry.status = CharacterLibraryEntryStatus.Error;
            entry.lastError = "이전 오류";

            registry.RefreshEntry(entry);

            Assert.That(entry.status, Is.EqualTo(CharacterLibraryEntryStatus.Ready));
            Assert.That(entry.lastError, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Given_EntryWithThumbnail_When_Removing_Then_DeletesThumbnailFile()
        {
            string folder = CreateTempFolder();
            string vrmPath = Path.Combine(folder, "miku.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1, 2, 3 });
            var registry = new CharacterLibraryRegistry(
                Path.Combine(folder, "characters.json"),
                _ => new VrmCharacterMetaResult
                {
                    success = true,
                    specVersion = "0.0",
                    meta = new CharacterLibraryEntryMeta { title = "테스트모델" },
                    thumbnailPng = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
                });
            CharacterLibraryEntry entry = registry.Register(vrmPath).entry;
            string thumbnailPath = registry.ResolveThumbnailPath(entry);

            Assert.That(File.Exists(thumbnailPath), Is.True);
            registry.Remove(entry.id);
            Assert.That(File.Exists(thumbnailPath), Is.False);
        }

        [Test]
        public void Given_ActiveEntry_When_ClearingActiveCharacter_Then_ActiveIdEmpty()
        {
            string folder = CreateTempFolder();
            string vrmPath = Path.Combine(folder, "miku.vrm");
            File.WriteAllBytes(vrmPath, new byte[] { 1 });
            var registry = CreateRegistry(folder);
            CharacterLibraryEntry entry = registry.Register(vrmPath).entry;
            registry.MarkUsed(entry.id);

            registry.ClearActiveCharacter();

            Assert.That(registry.ActiveCharacterId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Given_TwoRegistryInstances_When_SecondRegisters_Then_KeepsFirstsEntries()
        {
            string folder = CreateTempFolder();
            string libraryFile = Path.Combine(folder, "characters.json");
            File.WriteAllBytes(Path.Combine(folder, "a.vrm"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(folder, "b.vrm"), new byte[] { 2 });

            // 창과 매니저처럼 같은 파일을 쓰는 두 인스턴스를 만든다.
            var first = CreateRegistry(folder, libraryFile);
            var second = CreateRegistry(folder, libraryFile);

            // second가 먼저 문서를 캐시한 뒤 first가 저장하는 상황을 만든다.
            Assert.That(second.Entries, Is.Empty);
            first.Register(Path.Combine(folder, "a.vrm"));

            // second는 외부 저장을 감지해 문서를 다시 읽은 뒤 병합해야 한다.
            second.Register(Path.Combine(folder, "b.vrm"));

            var verifier = CreateRegistry(folder, libraryFile);
            Assert.That(verifier.Entries.Count, Is.EqualTo(2));
        }

        [Test]
        public void Given_LockedLibraryFile_When_Loading_Then_SaveBlockedAndFilePreserved()
        {
            string folder = CreateTempFolder();
            string libraryFile = Path.Combine(folder, "characters.json");
            string originalJson = "{\"schemaVersion\":1,\"entries\":[]}";
            File.WriteAllText(libraryFile, originalJson);

            var registry = CreateRegistry(folder, libraryFile);

            // 파일 잠금(백신/동기화 툴 등)으로 읽기가 실패하는 상황을 만든다.
            using (new FileStream(libraryFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // 읽기 실패 로그는 의도된 동작이므로 기대 로그로 선언한다.
                LogAssert.Expect(
                    LogType.Error,
                    new Regex("파일을 읽지 못했습니다"));
                Assert.That(registry.Entries, Is.Not.Null);
                Assert.That(registry.LastReadFailed, Is.True);
                // 읽기 실패 상태에서 저장하면 정상 파일을 덮어쓰므로 차단해야 한다.
                Assert.Throws<InvalidOperationException>(() => registry.Save());
            }

            // 원본 파일은 손상되지 않고 그대로 남아야 한다.
            Assert.That(File.ReadAllText(libraryFile), Is.EqualTo(originalJson));
        }

        private static CharacterLibraryRegistry CreateRegistry(string folder, string libraryFile = null)
        {
            return new CharacterLibraryRegistry(
                libraryFile ?? Path.Combine(folder, "characters.json"),
                _ => new VrmCharacterMetaResult
                {
                    success = true,
                    specVersion = "0.0",
                    meta = new CharacterLibraryEntryMeta { title = "테스트모델", author = "작가" },
                });
        }

        private static string CreateTempFolder()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "UnityFbx2Vmd-CharacterLibraryRegistryTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
