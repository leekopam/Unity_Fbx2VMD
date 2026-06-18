import assert from "node:assert/strict";
import test from "node:test";

import {
  createImportFbxRequest,
  createPlayModeAutoConnectionController,
  createRuntimeStateRequest,
  createSettingsWebSocket,
  getRuntimeState,
  postImportFbx
} from "../client/settingsApi.js";

test("createImportFbxRequest builds POST /import-fbx request", () => {
  const request = createImportFbxRequest({
    apiBaseUrl: "http://localhost:4100/",
    fbxPath: " C:/Motion/sample.fbx "
  });

  assert.equal(request.url, "http://localhost:4100/import-fbx");
  assert.equal(request.init.method, "POST");
  assert.equal(request.init.headers["Content-Type"], "application/json");
  assert.equal(request.init.body, JSON.stringify({ fbxPath: "C:/Motion/sample.fbx" }));
});

test("createImportFbxRequest rejects empty FBX path", () => {
  assert.throws(
    () => createImportFbxRequest({ apiBaseUrl: "http://localhost:4100", fbxPath: " " }),
    /FBX 경로 필요/
  );
});

test("postImportFbx sends request and returns JSON response", async () => {
  const calls = [];
  const result = await postImportFbx({
    apiBaseUrl: "http://localhost:4100",
    fbxPath: "C:/Motion/sample.fbx",
    fetchImpl: async (url, init) => {
      calls.push({ url, init });
      return {
        ok: true,
        status: 200,
        json: async () => ({ accepted: true, commandId: "cmd-1" })
      };
    }
  });

  assert.equal(calls.length, 1);
  assert.equal(calls[0].url, "http://localhost:4100/import-fbx");
  assert.deepEqual(result, { accepted: true, commandId: "cmd-1" });
});

test("postImportFbx includes HTTP failure status in error", async () => {
  await assert.rejects(
    () => postImportFbx({
      apiBaseUrl: "http://localhost:4100",
      fbxPath: "C:/Motion/sample.fbx",
      fetchImpl: async () => ({
        ok: false,
        status: 503,
        text: async () => "서비스 불가"
      })
    }),
    /HTTP 오류 503: 서비스 불가/
  );
});

test("getRuntimeState fetches GET /state and returns runtime state document", async () => {
  const calls = [];
  const result = await getRuntimeState({
    apiBaseUrl: "http://localhost:4100/",
    fetchImpl: async (url, init) => {
      calls.push({ url, init });
      return {
        ok: true,
        status: 200,
        json: async () => ({
          ok: true,
          runtimeState: {
            playMode: "playing",
            updatedAtUtc: "2026-06-18T04:00:00.000Z"
          }
        })
      };
    }
  });

  assert.deepEqual(createRuntimeStateRequest({ apiBaseUrl: "http://localhost:4100/" }), {
    url: "http://localhost:4100/state",
    init: { method: "GET" }
  });
  assert.equal(calls.length, 1);
  assert.equal(calls[0].url, "http://localhost:4100/state");
  assert.deepEqual(calls[0].init, { method: "GET" });
  assert.deepEqual(result.runtimeState, {
    playMode: "playing",
    updatedAtUtc: "2026-06-18T04:00:00.000Z"
  });
});

test("play mode auto connection controller opens only while Unity is playing", async () => {
  const states = [
    { runtimeState: { playMode: "stopped" } },
    { runtimeState: { playMode: "playing" } },
    { runtimeState: { playMode: "playing" } },
    { runtimeState: { playMode: "stopped" } }
  ];
  const statuses = [];
  let connectionCount = 0;
  let disconnectCount = 0;

  const controller = createPlayModeAutoConnectionController({
    fetchRuntimeState: async () => states.shift(),
    createWebSocketChannel: ({ wsUrl, onStatusChange }) => {
      connectionCount += 1;
      assert.equal(wsUrl, "ws://localhost:4100/settings");
      onStatusChange("connecting");
      return {
        disconnect() {
          disconnectCount += 1;
        }
      };
    },
    onStatusChange: (status) => statuses.push(status)
  });

  await controller.pollOnce();
  await controller.pollOnce();
  await controller.pollOnce();
  await controller.pollOnce();

  assert.equal(connectionCount, 1);
  assert.equal(disconnectCount, 1);
  assert.deepEqual(statuses, ["closed", "connecting", "closed"]);
});

test("createSettingsWebSocket reports connection states and messages", () => {
  const statuses = [];
  const messages = [];

  class FakeWebSocket {
    static instances = [];

    constructor(url) {
      this.url = url;
      FakeWebSocket.instances.push(this);
    }

    close() {
      this.closedByClient = true;
    }
  }

  const channel = createSettingsWebSocket({
    wsUrl: "ws://localhost:4100/settings",
    WebSocketImpl: FakeWebSocket,
    onStatusChange: (status) => statuses.push(status),
    onMessage: (message) => messages.push(message)
  });

  const socket = FakeWebSocket.instances[0];
  socket.onopen();
  socket.onmessage({ data: "{\"type\":\"ready\"}" });
  socket.onerror();
  channel.disconnect();

  assert.equal(socket.url, "ws://localhost:4100/settings");
  assert.deepEqual(statuses, ["connecting", "open", "error"]);
  assert.deepEqual(messages, [{ type: "ready" }]);
  assert.equal(socket.closedByClient, true);
});
