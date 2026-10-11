import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import {
  createDefaultSettingsDocument,
  queueCharacterCommand,
  readCommandResult,
  readRuntimeState,
  queueImportFbxCommand,
  resolveSettingsFilePath
} from "../server/settingsDocumentStore.js";

test("resolveSettingsFilePath uses explicit path before environment and defaults", () => {
  assert.equal(
    resolveSettingsFilePath({
      explicitPath: " C:/Settings/custom.json ",
      env: { UNITY_FBX2VMD_MAIN_RECORDING_SETTINGS_PATH: "C:/Settings/env.json" },
      localAppDataRoot: "C:/Users/User/AppData/Local"
    }),
    "C:/Settings/custom.json"
  );
});

test("resolveSettingsFilePath matches Unity default local app data layout", () => {
  assert.equal(
    resolveSettingsFilePath({
      env: {},
      localAppDataRoot: "C:/Users/User/AppData/Local"
    }),
    path.join(
      "C:/Users/User/AppData/Local",
      "Unity_Fbx2VMD",
      "MainRecordingSettings",
      "main-recording-settings.json"
    )
  );
});

test("createDefaultSettingsDocument matches Unity settings defaults", () => {
  assert.deepEqual(createDefaultSettingsDocument(), {
    schemaVersion: 1,
    updatedAtUtc: "",
    fbxPath: "",
    characterModelPath: "",
    captureWidth: 1920,
    captureHeight: 1080,
    openSettingsOnStart: true,
    runtimeState: {
      playMode: "stopped",
      updatedAtUtc: ""
    },
    pendingCommand: {
      commandId: "",
      action: "",
      fbxPath: "",
      characterId: "",
      presetId: "",
      path: "",
      paramsJson: "",
      requestedAtUtc: ""
    },
    lastCommandResult: {
      commandId: "",
      action: "",
      state: "",
      message: "",
      finishedAtUtc: ""
    }
  });
});

test("queueImportFbxCommand preserves existing settings and writes import command", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "main-recording-settings-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  await fs.writeFile(
    settingsPath,
    JSON.stringify({
      schemaVersion: 1,
      captureWidth: 1280,
      captureHeight: 720,
      openSettingsOnStart: false,
      characterModelPath: "C:/Models/model.vrm"
    }),
    "utf8"
  );

  const result = await queueImportFbxCommand({
    settingsPath,
    fbxPath: " C:/Motion/sample.fbx ",
    now: () => new Date("2026-06-17T00:00:00.000Z"),
    createCommandId: () => "cmd-123"
  });

  const saved = JSON.parse(await fs.readFile(settingsPath, "utf8"));
  assert.equal(result.commandId, "cmd-123");
  assert.equal(saved.updatedAtUtc, "2026-06-17T00:00:00.000Z");
  assert.equal(saved.captureWidth, 1280);
  assert.equal(saved.captureHeight, 720);
  assert.equal(saved.openSettingsOnStart, false);
  assert.equal(saved.characterModelPath, "C:/Models/model.vrm");
  assert.equal(saved.fbxPath, "C:/Motion/sample.fbx");
  assert.deepEqual(saved.pendingCommand, {
    commandId: "cmd-123",
    action: "ImportFbx",
    fbxPath: "C:/Motion/sample.fbx",
    characterId: "",
    presetId: "",
    path: "",
    paramsJson: "",
    requestedAtUtc: "2026-06-17T00:00:00.000Z"
  });
  assert.deepEqual(saved.runtimeState, {
    playMode: "stopped",
    updatedAtUtc: ""
  });
});

test("readRuntimeState normalizes missing and invalid play mode as stopped", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "main-recording-settings-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  await fs.writeFile(
    settingsPath,
    JSON.stringify({
      runtimeState: {
        playMode: "paused",
        updatedAtUtc: 100
      }
    }),
    "utf8"
  );

  const result = await readRuntimeState({ settingsPath });

  assert.deepEqual(result, {
    playMode: "stopped",
    updatedAtUtc: "",
    settingsPath
  });
});

test("queueCharacterCommand writes extended envelope fields", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "main-recording-settings-"));
  const settingsPath = path.join(tempRoot, "settings.json");

  const result = await queueCharacterCommand({
    settingsPath,
    action: "LoadCharacter",
    characterId: "char-01",
    presetId: "preset-9",
    path: " C:/Models/miku.vrm ",
    params: { presetName: "기본" },
    now: () => new Date("2026-10-09T02:00:00.000Z"),
    createCommandId: () => "cmd-char-1"
  });

  const saved = JSON.parse(await fs.readFile(settingsPath, "utf8"));
  assert.equal(result.accepted, true);
  assert.equal(result.commandId, "cmd-char-1");
  assert.equal(saved.pendingCommand.action, "LoadCharacter");
  assert.equal(saved.pendingCommand.characterId, "char-01");
  assert.equal(saved.pendingCommand.presetId, "preset-9");
  assert.equal(saved.pendingCommand.path, "C:/Models/miku.vrm");
  assert.equal(saved.pendingCommand.paramsJson, JSON.stringify({ presetName: "기본" }));
  assert.equal(saved.pendingCommand.requestedAtUtc, "2026-10-09T02:00:00.000Z");
});

test("queueCharacterCommand rejects empty action", async () => {
  await assert.rejects(
    () => queueCharacterCommand({ settingsPath: "x.json", action: " " }),
    /action/
  );
});

test("readCommandResult returns normalized last result", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "main-recording-settings-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  await fs.writeFile(
    settingsPath,
    JSON.stringify({
      lastCommandResult: {
        commandId: "cmd-9",
        action: "LoadCharacter",
        state: "success",
        message: "로드 완료",
        finishedAtUtc: "2026-10-09T02:00:00.000Z"
      }
    }),
    "utf8"
  );

  const result = await readCommandResult({ settingsPath });
  assert.equal(result.commandId, "cmd-9");
  assert.equal(result.state, "success");
  assert.equal(result.settingsPath, settingsPath);
});

test("readCommandResult normalizes missing result fields", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "main-recording-settings-"));
  const settingsPath = path.join(tempRoot, "settings.json");

  const result = await readCommandResult({ settingsPath });
  assert.equal(result.commandId, "");
  assert.equal(result.state, "");
});

test("queueCharacterCommand rejects a second command while one is pending", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "main-recording-settings-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  await queueCharacterCommand({
    settingsPath,
    action: "LoadCharacter",
    characterId: "char-01",
    createCommandId: () => "cmd-first"
  });

  await assert.rejects(
    () => queueCharacterCommand({
      settingsPath,
      action: "RemoveCharacter",
      characterId: "char-02",
      createCommandId: () => "cmd-second"
    }),
    /대기 중/
  );
  const saved = JSON.parse(await fs.readFile(settingsPath, "utf8"));
  assert.equal(saved.pendingCommand.commandId, "cmd-first");
  assert.equal(saved.pendingCommand.action, "LoadCharacter");
});

test("queueCharacterCommand rejects an unsupported action", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "main-recording-settings-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  await fs.writeFile(settingsPath, "{}", "utf8");

  await assert.rejects(
    () => queueCharacterCommand({ settingsPath, action: "Explode" }),
    /지원하지 않는 캐릭터 명령/
  );
  const saved = JSON.parse(await fs.readFile(settingsPath, "utf8"));
  assert.equal(saved.pendingCommand?.commandId ?? "", "");
});

test("queueImportFbxCommand rejects while a command is pending", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "main-recording-settings-"));
  const settingsPath = path.join(tempRoot, "settings.json");
  await queueCharacterCommand({
    settingsPath,
    action: "RefreshCharacter",
    characterId: "char-01",
    createCommandId: () => "cmd-pending"
  });

  await assert.rejects(
    () => queueImportFbxCommand({ settingsPath, fbxPath: "D:/m.fbx" }),
    /대기 중/
  );
});
