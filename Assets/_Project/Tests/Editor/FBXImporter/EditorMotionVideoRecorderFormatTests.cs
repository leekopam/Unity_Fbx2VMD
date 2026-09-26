using System;
using System.IO;
using System.Reflection;
using Fbx2Vmd.Recording;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    /// <summary>
    /// 영상 녹화 설정이 실제 Recorder 코덱, 확장자, 출력 폴더로 연결되는지 확인함.
    /// </summary>
    public class EditorMotionVideoRecorderFormatTests
    {
        private static readonly Type RecorderType =
            typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorMotionVideoRecorder",
                throwOnError: true);

        [Test]
        public void Given_Settings_When_CreatedWithDefaults_Then_UsesMp4AndDefaultDirectory()
        {
            var settings = new MotionVideoRecordingSettings("motion", 1920, 1080, 60f);

            Assert.That(settings.Format, Is.EqualTo(MotionVideoFileFormat.Mp4));
            Assert.That(settings.OutputDirectory, Is.Null);
        }

        [Test]
        public void Given_FileFormat_When_MappingCodec_Then_MatchesRecorderEncoder()
        {
            Assert.That(
                InvokeStatic("ToCodec", MotionVideoFileFormat.Mp4)?.ToString(),
                Is.EqualTo("MP4"));
            Assert.That(
                InvokeStatic("ToCodec", MotionVideoFileFormat.WebM)?.ToString(),
                Is.EqualTo("WEBM"));
        }

        [Test]
        public void Given_FileFormat_When_MappingExtension_Then_MatchesContainer()
        {
            Assert.That(
                InvokeStatic("GetExtension", MotionVideoFileFormat.Mp4),
                Is.EqualTo(".mp4"));
            Assert.That(
                InvokeStatic("GetExtension", MotionVideoFileFormat.WebM),
                Is.EqualTo(".webm"));
        }

        [Test]
        public void Given_OutputDirectory_When_CreatingOutputPath_Then_UsesConfiguredFolder()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "fbx2vmd-motion-video-" + Guid.NewGuid().ToString("N"));
            try
            {
                string created = (string)InvokeStatic(
                    "CreateOutputFilePath",
                    "테스트 모션",
                    directory);
                Assert.That(created, Does.StartWith(directory));
                Assert.That(Directory.Exists(directory), Is.True);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        private static object InvokeStatic(string methodName, params object[] arguments)
        {
            MethodInfo method = RecorderType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(null, arguments);
        }
    }
}
