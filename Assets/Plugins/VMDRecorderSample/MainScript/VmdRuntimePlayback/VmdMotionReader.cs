using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

internal static class VmdMotionReader
{
    private const string ExpectedHeader = "Vocaloid Motion Data 0002";
    private const int HeaderLength = 30;
    private const int ModelNameLength = 20;
    private const int BoneNameLength = 15;
    private const int MorphNameLength = 15;
    private const int BoneInterpolationLength = 64;
    private const int CameraFrameByteLength = 61;
    private const int LightFrameByteLength = 28;
    private const int SelfShadowFrameByteLength = 9;
    private const int IkNameLength = 20;

    private static readonly Encoding ShiftJis = Encoding.GetEncoding("shift_jis");

    internal static VmdMotionData Read(string filePath)
    {
        using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader reader = new BinaryReader(stream);

        string header = ReadFixedString(reader, HeaderLength);
        if (header != ExpectedHeader)
        {
            throw new InvalidDataException($"Unsupported VMD header: {header}");
        }

        string modelName = ReadFixedString(reader, ModelNameLength);
        List<VmdBoneFrame> boneFrames = ReadBoneFrames(reader);
        List<VmdMorphFrame> morphFrames = ReadMorphFrames(reader);
        uint cameraFrameCount = ReadSectionCountAndSkip(reader, CameraFrameByteLength);
        uint lightFrameCount = ReadSectionCountAndSkip(reader, LightFrameByteLength);
        uint selfShadowFrameCount = ReadSectionCountAndSkip(reader, SelfShadowFrameByteLength);
        List<VmdIkFrame> ikFrames = ReadIkFooter(reader);

        return new VmdMotionData(
            header,
            modelName,
            boneFrames,
            morphFrames,
            cameraFrameCount,
            lightFrameCount,
            selfShadowFrameCount,
            (uint)ikFrames.Count,
            ikFrames);
    }

    private static List<VmdBoneFrame> ReadBoneFrames(BinaryReader reader)
    {
        uint count = reader.ReadUInt32();
        var frames = new List<VmdBoneFrame>(GetListCapacity(count));
        for (uint i = 0; i < count; i++)
        {
            string boneName = ReadFixedString(reader, BoneNameLength);
            uint frameIndex = reader.ReadUInt32();
            var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            byte[] interpolation = reader.ReadBytes(BoneInterpolationLength);
            if (interpolation.Length != BoneInterpolationLength)
            {
                throw new EndOfStreamException("VMD bone interpolation block is truncated.");
            }

            frames.Add(new VmdBoneFrame(boneName, frameIndex, position, rotation, interpolation));
        }

        return frames;
    }

    private static List<VmdMorphFrame> ReadMorphFrames(BinaryReader reader)
    {
        uint count = reader.ReadUInt32();
        var frames = new List<VmdMorphFrame>(GetListCapacity(count));
        for (uint i = 0; i < count; i++)
        {
            string morphName = ReadFixedString(reader, MorphNameLength);
            uint frameIndex = reader.ReadUInt32();
            float weight = reader.ReadSingle();
            frames.Add(new VmdMorphFrame(morphName, frameIndex, weight));
        }

        return frames;
    }

    private static uint ReadSectionCountAndSkip(BinaryReader reader, int bytesPerFrame)
    {
        if (reader.BaseStream.Position >= reader.BaseStream.Length)
        {
            return 0;
        }

        uint count = reader.ReadUInt32();
        SkipBytes(reader, checked((long)count * bytesPerFrame));
        return count;
    }

    private static List<VmdIkFrame> ReadIkFooter(BinaryReader reader)
    {
        var frames = new List<VmdIkFrame>();
        if (reader.BaseStream.Position >= reader.BaseStream.Length)
        {
            return frames;
        }

        uint displayFrameCount = reader.ReadUInt32();
        for (uint frame = 0; frame < displayFrameCount; frame++)
        {
            uint frameIndex = reader.ReadUInt32();
            reader.ReadByte();
            uint ikCount = reader.ReadUInt32();
            bool leftFootEnabled = false;
            bool leftToeEnabled = false;
            bool rightFootEnabled = false;
            bool rightToeEnabled = false;

            for (uint ik = 0; ik < ikCount; ik++)
            {
                string ikName = ReadFixedString(reader, IkNameLength);
                bool enabled = reader.ReadByte() != 0;
                switch (ikName)
                {
                    case VmdIkFrame.LeftFootIkName:
                        leftFootEnabled = enabled;
                        break;
                    case VmdIkFrame.LeftToeIkName:
                        leftToeEnabled = enabled;
                        break;
                    case VmdIkFrame.RightFootIkName:
                        rightFootEnabled = enabled;
                        break;
                    case VmdIkFrame.RightToeIkName:
                        rightToeEnabled = enabled;
                        break;
                }
            }

            frames.Add(new VmdIkFrame(
                frameIndex,
                leftFootEnabled,
                leftToeEnabled,
                rightFootEnabled,
                rightToeEnabled));
        }

        return frames;
    }

    private static string ReadFixedString(BinaryReader reader, int byteLength)
    {
        byte[] bytes = reader.ReadBytes(byteLength);
        if (bytes.Length != byteLength)
        {
            throw new EndOfStreamException("VMD fixed string block is truncated.");
        }

        int actualLength = System.Array.IndexOf(bytes, (byte)0);
        if (actualLength < 0)
        {
            actualLength = bytes.Length;
        }

        return ShiftJis.GetString(bytes, 0, actualLength);
    }

    private static void SkipBytes(BinaryReader reader, long byteCount)
    {
        if (byteCount == 0)
        {
            return;
        }

        long target = reader.BaseStream.Position + byteCount;
        if (target > reader.BaseStream.Length)
        {
            throw new EndOfStreamException("VMD section is truncated.");
        }

        reader.BaseStream.Seek(byteCount, SeekOrigin.Current);
    }

    private static int GetListCapacity(uint count)
    {
        if (count > int.MaxValue)
        {
            throw new InvalidDataException($"VMD frame count is too large: {count}");
        }

        return (int)count;
    }
}
