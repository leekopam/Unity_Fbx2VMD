import assert from "node:assert/strict";
import { EventEmitter } from "node:events";
import test from "node:test";

import { extractWorkbenchUrl, startBoogleWorkbench } from "../electron/boogleWorkbench.js";

function createFakeChild() {
  const child = new EventEmitter();
  child.stdout = new EventEmitter();
  child.stderr = new EventEmitter();
  child.exitCode = null;
  child.signalCode = null;
  child.killed = false;
  child.kill = () => {
    child.killed = true;
    child.exitCode = 0;
    return true;
  };
  return child;
}

test("extractWorkbenchUrl은 준비 줄의 127.0.0.1 주소만 인정한다", () => {
  assert.equal(extractWorkbenchUrl("BOOGLE_WORKBENCH_READY http://127.0.0.1:7764"), "http://127.0.0.1:7764");
  assert.equal(extractWorkbenchUrl("BOOGLE_WORKBENCH_READY http://127.0.0.1:7764\r"), "http://127.0.0.1:7764");
  assert.equal(extractWorkbenchUrl("other line"), "");
  assert.equal(extractWorkbenchUrl("BOOGLE_WORKBENCH_READY http://0.0.0.0:7764"), "");
  assert.equal(extractWorkbenchUrl("BOOGLE_WORKBENCH_READY http://127.0.0.1:7764/attack"), "");
  assert.equal(extractWorkbenchUrl(null), "");
});

test("startBoogleWorkbench는 준비 줄에서 URL을 얻고 ELECTRON_RUN_AS_NODE로 실행한다", async () => {
  const child = createFakeChild();
  const calls = [];
  const workbench = startBoogleWorkbench({
    appRoot: "C:\\app",
    spawnProcess: (file, args, options) => {
      calls.push({ file, args, options });
      return child;
    }
  });

  child.stdout.emit("data", Buffer.from("noise line\nBOOGLE_WORKBENCH_READY http://127.0.0.1:9001\n"));
  assert.equal(await workbench.url, "http://127.0.0.1:9001");
  assert.equal(calls[0].options.env.ELECTRON_RUN_AS_NODE, "1");
  assert.equal(calls[0].args[0].endsWith("cli.js"), true);
  assert.deepEqual(calls[0].args[1], "workbench");

  workbench.stop();
  assert.equal(child.killed, true);
});

test("Workbench 프로세스가 준비 전에 종료되면 URL 요청이 거절된다", async () => {
  const child = createFakeChild();
  const workbench = startBoogleWorkbench({
    appRoot: "C:\\app",
    spawnProcess: () => child
  });

  child.emit("exit", 2);
  await assert.rejects(workbench.url, /종료/);
});

test("준비 시간이 지나면 자식 프로세스를 종료하고 URL 요청이 거절된다", async () => {
  const child = createFakeChild();
  const workbench = startBoogleWorkbench({
    appRoot: "C:\\app",
    spawnProcess: () => child,
    readyTimeoutMs: 10
  });

  await assert.rejects(workbench.url, /시간 초과/);
  assert.equal(child.killed, true);
});
