import { lstat, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";
import { resolveEvidenceDirectory } from "./analyze-contact-events.mjs";
import { reviewContactIntent } from "./review-contact-intent.mjs";

// Phase 3 자동 라벨 승격 — Jev 판정을 human-labels.csv로 기록함.
// 실패는 항상 안전측: 확신 없으면 라벨 미기록 + 리뷰 큐만.
export const defaultConfidenceFloor = 0.7;
const quote = value => `"${String(value).replace(/"/g, '""')}"`;

// 밴드 증거와 비모순 규칙 — 공중인데 발이 low면 모순, 접지인데 high면 모순.
export function contradictsBands(choice, bands) {
  const height = bands?.source_foot_y_m;
  if (choice === "airborne") return height === "low";
  if (["toe", "heel", "full_foot"].includes(choice)) return height === "high";
  return false;
}

// 사람·영상 라벨이 있는 구간은 Jev도 자동 기록도 하지 않음.
export function overlapsLabeled(labeledRows, side, f0, f1) {
  return labeledRows.some(row => row.side === side && row.contact_label &&
    Number(row.from_frame) <= f1 && Number(row.to_frame) >= f0);
}

export function buildFilteredTemplate(templateCsv, labeledRows) {
  const rows = readCsv(String(templateCsv).replace(/^\uFEFF/, ""));
  const kept = rows.filter(row =>
    !overlapsLabeled(labeledRows, row.side,
      Number(row.from_frame), Number(row.to_frame)));
  const header = "from_frame,to_frame,side,contact_label,motion_label,reviewer,notes";
  const lines = kept.map(row =>
    [row.from_frame, row.to_frame, row.side, "", "", "", ""].join(","));
  return { csv: [header, ...lines].join("\n") + "\n", kept, dropped: rows.length - kept.length };
}

export async function autoLabelContactIntent(state, metricsCsv, templateCsv,
  labelsCsv, { apiKey = "", fetchImpl = fetch,
    confidenceFloor = defaultConfidenceFloor } = {}) {
  const labeledRows = readCsv(String(labelsCsv).replace(/^\uFEFF/, ""))
    .filter(row => row.contact_label);
  const filtered = buildFilteredTemplate(templateCsv, labeledRows);
  if (!filtered.kept.length) {
    return { status: "NOTHING_TO_REVIEW",
      reason: "모든 후보 구간에 상위 라벨이 존재", csvLines: [] };
  }

  const review = await reviewContactIntent(state, metricsCsv, filtered.csv,
    { apiKey, fetchImpl });
  const result = {
    status: review.status === "MANUAL_REVIEW_REQUIRED" ? "OK" : review.status,
    review_status: review.status,
    ambiguous_intervals: review.ambiguous_intervals ?? 0,
    promoted: 0, deferred: 0,
    deferredIntervals: [], csvLines: [], model: null,
  };
  for (const item of review.intervals || []) {
    const pre = item.preliminary;
    if (!pre) { // dry-run이거나 오류 — 승격 없이 큐만
      result.deferredIntervals.push({ interval: item.interval,
        reason: pre === null && !apiKey ? "dry-run" : (item.error || "응답 없음") });
      result.deferred++;
      continue;
    }
    result.model = result.model || pre.model;
    const [range] = [item.interval];
    const [frameRange, side] = [range.split(":")[0].split("-").map(Number),
      range.split(":")[1]];
    const contradicted = contradictsBands(pre.contact.choice, item.source_bands);
    const qualified = pre.contact.choice !== "uncertain" &&
      pre.motion.choice !== "uncertain" &&
      pre.contact.confidence >= confidenceFloor && !contradicted;
    if (!qualified) {
      result.deferredIntervals.push({ interval: item.interval,
        reason: contradicted ? "밴드 증거와 모순" :
          `confidence ${pre.contact.confidence} < ${confidenceFloor} 또는 uncertain` });
      result.deferred++;
      continue;
    }
    result.csvLines.push([frameRange[0], frameRange[1], side,
      pre.contact.label, pre.motion.label, "jev-auto",
      `conf=${pre.contact.confidence};model=${pre.model}`]
      .map(quote).join(","));
    result.promoted++;
  }
  return result;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const { directory } = await resolveEvidenceDirectory(process.argv[2]);
  const labelsPath = path.join(directory, "human-labels.csv");
  const reportPath = path.join(directory, "auto-label-report.json");
  const reportStat = await lstat(reportPath).catch(error => {
    if (error.code === "ENOENT") return null;
    throw error;
  });
  if (reportStat?.isSymbolicLink())
    throw new Error("자동 라벨 보고 파일은 심볼릭 링크일 수 없음");
  let result;
  try {
    const [stateSource, metricsCsv, templateCsv, labelsCsv] = await Promise.all([
      readFile(path.join(directory, "state.json"), "utf8"),
      readFile(path.join(directory, "all-frames.csv"), "utf8"),
      readFile(path.join(directory, "human-labels-template.csv"), "utf8"),
      readFile(labelsPath, "utf8"),
    ]);
    result = await autoLabelContactIntent(JSON.parse(stateSource),
      metricsCsv, templateCsv, labelsCsv,
      { apiKey: process.env.TYPESAFE_API_KEY?.trim() || "" });
    if (result.csvLines.length) {
      const base = labelsCsv.endsWith("\n") ? labelsCsv : labelsCsv + "\n";
      await writeFile(labelsPath, base + result.csvLines.join("\n") + "\n", "utf8");
    }
  } catch (error) {
    result = { status: "BLOCKED", reason: error.message };
  }
  await writeFile(reportPath, JSON.stringify(result, null, 2), "utf8");
  process.stdout.write(`${JSON.stringify({ status: result.status,
    promoted: result.promoted ?? 0, deferred: result.deferred ?? 0 })}\n`);
  process.exitCode = result.status === "BLOCKED" ? 1 : 0;
}
