using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonSummaryMarkdownRendererTests
    {
        [Test]
        public void Given_SummaryData_When_RenderingHeader_Then_UsesSnapshotValues()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type summaryType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonSummaryData",
                throwOnError: true);
            Type rendererType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonSummaryMarkdownRenderer",
                throwOnError: false);
            Assert.That(rendererType, Is.Not.Null, "YYB 비교 요약 Markdown 렌더러가 필요합니다.");

            object summary = Activator.CreateInstance(summaryType, nonPublic: true);
            summaryType.GetField("session_id").SetValue(summary, "session-01");
            summaryType.GetField("generated_at").SetValue(summary, "2026-08-25T14:05:06.0000000+09:00");
            summaryType.GetField("fbx_file").SetValue(summary, "dance|sample.fbx");
            summaryType.GetField("duration_seconds").SetValue(summary, 2.5f);
            summaryType.GetField("target_frame_count").SetValue(summary, 75);
            summaryType.GetField("segment").SetValue(summary, "Full");
            summaryType.GetField("yyb_arm_swing_limit_enabled").SetValue(summary, true);
            summaryType.GetField("reference_clip_asset_path").SetValue(summary, "Assets/clip|sample.fbx");

            MethodInfo renderMethod = rendererType.GetMethod(
                "RenderHeader",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(renderMethod, Is.Not.Null);

            string markdown = (string)renderMethod.Invoke(null, new[] { summary });

            Assert.That(markdown, Does.StartWith("# YYB Visual Comparison Batch"));
            Assert.That(markdown, Does.Contain("- session id: `session-01`"));
            Assert.That(markdown, Does.Contain("- generated at: `2026-08-25 14:05:06`"));
            Assert.That(markdown, Does.Contain("- fbx file: `dance\\|sample.fbx`"));
            Assert.That(markdown, Does.Contain("- target frames: `75`"));
            Assert.That(markdown, Does.Contain("- YYB arm swing limit runtime override: `True`"));
            Assert.That(markdown, Does.Contain("- reference clip asset: `Assets/clip\\|sample.fbx`"));
        }
    }
}
