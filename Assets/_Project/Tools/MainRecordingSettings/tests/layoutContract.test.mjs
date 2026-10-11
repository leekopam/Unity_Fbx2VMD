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
  assert.match(html, /aria-label="온보딩" data-rail-page="assistant" data-panel-target="onboarding"/);
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
  assert.match(css, /grid-template-columns:\s*var\(--rail-width\) var\(--sidebar-width\) minmax\(640px, 1fr\);/);
  assert.match(css, /--rail-width:\s*56px;/);
  assert.match(css, /--sidebar-width:\s*249px;/);
  assert.match(css, /\.content-pane\s*{[\s\S]*overflow-y:\s*auto;/);
  assert.match(css, /\.onboarding-card\s*{[\s\S]*border-radius:\s*8px;/);
  assert.match(css, /\.tree-status\s*{[\s\S]*display:\s*none;/);
  assert.match(css, /\.tree-item\.selected \.tree-status\s*{[\s\S]*display:\s*inline-block;/);
});

test("sidebar splits character and environment pages — shader moved to environment page", async () => {
  const html = await fs.readFile(htmlUrl, "utf8");
  const css = await fs.readFile(cssUrl, "utf8");

  assert.match(html, /data-panel-target="onboarding"/);
  for (const panel of [
    "upperbody", "lowerbody", "physics", "library",
    "camera", "light", "shader", "lipsync", "audioio"
  ]) {
    assert.match(html, new RegExp(`data-panel-target="${panel}"`));
    assert.match(html, new RegExp(`data-panel-view="${panel}"[\\s\\S]*hidden`));
  }
  // 레일 페이지 전환: 온보딩(큐브) / 캐릭터(사람) / 환경 설정(전구) / 오디오(이퀄라이저)
  assert.match(html, /aria-label="온보딩" data-rail-page="assistant"/);
  assert.match(html, /aria-label="캐릭터" data-rail-page="character"/);
  assert.match(html, /aria-label="조명 도구" data-rail-page="environment"/);
  assert.match(html, /aria-label="오디오 및 립싱크" data-rail-page="audio"/);
  assert.match(html, /data-sidebar-page="assistant" data-default-panel="onboarding"/);
  assert.match(html, /data-sidebar-page="character" data-default-panel="upperbody"/);
  assert.match(html, /data-sidebar-page="environment" data-default-panel="camera"/);
  assert.match(html, /data-sidebar-page="audio" data-default-panel="lipsync"/);
  // 온보딩은 사이드바 선택지가 아니라 레일 페이지 버튼으로 연다.
  assert.doesNotMatch(html, /sidebar-heading[^>]*data-panel-target="onboarding"/);
  // 환경 설정 페이지는 헤딩 텍스트 없이 섹션만 보인다.
  assert.doesNotMatch(html, /환경 설정/);
  // 셰이더 항목은 환경 설정 페이지 안에 있고 캐릭터 섹션에는 없다.
  assert.match(
    html,
    /data-sidebar-page="environment"[\s\S]*?data-panel-target="shader"[\s\S]*?<\/div>\s*<div class="dev-mode-bar"/
  );
  const environmentPage = html.match(
    /data-sidebar-page="environment"[\s\S]*?<div class="dev-mode-bar"/
  );
  assert.ok(environmentPage);
  // 카메라·조명은 Stitch 계층 그대로 환경 페이지의 별도 항목이다.
  assert.match(environmentPage[0], /시네마토그래피[\s\S]*?data-panel-target="camera"[\s\S]*?Camera 1/);
  assert.match(environmentPage[0], />환경<[\s\S]*?data-panel-target="light"[\s\S]*?Directional Light/);
  assert.match(environmentPage[0], /data-panel-target="shader"[\s\S]*?툰 셰이더 파이프라인/);
  const characterPage = html.match(/data-sidebar-page="character"[\s\S]*?data-sidebar-page="environment"/);
  assert.ok(characterPage);
  assert.doesNotMatch(characterPage[0], /data-panel-target="shader"/);
  assert.doesNotMatch(characterPage[0], /data-panel-target="camera"/);
  assert.doesNotMatch(characterPage[0], /data-panel-target="light"/);
  // 캐릭터 페이지에는 라이브러리 섹션과 캐릭터 라이브러리 항목이 있다.
  assert.match(characterPage[0], />라이브러리<[\s\S]*?data-panel-target="library"[\s\S]*?캐릭터 라이브러리/);
  // 오디오 페이지에는 립싱크 파이프라인과 전역 I/O 항목이 있다.
  const audioPage = html.match(/data-sidebar-page="audio"[\s\S]*?<div class="dev-mode-bar"/);
  assert.ok(audioPage);
  assert.match(audioPage[0], /캐릭터 모델 립싱크 설정[\s\S]*?data-panel-target="lipsync"[\s\S]*?립싱크 및 오디오 파이프라인/);
  assert.match(audioPage[0], /오디오 전역 설정[\s\S]*?data-panel-target="audioio"[\s\S]*?전역 설정 및 I\/O 매핑/);
  assert.doesNotMatch(html, /<aside class="detail-panel"/);
  assert.match(css, /\.panel-view\[hidden\]\s*{[\s\S]*display:\s*none;/);
  assert.match(css, /\.sidebar-page\[hidden\]\s*{[\s\S]*display:\s*none;/);
});

test("camera and light panels declare the Stitch sections and controls", async () => {
  const html = await fs.readFile(htmlUrl, "utf8");
  const css = await fs.readFile(cssUrl, "utf8");

  // 카메라 패널: 트랜스폼/투영/배경/해상도/VMD + 개발자(AA·파이프라인)
  const cameraPanel = html.match(
    /data-panel-view="camera"[\s\S]*?<section id="lightPanel"/
  );
  assert.ok(cameraPanel);
  for (const id of ["camTransform", "camProjection", "camBackground", "camResolution", "camVmd"]) {
    assert.match(cameraPanel[0], new RegExp(`id="${id}"`));
  }
  assert.match(cameraPanel[0], /기본 트랜스폼/);
  assert.match(cameraPanel[0], /투영 및 구도/);
  assert.match(cameraPanel[0], /배경색 및 투명 녹화/);
  assert.match(cameraPanel[0], /캡처 해상도 및 프리셋/);
  assert.match(cameraPanel[0], /원클릭 캐릭터 자동 프레이밍/);
  assert.match(cameraPanel[0], /모션 베이킹 시작/);
  assert.match(cameraPanel[0], /data-dev-only[\s\S]*?id="camDevAA"[\s\S]*?id="camDevPipeline"/);
  assert.match(cameraPanel[0], /data-controls-value="persp"/);
  assert.match(cameraPanel[0], /data-controls-value="custom"/);

  // 조명 패널: 주 조명/그림자/앰비언트 + 개발자(바이어스·포그)
  const lightPanel = html.match(
    /data-panel-view="light"[\s\S]*?<section id="developerPanel"/
  );
  assert.ok(lightPanel);
  for (const id of ["ltMain", "ltShadow", "ltAmbient"]) {
    assert.match(lightPanel[0], new RegExp(`id="${id}"`));
  }
  assert.match(lightPanel[0], /주 조명/);
  assert.match(lightPanel[0], /그림자 설정/);
  assert.match(lightPanel[0], /앰비언트 환경광/);
  assert.match(lightPanel[0], /메인 카메라 기준 빛 방향 자동 일치/);
  assert.match(lightPanel[0], /기본값 조명 리셋/);
  assert.match(lightPanel[0], /실시간 라이팅 반영/);
  assert.match(lightPanel[0], /data-dev-only[\s\S]*?id="ltDevBias"[\s\S]*?id="ltDevAtmos"/);
  assert.match(lightPanel[0], /Trilight/);

  // 신규 컴포넌트 스타일과 헤더 액션 게이팅
  for (const sel of ["camera-icon", "sun-icon", "lb-vec", "lb-num", "lb-color-field"]) {
    assert.match(css, new RegExp(`\\.${sel}[\\s:{,]`));
  }
  assert.match(css, /data-active-panel="camera"[^\}]*#panelActions/);
  assert.match(css, /data-active-panel="light"[^\}]*#panelActions/);
});

test("library, lipsync, and audio I/O panels declare the Stitch sections and controls", async () => {
  const html = await fs.readFile(htmlUrl, "utf8");
  const css = await fs.readFile(cssUrl, "utf8");

  // 캐릭터 라이브러리 패널: 목록/프리셋 편집기/씬 복원 + 파이프라인 상태 카드
  const libraryPanel = html.match(
    /data-panel-view="library"[\s\S]*?<section id="cameraPanel"/
  );
  assert.ok(libraryPanel);
  for (const id of ["libPipeline", "libList", "libPresetEditor", "libSceneRestore", "libCmdResult"]) {
    assert.match(libraryPanel[0], new RegExp(`id="${id}"`));
  }
  assert.match(libraryPanel[0], /VRM 파일 추가/);
  assert.match(libraryPanel[0], /라이브러리 폴더 열기/);
  assert.match(libraryPanel[0], /등록된 캐릭터 목록/);
  // 캐릭터 행은 characterPanel.js가 레지스트리 스냅샷으로 동적 렌더링한다.
  assert.match(libraryPanel[0], /id="libListBody"/);
  assert.match(libraryPanel[0], /id="libPresetFields"/);
  assert.match(libraryPanel[0], /프리셋 파라미터 편집기/);
  assert.match(libraryPanel[0], /씬 캐릭터 복원/);
  assert.match(libraryPanel[0], /lb-toc-sublink/);

  // 립싱크 패널: 소스/분리 엔진/음소/가사 + 개발자(서버 진단)
  const lipSyncPanel = html.match(
    /data-panel-view="lipsync"[\s\S]*?<section id="audioIoPanel"/
  );
  assert.ok(lipSyncPanel);
  for (const id of ["lipProgress", "lipSource", "lipSeparateEngine", "lipPhoneme", "lipLyrics"]) {
    assert.match(lipSyncPanel[0], new RegExp(`id="${id}"`));
  }
  assert.match(lipSyncPanel[0], /보컬\/BGM 분리/);
  assert.match(lipSyncPanel[0], /가사 자동 검색/);
  assert.match(lipSyncPanel[0], /음소 추출/);
  assert.match(lipSyncPanel[0], /오디오 출력 장치/);
  assert.match(lipSyncPanel[0], /data-controls-value="audioseparator"/);
  assert.match(lipSyncPanel[0], /data-controls="subPhoneme"/);
  assert.match(lipSyncPanel[0], /data-dev-only[\s\S]*?id="lipDevServer"/);
  assert.match(lipSyncPanel[0], /127\.0\.0\.1:17845/);

  // 오디오 I/O 패널: 출력 장치/샘플레이트/버퍼
  const audioIoPanel = html.match(
    /data-panel-view="audioio"[\s\S]*?<section id="developerPanel"/
  );
  assert.ok(audioIoPanel);
  assert.match(audioIoPanel[0], /id="aioOutput"/);
  assert.match(audioIoPanel[0], /기본 출력 장치/);
  assert.match(audioIoPanel[0], /샘플레이트/);
  assert.match(audioIoPanel[0], /미리듣기 버퍼/);

  // 신규 컴포넌트 스타일
  for (const sel of ["char-row", "char-thumb", "lb-file-row", "lb-logbox", "lb-progress", "lb-chip", "lb-chip-err", "mic-icon", "folder-icon", "tune-icon"]) {
    assert.match(css, new RegExp(`\\.${sel}[\\s:{,]`));
  }
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
