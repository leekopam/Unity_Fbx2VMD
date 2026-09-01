using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    internal static class YybVisualComparisonFrameRoleDiagnosticsTestSupport
    {
        internal static object BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            string provenancePath,
            string resultPath,
            string frameMetricsPath,
            string contactSheetPath)
        {
            return BuildFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                referenceClipStartSeconds: 0f,
                provenancePath,
                resultPath,
                frameMetricsPath,
                contactSheetPath,
                candidateFrameIndexPath: string.Empty);
        }

        internal static object BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            string provenancePath,
            string resultPath,
            string frameMetricsPath,
            string contactSheetPath,
            string candidateFrameIndexPath)
        {
            return BuildFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                referenceClipStartSeconds: 0f,
                provenancePath,
                resultPath,
                frameMetricsPath,
                contactSheetPath,
                candidateFrameIndexPath);
        }

        internal static object BuildSummaryFrameRoleDiagnosticsWithReferenceClipStart(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string provenancePath,
            string resultPath,
            string frameMetricsPath,
            string contactSheetPath,
            string candidateFrameIndexPath)
        {
            return BuildFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                referenceClipStartSeconds,
                provenancePath,
                resultPath,
                frameMetricsPath,
                contactSheetPath,
                candidateFrameIndexPath);
        }

        internal static object BuildFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string provenancePath,
            string resultPath,
            string frameMetricsPath,
            string contactSheetPath,
            string candidateFrameIndexPath,
            string referenceVideoProjectRoot = null,
            string candidateFrameProjectRoot = null)
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type requestType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonFrameRoleDiagnosticsBuildRequest",
                throwOnError: true);
            object request = Activator.CreateInstance(requestType, nonPublic: true);
            SetProperty(request, "ReferenceTargetFrameCount", referenceTargetFrameCount);
            SetProperty(request, "BaselineRecordedFrameCount", baselineRecordedFrameCount);
            SetProperty(request, "CandidateRecordedFrameCount", candidateRecordedFrameCount);
            SetProperty(request, "RequestedDurationSeconds", requestedDurationSeconds);
            SetProperty(request, "ReferenceClipStartSeconds", referenceClipStartSeconds);
            SetProperty(request, "ReferenceVideoProvenanceEvidencePath", provenancePath);
            SetProperty(request, "ReferenceVideoAnalysisResultPath", resultPath);
            SetProperty(request, "ReferenceVideoFrameMetricsPath", frameMetricsPath);
            SetProperty(request, "ReferenceVideoContactSheetPath", contactSheetPath);
            SetProperty(request, "CandidateFrameIndexPath", candidateFrameIndexPath);
            SetProperty(
                request,
                "ReferenceVideoProjectRoot",
                referenceVideoProjectRoot ?? Directory.GetCurrentDirectory());
            SetProperty(
                request,
                "CandidateFrameProjectRoot",
                candidateFrameProjectRoot ?? Directory.GetCurrentDirectory());
            SetProperty(request, "TargetFrameCountRole", "ref_mmd_mp4 expected frame range for the full satisfaction_2 reference");
            SetProperty(request, "BaselineRecordedFrameCountRole", "Sub_Manual recorded comparison baseline; reported separately and not used as target_frame_count");
            SetProperty(request, "CandidateRecordedFrameCountRole", "Main_Auto candidate capture under test");
            SetProperty(request, "FrameQualityMetricBasis", "Unity pose metrics compare Sub_Manual and Main_Auto rows by recorderFrame; the ref_mmd_mp4 count is only the frame-count target");
            SetProperty(request, "VmdExportMetricBasis", "VMD export spike and floor metrics are evaluated on the Main_Auto candidate VMD");
            SetProperty(request, "ReferenceVideoCanonicalContext", "Ref MP4 is a manually postprocessed MMD render from Sub_Manual testPrefab + satisfaction_2.");
            SetProperty(request, "ReferenceVideoAnalysisMetricBasis", "MP4 analysis supplies visual bbox/framing context.");

            Type builderType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonFrameRoleDiagnosticsBuilder",
                throwOnError: true);
            MethodInfo buildMethod = builderType.GetMethod(
                "Build",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(buildMethod, Is.Not.Null);
            return buildMethod.Invoke(null, new[] { request });
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Expected property '{propertyName}' to exist.");
            property.SetValue(target, value);
        }

        internal static T GetField<T>(object target, string fieldName)
        {
            Assert.That(target, Is.Not.Null);
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");
            return (T)field.GetValue(target);
        }

        internal static void WriteFixturePng(string path, RectInt brightRect)
        {
            WriteFixturePng(path, new[] { brightRect });
        }

        internal static void WriteFixturePng(string path, params RectInt[] brightRects)
        {
            var texture = new Texture2D(10, 10, TextureFormat.RGBA32, mipChain: false);
            try
            {
                var pixels = new Color32[100];
                for (int index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = new Color32(0, 0, 0, 255);
                }

                foreach (RectInt brightRect in brightRects)
                {
                    for (int y = brightRect.yMin; y < brightRect.yMax; y++)
                    {
                        for (int x = brightRect.xMin; x < brightRect.xMax; x++)
                        {
                            pixels[(y * 10) + x] = new Color32(255, 255, 255, 255);
                        }
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
