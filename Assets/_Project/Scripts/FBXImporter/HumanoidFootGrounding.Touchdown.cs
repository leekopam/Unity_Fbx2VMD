using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed partial class HumanoidFootGrounding
    {
        private const int TouchdownSamplesPerFrame = 4;
        private readonly Dictionary<int, TouchdownWindow> _touchdownWindows = new();

        private sealed class TouchdownWindow
        {
            internal readonly Leg Leg;
            internal readonly Leg SupportingLeg;
            internal readonly int Start;
            internal readonly int Full;
            internal readonly int Channel;
            internal readonly int Point;
            internal readonly Vector3 Anchor;
            internal readonly Vector3 Correction;
            internal readonly RaycastHit Ground;
            internal readonly Matrix4x4 GroundMatrix;
            internal readonly RaycastHit SupportingGround;
            internal readonly Matrix4x4 SupportingGroundMatrix;

            internal TouchdownWindow(Leg leg, Leg supportingLeg, int start, int full, int channel, int point,
                Vector3 anchor, Vector3 correction, RaycastHit ground, RaycastHit supportingGround)
            {
                Leg = leg;
                SupportingLeg = supportingLeg;
                Start = start;
                Full = full;
                Channel = channel;
                Point = point;
                Anchor = anchor;
                Correction = correction;
                Ground = ground;
                GroundMatrix = ground.collider.transform.localToWorldMatrix;
                SupportingGround = supportingGround;
                SupportingGroundMatrix = supportingGround.collider.transform.localToWorldMatrix;
            }
        }

        internal void PrepareTouchdownReferences(AnimationClip clip, Action<float> evaluate,
            HumanoidGroundResponse ground)
        {
            try
            {
                PrepareTouchdownReferences(_left, _right, clip, evaluate, ground);
                PrepareTouchdownReferences(_right, _left, clip, evaluate, ground);
            }
            finally
            {
                RestoreAppliedPose();
                _left.ResetPreparationHistory();
                _right.ResetPreparationHistory();
                evaluate(0f);
            }
        }

        private void PrepareTouchdownReferences(Leg entering, Leg supporting, AnimationClip clip,
            Action<float> evaluate, HumanoidGroundResponse ground)
        {
            for (int start = 0; start + 2 < entering.WeightFrameCount; start++)
            {
                if (!entering.TryFindTouchdown(start, supporting, out int full, out int channel))
                    continue;

                evaluate(Mathf.Min(full / _frameRate, clip.length));
                ResetEvaluationState();
                entering.CapturePose();
                if (!entering.TryCaptureTouchdown(supporting, start, full, channel, ground, _humanScale, out var window) ||
                    !PreservesTouchdownTransition(window, clip, evaluate, ground))
                    continue;

                for (int frame = start; frame < full; frame++)
                    _touchdownWindows.Add(frame, window);
            }
        }

        private bool PreservesTouchdownTransition(TouchdownWindow window, AnimationClip clip,
            Action<float> evaluate, HumanoidGroundResponse ground)
        {
            // 양 끝의 기준 자세까지 비교하여 보정 창 경계에 튐을 남기지 않음.
            int first = Mathf.Max(0, window.Start - 2);
            int last = Mathf.Min(_left.WeightFrameCount - 1, window.Full + 7);
            var baseline = new TouchdownMotion();
            var candidate = new TouchdownMotion();
            for (int tick = first * TouchdownSamplesPerFrame; tick <= last * TouchdownSamplesPerFrame; tick++)
            {
                float frame = (float)tick / TouchdownSamplesPerFrame;
                bool integer = tick % TouchdownSamplesPerFrame == 0;
                bool checkBoundary = integer && frame > window.Start && frame <= window.Full;
                TouchdownPose beforeBaseline = default, beforeCandidate = default;
                if (checkBoundary && !Sample(frame - 0.001f, out beforeBaseline, out beforeCandidate)) return false;
                if (!Sample(frame, out var currentBaseline, out var currentCandidate)) return false;
                if (checkBoundary && !currentCandidate.PreservesBoundary(beforeCandidate, currentBaseline, beforeBaseline))
                    return false;
                baseline.Add(currentBaseline, integer);
                candidate.Add(currentCandidate, integer);
            }
            return candidate.Improves(baseline);

            bool Sample(float frame, out TouchdownPose baselinePose, out TouchdownPose candidatePose)
            {
                candidatePose = default;
                float time = Mathf.Min(frame / _frameRate, clip.length);
                evaluate(time);
                if (!TryApplyCore(time, ground, window, false, out bool rejected, out baselinePose) || rejected)
                    return false;
                candidatePose = ReadTouchdownPose();
                return true;
            }
        }

        private TouchdownPose ReadTouchdownPose() => new TouchdownPose(_hips.position.y,
            _left.Foot.position, _right.Foot.position, new Vector2(_left.KneeAngle, _right.KneeAngle));

        private readonly struct TouchdownPose
        {
            internal readonly float HipsY;
            internal readonly Vector3 LeftFoot;
            internal readonly Vector3 RightFoot;
            internal readonly Vector2 Knees;

            internal TouchdownPose(float hipsY, Vector3 leftFoot, Vector3 rightFoot, Vector2 knees)
            {
                HipsY = hipsY;
                LeftFoot = leftFoot;
                RightFoot = rightFoot;
                Knees = knees;
            }

            internal bool PreservesBoundary(TouchdownPose before, TouchdownPose baseline, TouchdownPose beforeBaseline)
            {
                const float tolerance = 0.00001f;
                return Vector3.Distance(LeftFoot, before.LeftFoot) <=
                    Vector3.Distance(baseline.LeftFoot, beforeBaseline.LeftFoot) + tolerance &&
                    Vector3.Distance(RightFoot, before.RightFoot) <=
                    Vector3.Distance(baseline.RightFoot, beforeBaseline.RightFoot) + tolerance;
            }
        }

        private struct TouchdownMotion
        {
            private TouchdownPose _previous;
            private TouchdownPose _beforePrevious;
            private TouchdownPose _previousInteger;
            private int _count;
            private bool _hasInteger;
            private float _maxDrop;
            private float _maxHipAcceleration;
            private float _maxKneeAcceleration;
            private float _maxLeftStep;
            private float _maxRightStep;

            internal void Add(TouchdownPose pose, bool integer)
            {
                if (_count > 0) _maxDrop = Mathf.Max(_maxDrop, _previous.HipsY - pose.HipsY);
                if (_count > 1)
                {
                    float acceleration = pose.HipsY - 2f * _previous.HipsY + _beforePrevious.HipsY;
                    Vector2 knees = pose.Knees - 2f * _previous.Knees + _beforePrevious.Knees;
                    _maxHipAcceleration = Mathf.Max(_maxHipAcceleration, Mathf.Abs(acceleration));
                    _maxKneeAcceleration = Mathf.Max(_maxKneeAcceleration,
                        Mathf.Max(Mathf.Abs(knees.x), Mathf.Abs(knees.y)));
                }
                if (integer)
                {
                    if (_hasInteger)
                    {
                        _maxLeftStep = Mathf.Max(_maxLeftStep, Vector3.Distance(_previousInteger.LeftFoot, pose.LeftFoot));
                        _maxRightStep = Mathf.Max(_maxRightStep, Vector3.Distance(_previousInteger.RightFoot, pose.RightFoot));
                    }
                    _previousInteger = pose;
                    _hasInteger = true;
                }
                _beforePrevious = _previous;
                _previous = pose;
                _count++;
            }

            internal bool Improves(TouchdownMotion baseline)
            {
                const float positionTolerance = 0.000001f;
                const float angleTolerance = 0.01f / (TouchdownSamplesPerFrame * TouchdownSamplesPerFrame);
                return _count > 2 && _maxDrop < baseline._maxDrop - positionTolerance &&
                    _maxHipAcceleration <= baseline._maxHipAcceleration + positionTolerance &&
                    _maxKneeAcceleration <= baseline._maxKneeAcceleration + angleTolerance &&
                    _maxLeftStep <= baseline._maxLeftStep + positionTolerance &&
                    _maxRightStep <= baseline._maxRightStep + positionTolerance;
            }
        }

        private bool TryApplyTouchdown(TouchdownWindow window, HumanoidGroundResponse ground,
            Leg.TargetState leftTarget, Leg.TargetState rightTarget, float offset, Vector2 footOffsets,
            out bool rejected)
        {
            rejected = false;
            var leg = window.Leg;
            if (!leg.IsPartialSupport) return true;
            float contactLimit = _humanScale * SoleClearancePerHumanScale;
            if (!_left.TryGetRegionHeights(out var leftHeights) || !_right.TryGetRegionHeights(out var rightHeights))
            {
                rejected = true;
                return true;
            }
            Vector2 heights = leg == _left ? leftHeights : rightHeights;
            float blend = 0.5f * Mathf.SmoothStep(0f, 1f,
                (Mathf.Min(heights.x, heights.y) - contactLimit) / (_humanScale * SupportedContactErrorPerHumanScale));
            if (blend <= 0f) return true;

            Vector3 originalTarget = leg == _left ? leftTarget.Position : rightTarget.Position;
            if (!window.SupportingLeg.HasFullSupportOn(window.SupportingGround, window.SupportingGroundMatrix, contactLimit) ||
                !leg.TryGetTouchdownTarget(window, ground, originalTarget, blend, contactLimit, out Vector3 proposed))
            {
                rejected = true;
                return true;
            }
            if (proposed == originalTarget) return true;

            bool baselineUsedPhysicalReach = UsedPhysicalReach;
            RestoreAppliedPose();
            _left.RestoreTarget(leftTarget);
            _right.RestoreTarget(rightTarget);
            if (leg.TrySetTouchdownTarget(proposed) && TryResolveReach(out float newOffset, out var newFootOffsets) &&
                TrySolveGroundPose(newOffset, newFootOffsets, out _) && RawGatePass &&
                _left.TryGetRegionHeights(out var newLeft) && _right.TryGetRegionHeights(out var newRight) &&
                SameRegionContact(leftHeights, newLeft, contactLimit) && SameRegionContact(rightHeights, newRight, contactLimit))
                return true;

            rejected = true;
            RestoreAppliedPose();
            _left.RestoreTarget(leftTarget);
            _right.RestoreTarget(rightTarget);
            UsedPhysicalReach = baselineUsedPhysicalReach;
            return TrySolveGroundPose(offset, footOffsets, out _);
        }

        private static bool SameRegionContact(Vector2 baseline, Vector2 candidate, float limit) =>
            (baseline.x <= limit) == (candidate.x <= limit) && (baseline.y <= limit) == (candidate.y <= limit);

        private sealed partial class Leg
        {
            internal int WeightFrameCount => _weights.Length;
            internal float KneeAngle => Vector3.Angle(Lower.position - Upper.position, Foot.position - Lower.position);
            internal bool IsPartialSupport => _support > 0f && _support < 1f;

            internal void ResetPreparationHistory()
            {
                _hasPrevTargetRot = false;
                _prevSupport = 0f;
                Diagnostic.Reset();
            }

            internal bool TryFindTouchdown(int start, Leg supporting, out int full, out int channel)
            {
                full = start + 1;
                channel = GetSingleSupportChannel(_weights[full]);
                if (_weights[start] != Vector2.zero || channel < 0 || _weights[full][channel] >= 0.999f)
                    return false;

                var contact = _plan.GetSample(channel, full);
                if (!contact.HasValue) return false;
                for (; full < _weights.Length; full++)
                {
                    var sample = _plan.GetSample(channel, full);
                    var other = supporting._weights[full];
                    if (GetSingleSupportChannel(_weights[full]) != channel ||
                        _weights[full][channel] <= _weights[full - 1][channel] ||
                        Mathf.Max(other.x, other.y) < 0.999f || !sample.HasValue ||
                        sample.FirstPoint != contact.FirstPoint)
                        return false;
                    if (_weights[full][channel] >= 0.999f) return true;
                }
                return false;
            }

            internal bool TryCaptureTouchdown(Leg supporting, int start, int full, int channel,
                HumanoidGroundResponse ground, float humanScale, out TouchdownWindow window)
            {
                window = null;
                if (!TryCalculateTarget(full, ground, humanScale) || !HasGround || _support < 0.999f ||
                    Diagnostic.mixed_ground_collider || !NeedsPelvisLowering())
                    return false;

                supporting.CapturePose();
                if (!supporting.TryCalculateTarget(full, ground, humanScale) || !supporting.HasGround ||
                    supporting._support < 0.999f || supporting.Diagnostic.mixed_ground_collider)
                    return false;

                var sample = _activeContacts[channel];
                window = new TouchdownWindow(this, supporting, start, full, channel, sample.FirstPoint, sample.Anchor,
                    _target - _originalFootPosition, _ground, supporting._ground);
                return true;
            }

            internal bool HasFullSupportOn(RaycastHit ground, Matrix4x4 matrix, float contactLimit) =>
                HasGround && _support >= 0.999f && !Diagnostic.mixed_ground_collider &&
                MatchesReferenceGround(_ground, ground, contactLimit) && _ground.collider.transform.localToWorldMatrix == matrix;

            internal bool TryGetTouchdownTarget(TouchdownWindow window, HumanoidGroundResponse ground,
                Vector3 originalTarget, float blend, float contactLimit, out Vector3 candidate)
            {
                candidate = originalTarget;
                if (!HasGround || GetSingleSupportChannel(_activeWeights) != window.Channel ||
                    _activeContacts[window.Channel].FirstPoint != window.Point || Diagnostic.mixed_ground_collider ||
                    !MatchesReferenceGround(_ground, window.Ground, contactLimit) ||
                    _ground.collider.transform.localToWorldMatrix != window.GroundMatrix ||
                    !ground.TryFindGround(window.Anchor, out RaycastHit anchor) ||
                    !MatchesReferenceGround(anchor, window.Ground, contactLimit))
                    return false;

                candidate = Vector3.Lerp(originalTarget, _originalFootPosition + window.Correction * _support, blend);
                return ground.TryFindGround(candidate, out RaycastHit candidateHit) &&
                    MatchesReferenceGround(candidateHit, window.Ground, contactLimit);
            }

            internal bool TrySetTouchdownTarget(Vector3 candidate) => Sampler.TrySample() &&
                HumanoidSoleGroundConstraint.TryLiftAbovePlane(candidate, _targetFootRotation, Sampler.LocalSolePoints,
                    Foot.lossyScale.x, _ground.point, _ground.normal, out Vector3 constrained, out _) &&
                TrySetReleaseTarget(constrained);

            internal bool TryGetRegionHeights(out Vector2 heights)
            {
                heights = default;
                if (!HasGround || !Sampler.TrySelectContact(false, _ground.normal, out _, out Vector3 rear) ||
                    !Sampler.TrySelectContact(true, _ground.normal, out _, out Vector3 front))
                    return false;
                heights = new Vector2(Vector3.Dot(rear - _ground.point, _ground.normal),
                    Vector3.Dot(front - _ground.point, _ground.normal));
                return true;
            }

            private bool NeedsPelvisLowering()
            {
                Vector3 targetToHip = Upper.position - _target;
                float reachSquared = _maximumReach * _maximumReach;
                float horizontalSquared = targetToHip.x * targetToHip.x + targetToHip.z * targetToHip.z;
                return targetToHip.y > 0f && horizontalSquared < reachSquared && targetToHip.sqrMagnitude > reachSquared;
            }
        }
    }
}
