import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";
import { loadLabelRows, unfold } from "./evaluate-label-accuracy.mjs";
import { resolveEvidenceDirectory } from "./analyze-contact-events.mjs";

// 합의 정답지 생성기 — 사람 라벨·영상 라벨·UnderPressure·규칙 계측(has_ground)
// 4개 독립 신호의 프레임 단위 투표로 자동 정답지를 만든다.
// 원칙: 사람 라벨 최우선 → 비사람 신호 2표 이상 만장일치 → 그 외 미확정.
// 신호가 갈리는 프레임은 사람 스팟 확인 큐로 보낸다.
// 주의: 이 정답지는 약한 레이블 합의물 — 각 신호가 서로를 참조하지 않지만
//      영상 라벨·규칙 신호가 평가 대상과 독립이 아닐 수 있어 오류율은
//      "상한 추정"으로만 해석한다(보고서 bias_note 참조).

const SIDES = ["left", "right"];

// all-frames.csv → 소스(보정 전) 발끝 높이로 접촉 의도를 추정.
// has_ground·밑창 클리어런스는 보정 솔버가 지면에 고정하므로 신호가 아님.
// 소스 발끝 ≤25mm → 접촉 의도 양성, >50mm → 음성, 사이는 기권.
const TOE_CONTACT_M = 0.025, TOE_AIR_M = 0.05;
export function loadRuleVotes(csvText) {
  const votes = { left: [], right: [] };
  for (const row of readCsv(csvText.replace(/^﻿/, ""))) {
    const side = (row.side || "").trim().toLowerCase();
    if (!SIDES.includes(side)) continue;
    const f = Number(row.frame);
    const toeY = Number(row.source_toes_y_m);
    if (!Number.isInteger(f) || !Number.isFinite(toeY)) continue;
    if (toeY <= TOE_CONTACT_M) votes[side][f] = true;
    else if (toeY > TOE_AIR_M) votes[side][f] = false;
  }
  return votes;
}

// underpressure-contacts.jsonl → 프레임별 진리값.
// 접촉 확률 ≥0.5 양성, ≤0.2 음성, 사이는 기권(bool 입력은 그대로 양성/음성).
const UP_POS = 0.5, UP_NEG = 0.2;
export function loadUpVotes(text) {
  const votes = { left: [], right: [] };
  const vote = v => {
    if (v === true || v === false) return v;
    const p = Number(v);
    if (!Number.isFinite(p) || (p > UP_NEG && p < UP_POS)) return undefined;
    return p >= UP_POS;
  };
  for (const line of text.split("\n")) {
    if (!line.trim()) continue;
    const rec = JSON.parse(line);
    const l = vote(rec.left), r = vote(rec.right);
    if (l !== undefined) votes.left[rec.frame] = l;
    if (r !== undefined) votes.right[rec.frame] = r;
  }
  return votes;
}

// 영상 라벨은 양성 구간만 표식 — 구간 밖은 기권(음성 단정 금지).
function intervalVotes(rows, side) {
  const votes = [];
  for (const r of rows.filter(x => x.side === side)) {
    for (let f = Math.max(0, r.f0); f <= r.f1; f++) votes[f] = true;
  }
  return votes;
}

export function buildConsensus({ humanRows = [], videoRows = [],
  upVotes, ruleVotes, lastFrame }) {
  // 사람 라벨은 reviewer와 무관하게 하나의 lane으로 합침(동일 파일 우선순위 동일)
  const human = { left: [], right: [] };
  for (const lane of unfold(humanRows, lastFrame).values())
    for (const side of SIDES)
      for (let f = 0; f < lane[side].length; f++)
        if (lane[side][f] !== undefined) human[side][f] = lane[side][f];
  const video = {
    left: intervalVotes(videoRows, "left"),
    right: intervalVotes(videoRows, "right"),
  };
  const truth = { left: [], right: [] };
  const spotCheck = [];
  const stats = { frames: lastFrame + 1, human_override: 0, consensus: 0,
    unlabeled: 0, disagreement: 0 };
  for (const side of SIDES) {
    for (let f = 0; f <= lastFrame; f++) {
      const h = human[side]?.[f];
      if (h === true || h === false) {
        truth[side][f] = h;
        if (h) stats.human_override++;
        continue;
      }
      const votes = [];
      if (video[side]?.[f] === true) votes.push({ src: "video", v: true });
      const u = upVotes?.[side]?.[f];
      if (u === true || u === false) votes.push({ src: "up", v: u });
      const r = ruleVotes?.[side]?.[f];
      if (r === true || r === false) votes.push({ src: "rule", v: r });
      const unique = [...new Set(votes.map(x => x.v))];
      if (votes.length >= 2 && unique.length === 1) {
        truth[side][f] = unique[0];
        stats.consensus++;
      } else {
        stats.unlabeled++;
        if (votes.length >= 2 && unique.length > 1) {
          stats.disagreement++;
          spotCheck.push({ frame: f, side,
            votes: Object.fromEntries(votes.map(x => [x.src, x.v])) });
        }
      }
    }
  }
  return { truth, spotCheck, stats };
}

// 진리값 배열 → 연속 구간 라벨 행으로 압축
export function truthToRows(truth) {
  const rows = [];
  for (const side of SIDES) {
    const lane = truth[side];
    let start = -1, cur = null;
    const flush = (end) => {
      if (cur === null) return;
      rows.push({ f0: start, f1: end - 1, side,
        contact: cur ? "발 전체" : "공중" });
      cur = null;
    };
    for (let f = 0; f <= lane.length; f++) {
      const v = lane[f];
      if (v === undefined || v === null) { flush(f); continue; }
      if (cur === null) { start = f; cur = v; }
      else if (v !== cur) { flush(f); start = f; cur = v; }
    }
  }
  return rows;
}

export function rowsToCsv(rows) {
  const esc = s => /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  const head = "from_frame,to_frame,side,contact_label,motion_label,reviewer,notes";
  return [head, ...rows.map(r =>
    [r.f0, r.f1, r.side, r.contact, "", "consensus",
      r.note || "auto-consensus"].map(esc).join(","))].join("\n") + "\n";
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [runArg, extraLabelsArg] = process.argv.slice(2);
  if (!runArg) {
    console.error("사용법: consensus-groundtruth.mjs <evidence run 폴더> " +
      "[같은 클립의 추가 human-labels.csv]");
    process.exit(2);
  }
  const { directory } = await resolveEvidenceDirectory(runArg);
  const read = n => readFile(path.join(directory, n), "utf8").catch(() => null);
  const [csv, state, humanCsv, videoCsv, upJsonl] = await Promise.all([
    read("all-frames.csv"), read("state.json"), read("human-labels.csv"),
    read("video-labels.csv"), read("underpressure-contacts.jsonl")]);
  const extraCsv = extraLabelsArg ?
    await readFile(extraLabelsArg, "utf8") : null;
  const lastFrame = JSON.parse(state || "{}").last_frame ??
    (csv ? Math.max(...readCsv(csv).map(r => Number(r.frame))) : 0);
  const humanRows = humanCsv ? loadLabelRows(humanCsv) : [];
  if (extraCsv)
    humanRows.push(...loadLabelRows(extraCsv));
  const videoRows = videoCsv ? loadLabelRows(videoCsv) : [];
  const { truth, spotCheck, stats } = buildConsensus({
    humanRows, videoRows, lastFrame,
    upVotes: upJsonl ? loadUpVotes(upJsonl) : null,
    ruleVotes: csv ? loadRuleVotes(csv) : null,
  });
  const rows = truthToRows(truth);
  await writeFile(path.join(directory, "consensus-labels.csv"),
    rowsToCsv(rows), "utf8");
  // 불일치 프레임을 연속 구간으로 압축 — 사람은 프레임 단위가 아니라
  // 구간 대표 샘플만 확인하면 됨
  const disagreementIntervals = [];
  for (const side of SIDES) {
    const flagged = spotCheck.filter(s => s.side === side)
      .map(s => s.frame).sort((a, b) => a - b);
    let s0 = -1, prev = -1;
    for (const f of flagged) {
      if (s0 < 0) { s0 = f; prev = f; continue; }
      if (f - prev > 5) { disagreementIntervals.push({ f0: s0, f1: prev, side }); s0 = f; }
      prev = f;
    }
    if (s0 >= 0) disagreementIntervals.push({ f0: s0, f1: prev, side });
  }
  const report = {
    status: "OK", directory, last_frame: lastFrame, ...stats,
    consensus_rows: rows.length, spot_check: spotCheck.length,
    disagreement_intervals: disagreementIntervals.length,
    spot_check_intervals: disagreementIntervals,
    spot_check_frames: spotCheck.slice(0, 200),
    bias_note: "합의 정답지는 약한 레이블 — 이로 측정한 오류율은 상한 추정이며 " +
      "평가 대상과 신호가 독립이 아니면 편향된다",
  };
  await writeFile(path.join(directory, "consensus-report.json"),
    JSON.stringify(report, null, 2), "utf8");
  console.log(JSON.stringify(report, null, 2));
}
