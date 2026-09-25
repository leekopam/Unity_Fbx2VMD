import assert from "node:assert/strict";
import { test } from "node:test";
import { reviewContactIntent } from "./review-contact-intent.mjs";

test("Jev는 원본의 애매한 구간만 예비 판정하고 불일치 때만 조사 순서를 묻는다", async () => {
  const header = ["frame", "side", "support_role", "source_foot_y_m",
    "source_toes_y_m", "source_foot_speed_mps", "source_toes_speed_mps",
    "retarget_foot_y_m", "retarget_toes_y_m", "retarget_foot_speed_mps",
    "retarget_toes_speed_mps", "rear_weight", "front_weight",
    "minimum_signed_mm", "foot_pitch_deg"];
  const rows = [header.join(",")];
  for (let frame = 0; frame < 6; frame++) {
    for (const side of ["left", "right"]) {
      const left = side === "left";
      const height = left ? [0, 0, .5, .5, 1, 1][frame] :
        [0, 0, 0, 1, 1, 1][frame];
      const speed = left ? ["", .01, .9, .9, 1, 1][frame] :
        ["", .01, .01, .01, 1, 1][frame];
      const values = { frame, side, support_role: left ? "both" : "released",
        source_foot_y_m: height, source_toes_y_m: height,
        source_foot_speed_mps: speed, source_toes_speed_mps: speed,
        retarget_foot_y_m: height, retarget_toes_y_m: height,
        retarget_foot_speed_mps: speed, retarget_toes_speed_mps: speed,
        rear_weight: left ? .8 : 0, front_weight: left ? .8 : 0,
        minimum_signed_mm: 0, foot_pitch_deg: 0 };
      rows.push(header.map(field => values[field]).join(","));
    }
  }
  const state = { status: "metrics_complete_review_required", input: "test.fbx",
    row_count: 12, last_frame: 5 };
  const template = "from_frame,to_frame,side,contact_label,motion_label,reviewer,notes\n" +
    "2,3,left,,,,\n1,2,right,,,,\n";
  const withoutKey = await reviewContactIntent(state, `${rows.join("\n")}\n`, template);
  assert.equal(withoutKey.status, "SKIPPED_NO_API_KEY");
  assert.equal(withoutKey.ambiguous_intervals, 1);
  assert.equal(withoutKey.intervals[0].pending_request.questions.contact_form.type, "choice");

  const requests = [];
  const answer = (choice, options) => ({ type: "choice", choice, confidence: .7,
    probabilities: Object.fromEntries(options.map(option =>
      [option, option === choice ? 1 : 0])) });
  const fetchImpl = async (url, options) => {
    assert.equal(url, "https://api.typesafe.ai/v1/systemone");
    assert.equal(options.headers.Authorization, "Bearer test-key");
    requests.push(JSON.parse(options.body));
    const answers = requests.length === 1 ? {
      contact_form: answer("toe", ["airborne", "toe", "heel", "full_foot", "uncertain"]),
      motion_intent: answer("rolling", ["fixed", "rolling", "intentional_movement", "uncertain"])
    } : { investigation: answer("foot_correction",
      ["source_label", "retargeting", "foot_correction", "manual_review"]) };
    return { ok: true, json: async () => ({ model: "jev-1.13.0", answers,
      usage: { input_tokens: 10, output_tokens: 5 } }) };
  };
  const withKey = await reviewContactIntent(state, `${rows.join("\n")}\n`, template,
    { apiKey: "test-key", fetchImpl });
  assert.equal(withKey.status, "MANUAL_REVIEW_REQUIRED");
  assert.equal(requests.length, 2);
  assert.equal(requests[0].state.source_stage.includes("Original FBX"), true);
  assert.equal(Object.hasOwn(requests[0].state, "retarget_and_final_median"), false);
  assert.equal(withKey.intervals[0].investigation.priority.label, "접지 보정");
  assert.equal(JSON.stringify(withKey).includes("test-key"), false);

  let matchingCalls = 0;
  const matching = await reviewContactIntent(state, `${rows.join("\n")}\n`, template,
    { apiKey: "test-key", fetchImpl: async () => {
      matchingCalls++;
      return { ok: true, json: async () => ({ model: "jev-1.13.0", answers: {
        contact_form: answer("full_foot", ["airborne", "toe", "heel", "full_foot", "uncertain"]),
        motion_intent: answer("fixed", ["fixed", "rolling", "intentional_movement", "uncertain"])
      } }) };
    } });
  assert.equal(matching.status, "MANUAL_REVIEW_REQUIRED");
  assert.equal(matchingCalls, 1);
});
