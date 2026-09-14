#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 재생 준비 전 기준 모델의 복제본에서 허벅지 로컬 굽힘 평면을 취득함.
    /// 모델·Avatar가 바뀔 때 다시 취득하고 같은 재생 세션에서는 결과를 보관함.
    /// </summary>
    internal static class EditorHumanoidLegBendCalibration
    {
        internal static bool TryCapture(Animator source, out Vector3 leftNormal, out Vector3 rightNormal)
        {
            leftNormal = Vector3.zero;
            rightNormal = Vector3.zero;
            if (source == null || source.avatar == null || !source.avatar.isValid || !source.avatar.isHuman)
                return false;

            int leftStretch = Array.IndexOf(HumanTrait.MuscleName, "Left Lower Leg Stretch");
            int rightStretch = Array.IndexOf(HumanTrait.MuscleName, "Right Lower Leg Stretch");
            if (leftStretch < 0 || rightStretch < 0)
                return false;

            GameObject temporaryRoot = null;
            try
            {
                // 비활성 부모 아래 복제하여 원본 변경과 복제된 동작 스크립트 실행을 피함.
                temporaryRoot = new GameObject("무릎 굽힘 교정") { hideFlags = HideFlags.HideAndDontSave };
                temporaryRoot.SetActive(false);
                GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, temporaryRoot.transform, false);
                Animator animator = clone.GetComponent<Animator>();
                using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
                {
                    HumanPose pose = new HumanPose();
                    handler.GetHumanPose(ref pose);
                    pose.muscles[leftStretch] = 0f;
                    pose.muscles[rightStretch] = 0f;
                    handler.SetHumanPose(ref pose);
                    if (!TryReadNormal(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                        HumanBodyBones.LeftFoot, out Vector3 left) ||
                        !TryReadNormal(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                        HumanBodyBones.RightFoot, out Vector3 right))
                        return false;

                    leftNormal = left;
                    rightNormal = right;
                    return true;
                }
            }
            catch (UnityException)
            {
                return false;
            }
            finally
            {
                if (temporaryRoot != null)
                    UnityEngine.Object.DestroyImmediate(temporaryRoot);
            }
        }

        private static bool TryReadNormal(Animator animator, HumanBodyBones upperBone,
            HumanBodyBones lowerBone, HumanBodyBones footBone, out Vector3 normal)
        {
            normal = Vector3.zero;
            Transform upper = animator.GetBoneTransform(upperBone);
            Transform lower = animator.GetBoneTransform(lowerBone);
            Transform foot = animator.GetBoneTransform(footBone);
            if (upper == null || lower == null || foot == null ||
                !lower.IsChildOf(upper) || !foot.IsChildOf(lower))
                return false;

            Vector3 cross = Vector3.Cross((lower.position - upper.position).normalized,
                (foot.position - lower.position).normalized);
            float squared = cross.sqrMagnitude;
            // 사용자 Avatar의 좁은 근육 범위가 교정 뒤에도 거의 직선인 자세를 만들면 채택하지 않음.
            if (float.IsNaN(squared) || float.IsInfinity(squared) || squared < 0.25f)
                return false;

            normal = Quaternion.Inverse(upper.rotation) * (cross / Mathf.Sqrt(squared));
            return true;
        }
    }
}
#endif
