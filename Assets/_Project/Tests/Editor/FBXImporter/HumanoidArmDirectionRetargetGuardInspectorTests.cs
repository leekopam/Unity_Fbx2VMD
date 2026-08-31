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

        [Test]
        public void Given_ArmDirectionPipelineSettings_When_ReadingInspectorDescriptions_Then_UseModelNeutralNames()
        {
            AssertInspectorText<HeaderAttribute>(
                "_enableYybArmDirectionRetargetCorrection",
                attribute => attribute.header,
                "Humanoid Arm Direction Retarget Correction");
            AssertModelNeutralTooltip("_enableYybArmDirectionRetargetCorrection");
            AssertModelNeutralTooltip("_YybArmDirectionLeftSideWeightScale");
            AssertModelNeutralTooltip("_YybArmDirectionRightSideWeightScale");
            AssertModelNeutralTooltip("_logYybArmDirectionRetargetCorrection");
        }

        private static void AssertInspectorText<TAttribute>(
            string fieldName,
            System.Func<TAttribute, string> readText,
            string expectedText)
            where TAttribute : System.Attribute
        {
            FieldInfo field = typeof(FBXVmdPipeline).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, fieldName);

            TAttribute attribute = field.GetCustomAttribute<TAttribute>();

            Assert.That(attribute, Is.Not.Null, fieldName);
            Assert.That(readText(attribute), Is.EqualTo(expectedText), fieldName);
        }

        private static void AssertModelNeutralTooltip(string fieldName)
        {
            FieldInfo field = typeof(FBXVmdPipeline).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, fieldName);

            TooltipAttribute tooltip = field.GetCustomAttribute<TooltipAttribute>();

            Assert.That(tooltip, Is.Not.Null, fieldName);
            Assert.That(tooltip.tooltip, Is.Not.Empty, fieldName);
            Assert.That(tooltip.tooltip, Does.Not.Contain("YYB"), fieldName);
        }
    }
}
