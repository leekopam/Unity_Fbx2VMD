using System.Collections.Generic;
using System.IO;
using Fbx2Vmd.EditorTools.FBXImporter;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public class FootDefectScanTests
    {
        private const float SupportH = 0.015f;
        private const float AirborneH = 0.05f;
        private const float PenDepth = 0.005f;

        [Test]
        public void EstimateFloor_UsesTenthPercentile()
        {
            // 추정기와 동일 규칙: 정렬 후 (int)((n-1)*0.1) 인덱스 —
            // 11개 표본에서는 두 번째로 작은 값.
            var heights = new float[] { 0.9f, 0.1f, 0.2f, 0.3f, 0.4f,
                0.5f, 0.6f, 0.7f, 0.8f, 0.15f, 0.25f };
            Assert.That(FootDefectScan.EstimateFloor(heights),
                Is.EqualTo(0.15f).Within(0.0001f));
            // 10개 표본에서는 인덱스 0 — 최솟값.
            var ten = new float[] { 0.9f, 0.1f, 0.2f, 0.3f, 0.4f,
                0.5f, 0.6f, 0.7f, 0.8f, 0.15f };
            Assert.That(FootDefectScan.EstimateFloor(ten),
                Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void Classify_BelowFloorBeyondDepth_IsPenetration()
        {
            string kind = FootDefectScan.Classify(
                footHeight: 0.01f, toesHeight: -0.012f,
                inPlant: false, SupportH, AirborneH, PenDepth);
            Assert.That(kind, Is.EqualTo(FootDefectScan.Penetration));
        }

        [Test]
        public void Classify_ShallowBelowFloor_IsNotPenetration()
        {
            // 허용 깊이 안쪽의 얕은 음수는 노이즈 — 관통이 아니라 저고도 후보.
            string kind = FootDefectScan.Classify(
                footHeight: 0.002f, toesHeight: -0.002f,
                inPlant: false, SupportH, AirborneH, PenDepth);
            Assert.That(kind, Is.EqualTo(FootDefectScan.NearFloorUnsupported));
        }

        [Test]
        public void Classify_LiftedInsidePlant_IsFloatingInPlant()
        {
            string kind = FootDefectScan.Classify(
                footHeight: 0.03f, toesHeight: 0.02f,
                inPlant: true, SupportH, AirborneH, PenDepth);
            Assert.That(kind, Is.EqualTo(FootDefectScan.FloatingInPlant));
        }

        [Test]
        public void Classify_LowFootOutsidePlant_IsNearFloor()
        {
            // 지지 높이 안이어도 의도 미분류이면 저고도 — 붕뜸 주력 후보.
            string kind = FootDefectScan.Classify(
                footHeight: 0.01f, toesHeight: 0.005f,
                inPlant: false, SupportH, AirborneH, PenDepth);
            Assert.That(kind, Is.EqualTo(FootDefectScan.NearFloorUnsupported));
        }

        [Test]
        public void Classify_HighFoot_IsNormal()
        {
            Assert.That(FootDefectScan.Classify(
                    footHeight: 0.2f, toesHeight: 0.15f,
                    inPlant: false, SupportH, AirborneH, PenDepth),
                Is.Null);
            // 지지 구간에서 발끝은 닿고 발목만 들린 롤은 붕뜸이 아님.
            Assert.That(FootDefectScan.Classify(
                    footHeight: 0.03f, toesHeight: 0.005f,
                    inPlant: true, SupportH, AirborneH, PenDepth),
                Is.Null);
        }

        [Test]
        public void MergeEvents_ContiguousFrames_JoinIntoOneEvent()
        {
            var flags = new List<(int frame, string kind, float lowest)>
            {
                (10, FootDefectScan.Penetration, -0.003f),
                (11, FootDefectScan.Penetration, -0.008f),
                (12, FootDefectScan.Penetration, -0.004f),
            };
            List<FootDefectScan.DefectEvent> events =
                FootDefectScan.MergeEvents(flags, true);
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].StartFrame, Is.EqualTo(10));
            Assert.That(events[0].EndFrame, Is.EqualTo(12));
            // 관통은 최저점이 최악 — peak는 가장 깊은 프레임.
            Assert.That(events[0].PeakFrame, Is.EqualTo(11));
            Assert.That(events[0].WorstMeters,
                Is.EqualTo(-0.008f).Within(0.0001f));
        }

        [Test]
        public void MergeEvents_GapOrKindChange_SplitsEvents()
        {
            var flags = new List<(int frame, string kind, float lowest)>
            {
                (10, FootDefectScan.Penetration, -0.008f),
                // 11은 정상(널) — 연속이 끊겨 12는 새 이벤트.
                (12, FootDefectScan.Penetration, -0.006f),
                (13, FootDefectScan.FloatingInPlant, 0.03f),
                (14, FootDefectScan.FloatingInPlant, 0.05f),
            };
            List<FootDefectScan.DefectEvent> events =
                FootDefectScan.MergeEvents(flags, false);
            Assert.That(events.Count, Is.EqualTo(3));
            // 붕뜸은 최고점이 최악.
            Assert.That(events[2].PeakFrame, Is.EqualTo(14));
            Assert.That(events[2].WorstMeters,
                Is.EqualTo(0.05f).Within(0.0001f));
        }

        [Test]
        public void ClassifyMotion_PinnedDominant_ReturnsPinned()
        {
            Assert.That(FootDefectScan.ClassifyMotion(0.7f, 0.1f),
                Is.EqualTo(FootDefectScan.MotionPinned));
        }

        [Test]
        public void ClassifyMotion_RollDominant_ReturnsRoll()
        {
            Assert.That(FootDefectScan.ClassifyMotion(0.2f, 0.65f),
                Is.EqualTo(FootDefectScan.MotionRoll));
        }

        [Test]
        public void ClassifyMotion_BelowThreshold_ReturnsNull()
        {
            // 둘 다 임계 미만이면 정직한 기권 — 사람에게 넘긴다.
            Assert.That(FootDefectScan.ClassifyMotion(0.59f, 0.3f), Is.Null);
            Assert.That(FootDefectScan.ClassifyMotion(0.3f, 0.59f), Is.Null);
        }

        [Test]
        public void IsPinnedFrame_RequiresStillness()
        {
            Assert.That(FootDefectScan.IsPinnedFrame(
                    0.02f, 0.02f, 0.05f), Is.True);
            // 발목이 움직이면 고정이 아님(발끝만 정지는 축 회전 영역).
            Assert.That(FootDefectScan.IsPinnedFrame(
                    0.08f, 0.02f, 0.05f), Is.False);
            // 발끝이 수평으로 미끄러지면 고정 아님 — 슬라이드 후보.
            Assert.That(FootDefectScan.IsPinnedFrame(
                    0.02f, 0.08f, 0.05f), Is.False);
        }

        [Test]
        public void IsRollFrame_RequiresStillToeAndHeelLift()
        {
            // 수직비율 0.6 이상 + 발끝 정지 = 발끝 축 롤.
            Assert.That(FootDefectScan.IsRollFrame(
                    0.7f, 0.02f, 0.05f), Is.True);
            // 수평으로 미끄러지는 발끝은 롤이 아니라 슬라이드 후보.
            Assert.That(FootDefectScan.IsRollFrame(
                    0.7f, 0.08f, 0.05f), Is.False);
            // 발이 평평(비율 낮음)하면 롤 아님.
            Assert.That(FootDefectScan.IsRollFrame(
                    0.3f, 0.02f, 0.05f), Is.False);
        }

        [Test]
        public void RollRatio_VerticalHeelLift_ApproachesOne()
        {
            var foot = new UnityEngine.Vector3(0f, 0.08f, 0f);
            var toe = new UnityEngine.Vector3(0f, 0.01f, 0.1f);
            float ratio = FootDefectScan.RollRatio(foot, toe);
            // 세그먼트 길이 대비 수직 성분이 클수록 1에 가까움.
            Assert.That(ratio, Is.GreaterThan(0.5f));
            // 같은 높이면 0.
            var flat = new UnityEngine.Vector3(0f, 0.01f, 0f);
            Assert.That(FootDefectScan.RollRatio(flat,
                new UnityEngine.Vector3(0f, 0.01f, 0.12f)),
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void BuildSimRow_LowestFootDecidesSideAndAdjust()
        {
            var row = FootDefectScan.BuildSimRow(100,
                new UnityEngine.Vector3(0f, 0.10f, 0f),
                new UnityEngine.Vector3(0f, 0.02f, 0.1f),
                new UnityEngine.Vector3(0f, 0.05f, 0f),
                new UnityEngine.Vector3(0f, 0.04f, 0.1f),
                inPlantLeft: false, inPlantRight: true);
            // 왼발 최저점 0.02 < 오른발 0.04 → 왼발이 보정 대상.
            Assert.That(row.LowIsLeft, Is.True);
            Assert.That(row.LowestBottomY, Is.EqualTo(0.02f).Within(0.0001f));
            // 보정 수요 = 목표면(0) - 최저점 = -0.02 → 끌어내림.
            Assert.That(row.PredAdjust, Is.EqualTo(-0.02f).Within(0.0001f));
            // 오른발이 plant 확정이므로 무료 지지 플래그는 안 켠다.
            Assert.That(row.GroundWhileNoPlant, Is.False);
        }

        [Test]
        public void BuildSimRow_NoPlantWithLiftedFeet_FlagsFreeSupport()
        {
            // 양발 모두 plant 미확정 + 최저 발바닥이 데드존 이상 들림
            // → 보정기가 무료로 지지를 만들려는 구간.
            var row = FootDefectScan.BuildSimRow(200,
                new UnityEngine.Vector3(0f, 0.03f, 0f),
                new UnityEngine.Vector3(0f, 0.02f, 0.1f),
                new UnityEngine.Vector3(0f, 0.05f, 0f),
                new UnityEngine.Vector3(0f, 0.04f, 0.1f),
                inPlantLeft: false, inPlantRight: false);
            Assert.That(row.GroundWhileNoPlant, Is.True);
            // 데드존(5mm) 이내 미세 들림은 플래그 안 켠다.
            var tiny = FootDefectScan.BuildSimRow(201,
                new UnityEngine.Vector3(0f, 0.003f, 0f),
                new UnityEngine.Vector3(0f, 0.002f, 0.1f),
                new UnityEngine.Vector3(0f, 0.05f, 0f),
                new UnityEngine.Vector3(0f, 0.04f, 0.1f),
                false, false);
            Assert.That(tiny.GroundWhileNoPlant, Is.False);
        }

        [Test]
        public void SimRows_WriteThenRead_RoundTrips()
        {
            string path = Path.Combine(
                Path.GetTempPath(), "grounding-sim-test.csv");
            var rows = new List<FootDefectScan.SimRow>
            {
                FootDefectScan.BuildSimRow(50,
                    new UnityEngine.Vector3(0f, 0.03f, 0f),
                    new UnityEngine.Vector3(0f, 0.02f, 0.1f),
                    new UnityEngine.Vector3(0f, 0.05f, 0f),
                    new UnityEngine.Vector3(0f, 0.04f, 0.1f),
                    true, false),
            };
            try
            {
                FootDefectScan.WriteSimRows(path, rows);
                var read = FootDefectScan.ReadSimRows(path);
                Assert.That(read.Count, Is.EqualTo(1));
                Assert.That(read[0].Frame, Is.EqualTo(50));
                Assert.That(read[0].LowIsLeft, Is.True);
                Assert.That(read[0].LowestBottomY,
                    Is.EqualTo(0.02f).Within(0.0001f));
                Assert.That(read[0].PredAdjust,
                    Is.EqualTo(-0.02f).Within(0.0001f));
                Assert.That(read[0].InPlantLeft, Is.True);
                Assert.That(read[0].InPlantRight, Is.False);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void WriteThenRead_RoundTripsEvents()
        {
            string path = Path.Combine(
                Path.GetTempPath(), "foot-defects-test.csv");
            var events = new List<FootDefectScan.DefectEvent>
            {
                new FootDefectScan.DefectEvent
                {
                    IsLeft = true, Kind = FootDefectScan.Penetration,
                    StartFrame = 100, EndFrame = 104, PeakFrame = 102,
                    WorstMeters = -0.0082f,
                },
            };
            try
            {
                FootDefectScan.WriteEvents(path, events);
                List<FootDefectScan.DefectEvent> read =
                    FootDefectScan.ReadEvents(path);
                Assert.That(read.Count, Is.EqualTo(1));
                Assert.That(read[0].IsLeft, Is.True);
                Assert.That(read[0].Kind, Is.EqualTo(FootDefectScan.Penetration));
                Assert.That(read[0].EndFrame, Is.EqualTo(104));
                Assert.That(read[0].WorstMeters,
                    Is.EqualTo(-0.0082f).Within(0.0005f));
                // 겹침 카운트도 같은 데이터로 검증.
                Assert.That(FootDefectScan.CountOverlapping(read, 90, 105, true),
                    Is.EqualTo(1));
                Assert.That(FootDefectScan.CountOverlapping(read, 90, 105, false),
                    Is.EqualTo(0));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
