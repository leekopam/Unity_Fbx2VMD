using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;

namespace Tests.Editor.FBXImporter
{
    public class FBXImportExceptionOwnershipTests
    {
        [Test]
        public void Given_ImportException_When_CheckingFileOwnership_Then_TypeHasDedicatedFile()
        {
            string importerPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "AssimpFBXImporter.cs");
            string exceptionPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXImportException.cs");

            Assert.That(File.Exists(exceptionPath), Is.True);
            Assert.That(File.ReadAllText(importerPath), Does.Not.Contain("class FBXImportException"));
            Assert.That(File.ReadAllText(exceptionPath), Does.Contain("public sealed class FBXImportException"));
        }

        [Test]
        public void Given_Message_When_CreatingImportException_Then_PreservesContract()
        {
            FBXImportException exception = new FBXImportException("import failed");

            Assert.That(exception.Message, Is.EqualTo("import failed"));
            Assert.That(exception.InnerException, Is.Null);
        }

        [Test]
        public void Given_MessageAndInnerException_When_CreatingImportException_Then_PreservesContract()
        {
            InvalidOperationException innerException = new InvalidOperationException("inner");

            FBXImportException exception = new FBXImportException("import failed", innerException);

            Assert.That(exception.Message, Is.EqualTo("import failed"));
            Assert.That(exception.InnerException, Is.SameAs(innerException));
        }
    }
}
