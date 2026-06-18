import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import WebSocket from "ws";

import { createSettingsBridgeServer } from "../server/settingsBridgeServer.js";

export async function runBridgeE2e({
  tempRoot,
  commandId = "cmd-bridge-e2e",
  now = () => new Date(),
  fbxFileName = "bridge-e2e.fbx"
} = {}) {
  const root = tempRoot ?? await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-e2e-"));
  await fs.mkdir(root, { recursive: true });

  const settingsPath = path.join(root, "main-recording-settings.json");
  const fbxPath = path.join(root, fbxFileName);
  await fs.writeFile(fbxPath, "temporary FBX placeholder for bridge E2E\n", "utf8");

  const bridge = createSettingsBridgeServer({
    settingsPath,
    createCommandId: () => commandId,
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

    return {
      ok: response.ok && httpResponse.accepted === true
        && savedCommand.commandId === commandId
        && savedCommand.fbxPath === fbxPath
        && importEventReceived,
      baseUrl: bridge.baseUrl,
      wsUrl: bridge.wsUrl,
      settingsPath,
      fbxPath,
      httpStatus: response.status,
      httpResponse,
      savedCommand,
      webSocketMessages
    };
  } finally {
    if (socket) {
      closeSocket(socket);
    }

    await bridge.close();
  }
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
