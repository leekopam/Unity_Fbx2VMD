import assert from "node:assert/strict";
import { test } from "node:test";
import { diagnoseRun } from "./diagnose-run.mjs";

function makeRecord() {
  return { runId: "test-run", label: "editmode-selftest", outcome: "Success",
    totalMs: 4200, stages: [
      { stage: "LoadingFbx", deltaMs: 600, message: "FBX 로드 중" },
      { stage: "AvatarReady", deltaMs: 2600, message: "Humanoid Avatar 준비 완료" },
      { stage: "Ready", deltaMs: 1000, message: "준비 완료" }],
    analysis: [
      { kind: "Bottleneck", severity: "Warning", stage: "AvatarReady",
        detail: "병목 후보: AvatarReady 2600ms",
        suggestedScopes: ["HumanoidAvatarBuilder.SetupHumanoid",
          "RuntimeHumanoidReferencePoseApplier.TryApply",
          "FBXImportController.LoadBoneMappingRuntime"] }] };
}

test("키가 없으면 pending_request만 남기고 스킵한다", async () => {
  const result = await diagnoseRun(makeRecord());
  assert.equal(result.status, "SKIPPED_NO_API_KEY");
  assert.equal(result.entries.length, 1);
  assert.equal(result.entries[0].pending_request.questions.cause_class.type, "choice");
  assert.equal(result.entries[0].pending_request.questions.deepen_target.type, "choice");
});

test("findings가 없으면 BLOCKED", async () => {
  const record = makeRecord();
  record.analysis = [];
  const result = await diagnoseRun(record, { apiKey: "k" });
  assert.equal(result.status, "BLOCKED");
});

test("Jev가 원인 분류와 심화 계측 대상을 선택한다", async () => {
  const requests = [];
  const answer = (choice, options) => ({ type: "choice", choice, confidence: .8,
    probabilities: Object.fromEntries(options.map(o => [o, o === choice ? 1 : 0])) });
  const fetchImpl = async (url, options) => {
    assert.equal(url, "https://api.typesafe.ai/v1/systemone");
    assert.equal(options.headers.Authorization, "Bearer test-key");
    requests.push(JSON.parse(options.body));
    return { ok: true, json: async () => ({ model: "jev-1.13.0", answers: {
      cause_class: answer("engine_pipeline",
        ["io_load", "compute_cpu", "engine_pipeline", "memory_gc", "uncertain"]),
      deepen_target: answer("HumanoidAvatarBuilder.SetupHumanoid",
        ["HumanoidAvatarBuilder.SetupHumanoid",
          "RuntimeHumanoidReferencePoseApplier.TryApply",
          "FBXImportController.LoadBoneMappingRuntime", "uncertain"])
    }, usage: { input_tokens: 10, output_tokens: 5 } }) };
  };
  const result = await diagnoseRun(makeRecord(), { apiKey: "test-key", fetchImpl });
  assert.equal(result.status, "DIAGNOSIS_COMPLETE");
  assert.equal(requests.length, 1);
  assert.equal(result.entries[0].cause.choice, "engine_pipeline");
  assert.equal(result.entries[0].cause.label, "Unity 엔진 파이프라인(임포트·아바타·애니메이션)");
  assert.equal(result.entries[0].deepen.choice, "HumanoidAvatarBuilder.SetupHumanoid");
  assert.equal(JSON.stringify(result).includes("test-key"), false);
});
