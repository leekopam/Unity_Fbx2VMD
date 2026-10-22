import fs from "node:fs";
import path from "node:path";
import { randomUUID } from "node:crypto";

// Unity 측 FbxPlaybackSmokeRunner의 파일 폴러와 통신하는 최소 클라이언트다.
// fbx_smoke_request.json에 명령을 쓰면 폴러가 소비하고
// fbx_smoke_status.json에 같은 request_id의 결과를 남긴다.
// Tools/Boogle/run-product-smoke.mjs와 동일 프로토콜, e2e 엔진 전용 경량 구현.

export const UNITY_COMMANDS = {
  environment: "capture_e2e_environment",
  frame: "capture_e2e_frame",
  startVideo: "start_e2e_video",
  stopVideo: "stop_e2e_video",
  enterPlay: "enter_e2e_play",
  exitPlay: "exit_e2e_play",
  openScene: "open_e2e_scene",
  preselection: "capture_preselection_state",
  playback: "capture_playback_seek_evidence",
  invalidInput: "capture_invalid_input_evidence",
  fullClip: "capture_satisfaction_full_clip_metrics",
  fullRegression: "capture_satisfaction_full_regression_evidence_208s_4k",
  fullNamedVmd: "capture_satisfaction_full_named_vmd",
  alternateModel: "capture_tetoris_testprefab_full_clip_metrics",
  productUi: "capture_product_ui_flow",
  vrmExport: "capture_satisfaction_vrm",
  motionRecording: "capture_motion_recording_evidence"
} as const;

export interface UnityStatus {
  request_id: string;
  status: string;
  command: string;
  message: string;
  passed: boolean;
  manifest_path?: string;
  failure_stage?: string;
  output_path?: string;
  capture_path?: string;
  environment_state_path?: string;
  preselection_state_path?: string;
  playback_state_path?: string;
  full_clip_state_path?: string;
  vrm_output_path?: string;
  vrm_file_size_bytes?: number;
  frame_count?: number;
  file_size_bytes?: number;
  failures?: string[];
  [key: string]: unknown;
}

export interface UnityClientOptions {
  /** Unity 프로젝트 루트(절대 경로). */
  projectRoot: string;
  /** 요청 픽업 대기 예산. GUI 에디터 첫 요청은 임포트·Native 준비로 30초를 넘을 수 있다. */
  pickupTimeoutMs?: number;
  /** 요청/상태 파일명 — F13 시각 비교는 yyb_visual_compare_* 쌍을 쓴다. */
  requestFileName?: string;
  statusFileName?: string;
}

export class UnityBusyError extends Error {}
export class UnityCommandFailedError extends Error {
  readonly status: UnityStatus;
  constructor(status: UnityStatus) {
    super(status.message || `unity command failed: ${status.command}`);
    this.status = status;
  }
}

export function createUnityClient(options: UnityClientOptions) {
  const runtimeRoot = path.join(options.projectRoot, "Docs", "Workflow", "Local", "runtime");
  const requestPath = path.join(runtimeRoot, options.requestFileName ?? "fbx_smoke_request.json");
  const statusPath = path.join(runtimeRoot, options.statusFileName ?? "fbx_smoke_status.json");
  const pickupTimeoutMs = options.pickupTimeoutMs ?? 120_000;

  async function readStatus(): Promise<UnityStatus | null> {
    let content: string;
    try {
      content = await fs.promises.readFile(statusPath, "utf8");
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code === "ENOENT") return null;
      throw error;
    }
    try {
      return JSON.parse(content) as UnityStatus;
    } catch (error) {
      if (error instanceof SyntaxError) return null;
      throw error;
    }
  }

  async function isRequestPending(): Promise<boolean> {
    try {
      await fs.promises.access(requestPath);
      return true;
    } catch {
      return false;
    }
  }

  /** 요청 파일의 request_id를 읽는다 — 파싱 실패·부재는 undefined. */
  async function readRequestId(): Promise<string | undefined> {
    try {
      const parsed = JSON.parse(await fs.promises.readFile(requestPath, "utf8")) as
        { request_id?: string };
      return parsed.request_id;
    } catch {
      return undefined;
    }
  }

  // Unity는 완료 상태를 쓰고 같은 틱에 요청 파일을 지운다 — 그 사이를 읽으면
  // "파일 있음 + status completed"가 보인다. 이 조합은 잔여물이지 진행 중이 아니다.
  const TERMINAL = new Set(["completed", "failed"]);
  // 가장 긴 명령 예산(30분)을 넘긴 running은 Unity 크래시 잔여물로 본다.
  const STALE_RUNNING_MS = 35 * 60 * 1000;

  /** 남아 있는 요청·폴러가 running 중이면 이전 작업이 진행 중이다. */
  async function isBusy(): Promise<boolean> {
    if (await isRequestPending()) {
      const [requestId, status] = await Promise.all([readRequestId(), readStatus()]);
      if (requestId !== undefined && status?.request_id === requestId &&
          TERMINAL.has(status.status)) {
        return false;
      }
      return true;
    }
    const status = await readStatus();
    if (status?.status !== "running") return false;
    const updated = Date.parse(typeof status.updated_at === "string" ? status.updated_at : "");
    return !Number.isFinite(updated) || Date.now() - updated <= STALE_RUNNING_MS;
  }

  /** run 시작 전 잔여 요청을 정리한다. 폴러가 소비 중인 파일은 건드리지 않는다. */
  async function cleanStaleRequest(expectedRequestId?: string): Promise<void> {
    try {
      // 다른 요청 id의 파일은 진행 중일 수 있으니 지우지 않는다.
      if (expectedRequestId !== undefined) {
        const onDisk = await readRequestId();
        if (onDisk !== undefined && onDisk !== expectedRequestId) return;
      }
      await fs.promises.rm(requestPath, { force: true });
    } catch {
      // 정리 실패는 실행을 막지 않는다.
    }
  }

  interface SendOptions {
    fields?: Record<string, unknown>;
    signal?: AbortSignal;
    timeoutMs?: number;
  }

  /**
   * 명령을 쓰고 같은 request_id의 최종 상태(completed/failed)를 기다린다.
   * 폴러가 요청 파일을 지우면 픽업으로 본다. failed는 UnityCommandFailedError.
   */
  async function send(command: string, sendOptions?: SendOptions): Promise<UnityStatus> {
    const requestId = randomUUID();
    if (await isBusy()) {
      throw new UnityBusyError(`unity runner is busy; cannot send ${command}`);
    }
    const request = { request_id: requestId, command, requested_command: command, ...sendOptions?.fields };
    await fs.promises.mkdir(runtimeRoot, { recursive: true });
    // 임시 파일에 쓰고 원자적으로 rename한다 — 폴러가 쓰기 도중 파일을 열면
    // 공유 위반으로 요청이 통째로 삭제되어 클라이언트가 무한 대기한다.
    const tempPath = `${requestPath}.${requestId}.tmp`;
    await fs.promises.writeFile(tempPath, JSON.stringify(request));
    await fs.promises.rename(tempPath, requestPath);

    const deadline = Date.now() + (sendOptions?.timeoutMs ?? 30_000);
    const pickupDeadline = Date.now() + pickupTimeoutMs;
    let pickedUp = false;

    try {
      for (;;) {
        if (sendOptions?.signal?.aborted) {
          throw new Error(`aborted waiting for ${command}`);
        }
        const status = await readStatus();
        if (status?.request_id === requestId &&
            (status.status === "completed" || status.status === "failed")) {
          if (status.status === "failed" || !status.passed) {
            throw new UnityCommandFailedError(status);
          }
          return status;
        }
        if (!pickedUp) {
          pickedUp = !(await isRequestPending()) || status?.request_id === requestId;
          if (!pickedUp) {
            if (Date.now() > pickupDeadline) {
              throw new Error(`unity did not pick up ${command} within ${pickupTimeoutMs}ms (editor not running?)`);
            }
          }
        }
        if (Date.now() > deadline) {
          throw new Error(`timed out waiting for ${command} (${sendOptions?.timeoutMs ?? 30_000}ms)`);
        }
        await new Promise((resolve) => setTimeout(resolve, pickedUp ? 500 : 1000));
      }
    } finally {
      // 미소비 요청이 남으면 폴러가 나중에 실행하지 않게 제거한다.
      // 자기 요청과 id가 다른 파일은 지우지 않는다.
      if (!pickedUp) await cleanStaleRequest(requestId);
    }
  }

  return {
    runtimeRoot,
    send,
    isBusy,
    cleanStaleRequest,
    readStatus
  };
}

export type UnityClient = ReturnType<typeof createUnityClient>;

/** 이 파일 기준 Unity 프로젝트 루트(테스트가 SDK 외 모듈을 직접 부를 때 사용). */
export function defaultUnityProjectRoot(): string {
  return new URL("../../../../../../", import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1");
}
