import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";

function numberOrNull(value) {
  const parsed = Number(value);
  return value === "" || value === undefined || !Number.isFinite(parsed)
    ? null : parsed;
}

function percentile(values, fraction) {
  const sorted = values.filter(Number.isFinite).sort((a, b) => a - b);
  return sorted.length ? sorted[Math.floor((sorted.length - 1) * fraction)] : null;
}

function summarize(values) {
  const valid = values.filter(Number.isFinite);
  if (!valid.length) return null;
  const mean = valid.reduce((sum, v) => sum + v, 0) / valid.length;
  return {
    count: valid.length,
    mean: Number(mean.toFixed(3)),
    p95: Number((percentile(valid, 0.95) ?? 0).toFixed(3)),
    max: Number(Math.max(...valid).toFixed(3)),
  };
}

// goal(goal_* 열)과 현행 출력(foot_* 열)의 프레임별 편차를 집계함.
// 도달 가능 비율은 다리 길이 데이터가 없어 골반→목표 거리가
// 관측된 골반→발 최대 거리 이내인 프레임 비율로 근사함 — 상한 추정으로만 씀.
export function reportGoalDeviation(csv, options = {}) {
  let rows;
  try { rows = readCsv(String(csv).replace(/^﻿/, "")); }
  catch { rows = []; }
  if (!rows.length) return { status: "BLOCKED", reason: "조인된 CSV가 비어 있음" };
  const columns = Object.keys(rows[0]);
  const required = ["foot_x_m", "foot_y_m", "foot_z_m",
    "goal_x_m", "goal_y_m", "goal_z_m", "hips_y_m"];
  const missing = required.filter(name => !columns.includes(name));
  if (missing.length)
    return { status: "BLOCKED", reason: `필요 열이 없음: ${missing.join(",")}` };

  const sides = {};
  const reachThresholdsMm = options.reachThresholdsMm ?? [10, 50, 100];
  for (const side of ["left", "right"]) {
    const distances = [], vertical = [], pitches = [];
    const hipToGoal = [], hipToFoot = [];
    for (const row of rows) {
      if (row.side !== side) continue;
      const goal = ["goal_x_m", "goal_y_m", "goal_z_m"]
        .map(name => numberOrNull(row[name]));
      const foot = ["foot_x_m", "foot_y_m", "foot_z_m"]
        .map(name => numberOrNull(row[name]));
      if (goal.includes(null) || foot.includes(null)) continue;
      const delta = Math.hypot(goal[0] - foot[0], goal[1] - foot[1],
        goal[2] - foot[2]) * 1000;
      distances.push(delta);
      vertical.push((goal[1] - foot[1]) * 1000);
      const goalPitch = numberOrNull(row.goal_pitch_deg);
      const footPitch = numberOrNull(row.foot_pitch_deg);
      if (goalPitch !== null && footPitch !== null)
        pitches.push(goalPitch - footPitch);
      // 근사 도달 추정 — 골반은 x/z를 안 남기므로 y 차이만 비교함.
      const hipsY = numberOrNull(row.hips_y_m);
      if (hipsY !== null) {
        hipToGoal.push(Math.abs(hipsY - goal[1]));
        hipToFoot.push(Math.abs(hipsY - foot[1]));
      }
    }
    const maxLegY = Math.max(...hipToFoot.filter(Number.isFinite), 0);
    const reachable = maxLegY > 0
      ? hipToGoal.filter(distance => distance <= maxLegY).length : 0;
    sides[side] = {
      distance_mm: summarize(distances),
      vertical_mm: summarize(vertical),
      pitch_deg: summarize(pitches),
      within_mm: Object.fromEntries(reachThresholdsMm.map(limit => [
        `${limit}mm`,
        distances.filter(d => d <= limit).length / Math.max(distances.length, 1),
      ])),
      // y만 쓰는 근사라 실제 도달 가능 비율의 느슨한 상한으로 해석해야 함.
      approx_reachable_ratio: hipToGoal.length
        ? Number((reachable / hipToGoal.length).toFixed(4)) : null,
    };
  }
  return { status: "OK", note:
      "approx_reachable_ratio는 골반 y 거리 근사 상한 — x/z 미포함",
    sides };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [csvPath, outArg] = process.argv.slice(2);
  if (!csvPath) {
    process.stderr.write(
      "usage: node goal-deviation.mjs <all-frames-with-goals.csv> [출력 경로]\n");
    process.exitCode = 1;
  } else {
    let result;
    try {
      result = reportGoalDeviation(await readFile(csvPath, "utf8"));
      result.input = path.basename(csvPath);
    } catch (error) {
      result = { status: "BLOCKED", reason: error.message };
    }
    const outputPath = path.resolve(outArg ||
      path.join(path.dirname(csvPath), "goal-deviation.json"));
    await writeFile(outputPath, JSON.stringify(result, null, 2));
    process.stdout.write(`${JSON.stringify({ status: result.status,
      resultPath: outputPath })}\n`);
    process.exitCode = result.status === "BLOCKED" ? 1 : 0;
  }
}
