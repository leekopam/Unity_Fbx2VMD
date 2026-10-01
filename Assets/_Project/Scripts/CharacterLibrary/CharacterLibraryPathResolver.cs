using System;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 캐릭터 라이브러리 저장 위치 해석. MainRecordingSettingsPathResolver와 같은 우선순위:
    /// 명시 경로 > 환경변수 > LocalAppData > persistentDataPath.
    /// </summary>
    public static class CharacterLibraryPathResolver
    {
        public const string EnvironmentVariableName = "UNITY_FBX2VMD_CHARACTER_LIBRARY_PATH";
        public const string AppFolderName = "Unity_Fbx2VMD";
        public const string FeatureFolderName = "CharacterLibrary";
        public const string LibraryFileName = "characters.json";
        public const string ThumbnailsFolderName = "thumbnails";
        public const string PresetsFolderName = "presets";

        public static string ResolveLibraryFilePath(
            string explicitPath = null,
            string environmentOverridePath = null,
            string localAppDataRoot = null,
            string persistentDataRoot = null,
            bool readProcessEnvironment = true)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                return explicitPath.Trim();
            }

            if (!string.IsNullOrWhiteSpace(environmentOverridePath))
            {
                return environmentOverridePath.Trim();
            }

            if (readProcessEnvironment)
            {
                string processOverridePath = Environment.GetEnvironmentVariable(EnvironmentVariableName);
                if (!string.IsNullOrWhiteSpace(processOverridePath))
                {
                    return processOverridePath.Trim();
                }
            }

            string root = localAppDataRoot;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                root = persistentDataRoot;
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                root = Application.persistentDataPath;
            }

            return Path.Combine(root, AppFolderName, FeatureFolderName, LibraryFileName);
        }

        /// <summary>
        /// 라이브러리 JSON 파일이 있는 디렉터리(썸네일 등 부속 파일의 루트)를 돌려준다.
        /// </summary>
        public static string ResolveLibraryDirectory(string libraryFilePath)
        {
            if (string.IsNullOrWhiteSpace(libraryFilePath))
            {
                return string.Empty;
            }

            return Path.GetDirectoryName(libraryFilePath);
        }

        /// <summary>
        /// 썸네일 PNG 캐시 디렉터리 경로를 돌려준다. 디렉터리는 생성하지 않는다.
        /// </summary>
        public static string ResolveThumbnailsDirectory(string libraryFilePath)
        {
            string libraryDirectory = ResolveLibraryDirectory(libraryFilePath);
            if (string.IsNullOrWhiteSpace(libraryDirectory))
            {
                return string.Empty;
            }

            return Path.Combine(libraryDirectory, ThumbnailsFolderName);
        }

        /// <summary>
        /// 엔트리의 thumbnailFileName(파일명만 허용)을 절대 경로로 변환한다.
        /// 경로 이탈 문자가 포함된 경우 빈 문자열을 돌려준다.
        /// </summary>
        public static string ResolveThumbnailPath(string libraryFilePath, string thumbnailFileName)
        {
            if (string.IsNullOrWhiteSpace(thumbnailFileName))
            {
                return string.Empty;
            }

            if (thumbnailFileName != Path.GetFileName(thumbnailFileName))
            {
                return string.Empty;
            }

            string thumbnailsDirectory = ResolveThumbnailsDirectory(libraryFilePath);
            if (string.IsNullOrWhiteSpace(thumbnailsDirectory))
            {
                return string.Empty;
            }

            return Path.Combine(thumbnailsDirectory, thumbnailFileName);
        }

        /// <summary>
        /// 프리셋 문서 디렉터리 경로를 돌려준다. 디렉터리는 생성하지 않는다.
        /// </summary>
        public static string ResolvePresetsDirectory(string libraryFilePath)
        {
            string libraryDirectory = ResolveLibraryDirectory(libraryFilePath);
            if (string.IsNullOrWhiteSpace(libraryDirectory))
            {
                return string.Empty;
            }

            return Path.Combine(libraryDirectory, PresetsFolderName);
        }

        /// <summary>
        /// 캐릭터 id로 프리셋 JSON 절대 경로를 돌려준다.
        /// id는 파일명만 허용하고, 경로 이탈 문자가 있으면 빈 문자열을 돌려준다.
        /// </summary>
        public static string ResolvePresetFilePath(string libraryFilePath, string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId) ||
                characterId != Path.GetFileName(characterId))
            {
                return string.Empty;
            }

            string presetsDirectory = ResolvePresetsDirectory(libraryFilePath);
            if (string.IsNullOrWhiteSpace(presetsDirectory))
            {
                return string.Empty;
            }

            return Path.Combine(presetsDirectory, characterId + ".json");
        }
    }
}
