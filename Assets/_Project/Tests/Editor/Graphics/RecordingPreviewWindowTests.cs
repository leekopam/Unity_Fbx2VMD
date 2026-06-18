using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Tests.Editor.Graphics
{
    public class RecordingPreviewWindowTests
    {
        private const string PreviewWindowTypeName =
            "Member_Han.Modules.Graphics.EditorTools.RecordingPreviewWindow, Assembly-CSharp-Editor";
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void Given_RecordingPreviewWindow_When_InspectingDefaults_Then_UsesThrottledPreview()
        {
            Type windowType = RequireType(PreviewWindowTypeName);

            Assert.That(GetStaticMemberValue<int>(windowType, "DefaultPreviewWidth"), Is.EqualTo(1920));
            Assert.That(GetStaticMemberValue<int>(windowType, "DefaultPreviewHeight"), Is.EqualTo(1080));
            Assert.That(GetStaticMemberValue<float>(windowType, "DefaultMaxFps"), Is.LessThanOrEqualTo(10f));

            object plan = InvokeStatic<object>(windowType, "CreateDefaultPreviewPlan");
            Assert.That(GetMemberValue<int>(plan, "Width"), Is.EqualTo(1920));
            Assert.That(GetMemberValue<int>(plan, "Height"), Is.EqualTo(1080));

            Assert.That(InvokeStatic<float>(windowType, "GetMinimumRenderIntervalSeconds", 10f),
                Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(InvokeStatic<bool>(windowType, "ShouldRender", 1.05d, 1.0d, 10f), Is.False);
            Assert.That(InvokeStatic<bool>(windowType, "ShouldRender", 1.11d, 1.0d, 10f), Is.True);
        }

        [Test]
        public void Given_RecordingPreviewWindow_When_InspectingManualUhdPlan_Then_UsesUhd4KWithoutRaisingDefaultFps()
        {
            Type windowType = RequireType(PreviewWindowTypeName);

            object plan = InvokeStatic<object>(windowType, "CreateManualUhdPreviewPlan");

            Assert.That(GetMemberValue<int>(plan, "Width"), Is.EqualTo(3840));
            Assert.That(GetMemberValue<int>(plan, "Height"), Is.EqualTo(2160));
            Assert.That(GetStaticMemberValue<float>(windowType, "DefaultMaxFps"), Is.LessThanOrEqualTo(10f));
            Assert.That(GetStaticMemberValue<string>(windowType, "MenuPath"),
                Is.EqualTo("Tools/Graphics/Open Recording Preview"));
        }

        [Test]
        public void Given_RecordingPreviewWindow_When_InspectingMenuContract_Then_IsIndependentFromSettingsSurface()
        {
            Type previewWindowType = RequireType(PreviewWindowTypeName);

            Assert.That(InvokeStatic<string>(previewWindowType, "GetWindowTitle"), Is.EqualTo("Recording Preview"));
            Assert.That(
                GetStaticMemberValue<string>(previewWindowType, "MenuPath"),
                Is.EqualTo("Tools/Graphics/Open Recording Preview"));
        }

        [Test]
        public void Given_MainRecordingScene_When_OpeningRecordingPreview_Then_RendersNonblankTexture()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("Unity batchmode null graphics device cannot render the EditorWindow preview texture.");
            }

            Type windowType = RequireType(PreviewWindowTypeName);
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            EditorWindow window = null;
            try
            {
                ExpectHeadlessWindowLogsIfNeeded();
                window = InvokeStatic<EditorWindow>(windowType, "OpenForMainCamera");

                Assert.That(InvokeInstance<int>(window, "GetPreviewTextureWidthForTests"), Is.EqualTo(1920));
                Assert.That(InvokeInstance<int>(window, "GetPreviewTextureHeightForTests"), Is.EqualTo(1080));
                int differentSamples = InvokeInstance<int>(
                    window,
                    "CountPreviewSamplesDifferentFromCornerForTests",
                    32,
                    18);
                Debug.Log(
                    "[RecordingPreviewWindowTests] " +
                    $"preview_width=1920 preview_height=1080 sample_count=576 nonbackground_samples={differentSamples}");
                Assert.That(differentSamples, Is.GreaterThan(0));
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                }
            }
        }

        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName);
            Assert.That(type, Is.Not.Null, $"{typeName} must exist.");
            return type;
        }

        private static T InvokeStatic<T>(Type type, string methodName, params object[] args)
        {
            MethodInfo method = type.GetMethod(methodName, StaticMembers);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} must exist.");
            return (T)method.Invoke(null, args);
        }

        private static T InvokeInstance<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, InstanceMembers);
            Assert.That(method, Is.Not.Null, $"{target.GetType().FullName}.{methodName} must exist.");
            return (T)method.Invoke(target, args);
        }

        private static T GetStaticMemberValue<T>(Type type, string memberName)
        {
            PropertyInfo property = type.GetProperty(memberName, StaticMembers);
            if (property != null)
            {
                return (T)property.GetValue(null);
            }

            FieldInfo field = type.GetField(memberName, StaticMembers);
            Assert.That(field, Is.Not.Null, $"Expected static field or property '{memberName}'.");
            return (T)field.GetValue(null);
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

        private static void ExpectHeadlessWindowLogsIfNeeded()
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                return;
            }

            LogAssert.Expect(LogType.Error, "No graphic device is available to initialize the view.");
            LogAssert.Expect(LogType.Error, "No graphic device is available to show the window.");
        }
    }
}
