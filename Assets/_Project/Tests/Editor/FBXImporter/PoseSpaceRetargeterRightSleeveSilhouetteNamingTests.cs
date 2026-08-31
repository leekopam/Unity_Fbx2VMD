using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public sealed class PoseSpaceRetargeterRightSleeveSilhouetteNamingTests
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void Given_RightSleeveSilhouetteCorrection_When_CheckingPrivateBoundary_Then_UsesModelNeutralNames()
        {
            AssertMethod(
                "ShouldApplyRightSleeveSilhouetteLocalOffsetFrameGate",
                typeof(bool));
            AssertMethod(
                "ApplyRightSleeveSilhouetteLocalOffsetReference",
                typeof(void));
            AssertMethod(
                "ApplyRightSleeveSilhouetteLocalOffsetToTransform",
                typeof(void),
                typeof(Transform),
                typeof(Vector3));
            AssertMethod(
                "RestoreRightSleeveSilhouetteLocalOffsetReference",
                typeof(void));

            string[] modelSpecificNames =
            {
                "ShouldApplyYybRightSleeveSilhouetteLocalOffsetFrameGate",
                "ApplyYybRightSleeveSilhouetteLocalOffsetReference",
                "ApplyYybRightSleeveSilhouetteLocalOffsetToTransform",
                "RestoreYybRightSleeveSilhouetteLocalOffsetReference"
            };

            foreach (string methodName in modelSpecificNames)
            {
                Assert.That(
                    typeof(PoseSpaceRetargeter).GetMethod(methodName, PrivateInstance),
                    Is.Null,
                    $"범용 private 구현에 모델 고유 이름이 남아 있습니다: {methodName}");
            }
        }

        private static void AssertMethod(
            string methodName,
            System.Type returnType,
            params System.Type[] parameterTypes)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                methodName,
                PrivateInstance,
                null,
                parameterTypes,
                null);

            Assert.That(method, Is.Not.Null, $"private 메서드가 없습니다: {methodName}");
            Assert.That(method.ReturnType, Is.EqualTo(returnType));
        }
    }
}
