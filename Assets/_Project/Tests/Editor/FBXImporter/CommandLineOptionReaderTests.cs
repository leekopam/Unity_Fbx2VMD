using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class CommandLineOptionReaderTests
    {
        [Test]
        public void Given_BooleanNumberAndFloatOptions_When_Reading_Then_ParsesInvariantValues()
        {
            Type readerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.CommandLineOptionReader",
                throwOnError: false);
            Assert.That(readerType, Is.Not.Null, "모델 중립 명령줄 옵션 판독기가 필요합니다.");

            string[] arguments = { "tool", "-enabled", "1", "-duration", "1.25" };
            MethodInfo readBoolMethod = readerType.GetMethod(
                "ReadBool",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo readFloatMethod = readerType.GetMethod(
                "ReadFloat",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(readBoolMethod, Is.Not.Null);
            Assert.That(readFloatMethod, Is.Not.Null);

            bool enabled = (bool)readBoolMethod.Invoke(null, new object[] { arguments, "-enabled", false });
            float duration = (float)readFloatMethod.Invoke(null, new object[] { arguments, "-duration", 0f });

            Assert.That(enabled, Is.True);
            Assert.That(duration, Is.EqualTo(1.25f));
        }
    }
}
