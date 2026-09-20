#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
        private Transform _leftFoot;
        private Transform _leftToes;
        private Transform _rightFoot;
        private Transform _rightToes;
        private HumanoidFootRotationBinding _leftFootRotation;
        private HumanoidFootRotationBinding _rightFootRotation;
        private Vector2 _footForwardLengths;

        internal bool HasFootRotationReference => _leftFootRotation != null && _rightFootRotation != null;
        internal Quaternion InitialRootRotation { get; private set; } = Quaternion.identity;

        internal bool IsInitialized =>
            _referenceInstance != null &&
            _referenceAnimator != null &&
            _poseHandler != null &&
            _animationPlayer.IsInitialized;

        internal float SourceHumanScale =>
            IsInitialized ? _referenceAnimator.humanScale : 0f;

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
            _leftFoot = _referenceAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _leftToes = _referenceAnimator.GetBoneTransform(HumanBodyBones.LeftToes);
            _rightFoot = _referenceAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            _rightToes = _referenceAnimator.GetBoneTransform(HumanBodyBones.RightToes);
            if (_leftFoot == null || _rightFoot == null)
            {
                throw new InvalidOperationException(
                    "Native Humanoid 기준 모델에 양발 본이 필요합니다.");
            }
            InitialRootRotation = _referenceAnimator.transform.rotation;
            if (!TryInitializeAvatarFootReference(_referenceAnimator.avatar.humanDescription.skeleton))
                TryInitializeFootReference();
            _animationPlayer.Initialize(_referenceAnimator, clip);
            _poseHandler = new HumanPoseHandler(
                _referenceAnimator.avatar,
                _referenceAnimator.transform);
        }

        private bool TryInitializeAvatarFootReference(SkeletonBone[] skeleton)
        {
            Transform root = _referenceAnimator.transform;
            if (skeleton == null || skeleton.Length == 0 || _leftToes == null || _rightToes == null)
                return false;

            var bones = new List<Transform>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform toes in new[] { _leftToes, _rightToes })
            {
                if (!toes.IsChildOf(root)) return false;
                for (Transform bone = toes; bone != root; bone = bone.parent)
                {
                    if (bones.Contains(bone)) continue;
                    if (!names.Add(bone.name)) return false;
                    bones.Add(bone);
                }
            }
            foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
                if (bone != root && names.Contains(bone.name) && !bones.Contains(bone)) return false;

            var reference = new Dictionary<string, SkeletonBone>(StringComparer.Ordinal);
            foreach (SkeletonBone pose in skeleton)
            {
                if (pose.name == null || !names.Contains(pose.name)) continue;
                if (reference.ContainsKey(pose.name) || !IsFinite(pose.position) || !IsFinite(pose.rotation) ||
                    !IsFinite(pose.scale) || pose.scale.x <= 0f || pose.scale.y <= 0f || pose.scale.z <= 0f ||
                    Mathf.Abs(Quaternion.Dot(pose.rotation, pose.rotation) - 1f) > 0.0001f)
                    return false;
                reference.Add(pose.name, pose);
            }
            if (reference.Count != bones.Count) return false;

            var original = new SkeletonBone[bones.Count];
            for (int i = 0; i < bones.Count; i++)
                original[i] = new SkeletonBone { position = bones[i].localPosition,
                    rotation = bones[i].localRotation, scale = bones[i].localScale };
            try
            {
                // 첫 동작의 기울기를 중립으로 삼지 않도록 Avatar 기준에서 발 축과 길이를 함께 취득함.
                foreach (Transform bone in bones)
                {
                    SkeletonBone pose = reference[bone.name];
                    bone.localPosition = pose.position;
                    bone.localRotation = pose.rotation;
                    bone.localScale = pose.scale;
                }
                return TryInitializeFootReference();
            }
            finally
            {
                // 재생기와 HumanPoseHandler가 임시 자세를 초기 상태로 기억하지 않도록 먼저 복원함.
                for (int i = 0; i < bones.Count; i++)
                {
                    bones[i].localPosition = original[i].position;
                    bones[i].localRotation = original[i].rotation;
                    bones[i].localScale = original[i].scale;
                }
            }
        }

        private bool TryInitializeFootReference()
        {
            Vector3 up = InitialRootRotation * Vector3.up;
            if (!HumanoidFootRotationBinding.TryCreate(_leftFoot, _leftToes, up, out var left) ||
                !HumanoidFootRotationBinding.TryCreate(_rightFoot, _rightToes, up, out var right))
                return false;
            _leftFootRotation = left;
            _rightFootRotation = right;
            _footForwardLengths = new Vector2(
                Vector3.ProjectOnPlane(_leftToes.position - _leftFoot.position, up).magnitude,
                Vector3.ProjectOnPlane(_rightToes.position - _rightFoot.position, up).magnitude);
            return true;
        }

        internal bool TryEvaluateFootFramesAt(float timeSeconds,
            out Quaternion leftFrame, out Quaternion rightFrame)
        {
            leftFrame = rightFrame = Quaternion.identity;
            if (!IsInitialized || !HasFootRotationReference)
                return false;
            _animationPlayer.EvaluateAt(timeSeconds);
            return _leftFootRotation.TryCaptureWorldFrame(out leftFrame) &&
                _rightFootRotation.TryCaptureWorldFrame(out rightFrame);
        }

        internal bool TryCaptureFootSupportReference(out Vector2 heights,
            out Quaternion leftFrame, out Quaternion rightFrame)
        {
            heights = Vector2.zero;
            leftFrame = rightFrame = Quaternion.identity;
            if (!IsInitialized || !HasFootRotationReference ||
                !_leftFootRotation.TryCaptureWorldFrame(out leftFrame) ||
                !_rightFootRotation.TryCaptureWorldFrame(out rightFrame))
                return false;
            // 재평가 없이 같은 원본 표본을 읽고, 기준 평면은 초기 자세에 고정함.
            Vector3 referenceUp = InitialRootRotation * Vector3.up;
            heights = new Vector2(
                Vector3.Dot(leftFrame * Vector3.forward, referenceUp) * _footForwardLengths.x,
                Vector3.Dot(rightFrame * Vector3.forward, referenceUp) * _footForwardLengths.y);
            return IsFinite(heights.x) && IsFinite(heights.y);
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

        internal bool TryEvaluateFootContactPointsAt(
            float timeSeconds,
            out Vector3 left,
            out Vector3 right)
        {
            left = Vector3.zero;
            right = Vector3.zero;
            if (!IsInitialized || _leftFoot == null || _rightFoot == null)
            {
                return false;
            }

            if (!TryEvaluateFootSupportPointsAt(
                    timeSeconds,
                    out Vector3 leftFoot,
                    out Vector3 leftToes,
                    out Vector3 rightFoot,
                    out Vector3 rightToes))
            {
                return false;
            }

            left = (leftFoot + leftToes) * 0.5f;
            right = (rightFoot + rightToes) * 0.5f;
            return IsFinite(left) && IsFinite(right);
        }

        internal bool TryEvaluateFootSupportPointsAt(
            float timeSeconds,
            out Vector3 leftFoot,
            out Vector3 leftToes,
            out Vector3 rightFoot,
            out Vector3 rightToes)
        {
            leftFoot = Vector3.zero;
            leftToes = Vector3.zero;
            rightFoot = Vector3.zero;
            rightToes = Vector3.zero;
            if (!IsInitialized || _leftFoot == null || _rightFoot == null)
            {
                return false;
            }

            _animationPlayer.EvaluateAt(timeSeconds);
            leftFoot = _leftFoot.position;
            leftToes = _leftToes == null ? leftFoot : _leftToes.position;
            rightFoot = _rightFoot.position;
            rightToes = _rightToes == null ? rightFoot : _rightToes.position;
            return IsFinite(leftFoot) && IsFinite(leftToes) &&
                IsFinite(rightFoot) && IsFinite(rightToes);
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
            _leftFoot = null;
            _leftToes = null;
            _rightFoot = null;
            _rightToes = null;
            _leftFootRotation = _rightFootRotation = null;
            _footForwardLengths = Vector2.zero;
            InitialRootRotation = Quaternion.identity;

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
