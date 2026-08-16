using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public sealed class RetargetingContext
    {
        public GameObject GhostRoot { get; }
        public GameObject TargetRoot { get; }
        public IReadOnlyDictionary<string, string> Mapping { get; }
        public AnimationClip Clip { get; }

        public RetargetingContext(
            GameObject ghostRoot,
            GameObject targetRoot,
            IDictionary<string, string> mapping,
            AnimationClip clip)
        {
            GhostRoot = ghostRoot;
            TargetRoot = targetRoot;
            Mapping = new Dictionary<string, string>(mapping ?? new Dictionary<string, string>());
            Clip = clip;
        }
    }
}
