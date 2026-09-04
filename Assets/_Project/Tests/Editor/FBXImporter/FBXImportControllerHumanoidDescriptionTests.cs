using System.IO;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public class FBXImportControllerHumanoidDescriptionTests
    {
        [Test]
        public void Given_CustomHumanoidMapping_When_ConfiguringImporter_Then_PreservesImportedSkeletonHierarchy()
        {
            string source = ReadControllerSource();

            Assert.That(source, Does.Contain("description.human = humanBones.ToArray();"));
            Assert.That(source, Does.Not.Contain("description.skeleton ="),
                "부모 정보를 설정할 수 없는 Transform 목록으로 Avatar skeleton을 덮어쓰면 안 됩니다.");
            Assert.That(
                source,
                Does.Not.Contain("AssetDatabase.LoadAllAssetsAtPath(relativePath)"),
                "FBX subasset Transform 목록은 원본 부모 계층을 보존하지 않습니다.");
        }

        [Test]
        public void Given_LegacyHumanoidDescription_When_ConfiguringImporter_Then_ResetsBeforeMapping()
        {
            string source = ReadControllerSource();
            int resetIndex = source.IndexOf("ResetExistingHumanoidDescription(");
            int mappingIndex = source.IndexOf("description.human = humanBones.ToArray();");

            Assert.That(resetIndex, Is.GreaterThanOrEqualTo(0),
                "이전 자동 임포트가 손상시킨 Humanoid description을 먼저 초기화해야 합니다.");
            Assert.That(mappingIndex, Is.GreaterThan(resetIndex),
                "골격 계층을 복구한 뒤 사용자 Human bone mapping을 적용해야 합니다.");
        }

        private static string ReadControllerSource()
        {
            string path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXImportController.cs");
            return File.ReadAllText(path);
        }
    }
}
