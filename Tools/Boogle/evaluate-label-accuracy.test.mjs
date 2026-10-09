import test from "node:test";
import assert from "node:assert/strict";
import { loadLabelRows, unfold, confusion, intervalIoU,
  evaluateRun } from "./evaluate-label-accuracy.mjs";

const HEADER = '"from_frame","to_frame","side","contact_label",' +
  '"motion_label","reviewer","notes"';
const row = (f0, f1, side, contact, reviewer) =>
  `"${f0}","${f1}","${side}","${contact}","","${reviewer}",""`;

test("loadLabelRows는 어휘와 reviewer를 읽음", () => {
  const rows = loadLabelRows(HEADER + "\n" + row(10, 20, "left", "발 전체", "user"));
  assert.equal(rows.length, 1);
  assert.equal(rows[0].reviewer, "user");
  assert.equal(rows[0].contact, "발 전체");
});

test("unfold는 접지 어휘를 프레임 진리값으로 펼치고 불확실은 제외", () => {
  const csv = HEADER + "\n" + row(5, 7, "left", "발 전체", "a") + "\n" +
    row(0, 3, "left", "불확실", "a");
  const truth = unfold(loadLabelRows(csv), 100).get("a").left;
  assert.equal(truth[4], undefined);
  assert.equal(truth[5], true);
  assert.equal(truth[7], true);
  assert.equal(truth[8], undefined);
});

test("confusion은 진리값이 정의된 프레임에서만 계산", () => {
  const pred = [true, true, false, undefined];
  const truth = [true, false, true, true];
  const c = confusion(pred, truth);
  assert.deepEqual([c.tp, c.fp, c.fn, c.tn], [1, 1, 1, 0]);
});

test("intervalIoU는 겹침 비율을 반환", () => {
  const a = loadLabelRows(HEADER + "\n" + row(0, 10, "left", "발 전체", "a"));
  const b = loadLabelRows(HEADER + "\n" + row(5, 15, "left", "발 전체", "b"));
  const [pair] = intervalIoU(a, b, "left");
  // 교집합 6프레임(5~10) / 합집합 16프레임(0~15)
  assert.equal(pair.iou, 0.375);
});

test("evaluateRun은 두 소스의 비교와 커버리지를 산출", () => {
  const csv = HEADER + "\n" +
    row(100, 110, "left", "발 전체", "ref-video") + "\n" +
    row(105, 115, "left", "발 전체", "user") + "\n" +
    row(200, 205, "right", "공중", "user");
  const result = evaluateRun(csv, 300);
  assert.equal(result.status, "OK");
  assert.deepEqual(result.reviewers.sort(), ["ref-video", "user"]);
  const left = result.comparisons.find(c => c.side === "left");
  // 105~110: 둘 다 양성 → tp 6. 111~115는 ref-video에 표식 자체가 없으므로
  // "증거 없음"이지 음성 확정이 아니라 fn으로 세지 않음.
  assert.equal(left.tp, 6);
  assert.equal(left.fn, 0);
  const cov = result.coverage.find(c => c.from === "ref-video" &&
    c.side === "left");
  assert.equal(cov.covered, 1);
  assert.equal(cov.coverage, 1);
});

test("겹치지 않는 라벨은 카운트 0으로 보고", () => {
  const csv = HEADER + "\n" +
    row(100, 110, "left", "발 전체", "ref-video") + "\n" +
    row(500, 505, "left", "발 전체", "user");
  const result = evaluateRun(csv, 1000);
  const left = result.comparisons.find(c => c.side === "left");
  assert.equal(left.tp + left.fp + left.fn + left.tn, 0);
});
