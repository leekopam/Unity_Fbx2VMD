import assert from "node:assert/strict";
import test from "node:test";
import { corpusDeltaPercentile, euclidean, knnMeanDistance, parseMuscleLines,
  scoreFrames, subsample } from "./pose-novelty.mjs";

const dims = 8;
const base = Array.from({ length: dims }, () => 0.2);

// 코퍼스: base 근처의 정상 자세들. 대상: base에 가까운 정상 + 완전히 다른 이상 자세.
function corpusVectors(count = 200) {
  return Array.from({ length: count }, (_, index) =>
    base.map((v, d) => v + 0.001 * ((index + d) % 7)));
}
function line(frame, muscles) {
  return JSON.stringify({ frame, muscles }) + "\n";
}

test("정상 자세는 낮은 novelty, 이형 자세는 높은 novelty", () => {
  const corpus = corpusVectors();
  const normal = knnMeanDistance(base.map(v => v + 0.002), corpus);
  const alien = knnMeanDistance(base.map(() => 1), corpus);
  assert.ok(normal < 0.01);
  assert.ok(alien > 0.5);
  assert.ok(alien > normal * 20);
});

test("전이 점수는 코퍼스 변화량 대비로 정규화됨", () => {
  const calm = [line(0, base), line(1, base.map(v => v + 0.001)),
    line(2, base.map(v => v + 0.002))].join("");
  const jump = [line(0, base), line(1, base.map(() => 0.9))].join("");
  const calmFrames = parseMuscleLines(calm);
  const corpus = { vectors: corpusVectors(), sequences: [calmFrames] };
  const p95 = corpusDeltaPercentile(corpus.sequences);
  const calmScores = scoreFrames(calmFrames, corpus, p95);
  const jumpScores = scoreFrames(parseMuscleLines(jump), corpus, p95);
  assert.ok(calmScores.get(1).transition_novelty <= 1);
  assert.ok(jumpScores.get(1).transition_novelty > 100);
});

test("프레임 불연속 구간은 전이 점수를 내지 않음", () => {
  const frames = parseMuscleLines(line(0, base) + line(5, base));
  const corpus = { vectors: corpusVectors(), sequences: [frames] };
  const scores = scoreFrames(frames, corpus, corpusDeltaPercentile([frames]));
  assert.equal(scores.get(5).transition_novelty, null);
});

test("subsample은 균등 스트라이드로 상한을 지킴", () => {
  const vectors = corpusVectors(1000);
  const picked = subsample(vectors, 100);
  assert.equal(picked.length, 100);
  assert.deepEqual(picked[0], vectors[0]);
  assert.equal(subsample(vectors, 2000).length, 1000);
});

test("euclidean·knn 기본 성질", () => {
  assert.equal(euclidean([0, 0], [3, 4]), 5);
  assert.equal(knnMeanDistance(base, []), null);
  assert.equal(knnMeanDistance(base, [base], 5), 0);
});
