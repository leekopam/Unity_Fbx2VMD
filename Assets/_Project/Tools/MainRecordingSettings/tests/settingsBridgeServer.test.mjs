import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import WebSocket from "ws";

import { createSettingsBridgeServer } from "../server/settingsBridgeServer.js";

test("settings bridge accepts POST /import-fbx and writes command document", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  const bridge = createSettingsBridgeServer({
    settingsPath,
    createCommandId: () => "cmd-http",
    now: () => new Date("2026-06-17T01:00:00.000Z")
  });

  await bridge.listen(0);
  try {
    const response = await fetch(`${bridge.baseUrl}/import-fbx`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ fbxPath: "C:/Motion/http.fbx" })
    });
    const body = await response.json();
    const saved = JSON.parse(await fs.readFile(settingsPath, "utf8"));

    assert.equal(response.status, 200);
    assert.equal(response.headers.get("access-control-allow-origin"), "*");
    assert.deepEqual(body, {
      accepted: true,
      commandId: "cmd-http",
      fbxPath: "C:/Motion/http.fbx",
      settingsPath
    });
    assert.equal(saved.pendingCommand.commandId, "cmd-http");
    assert.equal(saved.pendingCommand.action, "ImportFbx");
  } finally {
    await bridge.close();
  }
});

test("settings bridge broadcasts import command to WebSocket clients", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  const bridge = createSettingsBridgeServer({
    settingsPath,
    createCommandId: () => "cmd-ws",
    now: () => new Date("2026-06-17T01:10:00.000Z")
  });

  await bridge.listen(0);
  const socket = new WebSocket(`${bridge.wsUrl}/settings`);
  const messages = [];
  socket.on("message", (data) => messages.push(JSON.parse(data.toString())));

  try {
    await waitForSocketOpen(socket);
    await waitUntil(() => messages.some((message) => message.type === "status"));

    await fetch(`${bridge.baseUrl}/import-fbx`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ fbxPath: "C:/Motion/ws.fbx" })
    });

    await waitUntil(() => messages.some((message) => message.type === "import-fbx"));
    assert.deepEqual(messages.at(-1), {
      type: "import-fbx",
      accepted: true,
      commandId: "cmd-ws",
      fbxPath: "C:/Motion/ws.fbx",
      settingsPath
    });
  } finally {
    socket.close();
    await bridge.close();
  }
});

test("settings bridge exposes normalized runtime state through GET /state", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  await fs.writeFile(
    settingsPath,
    JSON.stringify({
      runtimeState: {
        playMode: "playing",
        updatedAtUtc: "2026-06-18T04:00:00.000Z"
      }
    }),
    "utf8"
  );
  const bridge = createSettingsBridgeServer({ settingsPath });

  await bridge.listen(0);
  try {
    const response = await fetch(`${bridge.baseUrl}/state`);
    const body = await response.json();

    assert.equal(response.status, 200);
    assert.deepEqual(body, {
      ok: true,
      settingsPath,
      runtimeState: {
        playMode: "playing",
        updatedAtUtc: "2026-06-18T04:00:00.000Z"
      }
    });
  } finally {
    await bridge.close();
  }
});

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
      throw new Error("Timed out waiting for condition.");
    }

    await new Promise((resolve) => setTimeout(resolve, 25));
  }
}
