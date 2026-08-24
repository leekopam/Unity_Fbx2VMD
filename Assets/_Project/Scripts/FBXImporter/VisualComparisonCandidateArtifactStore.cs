using System;
using System.IO;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonCandidateArtifactStore
    {
        internal static VmdSaveResult Copy(
            VmdSaveResult result,
            string destinationVmdPath,
            string fallbackDirectory,
            Func<string, string> sanitizeFileName)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationVmdPath) ?? fallbackDirectory);
            File.Copy(result.FilePath, destinationVmdPath, overwrite: true);
            string exportRotationDiagnosticsCsvPath = CopySiblingArtifact(
                result.FilePath,
                result.ExportRotationDiagnosticsCsvPath,
                destinationVmdPath,
                fallbackDirectory,
                sanitizeFileName);
            string exportIkSourceDiagnosticsCsvPath = CopySiblingArtifact(
                result.FilePath,
                result.ExportIkSourceDiagnosticsCsvPath,
                destinationVmdPath,
                fallbackDirectory,
                sanitizeFileName);
            return new VmdSaveResult
            {
                Success = result.Success,
                FilePath = destinationVmdPath,
                ErrorMessage = result.ErrorMessage ?? string.Empty,
                FrameCount = result.FrameCount,
                FileSizeBytes = new FileInfo(destinationVmdPath).Length,
                ExportRotationDiagnosticsCsvPath = exportRotationDiagnosticsCsvPath,
                ExportIkSourceDiagnosticsCsvPath = exportIkSourceDiagnosticsCsvPath
            };
        }

        private static string CopySiblingArtifact(
            string sourceVmdPath,
            string sourceArtifactPath,
            string candidateVmdPath,
            string fallbackDirectory,
            Func<string, string> sanitizeFileName)
        {
            if (string.IsNullOrWhiteSpace(sourceArtifactPath) ||
                !File.Exists(sourceArtifactPath) ||
                string.IsNullOrWhiteSpace(candidateVmdPath))
            {
                return string.Empty;
            }

            string destinationPath = BuildSiblingArtifactPath(
                sourceVmdPath,
                sourceArtifactPath,
                candidateVmdPath,
                fallbackDirectory,
                sanitizeFileName);
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                return string.Empty;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? fallbackDirectory);
            File.Copy(sourceArtifactPath, destinationPath, overwrite: true);
            return destinationPath;
        }

        private static string BuildSiblingArtifactPath(
            string sourceVmdPath,
            string sourceArtifactPath,
            string candidateVmdPath,
            string fallbackDirectory,
            Func<string, string> sanitizeFileName)
        {
            string artifactFileName = Path.GetFileName(sourceArtifactPath);
            string candidateDirectory = Path.GetDirectoryName(candidateVmdPath) ?? fallbackDirectory;
            string candidateBaseName = Path.GetFileNameWithoutExtension(candidateVmdPath);
            if (string.IsNullOrWhiteSpace(artifactFileName) ||
                string.IsNullOrWhiteSpace(candidateDirectory) ||
                string.IsNullOrWhiteSpace(candidateBaseName))
            {
                return string.Empty;
            }

            string sourceBaseName = Path.GetFileNameWithoutExtension(sourceVmdPath);
            string fallbackBaseName = Path.GetFileNameWithoutExtension(sourceArtifactPath);
            string safeFallbackBaseName = sanitizeFileName != null
                ? sanitizeFileName(fallbackBaseName)
                : fallbackBaseName;
            string suffix = !string.IsNullOrWhiteSpace(sourceBaseName) &&
                artifactFileName.StartsWith(sourceBaseName, StringComparison.OrdinalIgnoreCase)
                    ? artifactFileName.Substring(sourceBaseName.Length)
                    : $".{safeFallbackBaseName}{Path.GetExtension(sourceArtifactPath)}";
            if (string.IsNullOrWhiteSpace(suffix))
            {
                suffix = Path.GetExtension(sourceArtifactPath);
            }

            return Path.Combine(candidateDirectory, $"{candidateBaseName}{suffix}");
        }
    }
}
