import assert from "node:assert/strict";
import test from "node:test";
import { buildAnomalyState, reviewPoseAnomalies }
  from "./review-pose-anomalies.mjs";

const header = "frame,side,knee_flexion_deg,ankle_pitch_deg,pelvis_tilt_deg," +
  "pose_novelty,transition_novelty,joint_delta_deg,minimum_signed_mm," +
  "foot_rotation_step_deg";
function csv() {
  const lines = [header];
  for (let frame = 0; frame < 20; frame++) {
    for (const side of ["left", "right"]) {
      const bad = side === "left" && frame >= 5 && frame <= 12;
      lines.push([frame, side, bad ? -15 : 40, bad ? -180 : -135, 2,
        bad ? 0.9 : 0.05, bad ? 4 : 0.5, bad ? 30 : 1, 2, 1].join(","));
    }
  }
  return lines.join("\n") + "\n";
}
const anomalies = { anomalies: [
  { defect_type: "joint_limit_knee", side: "left",
    start_frame: 5, end_frame: 12, severity: "high",
    evidence: { min_deg: -4 } },
] };

const mockFetch = (choice = "broken_pose", confidence = 0.9) =>
  async () => ({ ok: true, json: async () => ({ model: "jev-test",
    answers: { pose_verdict: { type: "choice", choice, confidence,
      probabilities: { broken_pose: 0.9, acceptable_correction: 0.05,
        cannot_judge: 0.05 } } } }) });

test("의심 구간 상태는 의미 단위로만 구성됨", () => {
  const state = buildAnomalyState(anomalies.anomalies[0], readRows(csv()));
  assert.equal(state.defect_type, "joint_limit_knee");
  assert.equal(state.duration_frames, 8);
  assert.equal(state.metric_bands.knee_flexion_deg, "low");
  assert.equal(state.metric_bands.pose_novelty, "high");
  assert.ok(state.metric_medians.knee_flexion_deg < 0);
});

function readRows(text) {
  const [head, ...rest] = text.trimEnd().split("\n");
  const fields = head.split(",");
  return rest.map(line => Object.fromEntries(
    line.split(",").map((v, i) => [fields[i], v])));
}

test("파손 판정은 리뷰 큐로, 허용 판정은 큐에서 제외", async () => {
  const broken = await reviewPoseAnomalies(anomalies, csv(),
    { apiKey: "k", fetchImpl: mockFetch("broken_pose") });
  assert.equal(broken.review_queue.length, 1);
  assert.equal(broken.verdicts[0].verdict.label, "파손된 자세");
  const ok = await reviewPoseAnomalies(anomalies, csv(),
    { apiKey: "k", fetchImpl: mockFetch("acceptable_correction", 0.95) });
  assert.equal(ok.review_queue.length, 0);
});

test("confidence 미달의 허용 판정도 리뷰 큐행", async () => {
  const result = await reviewPoseAnomalies(anomalies, csv(),
    { apiKey: "k", fetchImpl: mockFetch("acceptable_correction", 0.5) });
  assert.equal(result.review_queue.length, 1);
});

test("apiKey 없으면 dry-run — 요청 초안만", async () => {
  const result = await reviewPoseAnomalies(anomalies, csv(), { apiKey: "" });
  assert.equal(result.status, "SKIPPED_NO_API_KEY");
  assert.ok(result.verdicts[0].pending_request.questions.pose_verdict);
});
