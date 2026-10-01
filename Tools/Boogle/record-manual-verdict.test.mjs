import assert from "node:assert/strict";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";
import { buildVerdictRecord, recordVerdict } from "./record-manual-verdict.mjs";

test("유효한 판정은 레코드를 만든다", () => {
  const record = buildVerdictRecord({
    run: "abc-123", verdict: "pass", step: "F10", frames: "1857,1881",
    reason: "접지 프레임에서 발이 바닥에 붙어 있음", reviewer: "user"
  }, new Date("2026-10-01T00:00:00Z"));
  assert.equal(record.run_id, "abc-123");
  assert.equal(record.verdict, "pass");
  assert.deepEqual(record.frames, [1857, 1881]);
  assert.equal(record.ts, "2026-10-01T00:00:00.000Z");
});

test("run 누락, 허용 외 verdict, 짧은 reason은 거부한다", () => {
  assert.throws(() => buildVerdictRecord({ verdict: "pass", reason: "열 자 이상의 근거입니다" }));
  assert.throws(() => buildVerdictRecord({ run: "r", verdict: "maybe", reason: "열 자 이상의 근거입니다" }));
  assert.throws(() => buildVerdictRecord({ run: "r", verdict: "fail", reason: "짧음" }));
});

test("frames가 정수가 아니면 거부한다", () => {
  assert.throws(() => buildVerdictRecord({ run: "r", verdict: "fail",
    frames: "abc", reason: "열 자 이상의 근거입니다" }));
});

test("recordVerdict는 JSONL에 1행을 추가한다", async (t) => {
  const dir = await mkdtemp(path.join(tmpdir(), "verdict-"));
  t.after(() => rm(dir, { recursive: true, force: true }));
  const file = path.join(dir, "manual-verdicts.jsonl");
  await recordVerdict({ run: "r1", verdict: "pass",
    reason: "첫 번째 관찰 근거입니다" }, file);
  await recordVerdict({ run: "r1", verdict: "fail", frames: "10",
    reason: "두 번째 관찰 근거입니다" }, file);
  const lines = (await readFile(file, "utf8")).trim().split("\n");
  assert.equal(lines.length, 2);
  assert.equal(JSON.parse(lines[1]).frames[0], 10);
});
