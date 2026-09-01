using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public sealed class YybVisualComparisonSampleOrderingDiagnosticsTests
    {
        [Test]
        public void Given_MetricsCsv_When_BuildingSampleOrderingDiagnostics_Then_ReportsFrameZeroPrewarmAndGroundingOrdering()
        {
            string tempCsv = Path.Combine(
                Path.GetTempPath(),
                "yyb-sample-ordering-diagnostics-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                File.WriteAllText(
                    tempCsv,
                    string.Join(
                        Environment.NewLine,
                        "reason,timeSinceLevelLoad,frameCount,recorderFrame,animationClipTime,retargetGroundingVerticalStepLast,retargetGroundingInitialVerticalStep,retargetGroundingStepClampCount,retargetGroundingSmoothedCount",
                        "start,1.5,120,0,0,0.1,0.45,12,60",
                        "finish,201.1,7208,6001,200,0.01,0.45,2196,5620"));

                object diagnostics = BuildSampleOrderingDiagnostic(
                    "MainAuto",
                    "Main_Auto",
                    tempCsv);

                Assert.That(GetField<string>(diagnostics, "job_mode"), Is.EqualTo("MainAuto"));
                Assert.That(GetField<string>(diagnostics, "scene_name"), Is.EqualTo("Main_Auto"));
                Assert.That(GetField<int>(diagnostics, "metric_row_count"), Is.EqualTo(2));
                Assert.That(GetField<string>(diagnostics, "first_metric_reason"), Is.EqualTo("start"));
                Assert.That(GetField<int>(diagnostics, "first_metric_recorder_frame"), Is.EqualTo(0));
                Assert.That(GetField<int>(diagnostics, "first_metric_engine_frame_count"), Is.EqualTo(120));
                Assert.That(GetField<float>(diagnostics, "first_metric_time_since_level_load"), Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(GetField<float>(diagnostics, "first_metric_animation_clip_time"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetField<float>(diagnostics, "first_metric_grounding_vertical_step_last"), Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(GetField<float>(diagnostics, "first_metric_grounding_initial_vertical_step"), Is.EqualTo(0.45f).Within(0.0001f));
                Assert.That(GetField<int>(diagnostics, "first_metric_grounding_step_clamp_count"), Is.EqualTo(12));
                Assert.That(GetField<int>(diagnostics, "first_metric_grounding_smoothed_count"), Is.EqualTo(60));
                Assert.That(GetField<string>(diagnostics, "finish_metric_reason"), Is.EqualTo("finish"));
                Assert.That(GetField<int>(diagnostics, "finish_metric_recorder_frame"), Is.EqualTo(6001));
                Assert.That(GetField<int>(diagnostics, "recording_metric_recorder_frame_span"), Is.EqualTo(6001));
                Assert.That(GetField<int>(diagnostics, "recording_metric_engine_frame_span"), Is.EqualTo(7088));
                Assert.That(GetField<float>(diagnostics, "recording_metric_time_since_level_load_span"), Is.EqualTo(199.6f).Within(0.0001f));
                Assert.That(GetField<int>(diagnostics, "recording_grounding_step_clamp_delta"), Is.EqualTo(2184));
                Assert.That(GetField<int>(diagnostics, "recording_grounding_smoothed_delta"), Is.EqualTo(5560));
                Assert.That(GetField<string>(diagnostics, "recording_phase_span_role"), Does.Contain("finish-first"));
            }
            finally
            {
                if (File.Exists(tempCsv))
                {
                    File.Delete(tempCsv);
                }
            }
        }

        [Test]
        public void Given_MetricsCsvWithGroundingStepLimit_When_BuildingSampleOrderingDiagnostics_Then_SeparatesPrewarmResidualFromRecordingCounters()
        {
            string tempCsv = Path.Combine(
                Path.GetTempPath(),
                "yyb-grounding-step-limit-diagnostics-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                File.WriteAllText(
                    tempCsv,
                    string.Join(
                        Environment.NewLine,
                        "reason,timeSinceLevelLoad,frameCount,recorderFrame,animationClipTime,retargetGroundingVerticalStepLast,retargetGroundingInitialVerticalStep,retargetGroundingStepClampCount,retargetGroundingSmoothedCount,retargetGroundingMaxStepPerFrame",
                        "start,1.5,120,0,0,-0.01,0.45,0,0,0.01",
                        "finish,201.1,6121,6001,200,-0.0005,0.45,2167,5563,0.01"));

                object diagnostics = BuildSampleOrderingDiagnostic(
                    "MainAuto",
                    "Main_Auto",
                    tempCsv);

                Assert.That(GetField<float>(diagnostics, "first_metric_grounding_max_step_per_frame"), Is.EqualTo(0.01f).Within(0.0001f));
                Assert.That(GetField<float>(diagnostics, "first_metric_grounding_vertical_step_to_max_ratio"), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetField<bool>(diagnostics, "first_metric_grounding_vertical_step_at_max_step"), Is.True);
                Assert.That(GetField<float>(diagnostics, "finish_metric_grounding_vertical_step_to_max_ratio"), Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(GetField<bool>(diagnostics, "finish_metric_grounding_vertical_step_at_max_step"), Is.False);
                Assert.That(GetField<int>(diagnostics, "recording_grounding_step_clamp_delta"), Is.EqualTo(2167));
                Assert.That(GetField<int>(diagnostics, "recording_grounding_smoothed_delta"), Is.EqualTo(5563));
                Assert.That(GetField<string>(diagnostics, "grounding_step_limit_role"), Does.Contain("prewarm"));
            }
            finally
            {
                if (File.Exists(tempCsv))
                {
                    File.Delete(tempCsv);
                }
            }
        }

        private static object BuildSampleOrderingDiagnostic(
            string jobMode,
            string sceneName,
            string metricsCsvPath)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner, Assembly-CSharp");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "BuildSampleOrderingDiagnostic",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(string) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner summary must expose frame-0/prewarm/grounding sample ordering diagnostics.");

            return method.Invoke(null, new object[] { jobMode, sceneName, metricsCsvPath });
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return (T)field.GetValue(instance);
            }

            PropertyInfo property = instance.GetType().GetProperty(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Expected field or property '{fieldName}' to exist.");
            return (T)property.GetValue(instance);
        }
    }
}
