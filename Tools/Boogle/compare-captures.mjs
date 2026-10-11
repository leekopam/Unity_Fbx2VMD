import { lstat, readFile, realpath, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";
import { analyzePoseAnomalies } from "./analyze-pose-anomalies.mjs";

const outputName = "ab-compare.json";

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

// 프레임·측 인덱스 — 두 CSV의 (frame, side) 교집합에서만 비교함.
function indexByFrameSide(rows) {
  const cell = new Map();
  for (const row of rows) cell.set(`${row.frame}:${row.side}`, row);
  return cell;
}

// 결함 유형별 구간 수와 총 프레임 수를 집계해 A/B delta를 만듦.
function summarizeAnomalies(result) {
  const counts = new Map();
  for (const anomaly of result.anomalies ?? []) {
    const key = `${anomaly.defect_type}:${anomaly.side}`;
    const entry = counts.get(key) ?? { spans: 0, frames: 0 };
    entry.spans++;
    entry.frames += anomaly.end_frame - anomaly.start_frame + 1;
    counts.set(key, entry);
  }
  return counts;
}

// 두 전체 클립 캡처의 결정적 결함·궤적 편차를 비교함.
// A는 기준선(alpha=0), B는 후보 — 새 결함 정의를 만들지 않고
// analyzePoseAnomalies의 기존 계측을 그대로 씀.
export function compareCaptures(csvA, csvB, options = {}) {
  let rowsA, rowsB;
  try { rowsA = readCsv(String(csvA).replace(/^﻿/, "")); }
  catch { rowsA = []; }
  try { rowsB = readCsv(String(csvB).replace(/^﻿/, "")); }
  catch { rowsB = []; }
  if (!rowsA.length || !rowsB.length)
    return { status: "BLOCKED", reason: "비교할 all-frames.csv가 비어 있음" };

  const anomaliesA = analyzePoseAnomalies(csvA, options);
  const anomaliesB = analyzePoseAnomalies(csvB, options);
  const countsA = summarizeAnomalies(anomaliesA);
  const countsB = summarizeAnomalies(anomaliesB);
  const defectKeys = new Set([...countsA.keys(), ...countsB.keys()]);
  const defects = {};
  let worsened = 0;
  for (const key of defectKeys) {
    const a = countsA.get(key) ?? { spans: 0, frames: 0 };
    const b = countsB.get(key) ?? { spans: 0, frames: 0 };
    defects[key] = { a, b, delta_frames: b.frames - a.frames };
    if (b.frames > a.frames) worsened++;
  }

  // 궤적 편차 — 발 위치 3축과 발목 pitch를 동일 프레임에서 대조함.
  const cellA = indexByFrameSide(rowsA);
  const cellB = indexByFrameSide(rowsB);
  const deviation = { distance_mm: {}, pitch_deg: {}, ankle_pitch_deg: {} };
  const columns = Object.keys(rowsB[0]);
  const hasPosition = ["foot_x_m", "foot_y_m", "foot_z_m"].every(c =>
    columns.includes(c));
  for (const side of ["left", "right"]) {
    const distances = [], pitches = [], ankles = [];
    for (const [key, b] of cellB) {
      if (!key.endsWith(`:${side}`)) continue;
      const a = cellA.get(key);
      if (!a) continue;
      if (hasPosition) {
        const delta = Math.hypot(
          (numberOrNull(b.foot_x_m) ?? 0) - (numberOrNull(a.foot_x_m) ?? 0),
          (numberOrNull(b.foot_y_m) ?? 0) - (numberOrNull(a.foot_y_m) ?? 0),
          (numberOrNull(b.foot_z_m) ?? 0) - (numberOrNull(a.foot_z_m) ?? 0));
        distances.push(delta * 1000);
      }
      const pitchA = numberOrNull(a.foot_pitch_deg);
      const pitchB = numberOrNull(b.foot_pitch_deg);
      if (pitchA !== null && pitchB !== null) pitches.push(pitchB - pitchA);
      const ankleA = numberOrNull(a.ankle_pitch_deg);
      const ankleB = numberOrNull(b.ankle_pitch_deg);
      if (ankleA !== null && ankleB !== null) ankles.push(ankleB - ankleA);
    }
    deviation.distance_mm[side] = summarize(distances.map(Math.abs));
    deviation.pitch_deg[side] = summarize(pitches);
    deviation.ankle_pitch_deg[side] = summarize(ankles);
  }

  return {
    status: "OK",
    frames_a: rowsA.length, frames_b: rowsB.length,
    defects,
    defects_worsened: worsened,
    skipped_a: anomaliesA.skipped ?? [],
    skipped_b: anomaliesB.skipped ?? [],
    deviation,
  };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [dirA, dirB, outArg] = process.argv.slice(2);
  if (!dirA || !dirB) {
    process.stderr.write(
      "usage: node compare-captures.mjs <기준선 근거 폴더> <후보 근거 폴더> [출력 경로]\n");
    process.exitCode = 1;
  } else {
    let result;
    try {
      const [csvA, csvB, stateSource] = await Promise.all([
        readFile(path.join(dirA, "all-frames.csv"), "utf8"),
        readFile(path.join(dirB, "all-frames.csv"), "utf8"),
        readFile(path.join(dirB, "state.json"), "utf8").catch(() => null),
      ]);
      const state = stateSource ? JSON.parse(stateSource) : {};
      result = compareCaptures(csvA, csvB,
        { sourceHumanScale: state.source_human_scale });
      result.baseline = dirA;
      result.candidate = dirB;
    } catch (error) {
      result = { status: "BLOCKED", reason: error.message };
    }
    const outputPath = path.resolve(outArg || path.join(dirB, outputName));
    const outputStat = await lstat(outputPath).catch(error => {
      if (error.code === "ENOENT") return null;
      throw error;
    });
    if (outputStat?.isSymbolicLink())
      throw new Error("A/B 비교 결과 파일은 심볼릭 링크일 수 없음");
    if (await realpath(path.dirname(outputPath)) !==
        await realpath(path.resolve(dirB)))
      throw new Error("A/B 비교 결과는 후보 근거 폴더 안에만 쓸 수 있음");
    await writeFile(outputPath, JSON.stringify(result, null, 2));
    process.stdout.write(`${JSON.stringify({ status: result.status,
      defects_worsened: result.defects_worsened,
      resultPath: outputPath })}\n`);
    process.exitCode = result.status === "BLOCKED" ? 1 : 0;
  }
}
