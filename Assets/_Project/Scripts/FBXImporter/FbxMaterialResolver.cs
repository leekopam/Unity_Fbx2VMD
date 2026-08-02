using System;
using System.IO;

namespace Fbx2Vmd.FBXImporter
{
    public static class FbxMaterialResolver
    {
        private static readonly string[] TextureCandidateDirectories =
        {
            "",
            "tex",
            "textures",
            "Texture",
            "Texture2D"
        };

        private static readonly string[] RuntimeTextureExtensions =
        {
            ".png",
            ".jpg",
            ".jpeg"
        };

        public static string ResolveTextureCandidate(string fbxPath, string textureReference)
        {
            if (string.IsNullOrWhiteSpace(fbxPath))
            {
                return string.Empty;
            }

            string fbxDirectory = Path.GetDirectoryName(GetFullPath(fbxPath));
            return ResolveTextureCandidateFromDirectory(fbxDirectory, textureReference);
        }

        public static string ResolveTextureCandidateFromMaterialName(string fbxDirectory, string materialName)
        {
            if (string.IsNullOrWhiteSpace(fbxDirectory) || string.IsNullOrWhiteSpace(materialName))
            {
                return string.Empty;
            }

            string sourceDirectory = GetFullPath(fbxDirectory);
            if (string.IsNullOrEmpty(sourceDirectory))
            {
                return string.Empty;
            }

            string materialToken = StripMaterialOrdinalPrefix(materialName.Trim());
            if (string.IsNullOrWhiteSpace(materialToken))
            {
                return string.Empty;
            }

            foreach (string candidateDirectory in TextureCandidateDirectories)
            {
                string directory = string.IsNullOrEmpty(candidateDirectory)
                    ? sourceDirectory
                    : Path.Combine(sourceDirectory, candidateDirectory);
                string candidate = FindTextureByBaseName(directory, materialToken);
                if (!string.IsNullOrEmpty(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        public static string ResolveTextureCandidateFromDirectory(string fbxDirectory, string textureReference)
        {
            if (string.IsNullOrWhiteSpace(fbxDirectory) || string.IsNullOrWhiteSpace(textureReference))
            {
                return string.Empty;
            }

            string sourceDirectory = GetFullPath(fbxDirectory);
            if (string.IsNullOrEmpty(sourceDirectory) || ContainsParentTraversal(textureReference))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(textureReference))
            {
                string rootedCandidate = GetFullPath(textureReference);
                return IsInsideDirectory(sourceDirectory, rootedCandidate) && File.Exists(rootedCandidate)
                    ? rootedCandidate
                    : string.Empty;
            }

            string normalizedReference = NormalizeSeparators(textureReference);
            string relativeCandidate = GetFullPath(Path.Combine(sourceDirectory, normalizedReference));
            if (IsInsideDirectory(sourceDirectory, relativeCandidate) && File.Exists(relativeCandidate))
            {
                return relativeCandidate;
            }

            string textureFileName = Path.GetFileName(normalizedReference);
            if (string.IsNullOrWhiteSpace(textureFileName))
            {
                return string.Empty;
            }

            foreach (string candidateDirectory in TextureCandidateDirectories)
            {
                string directory = string.IsNullOrEmpty(candidateDirectory)
                    ? sourceDirectory
                    : Path.Combine(sourceDirectory, candidateDirectory);
                string candidate = FindFileByName(directory, textureFileName);
                if (!string.IsNullOrEmpty(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static string FindFileByName(string directory, string textureFileName)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return string.Empty;
            }

            foreach (string file in Directory.EnumerateFiles(directory))
            {
                if (string.Equals(Path.GetFileName(file), textureFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return GetFullPath(file);
                }
            }

            return string.Empty;
        }

        private static string FindTextureByBaseName(string directory, string textureBaseName)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return string.Empty;
            }

            foreach (string file in Directory.EnumerateFiles(directory))
            {
                if (!IsRuntimeLoadableTexture(file))
                {
                    continue;
                }

                if (string.Equals(Path.GetFileNameWithoutExtension(file), textureBaseName, StringComparison.OrdinalIgnoreCase))
                {
                    return GetFullPath(file);
                }
            }

            return string.Empty;
        }

        private static bool IsRuntimeLoadableTexture(string path)
        {
            string extension = Path.GetExtension(path);
            foreach (string supportedExtension in RuntimeTextureExtensions)
            {
                if (string.Equals(extension, supportedExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string StripMaterialOrdinalPrefix(string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName))
            {
                return string.Empty;
            }

            int separator = materialName.IndexOf('_');
            if (separator <= 0)
            {
                return materialName;
            }

            string prefix = materialName.Substring(0, separator);
            foreach (char c in prefix)
            {
                if (!char.IsDigit(c) && c != '.')
                {
                    return materialName;
                }
            }

            return materialName.Substring(separator + 1);
        }

        private static bool ContainsParentTraversal(string path)
        {
            foreach (string segment in NormalizeSeparators(path).Split('/'))
            {
                if (segment == "..")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInsideDirectory(string rootDirectory, string candidatePath)
        {
            if (string.IsNullOrEmpty(rootDirectory) || string.IsNullOrEmpty(candidatePath))
            {
                return false;
            }

            string rootWithSeparator = rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string normalizedCandidate = candidatePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return normalizedCandidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFullPath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
            catch (NotSupportedException)
            {
                return string.Empty;
            }
            catch (PathTooLongException)
            {
                return string.Empty;
            }
        }

        private static string NormalizeSeparators(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
