import type { AgentErrorCode, StepExecutor, StepExecutorContext, StepVerdict } from "e2e";

import { createUnityClient, UnityBusyError, UnityCommandFailedError, type UnityStatus } from "./unityClient.js";

// 모델 없는 결정적 StepExecutor — 기존 Boogle request/status 명령을
// agent.act("unity <command> [fields-json]") 스텝 문법으로 실행한다.
// 예: await agent.act('unity capture_e2e_environment')
//     await agent.act('unity enter_e2e_play {"scene_path":"Assets/..."}')
//     await agent.assert('unity env play_mode == true')
// defineTool 경로는 ai 패키지 의존이 필요해 모델 에이전트 도입 시점으로 유보한다.

const ACT_PATTERN = /^unity\s+(\S+)(?:\s+(\{[\s\S]*\}))?\s*$/;
const ASSERT_PATTERN = /^unity\s+env\s+(\S+)\s*(==|!=)\s*(.+)\s*$/;

export interface UnityScriptedExecutorOptions {
  /** Unity 프로젝트 루트. 기본은 이 파일이 속한 Unity 프로젝트. */
  projectRoot?: string;
  pickupTimeoutMs?: number;
}

export function unityScriptedExecutor(options?: UnityScriptedExecutorOptions): StepExecutor {
  const client = createUnityClient({
    projectRoot: options?.projectRoot ?? defaultProjectRoot(),
    pickupTimeoutMs: options?.pickupTimeoutMs
  });

  async function runAct(instruction: string, ctx: StepExecutorContext): Promise<StepVerdict> {
    const match = ACT_PATTERN.exec(instruction);
    if (!match) {
      return blocked("AUTOMATION_UNSUPPORTED",
        `해석할 수 없는 unity 스텝입니다: ${instruction} (형식: unity <command> [fields-json])`);
    }
    let fields: Record<string, unknown> | undefined;
    if (match[2]) {
      try {
        fields = JSON.parse(match[2]) as Record<string, unknown>;
      } catch {
        return blocked("TEST_SETUP_FAILED", `fields JSON 파싱 실패: ${match[2]}`);
      }
    }
    const command = match[1];
    let status: UnityStatus;
    try {
      status = await ctx.budgets.runTool({ name: "unity_command", mutates: true }, () =>
        client.send(command, { fields, signal: ctx.signal, timeoutMs: ctx.budgets.remainingMs() }));
    } catch (error) {
      return commandErrorToVerdict(error, command);
    }
    return {
      status: status.passed ? "passed" : "failed",
      summary: `unity ${command}: ${status.status}${status.message ? ` — ${status.message}` : ""}`
    };
  }

  async function runAssert(instruction: string, ctx: StepExecutorContext): Promise<StepVerdict> {
    const match = ASSERT_PATTERN.exec(instruction);
    if (!match) {
      return blocked("AUTOMATION_UNSUPPORTED",
        `해석할 수 없는 unity assert입니다: ${instruction} (형식: unity env <field> ==|!= <value>)`);
    }
    const [, field, op, rawExpected] = match;
    let status: UnityStatus;
    try {
      status = await ctx.budgets.runTool({ name: "unity_command", mutates: false }, () =>
        client.send("capture_e2e_environment", { signal: ctx.signal, timeoutMs: 120_000 }));
    } catch (error) {
      return commandErrorToVerdict(error, "capture_e2e_environment");
    }
    const statePath = status.environment_state_path;
    if (typeof statePath !== "string" || statePath.length === 0) {
      return blocked("ENVIRONMENT_UNAVAILABLE", "환경 상태 경로가 없습니다.");
    }
    const fs = await import("node:fs");
    const env = JSON.parse(await fs.promises.readFile(statePath, "utf8")) as Record<string, unknown>;
    const actual = env[field];
    const expected = parseScalar(rawExpected);
    const equal = actual === expected || String(actual) === rawExpected.trim();
    const pass = op === "==" ? equal : !equal;
    return {
      status: pass ? "passed" : "failed",
      summary: `env.${field} = ${JSON.stringify(actual)} (${op} ${rawExpected.trim()} ${pass ? "만족" : "불만족"})`
    };
  }

  return {
    name: "unity-scripted",
    version: "1",
    // 명령 스텝은 매번 에디터에 실행해야 한다 — 리플레이 캐시를 쓰지 않는다.
    cache: "off",
    async runStep(ctx) {
      if (ctx.step.kind === "act") return runAct(ctx.step.instruction, ctx);
      if (ctx.step.kind === "assert") return runAssert(ctx.step.instruction, ctx);
      return blocked("AUTOMATION_UNSUPPORTED", `지원하지 않는 스텝 종류: ${ctx.step.kind}`);
    }
  };
}

function parseScalar(raw: string): unknown {
  const text = raw.trim();
  if (text === "true") return true;
  if (text === "false") return false;
  if (text === "null") return null;
  const asNumber = Number(text);
  if (text !== "" && !Number.isNaN(asNumber)) return asNumber;
  return text.replace(/^["']|["']$/g, "");
}

function commandErrorToVerdict(error: unknown, command: string): StepVerdict {
  if (error instanceof UnityBusyError) {
    return blocked("ENVIRONMENT_UNAVAILABLE", `unity 러너가 다른 작업 중입니다: ${command}`);
  }
  if (error instanceof UnityCommandFailedError) {
    // preflight 실패는 환경 문제, 그 외는 제품이 요구를 충족하지 못한 것이다.
    if (error.status.failure_stage === "preflight") {
      return blocked("ENVIRONMENT_UNAVAILABLE",
        `unity ${command} 선행조건 실패: ${error.status.message}`);
    }
    return { status: "failed", summary: `unity ${command} 실패: ${error.status.message}` };
  }
  const message = error instanceof Error ? error.message : String(error);
  if (/did not pick up|aborted|timed out/.test(message)) {
    return blocked("ENVIRONMENT_UNAVAILABLE", `unity ${command}: ${message}`);
  }
  return blocked("AUTOMATION_UNSUPPORTED", `unity ${command}: ${message}`);
}

function blocked(code: AgentErrorCode, summary: string): StepVerdict {
  return { status: "blocked", errorCode: code, summary };
}

function defaultProjectRoot(): string {
  return new URL("../../../../../../", import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1");
}
