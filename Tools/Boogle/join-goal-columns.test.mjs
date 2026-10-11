import assert from "node:assert/strict";
import { test } from "node:test";
import { indexGoalStream, joinGoalColumns, goalColumns }
  from "./join-goal-columns.mjs";

const csv = [
  "frame,side,foot_x_m",
  "0,left,0.1",
  "0,right,0.2",
  "1,left,0.3",
  "1,right,0.4",
].join("\n");

const jsonl = [
  JSON.stringify({ frame: 0, side: "left", pos: [0.11, 0.05, 0.01],
    rot: [-140, 0, 0], toe: [0.11, 0.02, 0.2], sole_offset: 0.03 }),
  JSON.stringify({ frame: 1, side: "right", pos: [0.41, 0.05, 0.01],
    rot: [-138, 2, 0], toe: [0.41, 0.02, 0.2], sole_offset: 0.03 }),
].join("\n");

test("goal 스트림을 프레임·측 키로 색인한다", () => {
  const cells = indexGoalStream(jsonl);
  assert.equal(cells.get("0:left").pos[0], 0.11);
  assert.equal(cells.get("1:right").rot[0], -138);
  assert.equal(cells.size, 2);
});

test("조인은 원본 열을 보존하고 goal_* 열을 붙인다", () => {
  const { csv: out, matched, total } = joinGoalColumns(csv, jsonl);
  assert.equal(matched, 2);
  assert.equal(total, 4);
  const lines = out.split("\n");
  assert.ok(lines[0].endsWith(goalColumns.join(",")));
  const left0 = lines[1].split(",");
  assert.equal(left0[2], "0.1"); // 원본 foot_x_m 보존
  assert.equal(left0[4], "0.05"); // goal_y_m
  const missing = lines[2].split(","); // 0:right는 goal 없음
  assert.equal(missing[missing.length - 1], "");
});

test("잘못된 스트림 레코드를 거절한다", () => {
  assert.throws(() => indexGoalStream('{"frame":"x","side":"left"}'), /유효/);
  assert.throws(() => indexGoalStream("not-json"), /파싱/);
  assert.throws(() => joinGoalColumns("", jsonl), /헤더/);
});
