import assert from "node:assert/strict";
import { mkdtemp, mkdir, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { compareManualCapture } from "./manual-compare.mjs";

test("F13은 같은 FBX 프레임의 측정값과 정면 캡처만 비교한다", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "fbx2vmd-f13-"));
  const directory = path.join(root, "Docs/Workflow/Local/f13");
  const png = Buffer.concat([Buffer.from("89504e470d0a1a0a", "hex"),
    Buffer.alloc(4), Buffer.from("49454e44ae426082", "hex")]);
  try {
    await mkdir(directory, { recursive: true });
    for (const role of ["manual", "auto"]) {
      await writeFile(path.join(directory, `${role}.csv`),
        `recorderFrame,animationClipName,animationClipTime,leftFootX\n30,motion,1,${role === "manual" ? 1 : 1.25}\n`);
      await writeFile(path.join(directory, `${role}.png`), png);
      await writeFile(path.join(directory, `${role}-index.csv`),
        `recorderFrame,view,path\n30,front,Docs/Workflow/Local/f13/${role}.png\n`);
    }
    const result = (role, jobMode) => ({ jobMode, success: true, frameCount: 60,
      comparisonMetricsCsvPath: `Docs/Workflow/Local/f13/${role}.csv`,
      comparisonFrameIndexPath: `Docs/Workflow/Local/f13/${role}-index.csv` });
    const summary = { results: [result("manual", "SubManualYyb"),
      result("auto", "MainAuto")] };
    const paired = await compareManualCapture(summary, root);
    assert.equal(paired.status, "MANUAL_REVIEW_REQUIRED");
    assert.equal(paired.frames[0].metrics.leftFootX.delta, 0.25);
    await writeFile(path.join(directory, "auto.csv"),
      "recorderFrame,animationClipName,animationClipTime,leftFootX\n30,motion,2,1.25\n");
    assert.equal((await compareManualCapture(summary, root)).status, "NOT_COMPARABLE");
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
