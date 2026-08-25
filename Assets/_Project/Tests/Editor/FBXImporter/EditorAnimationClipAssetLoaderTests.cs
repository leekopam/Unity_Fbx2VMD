using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class EditorAnimationClipAssetLoaderTests
    {
        private const string StandaloneClipPath = "Assets/Resources/EmptyClip.anim";

        [Test]
        public void Given_StandaloneAnimationAsset_When_LoadingFirstClip_Then_ReturnsClip()
        {
            AnimationClip clip = LoadFirst(StandaloneClipPath);

            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.name, Is.EqualTo("EmptyClip"));
        }

        [Test]
        public void Given_MissingAsset_When_LoadingFirstClip_Then_ReturnsNull()
        {
            AnimationClip clip = LoadFirst("Assets/_Project/FBX/missing-animation.fbx");

            Assert.That(clip, Is.Null);
        }

        [Test]
        public void Given_ExtractedLoader_When_CheckingRunner_Then_ModelSpecificRunnerHasNoAssetLoadingMethod()
        {
            MethodInfo method = typeof(YybVisualComparisonBatchRunner).GetMethod(
                "LoadFirstAnimationClip",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Null);
        }

        private static AnimationClip LoadFirst(string assetPath)
        {
            Type loaderType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorAnimationClipAssetLoader",
                throwOnError: false);
            Assert.That(loaderType, Is.Not.Null, "모델 중립 AnimationClip asset loader 타입이 필요합니다.");

            MethodInfo method = loaderType.GetMethod(
                "LoadFirst",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (AnimationClip)method.Invoke(null, new object[] { assetPath });
        }
    }
}
