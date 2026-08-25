using System;
using System.IO;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonArtifactPathResolver
    {
        internal static string ResolveProjectRelative(string path, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            {
                return path ?? string.Empty;
            }

            return string.IsNullOrWhiteSpace(projectRoot)
                ? path
                : Path.Combine(projectRoot, NormalizeSeparators(path));
        }

        internal static string ResolveArtifactPath(
            string path,
            string projectRoot,
            string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = NormalizeSeparators(path);
            if (Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            if (!string.IsNullOrWhiteSpace(projectRoot))
            {
                return ToAbsoluteProjectPath(normalized, projectRoot);
            }

            return string.IsNullOrWhiteSpace(baseDirectory)
                ? normalized
                : Path.Combine(baseDirectory, normalized);
        }

        internal static string ToAbsoluteProjectPath(string path, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = NormalizeSeparators(path);
            return Path.IsPathRooted(normalized)
                ? normalized
                : Path.Combine(projectRoot, normalized);
        }

        internal static string MakeProjectRelative(string absolutePath, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            string normalizedProjectRoot = (projectRoot ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            string normalizedAbsolute = absolutePath.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(normalizedProjectRoot) &&
                normalizedAbsolute.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedAbsolute.Substring(normalizedProjectRoot.Length + 1);
            }

            return normalizedAbsolute;
        }

        internal static string BuildSafeSessionId(
            string sessionId,
            string fallbackSessionId,
            string projectRoot,
            string outputDirectory,
            int maxFullPathLength,
            params string[] leafFileNames)
        {
            string safeSessionId = VisualComparisonArtifactNamePolicy.SanitizeFileName(
                sessionId,
                fallbackSessionId);
            string rootFolder = Path.Combine(
                    projectRoot ?? string.Empty,
                    outputDirectory ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int leafFileNameLength = 0;
            if (leafFileNames != null)
            {
                foreach (string leafFileName in leafFileNames)
                {
                    leafFileNameLength = Math.Max(
                        leafFileNameLength,
                        string.IsNullOrEmpty(leafFileName) ? 0 : leafFileName.Length);
                }
            }

            int maxSessionIdLength = Math.Max(
                16,
                maxFullPathLength - rootFolder.Length - 2 - leafFileNameLength);
            return VisualComparisonArtifactNamePolicy.ShortenToLength(
                safeSessionId,
                maxSessionIdLength);
        }

        internal static bool ReferToSameFile(string leftPath, string rightPath)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(leftPath),
                    Path.GetFullPath(rightPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string NormalizeSeparators(string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
