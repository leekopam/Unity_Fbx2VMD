import assert from "node:assert/strict";
import { test } from "node:test";

import {
  CHARACTER_ACTIONS,
  createCharacterCommandRequest,
  createCharactersRequest,
  createCommandResultRequest,
  getCharacters,
  sendCharacterCommand
} from "../client/characterApi.js";

test("createCharactersRequest builds a GET /characters request", () => {
  const request = createCharactersRequest({ apiBaseUrl: "http://localhost:4100/" });
  assert.equal(request.url, "http://localhost:4100/characters");
  assert.equal(request.init.method, "GET");
});

test("createCommandResultRequest builds a GET /command-result request", () => {
  const request = createCommandResultRequest({ apiBaseUrl: "http://localhost:4100" });
  assert.equal(request.url, "http://localhost:4100/command-result");
  assert.equal(request.init.method, "GET");
});

test("createCharacterCommandRequest sends only populated envelope fields", () => {
  const request = createCharacterCommandRequest({
    apiBaseUrl: "http://localhost:4100",
    action: CHARACTER_ACTIONS.loadCharacter,
    characterId: "abc"
  });
  assert.equal(request.url, "http://localhost:4100/character-command");
  assert.equal(request.init.method, "POST");
  const body = JSON.parse(request.init.body);
  assert.deepEqual(body, { action: "LoadCharacter", characterId: "abc" });
});

test("createCharacterCommandRequest carries preset, path and params", () => {
  const request = createCharacterCommandRequest({
    apiBaseUrl: "http://localhost:4100",
    action: CHARACTER_ACTIONS.updatePreset,
    characterId: "abc",
    presetId: "p1",
    params: { sections: { placement: [{ key: "position", type: "vector3", value: "0,1,0" }] } }
  });
  const body = JSON.parse(request.init.body);
  assert.equal(body.action, "UpdatePreset");
  assert.equal(body.characterId, "abc");
  assert.equal(body.presetId, "p1");
  assert.equal(body.params.sections.placement[0].value, "0,1,0");
  assert.equal("path" in body, false);
});

test("createCharacterCommandRequest rejects empty action", () => {
  assert.throws(
    () => createCharacterCommandRequest({ apiBaseUrl: "http://x", action: " " }),
    /action 필요/);
});

test("sendCharacterCommand posts and parses the queued result", async () => {
  const seen = [];
  const fetchImpl = async (url, init) => {
    seen.push({ url, init });
    return {
      ok: true,
      json: async () => ({ accepted: true, commandId: "cmd1" })
    };
  };
  const result = await sendCharacterCommand({
    apiBaseUrl: "http://localhost:4100",
    action: CHARACTER_ACTIONS.registerVrm,
    path: "D:/models/miku.vrm",
    fetchImpl
  });
  assert.equal(result.commandId, "cmd1");
  assert.equal(JSON.parse(seen[0].init.body).path, "D:/models/miku.vrm");
});

test("getCharacters surfaces HTTP errors", async () => {
  const fetchImpl = async () => ({
    ok: false,
    status: 500,
    text: async () => '{"error":"boom"}'
  });
  await assert.rejects(
    getCharacters({ apiBaseUrl: "http://localhost:4100", fetchImpl }),
    /HTTP 오류 500/);
});
