import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

// Unity 측 CharacterLibraryPathResolver와 같은 경로 규칙:
// 명시 경로 > 환경변수 > LocalAppData > ~/.local/share.
export const LIBRARY_ENVIRONMENT_VARIABLE = "UNITY_FBX2VMD_CHARACTER_LIBRARY_PATH";
export const LIBRARY_APP_FOLDER_NAME = "Unity_Fbx2VMD";
export const LIBRARY_FEATURE_FOLDER_NAME = "CharacterLibrary";
export const LIBRARY_FILE_NAME = "characters.json";
export const LIBRARY_PRESETS_FOLDER_NAME = "presets";
export const LIBRARY_THUMBNAILS_FOLDER_NAME = "thumbnails";

export function resolveLibraryFilePath({
  explicitPath,
  env = process.env,
  localAppDataRoot = process.env.LOCALAPPDATA
} = {}) {
  if (hasText(explicitPath)) {
    return explicitPath.trim();
  }

  if (hasText(env[LIBRARY_ENVIRONMENT_VARIABLE])) {
    return env[LIBRARY_ENVIRONMENT_VARIABLE].trim();
  }

  const root = hasText(localAppDataRoot)
    ? localAppDataRoot
    : path.join(os.homedir(), ".local", "share");

  return path.join(root, LIBRARY_APP_FOLDER_NAME, LIBRARY_FEATURE_FOLDER_NAME, LIBRARY_FILE_NAME);
}

// 라이브러리 문서 + 각 엔트리의 프리셋 문서를 읽는다.
// 쓰기는 Unity만 한다 — 이 함수는 표시용 read-only 스냅샷이다.
export async function readCharacterLibrary({
  libraryPath = resolveLibraryFilePath()
} = {}) {
  const document = await loadJsonOrDefault(libraryPath, createDefaultLibraryDocument);
  const entries = Array.isArray(document.entries) ? document.entries : [];
  const characters = [];

  for (const entry of entries) {
    if (!entry || typeof entry !== "object") {
      continue;
    }

    const presetPath = resolvePresetFilePath(libraryPath, entry.id);
    const presetDocument = presetPath
      ? await loadJsonOrDefault(presetPath, createDefaultPresetDocument)
      : createDefaultPresetDocument();
    const presets = Array.isArray(presetDocument.presets) ? presetDocument.presets : [];

    characters.push(normalizeEntry(entry, libraryPath, presets));
  }

  return {
    libraryPath,
    libraryDirectory: path.dirname(libraryPath),
    activeCharacterId: text(document.activeCharacterId),
    characters
  };
}

export function resolvePresetFilePath(libraryPath, characterId) {
  if (!hasText(characterId) || characterId !== path.basename(characterId)) {
    return "";
  }

  return path.join(path.dirname(libraryPath), LIBRARY_PRESETS_FOLDER_NAME, `${characterId}.json`);
}

export function resolveThumbnailPath(libraryPath, thumbnailFileName) {
  if (!hasText(thumbnailFileName) || thumbnailFileName !== path.basename(thumbnailFileName)) {
    return "";
  }

  return path.join(
    path.dirname(libraryPath), LIBRARY_THUMBNAILS_FOLDER_NAME, thumbnailFileName);
}

function normalizeEntry(entry, libraryPath, presets) {
  const meta = entry.meta && typeof entry.meta === "object" ? entry.meta : {};
  const compatibility = entry.compatibility && typeof entry.compatibility === "object"
    ? entry.compatibility
    : {};
  return {
    id: text(entry.id),
    displayName: text(entry.displayName) || text(meta.title),
    sourcePath: text(entry.sourcePath),
    fileSize: Number.isFinite(entry.fileSize) ? entry.fileSize : 0,
    status: text(entry.status),
    lastError: text(entry.lastError),
    contentHash: text(entry.contentHash),
    vrmSpecVersion: text(entry.vrmSpecVersion),
    thumbnailPath: resolveThumbnailPath(libraryPath, text(entry.thumbnailFileName)),
    defaultPresetId: text(entry.defaultPresetId),
    registeredAtUtc: text(entry.registeredAtUtc),
    lastUsedAtUtc: text(entry.lastUsedAtUtc),
    humanoidValid: compatibility.humanoidValid === true,
    meta: {
      title: text(meta.title),
      version: text(meta.version),
      author: text(meta.author),
      contactInformation: text(meta.contactInformation),
      reference: text(meta.reference),
      licenseType: text(meta.licenseType),
      allowedUser: text(meta.allowedUser),
      commercialUsage: text(meta.commercialUsage),
      otherLicenseUrl: text(meta.otherLicenseUrl)
    },
    presets: presets
      .filter((preset) => preset && typeof preset === "object")
      .map((preset) => ({
        id: text(preset.id),
        name: text(preset.name),
        createdAtUtc: text(preset.createdAtUtc),
        updatedAtUtc: text(preset.updatedAtUtc),
        isDefault: text(preset.id) === text(entry.defaultPresetId),
        sections: preset.sections && typeof preset.sections === "object"
          ? preset.sections
          : {}
      }))
  };
}

function createDefaultLibraryDocument() {
  return { schemaVersion: 1, entries: [], activeCharacterId: "" };
}

function createDefaultPresetDocument() {
  return { schemaVersion: 1, characterId: "", presets: [] };
}

async function loadJsonOrDefault(filePath, createDefault) {
  try {
    const raw = await fs.readFile(filePath, "utf8");
    if (!hasText(raw)) {
      return createDefault();
    }

    const parsed = JSON.parse(raw);
    return parsed && typeof parsed === "object" ? parsed : createDefault();
  } catch (error) {
    if (error.code === "ENOENT") {
      return createDefault();
    }

    throw error;
  }
}

function text(value) {
  return typeof value === "string" ? value : "";
}

function hasText(value) {
  return typeof value === "string" && value.trim().length > 0;
}
