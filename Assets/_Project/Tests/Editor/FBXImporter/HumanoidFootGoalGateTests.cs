using Fbx2Vmd.FBXImporter;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFootGoalGateTests
    {
        private const float FrameRate = 60f;
        private const float HumanScale = 1.5f;
        private HumanoidFootGoalGate _gate;

        [SetUp]
        public void CreateGate()
        {
            _gate = new HumanoidFootGoalGate(FrameRate, HumanScale);
        }

        private static HumanoidFootGoalGate.FrameEvidence PlantIntent =>
            new HumanoidFootGoalGate.FrameEvidence
            {
                HasConfidentPlantIntent = true,
                ToeHeightAboveFloor = 0.005f,
                AnkleSpeed = 0.01f,
            };

        private float Run(HumanoidFootGoalGate.FrameEvidence evidence, int frames)
        {
            float pin = 0f;
            for (int index = 0; index < frames; index++)
            {
                pin = _gate.Evaluate(evidence).Pin;
            }
            return pin;
        }

        [Test]
        public void Given_NoEvidence_When_Evaluating_Then_PinStaysClosed()
        {
            Assert.That(Run(new HumanoidFootGoalGate.FrameEvidence(), 30),
                Is.EqualTo(0f));
        }

        [Test]
        public void Given_ConfidentPlant_When_Short_Then_PinStaysClosed()
        {
            // 0.1s(60fps 기준 6프레임) 미만의 지지 의도는 스치기로 취급해 핀을 걸지 않음.
            Assert.That(Run(PlantIntent, 5), Is.EqualTo(0f));
        }

        [Test]
        public void Given_ConfidentPlant_When_Sustained_Then_PinOpens()
        {
            Assert.That(Run(PlantIntent, 6), Is.EqualTo(1f));
            Assert.That(Run(PlantIntent, 60), Is.EqualTo(1f));
        }

        [Test]
        public void Given_UncertainSpan_When_Evaluating_Then_PinStaysClosed()
        {
            var evidence = PlantIntent;
            evidence.InUncertainSpan = true;
            Assert.That(Run(evidence, 30), Is.EqualTo(0f));
        }

        [Test]
        public void Given_SlideIntent_When_Evaluating_Then_GroundedWithoutPin()
        {
            var output = _gate.Evaluate(new HumanoidFootGoalGate.FrameEvidence
            {
                HasSlideIntent = true,
            });
            Assert.That(output.Pin, Is.EqualTo(0f));
            Assert.That(output.Grounded, Is.True);
        }

        [Test]
        public void Given_ToeBrushing_When_Evaluating_Then_PinStaysClosed()
        {
            // 확실한 지지 의도 안에서도 발끝이 진입 높이 이상으로 떠 있으면 후보 탈락.
            var brush = PlantIntent;
            brush.ToeHeightAboveFloor = 0.03f;
            Assert.That(Run(brush, 30), Is.EqualTo(0f));
        }

        [Test]
        public void Given_PinOpen_When_ToeRisesIntoDeadband_Then_PinStaysOpen()
        {
            Run(PlantIntent, 10); // pin 열림
            var raised = PlantIntent;
            raised.ToeHeightAboveFloor = 0.04f; // 진입값 초과·해제값 미만
            Assert.That(Run(raised, 5), Is.EqualTo(1f),
                "데드밴드 안의 일시 상승은 pin을 닫지 않아야 함");
        }

        [Test]
        public void Given_PinOpen_When_ToeLeavesDeadband_Then_PinCloses()
        {
            Run(PlantIntent, 10);
            var lifted = PlantIntent;
            lifted.ToeHeightAboveFloor = 0.1f; // 해제값(0.075) 초과
            Assert.That(Run(lifted, 1), Is.EqualTo(0f));
        }

        [Test]
        public void Given_HumanLabelFalse_When_ConfidentPlant_Then_PinStaysClosed()
        {
            var suppressed = PlantIntent;
            suppressed.LabelIsSupport = false;
            Assert.That(Run(suppressed, 30), Is.EqualTo(0f));
        }

        [Test]
        public void Given_HumanLabelTrue_When_NoIntent_Then_PinOpens()
        {
            Assert.That(Run(new HumanoidFootGoalGate.FrameEvidence
            {
                LabelIsSupport = true,
                ToeHeightAboveFloor = 0.005f,
                AnkleSpeed = 0.01f,
            }, 6), Is.EqualTo(1f));
        }

        [Test]
        public void Given_MissingMeasurements_When_ConfidentPlant_Then_PinOpens()
        {
            Assert.That(Run(new HumanoidFootGoalGate.FrameEvidence
            {
                HasConfidentPlantIntent = true,
                ToeHeightAboveFloor = float.NaN,
                AnkleSpeed = float.NaN,
            }, 6), Is.EqualTo(1f));
        }

        [Test]
        public void Given_PinOpen_When_EvidenceDrops_Then_ReopensOnlyAfterPersistence()
        {
            Run(PlantIntent, 10);
            Run(new HumanoidFootGoalGate.FrameEvidence(), 2); // 후보 단절
            Assert.That(Run(PlantIntent, 3), Is.EqualTo(0f),
                "재개 후에도 지속 시간을 다시 채워야 pin이 열려야 함");
            Assert.That(Run(PlantIntent, 3), Is.EqualTo(1f));
        }
    }
}
