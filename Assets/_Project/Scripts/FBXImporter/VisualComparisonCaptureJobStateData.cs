using System;

namespace Fbx2Vmd.FBXImporter
{
    [Serializable]
    internal class VisualComparisonCaptureJobStateData
    {
        public int mode;
        public string scenePath;
        public string sceneName;
        public string displayName;
        public string manualTargetNameToken;
    }
}
