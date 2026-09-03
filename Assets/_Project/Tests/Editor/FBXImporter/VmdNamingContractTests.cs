using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tests.Editor.FBXImporter
{
    /// <summary>
    /// AGENTS.md VMD 명명 규칙 계약 검증:
    /// 출력 VMD 파일명 = Path.GetFileNameWithoutExtension(입력 FBX 경로)
    /// </summary>
    public class VmdNamingContractTests
    {
        // AGENTS.md에 명시된 표준 FBX 파일명 목록
        [TestCase("satisfaction_2.fbx", "satisfaction_2")]
        [TestCase("Antenna39 try_006 g.fbx", "Antenna39 try_006 g")]
        [TestCase("Snake Hip Hop Dance.fbx", "Snake Hip Hop Dance")]
        [TestCase("mikumikuni_retake_000.fbx", "mikumikuni_retake_000")]
        [TestCase("neo_1_001.fbx", "neo_1_001")]
        public void Given_FbxFileName_When_ExtractingBaseNameForVmd_Then_MatchesFbxBaseName(
            string fbxFileName, string expectedBaseName)
        {
            string actualBaseName = Path.GetFileNameWithoutExtension(fbxFileName);
            Assert.AreEqual(expectedBaseName, actualBaseName,
                $"VMD base name must equal FBX base name: '{fbxFileName}' → '{expectedBaseName}'");
        }

        // 전체 경로에서도 base name이 올바르게 추출되는지 확인 (슬래시 경로만 사용 — CI는 ubuntu-latest)
        [TestCase("Assets/Resources/Import_FBX/satisfaction_2.fbx", "satisfaction_2")]
        [TestCase("Assets/Resources/Import_FBX/Antenna39 try_006 g.fbx", "Antenna39 try_006 g")]
        [TestCase("Assets/Resources/Import_FBX/neo_1_001.fbx", "neo_1_001")]
        public void Given_FbxFullPath_When_ExtractingBaseNameForVmd_Then_ExtractedCorrectly(
            string fbxPath, string expectedBaseName)
        {
            string actualBaseName = Path.GetFileNameWithoutExtension(fbxPath);
            Assert.AreEqual(expectedBaseName, actualBaseName,
                $"전체 경로에서 FBX base name을 올바르게 추출해야 한다");
        }

        // 자동 suffix가 추가되어서는 안 된다는 계약 명세
        [TestCase("satisfaction_2.fbx", "_001")]
        [TestCase("satisfaction_2.fbx", "auto_")]
        [TestCase("satisfaction_2.fbx", "manual_")]
        [TestCase("satisfaction_2.fbx", "_output")]
        public void Given_FbxFileName_When_ExtractingBaseNameForVmd_Then_NoAutoSuffixAdded(
            string fbxFileName, string forbiddenSuffix)
        {
            string baseName = Path.GetFileNameWithoutExtension(fbxFileName);
            bool containsSuffix = baseName.Contains(forbiddenSuffix);
            Assert.IsFalse(containsSuffix,
                $"VMD base name '{baseName}'에 금지된 suffix '{forbiddenSuffix}'가 포함되면 안 된다 (AGENTS.md 규칙)");
        }

        // 확장자 대소문자 변형에도 base name이 동일해야 한다
        [TestCase("satisfaction_2.fbx")]
        [TestCase("satisfaction_2.FBX")]
        [TestCase("satisfaction_2.Fbx")]
        public void Given_FbxFileNameWithVariousExtensionCasing_When_ExtractingBaseName_Then_BaseNameUnchanged(
            string fbxFileName)
        {
            string baseName = Path.GetFileNameWithoutExtension(fbxFileName);
            Assert.AreEqual("satisfaction_2", baseName,
                "확장자 대소문자와 무관하게 base name은 동일해야 한다");
        }

        // 공백이 포함된 파일명의 base name 유지 확인
        [Test]
        public void Given_FbxFileNameWithSpaces_When_ExtractingBaseName_Then_SpacesPreserved()
        {
            string fbxFileName = "Snake Hip Hop Dance.fbx";
            string baseName = Path.GetFileNameWithoutExtension(fbxFileName);
            Assert.IsTrue(baseName.Contains(" "),
                "파일명 내 공백은 base name에 그대로 유지되어야 한다");
            Assert.AreEqual("Snake Hip Hop Dance", baseName);
        }
        [Test]
        public void Given_ExportedYybVmd_When_ReadingBoneNames_Then_MmdStandardNamesAreWritten()
        {
            string vmdPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "VMDRecorderSample", "smoke_satisfaction_2_208s.vmd");

            Assert.That(File.Exists(vmdPath), Is.True, "The YYB smoke VMD must exist before validating MMD bone names.");

            byte[] bytes = File.ReadAllBytes(vmdPath);
            const int headerLength = 30 + 20;
            const int boneFrameSize = 15 + 4 + 12 + 16 + 64;
            Assert.That(bytes.Length, Is.GreaterThan(headerLength + 4), "VMD must contain a bone frame table.");

            uint boneKeyFrameCount = BitConverter.ToUInt32(bytes, headerLength);
            int offset = headerLength + 4;
            Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(offset + (boneKeyFrameCount * boneFrameSize)));

            Encoding shiftJis = Encoding.GetEncoding(932);
            HashSet<string> names = new HashSet<string>();
            HashSet<uint> centerFrames = new HashSet<uint>();
            HashSet<uint> lowerBodyFrames = new HashSet<uint>();
            uint maxFrame = 0;
            int centerKeyCount = 0;
            int centerNonIdentityRotationCount = 0;
            int lowerBodyKeyCount = 0;
            int lowerBodyNonIdentityRotationCount = 0;
            int grooveKeyCount = 0;
            int grooveNonIdentityRotationCount = 0;

            const string centerName = "\u30bb\u30f3\u30bf\u30fc";
            const string lowerBodyName = "\u4e0b\u534a\u8eab";
            const string grooveName = "\u30b0\u30eb\u30fc\u30d6";

            for (int i = 0; i < boneKeyFrameCount; i++)
            {
                int frameOffset = offset + (i * boneFrameSize);
                int length = 0;
                while (length < 15 && bytes[frameOffset + length] != 0)
                {
                    length++;
                }

                string boneName = shiftJis.GetString(bytes, frameOffset, length);
                uint frame = BitConverter.ToUInt32(bytes, frameOffset + 15);
                bool hasNonIdentityRotation = HasNonIdentityRotation(bytes, frameOffset);

                names.Add(boneName);
                maxFrame = Math.Max(maxFrame, frame);

                if (boneName == centerName)
                {
                    centerKeyCount++;
                    centerFrames.Add(frame);
                    if (hasNonIdentityRotation)
                    {
                        centerNonIdentityRotationCount++;
                    }
                }
                else if (boneName == lowerBodyName)
                {
                    lowerBodyKeyCount++;
                    lowerBodyFrames.Add(frame);
                    if (hasNonIdentityRotation)
                    {
                        lowerBodyNonIdentityRotationCount++;
                    }
                }
                else if (boneName == grooveName)
                {
                    grooveKeyCount++;
                    if (hasNonIdentityRotation)
                    {
                        grooveNonIdentityRotationCount++;
                    }
                }
            }

            Assert.That(maxFrame, Is.EqualTo(6233), "Full Satisfaction VMD must match the complete 0..6233 clip range (6,234 frames).");
            int expectedFrameCount = checked((int)maxFrame + 1);
            Assert.That(centerKeyCount, Is.EqualTo(expectedFrameCount), "MMD center translation must be dense across the full recording.");
            Assert.That(centerFrames.Count, Is.EqualTo(expectedFrameCount), "MMD center translation must not contain skipped or duplicate frames.");
            Assert.That(centerNonIdentityRotationCount, Is.EqualTo(0), "MMD center rotation must stay identity; hips rotation belongs on lower-body.");
            Assert.That(lowerBodyKeyCount, Is.EqualTo(expectedFrameCount), "MMD lower-body must be exported as the hips rotation carrier.");
            Assert.That(lowerBodyFrames.Count, Is.EqualTo(expectedFrameCount), "MMD lower-body rotation carrier must be dense across the full recording.");
            Assert.That(lowerBodyNonIdentityRotationCount, Is.EqualTo(expectedFrameCount), "MMD lower-body must carry the recorded hips rotation.");
            Assert.That(grooveKeyCount, Is.EqualTo(0), "YYB export must not route hips rotation through groove.");
            Assert.That(grooveNonIdentityRotationCount, Is.EqualTo(0), "Groove rotation must not move the whole model.");
            Assert.That(names, Does.Contain("\u5168\u3066\u306e\u89aa"));
            Assert.That(names, Does.Contain("\u30bb\u30f3\u30bf\u30fc"));
            Assert.That(names, Does.Contain("\u4e0b\u534a\u8eab"));
            Assert.That(names, Does.Contain("\u5de6\u8db3\uff29\uff2b"));
            Assert.That(names, Does.Contain("\u53f3\u8db3\uff29\uff2b"));
            Assert.That(names, Does.Contain("\u5de6\u3064\u307e\u5148\uff29\uff2b"));
            Assert.That(names, Does.Contain("\u53f3\u3064\u307e\u5148\uff29\uff2b"));
            Assert.That(names, Does.Not.Contain("\u5de6\u8db3IK"));
            Assert.That(names, Does.Not.Contain("\u53f3\u8db3IK"));
            Assert.That(names, Does.Contain("\u5de6\u8155"));
            Assert.That(names, Does.Contain("\u53f3\u8155"));
            Assert.That(names, Does.Not.Contain("Null_00"));
            Assert.That(names, Does.Not.Contain("Null_330"));
        }

        private static bool HasNonIdentityRotation(byte[] bytes, int boneFrameOffset)
        {
            float x = BitConverter.ToSingle(bytes, boneFrameOffset + 31);
            float y = BitConverter.ToSingle(bytes, boneFrameOffset + 35);
            float z = BitConverter.ToSingle(bytes, boneFrameOffset + 39);
            float w = BitConverter.ToSingle(bytes, boneFrameOffset + 43);

            const float tolerance = 0.0001f;
            return Math.Abs(x) > tolerance ||
                Math.Abs(y) > tolerance ||
                Math.Abs(z) > tolerance ||
                Math.Abs(w - 1f) > tolerance;
        }
    }
}
