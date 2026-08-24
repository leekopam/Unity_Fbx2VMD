using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class VmdPlaybackProbeRuntimeOverrideApplierTests
    {
        [Test]
        public void Given_ExistingMotionPath_When_ApplyingProbe_Then_ConfiguresTarget()
        {
            var target = new GameObject("VMD playback probe target");
            string motionPath = Path.GetTempFileName();
            try
            {
                Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.FBXImporter.VmdPlaybackProbeRuntimeOverrideApplier",
                    throwOnError: false);
                Assert.That(applierType, Is.Not.Null, "진단용 VMD 재생 probe override 적용기가 필요합니다.");

                MethodInfo applyMethod = applierType.GetMethod(
                    "Apply",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(applyMethod, Is.Not.Null);

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new object[] { target, motionPath, null, true });

                Assert.That(applied, Is.True);
                VmdPlaybackProbe probe = target.GetComponent<VmdPlaybackProbe>();
                Assert.That(probe, Is.Not.Null);
                Assert.That(probe.PlaybackEnabled, Is.True);
                Assert.That(probe.ApplyIkTargets, Is.True);
                Assert.That(probe.MotionFilePath, Is.EqualTo(motionPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                File.Delete(motionPath);
            }
        }
    }
}
