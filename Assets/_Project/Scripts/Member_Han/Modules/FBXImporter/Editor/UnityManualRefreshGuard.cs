#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Member_Han.Modules.FBXImporter.EditorTools
{
    public static class UnityManualRefreshGuard
    {
        private const string MenuPath = "Machine Spirit/Unity/Manual Refresh And Check";

        public static List<string> GetRefreshableAssetPaths(IEnumerable<string> paths)
        {
            var result = new List<string>();
            if (paths == null)
            {
                return result;
            }

            foreach (string rawPath in paths)
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    continue;
                }

                string assetPath = rawPath.Replace('\\', '/').Trim();
                if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!File.Exists(assetPath) && !Directory.Exists(assetPath))
                {
                    continue;
                }

                if (!result.Contains(assetPath))
                {
                    result.Add(assetPath);
                }
            }

            return result;
        }

        public static bool RequestRefreshForAssets(IEnumerable<string> paths, string reason)
        {
            List<string> assetPaths = GetRefreshableAssetPaths(paths);
            foreach (string assetPath in assetPaths)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[UnityManualRefreshGuard] refresh requested: reason={reason}, assets={assetPaths.Count}");
            return EditorApplication.isCompiling || EditorApplication.isUpdating;
        }

        public static bool IsEditorBusy()
        {
            return EditorApplication.isCompiling || EditorApplication.isUpdating;
        }

        [MenuItem(MenuPath)]
        private static void ManualRefreshAndCheck()
        {
            RequestRefreshForAssets(Array.Empty<string>(), "menu");
            EditorApplication.delayCall += ReportManualRefreshSettled;
        }

        private static void ReportManualRefreshSettled()
        {
            if (IsEditorBusy())
            {
                EditorApplication.delayCall += ReportManualRefreshSettled;
                return;
            }

            Debug.Log("[UnityManualRefreshGuard] refresh settled; check Console for compile/import errors.");
        }
    }
}
#endif
