using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonRunOptionsResetterTests
    {
        [Test]
        public void Given_RunOptions_When_ResettingTransientSettings_Then_PreservesLaunchSettings()
        {
            object options = CreateOptions();
            Set(options, "fbxFileName", "future-model-motion.fbx");
            Set(options, "durationSeconds", 42f);
            Set(options, "targetFrameCount", 1260);
            Set(options, "enableFingerCloseups", true);
            Set(options, "enableRecorderParentFrameIkOffsetsWhenCenterParented", false);
            Set(options, "editorDiagnosticSmokeSegment", "tail");
            Set(options, "diagnosticCaptureWidthOverride", 1440);
            Set(options, "diagnosticCaptureHeightOverride", 1080);
            Set(options, "diagnosticScreenshotPaddingOverride", 0.2f);
            Set(options, "diagnosticScreenshotVerticalViewportCenterOverride", 0.6f);
            Set(options, "enableFinalIkFootGroundingRuntimeOverride", true);
            Set(options, "enableYybArmSwingLimitRuntimeOverride", true);
            Set(options, "vmdPlaybackProbeSourceVmdPath", "candidate.vmd");
            object defaults = CreateOptions();
            Set(defaults, "fbxFileName", "default.fbx");
            Set(defaults, "durationSeconds", 31f);
            Set(defaults, "targetFrameCount", 930);
            Set(defaults, "enableRecorderParentFrameIkOffsetsWhenCenterParented", true);
            Set(defaults, "editorDiagnosticSmokeSegment", "head");

            Reset(options, defaults);

            Assert.That(Get<string>(options, "fbxFileName"), Is.EqualTo("future-model-motion.fbx"));
            Assert.That(Get<float>(options, "durationSeconds"), Is.EqualTo(42f));
            Assert.That(Get<int>(options, "targetFrameCount"), Is.EqualTo(1260));
            Assert.That(Get<bool>(options, "enableFingerCloseups"), Is.True);
            Assert.That(Get<bool>(options, "enableRecorderParentFrameIkOffsetsWhenCenterParented"), Is.False);
            Assert.That(Get<string>(options, "editorDiagnosticSmokeSegment"), Is.EqualTo("tail"));
            Assert.That(Get<int>(options, "diagnosticCaptureWidthOverride"), Is.EqualTo(1440));
            Assert.That(Get<int>(options, "diagnosticCaptureHeightOverride"), Is.EqualTo(1080));
            Assert.That(Get<float>(options, "diagnosticScreenshotPaddingOverride"), Is.EqualTo(0.2f));
            Assert.That(Get<float>(options, "diagnosticScreenshotVerticalViewportCenterOverride"), Is.EqualTo(0.6f));
        }

        [Test]
        public void Given_RunOptions_When_ResettingTransientSettings_Then_AppliesGenericAndYybDefaults()
        {
            object options = CreateOptions();
            Set(options, "enableFinalIkFootGroundingRuntimeOverride", true);
            Set(options, "enableYybArmSwingLimitRuntimeOverride", true);
            Set(options, "vmdPlaybackProbeSourceVmdPath", "candidate.vmd");
            object defaults = CreateOptions();

            Reset(options, defaults);

            Assert.That(Get<bool>(options, "enableFinalIkFootGroundingRuntimeOverride"), Is.False);
            Assert.That(Get<bool>(options, "enableYybArmSwingLimitRuntimeOverride"), Is.False);
            Assert.That(Get<string>(options, "vmdPlaybackProbeSourceVmdPath"), Is.Null);
        }

        [Test]
        public void Given_NullOptions_When_ResettingTransientSettings_Then_Throws()
        {
            TargetInvocationException missingOptions = Assert.Throws<TargetInvocationException>(() =>
                Reset(null, CreateOptions()));
            TargetInvocationException missingDefaults = Assert.Throws<TargetInvocationException>(() =>
                Reset(CreateOptions(), null));

            Assert.That(missingOptions.InnerException, Is.TypeOf<ArgumentNullException>());
            Assert.That(missingDefaults.InnerException, Is.TypeOf<ArgumentNullException>());
        }

        private static object CreateOptions()
        {
            return Activator.CreateInstance(GetOptionsType(), nonPublic: true);
        }

        private static Type GetOptionsType()
        {
            return typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptions",
                throwOnError: true);
        }

        private static void Reset(object options, object defaults)
        {
            Type resetterType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptionsResetter",
                throwOnError: false);
            Assert.That(resetterType, Is.Not.Null, "YYB 실행 옵션 초기화 정책 경계가 필요합니다.");
            MethodInfo reset = resetterType.GetMethod(
                "ResetTransientSettings",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(reset, Is.Not.Null);
            reset.Invoke(null, new[] { options, defaults });
        }

        private static void Set(object target, string fieldName, object value)
        {
            GetOptionsType().GetField(fieldName).SetValue(target, value);
        }

        private static T Get<T>(object target, string fieldName)
        {
            return (T)GetOptionsType().GetField(fieldName).GetValue(target);
        }
    }
}
