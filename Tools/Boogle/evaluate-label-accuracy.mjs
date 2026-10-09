import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";
import { resolveEvidenceDirectory } from "./analyze-contact-events.mjs";

// Phase 6 라벨 교차 평가 — 같은 클립의 서로 다른 라벨 소스(사람/영상/jev)를
// 프레임 단위 접지 진리값으로 펼쳐 일치율·혼동행렬·구간 IoU를 산출함.
// 접지 양성 = 앞꿈치/뒤꿈치/발 전체, 음성 = 공중, 불확실/공백은 평가 제외.
const POSITIVE = new Set(["앞꿈치", "뒤꿈치", "발 전체"]);
const NEGATIVE = new Set(["공중"]);

export function loadLabelRows(csvText) {
  return readCsv(String(csvText).replace(/^﻿/, ""))
    .filter(row => row.from_frame && row.side)
    .map(row => ({
      f0: Number(row.from_frame), f1: Number(row.to_frame),
      side: row.side.trim().toLowerCase(),
      contact: (row.contact_label || "").trim(),
      reviewer: (row.reviewer || "").trim() || "unknown",
    }));
}

// 라벨 행을 reviewer 그룹별 프레임 진리값 맵으로 펼침.
export function unfold(rows, lastFrame) {
  const truth = new Map(); // reviewer -> side -> Array(tri-state)
  for (const row of rows) {
    const positive = POSITIVE.has(row.contact) ? true :
      NEGATIVE.has(row.contact) ? false : null;
    if (positive === null) continue;
    if (!truth.has(row.reviewer))
      truth.set(row.reviewer, { left: [], right: [] });
    const lane = truth.get(row.reviewer)[row.side];
    if (!lane) continue;
    for (let f = Math.max(0, row.f0);
      f <= Math.min(row.f1, lastFrame ?? row.f1); f++)
      lane[f] = positive;
  }
  return truth;
}

export function confusion(pred, truth) {
  let tp = 0, fp = 0, fn = 0, tn = 0;
  const n = Math.max(pred.length, truth.length);
  for (let f = 0; f < n; f++) {
    const t = truth[f], p = pred[f];
    if (t === undefined || t === null) continue;
    if (t && p === true) tp++;
    else if (!t && p === true) fp++;
    else if (t && p === false) fn++;
    else if (!t && p === false) tn++;
  }
  const precision = tp + fp ? tp / (tp + fp) : 0;
  const recall = tp + fn ? tp / (tp + fn) : 0;
  return { tp, fp, fn, tn, precision: +precision.toFixed(4),
    recall: +recall.toFixed(4),
    f1: +(precision + recall ? 2 * precision * recall / (precision + recall) : 0).toFixed(4) };
}

// 구간 단위 IoU — 같은 side의 구간 쌍 중 최대 IoU.
export function intervalIoU(aRows, bRows, side) {
  const pairs = [];
  const a = aRows.filter(r => r.side === side);
  const b = bRows.filter(r => r.side === side);
  for (const ra of a) {
    let best = 0;
    for (const rb of b) {
      const inter = Math.max(0, Math.min(ra.f1, rb.f1) - Math.max(ra.f0, rb.f0) + 1);
      const union = Math.max(ra.f1, rb.f1) - Math.min(ra.f0, rb.f0) + 1;
      best = Math.max(best, union > 0 ? inter / union : 0);
    }
    pairs.push({ a: [ra.f0, ra.f1], iou: +best.toFixed(3) });
  }
  return pairs;
}

export function evaluateRun(labelsCsv, lastFrame) {
  const rows = loadLabelRows(labelsCsv);
  const truth = unfold(rows, lastFrame);
  const reviewers = [...truth.keys()];
  const comparisons = [];
  for (let i = 0; i < reviewers.length; i++)
    for (let j = i + 1; j < reviewers.length; j++) {
      const [ra, rb] = [reviewers[i], reviewers[j]];
      for (const side of ["left", "right"]) {
        const a = truth.get(ra)[side], b = truth.get(rb)[side];
        // 서로 라벨이 겹치는 프레임에서만 비교
        comparisons.push({ pair: `${ra} vs ${rb}`, side,
          ...confusion(a, b) });
      }
    }
  const iou = reviewers.length >= 2 ? {
    left: intervalIoU(rows.filter(r => r.reviewer === reviewers[0]),
      rows.filter(r => r.reviewer === reviewers[1]), "left"),
    right: intervalIoU(rows.filter(r => r.reviewer === reviewers[0]),
      rows.filter(r => r.reviewer === reviewers[1]), "right"),
  } : null;
  // 커버리지: 한 소스의 양성 라벨 구간이 다른 소스 구간과 겹치는 비율.
  // 프레임 단위 진리값이 희소할 때의 대리 일치 지표.
  const coverage = [];
  for (let i = 0; i < reviewers.length; i++)
    for (let j = 0; j < reviewers.length; j++) {
      if (i === j) continue;
      const aPos = rows.filter(r => r.reviewer === reviewers[i] &&
        POSITIVE.has(r.contact));
      const bAny = rows.filter(r => r.reviewer === reviewers[j]);
      for (const side of ["left", "right"]) {
        const aSide = aPos.filter(r => r.side === side);
        const bSide = bAny.filter(r => r.side === side);
        if (!aSide.length || !bSide.length) continue;
        const covered = aSide.filter(ra => bSide.some(rb =>
          rb.f0 <= ra.f1 && rb.f1 >= ra.f0)).length;
        coverage.push({ from: reviewers[i], against: reviewers[j], side,
          positive_intervals: aSide.length, covered,
          coverage: +(covered / aSide.length).toFixed(3) });
      }
    }
  return { status: "OK", reviewers, label_rows: rows.length,
    comparisons, coverage, interval_iou: iou };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [runArg, extraArg] = process.argv.slice(2);
  if (!runArg) {
    console.error("사용법: evaluate-label-accuracy.mjs <evidence run 폴더> " +
      "[같은 클립의 추가 human-labels.csv]");
    process.exit(2);
  }
  const { directory } = await resolveEvidenceDirectory(runArg);
  let result;
  try {
    const [labelsCsv, stateText] = await Promise.all([
      readFile(path.join(directory, "human-labels.csv"), "utf8"),
      readFile(path.join(directory, "state.json"), "utf8").catch(() => "{}"),
    ]);
    const extraCsv = extraArg ? await readFile(extraArg, "utf8") : "";
    const lastFrame = JSON.parse(stateText).last_frame;
    result = evaluateRun(labelsCsv + (extraCsv ? "\n" +
      extraCsv.split("\n").slice(1).join("\n") : ""), lastFrame);
    await writeFile(path.join(directory, "label-accuracy.json"),
      JSON.stringify(result, null, 2), "utf8");
  } catch (error) {
    result = { status: "BLOCKED", reason: error.message };
  }
  process.stdout.write(`${JSON.stringify({ status: result.status,
    reviewers: result.reviewers?.length ?? 0,
    comparisons: result.comparisons?.length ?? 0 })}\n`);
  process.exitCode = result.status === "BLOCKED" ? 1 : 0;
}
