using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 캐릭터+모션 조합별 표면 보정 결과를 디스크에 저장하는 문서 형태.
    /// 솔버 재계산 없이 캐시 객체를 복원하기 위한 직렬화 전용 구조다.
    /// </summary>
    internal sealed class NativeSkinningCorrectionCacheDocument
    {
        internal int FrameCount;
        internal int CorrectedFrameCount;
        internal int FallbackFrameCount;
        internal int CorrectionEntryCount;
        internal NativeSkinningCorrectionCacheRendererEntry[] Renderers;
    }

    internal sealed class NativeSkinningCorrectionCacheRendererEntry
    {
        // 씬 오브젝트와 재바인딩용 식별 — Animator 루트 기준 Transform 경로.
        internal string RendererPath;
        internal string MeshAssetId;
        internal int VertexCount;
        internal NativeSkinningCorrectionCacheFrameData[] Frames;
    }

    internal sealed class NativeSkinningCorrectionCacheFrameData
    {
        internal int FrameIndex;
        internal int[] VertexIndices;
        internal Vector3[] Deltas;
    }

    /// <summary>
    /// 표면 보정 캐시 문서를 바이너리로 원자적으로 저장하고, 키·허용 범위·CRC를
    /// 검증해 불러옴. 키가 다르거나 손상된 파일은 미스로 보고해 재계산으로 넘긴다.
    /// </summary>
    internal static class NativeSkinningCorrectionCacheFileStore
    {
        private static readonly byte[] Magic =
            { (byte)'N', (byte)'S', (byte)'C', (byte)'C' };
        private const int FormatVersion = 1;
        internal const int KeyHashLength = 16;

        internal static string GetCacheDirectory()
        {
            // 빌드의 dataPath는 설치 폴더라 쓰기 불가할 수 있어 영구 폴더로 분기함.
            if (!Application.isEditor)
            {
                return Path.Combine(
                    Application.persistentDataPath,
                    "NativeSkinningCorrectionCache");
            }
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Library",
                "NativeSkinningCorrectionCache"));
        }

        internal static string GetCachePath(byte[] keyHash)
        {
            var builder = new StringBuilder(KeyHashLength * 2);
            for (int index = 0; index < KeyHashLength; index++)
            {
                builder.Append(keyHash[index].ToString("x2"));
            }
            return Path.Combine(
                GetCacheDirectory(),
                "correction-" + builder.ToString() + ".bin");
        }

        internal static bool TrySave(
            string filePath,
            byte[] keyHash,
            NativeSkinningCorrectionCacheDocument document,
            out string errorMessage)
        {
            string temporaryPath = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    errorMessage = "보정 캐시 경로가 필요합니다.";
                    return false;
                }
                if (keyHash == null || keyHash.Length != KeyHashLength)
                {
                    errorMessage = "보정 캐시 키 해시가 올바르지 않습니다.";
                    return false;
                }
                if (!TryValidateDocument(document, out errorMessage))
                {
                    return false;
                }

                string resolvedPath = Path.GetFullPath(filePath.Trim());
                string directoryPath = Path.GetDirectoryName(resolvedPath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                byte[] payload = WritePayload(document);
                byte[] checksum = BitConverter.GetBytes(
                    (int)Crc32.Compute(payload));
                using (var stream = new MemoryStream())
                {
                    stream.Write(Magic, 0, Magic.Length);
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                    {
                        writer.Write(FormatVersion);
                        writer.Write(keyHash, 0, KeyHashLength);
                        writer.Write(payload.Length);
                    }
                    stream.Write(payload, 0, payload.Length);
                    stream.Write(checksum, 0, checksum.Length);
                    temporaryPath =
                        resolvedPath + ".tmp-" + Guid.NewGuid().ToString("N");
                    File.WriteAllBytes(temporaryPath, stream.ToArray());
                }

                if (File.Exists(resolvedPath))
                {
                    File.Replace(temporaryPath, resolvedPath, null);
                }
                else
                {
                    File.Move(temporaryPath, resolvedPath);
                }

                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"표면 보정 캐시를 저장하지 못했습니다: {exception.Message}";
                return false;
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        internal static bool TryLoad(
            string filePath,
            byte[] expectedKeyHash,
            out NativeSkinningCorrectionCacheDocument document,
            out string errorMessage)
        {
            document = null;
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    errorMessage = "보정 캐시 경로가 필요합니다.";
                    return false;
                }
                string resolvedPath = Path.GetFullPath(filePath.Trim());
                if (!File.Exists(resolvedPath))
                {
                    errorMessage =
                        $"표면 보정 캐시를 찾지 못했습니다: {resolvedPath}";
                    return false;
                }

                byte[] bytes = File.ReadAllBytes(resolvedPath);
                int minimumLength = Magic.Length + sizeof(int) +
                    KeyHashLength + sizeof(int) + sizeof(uint);
                if (bytes.Length < minimumLength)
                {
                    errorMessage = "표면 보정 캐시 파일이 손상됐습니다.";
                    return false;
                }
                for (int index = 0; index < Magic.Length; index++)
                {
                    if (bytes[index] != Magic[index])
                    {
                        errorMessage = "표면 보정 캐시 파일 형식이 아닙니다.";
                        return false;
                    }
                }

                using (var stream = new MemoryStream(bytes))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    reader.BaseStream.Position = Magic.Length;
                    int version = reader.ReadInt32();
                    if (version != FormatVersion)
                    {
                        errorMessage =
                            $"표면 보정 캐시 형식 버전이 다릅니다: {version}";
                        return false;
                    }
                    byte[] storedKey = reader.ReadBytes(KeyHashLength);
                    if (expectedKeyHash != null &&
                        !AreEqual(storedKey, expectedKeyHash))
                    {
                        errorMessage = "표면 보정 캐시 키가 일치하지 않습니다.";
                        return false;
                    }
                    int payloadLength = reader.ReadInt32();
                    long expectedTotal = stream.Position + payloadLength +
                        sizeof(uint);
                    if (payloadLength < 0 || expectedTotal != bytes.Length)
                    {
                        errorMessage = "표면 보정 캐시 파일 길이가 손상됐습니다.";
                        return false;
                    }
                    byte[] payload = reader.ReadBytes(payloadLength);
                    uint storedChecksum = reader.ReadUInt32();
                    if (Crc32.Compute(payload) != storedChecksum)
                    {
                        errorMessage = "표면 보정 캐시 체크섬이 일치하지 않습니다.";
                        return false;
                    }

                    document = ReadPayload(payload);
                }

                if (!TryValidateDocument(document, out errorMessage))
                {
                    document = null;
                    return false;
                }

                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                document = null;
                errorMessage =
                    $"표면 보정 캐시를 불러오지 못했습니다: {exception.Message}";
                return false;
            }
        }

        private static byte[] WritePayload(
            NativeSkinningCorrectionCacheDocument document)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(document.FrameCount);
                writer.Write(document.CorrectedFrameCount);
                writer.Write(document.FallbackFrameCount);
                writer.Write(document.CorrectionEntryCount);
                writer.Write(document.Renderers.Length);
                foreach (NativeSkinningCorrectionCacheRendererEntry entry in
                         document.Renderers)
                {
                    writer.Write(entry.RendererPath ?? string.Empty);
                    writer.Write(entry.MeshAssetId ?? string.Empty);
                    writer.Write(entry.VertexCount);
                    writer.Write(entry.Frames.Length);
                    foreach (NativeSkinningCorrectionCacheFrameData frame in
                             entry.Frames)
                    {
                        writer.Write(frame.FrameIndex);
                        writer.Write(frame.VertexIndices.Length);
                        for (int index = 0;
                             index < frame.VertexIndices.Length;
                             index++)
                        {
                            writer.Write(frame.VertexIndices[index]);
                            Vector3 delta = frame.Deltas[index];
                            writer.Write(delta.x);
                            writer.Write(delta.y);
                            writer.Write(delta.z);
                        }
                    }
                }
                return stream.ToArray();
            }
        }

        private static NativeSkinningCorrectionCacheDocument ReadPayload(
            byte[] payload)
        {
            using (var stream = new MemoryStream(payload))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                var document = new NativeSkinningCorrectionCacheDocument
                {
                    FrameCount = reader.ReadInt32(),
                    CorrectedFrameCount = reader.ReadInt32(),
                    FallbackFrameCount = reader.ReadInt32(),
                    CorrectionEntryCount = reader.ReadInt32()
                };
                int rendererCount = reader.ReadInt32();
                if (rendererCount < 0 ||
                    rendererCount > stream.Length - stream.Position)
                {
                    throw new InvalidDataException(
                        "표면 보정 캐시의 렌더러 수가 올바르지 않습니다.");
                }
                var renderers =
                    new NativeSkinningCorrectionCacheRendererEntry[
                        rendererCount];
                for (int rendererIndex = 0;
                     rendererIndex < rendererCount;
                     rendererIndex++)
                {
                    var entry =
                        new NativeSkinningCorrectionCacheRendererEntry
                        {
                            RendererPath = reader.ReadString(),
                            MeshAssetId = reader.ReadString(),
                            VertexCount = reader.ReadInt32()
                        };
                    int frameCount = reader.ReadInt32();
                    // 프레임 최소 레코드는 FrameIndex+항목 수 8바이트 — 그 이상 선언은 손상.
                    if (frameCount < 0 ||
                        frameCount > (stream.Length - stream.Position) / 8)
                    {
                        throw new InvalidDataException(
                            "표면 보정 캐시의 프레임 수가 올바르지 않습니다.");
                    }
                    var frames =
                        new NativeSkinningCorrectionCacheFrameData[
                            frameCount];
                    for (int frameIndex = 0; frameIndex < frameCount;
                         frameIndex++)
                    {
                        var frame = new NativeSkinningCorrectionCacheFrameData
                        {
                            FrameIndex = reader.ReadInt32()
                        };
                        int entryCount = reader.ReadInt32();
                        // 항목 최소 레코드는 int+Vector3 16바이트 — 그 이상 선언은 손상.
                        if (entryCount < 0 ||
                            entryCount > (stream.Length - stream.Position) / 16)
                        {
                            throw new InvalidDataException(
                                "표면 보정 캐시의 보정 항목 수가 올바르지 않습니다.");
                        }
                        frame.VertexIndices = new int[entryCount];
                        frame.Deltas = new Vector3[entryCount];
                        for (int index = 0; index < entryCount; index++)
                        {
                            frame.VertexIndices[index] = reader.ReadInt32();
                            frame.Deltas[index] = new Vector3(
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle());
                        }
                        frames[frameIndex] = frame;
                    }
                    entry.Frames = frames;
                    renderers[rendererIndex] = entry;
                }
                document.Renderers = renderers;
                return document;
            }
        }

        private static bool TryValidateDocument(
            NativeSkinningCorrectionCacheDocument document,
            out string errorMessage)
        {
            if (document == null || document.Renderers == null)
            {
                errorMessage = "표면 보정 캐시 문서가 비어 있습니다.";
                return false;
            }
            if (document.FrameCount <= 0)
            {
                errorMessage = "표면 보정 캐시 프레임 수가 올바르지 않습니다.";
                return false;
            }

            int totalEntries = 0;
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (NativeSkinningCorrectionCacheRendererEntry entry in
                     document.Renderers)
            {
                if (entry == null ||
                    entry.VertexCount <= 0 ||
                    entry.Frames == null ||
                    string.IsNullOrWhiteSpace(entry.RendererPath) ||
                    // 동일 경로의 렌더러 항목이 둘이면 재바인딩이 모호해진다.
                    !seenPaths.Add(entry.RendererPath))
                {
                    errorMessage =
                        "표면 보정 캐시의 렌더러 항목이 올바르지 않습니다.";
                    return false;
                }
                var seenFrames = new HashSet<int>();
                foreach (NativeSkinningCorrectionCacheFrameData frame in
                         entry.Frames)
                {
                    if (frame == null ||
                        frame.FrameIndex < 0 ||
                        frame.FrameIndex >= document.FrameCount ||
                        frame.VertexIndices == null ||
                        frame.Deltas == null ||
                        frame.VertexIndices.Length != frame.Deltas.Length ||
                        // 중복 프레임 인덱스는 복원 시 생성자 예외를 만든다.
                        !seenFrames.Add(frame.FrameIndex))
                    {
                        errorMessage =
                            "표면 보정 캐시의 프레임 데이터가 올바르지 않습니다.";
                        return false;
                    }
                    var seenIndices = new HashSet<int>();
                    for (int index = 0;
                         index < frame.VertexIndices.Length;
                         index++)
                    {
                        int vertexIndex = frame.VertexIndices[index];
                        Vector3 delta = frame.Deltas[index];
                        if (vertexIndex < 0 ||
                            vertexIndex >= entry.VertexCount ||
                            !IsFinite(delta) ||
                            !seenIndices.Add(vertexIndex))
                        {
                            errorMessage =
                                "표면 보정 캐시의 보정 항목이 올바르지 않습니다.";
                            return false;
                        }
                    }
                    totalEntries += frame.VertexIndices.Length;
                }
            }

            if (totalEntries != document.CorrectionEntryCount)
            {
                errorMessage = "표면 보정 캐시의 보정 항목 수가 일치하지 않습니다.";
                return false;
            }
            errorMessage = string.Empty;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool AreEqual(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }
            for (int index = 0; index < first.Length; index++)
            {
                if (first[index] != second[index])
                {
                    return false;
                }
            }
            return true;
        }

        private static void TryDeleteTemporaryFile(string temporaryPath)
        {
            if (string.IsNullOrWhiteSpace(temporaryPath) ||
                !File.Exists(temporaryPath))
            {
                return;
            }
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // 저장 결과를 반환한 뒤 임시 파일 정리 실패로 결과를 뒤집지 않음.
            }
            catch (UnauthorizedAccessException)
            {
                // 저장 결과를 반환한 뒤 임시 파일 정리 실패로 결과를 뒤집지 않음.
            }
        }

        // 표준 CRC-32 (poly 0xEDB88320) — 저장 페이로드 손상 검출용.
        private static class Crc32
        {
            private static readonly uint[] Table = BuildTable();

            internal static uint Compute(byte[] data)
            {
                uint value = 0xFFFFFFFFu;
                for (int index = 0; index < data.Length; index++)
                {
                    value = Table[(value ^ data[index]) & 0xFF] ^
                        (value >> 8);
                }
                return ~value;
            }

            private static uint[] BuildTable()
            {
                var table = new uint[256];
                for (uint index = 0; index < 256; index++)
                {
                    uint value = index;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        value = (value & 1) != 0
                            ? 0xEDB88320u ^ (value >> 1)
                            : value >> 1;
                    }
                    table[index] = value;
                }
                return table;
            }
        }
    }
}
