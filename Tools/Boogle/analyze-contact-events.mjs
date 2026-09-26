import { createHash } from "node:crypto";
import { lstat, readFile, realpath, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";

const evidenceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)),
  "../../Docs/Workflow/Local/evidence/boogle");
// 허용된 증거 패밀리별 세션 매니페스트 폴더를 연결함.
// full-clip-f14는 F14 하체 전용 경로, vrm-character는 VRM 캡처 경로임.
const evidenceFamilies = {
  "full-clip": "full-clip-runs",
  "full-clip-f14": "alternate-model-runs",
  "vrm-character": "vrm-character-runs"
};
const sdkProjectId = "f2f44dc8-83ef-46d0-9d26-b0e52d1c4d20";
const baseColumns = ("frame,time_s,time_error_ms,side,grounding_status,has_ground," +
  "support_role,rear_weight,front_weight,rear_signed_mm,front_signed_mm," +
  "minimum_signed_mm,rear_anchor_x_m,rear_anchor_y_m,rear_anchor_z_m," +
  "front_anchor_x_m,front_anchor_y_m,front_anchor_z_m,rear_point_x_m," +
  "rear_point_y_m,rear_point_z_m,front_point_x_m,front_point_y_m,front_point_z_m," +
  "rear_vertex,front_vertex,rear_step_mm,front_step_mm,foot_rotation_step_deg," +
  "foot_x_m,foot_y_m,foot_z_m,foot_pitch_deg,foot_yaw_deg,foot_roll_deg," +
  "toes_y_m,knee_y_m,hips_y_m,root_y_m,source_foot_y_m,source_toes_y_m," +
  "source_foot_speed_mps,source_toes_speed_mps,retarget_foot_y_m," +
  "retarget_toes_y_m,retarget_foot_speed_mps,retarget_toes_speed_mps").split(",");
const textColumns = new Set(["side", "grounding_status", "has_ground", "support_role"]);
const speedColumns = new Set(["source_foot_speed_mps", "source_toes_speed_mps",
  "retarget_foot_speed_mps", "retarget_toes_speed_mps"]);
const optionalColumns = new Set(["rear_step_mm", "front_step_mm"]);
const supportRoles = new Set(["released", "front", "rear", "both"]);
const groundingStatuses = new Set(["Disabled", "Unavailable", "NoGround", "Applied", "Fallback"]);
const labels = { support: "지지 후보", airborne: "자유발 후보", uncertain: "불확실" };

function percentile(values, fraction) {
  const sorted = values.filter(Number.isFinite).sort((a, b) => a - b);
  return sorted.length ? sorted[Math.floor((sorted.length - 1) * fraction)] : null;
}

function maximum(values) {
  return values.reduce((value, current) => Math.max(value, current), -Infinity);
}

function minimum(values) {
  return values.reduce((value, current) => Math.min(value, current), Infinity);
}

function ranges(values) {
  const result = [];
  for (let start = 0; start < values.length;) {
    let end = start;
    while (end + 1 < values.length && values[end + 1] === values[start]) end++;
    result.push({ start, end, classification: values[start] });
    start = end + 1;
  }
  return result;
}

function classifySide(rows, scale, frameRate, hasClipMotion) {
  const minimumFrames = Math.max(2, Math.ceil(frameRate * 0.1));
  const floorFoot = percentile(rows.map(row => row.sourceFootY), 0.1);
  const floorToes = percentile(rows.map(row => row.sourceToesY), 0.1);
  const limits = { supportHeightM: scale * 0.015, supportSpeedMps: scale * 0.05,
    airborneHeightM: scale * 0.05, airborneSpeedMps: scale * 0.2,
    floatingGapMm: scale * 20, penetrationMm: scale * 10,
    sameVertexHorizontalStepMm: scale * 30, footRotationStepDeg: 12,
    minimumFrames, noiseGapFrames: 2 };
  const classes = rows.map(row => {
    if (row.sourceFootSpeed === null || row.sourceToesSpeed === null)
      return "uncertain";
    const footHeight = row.sourceFootY - floorFoot;
    const toesHeight = row.sourceToesY - floorToes;
    if (footHeight <= limits.supportHeightM &&
        toesHeight <= limits.supportHeightM &&
        row.sourceFootSpeed <= limits.supportSpeedMps &&
        row.sourceToesSpeed <= limits.supportSpeedMps)
      return "support";
    if (footHeight >= limits.airborneHeightM &&
        toesHeight >= limits.airborneHeightM &&
        row.sourceFootSpeed >= limits.airborneSpeedMps &&
        row.sourceToesSpeed >= limits.airborneSpeedMps)
      return "airborne";
    return "uncertain";
  });
  const hasStableFloor = hasClipMotion && ranges(classes).some(item =>
    item.classification === "support" && item.end - item.start + 1 >= minimumFrames);
  if (!hasStableFloor) classes.fill("uncertain");
  for (const item of ranges(classes))
    if (item.classification !== "uncertain" &&
        item.end - item.start + 1 < minimumFrames)
      classes.fill("uncertain", item.start, item.end + 1);
  const spans = ranges(classes);
  for (let index = 1; index + 1 < spans.length; index++) {
    const item = spans[index];
    if (item.classification === "uncertain" &&
        item.end - item.start + 1 <= limits.noiseGapFrames &&
        spans[index - 1].classification === spans[index + 1].classification &&
        spans[index - 1].classification !== "uncertain")
      classes.fill(spans[index - 1].classification, item.start, item.end + 1);
  }
  return { classes, floorFoot, floorToes, limits, hasStableFloor };
}

// 캡처가 기록한 원본 궤적 접지 의도와 이 분석기의 프레임 분류를 교차 대조함.
// 의도 구간의 과반 미만이 지지로 분류되면 불일치 사건으로 남김.
function crosscheckContactIntents(contactIntents, classesBySide) {
  if (!contactIntents || typeof contactIntents !== "object") return null;
  const summary = { source: contactIntents.source ?? null };
  if (contactIntents.error) summary.error = String(contactIntents.error);
  const mismatches = [];
  for (const side of ["left", "right"]) {
    const sideIntents = contactIntents[side];
    const classes = classesBySide[side];
    const intents = Array.isArray(sideIntents?.intents) ? sideIntents.intents : [];
    const uncertainSpans = Array.isArray(sideIntents?.uncertain_spans)
      ? sideIntents.uncertain_spans : [];
    const details = [];
    let coveredFrames = 0;
    let agreedFrames = 0;
    for (const intent of intents) {
      const start = intent?.start_frame;
      const end = intent?.end_frame_exclusive;
      const detail = { start_frame: start, end_frame_exclusive: end,
        mode: intent?.mode ?? null, certainty: intent?.certainty ?? null,
        starts_at_clip_start: intent?.starts_at_clip_start === true };
      if (!Number.isInteger(start) || !Number.isInteger(end) ||
          start < 0 || end > classes.length || start >= end) {
        detail.support_agreement = null;
        detail.malformed = true;
      } else {
        let support = 0;
        for (let frame = start; frame < end; frame++)
          if (classes[frame] === "support") support++;
        detail.support_agreement = support / (end - start);
        coveredFrames += end - start;
        agreedFrames += support;
        if (support * 2 < end - start)
          mismatches.push({ side, start_frame: start, end_frame: end - 1 });
      }
      details.push(detail);
    }
    // 의도 측 불확실 구간이 분석기에서는 확정됐는지도 교차 대조함.
    let uncertainOverlap = 0;
    for (const span of uncertainSpans) {
      if (!Array.isArray(span) || span.length !== 2 ||
          !Number.isInteger(span[0]) || !Number.isInteger(span[1])) continue;
      for (let frame = Math.max(0, span[0]);
           frame < Math.min(span[1], classes.length); frame++)
        if (classes[frame] !== "uncertain") uncertainOverlap++;
    }
    summary[side] = { intent_count: intents.length,
      support_frame_agreement: coveredFrames ? agreedFrames / coveredFrames : null,
      uncertain_overlap_frames: uncertainOverlap,
      uncertain_ratio: sideIntents?.uncertain_ratio ?? null,
      intents: details };
  }
  return { summary, mismatches };
}

function readFrames(state, csv) {
  const header = csv.replace(/^\uFEFF/, "").split(/\r?\n/, 1)[0].split(",");
  if (header.length < baseColumns.length ||
      baseColumns.some((field, index) => header[index] !== field) ||
      new Set(header).size !== header.length)
    throw new Error("47열 F10 계측 형식이 필요함");
  if (state?.status !== "metrics_complete_review_required" ||
      !Number.isInteger(state.last_frame) || state.last_frame < 1 ||
      !Number.isFinite(state.clip_frame_rate) || state.clip_frame_rate <= 0 ||
      !Number.isFinite(state.source_human_scale) || state.source_human_scale <= 0)
    throw new Error("완료된 F10 계측 상태가 필요함");
  const records = readCsv(csv.replace(/^\uFEFF/, ""));
  const expected = (state.last_frame + 1) * 2;
  if (records.length !== expected || state.row_count !== expected ||
      state.processed_frames !== state.last_frame + 1)
    throw new Error("전체 프레임·좌우 발 행 수 불일치");
  const bySide = { left: [], right: [] };
  let previousTime = -1;
  for (let index = 0; index < records.length; index++) {
    const row = records[index];
    const frame = Math.floor(index / 2);
    const time = Number(row.time_s);
    const side = index % 2 ? "right" : "left";
    if (Number(row.frame) !== frame || row.side !== side ||
        !Number.isFinite(time) || time < previousTime ||
        Math.abs(time - frame / state.clip_frame_rate) >
          0.5 / state.clip_frame_rate + 0.00002 ||
        !supportRoles.has(row.support_role) ||
        !groundingStatuses.has(row.grounding_status) ||
        !["True", "False"].includes(row.has_ground) ||
        (row.grounding_status === "NoGround" && row.has_ground !== "False"))
      throw new Error(`프레임·좌우·시간 또는 제품 지지 역할 오류: 행 ${index + 1}`);
    if (index % 2 === 0) previousTime = time;
    else if (time !== previousTime)
      throw new Error(`같은 프레임의 좌우 시간 불일치: ${frame}`);
    for (const field of baseColumns) {
      if (textColumns.has(field)) continue;
      if (row[field] === "" &&
          (optionalColumns.has(field) || (frame === 0 && speedColumns.has(field))))
        continue;
      if (row[field] === "" || !Number.isFinite(Number(row[field])))
        throw new Error(`계측값 누락·오류: ${field}, frame ${frame}, ${side}`);
    }
    const previous = bySide[side].at(-1);
    const rearPointX = Number(row.rear_point_x_m);
    const rearPointZ = Number(row.rear_point_z_m);
    const frontPointX = Number(row.front_point_x_m);
    const frontPointZ = Number(row.front_point_z_m);
    bySide[side].push({ frame, role: row.support_role,
      groundingStatus: row.grounding_status, hasGround: row.has_ground === "True",
      sourceFootY: Number(row.source_foot_y_m),
      sourceToesY: Number(row.source_toes_y_m),
      sourceFootSpeed: frame ? Number(row.source_foot_speed_mps) : null,
      sourceToesSpeed: frame ? Number(row.source_toes_speed_mps) : null,
      retargetFootY: Number(row.retarget_foot_y_m),
      retargetToesY: Number(row.retarget_toes_y_m),
      retargetFootSpeed: frame ? Number(row.retarget_foot_speed_mps) : null,
      retargetToesSpeed: frame ? Number(row.retarget_toes_speed_mps) : null,
      minimumSignedMm: Number(row.minimum_signed_mm),
      rearSignedMm: Number(row.rear_signed_mm),
      frontSignedMm: Number(row.front_signed_mm),
      footRotationStepDeg: Number(row.foot_rotation_step_deg),
      rearPointX, rearPointZ, frontPointX, frontPointZ,
      rearHorizontalStepMm: row.rear_step_mm === "" || !previous ? null :
        Math.hypot(rearPointX - previous.rearPointX,
          rearPointZ - previous.rearPointZ) * 1000,
      frontHorizontalStepMm: row.front_step_mm === "" || !previous ? null :
        Math.hypot(frontPointX - previous.frontPointX,
          frontPointZ - previous.frontPointZ) * 1000 });
  }
  return bySide;
}

function appendIssueEvents(events, segments, rows, classes, side, limits) {
  const issues = [
    { kind: "source_final_conflict", priority: 2, matches: (row, category) =>
      (category === "support" && row.role === "released") ||
      (category === "airborne" && row.role !== "released") },
    { kind: "final_measurement_unavailable", priority: 2, matches: (row, category) =>
      category === "support" && (row.groundingStatus !== "Applied" || !row.hasGround) },
    { kind: "sole_floating_candidate", priority: 3, matches: (row, category) =>
      category === "support" && row.groundingStatus === "Applied" && row.hasGround &&
      row.minimumSignedMm >= limits.floatingGapMm },
    { kind: "sole_penetration_candidate", priority: 3, matches: (row, category) =>
      category === "support" && row.groundingStatus === "Applied" && row.hasGround &&
      -row.minimumSignedMm >= limits.penetrationMm },
    { kind: "same_vertex_horizontal_step_candidate", priority: 3,
      matches: (row, category) =>
      category === "support" && row.groundingStatus === "Applied" && row.hasGround &&
      Math.max(row.rearHorizontalStepMm ?? 0, row.frontHorizontalStepMm ?? 0) >=
        limits.sameVertexHorizontalStepMm },
    { kind: "foot_rotation_step_candidate", priority: 2, matches: (row, category) =>
      category === "support" && row.footRotationStepDeg >= limits.footRotationStepDeg }
  ];
  for (const issue of issues) {
    let start = -1;
    let last = -1;
    let hitCount = 0;
    const emit = () => {
      if (start < 0) return;
      const priority = issue.kind === "source_final_conflict" &&
        hitCount >= limits.minimumFrames ? 3 : issue.priority;
      const id = `${side}-${issue.kind}-${start}-${last}`;
      const segment = segments.find(item => item.side === side &&
        item.start_frame <= start && item.end_frame >= start);
      events.push({ id, segment_id: segment?.id ?? null,
        side, start_frame: start, end_frame: last,
        representative_frame: Math.floor((start + last) / 2),
        kind: issue.kind, priority, hit_frames: hitCount,
        investigate_first: issue.kind === "source_final_conflict"
          ? "원본 판정·리타게팅·접지 보정 비교"
          : "최종 접지·메시 확인",
        status: "MANUAL_REVIEW_REQUIRED" });
    };
    for (let index = 0; index < rows.length; index++) {
      if (!issue.matches(rows[index], classes[index])) continue;
      if (start >= 0 && index - last > limits.noiseGapFrames + 1) {
        emit(); start = -1; hitCount = 0;
      }
      if (start < 0) start = index;
      last = index;
      hitCount++;
    }
    emit();
  }
}

export function analyzeContactEvents(state, csv, fingerprint = {}) {
  try {
    const bySide = readFrames(state, csv);
    const segments = [];
    const events = [];
    const transitions = [];
    const thresholds = {};
    const hasClipMotion = ["left", "right"].some(side =>
      maximum(bySide[side].map(row => row.sourceFootY)) -
        minimum(bySide[side].map(row => row.sourceFootY)) >=
          state.source_human_scale * 0.05);
    const classesBySide = {};
    for (const side of ["left", "right"]) {
      const rows = bySide[side];
      const { classes, floorFoot, floorToes, limits, hasStableFloor } =
        classifySide(rows, state.source_human_scale, state.clip_frame_rate,
          hasClipMotion);
      classesBySide[side] = classes;
      thresholds[side] = { floorFootM: floorFoot, floorToesM: floorToes,
        hasStableFloor, ...limits };
      let previousConfident = null;
      for (const span of ranges(classes)) {
        const selected = rows.slice(span.start, span.end + 1);
        const measured = selected.filter(row => row.hasGround &&
          row.groundingStatus === "Applied");
        const roleCounts = { released: 0, front: 0, rear: 0, both: 0 };
        let conflictFrames = 0;
        for (const row of selected) {
          roleCounts[row.role]++;
          if ((span.classification === "support" && row.role === "released") ||
              (span.classification === "airborne" && row.role !== "released"))
            conflictFrames++;
        }
        const segment = { id: `${side}-${span.start}-${span.end}`, side,
          start_frame: span.start, end_frame: span.end,
          representative_frame: Math.floor((span.start + span.end) / 2),
          source_classification: span.classification,
          source_label: labels[span.classification],
          contact_form: "uncertain", motion_intent: "uncertain",
          uncertain_reason: span.classification === "uncertain"
            ? hasStableFloor ? "원본 높이·속도·지속시간의 근거 부족 또는 충돌"
              : "안정적인 원본 바닥 후보 없음" : null,
          source_median: {
            foot_height_above_floor_m: percentile(selected.map(row =>
              row.sourceFootY - floorFoot), 0.5),
            toes_height_above_floor_m: percentile(selected.map(row =>
              row.sourceToesY - floorToes), 0.5),
            foot_speed_mps: percentile(selected.map(row => row.sourceFootSpeed), 0.5),
            toes_speed_mps: percentile(selected.map(row => row.sourceToesSpeed), 0.5)
          },
          retarget_median: {
            foot_y_m: percentile(selected.map(row => row.retargetFootY), 0.5),
            toes_y_m: percentile(selected.map(row => row.retargetToesY), 0.5),
            foot_speed_mps: percentile(selected.map(row => row.retargetFootSpeed), 0.5),
            toes_speed_mps: percentile(selected.map(row => row.retargetToesSpeed), 0.5)
          },
          final_measurements: { support_role_frames: roleCounts,
            ground_measurement_frames: measured.length,
            source_final_conflict_frames: conflictFrames,
            maximum_minimum_sole_gap_mm: measured.length ? maximum(measured.map(row =>
              row.minimumSignedMm)) : null,
            maximum_penetration_mm: measured.length ? Math.max(0,
              maximum(measured.map(row => -row.minimumSignedMm))) : null,
            maximum_same_vertex_horizontal_step_mm: percentile(measured.flatMap(row =>
              [row.rearHorizontalStepMm, row.frontHorizontalStepMm]), 1) ?? null,
            maximum_rear_signed_mm: measured.length ? maximum(measured.map(row =>
              row.rearSignedMm)) : null,
            maximum_front_signed_mm: measured.length ? maximum(measured.map(row =>
              row.frontSignedMm)) : null },
          status: "MANUAL_REVIEW_REQUIRED" };
        segments.push(segment);
        if (span.classification === "uncertain")
          events.push({ id: `${segment.id}-uncertain_source`, segment_id: segment.id,
            side, start_frame: span.start,
            end_frame: span.end, representative_frame: segment.representative_frame,
            kind: "uncertain_source", priority: 1,
            investigate_first: "원본 판정 확인", status: "MANUAL_REVIEW_REQUIRED" });
        if (span.classification !== "uncertain") {
          if (previousConfident &&
              previousConfident.classification !== span.classification &&
              span.start - previousConfident.end <= limits.noiseGapFrames + 1)
            transitions.push({ side, from: previousConfident.classification,
              to: span.classification, start_frame: previousConfident.end,
              end_frame: span.start,
              representative_frame: Math.floor((previousConfident.end +
                span.start) / 2), status: "MANUAL_REVIEW_REQUIRED" });
          previousConfident = span;
        }
      }
      appendIssueEvents(events, segments, rows, classes, side, limits);
    }
    // 캡처 기록의 접지 의도와 프레임 분류가 크게 어긋난 구간은 검토 사건으로 남김.
    const intentCrosscheck =
      crosscheckContactIntents(state.contact_intents, classesBySide);
    if (intentCrosscheck) {
      for (const mismatch of intentCrosscheck.mismatches) {
        events.push({
          id: `${mismatch.side}-contact_intent_mismatch-${mismatch.start_frame}-${mismatch.end_frame}`,
          segment_id: null, side: mismatch.side,
          start_frame: mismatch.start_frame, end_frame: mismatch.end_frame,
          representative_frame: Math.floor((mismatch.start_frame +
            mismatch.end_frame) / 2),
          kind: "contact_intent_mismatch", priority: 2,
          hit_frames: mismatch.end_frame - mismatch.start_frame + 1,
          investigate_first: "원본 의도 추정과 오프라인 분석 구간 비교",
          status: "MANUAL_REVIEW_REQUIRED" });
      }
    }
    events.sort((a, b) => b.priority - a.priority ||
      (b.end_frame - b.start_frame) - (a.end_frame - a.start_frame) ||
      a.start_frame - b.start_frame || a.side.localeCompare(b.side));
    const reviewQueue = events.slice(0, 20).map(item => item.id);
    return { status: "MANUAL_REVIEW_REQUIRED", analysis_version: 1,
      input: { fbx: state.input, model: state.model, scene: state.scene,
        frame_rate: state.clip_frame_rate, last_frame: state.last_frame,
        row_count: state.row_count, source_human_scale: state.source_human_scale,
        ...fingerprint },
      limits: thresholds, contact_intent_crosscheck: intentCrosscheck?.summary ?? null,
      summary: { segment_count: segments.length,
        event_count: events.length, transition_count: transitions.length,
        review_queue_count: reviewQueue.length },
      limitations: ["원본 XZ·root 궤적이 없어 고정·의도된 이동을 확정하지 않음",
        "원본 발 본은 밑창 압력·앞꿈치·뒤꿈치 접촉의 직접 측정값이 아님",
        "클립 내 바닥 높이와 물리 이상 임계값은 사람 표식으로 정확도 검증 전인 후보 기준",
        "제품 support_role은 원본 판정의 정답으로 사용하지 않음"],
      segments, events, transitions, review_queue: reviewQueue };
  } catch (error) {
    return { status: "BLOCKED", reason: error.message };
  }
}

// 허용된 증거 패밀리 아래 <runId>/<requestId> 구조만 풀어줌.
// 경로 상위 이동이나 다른 폴더는 거부함.
export function resolveEvidenceDirectoryPath(argument, root = evidenceRoot) {
  const directory = path.resolve(argument || "");
  const relative = path.relative(root, directory);
  if (!argument || !relative || relative.startsWith("..") ||
      path.isAbsolute(relative)) return null;
  const segments = relative.split(path.sep);
  if (segments.length !== 3 || !Object.hasOwn(evidenceFamilies, segments[0]))
    return null;
  return { directory, family: segments[0], runId: segments[1], requestId: segments[2] };
}

// 심볼릭 링크·연결점 우회를 막기 위해 실제 경로와 실행 경로가 같은지 확인함.
export async function resolveEvidenceDirectory(argument, root = evidenceRoot) {
  const resolved = resolveEvidenceDirectoryPath(argument, root);
  if (!resolved)
    throw new Error(
      "로컬 full-clip, full-clip-f14, vrm-character의 <runId>/<requestId> 근거 폴더가 필요합니다.");
  const realRoot = await realpath(root);
  const realDirectory = await realpath(resolved.directory);
  const realRelative = path.relative(realRoot, realDirectory);
  const relative = path.relative(root, resolved.directory);
  if (realRelative !== relative)
    throw new Error("로컬 근거 폴더의 실제 경로가 실행 경로와 다름");
  return resolved;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const { directory, family, runId, requestId } =
    await resolveEvidenceDirectory(process.argv[2]);
  const outputPath = path.join(directory, "contact-events.json");
  const outputStat = await lstat(outputPath).catch(error => {
    if (error.code === "ENOENT") return null;
    throw error;
  });
  if (outputStat?.isSymbolicLink())
    throw new Error("분석 결과 파일은 심볼릭 링크일 수 없음");
  let result;
  try {
    const sdkManifestPath = path.join(evidenceRoot, "projects", sdkProjectId,
      "runs", runId, "run-manifest.json");
    const sessionManifestPath = path.join(evidenceRoot, evidenceFamilies[family],
      runId, "manifest.json");
    const [stateSource, csv, sdkManifestSource, sessionManifestSource] = await Promise.all([
      readFile(path.join(directory, "state.json"), "utf8"),
      readFile(path.join(directory, "all-frames.csv"), "utf8"),
      readFile(sdkManifestPath, "utf8"),
      readFile(sessionManifestPath, "utf8")
    ]);
    const sdkManifest = JSON.parse(sdkManifestSource);
    const sessionManifest = JSON.parse(sessionManifestSource);
    if (sdkManifest.runId !== runId || sessionManifest.runId !== runId ||
        sessionManifest.requestId !== requestId ||
        sessionManifest.result !== "MANUAL_REVIEW_REQUIRED")
      throw new Error("SDK 실행 ID와 F10 근거 경로가 일치하지 않음");
    const sha256 = source => createHash("sha256").update(source).digest("hex");
    result = analyzeContactEvents(JSON.parse(stateSource), csv,
      { state_sha256: sha256(stateSource), csv_sha256: sha256(csv),
        sdk_input_conditions: sdkManifest.inputConditions,
        sdk_environment_conditions: sdkManifest.environmentConditions });
  } catch (error) {
    result = { status: "BLOCKED", reason: error.message };
  }
  await writeFile(outputPath, JSON.stringify(result, null, 2));
  process.stdout.write(`${JSON.stringify({ status: result.status,
    segments: result.summary?.segment_count ?? 0,
    events: result.summary?.event_count ?? 0,
    resultPath: outputPath })}\n`);
  process.exitCode = result.status === "BLOCKED" ? 1 : 0;
}
