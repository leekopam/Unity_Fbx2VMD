import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";

import {
  getSmokeImportFbxPath,
  getSmokePanelTarget,
  getSmokeScreenshotPath,
  getSmokeSettingsPath,
  getRendererEntry,
  isDevelopmentMode,
  isSmokeTestMode
} from "../electron/rendererEntry.js";
import { createElectronRuntimeEnv } from "../scripts/electronEnv.js";

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
