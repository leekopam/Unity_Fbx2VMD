using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.Settings
{
    public static class GraphicMaterialShaderTargetCollector
    {
        public static IEnumerable<Material> Enumerate(
            IEnumerable<Material> explicitTargets,
            IEnumerable<GameObject> sourceRoots)
        {
            if (explicitTargets != null)
            {
                foreach (Material material in explicitTargets)
                {
                    yield return material;
                }
            }

            if (sourceRoots == null)
            {
                yield break;
            }

            foreach (GameObject root in sourceRoots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        yield return material;
                    }
                }
            }
        }
    }
}
