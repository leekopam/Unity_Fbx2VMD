using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class EditorAnimatorControllerAssetLoaderTests
    {
        private const string ControllerPath =
            "Assets/Plugins/VMDRecorderSample/SampleAnimation/TestAnimator1.controller";

        [Test]
        public void Given_MissingThenExistingPaths_When_LoadingFirst_Then_ReturnsFirstAvailableController()
        {
            RuntimeAnimatorController controller = LoadFirst(
                "Assets/_Project/Missing.controller",
                ControllerPath);

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.name, Is.EqualTo("TestAnimator1"));
        }

        [Test]
        public void Given_AllPathsMissing_When_LoadingFirst_Then_ReturnsNull()
        {
            RuntimeAnimatorController controller = LoadFirst(
                "Assets/_Project/MissingA.controller",
                "Assets/_Project/MissingB.controller");

            Assert.That(controller, Is.Null);
        }

        [Test]
        public void Given_ExtractedLoader_When_CheckingRunner_Then_DirectControllerAssetLookupIsRemoved()
        {
            string runnerPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "YybVisualComparisonBatchRunner.cs");
            string source = File.ReadAllText(runnerPath);

            Assert.That(source, Does.Contain("EditorAnimatorControllerAssetLoader.LoadFirst("));
            Assert.That(source, Does.Not.Contain("AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>"));
        }

        private static RuntimeAnimatorController LoadFirst(params string[] assetPaths)
        {
            Type loaderType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorAnimatorControllerAssetLoader",
                throwOnError: false);
            Assert.That(loaderType, Is.Not.Null, "모델 중립 Animator Controller asset loader 타입이 필요합니다.");

            MethodInfo method = loaderType.GetMethod(
                "LoadFirst",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (RuntimeAnimatorController)method.Invoke(null, new object[] { assetPaths });
        }
    }
}
