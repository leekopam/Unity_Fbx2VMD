using UnityEngine;

namespace Fbx2Vmd.Retargeting
{
    /// <summary>
    /// 수동 Animator 참조 포즈 계산 유틸리티.
    /// PoseSpaceRetargeter에서 추출 (Phase B-5).
    /// ponytail: 순수 static — Animator 읽기만. HumanPose 변형은 PoseSpaceRetargeter가 담당.
    /// </summary>
    public static class ManualPoseReferenceProvider
    {
        /// <summary>Animator bone world position을 읽는다.</summary>
        public static Vector3 ReadAnimatorBoneWorldPosition(Animator animator, HumanBodyBones bone)
        {
            return animator.GetBoneTransform(bone).position;
        }

        /// <summary>Animator bone local position을 읽는다.</summary>
        public static Vector3 ReadAnimatorBoneLocalPosition(Animator animator, HumanBodyBones bone)
        {
            return animator.GetBoneTransform(bone).localPosition;
        }

        /// <summary>Animator root world position을 읽는다.</summary>
        public static Vector3 ReadAnimatorRootWorldPosition(Animator animator)
        {
            return animator.transform.position;
        }
    }
}
