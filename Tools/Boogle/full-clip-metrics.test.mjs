import assert from "node:assert/strict";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { buildFullClipMetrics } from "./full-clip-metrics.mjs";
import { runWithAdapter } from "../../Assets/_Project/Tools/MainRecordingSettings/node_modules/boogle-sdk/dist/runner.js";
import { compareRuns } from "../../Assets/_Project/Tools/MainRecordingSettings/node_modules/boogle-sdk/dist/metric.js";

const state = {
  maximum_time_error_ms: 0.007,
  maximum_penetration_mm: 54.19,
  maximum_sole_step_mm: 158.32,
  maximum_foot_rotation_step_degrees: 52.9,
  maximum_hips_step_mm: 91.45
};

test("F10 최대값의 단위를 보존하고 누락·잘못된 수치를 거절한다", () => {
  const metrics = buildFullClipMetrics(state);
  assert.deepEqual(metrics.map(({ value, unit }) => [value, unit]),
    [[0.007, "ms"], [54.19, "mm"], [158.32, "mm"], [52.9, "deg"], [91.45, "mm"]]);
  assert.equal(new Set(metrics.map(metric => metric.name)).size, 5);
  for (const key of Object.keys(state)) {
    for (const invalid of [undefined, null, "0", NaN, Infinity, -1])
      assert.throws(() => buildFullClipMetrics({ ...state, [key]: invalid }), /전체 클립/);
    assert.equal(buildFullClipMetrics({ ...state, [key]: 0 }).length, 5);
  }
});

test("SDK 봉인 후 수동 검토 판정을 유지하고 진단 경로 비교를 거절한다", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "fbx2vmd-full-clip-metrics-"));
  const config = {
    protocolVersion: "0.1.0",
    projectId: "11111111-1111-4111-8111-111111111111",
    adapter: { id: "fbx2vmd-smoke", version: "0.1.1" },
    testPack: { id: "fbx2vmd-product-smoke", version: "0.1.3" },
    inputConditions: { captureMode: "full-clip", lowerBodyOnly: "false" },
    environmentConditions: { unityVersion: "2022.3.62f3" }
  };
  const outcome = { status: "MANUAL_REVIEW_REQUIRED", metrics: buildFullClipMetrics(state) };
  try {
    const first = await runWithAdapter(root, config, async () => outcome);
    const bundle = JSON.parse(await readFile(path.join(first.sealedPath, "metrics.json"), "utf8"));
    assert.deepEqual(bundle.metrics, outcome.metrics);
    assert.equal(first.status, "MANUAL_REVIEW_REQUIRED");
    const same = await runWithAdapter(root, config, async () => outcome);
    const comparison = await compareRuns(root, config.projectId, first.runId, same.runId,
      outcome.metrics[0].name);
    assert.equal(comparison.delta, 0);
    assert.equal(comparison.currentStatus, "MANUAL_REVIEW_REQUIRED");
    assert.equal(comparison.status, "FAIL");
    for (const inputConditions of [
      { ...config.inputConditions, lowerBodyOnly: "true" },
      { ...config.inputConditions, captureMode: "grounding-case" }
    ]) {
      const diagnostic = await runWithAdapter(root, { ...config, inputConditions }, async () => outcome);
      assert.deepEqual(await compareRuns(root, config.projectId, first.runId, diagnostic.runId,
        outcome.metrics[0].name), { status: "NOT_COMPARABLE", reason: "conditions_mismatch" });
    }
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
