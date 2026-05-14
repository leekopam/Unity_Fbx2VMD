using NUnit.Framework;
using System;
using System.IO;

namespace Tests.Editor.VMDRecorderSample
{
    public class MotionComparisonProbeOutputPathsTests
    {
        [Test]
        public void Given_DataPath_When_GetProjectRootFromDataPath_Then_ReturnsParent()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            Directory.CreateDirectory(dataPath);

            try
            {
                Assert.That(MotionComparisonProbeOutputPaths.GetProjectRootFromDataPath(dataPath), Is.EqualTo(root));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_DataPath_When_GetOrCreateFolderFromDataPath_Then_CreatesFolder()
        {
            string root = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests", Guid.NewGuid().ToString("N"));
            string dataPath = Path.Combine(root, "Assets");
            Directory.CreateDirectory(dataPath);

            string created = string.Empty;

            try
            {
                created = MotionComparisonProbeOutputPaths.GetOrCreateFolderFromDataPath(
                    dataPath,
                    "Docs",
                    "Machine_Spirit",
                    "Local",
                    "ComparisonLogs");

                Assert.That(Directory.Exists(created), Is.True);
                Assert.That(created, Does.Contain(root));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }
    }
}

