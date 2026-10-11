import crypto from "node:crypto";
import http from "node:http";
import { WebSocketServer } from "ws";

import {
  readCharacterLibrary,
  resolveLibraryFilePath
} from "./characterLibraryStore.js";
import {
  queueCharacterCommand,
  queueImportFbxCommand,
  readCommandResult,
  readRuntimeState,
  resolveSettingsFilePath
} from "./settingsDocumentStore.js";

export const DEFAULT_BRIDGE_PORT = 4100;

// Electron 설정창 안에서 HTTP API와 WebSocket endpoint를 함께 여는 로컬 브리지 서버다.
// HTTP는 명령 요청을 받고, WebSocket은 설정창에 상태 이벤트를 되돌려준다.
export function createSettingsBridgeServer({
  host = "127.0.0.1",
  port = DEFAULT_BRIDGE_PORT,
  settingsPath = resolveSettingsFilePath(),
  libraryPath = resolveLibraryFilePath(),
  now,
  createCommandId
} = {}) {
  // 세션 토큰 — file:///"null" 오리진은 샌드박스 iframe과 구분이 안 되므로
  // Electron 렌더러만 아는 토큰으로 판별한다(executeJavaScript로 주입).
  const token = crypto.randomUUID();
  // 명령 큐잉의 파일 읽기-검사-쓰기를 직렬화한다 — 단일 프로세스라
  // promise 체인만으로 동시 HTTP 요청 간 TOCTOU 덮어쓰기를 막을 수 있다.
  let commandQueueLock = Promise.resolve();
  const withCommandQueueLock = (task) => {
    const run = commandQueueLock.then(task);
    commandQueueLock = run.catch(() => {});
    return run;
  };
  const server = http.createServer(async (request, response) => {
    await handleHttpRequest({
      request,
      response,
      settingsPath,
      libraryPath,
      now,
      createCommandId,
      broadcast,
      token,
      withCommandQueueLock
    });
  });
  const webSocketServer = new WebSocketServer({ noServer: true });

  // WebSocket upgrade 요청이 오면 /settings 경로로만 연결을 허용
  server.on("upgrade", (request, socket, head) => {
    const upgradeUrl = new URL(request.url, `http://${request.headers.host}`);
    const upgradeOrigin = request.headers.origin;
    if (
      upgradeUrl.pathname !== "/settings" ||
      (upgradeOrigin !== undefined && !isAllowedOrigin(upgradeOrigin)) ||
      (requiresBridgeToken(upgradeOrigin) && upgradeUrl.searchParams.get("token") !== token)
    ) {
      socket.destroy();
      return;
    }

    webSocketServer.handleUpgrade(request, socket, head, (webSocket) => {
      webSocketServer.emit("connection", webSocket, request);
    });
  });

  // 클라이언트가 연결되면 현재 브리지가 사용할 설정 파일 경로를 즉시 전송
  webSocketServer.on("connection", (webSocket) => {
    sendJson(webSocket, {
      type: "status",
      status: "ready",
      settingsPath
    });
  });

  // HTTP 요청 결과를 연결된 모든 설정창 클라이언트에 이벤트로 브로드캐스트
  function broadcast(message) {
    const payload = JSON.stringify(message);
    for (const client of webSocketServer.clients) {
      if (client.readyState === client.OPEN) {
        client.send(payload);
      }
    }
  }

  return {
    settingsPath,
    get token() {
      return token;
    },
    get baseUrl() {
      const address = server.address();
      const resolvedPort = typeof address === "object" && address ? address.port : port;
      return `http://${host}:${resolvedPort}`;
    },
    get wsUrl() {
      const address = server.address();
      const resolvedPort = typeof address === "object" && address ? address.port : port;
      return `ws://${host}:${resolvedPort}`;
    },
    listen(listenPort = port) {
      // smoke test에서는 port 0을 넘겨 사용 가능한 임시 포트를 받는다.
      return new Promise((resolve, reject) => {
        server.once("error", reject);
        server.listen(listenPort, host, () => {
          server.off("error", reject);
          resolve();
        });
      });
    },
    close() {
      // 테스트와 Electron 종료 시 WebSocket 클라이언트까지 정리한다.
      return new Promise((resolve, reject) => {
        for (const client of webSocketServer.clients) {
          client.close();
        }

        webSocketServer.close(() => {
          server.close((error) => {
            if (error) {
              reject(error);
              return;
            }

            resolve();
          });
        });
      });
    }
  };
}

async function handleHttpRequest({
  request,
  response,
  settingsPath,
  libraryPath,
  now,
  createCommandId,
  broadcast,
  token,
  withCommandQueueLock
}) {
  // 브라우저 출처를 검증한다 — 허용 외 Origin의 요청은 여기서 거절해
  // 임의 웹페이지가 단순 요청(preflight 없는 POST 포함)으로 명령을 보내는 것을 막는다.
  const origin = request.headers.origin;
  if (origin !== undefined && !isAllowedOrigin(origin)) {
    sendJsonResponse(response, 403, { error: "허용되지 않은 Origin입니다." });
    return;
  }
  writeCorsHeaders(response, origin);

  // 개발 서버에서 호출할 수 있도록 브라우저 preflight를 먼저 처리
  // (preflight는 커스텀 헤더를 실을 수 없으므로 토큰 검사보다 앞에 둔다)
  if (request.method === "OPTIONS") {
    response.writeHead(204);
    response.end();
    return;
  }

  // "null"/file:// 오리진은 샌드박스 iframe과 Electron file:// 렌더러가
  // 구분 불가하므로 세션 토큰으로만 판별한다(Sec-Fetch-Site로는 둘 다 cross-site).
  if (requiresBridgeToken(origin) && request.headers["x-bridge-token"] !== token) {
    sendJsonResponse(response, 403, { error: "유효한 브리지 토큰이 필요합니다." });
    return;
  }

  const requestUrl = new URL(request.url, `http://${request.headers.host}`);
  if (request.method === "GET" && requestUrl.pathname === "/health") {
    sendJsonResponse(response, 200, { ok: true, settingsPath });
    return;
  }

  if (request.method === "GET" && requestUrl.pathname === "/state") {
    const runtimeState = await readRuntimeState({ settingsPath });
    sendJsonResponse(response, 200, {
      ok: true,
      settingsPath,
      runtimeState: {
        playMode: runtimeState.playMode,
        updatedAtUtc: runtimeState.updatedAtUtc
      }
    });
    return;
  }

  // 캐릭터 명령 결과 회답 — Unity 프로세서가 기록한 마지막 결과를 돌려준다.
  if (request.method === "GET" && requestUrl.pathname === "/command-result") {
    const result = await readCommandResult({ settingsPath });
    sendJsonResponse(response, 200, { ok: true, ...result });
    return;
  }

  // 라이브러리 목록은 읽기 전용 스냅샷이다. 쓰기·씬 조작은 /character-command로만.
  if (request.method === "GET" && requestUrl.pathname === "/characters") {
    try {
      const library = await readCharacterLibrary({ libraryPath });
      sendJsonResponse(response, 200, { ok: true, ...library });
    } catch (error) {
      sendJsonResponse(response, 500, { ok: false, error: error.message });
    }
    return;
  }

  // 캐릭터 라이브러리 명령 큐잉 — Play 모드 요건은 없고,
  // Unity 에디터 폴러가 에디트 모드에서도 소비한다.
  if (request.method === "POST" && requestUrl.pathname === "/character-command") {
    try {
      const body = await readJsonBody(request);
      const result = await withCommandQueueLock(() => queueCharacterCommand({
        settingsPath,
        action: body.action,
        characterId: body.characterId,
        presetId: body.presetId,
        path: body.path,
        params: body.params,
        now,
        createCommandId
      }));
      broadcast({ type: "character-command", ...result });
      sendJsonResponse(response, 200, result);
    } catch (error) {
      sendJsonResponse(response, 400, { error: error.message });
    }
    return;
  }

  if (request.method !== "POST" || requestUrl.pathname !== "/import-fbx") {
    sendJsonResponse(response, 404, { error: "Not found." });
    return;
  }

  try {
    if ((await readRuntimeState({ settingsPath })).playMode !== "playing") {
      sendJsonResponse(response, 409, { error: "Unity Play 상태에서만 사용할 수 있습니다." });
      return;
    }

    const body = await readJsonBody(request);
    // HTTP 요청을 Unity가 polling으로 읽을 수 있는 JSON command envelope로 변환
    const result = await withCommandQueueLock(() => queueImportFbxCommand({
      settingsPath,
      fbxPath: body.fbxPath,
      now,
      createCommandId
    }));
    const event = { type: "import-fbx", ...result };
    broadcast(event);
    sendJsonResponse(response, 200, result);
  } catch (error) {
    sendJsonResponse(response, 400, { error: error.message });
  }
}

// 허용 출처: Electron file:// 렌더러(Origin "null"), 로컬 개발 서버(localhost/127.0.0.1).
// 브리지는 127.0.0.1에만 바인드되지만 Origin 화이트리스트까지 둬야
// 외부 웹페이지의 drive-by 요청을 브라우저가 차단하게 된다.
function isAllowedOrigin(origin) {
  if (origin === "null" || origin === "file://") {
    return true;
  }
  try {
    const { hostname } = new URL(origin);
    return hostname === "localhost" || hostname === "127.0.0.1" || hostname === "[::1]";
  } catch {
    return false;
  }
}

// opaque 오리진 요청만 토큰을 요구한다 — 실제 웹 오리진은 Origin 허용목록이
// 이미 판별하고, 오리진 없는 요청(curl 등 비브라우저)은 로컬 도구로 본다.
function requiresBridgeToken(origin) {
  return origin === "null" || origin === "file://";
}

function writeCorsHeaders(response, origin) {
  const allowOrigin = origin !== undefined && isAllowedOrigin(origin) ? origin : "*";
  response.setHeader("Access-Control-Allow-Origin", allowOrigin);
  response.setHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
  response.setHeader("Access-Control-Allow-Headers", "Content-Type, X-Bridge-Token");
}

function sendJsonResponse(response, statusCode, body) {
  response.writeHead(statusCode, { "Content-Type": "application/json; charset=utf-8" });
  response.end(JSON.stringify(body));
}

function sendJson(webSocket, message) {
  webSocket.send(JSON.stringify(message));
}

// Node HTTP request stream을 끝까지 읽어 JSON body로 복원한다.
async function readJsonBody(request) {
  const chunks = [];
  for await (const chunk of request) {
    chunks.push(chunk);
  }

  const rawBody = Buffer.concat(chunks).toString("utf8");
  if (!rawBody.trim()) {
    return {};
  }

  return JSON.parse(rawBody);
}
