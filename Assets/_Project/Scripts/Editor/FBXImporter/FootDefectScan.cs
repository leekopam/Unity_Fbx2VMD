// 발 결함 스캔(로컬·결정론): 원본 클립 전체 프레임을 훑어 관통·붕뜸·
// 지지 미분류 저고도 프레임을 찾아 foot-defects.csv로 남긴다.
// 바닥 기준은 추정기와 같은 궤적 10백분위 — y=0이 아니므로 소스에
// 베이크된 지면 오프셋이 있어도 오탐하지 않는다. Jev·API와 무관하다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Fbx2Vmd.EditorTools.FBXImporter
{
    internal static class FootDefectScan
    {
        internal const string FileName = "foot-defects.csv";
        internal const string Header =
            "side,kind,start_frame,end_frame,peak_frame,worst_mm,frames";

        internal const string Penetration = "penetration";
        internal const string FloatingInPlant = "floating_in_plant";
        internal const string NearFloorUnsupported = "near_floor_unsupported";

        // 보정 시뮬레이션 산출물 — 스윕이 잰 최저 발바닥 높이에서
        // ApplyRaycastGrounding(루트를 y=0 방향으로 당기는 경로)이 프레임마다
        // 가할 루트 보정 수요를 미리 계산해 둔다. 읽기 전용 진단용.
        internal const string SimFileName = "grounding-sim.csv";
        internal const string SimHeader =
            "frame,low_side,lowest_bottom_y_m,pred_root_adjust_m," +
            "in_plant_left,in_plant_right,ground_while_no_plant";
        // PoseSpaceRetargeter의 groundingDeadZone 기본값과 같은 수치.
        internal const float GroundingDeadZoneMeters = 0.005f;
        // 추정기 롤 증거와 같은 비율 — 발목-발끝 세그먼트 수직 비율.
        internal const float RollPitchRatio = 0.6f;
        // 스팬의 이 비율 이상 프레임이 한 패턴이어야 그 동작으로 채운다.
        internal const float MotionFractionThreshold = 0.6f;
        internal const string MotionPinned = "고정";
        internal const string MotionRoll = "구르기";

        // 바닥·지지·공중 임계는 HumanoidFootContactIntentEstimator와 같은 값을
        // 유지한다(추정기는 private이라 같은 비율을 여기에 둠).
        internal const float FloorPercentile = 0.1f;
        internal const float SupportHeightPerHumanScale = 0.015f;
        internal const float SupportSpeedPerHumanScale = 0.05f;
        internal const float AirborneHeightPerHumanScale = 0.05f;
        // 추정 바닥 아래로 이 깊이 이상이어야 관통. 백분위 바닥 특성상
        // 표본의 일부가 항상 0 아래에 오므로 얕은 음수는 노이즈로 본다.
        internal const float PenetrationDepthPerHumanScale = 0.005f;

        internal sealed class DefectEvent
        {
            public bool IsLeft;
            public string Kind;
            public int StartFrame;
            public int EndFrame;
            public int PeakFrame;
            public float WorstMeters;
            public int Frames => EndFrame - StartFrame + 1;
        }

        // 추정기와 같은 10백분위 바닥 — 발목·발끝 궤적을 각각 따로 추정한다.
        internal static float EstimateFloor(float[] heights)
        {
            if (heights == null || heights.Length == 0) return 0f;
            var sorted = (float[])heights.Clone();
            Array.Sort(sorted);
            return sorted[(int)((sorted.Length - 1) * FloorPercentile)];
        }

        // 한 발의 한 프레임 판정. footH·toeH는 추정 바닥 대비 높이(m),
        // inPlant는 그 프레임이 confident plant 의도 안인지 여부.
        // 우선순위: 관통(바닥 아래) > 지지 중 붕뜸 > 저고도 미분류.
        internal static string Classify(float footHeight, float toesHeight,
            bool inPlant, float supportHeight, float airborneHeight,
            float penetrationDepth)
        {
            float lowest = Mathf.Min(footHeight, toesHeight);
            if (lowest < -penetrationDepth)
            {
                return Penetration;
            }
            // 지지 확정 구간에서 최저점이 지지 높이를 넘으면 보정이 걸렸어야 할
            // 발이 떠 있는 것 — 원본 결함이든 추정 오인이든 표시 가치가 있다.
            if (inPlant && lowest > supportHeight)
            {
                return FloatingInPlant;
            }
            // 발이 공중 임계 이하로 낮은데 지지로 분류되지 않은 프레임 —
            // 핀이 안 걸려 붕뜸이 보정 없이 지나가는 주력 후보.
            if (!inPlant && lowest <= airborneHeight)
            {
                return NearFloorUnsupported;
            }
            return null;
        }

        // 프레임 순서대로 온 (frame, kind, lowest)을 같은 발·같은 종류의
        // 연속 프레임 이벤트로 묶는다. 정상 프레임이 끼면 새 이벤트로 분리.
        internal static List<DefectEvent> MergeEvents(
            IReadOnlyList<(int frame, string kind, float lowest)> flags,
            bool isLeft)
        {
            var events = new List<DefectEvent>();
            DefectEvent open = null;
            foreach ((int frame, string kind, float lowest) in flags)
            {
                if (kind == null) continue;
                if (open != null && open.Kind == kind &&
                    frame == open.EndFrame + 1)
                {
                    open.EndFrame = frame;
                    if (IsWorse(kind, lowest, open.WorstMeters))
                    {
                        open.WorstMeters = lowest;
                        open.PeakFrame = frame;
                    }
                    continue;
                }
                open = new DefectEvent
                {
                    IsLeft = isLeft,
                    Kind = kind,
                    StartFrame = frame,
                    EndFrame = frame,
                    PeakFrame = frame,
                    WorstMeters = lowest,
                };
                events.Add(open);
            }
            return events;
        }

        // 관통·저고도는 더 낮을수록, 지지 중 붕뜸은 더 높을수록 나쁜 값.
        private static bool IsWorse(string kind, float height, float worst) =>
            kind == FloatingInPlant ? height > worst : height < worst;

        internal sealed class SimRow
        {
            public int Frame;
            public bool LowIsLeft;
            public float LowestBottomY;
            public float PredAdjust;
            public bool InPlantLeft;
            public bool InPlantRight;
            public bool GroundWhileNoPlant =>
                !InPlantLeft && !InPlantRight &&
                Mathf.Abs(PredAdjust) > GroundingDeadZoneMeters;
        }

        // 추정기 롤 증거와 같은 정의: 발목-발끝 세그먼트의 수직 비율.
        internal static float RollRatio(Vector3 foot, Vector3 toe)
        {
            float length = Vector3.Distance(foot, toe);
            return length > 0.0001f ? (foot.y - toe.y) / length : 0f;
        }

        // 고정: 발목 3D 속도·발끝 수평속도가 모두 지지 속도 이하.
        // 접촉 여부는 호출부가 이미 확정한 스팬에만 적용하므로 절대 높이는
        // 보지 않는다 — 클립마다 백분위 바닥 추정이 어긋나 오탐을 만든다.
        internal static bool IsPinnedFrame(
            float footSpeed, float toesSpeedH, float supportSpeed) =>
            footSpeed <= supportSpeed && toesSpeedH <= supportSpeed;

        // 구르기: 발끝이 축처럼 머문 채 발목이 발끝 위로 들린 프레임.
        internal static bool IsRollFrame(float rollRatio,
            float toesSpeedH, float supportSpeed) =>
            toesSpeedH <= supportSpeed && rollRatio >= RollPitchRatio;

        // 스팬의 고정/구르기 프레임 비율로 동작을 정한다. 둘 다 임계
        // 미만이면 null — 의도된 이동과 foot-skate 결함은 수치로 못
        // 가리므로 사람에게 넘기는 정직한 기권.
        internal static string ClassifyMotion(float pinnedFraction,
            float rollFraction)
        {
            if (pinnedFraction >= MotionFractionThreshold) return MotionPinned;
            if (rollFraction >= MotionFractionThreshold) return MotionRoll;
            return null;
        }

        // 한 프레임에서 ApplyRaycastGrounding이 가할 수요를 계산한다.
        // 최저 발바닥 근사는 발목·발끝 중 낮은 쪽(실제는 발목-발반지름이지만
        // 측정 가능한 최저점이 더 보수적인 프록시). predAdjust는
        // 목표면(y=0) - 최저점 — 양수면 들어올림, 음수면 끌어내림.
        internal static SimRow BuildSimRow(int frame,
            Vector3 leftFoot, Vector3 leftToe,
            Vector3 rightFoot, Vector3 rightToe,
            bool inPlantLeft, bool inPlantRight)
        {
            float leftLow = Mathf.Min(leftFoot.y, leftToe.y);
            float rightLow = Mathf.Min(rightFoot.y, rightToe.y);
            bool lowIsLeft = leftLow <= rightLow;
            float lowest = Mathf.Min(leftLow, rightLow);
            return new SimRow
            {
                Frame = frame,
                LowIsLeft = lowIsLeft,
                LowestBottomY = lowest,
                PredAdjust = 0f - lowest,
                InPlantLeft = inPlantLeft,
                InPlantRight = inPlantRight,
            };
        }

        internal static string FormatSimRow(SimRow r) =>
            string.Join(",",
                r.Frame.ToString(CultureInfo.InvariantCulture),
                r.LowIsLeft ? "left" : "right",
                r.LowestBottomY.ToString("0.#####",
                    CultureInfo.InvariantCulture),
                r.PredAdjust.ToString("0.#####",
                    CultureInfo.InvariantCulture),
                r.InPlantLeft ? "1" : "0",
                r.InPlantRight ? "1" : "0",
                r.GroundWhileNoPlant ? "1" : "0");

        internal static void WriteSimRows(string path,
            IReadOnlyList<SimRow> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine(SimHeader);
            foreach (SimRow r in rows)
            {
                builder.AppendLine(FormatSimRow(r));
            }
            WriteAllTextAtomic(path, builder.ToString());
        }

        // 진단 도구 대칭용 역파서 — 깨진 행은 건너뛴다.
        internal static List<SimRow> ReadSimRows(string path)
        {
            var rows = new List<SimRow>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return rows;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception error) when (error is IOException ||
                error is UnauthorizedAccessException)
            {
                return rows;
            }
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("frame,", StringComparison.Ordinal))
                {
                    continue;
                }
                string[] cells = line.Split(',');
                if (cells.Length < 7) continue;
                if (!int.TryParse(cells[0], out int frame) ||
                    !float.TryParse(cells[2], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float lowest) ||
                    !float.TryParse(cells[3], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float adjust))
                {
                    continue;
                }
                rows.Add(new SimRow
                {
                    Frame = frame,
                    LowIsLeft = cells[1] == "left",
                    LowestBottomY = lowest,
                    PredAdjust = adjust,
                    InPlantLeft = cells[4] == "1",
                    InPlantRight = cells[5] == "1",
                });
            }
            return rows;
        }

        // 절반만 쓰인 파일이 남지 않게 임시 파일에 쓴 뒤 교체한다.
        internal static void WriteAllTextAtomic(string path, string content)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        internal static string FormatRow(DefectEvent e) =>
            string.Join(",",
                e.IsLeft ? "left" : "right",
                e.Kind,
                e.StartFrame.ToString(CultureInfo.InvariantCulture),
                e.EndFrame.ToString(CultureInfo.InvariantCulture),
                e.PeakFrame.ToString(CultureInfo.InvariantCulture),
                (e.WorstMeters * 1000f).ToString("0.0",
                    CultureInfo.InvariantCulture),
                e.Frames.ToString(CultureInfo.InvariantCulture));

        // 라벨링 창 배지용 역파서. 파일이 없거나 행이 깨져도 빈 목록으로 둔다.
        internal static List<DefectEvent> ReadEvents(string path)
        {
            var events = new List<DefectEvent>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return events;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception error) when (error is IOException ||
                error is UnauthorizedAccessException)
            {
                return events;
            }
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("side,", StringComparison.Ordinal))
                {
                    continue;
                }
                string[] cells = line.Split(',');
                if (cells.Length < 7) continue;
                if (!int.TryParse(cells[2], out int start) ||
                    !int.TryParse(cells[3], out int end) ||
                    !int.TryParse(cells[4], out int peak) ||
                    !float.TryParse(cells[5], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float worstMm))
                {
                    continue;
                }
                events.Add(new DefectEvent
                {
                    IsLeft = cells[0] == "left",
                    Kind = cells[1],
                    StartFrame = start,
                    EndFrame = end,
                    PeakFrame = peak,
                    WorstMeters = worstMm / 1000f,
                });
            }
            return events;
        }

        internal static void WriteEvents(string path,
            IReadOnlyList<DefectEvent> events)
        {
            var builder = new StringBuilder();
            builder.AppendLine(Header);
            foreach (DefectEvent e in events)
            {
                builder.AppendLine(FormatRow(e));
            }
            WriteAllTextAtomic(path, builder.ToString());
        }

        // 스팬 표시용 — 스팬과 겹치는 이벤트 수.
        internal static int CountOverlapping(IReadOnlyList<DefectEvent> events,
            int from, int to, bool isLeft)
        {
            if (events == null) return 0;
            int count = 0;
            foreach (DefectEvent e in events)
            {
                if (e.IsLeft == isLeft && e.StartFrame <= to && e.EndFrame >= from)
                {
                    count++;
                }
            }
            return count;
        }

        internal static string KindLabel(string kind) =>
            kind == Penetration ? "바닥 관통" :
            kind == FloatingInPlant ? "지지 중 붕뜸" :
            kind == NearFloorUnsupported ? "지지 미분류 저고도" : kind;
    }
}
