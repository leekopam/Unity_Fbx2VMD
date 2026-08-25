using System;
using System.IO;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonArtifactNamePolicy
    {
        internal static string SanitizeFileName(string fileName, string fallbackFileName)
        {
            string safeName = string.IsNullOrWhiteSpace(fileName)
                ? fallbackFileName ?? string.Empty
                : fileName.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalidChar, '_');
            }

            return safeName.Replace(' ', '_');
        }

        internal static string BuildEvidenceFileName(
            string prefix,
            string role,
            string extension,
            string fallbackExtension,
            string fallbackRole)
        {
            string safePrefix = SanitizeFileName(prefix, "evidence");
            string safeRole = SanitizeFileName(role, fallbackRole);
            string safeExtension = string.IsNullOrWhiteSpace(extension)
                ? fallbackExtension
                : extension;
            return $"{safePrefix}-{safeRole}{safeExtension}";
        }

        internal static string ShortenToLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            int safeMaxLength = Math.Max(10, maxLength);
            if (value.Length <= safeMaxLength)
            {
                return value;
            }

            const int hashLength = 8;
            int prefixLength = Math.Max(1, safeMaxLength - hashLength - 1);
            return $"{value.Substring(0, prefixLength)}_{CalculateStableHash(value):x8}";
        }

        private static uint CalculateStableHash(string value)
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;
            uint hash = offsetBasis;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash;
        }
    }
}
