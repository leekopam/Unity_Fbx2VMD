import assert from "node:assert/strict";
import { test } from "node:test";
import { compareCaptures } from "./compare-captures.mjs";

const header = "frame,time_s,side,has_ground,support_role,foot_x_m,foot_y_m," +
  "foot_z_m,foot_pitch_deg,ankle_pitch_deg,minimum_signed_mm";

function row(frame, side, overrides = {}) {
  const base = {
    frame, time_s: frame / 60, side, has_ground: "True",
    support_role: "support", foot_x_m: 0, foot_y_m: 0.05, foot_z_m: 0,
    foot_pitch_deg: -140, ankle_pitch_deg: -140, minimum_signed_mm: 5,
  };
  return Object.values({ ...base, ...overrides }).join(",");
}

function csv(frames, overridesFor = () => ({})) {
  const lines = [header];
  for (const frame of frames) {
    lines.push(row(frame, "left", overridesFor(frame, "left")));
    lines.push(row(frame, "right", overridesFor(frame, "right")));
  }
  return lines.join("\n");
}

test("동일 입력은 결함 delta 0·위치 편차 0을 보고한다", () => {
  const identical = csv([0, 1, 2, 3]);
  const result = compareCaptures(identical, identical);
  assert.equal(result.status, "OK");
  assert.equal(result.defects_worsened, 0);
  assert.equal(result.deviation.distance_mm.left.max, 0);
  assert.equal(result.deviation.pitch_deg.left.max, 0);
});

test("후보에서 침투가 새로 생기면 defects_worsened가 증가한다", () => {
  const baseline = csv([0, 1, 2, 3]);
  // 프레임 1~2에서 발바닥이 임계(10mm) 아래로 파고든 후보
  const candidate = csv([0, 1, 2, 3], (frame) =>
    frame === 1 || frame === 2 ? { minimum_signed_mm: -20 } : {});
  const result = compareCaptures(baseline, candidate);
  assert.equal(result.status, "OK");
  assert.ok(result.defects["penetration:left"].b.frames >= 2);
  assert.ok(result.defects["penetration:left"].delta_frames > 0);
  assert.ok(result.defects_worsened >= 1);
});

test("후보에서 결함이 사라지면 delta_frames가 음수다", () => {
  const baseline = csv([0, 1, 2], (frame) =>
    frame === 1 ? { minimum_signed_mm: -30 } : {});
  const candidate = csv([0, 1, 2]);
  const result = compareCaptures(baseline, candidate);
  assert.equal(result.status, "OK");
  assert.equal(result.defects["penetration:left"].delta_frames, -1);
  assert.equal(result.defects_worsened, 0);
});

test("B 쪽 발 위치가 이동하면 프레임 대응 편차가 mm로 집계된다", () => {
  const baseline = csv([0, 1]);
  const candidate = csv([0, 1], () => ({ foot_x_m: 0.01 }));
  const result = compareCaptures(baseline, candidate);
  assert.equal(result.status, "OK");
  assert.equal(result.deviation.distance_mm.left.mean, 10);
  assert.equal(result.deviation.distance_mm.left.max, 10);
});

test("빈 CSV는 BLOCKED로 보고한다", () => {
  assert.equal(compareCaptures("", csv([0])).status, "BLOCKED");
  assert.equal(compareCaptures(csv([0]), "").status, "BLOCKED");
});
