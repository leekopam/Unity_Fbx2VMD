using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class RetargetingRuntimeOverrideApplierTests
    {
        [Test]
        public void Given_PoseVisualSpikeSmoothing_When_Toggled_Then_ClampsOnlySmoothingSettings()
        {
            var pipelineObject = new GameObject("pose visual spike smoothing override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.enableYybArmSwingLimitCorrection = false;
                pipeline.usePostSetHumanPoseRightEndpointPositionReference = false;
                MethodInfo applyMethod = FindApplyMethod("ApplyPoseVisualSpikeSmoothing");

                bool disabledApplied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, false, 1.5f, 2f });

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.smoothRetargetPoseOnVisualStepSpike, Is.False);
                Assert.That(pipeline.RetargetPoseVisualSpikeCurrentWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    pipeline.RetargetPoseVisualSpikeForearmStretchClampMaxOffset,
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.enableYybArmSwingLimitCorrection, Is.False);
                Assert.That(pipeline.usePostSetHumanPoseRightEndpointPositionReference, Is.False);

                bool enabledApplied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.05f, 0.15f });

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.smoothRetargetPoseOnVisualStepSpike, Is.True);
                Assert.That(pipeline.RetargetPoseVisualSpikeCurrentWeight, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(
                    pipeline.RetargetPoseVisualSpikeForearmStretchClampMaxOffset,
                    Is.EqualTo(0.15f).Within(0.0001f));

                bool lowerBoundApplied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.5f, -1f });

                Assert.That(lowerBoundApplied, Is.True);
                Assert.That(pipeline.RetargetPoseVisualSpikeCurrentWeight, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(
                    pipeline.RetargetPoseVisualSpikeForearmStretchClampMaxOffset,
                    Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_ArmStretchClamp_When_Toggled_Then_ClampsAllGuardSettings()
        {
            var pipelineObject = new GameObject("arm stretch clamp override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                MethodInfo applyMethod = FindApplyMethod("ApplyArmStretchClamp");

                bool enabledApplied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.75f, 0.5f });

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.enableAnatomicalArmGuard, Is.True);
                Assert.That(pipeline.clampRetargetArmStretchMuscles, Is.True);
                Assert.That(pipeline.targetGuardClampAnatomicalArmMuscles, Is.True);
                Assert.That(pipeline.targetGuardClampArmStretchMuscles, Is.True);
                Assert.That(pipeline.ArmStretchMuscleLimit, Is.EqualTo(0.5f).Within(0.0001f));

                bool disabledApplied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, false, 0.5f, 0.5f });

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.enableAnatomicalArmGuard, Is.True);
                Assert.That(pipeline.clampRetargetArmStretchMuscles, Is.False);
                Assert.That(pipeline.targetGuardClampAnatomicalArmMuscles, Is.False);
                Assert.That(pipeline.targetGuardClampArmStretchMuscles, Is.False);
                Assert.That(pipeline.ArmStretchMuscleLimit, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        private static MethodInfo FindApplyMethod(string methodName)
        {
            Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.RetargetingRuntimeOverrideApplier",
                throwOnError: false);
            Assert.That(applierType, Is.Not.Null, "모델 중립적인 리타게팅 override 적용기가 필요합니다.");

            MethodInfo applyMethod = applierType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null, $"{methodName} 적용 메서드가 필요합니다.");
            return applyMethod;
        }
    }
}
