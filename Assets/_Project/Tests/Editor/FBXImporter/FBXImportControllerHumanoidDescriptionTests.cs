using System;
using System.IO;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;

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

        [Test]
        public void Given_HumanoidMotionClip_When_ApplyingImportPolicy_Then_BakesOriginalRootRotationIntoPose()
        {
            var clips = new[]
            {
                new ModelImporterClipAnimation
                {
                    lockRootRotation = false,
                    keepOriginalOrientation = false
                }
            };

            Type policyType = typeof(FBXImportController).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidClipImportPolicy");
            MethodInfo applyMethod = policyType?.GetMethod(
                "ApplyRootRotationContract",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null);

            applyMethod.Invoke(null, new object[] { clips });

            Assert.That(clips[0].lockRootRotation, Is.True,
                "root 회전은 applyRootMotion=false 재생에서도 사라지지 않도록 본 자세에 구워야 합니다.");
            Assert.That(clips[0].keepOriginalOrientation, Is.True,
                "모션캡처 FBX의 원본 방향을 Humanoid body orientation으로 대체하면 안 됩니다.");
        }

        [Test]
        public void Given_UnbakedHumanoidMotionClip_When_CheckingImportPolicy_Then_RequiresMigration()
        {
            var clips = new[]
            {
                new ModelImporterClipAnimation
                {
                    lockRootRotation = false,
                    keepOriginalOrientation = true
                }
            };
            MethodInfo contractMethod = FindPolicyMethod("HasRootRotationContract");

            bool hasContract = (bool)contractMethod.Invoke(null, new object[] { clips });

            Assert.That(hasContract, Is.False);
        }

        [Test]
        public void Given_ControlledImportWithLegacyClipSettings_When_PreparingImport_Then_MigratesRootRotationContract()
        {
            string source = ReadControllerSource();

            Assert.That(
                source,
                Does.Contain("TryEnsureHumanoidClipRootRotationContract(targetPath)"),
                "제어 폴더에 이미 있는 FBX도 이전 clip 설정이면 1회 마이그레이션해야 합니다.");
        }

        private static MethodInfo FindPolicyMethod(string methodName)
        {
            Type policyType = typeof(FBXImportController).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidClipImportPolicy");
            MethodInfo method = policyType?.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
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
