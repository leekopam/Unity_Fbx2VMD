import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import WebSocket from "ws";

import { createSettingsBridgeServer } from "../server/settingsBridgeServer.js";

export async function runBridgeE2e({
  tempRoot,
  commandId = "cmd-bridge-e2e",
  characterCommandId = "cmd-bridge-e2e-character",
  now = () => new Date(),
  fbxFileName = "bridge-e2e.fbx"
} = {}) {
  const root = tempRoot ?? await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-e2e-"));
  await fs.mkdir(root, { recursive: true });

  const settingsPath = path.join(root, "main-recording-settings.json");
  const fbxPath = path.join(root, fbxFileName);
  await fs.writeFile(fbxPath, "temporary FBX placeholder for bridge E2E\n", "utf8");
  await fs.writeFile(settingsPath, JSON.stringify({ runtimeState: { playMode: "playing" } }), "utf8");

  // 호출 순서: character 수락 → (invalid는 id 미소비) → import 수락 → duplicate 거절.
  const commandIds = [characterCommandId, commandId, commandId];
  let nextCommandId = 0;
  const bridge = createSettingsBridgeServer({
    settingsPath,
    createCommandId: () => commandIds[nextCommandId++] ?? commandId,
    now
  });

  let socket;
  const webSocketMessages = [];

  await bridge.listen(0);

  try {
    socket = new WebSocket(`${bridge.wsUrl}/settings`);
    socket.on("message", (data) => {
      webSocketMessages.push(JSON.parse(data.toString()));
    });

    await waitForSocketOpen(socket);
    await waitUntil(() => webSocketMessages.some((message) => message.type === "status"));

    // 캐릭터 명령 → pendingCommand 봉투 계약 검증. 뒤의 import-fbx가 덮어쓰므로 먼저 둔다.
    const characterParams = { label: "브리지E2E", retries: 2 };
    const characterResponse = await fetch(`${bridge.baseUrl}/character-command`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        action: "LoadCharacter",
        characterId: "char-e2e",
        presetId: "",
        path: "D:/e2e/character.vrm",
        params: characterParams
      })
    });
    const characterHttpResponse = await characterResponse.json();

    await waitUntil(() => webSocketMessages.some(
      (message) => message.type === "character-command"
        && message.commandId === characterCommandId
    ));

    const characterSaved = JSON.parse(await fs.readFile(settingsPath, "utf8"));
    const savedCharacterCommand = characterSaved.pendingCommand ?? {};
    const characterEventReceived = webSocketMessages.some(
      (message) => message.type === "character-command"
        && message.commandId === characterCommandId
    );

    // 미지원 action은 큐에 들어가지 않고 400으로 거절되어야 한다.
    const invalidResponse = await fetch(`${bridge.baseUrl}/character-command`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action: "FlyToMoon" })
    });
    const invalidRejected = invalidResponse.status === 400;

    // Unity 폴러 소비 시뮬레이션 — 에디터가 pendingCommand를 빈 봉투로 되돌리는 것과 동일.
    characterSaved.pendingCommand = createEmptyCommandEnvelope();
    await fs.writeFile(settingsPath, JSON.stringify(characterSaved), "utf8");

    const response = await fetch(`${bridge.baseUrl}/import-fbx`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ fbxPath })
    });
    const httpResponse = await response.json();

    await waitUntil(() => webSocketMessages.some(
      (message) => message.type === "import-fbx" && message.commandId === commandId
    ));

    const saved = JSON.parse(await fs.readFile(settingsPath, "utf8"));
    const savedCommand = saved.pendingCommand ?? {};
    const importEventReceived = webSocketMessages.some(
      (message) => message.type === "import-fbx" && message.commandId === commandId
    );

    // 대기 중인 명령이 있으면 다음 명령은 거절되어야 한다(무소식 소실 방지).
    const duplicateResponse = await fetch(`${bridge.baseUrl}/import-fbx`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ fbxPath })
    });
    const duplicateRejected = duplicateResponse.status === 400;

    return {
      ok: response.ok && httpResponse.accepted === true
        && savedCommand.commandId === commandId
        && savedCommand.fbxPath === fbxPath
        && importEventReceived
        && duplicateRejected
        && characterResponse.ok && characterHttpResponse.accepted === true
        && savedCharacterCommand.commandId === characterCommandId
        && savedCharacterCommand.action === "LoadCharacter"
        && savedCharacterCommand.characterId === "char-e2e"
        && savedCharacterCommand.path === "D:/e2e/character.vrm"
        && savedCharacterCommand.paramsJson === JSON.stringify(characterParams)
        && characterEventReceived
        && invalidRejected,
      baseUrl: bridge.baseUrl,
      wsUrl: bridge.wsUrl,
      settingsPath,
      fbxPath,
      httpStatus: response.status,
      httpResponse,
      savedCommand,
      duplicateRejected,
      characterHttpStatus: characterResponse.status,
      characterHttpResponse,
      savedCharacterCommand,
      invalidRejected,
      webSocketMessages
    };
  } finally {
    if (socket) {
      closeSocket(socket);
    }

    await bridge.close();
  }
}

// Unity MainRecordingSettingsCommandEnvelope의 기본값과 동일한 빈 명령 봉투.
function createEmptyCommandEnvelope() {
  return {
    commandId: "",
    action: "",
    fbxPath: "",
    characterId: "",
    presetId: "",
    path: "",
    paramsJson: "",
    requestedAtUtc: ""
  };
}

function waitForSocketOpen(socket) {
  return new Promise((resolve, reject) => {
    socket.once("open", resolve);
    socket.once("error", reject);
  });
}

async function waitUntil(predicate, timeoutMs = 3000) {
  const startedAt = Date.now();
  while (!predicate()) {
    if (Date.now() - startedAt > timeoutMs) {
      throw new Error("Timed out waiting for bridge E2E condition.");
    }

    await new Promise((resolve) => setTimeout(resolve, 25));
  }
}

function closeSocket(socket) {
  if (socket.readyState === socket.CLOSED || socket.readyState === socket.CLOSING) {
    return;
  }

  socket.close();
}
