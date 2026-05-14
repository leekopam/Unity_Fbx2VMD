using System.IO;
using UnityEngine;

internal static class MotionComparisonProbeOutputPaths
{
    public static string GetProjectRootFromDataPath(string dataPath)
    {
        DirectoryInfo projectRoot = Directory.GetParent(dataPath);
        return projectRoot != null ? projectRoot.FullName : dataPath;
    }

    public static string GetOrCreateFolderFromDataPath(string dataPath, params string[] pathSegments)
    {
        string rootPath = GetProjectRootFromDataPath(dataPath);

        string folderPath = rootPath;
        if (pathSegments != null)
        {
            foreach (string segment in pathSegments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                folderPath = Path.Combine(folderPath, segment);
            }
        }

        Directory.CreateDirectory(folderPath);
        return folderPath;
    }
}

