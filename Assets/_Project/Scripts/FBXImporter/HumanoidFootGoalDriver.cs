using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 원본 발 목표(goal)로 타겟 다리를 IK 구동하는 실험 경로임.
    /// useIkGoalFootDrive 플래그로만 진입하고 alpha=0이면 어떤 본도 건드리지 않음.
    /// pin이 확정된 프레임만 목표를 주장하고 나머지는 기존 경로 출력을 그대로 둠.
    /// </summary>
    internal sealed class HumanoidFootGoalDriver
    {
        // UE Squash&Stretch의 신장 상한 — 이 비율을 넘는 목표는 골반 드롭이 흡수함.
        internal const float MaximumExtensionRatio = 1.05f;
        // 앞꿈치 지지 발끝 하향 soft-cap — HumanoidFootGrounding.Leg과 동일값.
        internal const float ToePitchSoftKneeDeg = 25f;
        internal const float MaximumSupportedToePitch = 35f;
        // 무릎 과신전을 막는 신전 감쇠 경계 — 생산 경로의 감쇠 임계와 동일함.
        internal const float ExtensionDampingThresholdRadians = 2.8f;
        // pin·goal 전이의 UE FInterpTo식 지수 평활 속도(1/s).
        internal const float PinSmoothingSpeed = 12f;
        internal const float GoalPositionSmoothingSpeed = 18f;
        private const float MinimumBendNormalSquaredMagnitude = 0.00000001f;

        internal bool IsInitialized { get; private set; }

        private Animator _animator;
        private Transform _hips;
        private float _frameRate;
        private float _clipLengthSeconds;
        private float _scaleRatio = 1f;
        private HumanoidFootGoalStream _goals;
        // 지면 높이 두 벌 — 소스 월드값은 게이트 증거용, 골반 상대값은 goal 스냅용.
        private float[] _leftFloorWorldY = Array.Empty<float>();
        private float[] _rightFloorWorldY = Array.Empty<float>();
        private float[] _leftFloorRootSpaceY = Array.Empty<float>();
        private float[] _rightFloorRootSpaceY = Array.Empty<float>();
        private readonly LegDrive _left = new LegDrive();
        private readonly LegDrive _right = new LegDrive();
        private float _lastTimeSeconds = float.NaN;

        // stabilizer가 보존한 원본 궤적·의도·표식에서 goal 스트림과 핀 구간을 전부 선계산함.
        // 반환 false는 "데이터 부족으로 실험 경로 비활성"이며 예외가 아님.
        internal bool TryInitialize(
            Animator targetAnimator,
            HumanoidFootContactStabilizer stabilizer,
            float frameRate)
        {
            Clear();
            if (targetAnimator == null || stabilizer == null || !stabilizer.IsInitialized ||
                frameRate <= 0f)
            {
                return false;
            }

            IReadOnlyList<HumanoidFootContactSample> samples = stabilizer.SourceSamples;
            IReadOnlyList<Vector3> hipsPositions = stabilizer.SourceHipsPositions;
            IReadOnlyList<Quaternion> hipsRotations = stabilizer.SourceHipsRotations;
            if (samples == null || hipsPositions == null || hipsRotations == null ||
                samples.Count == 0 || hipsPositions.Count != samples.Count ||
                hipsRotations.Count != samples.Count)
            {
                return false;
            }

            return TryInitializeCore(targetAnimator, samples, hipsPositions, hipsRotations,
                stabilizer.IntentEstimate, stabilizer.IntentLabels,
                stabilizer.SourceHumanScale, frameRate);
        }

        // stabilizer에서 떼어낸 데이터 주입 경로 — EditMode 검증과 재사용을 분리함.
        internal bool TryInitializeCore(
            Animator targetAnimator,
            IReadOnlyList<HumanoidFootContactSample> samples,
            IReadOnlyList<Vector3> hipsPositions,
            IReadOnlyList<Quaternion> hipsRotations,
            HumanoidFootContactIntentEstimate intents,
            HumanoidFootContactIntentLabelSet labels,
            float sourceHumanScale,
            float frameRate)
        {
            Clear();
            if (targetAnimator == null || samples == null ||
                hipsPositions == null || hipsRotations == null ||
                samples.Count == 0 || hipsPositions.Count != samples.Count ||
                hipsRotations.Count != samples.Count || frameRate <= 0f)
            {
                return false;
            }

            _animator = targetAnimator;
            _hips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (_hips == null ||
                !_left.Initialize(targetAnimator, true) ||
                !_right.Initialize(targetAnimator, false))
            {
                Clear();
                return false;
            }

            _frameRate = frameRate;
            _clipLengthSeconds = (samples.Count - 1) / frameRate;
            _scaleRatio = sourceHumanScale > 0.0001f
                ? targetAnimator.humanScale / sourceHumanScale
                : 1f;
            _goals = BuildGoalStream(samples, hipsPositions, hipsRotations);
            BuildPinArrays(samples, hipsPositions, hipsRotations, intents,
                labels, frameRate, sourceHumanScale);
            IsInitialized = true;
            return true;
        }

        internal void Clear()
        {
            IsInitialized = false;
            _animator = null;
            _hips = null;
            _frameRate = 0f;
            _clipLengthSeconds = 0f;
            _scaleRatio = 1f;
            _goals = null;
            _leftFloorWorldY = _rightFloorWorldY = Array.Empty<float>();
            _leftFloorRootSpaceY = _rightFloorRootSpaceY = Array.Empty<float>();
            _left.Clear();
            _right.Clear();
            _lastTimeSeconds = float.NaN;
        }

        // goal 스트림은 stabilizer의 원본 샘플과 같은 인덱스 — 관측 산출물과 동일 데이터임.
        private static HumanoidFootGoalStream BuildGoalStream(
            IReadOnlyList<HumanoidFootContactSample> samples,
            IReadOnlyList<Vector3> hipsPositions,
            IReadOnlyList<Quaternion> hipsRotations)
        {
            var stream = new HumanoidFootGoalStream();
            for (int index = 0; index < samples.Count; index++)
            {
                HumanoidFootContactSample sample = samples[index];
                stream.Record(index, true, hipsPositions[index], hipsRotations[index],
                    sample.LeftFoot, sample.LeftFootFrame ?? Quaternion.identity, sample.LeftToes);
                stream.Record(index, false, hipsPositions[index], hipsRotations[index],
                    sample.RightFoot, sample.RightFootFrame ?? Quaternion.identity, sample.RightToes);
            }
            return stream;
        }

        // pin·grounded 판정을 프레임 배열로 선계산 — 탐색(seek) 시에도 게이트 상태가
        // 시퀀스 오염 없이 재현됨. 증거는 전부 원본 공간 측정값임.
        private void BuildPinArrays(
            IReadOnlyList<HumanoidFootContactSample> samples,
            IReadOnlyList<Vector3> hipsPositions,
            IReadOnlyList<Quaternion> hipsRotations,
            HumanoidFootContactIntentEstimate intents,
            HumanoidFootContactIntentLabelSet labels,
            float frameRate, float sourceHumanScale)
        {
            _leftFloorWorldY = BuildFloorHeights(samples, intents?.Left, true);
            _rightFloorWorldY = BuildFloorHeights(samples, intents?.Right, false);
            _leftFloorRootSpaceY = ToRootSpaceFloorY(_leftFloorWorldY,
                hipsPositions, hipsRotations);
            _rightFloorRootSpaceY = ToRootSpaceFloorY(_rightFloorWorldY,
                hipsPositions, hipsRotations);
            _left.Pin = new bool[samples.Count];
            _left.Grounded = new bool[samples.Count];
            _right.Pin = new bool[samples.Count];
            _right.Grounded = new bool[samples.Count];
            float scale = sourceHumanScale > 0.0001f ? sourceHumanScale : 1f;
            var leftGate = new HumanoidFootGoalGate(frameRate, scale);
            var rightGate = new HumanoidFootGoalGate(frameRate, scale);
            for (int frame = 0; frame < samples.Count; frame++)
            {
                EvaluateSide(samples, frame, true, intents, labels, leftGate,
                    _leftFloorWorldY, frameRate, _left.Pin, _left.Grounded);
                EvaluateSide(samples, frame, false, intents, labels, rightGate,
                    _rightFloorWorldY, frameRate, _right.Pin, _right.Grounded);
            }
        }

        private static void EvaluateSide(
            IReadOnlyList<HumanoidFootContactSample> samples, int frame, bool isLeft,
            HumanoidFootContactIntentEstimate intents,
            HumanoidFootContactIntentLabelSet labels,
            HumanoidFootGoalGate gate, float[] floorWorldY, float frameRate,
            bool[] pin, bool[] grounded)
        {
            bool confident = false, slide = false, uncertain = false;
            IReadOnlyList<HumanoidFootContactIntent> sideIntents =
                isLeft ? intents?.Left : intents?.Right;
            if (sideIntents != null)
            {
                foreach (HumanoidFootContactIntent intent in sideIntents)
                {
                    if (frame < intent.StartFrame || frame >= intent.EndFrameExclusive) continue;
                    if (intent.Certainty == HumanoidFootContactIntentCertainty.Uncertain)
                    {
                        uncertain = true;
                    }
                    else if (intent.Mode == HumanoidFootContactIntentMode.Plant)
                    {
                        confident = true;
                    }
                    else
                    {
                        slide = true;
                    }
                }
            }
            IReadOnlyList<Vector2Int> uncertainSpans =
                isLeft ? intents?.LeftUncertainSpans : intents?.RightUncertainSpans;
            if (uncertainSpans != null)
            {
                foreach (Vector2Int span in uncertainSpans)
                {
                    if (frame >= span.x && frame < span.y) uncertain = true;
                }
            }

            bool? labelSupport = null;
            IReadOnlyList<HumanoidFootContactIntentLabel> sideLabels =
                isLeft ? labels?.Left : labels?.Right;
            if (sideLabels != null)
            {
                foreach (HumanoidFootContactIntentLabel label in sideLabels)
                {
                    if (frame >= label.StartFrame && frame <= label.EndFrameInclusive)
                    {
                        labelSupport = label.IsSupport;
                    }
                }
            }

            HumanoidFootContactSample sample = samples[frame];
            Vector3 foot = isLeft ? sample.LeftFoot : sample.RightFoot;
            Vector3 toes = isLeft ? sample.LeftToes : sample.RightToes;
            Vector3 prevFoot = frame > 0
                ? (isLeft ? samples[frame - 1].LeftFoot : samples[frame - 1].RightFoot)
                : foot;
            Vector3 planarDelta = foot - prevFoot;
            planarDelta.y = 0f;
            var evidence = new HumanoidFootGoalGate.FrameEvidence
            {
                HasConfidentPlantIntent = confident,
                HasSlideIntent = slide,
                InUncertainSpan = uncertain,
                LabelIsSupport = labelSupport,
                ToeHeightAboveFloor = toes.y - floorWorldY[frame],
                AnkleSpeed = planarDelta.magnitude * frameRate,
            };
            HumanoidFootGoalGate.GateOutput output = gate.Evaluate(evidence);
            pin[frame] = output.Pin > 0.5f;
            grounded[frame] = output.Grounded;
        }

        // 의도 앵커가 덮는 구간은 그 앵커 높이를, 나머지는 클립 최저 발끝 높이를 지면으로 씀.
        private static float[] BuildFloorHeights(
            IReadOnlyList<HumanoidFootContactSample> samples,
            IReadOnlyList<HumanoidFootContactIntent> intents, bool isLeft)
        {
            var floor = new float[samples.Count];
            float clipMinimum = float.PositiveInfinity;
            for (int frame = 0; frame < samples.Count; frame++)
            {
                clipMinimum = Mathf.Min(clipMinimum,
                    (isLeft ? samples[frame].LeftToes : samples[frame].RightToes).y);
            }
            for (int frame = 0; frame < samples.Count; frame++)
            {
                floor[frame] = clipMinimum;
            }
            if (intents != null)
            {
                foreach (HumanoidFootContactIntent intent in intents)
                {
                    if (intent.Mode != HumanoidFootContactIntentMode.Plant) continue;
                    for (int frame = Math.Max(0, intent.StartFrame);
                         frame < Math.Min(samples.Count, intent.EndFrameExclusive); frame++)
                    {
                        floor[frame] = intent.Anchor.y;
                    }
                }
            }
            return floor;
        }

        // 월드 지면 높이를 프레임별 골반 상대 공간으로 변환 — goal 스냅 좌표계와 일치시킴.
        private static float[] ToRootSpaceFloorY(float[] floorWorldY,
            IReadOnlyList<Vector3> hipsPositions, IReadOnlyList<Quaternion> hipsRotations)
        {
            var rootSpace = new float[floorWorldY.Length];
            for (int frame = 0; frame < floorWorldY.Length; frame++)
            {
                Vector3 floorWorld = new Vector3(hipsPositions[frame].x,
                    floorWorldY[frame], hipsPositions[frame].z);
                rootSpace[frame] = (Quaternion.Inverse(hipsRotations[frame]) *
                    (floorWorld - hipsPositions[frame])).y;
            }
            return rootSpace;
        }

        // alpha=0이면 아무것도 하지 않음 — 기존 경로와 비트 동일한 출력의 근거임.
        internal bool TryApply(float timeSeconds, float alpha,
            Vector3 leftBendNormal, Vector3 rightBendNormal)
        {
            if (!IsInitialized || alpha <= 0f)
            {
                return true;
            }

            int frame = HumanoidMotionFrameCalculator.CalculateFrameIndex(
                timeSeconds, _clipLengthSeconds, _frameRate);
            // 탐색 점프는 스무딩 상태를 리셋 — 이전 프레임 잔여가 새 위치로 번지지 않게 함.
            bool jumped = !float.IsNaN(_lastTimeSeconds) &&
                Mathf.Abs(timeSeconds - _lastTimeSeconds) > 1.5f / _frameRate;
            float dt = float.IsNaN(_lastTimeSeconds) || jumped ? 0f :
                Mathf.Max(0f, timeSeconds - _lastTimeSeconds);
            _lastTimeSeconds = timeSeconds;

            _left.UpdateWeights(alpha, _left.Pin[frame], jumped, dt);
            _right.UpdateWeights(alpha, _right.Pin[frame], jumped, dt);
            if (_left.Weight <= 0f && _right.Weight <= 0f)
            {
                return true;
            }

            // 골반 드롭은 양다리 공유 — 한쪽이라도 목표가 신장 한계를 넘으면 함께 계산함.
            float pelvisOffset = CalculatePelvisOffset(frame);
            if (Mathf.Abs(pelvisOffset) > 0f)
            {
                _hips.position += Vector3.up * (pelvisOffset * Mathf.Max(_left.Weight, _right.Weight));
            }

            _left.TrySolve(frame, _goals.Samples, _hips, _scaleRatio,
                _leftFloorRootSpaceY[frame], leftBendNormal, dt);
            _right.TrySolve(frame, _goals.Samples, _hips, _scaleRatio,
                _rightFloorRootSpaceY[frame], rightBendNormal, dt);
            return true;
        }

        private float CalculatePelvisOffset(int frame)
        {
            bool needsLeft = TryBuildReachLeg(_left, frame,
                out HumanoidPelvisReachCalculator.Leg left);
            bool needsRight = TryBuildReachLeg(_right, frame,
                out HumanoidPelvisReachCalculator.Leg right);
            if (!needsLeft && !needsRight)
            {
                return 0f;
            }
            if (!needsLeft) left = PassiveLeg(_left);
            if (!needsRight) right = PassiveLeg(_right);
            // 골반이 목표를 쫓아 올라가지 않게 상방 이동은 0으로 묶음.
            return HumanoidPelvisReachCalculator.TryCalculateOffset(
                left, right, Vector3.up, _animator.humanScale, out float offset, out _)
                ? offset : 0f;
        }

        // weight가 0인 다리는 현재 발 위치를 목표로 둬 골반 계산이 그쪽을 억지로 내리지 않게 함.
        private static HumanoidPelvisReachCalculator.Leg PassiveLeg(LegDrive leg)
        {
            Vector3 toHip = leg.Upper.position - leg.Foot.position;
            return new HumanoidPelvisReachCalculator.Leg(toHip,
                leg.UpperLength, leg.LowerLength, 0f, 0f);
        }

        private bool TryBuildReachLeg(LegDrive leg, int frame,
            out HumanoidPelvisReachCalculator.Leg reachLeg)
        {
            reachLeg = default;
            if (leg.Weight <= 0f ||
                !TryGetGoalSample(frame, leg.IsLeft, out HumanoidFootGoalStream.GoalSample goal))
            {
                return false;
            }

            Vector3 goalWorld = GoalToWorld(goal, _hips, _scaleRatio);
            Vector3 toHip = leg.Upper.position - goalWorld;
            float distance = toHip.magnitude;
            float extensionReach = leg.Reach * MaximumExtensionRatio;
            if (distance <= extensionReach * 0.999f)
            {
                // UE식 5% 신장 이내면 골반을 내리지 않음 — IK가 직접 도달함.
                return false;
            }
            float kneeAngle = Vector3.Angle(
                leg.Upper.position - leg.Lower.position,
                leg.Foot.position - leg.Lower.position) * Mathf.Deg2Rad;
            float dampedReach = leg.Reach;
            HumanoidPelvisReachCalculator.TryCalculateExtensionReach(
                leg.UpperLength, leg.LowerLength, kneeAngle, distance,
                leg.Weight, ExtensionDampingThresholdRadians, out dampedReach);
            reachLeg = new HumanoidPelvisReachCalculator.Leg(toHip,
                leg.UpperLength, leg.LowerLength, leg.Weight, 0f,
                Math.Max(dampedReach, extensionReach * 0.9999f));
            return true;
        }

        private bool TryGetGoalSample(int frame, bool isLeft,
            out HumanoidFootGoalStream.GoalSample goal)
        {
            int index = frame * 2 + (isLeft ? 0 : 1);
            if (_goals == null || index < 0 || index >= _goals.Samples.Count)
            {
                goal = default;
                return false;
            }
            goal = _goals.Samples[index];
            return true;
        }

        // 골반 상대 goal을 타겟 월드로 변환 — 골반 프레임을 매개로 루트 모션을 따라감.
        internal static Vector3 GoalToWorld(HumanoidFootGoalStream.GoalSample goal,
            Transform hips, float scaleRatio)
        {
            return hips.position + hips.rotation * (goal.RootSpaceFootPosition * scaleRatio);
        }

        internal static Quaternion GoalFrameToWorld(HumanoidFootGoalStream.GoalSample goal,
            Transform hips)
        {
            return (hips.rotation * goal.RootSpaceFootRotation).normalized;
        }

        // FInterpTo식 지수 평활 — dt=0이면 현재값을 그대로 유지함.
        internal static float Smooth(float current, float target, float speed, float dt)
        {
            return target + (current - target) * Mathf.Exp(-speed * dt);
        }

        internal static Vector3 Smooth(Vector3 current, Vector3 target, float speed, float dt)
        {
            return target + (current - target) * Mathf.Exp(-speed * dt);
        }

        // 앞꿈치 지지 발끝 하향 soft-cap — 생산 경로의 tanh 압축과 동일 수학임.
        internal static Quaternion ApplyToePitchSoftCap(Quaternion footFrame,
            Vector3 groundNormal, float weight)
        {
            Vector3 forward = footFrame * Vector3.forward;
            float pitch = Mathf.Asin(Mathf.Clamp(
                Vector3.Dot(forward, groundNormal), -1f, 1f)) * Mathf.Rad2Deg;
            if (pitch >= -ToePitchSoftKneeDeg)
            {
                return footFrame;
            }
            float span = MaximumSupportedToePitch - ToePitchSoftKneeDeg;
            float limited = -(ToePitchSoftKneeDeg +
                span * (float)Math.Tanh((-pitch - ToePitchSoftKneeDeg) / span));
            Vector3 axis = Vector3.Cross(groundNormal, forward);
            if (axis.sqrMagnitude < 0.5f)
            {
                return footFrame;
            }
            Quaternion corrected = Quaternion.AngleAxis(pitch - limited, axis.normalized) * footFrame;
            return Quaternion.Slerp(footFrame, corrected, weight);
        }

        private sealed class LegDrive
        {
            internal bool IsLeft { get; private set; }
            internal Transform Upper { get; private set; }
            internal Transform Lower { get; private set; }
            internal Transform Foot { get; private set; }
            internal float UpperLength { get; private set; }
            internal float LowerLength { get; private set; }
            internal float Reach => UpperLength + LowerLength;
            internal float Weight { get; private set; }
            internal bool[] Pin = Array.Empty<bool>();
            internal bool[] Grounded = Array.Empty<bool>();

            private HumanoidFootRotationBinding _binding;
            private Vector3 _smoothedGoal;
            private bool _hasSmoothedGoal;

            internal bool Initialize(Animator animator, bool isLeft)
            {
                Clear();
                IsLeft = isLeft;
                Upper = animator.GetBoneTransform(
                    isLeft ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                Lower = animator.GetBoneTransform(
                    isLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                Foot = animator.GetBoneTransform(
                    isLeft ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                Transform toes = animator.GetBoneTransform(
                    isLeft ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
                if (Upper == null || Lower == null || Foot == null ||
                    !Lower.IsChildOf(Upper) || !Foot.IsChildOf(Lower))
                {
                    return false;
                }
                UpperLength = Vector3.Distance(Upper.position, Lower.position);
                LowerLength = Vector3.Distance(Lower.position, Foot.position);
                HumanoidFootRotationBinding.TryCreate(Foot, toes, animator.transform.up,
                    out _binding);
                return UpperLength > 0f && LowerLength > 0f;
            }

            internal void UpdateWeights(float alpha, bool pinned,
                bool jumped, float dt)
            {
                float target = alpha * (pinned ? 1f : 0f);
                if (jumped || dt <= 0f)
                {
                    Weight = target;
                    _hasSmoothedGoal = Weight > 0.001f;
                    return;
                }
                Weight = Smooth(Weight, target, PinSmoothingSpeed, dt);
                // weight가 소실되면 다음 pin 재진입 때 새 목표에서 다시 정박함 —
                // 이전 구간의 평활 잔여가 슬라이드로 보이지 않게 함.
                if (Weight < 0.001f && target <= 0f)
                {
                    Weight = 0f;
                    _hasSmoothedGoal = false;
                }
            }

            internal void TrySolve(int frame,
                IReadOnlyList<HumanoidFootGoalStream.GoalSample> samples,
                Transform hips, float scaleRatio, float floorRootSpaceY,
                Vector3 bendNormal, float dt)
            {
                if (Weight <= 0f)
                {
                    return;
                }

                int index = frame * 2 + (IsLeft ? 0 : 1);
                if (index < 0 || index >= samples.Count)
                {
                    return;
                }

                HumanoidFootGoalStream.GoalSample goal = samples[index];
                if (Grounded[frame])
                {
                    // pin 구간의 지면 스냅 — 목표 발끝이 추정 지면 아래로 가지 않음.
                    goal = HumanoidFootGoalStream.SnapAboveGround(goal, floorRootSpaceY);
                }

                Vector3 goalWorld = GoalToWorld(goal, hips, scaleRatio);
                if (!_hasSmoothedGoal)
                {
                    _smoothedGoal = goalWorld;
                    _hasSmoothedGoal = true;
                }
                else
                {
                    _smoothedGoal = Smooth(_smoothedGoal, goalWorld,
                        GoalPositionSmoothingSpeed, dt);
                }

                Quaternion upperRotation = Upper.rotation;
                Quaternion lowerRotation = Lower.rotation;
                Quaternion footRotation = Foot.rotation;
                if (bendNormal.sqrMagnitude <= MinimumBendNormalSquaredMagnitude)
                {
                    bendNormal = Vector3.Cross(
                        Lower.position - Upper.position,
                        Foot.position - Lower.position);
                }
                if (HumanoidLegPoseSolver.TrySolve(Upper, Lower, Foot,
                        _smoothedGoal, bendNormal, out _) && Weight < 1f)
                {
                    Upper.rotation = Quaternion.Slerp(upperRotation, Upper.rotation, Weight);
                    Lower.rotation = Quaternion.Slerp(lowerRotation, Lower.rotation, Weight);
                    Foot.rotation = Quaternion.Slerp(footRotation, Foot.rotation, Weight);
                }

                // 발 회전 — goal의 해부 프레임을 그대로 쓰되 pin 구간은 지면 정렬+soft-cap.
                if (_binding == null || !_binding.TryCaptureWorldFrame(out Quaternion currentFrame))
                {
                    return;
                }
                Quaternion goalFrame = GoalFrameToWorld(goal, hips);
                if (Grounded[frame])
                {
                    goalFrame = AlignSoleToGround(goalFrame, Vector3.up,
                        goal.RootSpaceFootPosition.y - goal.RootSpaceToesPosition.y);
                    goalFrame = ApplyToePitchSoftCap(goalFrame, Vector3.up, 1f);
                }
                Quaternion blended = Quaternion.Slerp(currentFrame, goalFrame, Weight);
                _binding.TryApplyWorldFrame(blended);
            }

            // 발목-발끝 높이차로 평지 정렬을 가중 — 발끝 지지(롤 스탠스)는 발끝 방향을 유지함.
            private static Quaternion AlignSoleToGround(Quaternion frame, Vector3 groundNormal,
                float soleOffset)
            {
                float flatWeight = Mathf.Clamp01(1f - Mathf.Abs(soleOffset) / 0.08f);
                if (flatWeight <= 0f)
                {
                    return frame;
                }
                Vector3 up = frame * Vector3.up;
                Quaternion aligned = Quaternion.FromToRotation(up, groundNormal) * frame;
                return Quaternion.Slerp(frame, aligned, flatWeight);
            }

            internal void Clear()
            {
                Upper = Lower = Foot = null;
                _binding = null;
                Weight = 0f;
                Pin = Grounded = Array.Empty<bool>();
                _hasSmoothedGoal = false;
                _smoothedGoal = Vector3.zero;
            }
        }
    }
}
