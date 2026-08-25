using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonPersistedRunStateNormalizerTests
    {
        [Test]
        public void Given_InvalidPersistedValues_When_Normalizing_Then_UsesSafeGenericAndYybValues()
        {
            Assembly assembly = typeof(FBXVmdPipeline).Assembly;
            Type stateType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunStateData",
                throwOnError: true);
            Type optionsType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptions",
                throwOnError: true);
            Type normalizerType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonPersistedRunStateNormalizer",
                throwOnError: false);
            Assert.That(normalizerType, Is.Not.Null, "YYB 복구 상태 정규화 경계가 필요합니다.");

            object state = Activator.CreateInstance(stateType, nonPublic: true);
            object defaults = Activator.CreateInstance(optionsType, nonPublic: true);
            Set(stateType, state, "fbxFileName", " ");
            Set(stateType, state, "durationSeconds", 0f);
            Set(stateType, state, "targetFrameCount", 0);
            Set(stateType, state, "retargetArmStretchMuscleLimit", float.NaN);
            Set(stateType, state, "yybArmSwingLimitWeight", 2f);
            Set(stateType, state, "enableVmdPlaybackProbeRuntimeOverride", false);
            Set(stateType, state, "applyVmdPlaybackProbeIkTargetsRuntimeOverride", true);
            Set(optionsType, defaults, "fbxFileName", "fallback.fbx");
            Set(optionsType, defaults, "retargetArmStretchMuscleLimit", 0.5f);

            MethodInfo normalizeMethod = normalizerType.GetMethod(
                "Normalize",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(normalizeMethod, Is.Not.Null);
            object normalized = normalizeMethod.Invoke(null, new[] { state, defaults });

            Assert.That(normalized, Is.SameAs(state));
            Assert.That(Get(stateType, state, "fbxFileName"), Is.EqualTo("fallback.fbx"));
            Assert.That(Get(stateType, state, "durationSeconds"), Is.EqualTo(0.1f));
            Assert.That(Get(stateType, state, "targetFrameCount"), Is.EqualTo(1));
            Assert.That(Get(stateType, state, "retargetArmStretchMuscleLimit"), Is.EqualTo(0.5f));
            Assert.That(Get(stateType, state, "yybArmSwingLimitWeight"), Is.EqualTo(1f));
            Assert.That(
                Get(stateType, state, "applyVmdPlaybackProbeIkTargetsRuntimeOverride"),
                Is.False);
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
