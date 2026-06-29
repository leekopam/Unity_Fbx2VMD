using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

namespace Tests.Editor.VMDRecorderSample
{
    public class VmdMotionReaderTests
    {
        [Test]
        public void Given_WriterOutput_When_ReadingVmd_Then_HeaderAndCenterFramesAreAvailable()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, $"vmd_reader_writer_output_{Guid.NewGuid():N}.vmd");

            try
            {
                WriteWriterOutput(filePath);

                VmdMotionData motion = VmdMotionReader.Read(filePath);

                Assert.That(motion.Header, Is.EqualTo("Vocaloid Motion Data 0002"));
                Assert.That(motion.ModelName, Is.EqualTo("readerModel"));
                Assert.That(motion.BoneFrames.Count, Is.GreaterThan(0));

                VmdBoneFrame[] centerFrames = motion.BoneFrames
                    .Where(frame => frame.BoneName == "\u30bb\u30f3\u30bf\u30fc")
                    .ToArray();
                Assert.That(centerFrames.Length, Is.EqualTo(3));
                CollectionAssert.AreEqual(new[] { 0u, 1u, 2u }, centerFrames.Select(frame => frame.FrameIndex).ToArray());
                Assert.That(centerFrames[2].Position, Is.EqualTo(new Vector3(2f, 0.5f, -1f)));
                Assert.That(Quaternion.Angle(centerFrames[2].Rotation, Quaternion.Euler(0f, 20f, 0f)), Is.LessThan(0.001f));
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Test]
        public void Given_VmdWithMorphFrame_When_ReadingVmd_Then_MorphFrameIsPreservedWithoutPlayback()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, $"vmd_reader_morph_{Guid.NewGuid():N}.vmd");

            try
            {
                WriteMinimalVmdWithOneMorph(filePath);

                VmdMotionData motion = VmdMotionReader.Read(filePath);

                Assert.That(motion.BoneFrames.Count, Is.EqualTo(0));
                Assert.That(motion.MorphFrames.Count, Is.EqualTo(1));
                Assert.That(motion.MorphFrames[0].MorphName, Is.EqualTo("blink"));
                Assert.That(motion.MorphFrames[0].FrameIndex, Is.EqualTo(12u));
                Assert.That(motion.MorphFrames[0].Weight, Is.EqualTo(0.75f).Within(0.0001f));
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        private static void WriteWriterOutput(string filePath)
        {
            var activeBones = new List<BoneNames>
            {
                (BoneNames)1,
                (BoneNames)6
            };
            var positions = new Dictionary<BoneNames, List<Vector3>>
            {
                [(BoneNames)1] = new List<Vector3>
                {
                    Vector3.zero,
                    new Vector3(1f, 0.25f, -0.5f),
                    new Vector3(2f, 0.5f, -1f)
                },
                [(BoneNames)6] = new List<Vector3>
                {
                    Vector3.zero,
                    Vector3.zero,
                    Vector3.zero
                }
            };
            var rotations = new Dictionary<BoneNames, List<Quaternion>>
            {
                [(BoneNames)1] = new List<Quaternion>
                {
                    Quaternion.identity,
                    Quaternion.Euler(0f, 10f, 0f),
                    Quaternion.Euler(0f, 20f, 0f)
                },
                [(BoneNames)6] = new List<Quaternion>
                {
                    Quaternion.identity,
                    Quaternion.identity,
                    Quaternion.identity
                }
            };

            VmdFileWriter.WriteVmdFile(
                modelName: "readerModel",
                filePath: filePath,
                activeBones: activeBones,
                frameCount: 3,
                keyReductionLevel: 1,
                positionDictionarySaved: positions,
                rotationDictionarySaved: rotations,
                morphSnapshot: null,
                useCenterAsParentOfAll: false,
                routeCenterBoneToGroove: false,
                centerNameString: "CENTER",
                grooveNameString: "GROOVE");
        }

        private static void WriteMinimalVmdWithOneMorph(string filePath)
        {
            using FileStream stream = new FileStream(filePath, FileMode.Create);
            using BinaryWriter writer = new BinaryWriter(stream);

            WriteShiftJisFixedString(writer, "Vocaloid Motion Data 0002", 30);
            WriteShiftJisFixedString(writer, "morphModel", 20);
            writer.Write(0u);
            writer.Write(1u);
            WriteShiftJisFixedString(writer, "blink", 15);
            writer.Write(12u);
            writer.Write(0.75f);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0u);
        }

        private static void WriteShiftJisFixedString(BinaryWriter writer, string value, int byteLength)
        {
            byte[] bytes = Encoding.GetEncoding("shift_jis").GetBytes(value);
            Assert.That(bytes.Length, Is.LessThanOrEqualTo(byteLength));

            writer.Write(bytes);
            writer.Write(new byte[byteLength - bytes.Length]);
        }
    }
}
