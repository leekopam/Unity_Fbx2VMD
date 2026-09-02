#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonManualRecorderSelector
    {
        internal static HumanoidSampleCode SelectAndActivate(string targetNameToken)
        {
            HumanoidSampleCode[] recorders = UnityEngine.Object.FindObjectsOfType<HumanoidSampleCode>(true);
            HumanoidSampleCode selected = null;
            foreach (HumanoidSampleCode recorder in recorders)
            {
                if (recorder == null)
                {
                    continue;
                }

                string hierarchyPath = BuildHierarchyPath(recorder.transform);
                if (hierarchyPath.IndexOf(targetNameToken, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    selected = recorder;
                    break;
                }
            }

            if (selected == null)
            {
                return null;
            }

            foreach (HumanoidSampleCode recorder in recorders)
            {
                if (recorder == null)
                {
                    continue;
                }

                recorder.gameObject.SetActive(ReferenceEquals(recorder, selected));
            }

            return selected;
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }
    }
}
#endif
