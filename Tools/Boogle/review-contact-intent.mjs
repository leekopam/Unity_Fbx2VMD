import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";

const evidenceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)),
  "../../Docs/Workflow/Local/evidence/boogle/full-clip");
const endpoint = "https://api.typesafe.ai/v1/systemone";
const sourceFields = ["source_foot_y_m", "source_toes_y_m",
  "source_foot_speed_mps", "source_toes_speed_mps"];
const stageFields = ["retarget_foot_y_m", "retarget_toes_y_m",
  "retarget_foot_speed_mps", "retarget_toes_speed_mps", "rear_weight",
  "front_weight", "minimum_signed_mm", "foot_pitch_deg"];
const contactNames = { airborne: "공중", toe: "앞꿈치", heel: "뒤꿈치",
  full_foot: "발 전체", uncertain: "불확실" };
const motionNames = { fixed: "고정", rolling: "구르기",
  intentional_movement: "의도된 이동", uncertain: "불확실" };
const priorityNames = { source_label: "원본 판정", retargeting: "리타게팅",
  foot_correction: "접지 보정", manual_review: "확인 필요" };
const roleNames = { released: "airborne", front: "toe", rear: "heel",
  both: "full_foot" };

function number(row, field) {
  if (row[field] === "" && field.endsWith("_speed_mps") && row.frame === "0")
    return null;
  const value = Number(row[field]);
  if (row[field] === "" || !Number.isFinite(value))
    throw new Error(`계측값 누락·오류: ${field}, frame ${row.frame}, ${row.side}`);
  return value;
}

function median(values) {
  const sorted = values.filter(value => value !== null).sort((a, b) => a - b);
  if (!sorted.length) return null;
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[middle] :
    (sorted[middle - 1] + sorted[middle]) / 2;
}

function band(value, values) {
  if (value === null) return "unknown";
  const sorted = values.filter(item => item !== null).sort((a, b) => a - b);
  const low = sorted[Math.floor((sorted.length - 1) * 0.25)];
  const high = sorted[Math.floor((sorted.length - 1) * 0.75)];
  if (high - low < 1e-8) return "middle";
  return value <= low ? "low" : value >= high ? "high" : "middle";
}

function summarize(rows, fields) {
  return Object.fromEntries(fields.map(field => {
    const value = median(rows.map(row => number(row, field)));
    return [field, value === null ? null : Number(value.toFixed(5))];
  }));
}

function readChoice(response, question, names) {
  const answer = response?.answers?.[question];
  if (answer?.type !== "choice" || !Object.hasOwn(names, answer.choice) ||
      !Number.isFinite(answer.confidence) || answer.confidence < 0 ||
      answer.confidence > 1 || !answer.probabilities ||
      Object.keys(names).some(name => !Number.isFinite(answer.probabilities[name]) ||
        answer.probabilities[name] < 0 || answer.probabilities[name] > 1))
    throw new Error(`Jev ${question} 응답 형식 오류`);
  return { choice: answer.choice, label: names[answer.choice],
    probabilities: answer.probabilities, confidence: answer.confidence };
}

async function askJev(payload, apiKey, fetchImpl) {
  for (let attempt = 0; attempt < 3; attempt++) {
    const response = await fetchImpl(endpoint, { method: "POST",
      headers: { Authorization: `Bearer ${apiKey}`, "Content-Type": "application/json" },
      body: JSON.stringify(payload), signal: AbortSignal.timeout(30000) });
    if ((response.status === 429 || response.status === 529) && attempt < 2) {
      await new Promise(resolve => setTimeout(resolve, 500 * 2 ** attempt));
      continue;
    }
    if (!response.ok) throw new Error(`Jev HTTP ${response.status}`);
    const body = await response.json();
    if (typeof body?.model !== "string" || !body.answers)
      throw new Error("Jev 응답 형식 오류");
    return body;
  }
}

function makeFirstRequest(interval) {
  return { model: "jev-latest", state: {
    source_stage: "Original FBX humanoid bones before target retargeting or correction",
    interval: interval.key,
    source_measurement_note: "Bone world Y is not sole height or pressure. Low/high bands are within this clip and side.",
    source_median: interval.sourceMedian,
    source_bands: interval.sourceBands,
    source_frames: interval.sourceFrames
  }, questions: {
    contact_form: { type: "choice",
      instructions: "Which foot contact form is most plausible for this original motion interval? Choose uncertain if the source measurements do not distinguish intent.",
      criteria: { airborne: "Foot is intentionally off the ground",
        toe: "Forefoot or toe contact dominates", heel: "Heel contact dominates",
        full_foot: "Whole foot contact dominates",
        uncertain: "The measurements cannot support a reliable contact form" } },
    motion_intent: { type: "choice",
      instructions: "Which movement intent is most plausible in this original motion interval? Choose uncertain when numeric features do not distinguish it.",
      criteria: { fixed: "Foot intends to stay fixed", rolling: "Foot rolls between heel and toe",
        intentional_movement: "Foot deliberately moves or slides",
        uncertain: "The measurements cannot support a reliable movement intent" } }
  } };
}

function makeFollowupRequest(interval, preliminary) {
  return { model: "jev-latest", state: {
    interval: interval.key, source_median: interval.sourceMedian,
    preliminary_contact: preliminary.contact.choice,
    retarget_and_final_median: interval.stageMedian,
    final_support_roles: interval.roleCounts,
    final_dominant_contact: interval.finalContact,
    measurement_note: "Retarget values precede contact correction; final weights, sole distance and foot pitch follow correction. No image or pressure data is available."
  }, questions: { investigation: { type: "choice",
    instructions: "The preliminary source contact and final support role disagree. Which stage should a human inspect first? This is a review priority, not a quality verdict.",
    criteria: { source_label: "Original motion intent or preliminary label needs review",
      retargeting: "Retargeting likely changes the foot trajectory",
      foot_correction: "Foot contact correction likely changes the final support behavior",
      manual_review: "These measurements cannot identify a stage" } } } };
}

export async function reviewContactIntent(state, metricsSource, templateSource,
  { apiKey = "", fetchImpl = fetch } = {}) {
  const headers = metricsSource.replace(/^\uFEFF/, "").split(/\r?\n/, 1)[0].split(",");
  if (!state || state.status !== "metrics_complete_review_required" ||
      !sourceFields.concat(stageFields).every(field => headers.includes(field)))
    return { status: "BLOCKED", reason: "완료된 47열 F10 원본·단계별 계측이 필요함" };
  const rows = readCsv(metricsSource.replace(/^\uFEFF/, ""));
  const template = readCsv(templateSource.replace(/^\uFEFF/, ""));
  if (template.length > 64)
    return { status: "BLOCKED", reason: "한 번에 검토할 구간은 64개 이하여야 함" };
  if (rows.length !== state.row_count || rows.length !== (state.last_frame + 1) * 2)
    return { status: "BLOCKED", reason: "전체 프레임 계측 행 수 불일치" };
  const bySide = { left: [], right: [] };
  const byKey = new Map();
  for (const row of rows) {
    const frame = Number(row.frame);
    const key = `${frame}:${row.side}`;
    if (!Number.isInteger(frame) || frame < 0 || frame > state.last_frame ||
        !bySide[row.side] || byKey.has(key))
      return { status: "BLOCKED", reason: "계측 프레임·발 중복 또는 오류" };
    bySide[row.side].push(row);
    byKey.set(key, row);
  }
  if (bySide.left.length !== state.last_frame + 1 ||
      bySide.right.length !== state.last_frame + 1)
    return { status: "BLOCKED", reason: "좌우 발 프레임 누락" };

  const intervals = [];
  const seen = new Set();
  for (const label of template) {
    const first = Number(label.from_frame);
    const last = Number(label.to_frame);
    const key = `${first}-${last}:${label.side}`;
    if (!Number.isInteger(first) || !Number.isInteger(last) || first < 0 ||
        last < first || last > state.last_frame || !bySide[label.side] || seen.has(key))
      return { status: "BLOCKED", reason: "사람 표식 구간 형식 오류" };
    seen.add(key);
    const segment = Array.from({ length: last - first + 1 }, (_, index) =>
      byKey.get(`${first + index}:${label.side}`));
    if (segment.some(row => !row))
      return { status: "BLOCKED", reason: `구간 계측 누락: ${key}` };
    if (segment.some(row => !Object.hasOwn(roleNames, row.support_role)))
      return { status: "BLOCKED", reason: `지지 역할 형식 오류: ${key}` };
    const sourceMedian = summarize(segment, sourceFields);
    const sourceBands = Object.fromEntries(sourceFields.map(field =>
      [field, band(sourceMedian[field], bySide[label.side].map(row => number(row, field)))]));
    const clearGround = sourceBands.source_foot_y_m === "low" &&
      sourceBands.source_foot_speed_mps === "low" &&
      sourceBands.source_toes_speed_mps === "low";
    const clearAir = sourceBands.source_foot_y_m === "high" &&
      sourceBands.source_foot_speed_mps === "high" &&
      sourceBands.source_toes_speed_mps === "high";
    if (clearGround || clearAir) continue;
    const roleCounts = Object.fromEntries(["released", "front", "rear", "both"]
      .map(role => [role, segment.filter(row => row.support_role === role).length]));
    const dominant = Object.entries(roleCounts).sort((a, b) => b[1] - a[1]);
    intervals.push({ key, sourceMedian, sourceBands,
      sourceFrames: segment.map(row => ({ frame: Number(row.frame),
        foot_y_m: number(row, "source_foot_y_m"),
        toes_y_m: number(row, "source_toes_y_m"),
        foot_speed_mps: number(row, "source_foot_speed_mps"),
        toes_speed_mps: number(row, "source_toes_speed_mps") })),
      stageMedian: summarize(segment, stageFields), roleCounts,
      finalContact: dominant[0][1] > dominant[1][1]
        ? roleNames[dominant[0][0]] : "uncertain" });
  }
  const result = { status: apiKey ? "MANUAL_REVIEW_REQUIRED" : "SKIPPED_NO_API_KEY",
    source_input: state.input, candidate_intervals: template.length,
    ambiguous_intervals: intervals.length, skipped_clear_intervals: template.length - intervals.length,
    confidence_is_project_accuracy: false, intervals: [] };
  for (const interval of intervals) {
    const entry = { interval: interval.key, source_bands: interval.sourceBands,
      final_contact: contactNames[interval.finalContact],
      preliminary: null, investigation: null };
    const firstRequest = makeFirstRequest(interval);
    if (!apiKey) {
      entry.pending_request = firstRequest;
      result.intervals.push(entry);
      continue;
    }
    try {
      const first = await askJev(firstRequest, apiKey, fetchImpl);
      entry.preliminary = { model: first.model,
        contact: readChoice(first, "contact_form", contactNames),
        motion: readChoice(first, "motion_intent", motionNames), usage: first.usage };
      if (entry.preliminary.contact.choice !== "uncertain" &&
          interval.finalContact !== "uncertain" &&
          entry.preliminary.contact.choice !== interval.finalContact) {
        const second = await askJev(makeFollowupRequest(interval, entry.preliminary),
          apiKey, fetchImpl);
        entry.investigation = { model: second.model,
          priority: readChoice(second, "investigation", priorityNames),
          usage: second.usage };
      }
    } catch (error) {
      entry.error = error instanceof TypeError ? "Jev 네트워크 오류" : error.message;
      result.status = "ADVISORY_ERROR";
      result.intervals.push(entry);
      break;
    }
    result.intervals.push(entry);
  }
  return result;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const directory = path.resolve(process.argv[2] || "");
  const relative = path.relative(evidenceRoot, directory);
  if (!process.argv[2] || !relative || relative.startsWith("..") ||
      path.isAbsolute(relative) || relative.split(path.sep).length !== 2)
    throw new Error("로컬 full-clip/<runId>/<requestId> 근거 폴더가 필요합니다.");
  const state = JSON.parse(await readFile(path.join(directory, "state.json"), "utf8"));
  const result = await reviewContactIntent(state,
    await readFile(path.join(directory, "all-frames.csv"), "utf8"),
    await readFile(path.join(directory, "human-labels-template.csv"), "utf8"),
    { apiKey: process.env.TYPESAFE_API_KEY?.trim() || "" });
  await writeFile(path.join(directory, "jev-review.json"), JSON.stringify(result, null, 2));
  const csv = ["interval,preliminary_contact,motion_intent,final_contact,investigation_priority,confidence"];
  for (const item of result.intervals || [])
    csv.push([item.interval, item.preliminary?.contact.label || "",
      item.preliminary?.motion.label || "", item.final_contact,
      item.investigation?.priority.label || "",
      item.preliminary?.contact.confidence ?? ""].join(","));
  await writeFile(path.join(directory, "jev-review.csv"), `${csv.join("\n")}\n`);
  process.stdout.write(`${JSON.stringify({ status: result.status,
    ambiguousIntervals: result.ambiguous_intervals ?? 0,
    reviewPath: path.join(directory, "jev-review.json") })}\n`);
  process.exitCode = ["BLOCKED", "ADVISORY_ERROR"].includes(result.status) ? 1 : 0;
}
