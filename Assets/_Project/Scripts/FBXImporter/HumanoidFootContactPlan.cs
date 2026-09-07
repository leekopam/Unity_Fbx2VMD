using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 프레임별 발 접촉 보정량을 대상 루트 공간에 보관함.
    /// </summary>
    internal sealed class HumanoidFootContactPlan
    {
        private readonly Vector3[] _leftRootSpaceCorrections;
        private readonly Vector3[] _rightRootSpaceCorrections;

        internal HumanoidFootContactPlan(
            Vector3[] leftRootSpaceCorrections,
            Vector3[] rightRootSpaceCorrections,
            float frameRate,
            int leftContactRunCount,
            int rightContactRunCount)
        {
            if (leftRootSpaceCorrections == null)
            {
                throw new ArgumentNullException(nameof(leftRootSpaceCorrections));
            }

            if (rightRootSpaceCorrections == null)
            {
                throw new ArgumentNullException(nameof(rightRootSpaceCorrections));
            }

            if (leftRootSpaceCorrections.Length == 0 ||
                leftRootSpaceCorrections.Length != rightRootSpaceCorrections.Length)
            {
                throw new ArgumentException("발 접촉 보정 배열 길이가 일치해야 합니다.");
            }

            if (!IsFinite(frameRate) || frameRate <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(frameRate));
            }

            _leftRootSpaceCorrections = leftRootSpaceCorrections;
            _rightRootSpaceCorrections = rightRootSpaceCorrections;
            FrameRate = frameRate;
            LeftContactRunCount = Mathf.Max(0, leftContactRunCount);
            RightContactRunCount = Mathf.Max(0, rightContactRunCount);
        }

        internal float FrameRate { get; }

        internal int FrameCount => _leftRootSpaceCorrections.Length;

        internal int LeftContactRunCount { get; }

        internal int RightContactRunCount { get; }

        internal bool TryEvaluate(
            float timeSeconds,
            out Vector3 leftRootSpaceCorrection,
            out Vector3 rightRootSpaceCorrection)
        {
            leftRootSpaceCorrection = Vector3.zero;
            rightRootSpaceCorrection = Vector3.zero;
            if (!IsFinite(timeSeconds) || timeSeconds < 0f)
            {
                return false;
            }

            float frame = Mathf.Clamp(
                timeSeconds * FrameRate,
                0f,
                FrameCount - 1);
            int firstFrame = Mathf.FloorToInt(frame);
            int secondFrame = Mathf.Min(firstFrame + 1, FrameCount - 1);
            float interpolation = frame - firstFrame;
            leftRootSpaceCorrection = Vector3.LerpUnclamped(
                _leftRootSpaceCorrections[firstFrame],
                _leftRootSpaceCorrections[secondFrame],
                interpolation);
            rightRootSpaceCorrection = Vector3.LerpUnclamped(
                _rightRootSpaceCorrections[firstFrame],
                _rightRootSpaceCorrections[secondFrame],
                interpolation);
            return IsFinite(leftRootSpaceCorrection) &&
                IsFinite(rightRootSpaceCorrection);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
