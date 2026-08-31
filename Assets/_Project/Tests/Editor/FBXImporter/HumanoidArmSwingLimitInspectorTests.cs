using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidArmSwingLimitInspectorTests
    {
        [Test]
        public void Given_ArmSwingLimitSettings_When_ReadingInspectorDescriptions_Then_UseKoreanModelNeutralNames()
        {
            FieldInfo guardEnableField = FindField(
                typeof(HumanoidArmSwingLimitGuard),
                "_enableSwingLimit");
            FieldInfo pipelineEnableField = FindField(
                typeof(FBXVmdPipeline),
                "_enableYybArmSwingLimitCorrection");

            AssertHeader(guardEnableField, "휴머노이드 팔 스윙 제한");
            AssertHeader(pipelineEnableField, "휴머노이드 팔 해부학적 스윙 보정");

            FieldInfo[] guardSettings = FindSerializedFields(typeof(HumanoidArmSwingLimitGuard));
            FieldInfo[] pipelineSettings = FindSerializedFields(typeof(FBXVmdPipeline))
                .Where(field => field.Name.IndexOf(
                    "YybArmSwing",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            Assert.That(guardSettings, Has.Length.EqualTo(15));
            Assert.That(pipelineSettings, Has.Length.EqualTo(14));
            AssertKoreanModelNeutralTooltips(guardSettings);
            AssertKoreanModelNeutralTooltips(pipelineSettings);
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"{type.Name}.{fieldName}");
            return field;
        }

        private static FieldInfo[] FindSerializedFields(Type type)
        {
            return type
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.GetCustomAttribute<SerializeField>() != null)
                .ToArray();
        }

        private static void AssertHeader(FieldInfo field, string expectedText)
        {
            HeaderAttribute header = field.GetCustomAttribute<HeaderAttribute>();

            Assert.That(header, Is.Not.Null, field.Name);
            Assert.That(header.header, Is.EqualTo(expectedText), field.Name);
        }

        private static void AssertKoreanModelNeutralTooltips(FieldInfo[] fields)
        {
            foreach (FieldInfo field in fields)
            {
                TooltipAttribute tooltip = field.GetCustomAttribute<TooltipAttribute>();

                Assert.That(tooltip, Is.Not.Null, field.Name);
                Assert.That(tooltip.tooltip, Does.Match("[가-힣]"), field.Name);
                Assert.That(
                    tooltip.tooltip.IndexOf("YYB", StringComparison.OrdinalIgnoreCase),
                    Is.EqualTo(-1),
                    field.Name);
            }
        }
    }
}
