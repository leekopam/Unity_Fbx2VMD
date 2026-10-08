using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class NativeSkinningCorrectionCacheFileStoreTests
    {
        private const string StoreTypeName =
            "Fbx2Vmd.FBXImporter.NativeSkinningCorrectionCacheFileStore";
        private const string DocumentTypeName =
            "Fbx2Vmd.FBXImporter.NativeSkinningCorrectionCacheDocument";
        private const string EntryTypeName =
            "Fbx2Vmd.FBXImporter.NativeSkinningCorrectionCacheRendererEntry";
        private const string FrameTypeName =
            "Fbx2Vmd.FBXImporter.NativeSkinningCorrectionCacheFrameData";

        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "nsc-cache-test-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void Given_Document_When_SaveAndLoad_Then_RoundTripsIdentically()
        {
            byte[] key = MakeKey(1);
            object document = MakeDocument();
            string path = Path.Combine(_directory, "cache.bin");

            Assert.That(TrySave(path, key, document, out string saveError),
                Is.True, saveError);
            Assert.That(
                TryLoad(path, key, out object loaded, out string loadError),
                Is.True,
                loadError);

            Assert.That(
                ReadField<int>(loaded, "FrameCount"),
                Is.EqualTo(ReadField<int>(document, "FrameCount")));
            Assert.That(
                ReadField<int>(loaded, "CorrectionEntryCount"),
                Is.EqualTo(ReadField<int>(document, "CorrectionEntryCount")));

            Array loadedRenderers = ReadField<Array>(loaded, "Renderers");
            Array originalRenderers = ReadField<Array>(document, "Renderers");
            Assert.That(
                loadedRenderers.Length, Is.EqualTo(originalRenderers.Length));

            object entry = loadedRenderers.GetValue(0);
            object original = originalRenderers.GetValue(0);
            Assert.That(
                ReadField<string>(entry, "RendererPath"),
                Is.EqualTo(ReadField<string>(original, "RendererPath")));
            Assert.That(
                ReadField<int>(entry, "VertexCount"),
                Is.EqualTo(ReadField<int>(original, "VertexCount")));

            Array loadedFrames = ReadField<Array>(entry, "Frames");
            Array originalFrames = ReadField<Array>(original, "Frames");
            Assert.That(
                ReadField<int[]>(loadedFrames.GetValue(0), "VertexIndices"),
                Is.EqualTo(ReadField<int[]>(
                    originalFrames.GetValue(0), "VertexIndices")));
            Assert.That(
                ReadField<Vector3[]>(loadedFrames.GetValue(0), "Deltas"),
                Is.EqualTo(ReadField<Vector3[]>(
                    originalFrames.GetValue(0), "Deltas")));
        }

        [Test]
        public void Given_DifferentKey_When_Loading_Then_FailsAsMiss()
        {
            byte[] key = MakeKey(1);
            string path = Path.Combine(_directory, "cache.bin");
            Assert.That(TrySave(path, key, MakeDocument(), out _), Is.True);

            Assert.That(
                TryLoad(path, MakeKey(2), out object loaded, out _),
                Is.False);
            Assert.That(loaded, Is.Null);
        }

        [Test]
        public void Given_CorruptedPayload_When_Loading_Then_FailsAsMiss()
        {
            byte[] key = MakeKey(1);
            string path = Path.Combine(_directory, "cache.bin");
            Assert.That(TrySave(path, key, MakeDocument(), out _), Is.True);

            byte[] bytes = File.ReadAllBytes(path);
            bytes[bytes.Length / 2] ^= 0xFF;
            File.WriteAllBytes(path, bytes);

            Assert.That(
                TryLoad(path, key, out object loaded, out _),
                Is.False);
            Assert.That(loaded, Is.Null);
        }

        [Test]
        public void Given_TruncatedFile_When_Loading_Then_FailsAsMiss()
        {
            byte[] key = MakeKey(1);
            string path = Path.Combine(_directory, "cache.bin");
            Assert.That(TrySave(path, key, MakeDocument(), out _), Is.True);

            byte[] bytes = File.ReadAllBytes(path);
            byte[] truncated = new byte[bytes.Length - 8];
            Array.Copy(bytes, truncated, truncated.Length);
            File.WriteAllBytes(path, truncated);

            Assert.That(TryLoad(path, key, out _, out _), Is.False);
        }

        [Test]
        public void Given_MissingFile_When_Loading_Then_FailsAsMiss()
        {
            Assert.That(
                TryLoad(
                    Path.Combine(_directory, "absent.bin"),
                    MakeKey(1),
                    out _,
                    out _),
                Is.False);
        }

        [Test]
        public void Given_EditorEnvironment_When_GetCacheDirectory_Then_UsesProjectLibrary()
        {
            // 에디터에서는 기존 Library 경로 계약을 유지해야 기존 캐시가 유효함.
            string directory = (string)RequireMethod(
                    StoreTypeName,
                    "GetCacheDirectory")
                .Invoke(null, null);

            Assert.That(Application.isEditor, Is.True);
            Assert.That(
                directory.Replace('\\', '/'),
                Does.EndWith("/Library/NativeSkinningCorrectionCache"));
        }

        [Test]
        public void Given_OutOfRangeVertexIndex_When_Saving_Then_Rejects()
        {
            object document = MakeDocument();
            Array renderers = ReadField<Array>(document, "Renderers");
            Array frames = ReadField<Array>(renderers.GetValue(0), "Frames");
            WriteField(
                frames.GetValue(0),
                "VertexIndices",
                new[] { 999999 });

            Assert.That(
                TrySave(
                    Path.Combine(_directory, "cache.bin"),
                    MakeKey(1),
                    document,
                    out _),
                Is.False);
        }

        [Test]
        public void Given_EntryCountMismatch_When_Saving_Then_Rejects()
        {
            object document = MakeDocument();
            WriteField(
                document,
                "CorrectionEntryCount",
                ReadField<int>(document, "CorrectionEntryCount") + 1);

            Assert.That(
                TrySave(
                    Path.Combine(_directory, "cache.bin"),
                    MakeKey(1),
                    document,
                    out _),
                Is.False);
        }

        [Test]
        public void Given_DuplicateFrameIndex_When_Saving_Then_Rejects()
        {
            object document = MakeDocument();
            Array renderers = ReadField<Array>(document, "Renderers");
            Array frames = ReadField<Array>(renderers.GetValue(0), "Frames");
            // 두 번째 프레임을 첫 번째와 같은 인덱스로 — 복원 시 생성자 예외 유발.
            WriteField(frames.GetValue(1), "FrameIndex", 1);

            Assert.That(
                TrySave(
                    Path.Combine(_directory, "cache.bin"),
                    MakeKey(1),
                    document,
                    out _),
                Is.False);
        }

        [Test]
        public void Given_DuplicateRendererPath_When_Saving_Then_Rejects()
        {
            object document = MakeDocument();
            Array renderers = ReadField<Array>(document, "Renderers");
            object first = renderers.GetValue(0);

            // 동일 경로 항목을 하나 더 추가하면 재바인딩이 모호해진다.
            object second = CreateInstance(EntryTypeName);
            WriteField(second, "RendererPath", "Mesh/Body");
            WriteField(second, "MeshAssetId", "guid:def");
            WriteField(second, "VertexCount", 10);
            Array emptyFrames = Array.CreateInstance(
                first.GetType().GetField(
                        "Frames",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .FieldType.GetElementType(),
                0);
            WriteField(second, "Frames", emptyFrames);

            Array duplicated = Array.CreateInstance(first.GetType(), 2);
            duplicated.SetValue(first, 0);
            duplicated.SetValue(second, 1);
            WriteField(document, "Renderers", duplicated);

            Assert.That(
                TrySave(
                    Path.Combine(_directory, "cache.bin"),
                    MakeKey(1),
                    document,
                    out _),
                Is.False);
        }

        private static bool TrySave(
            string path,
            byte[] key,
            object document,
            out string error)
        {
            object[] arguments = { path, key, document, null };
            bool result = (bool)RequireMethod(
                    StoreTypeName,
                    "TrySave")
                .Invoke(null, arguments);
            error = (string)arguments[3];
            return result;
        }

        private static bool TryLoad(
            string path,
            byte[] key,
            out object document,
            out string error)
        {
            object[] arguments = { path, key, null, null };
            bool result = (bool)RequireMethod(
                    StoreTypeName,
                    "TryLoad")
                .Invoke(null, arguments);
            document = arguments[2];
            error = (string)arguments[3];
            return result;
        }

        private static MethodInfo RequireMethod(
            string typeName,
            string methodName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline)
                .Assembly.GetType(typeName, throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{typeName} 타입이 필요합니다.");
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method;
        }

        private static object CreateInstance(string typeName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline)
                .Assembly.GetType(typeName, throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{typeName} 타입이 필요합니다.");
            return Activator.CreateInstance(type, nonPublic: true);
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            return (T)field.GetValue(target);
        }

        private static void WriteField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            field.SetValue(target, value);
        }

        private static byte[] MakeKey(byte seed)
        {
            var key = new byte[16];
            for (int index = 0; index < key.Length; index++)
            {
                key[index] = seed;
            }
            return key;
        }

        private static object MakeDocument()
        {
            object frameA = CreateInstance(FrameTypeName);
            WriteField(frameA, "FrameIndex", 1);
            WriteField(frameA, "VertexIndices", new[] { 3, 5 });
            WriteField(
                frameA,
                "Deltas",
                new[]
                {
                    new Vector3(0.1f, 0.2f, 0.3f),
                    new Vector3(-0.4f, 0f, 0.01f)
                });

            object frameB = CreateInstance(FrameTypeName);
            WriteField(frameB, "FrameIndex", 3);
            WriteField(frameB, "VertexIndices", new[] { 7 });
            WriteField(frameB, "Deltas", new[] { Vector3.one });

            Type frameType = frameA.GetType();
            Array frames = Array.CreateInstance(frameType, 2);
            frames.SetValue(frameA, 0);
            frames.SetValue(frameB, 1);

            object entry = CreateInstance(EntryTypeName);
            WriteField(entry, "RendererPath", "Mesh/Body");
            WriteField(entry, "MeshAssetId", "guid:abc");
            WriteField(entry, "VertexCount", 10);
            WriteField(entry, "Frames", frames);

            Type entryType = entry.GetType();
            Array renderers = Array.CreateInstance(entryType, 1);
            renderers.SetValue(entry, 0);

            object document = CreateInstance(DocumentTypeName);
            WriteField(document, "FrameCount", 4);
            WriteField(document, "CorrectedFrameCount", 2);
            WriteField(document, "FallbackFrameCount", 0);
            WriteField(document, "CorrectionEntryCount", 3);
            WriteField(document, "Renderers", renderers);
            return document;
        }
    }
}
