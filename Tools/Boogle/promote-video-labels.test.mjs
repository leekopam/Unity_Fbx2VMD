import assert from "node:assert/strict";
import test from "node:test";
import { classifyMotion, parseIntervals, promoteVideoLabels,
  toUnityFrame } from "./promote-video-labels.mjs";

const intervalsCsv = [
  "foot,f0_video60,f1_video60,f0_vmd30,f1_vmd30,len60,confidence",
  "L,1331,1352,665,676,22,2vote",   // vmd 665-676 → unity 1330-1352 (60fps)
  "R,1400,1420,700,710,21,2vote",
  "L,200,220,100,110,21,1vote",    // confidence 미달 → 보류
  "L,-400,-300,-200,-150,21,2vote" // 완전 음수 → 스킵
].join("\n");

const frameHeader = "frame,side,has_ground,foot_x_m,foot_z_m";
function framesCsv() {
  const lines = [frameHeader];
  for (let frame = 0; frame < 3000; frame++) {
    for (const side of ["left", "right"]) {
      // left 1330-1352 구간은 이동 없음, right 1400-1420은 0.2m 이동
      const moving = side === "right" && frame >= 1400 && frame <= 1420;
      const x = moving ? (0.1 + (frame - 1400) * 0.01) : 0.1;
      lines.push([frame, side, "True", x.toFixed(3), "0.2"].join(","));
    }
  }
  return lines.join("\n") + "\n";
}
const labelsCsv = '"from_frame","to_frame","side","contact_label","motion_label","reviewer","notes"\n';
const state = { clip_frame_rate: 60, last_frame: 2999, source_human_scale: 1 };

test("vmd30 → unity 프레임 변환", () => {
  assert.equal(toUnityFrame(665, 60), 1330);
  assert.equal(toUnityFrame(10, 30), 10);
});

test("2vote 구간만 승격, 음수·미달·겹침은 보류/스킵", () => {
  const result = promoteVideoLabels(intervalsCsv, framesCsv(), labelsCsv, state);
  assert.equal(result.promoted, 2);
  assert.equal(result.deferred, 1);
  assert.equal(result.skipped, 1);
  const lines = result.csvLines;
  assert.equal(lines.length, 2);
  assert.ok(lines[0].includes('"1330"') && lines[0].includes('"1352"'));
  assert.ok(lines[0].includes('"left"') && lines[0].includes('"발 전체"'));
  assert.ok(lines[0].includes('"고정"'));
  assert.ok(lines[1].includes('"의도된 이동"')); // right 구간 0.2m 이동
  assert.ok(lines[0].includes('"ref-video"'));
});

test("기존 사람 라벨과 겹치면 승격하지 않음", () => {
  const humanLabels = labelsCsv +
    '"1330","1340","left","뒤꿈치","고정","user_x","수동"\n';
  const result = promoteVideoLabels(intervalsCsv, framesCsv(), humanLabels, state);
  assert.equal(result.promoted, 1); // left 구간이 겹쳐 1건만
  assert.equal(result.deferred, 2);
});

test("동작 의도는 구간 수평 변위로 판정", () => {
  const rows = [
    { frame: 0, side: "left", foot_x_m: "0.10", foot_z_m: "0.20" },
    { frame: 1, side: "left", foot_x_m: "0.11", foot_z_m: "0.20" },
    { frame: 2, side: "left", foot_x_m: "0.10", foot_z_m: "0.20" },
  ];
  assert.equal(classifyMotion(rows, "left", 0, 2, 1).label, "고정");
  rows[1].foot_x_m = "0.30";
  assert.equal(classifyMotion(rows, "left", 0, 2, 1).label, "의도된 이동");
});

test("구간 파싱은 vmd 프레임만 요구", () => {
  const parsed = parseIntervals(intervalsCsv);
  assert.equal(parsed.length, 4);
  assert.equal(parsed[0].f0, 665);
});
