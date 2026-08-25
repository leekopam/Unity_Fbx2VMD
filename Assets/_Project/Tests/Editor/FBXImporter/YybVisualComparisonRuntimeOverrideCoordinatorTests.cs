using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonRuntimeOverrideCoordinatorTests
    {
        [Test]
        public void Given_RunState_When_Applying_Then_DelegatesGenericAndYybOverrides()
        {
            var pipelineObject = new GameObject("visual comparison override pipeline");
            try
            {
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
                Type stateType = runtimeAssembly.GetType(
                    "Fbx2Vmd.FBXImporter.YybVisualComparisonRunStateData",
                    throwOnError: true);
                Type coordinatorType = runtimeAssembly.GetType(
                    "Fbx2Vmd.FBXImporter.YybVisualComparisonRuntimeOverrideCoordinator",
                    throwOnError: false);
                Assert.That(coordinatorType, Is.Not.Null, "runtime override 조립 경계가 필요합니다.");

                object state = Activator.CreateInstance(stateType, nonPublic: true);
                stateType.GetField("enableManualAnimatorFootLocalRotationRuntimeOverride").SetValue(state, true);
                stateType.GetField("enableYybArmSwingLimitRuntimeOverride").SetValue(state, true);
                stateType.GetField("yybArmSwingLimitWeight").SetValue(state, 0.75f);
                stateType.GetField("enableRetargetBodyPositionXzRootMotionRuntimeOverride").SetValue(state, true);
                stateType.GetField("disableTargetHumanoidBonePositionLockRuntimeOverride").SetValue(state, true);
                stateType.GetField("enableYybRightSleeveSilhouetteOffsetRuntimeOverride").SetValue(state, true);
                stateType.GetField("yybRightSleeveSilhouetteLocalOffsetX").SetValue(state, 0.1f);
                stateType.GetField("yybRightSleeveSilhouetteLocalOffsetFrameGateStart").SetValue(state, 120f);
                stateType.GetField("yybRightSleeveSilhouetteLocalOffsetFrameGateEnd").SetValue(state, 360f);

                MethodInfo applyMethod = coordinatorType.GetMethod(
                    "Apply",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(applyMethod, Is.Not.Null);

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new[] { pipeline, state, (object)0.5f, 1f, 0.125f, 1f });

                Assert.That(applied, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorFootLocalRotationReference, Is.True);
                Assert.That(pipeline.enableYybArmSwingLimitCorrection, Is.True);
                Assert.That(pipeline.YybArmSwingLimitWeight, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(pipeline.ShouldUseRetargetBodyPositionXZRootMotion, Is.True);
                Assert.That(pipeline.ShouldLockTargetHumanoidBonePositions, Is.False);
                Assert.That(pipeline.useYybRightSleeveSilhouetteLocalOffsetReference, Is.True);
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetX, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateStart, Is.EqualTo(120f));
                Assert.That(pipeline.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd, Is.EqualTo(360f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }
    }
}
