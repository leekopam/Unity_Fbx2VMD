#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 지지점 신원과 공통 이동을 미리 계산하여 탐색 순서와 무관하게 평가함.
    /// </summary>
    internal sealed class EditorHumanoidFootContactPlan
    {
        // 방출 앵커가 목표에 뒤처진 갭을 비율로 회수하되 절대 상한을 두어
        // 방침 전환·핀 시작 경계에서도 프레임당 이동이 제한된 채 유지되게 함.
        private const float AnchorCatchUpFraction = 0.34f;
        private const float AnchorCatchUpStepFactor = 4f;
        // 이 프레임 수를 넘는 공백 뒤의 재획득은 새 착지로 보고 앵커를 재샘플함.
        private const int MaxReacquireGapFrames = 6;
        // 재획득 시드는 소멸 직전 발이 실제로 앵커에 붙어 있던 경우에만 유효함.
        // 감쇠 중 소멸하면 발은 이미 원시 위치로 돌아간 뒤라 옛 앵커 부활이 팝이 됨.
        private const float MinReacquireSeedWeight = 0.9f;
        private readonly Frame[][] _frames;
        internal int UnresolvedPairCount { get; private set; }
        internal string FirstIssue { get; private set; }

        private EditorHumanoidFootContactPlan(int count)
        {
            _frames = new[] { new Frame[count], new Frame[count] };
        }

        internal readonly struct Sample
        {
            private readonly int _firstPoint;
            private readonly int _secondPoint;
            private readonly float _blend;
            internal readonly Vector3 Anchor;
            internal readonly bool CanAlignPair;
            internal int FirstPoint => _firstPoint;
            internal bool HasValue => _firstPoint >= 0;
            internal bool IsInterpolatedPoint => _firstPoint != _secondPoint && _blend > 0f && _blend < 1f;

            internal Sample(int firstPoint, int secondPoint, float blend, Vector3 anchor, bool canAlignPair)
            {
                _firstPoint = firstPoint;
                _secondPoint = secondPoint;
                _blend = blend;
                Anchor = anchor;
                CanAlignPair = canAlignPair;
            }

            internal bool TryGetLocalPoint(EditorHumanoidFootSoleSampler sampler, out Vector3 point)
            {
                point = Vector3.zero;
                if (!sampler.TryGetLocalPoint(_firstPoint, out Vector3 first) ||
                    !sampler.TryGetLocalPoint(_secondPoint, out Vector3 second)) return false;
                // 신원 전환 사이의 가상 접촉점도 앵커와 같은 계수로 보간함.
                point = Vector3.LerpUnclamped(first, second, _blend);
                return true;
            }
        }

        internal Sample GetSample(int channel, float frame)
        {
            frame = Mathf.Clamp(frame, 0f, _frames[channel].Length - 1);
            int firstIndex = Mathf.FloorToInt(frame);
            Frame first = _frames[channel][firstIndex];
            Frame second = _frames[channel][Mathf.Min(firstIndex + 1, _frames[channel].Length - 1)];
            if (first.Point < 0) first = second;
            if (second.Point < 0) second = first;
            float blend = frame - firstIndex;
            // 접촉점 신원이 갈리는 경계는 지면과 무관한 가상점 보간 대신
            // 해당 프레임의 실제 피벗과 앵커를 그대로 교체함.
            if (first.Point >= 0 && second.Point >= 0 && first.Point != second.Point)
            {
                Frame chosen = blend < 0.5f ? first : second;
                return new Sample(chosen.Point, chosen.Point, 0f, chosen.Anchor,
                    first.CanAlignPair || second.CanAlignPair);
            }
            return new Sample(first.Point, second.Point, blend,
                Vector3.LerpUnclamped(first.Anchor, second.Anchor, blend), first.CanAlignPair || second.CanAlignPair);
        }

        internal static bool TryBuild(Transform foot, EditorHumanoidFootSoleSampler sampler,
            Vector3[][] source, Vector2[] weights, Quaternion sourceRotation, float scaleRatio,
            float frameRate, float clipLength, Action<float> evaluate, out EditorHumanoidFootContactPlan plan,
            Quaternion[] sourceFrames = null, Quaternion? localFootFrame = null,
            HumanoidFootAnchorPolicy[] anchorPolicies = null,
            float pinReleaseSourceDistance = 0f, float anchorTrackStep = 0f)
        {
            plan = null;
            if (foot == null || sampler == null || source == null || source.Length != 2 || weights == null ||
                weights.Length == 0 || source[0].Length != weights.Length || source[1].Length != weights.Length ||
                evaluate == null || frameRate <= 0f || scaleRatio <= 0f ||
                !HasValidFrames(sourceFrames, localFootFrame, weights.Length) ||
                (anchorPolicies != null && anchorPolicies.Length != weights.Length)) return false;
            var result = new EditorHumanoidFootContactPlan(weights.Length);
            var states = new Contact[2];
            var offsets = new Vector3[2];
            bool TryUpdateOffset(int channel, int point)
            {
                if (sourceFrames == null) return true;
                if (!sampler.TryGetLocalPoint(point, out Vector3 localPoint)) return false;
                // 준비한 발 기준축의 원본 단위 offset을 고정하여 회전 기여와 배율을 한 번만 적용함.
                offsets[channel] = Quaternion.Inverse(localFootFrame.Value) * (localPoint * foot.lossyScale.x / scaleRatio);
                return true;
            }
            Pair pair = null;
            bool wasOverlapping = false;
            var emitted = new Vector3[2];
            var emittedValid = new bool[2];
            var emittedContact = new Contact[2];
            var emittedPoint = new[] { -1, -1 };
            var emittedFrame = new int[2];
            var emittedWeight = new float[2];
            for (int frame = 0; frame < weights.Length; frame++)
            {
                Vector3 SourcePoint(int channel) => sourceFrames == null ? source[channel][frame] :
                    source[0][frame] + sourceFrames[frame] * offsets[channel];
                bool hasSample = false;
                bool TrySample()
                {
                    if (hasSample) return true;
                    evaluate(Mathf.Min(frame / frameRate, clipLength));
                    hasSample = sampler.TrySample();
                    return hasSample;
                }
                for (int channel = 0; channel < 2; channel++)
                {
                    if (weights[frame][channel] <= 0f) states[channel] = null;
                    else if (states[channel] == null)
                    {
                        if (!TrySample() || !sampler.TrySelectContact(channel == 1, Vector3.up,
                                out int point, out Vector3 anchor)) return false;
                        // 짧은 재획득이면 직전 방출 앵커에 정점 간 물리 변위를 더해 시드하여
                        // 새 정점이 재샘플 위치로 발을 끌어당기는 팝을 방지함.
                        if (emittedValid[channel] &&
                            emittedWeight[channel] >= MinReacquireSeedWeight &&
                            frame - emittedFrame[channel] <= MaxReacquireGapFrames &&
                            emittedPoint[channel] >= 0 &&
                            sampler.TryGetLocalPoint(emittedPoint[channel], out Vector3 previousLocal) &&
                            sampler.TryGetLocalPoint(point, out Vector3 currentLocal))
                        {
                            anchor = emitted[channel] + Vector3.ProjectOnPlane(
                                foot.rotation * ((currentLocal - previousLocal) * foot.lossyScale.x),
                                Vector3.up);
                        }
                        if (!TryUpdateOffset(channel, point)) return false;
                        states[channel] = new Contact(frame, point, anchor, SourcePoint(channel));
                    }
                }
                for (int channel = 0; channel < 2; channel++)
                {
                    states[channel]?.ApplyAnchorPolicy(SourcePoint(channel), sourceRotation,
                        scaleRatio,
                        anchorPolicies == null
                            ? HumanoidFootAnchorPolicy.Free
                            : anchorPolicies[frame],
                        pinReleaseSourceDistance);
                }
                bool overlapping = states[0] != null && states[1] != null;
                Vector3 previousPrimaryPoint = Vector3.zero;
                bool hasPreviousPrimaryPoint = false;
                if (overlapping && !wasOverlapping)
                {
                    if (!TrySample()) return false;
                    int primary = states[0].StartFrame <= states[1].StartFrame ? 0 : 1;
                    if (frame > 0 && emittedValid[primary] && emittedContact[primary] == states[primary])
                    {
                        evaluate(Mathf.Min((frame - 1) / frameRate, clipLength));
                        if (sampler.TrySample() &&
                            sampler.TryGetLocalPoint(states[primary].Point, out Vector3 previousLocalPoint))
                        {
                            previousPrimaryPoint = foot.TransformPoint(previousLocalPoint);
                            hasPreviousPrimaryPoint = true;
                        }
                        evaluate(Mathf.Min(frame / frameRate, clipLength));
                        if (!sampler.TrySample()) return false;
                    }
                    Vector3 previousRear = SourcePoint(0), previousFront = SourcePoint(1);
                    if (!TryCreatePair(foot, sampler, states, source, frame, sourceRotation, out pair))
                        result.RecordIssue(frame, "공동 접촉 자세 또는 지상 방향을 준비하지 못함");
                    else
                    {
                        for (int channel = 0; channel < 2; channel++)
                        {
                            if (!TryUpdateOffset(channel, states[channel].Point)) return false;
                            // 핀 고정 접촉은 기준점을 현재 원본 위치로 재설정해 앵커가 움직이지 않게 함.
                            // 비핀 접촉은 점 교체에 따른 기존 앵커 이동을 유지하고 원본 기준점 변경만 상쇄함.
                            states[channel].SourcePoint = states[channel].Pinned
                                ? SourcePoint(channel)
                                : states[channel].SourcePoint + SourcePoint(channel) -
                                    (channel == 0 ? previousRear : previousFront);
                        }
                    }
                }
                if (!overlapping) pair = null;
                wasOverlapping = overlapping;
                bool canAlign = false;
                int preservedPrimary = -1;
                if (pair != null)
                {
                    Vector3 direction = Vector3.ProjectOnPlane(sourceRotation * (source[1][frame] - source[0][frame]), Vector3.up);
                    if (direction.sqrMagnitude >= 0.00000001f)
                    {
                        float yaw = Vector3.SignedAngle(pair.SourceDirection, direction, Vector3.up);
                        Vector3 anchor = states[pair.Primary].GetAnchor(SourcePoint(pair.Primary), sourceRotation, scaleRatio);
                        HumanoidFootAnchorPolicy primaryPolicy = anchorPolicies == null
                            ? HumanoidFootAnchorPolicy.Free : anchorPolicies[frame];
                        if (hasPreviousPrimaryPoint &&
                            result._frames[pair.Primary][frame - 1].Point != states[pair.Primary].Point &&
                            sampler.TryGetLocalPoint(states[pair.Primary].Point, out Vector3 currentLocalPoint))
                        {
                            Vector3 anchorShift = Vector3.ProjectOnPlane(anchor - emitted[pair.Primary], Vector3.up);
                            Vector3 pointShift = Vector3.ProjectOnPlane(
                                foot.TransformPoint(currentLocalPoint) - previousPrimaryPoint, Vector3.up);
                            // 정점 교체의 실제 이동과 맞는 앵커 이동만 발목 자세 보상으로 허용함.
                            if (Vector3.Distance(anchorShift, pointShift) <= foot.lossyScale.x * 0.005f)
                                preservedPrimary = pair.Primary;
                        }
                        if (anchorTrackStep > 0f && emittedValid[pair.Primary] &&
                            emittedContact[pair.Primary] == states[pair.Primary] &&
                            preservedPrimary != pair.Primary)
                            anchor = MoveEmittedAnchor(emitted[pair.Primary], anchor, anchorTrackStep);
                        int secondary = 1 - pair.Primary;
                        // 보조 접촉은 최종 주 앵커를 기준으로 정렬해 이동 상한에 따른 간격 불일치를 막음.
                        Vector3 secondaryAnchor = anchor +
                            Quaternion.AngleAxis(yaw, Vector3.up) * pair.Offset;
                        // 핀 고정은 원본 추종만 멈추며, 쌍 간격과 양립 불가한 앵커를 그대로 두지 않음.
                        // 정합 위치로 한 번 접은 뒤 기준점을 현재 원본으로 옮겨 고정 상태를 유지함.
                        bool secondaryMisaligned = states[secondary].Pinned &&
                            Vector3.ProjectOnPlane(states[secondary].Anchor - secondaryAnchor,
                                Vector3.up).magnitude > foot.lossyScale.x * 0.005f;
                        if (!states[secondary].Pinned || secondaryMisaligned)
                        {
                            states[secondary].Anchor = secondaryAnchor;
                            states[secondary].SourcePoint = SourcePoint(secondary);
                        }
                        canAlign = true;
                    }
                    else
                    {
                        result.RecordIssue(frame, "원본 발 방향의 지상 투영이 소실됨");
                        pair = null;
                    }
                }
                for (int channel = 0; channel < 2; channel++)
                {
                    if (states[channel] == null)
                    {
                        result._frames[channel][frame] = new Frame(-1, Vector3.zero, false);
                        // 직전 방출 앵커·정점을 유지해 짧은 재획득 시드에 사용함.
                        continue;
                    }
                    HumanoidFootAnchorPolicy policy = anchorPolicies == null
                        ? HumanoidFootAnchorPolicy.Free
                        : anchorPolicies[frame];
                    Vector3 anchor = states[channel].GetAnchor(
                        SourcePoint(channel), sourceRotation, scaleRatio);
                    // 정점이 바뀐 프레임은 방출 기준점을 정점 이동량만큼 먼저 옮겨
                    // 같은 물리 위치끼리 비교하도록 함.
                    if (emittedValid[channel] && emittedContact[channel] == states[channel] &&
                        frame > 0 && TrySample() &&
                        result._frames[channel][frame - 1].Point >= 0 &&
                        result._frames[channel][frame - 1].Point != states[channel].Point &&
                        sampler.TryGetLocalPoint(result._frames[channel][frame - 1].Point,
                            out Vector3 replacedLocal) &&
                        sampler.TryGetLocalPoint(states[channel].Point, out Vector3 currentLocal))
                    {
                        emitted[channel] += Vector3.ProjectOnPlane(
                            foot.rotation * ((currentLocal - replacedLocal) * foot.lossyScale.x),
                            Vector3.up);
                    }
                    // 방출 앵커는 프레임당 상한+갭 비례 회수로만 움직여 방침 전환·핀 시작
                    // 경계에서 누적 지연이 한 번에 방출되는 팝을 막음.
                    // 정점 교체의 실제 이동과 어울리는 앵커 이동은 예외로 둠.
                    if (anchorTrackStep > 0f && emittedValid[channel] &&
                        emittedContact[channel] == states[channel] &&
                        preservedPrimary != channel)
                        anchor = MoveEmittedAnchor(emitted[channel], anchor, anchorTrackStep);
                    result._frames[channel][frame] =
                        new Frame(states[channel].Point, anchor, canAlign);
                    emitted[channel] = anchor;
                    emittedValid[channel] = true;
                    emittedContact[channel] = states[channel];
                    emittedPoint[channel] = states[channel].Point;
                    emittedFrame[channel] = frame;
                    emittedWeight[channel] = weights[frame][channel];
                }
            }
            plan = result;
            return true;
        }

        private static bool HasValidFrames(Quaternion[] sourceFrames, Quaternion? localFootFrame, int count)
        {
            if ((sourceFrames != null) != localFootFrame.HasValue) return false;
            if (sourceFrames == null) return true;
            if (sourceFrames.Length != count || !IsUnitRotation(localFootFrame.Value)) return false;
            foreach (Quaternion frame in sourceFrames)
                if (!IsUnitRotation(frame)) return false;
            return true;
        }

        private static bool IsUnitRotation(Quaternion rotation)
        {
            float length = Quaternion.Dot(rotation, rotation);
            return !float.IsNaN(length) && !float.IsInfinity(length) && Mathf.Abs(length - 1f) < 0.0001f;
        }

        private static bool TryCreatePair(Transform foot, EditorHumanoidFootSoleSampler sampler,
            Contact[] states, Vector3[][] source, int frame, Quaternion sourceRotation, out Pair pair)
        {
            pair = null;
            Vector3 direction = Vector3.ProjectOnPlane(sourceRotation * (source[1][frame] - source[0][frame]), Vector3.up);
            if (direction.sqrMagnitude < 0.00000001f ||
                !sampler.TryGetLocalPoint(states[0].Point, out Vector3 originalRear) ||
                !sampler.TryGetLocalPoint(states[1].Point, out Vector3 originalFront)) return false;
            Vector3 axis = Vector3.Cross(Vector3.up, foot.TransformVector(originalFront - originalRear)).normalized;
            if (axis.sqrMagnitude < 0.5f || !TryFindSupportPose(foot, sampler, axis, foot.rotation, Vector3.up,
                    out Quaternion rotation, out int rear, out int front)) return false;
            if (!sampler.TryGetLocalPoint(rear, out Vector3 newRear) ||
                !sampler.TryGetLocalPoint(front, out Vector3 newFront)) return false;
            int primary = states[0].StartFrame <= states[1].StartFrame ? 0 : 1;
            Vector3 offset = Vector3.ProjectOnPlane(rotation * ((newFront - newRear) * foot.lossyScale.x), Vector3.up);
            if (offset.sqrMagnitude < 0.00000001f) return false;
            // 접촉 정점 신원이 바뀌면 앵커를 실제 정점 이동량만큼 옮겨 발 자세 연속을 유지함.
            // 핀 고정 접촉도 보정하지 않으면 구 정점 위치의 앵커에 새 정점이 끌려가 팝이 됨.
            // 정점이 그대로인 채널은 이동량이 0이라 영향이 없음.
            for (int channel = 0; channel < 2; channel++)
            {
                Vector3 vertexShift = channel == 0 ? newRear - originalRear : newFront - originalFront;
                states[channel].Anchor += Vector3.ProjectOnPlane(
                    rotation * (vertexShift * foot.lossyScale.x), Vector3.up);
            }
            states[0].Point = rear;
            states[1].Point = front;
            pair = new Pair(primary, direction, primary == 0 ? offset : -offset);
            return true;
        }

        internal static bool TryFindSupportPose(Transform foot, EditorHumanoidFootSoleSampler sampler, Vector3 axis,
            Quaternion referenceRotation, Vector3 groundNormal,
            out Quaternion rotation, out int rear, out int front)
        {
            rotation = referenceRotation;
            rear = front = -1;
            Quaternion original = rotation;
            float Evaluate(float angle, out int rearPoint, out int frontPoint)
            {
                rearPoint = frontPoint = -1;
                Quaternion candidate = Quaternion.AngleAxis(angle, axis) * original;
                Vector3 normal = foot.rotation * (Quaternion.Inverse(candidate) * groundNormal);
                if (!sampler.TrySelectContact(false, normal.normalized, out rearPoint, out _) ||
                    !sampler.TrySelectContact(true, normal.normalized, out frontPoint, out _) ||
                    !sampler.TryGetLocalPoint(rearPoint, out Vector3 rearLocal) ||
                    !sampler.TryGetLocalPoint(frontPoint, out Vector3 frontLocal)) return float.NaN;
                return Vector3.Dot(candidate * ((rearLocal - frontLocal) * foot.lossyScale.x), groundNormal);
            }
            float best = float.PositiveInfinity;
            float leftValue = Evaluate(-45f, out _, out _);
            // 단조성을 가정하지 않고 짧은 구간별로 해를 찾은 뒤 원본에 가장 가까운 각도를 선택함.
            for (int step = -44; step <= 45; step++)
            {
                float rightValue = Evaluate(step, out _, out _);
                if (float.IsNaN(leftValue) || float.IsNaN(rightValue)) return false;
                if (leftValue * rightValue <= 0f)
                {
                    float left = step - 1f, right = step, value = leftValue;
                    for (int iteration = 0; iteration < 18; iteration++)
                    {
                        float middle = (left + right) * 0.5f;
                        float middleValue = Evaluate(middle, out _, out _);
                        if (value * middleValue <= 0f) right = middle;
                        else { left = middle; value = middleValue; }
                    }
                    float angle = (left + right) * 0.5f;
                    if (Mathf.Abs(angle) < Mathf.Abs(best)) best = angle;
                }
                leftValue = rightValue;
            }
            if (float.IsInfinity(best)) return false;
            Evaluate(best, out rear, out front);
            rotation = Quaternion.AngleAxis(best, axis) * original;
            return true;
        }

        // 추종 지연은 갭 비율로 회수하되 프레임 이동 절대 상한을 넘기지 않음.
        private static Vector3 MoveEmittedAnchor(Vector3 emitted, Vector3 target, float trackStep)
        {
            float step = Mathf.Clamp(Vector3.Distance(emitted, target) * AnchorCatchUpFraction,
                trackStep, trackStep * AnchorCatchUpStepFactor);
            return Vector3.MoveTowards(emitted, target, step);
        }

        private void RecordIssue(int frame, string reason)
        {
            UnresolvedPairCount++;
            if (FirstIssue == null) FirstIssue = $"프레임 {frame}: {reason}";
        }

        private readonly struct Frame
        {
            internal readonly int Point;
            internal readonly Vector3 Anchor;
            internal readonly bool CanAlignPair;
            internal Frame(int point, Vector3 anchor, bool canAlignPair)
            { Point = point; Anchor = anchor; CanAlignPair = canAlignPair; }
        }

        private sealed class Contact
        {
            internal readonly int StartFrame;
            internal int Point;
            internal Vector3 Anchor;
            internal Vector3 SourcePoint;
            internal bool Pinned { get; private set; }
            private Vector3 _pinSource;
            private bool _pinBlocked;

            internal Contact(int startFrame, int point, Vector3 anchor, Vector3 sourcePoint)
            { StartFrame = startFrame; Point = point; Anchor = anchor; SourcePoint = sourcePoint; }

            internal Vector3 GetAnchor(Vector3 source, Quaternion rotation, float scaleRatio) =>
                Anchor + Vector3.ProjectOnPlane(rotation * (source - SourcePoint), Vector3.up) * scaleRatio;

            // 확정 Plant 구간에서는 지금까지 추종한 위치를 진입 시점에 접어 앵커를 고정함.
            // 원본이 핀 시작점에서 임계 이상 움직이면 의도 추정 오류로 보고 남은 수명 동안 핀을 해제함.
            internal void ApplyAnchorPolicy(Vector3 source, Quaternion rotation, float scaleRatio,
                HumanoidFootAnchorPolicy policy, float pinReleaseSourceDistance)
            {
                if (policy != HumanoidFootAnchorPolicy.Pinned || _pinBlocked)
                {
                    Pinned = false;
                    return;
                }
                if (!Pinned)
                {
                    Anchor = GetAnchor(source, rotation, scaleRatio);
                    _pinSource = source;
                    Pinned = true;
                }
                else if (pinReleaseSourceDistance > 0f &&
                    Vector3.ProjectOnPlane(source - _pinSource, Vector3.up).magnitude >=
                        pinReleaseSourceDistance)
                {
                    Pinned = false;
                    _pinBlocked = true;
                    // 해제 순간에도 앵커가 튀지 않게 기준점을 현재 원본으로 넘김.
                    SourcePoint = source;
                }
                // 핀 동안 기준점을 매 프레임 갱신해 GetAnchor가 고정 앵커를 반환하게 함.
                if (Pinned) SourcePoint = source;
            }
        }

        private sealed class Pair
        {
            internal readonly int Primary;
            internal readonly Vector3 SourceDirection;
            internal readonly Vector3 Offset;
            internal Pair(int primary, Vector3 sourceDirection, Vector3 offset)
            { Primary = primary; SourceDirection = sourceDirection; Offset = offset; }
        }
    }
}
#endif
