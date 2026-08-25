using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonRunProfileTests
    {
        [Test]
        public void Given_DefaultProfile_When_CreatingOptions_Then_PreservesGenericAndYybValues()
        {
            object options = CreateDefaultOptions();

            Assert.That(Read<string>(options, "fbxFileName"), Is.EqualTo("satisfaction_2.fbx"));
            Assert.That(Read<float>(options, "durationSeconds"), Is.EqualTo(31f));
            Assert.That(
                Read<bool>(options, "enableRecorderParentFrameIkOffsetsWhenCenterParented"),
                Is.True);
            Assert.That(Read<float>(options, "mmdIkDeltaGuardLimitOverrideVmd"), Is.NaN);
            Assert.That(Read<int>(options, "mmdIkDeltaGuardRecoveryHoldFrames"), Is.EqualTo(-1));
            Assert.That(
                Read<float>(options, "manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight"),
                Is.EqualTo(0.125f));
            Assert.That(Read<bool>(options, "enableRetargetPoseVisualSpikeSmoothingRuntimeOverride"), Is.True);
            Assert.That(Read<float>(options, "retargetPoseVisualSpikeCurrentWeight"), Is.EqualTo(0.65f));
            Assert.That(Read<bool>(options, "enableYybArmSwingLimitRuntimeOverride"), Is.False);
            Assert.That(Read<float>(options, "yybArmSwingLimitWeight"), Is.EqualTo(0.85f));
            Assert.That(Read<bool>(options, "enableYybArmSleeveAnchorRuntimeOverride"), Is.True);
            Assert.That(Read<float>(options, "yybArmSleeveAnchorInfluence"), Is.EqualTo(0.825f));
            Assert.That(Read<bool>(options, "enableYybArmVisualTwistRuntimeOverride"), Is.True);
            Assert.That(Read<int>(options, "diagnosticCaptureWidthOverride"), Is.Zero);
            Assert.That(Read<float>(options, "diagnosticScreenshotPaddingOverride"), Is.NaN);
            Assert.That(Read<string>(options, "editorDiagnosticSmokeSegment"), Is.EqualTo("head"));
        }

        [Test]
        public void Given_DefaultProfile_When_CreatingTwice_Then_ReturnsIndependentOptions()
        {
            object first = CreateDefaultOptions();
            object second = CreateDefaultOptions();

            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void Given_ExtractedProfile_When_CheckingRunner_Then_DefaultCreationIsNotOwnedByRunner()
        {
            BindingFlags privateStatic = BindingFlags.NonPublic | BindingFlags.Static;

            Assert.That(
                typeof(YybVisualComparisonBatchRunner).GetMethod("CreateDefaultRunOptions", privateStatic),
                Is.Null);
            Assert.That(
                typeof(YybVisualComparisonBatchRunner).GetField("DefaultFbxFileName", privateStatic),
                Is.Null);
            Assert.That(
                typeof(YybVisualComparisonBatchRunner).GetField("DefaultYybArmSwingLimitWeight", privateStatic),
                Is.Null);
        }

        private static object CreateDefaultOptions()
        {
            Type profileType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunProfile",
                throwOnError: false);
            Assert.That(profileType, Is.Not.Null, "YYB 실행 기본값을 소유하는 concrete 프로필이 필요합니다.");

            MethodInfo method = profileType.GetMethod(
                "CreateDefaultOptions",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, null);
        }

        private static T Read<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            return (T)field.GetValue(target);
        }
    }
}
