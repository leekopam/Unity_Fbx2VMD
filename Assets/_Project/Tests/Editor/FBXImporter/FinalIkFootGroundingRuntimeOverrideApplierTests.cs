using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using RootMotion.FinalIK;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class FinalIkFootGroundingRuntimeOverrideApplierTests
    {
        [Test]
        public void Given_MissingPipeline_When_DisablingGrounding_Then_ReturnsFalse()
        {
            MethodInfo applyMethod = FindApplyMethod();

            bool applied = (bool)applyMethod.Invoke(null, new object[] { null, false });

            Assert.That(applied, Is.False);
        }

        [Test]
        public void Given_FinalIkFootGroundingRuntimeOverride_When_Disabled_Then_CleansExistingFootSolversForBaseline()
        {
            var pipelineObject = new GameObject("final IK grounding override pipeline");
            var targetObject = new GameObject("final IK grounding override target");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.targetCharacter = targetObject;
                var bipedIk = targetObject.AddComponent<BipedIK>();
                var grounder = targetObject.AddComponent<GrounderBipedIK>();
                grounder.ik = bipedIk;
                grounder.weight = 0.15f;
                bipedIk.enabled = true;
                bipedIk.fixTransforms = true;
                grounder.enabled = true;
                MethodInfo applyMethod = FindApplyMethod();

                bool enabledApplied = (bool)applyMethod.Invoke(null, new object[] { pipeline, true });
                bool disabledApplied = (bool)applyMethod.Invoke(null, new object[] { pipeline, false });

                Assert.That(enabledApplied, Is.True);
                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.enableFinalIkFootGroundingExperiment, Is.False);
                Assert.That(grounder.enabled, Is.False, "명시적인 비활성화는 기존 GrounderBipedIK 상태를 정리해야 합니다.");
                Assert.That(grounder.weight, Is.EqualTo(0f).Within(0.0001f), "명시적인 비활성화는 GrounderBipedIK 영향도를 제거해야 합니다.");
                Assert.That(bipedIk.enabled, Is.False, "명시적인 비활성화는 기존 BipedIK 상태를 정리해야 합니다.");
                Assert.That(bipedIk.fixTransforms, Is.False, "명시적인 비활성화는 BipedIK의 transform 고정을 해제해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        private static MethodInfo FindApplyMethod()
        {
            Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.FinalIkFootGroundingRuntimeOverrideApplier",
                throwOnError: false);
            Assert.That(applierType, Is.Not.Null, "모델 중립 FinalIK 접지 override 적용기가 필요합니다.");

            MethodInfo applyMethod = applierType.GetMethod(
                "Apply",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null);
            return applyMethod;
        }
    }
}
