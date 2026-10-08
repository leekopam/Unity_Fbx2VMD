import assert from "node:assert/strict";
import fs from "node:fs/promises";
import test from "node:test";

import { bootstrapLowerBodyUi } from "../client/lowerBodyUi.js";

// 파이프라인 패널 마크업 계약: 트리 항목·패널 뷰·개발자 블록·메뉴/모달이 함께 존재해야 한다.
test("index.html declares pipeline panels, tree entries, and dev-mode blocks", async () => {
  const html = await fs.readFile(new URL("../build/index.html", import.meta.url), "utf8");

  for (const panel of ["upperbody", "lowerbody", "physics"]) {
    assert.match(html, new RegExp(`data-panel-target="${panel}"`));
    assert.match(html, new RegExp(`data-panel-view="${panel}"`));
  }
  assert.match(html, /id="lowerBodyPanel"/);
  assert.match(html, /id="upperBodyPanel"/);
  assert.match(html, /id="physicsPanel"/);
  assert.match(html, /id="railMenuButton"/);
  assert.match(html, /id="railMenuPopup"/);
  assert.match(html, /id="themeMenuToggle"/);
  assert.match(html, /id="themeStatusBadge"/);
  assert.match(html, /id="panelActions"/);
  assert.match(html, /id="panelPresetSelect"/);
  assert.match(html, /lb-btn-solid/);
  assert.match(html, /id="devModeSwitch"/);
  assert.match(html, /data-dev-only/);
  assert.match(html, /하체 보정 파이프라인/);
  assert.match(html, /상체 보정 파이프라인/);
  assert.match(html, /캐릭터 물리 파이프라인/);
  // 우측 섹션 목차 레일은 유지하되 "인스펙터 바로가기" 헤딩 텍스트만 제거한다.
  assert.match(html, /class="lb-anchor"/);
  assert.doesNotMatch(html, /인스펙터 바로가기/);
  // 물리 패널 전용 요소
  assert.match(html, /id="hairBackendSeg"/);
  assert.match(html, /id="boingFallbackBadge"/);
  assert.match(html, /id="phAutoSetup"/);
  assert.match(html, /id="settingsModal"/);
  assert.match(html, /id="menuOpenSettings"/);

  // 패널 헤더 제목은 패널 전환 시 settingsUi.js가 주입한다.
  const script = await fs.readFile(new URL("../client/settingsUi.js", import.meta.url), "utf8");
  assert.match(script, /lowerbody:\s*"하체 보정 파라미터"/);
  assert.match(script, /upperbody:\s*"상체 보정 파라미터"/);
  assert.match(script, /physics:\s*"캐릭터 물리 파라미터"/);
});

test("rail menu popup opens on menu button and theme item toggles the theme", () => {
  const app = fakeEl("main", { classes: ["settings-app"], dataset: { theme: "light" } });
  const button = fakeEl("button", { attrs: { id: "railMenuButton", "aria-expanded": "false" } });
  const badge = fakeEl("span", { attrs: { id: "themeStatusBadge" } });
  const themeItem = fakeEl("button", { attrs: { id: "themeMenuToggle" } });
  const folderItem = fakeEl("button", { classes: ["menu-item"] });
  themeItem.classList.add("menu-item");
  const popup = fakeEl("div", {
    attrs: { id: "railMenuPopup" },
    hidden: true,
    children: [folderItem, themeItem]
  });
  const root = fakeRoot({
    ".settings-app": app,
    "#railMenuButton": button,
    "#railMenuPopup": popup,
    "#themeMenuToggle": themeItem,
    "#themeStatusBadge": badge
  });
  const store = fakeStorage();

  bootstrapLowerBodyUi(root, { storage: store });

  // 메뉴 버튼으로 팝업 열기/닫기
  button.dispatch("click");
  assert.equal(popup.hidden, false);
  assert.equal(button.getAttribute("aria-expanded"), "true");
  button.dispatch("click");
  assert.equal(popup.hidden, true);

  // 다크 모드 행 클릭 → 테마 토글, 배지 갱신, 팝업은 열린 채 유지
  button.dispatch("click");
  themeItem.dispatch("click");
  assert.equal(app.dataset.theme, "dark");
  assert.equal(store.data["mainRecordingSettings.theme"], "dark");
  assert.equal(popup.hidden, false);
  assert.equal(themeItem.getAttribute("aria-checked"), "true");
  assert.equal(badge.textContent, "ON");

  // 다시 클릭하면 라이트로 복귀
  themeItem.dispatch("click");
  assert.equal(app.dataset.theme, "light");
  assert.equal(themeItem.getAttribute("aria-checked"), "false");
  assert.equal(badge.textContent, "OFF");

  // 다른 메뉴 항목은 팝업을 닫는다
  folderItem.dispatch("click");
  assert.equal(popup.hidden, true);
});

test("dev mode switch reveals dev-only blocks and restores them when off", () => {
  const devSwitch = fakeEl("button", { attrs: { id: "devModeSwitch" } });
  const devCard = fakeEl("div", { attrs: { "data-dev-only": "" }, hidden: true });
  const devAnchor = fakeEl("div", { attrs: { "data-dev-only": "" }, hidden: true });
  const root = fakeRoot(
    { "#devModeSwitch": devSwitch },
    { "[data-dev-only]": [devCard, devAnchor] }
  );

  bootstrapLowerBodyUi(root, { storage: fakeStorage() });
  assert.equal(devCard.hidden, true);

  devSwitch.dispatch("click");
  assert.equal(devCard.hidden, false);
  assert.equal(devAnchor.hidden, false);
  assert.equal(devSwitch.getAttribute("aria-checked"), "true");

  devSwitch.dispatch("click");
  assert.equal(devCard.hidden, true);
  assert.equal(devSwitch.getAttribute("aria-checked"), "false");
});

test("slider input updates the value box with configured decimals", () => {
  const val = fakeEl("span", { classes: ["lb-val"] });
  const slider = fakeEl("input", {
    classes: ["lb-slider"],
    attrs: { type: "range", min: "0", max: "1", step: "0.01" },
    dataset: { default: "0.45", decimals: "2" },
    value: "0.45"
  });
  const ctl = fakeEl("div", { classes: ["lb-param-ctl"], children: [slider, val] });
  const root = fakeRoot({}, { ".lb-slider": [slider] });

  bootstrapLowerBodyUi(root, { storage: fakeStorage() });

  slider.value = "0.9";
  slider.dispatch("input");
  assert.equal(val.textContent, "0.90");
});

test("card head collapses the card, seg buttons and reset do not collapse it", () => {
  const body = fakeEl("div", { classes: ["lb-card-body"] });
  const segButton = fakeEl("button", { attrs: { "data-val": "on" } });
  const seg = fakeEl("div", { classes: ["lb-seg"], children: [segButton] });
  const head = fakeEl("header", { classes: ["lb-card-head"], children: [seg] });
  const card = fakeEl("article", { classes: ["lb-card"], children: [head, body] });
  const root = fakeRoot({}, { ".lb-card": [card] });

  bootstrapLowerBodyUi(root, { storage: fakeStorage() });

  head.dispatch("click", { target: segButton });
  assert.equal(card.classList.contains("collapsed"), false);

  head.dispatch("click", { target: head });
  assert.equal(card.classList.contains("collapsed"), true);
});

test("card reset restores sliders and segments to their defaults", () => {
  const segOn = fakeEl("button", { attrs: { "data-val": "on", "aria-pressed": "true" } });
  const segOff = fakeEl("button", { attrs: { "data-val": "off", "aria-pressed": "false" } });
  const seg = fakeEl("div", { classes: ["lb-seg"], dataset: { default: "on" }, children: [segOn, segOff] });
  const val = fakeEl("span", { classes: ["lb-val"] });
  const slider = fakeEl("input", {
    classes: ["lb-slider"],
    dataset: { default: "0.45", decimals: "2" },
    value: "0.45"
  });
  const ctl = fakeEl("div", { classes: ["lb-param-ctl"], children: [slider, val] });
  const reset = fakeEl("button", { attrs: { "data-lb-reset": "" } });
  const card = fakeEl("article", { classes: ["lb-card"], children: [seg, ctl, reset] });
  const root = fakeRoot({}, { "[data-lb-reset]": [reset] });

  bootstrapLowerBodyUi(root, { storage: fakeStorage() });

  slider.value = "0.9";
  segOff.setAttribute("aria-pressed", "true");
  segOn.setAttribute("aria-pressed", "false");
  reset.dispatch("click", { target: reset, stopPropagation() {} });

  assert.equal(slider.value, "0.45");
  assert.equal(val.textContent, "0.45");
  assert.equal(segOn.getAttribute("aria-pressed"), "true");
  assert.equal(segOff.getAttribute("aria-pressed"), "false");
});

test("seg with data-controls toggles its dependent sub-block", () => {
  const sub = fakeEl("div", { attrs: { id: "subThumbSmartCurve" } });
  const off = fakeEl("button", { attrs: { "data-val": "off", "aria-pressed": "false" } });
  const on = fakeEl("button", { attrs: { "data-val": "on", "aria-pressed": "true" } });
  const seg = fakeEl("div", {
    classes: ["lb-seg"],
    dataset: { default: "on", controls: "subThumbSmartCurve" },
    children: [off, on]
  });
  const root = fakeRoot({ "#subThumbSmartCurve": sub }, { ".lb-seg": [seg] });

  bootstrapLowerBodyUi(root, { storage: fakeStorage() });
  assert.equal(sub.hidden, false);

  off.dispatch("click", { target: off });
  assert.equal(sub.hidden, true);

  on.dispatch("click", { target: on });
  assert.equal(sub.hidden, false);
});

test("hair backend segment shows the Boing fallback badge", () => {
  const badge = fakeEl("div", { attrs: { id: "boingFallbackBadge" }, hidden: true });
  const mc2 = fakeEl("button", { attrs: { "data-val": "mc2", "aria-pressed": "true" } });
  const boing = fakeEl("button", { attrs: { "data-val": "boing", "aria-pressed": "false" } });
  const seg = fakeEl("div", {
    classes: ["lb-seg"],
    attrs: { id: "hairBackendSeg" },
    dataset: { default: "mc2" },
    children: [mc2, boing]
  });
  const root = fakeRoot(
    { "#hairBackendSeg": seg, "#boingFallbackBadge": badge },
    { ".lb-seg": [seg] }
  );

  bootstrapLowerBodyUi(root, { storage: fakeStorage() });
  assert.equal(badge.hidden, true);

  boing.dispatch("click", { target: boing });
  assert.equal(badge.hidden, false);

  mc2.dispatch("click", { target: mc2 });
  assert.equal(badge.hidden, true);
});

test("settings menu item opens the modal and backdrop closes it", () => {
  const modal = fakeEl("div", { attrs: { id: "settingsModal" }, hidden: true });
  const openItem = fakeEl("button", { attrs: { id: "menuOpenSettings" } });
  const closeBtn = fakeEl("button", { attrs: { id: "settingsModalClose" } });
  const backdrop = fakeEl("div", { attrs: { id: "settingsModalBackdrop" } });
  const root = fakeRoot({
    "#menuOpenSettings": openItem,
    "#settingsModal": modal,
    "#settingsModalClose": closeBtn,
    "#settingsModalBackdrop": backdrop
  });

  bootstrapLowerBodyUi(root, { storage: fakeStorage() });

  openItem.dispatch("click");
  assert.equal(modal.hidden, false);

  closeBtn.dispatch("click");
  assert.equal(modal.hidden, true);

  openItem.dispatch("click");
  backdrop.dispatch("click");
  assert.equal(modal.hidden, true);
});

test("toc sublink click scrolls to its card and activates the group link", () => {
  const card = fakeEl("article", { attrs: { id: "lbGrounding" } });
  const groupLink = fakeEl("a", { classes: ["lb-toc-link"], attrs: { href: "#lbFootLock" } });
  const sub = fakeEl("a", { classes: ["lb-toc-sublink"], attrs: { href: "#lbGrounding" } });
  fakeEl("li", {
    classes: ["lb-toc-group"],
    children: [
      groupLink,
      fakeEl("ul", { classes: ["lb-toc-sub"], children: [fakeEl("li", { children: [sub] })] })
    ]
  });
  const root = fakeRoot(
    { "#lbGrounding": card },
    { ".lb-toc-link, .lb-toc-sublink": [groupLink, sub] }
  );

  bootstrapLowerBodyUi(root, { storage: fakeStorage() });
  sub.dispatch("click");

  assert.equal(card._scrolled, true);
  assert.equal(sub.classList.contains("active"), true);
  assert.equal(groupLink.classList.contains("active"), true);
});

/* ── 최소 DOM 대역: classList·dataset·querySelector·closest 만 지원한다 ── */

function fakeEl(tagName, { classes = [], attrs = {}, dataset = {}, children = [], value = "", hidden = false } = {}) {
  const node = {
    tagName: tagName.toUpperCase(),
    children: [],
    dataset: { ...dataset },
    hidden,
    value,
    textContent: "",
    parent: null,
    _handlers: new Map(),
    _attrs: new Map(Object.entries(attrs)),
    _classes: new Set(classes),
    classList: {
      add: (c) => node._classes.add(c),
      remove: (c) => node._classes.delete(c),
      toggle: (c, force) => {
        const on = force === undefined ? !node._classes.has(c) : force;
        on ? node._classes.add(c) : node._classes.delete(c);
        return on;
      },
      contains: (c) => node._classes.has(c)
    },
    style: { _props: {}, setProperty(k, v) { this._props[k] = v; } },
    addEventListener(type, handler) {
      const list = node._handlers.get(type) ?? [];
      list.push(handler);
      node._handlers.set(type, list);
    },
    dispatch(type, event = {}) {
      event.target ??= node;
      // 실제 DOM처럼 부모로 버블링한다.
      event.stopPropagation ??= () => { event._stopped = true; };
      for (let n = node; n && !event._stopped; n = n.parent) {
        for (const h of n._handlers.get(type) ?? []) h(event);
      }
    },
    setAttribute(name, v) {
      node._attrs.set(name, String(v));
      if (name.startsWith("data-")) node.dataset[dataKey(name.slice(5))] = String(v);
    },
    getAttribute(name) { return node._attrs.get(name); },
    querySelector(sel) { return node._find(sel)[0] ?? null; },
    querySelectorAll(sel) { return node._find(sel); },
    closest(sel) {
      let n = node;
      while (n) {
        if (matchSelector(n, sel)) return n;
        n = n.parent;
      }
      return null;
    },
    scrollIntoView() { node._scrolled = true; },
    getBoundingClientRect() { return { top: 0 }; },
    _find(sel) {
      const out = [];
      const visit = (n) => {
        for (const child of n.children) {
          if (matchSelector(child, sel)) out.push(child);
          visit(child);
        }
      };
      visit(node);
      return out;
    }
  };
  for (const child of children) {
    child.parent = node;
    node.children.push(child);
  }
  for (const [name, v] of Object.entries(attrs)) {
    if (name.startsWith("data-")) node.dataset[dataKey(name.slice(5))] = String(v);
  }
  return node;
}

// data-foo-bar → dataset.fooBar
function dataKey(name) {
  return name.replace(/-([a-z])/g, (_, c) => c.toUpperCase());
}

// 지원 선택자: ".class", "#id", "tag", "[attr]", "tag[attr]", 쉼표 다중 선택
function matchSelector(node, selector) {
  return selector.split(",").some((part) => matchSimple(node, part.trim()));
}

function matchSimple(node, sel) {
  const tagMatch = sel.match(/^[a-zA-Z][a-zA-Z0-9]*/);
  if (tagMatch && node.tagName !== tagMatch[0].toUpperCase()) return false;
  for (const cls of sel.match(/\.[a-zA-Z0-9_-]+/g) ?? []) {
    if (!node._classes.has(cls.slice(1))) return false;
  }
  for (const attr of sel.match(/\[[a-zA-Z0-9_-]+\]/g) ?? []) {
    if (!node._attrs.has(attr.slice(1, -1)) && !(attr.slice(1, -1) in node.dataset)) return false;
  }
  const idMatch = sel.match(/#[a-zA-Z0-9_-]+/);
  if (idMatch && node._attrs.get("id") !== idMatch[0].slice(1)) return false;
  return Boolean(tagMatch || sel.match(/[.#\[]/));
}

function fakeRoot(single = {}, multi = {}) {
  return {
    querySelector(sel) { return single[sel] ?? null; },
    querySelectorAll(sel) { return multi[sel] ?? []; }
  };
}

function fakeStorage() {
  return {
    data: {},
    getItem(k) { return this.data[k] ?? null; },
    setItem(k, v) { this.data[k] = String(v); }
  };
}
