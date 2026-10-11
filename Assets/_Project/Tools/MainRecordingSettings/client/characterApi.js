import { DEFAULT_API_BASE_URL, withBridgeToken } from "./settingsApi.js";

// 캐릭터 라이브러리 브리지 클라이언트.
// 표시는 /characters 읽기 스냅샷, 조작은 /character-command 큐잉으로 나뉜다.
// 실제 쓰기·씬 변경은 Unity 프로세서가 수행하고 결과는 /command-result로 회수한다.

export const CHARACTER_ACTIONS = Object.freeze({
  registerVrm: "RegisterVrm",
  loadCharacter: "LoadCharacter",
  cancelLoad: "CancelLoad",
  removeCharacter: "RemoveCharacter",
  refreshCharacter: "RefreshCharacter",
  reconnectCharacter: "ReconnectCharacter",
  restoreSceneCharacter: "RestoreSceneCharacter",
  applyPreset: "ApplyPreset",
  savePreset: "SavePreset",
  setDefaultPreset: "SetDefaultPreset",
  deletePreset: "DeletePreset",
  renamePreset: "RenamePreset",
  copyPreset: "CopyPreset",
  updatePreset: "UpdatePreset"
});

export function createCharactersRequest({ apiBaseUrl = DEFAULT_API_BASE_URL } = {}) {
  return { url: `${normalizeBaseUrl(apiBaseUrl)}/characters`, init: { method: "GET" } };
}

export function createCommandResultRequest({ apiBaseUrl = DEFAULT_API_BASE_URL } = {}) {
  return { url: `${normalizeBaseUrl(apiBaseUrl)}/command-result`, init: { method: "GET" } };
}

export function createCharacterCommandRequest({
  apiBaseUrl = DEFAULT_API_BASE_URL,
  action,
  characterId = "",
  presetId = "",
  path = "",
  params = null
} = {}) {
  const baseUrl = normalizeBaseUrl(apiBaseUrl);
  const normalizedAction = normalizeRequiredText(action, "명령 action 필요");
  const body = { action: normalizedAction };
  if (hasText(characterId)) body.characterId = characterId.trim();
  if (hasText(presetId)) body.presetId = presetId.trim();
  if (hasText(path)) body.path = path.trim();
  if (params && typeof params === "object") body.params = params;

  return {
    url: `${baseUrl}/character-command`,
    init: {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    }
  };
}

export async function getCharacters({ apiBaseUrl, fetchImpl = globalThis.fetch } = {}) {
  return fetchJson(createCharactersRequest({ apiBaseUrl }), fetchImpl);
}

export async function getCommandResult({ apiBaseUrl, fetchImpl = globalThis.fetch } = {}) {
  return fetchJson(createCommandResultRequest({ apiBaseUrl }), fetchImpl);
}

export async function sendCharacterCommand({
  apiBaseUrl,
  action,
  characterId,
  presetId,
  path,
  params,
  fetchImpl = globalThis.fetch
} = {}) {
  const request = createCharacterCommandRequest({
    apiBaseUrl, action, characterId, presetId, path, params
  });
  return fetchJson(request, fetchImpl);
}

async function fetchJson(request, fetchImpl) {
  if (typeof fetchImpl !== "function") {
    throw new Error("fetch 구현 필요");
  }

  const response = await fetchImpl(request.url, withBridgeToken(request.init));
  if (!response.ok) {
    const body = typeof response.text === "function" ? await response.text() : "";
    const detail = body ? `: ${body}` : "";
    const error = new Error(`HTTP 오류 ${response.status}${detail}`);
    error.status = response.status;
    error.body = body;
    throw error;
  }

  if (typeof response.json !== "function") {
    return {};
  }

  return await response.json();
}

function normalizeBaseUrl(value) {
  return normalizeRequiredText(value, "API 기본 주소 필요").replace(/\/+$/, "");
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
