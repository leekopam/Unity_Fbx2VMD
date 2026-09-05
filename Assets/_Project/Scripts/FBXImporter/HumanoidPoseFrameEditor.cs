using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 현재 Humanoid 자세를 읽고 선택 프레임 보정을 대상 Animator에 적용함.
    /// </summary>
    internal sealed class HumanoidPoseFrameEditor : IDisposable
    {
        private HumanPoseHandler _poseHandler;
        private HumanPose _workingPose;
        private Animator _targetAnimator;
        private Transform[] _humanoidBones = Array.Empty<Transform>();
        private Vector3[] _boneLocalPositions = Array.Empty<Vector3>();
        private Vector3[] _boneLocalScales = Array.Empty<Vector3>();

        internal bool IsInitialized => _poseHandler != null;

        internal void Initialize(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                throw new ArgumentNullException(nameof(targetAnimator));
            }

            Avatar avatar = targetAnimator.avatar;
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                throw new InvalidOperationException(
                    "Humanoid 자세 수정에는 유효한 Humanoid Avatar가 필요합니다.");
            }

            Dispose();
            _targetAnimator = targetAnimator;
            _poseHandler = new HumanPoseHandler(avatar, targetAnimator.transform);
            _workingPose = new HumanPose();
            CacheHumanoidBones(targetAnimator);
        }

        internal bool TryCapture(out HumanPose pose)
        {
            pose = default;
            if (!IsInitialized)
            {
                return false;
            }

            _poseHandler.GetHumanPose(ref _workingPose);
            if (!IsFinite(_workingPose))
            {
                return false;
            }

            pose = new HumanPose
            {
                bodyPosition = _workingPose.bodyPosition,
                bodyRotation = _workingPose.bodyRotation,
                muscles = (float[])_workingPose.muscles.Clone()
            };
            return true;
        }

        internal bool TryApply(
            HumanoidPoseCorrectionDocument document,
            int frameIndex)
        {
            if (document == null || !TryCapture(out HumanPose pose))
            {
                return false;
            }

            if (!document.TryApplyMuscleDeltas(frameIndex, pose.muscles))
            {
                return false;
            }

            ApplyPosePreservingGeometry(ref pose);
            return true;
        }

        internal bool TryApplyArmDirectionReference(
            HumanoidArmDirectionReference reference,
            out float maxDirectionErrorDegrees)
        {
            maxDirectionErrorDegrees = 0f;
            if (!TryGetArmBones(
                    out Transform leftUpperArm,
                    out Transform leftLowerArm,
                    out Transform leftHand,
                    out Transform rightUpperArm,
                    out Transform rightLowerArm,
                    out Transform rightHand))
            {
                return false;
            }

            Quaternion leftUpperRotation = leftUpperArm.localRotation;
            Quaternion leftLowerRotation = leftLowerArm.localRotation;
            Quaternion rightUpperRotation = rightUpperArm.localRotation;
            Quaternion rightLowerRotation = rightLowerArm.localRotation;

            if (TryApplyArmSegment(
                    leftUpperArm,
                    leftLowerArm,
                    reference.LeftUpperArm,
                    ref maxDirectionErrorDegrees) &&
                TryApplyArmSegment(
                    leftLowerArm,
                    leftHand,
                    reference.LeftForearm,
                    ref maxDirectionErrorDegrees) &&
                TryApplyArmSegment(
                    rightUpperArm,
                    rightLowerArm,
                    reference.RightUpperArm,
                    ref maxDirectionErrorDegrees) &&
                TryApplyArmSegment(
                    rightLowerArm,
                    rightHand,
                    reference.RightForearm,
                    ref maxDirectionErrorDegrees))
            {
                return true;
            }

            // 일부 구간만 적용된 자세를 남기지 않도록 실패 시 팔 회전을 원복함.
            leftUpperArm.localRotation = leftUpperRotation;
            leftLowerArm.localRotation = leftLowerRotation;
            rightUpperArm.localRotation = rightUpperRotation;
            rightLowerArm.localRotation = rightLowerRotation;
            maxDirectionErrorDegrees = 0f;
            return false;
        }

        public void Dispose()
        {
            _poseHandler?.Dispose();
            _poseHandler = null;
            _workingPose = default;
            _targetAnimator = null;
            _humanoidBones = Array.Empty<Transform>();
            _boneLocalPositions = Array.Empty<Vector3>();
            _boneLocalScales = Array.Empty<Vector3>();
        }

        private bool TryGetArmBones(
            out Transform leftUpperArm,
            out Transform leftLowerArm,
            out Transform leftHand,
            out Transform rightUpperArm,
            out Transform rightLowerArm,
            out Transform rightHand)
        {
            leftUpperArm = _targetAnimator?.GetBoneTransform(
                HumanBodyBones.LeftUpperArm);
            leftLowerArm = _targetAnimator?.GetBoneTransform(
                HumanBodyBones.LeftLowerArm);
            leftHand = _targetAnimator?.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpperArm = _targetAnimator?.GetBoneTransform(
                HumanBodyBones.RightUpperArm);
            rightLowerArm = _targetAnimator?.GetBoneTransform(
                HumanBodyBones.RightLowerArm);
            rightHand = _targetAnimator?.GetBoneTransform(HumanBodyBones.RightHand);
            return IsInitialized &&
                leftUpperArm != null &&
                leftLowerArm != null &&
                leftHand != null &&
                rightUpperArm != null &&
                rightLowerArm != null &&
                rightHand != null;
        }

        private bool TryApplyArmSegment(
            Transform start,
            Transform end,
            Vector3 referenceDirection,
            ref float maxDirectionErrorDegrees)
        {
            Vector3 targetDirection = _targetAnimator.transform.InverseTransformDirection(
                end.position - start.position);
            if (!HumanoidArmSwingCorrectionCalculator.TryCalculate(
                    targetDirection,
                    referenceDirection,
                    out Quaternion rootSpaceCorrection,
                    out float errorDegrees))
            {
                return false;
            }

            Quaternion rootRotation = _targetAnimator.transform.rotation;
            Quaternion worldCorrection =
                rootRotation * rootSpaceCorrection * Quaternion.Inverse(rootRotation);
            start.rotation = worldCorrection * start.rotation;
            maxDirectionErrorDegrees = Mathf.Max(
                maxDirectionErrorDegrees,
                errorDegrees);
            return true;
        }

        private void CacheHumanoidBones(Animator targetAnimator)
        {
            int boneCapacity = (int)HumanBodyBones.LastBone;
            Transform[] bones = new Transform[boneCapacity];
            int boneCount = 0;

            for (int index = 0; index < boneCapacity; index++)
            {
                Transform bone = targetAnimator.GetBoneTransform((HumanBodyBones)index);
                if (bone != null)
                {
                    bones[boneCount++] = bone;
                }
            }

            _humanoidBones = new Transform[boneCount];
            Array.Copy(bones, _humanoidBones, boneCount);
            _boneLocalPositions = new Vector3[boneCount];
            _boneLocalScales = new Vector3[boneCount];
        }

        private void CaptureBoneGeometry()
        {
            for (int index = 0; index < _humanoidBones.Length; index++)
            {
                Transform bone = _humanoidBones[index];
                _boneLocalPositions[index] = bone.localPosition;
                _boneLocalScales[index] = bone.localScale;
            }
        }

        private void RestoreBoneGeometry()
        {
            for (int index = 0; index < _humanoidBones.Length; index++)
            {
                Transform bone = _humanoidBones[index];
                bone.localPosition = _boneLocalPositions[index];
                bone.localScale = _boneLocalScales[index];
            }
        }

        private void ApplyPosePreservingGeometry(ref HumanPose pose)
        {
            CaptureBoneGeometry();
            try
            {
                _poseHandler.SetHumanPose(ref pose);
            }
            finally
            {
                RestoreBoneGeometry();
            }
        }

        private static bool IsFinite(HumanPose pose)
        {
            if (!IsFinite(pose.bodyPosition) ||
                !IsFinite(pose.bodyRotation) ||
                pose.muscles == null ||
                pose.muscles.Length < HumanTrait.MuscleCount)
            {
                return false;
            }

            for (int index = 0; index < HumanTrait.MuscleCount; index++)
            {
                if (!IsFinite(pose.muscles[index]))
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
