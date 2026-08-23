using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Tests.Editor.FBXImporter
{
    public class FBXImportControllerOwnershipTests
    {
        [Test]
        public void Given_ImportController_When_CheckingPipelineComposition_Then_KeepsSingleControllerField()
        {
            FieldInfo field = typeof(FBXVmdPipeline).GetField(
                "_importController",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(FBXImportController)));
        }

        [Test]
        public void Given_ImportEntryPoints_When_InspectingPipelineSource_Then_DelegatesToSingleController()
        {
            string sourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(Regex.Matches(source, @"new FBXImportController\(").Count, Is.EqualTo(1));
            Assert.That(source, Does.Contain("_importController.ImportFromDialog();"));
            Assert.That(source, Does.Contain("_importController.LoadFromImportFolder();"));
            Assert.That(source, Does.Contain("return _importController.TryImportFromSharedSettings(sourcePath);"));
        }
    }
}
