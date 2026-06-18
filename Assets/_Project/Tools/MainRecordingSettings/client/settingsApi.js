export const DEFAULT_API_BASE_URL = "http://localhost:4100";
export const DEFAULT_WS_URL = "ws://localhost:4100/settings";

// FBX 가져오기 요청을 실제 fetch 호출에 넘길 수 있는 형태로 만든다.
// UI와 테스트가 같은 요청 생성 로직을 공유하도록 분리했다.
export function createImportFbxRequest({ apiBaseUrl = DEFAULT_API_BASE_URL, fbxPath }) {
  const normalizedPath = normalizeRequiredText(fbxPath, "FBX 경로 필요");
  const baseUrl = normalizeBaseUrl(apiBaseUrl);

  return {
    url: `${baseUrl}/import-fbx`,
    init: {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ fbxPath: normalizedPath })
    }
  };
}

// 로컬 브리지 서버의 /import-fbx endpoint에 FBX 경로를 전달한다.
// fetchImpl을 주입받아 브라우저/Electron 환경과 단위 테스트를 같은 코드로 처리한다.
export async function postImportFbx({
  apiBaseUrl = DEFAULT_API_BASE_URL,
  fbxPath,
  fetchImpl = globalThis.fetch
}) {
  if (typeof fetchImpl !== "function") {
    throw new Error("fetch 구현 필요");
  }

  const request = createImportFbxRequest({ apiBaseUrl, fbxPath });
  const response = await fetchImpl(request.url, request.init);
  if (!response.ok) {
    const body = typeof response.text === "function" ? await response.text() : "";
    const detail = body ? `: ${body}` : "";
    throw new Error(`HTTP 오류 ${response.status}${detail}`);
  }

  if (typeof response.json !== "function") {
    return {};
  }

  return await response.json();
}

export function createRuntimeStateRequest({ apiBaseUrl = DEFAULT_API_BASE_URL } = {}) {
  const baseUrl = normalizeBaseUrl(apiBaseUrl);

  return {
    url: `${baseUrl}/state`,
    init: {
      method: "GET"
    }
  };
}

export async function getRuntimeState({
  apiBaseUrl = DEFAULT_API_BASE_URL,
  fetchImpl = globalThis.fetch
} = {}) {
  if (typeof fetchImpl !== "function") {
    throw new Error("fetch 구현 필요");
  }

  const request = createRuntimeStateRequest({ apiBaseUrl });
  const response = await fetchImpl(request.url, request.init);
  if (!response.ok) {
    const body = typeof response.text === "function" ? await response.text() : "";
    const detail = body ? `: ${body}` : "";
    throw new Error(`HTTP 오류 ${response.status}${detail}`);
  }

  if (typeof response.json !== "function") {
    return { runtimeState: { playMode: "stopped", updatedAtUtc: "" } };
  }

  return await response.json();
}

export function createPlayModeAutoConnectionController({
  apiBaseUrl = DEFAULT_API_BASE_URL,
  wsUrl = DEFAULT_WS_URL,
  pollIntervalMs = 1000,
  fetchRuntimeState = () => getRuntimeState({ apiBaseUrl: resolveRuntimeValue(apiBaseUrl) }),
  createWebSocketChannel = createSettingsWebSocket,
  setTimeoutImpl = globalThis.setTimeout,
  clearTimeoutImpl = globalThis.clearTimeout,
  onStatusChange = () => {},
  onMessage = () => {},
  onError = () => {}
} = {}) {
  let socketChannel = null;
  let timeoutId = null;
  let running = false;
  let polling = false;

  async function pollOnce() {
    const stateDocument = await fetchRuntimeState();
    const playMode = normalizePlayMode(stateDocument?.runtimeState?.playMode ?? stateDocument?.playMode);

    if (playMode === "playing") {
      connectIfNeeded();
      return;
    }

    disconnectIfNeeded();
  }

  function connectIfNeeded() {
    if (socketChannel) {
      return;
    }

    socketChannel = createWebSocketChannel({
      wsUrl: resolveRuntimeValue(wsUrl),
      onStatusChange: (status) => {
        if (status === "closed" || status === "error") {
          socketChannel = null;
        }

        onStatusChange(status);
      },
      onMessage,
      onError
    });
  }

  function disconnectIfNeeded() {
    if (socketChannel) {
      socketChannel.disconnect();
      socketChannel = null;
    }

    onStatusChange("closed");
  }

  async function loop() {
    if (!running || polling) {
      scheduleNextPoll();
      return;
    }

    polling = true;
    try {
      await pollOnce();
    } catch (error) {
      disconnectIfNeeded();
      onStatusChange("error");
      onError(error);
    } finally {
      polling = false;
      scheduleNextPoll();
    }
  }

  function scheduleNextPoll() {
    if (!running || typeof setTimeoutImpl !== "function") {
      return;
    }

    timeoutId = setTimeoutImpl(() => {
      void loop();
    }, pollIntervalMs);
  }

  return {
    async pollOnce() {
      try {
        await pollOnce();
      } catch (error) {
        disconnectIfNeeded();
        onStatusChange("error");
        onError(error);
      }
    },
    start() {
      if (running) {
        return;
      }

      running = true;
      void loop();
    },
    stop() {
      running = false;
      if (timeoutId != null && typeof clearTimeoutImpl === "function") {
        clearTimeoutImpl(timeoutId);
        timeoutId = null;
      }

      disconnectIfNeeded();
    },
    isConnected() {
      return socketChannel != null;
    }
  };
}

// 설정창에서 WebSocket 상태와 서버 이벤트를 구독한다.
// WebSocketImpl을 주입받아 실제 브라우저 객체 없이도 연결 상태 전이를 테스트할 수 있다.
export function createSettingsWebSocket({
  wsUrl = DEFAULT_WS_URL,
  WebSocketImpl = globalThis.WebSocket,
  onStatusChange = () => {},
  onMessage = () => {},
  onError = () => {}
}) {
  const normalizedUrl = normalizeRequiredText(wsUrl, "WebSocket 주소 필요");
  if (typeof WebSocketImpl !== "function") {
    throw new Error("WebSocket 구현 필요");
  }

  onStatusChange("connecting");
  const socket = new WebSocketImpl(normalizedUrl);
  socket.onopen = () => onStatusChange("open");
  socket.onclose = () => onStatusChange("closed");
  socket.onerror = (event) => {
    onStatusChange("error");
    onError(event);
  };
  socket.onmessage = (event) => {
    onMessage(parseSocketMessage(event.data));
  };

  return {
    socket,
    disconnect() {
      socket.close();
    }
  };
}

// 사용자가 주소 끝에 /를 붙여도 endpoint 조합이 중복 슬래시를 만들지 않게 한다.
function normalizeBaseUrl(value) {
  return normalizeRequiredText(value, "API 기본 주소 필요").replace(/\/+$/, "");
}

function resolveRuntimeValue(value) {
  return typeof value === "function" ? value() : value;
}

function normalizePlayMode(value) {
  return value === "playing" ? "playing" : "stopped";
}

function normalizeRequiredText(value, message) {
  const normalized = typeof value === "string" ? value.trim() : "";
  if (!normalized) {
    throw new Error(message);
  }

  return normalized;
}

// 서버가 JSON이 아닌 진단 문자열을 보내도 UI 로그가 깨지지 않게 감싼다.
function parseSocketMessage(data) {
  if (typeof data !== "string") {
    return data;
  }

  try {
    return JSON.parse(data);
  } catch {
    return { type: "텍스트", value: data };
  }
}
