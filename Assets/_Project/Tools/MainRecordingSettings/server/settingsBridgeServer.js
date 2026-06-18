import http from "node:http";
import { WebSocketServer } from "ws";

import {
  queueImportFbxCommand,
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
  now,
  createCommandId
} = {}) {
  const server = http.createServer(async (request, response) => {
    await handleHttpRequest({
      request,
      response,
      settingsPath,
      now,
      createCommandId,
      broadcast
    });
  });
  const webSocketServer = new WebSocketServer({ noServer: true });

  // WebSocket upgrade 요청이 오면 /settings 경로로만 연결을 허용
  server.on("upgrade", (request, socket, head) => {
    if (new URL(request.url, `http://${request.headers.host}`).pathname !== "/settings") {
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
  now,
  createCommandId,
  broadcast
}) {
  writeCorsHeaders(response);

  // 개발 서버에서 호출할 수 있도록 브라우저 preflight를 먼저 처리
  if (request.method === "OPTIONS") {
    response.writeHead(204);
    response.end();
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

  if (request.method !== "POST" || requestUrl.pathname !== "/import-fbx") {
    sendJsonResponse(response, 404, { error: "Not found." });
    return;
  }

  try {
    const body = await readJsonBody(request);
    // HTTP 요청을 Unity가 polling으로 읽을 수 있는 JSON command envelope로 변환
    const result = await queueImportFbxCommand({
      settingsPath,
      fbxPath: body.fbxPath,
      now,
      createCommandId
    });
    const event = { type: "import-fbx", ...result };
    broadcast(event);
    sendJsonResponse(response, 200, result);
  } catch (error) {
    sendJsonResponse(response, 400, { error: error.message });
  }
}

function writeCorsHeaders(response) {
  response.setHeader("Access-Control-Allow-Origin", "*");
  response.setHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
  response.setHeader("Access-Control-Allow-Headers", "Content-Type");
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
