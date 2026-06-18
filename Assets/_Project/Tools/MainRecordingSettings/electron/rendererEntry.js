import path from "node:path";

export const DEFAULT_DEV_SERVER_URL = "http://localhost:3000";
const SMOKE_IMPORT_FBX_PREFIX = "--smoke-import-fbx=";
const SMOKE_SETTINGS_PATH_PREFIX = "--smoke-settings-path=";
const SMOKE_SCREENSHOT_PATH_PREFIX = "--smoke-screenshot-path=";
const SMOKE_PANEL_TARGET_PREFIX = "--smoke-panel-target=";

export function isDevelopmentMode({ argv = process.argv, env = process.env } = {}) {
  return argv.includes("--dev") || env.NODE_ENV === "development";
}

export function isSmokeTestMode({ argv = process.argv } = {}) {
  return argv.includes("--smoke-test");
}

export function getSmokeImportFbxPath({ argv = process.argv } = {}) {
  return getArgumentValue(argv, SMOKE_IMPORT_FBX_PREFIX);
}

export function getSmokeSettingsPath({ argv = process.argv } = {}) {
  return getArgumentValue(argv, SMOKE_SETTINGS_PATH_PREFIX);
}

export function getSmokeScreenshotPath({ argv = process.argv } = {}) {
  return getArgumentValue(argv, SMOKE_SCREENSHOT_PATH_PREFIX);
}

export function getSmokePanelTarget({ argv = process.argv } = {}) {
  return getArgumentValue(argv, SMOKE_PANEL_TARGET_PREFIX);
}

export function getRendererEntry({
  argv = process.argv,
  env = process.env,
  appRoot
} = {}) {
  if (!appRoot) {
    throw new Error("appRoot is required.");
  }

  if (isDevelopmentMode({ argv, env })) {
    return {
      mode: "development",
      type: "url",
      target: DEFAULT_DEV_SERVER_URL
    };
  }

  return {
    mode: "production",
    type: "file",
    target: path.join(appRoot, "build", "index.html")
  };
}

function getArgumentValue(argv, prefix) {
  const arg = argv.find((value) => typeof value === "string" && value.startsWith(prefix));
  return arg ? arg.slice(prefix.length).trim() : "";
}
