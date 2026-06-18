import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import {
  createDefaultSettingsDocument,
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
      requestedAtUtc: ""
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
