import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";

import {
  readCharacterLibrary,
  resolveLibraryFilePath,
  resolvePresetFilePath,
  resolveThumbnailPath
} from "../server/characterLibraryStore.js";

async function makeLibraryDir() {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), "charlib-"));
  return { dir, libraryPath: path.join(dir, "characters.json") };
}

test("resolveLibraryFilePath follows the Unity resolver order", () => {
  assert.equal(
    resolveLibraryFilePath({ explicitPath: "  D:/lib/characters.json " }),
    "D:/lib/characters.json");
  assert.equal(
    resolveLibraryFilePath({
      env: { UNITY_FBX2VMD_CHARACTER_LIBRARY_PATH: "D:/env/characters.json" },
      localAppDataRoot: "D:/appdata"
    }),
    "D:/env/characters.json");
  assert.equal(
    resolveLibraryFilePath({ env: {}, localAppDataRoot: "D:/appdata" }),
    path.join("D:/appdata", "Unity_Fbx2VMD", "CharacterLibrary", "characters.json"));
});

test("readCharacterLibrary returns an empty snapshot when the file is missing", async () => {
  const { libraryPath } = await makeLibraryDir();
  const library = await readCharacterLibrary({ libraryPath });

  assert.equal(library.activeCharacterId, "");
  assert.deepEqual(library.characters, []);
});

test("readCharacterLibrary normalizes entries and merges preset documents", async () => {
  const { dir, libraryPath } = await makeLibraryDir();
  await fs.writeFile(libraryPath, JSON.stringify({
    schemaVersion: 1,
    activeCharacterId: "abc",
    entries: [
      {
        id: "abc",
        displayName: "Miku",
        sourcePath: "D:/models/miku.vrm",
        fileSize: 26306676,
        status: "ready",
        vrmSpecVersion: "0.0",
        thumbnailFileName: "abc.png",
        defaultPresetId: "p1",
        registeredAtUtc: "2026-10-05T10:51:20Z",
        meta: { title: "止丸式初音ミクNT", author: "止丸", licenseType: "Redistribution_Prohibited" },
        compatibility: { humanoidValid: true }
      },
      { id: "ghost" } // 프리셋 파일 없는 엔트리도 목록에 남는다
    ]
  }), "utf8");

  const presetsDir = path.join(dir, "presets");
  await fs.mkdir(presetsDir, { recursive: true });
  await fs.writeFile(path.join(presetsDir, "abc.json"), JSON.stringify({
    presets: [
      { id: "p1", name: "Default Stage", updatedAtUtc: "2026-10-06T00:00:00Z",
        sections: { placement: [{ key: "position", type: "vector3", value: "0,0,0" }] } },
      { id: "p2", name: "Dance" }
    ]
  }), "utf8");

  const library = await readCharacterLibrary({ libraryPath });

  assert.equal(library.activeCharacterId, "abc");
  assert.equal(library.libraryDirectory, dir);
  assert.equal(library.characters.length, 2);

  const miku = library.characters[0];
  assert.equal(miku.displayName, "Miku");
  assert.equal(miku.humanoidValid, true);
  assert.equal(miku.meta.author, "止丸");
  assert.equal(miku.thumbnailPath, path.join(dir, "thumbnails", "abc.png"));
  assert.equal(miku.presets.length, 2);
  assert.equal(miku.presets[0].isDefault, true);
  assert.equal(miku.presets[1].isDefault, false);
  assert.equal(miku.presets[0].sections.placement[0].key, "position");

  const ghost = library.characters[1];
  assert.equal(ghost.id, "ghost");
  assert.deepEqual(ghost.presets, []);
});

test("path resolvers reject traversal segments", async () => {
  const { libraryPath } = await makeLibraryDir();
  assert.equal(resolvePresetFilePath(libraryPath, "../evil"), "");
  assert.equal(resolveThumbnailPath(libraryPath, "..\\evil.png"), "");
  assert.equal(resolvePresetFilePath(libraryPath, "ok"),
    path.join(path.dirname(libraryPath), "presets", "ok.json"));
});
