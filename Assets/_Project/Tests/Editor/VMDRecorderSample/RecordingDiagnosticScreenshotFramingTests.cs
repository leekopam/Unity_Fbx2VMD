using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.VMDRecorderSample
{
    public sealed class RecordingDiagnosticScreenshotFramingTests
    {
        [Test]
        public void Given_ProbeScreenshotFramingOverride_When_Applied_Then_ClampsPaddingAndViewportCenter()
        {
            GameObject target = null;
            try
            {
                target = new GameObject("diagnostic-probe");
                MotionComparisonProbe probe = target.AddComponent<MotionComparisonProbe>();

                MethodInfo method = typeof(MotionComparisonProbe).GetMethod(
                    "SetScreenshotFraming",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: new[] { typeof(float), typeof(float) },
                    modifiers: null);

                Assert.That(method, Is.Not.Null, "Diagnostic screenshot framing must be overrideable without changing production defaults.");

                method.Invoke(probe, new object[] { 0.1f, 1.5f });

                Assert.That(GetProperty<float>(probe, "ScreenshotPadding"), Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(GetProperty<float>(probe, "ScreenshotVerticalViewportCenter"), Is.EqualTo(1f).Within(0.0001f));

                method.Invoke(probe, new object[] { 0.75f, 0.4f });

                Assert.That(GetProperty<float>(probe, "ScreenshotPadding"), Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(GetProperty<float>(probe, "ScreenshotVerticalViewportCenter"), Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        [Test]
        public void Given_RecordingDiagnosticsFramingOverride_When_StartingProbe_Then_AppliesToComparisonProbe()
        {
            GameObject target = null;
            try
            {
                target = new GameObject("diagnostic-recorder");
                HumanoidSampleCode sampleCode = target.AddComponent<HumanoidSampleCode>();

                MethodInfo setDiagnosticsMethod = typeof(HumanoidSampleCode).GetMethod(
                    "SetRecordingDiagnostics",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: new[]
                    {
                        typeof(bool),
                        typeof(bool),
                        typeof(bool),
                        typeof(float[]),
                        typeof(int),
                        typeof(int),
                        typeof(float),
                        typeof(float)
                    },
                    modifiers: null);
                Assert.That(setDiagnosticsMethod, Is.Not.Null, "Recorder diagnostics must pass screenshot framing overrides to MotionComparisonProbe.");

                setDiagnosticsMethod.Invoke(
                    sampleCode,
                    new object[] { true, false, false, null, 1920, 1080, 0.75f, 0.4f });

                MethodInfo startComparisonProbeMethod = typeof(HumanoidSampleCode).GetMethod(
                    "StartComparisonProbe",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(startComparisonProbeMethod, Is.Not.Null);
                startComparisonProbeMethod.Invoke(sampleCode, new object[] { "diagnostic" });

                MotionComparisonProbe probe = target.GetComponent<MotionComparisonProbe>();
                Assert.That(probe, Is.Not.Null);
                Assert.That(probe.ScreenshotWidth, Is.EqualTo(1920));
                Assert.That(probe.ScreenshotHeight, Is.EqualTo(1080));
                Assert.That(GetProperty<float>(probe, "ScreenshotPadding"), Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(GetProperty<float>(probe, "ScreenshotVerticalViewportCenter"), Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            Assert.That(instance, Is.Not.Null);
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Expected property '{propertyName}' to exist.");

            return (T)property.GetValue(instance);
        }
    }
}
