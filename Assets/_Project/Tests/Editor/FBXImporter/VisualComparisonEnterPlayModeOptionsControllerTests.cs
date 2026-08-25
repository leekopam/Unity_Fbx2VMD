using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonEnterPlayModeOptionsControllerTests
    {
        [Test]
        public void Given_EditorSettings_When_ApplyingAndRestoring_Then_PreservesOriginalOptions()
        {
            bool originalEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            EnterPlayModeOptions originalOptions = EditorSettings.enterPlayModeOptions;

            try
            {
                Type controllerType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.VisualComparisonEnterPlayModeOptionsController",
                    throwOnError: false);
                Assert.That(controllerType, Is.Not.Null, "모델 중립 PlayMode 설정 수명주기 경계가 필요합니다.");

                object controller = Activator.CreateInstance(controllerType, nonPublic: true);
                MethodInfo applyMethod = controllerType.GetMethod(
                    "Apply",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo restoreMethod = controllerType.GetMethod(
                    "Restore",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(applyMethod, Is.Not.Null);
                Assert.That(restoreMethod, Is.Not.Null);

                Assert.That((bool)applyMethod.Invoke(controller, new object[] { false }), Is.True);
                Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.True);
                Assert.That(
                    EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableDomainReload),
                    Is.True);
                Assert.That((bool)applyMethod.Invoke(controller, new object[] { false }), Is.False);

                Assert.That((bool)restoreMethod.Invoke(controller, null), Is.True);
                Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.EqualTo(originalEnabled));
                Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(originalOptions));
                Assert.That((bool)restoreMethod.Invoke(controller, null), Is.False);
            }
            finally
            {
                EditorSettings.enterPlayModeOptions = originalOptions;
                EditorSettings.enterPlayModeOptionsEnabled = originalEnabled;
            }
        }
    }
}
