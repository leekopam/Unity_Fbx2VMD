import { lstat, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";
import { resolveEvidenceDirectory } from "./analyze-contact-events.mjs";

// Phase V 영상 라벨 승격 — contact_intervals.csv의 VMD 매핑을 Unity 프레임으로
// 변환해 human-labels.csv에 기록함. 영상은 후처리 결과라 "접지 시점" 증거로만 쓰고,
// 자세 정답으로는 취급하지 않음.
const promoteConfidence = new Set(["2vote"]);
const quote = value => `"${String(value).replace(/"/g, '""')}"`;

export function parseIntervals(csvText) {
  const rows = readCsv(String(csvText).replace(/^\uFEFF/, ""));
  return rows.map(row => ({
    foot: row.foot,
    f0: Number(row.f0_vmd30), f1: Number(row.f1_vmd30),
    confidence: row.confidence,
  })).filter(row => Number.isFinite(row.f0) && Number.isFinite(row.f1));
}

// vmd30 → unity 프레임. clip_rate가 60이면 ×2(w2 스크립트와 동일 관계).
export function toUnityFrame(vmdFrame, clipRate) {
  return Math.round(vmdFrame * clipRate / 30);
}

// 구간 수평 변위로 동작 의도를 판정 — 구르기는 변위만으로 구분 불가라 미기입.
export function classifyMotion(rows, side, f0, f1, scale) {
  const cells = rows.filter(row => row.side === side &&
    Number(row.frame) >= f0 && Number(row.frame) <= f1);
  if (cells.length < 2) return { label: "", displacement: null };
  const xs = cells.map(r => Number(r.foot_x_m));
  const zs = cells.map(r => Number(r.foot_z_m));
  const displacement = Math.hypot(Math.max(...xs) - Math.min(...xs),
    Math.max(...zs) - Math.min(...zs));
  // 정지 발의 잔존 떨림 여유를 인간 체형 배율로 정규화함.
  const limit = 0.05 * (scale > 0 ? scale : 1);
  return { label: displacement > limit ? "의도된 이동" : "고정", displacement };
}

// 이미 사람/영상이 기록한 라벨 구간과 겹치면 승격하지 않음(상위 증거 우선).
export function overlapsExisting(labelRows, side, f0, f1) {
  return labelRows.some(row => row.side === side && row.contact_label &&
    Number(row.from_frame) <= f1 && Number(row.to_frame) >= f0);
}

export function promoteVideoLabels(intervalsCsv, framesCsv, labelsCsv, state = {}) {
  const clipRate = Number(state.clip_frame_rate) > 0 ? state.clip_frame_rate : 60;
  const lastFrame = Number.isInteger(state.last_frame) ? state.last_frame : Infinity;
  const scale = Number(state.source_human_scale) || 1;
  const intervals = parseIntervals(intervalsCsv);
  const frames = readCsv(String(framesCsv).replace(/^\uFEFF/, ""));
  const existing = readCsv(String(labelsCsv).replace(/^\uFEFF/, ""))
    .filter(row => row.contact_label);

  const promoted = [], deferred = [], skipped = [];
  const newRows = [];
  for (const interval of intervals) {
    const side = interval.foot === "L" ? "left" :
      interval.foot === "R" ? "right" : null;
    if (!side) { skipped.push({ ...interval, reason: "알 수 없는 foot" }); continue; }
    const u0 = Math.max(0, toUnityFrame(interval.f0, clipRate));
    const u1 = Math.min(lastFrame, toUnityFrame(interval.f1, clipRate));
    if (!(u1 >= u0) || u0 > lastFrame) {
      skipped.push({ ...interval, reason: "클립 범위 밖", u0, u1 });
      continue;
    }
    if (!promoteConfidence.has(interval.confidence)) {
      deferred.push({ ...interval, u0, u1, reason: "confidence 미달" });
      continue;
    }
    if (overlapsExisting(existing, side, u0, u1)) {
      deferred.push({ ...interval, u0, u1, reason: "상위 라벨과 겹침" });
      continue;
    }
    const motion = classifyMotion(frames, side, u0, u1, scale);
    const row = { from_frame: u0, to_frame: u1, side,
      contact_label: "발 전체", motion_label: motion.label,
      reviewer: "ref-video",
      notes: `video_conf=${interval.confidence};disp=${motion.displacement === null ?
        "n/a" : motion.displacement.toFixed(3)}` };
    newRows.push(row);
    promoted.push({ ...interval, u0, u1 });
  }

  const appended = newRows.map(row =>
    [row.from_frame, row.to_frame, row.side, row.contact_label,
      row.motion_label, row.reviewer, row.notes].map(quote).join(","));
  return {
    status: "OK",
    promoted: promoted.length, deferred: deferred.length, skipped: skipped.length,
    deferredIntervals: deferred, skippedIntervals: skipped,
    csvLines: appended,
  };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [runArg, intervalsArg] = process.argv.slice(2);
  if (!runArg || !intervalsArg) {
    console.error("사용법: promote-video-labels.mjs <evidence run 폴더> <contact_intervals.csv>");
    process.exit(2);
  }
  const { directory } = await resolveEvidenceDirectory(runArg);
  const labelsPath = path.join(directory, "human-labels.csv");
  // 영상 라벨은 런타임이 human-labels.csv만 읽는다는 점을 이용해 별도 파일에 기록 —
  // 사람 라벨과 혼합해 런타임 정답지로 소비되는 것을 막음.
  const videoPath = path.join(directory, "video-labels.csv");
  let result;
  try {
    const [intervalsCsv, framesCsv, labelsCsv, stateSource] = await Promise.all([
      readFile(intervalsArg, "utf8"),
      readFile(path.join(directory, "all-frames.csv"), "utf8"),
      readFile(labelsPath, "utf8"),
      readFile(path.join(directory, "state.json"), "utf8").catch(() => "{}"),
    ]);
    result = promoteVideoLabels(intervalsCsv, framesCsv, labelsCsv,
      JSON.parse(stateSource));
    if (result.csvLines.length) {
      const header = '"from_frame","to_frame","side","contact_label",' +
        '"motion_label","reviewer","notes"';
      let existing = "";
      try { existing = await readFile(videoPath, "utf8"); } catch { /* 신규 */ }
      const base = existing ? (existing.endsWith("\n") ? existing : existing + "\n") :
        header + "\n";
      await writeFile(videoPath, base + result.csvLines.join("\n") + "\n", "utf8");
    }
    await writeFile(path.join(directory, "video-label-promotion.json"),
      JSON.stringify({ ...result, csvLines: undefined,
        source: path.basename(intervalsArg) }, null, 2), "utf8");
  } catch (error) {
    result = { status: "BLOCKED", reason: error.message };
  }
  process.stdout.write(`${JSON.stringify({ status: result.status,
    promoted: result.promoted ?? 0, deferred: result.deferred ?? 0 })}\n`);
  process.exitCode = result.status === "BLOCKED" ? 1 : 0;
}
