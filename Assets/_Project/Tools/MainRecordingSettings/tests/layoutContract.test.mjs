import assert from "node:assert/strict";
import fs from "node:fs/promises";
import test from "node:test";

const htmlUrl = new URL("../build/index.html", import.meta.url);
const cssUrl = new URL("../build/styles.css", import.meta.url);
const mainUrl = new URL("../electron/main.js", import.meta.url);

test("settings shell exposes the reference rail, sidebar, main pane, and status regions", async () => {
  const html = await fs.readFile(htmlUrl, "utf8");

  assert.match(html, /<div class="app-shell" data-shell="main-recording-settings" data-active-panel="onboarding">/);
  assert.match(html, /<nav class="rail" aria-label="설정 도구">/);
  assert.match(html, /<aside class="sidebar" aria-label="장면 계층">/);
  assert.match(html, /<section class="content-pane" aria-labelledby="settingsTitle">/);
  assert.match(
    html,
    /<div id="statusBadge" class="status-badge" data-status="idle" aria-label="WebSocket 연결 상태" aria-live="polite">/
  );
});

test("onboarding shell renders one page-owned settings region without the camera detail inspector", async () => {
  const html = await fs.readFile(htmlUrl, "utf8");

  assert.match(html, /<div class="app-shell" data-shell="main-recording-settings" data-active-panel="onboarding">/);
  assert.match(html, /<button class="sidebar-heading active" type="button" aria-current="page" data-panel-target="onboarding">/);
  assert.doesNotMatch(html, /<aside class="detail-panel"/);
  assert.doesNotMatch(html, /class="tree-item selected"/);
});

test("onboarding shell renders three color cards with a single FBX import button as the primary action", async () => {
  const html = await fs.readFile(htmlUrl, "utf8");

  assert.equal((html.match(/class="onboarding-card/g) ?? []).length, 3);
  assert.match(html, /<article class="onboarding-card card-primary" data-card="fbx-import">/);
  assert.match(html, /<article class="onboarding-card card-secondary" data-card="interaction">/);
  assert.match(html, /<article class="onboarding-card card-tertiary" data-card="community">/);
  assert.match(html, /data-card="fbx-import"[\s\S]*id="importButton"[\s\S]*FBX 가져오기/);
  assert.doesNotMatch(html, /id="fbxPath"/);
  assert.doesNotMatch(html, /id="chooseFbxButton"/);
  assert.doesNotMatch(html, />파일 선택<\/button>/);
  assert.match(html, /data-card="interaction"[\s\S]*준비중/);
  assert.match(html, /data-card="community"[\s\S]*준비중/);
});

test("layout CSS keeps the 1265x675 reference frame and separates onboarding from detail panels", async () => {
  const css = await fs.readFile(cssUrl, "utf8");

  assert.match(css, /--reference-width:\s*1265px;/);
  assert.match(css, /--reference-height:\s*675px;/);
  assert.match(css, /html,\s*body\s*{[\s\S]*height:\s*100%;[\s\S]*overflow:\s*hidden;/);
  assert.match(css, /\.settings-app\s*{[\s\S]*height:\s*100vh;[\s\S]*overflow:\s*hidden;/);
  assert.match(css, /grid-template-columns:\s*56px 249px minmax\(640px, 1fr\);/);
  assert.match(css, /\.content-pane\s*{[\s\S]*overflow-y:\s*auto;/);
  assert.match(css, /\.onboarding-card\s*{[\s\S]*border-radius:\s*8px;/);
  assert.match(css, /\.tree-status\s*{[\s\S]*display:\s*none;/);
  assert.match(css, /\.tree-item\.selected \.tree-status\s*{[\s\S]*display:\s*inline-block;/);
});

test("camera sidebar item switches the single content pane from onboarding to camera settings", async () => {
  const html = await fs.readFile(htmlUrl, "utf8");
  const css = await fs.readFile(cssUrl, "utf8");

  assert.match(html, /data-panel-target="onboarding"/);
  assert.match(html, /data-panel-target="camera"/);
  assert.match(html, /data-panel-view="onboarding"/);
  assert.match(html, /data-panel-view="camera"[\s\S]*hidden/);
  assert.match(html, /id="cameraSettingsTitle"/);
  assert.doesNotMatch(html, /<aside class="detail-panel"/);
  assert.match(css, /\.panel-view\[hidden\]\s*{[\s\S]*display:\s*none;/);
});

test("Electron production window opens at the reference shell size", async () => {
  const main = await fs.readFile(mainUrl, "utf8");

  assert.match(main, /width:\s*1265,/);
  assert.match(main, /height:\s*675,/);
});

test("onboarding CSS keeps the FBX action compact without copying bundled reference assets", async () => {
  const css = await fs.readFile(cssUrl, "utf8");

  assert.match(css, /\.fbx-action-row\s*{[\s\S]*grid-template-columns:\s*max-content;/);
  assert.match(css, /\.card-button\s*{[\s\S]*min-width:\s*96px;[\s\S]*min-height:\s*32px;/);
  assert.doesNotMatch(css, /build\/assets\//);
  assert.doesNotMatch(css, /primeicons|noto-sans-sc|radio-canada/i);
});
