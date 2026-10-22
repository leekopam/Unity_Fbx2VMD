import { spawn, type ChildProcess } from "node:child_process";
import { createRequire } from "node:module";
import fs from "node:fs";
import http from "node:http";
import net from "node:net";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

import type {
  BrowserLease,
  BrowserProvider,
  BrowserReleaseContext,
  BrowserRequest
} from "@e2e-dev/web";

// e2e web 엔진에 Electron을 attempt 단위로 빌려주는 provider다.
// e2e의 per-attempt 계약(매 시도 새 브라우저, default context, 호스트가 종료 책임)에
// 맞춰 Electron을 --remote-debugging-port로 띄워 CDP 엔드포인트를 반환하고,
// release에서 프로세스를 종료한다.
// e2e는 file:// 탐색을 거부하므로 build/를 고정 포트로 정적 서빙하고
// Electron 창이 그 http URL을 로드하게 한다.
export const E2E_ENTRY_URL = "http://127.0.0.1:4199/index.html";
const STATIC_PORT = 4199;

const appRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const buildDir = path.join(appRoot, "build");
const clientDir = path.join(appRoot, "client");
const require = createRequire(import.meta.url);
const electronBinary = require("electron") as string;
const pidDir = path.join(appRoot, ".e2e", "electron-pids");
const CDP_WAIT_MS = 20000;

const CONTENT_TYPES: Record<string, string> = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".svg": "image/svg+xml",
  ".ico": "image/x-icon",
  ".woff": "font/woff",
  ".woff2": "font/woff2",
  ".ttf": "font/ttf",
  ".map": "application/json; charset=utf-8",
  ".mp3": "audio/mpeg",
  ".wav": "audio/wav"
};

interface ElectronLease extends BrowserLease {
  child: ChildProcess;
  pidFile: string;
  settingsPath: string;
}

export function electronProvider(): BrowserProvider {
  let staticServer: http.Server | undefined;

  const ensureStaticServer = () => {
    staticServer ??= createStaticServer();
    return staticServer;
  };

  return {
    name: "electron",
    scope: "attempt",

    async acquire(request: BrowserRequest): Promise<BrowserLease> {
      ensureStaticServer();
      const cdpPort = await freePort();
      const tag = request.attemptId ?? `${Date.now()}`;
      const settingsPath = path.join(os.tmpdir(), `mrs-e2e-${tag}.json`);
      const fbxPath = path.join(os.tmpdir(), `mrs-e2e-${tag}.fbx`);
      fs.writeFileSync(settingsPath, JSON.stringify({ runtimeState: { playMode: "playing" } }));
      fs.writeFileSync(fbxPath, "e2e placeholder fbx\n", "utf8");

      const env = { ...process.env };
      delete env.ELECTRON_RUN_AS_NODE;

      const child = spawn(
        electronBinary,
        [
          `--remote-debugging-port=${cdpPort}`,
          appRoot,
          "--e2e-test",
          `--e2e-entry-url=${E2E_ENTRY_URL}`,
          `--smoke-settings-path=${settingsPath}`,
          `--smoke-import-fbx=${fbxPath}`
        ],
        { stdio: ["ignore", "ignore", "pipe"], env }
      );
      child.stderr?.on("data", (chunk) => {
        const line = chunk.toString();
        // 초기 about:blank 로드가 entry URL 로드에 취소되는 무해한 경합과
        // 그 경고의 연속 줄(안내 문구)은 무시한다.
        if (
          line.includes("ERR_ABORTED") ||
          line.includes("UnhandledPromiseRejectionWarning") ||
          line.includes("trace-warnings") ||
          line.includes("unhandled-rejections=strict")
        ) {
          return;
        }
        if (line.includes("Error") || line.includes("error")) {
          request.log(`electron stderr: ${line.trim()}`);
        }
      });

      const cdpEndpoint = await waitForCdp(cdpPort, request.signal, child);
      const pidFile = path.join(pidDir, `${process.pid}-${child.pid}.pid`);
      fs.mkdirSync(pidDir, { recursive: true });
      fs.writeFileSync(pidFile, String(child.pid), "utf8");

      return { id: `electron-${child.pid}`, cdpEndpoint, child, pidFile, settingsPath } satisfies ElectronLease;
    },

    async release(lease: BrowserLease): Promise<void> {
      const { child, pidFile } = lease as ElectronLease;
      try {
        fs.rmSync(pidFile, { force: true });
      } catch {
        // pid 파일 정리 실패는 종료를 막지 않는다.
      }
      if (child && !child.killed) {
        child.kill();
      }
    },

    // 워커가 죽어 release를 못 부른 Electron 잔여분을 pid 파일로 찾아 종료한다.
    async sweep(context: BrowserReleaseContext): Promise<readonly string[]> {
      const swept: string[] = [];
      let files: string[] = [];
      try {
        files = fs.readdirSync(pidDir).filter((name) => name.endsWith(".pid"));
      } catch {
        return swept;
      }
      for (const file of files) {
        const pid = Number(fs.readFileSync(path.join(pidDir, file), "utf8").trim());
        if (Number.isSafeInteger(pid) && pid > 0) {
          try {
            process.kill(pid);
            swept.push(`pid-${pid}`);
          } catch {
            // 이미 죽은 프로세스면 파일만 지운다.
          }
        }
        fs.rmSync(path.join(pidDir, file), { force: true });
      }
      if (swept.length > 0) {
        context.log(`swept ${swept.length} stale electron process(es)`);
      }
      return swept;
    }
  };
}

// 렌더러 정적 서버 — index.html이 "../client/*.js"를 참조하므로
// file:// 레이아웃 그대로 appRoot 기준으로 매핑한다.
// "/"·"/index.html"은 build/index.html로 보낸다.
function createStaticServer(): http.Server {
  const server = http.createServer((request, response) => {
    const pathname = decodeURIComponent(new URL(request.url ?? "/", "http://localhost").pathname);
    const relative = pathname === "/" || pathname === "/index.html"
      ? "index.html"
      : pathname.replace(/^\/+/, "");
    // /client/* 만 client/ 디렉터리 기준, 그 외는 build/ 기준으로 본다.
    const underClient = relative.startsWith("client/");
    const baseDir = underClient ? clientDir : buildDir;
    const resolved = path.resolve(baseDir, underClient ? relative.slice("client/".length) : relative);
    if (!resolved.startsWith(baseDir + path.sep) && path.dirname(resolved) !== baseDir) {
      response.writeHead(403).end();
      return;
    }
    fs.createReadStream(resolved)
      .on("error", () => response.writeHead(404).end())
      .once("open", () => {
        response.writeHead(200, { "Content-Type": CONTENT_TYPES[path.extname(resolved).toLowerCase()] ?? "application/octet-stream" });
      })
      .pipe(response);
  });
  server.listen(STATIC_PORT, "127.0.0.1");
  server.unref();
  return server;
}

async function freePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      const port = typeof address === "object" && address ? address.port : 0;
      server.close(() => resolve(port));
    });
  });
}

// Electron의 remote debugging HTTP가 준비될 때까지 폴링해 browser-level WS를 돌려준다.
function waitForCdp(port: number, signal: AbortSignal, child: ChildProcess): Promise<string> {
  return new Promise((resolve, reject) => {
    const deadline = Date.now() + CDP_WAIT_MS;
    let settled = false;
    const fail = (error: Error) => {
      if (!settled) {
        settled = true;
        cleanup();
        reject(error);
      }
    };
    const onExit = () => fail(new Error(`electron exited before CDP was ready (code ${child.exitCode})`));
    const onAbort = () => fail(new Error("aborted waiting for electron CDP"));
    const cleanup = () => {
      child.off("exit", onExit);
      signal.removeEventListener("abort", onAbort);
    };
    child.once("exit", onExit);
    signal.addEventListener("abort", onAbort, { once: true });

    const poll = () => {
      if (settled) {
        return;
      }
      const request = http.get(`http://127.0.0.1:${port}/json/version`, (response) => {
        let body = "";
        response.on("data", (chunk) => (body += chunk));
        response.on("end", () => {
          try {
            const endpoint = JSON.parse(body).webSocketDebuggerUrl;
            if (typeof endpoint === "string" && endpoint.startsWith("ws://")) {
              settled = true;
              cleanup();
              resolve(endpoint);
              return;
            }
          } catch {
            // 준비 전 응답이면 다음 폴링으로 넘어간다.
          }
          schedule();
        });
      });
      request.on("error", schedule);
      request.setTimeout(2000, () => request.destroy());
    };
    const schedule = () => {
      if (settled) {
        return;
      }
      if (Date.now() > deadline) {
        fail(new Error(`timed out waiting for electron CDP on port ${port}`));
        return;
      }
      setTimeout(poll, 250);
    };
    poll();
  });
}
