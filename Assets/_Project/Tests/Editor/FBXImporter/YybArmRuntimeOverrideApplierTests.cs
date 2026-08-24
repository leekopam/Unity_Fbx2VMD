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
    }
}
