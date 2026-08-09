using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public partial class PoseSpaceRetargeter
    {
#if UNITY_EDITOR
        private static Vector3 ReadAnimatorBoneWorldPosition(Animator animator, HumanBodyBones bone)
        {
            if (animator == null)
            {
                return BuildNaNVector3();
            }

            Transform targetBone = animator.GetBoneTransform(bone);
            return targetBone != null ? targetBone.position : BuildNaNVector3();
        }

        private static Vector3 ReadAnimatorBoneLocalPosition(Animator animator, HumanBodyBones bone)
        {
            if (animator == null)
            {
                return BuildNaNVector3();
            }

            Transform targetBone = animator.GetBoneTransform(bone);
            if (targetBone == null)
            {
                return BuildNaNVector3();
            }

            Vector3 localPosition = targetBone.localPosition;
            return IsFinite(localPosition) ? localPosition : BuildNaNVector3();
        }

        private static Vector3 ReadAnimatorRootWorldPosition(Animator animator)
        {
            if (animator == null)
            {
                return BuildNaNVector3();
            }

            Vector3 position = animator.transform.position;
            return IsFinite(position) ? position : BuildNaNVector3();
        }

        private static Quaternion ReadAnimatorRootWorldRotation(Animator animator)
        {
            if (animator == null)
            {
                return BuildNaNQuaternion();
            }

            Quaternion rotation = animator.transform.rotation;
            return IsFinite(rotation) ? rotation : BuildNaNQuaternion();
        }

        private static RetargetEndpointStageWorldPositions CaptureEndpointStageWorldPositions(Animator animator)
        {
            return new RetargetEndpointStageWorldPositions
            {
                LeftFoot = ReadAnimatorBoneWorldPosition(animator, HumanBodyBones.LeftFoot),
                LeftToes = ReadAnimatorBoneWorldPosition(animator, HumanBodyBones.LeftToes),
                RightFoot = ReadAnimatorBoneWorldPosition(animator, HumanBodyBones.RightFoot),
                RightToes = ReadAnimatorBoneWorldPosition(animator, HumanBodyBones.RightToes)
            };
        }

        private static Vector3 BuildNaNVector3()
        {
            return new Vector3(float.NaN, float.NaN, float.NaN);
        }

        private static Quaternion BuildNaNQuaternion()
        {
            return new Quaternion(float.NaN, float.NaN, float.NaN, float.NaN);
        }
#endif
    }
}
