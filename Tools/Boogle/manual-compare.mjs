import { readFile } from "node:fs/promises";
import path from "node:path";

export function readCsv(source) {
  const rows = [];
  let row = [];
  let value = "";
  let quoted = false;
  for (let index = 0; index < source.length; index++) {
    const char = source[index];
    if (char === '"') {
      if (quoted && source[index + 1] === '"') { value += '"'; index++; }
      else quoted = !quoted;
    } else if (char === "," && !quoted) {
      row.push(value); value = "";
    } else if ((char === "\n" || char === "\r") && !quoted) {
      if (char === "\r" && source[index + 1] === "\n") index++;
      row.push(value);
      if (row.some((field) => field !== "")) rows.push(row);
      row = []; value = "";
    } else value += char;
  }
  if (quoted) throw new Error("CSV 따옴표가 닫히지 않았습니다.");
  if (value || row.length) { row.push(value); rows.push(row); }
  const [header, ...records] = rows;
  if (!header?.length) throw new Error("CSV 헤더가 없습니다.");
  return records.map((fields) => {
    if (fields.length !== header.length) throw new Error("CSV 열 개수가 일치하지 않습니다.");
    return Object.fromEntries(header.map((key, index) => [key, fields[index]]));
  });
}

function localEvidencePath(projectRoot, relativePath) {
  const root = path.resolve(projectRoot, "Docs/Workflow/Local");
  const resolved = path.resolve(projectRoot, relativePath || "");
  const relative = path.relative(root, resolved);
  if (!relative || relative.startsWith("..") || path.isAbsolute(relative))
    throw new Error(`로컬 근거 경로가 아닙니다: ${relativePath}`);
  return resolved;
}

function bySample(rows) {
  const result = new Map();
  for (const row of rows) {
    // 수동 종료 샘플은 마지막 Animator 평가 전에 기록되므로 명시적 시각 샘플만 비교함.
    if (row.reason === "finish") continue;
    const frame = Number(row.recorderFrame);
    if (!Number.isInteger(frame) || frame < 0) continue;
    const key = row.reason || String(frame);
    if (result.has(key)) {
      const previous = result.get(key);
      if (previous.animationClipName !== row.animationClipName ||
          previous.animationClipTime !== row.animationClipTime)
        throw new Error(`중복 샘플의 클립·시간 불일치: ${key}`);
      continue;
    }
    result.set(key, row);
  }
  return result;
}

export async function compareManualCapture(summary, projectRoot) {
  const manual = summary.results?.find((item) => item.jobMode === "SubManualYyb");
  const automatic = summary.results?.find((item) => item.jobMode === "MainAuto");
  if (!manual?.success || !automatic?.success ||
      !manual.comparisonMetricsCsvPath || !automatic.comparisonMetricsCsvPath ||
      !manual.comparisonFrameIndexPath || !automatic.comparisonFrameIndexPath)
    return { status: "BLOCKED", reason: "수동 YYB 또는 자동 경로의 새 CSV·캡처가 없음" };
  if (manual.frameCount !== automatic.frameCount || manual.frameCount < 1)
    return { status: "NOT_COMPARABLE", reason: "기록 프레임 수가 다름" };

  const [manualCsv, automaticCsv, manualIndexCsv, automaticIndexCsv] = await Promise.all([
    manual.comparisonMetricsCsvPath, automatic.comparisonMetricsCsvPath,
    manual.comparisonFrameIndexPath, automatic.comparisonFrameIndexPath
  ].map(async (file) => readCsv(await readFile(localEvidencePath(projectRoot, file), "utf8"))));
  const manualRows = bySample(manualCsv);
  const automaticRows = bySample(automaticCsv);
  if (manualRows.size !== automaticRows.size ||
      [...manualRows.keys()].some((sample) => !automaticRows.has(sample)))
    return { status: "NOT_COMPARABLE", reason: "수동·자동 측정 시각 집합이 다름" };
  const manualImages = new Map(manualIndexCsv.filter((row) => row.view === "front")
    .map((row) => [row.reason || row.recorderFrame, row.path]));
  const automaticImages = new Map(automaticIndexCsv.filter((row) => row.view === "front")
    .map((row) => [row.reason || row.recorderFrame, row.path]));
  const manualSideImages = new Map(manualIndexCsv.filter((row) => row.view === "right")
    .map((row) => [row.reason || row.recorderFrame, row.path]));
  const automaticSideImages = new Map(automaticIndexCsv.filter((row) => row.view === "right")
    .map((row) => [row.reason || row.recorderFrame, row.path]));
  const fields = ["rootX", "rootY", "rootZ", "leftFootX", "leftFootZ",
    "rightFootX", "rightFootZ", "lowestFootBottomY", "leftKneeAngle",
    "rightKneeAngle", "maxScaleDelta", "cameraFacingDot"];
  const frames = [];
  for (const [sample, manualRow] of manualRows) {
    const automaticRow = automaticRows.get(sample);
    if (!automaticRow) continue;
    const manualTime = Number(manualRow.animationClipTime);
    const automaticTime = Number(automaticRow.animationClipTime);
    if (!Number.isFinite(manualTime) || !Number.isFinite(automaticTime) ||
        Math.abs(manualTime - automaticTime) > 1 / 60 + 0.001 ||
        manualRow.animationClipName !== automaticRow.animationClipName)
      return { status: "NOT_COMPARABLE", reason: `시각 ${sample}의 FBX 클립·시간 불일치` };
    const metrics = {};
    for (const field of fields) {
      const before = Number(manualRow[field]);
      const after = Number(automaticRow[field]);
      if (manualRow[field] && automaticRow[field] && Number.isFinite(before) &&
          Number.isFinite(after)) metrics[field] = { manual: before, automatic: after,
            delta: Number((after - before).toFixed(6)) };
    }
    const manualImage = manualImages.get(sample);
    const automaticImage = automaticImages.get(sample);
    const manualSideImage = manualSideImages.get(sample);
    const automaticSideImage = automaticSideImages.get(sample);
    frames.push({ frame: Number(automaticRow.recorderFrame), sample,
      manualFrame: Number(manualRow.recorderFrame),
      automaticFrame: Number(automaticRow.recorderFrame),
      manualClipTime: manualTime, automaticClipTime: automaticTime,
      clipTime: manualTime, metrics,
      manualImage: manualImage || null, automaticImage: automaticImage || null,
      manualSideImage: manualSideImage || null,
      automaticSideImage: automaticSideImage || null });
  }
  if (!frames.length || !frames.some((item) => item.manualImage && item.automaticImage))
    return { status: "BLOCKED", reason: "같은 시각의 수치·정면 캡처 쌍이 없음" };
  if (frames.some((item) => Boolean(item.manualImage) !== Boolean(item.automaticImage)) ||
      frames.some((item) => Boolean(item.manualSideImage) !==
        Boolean(item.automaticSideImage)) ||
      !frames.some((item) => Object.keys(item.metrics).length))
    return { status: "BLOCKED", reason: "수동·자동 캡처 또는 측정값이 불완전함" };
  for (const item of frames) {
    for (const file of [item.manualImage, item.automaticImage,
      item.manualSideImage, item.automaticSideImage].filter(Boolean)) {
      const bytes = await readFile(localEvidencePath(projectRoot, file));
      if (bytes.length < 20 || bytes.subarray(0, 8).toString("hex") !==
          "89504e470d0a1a0a" || bytes.subarray(-8).toString("hex") !==
          "49454e44ae426082")
        return { status: "BLOCKED", reason: `캡처 PNG 손상: ${file}` };
    }
  }
  return { status: "MANUAL_REVIEW_REQUIRED", frameCount: manual.frameCount,
    comparedFrames: frames.length, frames, manual, automatic };
}
