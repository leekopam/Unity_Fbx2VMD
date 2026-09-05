#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 기준 Humanoid 모델에서 같은 시간의 원본 자세를 샘플링함.
    /// </summary>
    internal sealed class EditorHumanoidPoseReferencePlayer : IDisposable
    {
        private readonly NativeHumanoidAnimationPlayer _animationPlayer =
            new NativeHumanoidAnimationPlayer();

        private GameObject _referenceInstance;
        private Animator _referenceAnimator;
        private HumanPoseHandler _poseHandler;

        internal bool IsInitialized =>
            _referenceInstance != null &&
            _referenceAnimator != null &&
            _poseHandler != null &&
            _animationPlayer.IsInitialized;

        internal void Initialize(Animator targetAnimator, AnimationClip clip)
        {
            if (targetAnimator == null)
            {
                throw new ArgumentNullException(nameof(targetAnimator));
            }

            GameObject referenceSource =
                PrefabUtility.GetCorrespondingObjectFromOriginalSource(targetAnimator.gameObject) ??
                targetAnimator.gameObject;
            InitializeFromSourceModel(referenceSource, clip);
        }

        internal void InitializeFromSourceModel(
            GameObject referenceSource,
            AnimationClip clip)
        {
            if (referenceSource == null)
            {
                throw new ArgumentNullException(nameof(referenceSource));
            }

            Dispose();

            _referenceInstance = UnityEngine.Object.Instantiate(referenceSource);
            _referenceInstance.name = $"EditorHumanoidPoseReference_{referenceSource.name}";
            _referenceInstance.hideFlags = HideFlags.HideAndDontSave;
            _referenceInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            DisableRuntimeComponents(_referenceInstance);
            _referenceInstance.SetActive(true);

            _referenceAnimator =
                _referenceInstance.GetComponent<Animator>() ??
                _referenceInstance.GetComponentInChildren<Animator>(true);
            if (_referenceAnimator == null)
            {
                throw new InvalidOperationException(
                    "Native Humanoid 기준 모델에 Animator가 없습니다.");
            }

            _referenceAnimator.runtimeAnimatorController = null;
            _referenceAnimator.enabled = true;
            _animationPlayer.Initialize(_referenceAnimator, clip);
            _poseHandler = new HumanPoseHandler(
                _referenceAnimator.avatar,
                _referenceAnimator.transform);
        }

        internal bool TryEvaluateAt(float timeSeconds, ref HumanPose pose)
        {
            if (!IsInitialized)
            {
                return false;
            }

            _animationPlayer.EvaluateAt(timeSeconds);
            _poseHandler.GetHumanPose(ref pose);
            return IsFinite(pose);
        }

        internal bool TryEvaluateArmDirectionsAt(
            float timeSeconds,
            out HumanoidArmDirectionReference reference)
        {
            reference = default;
            if (!IsInitialized)
            {
                return false;
            }

            _animationPlayer.EvaluateAt(timeSeconds);
            return TryCaptureArmDirectionReference(_referenceAnimator, out reference);
        }

        internal bool TryApplyHumanoidBoneLocalRotationsTo(Animator targetAnimator)
        {
            if (!IsInitialized ||
                targetAnimator == null ||
                targetAnimator.avatar == null ||
                !targetAnimator.avatar.isValid ||
                !targetAnimator.avatar.isHuman)
            {
                return false;
            }

            bool applied = false;
            for (HumanBodyBones bone = HumanBodyBones.Hips;
                 bone < HumanBodyBones.LastBone;
                 bone++)
            {
                Transform referenceBone = _referenceAnimator.GetBoneTransform(bone);
                Transform targetBone = targetAnimator.GetBoneTransform(bone);
                if (referenceBone == null ||
                    targetBone == null ||
                    !IsFinite(referenceBone.localRotation))
                {
                    continue;
                }

                targetBone.localRotation = referenceBone.localRotation;
                applied = true;
            }

            return applied;
        }

        public void Dispose()
        {
            _animationPlayer.Dispose();
            _poseHandler?.Dispose();
            _poseHandler = null;
            _referenceAnimator = null;

            if (_referenceInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(_referenceInstance);
                _referenceInstance = null;
            }
        }

        private static void DisableRuntimeComponents(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (Animation animation in root.GetComponentsInChildren<Animation>(true))
            {
                animation.enabled = false;
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
        }

        private static bool TryCaptureArmDirectionReference(
            Animator animator,
            out HumanoidArmDirectionReference reference)
        {
            reference = default;
            if (!TryGetDirection(
                    animator,
                    HumanBodyBones.LeftUpperArm,
                    HumanBodyBones.LeftLowerArm,
                    out Vector3 leftUpperArm) ||
                !TryGetDirection(
                    animator,
                    HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.LeftHand,
                    out Vector3 leftForearm) ||
                !TryGetDirection(
                    animator,
                    HumanBodyBones.RightUpperArm,
                    HumanBodyBones.RightLowerArm,
                    out Vector3 rightUpperArm) ||
                !TryGetDirection(
                    animator,
                    HumanBodyBones.RightLowerArm,
                    HumanBodyBones.RightHand,
                    out Vector3 rightForearm))
            {
                return false;
            }

            reference = new HumanoidArmDirectionReference(
                leftUpperArm,
                leftForearm,
                rightUpperArm,
                rightForearm);
            return true;
        }

        private static bool TryGetDirection(
            Animator animator,
            HumanBodyBones startBone,
            HumanBodyBones endBone,
            out Vector3 direction)
        {
            direction = Vector3.zero;
            if (animator == null)
            {
                return false;
            }

            Transform start = animator.GetBoneTransform(startBone);
            Transform end = animator.GetBoneTransform(endBone);
            if (start == null || end == null)
            {
                return false;
            }

            direction = animator.transform.InverseTransformDirection(
                end.position - start.position);
            if (!IsFinite(direction) || direction.sqrMagnitude <= 0.000001f)
            {
                direction = Vector3.zero;
                return false;
            }

            direction.Normalize();
            return true;
        }

        private static bool IsFinite(HumanPose pose)
        {
            if (!IsFinite(pose.bodyPosition) || !IsFinite(pose.bodyRotation) || pose.muscles == null)
            {
                return false;
            }

            for (int i = 0; i < pose.muscles.Length; i++)
            {
                if (!IsFinite(pose.muscles[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
#endif
