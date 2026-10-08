// 보정·물리 파이프라인 패널 — 테마 전환, 개발자 모드, 카드/슬라이더/초기화 인터랙션
// 상태 키는 data-* 속성으로 마크업에 선언하고, 값 읽기·쓰기는 이 모듈이 일원화한다.

const THEME_STORAGE_KEY = "mainRecordingSettings.theme";

export function bootstrapLowerBodyUi(root = document, { storage } = {}) {
  const themeRoot = root.querySelector(".settings-app");
  const shell = root.querySelector(".app-shell");
  const onboardingTarget = root.querySelector('[data-panel-target="onboarding"]');
  const menuButton = root.querySelector("#railMenuButton");
  const menuPopup = root.querySelector("#railMenuPopup");
  const themeMenuItem = root.querySelector("#themeMenuToggle");
  const themeStatusBadge = root.querySelector("#themeStatusBadge");
  const settingsOpenItem = root.querySelector("#menuOpenSettings");
  const settingsModal = root.querySelector("#settingsModal");
  const settingsClose = root.querySelector("#settingsModalClose");
  const settingsBackdrop = root.querySelector("#settingsModalBackdrop");
  const devSwitch = root.querySelector("#devModeSwitch");
  const devBlocks = toArray(root.querySelectorAll?.("[data-dev-only]"));
  const cards = toArray(root.querySelectorAll?.(".lb-card"));
  const sliders = toArray(root.querySelectorAll?.(".lb-slider"));
  const segs = toArray(root.querySelectorAll?.(".lb-seg"));
  const cardResets = toArray(root.querySelectorAll?.("[data-lb-reset]"));
  const rowResets = toArray(root.querySelectorAll?.(".lb-row-reset"));
  const hairBackendSeg = root.querySelector("#hairBackendSeg");
  const boingFallbackBadge = root.querySelector("#boingFallbackBadge");
  const store = storage ?? safeLocalStorage();

  applyTheme(readTheme(store));
  setDevMode(devSwitch?.classList.contains("on") ?? false);
  for (const slider of sliders) paintSlider(slider);
  for (const seg of segs) syncSegEffects(seg, getSegValue(seg));

  menuButton?.addEventListener("click", (event) => {
    event.stopPropagation?.();
    setMenuOpen(menuPopup?.hidden !== false);
  });

  // 팝업 바깥 클릭 / Esc 로 닫기 (모달도 함께 닫는다)
  root.addEventListener?.("click", (event) => {
    if (menuPopup?.hidden) return;
    if (event.target?.closest?.("#railMenuPopup, #railMenuButton")) return;
    setMenuOpen(false);
  });
  root.addEventListener?.("keydown", (event) => {
    if (event.key !== "Escape") return;
    setMenuOpen(false);
    setModalOpen(false);
  });

  // 다크 모드 행: 클릭해도 팝업을 닫지 않고 배지·미니 스위치를 즉시 갱신한다.
  themeMenuItem?.addEventListener("click", () => {
    const next = themeRoot?.dataset.theme === "dark" ? "light" : "dark";
    applyTheme(next);
    store?.setItem?.(THEME_STORAGE_KEY, next);
  });

  // 설정 모달: 메뉴 "설정"에서 열고 ×·배경·Esc로 닫는다.
  settingsOpenItem?.addEventListener("click", () => {
    setModalOpen(true);
  });
  settingsClose?.addEventListener("click", () => setModalOpen(false));
  settingsBackdrop?.addEventListener("click", () => setModalOpen(false));

  // 테마 행을 제외한 메뉴 항목은 선택 시 팝업을 닫는다.
  for (const item of toArray(menuPopup?.querySelectorAll?.(".menu-item"))) {
    if (item === themeMenuItem) continue;
    item.addEventListener("click", () => setMenuOpen(false));
  }

  devSwitch?.addEventListener("click", () => {
    setDevMode(!devSwitch.classList.contains("on"));
  });

  for (const card of cards) {
    card.querySelector(".lb-card-head")?.addEventListener("click", (event) => {
      // 세그먼트·초기화 버튼 클릭은 접기로 처리하지 않는다.
      if (event.target?.closest?.(".lb-seg, .lb-reset")) return;
      card.classList.toggle("collapsed");
    });
  }

  for (const seg of segs) {
    seg.addEventListener("click", (event) => {
      const button = event.target?.closest?.("button[data-val]");
      if (!button) return;
      for (const b of seg.querySelectorAll("button[data-val]")) {
        b.setAttribute("aria-pressed", String(b === button));
      }
      syncSegEffects(seg, button.dataset.val);
    });
  }

  // 우측 섹션 목차: 클릭 스크롤 + 스크롤 스파이로 현재 카드 하이라이트
  const tocLinks = toArray(root.querySelectorAll?.(".lb-toc-link, .lb-toc-sublink"));
  const tocTargets = new Map();
  for (const link of tocLinks) {
    const target = root.querySelector(link.getAttribute("href") ?? "");
    if (!target) continue;
    tocTargets.set(target, link);
    link.addEventListener("click", (event) => {
      event.preventDefault?.();
      target.scrollIntoView({ block: "start", behavior: "smooth" });
      setActiveToc(target);
    });
  }

  if (tocTargets.size && typeof globalThis.IntersectionObserver === "function") {
    const spy = new IntersectionObserver((entries) => {
      const visible = entries
        .filter((e) => e.isIntersecting)
        .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
      if (visible[0]) setActiveToc(visible[0].target);
    }, { root: root.querySelector?.(".content-pane") ?? null, rootMargin: "-8% 0px -75% 0px" });
    for (const target of tocTargets.keys()) spy.observe(target);
  }

  function setActiveToc(target) {
    const link = tocTargets.get(target);
    if (!link) return;
    for (const l of tocLinks) l.classList.remove("active");
    link.classList.add("active");
    link.closest(".lb-toc-group")?.querySelector(".lb-toc-link")?.classList.add("active");
  }

  // 물리 패널 액션 버튼 피드백 (브릿지 미연결 — 시각적 확인만)
  wireBusyButton("#phAutoSetup", "자동 매핑 중...", "설정 완료 (38개 본 매핑)");
  wireBusyButton("#phReapply", null, "적용됨");
  wireBusyButton("#phResetSim", null, "리셋됨");
  wireBusyButton("#phRebuild", null, "재생성됨");
  wireBusyButton("#phExportJson", null, "보냄");
  wireBusyButton("#phRunValidation", "시퀀스 전송 중...", "전송됨");
  wireBusyButton("#phRunBenchmark", "측정 중...", "완료");

  root.querySelector("#phClearGenerated")?.addEventListener("click", () => {
    const confirmed = globalThis.window?.confirm?.(
      "생성된 모든 메쉬 클로스 프록시와 캡슐 콜라이더를 삭제하고 원본 리깅 상태로 복원하시겠습니까?");
    if (confirmed) wireBusyButtonRun(root.querySelector("#phClearGenerated"), "초기화 완료");
  });

  for (const slider of sliders) {
    slider.addEventListener("input", () => paintSlider(slider));
  }

  for (const button of cardResets) {
    button.addEventListener("click", (event) => {
      event.stopPropagation?.();
      resetCard(button.closest(".lb-card"));
    });
  }

  for (const button of rowResets) {
    button.addEventListener("click", (event) => {
      event.stopPropagation?.();
      resetRow(button.closest(".lb-param"));
    });
  }

  function applyTheme(theme) {
    if (themeRoot) themeRoot.dataset.theme = theme;
    const dark = theme === "dark";
    themeMenuItem?.setAttribute("aria-checked", String(dark));
    if (themeStatusBadge) themeStatusBadge.textContent = dark ? "ON" : "OFF";
  }

  function setMenuOpen(open) {
    if (!menuPopup || !menuButton) return;
    menuPopup.hidden = !open;
    menuButton.setAttribute("aria-expanded", String(open));
  }

  function setModalOpen(open) {
    if (settingsModal) settingsModal.hidden = !open;
  }

  function readTheme(storeRef) {
    try {
      return storeRef?.getItem?.(THEME_STORAGE_KEY)
        ?? themeRoot?.dataset.theme
        ?? "light";
    } catch {
      return themeRoot?.dataset.theme ?? "light";
    }
  }

  function setDevMode(on) {
    devSwitch?.classList.toggle("on", on);
    devSwitch?.setAttribute("aria-checked", String(on));
    for (const block of devBlocks) block.hidden = !on;
    // 개발자 전용 패널을 보는 중 끄면 온보딩으로 되돌린다.
    if (!on && shell?.dataset.activePanel === "developer") {
      onboardingTarget?.click?.();
    }
  }

  // 슬라이더 채움 효과 + 값 표시 갱신 (0값 라벨 지원: 거리 컬링 "비활성")
  function paintSlider(slider) {
    const min = Number(slider.min ?? 0);
    const max = Number(slider.max ?? 100);
    const value = Number(slider.value);
    const pct = max > min ? ((value - min) / (max - min)) * 100 : 0;
    slider.style?.setProperty("--fill", `${pct}%`);
    const box = slider.closest(".lb-param-ctl")?.querySelector(".lb-val");
    if (box) {
      const zeroLabel = slider.dataset.zeroLabel;
      if (zeroLabel && value === Number(slider.min ?? 0)) {
        box.textContent = zeroLabel;
      } else {
        const decimals = Number(slider.dataset.decimals ?? 0);
        box.textContent = value.toFixed(decimals);
      }
    }
  }

  function resetCard(card) {
    if (!card) return;
    for (const slider of card.querySelectorAll(".lb-slider")) {
      setSliderValue(slider, slider.dataset.default);
    }
    for (const seg of card.querySelectorAll(".lb-seg")) {
      setSegValue(seg, seg.dataset.default);
    }
  }

  function resetRow(row) {
    if (!row) return;
    const slider = row.querySelector(".lb-slider");
    if (slider) setSliderValue(slider, slider.dataset.default);
    const seg = row.querySelector(".lb-seg");
    if (seg) setSegValue(seg, seg.dataset.default);
  }

  function setSliderValue(slider, value) {
    if (value == null) return;
    slider.value = value;
    paintSlider(slider);
  }

  function setSegValue(seg, value) {
    if (value == null) return;
    for (const b of seg.querySelectorAll("button[data-val]")) {
      b.setAttribute("aria-pressed", String(b.dataset.val === value));
    }
    syncSegEffects(seg, value);
  }

  function getSegValue(seg) {
    const pressed = toArray(seg.querySelectorAll?.("button[data-val]"))
      .find((b) => b.getAttribute("aria-pressed") === "true");
    return pressed?.dataset.val ?? null;
  }

  // 세그먼트 부수 효과: 종속 서브블록 표시/숨김 + Boing 폴백 배지
  function syncSegEffects(seg, value) {
    const controlsId = seg.dataset?.controls;
    if (controlsId) {
      const sub = root.querySelector(`#${controlsId}`);
      if (sub) sub.hidden = value === "off" || value === "no";
    }
    if (seg === hairBackendSeg && boingFallbackBadge) {
      boingFallbackBadge.hidden = value !== "boing";
    }
  }

  // 클릭 시 진행 문구 → 완료 문구 → 원복 순으로 라벨을 바꾸는 시각 피드백
  function wireBusyButton(selector, busyText, doneText) {
    const button = root.querySelector(selector);
    if (!button) return;
    button.addEventListener("click", () => wireBusyButtonRun(button, doneText, busyText));
  }

  function wireBusyButtonRun(button, doneText, busyText = null) {
    const label = button.querySelector?.("span") ?? button;
    const original = label.textContent;
    if (busyText) label.textContent = busyText;
    globalThis.setTimeout?.(() => {
      label.textContent = doneText;
      globalThis.setTimeout?.(() => { label.textContent = original; }, 1600);
    }, busyText ? 700 : 0);
  }
}

function toArray(list) {
  return list ? Array.from(list) : [];
}

function safeLocalStorage() {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    return null;
  }
}

if (typeof document !== "undefined") {
  bootstrapLowerBodyUi(document);
}
