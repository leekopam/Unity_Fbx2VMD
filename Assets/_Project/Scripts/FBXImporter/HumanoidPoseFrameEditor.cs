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

            CaptureBoneGeometry();
            try
            {
                _poseHandler.SetHumanPose(ref pose);
            }
            finally
            {
                RestoreBoneGeometry();
            }

            return true;
        }

        public void Dispose()
        {
            _poseHandler?.Dispose();
            _poseHandler = null;
            _workingPose = default;
            _humanoidBones = Array.Empty<Transform>();
            _boneLocalPositions = Array.Empty<Vector3>();
            _boneLocalScales = Array.Empty<Vector3>();
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
