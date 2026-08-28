namespace Fbx2Vmd.FBXImporter
{
    internal static class ThumbTransformNamePolicy
    {
        internal static bool IsBaseHelper(string transformName)
        {
            if (string.IsNullOrEmpty(transformName))
            {
                return false;
            }

            string normalizedName = transformName.ToLowerInvariant();
            string compactName = normalizedName
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(".", string.Empty)
                .Replace(" ", string.Empty);
            if (!compactName.Contains("thumb0"))
            {
                return false;
            }

            return !normalizedName.Contains("thumb1") &&
                !normalizedName.Contains("thumb2") &&
                !normalizedName.Contains("thumb3") &&
                !normalizedName.Contains("proximal") &&
                !normalizedName.Contains("intermediate") &&
                !normalizedName.Contains("distal") &&
                !normalizedName.Contains("thumbtip");
        }

        internal static bool IsActiveBaseSource(string transformName)
        {
            if (string.IsNullOrEmpty(transformName))
            {
                return false;
            }

            string normalizedName = transformName.ToLowerInvariant();
            return normalizedName.Contains("thumb0m") &&
                !normalizedName.Contains("ghost") &&
                !normalizedName.Contains("thumb1") &&
                !normalizedName.Contains("thumb2") &&
                !normalizedName.Contains("thumbtip");
        }
    }
}
