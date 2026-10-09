import { lstat, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";
import { resolveEvidenceDirectory } from "./analyze-contact-events.mjs";

// Phase 4 자세 심판 — 층1 계측기의 의심 구간을 Jev가 "파손/허용/판단불가"로 심판함.
// Jev에는 코드가 계산한 의미 단위만 보내고 원시 프레임 배열은 넣지 않음.
const endpoint = "https://api.typesafe.ai/v1/systemone";
const verdictNames = { broken_pose: "파손된 자세",
  acceptable_correction: "허용 범위 보정", cannot_judge: "판단 불가" };

function median(values) {
  const sorted = values.filter(Number.isFinite).sort((a, b) => a - b);
  if (!sorted.length) return null;
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[middle] :
    (sorted[middle - 1] + sorted[middle]) / 2;
}

function band(value, values) {
  if (value === null) return "unknown";
  const sorted = values.filter(Number.isFinite).sort((a, b) => a - b);
  const low = sorted[Math.floor((sorted.length - 1) * 0.25)];
  const high = sorted[Math.floor((sorted.length - 1) * 0.75)];
  if (high - low < 1e-8) return "middle";
  return value <= low ? "low" : value >= high ? "high" : "middle";
}

// 구간의 구조화 증거 — Jev가 판단에 쓰는 의미 단위만 모음.
export function buildAnomalyState(anomaly, rows) {
  const fields = ["knee_flexion_deg", "ankle_pitch_deg", "pelvis_tilt_deg",
    "pose_novelty", "transition_novelty", "joint_delta_deg",
    "minimum_signed_mm", "foot_rotation_step_deg"];
  const sideRows = rows.filter(row => row.side === anomaly.side ||
    anomaly.side === "both");
  const inSpan = sideRows.filter(row => Number(row.frame) >= anomaly.start_frame &&
    Number(row.frame) <= anomaly.end_frame);
  const allValues = Object.fromEntries(fields.map(field =>
    [field, sideRows.map(row => Number(row[field])).filter(Number.isFinite)]));
  const spanMedian = Object.fromEntries(fields.map(field =>
    [field, median(inSpan.map(row => Number(row[field]))
      .filter(Number.isFinite))]));
  const bands = Object.fromEntries(fields.map(field =>
    [field, allValues[field].length
      ? band(spanMedian[field], allValues[field]) : "missing"]));
  return {
    interval: `${anomaly.start_frame}-${anomaly.end_frame}, ${anomaly.side}`,
    defect_type: anomaly.defect_type, severity: anomaly.severity,
    duration_frames: anomaly.end_frame - anomaly.start_frame + 1,
    defect_evidence: anomaly.evidence || {},
    metric_medians: spanMedian, metric_bands: bands,
    measurement_note: "Angles in degrees; novelty = kNN distance to the project's own dance-pose corpus (higher = more unusual); bands are quartiles within this clip.",
  };
}

function makeVerdictRequest(state) {
  return { model: "jev-latest", state, questions: {
    pose_verdict: { type: "choice",
      instructions: "A deterministic analyzer flagged this span during foot-contact correction. Judge whether the corrected pose is broken or an acceptable constraint satisfaction. Choose cannot_judge if the evidence is insufficient.",
      criteria: {
        broken_pose: "Correction produced an unnatural or physically implausible pose",
        acceptable_correction: "Change stays within what grounding constraints require",
        cannot_judge: "The metrics cannot distinguish broken from acceptable" } } } };
}

function readVerdict(response) {
  const answer = response?.answers?.pose_verdict;
  if (answer?.type !== "choice" || !Object.hasOwn(verdictNames, answer.choice) ||
      !Number.isFinite(answer.confidence) || answer.confidence < 0 ||
      answer.confidence > 1)
    throw new Error("Jev pose_verdict 응답 형식 오류");
  return { choice: answer.choice, label: verdictNames[answer.choice],
    confidence: answer.confidence, probabilities: answer.probabilities };
}

async function askJev(payload, apiKey, fetchImpl) {
  for (let attempt = 0; attempt < 3; attempt++) {
    const response = await fetchImpl(endpoint, { method: "POST",
      headers: { Authorization: `Bearer ${apiKey}`,
        "Content-Type": "application/json" },
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

export async function reviewPoseAnomalies(anomaliesJson, metricsCsv,
  { apiKey = "", fetchImpl = fetch, confidenceFloor = 0.7 } = {}) {
  const anomalies = anomaliesJson?.anomalies || [];
  const rows = readCsv(String(metricsCsv).replace(/^\uFEFF/, ""));
  if (!rows.length) return { status: "BLOCKED", reason: "all-frames.csv가 비어 있음" };
  if (anomalies.length > 64)
    return { status: "BLOCKED", reason: "한 번에 심판할 의심 구간은 64개 이하여야 함" };

  const result = { status: apiKey ? "OK" : "SKIPPED_NO_API_KEY",
    candidate_intervals: anomalies.length, verdicts: [], review_queue: [] };
  for (const anomaly of anomalies) {
    const state = buildAnomalyState(anomaly, rows);
    const entry = { interval: state.interval, defect_type: anomaly.defect_type,
      verdict: null };
    if (!apiKey) {
      entry.pending_request = makeVerdictRequest(state);
      result.verdicts.push(entry);
      continue;
    }
    try {
      const response = await askJev(makeVerdictRequest(state), apiKey, fetchImpl);
      entry.verdict = { model: response.model, ...readVerdict(response) };
      // 파손 확정 또는 판단 불가·미달은 사람 리뷰 큐로 보냄.
      if (entry.verdict.choice !== "acceptable_correction" ||
          entry.verdict.confidence < confidenceFloor)
        result.review_queue.push({ interval: entry.interval,
          defect_type: entry.defect_type, verdict: entry.verdict });
    } catch (error) {
      entry.error = error instanceof TypeError ? "Jev 네트워크 오류" : error.message;
      result.status = "ADVISORY_ERROR";
      result.verdicts.push(entry);
      break;
    }
    result.verdicts.push(entry);
  }
  return result;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const { directory } = await resolveEvidenceDirectory(process.argv[2]);
  const outputPath = path.join(directory, "pose-review.json");
  const outputStat = await lstat(outputPath).catch(error => {
    if (error.code === "ENOENT") return null;
    throw error;
  });
  if (outputStat?.isSymbolicLink())
    throw new Error("자세 심판 결과 파일은 심볼릭 링크일 수 없음");
  let result;
  try {
    const [anomaliesSource, metricsCsv] = await Promise.all([
      readFile(path.join(directory, "pose-anomalies.json"), "utf8"),
      readFile(path.join(directory, "all-frames.csv"), "utf8"),
    ]);
    result = await reviewPoseAnomalies(JSON.parse(anomaliesSource), metricsCsv,
      { apiKey: process.env.TYPESAFE_API_KEY?.trim() || "" });
  } catch (error) {
    result = { status: "BLOCKED", reason: error.message };
  }
  await writeFile(outputPath, JSON.stringify(result, null, 2), "utf8");
  process.stdout.write(`${JSON.stringify({ status: result.status,
    verdicts: result.verdicts?.length ?? 0,
    queued: result.review_queue?.length ?? 0 })}\n`);
  process.exitCode = result.status === "BLOCKED" ? 1 : 0;
}
