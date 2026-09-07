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
        private int[] _humanoidBoneIndexes = Array.Empty<int>();
        private Quaternion[] _boneLocalRotations = Array.Empty<Quaternion>();
        private Vector3[] _boneLocalPositions = Array.Empty<Vector3>();
        private Vector3[] _boneLocalScales = Array.Empty<Vector3>();
        private bool[] _affectedBoneRotations = Array.Empty<bool>();
        private float[] _originalMuscles = Array.Empty<float>();

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

            CaptureOriginalMuscles(pose.muscles);
            if (!document.TryApplyMuscleDeltas(frameIndex, pose.muscles))
            {
                return false;
            }

            ApplyPosePreservingGeometry(ref pose, _originalMuscles);
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
                    isForearm: false,
                    ref maxDirectionErrorDegrees) &&
                TryApplyArmSegment(
                    leftLowerArm,
                    leftHand,
                    reference.LeftForearm,
                    isForearm: true,
                    ref maxDirectionErrorDegrees) &&
                TryApplyArmSegment(
                    rightUpperArm,
                    rightLowerArm,
                    reference.RightUpperArm,
                    isForearm: false,
                    ref maxDirectionErrorDegrees) &&
                TryApplyArmSegment(
                    rightLowerArm,
                    rightHand,
                    reference.RightForearm,
                    isForearm: true,
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
            _humanoidBoneIndexes = Array.Empty<int>();
            _boneLocalRotations = Array.Empty<Quaternion>();
            _boneLocalPositions = Array.Empty<Vector3>();
            _boneLocalScales = Array.Empty<Vector3>();
            _affectedBoneRotations = Array.Empty<bool>();
            _originalMuscles = Array.Empty<float>();
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
            bool isForearm,
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

            if (!isForearm ||
                HumanoidArmDirectionCorrectionPolicy.ShouldApplyForearmCorrection(
                    errorDegrees))
            {
                Quaternion rootRotation = _targetAnimator.transform.rotation;
                Quaternion worldCorrection =
                    rootRotation * rootSpaceCorrection * Quaternion.Inverse(rootRotation);
                start.rotation = worldCorrection * start.rotation;
            }

            maxDirectionErrorDegrees = Mathf.Max(
                maxDirectionErrorDegrees,
                errorDegrees);
            return true;
        }

        private void CacheHumanoidBones(Animator targetAnimator)
        {
            int boneCapacity = (int)HumanBodyBones.LastBone;
            Transform[] bones = new Transform[boneCapacity];
            int[] boneIndexes = new int[boneCapacity];
            int boneCount = 0;

            for (int index = 0; index < boneCapacity; index++)
            {
                Transform bone = targetAnimator.GetBoneTransform((HumanBodyBones)index);
                if (bone != null)
                {
                    bones[boneCount] = bone;
                    boneIndexes[boneCount] = index;
                    boneCount++;
                }
            }

            _humanoidBones = new Transform[boneCount];
            Array.Copy(bones, _humanoidBones, boneCount);
            _humanoidBoneIndexes = new int[boneCount];
            Array.Copy(boneIndexes, _humanoidBoneIndexes, boneCount);
            _boneLocalRotations = new Quaternion[boneCount];
            _boneLocalPositions = new Vector3[boneCount];
            _boneLocalScales = new Vector3[boneCount];
            _affectedBoneRotations = new bool[boneCapacity];
        }

        private void CaptureBoneGeometry()
        {
            for (int index = 0; index < _humanoidBones.Length; index++)
            {
                Transform bone = _humanoidBones[index];
                _boneLocalRotations[index] = bone.localRotation;
                _boneLocalPositions[index] = bone.localPosition;
                _boneLocalScales[index] = bone.localScale;
            }
        }

        private void CaptureOriginalMuscles(float[] muscles)
        {
            if (_originalMuscles.Length != muscles.Length)
            {
                _originalMuscles = new float[muscles.Length];
            }

            Array.Copy(muscles, _originalMuscles, muscles.Length);
        }

        private void RestoreBoneGeometry(bool preserveUnaffectedRotations)
        {
            for (int index = 0; index < _humanoidBones.Length; index++)
            {
                Transform bone = _humanoidBones[index];
                if (preserveUnaffectedRotations &&
                    !_affectedBoneRotations[_humanoidBoneIndexes[index]])
                {
                    bone.localRotation = _boneLocalRotations[index];
                }

                bone.localPosition = _boneLocalPositions[index];
                bone.localScale = _boneLocalScales[index];
            }
        }

        private void ApplyPosePreservingGeometry(
            ref HumanPose pose,
            float[] originalMuscles)
        {
            CaptureBoneGeometry();
            bool canPreserveUnaffectedRotations = TryMarkAffectedBoneRotations(
                originalMuscles,
                pose.muscles);
            try
            {
                _poseHandler.SetHumanPose(ref pose);
            }
            finally
            {
                RestoreBoneGeometry(canPreserveUnaffectedRotations);
            }
        }

        private bool TryMarkAffectedBoneRotations(
            float[] originalMuscles,
            float[] correctedMuscles)
        {
            if (originalMuscles == null ||
                correctedMuscles == null ||
                originalMuscles.Length < HumanTrait.MuscleCount ||
                correctedMuscles.Length < HumanTrait.MuscleCount ||
                _affectedBoneRotations.Length < (int)HumanBodyBones.LastBone)
            {
                return false;
            }

            Array.Clear(
                _affectedBoneRotations,
                0,
                _affectedBoneRotations.Length);
            for (int muscleIndex = 0;
                 muscleIndex < HumanTrait.MuscleCount;
                 muscleIndex++)
            {
                if (originalMuscles[muscleIndex] == correctedMuscles[muscleIndex])
                {
                    continue;
                }

                int boneIndex = HumanTrait.BoneFromMuscle(muscleIndex);
                if (boneIndex < 0 || boneIndex >= _affectedBoneRotations.Length)
                {
                    return false;
                }

                _affectedBoneRotations[boneIndex] = true;
            }

            return true;
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
