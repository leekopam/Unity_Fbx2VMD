import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";

// HumanoidFootGoalStream이 프레임·측별로 남기는 goal 스트림 형식:
// {"frame":0,"side":"left","pos":[x,y,z],"rot":[pitch,yaw,roll],
//  "toe":[x,y,z],"sole_offset":m} — 좌표는 ghost 루트 기준 상대값.
export const goalColumns = [
  "goal_x_m", "goal_y_m", "goal_z_m",
  "goal_pitch_deg", "goal_yaw_deg", "goal_roll_deg",
  "goal_toe_y_m", "goal_sole_offset_m",
];

function numberOrEmpty(value) {
  return Number.isFinite(value) ? String(value) : "";
}

// goal 스트림을 (frame, side) 키로 색인함. 중복은 마지막 기록을 씀.
export function indexGoalStream(jsonl) {
  const cells = new Map();
  for (const line of String(jsonl).split(/\r?\n/)) {
    if (!line.trim()) continue;
    let record;
    try { record = JSON.parse(line); }
    catch { throw new Error(`goal 스트림 JSON 파싱 실패: ${line.slice(0, 80)}`); }
    if (!Number.isInteger(record.frame) ||
        (record.side !== "left" && record.side !== "right"))
      throw new Error(`goal 스트림 레코드의 frame/side가 유효하지 않음: ${line.slice(0, 80)}`);
    cells.set(`${record.frame}:${record.side}`, record);
  }
  return cells;
}

// all-frames.csv에 goal_* 열을 붙인 새 CSV 텍스트를 만듦. 원본은 유지함.
export function joinGoalColumns(csv, goalJsonl) {
  const rows = readCsv(String(csv).replace(/^﻿/, ""));
  if (!rows.length) throw new Error("all-frames.csv가 비어 있습니다.");
  const cells = indexGoalStream(goalJsonl);
  const header = rows[0] && Object.keys(rows[0]);
  const lines = [[...header, ...goalColumns].join(",")];
  let matched = 0;
  for (const row of rows) {
    const goal = cells.get(`${row.frame}:${row.side}`);
    if (goal) matched++;
    const values = goal ? [
      goal.pos?.[0], goal.pos?.[1], goal.pos?.[2],
      goal.rot?.[0], goal.rot?.[1], goal.rot?.[2],
      goal.toe?.[1], goal.sole_offset,
    ].map(numberOrEmpty) : goalColumns.map(() => "");
    lines.push([...header.map(key => row[key]), ...values].join(","));
  }
  return { csv: lines.join("\n"), matched, total: rows.length };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [dirArg] = process.argv.slice(2);
  if (!dirArg) {
    process.stderr.write(
      "usage: node join-goal-columns.mjs <캡처 근거 폴더(all-frames.csv와 goal-stream.jsonl 포함)>\n");
    process.exitCode = 1;
  } else {
    const directory = path.resolve(dirArg);
    const [csv, jsonl] = await Promise.all([
      readFile(path.join(directory, "all-frames.csv"), "utf8"),
      readFile(path.join(directory, "goal-stream.jsonl"), "utf8"),
    ]);
    const joined = joinGoalColumns(csv, jsonl);
    const outputPath = path.join(directory, "all-frames-with-goals.csv");
    await writeFile(outputPath, joined.csv);
    process.stdout.write(`${JSON.stringify({ matched: joined.matched,
      total: joined.total, resultPath: outputPath })}\n`);
  }
}
