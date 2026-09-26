import assert from "node:assert/strict";
import { mkdir, mkdtemp, rm, symlink } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { analyzeContactEvents, resolveEvidenceDirectory,
  resolveEvidenceDirectoryPath } from "./analyze-contact-events.mjs";

const columns = ("frame,time_s,time_error_ms,side,grounding_status,has_ground," +
  "support_role,rear_weight,front_weight,rear_signed_mm,front_signed_mm," +
  "minimum_signed_mm,rear_anchor_x_m,rear_anchor_y_m,rear_anchor_z_m," +
  "front_anchor_x_m,front_anchor_y_m,front_anchor_z_m,rear_point_x_m," +
  "rear_point_y_m,rear_point_z_m,front_point_x_m,front_point_y_m,front_point_z_m," +
  "rear_vertex,front_vertex,rear_step_mm,front_step_mm,foot_rotation_step_deg," +
  "foot_x_m,foot_y_m,foot_z_m,foot_pitch_deg,foot_yaw_deg,foot_roll_deg," +
  "toes_y_m,knee_y_m,hips_y_m,root_y_m,source_foot_y_m,source_toes_y_m," +
  "source_foot_speed_mps,source_toes_speed_mps,retarget_foot_y_m," +
  "retarget_toes_y_m,retarget_foot_speed_mps,retarget_toes_speed_mps").split(",");
const state = { status: "metrics_complete_review_required", last_frame: 19,
  processed_frames: 20, row_count: 40, clip_frame_rate: 60,
  source_human_scale: 1, scene: "Main_Auto", model: "YYB", input: "motion.fbx" };

function createCsv() {
  const lines = [columns.join(",")];
  for (let frame = 0; frame < 20; frame++) {
    for (const side of ["left", "right"]) {
      const airborne = frame >= 12;
      const uncertain = frame >= 8 && frame < 12;
      const values = Object.fromEntries(columns.map(field => [field, "0"]));
      Object.assign(values, { frame, side, time_s: frame / 60,
        grounding_status: "Applied", has_ground: "True",
        support_role: airborne ? "released" :
          side === "left" && frame >= 2 && frame <= 7 ? "released" : "both",
        source_foot_y_m: airborne ? 0.2 : uncertain ? 0.1 : 0.08,
        source_toes_y_m: airborne ? 0.15 : uncertain ? 0.04 : 0.02,
        source_foot_speed_mps: frame === 0 ? "" :
          airborne ? 0.5 : uncertain ? 0.12 : 0.01,
        source_toes_speed_mps: frame === 0 ? "" :
          airborne ? 0.5 : uncertain ? 0.12 : 0.01,
        retarget_foot_speed_mps: frame === 0 ? "" : 0.01,
        retarget_toes_speed_mps: frame === 0 ? "" : 0.01,
        rear_step_mm: "", front_step_mm: "" });
      lines.push(columns.map(field => values[field]).join(","));
    }
  }
  return `${lines.join("\n")}\n`;
}

test("47열 전체 프레임에서 지지·자유발·불확실과 충돌 사건 추출", () => {
  const csv = createCsv();
  const result = analyzeContactEvents(state, csv);
  assert.equal(result.status, "MANUAL_REVIEW_REQUIRED");
  assert.equal(result.input.row_count, 40);
  assert.ok(result.segments.some(item => item.side === "left" &&
    item.source_classification === "support"));
  assert.ok(result.segments.some(item => item.side === "left" &&
    item.source_classification === "airborne"));
  assert.ok(result.segments.some(item => item.source_classification === "uncertain"));
  assert.ok(result.events.some(item => item.kind === "source_final_conflict" &&
    item.side === "left" && item.start_frame === 2 && item.end_frame === 7 &&
    item.priority === 3));
  assert.ok(result.segments.every(item => item.contact_form === "uncertain" &&
    item.motion_intent === "uncertain"));
  assert.deepEqual(result, analyzeContactEvents(state, csv));
});

test("빈 자료·39열·누락·중복·시간 역전 차단", () => {
  const csv = createCsv();
  assert.equal(analyzeContactEvents(state, "").status, "BLOCKED");
  const legacy = csv.trimEnd().split("\n").map(line =>
    line.split(",").slice(0, 39).join(",")).join("\n");
  assert.equal(analyzeContactEvents(state, legacy).status, "BLOCKED");
  const lines = csv.trimEnd().split("\n");
  assert.equal(analyzeContactEvents(state,
    `${lines.filter((_, index) => index !== 5).join("\n")}\n`).status, "BLOCKED");
  const duplicate = [...lines];
  duplicate[4] = duplicate[3];
  assert.equal(analyzeContactEvents(state, `${duplicate.join("\n")}\n`).status,
    "BLOCKED");
  const reversed = [...lines];
  const cells = reversed[21].split(",");
  cells[1] = "-1";
  reversed[21] = cells.join(",");
  assert.equal(analyzeContactEvents(state, `${reversed.join("\n")}\n`).status,
    "BLOCKED");
  const invalidStatus = [...lines];
  const invalidCells = invalidStatus[3].split(",");
  invalidCells[columns.indexOf("grounding_status")] = "Unknown";
  invalidStatus[3] = invalidCells.join(",");
  assert.equal(analyzeContactEvents(state,
    `${invalidStatus.join("\n")}\n`).status, "BLOCKED");
});

test("물리 이상·측정 불가를 국소 사건으로 기록하고 정지 클립은 불확실", () => {
  const lines = createCsv().trimEnd().split("\n");
  const anomalous = lines[7].split(",");
  anomalous[columns.indexOf("minimum_signed_mm")] = "50";
  anomalous[columns.indexOf("rear_step_mm")] = "50";
  anomalous[columns.indexOf("rear_point_x_m")] = "0.05";
  lines[7] = anomalous.join(",");
  const unavailable = lines[9].split(",");
  unavailable[columns.indexOf("has_ground")] = "False";
  lines[9] = unavailable.join(",");
  const result = analyzeContactEvents(state, `${lines.join("\n")}\n`);
  assert.equal(result.status, "MANUAL_REVIEW_REQUIRED");
  assert.ok(result.events.some(item => item.kind === "sole_floating_candidate" &&
    item.side === "left" && item.start_frame === 3 && item.end_frame === 3));
  assert.ok(result.events.some(item =>
    item.kind === "same_vertex_horizontal_step_candidate" &&
    item.start_frame === 3));
  assert.ok(result.events.some(item =>
    item.kind === "final_measurement_unavailable" && item.start_frame === 4));
  const staticLines = createCsv().trimEnd().split("\n");
  for (let index = 1; index < staticLines.length; index++) {
    const cells = staticLines[index].split(",");
    cells[columns.indexOf("source_foot_y_m")] = "0.08";
    cells[columns.indexOf("source_toes_y_m")] = "0.02";
    staticLines[index] = cells.join(",");
  }
  const staticResult = analyzeContactEvents(state, `${staticLines.join("\n")}\n`);
  assert.equal(staticResult.status, "MANUAL_REVIEW_REQUIRED");
  assert.ok(staticResult.segments.every(item =>
    item.source_classification === "uncertain"));
});

test("state.json의 접지 의도 추정과 분석 구간을 교차 대조한다", () => {
  const csv = createCsv();
  const enriched = { ...state, contact_intents: {
    source: "source_fbx_trajectory",
    left: { intents: [
        { start_frame: 1, end_frame_exclusive: 8, mode: "plant",
          certainty: "confident", starts_at_clip_start: true,
          anchor_m: [0, 0, 0] },
        { start_frame: 12, end_frame_exclusive: 16, mode: "plant",
          certainty: "confident", starts_at_clip_start: false,
          anchor_m: [0, 0, 0] }],
      uncertain_spans: [[12, 14]], uncertain_ratio: 0.25 },
    right: { intents: [], uncertain_spans: [], uncertain_ratio: 1 } } };
  const result = analyzeContactEvents(enriched, csv);
  const check = result.contact_intent_crosscheck;
  assert.equal(check.source, "source_fbx_trajectory");
  assert.equal(check.left.intent_count, 2);
  // 1~7 프레임 의도는 분석기도 지지로 분류해 완전 일치함.
  assert.equal(check.left.intents[0].support_agreement, 1);
  assert.equal(check.left.intents[0].starts_at_clip_start, true);
  // 12~15 프레임 의도는 분석기가 자유발로 분류해 불일치 사건이 된다.
  assert.equal(check.left.intents[1].support_agreement, 0);
  assert.ok(Math.abs(check.left.support_frame_agreement - 7 / 11) < 1e-9);
  // 의도 측 불확실 구간 12~13은 분석기에서 자유발로 확정돼 겹침 2프레임임.
  assert.equal(check.left.uncertain_overlap_frames, 2);
  assert.equal(check.left.uncertain_ratio, 0.25);
  assert.equal(check.right.intent_count, 0);
  assert.equal(check.right.support_frame_agreement, null);
  assert.ok(result.events.some(item => item.kind === "contact_intent_mismatch" &&
    item.side === "left" && item.start_frame === 12 && item.end_frame === 15));
});

test("접지 의도 추정이 없는 상태 문서는 교차 대조 없이 기존 결과를 유지한다", () => {
  const csv = createCsv();
  const result = analyzeContactEvents(state, csv);
  assert.equal(result.status, "MANUAL_REVIEW_REQUIRED");
  assert.equal(result.contact_intent_crosscheck, null);
  assert.ok(result.events.every(item =>
    item.kind !== "contact_intent_mismatch"));
});

test("Applied↔Fallback 번복은 게이트 요동 사건으로 기록한다", () => {
  const lines = createCsv().trimEnd().split("\n");
  const setStatus = (frame, side, status) => {
    const index = frame * 2 + (side === "right" ? 2 : 1);
    const cells = lines[index].split(",");
    cells[columns.indexOf("grounding_status")] = status;
    lines[index] = cells.join(",");
  };
  // 왼발 5~8프레임: A,F,A,F,A — 4회 번복으로 요동 구간이 됨.
  for (const [frame, status] of [[5, "Fallback"], [6, "Applied"],
      [7, "Fallback"], [8, "Applied"]])
    setStatus(frame, "left", status);
  // 오른발 12~15프레임: 지속 Fallback — 가장자리 전이 1회씩이라 요동 아님.
  for (const frame of [12, 13, 14, 15])
    setStatus(frame, "right", "Fallback");
  const result = analyzeContactEvents(state, `${lines.join("\n")}\n`);
  const flickers = result.events.filter(item => item.kind === "applied_gate_flicker");
  assert.equal(flickers.length, 1);
  assert.equal(flickers[0].side, "left");
  assert.equal(flickers[0].start_frame, 4);
  assert.equal(flickers[0].end_frame, 8);
  assert.equal(flickers[0].hit_frames, 4);
  assert.ok(result.events.every(item => item.kind !== "applied_gate_flicker" ||
    item.side !== "right"));
});

test("게이트 수치 열이 있으면 요약을 만들고 없으면 null을 유지한다", () => {
  const base = createCsv();
  assert.equal(analyzeContactEvents(state, base).gate, null);
  const gateColumns = ["grounding_target_error_mm", "grounding_sole_clearance_mm",
    "grounding_contact_error_mm", "grounding_supported_contacts"];
  const lines = base.trimEnd().split("\n");
  lines[0] = `${lines[0]},${gateColumns.join(",")}`;
  for (let index = 1; index < lines.length; index++) {
    const fallback = index === 12;
    lines[index] = `${lines[index]},${fallback ? "0,0,9,1" : "0,0,0,0"}`;
    if (fallback) {
      const cells = lines[index].split(",");
      cells[columns.indexOf("grounding_status")] = "Fallback";
      lines[index] = cells.join(",");
    }
  }
  const result = analyzeContactEvents(state, `${lines.join("\n")}\n`);
  assert.equal(result.gate.left.measured_frames, 20);
  assert.equal(result.gate.left.fallback_frames, 0);
  assert.equal(result.gate.right.measured_frames, 20);
  assert.equal(result.gate.right.fallback_frames, 1);
  assert.equal(result.gate.right.maximum_supported_contact_error_mm, 9);
});

test("허용된 증거 패밀리의 runId/requestId 경로만 풀어준다", () => {
  const root = path.resolve("D:/evidence/boogle");
  const runId = "11111111-2222-3333-4444-555555555555";
  const requestId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
  for (const family of ["full-clip", "full-clip-f14", "vrm-character"]) {
    const resolved = resolveEvidenceDirectoryPath(
      path.join(root, family, runId, requestId), root);
    assert.equal(resolved.family, family);
    assert.equal(resolved.runId, runId);
    assert.equal(resolved.requestId, requestId);
  }
  // 허용 패밀리 밖의 폴더, 깊이 부족·초과, 상위 이동, 루트 밖 경로는 거부함.
  for (const bad of [
    path.join(root, "full-clip-runs", runId, requestId),
    path.join(root, "full-clip", runId),
    path.join(root, "full-clip", runId, requestId, "nested"),
    path.join(root, "..", "full-clip", runId, requestId),
    "relative/path",
    ""])
    assert.equal(resolveEvidenceDirectoryPath(bad, root), null, bad);
});

test("실제 경로와 실행 경로가 다르면 증거 폴더를 거부한다", async (t) => {
  const temp = await mkdtemp(path.join(os.tmpdir(), "contact-events-"));
  t.after(() => rm(temp, { recursive: true, force: true }));
  const root = path.join(temp, "boogle");
  const runId = "11111111-2222-3333-4444-555555555555";
  const requestId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
  const directory = path.join(root, "full-clip-f14", runId, requestId);
  await mkdir(directory, { recursive: true });
  // 실제 폴더는 F14 패밀리를 그대로 허용함.
  const resolved = await resolveEvidenceDirectory(directory, root);
  assert.equal(resolved.family, "full-clip-f14");
  // 심볼릭 링크·연결점으로 만든 겉보기 경로는 실제 경로와 달라 거부함.
  const linkDirectory = path.join(root, "full-clip", runId, requestId);
  try {
    await mkdir(path.dirname(linkDirectory), { recursive: true });
    await symlink(directory, linkDirectory, "junction");
  } catch (error) {
    t.skip(`링크를 만들 수 없는 환경: ${error.message}`);
    return;
  }
  await assert.rejects(() => resolveEvidenceDirectory(linkDirectory, root),
    /실제 경로가 실행 경로와 다름/);
});
