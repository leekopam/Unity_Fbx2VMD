using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class ManualLowerBodySegmentDirectionRuntimeOverrideApplierTests
    {
        [Test]
        public void Given_LowerBodySegmentDirectionSettings_When_Applied_Then_ClampsValues()
        {
            var pipelineObject = new GameObject("lower body segment direction override pipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.ManualLowerBodySegmentDirectionRuntimeOverrideApplier",
                    throwOnError: false);
                Assert.That(applierType, Is.Not.Null, "모델 중립 하체 방향 override 적용기가 필요합니다.");

                MethodInfo applyMethod = applierType.GetMethod(
                    "Apply",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(applyMethod, Is.Not.Null);

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new object[]
                    {
                        pipeline,
                        true,
                        1.5f,
                        -1f,
                        true,
                        -2f,
                        true,
                        -3f,
                        -4f,
                        -5f,
                        2f,
                        -1f,
                        -6f,
                        -7f,
                        2f,
                        true,
                        -8f
                    });

                Assert.That(applied, Is.True);
                Assert.That(pipeline.ShouldUseManualAnimatorLowerBodySegmentDirectionReference, Is.True);
                Assert.That(pipeline.manualAnimatorLowerBodySegmentDirectionReferenceWeight, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, Is.EqualTo(0f));
                Assert.That(pipeline.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference, Is.True);
                Assert.That(pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale, Is.EqualTo(1f));
                Assert.That(pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight, Is.EqualTo(0f));
                Assert.That(pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight, Is.EqualTo(1f));
                Assert.That(pipeline.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }
    }
}
