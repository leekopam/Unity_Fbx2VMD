import assert from "node:assert/strict";
import fs from "node:fs/promises";
import test from "node:test";

const mojibakePattern = /(?:\?뚯씪|\?곌껐|罹|鍮|媛|濡|ㅼ젙|꾩|�)/;

test("built settings UI keeps Korean display text readable", async () => {
  const html = await fs.readFile(new URL("../build/index.html", import.meta.url), "utf8");
  const script = await fs.readFile(new URL("../client/settingsUi.js", import.meta.url), "utf8");

  assert.match(html, /<html lang="ko">/);
  assert.match(html, /<meta charset="UTF-8" \/>/);
  assert.match(html, /<title>Onboarding Assistant<\/title>/);
  assert.match(html, /<h1 id="settingsTitle">Onboarding Assistant<\/h1>/);
  assert.match(html, /FBX 파일을 선택해 프로젝트로 가져오고 모션 캡쳐 설정을 시작합니다\./);
  assert.match(html, /<span id="statusText">연결 안 됨<\/span>/);
  assert.match(html, /<button id="importButton" class="card-button card-button-primary" type="button" aria-label="FBX 파일 가져오기">FBX 가져오기<\/button>/);
  assert.match(html, /<h2 id="eventLogTitle">이벤트 로그<\/h2>/);
  assert.doesNotMatch(html, mojibakePattern);

  assert.match(script, /idle: "연결 안 됨"/);
  assert.match(script, /open: "연결됨"/);
  assert.match(script, /가져오기 버튼을 누르면 FBX 파일 선택 창이 열립니다\./);
  assert.match(script, /아직 수신한 메시지가 없습니다\./);
  assert.doesNotMatch(script, mojibakePattern);
});
