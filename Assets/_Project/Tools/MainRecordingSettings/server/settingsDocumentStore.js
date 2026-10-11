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

// Unity 측 MainRecordingSettingsCommandEnvelope의 캐릭터 액션 상수와 동일하게 유지한다.
export const CHARACTER_COMMAND_ACTIONS = Object.freeze([
  "LoadCharacter",
  "ApplyPreset",
  "SavePreset",
  "RestoreSceneCharacter",
  "RegisterVrm",
  "RemoveCharacter",
  "RefreshCharacter",
  "ReconnectCharacter",
  "SetDefaultPreset",
  "DeletePreset",
  "RenamePreset",
  "CancelLoad",
  "CopyPreset",
  "UpdatePreset"
]);

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
    pendingCommand: createEmptyCommandEnvelope(),
    lastCommandResult: createEmptyCommandResult()
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

// FBX 임포트 명령을 대기열에 추가한다.
// 소비 전 명령이 남아 있으면 덮어쓰지 않고 거절한다(무소식 소실 방지).
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

  rejectIfCommandPending(document);

  document.updatedAtUtc = timestamp;
  document.fbxPath = normalizedFbxPath;
  document.pendingCommand = {
    ...createEmptyCommandEnvelope(),
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

// 캐릭터 라이브러리 명령을 큐에 넣는다. Unity 프로세서가 폴링으로 소비하고
// lastCommandResult에 회답을 기록한다. Play 모드가 아니어도 큐잉은 허용된다.
export async function queueCharacterCommand({
  settingsPath = resolveSettingsFilePath(),
  action,
  characterId = "",
  presetId = "",
  path: commandPath = "",
  params = null,
  now = () => new Date(),
  createCommandId = createDefaultCommandId
} = {}) {
  const normalizedAction = normalizeRequiredText(action, "명령 action은 필수입니다.");
  if (!CHARACTER_COMMAND_ACTIONS.includes(normalizedAction)) {
    throw new Error(`지원하지 않는 캐릭터 명령입니다: ${normalizedAction}`);
  }

  const timestamp = now().toISOString();
  const commandId = createCommandId();
  const document = normalizeDocument(await loadSettingsDocument(settingsPath));

  rejectIfCommandPending(document);

  document.updatedAtUtc = timestamp;
  document.pendingCommand = {
    ...createEmptyCommandEnvelope(),
    commandId,
    action: normalizedAction,
    characterId: normalizeOptionalText(characterId).trim(),
    presetId: normalizeOptionalText(presetId).trim(),
    path: normalizeOptionalText(commandPath).trim(),
    paramsJson: params && typeof params === "object" ? JSON.stringify(params) : "",
    requestedAtUtc: timestamp
  };

  await saveSettingsDocument(settingsPath, document);

  return {
    accepted: true,
    commandId,
    action: normalizedAction,
    settingsPath
  };
}

// 마지막 명령 결과 회답을 읽는다 — Electron의 로딩/성공/실패 표시용.
export async function readCommandResult({
  settingsPath = resolveSettingsFilePath()
} = {}) {
  const document = normalizeDocument(await loadSettingsDocument(settingsPath));
  return { ...document.lastCommandResult, settingsPath };
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
  normalized.pendingCommand.characterId = normalizeOptionalText(normalized.pendingCommand.characterId);
  normalized.pendingCommand.presetId = normalizeOptionalText(normalized.pendingCommand.presetId);
  normalized.pendingCommand.path = normalizeOptionalText(normalized.pendingCommand.path);
  normalized.pendingCommand.paramsJson = normalizeOptionalText(normalized.pendingCommand.paramsJson);
  normalized.pendingCommand.requestedAtUtc = normalizeOptionalText(normalized.pendingCommand.requestedAtUtc);

  normalized.lastCommandResult = {
    ...createEmptyCommandResult(),
    ...(normalized.lastCommandResult && typeof normalized.lastCommandResult === "object"
      ? normalized.lastCommandResult
      : {})
  };
  normalized.lastCommandResult.commandId = normalizeOptionalText(normalized.lastCommandResult.commandId);
  normalized.lastCommandResult.action = normalizeOptionalText(normalized.lastCommandResult.action);
  normalized.lastCommandResult.state = normalizeOptionalText(normalized.lastCommandResult.state);
  normalized.lastCommandResult.message = normalizeOptionalText(normalized.lastCommandResult.message);
  normalized.lastCommandResult.finishedAtUtc = normalizeOptionalText(normalized.lastCommandResult.finishedAtUtc);

  return normalized;
}

// 소비 전 명령이 남아 있으면 새 명령을 받지 않는다.
// Unity 측 소비자가 pendingCommand를 비울 때까지 대기하도록 거절한다.
function rejectIfCommandPending(document) {
  if (hasText(document?.pendingCommand?.commandId)) {
    throw new Error(
      `이전 명령이 아직 대기 중입니다 (commandId: ${document.pendingCommand.commandId}).`);
  }
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
    characterId: "",
    presetId: "",
    path: "",
    paramsJson: "",
    requestedAtUtc: ""
  };
}

function createEmptyCommandResult() {
  return {
    commandId: "",
    action: "",
    state: "",
    message: "",
    finishedAtUtc: ""
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
