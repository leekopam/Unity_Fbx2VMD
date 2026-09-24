import { execFileSync } from "node:child_process";
import { createHash, randomUUID } from "node:crypto";
import { createReadStream } from "node:fs";
import { access, copyFile, mkdir, open, readFile, rm, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const evidenceRoot = path.join(projectRoot, "Docs/Workflow/Local/evidence/boogle");
const runtimeRoot = path.join(projectRoot, "Docs/Workflow/Local/runtime");
const requestPath = path.join(runtimeRoot, "fbx_smoke_request.json");
const statusPath = path.join(runtimeRoot, "fbx_smoke_status.json");
const tracePath = path.join(runtimeRoot, "fbx_smoke_trace.log");
const fbxPath = path.join(projectRoot, "Assets/Resources/Import_FBX/satisfaction_2.fbx");
const outputPath = path.join(projectRoot, "Assets/VMDRecorderSample/satisfaction_2.vmd");
const sdkRunnerPath = path.join(
  projectRoot,
  "Assets/_Project/Tools/MainRecordingSettings/node_modules/boogle-sdk/dist/runner.js"
);
const projectId = "f2f44dc8-83ef-46d0-9d26-b0e52d1c4d20";
const command = "capture_satisfaction_quick_vmd_smoke_2s";
const preselectionCommand = "capture_preselection_state";
const playbackCommand = "capture_playback_seek_evidence";
const invalidInputCommand = "capture_invalid_input_evidence";

async function readOptional(pathToRead) {
  try {
    return await readFile(pathToRead);
  } catch (error) {
    if (error.code === "ENOENT") return null;
    throw error;
  }
}

async function readStatus() {
  const content = await readOptional(statusPath);
  if (!content) return null;
  try {
    return JSON.parse(content.toString("utf8"));
  } catch (error) {
    if (error instanceof SyntaxError) return null;
    throw error;
  }
}

async function hashFile(filePath) {
  const digest = createHash("sha256");
  try {
    for await (const chunk of createReadStream(filePath)) digest.update(chunk);
  } catch (error) {
    if (error.code === "ENOENT") return "missing";
    throw error;
  }
  return digest.digest("hex");
}

async function hasCompletePngWithin(filePath, directory) {
  const resolved = path.resolve(filePath || "");
  const relative = path.relative(directory, resolved);
  if (relative.startsWith("..") || path.isAbsolute(relative)) return false;
  const file = await open(resolved, "r");
  try {
    const { size } = await file.stat();
    if (size < 20) return false;
    const head = Buffer.alloc(8);
    const tail = Buffer.alloc(8);
    await file.read(head, 0, 8, 0);
    await file.read(tail, 0, 8, size - 8);
    return head.equals(Buffer.from("89504e470d0a1a0a", "hex")) &&
      tail.equals(Buffer.from("49454e44ae426082", "hex"));
  } finally {
    await file.close();
  }
}

function inspectShortVmd(bytes) {
  if (bytes.length < 58 ||
      bytes.subarray(0, 30).toString("ascii").replace(/\0+$/, "") !==
        "Vocaloid Motion Data 0002") return null;
  const boneRecords = bytes.readUInt32LE(50);
  const morphOffset = 54 + boneRecords * 111;
  if (boneRecords < 60 || morphOffset + 4 > bytes.length) return null;
  const frames = new Set();
  const bones = new Set();
  for (let index = 0; index < boneRecords; index++) {
    const offset = 54 + index * 111;
    const frame = bytes.readUInt32LE(offset + 15);
    if (frame >= 60) return null;
    const key = `${bytes.subarray(offset, offset + 15).toString("hex")}:${frame}`;
    if (bones.has(key)) return null;
    bones.add(key);
    frames.add(frame);
  }
  const morphRecords = bytes.readUInt32LE(morphOffset);
  if (morphOffset + 4 + morphRecords * 23 > bytes.length) return null;
  return { boneRecords, morphRecords, uniqueFrames: frames.size,
    bytesPerFrame: bytes.length / 60 };
}

async function executeSmoke(runId) {
  const sessionRoot = path.join(evidenceRoot, "product-smoke", runId);
  await mkdir(sessionRoot, { recursive: true });
  const backupPath = path.join(sessionRoot, "prior-output.vmd");
  const backupMetaPath = path.join(sessionRoot, "prior-output.vmd.meta");
  const priorOutput = await readOptional(outputPath);
  const priorMeta = await readOptional(`${outputPath}.meta`);
  const requestId = randomUUID();
  const traceOffset = (await stat(tracePath).catch(() => ({ size: 0 }))).size;
  let outcome = { status: "INFRA_ERROR" };
  let failureStage = "preflight";
  let status = null;
  let requestSubmitted = false;
  let backupCreated = false;
  let stillRunning = false;
  let restored = false;
  let vmdStructure = null;

  try {
    await access(fbxPath);
    await access(path.dirname(outputPath));
    if (await readOptional(requestPath) || (await readStatus())?.status === "running") {
      outcome = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      if (priorOutput) await copyFile(outputPath, backupPath);
      if (priorMeta) await copyFile(`${outputPath}.meta`, backupMetaPath);
      backupCreated = true;
      const request = { request_id: requestId, command, requested_command: command };
      await writeFile(requestPath, JSON.stringify(request), { flag: "wx" });
      requestSubmitted = true;
      const startedAt = Date.now();
      while (Date.now() - startedAt < 600000) {
        await new Promise((resolve) => setTimeout(resolve, 500));
        const candidate = await readStatus();
        if (candidate?.request_id !== requestId) {
          if (Date.now() - startedAt > 30000) break;
          continue;
        }
        status = candidate;
        if (candidate.status === "running") {
          stillRunning = true;
          continue;
        }
        stillRunning = false;
        break;
      }

      if (!status || status.status === "running") {
        outcome = status?.status === "running"
          ? { status: "TIMED_OUT", failureKind: "timeout" }
          : { status: "BLOCKED", failureKind: "preflight" };
      } else if (status.status !== "completed" || status.passed !== true) {
        failureStage = status.failure_stage || "product_path";
        outcome = failureStage === "preflight"
          ? { status: "BLOCKED", failureKind: "preflight" }
          : { status: "FAIL", failureKind: "test_failure" };
      } else {
        failureStage = "output";
        const output = await stat(outputPath).catch(() => null);
        const hasExpectedPath = path.resolve(status.output_path || "").toLowerCase() === outputPath.toLowerCase();
        if (output?.size > 0) vmdStructure = inspectShortVmd(await readFile(outputPath));
        if (status.total_jobs === 1 && status.success_jobs === 1 &&
            status.frame_count === 60 && status.file_size_bytes === output?.size &&
            output?.size > 0 && hasExpectedPath &&
            vmdStructure?.uniqueFrames === 60 && vmdStructure.boneRecords % 60 === 0) {
          failureStage = "";
          outcome = {
            status: "PASS",
            metrics: [{
              name: "artifact/bytes_per_frame",
              value: output.size / status.frame_count,
              unit: "B/frame",
              definitionVersion: "1",
              aggregation: "single",
              direction: "lower",
              threshold: { kind: "relative_percent", value: 25 }
            }]
          };
        } else {
          outcome = { status: "FAIL", failureKind: "test_failure" };
        }
      }
    }
  } catch (error) {
    failureStage = "infrastructure";
    outcome = error.code === "ENOENT" || error.code === "EEXIST"
      ? { status: "BLOCKED", failureKind: "preflight" }
      : { status: "INFRA_ERROR" };
  } finally {
    if (requestSubmitted && !stillRunning) {
      const currentRequest = await readOptional(requestPath);
      if (currentRequest) {
        try {
          if (JSON.parse(currentRequest.toString("utf8")).request_id === requestId) {
            await rm(requestPath);
          }
        } catch (error) {
          if (!(error instanceof SyntaxError)) throw error;
        }
      }
    }
    try {
      if (!backupCreated) {
        restored = true;
      } else if (!stillRunning) {
        if (priorOutput) await copyFile(backupPath, outputPath);
        else await rm(outputPath, { force: true });
        if (priorMeta) await copyFile(backupMetaPath, `${outputPath}.meta`);
        else await rm(`${outputPath}.meta`, { force: true });
        restored = true;
      }
    } catch (error) {
      failureStage = "cleanup";
      outcome = { status: "INFRA_ERROR" };
    }
    const trace = await readOptional(tracePath);
    if (trace && trace.length > traceOffset) {
      await writeFile(path.join(sessionRoot, "unity-trace.log"), trace.subarray(traceOffset));
    }
    await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
      runId, requestId, command, result: outcome.status, failureStage, restored,
      status, outputPath, backupPath: priorOutput ? backupPath : null,
      vmdStructure
    }, null, 2));
  }
  return outcome;
}

async function executePreselection(runId) {
  const sessionRoot = path.join(evidenceRoot, "preselection-runs", runId);
  await mkdir(sessionRoot, { recursive: true });
  const requestId = randomUUID();
  const traceOffset = (await stat(tracePath).catch(() => ({ size: 0 }))).size;
  let status = null;
  let result = { status: "INFRA_ERROR" };
  let failureStage = "preflight";
  let submitted = false;
  let running = false;
  try {
    if (await readOptional(requestPath) || (await readStatus())?.status === "running") {
      result = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      await writeFile(requestPath, JSON.stringify({
        request_id: requestId, command: preselectionCommand, requested_command: preselectionCommand
      }), { flag: "wx" });
      submitted = true;
      const startedAt = Date.now();
      while (Date.now() - startedAt < 30000) {
        await new Promise((resolve) => setTimeout(resolve, 500));
        const candidate = await readStatus();
        if (candidate?.request_id !== requestId) {
          if (Date.now() - startedAt > 10000) break;
          continue;
        }
        status = candidate;
        running = candidate.status === "running";
        if (!running) break;
      }
      if (!status || running) {
        result = { status: running ? "TIMED_OUT" : "BLOCKED",
          failureKind: running ? "timeout" : "preflight" };
      } else if (status.status !== "completed" || status.passed !== true) {
        failureStage = status.failure_stage || "preselection";
        result = { status: failureStage === "preflight" ? "BLOCKED" : "FAIL",
          failureKind: failureStage === "preflight" ? "preflight" : "test_failure" };
      } else {
        failureStage = "evidence";
        const statePath = path.resolve(status.preselection_state_path || "");
        const capturePath = path.resolve(status.capture_path || "");
        const allowedRoot = path.join(evidenceRoot, "preselection") + path.sep;
        if (statePath.startsWith(allowedRoot) && capturePath.startsWith(allowedRoot)) {
          const state = JSON.parse(await readFile(statePath, "utf8"));
          const capture = await readFile(capturePath);
          const completePng = capture.subarray(0, 8).equals(Buffer.from("89504e470d0a1a0a", "hex")) &&
            capture.subarray(-8).equals(Buffer.from("49454e44ae426082", "hex"));
          if (state.structural_passed === true && state.visual_review_required === true &&
              state.scene === "Main_Auto" && state.avatar_valid === true &&
              state.has_prepared_motion === false && state.is_recording === false &&
              state.max_pose_drift_mm <= 0.5 && completePng) {
            failureStage = "";
            result = { status: "MANUAL_REVIEW_REQUIRED" };
          } else {
            result = { status: "FAIL", failureKind: "test_failure" };
          }
        } else {
          result = { status: "INFRA_ERROR" };
        }
      }
    }
  } catch (error) {
    const wasPreflight = failureStage === "preflight";
    failureStage = "infrastructure";
    result = { status: wasPreflight &&
      (error.code === "ENOENT" || error.code === "EEXIST") ? "BLOCKED" : "INFRA_ERROR" };
  } finally {
    if (submitted && !running) {
      const current = await readOptional(requestPath);
      if (current) {
        try {
          if (JSON.parse(current.toString("utf8")).request_id === requestId) {
            await rm(requestPath);
          }
        } catch (error) {
          if (!(error instanceof SyntaxError)) throw error;
        }
      }
    }
    const trace = await readOptional(tracePath);
    if (trace && trace.length > traceOffset) {
      await writeFile(path.join(sessionRoot, "unity-trace.log"), trace.subarray(traceOffset));
    }
    await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
      runId, requestId, command: preselectionCommand, result: result.status,
      failureStage, status
    }, null, 2));
  }
  return result;
}

async function executePlayback(runId) {
  const sessionRoot = path.join(evidenceRoot, "playback-runs", runId);
  await mkdir(sessionRoot, { recursive: true });
  const requestId = randomUUID();
  const traceOffset = (await stat(tracePath).catch(() => ({ size: 0 }))).size;
  let status = null;
  let result = { status: "INFRA_ERROR" };
  let failureStage = "preflight";
  let submitted = false;
  let running = false;
  let csvPath = null;
  let frameMapPath = null;
  let humanLabelsPath = null;
  try {
    await access(fbxPath);
    if (await readOptional(requestPath) || (await readStatus())?.status === "running") {
      result = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      await writeFile(requestPath, JSON.stringify({
        request_id: requestId, command: playbackCommand, requested_command: playbackCommand
      }), { flag: "wx" });
      submitted = true;
      const startedAt = Date.now();
      while (Date.now() - startedAt < 1260000) {
        await new Promise((resolve) => setTimeout(resolve, 500));
        const candidate = await readStatus();
        if (candidate?.request_id !== requestId) {
          if (Date.now() - startedAt > 30000) break;
          continue;
        }
        status = candidate;
        running = candidate.status === "running";
        if (!running) break;
      }
      if (!status || running) {
        result = { status: running ? "TIMED_OUT" : "BLOCKED",
          failureKind: running ? "timeout" : "preflight" };
      } else if (status.status !== "completed" || status.passed !== true) {
        failureStage = status.failure_stage || "playback";
        result = { status: failureStage === "preflight" ? "BLOCKED" : "FAIL",
          failureKind: failureStage === "preflight" ? "preflight" : "test_failure" };
      } else {
        failureStage = "evidence";
        const statePath = path.resolve(status.playback_state_path || "");
        const allowedRoot = path.join(evidenceRoot, "playback");
        const relativePath = path.relative(allowedRoot, statePath);
        if (relativePath.startsWith("..") || path.isAbsolute(relativePath)) {
          result = { status: "INFRA_ERROR" };
        } else {
          const state = JSON.parse(await readFile(statePath, "utf8"));
          const captures = [state.first_capture_path, state.second_capture_path,
            state.repeat_capture_path];
          const expectedFootFrames = [0, 165, 166, 167, 543, 544, 545, 789, 790, 791,
            1323, 1324, 1325, 1326, 1327, 1328, 1329, 1330, 1331, 1332, 1333];
          const footFrames = state.foot_frames || [];
          csvPath = path.join(path.dirname(statePath), "foot-frames.csv");
          const rows = ["frame,time_s,side,grounding_status,has_ground,support_role,rear_weight,front_weight,rear_signed_mm,front_signed_mm,minimum_signed_mm,root_y_m,hips_y_m,knee_y_m,rear_vertex,front_vertex"];
          for (const frame of footFrames) {
            if (!frame) continue;
            for (const side of ["left", "right"]) {
              const foot = frame[side];
              if (!foot) continue;
              rows.push([frame.actual_frame, frame.time_seconds, side,
                frame.grounding_status, foot.has_ground, foot.support_role,
                foot.rear_weight, foot.front_weight, foot.rear_distance_mm,
                foot.front_distance_mm, foot.minimum_distance_mm,
                frame.root_position?.y, frame.hips_position?.y,
                frame[`${side}_knee_position`]?.y,
                foot.rear_vertex, foot.front_vertex].join(","));
            }
          }
          await writeFile(csvPath, `${rows.join("\n")}\n`);
          frameMapPath = path.join(path.dirname(statePath), "frame-map.csv");
          const samples = [["paused", state.paused], ["first_seek", state.first_seek],
            ["second_seek", state.second_seek], ["repeat_seek", state.repeat_seek],
            ["resumed", state.resumed],
            ...footFrames.filter(Boolean).map((frame) => [`foot_${frame.actual_frame}`, frame])];
          const frameRows = ["sample,requested_clip_frame,unity_frame,time_s,expected_vmd_frame_at_30fps",
            ...samples.map(([name, sample]) => [name, sample?.requested_frame,
              sample?.actual_frame, sample?.time_seconds,
              Math.round((sample?.time_seconds || 0) * 30)].join(","))];
          await writeFile(frameMapPath, `${frameRows.join("\n")}\n`);
          humanLabelsPath = path.join(path.dirname(statePath), "human-labels.csv");
          const intervals = [[0, 0], [165, 167], [543, 545], [789, 791], [1323, 1333]];
          const labelRows = ["from_frame,to_frame,side,contact_label,motion_label,reviewer,notes",
            ...intervals.flatMap(([start, end]) => ["left", "right"].map((side) =>
              `${start},${end},${side},,,,`))];
          await writeFile(humanLabelsPath, `${labelRows.join("\n")}\n`, { flag: "wx" });
          const footEvidenceValid = footFrames.length === expectedFootFrames.length &&
            footFrames.every((frame, index) => frame?.requested_frame === expectedFootFrames[index] &&
              frame.actual_frame === expectedFootFrames[index] &&
              ["Applied", "NoGround"].includes(frame.grounding_status) &&
              frame.left && frame.right &&
              typeof frame.left.support_role === "string" &&
              typeof frame.right.support_role === "string" &&
              [frame.left, frame.right].every((foot) => !foot.has_ground ||
                [foot.rear_distance_mm, foot.front_distance_mm, foot.minimum_distance_mm,
                  foot.rear_weight, foot.front_weight].every(Number.isFinite)) &&
              Number.isFinite(frame.root_position?.y) &&
              Number.isFinite(frame.hips_position?.y));
          const pngPaths = [...captures, ...footFrames.map((frame) => frame?.game_view_path),
            ...footFrames.map((frame) => frame?.side_view_path).filter(Boolean)];
          const completePngs = (await Promise.all(pngPaths.map((file) =>
            hasCompletePngWithin(file, path.dirname(statePath))))).every(Boolean);
          const sideFrames = footFrames.filter((frame) => frame?.side_view_path)
            .map((frame) => frame.actual_frame);
          const expectedFrames = state.first_seek?.actual_frame === 166 &&
            state.second_seek?.actual_frame === 544 &&
            state.repeat_seek?.actual_frame === 166;
          const expectedStates = state.paused?.state === "PreviewPaused" &&
            state.first_seek?.state === "PreviewPaused" &&
            state.second_seek?.state === "PreviewPaused" &&
            state.repeat_seek?.state === "PreviewPaused" &&
            state.resumed?.state === "PreviewPlaying";
          const expectedStages = ["Selected", "Copied", "LoadingFbx", "AvatarReady", "Ready"]
            .every((stage) => state.stage_log?.some((line) =>
              line.includes(`상태=${stage},`)));
          if (state.structural_passed === true && state.visual_review_required === true &&
              state.scene === "Main_Auto" && expectedFrames && expectedStates && expectedStages &&
              state.source_asset_path === "Assets/Resources/Import_FBX/satisfaction_2.fbx" &&
              state.model && state.avatar && state.clip_name &&
              state.importer_clip_count > 0 && state.importer_animation_type === "Human" &&
              state.last_frame >= 1333 && state.clip_frame_rate > 0 &&
              state.repeat_max_position_delta_mm <= 0.5 &&
              state.repeat_max_rotation_delta_degrees <= 0.5 &&
              state.repeat_max_muscle_delta <= 0.0001 && footEvidenceValid &&
              sideFrames.join(",") === "0,166,544" &&
              typeof state.apply_root_motion === "boolean" &&
              typeof state.lock_root_height_y === "boolean" &&
              typeof state.lock_root_position_xz === "boolean" && completePngs) {
            failureStage = "";
            result = { status: "MANUAL_REVIEW_REQUIRED" };
          } else {
            result = { status: "FAIL", failureKind: "test_failure" };
          }
        }
      }
    }
  } catch (error) {
    failureStage = "infrastructure";
    result = !submitted && (error.code === "ENOENT" || error.code === "EEXIST")
      ? { status: "BLOCKED", failureKind: "preflight" }
      : { status: "INFRA_ERROR" };
  } finally {
    if (submitted && !running) {
      const current = await readOptional(requestPath);
      if (current) {
        try {
          if (JSON.parse(current.toString("utf8")).request_id === requestId) {
            await rm(requestPath);
          }
        } catch (error) {
          if (!(error instanceof SyntaxError)) throw error;
        }
      }
    }
    const trace = await readOptional(tracePath);
    if (trace && trace.length > traceOffset) {
      await writeFile(path.join(sessionRoot, "unity-trace.log"), trace.subarray(traceOffset));
    }
    await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
      runId, requestId, command: playbackCommand, result: result.status,
      failureStage, status, csvPath, frameMapPath, humanLabelsPath
    }, null, 2));
  }
  return result;
}

async function executeInvalidInput(runId) {
  const sessionRoot = path.join(evidenceRoot, "invalid-input-runs", runId);
  await mkdir(sessionRoot, { recursive: true });
  const requestId = randomUUID();
  const traceOffset = (await stat(tracePath).catch(() => ({ size: 0 }))).size;
  let status = null;
  let result = { status: "INFRA_ERROR" };
  let failureStage = "preflight";
  let submitted = false;
  let running = false;
  try {
    await access(fbxPath);
    if (await readOptional(requestPath) || (await readStatus())?.status === "running") {
      result = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      await writeFile(requestPath, JSON.stringify({
        request_id: requestId, command: invalidInputCommand, requested_command: invalidInputCommand
      }), { flag: "wx" });
      submitted = true;
      const startedAt = Date.now();
      while (Date.now() - startedAt < 120000) {
        await new Promise((resolve) => setTimeout(resolve, 500));
        const candidate = await readStatus();
        if (candidate?.request_id !== requestId) {
          if (Date.now() - startedAt > 30000) break;
          continue;
        }
        status = candidate;
        running = candidate.status === "running";
        if (!running) break;
      }
      if (!status || running) {
        result = { status: running ? "TIMED_OUT" : "BLOCKED",
          failureKind: running ? "timeout" : "preflight" };
      } else if (status.status !== "completed" || status.passed !== true) {
        failureStage = status.failure_stage || "invalid_input";
        result = { status: failureStage === "preflight" ? "BLOCKED" : "FAIL",
          failureKind: failureStage === "preflight" ? "preflight" : "test_failure" };
      } else {
        failureStage = "evidence";
        const statePath = path.resolve(status.failure_evidence_path || "");
        const relativePath = path.relative(path.join(evidenceRoot, "invalid-input"), statePath);
        if (relativePath.startsWith("..") || path.isAbsolute(relativePath)) {
          result = { status: "INFRA_ERROR" };
        } else {
          const state = JSON.parse(await readFile(statePath, "utf8"));
          const missing = state.missing_fbx;
          const avatar = state.invalid_avatar;
          if (state.test_passed === true && state.scene === "Main_Auto" &&
              state.original_avatar_restored === true &&
              state.is_recording === false && state.capture_framerate === 0 &&
              missing?.product_status === "Failed" &&
              missing?.failure_stage === "input_validation" &&
              missing?.expected_rejection_observed === true &&
              missing?.product_error?.includes("FBX 파일을 찾을 수 없습니다") &&
              avatar?.product_status === "Failed" &&
              avatar?.failure_stage === "avatar_preparation" &&
              avatar?.expected_rejection_observed === true &&
              avatar?.product_error?.includes("유효한 Humanoid Avatar가 없습니다")) {
            failureStage = "";
            result = { status: "PASS" };
          } else {
            result = { status: "FAIL", failureKind: "test_failure" };
          }
        }
      }
    }
  } catch (error) {
    failureStage = "infrastructure";
    result = error.code === "EEXIST"
      ? { status: "BLOCKED", failureKind: "preflight" }
      : { status: "INFRA_ERROR" };
  } finally {
    if (submitted && !running) {
      const current = await readOptional(requestPath);
      if (current) {
        try {
          if (JSON.parse(current.toString("utf8")).request_id === requestId) {
            await rm(requestPath);
          }
        } catch (error) {
          if (!(error instanceof SyntaxError)) throw error;
        }
      }
    }
    const trace = await readOptional(tracePath);
    if (trace && trace.length > traceOffset) {
      await writeFile(path.join(sessionRoot, "unity-trace.log"), trace.subarray(traceOffset));
    }
    await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
      runId, requestId, command: invalidInputCommand, result: result.status,
      failureStage, status
    }, null, 2));
  }
  return result;
}

async function main() {
  const mode = process.argv[2] || "smoke";
  if (!["smoke", "preselection", "playback", "invalid-input"].includes(mode) ||
      process.argv.length > (mode === "smoke" ? 2 : 3)) {
    throw new Error("사용법: node Tools/Boogle/run-product-smoke.mjs [preselection|playback|invalid-input]");
  }
  if (Number(process.versions.node.split(".")[0]) !== 24) {
    throw new Error("Node.js 24가 필요합니다.");
  }
  await access(sdkRunnerPath);
  const unityVersion = (await readFile(
    path.join(projectRoot, "ProjectSettings/ProjectVersion.txt"), "utf8"
  )).match(/^m_EditorVersion:\s*(\S+)/m)?.[1] || "unknown";
  const fbxHash = await hashFile(fbxPath);
  const modelHash = await hashFile(path.join(
    projectRoot, "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx"
  ));
  const sceneHash = await hashFile(path.join(projectRoot, "Assets/_Project/Scene/Main_Auto.unity"));
  const gitRevision = execFileSync("git", ["rev-parse", "HEAD"], {
    cwd: projectRoot, encoding: "utf8"
  }).trim();
  const workingTreeStatus = execFileSync("git", ["status", "--porcelain"], {
    cwd: projectRoot, encoding: "utf8"
  });
  const { runWithAdapter } = await import(pathToFileURL(sdkRunnerPath).href);
  const run = await runWithAdapter(evidenceRoot, {
    protocolVersion: "0.1.0",
    projectId,
    adapter: { id: "fbx2vmd-smoke", version: "0.1.1" },
    testPack: { id: "fbx2vmd-product-smoke", version: "0.1.2" },
    retries: 0,
    inputConditions: {
      testCaseIds: mode === "preselection" ? "F01" : mode === "playback" ? "F03,F04,F05" :
        mode === "invalid-input" ? "F07" : "F02,F06",
      fbxSha256: fbxHash,
      modelSha256: modelHash, sceneSha256: sceneHash
    },
    environmentConditions: {
      unityVersion, scene: "Main_Auto", nodeVersion: process.versions.node,
      gitRevision,
      workingTreeStatusSha256: createHash("sha256").update(workingTreeStatus).digest("hex")
    }
  }, async (temporaryPath) => {
    const runId = path.basename(temporaryPath);
    try {
      return mode === "preselection" ? await executePreselection(runId)
        : mode === "playback" ? await executePlayback(runId)
          : mode === "invalid-input" ? await executeInvalidInput(runId)
            : await executeSmoke(runId);
    } catch (error) {
      const sessionRoot = path.join(evidenceRoot,
        mode === "preselection" ? "preselection-runs" :
          mode === "playback" ? "playback-runs" :
            mode === "invalid-input" ? "invalid-input-runs" : "product-smoke", runId);
      await mkdir(sessionRoot, { recursive: true });
      await writeFile(path.join(sessionRoot, "adapter-error.json"), JSON.stringify({
        runId, result: "INFRA_ERROR", failureStage: "adapter", message: error.message
      }, null, 2));
      return { status: "INFRA_ERROR" };
    }
  });
  process.stdout.write(`${JSON.stringify({
    projectId: run.projectId, runId: run.runId, status: run.status,
    sealedPath: run.sealedPath
  })}\n`);
  process.exitCode = run.status === "PASS" ? 0 : 1;
}

try {
  await main();
} catch (error) {
  process.stderr.write(`${error.message}\n`);
  process.exitCode = 2;
}
