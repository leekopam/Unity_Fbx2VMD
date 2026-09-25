import { execFileSync } from "node:child_process";
import { createHash, randomUUID } from "node:crypto";
import { createReadStream } from "node:fs";
import { access, copyFile, mkdir, open, readFile, readdir, rm, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { compareManualCapture } from "./manual-compare.mjs";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const evidenceRoot = path.join(projectRoot, "Docs/Workflow/Local/evidence/boogle");
const runtimeRoot = path.join(projectRoot, "Docs/Workflow/Local/runtime");
const requestPath = path.join(runtimeRoot, "fbx_smoke_request.json");
const statusPath = path.join(runtimeRoot, "fbx_smoke_status.json");
const tracePath = path.join(runtimeRoot, "fbx_smoke_trace.log");
const fbxPath = path.join(projectRoot, "Assets/Resources/Import_FBX/satisfaction_2.fbx");
const outputPath = path.join(projectRoot, "Assets/VMDRecorderSample/satisfaction_2.vmd");
const vrmOutputPath = path.join(projectRoot, "Assets/VMDRecorderSample/satisfaction_2.vrm");
const fullRegressionOutputPath = path.join(projectRoot,
  "Assets/VMDRecorderSample/smoke_satisfaction_2_208s.vmd");
const sdkRunnerPath = path.join(
  projectRoot,
  "Assets/_Project/Tools/MainRecordingSettings/node_modules/boogle-sdk/dist/runner.js"
);
const projectId = "f2f44dc8-83ef-46d0-9d26-b0e52d1c4d20";
const command = "capture_satisfaction_quick_vmd_smoke_2s";
const preselectionCommand = "capture_preselection_state";
const playbackCommand = "capture_playback_seek_evidence";
const footLiveCommand = "capture_tetoris_live_foot_evidence";
const fullClipCommand = "capture_satisfaction_full_clip_metrics";
const alternateModelCommand = "capture_tetoris_testprefab_full_clip_metrics";
const productUiCommand = "capture_product_ui_flow";
const fullRegressionCommand = "capture_satisfaction_full_regression_evidence_208s_4k";
const fullNamedVmdCommand = "capture_satisfaction_full_named_vmd";
const exportVrmCommand = "capture_satisfaction_vrm";
const segmentCases = [
  ["head", "capture_satisfaction_head_31s", "smoke_satisfaction_2_31s"],
  ["middle", "capture_satisfaction_middle_31s", "smoke_middle_satisfaction_2_31s"],
  ["tail", "capture_satisfaction_tail_31s", "smoke_tail_satisfaction_2_31s"]
];
const invalidInputCommand = "capture_invalid_input_evidence";
const environmentCommand = "capture_e2e_environment";
const enterPlayCommand = "enter_e2e_play";
const exitPlayCommand = "exit_e2e_play";
const environmentFields = ["play_mode", "scene", "scene_path", "scene_dirty",
  "model_name", "model_active", "model_position", "model_rotation", "model_scale",
  "avatar_name", "avatar_valid", "is_processing", "has_prepared_motion",
  "is_playing_motion", "is_recording", "recorder_recording",
  "recorder_output_name", "recorder_last_saved_path", "capture_framerate", "time_scale"];

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

function inspectVmd(bytes, expectedFrames) {
  if (bytes.length < 58 ||
      bytes.subarray(0, 30).toString("ascii").replace(/\0+$/, "") !==
        "Vocaloid Motion Data 0002") return null;
  const boneRecords = bytes.readUInt32LE(50);
  const morphOffset = 54 + boneRecords * 111;
  if (boneRecords < expectedFrames || morphOffset + 4 > bytes.length) return null;
  const frames = new Set();
  const bones = new Set();
  for (let index = 0; index < boneRecords; index++) {
    const offset = 54 + index * 111;
    const frame = bytes.readUInt32LE(offset + 15);
    if (frame >= expectedFrames) return null;
    const key = `${bytes.subarray(offset, offset + 15).toString("hex")}:${frame}`;
    if (bones.has(key)) return null;
    bones.add(key);
    frames.add(frame);
  }
  const morphRecords = bytes.readUInt32LE(morphOffset);
  if (morphOffset + 4 + morphRecords * 23 > bytes.length) return null;
  return { boneRecords, morphRecords, uniqueFrames: frames.size,
    bytesPerFrame: bytes.length / expectedFrames };
}

function inspectVrm(bytes) {
  if (bytes.length < 20 || bytes.toString("ascii", 0, 4) !== "glTF" ||
      bytes.readUInt32LE(4) !== 2 || bytes.readUInt32LE(8) !== bytes.length) return null;
  const jsonLength = bytes.readUInt32LE(12);
  if (bytes.readUInt32LE(16) !== 0x4e4f534a || jsonLength < 2 ||
      20 + jsonLength > bytes.length) return null;
  try {
    const gltf = JSON.parse(bytes.toString("utf8", 20, 20 + jsonLength));
    const meta = gltf.extensions?.VRM?.meta;
    if (gltf.asset?.version !== "2.0" || !gltf.extensionsUsed?.includes("VRM") ||
        !Array.isArray(gltf.meshes) || gltf.meshes.length === 0 ||
        meta?.title !== "Hatsune Miku" || meta?.author !== "SANMUYYB" ||
        meta?.licenseName !== "Redistribution_Prohibited") return null;
    return { meshCount: gltf.meshes.length, nodeCount: gltf.nodes?.length || 0,
      title: meta.title, author: meta.author, licenseName: meta.licenseName };
  } catch {
    return null;
  }
}

async function executeControl(runId, controlCommand) {
  const sessionRoot = path.join(evidenceRoot, "control-runs", runId);
  await mkdir(sessionRoot, { recursive: true });
  const requestId = randomUUID();
  const traceOffset = (await stat(tracePath).catch(() => ({ size: 0 }))).size;
  let status = null;
  let state = null;
  let result = { status: "INFRA_ERROR" };
  let failureStage = "preflight";
  let submitted = false;
  let terminal = false;
  try {
    if (await readOptional(requestPath) || (await readStatus())?.status === "running") {
      result = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      await writeFile(requestPath, JSON.stringify({
        request_id: requestId, command: controlCommand, requested_command: controlCommand
      }), { flag: "wx" });
      submitted = true;
      const startedAt = Date.now();
      while (Date.now() - startedAt < 180000) {
        await new Promise((resolve) => setTimeout(resolve, 500));
        const candidate = await readStatus();
        if (candidate?.request_id !== requestId) continue;
        status = candidate;
        if (candidate.status !== "running") {
          terminal = true;
          break;
        }
      }
      if (!terminal) {
        failureStage = "timeout";
        result = { status: "TIMED_OUT", failureKind: "timeout" };
      } else if (status.status !== "completed" || status.passed !== true) {
        failureStage = status.failure_stage || "environment";
        result = { status: failureStage === "preflight" ? "BLOCKED" : "FAIL",
          failureKind: failureStage === "preflight" ? "preflight" : "test_failure" };
      } else if (controlCommand === environmentCommand) {
        failureStage = "evidence";
        const statePath = path.resolve(status.environment_state_path || "");
        const relative = path.relative(path.join(evidenceRoot, "e2e-environment"), statePath);
        if (relative.startsWith("..") || path.isAbsolute(relative)) {
          result = { status: "INFRA_ERROR" };
        } else {
          state = JSON.parse(await readFile(statePath, "utf8"));
          failureStage = "";
          result = { status: "PASS" };
        }
      } else {
        failureStage = "";
        result = { status: "PASS" };
      }
    }
  } catch (error) {
    failureStage = "infrastructure";
    result = { status: submitted ? "INFRA_ERROR" : "BLOCKED" };
  } finally {
    if (submitted && terminal) {
      const current = await readOptional(requestPath);
      if (current) {
        try {
          if (JSON.parse(current.toString("utf8")).request_id === requestId)
            await rm(requestPath);
        } catch (error) {
          if (!(error instanceof SyntaxError)) throw error;
        }
      }
    }
    const trace = await readOptional(tracePath);
    if (trace && trace.length > traceOffset)
      await writeFile(path.join(sessionRoot, `${controlCommand}-${requestId}-trace.log`),
        trace.subarray(traceOffset));
    await writeFile(path.join(sessionRoot, `${controlCommand}-${requestId}.json`), JSON.stringify({
      runId, requestId, command: controlCommand, result: result.status,
      failureStage, status, state, terminal
    }, null, 2));
  }
  return { ...result, state, requestId, terminal, unityStatus: status };
}

async function executeSmoke(runId, options = {}) {
  const smokeCommand = options.command || command;
  const smokeOutputPath = options.outputPath || outputPath;
  const expectedFrames = options.frameCount || 60;
  const sessionRoot = path.join(evidenceRoot, options.folder || "product-smoke", runId);
  await mkdir(sessionRoot, { recursive: true });
  const backupPath = path.join(sessionRoot, "prior-output.vmd");
  const backupMetaPath = path.join(sessionRoot, "prior-output.vmd.meta");
  const priorOutput = await readOptional(smokeOutputPath);
  const priorMeta = await readOptional(`${smokeOutputPath}.meta`);
  const outputDirectory = path.dirname(smokeOutputPath);
  const extraBackups = [];
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
    await access(path.dirname(smokeOutputPath));
    if (await readOptional(requestPath) || (await readStatus())?.status === "running") {
      outcome = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      if (priorOutput) await copyFile(smokeOutputPath, backupPath);
      if (priorMeta) await copyFile(`${smokeOutputPath}.meta`, backupMetaPath);
      if (options.protectedPrefix) {
        for (const name of await readdir(outputDirectory)) {
          if (!name.startsWith(options.protectedPrefix)) continue;
          const target = path.join(outputDirectory, name);
          if (target === smokeOutputPath || target === `${smokeOutputPath}.meta`) continue;
          const backup = path.join(sessionRoot, `prior-${name}`);
          await copyFile(target, backup);
          extraBackups.push({ target, backup, sha256: await hashFile(target) });
        }
      }
      backupCreated = true;
      const request = { request_id: requestId, command: smokeCommand,
        requested_command: smokeCommand };
      await writeFile(requestPath, JSON.stringify(request), { flag: "wx" });
      requestSubmitted = true;
      // 종료 상태가 확인되기 전에는 기존 산출물에 손대지 않는다.
      stillRunning = true;
      const startedAt = Date.now();
      while (Date.now() - startedAt < (options.timeoutMs || 600000)) {
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
        failureStage = "timeout";
        outcome = { status: "TIMED_OUT", failureKind: "timeout" };
      } else if (status.status !== "completed" || status.passed !== true) {
        failureStage = status.failure_stage || "product_path";
        outcome = failureStage === "preflight"
          ? { status: "BLOCKED", failureKind: "preflight" }
          : { status: "FAIL", failureKind: "test_failure" };
      } else {
        failureStage = "output";
        const output = await stat(smokeOutputPath).catch(() => null);
        const hasExpectedPath = path.resolve(status.output_path || "").toLowerCase() === smokeOutputPath.toLowerCase();
        const manifestPath = path.resolve(status.manifest_path || "");
        const manifestRelative = path.relative(path.join(projectRoot, "Docs/Workflow/Local"), manifestPath);
        const hasManifest = !options.visualReview ||
          (!manifestRelative.startsWith("..") && !path.isAbsolute(manifestRelative) &&
            !!(await stat(manifestPath).catch(() => null)));
        if (output?.size > 0) vmdStructure = inspectVmd(await readFile(smokeOutputPath), expectedFrames);
        if (status.total_jobs === 1 && status.success_jobs === 1 &&
            status.frame_count === expectedFrames && status.file_size_bytes === output?.size &&
            output?.size > 0 && hasExpectedPath && hasManifest &&
            vmdStructure?.uniqueFrames === expectedFrames &&
            vmdStructure.boneRecords % expectedFrames === 0) {
          failureStage = "";
          outcome = {
            status: options.visualReview ? "MANUAL_REVIEW_REQUIRED" : "PASS",
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
    const cleanupErrors = [];
    if (requestSubmitted && !stillRunning) {
      try {
        const currentRequest = await readOptional(requestPath);
        if (currentRequest && JSON.parse(currentRequest.toString("utf8")).request_id === requestId)
          await rm(requestPath);
      } catch (error) {
        cleanupErrors.push(`request: ${error.message}`);
      }
    }
    try {
      if (!backupCreated) {
        restored = true;
      } else if (!stillRunning) {
        if (priorOutput) await copyFile(backupPath, smokeOutputPath);
        else await rm(smokeOutputPath, { force: true });
        if (priorMeta) await copyFile(backupMetaPath, `${smokeOutputPath}.meta`);
        else await rm(`${smokeOutputPath}.meta`, { force: true });
        if (options.protectedPrefix) {
          const originals = new Set(extraBackups.map((entry) => entry.target));
          for (const name of await readdir(outputDirectory)) {
            const target = path.join(outputDirectory, name);
            if (name.startsWith(options.protectedPrefix) &&
                target !== smokeOutputPath && target !== `${smokeOutputPath}.meta` &&
                !originals.has(target)) await rm(target, { force: true });
          }
          for (const entry of extraBackups) {
            await copyFile(entry.backup, entry.target);
            if (await hashFile(entry.target) !== entry.sha256)
              throw new Error(`기존 진단 산출물이 원본과 다릅니다: ${entry.target}`);
          }
        }
        const currentOutput = await readOptional(smokeOutputPath);
        const currentMeta = await readOptional(`${smokeOutputPath}.meta`);
        restored = (priorOutput === null ? currentOutput === null :
          currentOutput !== null && priorOutput.equals(currentOutput)) &&
          (priorMeta === null ? currentMeta === null :
            currentMeta !== null && priorMeta.equals(currentMeta));
        if (!restored) throw new Error("기존 VMD 또는 .meta가 원본과 다릅니다.");
      }
    } catch (error) {
      cleanupErrors.push(`output: ${error.message}`);
    }
    try {
      const trace = await readOptional(tracePath);
      if (trace && trace.length > traceOffset)
        await writeFile(path.join(sessionRoot, "unity-trace.log"), trace.subarray(traceOffset));
    } catch (error) {
      cleanupErrors.push(`trace: ${error.message}`);
    }
    if (cleanupErrors.length) {
      failureStage = "cleanup";
      outcome = { status: "INFRA_ERROR" };
    }
    await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
      runId, requestId, command: smokeCommand, result: outcome.status, failureStage, restored,
      status, outputPath: smokeOutputPath, backupPath: priorOutput ? backupPath : null,
      backupMetaPath: priorMeta ? backupMetaPath : null,
      extraBackups, vmdStructure, cleanupErrors
    }, null, 2));
  }
  return { ...outcome, requestId, terminal: !stillRunning, restored };
}

async function executeVrmOutput(runId) {
  const sessionRoot = path.join(evidenceRoot, "full-vrm-output", runId);
  await mkdir(sessionRoot, { recursive: true });
  const backupPath = path.join(sessionRoot, "prior-output.vrm");
  const backupMetaPath = `${backupPath}.meta`;
  const priorOutput = await readOptional(vrmOutputPath);
  const priorMeta = await readOptional(`${vrmOutputPath}.meta`);
  let commandResult = null;
  let structure = null;
  let fileSizeBytes = 0;
  let restored = false;
  let result = { status: "INFRA_ERROR" };
  try {
    if (priorOutput) await copyFile(vrmOutputPath, backupPath);
    if (priorMeta) await copyFile(`${vrmOutputPath}.meta`, backupMetaPath);
    commandResult = await executeControl(runId, exportVrmCommand);
    result = { status: commandResult.status };
    if (commandResult.status === "PASS") {
      const file = await stat(vrmOutputPath).catch(() => null);
      fileSizeBytes = file?.size || 0;
      if (fileSizeBytes > 0) structure = inspectVrm(await readFile(vrmOutputPath));
      result = {
        status: structure &&
          path.resolve(commandResult.unityStatus?.vrm_output_path || "").toLowerCase() ===
            vrmOutputPath.toLowerCase() &&
          commandResult.unityStatus?.vrm_file_size_bytes === fileSizeBytes
          ? "MANUAL_REVIEW_REQUIRED" : "FAIL"
      };
    }
  } catch (error) {
    result = { status: "INFRA_ERROR", message: error.message };
  } finally {
    if (!commandResult || commandResult.terminal || commandResult.status === "BLOCKED") {
      try {
        if (priorOutput) await copyFile(backupPath, vrmOutputPath);
        else await rm(vrmOutputPath, { force: true });
        if (priorMeta) await copyFile(backupMetaPath, `${vrmOutputPath}.meta`);
        else await rm(`${vrmOutputPath}.meta`, { force: true });
        const currentOutput = await readOptional(vrmOutputPath);
        const currentMeta = await readOptional(`${vrmOutputPath}.meta`);
        restored = (priorOutput === null ? currentOutput === null :
          currentOutput !== null && priorOutput.equals(currentOutput)) &&
          (priorMeta === null ? currentMeta === null :
            currentMeta !== null && priorMeta.equals(currentMeta));
        if (!restored) result = { status: "INFRA_ERROR", message: "VRM 복원 불일치" };
      } catch (error) {
        result = { status: "INFRA_ERROR", message: error.message };
      }
    }
    await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
      runId, result: result.status, requestId: commandResult?.requestId ?? null,
      unityStatus: commandResult?.unityStatus ?? null, outputPath: vrmOutputPath,
      fileSizeBytes, structure, restored
    }, null, 2));
  }
  return { ...result, requestId: commandResult?.requestId ?? null,
    outputPath: vrmOutputPath, fileSizeBytes, structure, restored };
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
      running = true;
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
        failureStage = running ? "timeout" : "preflight";
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
  return { ...result, requestId };
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
  let f11Metrics = null;
  try {
    await access(fbxPath);
    if (await readOptional(requestPath) || (await readStatus())?.status === "running") {
      result = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      await writeFile(requestPath, JSON.stringify({
        request_id: requestId, command: playbackCommand, requested_command: playbackCommand
      }), { flag: "wx" });
      submitted = true;
      running = true;
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
        failureStage = running ? "timeout" : "preflight";
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
            1323, 1324, 1325, 1326, 1327, 1328, 1329, 1330, 1331, 1332, 1333,
            2404, 2405, 2406, 8019, 8020, 8021, 11615, 11616, 11617];
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
          const intervals = [[0, 0], [165, 167], [543, 545], [789, 791], [1323, 1333],
            [2404, 2406], [8019, 8021], [11615, 11617]];
          const labelRows = ["from_frame,to_frame,side,contact_label,motion_label,reviewer,notes",
            ...intervals.flatMap(([start, end]) => ["left", "right"].map((side) =>
              `${start},${end},${side},,,,`))];
          await writeFile(path.join(path.dirname(statePath), "human-labels-template.csv"),
            `${labelRows.join("\n")}\n`, { flag: "wx" });
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
          const mandatoryBones = new Set(["Hips", "Head", "LeftHand", "RightHand",
            "LeftFoot", "RightFoot"]);
          const stageEvidenceValid = footFrames.length === expectedFootFrames.length &&
            footFrames.every((frame) => {
              const before = frame?.f11_before_foot_stabilization;
              const after = frame?.f11_after_foot_stabilization;
              return Array.isArray(before) && Array.isArray(after) &&
                before.length === 20 && after.length === before.length &&
                before.every((bone, index) => {
                  const finalBone = after[index];
                  return bone.bone === finalBone?.bone &&
                    bone.present === finalBone.present &&
                    (!mandatoryBones.has(bone.bone) || bone.present) &&
                    (!bone.present || [bone, finalBone].every((sample) =>
                      [sample.position, sample.rotation, sample.local_position,
                        sample.local_scale].every((value) =>
                        value && Object.keys(value).length >= 3 &&
                        Object.values(value).every(Number.isFinite))));
                });
            });
          if (stageEvidenceValid) {
            const length = (vector) => Math.hypot(vector.x, vector.y, vector.z);
            const pairs = footFrames.flatMap((frame) =>
              frame.f11_before_foot_stabilization.map((before, index) =>
                [before, frame.f11_after_foot_stabilization[index]]));
            f11Metrics = {
              status: "MANUAL_REVIEW_REQUIRED",
              sampledFrames: footFrames.length,
              bonesPerStage: 20,
              maxBoneLengthDeltaMm: Math.max(...pairs.filter(([before]) => before.present)
                .map(([before, after]) => 1000 * Math.abs(
                  length(before.local_position) - length(after.local_position)))),
              maxLocalScaleDelta: Math.max(...pairs.filter(([before]) => before.present)
                .map(([before, after]) => length({
                  x: before.local_scale.x - after.local_scale.x,
                  y: before.local_scale.y - after.local_scale.y,
                  z: before.local_scale.z - after.local_scale.z
                })))
            };
          } else f11Metrics = { status: "FAIL" };
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
              stageEvidenceValid &&
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
      failureStage, status, csvPath, frameMapPath, humanLabelsPath, f11Metrics
    }, null, 2));
  }
  return { ...result, requestId };
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
      running = true;
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
        failureStage = running ? "timeout" : "preflight";
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
  return { ...result, requestId };
}

async function executeFootLive(runId) {
  const sessionRoot = path.join(evidenceRoot, "foot-live-runs", runId);
  await mkdir(sessionRoot, { recursive: true });
  const requestId = randomUUID();
  const traceOffset = (await stat(tracePath).catch(() => ({ size: 0 }))).size;
  const steps = [];
  const vmdBefore = await hashFile(outputPath);
  const vmdMetaBefore = await hashFile(`${outputPath}.meta`);
  let before = null;
  let after = null;
  let state = null;
  let unityStatus = null;
  let submitted = false;
  let terminal = false;
  let enteredPlay = false;
  let result = { status: "INFRA_ERROR" };
  let failureStage = "preflight";
  try {
    await access(path.join(projectRoot, "Assets/Resources/Import_FBX/tetoris_001.fbx"));
    const baseline = await executeControl(runId, environmentCommand);
    steps.push({ name: "before", status: baseline.status, requestId: baseline.requestId });
    before = baseline.state;
    if (baseline.status !== "PASS" || !before || before.play_mode ||
        before.scene_path !== "Assets/_Project/Scene/Main_Auto.unity" ||
        before.scene_dirty || before.model_name !== "YYB Hatsune Miku" ||
        !before.model_active || before.has_prepared_motion || before.is_recording ||
        before.capture_framerate !== 0) {
      result = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      const enter = await executeControl(runId, enterPlayCommand);
      steps.push({ name: "enter_play", status: enter.status, requestId: enter.requestId });
      enteredPlay = enter.status === "PASS";
      if (!enteredPlay) result = { status: enter.status };
      else if (await readOptional(requestPath) || (await readStatus())?.status === "running")
        result = { status: "BLOCKED", failureKind: "preflight" };
      else {
        await writeFile(requestPath, JSON.stringify({ request_id: requestId,
          command: footLiveCommand, requested_command: footLiveCommand, run_id: runId
        }), { flag: "wx" });
        submitted = true;
        const startedAt = Date.now();
        while (Date.now() - startedAt < 1260000) {
          await new Promise((resolve) => setTimeout(resolve, 500));
          const candidate = await readStatus();
          if (candidate?.request_id !== requestId) continue;
          unityStatus = candidate;
          if (candidate.status !== "running") { terminal = true; break; }
        }
        if (!terminal) {
          failureStage = "timeout";
          result = { status: "TIMED_OUT", failureKind: "timeout" };
        } else if (unityStatus.status !== "completed" || unityStatus.passed !== true) {
          failureStage = unityStatus.failure_stage || "capture";
          result = { status: failureStage === "preflight" ? "BLOCKED" : "FAIL",
            failureKind: failureStage === "preflight" ? "preflight" : "test_failure" };
        } else {
          failureStage = "evidence";
          const statePath = path.resolve(unityStatus.foot_live_state_path || "");
          const allowedRoot = path.join(evidenceRoot, "foot-live", runId, requestId);
          if (path.relative(allowedRoot, statePath) !== "state.json")
            throw new Error("F09 상태 파일 경로가 실행 폴더를 벗어났습니다.");
          state = JSON.parse(await readFile(statePath, "utf8"));
          const ranges = [[1525, 1565], [3769, 3822]];
          const expectedCount = ranges.reduce((sum, [first, last]) => sum + last - first + 1, 0);
          const seekKeys = new Set((state.seek_rows || []).map((row) =>
            `${row.segment_start}:${row.frame}:${row.side}`));
          const seekComplete = state.seek_rows?.length === expectedCount * 2 &&
            ranges.every(([first, last]) => Array.from({ length: last - first + 1 }, (_, offset) =>
              ["left", "right"].every((side) => seekKeys.has(`${first}:${first + offset}:${side}`)))
              .every(Boolean));
          const liveFrames = state.live_frame_map || [];
          const liveKeys = new Set(liveFrames.map((entry) => `${entry.segment_start}:${entry.frame}`));
          const missing = ranges.map(([first, last]) => Array.from({ length: last - first + 1 },
            (_, offset) => first + offset).filter((frame) => !liveKeys.has(`${first}:${frame}`)));
          const videoPaths = state.video_paths || [];
          const videosComplete = videoPaths.length === 2 && (await Promise.all(
            videoPaths.map(async (video) => {
              const resolved = path.resolve(video || "");
              if (path.relative(allowedRoot, resolved).startsWith("..") ||
                  path.isAbsolute(path.relative(allowedRoot, resolved))) return false;
              const handle = await open(resolved, "r");
              try {
                if ((await handle.stat()).size <= 1024) return false;
                const header = Buffer.alloc(8);
                await handle.read(header, 0, 8, 0);
                return header.subarray(4).toString("ascii") === "ftyp";
              } finally { await handle.close(); }
            }))).every(Boolean);
          const valid = ["partial", "completed"].includes(state.status) &&
            state.input === "tetoris_001.fbx" && state.scene ===
              "Assets/_Project/Scene/Main_Auto.unity" &&
            state.video_mapping_verified === false && seekComplete &&
            liveFrames.length > 0 && state.live_rows?.length === liveFrames.length * 2 &&
            JSON.stringify(state.missing_frames) === JSON.stringify(missing) && videosComplete;
          const csvPath = path.join(allowedRoot, "live-foot.csv");
          const csv = ["segment_start,observed_index,frame,time_s,side,has_ground,support_role,rear_weight,front_weight,rear_signed_mm,front_signed_mm,minimum_signed_mm,rear_vertex,front_vertex"];
          for (const row of state.live_rows || []) csv.push([
            row.segment_start, row.observed_index, row.frame, row.time_seconds,
            row.side, row.has_ground, row.support_role, ...row.weight,
            ...row.signed_ground_distance_mm, ...row.fixed_vertex
          ].join(","));
          await writeFile(csvPath, `${csv.join("\n")}\n`);
          const labelsPath = path.join(allowedRoot, "human-labels.csv");
          const labels = ["from_frame,to_frame,side,contact_label,motion_label,reviewer,notes"];
          for (const [first, last] of ranges)
            for (const side of ["left", "right"])
              labels.push(`${first},${last},${side},,,,`);
          await writeFile(path.join(allowedRoot, "human-labels-template.csv"),
            `${labels.join("\n")}\n`, { flag: "wx" });
          await writeFile(labelsPath, `${labels.join("\n")}\n`, { flag: "wx" });
          result = valid ? { status: "MANUAL_REVIEW_REQUIRED" } :
            { status: "FAIL", failureKind: "test_failure" };
          if (valid) failureStage = "";
        }
      }
    }
  } catch (error) {
    failureStage = "infrastructure";
    result = { status: "INFRA_ERROR", message: error.message };
  } finally {
    if (submitted && terminal) {
      const pending = await readOptional(requestPath);
      if (pending && JSON.parse(pending.toString("utf8")).request_id === requestId)
        await rm(requestPath);
    }
    if (enteredPlay && (!submitted || terminal)) {
      const exit = await executeControl(runId, exitPlayCommand);
      steps.push({ name: "exit_play", status: exit.status, requestId: exit.requestId });
      if (exit.status !== "PASS") result = { status: "INFRA_ERROR" };
    }
    if (!submitted || terminal) {
      const final = await executeControl(runId, environmentCommand);
      steps.push({ name: "after", status: final.status, requestId: final.requestId });
      after = final.state;
      if (!before || final.status !== "PASS" ||
          !environmentFields.every((field) =>
            JSON.stringify(before[field]) === JSON.stringify(after?.[field])) ||
          vmdBefore !== await hashFile(outputPath) ||
          vmdMetaBefore !== await hashFile(`${outputPath}.meta`))
        result = { status: "INFRA_ERROR" };
    }
    const trace = await readOptional(tracePath);
    if (trace && trace.length > traceOffset)
      await writeFile(path.join(sessionRoot, "unity-trace.log"), trace.subarray(traceOffset));
    await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
      runId, requestId, result: result.status, failureStage, steps, before, after,
      unityStatus, statePath: unityStatus?.foot_live_state_path || null,
      missingFrames: state?.missing_frames || null
    }, null, 2));
  }
  return { ...result, requestId };
}

async function executeFullClip(runId, alternateModel = false) {
  const sessionRoot = path.join(evidenceRoot,
    alternateModel ? "alternate-model-runs" : "full-clip-runs", runId);
  await mkdir(sessionRoot, { recursive: true });
  const requestId = randomUUID();
  const traceOffset = (await stat(tracePath).catch(() => ({ size: 0 }))).size;
  const steps = [];
  const vmdBefore = await hashFile(outputPath);
  const vmdMetaBefore = await hashFile(`${outputPath}.meta`);
  let before = null;
  let after = null;
  let unityStatus = null;
  let state = null;
  let submitted = false;
  let terminal = false;
  let enteredPlay = false;
  let result = { status: "INFRA_ERROR" };
  let failureStage = "preflight";
  let humanLabelsPath = null;
  try {
    await access(alternateModel
      ? path.join(projectRoot, "Assets/Resources/Import_FBX/tetoris_001.fbx") : fbxPath);
    const baseline = await executeControl(runId, environmentCommand);
    steps.push({ name: "before", status: baseline.status, requestId: baseline.requestId });
    before = baseline.state;
    if (baseline.status !== "PASS" || !before || before.play_mode ||
        before.scene_path !== "Assets/_Project/Scene/Main_Auto.unity" ||
        before.scene_dirty || before.model_name !== "YYB Hatsune Miku" ||
        !before.model_active || before.has_prepared_motion || before.is_recording ||
        before.capture_framerate !== 0) {
      result = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      const enter = await executeControl(runId, enterPlayCommand);
      steps.push({ name: "enter_play", status: enter.status, requestId: enter.requestId });
      enteredPlay = enter.status === "PASS";
      if (!enteredPlay) result = { status: enter.status };
      else if (await readOptional(requestPath) || (await readStatus())?.status === "running")
        result = { status: "BLOCKED", failureKind: "preflight" };
      else {
        await writeFile(requestPath, JSON.stringify({ request_id: requestId,
          command: alternateModel ? alternateModelCommand : fullClipCommand,
          requested_command: alternateModel ? alternateModelCommand : fullClipCommand,
          run_id: runId
        }), { flag: "wx" });
        submitted = true;
        const startedAt = Date.now();
        while (Date.now() - startedAt < 1920000) {
          await new Promise((resolve) => setTimeout(resolve, 500));
          const candidate = await readStatus();
          if (candidate?.request_id !== requestId) continue;
          unityStatus = candidate;
          if (candidate.status !== "running") { terminal = true; break; }
        }
        if (!terminal) {
          failureStage = "timeout";
          result = { status: "TIMED_OUT", failureKind: "timeout" };
        } else if (!unityStatus.full_clip_state_path) {
          failureStage = unityStatus.failure_stage || "capture";
          result = { status: failureStage === "preflight" ? "BLOCKED" : "FAIL",
            failureKind: failureStage === "preflight" ? "preflight" : "test_failure" };
        } else {
          failureStage = "evidence";
          const allowedRoot = path.join(evidenceRoot,
            alternateModel ? "full-clip-f14" : "full-clip", runId, requestId);
          const statePath = path.resolve(unityStatus.full_clip_state_path || "");
          if (path.relative(allowedRoot, statePath) !== "state.json")
            throw new Error("상태 파일 경로가 실행 폴더를 벗어났습니다.");
          state = JSON.parse(await readFile(statePath, "utf8"));
          if (unityStatus.status !== "completed" || unityStatus.passed !== true) {
            failureStage = state.failure_stage || unityStatus.failure_stage || "capture";
            result = { status: failureStage === "preflight" ? "BLOCKED" : "FAIL",
              failureKind: failureStage === "preflight" ? "preflight" : "test_failure" };
          } else {
            const csvPath = path.resolve(state.csv_path || "");
            if (path.relative(allowedRoot, csvPath) !== "all-frames.csv")
              throw new Error("CSV 경로가 실행 폴더를 벗어났습니다.");
            const rows = (await readFile(csvPath, "utf8")).trimEnd().split(/\r?\n/);
            const expected = (state.last_frame + 1) * 2;
            const valid = state.status === "metrics_complete_review_required" &&
              state.scene === "Assets/_Project/Scene/Main_Auto.unity" &&
              state.input === (alternateModel ? "tetoris_001.fbx" : "satisfaction_2.fbx") &&
              state.model === (alternateModel ? "testPrefab" : "YYB Hatsune Miku") &&
              state.last_frame >= (alternateModel ? 8706 : 1333) &&
              state.clip_frame_rate > 0 && state.processed_frames === state.last_frame + 1 &&
              state.row_count === expected && rows.length === expected + 1 &&
              rows[0].startsWith("frame,time_s,time_error_ms,side,") &&
              rows.slice(1).every((row, index) => {
                const cells = row.split(",");
                const frame = Math.floor(index / 2);
                return cells.length === 47 && Number(cells[0]) === frame &&
                  cells[3] === (index % 2 ? "right" : "left") &&
                  Math.abs(Number(cells[1]) - frame / state.clip_frame_rate) * 1000 <=
                    500 / state.clip_frame_rate + 0.02 &&
                  Number.isFinite(Number(cells[2]));
              });
            result = valid ? { status: "MANUAL_REVIEW_REQUIRED" } :
              { status: "FAIL", failureKind: "test_failure" };
            if (valid) {
              failureStage = "";
              const ranges = [];
              for (const frame of state.review_frames || []) {
                const last = ranges.at(-1);
                if (last && frame === last[1] + 1) last[1] = frame;
                else ranges.push([frame, frame]);
              }
              const labels = ["from_frame,to_frame,side,contact_label,motion_label,reviewer,notes",
                ...ranges.flatMap(([first, last]) => ["left", "right"].map((side) =>
                  `${first},${last},${side},,,,`))];
              humanLabelsPath = path.join(allowedRoot, "human-labels.csv");
              await writeFile(path.join(allowedRoot, "human-labels-template.csv"),
                `${labels.join("\n")}\n`, { flag: "wx" });
              await writeFile(humanLabelsPath, `${labels.join("\n")}\n`, { flag: "wx" });
            }
          }
        }
      }
    }
  } catch (error) {
    failureStage = "infrastructure";
    result = { status: "INFRA_ERROR", message: error.message };
  } finally {
    if (submitted && terminal) {
      const pending = await readOptional(requestPath);
      if (pending && JSON.parse(pending.toString("utf8")).request_id === requestId)
        await rm(requestPath);
    }
    if (enteredPlay && (!submitted || terminal)) {
      const exit = await executeControl(runId, exitPlayCommand);
      steps.push({ name: "exit_play", status: exit.status, requestId: exit.requestId });
      if (exit.status !== "PASS") result = { status: "INFRA_ERROR" };
    }
    if (!submitted || terminal) {
      const final = await executeControl(runId, environmentCommand);
      steps.push({ name: "after", status: final.status, requestId: final.requestId });
      after = final.state;
      if (!before || final.status !== "PASS" ||
          !environmentFields.every((field) =>
            JSON.stringify(before[field]) === JSON.stringify(after?.[field])) ||
          vmdBefore !== await hashFile(outputPath) ||
          vmdMetaBefore !== await hashFile(`${outputPath}.meta`))
        result = { status: "INFRA_ERROR" };
    }
    const trace = await readOptional(tracePath);
    if (trace && trace.length > traceOffset)
      await writeFile(path.join(sessionRoot, "unity-trace.log"), trace.subarray(traceOffset));
    await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
      runId, requestId, result: result.status, failureStage, steps, before, after,
      unityStatus, statePath: unityStatus?.full_clip_state_path || null,
      humanLabelsPath,
      processedFrames: state?.processed_frames ?? null, rowCount: state?.row_count ?? null,
      nativeSkinningProcessedFrames: state?.native_skinning_processed_frames ?? null,
      nativeSkinningTotalFrames: state?.native_skinning_total_frames ?? null,
      lowerBodyDirectAssessment: alternateModel ? "DIAGNOSTIC_ONLY" : null,
      fullVmdOutput: alternateModel ? "NOT_CAPTURED_BY_METRICS" : null
    }, null, 2));
  }
  return { ...result, requestId };
}

async function executeProductUi(runId) {
  const sessionRoot = path.join(evidenceRoot, "product-ui-runs", runId);
  await mkdir(sessionRoot, { recursive: true });
  const requestId = randomUUID();
  const steps = [];
  const vmdBefore = await hashFile(outputPath);
  const vmdMetaBefore = await hashFile(`${outputPath}.meta`);
  let before = null;
  let after = null;
  let unityStatus = null;
  let state = null;
  let submitted = false;
  let terminal = false;
  let enteredPlay = false;
  let result = { status: "INFRA_ERROR" };
  let failureStage = "preflight";
  try {
    await access(path.join(projectRoot, "Assets/Resources/Import_FBX/Snake Hip Hop Dance.fbx"));
    const baseline = await executeControl(runId, environmentCommand);
    steps.push({ name: "before", status: baseline.status, requestId: baseline.requestId });
    before = baseline.state;
    if (baseline.status !== "PASS" || !before || before.play_mode ||
        before.scene_path !== "Assets/_Project/Scene/Main_Auto.unity" ||
        before.scene_dirty || before.model_name !== "YYB Hatsune Miku" ||
        !before.model_active || before.has_prepared_motion || before.is_recording ||
        before.capture_framerate !== 0) {
      result = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      const enter = await executeControl(runId, enterPlayCommand);
      steps.push({ name: "enter_play", status: enter.status, requestId: enter.requestId });
      enteredPlay = enter.status === "PASS";
      if (!enteredPlay) result = { status: enter.status };
      else if (await readOptional(requestPath) || (await readStatus())?.status === "running")
        result = { status: "BLOCKED", failureKind: "preflight" };
      else {
        await writeFile(requestPath, JSON.stringify({ request_id: requestId,
          command: productUiCommand, requested_command: productUiCommand, run_id: runId
        }), { flag: "wx" });
        submitted = true;
        const startedAt = Date.now();
        while (Date.now() - startedAt < 480000) {
          await new Promise((resolve) => setTimeout(resolve, 500));
          const candidate = await readStatus();
          if (candidate?.request_id !== requestId) continue;
          unityStatus = candidate;
          if (candidate.status !== "running") { terminal = true; break; }
        }
        if (!terminal) {
          failureStage = "timeout";
          result = { status: "TIMED_OUT", failureKind: "timeout" };
        } else if (!unityStatus.manifest_path) {
          failureStage = unityStatus.failure_stage || "capture";
          result = { status: failureStage === "preflight" ? "BLOCKED" : "FAIL" };
        } else {
          const allowedRoot = path.join(evidenceRoot, "product-ui", runId, requestId);
          const statePath = path.resolve(unityStatus.manifest_path);
          if (path.relative(allowedRoot, statePath) !== "state.json")
            throw new Error("F15 상태 파일 경로가 실행 폴더를 벗어났습니다.");
          state = JSON.parse(await readFile(statePath, "utf8"));
          if (unityStatus.status !== "completed" || unityStatus.passed !== true) {
            failureStage = state.failure_stage || unityStatus.failure_stage || "capture";
            result = { status: "FAIL", failureKind: "test_failure" };
          } else {
            const expectedSteps = ["before_import", "import_ready", "playing", "paused",
              "recording", "recording_stopped", "stopped", "invalid_input_error"];
            const recordedSteps = state.events?.map((entry) => entry.step) || [];
            const validSteps = expectedSteps.every((step, index) =>
              recordedSteps.indexOf(step) >= 0 &&
              (index === 0 || recordedSteps.indexOf(step) > recordedSteps.indexOf(expectedSteps[index - 1])));
            const byStep = Object.fromEntries(state.events.map((entry) => [entry.step, entry]));
            const button = (step, name) => byStep[step]?.buttons.find((entry) => entry.name === name);
            const validUiState = byStep.playing?.is_playing === true &&
              byStep.paused?.is_playing === false &&
              byStep.recording?.is_recording === true &&
              byStep.recording_stopped?.is_recording === false &&
              byStep.invalid_input_error?.session_state === "Failed" &&
              byStep.invalid_input_error?.progress_text.includes("오류") &&
              button("playing", "FBX_PlayPause_Button")?.label === "일시정지" &&
              button("paused", "FBX_PlayPause_Button")?.label === "재생" &&
              button("recording", "FBX_Record_Button")?.label === "녹화 중지";
            const screenshots = state.events.filter((entry) => entry.game_view_path);
            for (const entry of screenshots) {
              const imagePath = path.resolve(entry.game_view_path);
              const relativeImagePath = path.relative(allowedRoot, imagePath);
              if (relativeImagePath.startsWith("..") || path.isAbsolute(relativeImagePath))
                throw new Error("F15 Game View 경로가 실행 폴더를 벗어났습니다.");
              const startedImageWait = Date.now();
              while (!(await readOptional(imagePath)) && Date.now() - startedImageWait < 5000)
                await new Promise((resolve) => setTimeout(resolve, 250));
              if ((await stat(imagePath).catch(() => ({ size: 0 }))).size === 0)
                throw new Error("F15 Game View 캡처가 생성되지 않았습니다.");
            }
            const valid = state.status === "manual_review_required" && validSteps && validUiState &&
              state.input === "Snake Hip Hop Dance.fbx" && state.model === "YYB Hatsune Miku" &&
              state.auto_vmd_recording_suppressed === true && screenshots.length >= 3 &&
              screenshots.every((entry) => entry.game_view_path &&
                !entry.progress_text.includes("�")) &&
              state.events.every((entry) => entry.buttons.length === 4);
            result = valid ? { status: "MANUAL_REVIEW_REQUIRED" } :
              { status: "FAIL", failureKind: "test_failure" };
            failureStage = valid ? "" : "evidence";
          }
        }
      }
    }
  } catch (error) {
    failureStage = "infrastructure";
    result = { status: "INFRA_ERROR", message: error.message };
  } finally {
    if (submitted && terminal) {
      const pending = await readOptional(requestPath);
      if (pending && JSON.parse(pending.toString("utf8")).request_id === requestId)
        await rm(requestPath);
    }
    if (enteredPlay && (!submitted || terminal)) {
      const exit = await executeControl(runId, exitPlayCommand);
      steps.push({ name: "exit_play", status: exit.status, requestId: exit.requestId });
      if (exit.status !== "PASS") result = { status: "INFRA_ERROR" };
    }
    if (!submitted || terminal) {
      const final = await executeControl(runId, environmentCommand);
      steps.push({ name: "after", status: final.status, requestId: final.requestId });
      after = final.state;
      if (!before || final.status !== "PASS" ||
          !environmentFields.every((field) =>
            JSON.stringify(before[field]) === JSON.stringify(after?.[field])) ||
          vmdBefore !== await hashFile(outputPath) ||
          vmdMetaBefore !== await hashFile(`${outputPath}.meta`))
        result = { status: "INFRA_ERROR" };
    }
    await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
      runId, requestId, result: result.status, failureStage, steps, before, after,
      unityStatus, statePath: unityStatus?.manifest_path || null,
      eventSteps: state?.events?.map((entry) => entry.step) || [],
      screenshotCount: state?.events?.filter((entry) => entry.game_view_path).length || 0
    }, null, 2));
  }
  return { ...result, requestId };
}

async function executeFullRegression(runId, namedOutput = false) {
  const sessionRoot = path.join(evidenceRoot,
    namedOutput ? "full-output-runs" : "full-regression-runs", runId);
  await mkdir(sessionRoot, { recursive: true });
  const steps = [];
  let before = null;
  let after = null;
  let smoke = null;
  let vrm = null;
  let enteredPlay = false;
  let result = { status: "INFRA_ERROR" };
  try {
    const baseline = await executeControl(runId, environmentCommand);
    steps.push({ name: "before", status: baseline.status, requestId: baseline.requestId });
    before = baseline.state;
    if (baseline.status !== "PASS" || !before || before.play_mode ||
        before.scene_path !== "Assets/_Project/Scene/Main_Auto.unity" ||
        before.scene_dirty || before.model_name !== "YYB Hatsune Miku" ||
        !before.model_active || !before.avatar_valid || before.is_processing ||
        before.has_prepared_motion || before.is_recording ||
        before.capture_framerate !== 0) {
      result = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      const enter = await executeControl(runId, enterPlayCommand);
      steps.push({ name: "enter_play", status: enter.status, requestId: enter.requestId });
      enteredPlay = enter.status === "PASS";
      result = enter;
      if (enteredPlay) {
        smoke = await executeSmoke(runId, {
          command: namedOutput ? fullNamedVmdCommand : fullRegressionCommand,
          outputPath: namedOutput ? outputPath : fullRegressionOutputPath,
          frameCount: 6234,
          folder: namedOutput ? "full-named-vmd-output" : "full-regression-output",
          protectedPrefix: namedOutput ? "satisfaction_2" : "smoke_satisfaction_2_208s",
          timeoutMs: 3600000,
          visualReview: true
        });
        steps.push({ name: namedOutput ? "F12_VMD" : "F10_208s",
          status: smoke.status, requestId: smoke.requestId });
        result = smoke;
      }
    }
  } catch (error) {
    result = { status: "INFRA_ERROR", message: error.message };
  } finally {
    if (enteredPlay && (!smoke || smoke.terminal)) {
      const exit = await executeControl(runId, exitPlayCommand);
      steps.push({ name: "exit_play", status: exit.status, requestId: exit.requestId });
      if (exit.status !== "PASS") result = { status: "INFRA_ERROR" };
    }
    if (namedOutput && smoke?.terminal && smoke.status === "MANUAL_REVIEW_REQUIRED" &&
        steps.some((step) => step.name === "exit_play" && step.status === "PASS")) {
      vrm = await executeVrmOutput(runId);
      steps.push({ name: "F12_VRM", status: vrm.status, requestId: vrm.requestId });
      if (vrm.status !== "MANUAL_REVIEW_REQUIRED") result = { status: vrm.status };
    }
    if (!enteredPlay || !smoke || smoke.terminal) {
      const final = await executeControl(runId, environmentCommand);
      steps.push({ name: "after", status: final.status, requestId: final.requestId });
      after = final.state;
      if (!before || final.status !== "PASS" || !environmentFields.every((field) =>
        JSON.stringify(before[field]) === JSON.stringify(after?.[field])) ||
        smoke && !smoke.restored || vrm && !vrm.restored)
        result = { status: "INFRA_ERROR" };
    }
    await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
      runId, result: result.status, steps, before, after, smokeRequestId: smoke?.requestId,
      smokeTerminal: smoke?.terminal ?? null, restored: smoke?.restored ?? null,
      ...(namedOutput ? { vrm: vrm ?? { status: "SKIP",
        reason: "F12 실행 전 준비 단계에서 중단" },
        manualReference: { status: "NOT_COMPARABLE",
          reason: "동일 모델·Avatar·녹화 구간의 수동 VMD 기준이 확인되지 않음" } } : {})
    }, null, 2));
  }
  return result;
}

async function executeManualComparison(runId) {
  const sessionRoot = path.join(evidenceRoot, "manual-comparison-runs", runId);
  await mkdir(sessionRoot, { recursive: true });
  const manualScenePath = path.join(projectRoot, "Assets/_Project/Scene/Sub_Manual_F13.unity");
  const automaticScenePath = path.join(projectRoot, "Assets/_Project/Scene/Main_Auto.unity");
  const manualPrefabPath = path.join(projectRoot,
    "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku.prefab");
  const automaticPrefabPath = path.join(projectRoot,
    "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku.prefab");
  const [manualScene, automaticScene, manualPrefab, automaticPrefab,
    manualMeta, automaticMeta] = await Promise.all([
    manualScenePath, automaticScenePath, manualPrefabPath, automaticPrefabPath,
    `${manualPrefabPath}.meta`, `${automaticPrefabPath}.meta`
  ].map((file) => readFile(file, "utf8")));
  const guid = (source) => source.match(/^guid:\s*([0-9a-f]{32})/m)?.[1] || null;
  const avatar = (source) => source.match(/^\s*m_Avatar: \{fileID: \d+(?:, guid: ([0-9a-f]{32}))?/m)?.[1] || null;
  const effectiveAvatar = (scene, prefab, prefabGuid) =>
    scene.match(new RegExp(`- target: \\{fileID: \\d+, guid: ${prefabGuid}, type: \\d+\\}` +
      "\\s+propertyPath: m_Avatar\\s+value:\\s+objectReference: " +
      "\\{fileID: \\d+, guid: ([0-9a-f]{32})"))?.[1] || avatar(prefab);
  const camera = (source) => {
    const blocks = source.split(/^--- !u!/m);
    const gameObject = blocks.find((block) => /^1 &\d+/m.test(block) &&
      /^\s*m_Name: Main Camera\s*$/m.test(block));
    const gameObjectId = gameObject?.match(/^1 &(\d+)/)?.[1];
    const ownsCamera = (block, type) => gameObjectId && block.startsWith(`${type} &`) &&
      block.includes(`m_GameObject: {fileID: ${gameObjectId}}`);
    const lens = blocks.find((block) => ownsCamera(block, 20));
    const transform = blocks.find((block) => ownsCamera(block, 4));
    if (!lens || !transform) return null;
    const field = (block, name) => block.match(new RegExp(`^\\s*${name}: (.+)$`, "m"))?.[1] || null;
    return {
      position: field(transform, "m_LocalPosition"),
      rotation: field(transform, "m_LocalRotation"),
      orthographic: field(lens, "orthographic"),
      orthographicSize: field(lens, "orthographic size"),
      fieldOfView: field(lens, "field of view")
    };
  };
  const manual = { prefabGuid: guid(manualMeta),
    avatarGuid: effectiveAvatar(manualScene, manualPrefab, guid(manualMeta)),
    camera: camera(manualScene) };
  const automatic = { prefabGuid: guid(automaticMeta),
    avatarGuid: effectiveAvatar(automaticScene, automaticPrefab, guid(automaticMeta)),
    camera: camera(automaticScene) };
  manual.sceneContainsPrefab = manualScene.includes(`guid: ${manual.prefabGuid}`);
  automatic.sceneContainsPrefab = automaticScene.includes(`guid: ${automatic.prefabGuid}`);
  const sameCamera = (left, right) => left && right &&
    ["position", "rotation", "orthographic", "orthographicSize", "fieldOfView"]
      .every((field) => {
        const numbers = (value) => (value?.match(/[-+]?(?:\d+\.?\d*|\.\d+)(?:e[-+]?\d+)?/gi) || [])
          .map(Number);
        const a = numbers(left[field]);
        const b = numbers(right[field]);
        return a.length > 0 && a.length === b.length &&
          a.every((value, index) => Math.abs(value - b[index]) < 0.00001);
      });
  const differences = [];
  for (const field of ["prefabGuid", "avatarGuid", "sceneContainsPrefab"]) {
    if (!manual[field] || !automatic[field] ||
        JSON.stringify(manual[field]) !== JSON.stringify(automatic[field]))
      differences.push({ field, manual: manual[field], automatic: automatic[field] });
  }
  if (!sameCamera(manual.camera, automatic.camera))
    differences.push({ field: "camera", manual: manual.camera,
      automatic: automatic.camera });
  const manifest = { runId, manualReference: "Sub_Manual_F13 controlled fixture",
    manual, automatic, differences,
    inputFbxSha256: await hashFile(fbxPath) };
  let result = { status: "NOT_COMPARABLE", reason: "모델·Avatar·카메라 조건이 다름" };
  if (!differences.length) {
    const visualRequestPath = path.join(runtimeRoot, "yyb_visual_compare_request.json");
    const visualStatusPath = path.join(runtimeRoot, "yyb_visual_compare_status.json");
    const outputDirectory = path.join(projectRoot, "Assets/VMDRecorderSample");
    const prefixes = ["testPrefab_satisfaction_2_", "yyb_satisfaction_2_",
      "smoke_satisfaction_2_31s"];
    const protectedNames = () => readdir(outputDirectory).then((names) =>
      names.filter((name) => prefixes.some((prefix) => name.startsWith(prefix))));
    const before = await executeControl(runId, environmentCommand);
    manifest.before = before.state;
    if (before.status !== "PASS" || before.state?.play_mode ||
        before.state?.scene_path !== "Assets/_Project/Scene/Main_Auto.unity" ||
        before.state?.scene_dirty || await readOptional(visualRequestPath) ||
        (await readOptional(visualStatusPath))?.toString("utf8").includes('"status": "running"')) {
      result = { status: "BLOCKED", reason: "Unity 편집기 또는 비교 요청이 준비되지 않음" };
    } else {
      const priorNames = await protectedNames();
      for (const name of priorNames)
        await copyFile(path.join(outputDirectory, name), path.join(sessionRoot, `prior-${name}`));
      const requestId = randomUUID();
      let settled = false;
      let visualStatus = null;
      try {
        await writeFile(visualRequestPath, JSON.stringify({ request_id: requestId,
          fbx_file: "satisfaction_2.fbx", duration_seconds: 3,
          finger_closeups: false, f13_pair_only: true }), { flag: "wx" });
        const start = Date.now();
        while (Date.now() - start < 1200000) {
          await new Promise((resolve) => setTimeout(resolve, 1000));
          const content = await readOptional(visualStatusPath);
          if (!content) continue;
          try { visualStatus = JSON.parse(content.toString("utf8")); }
          catch (error) { if (error instanceof SyntaxError) continue; throw error; }
          if (visualStatus.request_id !== requestId) continue;
          if (["completed", "failed"].includes(visualStatus.status)) {
            settled = true;
            break;
          }
        }
        manifest.visualRequestId = requestId;
        manifest.visualStatus = visualStatus;
        if (!settled) result = { status: "TIMED_OUT", reason: "Unity 비교 실행 종료 미확인" };
        else if (!visualStatus.summary_json_path)
          result = { status: "BLOCKED", reason: "새 Unity 비교 요약이 없음" };
        else {
          const summaryPath = path.resolve(projectRoot, visualStatus.summary_json_path);
          const relative = path.relative(path.join(projectRoot, "Docs/Workflow/Local"), summaryPath);
          if (!relative || relative.startsWith("..") || path.isAbsolute(relative))
            throw new Error("비교 요약 경로가 로컬 근거 폴더 밖입니다.");
          const summaryText = (await readFile(summaryPath, "utf8")).replace(/^\uFEFF/, "")
            .replace(/^(\s*"[^"]+":\s*)NaN(?=\s*[,}])/gm, "$1null");
          const summary = JSON.parse(summaryText);
          manifest.inputFbxSha256After = await hashFile(fbxPath);
          result = summary.fbx_file === "satisfaction_2.fbx" &&
              manifest.inputFbxSha256After === manifest.inputFbxSha256
            ? await compareManualCapture(summary, projectRoot)
            : { status: "NOT_COMPARABLE", reason: "Unity 비교 입력 FBX가 다름" };
          manifest.summaryPath = visualStatus.summary_json_path;
        }
      } catch (error) {
        result = { status: "INFRA_ERROR", reason: error.message };
      } finally {
        if (settled) {
          try {
            for (const name of await protectedNames()) {
              if (!priorNames.includes(name)) await rm(path.join(outputDirectory, name));
            }
            for (const name of priorNames)
              await copyFile(path.join(sessionRoot, `prior-${name}`), path.join(outputDirectory, name));
            manifest.outputsRestored = (await Promise.all(priorNames.map(async (name) =>
              await hashFile(path.join(outputDirectory, name)) ===
                await hashFile(path.join(sessionRoot, `prior-${name}`))))).every(Boolean) &&
              (await protectedNames()).length === priorNames.length;
            const after = await executeControl(runId, environmentCommand);
            manifest.after = after.state;
            manifest.environmentRestored = after.status === "PASS" &&
              environmentFields.every((field) =>
                JSON.stringify(before.state?.[field]) === JSON.stringify(after.state?.[field]));
            if (!manifest.environmentRestored || !manifest.outputsRestored)
              result = { status: "INFRA_ERROR", reason: "실행 전후 상태 복원 불일치" };
          } catch (error) {
            result = { status: "INFRA_ERROR", reason: `복원 실패: ${error.message}` };
          }
        }
      }
    }
  }
  manifest.result = result.status;
  manifest.reason = result.reason;
  if (result.frames) manifest.comparison = result;
  await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify(manifest, null, 2));
  return { status: result.status };
}

async function executeSegments(runId) {
  const sessionRoot = path.join(evidenceRoot, "segment-runs", runId);
  await mkdir(sessionRoot, { recursive: true });
  const steps = [];
  let before = null;
  let after = null;
  let enteredPlay = false;
  let result = { status: "INFRA_ERROR" };
  try {
    const baseline = await executeControl(runId, environmentCommand);
    steps.push({ name: "before", status: baseline.status, requestId: baseline.requestId });
    before = baseline.state;
    if (baseline.status !== "PASS" || !before || before.play_mode ||
        before.scene_path !== "Assets/_Project/Scene/Main_Auto.unity" ||
        before.scene_dirty || before.model_name !== "YYB Hatsune Miku" ||
        !before.model_active || !before.avatar_valid || before.is_processing ||
        before.has_prepared_motion || before.is_recording ||
        before.capture_framerate !== 0) {
      result = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      const enter = await executeControl(runId, enterPlayCommand);
      steps.push({ name: "enter_play", status: enter.status, requestId: enter.requestId });
      enteredPlay = enter.status === "PASS";
      result = enter;
      if (enteredPlay) {
        for (const [name, segmentCommand, prefix] of segmentCases) {
          const step = await executeSmoke(runId, {
            command: segmentCommand,
            outputPath: path.join(projectRoot, "Assets/VMDRecorderSample", `${prefix}.vmd`),
            frameCount: 930,
            folder: `segment-${name}`,
            protectedPrefix: prefix,
            timeoutMs: 900000,
            visualReview: true
          });
          steps.push({ name, status: step.status, requestId: step.requestId,
            terminal: step.terminal, restored: step.restored });
          result = step;
          if (step.status !== "MANUAL_REVIEW_REQUIRED") break;
        }
      }
    }
  } catch (error) {
    result = { status: "INFRA_ERROR", message: error.message };
  } finally {
    const requestActive = !!(await readOptional(requestPath)) ||
      (await readStatus())?.status === "running";
    if (enteredPlay && !requestActive) {
      const exit = await executeControl(runId, exitPlayCommand);
      steps.push({ name: "exit_play", status: exit.status, requestId: exit.requestId });
      if (exit.status !== "PASS") result = { status: "INFRA_ERROR" };
    }
    if (before && !requestActive) {
      const final = await executeControl(runId, environmentCommand);
      steps.push({ name: "after", status: final.status, requestId: final.requestId });
      after = final.state;
      if (final.status !== "PASS" || !environmentFields.every((field) =>
        JSON.stringify(before[field]) === JSON.stringify(after?.[field])) ||
        steps.some((step) => step.restored === false)) result = { status: "INFRA_ERROR" };
    }
    await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
      runId, result: result.status, steps, before, after, requestActive
    }, null, 2));
  }
  return result;
}

async function executeSuite(runId) {
  const sessionRoot = path.join(evidenceRoot, "suite-runs", runId);
  await mkdir(sessionRoot, { recursive: true });
  const steps = [];
  const outputHashBefore = await hashFile(outputPath);
  const metaHashBefore = await hashFile(`${outputPath}.meta`);
  let before = null;
  let after = null;
  let enteredPlay = false;
  let result = { status: "INFRA_ERROR" };
  let restored = false;
  let cleanupError = null;
  try {
    const baseline = await executeControl(runId, environmentCommand);
    steps.push({ name: "F08_before", status: baseline.status, requestId: baseline.requestId });
    before = baseline.state;
    if (baseline.status !== "PASS" || !before || before.play_mode ||
        before.scene !== "Main_Auto" ||
        before.scene_path !== "Assets/_Project/Scene/Main_Auto.unity" ||
        before.scene_dirty || before.model_name !== "YYB Hatsune Miku" ||
        !before.model_active ||
        !before.avatar_valid || before.is_processing || before.has_prepared_motion ||
        before.is_recording || before.recorder_recording || before.capture_framerate !== 0) {
      result = { status: "BLOCKED", failureKind: "preflight" };
    } else {
      const enter = await executeControl(runId, enterPlayCommand);
      steps.push({ name: "F08_enter_play", status: enter.status, requestId: enter.requestId });
      enteredPlay = enter.status === "PASS";
      result = enter.status === "PASS" ? { status: "PASS" } : enter;
      if (enteredPlay) {
        for (const [name, run, allowed] of [
          ["F01", executePreselection, ["MANUAL_REVIEW_REQUIRED"]],
          ["F07", executeInvalidInput, ["PASS"]],
          ["F02_F03_F04_F05_F11", executePlayback, ["MANUAL_REVIEW_REQUIRED"]],
          ["F06", executeSmoke, ["PASS"]]
        ]) {
          const step = await run(runId);
          steps.push({ name, status: step.status, requestId: step.requestId });
          if (!allowed.includes(step.status)) {
            result = step;
            break;
          }
          if (step.status === "MANUAL_REVIEW_REQUIRED")
            result = { status: "MANUAL_REVIEW_REQUIRED" };
        }
      }
    }
  } catch (error) {
    result = { status: "INFRA_ERROR" };
    cleanupError = error.message;
  } finally {
    try {
      const pending = await readOptional(requestPath);
      const latest = await readStatus();
      const needsRecovery = steps.some((step) => step.status === "TIMED_OUT");
      if (enteredPlay && !needsRecovery && !pending && latest?.status !== "running") {
        const exit = await executeControl(runId, exitPlayCommand);
        steps.push({ name: "F08_exit_play", status: exit.status, requestId: exit.requestId });
        if (exit.status !== "PASS") result = exit;
      }
      if (!needsRecovery && !await readOptional(requestPath) &&
          (await readStatus())?.status !== "running") {
        const finalState = await executeControl(runId, environmentCommand);
        steps.push({ name: "F08_after", status: finalState.status, requestId: finalState.requestId });
        after = finalState.state;
      }
      const stateRestored = !!before && !!after && environmentFields.every((field) =>
        JSON.stringify(before[field]) === JSON.stringify(after[field]));
      const outputHashAfter = await hashFile(outputPath);
      const metaHashAfter = await hashFile(`${outputPath}.meta`);
      restored = stateRestored && outputHashBefore === outputHashAfter &&
        metaHashBefore === metaHashAfter;
      if (enteredPlay && !restored && result.status !== "TIMED_OUT")
        result = { status: "INFRA_ERROR" };
      await writeFile(path.join(sessionRoot, "manifest.json"), JSON.stringify({
        runId, result: result.status, restored, steps, before, after,
        outputHashBefore, outputHashAfter, metaHashBefore, metaHashAfter,
        cleanupError, pendingRequest: !!(await readOptional(requestPath))
      }, null, 2));
    } catch (error) {
      result = { status: "INFRA_ERROR" };
      await writeFile(path.join(sessionRoot, "cleanup-error.json"), JSON.stringify({
        runId, message: error.message, steps
      }, null, 2));
    }
  }
  if (!restored && before && steps.some((step) =>
      step.name === "F08_enter_play" && step.status !== "BLOCKED")) {
    try {
      const recovery = await recoverSuite(runId);
      if (recovery.restored) restored = true;
    } catch (error) {
      await writeFile(path.join(sessionRoot, "recovery-error.json"), JSON.stringify({
        runId, message: error.message
      }, null, 2));
    }
  }
  return result;
}

async function recoverSuite(runId) {
  if (!/^[0-9a-f]{8}-[0-9a-f-]{27,}$/i.test(runId))
    throw new Error("복구할 SDK 실행 ID가 유효하지 않습니다.");
  const sessionRoot = path.join(evidenceRoot, "suite-runs", runId);
  const manifestPath = path.join(sessionRoot, "manifest.json");
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  if (manifest.restored) return { status: "PASS", restored: true };
  const smokeManifestPath = path.join(evidenceRoot, "product-smoke", runId, "manifest.json");
  const smokeContent = await readOptional(smokeManifestPath);
  const smoke = smokeContent ? JSON.parse(smokeContent.toString("utf8")) : null;
  const ownedIds = new Set(manifest.steps.map((step) => step.requestId).filter(Boolean));
  if (smoke?.requestId) ownedIds.add(smoke.requestId);
  let pending = await readOptional(requestPath);
  if (pending && !ownedIds.has(JSON.parse(pending.toString("utf8")).request_id))
    return { status: "BLOCKED", restored: false };
  const startedAt = Date.now();
  while (pending && Date.now() - startedAt < 180000) {
    await new Promise((resolve) => setTimeout(resolve, 500));
    pending = await readOptional(requestPath);
  }
  if (pending) return { status: "TIMED_OUT", restored: false };
  const latest = await readStatus();
  if (latest?.status === "running" || !ownedIds.has(latest?.request_id))
    return { status: "BLOCKED", restored: false };
  const timedOut = [...manifest.steps].reverse().find((step) => step.status === "TIMED_OUT");
  if (timedOut && latest.request_id !== timedOut.requestId)
    return { status: "BLOCKED", restored: false };

  if (smoke && !smoke.restored) {
    if (smoke.backupPath && await hashFile(smoke.backupPath) !== manifest.outputHashBefore)
      return { status: "INFRA_ERROR", restored: false };
    const metaBackup = path.join(evidenceRoot, "product-smoke", runId, "prior-output.vmd.meta");
    if (manifest.metaHashBefore !== "missing" &&
        await hashFile(metaBackup) !== manifest.metaHashBefore)
      return { status: "INFRA_ERROR", restored: false };
    for (const [target, backup, expected] of [
      [outputPath, smoke.backupPath, manifest.outputHashBefore],
      [`${outputPath}.meta`, manifest.metaHashBefore === "missing" ? null : metaBackup,
        manifest.metaHashBefore]
    ]) {
      if (await hashFile(target) !== expected) {
        if (await readOptional(target))
          await copyFile(target, path.join(sessionRoot,
            `late-output-${randomUUID()}${target.endsWith(".meta") ? ".vmd.meta" : ".vmd"}`));
        if (backup) await copyFile(backup, target);
        else await rm(target, { force: true });
      }
    }
    smoke.restored = await hashFile(outputPath) === manifest.outputHashBefore &&
      await hashFile(`${outputPath}.meta`) === manifest.metaHashBefore;
    await writeFile(smokeManifestPath, JSON.stringify(smoke, null, 2));
    if (!smoke.restored) return { status: "INFRA_ERROR", restored: false };
  }

  const current = await executeControl(runId, environmentCommand);
  if (current.status !== "PASS") return { status: current.status, restored: false };
  if (current.state.play_mode) {
    const exit = await executeControl(runId, exitPlayCommand);
    manifest.steps.push({ name: "F08_recovery_exit_play", status: exit.status,
      requestId: exit.requestId });
    if (exit.status !== "PASS") {
      await writeFile(manifestPath, JSON.stringify(manifest, null, 2));
      return { status: exit.status, restored: false };
    }
  }
  const finalState = await executeControl(runId, environmentCommand);
  manifest.steps.push({ name: "F08_recovery_after", status: finalState.status,
    requestId: finalState.requestId });
  manifest.after = finalState.state;
  manifest.outputHashAfter = await hashFile(outputPath);
  manifest.metaHashAfter = await hashFile(`${outputPath}.meta`);
  manifest.restored = finalState.status === "PASS" && environmentFields.every((field) =>
    JSON.stringify(manifest.before?.[field]) === JSON.stringify(manifest.after?.[field])) &&
    manifest.outputHashBefore === manifest.outputHashAfter &&
    manifest.metaHashBefore === manifest.metaHashAfter;
  manifest.recovery = { status: manifest.restored ? "PASS" : "INFRA_ERROR",
    completedAt: new Date().toISOString() };
  await writeFile(manifestPath, JSON.stringify(manifest, null, 2));
  return { status: manifest.recovery.status, restored: manifest.restored };
}

async function main() {
  const mode = process.argv[2] || "smoke";
  if (!["smoke", "preselection", "playback", "foot-live", "full-clip", "full-regression", "full-output", "vrm-output", "manual-compare", "alternate-model", "product-ui", "segments", "invalid-input", "environment", "suite", "recover"].includes(mode) ||
      process.argv.length > (mode === "smoke" ? 2 : mode === "recover" ? 4 : 3)) {
    throw new Error("사용법: node Tools/Boogle/run-product-smoke.mjs [preselection|playback|foot-live|full-clip|full-regression|full-output|vrm-output|manual-compare|alternate-model|product-ui|segments|invalid-input|environment|suite|recover <runId>]");
  }
  if (Number(process.versions.node.split(".")[0]) !== 24) {
    throw new Error("Node.js 24가 필요합니다.");
  }
  if (mode === "recover") {
    const recovery = await recoverSuite(process.argv[3] || "");
    process.stdout.write(`${JSON.stringify({ runId: process.argv[3], ...recovery })}\n`);
    process.exitCode = recovery.status === "PASS" ? 0 : 1;
    return;
  }
  await access(sdkRunnerPath);
  const unityVersion = (await readFile(
    path.join(projectRoot, "ProjectSettings/ProjectVersion.txt"), "utf8"
  )).match(/^m_EditorVersion:\s*(\S+)/m)?.[1] || "unknown";
  const fbxHash = await hashFile(mode === "foot-live" || mode === "alternate-model"
    ? path.join(projectRoot, "Assets/Resources/Import_FBX/tetoris_001.fbx") :
    mode === "product-ui"
      ? path.join(projectRoot, "Assets/Resources/Import_FBX/Snake Hip Hop Dance.fbx")
      : fbxPath);
  const modelHash = await hashFile(path.join(
    projectRoot, mode === "alternate-model"
      ? "Assets/Plugins/VMDRecorderSample/Models/TestModel/testPrefab.prefab"
      : "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx"
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
    testPack: { id: "fbx2vmd-product-smoke", version: "0.1.3" },
    retries: 0,
    inputConditions: {
      testCaseIds: mode === "suite" ? "F01,F02,F03,F04,F05,F06,F07,F08,F11" :
        mode === "environment" ? "F08" : mode === "foot-live" ? "F09" :
        mode === "full-output" ? "F12" :
        mode === "vrm-output" ? "F12" :
        mode === "manual-compare" ? "F13" :
        mode === "alternate-model" ? "F14" :
        mode === "product-ui" ? "F15" :
        mode === "full-clip" || mode === "full-regression" || mode === "segments" ? "F10" :
        mode === "preselection" ? "F01" : mode === "playback" ? "F02,F03,F04,F05,F11" :
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
      return mode === "suite" ? await executeSuite(runId)
        : mode === "environment" ? await executeControl(runId, environmentCommand)
        : mode === "preselection" ? await executePreselection(runId)
        : mode === "playback" ? await executePlayback(runId)
        : mode === "foot-live" ? await executeFootLive(runId)
        : mode === "full-clip" ? await executeFullClip(runId)
        : mode === "alternate-model" ? await executeFullClip(runId, true)
        : mode === "product-ui" ? await executeProductUi(runId)
        : mode === "full-regression" ? await executeFullRegression(runId)
        : mode === "full-output" ? await executeFullRegression(runId, true)
        : mode === "vrm-output" ? await executeVrmOutput(runId)
        : mode === "manual-compare" ? await executeManualComparison(runId)
        : mode === "segments" ? await executeSegments(runId)
          : mode === "invalid-input" ? await executeInvalidInput(runId)
            : await executeSmoke(runId);
    } catch (error) {
      const sessionRoot = path.join(evidenceRoot,
        mode === "suite" ? "suite-runs" :
        mode === "environment" ? "control-runs" :
        mode === "preselection" ? "preselection-runs" :
          mode === "playback" ? "playback-runs" :
            mode === "foot-live" ? "foot-live-runs" :
            mode === "full-clip" ? "full-clip-runs" :
            mode === "full-regression" ? "full-regression-runs" :
            mode === "full-output" ? "full-output-runs" :
            mode === "manual-compare" ? "manual-comparison-runs" :
            mode === "alternate-model" ? "alternate-model-runs" :
            mode === "product-ui" ? "product-ui-runs" :
            mode === "segments" ? "segment-runs" :
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
