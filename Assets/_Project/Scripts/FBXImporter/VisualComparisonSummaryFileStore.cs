using System.IO;
using System.Text;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonSummaryFileStore
    {
        internal static void WriteJson(string path, object summary)
        {
            EnsureParentDirectory(path);
            File.WriteAllText(path, JsonUtility.ToJson(summary, true), Encoding.UTF8);
        }

        internal static void WriteText(string path, string content)
        {
            EnsureParentDirectory(path);
            File.WriteAllText(path, content ?? string.Empty, Encoding.UTF8);
        }

        internal static void CopyLatest(string sourcePath, string projectRoot, string relativeTargetPath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            string targetPath = Path.Combine(projectRoot, relativeTargetPath);
            EnsureParentDirectory(targetPath);
            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        private static void EnsureParentDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
