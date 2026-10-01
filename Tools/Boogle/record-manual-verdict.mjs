#!/usr/bin/env node
// MANUAL_REVIEW_REQUIRED 실행에 대한 사람 판정을 기계 판독 JSONL로 누적한다.
// 판정이 파일에 남으면 다음 세션이 같은 영상·프레임을 재판정 없이 인용할 수 있다.
//
// 사용:
//   node Tools/Boogle/record-manual-verdict.mjs --run <runId> --verdict pass|fail|uncertain
//       [--step F10] [--frames 1857,1881] --reason "관찰 근거" [--reviewer 이름]
import { appendFile, mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const verdictPath = path.join(projectRoot,
  "Docs/Workflow/Local/artifacts/harness/manual-verdicts.jsonl");
const verdicts = new Set(["pass", "fail", "uncertain"]);

function parseArgs(argv) {
  const args = {};
  for (let index = 0; index < argv.length; index += 2) {
    const key = argv[index];
    if (!key.startsWith("--") || index + 1 >= argv.length)
      throw new Error(`인자 쌍이 올바르지 않습니다: ${key}`);
    args[key.slice(2)] = argv[index + 1];
  }
  return args;
}

export function buildVerdictRecord(args, now = new Date()) {
  if (typeof args.run !== "string" || !args.run.trim())
    throw new Error("--run(runId)이 필요합니다.");
  if (!verdicts.has(args.verdict))
    throw new Error(`--verdict는 ${[...verdicts].join("/")} 중 하나여야 합니다.`);
  if (typeof args.reason !== "string" || args.reason.trim().length < 10)
    throw new Error("--reason에 10자 이상의 관찰 근거가 필요합니다.");
  const frames = args.frames === undefined ? null : args.frames.split(",").map(value => {
    const frame = Number(value.trim());
    if (!Number.isInteger(frame) || frame < 0)
      throw new Error(`--frames의 값이 정수가 아닙니다: ${value}`);
    return frame;
  });
  return {
    ts: now.toISOString(),
    run_id: args.run.trim(),
    step: args.step ?? null,
    verdict: args.verdict,
    frames,
    reason: args.reason.trim(),
    reviewer: args.reviewer ?? null
  };
}

export async function recordVerdict(args, filePath = verdictPath) {
  const record = buildVerdictRecord(args);
  await mkdir(path.dirname(filePath), { recursive: true });
  await appendFile(filePath, `${JSON.stringify(record)}\n`);
  return record;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  try {
    const record = await recordVerdict(parseArgs(process.argv.slice(2)));
    process.stdout.write(`${JSON.stringify({ recorded: true, file: verdictPath, record })}\n`);
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 2;
  }
}
