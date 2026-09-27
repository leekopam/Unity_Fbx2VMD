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

test("F15 녹화 MP4를 같은 실행의 SDK artifact로 제출한다", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "fbx2vmd-video-artifact-"));
  const evidenceRoot = path.join(root, "evidence");
  const recordingsRoot = path.join(root, "Recordings");
  const temporaryPath = path.join(root, "sdk-run");
  const runId = "33333333-3333-4333-8333-333333333333";
  const requestId = "44444444-4444-4444-8444-444444444444";
  const videoPath = path.join(recordingsRoot, "Snake Hip Hop Dance_20260927_130000.mp4");
  try {
    await mkdir(path.join(evidenceRoot, "product-ui-runs", runId), { recursive: true });
    await mkdir(path.join(evidenceRoot, "product-ui", runId, requestId), { recursive: true });
    await mkdir(recordingsRoot);
    await mkdir(temporaryPath);
    await writeFile(path.join(evidenceRoot, "product-ui-runs", runId, "manifest.json"),
      JSON.stringify({ runId, requestId }));
    await writeFile(path.join(evidenceRoot, "product-ui", runId, requestId, "state.json"),
      JSON.stringify({ status: "manual_review_required",
        video_result_message: `FBX 모션 영상 저장 완료: ${videoPath}` }));
    await writeFile(videoPath, "mp4-data");
    const artifacts = await collectProductArtifacts(evidenceRoot, temporaryPath, runId,
      async (submissionRoot) => {
        const manifest = JSON.parse(await readFile(path.join(submissionRoot, "artifacts.json"), "utf8"));
        return Promise.all(manifest.artifacts.map(async (item) => ({ kind: item.kind,
          content: await readFile(path.join(submissionRoot, "artifacts", item.fileName), "utf8") })));
      }, recordingsRoot);
    assert(artifacts.some((item) => item.kind === "video" && item.content === "mp4-data"));
    assert(artifacts.some((item) => item.content.includes("Recordings/Snake Hip Hop Dance")));
    const outside = path.join(root, "outside.mp4");
    await writeFile(outside, "other-video");
    await writeFile(path.join(evidenceRoot, "product-ui", runId, requestId, "state.json"),
      JSON.stringify({ status: "manual_review_required",
        video_result_message: `FBX 모션 영상 저장 완료: ${outside}` }));
    await assert.rejects(collectProductArtifacts(evidenceRoot, temporaryPath, runId,
      async () => [], recordingsRoot), /Recordings 밖/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
