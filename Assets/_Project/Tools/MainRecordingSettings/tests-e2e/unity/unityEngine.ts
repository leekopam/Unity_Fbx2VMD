import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { defineEngine, EngineError } from "e2e/engine";
import type {
  Engine,
  EngineAppDeclaration,
  EngineAppCheckInfo,
  EngineCleanupContext,
  EngineSnapshot,
  OperationContext,
  SemanticNode,
  ViewportSize
} from "e2e/engine";

import { createUnityClient, UnityBusyError, UnityCommandFailedError, UNITY_COMMANDS, type UnityClient } from "./unityClient.js";

// tester-army/e2e 커스텀 엔진 — Unity 에디터를 vision-only 표면으로 구동한다.
// 관측은 Game View 픽셀(capture_e2e_frame)만 제공하고, 검증은
// capture_e2e_environment의 환경 필드와 계측 fixture가 담당한다.
// Unity 에디터는 e2e가 띄우지 않는다: 사용자가 열어둔 에디터의
// FbxPlaybackSmokeRunner 폴러가 요청을 소비한다(run-product-smoke.mjs와 동일 전제).

const packageRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const unityProjectRoot = path.resolve(packageRoot, "..", "..", "..", "..");
const ENGINE_VERSION = "0.1.0";

export interface UnityEngineOptions {
  /** Unity 프로젝트 루트. 기본은 이 패키지가 속한 Unity 프로젝트. */
  projectRoot?: string;
  /** 명령 픽업 대기 예산(ms). 첫 요청은 에디터 임포트로 느릴 수 있다. */
  pickupTimeoutMs?: number;
}

export function unityEngine(options?: UnityEngineOptions) {
  const projectRoot = path.resolve(options?.projectRoot ?? unityProjectRoot);
  const client: UnityClient = createUnityClient({
    projectRoot,
    pickupTimeoutMs: options?.pickupTimeoutMs
  });

  // startAttempt에서 받은 attempt 산출물 디렉터리 — screenshot/video가 여기에 쓴다.
  let artifactsDir: string | undefined;
  // 가장 최근 observe가 본 Game View 크기(CSS 픽셀 = 물리 픽셀, scale 1).
  let lastViewport: ViewportSize = { width: 0, height: 0 };
  // attempt마다 관측한 환경 상태(없으면 observe 전 캡처하지 않는다).
  let lastEnvironment: Record<string, unknown> | undefined;
  // attempt 시작 시점의 복원 기준선 — endAttempt가 이와 비교해 상태를 되돌린다.
  // 부분 캡처만 성공해도 저장한다 — undefined면 복원 자체가 생략되기 때문이다.
  let restoreBaseline: {
    environment: Record<string, unknown> | undefined;
    outputs: Array<{ target: string; backup: string | null; sha256: string }>;
    error?: string;
  } | undefined;
  // artifacts.startVideo는 Edit Mode에서도 호출될 수 있다 — Play 진입까지 보류한다.
  let videoPending = false;
  let activeVideo: { outputPath: string; startedAt: string } | undefined;

  const PROTECTED_OUTPUTS = [
    "Assets/VMDRecorderSample/satisfaction_2.vmd",
    "Assets/VMDRecorderSample/satisfaction_2.vmd.meta",
    // F12(capture_satisfaction_vrm)가 같은 폴더의 VRM을 덮어쓴다 —
    // 백업·복원 대상에 포함하지 않으면 사용자 산출물이 파괴된다.
    "Assets/VMDRecorderSample/satisfaction_2.vrm",
    "Assets/VMDRecorderSample/satisfaction_2.vrm.meta"
  ];

  async function captureEnvironment(context: OperationContext): Promise<Record<string, unknown>> {
    const status = await runCommand(UNITY_COMMANDS.environment, context);
    const statePath = status.environment_state_path;
    if (typeof statePath !== "string" || statePath.length === 0) {
      throw new EngineError("ENGINE_FAILURE", "capture_e2e_environment가 상태 경로를 반환하지 않았습니다.", { retryable: false });
    }
    return parseUnityJson(await fs.promises.readFile(statePath, "utf8"));
  }

  async function backupOutputs(directory: string) {
    const entries: Array<{ target: string; backup: string | null; sha256: string }> = [];
    for (const relative of PROTECTED_OUTPUTS) {
      const target = path.join(projectRoot, relative);
      const sha256 = await hashOptionalFile(target);
      let backup: string | null = null;
      if (sha256 !== "missing") {
        backup = path.join(directory, "restore", path.basename(relative));
        await fs.promises.mkdir(path.dirname(backup), { recursive: true });
        await fs.promises.copyFile(target, backup);
      }
      entries.push({ target, backup, sha256 });
    }
    return entries;
  }

  /** attempt 종료 시 편집기 상태와 보호 출력물을 기준선으로 되돌린다. */
  async function restoreBaselineState(signal: AbortSignal): Promise<string[]> {
    if (!restoreBaseline) return ["복원 기준선이 없습니다 — attempt 시작 시 캡처가 없었습니다."];
    const problems: string[] = [];
    if (restoreBaseline.error) {
      problems.push(`기준선 캡처 부분 실패: ${restoreBaseline.error}`);
    }
    const context: OperationContext = {
      signal, timeoutMs: 120_000, runId: "cleanup", attemptId: "cleanup", origin: "test"
    };

    const current = await captureEnvironment(context).catch(() => null);
    if (current && restoreBaseline.environment) {
      const baseline = restoreBaseline.environment;
      if (current.play_mode === true && baseline.play_mode !== true) {
        await runCommand(UNITY_COMMANDS.exitPlay, context, undefined, 90_000).catch((error) => {
          problems.push(`play 종료 실패: ${error instanceof Error ? error.message : error}`);
        });
      } else if (current.play_mode !== true && baseline.play_mode === true) {
        await runCommand(UNITY_COMMANDS.enterPlay, context, undefined, 90_000).catch((error) => {
          problems.push(`play 재진입 실패: ${error instanceof Error ? error.message : error}`);
        });
      }
      if (current.scene_path && baseline.scene_path && current.scene_path !== baseline.scene_path) {
        problems.push(`씬 변경 감지: ${baseline.scene_path} → ${current.scene_path}`);
      }
      if (current.scene_dirty === true && baseline.scene_dirty !== true) {
        problems.push("씬이 dirty 상태로 남았습니다(저장되지 않은 변경)");
      }
    } else {
      problems.push("복원 검증용 환경 상태를 읽지 못했습니다.");
    }

    for (const entry of restoreBaseline.outputs) {
      const currentSha = await hashOptionalFile(entry.target);
      if (currentSha === entry.sha256) continue;
      if (entry.backup === null) {
        await fs.promises.rm(entry.target, { force: true });
      } else {
        await fs.promises.copyFile(entry.backup, entry.target);
      }
      const after = await hashOptionalFile(entry.target);
      if (after !== entry.sha256) {
        problems.push(`출력물 복원 실패: ${path.basename(entry.target)}`);
      }
    }
    return problems;
  }

  async function runCommand(
    command: string,
    context: OperationContext,
    fields?: Record<string, unknown>,
    timeoutMs?: number
  ) {
    try {
      const status = await client.send(command, {
        fields,
        signal: context.signal,
        timeoutMs: timeoutMs ?? context.timeoutMs
      });
      // Play 진입이 성공한 직후 보류 중인 영상 녹화를 시작한다.
      if (command === UNITY_COMMANDS.enterPlay && status.passed) {
        await startVideoIfPending(context).catch(() => {});
      }
      return status;
    } catch (error) {
      if (error instanceof UnityBusyError) {
        throw new EngineError("INVALID_STATE", error.message, { retryable: false, cause: error });
      }
      if (error instanceof UnityCommandFailedError) {
        const committed = error.status.status === "failed" && error.status.failure_stage !== "preflight";
        throw new EngineError(
          committed ? "ACTION_MAY_HAVE_COMMITTED" : "INVALID_STATE",
          error.message,
          { retryable: false, cause: error }
        );
      }
      if (error instanceof Error && /did not pick up|timed out|aborted/.test(error.message)) {
        throw new EngineError("OPERATION_TIMEOUT", error.message, { retryable: false, cause: error });
      }
      throw new EngineError("ENGINE_FAILURE", error instanceof Error ? error.message : String(error), {
        retryable: false,
        cause: error
      });
    }
  }

  /** 실제로 Unity Recorder를 시작하고 activeVideo를 기록한다. 이미 진행 중이면 무시한다. */
  async function beginVideo(context: OperationContext): Promise<boolean> {
    if (activeVideo) return true;
    const status = await client.send(UNITY_COMMANDS.startVideo, {
      signal: context.signal, timeoutMs: 60_000
    });
    if (status.passed && typeof status.output_path === "string") {
      activeVideo = { outputPath: status.output_path, startedAt: new Date().toISOString() };
      return true;
    }
    return false;
  }

  /** Play 진입이 확정되면 보류 중인 영상 녹화를 시작한다. */
  async function startVideoIfPending(context: OperationContext): Promise<void> {
    if (!videoPending) return;
    if (await beginVideo(context)) videoPending = false;
  }

  /** 진행 중 녹화를 끝내고 attempt 산출물 디렉터리로 이동한다. */
  async function finalizeVideo(context: OperationContext): Promise<readonly { path: string; startedAt: string }[]> {
    if (!activeVideo) {
      videoPending = false;
      return [];
    }
    const video = activeVideo;
    activeVideo = undefined;
    videoPending = false;
    // Play 종료로 레코더가 자동 정리됐을 수 있으므로 stop 실패는 흡수하고 파일 회수를 시도한다.
    await client.send(UNITY_COMMANDS.stopVideo, {
      signal: context.signal, timeoutMs: 120_000
    }).catch(() => {});
    if (!artifactsDir) return [];
    // Recorder가 StopRecording 뒤에도 파일을 정리하는 시간을 준다.
    const target = path.join(artifactsDir, "video", `attempt-${context.attemptId}.mp4`);
    await fs.promises.mkdir(path.dirname(target), { recursive: true });
    const ready = await waitForStableFile(video.outputPath, context.signal, 30_000);
    if (!ready) {
      throw new EngineError("ENGINE_FAILURE", `영상 파일이 완성되지 않았습니다: ${video.outputPath}`, { retryable: false });
    }
    await fs.promises.copyFile(video.outputPath, target);
    return [{ path: path.posix.join("video", path.basename(target)), startedAt: video.startedAt }];
  }

  /** Game View PNG를 캡처해 절대 경로로 돌려준다. */
  async function captureFrame(context: OperationContext): Promise<{ path: string; width: number; height: number }> {
    const status = await runCommand(UNITY_COMMANDS.frame, context);
    const capturePath = status.capture_path;
    if (typeof capturePath !== "string" || capturePath.length === 0) {
      throw new EngineError("ENGINE_FAILURE", "capture_e2e_frame이 캡처 경로를 반환하지 않았습니다.", { retryable: false });
    }
    const png = await fs.promises.readFile(capturePath);
    const { width, height } = readPngSize(png);
    return { path: capturePath, width, height };
  }

  async function observe(context: OperationContext, observeOptions?: { pixels?: boolean }): Promise<EngineSnapshot> {
    // 환경 상태는 root 노드의 속성으로 노출한다(플레이·씬·모델·녹화 플래그).
    let envName = "Unity Editor";
    try {
      const envStatus = await runCommand(UNITY_COMMANDS.environment, context);
      const statePath = envStatus.environment_state_path;
      if (typeof statePath === "string" && statePath.length > 0) {
        lastEnvironment = parseUnityJson(await fs.promises.readFile(statePath, "utf8"));
        const scene = String(lastEnvironment.scene ?? "");
        const play = lastEnvironment.play_mode === true;
        const model = String(lastEnvironment.model_name ?? "");
        envName = `${scene || "scene"} ${play ? "playing" : "edit"} ${model}`.trim();
      }
    } catch {
      // 환경 캡처 실패는 관측을 막지 않는다 — 픽셀만으로도 진행 가능하다.
    }

    let pixels: EngineSnapshot["pixels"];
    if (observeOptions?.pixels) {
      const frame = await captureFrame(context);
      pixels = {
        data: new Uint8Array(await fs.promises.readFile(frame.path)),
        mediaType: "image/png",
        width: frame.width,
        height: frame.height,
        scale: 1
      };
      lastViewport = { width: frame.width, height: frame.height };
    }

    const revision = `obs-${Date.now()}`;
    const root: SemanticNode = {
      ref: { id: "unity-root", revision },
      role: "window",
      name: envName,
      rect: { x: 0, y: 0, width: lastViewport.width, height: lastViewport.height },
      attributes: environmentAttributes(lastEnvironment)
    };
    return { root, viewport: lastViewport };
  }

  /** status의 산출물 경로 필드를 attempt 디렉터리로 복사한다. */
  async function attachStatusArtifacts(status: Record<string, unknown>, fixtureContext: {
    attachArtifact: (kind: "log", relativePath: string) => void;
  }): Promise<void> {
    const keys = ["manifest_path", "environment_state_path", "full_clip_state_path",
      "playback_state_path", "preselection_state_path", "foot_live_state_path",
      "failure_evidence_path", "output_path", "vrm_output_path", "capture_path"];
    for (const key of keys) {
      const value = status[key];
      if (typeof value !== "string" || value.length === 0) continue;
      try {
        await copyIntoArtifacts(artifactsDir, fixtureContext, value);
        // manifest/state 파일은 같은 폴더의 단계별 증거(PNG·CSV)를 색인한다 —
        // F15처럼 같은 이름(state.json)이 여러 번 나오는 명령도 서로 덮지 않게 복사한다.
        if (key === "manifest_path" || key.endsWith("_state_path")) {
          await copySiblingEvidence(artifactsDir, fixtureContext, value);
          await copyMentionedVideo(artifactsDir, fixtureContext, value);
        }
      } catch {
        // 산출물 첨부 실패는 명령 결과를 바꾸지 않는다.
      }
    }
  }

  const engine: Engine = {
    name: "unity",
    version: ENGINE_VERSION,
    spiVersion: 1,
    platform: "unity",
    workers: 1,

    validateApp(_app: EngineAppDeclaration, _info: EngineAppCheckInfo) {
      // Unity 에디터는 url/bundleId 없이 열려 있는 프로세스를 대상으로 한다.
    },

    async observe(context, observeOptions) {
      return observe(context, observeOptions);
    },

    artifacts: {
      async screenshot(label: string | undefined, context: OperationContext): Promise<string> {
        const frame = await captureFrame(context);
        if (!artifactsDir) {
          throw new EngineError("INVALID_STATE", "screenshot called before startAttempt", { retryable: false });
        }
        const safe = (label ?? "screenshot").replace(/[^a-z0-9._-]+/gi, "-").slice(0, 80) || "screenshot";
        const relative = path.posix.join("screenshots", `${safe}.png`);
        const target = path.join(artifactsDir, relative);
        await fs.promises.mkdir(path.dirname(target), { recursive: true });
        await fs.promises.copyFile(frame.path, target);
        return relative;
      },
      async startVideo(context: OperationContext): Promise<void> {
        // Unity Recorder는 Play Mode에서만 동작한다 — Edit Mode면 Play 진입까지 보류한다.
        const env = await captureEnvironment(context).catch(() => null);
        if (env?.play_mode === true && await beginVideo(context)) return;
        videoPending = true;
      },
      async stopVideo(context: OperationContext) {
        return finalizeVideo(context);
      }
    },

    state: {
      async capture(context: OperationContext) {
        const data = await captureEnvironment(context);
        data.output_sha256 = await hashOptionalFile(
          path.join(projectRoot, "Assets", "VMDRecorderSample", "satisfaction_2.vmd"));
        return { format: "unity-editor-env", version: 1, data };
      },
      async restore(state, context) {
        const data = state.data as Record<string, unknown>;
        // 플레이 상태만 복원 계약이 있다 — 씬/모델은 enter_e2e_play 선행조건이 강제한다.
        if (data.play_mode === false) {
          await runCommand(UNITY_COMMANDS.exitPlay, context, undefined, 90_000);
        } else if (data.play_mode === true) {
          await runCommand(UNITY_COMMANDS.enterPlay, context, undefined, 90_000);
        }
      }
    },

    session: {
      // Unity는 주소 지정 표면이 없다 — restart는 Play 컨텍스트 재생성으로 매핑한다.
      async restart(context: OperationContext) {
        await runCommand(UNITY_COMMANDS.exitPlay, context, undefined, 90_000);
        await runCommand(UNITY_COMMANDS.enterPlay, context, undefined, 90_000);
      }
    },

    // 테스트가 Unity 자동화 명령을 step으로 호출하는 fixture다.
    // run()이 프로토콜 전체를 노출하고 나머지는 읽기 편의 래퍼다.
    fixtures: {
      unity: (fixtureContext) => fixtureContext.fixture("unity", {
        async run(command: string, fields?: Record<string, unknown>, timeoutMs?: number) {
          const status = await runCommand(command, fixtureContext.operation(timeoutMs), fields, timeoutMs);
          await attachStatusArtifacts(status, fixtureContext);
          return status;
        },
        async environment() {
          const status = await runCommand(UNITY_COMMANDS.environment, fixtureContext.operation());
          const statePath = status.environment_state_path;
          if (typeof statePath === "string" && statePath.length > 0) {
            return parseUnityJson(await fs.promises.readFile(statePath, "utf8"));
          }
          return {};
        },
        async enterPlay() {
          return runCommand(UNITY_COMMANDS.enterPlay, fixtureContext.operation(90_000));
        },
        async exitPlay() {
          return runCommand(UNITY_COMMANDS.exitPlay, fixtureContext.operation(90_000));
        },
        async fullClipMetrics(fields?: Record<string, unknown>) {
          const status = await runCommand(UNITY_COMMANDS.fullClip,
            fixtureContext.operation(1_800_000), fields, 1_800_000);
          await attachStatusArtifacts(status, fixtureContext);
          return status;
        },
        async startVideo() {
          const operation = fixtureContext.operation(60_000);
          const env = await captureEnvironment(operation).catch(() => null);
          if (env?.play_mode !== true) {
            throw new EngineError("INVALID_STATE", "영상 녹화는 Play Mode에서 시작해야 합니다.", { retryable: false });
          }
          if (!(await beginVideo(operation))) {
            throw new EngineError("ENGINE_FAILURE", "start_e2e_video가 출력 경로를 반환하지 않았습니다.", { retryable: false });
          }
        },
        async stopVideo() {
          const segments = await finalizeVideo(fixtureContext.operation(120_000));
          return segments.map((segment) => segment.path);
        }
      }, {
        run: {
          kind: "resource",
          label: (command: string) => `unity ${command}`,
          timeout: (_command: string, _fields, timeoutMs?: number) => timeoutMs
        },
        environment: { kind: "resource", label: () => "unity environment" },
        enterPlay: { kind: "resource", label: () => "enter play", timeout: 90_000 },
        exitPlay: { kind: "resource", label: () => "exit play", timeout: 90_000 },
        fullClipMetrics: { kind: "resource", label: () => "full clip metrics", timeout: 1_800_000 },
        startVideo: { kind: "resource", label: () => "start video", timeout: 60_000 },
        stopVideo: { kind: "resource", label: () => "stop video", timeout: 120_000 }
      })
    },

    async init() {
      // 폴러 생존 확인: 환경 캡처를 한 번 돌려 에디터가 요청을 소비하는지 본다.
      const probe = new AbortController();
      const status = await client.send(UNITY_COMMANDS.environment, {
        signal: probe.signal,
        timeoutMs: 180_000
      });
      if (!status.passed) {
        throw new EngineError("INVALID_STATE", "unity environment probe failed", { retryable: false });
      }
    },

    async startAttempt(attemptContext) {
      artifactsDir = attemptContext.artifactsDir;
      await client.cleanStaleRequest();
      // 복원 기준선: 환경 필드 + 보호 출력물 백업. 실패해도 attempt를 막지 않는다 —
      // 복원 불가 상태는 endAttempt의 검증이 드러낸다.
      const context: OperationContext = {
        signal: attemptContext.signal, timeoutMs: 120_000,
        runId: "attempt-setup", attemptId: attemptContext.attemptId, origin: "test"
      };
      // 환경·출력물을 독립 캡처한다 — 하나가 실패해도 나머지로 부분 복원을 수행하고,
      // 실패 사실은 기준선에 남겨 endAttempt가 조용히 넘어가지 않게 한다.
      let environment: Record<string, unknown> | undefined;
      let outputs: Array<{ target: string; backup: string | null; sha256: string }> = [];
      const errors: string[] = [];
      try {
        environment = await captureEnvironment(context);
      } catch (error) {
        errors.push(`environment: ${error instanceof Error ? error.message : error}`);
      }
      try {
        outputs = await backupOutputs(attemptContext.artifactsDir);
      } catch (error) {
        errors.push(`outputs: ${error instanceof Error ? error.message : error}`);
      }
      if (errors.length > 0) {
        attemptContext.appLog({
          source: "system", level: "warning",
          text: `restore baseline capture partial: ${errors.join("; ")}`
        });
      }
      restoreBaseline = {
        environment,
        outputs,
        error: errors.length > 0 ? errors.join("; ") : undefined
      };
      attemptContext.environment({ editor: "unity", protocol: "fbx_smoke_files" });
    },

    async endAttempt(context: EngineCleanupContext) {
      const problems: string[] = [];
      // attempt이 녹화를 남겼으면 먼저 끝낸다 — 녹화 중 Play 종료는 MP4를 깨뜨린다.
      // 확정 실패를 삼키지 않는다 — 레코더가 잔존하면 다음 attempt가 깨진다.
      if (activeVideo || videoPending) {
        await finalizeVideo({
          signal: context.signal, timeoutMs: context.timeoutMs,
          runId: "cleanup", attemptId: "cleanup", origin: "test"
        }).catch((error) => {
          problems.push(`영상 확정 실패: ${error instanceof Error ? error.message : error}`);
        });
      }
      problems.push(...await restoreBaselineState(context.signal));
      restoreBaseline = undefined;
      await client.cleanStaleRequest();
      if (problems.length > 0) {
        throw new EngineError("INVALID_STATE", `에디터 상태 복원 실패: ${problems.join("; ")}`, { retryable: false });
      }
    },

    async dispose() {
      artifactsDir = undefined;
      restoreBaseline = undefined;
      await client.cleanStaleRequest();
    }
  };

  return defineEngine(engine);
}

/** Unity JsonUtility는 NaN/Infinity를 비표준 리터럴로 출력한다 — 파싱 전 정규화. */
function parseUnityJson(text: string): Record<string, unknown> {
  return JSON.parse(
    text.replace(/:\s*(NaN|Infinity|-Infinity)/g, ": null")) as Record<string, unknown>;
}

function environmentAttributes(env: Record<string, unknown> | undefined): Record<string, string> | undefined {
  if (!env) return undefined;
  const keys = [
    "play_mode", "scene", "scene_dirty", "model_name", "avatar_valid",
    "has_prepared_motion", "is_playing_motion", "is_recording",
    "motion_time_seconds", "capture_framerate", "time_scale"
  ];
  const attributes: Record<string, string> = {};
  for (const key of keys) {
    if (env[key] !== undefined) attributes[key] = String(env[key]);
  }
  return attributes;
}

/** PNG IHDR에서 크기만 읽는다(의존성 추가 없이 24바이트 검사). */
function readPngSize(png: Buffer): { width: number; height: number } {
  if (png.length < 24 || png.readUInt32BE(0) !== 0x89504e47) {
    return { width: 0, height: 0 };
  }
  return { width: png.readUInt32BE(16), height: png.readUInt32BE(20) };
}

async function hashOptionalFile(filePath: string): Promise<string> {
  try {
    const { createHash } = await import("node:crypto");
    const hash = createHash("sha256");
    const stream = fs.createReadStream(filePath);
    for await (const chunk of stream) hash.update(chunk);
    return hash.digest("hex");
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === "ENOENT") return "missing";
    throw error;
  }
}

/** 파일이 존재하고 크기가 안정될 때까지 기다린다(Recorder flush 대기). */
async function waitForStableFile(filePath: string, signal: AbortSignal, timeoutMs: number): Promise<boolean> {
  const deadline = Date.now() + timeoutMs;
  let lastSize = -1;
  let stableCount = 0;
  while (Date.now() < deadline) {
    if (signal.aborted) return false;
    try {
      const stat = await fs.promises.stat(filePath);
      if (stat.size > 0 && stat.size === lastSize) {
        stableCount += 1;
        if (stableCount >= 2) return true;
      } else {
        stableCount = 0;
        lastSize = stat.size;
      }
    } catch {
      // 아직 없거나 쓰는 중 — 계속 기다린다.
    }
    await new Promise((resolve) => setTimeout(resolve, 1000));
  }
  return false;
}

const EVIDENCE_EXTENSIONS = new Set([
  ".png", ".jpg", ".jpeg", ".webp", ".mp4", ".webm",
  ".json", ".csv", ".xml", ".txt", ".log", ".md", ".vmd"
]);

/** Unity 측 산출물을 attempt 디렉터리 아래로 복사하고 step에 첨부한다. */
async function copyIntoArtifacts(
  artifactsDir: string | undefined,
  fixtureContext: { attachArtifact: (kind: "log", relativePath: string) => void },
  sourcePath: string
): Promise<void> {
  if (!artifactsDir) return;
  const base = path.basename(sourcePath);
  // 같은 basename(state.json 등)을 가진 산출물이 반복되면 부모 폴더명으로 구분한다.
  let relative = path.posix.join("unity", base);
  let target = path.join(artifactsDir, relative);
  if (await exists(target)) {
    const parent = path.basename(path.dirname(sourcePath)).replace(/[^A-Za-z0-9._-]/g, "_");
    relative = path.posix.join("unity", `${parent}-${base}`);
    target = path.join(artifactsDir, relative);
  }
  let counter = 1;
  while (await exists(target)) {
    relative = path.posix.join("unity", `${counter++}-${base}`);
    target = path.join(artifactsDir, relative);
  }
  await fs.promises.mkdir(path.dirname(target), { recursive: true });
  await fs.promises.copyFile(sourcePath, target);
  fixtureContext.attachArtifact("log", relative);
}

async function exists(filePath: string): Promise<boolean> {
  try {
    await fs.promises.access(filePath);
    return true;
  } catch {
    return false;
  }
}

/** state JSON의 video_result_message가 가리키는 MP4도 첨부한다(F15 녹화물). */
async function copyMentionedVideo(
  artifactsDir: string | undefined,
  fixtureContext: { attachArtifact: (kind: "log", relativePath: string) => void },
  statePath: string
): Promise<void> {
  try {
    const state = JSON.parse(await fs.promises.readFile(statePath, "utf8")) as
      { video_result_message?: string };
    const match = /([A-Za-z]:[\\/][^"'\r\n]+?\.mp4)/.exec(state.video_result_message ?? "");
    if (match && (await exists(match[1]))) {
      await copyIntoArtifacts(artifactsDir, fixtureContext, match[1]);
    }
  } catch {
    // MP4 경로가 없거나 읽기 실패는 첨부를 막지 않는다.
  }
}

/** manifest/state JSON과 같은 폴더의 증거 파일을 함께 첨부한다. */
async function copySiblingEvidence(
  artifactsDir: string | undefined,
  fixtureContext: { attachArtifact: (kind: "log", relativePath: string) => void },
  statePath: string
): Promise<void> {
  if (!artifactsDir) return;
  const dir = path.dirname(statePath);
  let entries: fs.Dirent[];
  try {
    entries = await fs.promises.readdir(dir, { withFileTypes: true });
  } catch {
    return;
  }
  const stateFull = path.resolve(statePath);
  // 단계 캡처가 많은 흐름도 상한으로 묶는다.
  for (const entry of entries.slice(0, 96)) {
    if (!entry.isFile()) continue;
    const full = path.join(dir, entry.name);
    if (path.resolve(full) === stateFull) continue;
    if (!EVIDENCE_EXTENSIONS.has(path.extname(entry.name).toLowerCase())) continue;
    try {
      await copyIntoArtifacts(artifactsDir, fixtureContext, full);
    } catch {
      // 개별 증거 실패는 나머지 첨부를 막지 않는다.
    }
  }
}
