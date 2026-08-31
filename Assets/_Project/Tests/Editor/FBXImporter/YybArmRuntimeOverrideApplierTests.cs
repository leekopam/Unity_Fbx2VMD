using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybArmRuntimeOverrideApplierTests
    {
        [Test]
        public void Given_SwingLimitOverride_When_Toggled_Then_OnlyChangesSwingLimitSettings()
        {
            var pipelineObject = new GameObject("arm swing limit override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.ShouldUseManualAnimatorBodyRotationReference = false;
                pipeline.ShouldUseManualAnimatorFullBodyPoseReference = false;
                pipeline.ShouldUseManualAnimatorHipsLocalPositionReference = false;

                bool enabledApplied = ApplySwingLimit(pipeline, true);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.enableYybArmSwingLimitCorrection, Is.True);
                Assert.That(pipeline.YybArmSwingLimitWeight, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingMaxDownDot, Is.EqualTo(0.42f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingMinHandHorizontalRatio, Is.EqualTo(0.07f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingMaxHandBelowShoulderRatio, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseManualAnimatorBodyRotationReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorFullBodyPoseReference, Is.False);
                Assert.That(pipeline.ShouldUseManualAnimatorHipsLocalPositionReference, Is.False);

                bool disabledApplied = ApplySwingLimit(pipeline, false);

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.enableYybArmSwingLimitCorrection, Is.False);
                Assert.That(pipeline.YybArmSwingLimitWeight, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_SwingLimitOverride_When_HorizontalReachProvided_Then_ClampsReachSettings()
        {
            var pipelineObject = new GameObject("arm swing horizontal reach override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplySwingLimit(
                    pipeline,
                    true,
                    horizontalReachLimitWeight: 1.5f,
                    maxHandHorizontalReachRatio: -0.2f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingHorizontalReachLimitWeight, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingMaxHandHorizontalReachRatio, Is.EqualTo(0f).Within(0.0001f));

                bool disabledApplied = ApplySwingLimit(
                    pipeline,
                    false,
                    horizontalReachLimitWeight: 0.75f,
                    maxHandHorizontalReachRatio: 0.55f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingHorizontalReachLimitWeight, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingMaxHandHorizontalReachRatio, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_SwingLimitOverride_When_HorizontalReachGateProvided_Then_ClampsGateSeparately()
        {
            var pipelineObject = new GameObject("arm swing horizontal reach gate override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplySwingLimit(
                    pipeline,
                    true,
                    maxHandBelowShoulderRatio: 1.8f,
                    horizontalReachMaxHandBelowShoulderRatio: 0.95f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingMaxHandBelowShoulderRatio, Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(pipeline.YybArmSwingHorizontalReachMaxHandBelowShoulderRatio, Is.EqualTo(0.95f).Within(0.0001f));

                bool disabledApplied = ApplySwingLimit(
                    pipeline,
                    false,
                    maxHandBelowShoulderRatio: 1.8f,
                    horizontalReachMaxHandBelowShoulderRatio: 0.95f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingHorizontalReachMaxHandBelowShoulderRatio, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_SwingLimitOverride_When_ElbowGuardProvided_Then_ClampsAngle()
        {
            var pipelineObject = new GameObject("arm swing elbow guard override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplySwingLimit(
                    pipeline,
                    true,
                    horizontalReachMinElbowAngleAfterApply: 200f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(
                    pipeline.YybArmSwingHorizontalReachMinElbowAngleAfterApply,
                    Is.EqualTo(180f).Within(0.0001f));

                bool disabledApplied = ApplySwingLimit(
                    pipeline,
                    false,
                    horizontalReachMinElbowAngleAfterApply: 12f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(
                    pipeline.YybArmSwingHorizontalReachMinElbowAngleAfterApply,
                    Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_SwingLimitOverride_When_RaisedPoseSettingsProvided_Then_ClampsSettings()
        {
            var pipelineObject = new GameObject("arm swing raised pose override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();

                bool enabledApplied = ApplySwingLimit(
                    pipeline,
                    true,
                    raisedPoseHorizontalReachLimitWeight: 1.5f,
                    raisedPoseMinUpperArmDownDot: -0.25f,
                    raisedPoseMaxHandBelowShoulderRatio: 1.75f,
                    raisedPoseMaxHandHorizontalReachRatio: 1.8f);

                Assert.That(enabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingRaisedPoseHorizontalReachLimitWeight, Is.EqualTo(1f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMinUpperArmDownDot, Is.EqualTo(0f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio, Is.EqualTo(1.5f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio, Is.EqualTo(1.5f));

                bool disabledApplied = ApplySwingLimit(
                    pipeline,
                    false,
                    raisedPoseHorizontalReachLimitWeight: 0.75f,
                    raisedPoseMinUpperArmDownDot: 0.25f,
                    raisedPoseMaxHandBelowShoulderRatio: 0.8f,
                    raisedPoseMaxHandHorizontalReachRatio: 1f);

                Assert.That(disabledApplied, Is.True);
                Assert.That(pipeline.YybArmSwingRaisedPoseHorizontalReachLimitWeight, Is.EqualTo(0f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMinUpperArmDownDot, Is.EqualTo(0.25f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio, Is.EqualTo(0.8f));
                Assert.That(pipeline.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_YybArmProfile_When_ApplyingDirectionSettings_Then_ClampsSideWeights()
        {
            var pipelineObject = new GameObject("YYB arm override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.YybArmRuntimeOverrideApplier",
                    throwOnError: false);
                Assert.That(applierType, Is.Not.Null, "YYB 전용 팔 override 적용기가 필요합니다.");

                MethodInfo applyMethod = applierType.GetMethod(
                    "ApplyDirection",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(applyMethod, Is.Not.Null);

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 0.4f, 0.5f, 70f, 80f, -1f, 2f });

                Assert.That(applied, Is.True);
                Assert.That(pipeline.enableYybArmDirectionRetargetCorrection, Is.True);
                Assert.That(pipeline.YybArmDirectionUpperArmWeight, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionForearmWeight, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(pipeline.YybArmDirectionLeftSideWeightScale, Is.EqualTo(0f));
                Assert.That(pipeline.YybArmDirectionRightSideWeightScale, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        private static bool ApplySwingLimit(
            FBXVmdPipeline pipeline,
            bool enabled,
            float weight = 0.5f,
            float maxDownDot = 0.42f,
            float minHandHorizontalRatio = 0.07f,
            float maxHandBelowShoulderRatio = 0.6f,
            float horizontalReachLimitWeight = 0f,
            float maxHandHorizontalReachRatio = 0f,
            float horizontalReachMaxHandBelowShoulderRatio = 0f,
            float horizontalReachMinElbowAngleAfterApply = 0f,
            float raisedPoseHorizontalReachLimitWeight = 0f,
            float raisedPoseMinUpperArmDownDot = 0.55f,
            float raisedPoseMaxHandBelowShoulderRatio = 0.05f,
            float raisedPoseMaxHandHorizontalReachRatio = 0f)
        {
            Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybArmRuntimeOverrideApplier",
                throwOnError: false);
            Assert.That(applierType, Is.Not.Null, "팔 override 적용기가 필요합니다.");

            MethodInfo applyMethod = applierType.GetMethod(
                "ApplySwingLimit",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null, "팔 스윙 제한 적용 메서드가 필요합니다.");

            return (bool)applyMethod.Invoke(
                null,
                new object[]
                {
                    pipeline,
                    enabled,
                    weight,
                    maxDownDot,
                    minHandHorizontalRatio,
                    maxHandBelowShoulderRatio,
                    horizontalReachLimitWeight,
                    maxHandHorizontalReachRatio,
                    horizontalReachMaxHandBelowShoulderRatio,
                    horizontalReachMinElbowAngleAfterApply,
                    raisedPoseHorizontalReachLimitWeight,
                    raisedPoseMinUpperArmDownDot,
                    raisedPoseMaxHandBelowShoulderRatio,
                    raisedPoseMaxHandHorizontalReachRatio
                });
        }
    }
}
