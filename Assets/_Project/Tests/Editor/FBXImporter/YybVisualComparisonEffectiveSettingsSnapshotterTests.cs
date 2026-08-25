using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonEffectiveSettingsSnapshotterTests
    {
        [Test]
        public void Given_PipelineSettings_When_Capturing_Then_RecordsGenericAndYybValues()
        {
            GameObject pipelineObject = new GameObject("EffectiveSettingsSnapshotTest");
            try
            {
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.ShouldUseManualAnimatorFootLocalRotationReference = true;
                pipeline.manualAnimatorFootLocalRotationReferenceWeight = 0.37f;
                pipeline.enableYybArmSwingLimitCorrection = true;
                pipeline.YybArmSwingLimitWeight = 0.63f;

                Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
                Type resultType = runtimeAssembly.GetType(
                    "Fbx2Vmd.FBXImporter.YybVisualComparisonCaptureResultData",
                    throwOnError: true);
                object result = Activator.CreateInstance(resultType, nonPublic: true);
                Type snapshotterType = runtimeAssembly.GetType(
                    "Fbx2Vmd.FBXImporter.YybVisualComparisonEffectiveSettingsSnapshotter",
                    throwOnError: false);
                Assert.That(snapshotterType, Is.Not.Null, "pipeline 설정 스냅샷 책임을 분리해야 합니다.");

                MethodInfo captureMethod = snapshotterType.GetMethod(
                    "Capture",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(captureMethod, Is.Not.Null);
                captureMethod.Invoke(null, new[] { result, pipeline });

                Assert.That(resultType.GetField("hasFBXVmdPipelineEffectiveSettings").GetValue(result), Is.True);
                Assert.That(resultType.GetField("ShouldUseManualAnimatorFootLocalRotationReference").GetValue(result), Is.True);
                Assert.That(resultType.GetField("manualAnimatorFootLocalRotationReferenceWeight").GetValue(result), Is.EqualTo(0.37f));
                Assert.That(resultType.GetField("enableYybArmSwingLimitCorrection").GetValue(result), Is.True);
                Assert.That(resultType.GetField("yybArmSwingLimitWeight").GetValue(result), Is.EqualTo(0.63f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }
    }
}
