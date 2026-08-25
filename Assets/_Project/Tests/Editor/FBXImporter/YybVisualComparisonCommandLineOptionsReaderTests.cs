using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonCommandLineOptionsReaderTests
    {
        [Test]
        public void Given_CommandLineArguments_When_Applying_Then_OverridesMatchingYybOptionsOnly()
        {
            Type assemblyMarker = typeof(FBXVmdPipeline);
            Type optionsType = assemblyMarker.Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptions",
                throwOnError: true);
            Type readerType = assemblyMarker.Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonCommandLineOptionsReader",
                throwOnError: false);
            Assert.That(readerType, Is.Not.Null, "YYB 명령줄 옵션 조립 책임이 필요합니다.");

            object options = Activator.CreateInstance(optionsType, nonPublic: true);
            optionsType.GetField("fbxFileName").SetValue(options, "fallback.fbx");
            optionsType.GetField("durationSeconds").SetValue(options, 2f);
            optionsType.GetField("diagnosticCaptureWidthOverride").SetValue(options, 640);
            string[] arguments =
            {
                "tool",
                "-yybCompareFbx", "future-model.fbx",
                "-yybCompareDuration", "3.5",
                "-yybCompareYybArmSwingLimitEnabled", "1"
            };

            MethodInfo applyMethod = readerType.GetMethod(
                "Apply",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null);
            applyMethod.Invoke(null, new[] { arguments, options });

            Assert.That(optionsType.GetField("fbxFileName").GetValue(options), Is.EqualTo("future-model.fbx"));
            Assert.That(optionsType.GetField("durationSeconds").GetValue(options), Is.EqualTo(3.5f));
            Assert.That(
                optionsType.GetField("enableYybArmSwingLimitRuntimeOverride").GetValue(options),
                Is.True);
            Assert.That(optionsType.GetField("diagnosticCaptureWidthOverride").GetValue(options), Is.EqualTo(640));
        }
    }
}
