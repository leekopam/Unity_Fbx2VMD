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
        public void Given_LimitWithoutRecoveryTrigger_When_Applying_Then_ChangesOnlyFootAndToeClamp()
        {
            var recorderObject = new GameObject("IK delta clamp override recorder");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.MaxMmdCenterExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdFootIkExportDeltaPerFrame = 0.10f;
                recorder.MaxMmdToeIkExportDeltaPerFrame = 0.10f;
                recorder.UseMmdIkExportDeltaRecoveryLimit = true;
                recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame = 0.08f;
                recorder.MmdIkExportDeltaRecoveryHoldFrames = 3;

                bool applied = InvokeApply(recorder, 0.12f, float.NaN, float.NaN, 0);

                Assert.That(applied, Is.True);
                Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.True);
                Assert.That(recorder.MaxMmdCenterExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.MaxMmdFootIkExportDeltaPerFrame, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(recorder.MaxMmdToeIkExportDeltaPerFrame, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(recorder.UseMmdIkExportDeltaRecoveryLimit, Is.False);
                Assert.That(recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame, Is.Zero);
                Assert.That(recorder.MmdIkExportDeltaRecoveryHoldFrames, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        [Test]
        public void Given_RecoveryTrigger_When_Applying_Then_PreservesBaseClampAndSetsRecoveryWindow()
        {
            var recorderObject = new GameObject("IK delta recovery override recorder");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.MaxMmdCenterExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdFootIkExportDeltaPerFrame = 0.10f;
                recorder.MaxMmdToeIkExportDeltaPerFrame = 0.10f;

                bool applied = InvokeApply(recorder, 0.12f, 0.30f, float.NaN, 0);

                Assert.That(applied, Is.True);
                Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.True);
                Assert.That(recorder.MaxMmdCenterExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.MaxMmdFootIkExportDeltaPerFrame, Is.EqualTo(0.10f).Within(0.0001f));
                Assert.That(recorder.MaxMmdToeIkExportDeltaPerFrame, Is.EqualTo(0.10f).Within(0.0001f));
                Assert.That(recorder.UseMmdIkExportDeltaRecoveryLimit, Is.True);
                Assert.That(recorder.MmdIkExportDeltaRecoveryLimitPerFrame, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryTriggerPerFrame, Is.EqualTo(0.30f).Within(0.0001f));
                Assert.That(float.IsNaN(recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame), Is.True);
                Assert.That(recorder.MmdIkExportDeltaRecoveryHoldFrames, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        [Test]
        public void Given_RecoveryDebtThreshold_When_Applying_Then_SetsDebtRecoveryWindow()
        {
            var recorderObject = new GameObject("IK delta recovery debt override recorder");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.MaxMmdCenterExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdFootIkExportDeltaPerFrame = 0.10f;
                recorder.MaxMmdToeIkExportDeltaPerFrame = 0.10f;

                bool applied = InvokeApply(recorder, 0.12099f, 0.26f, 0.08f, 0);

                Assert.That(applied, Is.True);
                Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.True);
                Assert.That(recorder.MaxMmdCenterExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.MaxMmdFootIkExportDeltaPerFrame, Is.EqualTo(0.10f).Within(0.0001f));
                Assert.That(recorder.MaxMmdToeIkExportDeltaPerFrame, Is.EqualTo(0.10f).Within(0.0001f));
                Assert.That(recorder.UseMmdIkExportDeltaRecoveryLimit, Is.True);
                Assert.That(recorder.MmdIkExportDeltaRecoveryLimitPerFrame, Is.EqualTo(0.12099f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryTriggerPerFrame, Is.EqualTo(0.26f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryHoldFrames, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        [Test]
        public void Given_RecoveryHoldFrames_When_Applying_Then_SetsHoldWindow()
        {
            var recorderObject = new GameObject("IK delta recovery hold override recorder");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.MaxMmdCenterExportDeltaPerFrame = 0.11f;
                recorder.MaxMmdFootIkExportDeltaPerFrame = 0.10f;
                recorder.MaxMmdToeIkExportDeltaPerFrame = 0.10f;

                bool applied = InvokeApply(recorder, 0.1209f, 0.26f, 0.08f, 3);

                Assert.That(applied, Is.True);
                Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.True);
                Assert.That(recorder.MaxMmdCenterExportDeltaPerFrame, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(recorder.MaxMmdFootIkExportDeltaPerFrame, Is.EqualTo(0.10f).Within(0.0001f));
                Assert.That(recorder.MaxMmdToeIkExportDeltaPerFrame, Is.EqualTo(0.10f).Within(0.0001f));
                Assert.That(recorder.UseMmdIkExportDeltaRecoveryLimit, Is.True);
                Assert.That(recorder.MmdIkExportDeltaRecoveryLimitPerFrame, Is.EqualTo(0.1209f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryTriggerPerFrame, Is.EqualTo(0.26f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(recorder.MmdIkExportDeltaRecoveryHoldFrames, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        private static bool InvokeApply(
            UnityHumanoidVMDRecorder recorder,
            float limitVmd,
            float recoveryTriggerVmd,
            float recoveryDebtThresholdVmd,
            int recoveryHoldFrames)
        {
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

            return (bool)applyMethod.Invoke(
                null,
                new object[]
                {
                    recorder,
                    limitVmd,
                    recoveryTriggerVmd,
                    recoveryDebtThresholdVmd,
                    recoveryHoldFrames
                });
        }
    }
}
