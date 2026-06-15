using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.Graphics
{
    public class RecordingCaptureResolutionTests
    {
        private const string ResolutionTypeName =
            "Member_Han.Modules.Graphics.RecordingCaptureResolution, Assembly-CSharp";
        private const string PresetTypeName =
            "Member_Han.Modules.Graphics.RecordingCaptureQualityPreset, Assembly-CSharp";
        private const string FileManagerTypeName =
            "Member_Han.Modules.FBXImporter.FileManager, Assembly-CSharp";
        private const string RecodingSettingTypeName =
            "RecodingSetting, Assembly-CSharp";
        private const string MotionComparisonProbeTypeName =
            "MotionComparisonProbe, Assembly-CSharp-firstpass";
        private const string HumanoidSampleCodeTypeName =
            "HumanoidSampleCode, Assembly-CSharp-firstpass";
        private const BindingFlags StaticMethods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void Given_RecordingCapturePresetIsUhd4K_When_CreatingPlan_Then_Uses3840By2160()
        {
            Type resolutionType = RequireType(ResolutionTypeName);
            Type presetType = RequireType(PresetTypeName);
            object preset = Enum.Parse(presetType, "Uhd4K");

            object plan = InvokeStatic(resolutionType, "CreatePlan", presetType, preset);

            Assert.That(GetMemberValue<int>(plan, "Width"), Is.EqualTo(3840));
            Assert.That(GetMemberValue<int>(plan, "Height"), Is.EqualTo(2160));
            Assert.That(GetMemberValue<int>(plan, "PixelCount"), Is.EqualTo(8294400));
        }

        [Test]
        public void Given_MotionComparisonProbe_When_SetRecordingCaptureResolution_Then_UsesClampedDimensions()
        {
            Type probeType = RequireType(MotionComparisonProbeTypeName);
            var gameObject = new GameObject("Motion Comparison Probe Resolution Test");

            try
            {
                var probe = gameObject.AddComponent(probeType);

                InvokeInstance(probe, "SetScreenshotCaptureResolution", 3840, 2160);

                Assert.That(GetMemberValue<int>(probe, "ScreenshotWidth"), Is.EqualTo(3840));
                Assert.That(GetMemberValue<int>(probe, "ScreenshotHeight"), Is.EqualTo(2160));

                InvokeInstance(probe, "SetScreenshotCaptureResolution", 32, 99999);

                Assert.That(GetMemberValue<int>(probe, "ScreenshotWidth"), Is.EqualTo(128));
                Assert.That(GetMemberValue<int>(probe, "ScreenshotHeight"), Is.EqualTo(4320));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Given_RecodingSetting_When_ApplyingCaptureResolution_Then_FileManagerReceivesUhd4KPlan()
        {
            Type fileManagerType = RequireType(FileManagerTypeName);
            Type recodingSettingType = RequireType(RecodingSettingTypeName);
            Type presetType = RequireType(PresetTypeName);
            var fileManagerObject = new GameObject("Recording Capture FileManager Test");
            var settingObject = new GameObject("Recording Capture RecodingSetting Test");

            try
            {
                var fileManager = fileManagerObject.AddComponent(fileManagerType);
                var recodingSetting = settingObject.AddComponent(recodingSettingType);
                object preset = Enum.Parse(presetType, "Uhd4K");

                SetField(recodingSetting, "recordingFileManager", fileManager);
                SetField(recodingSetting, "recordingCaptureQuality", preset);
                SetField(recodingSetting, "customRecordingCaptureWidth", 111);
                SetField(recodingSetting, "customRecordingCaptureHeight", 222);

                InvokeInstance(recodingSetting, "ApplyDiagnosticsToFileManager");
                object plan = InvokeInstance(fileManager, "CreateRecordingCaptureResolutionPlan");

                Assert.That(GetField<object>(fileManager, "recordingCaptureQuality").ToString(), Is.EqualTo("Uhd4K"));
                Assert.That(GetField<int>(fileManager, "customRecordingCaptureWidth"), Is.EqualTo(111));
                Assert.That(GetField<int>(fileManager, "customRecordingCaptureHeight"), Is.EqualTo(222));
                Assert.That(GetMemberValue<int>(plan, "Width"), Is.EqualTo(3840));
                Assert.That(GetMemberValue<int>(plan, "Height"), Is.EqualTo(2160));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
                UnityEngine.Object.DestroyImmediate(fileManagerObject);
            }
        }

        [Test]
        public void Given_HumanoidSampleCode_When_SetRecordingDiagnosticsWithCaptureResolution_Then_StoresProbeResolution()
        {
            Type controllerType = RequireType(HumanoidSampleCodeTypeName);
            var gameObject = new GameObject("Humanoid Sample Capture Resolution Test");

            try
            {
                var controller = gameObject.AddComponent(controllerType);

                InvokeInstance(
                    controller,
                    "SetRecordingDiagnostics",
                    true,
                    true,
                    false,
                    null,
                    3840,
                    2160);

                Assert.That(GetMemberValue<int>(controller, "ProbeScreenshotWidth"), Is.EqualTo(3840));
                Assert.That(GetMemberValue<int>(controller, "ProbeScreenshotHeight"), Is.EqualTo(2160));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName);
            Assert.That(type, Is.Not.Null, $"{typeName} must exist.");
            return type;
        }

        private static object InvokeStatic(Type type, string methodName, Type parameterType, object argument)
        {
            MethodInfo method = type.GetMethod(methodName, StaticMethods, null, new[] { parameterType }, null);
            Assert.That(method, Is.Not.Null, $"Expected static method '{methodName}'.");
            return method.Invoke(null, new[] { argument });
        }

        private static object InvokeInstance(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, InstanceMembers);
            Assert.That(method, Is.Not.Null, $"Expected instance method '{methodName}'.");
            return method.Invoke(target, arguments);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceMembers);
            Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceMembers);
            Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}'.");
            return (T)field.GetValue(target);
        }

        private static T GetMemberValue<T>(object target, string memberName)
        {
            PropertyInfo property = target.GetType().GetProperty(memberName, InstanceMembers);
            if (property != null)
            {
                return (T)property.GetValue(target);
            }

            FieldInfo field = target.GetType().GetField(memberName, InstanceMembers);
            Assert.That(field, Is.Not.Null, $"Expected field or property '{memberName}'.");
            return (T)field.GetValue(target);
        }
    }
}
