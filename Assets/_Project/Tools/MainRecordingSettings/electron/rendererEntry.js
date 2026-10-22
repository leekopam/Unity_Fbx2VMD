import path from "node:path";

export const DEFAULT_DEV_SERVER_URL = "http://localhost:3000";
const SMOKE_IMPORT_FBX_PREFIX = "--smoke-import-fbx=";
const SMOKE_SETTINGS_PATH_PREFIX = "--smoke-settings-path=";
const SMOKE_SCREENSHOT_PATH_PREFIX = "--smoke-screenshot-path=";
const SMOKE_PANEL_TARGET_PREFIX = "--smoke-panel-target=";
const E2E_ENTRY_URL_PREFIX = "--e2e-entry-url=";

export function isDevelopmentMode({ argv = process.argv, env = process.env } = {}) {
  return argv.includes("--dev") || env.NODE_ENV === "development";
}

export function isSmokeTestMode({ argv = process.argv } = {}) {
  return argv.includes("--smoke-test");
}

// E2E 구동 모드 — 외부 테스트 러너가 CDP로 창을 붙여 구동한다.
// 스모크/감사처럼 종료 로직 없이 창을 열어두되, 싱글 인스턴스 락과
// 고정 브리지 포트는 우회해 사용자 설정창과 공존할 수 있게 한다.
export function isE2eTestMode({ argv = process.argv } = {}) {
  return argv.includes("--e2e-test");
}

// e2e는 file:// 탐색을 거부하므로, 구동 모드에서는 러너가 띄운 정적 서버의
// http URL을 창이 대신 로드한다. file:// 로드와 달리 실제 http 오리진이라
// 브리지가 요구하는 토큰 없이도 Origin 허용목록을 통과한다.
export function getE2eEntryUrl({ argv = process.argv } = {}) {
  return getArgumentValue(argv, E2E_ENTRY_URL_PREFIX);
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
