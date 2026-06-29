using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor;
using UnityEngine;

using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

namespace Tests.Editor.VMDRecorderSample
{
    public class VmdHumanoidBoneMapTests
    {
        [Test]
        public void Given_WriterFootIkName_When_ResolvingBinding_Then_MapsToFootIkTarget()
        {
            bool resolved = VmdHumanoidBoneMap.TryResolveWriterBoneName(
                "左足ＩＫ",
                out VmdHumanoidBoneBinding binding);

            Assert.That(resolved, Is.True);
            Assert.That(binding.RecorderBoneName, Is.EqualTo(BoneNames.左足ＩＫ));
            Assert.That(binding.HasHumanBodyBone, Is.True);
            Assert.That(binding.HumanBodyBone, Is.EqualTo(HumanBodyBones.LeftFoot));
            Assert.That(binding.IsIkTarget, Is.True);
            Assert.That(binding.IsMotionCarrier, Is.True);
        }

        [Test]
        public void Given_WriterHumanoidBoneNames_When_ResolvingBinding_Then_MapsToHumanoidBones()
        {
            AssertBinding("上半身", BoneNames.上半身, HumanBodyBones.Spine, isIkTarget: false);
            AssertBinding("上半身2", BoneNames.上半身2, HumanBodyBones.Chest, isIkTarget: false);
            AssertBinding("左足首", BoneNames.左足首, HumanBodyBones.LeftFoot, isIkTarget: false);
            AssertBinding("下半身", BoneNames.下半身, HumanBodyBones.Hips, isIkTarget: false);
        }

        [Test]
        public void Given_CenterRoutedToGroove_When_ResolvingCarrierNames_Then_SeparatesParentAndCenterCarriers()
        {
            bool parentResolved = VmdHumanoidBoneMap.TryResolveWriterBoneName(
                "センター",
                useCenterAsParentOfAll: true,
                routeCenterBoneToGroove: true,
                centerNameString: "センター",
                grooveNameString: "グルーブ",
                out VmdHumanoidBoneBinding parentBinding);
            bool centerResolved = VmdHumanoidBoneMap.TryResolveWriterBoneName(
                "グルーブ",
                useCenterAsParentOfAll: true,
                routeCenterBoneToGroove: true,
                centerNameString: "センター",
                grooveNameString: "グルーブ",
                out VmdHumanoidBoneBinding centerBinding);

            Assert.That(parentResolved, Is.True);
            Assert.That(centerResolved, Is.True);
            Assert.That(parentBinding.RecorderBoneName, Is.EqualTo(BoneNames.全ての親));
            Assert.That(parentBinding.HasHumanBodyBone, Is.False);
            Assert.That(parentBinding.IsMotionCarrier, Is.True);
            Assert.That(centerBinding.RecorderBoneName, Is.EqualTo(BoneNames.センター));
            Assert.That(centerBinding.HumanBodyBone, Is.EqualTo(HumanBodyBones.Hips));
        }

        [Test]
        public void Given_UnknownWriterBoneName_When_ResolvingBinding_Then_ReturnsFalse()
        {
            bool resolved = VmdHumanoidBoneMap.TryResolveWriterBoneName(
                "未登録ボーン",
                out VmdHumanoidBoneBinding binding);

            Assert.That(resolved, Is.False);
            Assert.That(binding.RecorderBoneName, Is.EqualTo(BoneNames.None));
        }

        private static void AssertBinding(
            string writerBoneName,
            BoneNames expectedRecorderBoneName,
            HumanBodyBones expectedHumanBodyBone,
            bool isIkTarget)
        {
            bool resolved = VmdHumanoidBoneMap.TryResolveWriterBoneName(
                writerBoneName,
                out VmdHumanoidBoneBinding binding);

            Assert.That(resolved, Is.True, writerBoneName);
            Assert.That(binding.RecorderBoneName, Is.EqualTo(expectedRecorderBoneName));
            Assert.That(binding.HasHumanBodyBone, Is.True);
            Assert.That(binding.HumanBodyBone, Is.EqualTo(expectedHumanBodyBone));
            Assert.That(binding.IsIkTarget, Is.EqualTo(isIkTarget));
        }
    }

    public static class VmdHumanoidBoneMapTestBatchRunner
    {
        public static void Run()
        {
            string resultPath = GetArgumentValue("-testResults");
            if (string.IsNullOrEmpty(resultPath))
            {
                resultPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults-VmdHumanoidBoneMap.xml");
            }

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var results = new List<TestResultRecord>();
            var tests = new VmdHumanoidBoneMapTests();

            RunTest(results, nameof(tests.Given_WriterFootIkName_When_ResolvingBinding_Then_MapsToFootIkTarget),
                tests.Given_WriterFootIkName_When_ResolvingBinding_Then_MapsToFootIkTarget);
            RunTest(results, nameof(tests.Given_WriterHumanoidBoneNames_When_ResolvingBinding_Then_MapsToHumanoidBones),
                tests.Given_WriterHumanoidBoneNames_When_ResolvingBinding_Then_MapsToHumanoidBones);
            RunTest(results, nameof(tests.Given_CenterRoutedToGroove_When_ResolvingCarrierNames_Then_SeparatesParentAndCenterCarriers),
                tests.Given_CenterRoutedToGroove_When_ResolvingCarrierNames_Then_SeparatesParentAndCenterCarriers);
            RunTest(results, nameof(tests.Given_UnknownWriterBoneName_When_ResolvingBinding_Then_ReturnsFalse),
                tests.Given_UnknownWriterBoneName_When_ResolvingBinding_Then_ReturnsFalse);

            double duration = Math.Max(0.001, (DateTimeOffset.UtcNow - start).TotalSeconds);
            string resultDirectory = Path.GetDirectoryName(resultPath);
            if (!string.IsNullOrEmpty(resultDirectory))
            {
                Directory.CreateDirectory(resultDirectory);
            }

            File.WriteAllText(resultPath, BuildXml(results, duration));

            int failed = 0;
            foreach (TestResultRecord result in results)
            {
                if (result.Failure != null)
                {
                    failed++;
                    Console.Error.WriteLine(result.Failure);
                }
            }

            Console.WriteLine($"VmdHumanoidBoneMap tests completed; results written to {resultPath}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static void RunTest(List<TestResultRecord> results, string methodName, TestDelegate action)
        {
            string name = typeof(VmdHumanoidBoneMapTests).FullName + "." + methodName;
            DateTimeOffset start = DateTimeOffset.UtcNow;
            string failure = null;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex.ToString();
            }

            results.Add(new TestResultRecord(name, Math.Max(0.001, (DateTimeOffset.UtcNow - start).TotalSeconds), failure));
        }

        private static string GetArgumentValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static string BuildXml(IReadOnlyList<TestResultRecord> results, double duration)
        {
            int failed = 0;
            foreach (TestResultRecord result in results)
            {
                if (result.Failure != null)
                {
                    failed++;
                }
            }

            int passed = results.Count - failed;
            string runResult = failed == 0 ? "Passed" : "Failed";
            var writer = new System.Text.StringBuilder();
            writer.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            writer.AppendLine($"<test-run testcasecount=\"{results.Count}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\" duration=\"{duration:0.000}\">");
            writer.AppendLine($"  <test-suite type=\"TestFixture\" name=\"{SecurityElement.Escape(typeof(VmdHumanoidBoneMapTests).FullName)}\" result=\"{runResult}\" total=\"{results.Count}\" passed=\"{passed}\" failed=\"{failed}\">");

            foreach (TestResultRecord result in results)
            {
                string testResult = result.Failure == null ? "Passed" : "Failed";
                string failureNode = result.Failure == null
                    ? string.Empty
                    : $"<failure><message>{SecurityElement.Escape(result.Failure)}</message></failure>";
                string escapedName = SecurityElement.Escape(result.Name);
                writer.AppendLine($"    <test-case name=\"{escapedName}\" fullname=\"{escapedName}\" result=\"{testResult}\" duration=\"{result.Duration:0.000}\">{failureNode}</test-case>");
            }

            writer.AppendLine("  </test-suite>");
            writer.AppendLine("</test-run>");
            return writer.ToString();
        }

        private sealed class TestResultRecord
        {
            public TestResultRecord(string name, double duration, string failure)
            {
                Name = name;
                Duration = duration;
                Failure = failure;
            }

            public string Name { get; }

            public double Duration { get; }

            public string Failure { get; }
        }
    }
}
