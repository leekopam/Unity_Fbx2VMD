namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 접지 의도·사람 표식·발목 높이/속도 증거를 pin(0..1)과 grounded로 변환함.
    /// 지지 의도가 확실하고 충분히 지속된 구간만 pin을 열고, 증거 부족·스치기는 0을 돌려줌.
    /// 순수 계산 전용 — Unity 씬 상태를 참조하지 않음.
    /// </summary>
    internal sealed class HumanoidFootGoalGate
    {
        internal struct FrameEvidence
        {
            // 이 프레임을 덮는 의도 구간이 있고 확신일 때만 pin 후보가 됨.
            internal bool HasConfidentPlantIntent;
            // 미끄럼 의도 구간 — 핀은 걸지 않지만 지면 접촉 상태로 취급함.
            internal bool HasSlideIntent;
            // 추정이 기권한 불확실 구간 — 항상 패스스루(pin=0).
            internal bool InUncertainSpan;
            // 사람 표식 — true는 pin 후보 강제, false는 pin 억제.
            internal bool? LabelIsSupport;
            // 발끝이 추정 지면 위로 떠 있는 높이(m). NaN이면 히스테리시스 미적용.
            internal float ToeHeightAboveFloor;
            // 발목 수평 속도(m/s). NaN이면 히스테리시스 미적용.
            internal float AnkleSpeed;
        }

        internal struct GateOutput
        {
            // 0이면 패스스루, 1이면 goal 핀 — 부분 값은 향후 블렌딩 여지로 남김.
            internal float Pin;
            // 접촉 의도가 있는 프레임 — goal을 지면 아래로 내리지 않는 스냅 대상.
            internal bool Grounded;
        }

        private readonly int _minimumSupportFrames;
        private readonly float _enterToeHeight;
        private readonly float _exitToeHeight;
        private readonly float _enterAnkleSpeed;
        private readonly float _exitAnkleSpeed;
        private int _candidateRunLength;
        private bool _pinOpen;

        // 지속시간·높이·속도 기본값은 접지 의도 추정기와 같은 상수를 씀.
        internal HumanoidFootGoalGate(float frameRate, float humanScale)
            : this(frameRate, humanScale,
                  HumanoidFootContactIntentEstimator.SupportSpeedPerHumanScale,
                  HumanoidFootContactIntentEstimator.SlideDisplacementPerHumanScale)
        {
        }

        internal HumanoidFootGoalGate(float frameRate, float humanScale,
            float supportSpeedPerHumanScale, float slideDisplacementPerHumanScale)
        {
            if (frameRate <= 0f || humanScale <= 0f)
            {
                throw new System.ArgumentException(
                    "핀 게이트에는 유효한 프레임률과 키 비율이 필요합니다.");
            }

            _minimumSupportFrames = UnityEngine.Mathf.Max(2,
                UnityEngine.Mathf.CeilToInt(frameRate * 0.1f));
            // 진입은 지지 높이·속도와 같은 엄격값, 해제는 공중 판정값 — 그 사이가 데드밴드.
            _enterToeHeight = humanScale * 0.015f;
            _exitToeHeight = humanScale * 0.05f;
            _enterAnkleSpeed = humanScale * supportSpeedPerHumanScale;
            _exitAnkleSpeed = humanScale * supportSpeedPerHumanScale * 4f;
        }

        internal GateOutput Evaluate(in FrameEvidence evidence)
        {
            GateOutput output = new GateOutput();
            bool candidate = CandidateFor(evidence);
            output.Grounded = candidate || evidence.HasSlideIntent;
            if (!candidate)
            {
                _candidateRunLength = 0;
                _pinOpen = false;
                return output;
            }

            _candidateRunLength++;
            // 지지 의도가 0.1s 이상 이어진 뒤에만 pin을 연다 — 스치기·미세 지터 차단.
            if (_candidateRunLength >= _minimumSupportFrames)
            {
                _pinOpen = true;
            }

            output.Pin = _pinOpen ? 1f : 0f;
            return output;
        }

        internal void Reset()
        {
            _candidateRunLength = 0;
            _pinOpen = false;
        }

        private bool CandidateFor(in FrameEvidence evidence)
        {
            if (evidence.LabelIsSupport == false)
            {
                return false;
            }

            bool candidate = evidence.LabelIsSupport == true ||
                evidence.HasConfidentPlantIntent;
            // 기권 구간은 사람 강제 표식이 없으면 후보가 될 수 없음.
            if (candidate && evidence.InUncertainSpan &&
                evidence.LabelIsSupport != true)
            {
                return false;
            }

            if (!candidate)
            {
                return false;
            }

            // 발끝 스치기 차단 — pin이 닫힌 상태에선 엄격 진입값을, 열린 상태에선
            // 완만 해제값을 요구해 경계 프레임의 채터를 막음. 측정이 없으면 증거 그대로 신뢰함.
            if (!float.IsNaN(evidence.ToeHeightAboveFloor))
            {
                float heightLimit = _pinOpen ? _exitToeHeight : _enterToeHeight;
                if (evidence.ToeHeightAboveFloor > heightLimit)
                {
                    return false;
                }
            }
            if (!float.IsNaN(evidence.AnkleSpeed))
            {
                float speedLimit = _pinOpen ? _exitAnkleSpeed : _enterAnkleSpeed;
                if (evidence.AnkleSpeed > speedLimit)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
