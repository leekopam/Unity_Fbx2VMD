import assert from "node:assert/strict";
import test from "node:test";
import { buildConsensus, loadRuleVotes, loadUpVotes, rowsToCsv,
  truthToRows } from "./consensus-groundtruth.mjs";

const hdr = "frame,side,source_toes_y_m\n";

test("규칙 신호는 지속된 발끝 접촉만 양성으로 읽음", () => {
  // left: f0~7 12mm 8연속 → 양성, f10~12 3연속(0.05s) → 기권
  // right: f8~14 12mm 7연속 → 양성
  const rows = Array.from({ length: 15 }, (_, f) =>
    `${f},left,${f < 8 || (f >= 10 && f <= 12) ? "0.012" : "0.12"}\n` +
    `${f},right,${f >= 8 ? "0.012" : "0.12"}\n`).join("");
  const votes = loadRuleVotes(hdr + rows);
  assert.equal(votes.left[0], true);
  assert.equal(votes.left[7], true);
  assert.equal(votes.left[10], undefined); // 순간 스치기 기권
  assert.equal(votes.right[0], false);
  assert.equal(votes.right[10], true);
});

test("UnderPressure JSONL은 좌우 프레임별 진리값", () => {
  const votes = loadUpVotes(
    '{"frame":0,"left":true,"right":false}\n{"frame":1,"left":false,"right":true}\n');
  assert.equal(votes.left[0], true);
  assert.equal(votes.right[1], true);
  assert.equal(votes.left[1], false);
});

test("사람 라벨은 다른 신호보다 우선한다", () => {
  const { truth, stats } = buildConsensus({
    humanRows: [{ f0: 0, f1: 2, side: "left", contact: "공중",
      reviewer: "h" }],
    videoRows: [{ f0: 0, f1: 2, side: "left", contact: "발 전체",
      reviewer: "ref-video" }],
    upVotes: { left: [true, true, true], right: [] },
    ruleVotes: { left: [true, true, true], right: [] },
    lastFrame: 2,
  });
  // 영상+UP+규칙 전부 양성이어도 사람 음성이 정답
  assert.deepEqual(truth.left.slice(0, 3), [false, false, false]);
  assert.equal(stats.consensus, 0);
});

test("어느 신호든 양성이면 양성(OR), 충돌은 스팟큐에 기록", () => {
  const { truth, spotCheck, stats } = buildConsensus({
    humanRows: [],
    videoRows: [{ f0: 0, f1: 1, side: "left", contact: "발 전체",
      reviewer: "ref-video" }],
    upVotes: { left: [true, false], right: [] },
    ruleVotes: { left: [true, false], right: [] },
    lastFrame: 1,
  });
  assert.equal(truth.left[0], true);  // video+up+rule 양성
  assert.equal(truth.left[1], true);  // video 양성 단독 → OR 양성
  assert.equal(stats.consensus, 2);
  assert.equal(stats.disagreement, 1); // up·rule 음성 vs video 양성 충돌 기록
  assert.equal(spotCheck.length, 1);
  assert.equal(spotCheck[0].resolved, "positive_or");
});

test("전 신호 음성이면 음성, 기권만이면 미확정", () => {
  const { truth, stats } = buildConsensus({
    humanRows: [],
    videoRows: [],
    upVotes: { left: [false, undefined], right: [] },
    ruleVotes: { left: [false, undefined], right: [] },
    lastFrame: 1,
  });
  assert.equal(truth.left[0], false);  // up+rule 동시 음성
  assert.equal(truth.left[1], undefined); // 둘 다 기권 → 미확정
  assert.equal(stats.consensus, 1);
  // 미확정 = left f1 기권 + right f0/f1 무신호
  assert.equal(stats.unlabeled, 3);
});

test("진리값 배열은 연속 구간으로 압축되고 CSV로 나감", () => {
  const rows = truthToRows({
    left: [true, true, false, false, false, true],
    right: [false, false],
  });
  assert.deepEqual(rows.map(r => [r.f0, r.f1, r.side, r.contact]), [
    [0, 1, "left", "발 전체"], [2, 4, "left", "공중"],
    [5, 5, "left", "발 전체"], [0, 1, "right", "공중"]]);
  const csv = rowsToCsv(rows);
  assert.ok(csv.includes("0,1,left,발 전체"));
  assert.ok(csv.includes("consensus"));
});
