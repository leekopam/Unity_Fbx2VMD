using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.VMDRecorderSample
{
    public class HumanoidSampleCodeRecordingLifecycleTests
    {
        [Test]
        public void Given_AutoRecordingWasStartedBeforeStart_When_HumanoidSampleCodeStartRuns_Then_DoesNotClearRecordingSession()
        {
            GameObject target = null;
            try
            {
                target = new GameObject("recording-lifecycle-target");
                HumanoidSampleCode sampleCode = target.AddComponent<HumanoidSampleCode>();
                FieldInfo activeField = typeof(HumanoidSampleCode).GetField(
                    "_isRecordingSessionActive",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(activeField, Is.Not.Null);
                activeField.SetValue(sampleCode, true);

                MethodInfo startMethod = typeof(HumanoidSampleCode).GetMethod(
                    "Start",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(startMethod, Is.Not.Null);
                startMethod.Invoke(sampleCode, null);

                Assert.That(
                    activeField.GetValue(sampleCode),
                    Is.EqualTo(true),
                    "HumanoidSampleCode.Start must not call SetReady over an already-started recording session.");
            }
            finally
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }
        }
    }
}
