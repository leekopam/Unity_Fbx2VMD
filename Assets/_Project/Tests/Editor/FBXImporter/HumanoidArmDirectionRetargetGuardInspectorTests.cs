using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidArmDirectionRetargetGuardInspectorTests
    {
        [Test]
        public void Given_HumanoidArmDirectionGuard_When_ReadingInspectorHeader_Then_UsesModelNeutralName()
        {
            FieldInfo enableField = typeof(HumanoidArmDirectionRetargetGuard).GetField(
                "_enableDirectionRetarget",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(enableField, Is.Not.Null);

            HeaderAttribute header = enableField.GetCustomAttribute<HeaderAttribute>();

            Assert.That(header, Is.Not.Null);
            Assert.That(header.header, Is.EqualTo("Humanoid Arm Direction Retarget Guard"));
        }
    }
}
