using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonRunLogFormatterTests
    {
        [Test]
        public void Given_DefaultOptions_When_BuildingStartMessage_Then_FormatsProfileAndSentinels()
        {
            object options = CreateDefaultOptions();

            string message = BuildMessage("BuildStartMessage", options, "Head", false);

            Assert.That(
                message,
                Does.StartWith(
                    "[YybVisualComparisonBatchRunner] 시작: fbx=satisfaction_2.fbx, duration=31.00s, " +
                    "targetFrames=0, fingerCloseups=False"));
            Assert.That(message, Does.Contain("mmdIkDeltaGuardLimitOverrideVmd=none"));
            Assert.That(message, Does.Contain("yybArmSwingLimit=False/0.85"));
            Assert.That(message, Does.Contain("yybArmSleeveAnchor=False/True/0.83/0.00/85.0"));
            Assert.That(message, Does.Contain("diagnosticCapture=nonexnone"));
            Assert.That(message, Does.Contain("diagnosticFraming=none/none"));
            Assert.That(message, Does.EndWith("batchMode=False"));
        }

        [Test]
        public void Given_RuntimeOverrides_When_BuildingTraceMessage_Then_UsesExistingPrecision()
        {
            object options = CreateDefaultOptions();
            Write(options, "mmdIkDeltaGuardLimitOverrideVmd", 0.1234f);
            Write(options, "mmdIkDeltaGuardRecoveryHoldFrames", 5);
            Write(options, "diagnosticCaptureWidthOverride", 1920);
            Write(options, "diagnosticCaptureHeightOverride", 1080);
            Write(options, "diagnosticScreenshotPaddingOverride", 1.25f);
            Write(options, "diagnosticScreenshotVerticalViewportCenterOverride", 0.4f);

            string message = BuildMessage("BuildTraceMessage", options, "Tail");

            Assert.That(message, Does.StartWith("run started fbx=satisfaction_2.fbx duration=31.00s"));
            Assert.That(message, Does.Contain("mmdIkDeltaGuardLimitOverrideVmd=0.123"));
            Assert.That(message, Does.Contain("mmdIkDeltaGuardRecoveryHoldFrames=5"));
            Assert.That(message, Does.Contain("segment=Tail"));
            Assert.That(message, Does.Contain("diagnosticCapture=1920x1080"));
            Assert.That(message, Does.EndWith("diagnosticFraming=1.25/0.4"));
        }

        [Test]
        public void Given_ExtractedFormatter_When_CheckingRunner_Then_LogFormattingHelpersAreRemoved()
        {
            BindingFlags privateStatic = BindingFlags.NonPublic | BindingFlags.Static;

            Assert.That(
                typeof(YybVisualComparisonBatchRunner)
                    .GetMethods(privateStatic)
                    .Where(method => method.Name == "FormatRuntimeOverride"),
                Is.Empty);
            Assert.That(
                typeof(YybVisualComparisonBatchRunner).GetMethod(
                    "FormatDiagnosticScreenshotFramingOverride",
                    privateStatic),
                Is.Null);
        }

        private static object CreateDefaultOptions()
        {
            Type profileType = FindRuntimeType("YybVisualComparisonRunProfile");
            MethodInfo method = profileType.GetMethod(
                "CreateDefaultOptions",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return method.Invoke(null, null);
        }

        private static string BuildMessage(string methodName, params object[] arguments)
        {
            Type formatterType = FindRuntimeType("YybVisualComparisonRunLogFormatter");
            MethodInfo method = formatterType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, arguments);
        }

        private static Type FindRuntimeType(string typeName)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                $"Fbx2Vmd.FBXImporter.{typeName}",
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{typeName} 타입이 필요합니다.");
            return type;
        }

        private static void Write(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            field.SetValue(target, value);
        }
    }
}
