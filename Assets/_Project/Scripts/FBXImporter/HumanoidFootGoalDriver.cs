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
        // 앞꿈치 지지 발끝 하향 soft-cap — HumanoidFootGrounding.Leg과 동일값.
        internal const float ToePitchSoftKneeDeg = 25f;
        internal const float MaximumSupportedToePitch = 35f;
        // 무릎 과신전을 막는 신전 감쇠 경계 — 생산 경로의 감쇠 임계와 동일함.
        internal const float ExtensionDampingThresholdRadians = 2.8f;
        // goal 전이의 UE FInterpTo식 지수 평활 속도(1/s).
        internal const float GoalPositionSmoothingSpeed = 18f;
        // 핀 가중의 선형 전이 속도(1/s) — 지수 감쇠는 첫 프레임에 33%가 움직여
        // gate↔드라이버 자세 갭(수십mm)이 한계를 넘는 단발 스텝으로 노출됨.
        // 고정 속도 선형이면 프레임당 스텝이 갭/전이 프레임 수로 균일하게 상한 아래가 됨.
        internal const float PinWeightChangePerSecond = 3.5f;
        // 이 각도 이하의 무릎 굽힘은 bend normal이 수치적으로 불안정해 마지막 유효값을 유지함.
        private const float MinimumStableBendSine = 0.0175f;
        // 밑창 지면 정렬의 피치 가중 구간 — 이 각도 이상 기울면 롤 스탠스로 간주해 정렬하지 않음.
        private const float SoleAlignPitchSpanDeg = 45f;
        private const float MinimumAxisSquaredMagnitude = 0.000001f;

        internal bool IsInitialized { get; private set; }
        // 마지막 TryApply가 실제로 골반에 적용한 수직 오프셋(m). 진단 열 기록용임.
        internal float LastAppliedPelvisOffset { get; private set; }
        // 테스트 진단용 — 마지막 solve에 사용된 왼쪽 다리 굽힘 법선·평활 목표.
        internal Vector3 DebugLeftBendNormal => _left.LastBendNormal;
        internal Vector3 DebugLeftSmoothedGoal => _left.LastSmoothedGoal;
        internal Vector3 DebugLeftSolveGoal => _left.LastSolveGoal;
        internal Vector3 DebugLeftPreUpper => _left.DebugPreUpperPosition;
        internal Vector3 DebugLeftPreLower => _left.DebugPreLowerPosition;
        internal Vector3 DebugLeftPostLower => _left.DebugPostLowerPosition;
        internal Vector3 DebugLeftDesiredKnee => _left.DebugDesiredKnee;
        internal Vector3 LastUsedLeftBendNormal => _left.LastUsedBendNormal;
        internal string DebugLeftSolveInputs => _left.DebugSolveInputs;
        internal bool DebugLeftSolveSucceeded => _left.DebugSolveSucceeded;
        internal float DebugLeftTargetError => _left.DebugTargetError;
        // 실제 경로에서 밑창 프로브가 배선됐고 lift가 발화하는지 진단용.
        internal int DebugLeftSoleProbeCount => _left.DebugSoleProbeCount;
        internal int DebugRightSoleProbeCount => _right.DebugSoleProbeCount;
        internal float DebugLeftLastLift => _left.DebugLastLift;
        internal float DebugRightLastLift => _right.DebugLastLift;
        internal float DebugLeftLastLowestProbe => _left.DebugLastLowestProbe;
        internal float DebugRightLastLowestProbe => _right.DebugLastLowestProbe;
        // 캡처 경로에서 핀 가중치가 실제로 올라가는지 진단용.
        internal float DebugLeftWeight => _left.Weight;
        internal float DebugRightWeight => _right.Weight;
        // reach 계산이 현재 프레임에 앵커를 쓰는지 진단용 — 해제 감쇠·weight 소실
        // 뒤에도 true면 다음 재진입의 골반 계산이 오래된 앵커로 오염됨.
        internal bool DebugLeftPinAnchorInReach => _left.TryGetPinAnchor(out _);
        internal float[] DebugLeftFloorWorldY => _leftFloorWorldY;
        internal float[] DebugRightFloorWorldY => _rightFloorWorldY;
        internal bool[] DebugLeftPinArray => _left.Pin;
        internal bool[] DebugRightPinArray => _right.Pin;
        internal bool DebugRightSolveSucceeded => _right.DebugSolveSucceeded;
        internal Vector3 DebugRightDesiredKnee => _right.DebugDesiredKnee;
        internal Vector3 LastUsedRightBendNormal => _right.LastUsedBendNormal;
        internal string DebugRightSolveInputs => _right.DebugSolveInputs;
        internal float DebugRightTargetError => _right.DebugTargetError;
        internal float DebugRightReferenceWeight => _right.DebugReferenceWeight;
        internal Vector3 DebugRightProjectedReference => _right.DebugProjectedReference;
        internal float DebugLeftReferenceWeight => _left.DebugReferenceWeight;
        internal Vector3 DebugLeftProjectedReference => _left.DebugProjectedReference;
        internal Vector3 DebugLeftPreFoot => _left.DebugPreFootPosition;
        internal Vector3 DebugLeftPostBlendLower => _left.DebugPostBlendLowerPosition;
        internal Vector3 DebugLeftPostBlendFoot => _left.DebugPostBlendFootPosition;
        internal Vector3 DebugRightPreFoot => _right.DebugPreFootPosition;
        internal Vector3 DebugRightPreUpper => _right.DebugPreUpperPosition;
        internal Vector3 DebugRightPreLower => _right.DebugPreLowerPosition;
        internal Vector3 DebugRightPostLower => _right.DebugPostLowerPosition;
        internal Vector3 DebugRightPostBlendLower => _right.DebugPostBlendLowerPosition;
        internal Vector3 DebugRightPostBlendFoot => _right.DebugPostBlendFootPosition;
        // 골반 오프셋 계산에 들어간 양다리 입력(무릎각·거리·감쇠 도달·필요 플래그).
        internal string DebugPelvisInputs { get; private set; } = string.Empty;

        private Animator _animator;
        private Transform _hips;
        private float _frameRate;
        private float _clipLengthSeconds;
        private HumanoidFootGoalStream _goals;
        // goal 복원용 소스 골반 궤적 — rootSpace 베이크와 같은 프레임.
        private IReadOnlyList<Vector3> _sourceHipsPositions;
        private IReadOnlyList<Quaternion> _sourceHipsRotations;
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
            // gate와 같은 해부학 기준 법선 — 무릎이 거의 펴진 구간의 자세 cross는
            // 잡음성이고 부호가 뒤집혀 무릎을 반대쪽으로 붕괴시키므로 기준 법선과의
            // 가중 정합으로 굽힘면을 고정함. 취득 실패 시 기존 캐시 경로로 폴백함.
            if (HumanoidLegBendCalibration.TryCapture(targetAnimator,
                    out Vector3 leftReference, out Vector3 rightReference))
            {
                _left.SetBendReference(leftReference);
                _right.SetBendReference(rightReference);
            }

            _frameRate = frameRate;
            _clipLengthSeconds = (samples.Count - 1) / frameRate;
            // goal 복원은 소스 골반 프레임을 씀 — rootSpace가 소스 골반 기준으로
            // 구워졌으므로 타겟 골반(실측 높이 1.03 vs 소스 0.77)을 쓰면 목표가
            // 그 차이만큼 어긋남. 소스 골반 복원은 베이크의 정확한 역변환이라
            // 목표가 원본 월드 발 위치와 일치하고 골반 보정과도 무관하게 고정됨.
            _sourceHipsPositions = hipsPositions;
            _sourceHipsRotations = hipsRotations;
            _goals = BuildGoalStream(samples, hipsPositions, hipsRotations);
            BuildPinArrays(samples, hipsPositions, hipsRotations, intents,
                labels, frameRate, sourceHumanScale);
            IsInitialized = true;
            return true;
        }

        // grounding이 측정한 발목 로컬 밑창 정점을 넘겨받음 — 발끝 본만으로는
        // 밑창 정점이 지면 아래로 파고드는 침투를 스냅할 수 없기 때문임.
        // 미설정 시 기존 발끝 스냅만 적용됨(폴백 동작 불변).
        internal void SetSoleProbes(bool isLeft, Vector3[] localPoints)
        {
            (isLeft ? _left : _right).SetSoleProbes(localPoints);
        }

        internal void Clear()
        {
            IsInitialized = false;
            LastAppliedPelvisOffset = 0f;
            _animator = null;
            _hips = null;
            _frameRate = 0f;
            _clipLengthSeconds = 0f;
            _goals = null;
            _sourceHipsPositions = null;
            _sourceHipsRotations = null;
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

        // plant 구간은 그 구간의 최저 발끝 높이를, 나머지는 클립 최저 발끝 높이를 지면으로 씀.
        // 의도 Anchor는 발목·발끝 중점이라 지면으로 쓰면 스냅이 발을 반 발바닥 높이만큼 띄움.
        private static float[] BuildFloorHeights(
            IReadOnlyList<HumanoidFootContactSample> samples,
            IReadOnlyList<HumanoidFootContactIntent> intents, bool isLeft)
        {
            var floor = new float[samples.Count];
            float clipMinimum = MinimumToeHeight(samples, isLeft, 0, samples.Count);
            for (int frame = 0; frame < samples.Count; frame++)
            {
                floor[frame] = clipMinimum;
            }
            if (intents != null)
            {
                foreach (HumanoidFootContactIntent intent in intents)
                {
                    if (intent.Mode != HumanoidFootContactIntentMode.Plant) continue;
                    int start = Math.Max(0, intent.StartFrame);
                    int end = Math.Min(samples.Count, intent.EndFrameExclusive);
                    float contactHeight = MinimumToeHeight(samples, isLeft, start, end);
                    for (int frame = start; frame < end; frame++)
                    {
                        floor[frame] = contactHeight;
                    }
                }
            }
            return floor;
        }

        private static float MinimumToeHeight(IReadOnlyList<HumanoidFootContactSample> samples,
            bool isLeft, int start, int endExclusive)
        {
            float minimum = float.PositiveInfinity;
            for (int frame = start; frame < endExclusive; frame++)
            {
                minimum = Mathf.Min(minimum,
                    (isLeft ? samples[frame].LeftToes : samples[frame].RightToes).y);
            }
            return minimum;
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
            LastAppliedPelvisOffset = 0f;
            if (!IsInitialized || alpha <= 0f)
            {
                return true;
            }

            int frame = HumanoidMotionFrameCalculator.CalculateFrameIndex(
                timeSeconds, _clipLengthSeconds, _frameRate);
            // 탐색 점프는 스무딩 상태를 리셋 — 이전 프레임 잔여가 새 위치로 번지지 않게 함.
            bool firstEval = float.IsNaN(_lastTimeSeconds);
            bool jumped = !firstEval &&
                Mathf.Abs(timeSeconds - _lastTimeSeconds) > 1.5f / _frameRate;
            float dt = firstEval || jumped ? 0f :
                Mathf.Max(0f, timeSeconds - _lastTimeSeconds);
            _lastTimeSeconds = timeSeconds;

            // 첫 평가는 전이 맥락이 없으므로 스냅으로 즉시 적용 — 같은 시각 재평가는
            // UpdateWeights의 dt<=0 조기 반환으로 상태를 유지해 멱등 재적용이 됨.
            _left.UpdateWeights(alpha, _left.Pin[frame], jumped || firstEval, dt);
            _right.UpdateWeights(alpha, _right.Pin[frame], jumped || firstEval, dt);
            if (_left.Weight <= 0f && _right.Weight <= 0f)
            {
                return true;
            }

            // 목표 골반은 소스 골반 궤적에서 가져옴 — rootSpace가 소스 골반 기준으로
            // 구워졌으므로 같은 프레임의 소스 골반으로 복원해야 원본 월드 발 위치가
            // 정확히 나옴. 타겟 골반은 골반 드롭의 영향을 받아 목표가 흔들림.
            Vector3 goalHipsPosition = _sourceHipsPositions[frame];
            Quaternion goalHipsRotation = _sourceHipsRotations[frame];
            // 골반 드롭은 양다리 공유 — 한쪽이라도 목표가 신장 한계를 넘으면 함께 계산함.
            float pelvisOffset = CalculatePelvisOffset(frame);
            if (Mathf.Abs(pelvisOffset) > 0f)
            {
                LastAppliedPelvisOffset =
                    pelvisOffset * Mathf.Max(_left.Weight, _right.Weight);
                _hips.position += Vector3.up * LastAppliedPelvisOffset;
            }

            // 밑창 프로브와 지면 모두 월드 좌표 — 소스·타겟은 같은 월드 공간을 공유하므로
            // 소스 월드 지면 높이를 그대로 비교함(골반 상대 변환은 회전 틀어짐 오차가 생김).
            _left.TrySolve(frame, _goals.Samples, goalHipsPosition, goalHipsRotation,
                _leftFloorRootSpaceY[frame], _leftFloorWorldY[frame], leftBendNormal, dt);
            _right.TrySolve(frame, _goals.Samples, goalHipsPosition, goalHipsRotation,
                _rightFloorRootSpaceY[frame], _rightFloorWorldY[frame], rightBendNormal, dt);
            return true;
        }

        // 이 프레임에 한 발이라도 IK가 목표를 주장하는지 — weight 갱신 전 상태로 판정해
        // 호출부가 stabilizer 이전에 gate 경로를 선택할 수 있게 함.
        // false(기권) 프레임은 alpha=0과 같은 gate 보정이 대신 적용됨.
        internal bool WouldClaim(float timeSeconds)
        {
            if (!IsInitialized)
            {
                return false;
            }
            int frame = HumanoidMotionFrameCalculator.CalculateFrameIndex(
                timeSeconds, _clipLengthSeconds, _frameRate);
            return _left.Pin[frame] || _right.Pin[frame] ||
                _left.Weight > 0f || _right.Weight > 0f;
        }

        private float CalculatePelvisOffset(int frame)
        {
            bool needsLeft = TryBuildReachLeg(_left, frame,
                out HumanoidPelvisReachCalculator.Leg left, out string leftDebug);
            bool needsRight = TryBuildReachLeg(_right, frame,
                out HumanoidPelvisReachCalculator.Leg right, out string rightDebug);
            DebugPelvisInputs = $"L[{leftDebug}] R[{rightDebug}]";
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
            out HumanoidPelvisReachCalculator.Leg reachLeg, out string debug)
        {
            reachLeg = default;
            debug = "inactive";
            if (leg.Weight <= 0f ||
                !TryGetGoalSample(frame, leg.IsLeft, out HumanoidFootGoalStream.GoalSample goal))
            {
                return false;
            }

            Vector3 goalWorld = _sourceHipsPositions[frame] +
                _sourceHipsRotations[frame] * goal.RootSpaceFootPosition;
            // reach 계산은 solve와 같은 유효 목표를 봐야 함 — 해제 감쇠·감쇠 중
            // 재핀의 블렌드 목표와 어긋나면 골반 드롭이 실제 추종 대상과 달라짐.
            goalWorld = leg.ApplyPinBlend(frame, goalWorld);
            Vector3 toHip = leg.Upper.position - goalWorld;
            float distance = toHip.magnitude;
            // 감쇠 기준각은 현재 자세가 아니라 임계값으로 고정함 — 자세 이력이 섞이면
            // 신전 경계에서 같은 목표라도 프레임마다 도달 거리가 달라져 골반 드롭이
            // 진동하고 무릎이 흔들림. 임계값 기준은 min(현재각, 임계값)과 수학적으로
            // 동등하면서 감쇠 도달을 목표 거리의 순수 함수로 만듦.
            // 솔버는 본을 늘리지 않으므로 감쇠 도달 거리를 넘는 목표만 골반 드롭으로 흡수함.
            // 계산기는 본 길이 합을 넘는 MaximumReach를 거부하므로 신장 비율을 섞으면 드롭이 항상 실패함.
            if (!HumanoidPelvisReachCalculator.TryCalculateExtensionReach(
                    leg.UpperLength, leg.LowerLength, ExtensionDampingThresholdRadians,
                    distance, leg.Weight, ExtensionDampingThresholdRadians, out float dampedReach))
            {
                dampedReach = leg.Reach;
            }
            debug = $"w={leg.Weight:F2} d={distance:F5} " +
                $"damped={dampedReach:F5} need={distance > dampedReach} " +
                $"up={leg.Upper.position:F4} low={leg.Lower.position:F4} ft={leg.Foot.position:F4}";
            if (distance <= dampedReach)
            {
                return false;
            }
            reachLeg = new HumanoidPelvisReachCalculator.Leg(toHip,
                leg.UpperLength, leg.LowerLength, leg.Weight, 0f, dampedReach);
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

        // 골반 상대 goal 회전을 월드로 변환 — 골반 프레임을 매개로 루트 모션을 따라감.
        internal static Quaternion GoalFrameToWorld(HumanoidFootGoalStream.GoalSample goal,
            Quaternion hipsRotation)
        {
            return (hipsRotation * goal.RootSpaceFootRotation).normalized;
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
            // Asin은 ±90° 근처에서 float 오차가 0.02°까지 커져 atan2(수직, 수평)로 구함.
            float vertical = Vector3.Dot(forward, groundNormal);
            float horizontal = (forward - groundNormal * vertical).magnitude;
            float pitch = Mathf.Atan2(vertical, horizontal) * Mathf.Rad2Deg;
            if (pitch >= -ToePitchSoftKneeDeg)
            {
                return footFrame;
            }
            float span = MaximumSupportedToePitch - ToePitchSoftKneeDeg;
            float limited = -(ToePitchSoftKneeDeg +
                span * (float)Math.Tanh((-pitch - ToePitchSoftKneeDeg) / span));
            Vector3 axis = Vector3.Cross(groundNormal, forward);
            // 발끝이 지면 법선과 평행(수직 하향)하면 cross가 퇴화함 — 이때도 cap이 필요하므로
            // 같은 회전 부호를 주는 발 lateral 축으로 대체함.
            if (axis.sqrMagnitude < MinimumAxisSquaredMagnitude)
            {
                axis = footFrame * Vector3.right;
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
            // pin 재진입 프레임의 목표를 수평 앵커로 유지 — 핀 구간 내내 정박함.
            private Vector3 _pinAnchor;
            private bool _pinAnchorValid;
            // 마지막 핀 프레임의 목표 발 회전 — 해제 감쇠 중 목표 프레임 이행의
            // 기준으로 써서 회전도 위치처럼 weight 비례로 연속 이행하게 함.
            private Quaternion _pinAnchorFrame;
            // 해제 감쇠 진입 표시 — 같은 핀 안에서만 앵커를 유지하고 감쇠 중
            // 재핀되면 새 목표로 앵커를 다시 잡게 함.
            private bool _pinReleased;
            // 발목 로컬 좌표의 밑창 정점 — grounding 샘플러가 넘겨준 측정값임.
            private Vector3[] _soleProbesLocal = Array.Empty<Vector3>();
            // 무릎이 거의 펴진 프레임은 cross가 퇴화해 방향이 프레임마다 뒤집히므로
            // 마지막으로 유효했던 굽힘 법선을 유지함 — stabilizer의 init 폴백과 같은 의도임.
            private Vector3 _lastBendNormal;
            // 해부학 기준 굽힘 법선(허벅지 로컬) — HumanoidLegBendCalibration이
            // 바인드 자세에서 취득한 gate와 같은 규약임. 미설정 시 zero로 두고 폴백함.
            private Vector3 _referenceNormal;
            private float _stableNormalSquaredThreshold;
            internal Vector3 LastBendNormal => _lastBendNormal;
            internal Vector3 LastSmoothedGoal => _smoothedGoal;
            // 이번 solve가 실제로 쫓은 목표(핀 앵커·해제 블렌드 반영 후, 평활 전) —
            // 해제 전이의 목표 불연속 검증용.
            internal Vector3 LastSolveGoal { get; private set; }
            // 핀 구간의 수평 앵커 — reach 계산이 solve와 동일 목표를 보게 공개함.
            // 해제 감쇠부터는 solve 목표가 앵커에서 원본으로 이행 중이므로 원본
            // 목표를 보게 함 — 오래된 앵커가 거리를 부풀려 단발 골반 드롭을 만들지 않게 함.
            internal bool TryGetPinAnchor(out Vector3 anchor)
            {
                anchor = _pinAnchor;
                return _pinAnchorValid && !_pinReleased;
            }
            // solve와 reach가 공유하는 유효 목표 — 핀 중은 앵커, 해제 감쇠·감쇠 중
            // 재핀은 앵커↔원본 블렌드, 그 외는 원본 목표를 반환함. 상태를 바꾸지
            // 않으므로 solve보다 먼저 실행되는 골반 계산도 같은 목표를 볼 수 있음.
            internal Vector3 ApplyPinBlend(int frame, Vector3 rawGoal)
            {
                if (!_pinAnchorValid || Weight <= 0f)
                {
                    return rawGoal;
                }
                bool pinned = frame >= 0 && frame < Pin.Length && Pin[frame];
                if (pinned && !_pinReleased)
                {
                    rawGoal.x = _pinAnchor.x;
                    rawGoal.z = _pinAnchor.z;
                    return rawGoal;
                }
                // 해제 감쇠 또는 감쇠 중 재핀 — solve가 블렌드 위치를 목표·재앵커로 씀.
                rawGoal.x = Mathf.Lerp(rawGoal.x, _pinAnchor.x, Weight);
                rawGoal.z = Mathf.Lerp(rawGoal.z, _pinAnchor.z, Weight);
                return rawGoal;
            }
            // solve 직전·직후 포즈 진단 — 무릎 결과 분기 추적용.
            internal Vector3 DebugPreUpperPosition { get; private set; }
            internal Vector3 DebugPreLowerPosition { get; private set; }
            internal Vector3 DebugPostLowerPosition { get; private set; }
            internal Vector3 DebugDesiredKnee { get; private set; }
            // 최근 solve에 실제 사용된 굽힘 법선 — 해부학 기준 정합 결과라 사후
            // 지면 상승 IK도 같은 굽힘면을 쓸 수 있게 컨트롤러에 공개함.
            internal Vector3 LastUsedBendNormal { get; private set; }
            internal float DebugReferenceWeight { get; private set; }
            internal Vector3 DebugProjectedReference { get; private set; }
            internal Vector3 DebugPreFootPosition { get; private set; }
            internal Vector3 DebugPostBlendLowerPosition { get; private set; }
            internal Vector3 DebugPostBlendFootPosition { get; private set; }
            internal string DebugSolveInputs { get; private set; }
            internal bool DebugSolveSucceeded { get; private set; }
            internal float DebugTargetError { get; private set; }
            internal int DebugSoleProbeCount => _soleProbesLocal.Length;
            internal float DebugLastLift { get; private set; }
            internal float DebugLastLowestProbe { get; private set; }

            internal void SetSoleProbes(Vector3[] localPoints)
            {
                _soleProbesLocal = localPoints ?? Array.Empty<Vector3>();
            }

            // 허벅지 로컬 공간의 해부학 기준 법선 — HumanoidLegBendCalibration 결과를
            // 그대로 받음(게이트의 _referenceNormal과 같은 규약).
            internal void SetBendReference(Vector3 referenceNormal)
            {
                _referenceNormal = referenceNormal;
            }

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
                _stableNormalSquaredThreshold =
                    UpperLength * LowerLength * MinimumStableBendSine;
                _stableNormalSquaredThreshold *= _stableNormalSquaredThreshold;
                HumanoidFootRotationBinding.TryCreate(Foot, toes, animator.transform.up,
                    out _binding);
                _lastBendNormal = Vector3.Cross(
                    Lower.position - Upper.position, Foot.position - Lower.position);
                if (!IsStableBendNormal(_lastBendNormal))
                {
                    _lastBendNormal = animator.transform.right;
                }
                _referenceNormal = Vector3.zero;
                return UpperLength > 0f && LowerLength > 0f;
            }

            internal void UpdateWeights(float alpha, bool pinned,
                bool reset, float dt)
            {
                float target = alpha * (pinned ? 1f : 0f);
                if (reset)
                {
                    Weight = target;
                    // 시드 자체는 TrySolve가 담당 — 여기서 true로 두면 평활 목표가
                    // Vector3.zero로 남아 IK가 월드 원점으로 접힘.
                    _hasSmoothedGoal = false;
                    // 시간 점프는 핀 에피소드도 끊음 — 이전 위치의 앵커가
                    // 새 위치의 골반 계산·해제 블렌드로 번지지 않게 함.
                    _pinAnchorValid = false;
                    _pinReleased = false;
                    return;
                }
                if (dt <= 0f)
                {
                    // 같은 시각 재평가(Seek 동일 시각·자세 복원 재적용) — 경과 시간이
                    // 없으므로 상태를 유지해 멱등 재적용이 되게 함. 여기서 스냅하면
                    // 핀 앵커 파괴·weight 급변으로 재평가마다 발이 점프함
                    // (gate의 ApplyGateHold도 같은 규약으로 멱등 처리함).
                    return;
                }
                // 선형 전이 — 프레임당 변화량이 일정해 해제·진입 시의 자세 갭
                // 노출 스텝이 갭/전이 프레임 수로 상한 아래에 머묾.
                Weight = Mathf.MoveTowards(Weight, target, PinWeightChangePerSecond * dt);
                // weight가 소실되면 다음 pin 재진입 때 새 목표에서 다시 정박함 —
                // 이전 구간의 평활 잔여가 슬라이드로 보이지 않게 함.
                if (Weight <= 0f && target <= 0f)
                {
                    Weight = 0f;
                    _hasSmoothedGoal = false;
                    // 핀 에피소드 종료 — weight 소실 후 TrySolve가 다시 안 불려도
                    // 앵커가 남아 다음 재진입의 골반 계산을 오염하지 않게 함.
                    _pinAnchorValid = false;
                    _pinReleased = false;
                }
            }

            internal void TrySolve(int frame,
                IReadOnlyList<HumanoidFootGoalStream.GoalSample> samples,
                Vector3 hipsPosition, Quaternion hipsRotation,
                float floorRootSpaceY, float floorWorldY,
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

                // 밑창 인식 지면 스냅 — 발끝 본 스냅만으로는 회전된 발의 밑창 정점이
                // 지면 아래로 파고듦. 목표 회전 기준으로 가장 낮은 밑창 정점이
                // 지면에 닿도록 목표 발목을 들어올림. pin 여부와 무관하게 적용함.
                Quaternion goalFrame = GoalFrameToWorld(goal, hipsRotation);
                // 지면 정렬·soft-cap을 먼저 반영 — 뒤의 밑창 프로브 평가와 최종
                // 적용 회전이 같은 목표 프레임을 기준으로 해야 lift가 정확해짐.
                if (Grounded[frame])
                {
                    goalFrame = AlignSoleToGround(goalFrame, Vector3.up);
                    goalFrame = ApplyToePitchSoftCap(goalFrame, Vector3.up, 1f);
                }
                // 핀 해제 감쇠 중 회전 불연속 방지 — 목표 프레임도 위치와 같은
                // weight 비례로 마지막 핀 프레임에서 이행해 발 회전이 단발로
                // 점프하지 않게 함. 감쇠 중 재핀이면 현재 유효 프레임으로 이어 잡음.
                if (Pin[frame])
                {
                    _pinAnchorFrame = _pinReleased && _pinAnchorValid && Weight > 0f
                        ? Quaternion.Slerp(goalFrame, _pinAnchorFrame, Weight)
                        : goalFrame;
                }
                else if (_pinAnchorValid && Weight > 0f)
                {
                    goalFrame = Quaternion.Slerp(goalFrame, _pinAnchorFrame, Weight);
                }
                // rootSpace는 소스 골반 기준으로 구워짐 — 소스 골반으로 복원하면
                // 원본 월드 발 위치가 정확히 나오고 별도의 신장 스케일이 불필요함.
                Vector3 goalWorld = hipsPosition +
                    hipsRotation * goal.RootSpaceFootPosition;
                if (_soleProbesLocal.Length > 0 &&
                    _binding != null &&
                    _binding.TryResolveAppliedRotation(goalFrame, out Quaternion appliedFootRotation))
                {
                    // 바인딩이 뼈에 실제로 쓸 회전으로 밑창 정점을 평가함 —
                    // goalFrame은 canonical frame 규약이라 발목 뼈 로컬 좌표의
                    // 프로브에 그대로 곱하면 축이 어긋나 높이가 틀어짐.
                    DebugLastLowestProbe = LowestSoleProbeHeight(
                        goalWorld, appliedFootRotation);
                    DebugLastLift = floorWorldY - DebugLastLowestProbe;
                    if (DebugLastLift > 0f)
                    {
                        goalWorld.y += DebugLastLift;
                    }
                }

                // pin 구간의 수평 앵커 고정 — 핀은 발이 심어진 상태인데 VMD 목표를
                // 계속 추종하면 접지 발이 프레임마다 미끄러짐(잔차 슬라이드). 재진입
                // 프레임의 목표를 수평 앵커로 삼아 정박하고, 수직은 지면 스냅·
                // 리프트를 계속 반영함.
                if (Pin[frame])
                {
                    if (!_pinAnchorValid || _pinReleased)
                    {
                        // 감쇠 중 재핀이면 현재 유효 목표(블렌드 위치)로 앵커를
                        // 이어 잡아 정박 지점이 생목표로 단발 점프하지 않게 함.
                        _pinAnchor = _pinReleased && _pinAnchorValid
                            ? new Vector3(
                                Mathf.Lerp(goalWorld.x, _pinAnchor.x, Weight),
                                goalWorld.y,
                                Mathf.Lerp(goalWorld.z, _pinAnchor.z, Weight))
                            : goalWorld;
                        _pinAnchorValid = true;
                        _pinReleased = false;
                    }
                    goalWorld.x = _pinAnchor.x;
                    goalWorld.z = _pinAnchor.z;
                }
                else if (_pinAnchorValid && Weight > 0f)
                {
                    // 핀 해제 감쇠 중 목표 불연속 방지 — 앵커와 원본 목표 사이를
                    // weight 비례로 이행해 solve 목표가 프레임 단위로 점프하지 않게 함.
                    // ApplyPinBlend와 동일 수식 — 골반 reach 계산도 같은 목표를 봄.
                    _pinReleased = true;
                    goalWorld = ApplyPinBlend(frame, goalWorld);
                }
                else
                {
                    _pinAnchorValid = false;
                    _pinReleased = false;
                }

                LastSolveGoal = goalWorld;
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
                // gate와 같은 해부학 기준 정합 — 무릎이 거의 펴진 구간의 자세 cross는
                // 잡음성이고 부호가 뒤집혀 무릎을 반대쪽으로 붕괴시키므로 기준 법선과
                // 가중 정합해 굽힘면을 고정함. 기준이 없거나 정합이 퇴화하면
                // 기존 입력·캐시 경로로 폴백함.
                Vector3 referenceNormal = upperRotation * _referenceNormal;
                DebugProjectedReference = referenceNormal;
                bool bendAligned = HumanoidLegPoseSolver.TryCalculateBendNormal(
                    Lower.position - Upper.position,
                    Foot.position - Lower.position,
                    _smoothedGoal - Upper.position,
                    referenceNormal, out bendNormal, out float referenceWeight);
                DebugReferenceWeight = bendAligned ? referenceWeight : -1f;
                if (bendAligned)
                {
                    _lastBendNormal = bendNormal;
                }
                else if (IsStableBendNormal(referenceNormal))
                {
                    bendNormal = referenceNormal;
                    _lastBendNormal = bendNormal;
                }
                else
                {
                    // 정합 실패 시 out은 zero — 캐시 유지가 우선. 경계값(1~3°)의
                    // 현재 자세 cross는 잡음성이라 캐시를 덮어쓰면 굽힘면이
                    // 프레임마다 흔들리므로 자세 cross는 캐시가 비어 있을 때만 시드로 씀.
                    if (!IsStableBendNormal(_lastBendNormal))
                    {
                        Vector3 currentBendNormal = Vector3.Cross(
                            Lower.position - Upper.position,
                            Foot.position - Lower.position);
                        if (IsStableBendNormal(currentBendNormal))
                        {
                            _lastBendNormal = currentBendNormal;
                        }
                    }
                    bendNormal = _lastBendNormal;
                }
                DebugPreUpperPosition = Upper.position;
                DebugPreLowerPosition = Lower.position;
                DebugPreFootPosition = Foot.position;
                LastUsedBendNormal = bendNormal;
                bool solved = HumanoidLegPoseSolver.TrySolve(Upper, Lower, Foot,
                    _smoothedGoal, bendNormal, out float targetError);
                DebugPostLowerPosition = Lower.position;
                // 래퍼가 계산한 목표 무릎 — solve와 동일 수학으로 기대값을 재구성함.
                {
                    Vector3 direction = _smoothedGoal - Upper.position;
                    float distance = direction.magnitude;
                    float along = (distance * distance + UpperLength * UpperLength -
                        LowerLength * LowerLength) / (2f * distance);
                    float radius = Mathf.Sqrt(Mathf.Max(0f,
                        UpperLength * UpperLength - along * along));
                    Vector3 bendDir = Vector3.Cross(direction,
                        bendNormal.normalized).normalized;
                    DebugDesiredKnee = Upper.position + direction.normalized * along +
                        bendDir * radius;
                    DebugSolveInputs = string.Format(
                        "dir={0:F4} d={1:F4} along={2:F4} r={3:F4} bendDir={4:F4} UL={5:F4} LL={6:F4}",
                        direction, distance, along, radius, bendDir, UpperLength, LowerLength);
                }
                DebugSolveSucceeded = solved;
                DebugTargetError = targetError;
                if (solved && Weight < 1f)
                {
                    Upper.rotation = Quaternion.Slerp(upperRotation, Upper.rotation, Weight);
                    Lower.rotation = Quaternion.Slerp(lowerRotation, Lower.rotation, Weight);
                    Foot.rotation = Quaternion.Slerp(footRotation, Foot.rotation, Weight);
                }
                DebugPostBlendLowerPosition = Lower.position;
                DebugPostBlendFootPosition = Foot.position;

                // 발 회전 — 목표 프레임(grounded 시 정렬+soft-cap 반영됨)을 가중 적용.
                if (_binding == null || !_binding.TryCaptureWorldFrame(out Quaternion currentFrame))
                {
                    return;
                }
                Quaternion blended = Quaternion.Slerp(currentFrame, goalFrame, Weight);
                _binding.TryApplyWorldFrame(blended);
            }

            // 목표 발 피치로 평지 정렬을 가중 — 급하게 기운 발끝 지지(롤 스탠스)는
            // 발끝 방향을 유지하고, 평평한 목표일수록 밑창을 지면에 맞춤.
            // (이전 발목-발끝 높이차 기준은 모델의 고유 밑창 오프셋에 스케일 종속이라
            //  이 모델에서는 가중치가 항상 0이 되어 정렬이 사실상 동작하지 않았음.)
            private static Quaternion AlignSoleToGround(Quaternion frame, Vector3 groundNormal)
            {
                Vector3 forward = frame * Vector3.forward;
                float vertical = Vector3.Dot(forward, groundNormal);
                float horizontal = (forward - groundNormal * vertical).magnitude;
                float pitchDeg = Mathf.Atan2(vertical, horizontal) * Mathf.Rad2Deg;
                float flatWeight = Mathf.Clamp01(1f - Mathf.Abs(pitchDeg) / SoleAlignPitchSpanDeg);
                if (flatWeight <= 0f)
                {
                    return frame;
                }
                Vector3 up = frame * Vector3.up;
                Quaternion aligned = Quaternion.FromToRotation(up, groundNormal) * frame;
                return Quaternion.Slerp(frame, aligned, flatWeight);
            }

            // 목표 발목·회전에서 가장 낮은 밑창 정점의 월드 높이를 구함.
            private float LowestSoleProbeHeight(Vector3 ankleWorld, Quaternion footFrame)
            {
                float lowest = float.PositiveInfinity;
                float scale = Foot.lossyScale.x;
                foreach (Vector3 probe in _soleProbesLocal)
                {
                    Vector3 world = ankleWorld + footFrame * (probe * scale);
                    lowest = Mathf.Min(lowest, world.y);
                }
                return lowest;
            }

            // 무릎 굽힘 각이 MinimumStableBendSine 이하면 cross 정규화 방향이
            // 노이즈에 지배되므로 불안정으로 판정함 — 임계는 다리 길이로 정규화됨.
            private bool IsStableBendNormal(Vector3 bendNormal)
            {
                return bendNormal.sqrMagnitude > _stableNormalSquaredThreshold;
            }

            internal void Clear()
            {
                Upper = Lower = Foot = null;
                _binding = null;
                Weight = 0f;
                Pin = Grounded = Array.Empty<bool>();
                _hasSmoothedGoal = false;
                _smoothedGoal = Vector3.zero;
                _pinAnchorValid = false;
                _pinAnchor = Vector3.zero;
                _pinAnchorFrame = Quaternion.identity;
                _pinReleased = false;
                _soleProbesLocal = Array.Empty<Vector3>();
                _lastBendNormal = Vector3.zero;
                _referenceNormal = Vector3.zero;
                _stableNormalSquaredThreshold = 0f;
            }
        }
    }
}
