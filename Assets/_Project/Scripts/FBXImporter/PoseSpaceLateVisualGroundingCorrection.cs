using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(29950)]
    public class PoseSpaceLateVisualGroundingCorrection : MonoBehaviour
    {
        [SerializeField] private PoseSpaceRetargeter retargeter;

        public void Initialize(PoseSpaceRetargeter owner)
        {
            retargeter = owner;
            enabled = retargeter != null;
        }

        private void Awake()
        {
            if (retargeter == null)
            {
                retargeter = GetComponent<PoseSpaceRetargeter>();
            }
        }

        private void LateUpdate()
        {
            if (retargeter == null)
            {
                return;
            }

            retargeter.ApplyLateVisualGroundingCorrection();
        }
    }
}
