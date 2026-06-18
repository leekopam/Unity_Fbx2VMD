import crypto from "node:crypto";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

export const SETTINGS_ENVIRONMENT_VARIABLE = "UNITY_FBX2VMD_MAIN_RECORDING_SETTINGS_PATH";
export const SETTINGS_APP_FOLDER_NAME = "Unity_Fbx2VMD";
export const SETTINGS_FEATURE_FOLDER_NAME = "MainRecordingSettings";
export const SETTINGS_FILE_NAME = "main-recording-settings.json";
export const IMPORT_FBX_ACTION = "ImportFbx";
export const PLAY_MODE_PLAYING = "playing";
export const PLAY_MODE_STOPPED = "stopped";

export function resolveSettingsFilePath({
  explicitPath,
  env = process.env,
  localAppDataRoot = process.env.LOCALAPPDATA
} = {}) {
  if (hasText(explicitPath)) {
    return explicitPath.trim();
  }

  if (hasText(env[SETTINGS_ENVIRONMENT_VARIABLE])) {
    return env[SETTINGS_ENVIRONMENT_VARIABLE].trim();
  }

  const root = hasText(localAppDataRoot)
    ? localAppDataRoot
    : path.join(os.homedir(), ".local", "share");

  return path.join(root, SETTINGS_APP_FOLDER_NAME, SETTINGS_FEATURE_FOLDER_NAME, SETTINGS_FILE_NAME);
}

export function createDefaultSettingsDocument() {
  return {
    schemaVersion: 1,
    updatedAtUtc: "",
    fbxPath: "",
    characterModelPath: "",
    captureWidth: 1920,
    captureHeight: 1080,
    openSettingsOnStart: true,
    runtimeState: createDefaultRuntimeState(),
    pendingCommand: createEmptyCommandEnvelope()
  };
}

export async function readRuntimeState({
  settingsPath = resolveSettingsFilePath()
} = {}) {
  const document = normalizeDocument(await loadSettingsDocument(settingsPath));
  return {
    ...document.runtimeState,
    settingsPath
  };
}

// FBX 임포트 명령을 대기열에 추가하는 함수, 기존 명령이 있을 경우 덮어쓰기
export async function queueImportFbxCommand({
  settingsPath = resolveSettingsFilePath(),
  fbxPath,
  now = () => new Date(),
  createCommandId = createDefaultCommandId
} = {}) {
  const normalizedFbxPath = normalizeRequiredText(fbxPath, "FBX 경로는 필수입니다.");
  const timestamp = now().toISOString();
  const commandId = createCommandId();
  const document = normalizeDocument(await loadSettingsDocument(settingsPath));

  document.updatedAtUtc = timestamp;
  document.fbxPath = normalizedFbxPath;
  document.pendingCommand = {
    commandId,
    action: IMPORT_FBX_ACTION,
    fbxPath: normalizedFbxPath,
    requestedAtUtc: timestamp
  };

  await saveSettingsDocument(settingsPath, document);

  return {
    accepted: true,
    commandId,
    fbxPath: normalizedFbxPath,
    settingsPath
  };
}

async function loadSettingsDocument(settingsPath) {
  try {
    const json = await fs.readFile(settingsPath, "utf8");
    if (!hasText(json)) {
      return createDefaultSettingsDocument();
    }

    return JSON.parse(json);
  } catch (error) {
    if (error.code === "ENOENT") {
      return createDefaultSettingsDocument();
    }

    throw error;
  }
}

async function saveSettingsDocument(settingsPath, document) {
  await fs.mkdir(path.dirname(settingsPath), { recursive: true });
  const tempPath = `${settingsPath}.tmp-${crypto.randomUUID().replaceAll("-", "")}`;
  await fs.writeFile(tempPath, `${JSON.stringify(document, null, 2)}\n`, "utf8");
  await fs.rename(tempPath, settingsPath);
}

function normalizeDocument(document) {
  const normalized = {
    ...createDefaultSettingsDocument(),
    ...(document && typeof document === "object" ? document : {})
  };

  normalized.schemaVersion = toPositiveInt(normalized.schemaVersion, 1);
  normalized.captureWidth = toPositiveInt(normalized.captureWidth, 1920);
  normalized.captureHeight = toPositiveInt(normalized.captureHeight, 1080);
  normalized.updatedAtUtc = normalizeOptionalText(normalized.updatedAtUtc);
  normalized.fbxPath = normalizeOptionalText(normalized.fbxPath);
  normalized.characterModelPath = normalizeOptionalText(normalized.characterModelPath);
  normalized.openSettingsOnStart = normalized.openSettingsOnStart !== false;
  normalized.runtimeState = normalizeRuntimeState(normalized.runtimeState);
  normalized.pendingCommand = {
    ...createEmptyCommandEnvelope(),
    ...(normalized.pendingCommand && typeof normalized.pendingCommand === "object"
      ? normalized.pendingCommand
      : {})
  };
  normalized.pendingCommand.commandId = normalizeOptionalText(normalized.pendingCommand.commandId);
  normalized.pendingCommand.action = normalizeOptionalText(normalized.pendingCommand.action);
  normalized.pendingCommand.fbxPath = normalizeOptionalText(normalized.pendingCommand.fbxPath);
  normalized.pendingCommand.requestedAtUtc = normalizeOptionalText(normalized.pendingCommand.requestedAtUtc);

  return normalized;
}

function createDefaultRuntimeState() {
  return {
    playMode: PLAY_MODE_STOPPED,
    updatedAtUtc: ""
  };
}

function normalizeRuntimeState(value) {
  const state = {
    ...createDefaultRuntimeState(),
    ...(value && typeof value === "object" ? value : {})
  };

  state.playMode = state.playMode === PLAY_MODE_PLAYING
    ? PLAY_MODE_PLAYING
    : PLAY_MODE_STOPPED;
  state.updatedAtUtc = normalizeOptionalText(state.updatedAtUtc);
  return state;
}

function createEmptyCommandEnvelope() {
  return {
    commandId: "",
    action: "",
    fbxPath: "",
    requestedAtUtc: ""
  };
}

function createDefaultCommandId() {
  return crypto.randomUUID().replaceAll("-", "");
}

function toPositiveInt(value, fallback) {
  return Number.isInteger(value) && value > 0 ? value : fallback;
}

function normalizeOptionalText(value) {
  return typeof value === "string" ? value : "";
}

function normalizeRequiredText(value, message) {
  const normalized = typeof value === "string" ? value.trim() : "";
  if (!normalized) {
    throw new Error(message);
  }

  return normalized;
}

function hasText(value) {
  return typeof value === "string" && value.trim().length > 0;
}
