using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonRunOptionsNormalizerTests
    {
        [Test]
        public void Given_StartOptions_When_Normalizing_Then_DerivesSafeGenericAndYybValues()
        {
            Assembly assembly = typeof(FBXVmdPipeline).Assembly;
            Type optionsType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptions",
                throwOnError: true);
            Type normalizerType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptionsNormalizer",
                throwOnError: false);
            Assert.That(normalizerType, Is.Not.Null, "YYB 시작 옵션 정규화 경계가 필요합니다.");

            object options = Activator.CreateInstance(optionsType, nonPublic: true);
            object defaults = Activator.CreateInstance(optionsType, nonPublic: true);
            Set(optionsType, options, "fbxFileName", " ");
            Set(optionsType, options, "durationSeconds", 0f);
            Set(optionsType, options, "yybArmSwingLimitWeight", 2f);
            Set(optionsType, options, "enableVmdPlaybackProbeRuntimeOverride", false);
            Set(optionsType, options, "applyVmdPlaybackProbeIkTargetsRuntimeOverride", true);
            Set(optionsType, options, "vmdPlaybackProbeSourceVmdPath", "old.vmd");
            Set(optionsType, defaults, "fbxFileName", "fallback.fbx");

            MethodInfo normalizeMethod = normalizerType.GetMethod(
                "Normalize",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(normalizeMethod, Is.Not.Null);
            object normalized = normalizeMethod.Invoke(null, new[] { options, defaults, (object)60f });

            Assert.That(normalized, Is.SameAs(options));
            Assert.That(Get(optionsType, options, "fbxFileName"), Is.EqualTo("fallback.fbx"));
            Assert.That(Get(optionsType, options, "durationSeconds"), Is.EqualTo(0.1f));
            Assert.That(Get(optionsType, options, "targetFrameCount"), Is.EqualTo(6));
            Assert.That(Get(optionsType, options, "yybArmSwingLimitWeight"), Is.EqualTo(1f));
            Assert.That(
                Get(optionsType, options, "applyVmdPlaybackProbeIkTargetsRuntimeOverride"),
                Is.False);
            Assert.That(Get(optionsType, options, "vmdPlaybackProbeSourceVmdPath"), Is.EqualTo(string.Empty));
        }

        private static object Get(Type type, object target, string fieldName)
        {
            return type.GetField(fieldName).GetValue(target);
        }

        private static void Set(Type type, object target, string fieldName, object value)
        {
            type.GetField(fieldName).SetValue(target, value);
        }
    }
}
