import assert from "node:assert/strict";
import fs from "node:fs/promises";
import test from "node:test";

test("built settings UI keeps a small connection status badge in the top-right toolbar", async () => {
  const html = await fs.readFile(new URL("../build/index.html", import.meta.url), "utf8");

  assert.match(
    html,
    /<header class="content-header">[\s\S]*<div id="statusBadge" class="status-badge"[\s\S]*aria-label="WebSocket 연결 상태"/
  );
  assert.match(html, /<span id="statusText">연결 안 됨<\/span>/);
});

test("built settings UI does not expose manual WebSocket controls", async () => {
  const html = await fs.readFile(new URL("../build/index.html", import.meta.url), "utf8");

  assert.doesNotMatch(html, /id="connectButton"/);
  assert.doesNotMatch(html, /id="disconnectButton"/);
  assert.doesNotMatch(html, /id="apiBaseUrl"/);
  assert.doesNotMatch(html, /id="wsUrl"/);
  assert.doesNotMatch(html, /WebSocket 연결<\/button>/);
  assert.doesNotMatch(html, /연결 종료<\/button>/);
});

test("connection status badge uses green only when connected and red when disconnected", async () => {
  const css = await fs.readFile(new URL("../build/styles.css", import.meta.url), "utf8");

  assert.match(css, /\.status-badge\[data-status="open"\] \.status-dot[\s\S]*background: #168251;/);
  assert.match(css, /\.status-badge\[data-status="idle"\] \.status-dot[\s\S]*background: #c73636;/);
  assert.match(css, /\.status-badge\[data-status="closed"\] \.status-dot[\s\S]*background: #c73636;/);
  assert.match(css, /\.status-badge\[data-status="error"\] \.status-dot[\s\S]*background: #c73636;/);
});
