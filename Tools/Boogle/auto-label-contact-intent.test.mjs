import assert from "node:assert/strict";
import test from "node:test";
import { autoLabelContactIntent, buildFilteredTemplate,
  contradictsBands } from "./auto-label-contact-intent.mjs";

const columns = ("frame,time_s,time_error_ms,side,grounding_status,has_ground," +
  "support_role,rear_weight,front_weight,rear_signed_mm,front_signed_mm," +
  "minimum_signed_mm,rear_anchor_x_m,rear_anchor_y_m,rear_anchor_z_m," +
  "front_anchor_x_m,front_anchor_y_m,front_anchor_z_m,rear_point_x_m," +
  "rear_point_y_m,rear_point_z_m,front_point_x_m,front_point_y_m,front_point_z_m," +
  "rear_vertex,front_vertex,rear_step_mm,front_step_mm,foot_rotation_step_deg," +
  "foot_x_m,foot_y_m,foot_z_m,foot_pitch_deg,foot_yaw_deg,foot_roll_deg," +
  "toes_y_m,knee_y_m,hips_y_m,root_y_m,source_foot_y_m,source_toes_y_m," +
  "source_foot_speed_mps,source_toes_speed_mps,retarget_foot_y_m," +
  "retarget_toes_y_m,retarget_foot_speed_mps,retarget_toes_speed_mps," +
  "grounding_target_error_mm,grounding_sole_clearance_mm," +
  "grounding_contact_error_mm,grounding_supported_contacts").split(",");

const state = { status: "metrics_complete_review_required", last_frame: 11,
  row_count: 24, clip_frame_rate: 60, source_human_scale: 1, input: "m.fbx" };

// 3개 높이·속도 밴드: 0-3 low(지지), 4-7 mid(애매 — Jev 대상), 8-11 high(공중)
function metricsCsv() {
  const lines = [columns.join(",")];
  for (let frame = 0; frame < 12; frame++) {
    for (const side of ["left", "right"]) {
      const v = Object.fromEntries(columns.map(f => [f, "0"]));
      const band = frame < 4 ? 0 : frame < 8 ? 1 : 2;
      const heights = [0.005, 0.03, 0.15], speeds = [0.01, 0.15, 0.6];
      Object.assign(v, { frame, side, time_s: frame / 60,
        grounding_status: "Applied",
        has_ground: band === 2 ? "False" : "True",
        support_role: band === 2 ? "released" : "both",
        source_foot_y_m: heights[band], source_toes_y_m: heights[band] * 0.8,
        source_foot_speed_mps: frame === 0 ? "" : speeds[band],
        source_toes_speed_mps: frame === 0 ? "" : speeds[band],
        retarget_foot_speed_mps: "0.01", retarget_toes_speed_mps: "0.01",
        rear_weight: "0.5", front_weight: "0.5", minimum_signed_mm: "2",
        foot_pitch_deg: "10" });
      lines.push(columns.map(f => v[f]).join(","));
    }
  }
  return lines.join("\n") + "\n";
}
const templateCsv = "from_frame,to_frame,side,contact_label,motion_label,reviewer,notes\n" +
  "0,3,left,,,,\n4,7,left,,,,\n8,11,left,,,,\n4,7,right,,,,\n";
const emptyLabels = '"from_frame","to_frame","side","contact_label","motion_label","reviewer","notes"\n';

function mockFetch(confidence = 0.85, contact = "full_foot") {
  return async () => ({ ok: true, json: async () => ({ model: "jev-test",
    answers: {
      contact_form: { type: "choice", choice: contact, confidence,
        probabilities: { airborne: 0.05, toe: 0.05, heel: 0.05,
          full_foot: 0.8, uncertain: 0.05 } },
      motion_intent: { type: "choice", choice: "fixed", confidence,
        probabilities: { fixed: 0.8, rolling: 0.05,
          intentional_movement: 0.1, uncertain: 0.05 } },
    } }) });
}

test("고신뢰 Jev 판정은 human-labels 행으로 승격", async () => {
  const result = await autoLabelContactIntent(state, metricsCsv(), templateCsv,
    emptyLabels, { apiKey: "k", fetchImpl: mockFetch() });
  assert.ok(result.promoted >= 1);
  assert.ok(result.csvLines.every(l => l.includes('"jev-auto"')));
  assert.ok(result.csvLines.every(l => l.includes('"발 전체"') &&
    l.includes('"고정"')));
});

test("confidence 미달은 라벨 미기록 + 보류", async () => {
  const result = await autoLabelContactIntent(state, metricsCsv(), templateCsv,
    emptyLabels, { apiKey: "k", fetchImpl: mockFetch(0.4) });
  assert.equal(result.promoted, 0);
  assert.equal(result.csvLines.length, 0);
  assert.ok(result.deferred >= 1);
});

test("사람 라벨 구간은 Jev 호출 자체에서 제외", async () => {
  const labels = emptyLabels + '"4","7","left","발 전체","고정","user","기존"\n';
  const filtered = buildFilteredTemplate(templateCsv,
    [{ side: "left", from_frame: 4, to_frame: 7, contact_label: "발 전체" }]);
  assert.equal(filtered.kept.length, 3);
  const result = await autoLabelContactIntent(state, metricsCsv(), templateCsv,
    labels, { apiKey: "k", fetchImpl: mockFetch() });
  assert.ok(result.csvLines.every(l => !l.startsWith('"4","7","left"')));
});

test("밴드 모순 규칙", () => {
  assert.ok(contradictsBands("airborne", { source_foot_y_m: "low" }));
  assert.ok(contradictsBands("full_foot", { source_foot_y_m: "high" }));
  assert.ok(!contradictsBands("full_foot", { source_foot_y_m: "low" }));
  assert.ok(!contradictsBands("uncertain", { source_foot_y_m: "low" }));
});

test("apiKey 없으면 dry-run — 승격 0, 보고만 생성", async () => {
  const result = await autoLabelContactIntent(state, metricsCsv(), templateCsv,
    emptyLabels, { apiKey: "" });
  assert.equal(result.promoted, 0);
  assert.equal(result.csvLines.length, 0);
});
