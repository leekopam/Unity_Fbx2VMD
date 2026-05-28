using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

internal static class VmdFileWriter
{
    private const int ParentOfAllOrdinal = 0;
    private const int CenterOrdinal = 1;
    private const int RightToeIkOrdinal = 5;

    private static readonly byte[] LinearBoneInterpolation =
    {
        20, 20, 20, 20,
        20, 20, 20, 20,
        107, 107, 107, 107,
        107, 107, 107, 107,
        20, 20, 20, 20,
        20, 20, 20, 20,
        107, 107, 107, 107,
        107, 107, 107, 107,
        20, 20, 20, 20,
        20, 20, 20, 20,
        107, 107, 107, 107,
        107, 107, 107, 107,
        20, 20, 20, 20,
        20, 20, 20, 20,
        107, 107, 107, 107,
        107, 107, 107, 107
    };

    private static readonly IReadOnlyDictionary<int, string> MmdBoneNamesByOrdinal = new Dictionary<int, string>
    {
        [0] = "\u5168\u3066\u306e\u89aa",
        [1] = "\u30bb\u30f3\u30bf\u30fc",
        [2] = "\u5de6\u8db3\uff29\uff2b",
        [3] = "\u53f3\u8db3\uff29\uff2b",
        [4] = "\u5de6\u3064\u307e\u5148\uff29\uff2b",
        [5] = "\u53f3\u3064\u307e\u5148\uff29\uff2b",
        [6] = "\u4e0a\u534a\u8eab",
        [7] = "\u4e0a\u534a\u8eab2",
        [8] = "\u9996",
        [9] = "\u982d",
        [10] = "\u5de6\u80a9",
        [11] = "\u5de6\u8155",
        [12] = "\u5de6\u3072\u3058",
        [13] = "\u5de6\u624b\u9996",
        [14] = "\u53f3\u80a9",
        [15] = "\u53f3\u8155",
        [16] = "\u53f3\u3072\u3058",
        [17] = "\u53f3\u624b\u9996",
        [18] = "\u5de6\u89aa\u6307\uff11",
        [19] = "\u5de6\u89aa\u6307\uff12",
        [20] = "\u5de6\u4eba\u6307\uff11",
        [21] = "\u5de6\u4eba\u6307\uff12",
        [22] = "\u5de6\u4eba\u6307\uff13",
        [23] = "\u5de6\u4e2d\u6307\uff11",
        [24] = "\u5de6\u4e2d\u6307\uff12",
        [25] = "\u5de6\u4e2d\u6307\uff13",
        [26] = "\u5de6\u85ac\u6307\uff11",
        [27] = "\u5de6\u85ac\u6307\uff12",
        [28] = "\u5de6\u85ac\u6307\uff13",
        [29] = "\u5de6\u5c0f\u6307\uff11",
        [30] = "\u5de6\u5c0f\u6307\uff12",
        [31] = "\u5de6\u5c0f\u6307\uff13",
        [32] = "\u53f3\u89aa\u6307\uff11",
        [33] = "\u53f3\u89aa\u6307\uff12",
        [34] = "\u53f3\u4eba\u6307\uff11",
        [35] = "\u53f3\u4eba\u6307\uff12",
        [36] = "\u53f3\u4eba\u6307\uff13",
        [37] = "\u53f3\u4e2d\u6307\uff11",
        [38] = "\u53f3\u4e2d\u6307\uff12",
        [39] = "\u53f3\u4e2d\u6307\uff13",
        [40] = "\u53f3\u85ac\u6307\uff11",
        [41] = "\u53f3\u85ac\u6307\uff12",
        [42] = "\u53f3\u85ac\u6307\uff13",
        [43] = "\u53f3\u5c0f\u6307\uff11",
        [44] = "\u53f3\u5c0f\u6307\uff12",
        [45] = "\u53f3\u5c0f\u6307\uff13",
        [46] = "\u5de6\u8db3",
        [47] = "\u53f3\u8db3",
        [48] = "\u5de6\u3072\u3056",
        [49] = "\u53f3\u3072\u3056",
        [50] = "\u5de6\u8db3\u9996",
        [51] = "\u53f3\u8db3\u9996",
        [52] = "\u4e0b\u534a\u8eab",
    };

    internal static void WriteVmdFile(
        string modelName,
        string filePath,
        List<BoneNames> activeBones,
        int frameCount,
        int keyReductionLevel,
        IReadOnlyDictionary<BoneNames, List<Vector3>> positionDictionarySaved,
        IReadOnlyDictionary<BoneNames, List<Quaternion>> rotationDictionarySaved,
        VmdMorphRecorder morphSnapshot,
        bool useCenterAsParentOfAll,
        bool routeCenterBoneToGroove,
        string centerNameString,
        string grooveNameString)
    {
        const string shiftJis = "shift_jis";
        const int intByteLength = 4;

        using FileStream fileStream = new FileStream(filePath, FileMode.Create);
        using BinaryWriter binaryWriter = new BinaryWriter(fileStream);

        const int fileTypeLength = 30;
        const string rightFileType = "Vocaloid Motion Data 0002";
        byte[] fileTypeBytes = GetShiftJisBytes(rightFileType, shiftJis, fileTypeLength);
        binaryWriter.Write(fileTypeBytes, 0, fileTypeBytes.Length);
        binaryWriter.Write(new byte[fileTypeLength - fileTypeBytes.Length], 0, fileTypeLength - fileTypeBytes.Length);

        const int modelNameLength = 20;
        byte[] modelNameBytes = GetShiftJisBytes(modelName, shiftJis, modelNameLength);
        binaryWriter.Write(modelNameBytes, 0, modelNameBytes.Length);
        binaryWriter.Write(new byte[modelNameLength - modelNameBytes.Length], 0, modelNameLength - modelNameBytes.Length);

        void LoopWithBoneCondition(Action<BoneNames, int> action)
        {
            for (int i = 0; i < frameCount; i++)
            {
                foreach (BoneNames boneName in activeBones)
                {
                    if (!ShouldWriteBoneFrame(boneName, i, frameCount, keyReductionLevel))
                    {
                        continue;
                    }

                    action(boneName, i);
                }
            }
        }

        uint allKeyFrameNumber = 0;
        LoopWithBoneCondition((a, b) => { allKeyFrameNumber++; });
        binaryWriter.Write(BitConverter.GetBytes(allKeyFrameNumber), 0, intByteLength);

        LoopWithBoneCondition((boneName, i) =>
        {
            const int boneNameLength = 15;
            string boneNameString = GetExportBoneName(boneName, useCenterAsParentOfAll, routeCenterBoneToGroove, centerNameString, grooveNameString);
            byte[] boneNameBytes = GetShiftJisBytes(boneNameString, shiftJis, boneNameLength);
            binaryWriter.Write(boneNameBytes, 0, boneNameBytes.Length);
            binaryWriter.Write(new byte[boneNameLength - boneNameBytes.Length], 0, boneNameLength - boneNameBytes.Length);

            binaryWriter.Write(BitConverter.GetBytes((uint)i), 0, intByteLength);

            Vector3 position = positionDictionarySaved[boneName][i];
            binaryWriter.Write(BitConverter.GetBytes(position.x), 0, intByteLength);
            binaryWriter.Write(BitConverter.GetBytes(position.y), 0, intByteLength);
            binaryWriter.Write(BitConverter.GetBytes(position.z), 0, intByteLength);

            Quaternion rotation = rotationDictionarySaved[boneName][i];
            binaryWriter.Write(BitConverter.GetBytes(rotation.x), 0, intByteLength);
            binaryWriter.Write(BitConverter.GetBytes(rotation.y), 0, intByteLength);
            binaryWriter.Write(BitConverter.GetBytes(rotation.z), 0, intByteLength);
            binaryWriter.Write(BitConverter.GetBytes(rotation.w), 0, intByteLength);

            binaryWriter.Write(LinearBoneInterpolation, 0, LinearBoneInterpolation.Length);
        });

        WriteMorphFrames(binaryWriter, morphSnapshot, frameCount, shiftJis, intByteLength);

        binaryWriter.Write(BitConverter.GetBytes(0), 0, intByteLength);
        binaryWriter.Write(BitConverter.GetBytes(0), 0, intByteLength);
        binaryWriter.Write(BitConverter.GetBytes(0), 0, intByteLength);

        WriteIkFooter(binaryWriter, shiftJis, intByteLength);
    }

    internal static bool ShouldWriteBoneFrame(BoneNames boneName, int frameIndex, int frameCount, int keyReductionLevel)
    {
        return true;
    }

    private static bool IsMotionCarrierBone(BoneNames boneName)
    {
        int ordinal = (int)boneName;
        return ordinal >= ParentOfAllOrdinal && ordinal <= RightToeIkOrdinal;
    }

    private static string GetExportBoneName(
        BoneNames boneName,
        bool useCenterAsParentOfAll,
        bool routeCenterBoneToGroove,
        string centerNameString,
        string grooveNameString)
    {
        int ordinal = (int)boneName;
        if (ordinal == ParentOfAllOrdinal && useCenterAsParentOfAll && routeCenterBoneToGroove)
        {
            return centerNameString;
        }

        if (ordinal == CenterOrdinal && useCenterAsParentOfAll && routeCenterBoneToGroove)
        {
            return grooveNameString;
        }

        return MmdBoneNamesByOrdinal.TryGetValue(ordinal, out string mappedName)
            ? mappedName
            : boneName.ToString();
    }

    private static byte[] GetShiftJisBytes(string value, string shiftJis, int maxByteLength)
    {
        byte[] bytes = System.Text.Encoding.GetEncoding(shiftJis).GetBytes(value ?? string.Empty);
        if (bytes.Length <= maxByteLength)
        {
            return bytes;
        }

        return bytes.Take(maxByteLength).ToArray();
    }

    private static void WriteMorphFrames(BinaryWriter binaryWriter, VmdMorphRecorder morphSnapshot, int frameCount, string shiftJis, int intByteLength)
    {
        const int morphNameLength = 15;
        if (morphSnapshot == null)
        {
            binaryWriter.Write(BitConverter.GetBytes(0), 0, intByteLength);
            return;
        }

        void LoopWithMorphCondition(Action<string, int> action)
        {
            for (int i = 0; i < frameCount; i++)
            {
                foreach (string morphName in morphSnapshot.MorphDrivers.Keys)
                {
                    VmdMorphRecorder.MorphDriver driver = morphSnapshot.MorphDrivers[morphName];
                    if (driver.ValueList.Count == 0) { continue; }
                    if (i >= driver.ValueList.Count) { continue; }
                    if (!driver.ValueList[i].enabled) { continue; }

                    byte[] morphNameBytes = GetShiftJisBytes(morphName, shiftJis, morphNameLength);
                    if (morphNameBytes.Length == 0) { continue; }

                    action(morphName, i);
                }
            }
        }

        uint allMorphNumber = 0;
        LoopWithMorphCondition((a, b) => { allMorphNumber++; });
        binaryWriter.Write(BitConverter.GetBytes(allMorphNumber), 0, intByteLength);

        LoopWithMorphCondition((morphName, i) =>
        {
            byte[] morphNameBytes = GetShiftJisBytes(morphName, shiftJis, morphNameLength);
            binaryWriter.Write(morphNameBytes, 0, morphNameBytes.Length);
            binaryWriter.Write(new byte[morphNameLength - morphNameBytes.Length], 0, morphNameLength - morphNameBytes.Length);

            binaryWriter.Write(BitConverter.GetBytes((uint)i), 0, intByteLength);

            byte[] valueByte = BitConverter.GetBytes(morphSnapshot.MorphDrivers[morphName].ValueList[i].value);
            binaryWriter.Write(valueByte, 0, intByteLength);
        });
    }

    private static void WriteIkFooter(BinaryWriter binaryWriter, string shiftJis, int intByteLength)
    {
        byte[] ikCount = BitConverter.GetBytes(1);
        byte[] ikFrameNumber = BitConverter.GetBytes(0);
        byte modelDisplay = Convert.ToByte(1);
        byte[] ikNumber = BitConverter.GetBytes(4);
        const int ikNameLength = 20;
        byte[] leftIKName = GetShiftJisBytes("\u5de6\u8db3\uff29\uff2b", shiftJis, ikNameLength);
        byte[] rightIKName = GetShiftJisBytes("\u53f3\u8db3\uff29\uff2b", shiftJis, ikNameLength);
        byte[] leftToeIKName = GetShiftJisBytes("\u5de6\u3064\u307e\u5148\uff29\uff2b", shiftJis, ikNameLength);
        byte[] rightToeIKName = GetShiftJisBytes("\u53f3\u3064\u307e\u5148\uff29\uff2b", shiftJis, ikNameLength);
        byte ikOn = Convert.ToByte(1);

        binaryWriter.Write(ikCount, 0, intByteLength);
        binaryWriter.Write(ikFrameNumber, 0, intByteLength);
        binaryWriter.Write(modelDisplay);
        binaryWriter.Write(ikNumber, 0, intByteLength);
        WritePaddedBytes(binaryWriter, leftIKName, ikNameLength);
        binaryWriter.Write(ikOn);
        WritePaddedBytes(binaryWriter, leftToeIKName, ikNameLength);
        binaryWriter.Write(ikOn);
        WritePaddedBytes(binaryWriter, rightIKName, ikNameLength);
        binaryWriter.Write(ikOn);
        WritePaddedBytes(binaryWriter, rightToeIKName, ikNameLength);
        binaryWriter.Write(ikOn);
    }

    private static void WritePaddedBytes(BinaryWriter binaryWriter, byte[] bytes, int byteLength)
    {
        binaryWriter.Write(bytes, 0, bytes.Length);
        binaryWriter.Write(new byte[byteLength - bytes.Length], 0, byteLength - bytes.Length);
    }
}
