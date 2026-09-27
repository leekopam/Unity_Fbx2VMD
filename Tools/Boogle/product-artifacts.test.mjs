import assert from "node:assert/strict";
import { mkdtemp, mkdir, readFile, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { collectProductArtifacts } from "./product-artifacts.mjs";

test("제품 실행의 매니페스트와 요청별 CSV·PNG를 SDK artifact로 제출한다", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "fbx2vmd-artifact-"));
  const evidenceRoot = path.join(root, "evidence");
  const temporaryPath = path.join(root, "sdk-run");
  const runId = "11111111-1111-4111-8111-111111111111";
  const requestId = "22222222-2222-4222-8222-222222222222";
  try {
    await mkdir(path.join(evidenceRoot, "full-clip-runs", runId), { recursive: true });
    await mkdir(path.join(evidenceRoot, "full-clip", runId, requestId), { recursive: true });
    await mkdir(path.join(evidenceRoot, "playback", requestId), { recursive: true });
    await mkdir(temporaryPath);
    await writeFile(path.join(evidenceRoot, "full-clip-runs", runId, "manifest.json"),
      JSON.stringify({ runId, requestId }));
    await writeFile(path.join(evidenceRoot, "full-clip", runId, requestId, "all-frames.csv"),
      "frame,value\n1,2\n");
    await writeFile(path.join(evidenceRoot, "playback", requestId, "frame.png"), "png");
    await writeFile(path.join(evidenceRoot, "playback", requestId, "duplicate.csv"),
      "frame,value\n1,2\n");
    await writeFile(path.join(evidenceRoot, "playback", requestId, "prior-output.vmd"), "private");
    const artifacts = await collectProductArtifacts(evidenceRoot, temporaryPath, runId,
      async (submissionRoot) => {
        const manifest = JSON.parse(await readFile(path.join(submissionRoot, "artifacts.json"), "utf8"));
        const submitted = await Promise.all(manifest.artifacts.map(async (item) => ({
          kind: item.kind,
          content: await readFile(path.join(submissionRoot, "artifacts", item.fileName), "utf8")
        })));
        return submitted;
      });
    assert.equal(artifacts.length, 4);
    assert.deepEqual(artifacts.map((item) => item.kind).sort(),
      ["capture", "output", "output", "output"]);
    assert(artifacts.some((item) => item.content === "png"));
    assert(artifacts.some((item) => item.content === "frame,value\n1,2\n"));
    const sourceMap = artifacts.find((item) => item.content.includes("full-clip-runs/"));
    assert.match(JSON.parse(sourceMap.content).sources.find((item) =>
      item.source.endsWith("manifest.json")).sdkPath,
      /^artifacts\/[0-9a-f]{64}\.json$/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
