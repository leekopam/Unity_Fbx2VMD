import assert from "node:assert/strict";
import fs from "node:fs/promises";
import test from "node:test";

import {
  isAllowedExternalNavigationUrl
} from "../electron/shellNavigationPolicy.js";

const mainUrl = new URL("../electron/main.js", import.meta.url);

test("Electron shell hides the native menu and opens outside navigation externally", async () => {
  const main = await fs.readFile(mainUrl, "utf8");

  assert.match(main, /const \{ app, BrowserWindow, dialog, ipcMain, shell \} = electron;/);
  assert.match(main, /mainWindow\.setMenuBarVisibility\(false\);/);
  assert.match(main, /webContents\.on\("will-navigate"/);
  assert.match(main, /event\.preventDefault\(\);/);
  assert.match(main, /openAllowedExternalNavigation\(\{\s*shell,\s*url\s*\}\);/s);
});

test("Electron shell keeps project reference bounds for the onboarding workspace", async () => {
  const main = await fs.readFile(mainUrl, "utf8");

  assert.match(main, /width:\s*1265,/);
  assert.match(main, /height:\s*675,/);
  assert.match(main, /minWidth:\s*1080,/);
  assert.match(main, /minHeight:\s*600,/);
});

test("Electron shell injects the bridge token for file:// renders outside smoke mode", async () => {
  const main = await fs.readFile(mainUrl, "utf8");

  // opaque 오리진 렌더러는 세션 토큰으로만 인증되므로 주입이 smoke 가드
  // 안에 있으면 프로덕션 전 요청이 403이 된다 — did-finish-load 등록이
  // smoke 핸들러보다 앞에 오는지 순서까지 고정한다.
  const injectIndex = main.indexOf('webContents.on("did-finish-load"');
  const smokeIndex = main.indexOf("attachSmokeTestExit(mainWindow, entry, bridgeServer);");
  assert.ok(injectIndex !== -1, "did-finish-load 주입 핸들러 없음");
  assert.ok(smokeIndex !== -1, "smoke 핸들러 호출 없음");
  assert.ok(injectIndex < smokeIndex, "주입이 smoke 핸들러 안쪽에 있음");
  assert.match(main, /applyRendererBridgeConfig\(mainWindow, bridgeServer\)/);
  // token 줄이 smoke 전용 함수에만 있어도 통과되지 않게 주입 함수 본문으로 범위를 좁힌다.
  const fnStart = main.indexOf("async function applyRendererBridgeConfig");
  const fnEnd = main.indexOf("\n}", fnStart);
  assert.match(main.slice(fnStart, fnEnd), /token:\s*\$\{JSON\.stringify\(bridge\.token\)\}/);
});

test("Electron shell keeps one settings window instance and focuses the existing window", async () => {
  const main = await fs.readFile(mainUrl, "utf8");

  assert.match(main, /requestSingleInstanceLock\(\{/);
  assert.match(main, /unityEditorPid: process\.env\.UNITY_FBX2VMD_EDITOR_PID/);
  assert.match(main, /second-instance/);
  assert.match(main, /BrowserWindow\.getAllWindows\(\)/);
  assert.match(main, /\.restore\(\)/);
  assert.match(main, /\.focus\(\)/);
});

test("Electron shell externalizes only http and https navigation", () => {
  assert.equal(isAllowedExternalNavigationUrl("https://example.com/docs"), true);
  assert.equal(isAllowedExternalNavigationUrl("http://localhost:5173/preview"), true);
  assert.equal(isAllowedExternalNavigationUrl("file:///C:/Temp/unsafe.html"), false);
  assert.equal(isAllowedExternalNavigationUrl("javascript:alert(1)"), false);
  assert.equal(isAllowedExternalNavigationUrl("vscode://file/project"), false);
  assert.equal(isAllowedExternalNavigationUrl("not a url"), false);
});
