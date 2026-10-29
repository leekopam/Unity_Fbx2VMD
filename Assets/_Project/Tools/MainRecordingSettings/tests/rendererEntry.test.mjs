import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import vm from "node:vm";

import {
  getE2eImportVrmPath,
  getSmokeImportFbxPath,
  getSmokePanelTarget,
  getSmokeScreenshotPath,
  getSmokeSettingsPath,
  getRendererEntry,
  initializeSmokeSettings,
  isDevelopmentMode,
  isSmokeTestMode
} from "../electron/rendererEntry.js";
import { createElectronRuntimeEnv } from "../scripts/electronEnv.js";

test("preload는 첫 화면 실행 전에 소유 브리지 설정을 주입한다", async () => {
  const source = await fs.readFile(new URL("../electron/preload.cjs", import.meta.url), "utf8");
  const config = { apiBaseUrl: "http://127.0.0.1:12345", wsUrl: "ws://127.0.0.1:12345/settings", token: "fixture" };
  const exposed = [];
  vm.runInNewContext(source, { require: () => ({
    contextBridge: { exposeInMainWorld: (name, value) => exposed.push([name, value]) },
    ipcRenderer: { sendSync: channel => {
      assert.equal(channel, "settings:get-bridge-config");
      assert.equal(exposed.length, 0);
      return config;
    } }
  }) });
  assert.equal(exposed[0][0], "mainRecordingSettingsBridgeConfig");
  assert.equal(exposed[0][1], config);
  assert.equal(exposed[1][0], "settingsShell");
});

test("preload는 브리지 미준비 오류를 알리고 불완전 설정을 노출하지 않는다", async () => {
  const source = await fs.readFile(new URL("../electron/preload.cjs", import.meta.url), "utf8");
  const exposed = [];
  assert.throws(() => vm.runInNewContext(source, { require: () => ({
    contextBridge: { exposeInMainWorld: name => exposed.push(name) },
    ipcRenderer: { sendSync: () => null }
  }) }), /설정 브리지가 준비되지 않았습니다/);
  assert.deepEqual(exposed, []);
});

test("VRM 시험 파일 지정은 E2E 실행에서만 허용한다", () => {
  const argument = "--e2e-import-vrm=C:/Fixtures/model.vrm";
  assert.equal(getE2eImportVrmPath({ argv: [argument] }), "");
  assert.equal(getE2eImportVrmPath({ argv: ["--e2e-test", argument] }), "C:/Fixtures/model.vrm");
  assert.equal(getE2eImportVrmPath({ argv: ["--e2e-test"] }), "");
});

test("시험 설정 초기화는 기존 실제 연결 문서를 그대로 보존한다", async () => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "mrs-settings-preserve-"));
  const settingsPath = path.join(root, "settings.json");
  const original = '{"captureWidth":3840,"runtimeState":{"playMode":"stopped"},"pendingCommand":{"commandId":"keep"}}\n';
  try {
    await fs.writeFile(settingsPath, original, "utf8");
    await initializeSmokeSettings(settingsPath);
    assert.equal(await fs.readFile(settingsPath, "utf8"), original);
  } finally {
    await fs.rm(root, { recursive: true, force: true });
  }
});

test("시험 설정이 없으면 mock Play 상태 문서를 생성한다", async () => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "mrs-settings-create-"));
  const settingsPath = path.join(root, "nested", "settings.json");
  try {
    await initializeSmokeSettings(settingsPath);
    assert.deepEqual(JSON.parse(await fs.readFile(settingsPath, "utf8")), {
      runtimeState: { playMode: "playing" }
    });
  } finally {
    await fs.rm(root, { recursive: true, force: true });
  }
});

test("isDevelopmentMode returns true when --dev is present", () => {
  assert.equal(
    isDevelopmentMode({
      argv: ["electron", ".", "--dev"],
      env: {}
    }),
    true
  );
});

test("isDevelopmentMode returns true when NODE_ENV is development", () => {
  assert.equal(
    isDevelopmentMode({
      argv: ["electron", "."],
      env: { NODE_ENV: "development" }
    }),
    true
  );
});

test("isSmokeTestMode returns true when --smoke-test is present", () => {
  assert.equal(
    isSmokeTestMode({
      argv: ["electron", ".", "--smoke-test"]
    }),
    true
  );
});

test("getSmokeImportFbxPath returns the FBX path smoke argument", () => {
  assert.equal(
    getSmokeImportFbxPath({
      argv: ["electron", ".", "--smoke-import-fbx=C:/Motion/smoke.fbx"]
    }),
    "C:/Motion/smoke.fbx"
  );
});

test("getSmokeSettingsPath returns the settings path smoke argument", () => {
  assert.equal(
    getSmokeSettingsPath({
      argv: ["electron", ".", "--smoke-settings-path=C:/Temp/settings.json"]
    }),
    "C:/Temp/settings.json"
  );
});

test("getSmokeScreenshotPath returns the screenshot path smoke argument", () => {
  assert.equal(
    getSmokeScreenshotPath({
      argv: ["electron", ".", "--smoke-screenshot-path=C:/Temp/settings.png"]
    }),
    "C:/Temp/settings.png"
  );
});

test("getSmokePanelTarget returns the panel target smoke argument", () => {
  assert.equal(
    getSmokePanelTarget({
      argv: ["electron", ".", "--smoke-panel-target=camera"]
    }),
    "camera"
  );
});

test("getRendererEntry returns localhost URL in development mode", () => {
  const entry = getRendererEntry({
    argv: ["electron", ".", "--dev"],
    env: {},
    appRoot: "D:/project/Assets/_Project/Tools/MainRecordingSettings"
  });

  assert.deepEqual(entry, {
    mode: "development",
    type: "url",
    target: "http://localhost:3000"
  });
});

test("getRendererEntry returns build index file in production mode", () => {
  const appRoot = "D:/project/Assets/_Project/Tools/MainRecordingSettings";
  const entry = getRendererEntry({
    argv: ["electron", "."],
    env: {},
    appRoot
  });

  assert.equal(entry.mode, "production");
  assert.equal(entry.type, "file");
  assert.equal(entry.target, path.join(appRoot, "build", "index.html"));
});

test("createElectronRuntimeEnv removes ELECTRON_RUN_AS_NODE for app launch", () => {
  const env = createElectronRuntimeEnv({
    ELECTRON_RUN_AS_NODE: "1",
    PATH: "C:/Tools"
  });

  assert.equal(Object.hasOwn(env, "ELECTRON_RUN_AS_NODE"), false);
  assert.equal(env.PATH, "C:/Tools");
});
