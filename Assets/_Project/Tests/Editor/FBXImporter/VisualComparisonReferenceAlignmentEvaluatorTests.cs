using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonReferenceAlignmentEvaluatorTests
    {
        [Test]
        public void Given_AlignedDiagnostics_When_Evaluating_Then_ReturnsTrue()
        {
            object diagnostics = CreateAlignedDiagnostics();

            bool result = Evaluate(diagnostics);

            Assert.That(result, Is.True);
        }

        [Test]
        public void Given_DiagnosticReadError_When_Evaluating_Then_ReturnsFalse()
        {
            object diagnostics = CreateAlignedDiagnostics();
            SetField(diagnostics, "reference_mp4_frame_metrics_error", "read failed");

            bool result = Evaluate(diagnostics);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Given_InsufficientMatchedSamples_When_Evaluating_Then_ReturnsFalse()
        {
            object diagnostics = CreateAlignedDiagnostics();
            SetField(diagnostics, "candidate_vs_reference_time_matched_sample_count", 4);

            bool result = Evaluate(diagnostics);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Given_EndpointDeltaBeyondPixelTolerance_When_Evaluating_Then_ReturnsFalse()
        {
            object diagnostics = CreateAlignedDiagnostics();
            SetField(
                diagnostics,
                "candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta",
                0.302f);

            bool result = Evaluate(diagnostics);

            Assert.That(result, Is.False);
        }

        private static object CreateAlignedDiagnostics()
        {
            Type diagnosticsType = GetRuntimeType("VisualComparisonFrameRoleDiagnosticsData");
            object diagnostics = Activator.CreateInstance(diagnosticsType, nonPublic: true);
            SetField(diagnostics, "reference_mp4_current_clip_sample_count", 5);
            SetField(diagnostics, "candidate_vs_reference_time_matched_sample_count", 5);
            SetField(diagnostics, "candidate_screenshot_nonblank_frame_count", 5);
            SetField(diagnostics, "candidate_vs_reference_time_matched_max_seconds_gap", 0.1f);
            SetField(diagnostics, "candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta", 0.05f);
            SetField(diagnostics, "candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta", 0.02f);
            SetField(diagnostics, "candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta", 0.15f);
            SetField(diagnostics, "candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta", 0.25f);
            SetField(
                diagnostics,
                "candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta",
                0.301f);
            return diagnostics;
        }

        private static bool Evaluate(object diagnostics)
        {
            Type evaluatorType = GetRuntimeType("VisualComparisonReferenceAlignmentEvaluator");
            MethodInfo method = evaluatorType.GetMethod(
                "HasAlignedEvidence",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(null, new[] { diagnostics });
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                $"Fbx2Vmd.FBXImporter.{typeName}",
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{typeName} 타입이 필요합니다.");
            return type;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            field.SetValue(target, value);
        }
    }
}
