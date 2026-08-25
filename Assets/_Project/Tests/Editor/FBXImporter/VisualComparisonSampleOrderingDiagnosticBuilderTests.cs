using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonSampleOrderingDiagnosticBuilderTests
    {
        [Test]
        public void Given_MetricsCsv_When_Populating_Then_CalculatesRecordingSpans()
        {
            string csvPath = Path.Combine(
                Path.GetTempPath(),
                "visual-comparison-ordering-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                File.WriteAllText(
                    csvPath,
                    string.Join(
                        Environment.NewLine,
                        "reason,timeSinceLevelLoad,frameCount,recorderFrame,animationClipTime,retargetGroundingStepClampCount,retargetGroundingSmoothedCount",
                        "start,1.5,120,0,0,12,60",
                        "finish,201.1,7208,6001,200,2196,5620"));

                Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
                Type dataType = runtimeAssembly.GetType(
                    "Fbx2Vmd.FBXImporter.VisualComparisonSampleOrderingDiagnosticData",
                    throwOnError: true);
                Type builderType = runtimeAssembly.GetType(
                    "Fbx2Vmd.FBXImporter.VisualComparisonSampleOrderingDiagnosticBuilder",
                    throwOnError: false);
                Assert.That(builderType, Is.Not.Null, "샘플 순서 CSV 판독 책임을 분리해야 합니다.");

                object diagnostic = Activator.CreateInstance(dataType, nonPublic: true);
                MethodInfo populateMethod = builderType.GetMethod(
                    "Populate",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(populateMethod, Is.Not.Null);
                populateMethod.Invoke(null, new[] { diagnostic, "MainAuto", "Main_Auto", csvPath, string.Empty });

                Assert.That(dataType.GetField("metric_row_count").GetValue(diagnostic), Is.EqualTo(2));
                Assert.That(dataType.GetField("recording_metric_recorder_frame_span").GetValue(diagnostic), Is.EqualTo(6001));
                Assert.That(dataType.GetField("recording_metric_engine_frame_span").GetValue(diagnostic), Is.EqualTo(7088));
            }
            finally
            {
                if (File.Exists(csvPath))
                {
                    File.Delete(csvPath);
                }
            }
        }
    }
}
