import assert from "node:assert/strict";
import test from "node:test";

import { bootstrapSettingsUi } from "../client/settingsUi.js";

test("FBX import primary button opens the file picker and submits the selected file", async () => {
  const elements = {
    ".app-shell": createFakeElement("div"),
    "#importButton": createFakeElement("button"),
    "#statusBadge": createFakeElement("div"),
    "#statusText": createFakeElement("span"),
    "#feedback": createFakeElement("p"),
    "#eventLog": createFakeElement("div")
  };
  const root = {
    querySelector(selector) {
      return elements[selector] ?? null;
    }
  };
  const beforeUnloadHandlers = [];
  const requests = [];
  const previousDocument = globalThis.document;
  const previousFetch = globalThis.fetch;
  const previousWindow = globalThis.window;

  globalThis.document = {
    createElement: () => createFakeElement("div")
  };
  globalThis.fetch = async (url, init = {}) => {
    if (init.method === "GET") {
      return {
        ok: true,
        status: 200,
        json: async () => ({ runtimeState: { playMode: "stopped", updatedAtUtc: "" } })
      };
    }

    requests.push({ url, init });
    return {
      ok: true,
      status: 200,
      json: async () => ({ accepted: true, commandId: "cmd-1" })
    };
  };
  globalThis.window = {
    mainRecordingSettingsBridgeConfig: {
      apiBaseUrl: "http://localhost:4100",
      wsUrl: "ws://localhost:4100/settings"
    },
    settingsShell: {
      chooseFbxFile: async () => "C:/Motion/sample.fbx"
    },
    addEventListener(type, handler) {
      if (type === "beforeunload") {
        beforeUnloadHandlers.push(handler);
      }
    }
  };

  try {
    bootstrapSettingsUi(root);

    assert.equal(elements["#importButton"].disabled, false);
    assert.equal(elements["#importButton"].textContent, "FBX 가져오기");

    await elements["#importButton"].dispatch("click");

    assert.equal(requests.length, 1);
    assert.equal(requests[0].url, "http://localhost:4100/import-fbx");
    assert.deepEqual(JSON.parse(requests[0].init.body), {
      fbxPath: "C:/Motion/sample.fbx"
    });
    assert.equal(elements["#feedback"].textContent, "FBX 가져오기 요청을 보냈습니다.");
    assert.equal(elements["#eventLog"].dataset.empty, "false");
  } finally {
    for (const handler of beforeUnloadHandlers) {
      handler();
    }

    globalThis.document = previousDocument;
    globalThis.fetch = previousFetch;
    globalThis.window = previousWindow;
  }
});

test("Camera 1 tree item switches to the camera settings panel", () => {
  const shell = createFakeElement("div");
  shell.dataset.activePanel = "onboarding";

  const onboardingButton = createFakeElement("button");
  onboardingButton.dataset.panelTarget = "onboarding";
  onboardingButton.className = "sidebar-heading active";
  onboardingButton.setAttribute("aria-current", "page");

  const cameraButton = createFakeElement("button");
  cameraButton.dataset.panelTarget = "camera";
  cameraButton.className = "tree-item";

  const onboardingPanel = createFakeElement("section");
  onboardingPanel.dataset.panelView = "onboarding";
  onboardingPanel.hidden = false;

  const cameraPanel = createFakeElement("section");
  cameraPanel.dataset.panelView = "camera";
  cameraPanel.hidden = true;

  const elements = {
    ".app-shell": shell,
    "#importButton": createFakeElement("button"),
    "#statusBadge": createFakeElement("div"),
    "#statusText": createFakeElement("span"),
    "#feedback": createFakeElement("p"),
    "#eventLog": createFakeElement("div")
  };
  const root = {
    querySelector(selector) {
      return elements[selector] ?? null;
    },
    querySelectorAll(selector) {
      if (selector === "[data-panel-target]") {
        return [onboardingButton, cameraButton];
      }

      if (selector === "[data-panel-view]") {
        return [onboardingPanel, cameraPanel];
      }

      return [];
    }
  };
  const beforeUnloadHandlers = [];
  const previousDocument = globalThis.document;
  const previousFetch = globalThis.fetch;
  const previousWindow = globalThis.window;

  globalThis.document = {
    createElement: () => createFakeElement("div")
  };
  globalThis.fetch = async () => ({
    ok: true,
    status: 200,
    json: async () => ({ runtimeState: { playMode: "stopped", updatedAtUtc: "" } })
  });
  globalThis.window = {
    addEventListener(type, handler) {
      if (type === "beforeunload") {
        beforeUnloadHandlers.push(handler);
      }
    }
  };

  try {
    bootstrapSettingsUi(root);
    cameraButton.dispatch("click");

    assert.equal(shell.dataset.activePanel, "camera");
    assert.equal(cameraButton.className, "tree-item selected");
    assert.equal(cameraButton.getAttribute("aria-current"), "page");
    assert.equal(onboardingButton.className, "sidebar-heading");
    assert.equal(onboardingButton.getAttribute("aria-current"), undefined);
    assert.equal(onboardingPanel.hidden, true);
    assert.equal(cameraPanel.hidden, false);
  } finally {
    for (const handler of beforeUnloadHandlers) {
      handler();
    }

    globalThis.document = previousDocument;
    globalThis.fetch = previousFetch;
    globalThis.window = previousWindow;
  }
});

test("개발자 패널을 열면 Workbench iframe을 탑재하고 실패 시 재시도 버튼을 연다", async () => {
  const shell = createFakeElement("div");
  shell.dataset.activePanel = "onboarding";

  const onboardingButton = createFakeElement("button");
  onboardingButton.dataset.panelTarget = "onboarding";
  const developerButton = createFakeElement("button");
  developerButton.dataset.panelTarget = "developer";

  const onboardingPanel = createFakeElement("section");
  onboardingPanel.dataset.panelView = "onboarding";
  const developerPanel = createFakeElement("section");
  developerPanel.dataset.panelView = "developer";
  developerPanel.hidden = true;

  const frame = createFakeElement("iframe");
  frame.hidden = true;
  const status = createFakeElement("p");
  const retry = createFakeElement("button");
  retry.hidden = true;

  const elements = {
    ".app-shell": shell,
    "#importButton": createFakeElement("button"),
    "#statusBadge": createFakeElement("div"),
    "#statusText": createFakeElement("span"),
    "#feedback": createFakeElement("p"),
    "#eventLog": createFakeElement("div"),
    "#boogleFrame": frame,
    "#workbenchStatus": status,
    "#workbenchRetry": retry
  };
  const root = {
    querySelector(selector) {
      return elements[selector] ?? null;
    },
    querySelectorAll(selector) {
      if (selector === "[data-panel-target]") {
        return [onboardingButton, developerButton];
      }
      if (selector === "[data-panel-view]") {
        return [onboardingPanel, developerPanel];
      }
      return [];
    }
  };
  const beforeUnloadHandlers = [];
  const urls = [];
  const previousDocument = globalThis.document;
  const previousFetch = globalThis.fetch;
  const previousWindow = globalThis.window;

  globalThis.document = {
    createElement: () => createFakeElement("div")
  };
  globalThis.fetch = async () => ({
    ok: true,
    status: 200,
    json: async () => ({ runtimeState: { playMode: "stopped", updatedAtUtc: "" } })
  });
  globalThis.window = {
    settingsShell: {
      getWorkbenchUrl: async () => {
        if (urls.length === 0) {
          urls.push("");
          return "";
        }
        urls.push("http://127.0.0.1:9001");
        return urls[urls.length - 1];
      }
    },
    addEventListener(type, handler) {
      if (type === "beforeunload") {
        beforeUnloadHandlers.push(handler);
      }
    }
  };

  try {
    bootstrapSettingsUi(root);
    developerButton.dispatch("click");
    await new Promise((resolve) => setTimeout(resolve, 0));

    assert.equal(developerPanel.hidden, false);
    assert.equal(urls.length, 1);
    assert.equal(status.dataset.tone, "error");
    assert.equal(retry.hidden, false);

    retry.dispatch("click");
    await new Promise((resolve) => setTimeout(resolve, 0));

    assert.equal(urls.length, 2);
    assert.equal(frame.src, "http://127.0.0.1:9001");
    assert.equal(frame.hidden, false);
    assert.equal(retry.hidden, true);
  } finally {
    for (const handler of beforeUnloadHandlers) {
      handler();
    }

    globalThis.document = previousDocument;
    globalThis.fetch = previousFetch;
    globalThis.window = previousWindow;
  }
});

test("Workbench iframe 로드가 시간 초과되면 오류와 재시도 경로를 연다", async () => {
  const fixture = setupWorkbenchDom({ urls: ["http://127.0.0.1:9001"] });
  try {
    bootstrapSettingsUi(fixture.root, { workbenchLoadTimeoutMs: 5 });
    fixture.developerButton.dispatch("click");
    await new Promise((resolve) => setTimeout(resolve, 30));

    assert.equal(fixture.frame.src, "http://127.0.0.1:9001");
    assert.equal(fixture.status.dataset.tone, "error");
    assert.match(fixture.status.textContent, /초과/);
    assert.equal(fixture.retry.hidden, false);
  } finally {
    fixture.restore();
  }
});

test("다시 불러오기 버튼은 Workbench URL을 다시 요청한다", async () => {
  const fixture = setupWorkbenchDom({ urls: ["http://127.0.0.1:9001", "http://127.0.0.1:9002"] });
  try {
    bootstrapSettingsUi(fixture.root);
    fixture.developerButton.dispatch("click");
    await new Promise((resolve) => setTimeout(resolve, 0));
    assert.equal(fixture.frame.src, "http://127.0.0.1:9001");
    assert.equal(fixture.calls.length, 1);

    fixture.reload.dispatch("click");
    await new Promise((resolve) => setTimeout(resolve, 0));

    assert.equal(fixture.calls.length, 2);
    assert.equal(fixture.frame.src, "http://127.0.0.1:9002");
    assert.equal(fixture.frame.hidden, false);
    assert.equal(fixture.status.dataset.tone, "info");
  } finally {
    fixture.restore();
  }
});

test("로딩 중 다시 불러오기를 눌러도 이전 요청의 늦은 응답이 상태를 덮지 않는다", async () => {
  const fixture = setupWorkbenchDom();
  const resolvers = [];
  try {
    bootstrapSettingsUi(fixture.root, { workbenchLoadTimeoutMs: 60 });
    // URL 요청을 수동으로 지연시켜 응답 대기 중 재시도 상황을 만든다.
    globalThis.window.settingsShell.getWorkbenchUrl = () =>
      new Promise((resolve) => resolvers.push(resolve));

    fixture.developerButton.dispatch("click");
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.reload.dispatch("click");
    await new Promise((resolve) => setTimeout(resolve, 0));
    assert.equal(resolvers.length, 2);
    assert.equal(fixture.frame.src, "about:blank");

    // 첫 요청의 늦은 응답은 무효화돼 iframe 주소와 상태를 덮지 않는다.
    resolvers[0]("http://127.0.0.1:9001");
    await new Promise((resolve) => setTimeout(resolve, 0));
    assert.equal(fixture.frame.src, "about:blank");
    assert.equal(fixture.status.dataset.tone, "info");
    assert.equal(fixture.status.hidden, false);
    assert.equal(fixture.retry.hidden, true);

    // 비운 iframe의 load 이벤트는 로딩 상태를 닫지 않는다.
    fixture.frame.dispatch("load");
    assert.equal(fixture.status.hidden, false);

    resolvers[1]("http://127.0.0.1:9002");
    await new Promise((resolve) => setTimeout(resolve, 0));
    assert.equal(fixture.frame.src, "http://127.0.0.1:9002");
    assert.equal(fixture.frame.hidden, false);

    // 기대한 주소의 load만 로딩 상태를 닫고, 이후에도 오류가 덧씌워지지 않는다.
    fixture.frame.dispatch("load");
    assert.equal(fixture.status.hidden, true);
    await new Promise((resolve) => setTimeout(resolve, 80));
    assert.equal(fixture.status.dataset.tone, "info");
    assert.equal(fixture.retry.hidden, true);
  } finally {
    fixture.restore();
  }
});

// 개발자 패널 테스트가 쓰는 최소 DOM·window 대역을 만든다.
function setupWorkbenchDom({ urls = [] } = {}) {
  const shell = createFakeElement("div");
  shell.dataset.activePanel = "onboarding";

  const onboardingButton = createFakeElement("button");
  onboardingButton.dataset.panelTarget = "onboarding";
  const developerButton = createFakeElement("button");
  developerButton.dataset.panelTarget = "developer";

  const onboardingPanel = createFakeElement("section");
  onboardingPanel.dataset.panelView = "onboarding";
  const developerPanel = createFakeElement("section");
  developerPanel.dataset.panelView = "developer";
  developerPanel.hidden = true;

  const frame = createFakeElement("iframe");
  frame.hidden = true;
  const status = createFakeElement("p");
  const retry = createFakeElement("button");
  retry.hidden = true;
  const reload = createFakeElement("button");

  const elements = {
    ".app-shell": shell,
    "#importButton": createFakeElement("button"),
    "#statusBadge": createFakeElement("div"),
    "#statusText": createFakeElement("span"),
    "#feedback": createFakeElement("p"),
    "#eventLog": createFakeElement("div"),
    "#boogleFrame": frame,
    "#workbenchStatus": status,
    "#workbenchRetry": retry,
    "#workbenchReload": reload
  };
  const root = {
    querySelector(selector) {
      return elements[selector] ?? null;
    },
    querySelectorAll(selector) {
      if (selector === "[data-panel-target]") {
        return [onboardingButton, developerButton];
      }
      if (selector === "[data-panel-view]") {
        return [onboardingPanel, developerPanel];
      }
      return [];
    }
  };
  const calls = [];
  const beforeUnloadHandlers = [];
  const previousDocument = globalThis.document;
  const previousFetch = globalThis.fetch;
  const previousWindow = globalThis.window;

  globalThis.document = {
    createElement: () => createFakeElement("div")
  };
  globalThis.fetch = async () => ({
    ok: true,
    status: 200,
    json: async () => ({ runtimeState: { playMode: "stopped", updatedAtUtc: "" } })
  });
  globalThis.window = {
    settingsShell: {
      getWorkbenchUrl: async () => {
        calls.push(true);
        return urls[Math.min(calls.length, urls.length) - 1] ?? "";
      }
    },
    addEventListener(type, handler) {
      if (type === "beforeunload") {
        beforeUnloadHandlers.push(handler);
      }
    }
  };

  return {
    root,
    frame,
    status,
    retry,
    reload,
    developerButton,
    developerPanel,
    calls,
    restore() {
      for (const handler of beforeUnloadHandlers) {
        handler();
      }

      globalThis.document = previousDocument;
      globalThis.fetch = previousFetch;
      globalThis.window = previousWindow;
    }
  };
}

function createFakeElement(tagName) {
  const handlers = new Map();
  const attributes = new Map();

  return {
    tagName,
    children: [],
    className: "",
    dataset: {},
    disabled: false,
    hidden: false,
    textContent: "",
    value: "",
    addEventListener(type, handler) {
      handlers.set(type, handler);
    },
    async dispatch(type) {
      const handler = handlers.get(type);
      if (typeof handler === "function") {
        await handler();
      }
    },
    prepend(child) {
      this.children.unshift(child);
      this.textContent = this.children.map((item) => item.textContent).join("\n");
    },
    setAttribute(name, value) {
      attributes.set(name, String(value));
    },
    getAttribute(name) {
      return attributes.get(name);
    },
    removeAttribute(name) {
      attributes.delete(name);
    }
  };
}
