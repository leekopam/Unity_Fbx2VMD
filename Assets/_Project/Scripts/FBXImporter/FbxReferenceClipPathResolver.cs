using System;
using System.IO;

namespace Fbx2Vmd.FBXImporter
{
    internal static class FbxReferenceClipPathResolver
    {
        internal static string Resolve(
            string fbxFileName,
            string defaultFbxFileName,
            string projectFbxDirectory,
            string importFbxDirectory,
            Func<string, bool> hasReferenceClip)
        {
            if (hasReferenceClip == null)
            {
                throw new ArgumentNullException(nameof(hasReferenceClip));
            }

            string normalizedFileName = NormalizeFileName(fbxFileName, defaultFbxFileName);
            string projectCandidate = Path.Combine(projectFbxDirectory, normalizedFileName).Replace('\\', '/');
            if (hasReferenceClip(projectCandidate))
            {
                return projectCandidate;
            }

            string importCandidate = Path.Combine(importFbxDirectory, normalizedFileName).Replace('\\', '/');
            _ = hasReferenceClip(importCandidate);
            return importCandidate;
        }

        internal static string NormalizeFileName(string fbxFileName, string defaultFbxFileName)
        {
            string name = string.IsNullOrWhiteSpace(fbxFileName) ? defaultFbxFileName : fbxFileName.Trim();
            return string.Equals(Path.GetExtension(name), ".fbx", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(name)
                : Path.GetFileNameWithoutExtension(name) + ".fbx";
        }
    }
}
