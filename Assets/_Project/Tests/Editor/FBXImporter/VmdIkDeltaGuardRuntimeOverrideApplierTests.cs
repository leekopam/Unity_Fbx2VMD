using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class VmdIkDeltaGuardRuntimeOverrideApplierTests
    {
        [Test]
        public void Given_GenericRecorderOverride_When_ApplyingRecoverySettings_Then_PreservesUnrelatedClamp()
        {
            var recorderObject = new GameObject("generic recorder override");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.MaxMmdCenterExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdFootIkExportDeltaPerFrame = 0.10f;
                recorder.MaxMmdToeIkExportDeltaPerFrame = 0.10f;

                Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.VmdIkDeltaGuardRuntimeOverrideApplier",
                    throwOnError: false);
                Assert.That(applierType, Is.Not.Null, "모델 중립적인 VMD IK 델타 override 적용기가 필요합니다.");

                MethodInfo applyMethod = applierType.GetMethod(
                    "Apply",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: new[]
                    {
                        typeof(UnityHumanoidVMDRecorder),
                        typeof(float),
                        typeof(float),
                        typeof(float),
                        typeof(int)
                    },
                    modifiers: null);
                Assert.That(applyMethod, Is.Not.Null);

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { recorder, 0.12f, 0.30f, 0.08f, 3 });

                Assert.That(applied, Is.True);
                Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.True);
                Assert.That(recorder.MaxMmdCenterExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.UseMmdIkExportDeltaRecoveryLimit, Is.True);
                Assert.That(recorder.MmdIkExportDeltaRecoveryLimitPerFrame, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryTriggerPerFrame, Is.EqualTo(0.30f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryHoldFrames, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }
    }
}
