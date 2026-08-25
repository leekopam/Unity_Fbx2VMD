using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public sealed class VisualComparisonRunOptionsCopierTests
    {
        [Test]
        public void Given_GenericOptions_When_Copying_Then_PreservesModelNeutralValues()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type optionsType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonRunOptions",
                throwOnError: true);
            Type copierType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonRunOptionsCopier",
                throwOnError: false);
            Assert.That(copierType, Is.Not.Null, "모델 중립 실행 옵션 복사 경계가 필요합니다.");

            object source = Activator.CreateInstance(optionsType, nonPublic: true);
            object destination = Activator.CreateInstance(optionsType, nonPublic: true);
            optionsType.GetField("fbxFileName").SetValue(source, "future-model-motion.fbx");
            optionsType.GetField("durationSeconds").SetValue(source, 12.5f);
            optionsType.GetField("enableFinalIkFootGroundingRuntimeOverride").SetValue(source, true);

            copierType.GetMethod("Copy", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { source, destination });

            Assert.That(optionsType.GetField("fbxFileName").GetValue(destination), Is.EqualTo("future-model-motion.fbx"));
            Assert.That(optionsType.GetField("durationSeconds").GetValue(destination), Is.EqualTo(12.5f));
            Assert.That(
                optionsType.GetField("enableFinalIkFootGroundingRuntimeOverride").GetValue(destination),
                Is.True);
        }

        [Test]
        public void Given_YybOptions_When_Copying_Then_PreservesGenericAndProfileValues()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type optionsType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptions",
                throwOnError: true);
            Type copierType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptionsCopier",
                throwOnError: false);
            Assert.That(copierType, Is.Not.Null, "YYB 전용 실행 옵션 복사 경계가 필요합니다.");

            object source = Activator.CreateInstance(optionsType, nonPublic: true);
            object destination = Activator.CreateInstance(optionsType, nonPublic: true);
            optionsType.GetField("fbxFileName").SetValue(source, "profile-motion.fbx");
            optionsType.GetField("enableYybArmSwingLimitRuntimeOverride").SetValue(source, true);
            optionsType.GetField("yybArmSwingLimitWeight").SetValue(source, 0.75f);

            copierType.GetMethod("Copy", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { source, destination });

            Assert.That(optionsType.GetField("fbxFileName").GetValue(destination), Is.EqualTo("profile-motion.fbx"));
            Assert.That(
                optionsType.GetField("enableYybArmSwingLimitRuntimeOverride").GetValue(destination),
                Is.True);
            Assert.That(optionsType.GetField("yybArmSwingLimitWeight").GetValue(destination), Is.EqualTo(0.75f));
        }
    }
}
