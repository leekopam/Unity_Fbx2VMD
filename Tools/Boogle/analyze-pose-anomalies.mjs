import { lstat, readFile, realpath, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";
import { resolveEvidenceDirectory } from "./analyze-contact-events.mjs";

const outputName = "pose-anomalies.json";

// 결정적 자세 결함 계측 임계값. 거리 계열은 source_human_scale로 정규화함.
export const defaultThresholds = {
  // 접지 프레임의 발 수평 드리프트(mm/프레임, 인간 체형 배율 곱)
  residualSlideMmPerScale: 12,
  // 무릎 역신전·과굴곡(도). 음수는 역신전
  kneeHyperextensionDeg: -4,
  kneeMaxFlexionDeg: 170,
  // 발목 피치 허용 범위(도) — 족저굴곡 양수, 배측굴곡 음수
  anklePitchMinDeg: -45,
  anklePitchMaxDeg: 75,
  // 발 회전 프레임간 스텝(도) — 이상치 상한
  footRotationSpikeDeg: 25,
  // 골반 높이 프레임간 스텝(mm, 배율 곱) — 핀 경계 팝·지터 공용
  hipsStepSpikeMmPerScale: 40,
  // 발바닥 침투(mm 음수 초과분)
  penetrationMm: 10,
  // 핀 on/off 전후 탐색 창(프레임)
  pinBoundaryWindow: 2,
};

function percentile(values, fraction) {
  const sorted = values.filter(Number.isFinite).sort((a, b) => a - b);
  return sorted.length ? sorted[Math.floor((sorted.length - 1) * fraction)] : null;
}

function numberOrNull(value) {
  const parsed = Number(value);
  return value === "" || value === undefined || !Number.isFinite(parsed)
    ? null : parsed;
}

// 같은 (side, defect)의 연속 프레임을 하나의 구간으로 병합함.
function mergeSpans(flags) {
  const spans = [];
  for (let index = 0; index < flags.length;) {
    if (!flags[index]) { index++; continue; }
    let end = index;
    while (end + 1 < flags.length && flags[end + 1]) end++;
    spans.push({ start_frame: flags[index].frame, end_frame: flags[end].frame });
    index = end + 1;
  }
  return spans;
}

function severity(ratio) {
  return ratio >= 2 ? "high" : "medium";
}

// 자세 이상 결정적 계측기. 결과는 Jev 2차 심판과 리뷰 큐가 소비함.
// 열이 없는 구형 CSV는 해당 계측을 건너뛰고 skipped에 기록함.
export function analyzePoseAnomalies(csv, options = {}) {
  const thresholds = { ...defaultThresholds, ...(options.thresholds || {}) };
  const scale = Number(options.sourceHumanScale) > 0
    ? Number(options.sourceHumanScale) : 1;
  let rows;
  try { rows = readCsv(String(csv).replace(/^\uFEFF/, "")); }
  catch { rows = []; }
  if (!rows.length) return { status: "BLOCKED", reason: "all-frames.csv가 비어 있음" };

  const columns = Object.keys(rows[0]);
  const has = name => columns.includes(name);
  const anomalies = [];
  const skipped = [];
  const frameCount = Math.max(...rows.map(r => Number(r.frame))) + 1;

  // 프레임·측 별 행 인덱스
  const cell = new Map();
  for (const row of rows) cell.set(`${row.frame}:${row.side}`, row);

  const flag = (defect, side, frameFlags, ratio, evidence) => {
    for (const span of mergeSpans(frameFlags)) {
      anomalies.push({
        defect_type: defect, side,
        start_frame: span.start_frame, end_frame: span.end_frame,
        severity: severity(ratio), evidence,
      });
    }
  };

  // 1. 잔류 미끄럼 — 접지 프레임의 발 수평 드리프트·피크 스텝
  if (has("has_ground") && has("foot_x_m") && has("foot_z_m")) {
    const stepLimit = thresholds.residualSlideMmPerScale * scale;
    for (const side of ["left", "right"]) {
      const flags = [];
      let previous = null;
      for (let frame = 0; frame < frameCount; frame++) {
        const row = cell.get(`${frame}:${side}`);
        const grounded = row && row.has_ground === "True" &&
          row.support_role !== "released";
        if (!grounded) { previous = null; continue; }
        let drift = 0;
        if (previous) {
          drift = Math.hypot(numberOrNull(row.foot_x_m) - previous.x,
            numberOrNull(row.foot_z_m) - previous.z) * 1000;
        }
        previous = { x: numberOrNull(row.foot_x_m), z: numberOrNull(row.foot_z_m) };
        if (drift > stepLimit) flags.push({ frame });
      }
      if (flags.length) flag("residual_slide", side, flags, 1.5,
        { step_limit_mm: stepLimit });
    }
  } else skipped.push("residual_slide");

  // 2. 관절 한계 — 무릎 역신전/과굴곡·발목 범위 (Phase 1 열)
  if (has("knee_flexion_deg") && has("ankle_pitch_deg")) {
    for (const side of ["left", "right"]) {
      const kneeFlags = [], ankleFlags = [];
      for (let frame = 0; frame < frameCount; frame++) {
        const row = cell.get(`${frame}:${side}`);
        if (!row) continue;
        const knee = numberOrNull(row.knee_flexion_deg);
        const ankle = numberOrNull(row.ankle_pitch_deg);
        if (knee !== null && (knee < thresholds.kneeHyperextensionDeg ||
          knee > thresholds.kneeMaxFlexionDeg)) kneeFlags.push({ frame });
        if (ankle !== null && (ankle < thresholds.anklePitchMinDeg ||
          ankle > thresholds.anklePitchMaxDeg)) ankleFlags.push({ frame });
      }
      if (kneeFlags.length) flag("joint_limit_knee", side, kneeFlags, 2,
        { min_deg: thresholds.kneeHyperextensionDeg });
      if (ankleFlags.length) flag("joint_limit_ankle", side, ankleFlags, 1.5,
        { range_deg: [thresholds.anklePitchMinDeg, thresholds.anklePitchMaxDeg] });
    }
  } else skipped.push("joint_limit");

  // 3. 지터 — 발 회전·골반 높이 스텝 스파이크(절대 상한 + p99 대비)
  if (has("foot_rotation_step_deg")) {
    for (const side of ["left", "right"]) {
      const steps = [];
      for (let frame = 1; frame < frameCount; frame++) {
        const row = cell.get(`${frame}:${side}`);
        const value = row ? numberOrNull(row.foot_rotation_step_deg) : null;
        if (value !== null) steps.push({ frame, value });
      }
      const p99 = percentile(steps.map(s => s.value), 0.99) ?? 0;
      const limit = Math.max(thresholds.footRotationSpikeDeg, p99 * 1.5);
      const flags = steps.filter(s => s.value > limit);
      if (flags.length) flag("jitter_foot_rotation", side, flags,
        Math.max(...flags.map(f => f.value)) / limit, { limit_deg: limit });
    }
  } else skipped.push("jitter_foot_rotation");

  if (has("hips_y_m")) {
    const hips = [];
    for (let frame = 0; frame < frameCount; frame++) {
      const row = cell.get(`${frame}:left`) || cell.get(`${frame}:right`);
      if (row) hips.push({ frame, y: numberOrNull(row.hips_y_m) });
    }
    const steps = [];
    for (let index = 1; index < hips.length; index++) {
      if (hips[index].y === null || hips[index - 1].y === null) continue;
      steps.push({ frame: hips[index].frame,
        value: Math.abs(hips[index].y - hips[index - 1].y) * 1000 });
    }
    const limit = thresholds.hipsStepSpikeMmPerScale * scale;
    const flags = steps.filter(s => s.value > limit);
    if (flags.length) flag("jitter_hips", "both", flags,
      Math.max(...flags.map(f => f.value)) / limit, { limit_mm: limit });
  } else skipped.push("jitter_hips");

  // 4. 침투 — 발바닥이 지면 아래로 파고든 깊이
  if (has("minimum_signed_mm")) {
    for (const side of ["left", "right"]) {
      const flags = [];
      for (let frame = 0; frame < frameCount; frame++) {
        const row = cell.get(`${frame}:${side}`);
        const value = row ? numberOrNull(row.minimum_signed_mm) : null;
        if (value !== null && value < -thresholds.penetrationMm)
          flags.push({ frame });
      }
      if (flags.length) flag("penetration", side, flags, 1.5,
        { limit_mm: -thresholds.penetrationMm });
    }
  } else skipped.push("penetration");

  // 5. 균형 — CoM 투영이 지지 폴리곤 밖 (Phase 1 열)
  if (has("support_polygon_contains_com")) {
    const flags = [];
    for (let frame = 0; frame < frameCount; frame++) {
      const row = cell.get(`${frame}:left`);
      if (!row) continue;
      const grounded = row.has_ground === "True";
      if (grounded && row.support_polygon_contains_com === "False")
        flags.push({ frame });
    }
    if (flags.length) flag("balance_escape", "both", flags, 1, {});
  } else skipped.push("balance_escape");

  // 6. 보정 편차 — 리타겟↔보정 후 관절 회전 차이 (열이 채워진 경우만)
  if (has("joint_delta_deg")) {
    for (const side of ["left", "right"]) {
      const deltas = [];
      for (let frame = 0; frame < frameCount; frame++) {
        const row = cell.get(`${frame}:${side}`);
        const value = row ? numberOrNull(row.joint_delta_deg) : null;
        if (value !== null) deltas.push({ frame, value });
      }
      if (!deltas.length) continue;
      const p95 = percentile(deltas.map(d => d.value), 0.95) ?? 0;
      const limit = Math.max(8, p95 * 2);
      const flags = deltas.filter(d => d.value > limit);
      if (flags.length) flag("correction_deviation", side, flags,
        Math.max(...flags.map(f => f.value)) / limit, { limit_deg: limit });
    }
  } else skipped.push("correction_deviation");

  // 7. 핀 경계 팝 — 접지 on/off 전환 ±window 내 골반·발 스텝 스파이크
  if (has("has_ground") && has("hips_y_m")) {
    const transitions = new Set();
    for (const side of ["left", "right"]) {
      for (let frame = 1; frame < frameCount; frame++) {
        const current = cell.get(`${frame}:${side}`);
        const previous = cell.get(`${frame - 1}:${side}`);
        if (current && previous && current.has_ground !== previous.has_ground)
          transitions.add(frame);
      }
    }
    const window = thresholds.pinBoundaryWindow;
    const hipStep = thresholds.hipsStepSpikeMmPerScale * scale * 0.5;
    const flags = [];
    for (const frame of transitions) {
      for (let delta = -window; delta <= window; delta++) {
        const a = cell.get(`${frame + delta - 1}:left`);
        const b = cell.get(`${frame + delta}:left`);
        if (!a || !b) continue;
        const step = Math.abs((numberOrNull(b.hips_y_m) ?? 0) -
          (numberOrNull(a.hips_y_m) ?? 0)) * 1000;
        if (step > hipStep) { flags.push({ frame }); break; }
      }
    }
    if (flags.length) flag("pin_boundary_pop", "both", flags, 1.2,
      { transition_count: transitions.size });
  } else skipped.push("pin_boundary_pop");

  // 8. 자세 사전 점수 — Phase P가 채운 novelty 열(상위 p99 이상)
  for (const [defect, column] of [["pose_novelty_spike", "pose_novelty"],
    ["transition_novelty_spike", "transition_novelty"]]) {
    if (!has(column)) { skipped.push(defect); continue; }
    for (const side of ["left", "right"]) {
      const values = [];
      for (let frame = 0; frame < frameCount; frame++) {
        const row = cell.get(`${frame}:${side}`);
        const value = row ? numberOrNull(row[column]) : null;
        if (value !== null) values.push({ frame, value });
      }
      if (values.length < 30) continue;
      const p99 = percentile(values.map(v => v.value), 0.99);
      const flags = values.filter(v => v.value > p99);
      if (flags.length) flag(defect, side, flags, 1,
        { p99: p99, note: "novelty는 후보 신호 — 단독 판정 금지" });
    }
  }

  return {
    status: anomalies.length ? "ANOMALIES_FOUND" : "CLEAN",
    anomaly_count: anomalies.length,
    skipped,
    thresholds,
    anomalies: anomalies.sort((a, b) => a.start_frame - b.start_frame ||
      a.defect_type.localeCompare(b.defect_type)),
  };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const { directory } = await resolveEvidenceDirectory(process.argv[2]);
  const outputPath = path.join(directory, outputName);
  const outputStat = await lstat(outputPath).catch(error => {
    if (error.code === "ENOENT") return null;
    throw error;
  });
  if (outputStat?.isSymbolicLink())
    throw new Error("자세 이상 결과 파일은 심볼릭 링크일 수 없음");
  let result;
  try {
    const [csv, stateSource] = await Promise.all([
      readFile(path.join(directory, "all-frames.csv"), "utf8"),
      readFile(path.join(directory, "state.json"), "utf8").catch(() => null),
    ]);
    const state = stateSource ? JSON.parse(stateSource) : {};
    result = analyzePoseAnomalies(csv,
      { sourceHumanScale: state.source_human_scale });
    result.run = path.basename(path.dirname(directory)) + "/" +
      path.basename(directory);
  } catch (error) {
    result = { status: "BLOCKED", reason: error.message };
  }
  await writeFile(outputPath, JSON.stringify(result, null, 2));
  process.stdout.write(`${JSON.stringify({ status: result.status,
    anomalies: result.anomaly_count ?? 0, resultPath: outputPath })}\n`);
  process.exitCode = result.status === "BLOCKED" ? 1 : 0;
}
