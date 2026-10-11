import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import { runBridgeE2e } from "../scripts/bridgeE2e.js";

test("bridge E2E opens HTTP and WebSocket endpoints and writes an import command", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "settings-bridge-e2e-test-"));

  const result = await runBridgeE2e({
    tempRoot,
    commandId: "cmd-e2e-test",
    now: () => new Date("2026-06-17T02:00:00.000Z")
  });

  const saved = JSON.parse(await fs.readFile(result.settingsPath, "utf8"));

  assert.equal(result.ok, true);
  assert.equal(result.httpResponse.accepted, true);
  assert.equal(result.httpResponse.commandId, "cmd-e2e-test");
  assert.equal(result.savedCommand.commandId, "cmd-e2e-test");
  assert.equal(result.savedCommand.action, "ImportFbx");
  assert.equal(saved.pendingCommand.commandId, "cmd-e2e-test");
  assert.equal(saved.pendingCommand.fbxPath, result.fbxPath);
  assert.equal(saved.updatedAtUtc, "2026-06-17T02:00:00.000Z");
  assert.ok(result.webSocketMessages.some((message) => message.type === "status"));
  assert.ok(result.webSocketMessages.some((message) => message.type === "import-fbx"));

  // 큐 계약: 대기 중 중복 거절, 미지원 action 거절, 캐릭터 명령 봉투·중계 검증
  assert.equal(result.duplicateRejected, true);
  assert.equal(result.invalidRejected, true);
  assert.equal(result.characterHttpResponse.accepted, true);
  assert.equal(result.savedCharacterCommand.action, "LoadCharacter");
  assert.equal(result.savedCharacterCommand.characterId, "char-e2e");
  assert.equal(
    result.savedCharacterCommand.paramsJson,
    JSON.stringify({ label: "브리지E2E", retries: 2 })
  );
  assert.ok(result.webSocketMessages.some(
    (message) => message.type === "character-command"
  ));
});
