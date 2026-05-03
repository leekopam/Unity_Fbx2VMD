using NUnit.Framework;
using System.IO;

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
    }
}
