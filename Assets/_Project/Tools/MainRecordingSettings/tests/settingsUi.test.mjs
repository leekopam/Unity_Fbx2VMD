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
