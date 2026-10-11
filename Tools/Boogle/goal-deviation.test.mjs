import assert from "node:assert/strict";
import { test } from "node:test";
import { reportGoalDeviation } from "./goal-deviation.mjs";

const header = "frame,side,foot_x_m,foot_y_m,foot_z_m," +
  "goal_x_m,goal_y_m,goal_z_m,hips_y_m,foot_pitch_deg,goal_pitch_deg";

function csv(entries) {
  return [header, ...entries.map(e => e.join(","))].join("\n");
}

test("goal과 출력이 같으면 편차 0·전 프레임 도달로 보고한다", () => {
  const result = reportGoalDeviation(csv([
    [0, "left", 0, 0.05, 0, 0, 0.05, 0, 1.0, -140, -140],
    [0, "right", 0.1, 0.05, 0, 0.1, 0.05, 0, 1.0, -140, -140],
    [1, "left", 0, 0.05, 0, 0, 0.05, 0, 1.0, -140, -140],
  ]));
  assert.equal(result.status, "OK");
  assert.equal(result.sides.left.distance_mm.max, 0);
  assert.equal(result.sides.left.approx_reachable_ratio, 1);
});

test("goal이 출력보다 높으면 수직 편차가 양수다", () => {
  const result = reportGoalDeviation(csv([
    [0, "left", 0, 0.05, 0, 0, 0.08, 0, 1.0, -140, -140],
  ]));
  assert.equal(result.sides.left.vertical_mm.mean, 30);
  assert.equal(result.sides.left.distance_mm.mean, 30);
});

test("goal 열이 비어 있으면 해당 행을 건너뛴다", () => {
  const result = reportGoalDeviation(csv([
    [0, "left", 0, 0.05, 0, "", "", "", 1.0, -140, ""],
    [1, "left", 0, 0.05, 0, 0, 0.05, 0, 1.0, -140, -140],
  ]));
  assert.equal(result.sides.left.distance_mm.count, 1);
});

test("필수 열이 없거나 비어 있으면 BLOCKED다", () => {
  assert.equal(reportGoalDeviation("frame,side\n0,left").status, "BLOCKED");
  assert.equal(reportGoalDeviation("").status, "BLOCKED");
});
