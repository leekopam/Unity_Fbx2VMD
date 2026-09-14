using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 한 프레임에서 샘플링한 양발 접촉점임.
    /// </summary>
    internal readonly struct HumanoidFootContactSample
    {
        internal HumanoidFootContactSample(Vector3 left, Vector3 right)
            : this(left, left, right, right)
        {
        }

        internal HumanoidFootContactSample(
            Vector3 leftFoot,
            Vector3 leftToes,
            Vector3 rightFoot,
            Vector3 rightToes)
            : this(leftFoot, leftToes, rightFoot, rightToes, null)
        {
        }

        internal HumanoidFootContactSample(
            Vector3 leftFoot, Vector3 leftToes, Vector3 rightFoot, Vector3 rightToes,
            Vector2? relativeFootHeights)
            : this(leftFoot, leftToes, rightFoot, rightToes, relativeFootHeights, null, null)
        {
        }

        internal HumanoidFootContactSample(
            Vector3 leftFoot, Vector3 leftToes, Vector3 rightFoot, Vector3 rightToes,
            Vector2? relativeFootHeights, Quaternion? leftFootFrame, Quaternion? rightFootFrame)
        {
            LeftFoot = leftFoot;
            LeftToes = leftToes;
            RightFoot = rightFoot;
            RightToes = rightToes;
            RelativeFootHeights = relativeFootHeights;
            LeftFootFrame = leftFootFrame;
            RightFootFrame = rightFootFrame;
        }

        // 기준축이 없는 표본과 평평한 발(0)을 구분함.
        internal Vector2? RelativeFootHeights { get; }

        internal Quaternion? LeftFootFrame { get; }

        internal Quaternion? RightFootFrame { get; }

        internal Vector3 LeftFoot { get; }

        internal Vector3 LeftToes { get; }

        internal Vector3 RightFoot { get; }

        internal Vector3 RightToes { get; }

        internal Vector3 Left => (LeftFoot + LeftToes) * 0.5f;

        internal Vector3 Right => (RightFoot + RightToes) * 0.5f;
    }

    /// <summary>
    /// 원본 접촉 구간과 대상 궤적에서 고정 길이 IK용 보정 계획을 계산함.
    /// </summary>
    internal static class HumanoidFootContactPlanner
    {
        private const float ContactHeightMarginPerHumanScale = 1f / 30f;
        private const float ContactSpeedLimitPerHumanScale = 1f / 6f;
        private const float MinimumContactDurationSeconds = 0.1f;
        private const float ReleaseDurationSeconds = 0.1f;
        private const float MinimumQuaternionMagnitude = 0.000001f;
        private const float MinimumSegmentLength = 0.000001f;
        private const float MinimumHeightCorrectionPerHumanScale = 1f / 1000f;
        private const float ToeHeightWeight = 2f;

        internal static HumanoidFootContactPlan Build(
            IReadOnlyList<HumanoidFootContactSample> sourceSamples,
            IReadOnlyList<HumanoidFootContactSample> targetSamples,
            float frameRate,
            float sourceHumanScale,
            Quaternion sourceToTargetRotation,
            float targetHumanScale = float.NaN)
        {
            ValidateSamples(sourceSamples, targetSamples, frameRate);

            float normalizedSourceHumanScale = IsFinite(sourceHumanScale) &&
                sourceHumanScale > 0f
                    ? sourceHumanScale
                    : 1f;
            float normalizedTargetHumanScale = IsFinite(targetHumanScale) &&
                targetHumanScale > 0f
                    ? targetHumanScale
                    : normalizedSourceHumanScale;
            Quaternion normalizedRotation = NormalizeRotation(sourceToTargetRotation);
            int minimumContactFrames = Mathf.Max(
                1,
                Mathf.CeilToInt(frameRate * MinimumContactDurationSeconds));
            int releaseFrames = Mathf.Max(
                1,
                Mathf.CeilToInt(frameRate * ReleaseDurationSeconds));

            var sourceLeft = new Vector3[sourceSamples.Count];
            var sourceRight = new Vector3[sourceSamples.Count];
            var targetLeft = new Vector3[targetSamples.Count];
            var targetRight = new Vector3[targetSamples.Count];
            var sourceLeftFeet = new Vector3[sourceSamples.Count];
            var sourceLeftToes = new Vector3[sourceSamples.Count];
            var sourceRightFeet = new Vector3[sourceSamples.Count];
            var sourceRightToes = new Vector3[sourceSamples.Count];
            var targetLeftFeet = new Vector3[targetSamples.Count];
            var targetLeftToes = new Vector3[targetSamples.Count];
            var targetRightFeet = new Vector3[targetSamples.Count];
            var targetRightToes = new Vector3[targetSamples.Count];
            for (int index = 0; index < sourceSamples.Count; index++)
            {
                HumanoidFootContactSample source = sourceSamples[index];
                HumanoidFootContactSample target = targetSamples[index];
                if (!IsFinite(source.Left) ||
                    !IsFinite(source.Right) ||
                    !IsFinite(target.Left) ||
                    !IsFinite(target.Right) ||
                    !IsFinite(source.LeftFoot) ||
                    !IsFinite(source.LeftToes) ||
                    !IsFinite(source.RightFoot) ||
                    !IsFinite(source.RightToes) ||
                    !IsFinite(target.LeftFoot) ||
                    !IsFinite(target.LeftToes) ||
                    !IsFinite(target.RightFoot) ||
                    !IsFinite(target.RightToes))
                {
                    throw new ArgumentException("발 접촉 표본은 유한한 값이어야 합니다.");
                }

                sourceLeft[index] = source.Left;
                sourceRight[index] = source.Right;
                targetLeft[index] = target.Left;
                targetRight[index] = target.Right;
                sourceLeftFeet[index] = source.LeftFoot;
                sourceLeftToes[index] = source.LeftToes;
                sourceRightFeet[index] = source.RightFoot;
                sourceRightToes[index] = source.RightToes;
                targetLeftFeet[index] = target.LeftFoot;
                targetLeftToes[index] = target.LeftToes;
                targetRightFeet[index] = target.RightFoot;
                targetRightToes[index] = target.RightToes;
            }

            Vector3[] leftCorrections = BuildFootCorrections(
                sourceLeft,
                targetLeft,
                frameRate,
                normalizedSourceHumanScale,
                normalizedRotation,
                minimumContactFrames,
                releaseFrames,
                out int leftRunCount);
            Vector3[] rightCorrections = BuildFootCorrections(
                sourceRight,
                targetRight,
                frameRate,
                normalizedSourceHumanScale,
                normalizedRotation,
                minimumContactFrames,
                releaseFrames,
                out int rightRunCount);
            Vector3[] leftToeDirections = ApplySupportPoseCorrections(
                sourceLeftFeet,
                sourceLeftToes,
                targetLeftFeet,
                targetLeftToes,
                leftCorrections,
                normalizedSourceHumanScale,
                normalizedTargetHumanScale,
                normalizedRotation);
            Vector3[] rightToeDirections = ApplySupportPoseCorrections(
                sourceRightFeet,
                sourceRightToes,
                targetRightFeet,
                targetRightToes,
                rightCorrections,
                normalizedSourceHumanScale,
                normalizedTargetHumanScale,
                normalizedRotation);
            return new HumanoidFootContactPlan(
                leftCorrections,
                rightCorrections,
                leftToeDirections,
                rightToeDirections,
                frameRate,
                leftRunCount,
                rightRunCount);
        }

        private static Vector3[] ApplySupportPoseCorrections(
            IReadOnlyList<Vector3> sourceFeet,
            IReadOnlyList<Vector3> sourceToes,
            IReadOnlyList<Vector3> targetFeet,
            IReadOnlyList<Vector3> targetToes,
            Vector3[] rootSpaceCorrections,
            float sourceHumanScale,
            float targetHumanScale,
            Quaternion sourceToTargetRotation)
        {
            float scaleRatio = targetHumanScale / sourceHumanScale;
            float sourceFootBaseline = CalculatePercentileHeight(sourceFeet, 0.02f);
            float sourceToesBaseline = CalculatePercentileHeight(sourceToes, 0.02f);
            float targetFootBaseline = CalculatePercentileHeight(targetFeet, 0.02f);
            float targetToesBaseline = CalculatePercentileHeight(targetToes, 0.02f);
            Quaternion targetToSourceRotation = Quaternion.Inverse(sourceToTargetRotation);
            var rootSpaceToeDirections = new Vector3[sourceFeet.Count];

            for (int index = 0; index < sourceFeet.Count; index++)
            {
                Vector3 targetSegment = targetToes[index] - targetFeet[index];
                float targetSegmentLength = targetSegment.magnitude;
                float desiredFootHeight = targetFootBaseline +
                    (sourceFeet[index].y - sourceFootBaseline) * scaleRatio;
                float desiredToesHeight = targetToesBaseline +
                    (sourceToes[index].y - sourceToesBaseline) * scaleRatio;
                if (targetSegmentLength <= MinimumSegmentLength)
                {
                    float footOnlyHeightCorrection =
                        desiredFootHeight - targetFeet[index].y;
                    if (Mathf.Abs(footOnlyHeightCorrection) >=
                        targetHumanScale * MinimumHeightCorrectionPerHumanScale)
                    {
                        rootSpaceCorrections[index] += targetToSourceRotation *
                            (Vector3.up * footOnlyHeightCorrection);
                    }
                    continue;
                }

                float desiredVerticalSeparation = Mathf.Clamp(
                    desiredToesHeight - desiredFootHeight,
                    -targetSegmentLength,
                    targetSegmentLength);

                // 발끝은 실제 지지점이고 Foot 본은 발목에 가까운 대리점이므로
                // 길이 제약이 충돌할 때 발끝 높이에 더 큰 가중치를 둠.
                float correctedFootHeight =
                    (desiredFootHeight +
                     ToeHeightWeight *
                     (desiredToesHeight - desiredVerticalSeparation)) /
                    (1f + ToeHeightWeight);
                float heightCorrection =
                    correctedFootHeight - targetFeet[index].y;
                if (Mathf.Abs(heightCorrection) >=
                    targetHumanScale * MinimumHeightCorrectionPerHumanScale)
                {
                    rootSpaceCorrections[index] += targetToSourceRotation *
                        (Vector3.up * heightCorrection);
                }

                Vector3 sourceSegment = sourceToes[index] - sourceFeet[index];
                Vector3 mappedSourceDirection =
                    sourceToTargetRotation * sourceSegment.normalized;
                Vector2 horizontalDirection = new Vector2(
                    mappedSourceDirection.x,
                    mappedSourceDirection.z);
                if (horizontalDirection.sqrMagnitude <= MinimumSegmentLength)
                {
                    horizontalDirection = new Vector2(
                        targetSegment.x,
                        targetSegment.z);
                }

                horizontalDirection.Normalize();
                float horizontalLength = Mathf.Sqrt(Mathf.Max(
                    0f,
                    targetSegmentLength * targetSegmentLength -
                    desiredVerticalSeparation * desiredVerticalSeparation));
                Vector3 desiredWorldDirection = new Vector3(
                    horizontalDirection.x * horizontalLength,
                    desiredVerticalSeparation,
                    horizontalDirection.y * horizontalLength);
                rootSpaceToeDirections[index] =
                    targetToSourceRotation * desiredWorldDirection;
            }

            return rootSpaceToeDirections;
        }

        private static Vector3[] BuildFootCorrections(
            IReadOnlyList<Vector3> sourcePoints,
            IReadOnlyList<Vector3> targetPoints,
            float frameRate,
            float sourceHumanScale,
            Quaternion sourceToTargetRotation,
            int minimumContactFrames,
            int releaseFrames,
            out int runCount)
        {
            float contactHeight = CalculatePercentileHeight(sourcePoints, 0.02f) +
                sourceHumanScale * ContactHeightMarginPerHumanScale;
            float contactSpeedLimit =
                sourceHumanScale * ContactSpeedLimitPerHumanScale;
            bool[] contactFrames = DetectContactFrames(
                sourcePoints,
                frameRate,
                contactHeight,
                contactSpeedLimit);
            var corrections = new Vector3[sourcePoints.Count];
            Quaternion targetToSourceRotation =
                Quaternion.Inverse(sourceToTargetRotation);
            runCount = 0;
            int runStart = -1;

            for (int index = 0; index <= contactFrames.Length; index++)
            {
                bool isContact = index < contactFrames.Length && contactFrames[index];
                if (isContact && runStart < 0)
                {
                    runStart = index;
                    continue;
                }

                if (isContact || runStart < 0)
                {
                    continue;
                }

                int runEnd = index - 1;
                if (runEnd - runStart + 1 >= minimumContactFrames)
                {
                    ApplyContactRun(
                        sourcePoints,
                        targetPoints,
                        contactFrames,
                        corrections,
                        sourceToTargetRotation,
                        targetToSourceRotation,
                        runStart,
                        runEnd,
                        releaseFrames);
                    runCount++;
                }

                runStart = -1;
            }

            return corrections;
        }

        private static void ApplyContactRun(
            IReadOnlyList<Vector3> sourcePoints,
            IReadOnlyList<Vector3> targetPoints,
            IReadOnlyList<bool> contactFrames,
            Vector3[] corrections,
            Quaternion sourceToTargetRotation,
            Quaternion targetToSourceRotation,
            int runStart,
            int runEnd,
            int releaseFrames)
        {
            Vector3 sourceAnchor = sourcePoints[runStart];
            Vector3 targetAnchor = targetPoints[runStart];
            for (int index = runStart; index <= runEnd; index++)
            {
                Vector3 sourceDelta = sourcePoints[index] - sourceAnchor;
                sourceDelta.y = 0f;
                Vector3 desiredTargetPoint =
                    targetAnchor + sourceToTargetRotation * sourceDelta;
                Vector3 worldCorrection = desiredTargetPoint - targetPoints[index];
                worldCorrection.y = 0f;
                Vector3 rootSpaceCorrection = targetToSourceRotation * worldCorrection;
                rootSpaceCorrection.y = 0f;
                corrections[index] = rootSpaceCorrection;
            }

            Vector3 releaseCorrection = corrections[runEnd];
            for (int releaseIndex = 1; releaseIndex <= releaseFrames; releaseIndex++)
            {
                int frameIndex = runEnd + releaseIndex;
                if (frameIndex >= corrections.Length || contactFrames[frameIndex])
                {
                    break;
                }

                float progress = releaseIndex / (releaseFrames + 1f);
                float smoothProgress = progress * progress * (3f - 2f * progress);
                corrections[frameIndex] =
                    releaseCorrection * (1f - smoothProgress);
            }
        }

        private static bool[] DetectContactFrames(
            IReadOnlyList<Vector3> points,
            float frameRate,
            float contactHeight,
            float contactSpeedLimit)
        {
            var contactCandidates = new bool[points.Count];
            var stableFrames = new bool[points.Count];
            for (int index = 0; index < points.Count; index++)
            {
                float horizontalSpeed = index == 0
                    ? 0f
                    : HorizontalDistance(points[index - 1], points[index]) * frameRate;
                contactCandidates[index] = points[index].y <= contactHeight &&
                    horizontalSpeed <= contactSpeedLimit;
                stableFrames[index] = contactCandidates[index] &&
                    CalculateCenteredSpeed(points, index, frameRate) <=
                    contactSpeedLimit;
            }

            // 낮은 발이 계속 상승·하강하는 통과 구간은 접촉으로 보지 않되,
            // 안정 프레임 사이의 발 롤링은 하나의 접촉 구간으로 보존함.
            var result = new bool[points.Count];
            int candidateStart = -1;
            for (int index = 0; index <= contactCandidates.Length; index++)
            {
                bool isCandidate = index < contactCandidates.Length &&
                    contactCandidates[index];
                if (isCandidate && candidateStart < 0)
                {
                    candidateStart = index;
                    continue;
                }

                if (isCandidate || candidateStart < 0)
                {
                    continue;
                }

                MarkStableContactSpan(
                    stableFrames,
                    result,
                    candidateStart,
                    index - 1);
                candidateStart = -1;
            }

            return result;
        }

        private static void MarkStableContactSpan(
            IReadOnlyList<bool> stableFrames,
            bool[] contactFrames,
            int candidateStart,
            int candidateEnd)
        {
            int firstStable = -1;
            int lastStable = -1;
            for (int index = candidateStart; index <= candidateEnd; index++)
            {
                if (!stableFrames[index])
                {
                    continue;
                }

                if (firstStable < 0)
                {
                    firstStable = index;
                }

                lastStable = index;
            }

            if (firstStable < 0)
            {
                return;
            }

            int contactStart = Mathf.Max(candidateStart, firstStable - 1);
            int contactEnd = Mathf.Min(candidateEnd, lastStable + 1);
            for (int index = contactStart; index <= contactEnd; index++)
            {
                contactFrames[index] = true;
            }
        }

        private static float CalculateCenteredSpeed(
            IReadOnlyList<Vector3> points,
            int index,
            float frameRate)
        {
            if (points.Count <= 1)
            {
                return 0f;
            }

            if (index == 0)
            {
                return Vector3.Distance(points[0], points[1]) * frameRate;
            }

            if (index == points.Count - 1)
            {
                return Vector3.Distance(points[index - 1], points[index]) * frameRate;
            }

            return Vector3.Distance(points[index - 1], points[index + 1]) *
                frameRate * 0.5f;
        }

        private static float CalculatePercentileHeight(
            IReadOnlyList<Vector3> points,
            float percentile)
        {
            var heights = new float[points.Count];
            for (int index = 0; index < points.Count; index++)
            {
                heights[index] = points[index].y;
            }

            Array.Sort(heights);
            int percentileIndex = Mathf.Clamp(
                Mathf.CeilToInt(heights.Length * percentile) - 1,
                0,
                heights.Length - 1);
            return heights[percentileIndex];
        }

        private static void ValidateSamples(
            IReadOnlyList<HumanoidFootContactSample> sourceSamples,
            IReadOnlyList<HumanoidFootContactSample> targetSamples,
            float frameRate)
        {
            if (sourceSamples == null)
            {
                throw new ArgumentNullException(nameof(sourceSamples));
            }

            if (targetSamples == null)
            {
                throw new ArgumentNullException(nameof(targetSamples));
            }

            if (sourceSamples.Count == 0 || sourceSamples.Count != targetSamples.Count)
            {
                throw new ArgumentException("원본과 대상 발 접촉 표본 수가 일치해야 합니다.");
            }

            if (!IsFinite(frameRate) || frameRate <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(frameRate));
            }
        }

        private static Quaternion NormalizeRotation(Quaternion rotation)
        {
            if (!IsFinite(rotation) ||
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w <= MinimumQuaternionMagnitude)
            {
                throw new ArgumentException("원본과 대상 사이 회전은 유효해야 합니다.");
            }

            return rotation.normalized;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            return Vector2.Distance(
                new Vector2(first.x, first.z),
                new Vector2(second.x, second.z));
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
