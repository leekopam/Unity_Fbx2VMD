import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import {
  SETTINGS_EXECUTABLE_FILE_NAME,
  packageElectronRelease
} from "../scripts/packageElectronRelease.mjs";

test("packageElectronRelease copies Electron runtime and app resources into release subfolder", async () => {
  const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), "main-recording-settings-package-"));
  const appRoot = path.join(tempRoot, "app");
  const electronDist = path.join(tempRoot, "electron-dist");
  const outputDir = path.join(tempRoot, "release", "MainRecordingSettings");

  await createFakeAppRoot(appRoot);
  await createFakeElectronDist(electronDist);

  const result = await packageElectronRelease({
    appRoot,
    electronDist,
    outputDir
  });

  assert.equal(result.executablePath, path.join(outputDir, SETTINGS_EXECUTABLE_FILE_NAME));
  assert.equal(result.archivePath, path.join(outputDir, "resources", "app.asar"));
  assert.equal(result.packageMode, "electron");
  assert.equal(result.requiredRuntimeCommands, 0);
  assert.equal(result.copiedAppEntries.includes("node_modules/ws"), true);
  assert.equal(await exists(path.join(outputDir, SETTINGS_EXECUTABLE_FILE_NAME)), true);
  assert.equal(await exists(path.join(outputDir, "electron.exe")), false);
  assert.equal(await exists(path.join(outputDir, "resources", "default_app.asar")), false);
  assert.equal(await exists(path.join(outputDir, "resources", "app.asar")), true);
  assert.equal(await exists(path.join(outputDir, "resources", "app", "package.json")), true);
  assert.equal(await exists(path.join(outputDir, "resources", "app", "build", "index.html")), true);
  assert.equal(await exists(path.join(outputDir, "resources", "app", "client", "settingsUi.js")), true);
  assert.equal(await exists(path.join(outputDir, "resources", "app", "server", "settingsBridgeServer.js")), true);
  assert.equal(await exists(path.join(outputDir, "resources", "app", "node_modules", "ws", "package.json")), true);
  assert.equal(await exists(path.join(outputDir, "resources", "app", "node_modules", "electron")), false);
  assert.equal(await exists(path.join(outputDir, "resources", "app", "build.meta")), false);
});

async function createFakeAppRoot(appRoot) {
  await fs.mkdir(path.join(appRoot, "build"), { recursive: true });
  await fs.mkdir(path.join(appRoot, "client"), { recursive: true });
  await fs.mkdir(path.join(appRoot, "electron"), { recursive: true });
  await fs.mkdir(path.join(appRoot, "server"), { recursive: true });
  await fs.mkdir(path.join(appRoot, "node_modules", "ws"), { recursive: true });
  await fs.mkdir(path.join(appRoot, "node_modules", "electron"), { recursive: true });

  await fs.writeFile(path.join(appRoot, "package.json"), JSON.stringify({
    name: "main-recording-settings",
    type: "module",
    main: "electron/main.js"
  }), "utf8");
  await fs.writeFile(path.join(appRoot, "build", "index.html"), "<html></html>", "utf8");
  await fs.writeFile(path.join(appRoot, "build.meta"), "ignored", "utf8");
  await fs.writeFile(path.join(appRoot, "client", "settingsUi.js"), "export {};\n", "utf8");
  await fs.writeFile(path.join(appRoot, "electron", "main.js"), "export {};\n", "utf8");
  await fs.writeFile(path.join(appRoot, "server", "settingsBridgeServer.js"), "export {};\n", "utf8");
  await fs.writeFile(path.join(appRoot, "node_modules", "ws", "package.json"), "{\"name\":\"ws\"}\n", "utf8");
  await fs.writeFile(path.join(appRoot, "node_modules", "electron", "package.json"), "{\"name\":\"electron\"}\n", "utf8");
}

async function createFakeElectronDist(electronDist) {
  await fs.mkdir(path.join(electronDist, "resources"), { recursive: true });
  await fs.writeFile(path.join(electronDist, "electron.exe"), "fake exe", "utf8");
  await fs.writeFile(path.join(electronDist, "chrome_100_percent.pak"), "fake pak", "utf8");
  await fs.writeFile(path.join(electronDist, "chrome_100_percent.pak.meta"), "ignored", "utf8");
  await fs.writeFile(path.join(electronDist, "resources", "default_app.asar"), "fake default app", "utf8");
}

async function exists(filePath) {
  try {
    await fs.access(filePath);
    return true;
  } catch {
    return false;
  }
}
