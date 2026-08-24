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
        public void Given_GenericRetargetingSettings_When_ApplyingSmoothing_Then_ClampsConfiguration()
        {
            var pipelineObject = new GameObject("generic retargeting pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.RetargetingRuntimeOverrideApplier",
                    throwOnError: false);
                Assert.That(applierType, Is.Not.Null, "모델 중립적인 리타게팅 override 적용기가 필요합니다.");

                MethodInfo applyMethod = applierType.GetMethod(
                    "ApplyPoseVisualSpikeSmoothing",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(applyMethod, Is.Not.Null);

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { pipeline, true, 2f, -1f });

                Assert.That(applied, Is.True);
                Assert.That(pipeline.smoothRetargetPoseOnVisualStepSpike, Is.True);
                Assert.That(pipeline.RetargetPoseVisualSpikeCurrentWeight, Is.EqualTo(1f));
                Assert.That(pipeline.RetargetPoseVisualSpikeForearmStretchClampMaxOffset, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }
    }
}
