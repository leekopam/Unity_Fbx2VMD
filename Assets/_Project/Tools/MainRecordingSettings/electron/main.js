import { createRequire } from "node:module";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { createFbxFileDialogOptions, extractSelectedFbxPath } from "./fileDialog.js";
import {
  getRendererEntry,
  getSmokeImportFbxPath,
  getSmokePanelTarget,
  getSmokeScreenshotPath,
  getSmokeSettingsPath,
  isSmokeTestMode
} from "./rendererEntry.js";
import { openAllowedExternalNavigation } from "./shellNavigationPolicy.js";
import { createSettingsBridgeServer } from "../server/settingsBridgeServer.js";

const require = createRequire(import.meta.url);
const electron = require("electron");
const { app, BrowserWindow, dialog, ipcMain, shell } = electron;
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const appRoot = path.resolve(__dirname, "..");
const preloadPath = path.join(__dirname, "preload.cjs");
let bridgeServer = null;
const gotSingleInstanceLock = app.requestSingleInstanceLock();

if (!gotSingleInstanceLock) {
  app.quit();
} else {
  app.on("second-instance", focusExistingSettingsWindow);
  app.whenReady().then(startApplication);
}

async function createMainWindow() {
  const mainWindow = new BrowserWindow({
    width: 1265,
    height: 675,
    minWidth: 1080,
    minHeight: 600,
    title: "Main Recording Settings",
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      preload: preloadPath
    }
  });
  mainWindow.setMenuBarVisibility(false);
  keepNavigationInsideShell(mainWindow);

  const entry = getRendererEntry({ appRoot });
  attachSmokeTestExit(mainWindow, entry, bridgeServer);
  if (entry.type === "url") {
    await mainWindow.loadURL(entry.target);
    return;
  }

  await mainWindow.loadFile(entry.target);
}

function keepNavigationInsideShell(mainWindow) {
  const webContents = mainWindow.webContents;
  webContents.on("will-navigate", (event, url) => {
    if (url === webContents.getURL()) {
      return;
    }

    event.preventDefault();
    openAllowedExternalNavigation({ shell, url });
  });
  webContents.setWindowOpenHandler(({ url }) => {
    openAllowedExternalNavigation({ shell, url });
    return { action: "deny" };
  });
}

function attachSmokeTestExit(mainWindow, entry, bridge) {
  if (!isSmokeTestMode()) {
    return;
  }

  const smokeImportFbxPath = getSmokeImportFbxPath();
  const smokePanelTarget = getSmokePanelTarget();
  const smokeScreenshotPath = getSmokeScreenshotPath();
  let finished = false;
  const timeout = setTimeout(() => {
    finish(2, `SMOKE_LOAD_TIMEOUT ${entry.target}`);
  }, 10000);

  mainWindow.webContents.once("did-finish-load", async () => {
    if (entry.type !== "file") {
      finish(0, `SMOKE_LOAD_OK ${entry.target}`);
      return;
    }

    try {
      await applyRendererBridgeConfig(mainWindow, bridge);
      const uiReady = await mainWindow.webContents.executeJavaScript(
        "document.querySelector('#apiBaseUrl') == null && document.querySelector('#wsUrl') == null && document.querySelector('#connectButton') == null && document.querySelector('#disconnectButton') == null && document.querySelector('#fbxPath') == null && document.querySelector('#chooseFbxButton') == null && document.querySelector('#importButton')?.disabled === false && typeof window.settingsShell?.chooseFbxFile === 'function'"
      );
      if (!uiReady) {
        finish(3, `SMOKE_UI_NOT_READY ${entry.target}`);
        return;
      }

      if (smokePanelTarget) {
        const panelResult = await runRendererPanelSmoke({
          mainWindow,
          panelTarget: smokePanelTarget
        });

        if (!panelResult.ok) {
          finish(8, `SMOKE_PANEL_FAIL ${JSON.stringify(panelResult)}`);
          return;
        }
      }

      if (smokeScreenshotPath) {
        await captureSmokeScreenshot({
          mainWindow,
          screenshotPath: smokeScreenshotPath
        });
      }

      if (smokeImportFbxPath) {
        await runRendererImportSmoke({
          mainWindow,
          bridge,
          fbxPath: smokeImportFbxPath,
          finish
        });
        return;
      }
    } catch (error) {
      finish(7, `SMOKE_ERROR ${error.message}`);
      return;
    }

    const screenshotSuffix = smokeScreenshotPath ? ` ${smokeScreenshotPath}` : "";
    finish(0, `SMOKE_LOAD_OK ${entry.target}${screenshotSuffix}`);
  });

  mainWindow.webContents.once("did-fail-load", (_event, errorCode, errorDescription, validatedURL) => {
    finish(1, `SMOKE_LOAD_FAIL ${errorCode} ${errorDescription} ${validatedURL}`);
  });

  function finish(exitCode, message) {
    if (finished) {
      return;
    }

    finished = true;
    clearTimeout(timeout);
    console.log(message);
    app.exit(exitCode);
  }
}

async function applyRendererBridgeConfig(mainWindow, bridge) {
  if (!bridge) {
    return;
  }

  await mainWindow.webContents.executeJavaScript(`
    window.mainRecordingSettingsBridgeConfig = {
      apiBaseUrl: ${JSON.stringify(bridge.baseUrl)},
      wsUrl: ${JSON.stringify(`${bridge.wsUrl}/settings`)}
    };
  `);
}

async function runRendererImportSmoke({ mainWindow, bridge, fbxPath, finish }) {
  if (!bridge) {
    finish(4, "SMOKE_IMPORT_FAIL bridge missing");
    return;
  }

  const result = await mainWindow.webContents.executeJavaScript(`
    (async () => {
      window.mainRecordingSettingsBridgeConfig = {
        apiBaseUrl: ${JSON.stringify(bridge.baseUrl)},
        wsUrl: ${JSON.stringify(`${bridge.wsUrl}/settings`)}
      };

      const importButton = document.querySelector("#importButton");
      if (!importButton || importButton.disabled) {
        return { ok: false, reason: "import button is not ready" };
      }

      importButton.click();
      const startedAt = Date.now();
      while (Date.now() - startedAt < 5000) {
        const log = document.querySelector("#eventLog");
        if (log?.dataset.empty === "false" && log.textContent.includes('"accepted":true')) {
          return {
            ok: true,
            feedback: document.querySelector("#feedback")?.textContent ?? "",
            log: log.textContent
          };
        }

        await new Promise((resolve) => setTimeout(resolve, 50));
      }

      return {
        ok: false,
        reason: "timed out waiting for import response",
        feedback: document.querySelector("#feedback")?.textContent ?? "",
        log: document.querySelector("#eventLog")?.textContent ?? ""
      };
    })()
  `);

  if (!result.ok) {
    finish(5, `SMOKE_IMPORT_FAIL ${JSON.stringify(result)}`);
    return;
  }

  const saved = JSON.parse(await fs.readFile(bridge.settingsPath, "utf8"));
  if (saved.pendingCommand?.fbxPath !== fbxPath) {
    finish(6, `SMOKE_IMPORT_COMMAND_MISMATCH ${bridge.settingsPath}`);
    return;
  }

  finish(0, `SMOKE_IMPORT_OK ${bridge.settingsPath}`);
}

async function runRendererPanelSmoke({ mainWindow, panelTarget }) {
  return mainWindow.webContents.executeJavaScript(`
    (() => {
      const target = ${JSON.stringify(panelTarget)};
      const button = document.querySelector(\`[data-panel-target="\${target}"]\`);
      if (!button) {
        return { ok: false, reason: "panel target missing", target };
      }

      button.click();

      const view = document.querySelector(\`[data-panel-view="\${target}"]\`);
      const htmlOverflowY = window.getComputedStyle(document.documentElement).overflowY;
      const bodyOverflowY = window.getComputedStyle(document.body).overflowY;
      const bodyScrolls = document.documentElement.scrollHeight > window.innerHeight;

      return {
        ok: view?.hidden === false && htmlOverflowY === "hidden" && bodyOverflowY === "hidden" && !bodyScrolls,
        target,
        viewHidden: view?.hidden ?? null,
        activePanel: document.querySelector(".app-shell")?.dataset.activePanel ?? "",
        htmlOverflowY,
        bodyOverflowY,
        bodyScrollHeight: document.documentElement.scrollHeight,
        viewportHeight: window.innerHeight
      };
    })()
  `);
}

async function captureSmokeScreenshot({ mainWindow, screenshotPath }) {
  await fs.mkdir(path.dirname(screenshotPath), { recursive: true });
  let lastError = null;

  for (let attempt = 0; attempt < 5; attempt += 1) {
    try {
      await delay(150);
      const image = await mainWindow.webContents.capturePage();
      await fs.writeFile(screenshotPath, image.toPNG());
      return;
    } catch (error) {
      lastError = error;
    }
  }

  throw lastError;
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function startApplication() {
  registerIpcHandlers();

  const smokeImportFbxPath = getSmokeImportFbxPath();
  const smokeSettingsPath = getSmokeSettingsPath()
    || (smokeImportFbxPath
      ? path.join(app.getPath("temp"), `main-recording-settings-smoke-${Date.now()}.json`)
      : undefined);

  bridgeServer = createSettingsBridgeServer({
    port: isSmokeTestMode() ? 0 : undefined,
    settingsPath: smokeSettingsPath
  });
  await bridgeServer.listen();
  console.log(`SETTINGS_BRIDGE_READY ${bridgeServer.baseUrl}`);

  await createMainWindow();

  app.on("activate", async () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      await createMainWindow();
    }
  });
}

function focusExistingSettingsWindow() {
  const [existingWindow] = BrowserWindow.getAllWindows();
  if (!existingWindow) {
    return;
  }

  if (existingWindow.isMinimized()) {
    existingWindow.restore();
  }

  existingWindow.focus();
}

function registerIpcHandlers() {
  ipcMain.handle("settings:choose-fbx-file", async () => {
    const smokeImportFbxPath = getSmokeImportFbxPath();
    if (smokeImportFbxPath) {
      return smokeImportFbxPath;
    }

    const result = await dialog.showOpenDialog(createFbxFileDialogOptions());
    return extractSelectedFbxPath(result);
  });
}

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});

app.on("before-quit", async () => {
  if (bridgeServer == null) {
    return;
  }

  const server = bridgeServer;
  bridgeServer = null;
  await server.close();
});
