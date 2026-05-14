using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

internal static class VmdFileWriter
{
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
        string centerNameString,
        string grooveNameString)
    {
        const string ShiftJIS = "shift_jis";
        const int intByteLength = 4;

        using FileStream fileStream = new FileStream(filePath, FileMode.Create);
        using BinaryWriter binaryWriter = new BinaryWriter(fileStream);

        const int fileTypeLength = 30;
        const string rightFileType = "Vocaloid Motion Data 0002";
        byte[] fileTypeBytes = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes(rightFileType);
        binaryWriter.Write(fileTypeBytes, 0, fileTypeBytes.Length);
        binaryWriter.Write(new byte[fileTypeLength - fileTypeBytes.Length], 0, fileTypeLength - fileTypeBytes.Length);

        const int modelNameLength = 20;
        byte[] modelNameBytes = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes(modelName);
        modelNameBytes = modelNameBytes.Take(Math.Min(modelNameLength, modelNameBytes.Length)).ToArray();
        binaryWriter.Write(modelNameBytes, 0, modelNameBytes.Length);
        binaryWriter.Write(new byte[modelNameLength - modelNameBytes.Length], 0, modelNameLength - modelNameBytes.Length);

        void LoopWithBoneCondition(Action<BoneNames, int> action)
        {
            for (int i = 0; i < frameCount; i++)
            {
                if ((i % keyReductionLevel) != 0) { continue; }
                foreach (BoneNames boneName in activeBones)
                {
                    action(boneName, i);
                }
            }
        }

        uint allKeyFrameNumber = 0;
        LoopWithBoneCondition((a, b) => { allKeyFrameNumber++; });
        byte[] allKeyFrameNumberByte = BitConverter.GetBytes(allKeyFrameNumber);
        binaryWriter.Write(allKeyFrameNumberByte, 0, intByteLength);

        LoopWithBoneCondition((boneName, i) =>
        {
            const int boneNameLength = 15;
            string boneNameString = boneName.ToString();
            if (boneName == BoneNames.全ての親 && useCenterAsParentOfAll)
            {
                boneNameString = centerNameString;
            }
            if (boneName == BoneNames.センター && useCenterAsParentOfAll)
            {
                boneNameString = grooveNameString;
            }

            byte[] boneNameBytes = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes(boneNameString);
            binaryWriter.Write(boneNameBytes, 0, boneNameBytes.Length);
            binaryWriter.Write(new byte[boneNameLength - boneNameBytes.Length], 0, boneNameLength - boneNameBytes.Length);

            byte[] frameNumberByte = BitConverter.GetBytes((ulong)i);
            binaryWriter.Write(frameNumberByte, 0, intByteLength);

            Vector3 position = positionDictionarySaved[boneName][i];
            binaryWriter.Write(BitConverter.GetBytes(position.x), 0, intByteLength);
            binaryWriter.Write(BitConverter.GetBytes(position.y), 0, intByteLength);
            binaryWriter.Write(BitConverter.GetBytes(position.z), 0, intByteLength);

            Quaternion rotation = rotationDictionarySaved[boneName][i];
            binaryWriter.Write(BitConverter.GetBytes(rotation.x), 0, intByteLength);
            binaryWriter.Write(BitConverter.GetBytes(rotation.y), 0, intByteLength);
            binaryWriter.Write(BitConverter.GetBytes(rotation.z), 0, intByteLength);
            binaryWriter.Write(BitConverter.GetBytes(rotation.w), 0, intByteLength);

            byte[] interpolateBytes = new byte[64];
            binaryWriter.Write(interpolateBytes, 0, interpolateBytes.Length);
        });

        WriteMorphFrames(binaryWriter, morphSnapshot, frameCount, ShiftJIS, intByteLength);

        byte[] cameraFrameCount = BitConverter.GetBytes(0);
        binaryWriter.Write(cameraFrameCount, 0, intByteLength);

        byte[] lightFrameCount = BitConverter.GetBytes(0);
        binaryWriter.Write(lightFrameCount, 0, intByteLength);

        byte[] selfShadowCount = BitConverter.GetBytes(0);
        binaryWriter.Write(selfShadowCount, 0, intByteLength);

        WriteIkFooter(binaryWriter, ShiftJIS, intByteLength);
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

                    byte[] morphNameBytes = System.Text.Encoding.GetEncoding(shiftJis).GetBytes(morphName);
                    if (morphNameLength - morphNameBytes.Length < 0) { continue; }

                    action(morphName, i);
                }
            }
        }

        uint allMorphNumber = 0;
        LoopWithMorphCondition((a, b) => { allMorphNumber++; });
        byte[] faceFrameCount = BitConverter.GetBytes(allMorphNumber);
        binaryWriter.Write(faceFrameCount, 0, intByteLength);

        LoopWithMorphCondition((morphName, i) =>
        {
            byte[] morphNameBytes = System.Text.Encoding.GetEncoding(shiftJis).GetBytes(morphName);
            binaryWriter.Write(morphNameBytes, 0, morphNameBytes.Length);
            binaryWriter.Write(new byte[morphNameLength - morphNameBytes.Length], 0, morphNameLength - morphNameBytes.Length);

            byte[] frameNumberByte = BitConverter.GetBytes((ulong)i);
            binaryWriter.Write(frameNumberByte, 0, intByteLength);

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
        const int IKNameLength = 20;
        byte[] leftIKName = System.Text.Encoding.GetEncoding(shiftJis).GetBytes("左足ＩＫ");
        byte[] rightIKName = System.Text.Encoding.GetEncoding(shiftJis).GetBytes("右足ＩＫ");
        byte[] leftToeIKName = System.Text.Encoding.GetEncoding(shiftJis).GetBytes("左つま先ＩＫ");
        byte[] rightToeIKName = System.Text.Encoding.GetEncoding(shiftJis).GetBytes("右つま先ＩＫ");
        byte ikOn = Convert.ToByte(1);

        binaryWriter.Write(ikCount, 0, intByteLength);
        binaryWriter.Write(ikFrameNumber, 0, intByteLength);
        binaryWriter.Write(modelDisplay);
        binaryWriter.Write(ikNumber, 0, intByteLength);
        binaryWriter.Write(leftIKName, 0, leftIKName.Length);
        binaryWriter.Write(new byte[IKNameLength - leftIKName.Length], 0, IKNameLength - leftIKName.Length);
        binaryWriter.Write(ikOn);
        binaryWriter.Write(leftToeIKName, 0, leftToeIKName.Length);
        binaryWriter.Write(new byte[IKNameLength - leftToeIKName.Length], 0, IKNameLength - leftToeIKName.Length);
        binaryWriter.Write(ikOn);
        binaryWriter.Write(rightIKName, 0, rightIKName.Length);
        binaryWriter.Write(new byte[IKNameLength - rightIKName.Length], 0, IKNameLength - rightIKName.Length);
        binaryWriter.Write(ikOn);
        binaryWriter.Write(rightToeIKName, 0, rightToeIKName.Length);
        binaryWriter.Write(new byte[IKNameLength - rightToeIKName.Length], 0, IKNameLength - rightToeIKName.Length);
        binaryWriter.Write(ikOn);
    }
}
