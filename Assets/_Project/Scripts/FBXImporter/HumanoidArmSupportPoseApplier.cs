using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 명시적으로 연결된 상완 보조 본을 bind 기준으로 추종하고 세션 종료 시 복원함.
    /// </summary>
    internal sealed class HumanoidArmSupportPoseApplier : IDisposable
    {
        private const float BindTolerance = 0.0001f;
        private readonly List<Binding> _bindings = new List<Binding>(2);

        private readonly struct Binding
        {
            internal readonly Transform Driver;
            internal readonly Transform Support;
            internal readonly Quaternion BindOffset;
            internal readonly Quaternion OriginalRotation;

            internal Binding(Transform driver, Transform support, Quaternion bindOffset)
            {
                Driver = driver;
                Support = support;
                BindOffset = bindOffset;
                OriginalRotation = support.localRotation;
            }
        }

        internal void Initialize(Animator animator, AnimationClip clip)
        {
            Dispose();
            if (animator == null || animator.avatar == null || !animator.isHuman)
                return;

            HumanoidArmSleeveAnchorGuard guard = animator.GetComponent<HumanoidArmSleeveAnchorGuard>();
            if (guard != null && guard.isActiveAndEnabled && guard.enableSleeveAnchor)
                return;

            SkinnedMeshRenderer[] skins = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm })
            {
                Transform support = HumanoidArmSleeveAnchorGuard.FindLegacyArmAnchor(animator, bone);
                if (support == null || HasHumanDescendant(animator, support) ||
                    HasAuthoredTransformCurve(animator, clip, support))
                    continue;

                TryAddBinding(animator.GetBoneTransform(bone), support, skins);
            }
        }

        internal bool TryAddBinding(Transform driver, Transform support, SkinnedMeshRenderer[] skins)
        {
            if (driver == null || support == null || driver == support || driver.parent == null ||
                driver.parent != support.parent || skins == null ||
                _bindings.Exists(binding => binding.Support == support))
                return false;

            if (!TryFindBindOffset(driver, support, skins, out Quaternion offset))
                return false;

            _bindings.Add(new Binding(driver, support, offset));
            return true;
        }

        internal void Apply()
        {
            foreach (Binding binding in _bindings)
            {
                if (binding.Driver == null || binding.Support == null ||
                    binding.Driver.parent != binding.Support.parent)
                    continue;

                // 현재 자세를 기준으로 재보정하지 않아 seek/정지 프레임의 누적 오차를 막음.
                binding.Support.localRotation = binding.Driver.localRotation * binding.BindOffset;
            }
        }

        public void Dispose()
        {
            foreach (Binding binding in _bindings)
            {
                if (binding.Support != null)
                    binding.Support.localRotation = binding.OriginalRotation;
            }
            _bindings.Clear();
        }

        private static bool TryFindBindOffset(
            Transform driver, Transform support, SkinnedMeshRenderer[] skins, out Quaternion offset)
        {
            offset = Quaternion.identity;
            bool hasOffset = false;
            bool hasWeight = false;
            foreach (SkinnedMeshRenderer skin in skins)
            {
                if (skin == null || skin.sharedMesh == null)
                    continue;

                Transform[] bones = skin.bones;
                int supportIndex = Array.IndexOf(bones, support);
                if (supportIndex < 0)
                    continue;

                int driverIndex = Array.IndexOf(bones, driver);
                Matrix4x4[] bindposes = skin.sharedMesh.bindposes;
                if (driverIndex < 0 || driverIndex >= bindposes.Length || supportIndex >= bindposes.Length)
                    return false;

                Matrix4x4 relative = bindposes[driverIndex] * bindposes[supportIndex].inverse;
                if (!IsRigidRotation(relative))
                    return false;

                Quaternion candidate = relative.rotation;
                if (hasOffset && Quaternion.Angle(offset, candidate) > 0.05f)
                    return false;

                offset = candidate;
                hasOffset = true;
                var weights = skin.sharedMesh.GetAllBoneWeights();
                for (int index = 0; index < weights.Length && !hasWeight; index++)
                    hasWeight = weights[index].boneIndex == supportIndex && weights[index].weight > 0f;
            }
            return hasOffset && hasWeight;
        }

        private static bool IsRigidRotation(Matrix4x4 matrix)
        {
            for (int index = 0; index < 16; index++)
            {
                if (float.IsNaN(matrix[index]) || float.IsInfinity(matrix[index]))
                    return false;
            }

            Vector3 x = matrix.GetColumn(0);
            Vector3 y = matrix.GetColumn(1);
            Vector3 z = matrix.GetColumn(2);
            Vector3 translation = matrix.GetColumn(3);
            return translation.sqrMagnitude < BindTolerance * BindTolerance &&
                Mathf.Abs(x.sqrMagnitude - 1f) < BindTolerance &&
                Mathf.Abs(y.sqrMagnitude - 1f) < BindTolerance &&
                Mathf.Abs(z.sqrMagnitude - 1f) < BindTolerance &&
                Mathf.Abs(Vector3.Dot(x, y)) < BindTolerance &&
                Mathf.Abs(Vector3.Dot(x, z)) < BindTolerance &&
                Mathf.Abs(Vector3.Dot(y, z)) < BindTolerance &&
                Vector3.Dot(Vector3.Cross(x, y), z) > 1f - BindTolerance &&
                Mathf.Abs(matrix.m30) < BindTolerance && Mathf.Abs(matrix.m31) < BindTolerance &&
                Mathf.Abs(matrix.m32) < BindTolerance && Mathf.Abs(matrix.m33 - 1f) < BindTolerance;
        }

        private static bool HasHumanDescendant(Animator animator, Transform support)
        {
            for (int index = 0; index < (int)HumanBodyBones.LastBone; index++)
            {
                Transform human = animator.GetBoneTransform((HumanBodyBones)index);
                if (human != null && (human == support || human.IsChildOf(support)))
                    return true;
            }
            return false;
        }

        private static bool HasAuthoredTransformCurve(Animator animator, AnimationClip clip, Transform support)
        {
#if UNITY_EDITOR
            if (clip == null)
                return false;

            string path = UnityEditor.AnimationUtility.CalculateTransformPath(support, animator.transform);
            foreach (UnityEditor.EditorCurveBinding binding in UnityEditor.AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type == typeof(Transform) && binding.path == path)
                    return true;
            }
#endif
            return false;
        }
    }
}
