import assert from "node:assert/strict";
import test from "node:test";
import { buildConsensus, loadRuleVotes, loadUpVotes, rowsToCsv,
  truthToRows } from "./consensus-groundtruth.mjs";

const hdr = "frame,side,source_toes_y_m\n";

test("규칙 신호는 소스 발끝 높이를 접촉 의도로 읽음", () => {
  const votes = loadRuleVotes(hdr +
    "0,left,0.012\n0,right,0.12\n1,left,0.035\n");
  assert.equal(votes.left[0], true);   // 12mm → 접촉
  assert.equal(votes.right[0], false); // 120mm → 공중
  assert.equal(votes.left[1], undefined); // 35mm → 기권 구간
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

test("비사람 신호 2표 이상 만장일치만 정답 채택", () => {
  const { truth, spotCheck, stats } = buildConsensus({
    humanRows: [],
    videoRows: [{ f0: 0, f1: 1, side: "left", contact: "발 전체",
      reviewer: "ref-video" }],
    upVotes: { left: [true, false], right: [] },
    ruleVotes: { left: [true, false], right: [] },
    lastFrame: 1,
  });
  assert.equal(truth.left[0], true);   // video+up+rule 만장일치
  // f=1: video 양성 vs up·rule 음성 → 불일치, 미확정
  assert.equal(truth.left[1], undefined);
  assert.equal(stats.consensus, 1);
  assert.equal(spotCheck.length, 1);
});

test("신호가 갈리면 스팟 확인 큐로 보냄", () => {
  const { truth, spotCheck } = buildConsensus({
    humanRows: [],
    videoRows: [{ f0: 5, f1: 5, side: "right", contact: "앞꿈치",
      reviewer: "ref-video" }],
    upVotes: { left: [], right: [undefined, undefined, undefined, undefined, undefined, false] },
    ruleVotes: { left: [], right: [undefined, undefined, undefined, undefined, undefined, true] },
    lastFrame: 5,
  });
  assert.equal(truth.right[5], undefined);
  assert.equal(spotCheck.length, 1);
  assert.equal(spotCheck[0].frame, 5);
  assert.equal(spotCheck[0].side, "right");
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
