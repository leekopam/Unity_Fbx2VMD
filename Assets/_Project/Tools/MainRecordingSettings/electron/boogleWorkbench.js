import { spawn } from "node:child_process";
import path from "node:path";

const READY_PREFIX = "BOOGLE_WORKBENCH_READY";
const READY_TIMEOUT_MS = 15000;

// 준비 완료 줄에서 workbench URL만 추출한다. 127.0.0.1 형식만 허용.
export function extractWorkbenchUrl(line) {
  if (typeof line !== "string" || !line.startsWith(READY_PREFIX)) {
    return "";
  }

  const url = line.slice(READY_PREFIX.length).trim();
  return /^http:\/\/127\.0\.0\.1:\d+$/.test(url) ? url : "";
}

// boogle-sdk의 workbench 명령을 자식 프로세스로 띄워 준비 URL을 받는다.
// Electron 안에서는 ELECTRON_RUN_AS_NODE로 node 런타임으로 실행한다.
// 준비 완료 후 프로세스가 종료되면 onExit을 호출해 호출자가 참조를 정리하게 한다.
export function startBoogleWorkbench({ appRoot, spawnProcess = spawn, onError, onExit, readyTimeoutMs = READY_TIMEOUT_MS } = {}) {
  const cliPath = path.join(appRoot, "node_modules", "boogle-sdk", "dist", "cli.js");
  const child = spawnProcess(process.execPath, [cliPath, "workbench"], {
    env: { ...process.env, ELECTRON_RUN_AS_NODE: "1" },
    stdio: ["ignore", "pipe", "pipe"],
    windowsHide: true
  });
  // stderr는 소비하지 않으므로 출력이 차는 것을 막기 위해 흘려보낸다.
  child.stderr?.resume?.();

  let buffer = "";
  let settled = false;
  const stop = () => {
    if (child.exitCode === null && child.signalCode === null) {
      child.kill();
    }
  };
  const url = new Promise((resolve, reject) => {
    const timeout = setTimeout(() => {
      if (settled) {
        return;
      }
      settled = true;
      stop();
      reject(new Error("Workbench 시작 시간 초과"));
    }, readyTimeoutMs);

    const settle = (fn, value) => {
      if (settled) {
        return;
      }
      settled = true;
      clearTimeout(timeout);
      fn(value);
    };

    child.stdout.on("data", (chunk) => {
      buffer += chunk.toString("utf8");
      const lines = buffer.split(/\r?\n/);
      buffer = lines.pop() ?? "";
      for (const line of lines) {
        const found = extractWorkbenchUrl(line);
        if (found) {
          settle(resolve, found);
          return;
        }
      }
    });
    child.once("error", (error) => settle(reject, error));
    child.once("exit", (code) => {
      if (!settled) {
        settle(reject, new Error(`Workbench 프로세스가 종료됨 (code ${code})`));
        return;
      }
      onExit?.(code);
    });
  });
  url.catch((error) => onError?.(error));

  return { url, stop };
}
