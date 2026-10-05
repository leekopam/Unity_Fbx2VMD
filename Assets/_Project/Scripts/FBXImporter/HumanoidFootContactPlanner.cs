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
            float targetHumanScale = float.NaN,
            HumanoidFootContactIntentEstimate intents = null)
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

            Quaternion targetToSourceRotation =
                Quaternion.Inverse(normalizedRotation);
            // 발끝 방향 세그먼트를 먼저 계산해야 핀 보정이 발끝 기준점을 쓸 수 있음.
            Vector3[] leftToeSegments = BuildDesiredToeSegments(
                sourceLeftFeet,
                sourceLeftToes,
                targetLeftFeet,
                targetLeftToes,
                normalizedSourceHumanScale,
                normalizedTargetHumanScale,
                normalizedRotation);
            Vector3[] rightToeSegments = BuildDesiredToeSegments(
                sourceRightFeet,
                sourceRightToes,
                targetRightFeet,
                targetRightToes,
                normalizedSourceHumanScale,
                normalizedTargetHumanScale,
                normalizedRotation);
            Vector3[] leftCorrections = BuildFootCorrections(
                sourceLeft,
                targetLeft,
                sourceLeftToes,
                targetLeftFeet,
                leftToeSegments,
                frameRate,
                normalizedSourceHumanScale,
                normalizedRotation,
                minimumContactFrames,
                releaseFrames,
                intents?.Left,
                out int leftRunCount);
            Vector3[] rightCorrections = BuildFootCorrections(
                sourceRight,
                targetRight,
                sourceRightToes,
                targetRightFeet,
                rightToeSegments,
                frameRate,
                normalizedSourceHumanScale,
                normalizedRotation,
                minimumContactFrames,
                releaseFrames,
                intents?.Right,
                out int rightRunCount);
            ApplySupportHeightCorrections(
                sourceLeftFeet,
                sourceLeftToes,
                targetLeftFeet,
                targetLeftToes,
                leftCorrections,
                normalizedSourceHumanScale,
                normalizedTargetHumanScale,
                targetToSourceRotation);
            ApplySupportHeightCorrections(
                sourceRightFeet,
                sourceRightToes,
                targetRightFeet,
                targetRightToes,
                rightCorrections,
                normalizedSourceHumanScale,
                normalizedTargetHumanScale,
                targetToSourceRotation);
            var leftToeDirections = new Vector3[leftToeSegments.Length];
            var rightToeDirections = new Vector3[rightToeSegments.Length];
            for (int index = 0; index < leftToeSegments.Length; index++)
            {
                leftToeDirections[index] =
                    targetToSourceRotation * leftToeSegments[index];
                // 발끝 방향 보정은 발목 축 회전으로 적용돼 발끝이 수평 이동하고
                // 그 절반만큼 접촉 중점이 밀림. 모든 프레임에서 발목 보정에
                // 회전 유발 수평 이동의 절반을 빼 "중점 = 기준 중점 + 보정"
                // 불변식을 지킴. 방향 델타가 0이면 보상도 0이라 게이트 불필요.
                leftCorrections[index] += ToeRotationMidpointCompensation(
                    targetLeftToes[index] - targetLeftFeet[index],
                    leftToeSegments[index],
                    targetToSourceRotation);
            }
            for (int index = 0; index < rightToeSegments.Length; index++)
            {
                rightToeDirections[index] =
                    targetToSourceRotation * rightToeSegments[index];
                rightCorrections[index] += ToeRotationMidpointCompensation(
                    targetRightToes[index] - targetRightFeet[index],
                    rightToeSegments[index],
                    targetToSourceRotation);
            }
            return new HumanoidFootContactPlan(
                leftCorrections,
                rightCorrections,
                leftToeDirections,
                rightToeDirections,
                frameRate,
                leftRunCount,
                rightRunCount);
        }

        // 발끝 세그먼트(발목→발끝 벡터)의 목표 형태를 대상 공간에 계산함.
        // 핀 보정이 발끝 기준점을 쓰려면 위치 보정보다 먼저 필요함.
        private static Vector3[] BuildDesiredToeSegments(
            IReadOnlyList<Vector3> sourceFeet,
            IReadOnlyList<Vector3> sourceToes,
            IReadOnlyList<Vector3> targetFeet,
            IReadOnlyList<Vector3> targetToes,
            float sourceHumanScale,
            float targetHumanScale,
            Quaternion sourceToTargetRotation)
        {
            float scaleRatio = targetHumanScale / sourceHumanScale;
            float sourceFootBaseline = CalculatePercentileHeight(sourceFeet, 0.02f);
            float sourceToesBaseline = CalculatePercentileHeight(sourceToes, 0.02f);
            float targetFootBaseline = CalculatePercentileHeight(targetFeet, 0.02f);
            float targetToesBaseline = CalculatePercentileHeight(targetToes, 0.02f);
            var segments = new Vector3[sourceFeet.Count];

            for (int index = 0; index < sourceFeet.Count; index++)
            {
                Vector3 targetSegment = targetToes[index] - targetFeet[index];
                float targetSegmentLength = targetSegment.magnitude;
                if (targetSegmentLength <= MinimumSegmentLength)
                {
                    continue;
                }

                float desiredFootHeight = targetFootBaseline +
                    (sourceFeet[index].y - sourceFootBaseline) * scaleRatio;
                float desiredToesHeight = targetToesBaseline +
                    (sourceToes[index].y - sourceToesBaseline) * scaleRatio;
                float desiredVerticalSeparation = Mathf.Clamp(
                    desiredToesHeight - desiredFootHeight,
                    -targetSegmentLength,
                    targetSegmentLength);

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
                segments[index] = new Vector3(
                    horizontalDirection.x * horizontalLength,
                    desiredVerticalSeparation,
                    horizontalDirection.y * horizontalLength);
            }

            return segments;
        }

        private static void ApplySupportHeightCorrections(
            IReadOnlyList<Vector3> sourceFeet,
            IReadOnlyList<Vector3> sourceToes,
            IReadOnlyList<Vector3> targetFeet,
            IReadOnlyList<Vector3> targetToes,
            Vector3[] rootSpaceCorrections,
            float sourceHumanScale,
            float targetHumanScale,
            Quaternion targetToSourceRotation)
        {
            float scaleRatio = targetHumanScale / sourceHumanScale;
            float sourceFootBaseline = CalculatePercentileHeight(sourceFeet, 0.02f);
            float sourceToesBaseline = CalculatePercentileHeight(sourceToes, 0.02f);
            float targetFootBaseline = CalculatePercentileHeight(targetFeet, 0.02f);
            float targetToesBaseline = CalculatePercentileHeight(targetToes, 0.02f);

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
            }
        }

        private static Vector3[] BuildFootCorrections(
            IReadOnlyList<Vector3> sourcePoints,
            IReadOnlyList<Vector3> targetPoints,
            IReadOnlyList<Vector3> sourceToes,
            IReadOnlyList<Vector3> targetFeet,
            IReadOnlyList<Vector3> toeSegments,
            float frameRate,
            float sourceHumanScale,
            Quaternion sourceToTargetRotation,
            int minimumContactFrames,
            int releaseFrames,
            IReadOnlyList<HumanoidFootContactIntent> intents,
            out int runCount)
        {
            // 확정된 의도 구간은 프레임별 앵커 방침으로 변환해 보정 계획에 반영함.
            HumanoidFootAnchorPolicy[] policies =
                HumanoidFootAnchorPolicyResolver.Rasterize(intents, sourcePoints.Count);
            float pinRelease =
                sourceHumanScale * HumanoidFootContactIntentEstimator.SlideDisplacementPerHumanScale;
            float contactHeight = CalculatePercentileHeight(sourcePoints, 0.02f) +
                sourceHumanScale * ContactHeightMarginPerHumanScale;
            float contactSpeedLimit =
                sourceHumanScale * ContactSpeedLimitPerHumanScale;
            bool[] contactFrames = DetectContactFrames(
                sourcePoints,
                frameRate,
                contactHeight,
                contactSpeedLimit);
            // 발끝이 낮고 정지한 프레임의 지지점은 발끝임. 이 프레임에서는
            // 발목이 움직여도(롤) 발끝을 고정해야 드리프트가 생기지 않음.
            float toeContactHeight =
                CalculatePercentileHeight(sourceToes, 0.02f) +
                sourceHumanScale * ContactHeightMarginPerHumanScale;
            var toePivot = new bool[sourcePoints.Count];
            for (int index = 0; index < sourcePoints.Count; index++)
            {
                toePivot[index] = sourceToes[index].y <= toeContactHeight &&
                    CalculateCenteredSpeed(sourceToes, index, frameRate) <=
                    contactSpeedLimit;
            }
            // 발끝이 바닥에 닿아있는 프레임은 의도·방침과 무관하게 접촉이다.
            // 롤·슬라이드·정착 구간은 중점이 들리거나 움직여 런 밖으로 빠지는데,
            // 그러면 확정 방침이 소비되지 않고 런 종료의 보정 해제가
            // 발끝 잔류 접촉 안에서 일어나 전이 팝이 된다.
            // 발끝 착지 프레임을 모두 런에 포함시켜 추종이 실제 리프트오프까지
            // 이어지게 함. 단, 빠르게 상승 중인 발끝은 이륙 진행 중이므로 제외함.
            for (int index = 0; index < contactFrames.Length; index++)
            {
                if (contactFrames[index] ||
                    sourceToes[index].y > toeContactHeight)
                {
                    continue;
                }

                float toeRiseSpeed = index == 0
                    ? 0f
                    : (sourceToes[index].y - sourceToes[index - 1].y) * frameRate;
                if (toeRiseSpeed <= contactSpeedLimit)
                {
                    contactFrames[index] = true;
                }
            }

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
                        sourceToes,
                        toeSegments,
                        toePivot,
                        contactFrames,
                        corrections,
                        sourceToTargetRotation,
                        targetToSourceRotation,
                        runStart,
                        runEnd,
                        releaseFrames,
                        policies,
                        pinRelease);
                    runCount++;
                }

                runStart = -1;
            }

            return corrections;
        }

        // 기본값은 기존 핀/해제/해제블렌드 구현 — 병합된 의도와 기존 핀 경로의
        // 조합 효과를 분리 측정할 수 있게 실험 토글로만 열어둠.
        // 오프라인 솔버는 FbxDiagnostics.RunAll의 -offlineSolver 인자나 EditMode
        // 테스트에서만 켜고, 병합만으로 게이트 미달일 때 활성화를 검토함.
        internal static bool UseOfflineContactSolver = false;

        // 오프라인 제약 풀이의 반복 계수 — 접촉 프레임은 강하게 수렴하고
        // 비접촉 프레임은 원본 상대이동을 유지하는 쪽으로 약하게 수렴함.
        private const float ContactHardFactor = 0.9f;
        private const float FollowSoftFactor = 0.05f;
        private const int SolverIterationsPerFrame = 8;
        private const int SolverMinimumIterations = 60;

        private static void ApplyContactRun(
            IReadOnlyList<Vector3> sourcePoints,
            IReadOnlyList<Vector3> targetPoints,
            IReadOnlyList<Vector3> sourceToes,
            IReadOnlyList<Vector3> toeSegments,
            IReadOnlyList<bool> toePivot,
            IReadOnlyList<bool> contactFrames,
            Vector3[] corrections,
            Quaternion sourceToTargetRotation,
            Quaternion targetToSourceRotation,
            int runStart,
            int runEnd,
            int releaseFrames,
            HumanoidFootAnchorPolicy[] policies,
            float pinReleaseDistance)
        {
            if (UseOfflineContactSolver)
            {
                ApplyContactRunOffline(
                    sourcePoints, targetPoints, contactFrames, corrections,
                    sourceToTargetRotation, targetToSourceRotation,
                    runStart, runEnd, releaseFrames, policies, pinReleaseDistance);
                return;
            }

            // 추종 궤적은 항상 발목·발끝 중점 기준(기존 동작과 동일).
            // 핀은 롤 프레임에서 발끝을 고정점으로 잡되 중점 목표로 환산해
            // 발목이 심은 발끝 주위를 회전하도록 함. 기준 전환은 핀 경계에서만
            // 일어나고 모든 전이는 연속으로 유지됨.
            Vector3 desiredVertex = targetPoints[runStart];
            Vector3 previousSourceVertex = sourcePoints[runStart];
            // 핀과 무관하게 원본 스텝을 계속 누적하는 순수 추종 궤적.
            // 핀 해제 시 이 궤적으로 수렴해야 런 끝점이 원본 총이동과 일치함.
            Vector3 followVertex = desiredVertex;
            // 핀 해제 직후 남은 위치 차 — releaseFrames에 걸쳐 0으로 블렌드함.
            Vector3 releaseResidual = Vector3.zero;
            int framesSinceRelease = int.MaxValue;
            bool pinned = false;
            bool pinBlocked = false;
            bool pinPivot = false;
            Vector3 pinnedVertex = Vector3.zero;
            Vector3 pinSourceVertex = Vector3.zero;
            for (int index = runStart; index <= runEnd; index++)
            {
                bool pivot = toePivot[index];
                bool wantsPin =
                    policies[index] == HumanoidFootAnchorPolicy.Pinned &&
                    !pinBlocked;
                bool released = pinned && !wantsPin;

                // 방침이 Pinned가 아닌 프레임에서는 핀이 풀림.
                if (released)
                {
                    pinned = false;
                }

                // 순수 추종 궤적은 핀 여부와 무관하게 매 프레임 전진함.
                // 핀 기간의 원본 이동까지 누적해야 해제 수렴 끝점이 맞음.
                Vector3 step = sourcePoints[index] - previousSourceVertex;
                step.y = 0f;
                followVertex += sourceToTargetRotation * step;
                previousSourceVertex = sourcePoints[index];

                if (pinned)
                {
                    if (pivot != pinPivot)
                    {
                        // 핀 기준 전환: 같은 물리 위치로 환산해 팝을 막음.
                        // 목표 자세에서 발끝은 중점보다 세그먼트 절반만큼 앞에 있음.
                        pinnedVertex += toeSegments[index] * 0.5f *
                            (pivot ? 1f : -1f);
                        pinPivot = pivot;
                        pinSourceVertex = pivot
                            ? sourceToes[index]
                            : sourcePoints[index];
                    }

                    // 발끝 핀은 중점 목표를 핀 발끝 - 세그먼트/2로 둬서
                    // 롤 중 발목 회전을 허용하면서 발끝만 고정함.
                    desiredVertex = pinPivot
                        ? pinnedVertex - toeSegments[index] * 0.5f
                        : pinnedVertex;
                    Vector3 sourceVertex = pinPivot
                        ? sourceToes[index]
                        : sourcePoints[index];
                    if (pinReleaseDistance > 0f &&
                        HorizontalDistance(sourceVertex, pinSourceVertex) >=
                            pinReleaseDistance)
                    {
                        // 원본 지지점이 핀 구간에서 실제로 움직이면 의도 추정
                        // 오류로 보고 추종을 재개함.
                        pinned = false;
                        pinBlocked = true;
                        // 이 프레임은 핀 목표로 적용되므로 잔차 캡처를 여기서 함.
                        releaseResidual = desiredVertex - followVertex;
                        framesSinceRelease = 0;
                    }
                }
                else
                {
                    if (released)
                    {
                        // 핀이 풀린 프레임의 잔차를 잡아 추종 궤적까지
                        // 블렌드할 양으로 남김. 즉시 점프하면 팝이 됨.
                        releaseResidual = desiredVertex - followVertex;
                        framesSinceRelease = 0;
                    }
                    else if (framesSinceRelease <= releaseFrames)
                    {
                        framesSinceRelease++;
                    }

                    if (framesSinceRelease > releaseFrames)
                    {
                        desiredVertex = followVertex;
                    }
                    else
                    {
                        float blendT = releaseFrames > 0
                            ? Mathf.Clamp01(
                                framesSinceRelease / (releaseFrames + 1f))
                            : 1f;
                        float smooth = blendT * blendT * (3f - 2f * blendT);
                        desiredVertex =
                            followVertex + releaseResidual * (1f - smooth);
                    }

                    if (wantsPin)
                    {
                        // 핀 진입 시 추종 위치 그대로를 앵커로 삼아 경계 연속 유지.
                        // 발끝이 심어진 프레임이면 발끝 기준으로 핀을 잡음.
                        pinned = true;
                        pinPivot = pivot;
                        pinnedVertex = pivot
                            ? desiredVertex + toeSegments[index] * 0.5f
                            : desiredVertex;
                        pinSourceVertex = pivot
                            ? sourceToes[index]
                            : sourcePoints[index];
                    }
                }

                Vector3 worldCorrection = desiredVertex - targetPoints[index];
                worldCorrection.y = 0f;
                Vector3 rootSpaceCorrection =
                    targetToSourceRotation * worldCorrection;
                rootSpaceCorrection.y = 0f;
                corrections[index] = rootSpaceCorrection;
            }

            ApplyReleaseBlend(corrections, contactFrames, runEnd, releaseFrames);
        }

        /// <summary>
        /// 클립 전체를 아는 오프라인 제약 풀이로 접촉 런을 보정함.
        /// 런 안 각 프레임의 대상 접촉점을 입자로 보고 반복 수렴함:
        ///   - 연속된 핀 프레임은 같은 수평 위치로 강하게 수렴(하드 제약)
        ///   - 나머지 프레임은 원본의 프레임간 상대 이동을 유지(소프트 제약)
        ///   - 런 첫 프레임은 진입 위치에 고정해 경계 연속성을 지킴
        /// 섬 단위 핀 해제가 없으므로 누적 잔차가 생기지 않음.
        /// </summary>
        private static void ApplyContactRunOffline(
            IReadOnlyList<Vector3> sourcePoints,
            IReadOnlyList<Vector3> targetPoints,
            IReadOnlyList<bool> contactFrames,
            Vector3[] corrections,
            Quaternion sourceToTargetRotation,
            Quaternion targetToSourceRotation,
            int runStart,
            int runEnd,
            int releaseFrames,
            HumanoidFootAnchorPolicy[] policies,
            float pinReleaseDistance)
        {
            int length = runEnd - runStart + 1;
            var solved = new Vector3[length];
            for (int k = 0; k < length; k++)
            {
                solved[k] = targetPoints[runStart + k];
            }

            // 핀 구간마다 원본 기준점을 잡아, 핀 안에서 원본이 실제로 크게 움직이면
            // 의도 추정 오류로 보고 그 프레임의 핀을 해제(소프트)함.
            var effectivePinned = new bool[length];
            int pinStart = -1;
            for (int k = 0; k <= length; k++)
            {
                bool isPin = k < length &&
                    policies[runStart + k] == HumanoidFootAnchorPolicy.Pinned;
                if (isPin && pinStart < 0) { pinStart = k; continue; }
                if (isPin || pinStart < 0) { continue; }
                Vector3 pinSource = sourcePoints[runStart + pinStart];
                for (int j = pinStart; j < k; j++)
                {
                    effectivePinned[j] = pinReleaseDistance <= 0f ||
                        HorizontalDistance(sourcePoints[runStart + j], pinSource) <
                        pinReleaseDistance;
                }
                pinStart = -1;
            }

            Vector3 entryAnchor = solved[0];
            int iterations = Mathf.Max(SolverMinimumIterations,
                SolverIterationsPerFrame * length);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int k = 1; k < length; k++)
                {
                    if (effectivePinned[k - 1] && effectivePinned[k])
                    {
                        // 하드 제약: 연속 접촉 프레임을 수평 중점으로 수렴함.
                        float midX = (solved[k - 1].x + solved[k].x) * 0.5f;
                        float midZ = (solved[k - 1].z + solved[k].z) * 0.5f;
                        solved[k - 1] = HorizontalLerp(
                            solved[k - 1], midX, midZ, ContactHardFactor);
                        solved[k] = HorizontalLerp(
                            solved[k], midX, midZ, ContactHardFactor);
                    }
                    else
                    {
                        // 소프트 제약: 원본의 상대 이동을 유지하도록 쌍을 당김.
                        Vector3 relative = sourceToTargetRotation *
                            (sourcePoints[runStart + k] -
                             sourcePoints[runStart + k - 1]);
                        relative.y = 0f;
                        Vector3 error = new Vector3(
                            solved[k].x - solved[k - 1].x - relative.x,
                            0f,
                            solved[k].z - solved[k - 1].z - relative.z);
                        Vector3 half = error * (FollowSoftFactor * 0.5f);
                        solved[k - 1] = new Vector3(
                            solved[k - 1].x + half.x, solved[k - 1].y,
                            solved[k - 1].z + half.z);
                        solved[k] = new Vector3(
                            solved[k].x - half.x, solved[k].y,
                            solved[k].z - half.z);
                    }
                }
                // 런 진입 프레임을 고정해 보정 시작점의 연속성을 유지함.
                solved[0] = new Vector3(entryAnchor.x, solved[0].y, entryAnchor.z);
            }

            for (int k = 0; k < length; k++)
            {
                int index = runStart + k;
                Vector3 rootSpaceCorrection = targetToSourceRotation *
                    (solved[k] - targetPoints[index]);
                rootSpaceCorrection.y = 0f;
                corrections[index] = rootSpaceCorrection;
            }

            ApplyReleaseBlend(corrections, contactFrames, runEnd, releaseFrames);
        }

        private static Vector3 HorizontalLerp(Vector3 from, float x, float z,
            float t)
        {
            return new Vector3(
                Mathf.Lerp(from.x, x, t), from.y, Mathf.Lerp(from.z, z, t));
        }

        /// <summary>
        /// 발끝 방향 보정이 발목 축 회전으로 적용될 때 발끝이 수평 이동하고
        /// 접촉 중점(발목·발끝 평균)이 그 절반만큼 밀림. 접촉 중점을 계획 위치에
        /// 유지하려고 발목 보정에서 회전 유발 수평 이동의 절반을 뺌.
        /// 수직 성분은 착지 자체의 발끝 하강이므로 보상하지 않음.
        /// </summary>
        internal static Vector3 ToeRotationMidpointCompensation(
            Vector3 currentToeOffset,
            Vector3 desiredToeOffset,
            Quaternion targetToSourceRotation)
        {
            Vector3 horizontalDelta = desiredToeOffset - currentToeOffset;
            horizontalDelta.y = 0f;
            Vector3 rootSpace = targetToSourceRotation *
                (horizontalDelta * -0.5f);
            rootSpace.y = 0f;
            return rootSpace;
        }

        // 런 끝 잔여 보정을 비접촉 프레임에 걸쳐 부드럽게 감쇠시킴.
        private static void ApplyReleaseBlend(
            Vector3[] corrections,
            IReadOnlyList<bool> contactFrames,
            int runEnd,
            int releaseFrames)
        {
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
