using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

namespace Tests.Editor.VMDRecorderSample
{
    public class VmdFileWriterTests
    {
        [Test]
        public void Given_MinimalFrames_When_WritingVmd_Then_HeaderAndKeyframeCountMatch()
        {
            List<BoneNames> allBones = Enum
                .GetValues(typeof(BoneNames))
                .Cast<BoneNames>()
                .Where(b => b != BoneNames.None)
                .ToList();

            Assume.That(allBones.Count >= 2, "Need at least 2 bones for this test");

            List<BoneNames> activeBones = allBones.Take(2).ToList();

            int frameCount = 5;
            int keyReductionLevel = 2; // frames: 0,2,4
            int framesWritten = (int)Math.Ceiling(frameCount / (double)keyReductionLevel);
            uint expectedKeyframeCount = (uint)(activeBones.Count * framesWritten);

            var positions = new Dictionary<BoneNames, List<Vector3>>();
            var rotations = new Dictionary<BoneNames, List<Quaternion>>();

            foreach (BoneNames bone in activeBones)
            {
                var p = new List<Vector3>(frameCount);
                var r = new List<Quaternion>(frameCount);
                for (int i = 0; i < frameCount; i++)
                {
                    p.Add(new Vector3(i, i * 2, i * 3));
                    r.Add(Quaternion.identity);
                }

                positions.Add(bone, p);
                rotations.Add(bone, r);
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, $"vmd_writer_{Guid.NewGuid():N}.vmd");

            try
            {
                VmdFileWriter.WriteVmdFile(
                    modelName: "testModel",
                    filePath: filePath,
                    activeBones: activeBones,
                    frameCount: frameCount,
                    keyReductionLevel: keyReductionLevel,
                    positionDictionarySaved: positions,
                    rotationDictionarySaved: rotations,
                    morphSnapshot: null,
                    useCenterAsParentOfAll: false,
                    centerNameString: "CENTER",
                    grooveNameString: "GROOVE");

                byte[] bytes = File.ReadAllBytes(filePath);

                byte[] signature = System.Text.Encoding.ASCII.GetBytes("Vocaloid Motion Data 0002");
                Assert.GreaterOrEqual(bytes.Length, 30 + 20 + 4, "VMD file must contain header + keyframe count");
                CollectionAssert.AreEqual(signature, bytes.Take(signature.Length).ToArray(), "VMD signature mismatch");

                int keyFrameCountOffset = 30 + 20;
                uint actualKeyframeCount = BitConverter.ToUInt32(bytes, keyFrameCountOffset);
                Assert.AreEqual(expectedKeyframeCount, actualKeyframeCount, "Bone keyframe count mismatch");
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
    }
}

