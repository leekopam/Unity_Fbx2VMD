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

test("Electron shell keeps one settings window instance and focuses the existing window", async () => {
  const main = await fs.readFile(mainUrl, "utf8");

  assert.match(main, /requestSingleInstanceLock\(\)/);
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
