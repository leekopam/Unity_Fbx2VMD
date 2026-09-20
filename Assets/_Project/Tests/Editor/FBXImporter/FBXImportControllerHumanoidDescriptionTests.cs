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
                "ApplyRootPoseContract",
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
            MethodInfo contractMethod = FindPolicyMethod("HasRootPoseContract");

            bool hasContract = (bool)contractMethod.Invoke(null, new object[] { clips });

            Assert.That(hasContract, Is.False);
        }

        [Test]
        public void Given_ControlledImportWithLegacyClipSettings_When_PreparingImport_Then_MigratesRootPoseContract()
        {
            string source = ReadControllerSource();

            Assert.That(
                source,
                Does.Contain("TryEnsureHumanoidClipRootPoseContract(targetPath)"),
                "제어 폴더에 이미 있는 FBX도 이전 clip 설정이면 1회 마이그레이션해야 합니다.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Given_RotationBakedClip_When_ApplyingRootPoseContract_Then_PreservesHeightMotionAndOtherSettings(bool bakeXZ)
        {
            var clips = new[]
            {
                new ModelImporterClipAnimation
                {
                    name = "높이 이동 검증",
                    lockRootRotation = true,
                    keepOriginalOrientation = true,
                    lockRootHeightY = false,
                    lockRootPositionXZ = bakeXZ,
                    keepOriginalPositionXZ = true,
                    keepOriginalPositionY = true,
                    heightFromFeet = false,
                    heightOffset = 0.125f,
                    firstFrame = 12f,
                    lastFrame = 240f
                }
            };
            MethodInfo check = FindPolicyMethod("HasRootPoseContract");
            MethodInfo apply = FindPolicyMethod("ApplyRootPoseContract");

            Assert.That((bool)check.Invoke(null, new object[] { clips }), Is.False,
                "회전 설정이 맞아도 Y 이동이 분리된 기존 캐시는 보정해야 합니다.");
            apply.Invoke(null, new object[] { clips });

            Assert.That(clips[0].lockRootHeightY, Is.True);
            Assert.That((bool)check.Invoke(null, new object[] { clips }), Is.True,
                "적용 후 같은 캐시를 다시 임포트하지 않아야 합니다.");
            Assert.That(clips[0].lockRootPositionXZ, Is.EqualTo(bakeXZ));
            Assert.That(clips[0].keepOriginalPositionXZ, Is.True);
            Assert.That(clips[0].keepOriginalPositionY, Is.True);
            Assert.That(clips[0].heightFromFeet, Is.False);
            Assert.That(clips[0].heightOffset, Is.EqualTo(0.125f));
            Assert.That(clips[0].firstFrame, Is.EqualTo(12f));
            Assert.That(clips[0].lastFrame, Is.EqualTo(240f));
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
