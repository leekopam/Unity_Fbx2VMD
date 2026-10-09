import { readdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const refRoot = path.resolve(toolDir, "../../Docs/Workflow/Local/ReferenceAnalysis");
const outPath = path.resolve(refRoot, "calibration-stats.json");

// Phase V-2: 고증거 FBX의 영상 접촉·보정량 분포를 산출해 저증거 FBX의
// "허용 편차 경계"로 이전함. 이전 대상은 분류 임계가 아니라 분포 경계뿐임.
function percentile(values, fraction) {
  const sorted = values.filter(Number.isFinite).sort((a, b) => a - b);
  return sorted.length ? sorted[Math.floor((sorted.length - 1) * fraction)] : null;
}

async function walk(dir, acc = []) {
  let entries;
  try { entries = await readdir(dir, { withFileTypes: true }); }
  catch { return acc; }
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) await walk(full, acc);
    else acc.push(full);
  }
  return acc;
}

function stats(values) {
  if (!values.length) return null;
  return { count: values.length, mean: values.reduce((a, b) => a + b, 0) / values.length,
    p50: percentile(values, 0.5), p95: percentile(values, 0.95),
    min: Math.min(...values), max: Math.max(...values) };
}

const files = await walk(refRoot);
const intervalFiles = files.filter(f => f.endsWith("contact_intervals.csv"));
const mapFiles = files.filter(f => f.endsWith("correction_map.csv"));

const lengths60 = [], confidences = {};
for (const file of intervalFiles) {
  for (const row of readCsv(await readFile(file, "utf8"))) {
    const len = Number(row.len60);
    if (Number.isFinite(len)) lengths60.push(len);
    confidences[row.confidence] = (confidences[row.confidence] || 0) + 1;
  }
}

const offsetDepths = [], offsetDurations = [];
for (const file of mapFiles) {
  for (const row of readCsv(await readFile(file, "utf8"))) {
    const srcMin = Number(row.src_y_min);
    const prodY = Number(row.prod_y_mean);
    if (Number.isFinite(srcMin) && Number.isFinite(prodY))
      offsetDepths.push(Math.abs(prodY - srcMin));
    const len = Number(row.len60);
    if (Number.isFinite(len)) offsetDurations.push(len);
  }
}

const result = {
  generated_at: new Date().toISOString(),
  sources: { interval_files: intervalFiles.length, map_files: mapFiles.length },
  contact_interval_length_frames60: stats(lengths60),
  confidence_distribution: confidences,
  correction_offset_depth_m: stats(offsetDepths),
  correction_duration_frames60: stats(offsetDurations),
  note: "허용 편차 경계용 통계 — 분류 임계 이전이 아니라 분포(예: p95) 경계로만 사용",
};
await writeFile(outPath, JSON.stringify(result, null, 2) + "\n", "utf8");
console.log(JSON.stringify({ outPath,
  intervals: lengths60.length, maps: mapFiles.length }));
