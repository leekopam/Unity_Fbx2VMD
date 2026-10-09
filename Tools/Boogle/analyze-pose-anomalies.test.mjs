import assert from "node:assert/strict";
import test from "node:test";
import { analyzePoseAnomalies } from "./analyze-pose-anomalies.mjs";

const baseColumns = ("frame,time_s,time_error_ms,side,grounding_status,has_ground," +
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
  "grounding_contact_error_mm,grounding_supported_contacts," +
  "knee_flexion_deg,ankle_pitch_deg,pelvis_tilt_deg,com_x_m,com_z_m," +
  "support_polygon_contains_com,joint_delta_deg,pose_novelty,transition_novelty")
  .split(",");

// 정상 기저: 접지 중 발 고정, 무릎 40도, 발목 0도, CoM 지지 내부.
function createCsv(overrides = {}) {
  const lines = [baseColumns.join(",")];
  for (let frame = 0; frame < 40; frame++) {
    for (const side of ["left", "right"]) {
      const values = Object.fromEntries(baseColumns.map(f => [f, "0"]));
      const grounded = side === "left" ? frame < 30 : frame < 20;
      Object.assign(values, {
        frame, side, time_s: frame / 60,
        grounding_status: "Applied", has_ground: grounded ? "True" : "False",
        support_role: grounded ? "both" : "released",
        minimum_signed_mm: grounded ? "2" : "60",
        foot_x_m: "0.1", foot_z_m: "0.2", foot_y_m: grounded ? "0.01" : "0.2",
        foot_rotation_step_deg: frame === 0 ? "0" : "1.5",
        hips_y_m: "0.9", knee_flexion_deg: "40", ankle_pitch_deg: "-135",
        pelvis_tilt_deg: "2", com_x_m: "0.11", com_z_m: "0.2",
        support_polygon_contains_com: grounded ? "True" : "False",
      });
      (overrides[frame] || (() => {}))(values, side);
      lines.push(baseColumns.map(f => values[f]).join(","));
    }
  }
  return `${lines.join("\n")}\n`;
}

test("정상 클립은 CLEAN", () => {
  const result = analyzePoseAnomalies(createCsv(), { sourceHumanScale: 1 });
  assert.equal(result.status, "CLEAN");
  assert.equal(result.anomaly_count, 0);
});

test("접지 구간 발 드리프트는 residual_slide로 잡힘", () => {
  const csv = createCsv({
    10: v => { if (v.side === "left") v.foot_x_m = "0.16"; },
    11: v => { if (v.side === "left") v.foot_x_m = "0.22"; },
  });
  const result = analyzePoseAnomalies(csv, { sourceHumanScale: 1 });
  const hit = result.anomalies.filter(a => a.defect_type === "residual_slide");
  assert.ok(hit.some(a => a.side === "left" && a.start_frame <= 11));
});

test("무릎 역신전·발목 범위 초과는 joint_limit으로 잡힘", () => {
  const csv = createCsv({
    15: v => { v.knee_flexion_deg = "-12"; v.ankle_pitch_deg = "-180"; },
    16: v => { v.knee_flexion_deg = "-12"; v.ankle_pitch_deg = "-180"; },
  });
  const result = analyzePoseAnomalies(csv, { sourceHumanScale: 1 });
  assert.ok(result.anomalies.some(a => a.defect_type === "joint_limit_knee"));
  assert.ok(result.anomalies.some(a => a.defect_type === "joint_limit_ankle"));
});

test("발 회전 스파이크와 골반 팝은 jitter로 잡힘", () => {
  const csv = createCsv({
    20: v => { v.foot_rotation_step_deg = "80"; v.hips_y_m = "1.05"; },
  });
  const result = analyzePoseAnomalies(csv, { sourceHumanScale: 1 });
  assert.ok(result.anomalies.some(a => a.defect_type === "jitter_foot_rotation"));
  assert.ok(result.anomalies.some(a => a.defect_type === "jitter_hips"));
});

test("침투와 균형 이탈은 각각 penetration·balance_escape", () => {
  const csv = createCsv({
    5: v => { v.minimum_signed_mm = "-30"; },
    6: v => { v.support_polygon_contains_com = "False"; },
  });
  const result = analyzePoseAnomalies(csv, { sourceHumanScale: 1 });
  assert.ok(result.anomalies.some(a => a.defect_type === "penetration" &&
    a.side === "left"));
  assert.ok(result.anomalies.some(a => a.defect_type === "balance_escape"));
});

test("핀 전환 경계의 골반 스텝은 pin_boundary_pop", () => {
  const csv = createCsv({
    20: v => { if (v.side === "left") v.hips_y_m = "1.0"; },
  });
  const result = analyzePoseAnomalies(csv, { sourceHumanScale: 1 });
  assert.ok(result.anomalies.some(a => a.defect_type === "pin_boundary_pop"));
});

test("신규 열이 없는 구형 CSV는 계측만 건너뛰고 차단되지 않음", () => {
  const drop = new Set(["knee_flexion_deg", "ankle_pitch_deg", "pelvis_tilt_deg",
    "com_x_m", "com_z_m", "support_polygon_contains_com", "joint_delta_deg",
    "pose_novelty", "transition_novelty"]);
  const keep = baseColumns.filter(c => !drop.has(c));
  const full = createCsv();
  const lines = full.trimEnd().split("\n");
  const legacy = lines.map(line =>
    line.split(",").filter((_, i) => !drop.has(baseColumns[i])).join(","))
    .join("\n");
  const result = analyzePoseAnomalies(legacy, { sourceHumanScale: 1 });
  assert.notEqual(result.status, "BLOCKED");
  assert.ok(result.skipped.includes("joint_limit"));
  assert.ok(result.skipped.includes("balance_escape"));
  assert.ok(result.skipped.includes("pose_novelty_spike"));
});

test("빈 CSV는 BLOCKED", () => {
  assert.equal(analyzePoseAnomalies("").status, "BLOCKED");
});
