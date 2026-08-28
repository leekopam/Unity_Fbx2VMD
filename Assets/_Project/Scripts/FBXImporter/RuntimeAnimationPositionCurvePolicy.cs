namespace Fbx2Vmd.FBXImporter
{
    internal static class RuntimeAnimationPositionCurvePolicy
    {
        internal static bool ShouldImport(string relativePath, string nodeName)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return true;
            }

            if (string.IsNullOrEmpty(nodeName))
            {
                return false;
            }

            string normalizedName = nodeName
                .Replace(" ", "")
                .Replace("_", "")
                .Replace(":", "")
                .ToLowerInvariant();
            return normalizedName.Contains("root")
                || normalizedName.Contains("hips")
                || normalizedName.Contains("pelvis")
                || normalizedName.Contains("center")
                || normalizedName.Contains("groove");
        }
    }
}
