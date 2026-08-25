#if UNITY_EDITOR
using UnityEditor;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonRunStateStore
    {
        internal static void SaveJson(string key, string json)
        {
            SessionState.SetString(key, json ?? string.Empty);
        }

        internal static string ReadJson(string key)
        {
            return SessionState.GetString(key, string.Empty);
        }

        internal static void Clear(string key)
        {
            SessionState.EraseString(key);
        }
    }
}
#endif
