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
  await fs.writeFile(settingsPath, JSON.stringify({ runtimeState: { playMode: "playing" } }), "utf8");
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
  await fs.writeFile(settingsPath, JSON.stringify({ runtimeState: { playMode: "playing" } }), "utf8");
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

test("settings bridge rejects import in Edit Mode without changing the command", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  const bridge = createSettingsBridgeServer({ settingsPath });
  await bridge.listen(0);
  try {
    const response = await fetch(`${bridge.baseUrl}/import-fbx`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ fbxPath: "C:/Motion/edit.fbx" })
    });
    assert.equal(response.status, 409);
    assert.equal((await response.json()).error, "Unity Play 상태에서만 사용할 수 있습니다.");
    await assert.rejects(fs.readFile(settingsPath, "utf8"), { code: "ENOENT" });
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

test("settings bridge queues character command regardless of play mode", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  await fs.writeFile(settingsPath, JSON.stringify({ runtimeState: { playMode: "stopped" } }), "utf8");
  const bridge = createSettingsBridgeServer({
    settingsPath,
    createCommandId: () => "cmd-char",
    now: () => new Date("2026-10-09T03:00:00.000Z")
  });

  await bridge.listen(0);
  try {
    const response = await fetch(`${bridge.baseUrl}/character-command`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action: "LoadCharacter", characterId: "char-01" })
    });
    const body = await response.json();
    const saved = JSON.parse(await fs.readFile(settingsPath, "utf8"));

    assert.equal(response.status, 200);
    assert.equal(body.accepted, true);
    assert.equal(body.commandId, "cmd-char");
    assert.equal(saved.pendingCommand.action, "LoadCharacter");
    assert.equal(saved.pendingCommand.characterId, "char-01");
  } finally {
    await bridge.close();
  }
});

test("settings bridge returns last command result through GET /command-result", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  await fs.writeFile(
    settingsPath,
    JSON.stringify({
      lastCommandResult: {
        commandId: "cmd-7",
        action: "ApplyPreset",
        state: "error",
        message: "프리셋 없음",
        finishedAtUtc: "2026-10-09T03:00:00.000Z"
      }
    }),
    "utf8"
  );
  const bridge = createSettingsBridgeServer({ settingsPath });

  await bridge.listen(0);
  try {
    const response = await fetch(`${bridge.baseUrl}/command-result`);
    const body = await response.json();

    assert.equal(response.status, 200);
    assert.equal(body.ok, true);
    assert.equal(body.commandId, "cmd-7");
    assert.equal(body.state, "error");
  } finally {
    await bridge.close();
  }
});

test("settings bridge serves the character library snapshot through GET /characters", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  const libraryPath = path.join(tempRoot, "CharacterLibrary", "characters.json");
  await fs.mkdir(path.dirname(libraryPath), { recursive: true });
  await fs.writeFile(libraryPath, JSON.stringify({
    activeCharacterId: "abc",
    entries: [
      { id: "abc", displayName: "Miku", status: "ready", meta: { author: "YYB" } }
    ]
  }), "utf8");
  const bridge = createSettingsBridgeServer({ settingsPath, libraryPath });

  await bridge.listen(0);
  try {
    const response = await fetch(`${bridge.baseUrl}/characters`);
    const body = await response.json();

    assert.equal(response.status, 200);
    assert.equal(body.ok, true);
    assert.equal(body.activeCharacterId, "abc");
    assert.equal(body.characters.length, 1);
    assert.equal(body.characters[0].displayName, "Miku");
  } finally {
    await bridge.close();
  }
});

test("settings bridge returns an empty snapshot when the library file is missing", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  const libraryPath = path.join(tempRoot, "missing", "characters.json");
  const bridge = createSettingsBridgeServer({ settingsPath, libraryPath });

  await bridge.listen(0);
  try {
    const response = await fetch(`${bridge.baseUrl}/characters`);
    const body = await response.json();

    assert.equal(response.status, 200);
    assert.equal(body.ok, true);
    assert.deepEqual(body.characters, []);
  } finally {
    await bridge.close();
  }
});

test("settings bridge rejects requests from a disallowed Origin", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  const bridge = createSettingsBridgeServer({ settingsPath });

  await bridge.listen(0);
  try {
    const response = await fetch(`${bridge.baseUrl}/character-command`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Origin: "https://evil.example"
      },
      body: JSON.stringify({ action: "RemoveCharacter", characterId: "x" })
    });
    assert.equal(response.status, 403);
    const saved = JSON.parse(await fs.readFile(settingsPath, "utf8").catch(() => "{}"));
    assert.equal(saved.pendingCommand?.commandId ?? "", "");
  } finally {
    await bridge.close();
  }
});

test("settings bridge rejects opaque origin without a bridge token", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  const bridge = createSettingsBridgeServer({ settingsPath });

  await bridge.listen(0);
  try {
    const response = await fetch(`${bridge.baseUrl}/characters`, {
      headers: { Origin: "null" }
    });
    assert.equal(response.status, 403);
  } finally {
    await bridge.close();
  }
});

test("settings bridge accepts opaque origin with a valid bridge token", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  const libraryPath = path.join(tempRoot, "missing", "characters.json");
  const bridge = createSettingsBridgeServer({ settingsPath, libraryPath });

  await bridge.listen(0);
  try {
    // 프로덕션 Electron(file://) 요청 형태 재현 — Origin은 null이고
    // Sec-Fetch-Site는 cross-site여도 토큰이 맞으면 허용해야 한다.
    const response = await fetch(`${bridge.baseUrl}/characters`, {
      headers: {
        Origin: "null",
        "Sec-Fetch-Site": "cross-site",
        "X-Bridge-Token": bridge.token
      }
    });
    assert.equal(response.status, 200);
    assert.equal(response.headers.get("access-control-allow-origin"), "null");
  } finally {
    await bridge.close();
  }
});

test("settings bridge rejects opaque origin with a wrong bridge token", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  const bridge = createSettingsBridgeServer({ settingsPath });

  await bridge.listen(0);
  try {
    const response = await fetch(`${bridge.baseUrl}/characters`, {
      headers: {
        Origin: "null",
        "X-Bridge-Token": "wrong-token"
      }
    });
    assert.equal(response.status, 403);
  } finally {
    await bridge.close();
  }
});

test("settings bridge allows a localhost dev origin and echoes it in CORS", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  const libraryPath = path.join(tempRoot, "missing", "characters.json");
  const bridge = createSettingsBridgeServer({ settingsPath, libraryPath });

  await bridge.listen(0);
  try {
    const response = await fetch(`${bridge.baseUrl}/characters`, {
      headers: { Origin: "http://localhost:3000" }
    });
    assert.equal(response.status, 200);
    assert.equal(response.headers.get("access-control-allow-origin"), "http://localhost:3000");
  } finally {
    await bridge.close();
  }
});

test("settings bridge rejects a second character command while one is pending", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  const bridge = createSettingsBridgeServer({ settingsPath });

  await bridge.listen(0);
  try {
    const first = await fetch(`${bridge.baseUrl}/character-command`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action: "LoadCharacter", characterId: "char-01" })
    });
    assert.equal(first.status, 200);

    const second = await fetch(`${bridge.baseUrl}/character-command`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action: "RemoveCharacter", characterId: "char-01" })
    });
    assert.equal(second.status, 400);
    const saved = JSON.parse(await fs.readFile(settingsPath, "utf8"));
    assert.equal(saved.pendingCommand.action, "LoadCharacter");
  } finally {
    await bridge.close();
  }
});
