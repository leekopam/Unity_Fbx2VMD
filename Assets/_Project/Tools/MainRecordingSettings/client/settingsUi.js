import {
  DEFAULT_API_BASE_URL,
  DEFAULT_WS_URL,
  createPlayModeAutoConnectionController,
  getRuntimeState,
  postImportFbx
} from "./settingsApi.js";

const statusLabels = {
  idle: "연결 안 됨",
  connecting: "연결 중",
  open: "연결됨",
  closed: "연결 안 됨",
  error: "연결 오류"
};

export function bootstrapSettingsUi(root = document, { workbenchLoadTimeoutMs = 15000 } = {}) {
  const elements = {
    shell: root.querySelector(".app-shell"),
    importButton: root.querySelector("#importButton"),
    statusBadge: root.querySelector("#statusBadge"),
    statusText: root.querySelector("#statusText"),
    feedback: root.querySelector("#feedback"),
    log: root.querySelector("#eventLog"),
    workbenchFrame: root.querySelector("#boogleFrame"),
    workbenchStatus: root.querySelector("#workbenchStatus"),
    workbenchRetry: root.querySelector("#workbenchRetry"),
    workbenchReload: root.querySelector("#workbenchReload")
  };
  let workbenchRequested = false;
  let workbenchLoadTimer = null;
  elements.workbenchRetry?.addEventListener("click", reloadWorkbench);
  elements.workbenchReload?.addEventListener("click", reloadWorkbench);
  elements.workbenchFrame?.addEventListener("load", () => {
    clearTimeout(workbenchLoadTimer);
    workbenchLoadTimer = null;
    if (elements.workbenchStatus) {
      elements.workbenchStatus.textContent = "";
      elements.workbenchStatus.hidden = true;
    }
  });
  const panelTargets = Array.from(root.querySelectorAll?.("[data-panel-target]") ?? []);
  const panelViews = Array.from(root.querySelectorAll?.("[data-panel-view]") ?? []);

  let isSubmitting = false;
  const autoConnection = createPlayModeAutoConnectionController({
    apiBaseUrl: () => getBridgeConfig().apiBaseUrl ?? DEFAULT_API_BASE_URL,
    wsUrl: () => getBridgeConfig().wsUrl ?? DEFAULT_WS_URL,
    fetchRuntimeState: () => getRuntimeState({
      apiBaseUrl: getBridgeConfig().apiBaseUrl ?? DEFAULT_API_BASE_URL
    }),
    onStatusChange: setConnectionStatus,
    onMessage: (message) => appendLog("수신", message),
    onError: () => setFeedback("Unity Play 상태 확인 또는 WebSocket 연결에 실패했습니다.", "error")
  });

  setConnectionStatus("idle");
  setFeedback("가져오기 버튼을 누르면 FBX 파일 선택 창이 열립니다.", "info");
  renderLogEmptyState();
  initializePanelSwitching();
  refreshControls();
  autoConnection.start();
  globalThis.window?.addEventListener?.("beforeunload", () => autoConnection.stop());

  elements.importButton.addEventListener("click", async () => {
    if (isSubmitting) {
      return;
    }

    const chooser = globalThis.window?.settingsShell?.chooseFbxFile;
    if (typeof chooser !== "function") {
      setFeedback("현재 환경에서는 파일 선택 창을 열 수 없습니다.", "error");
      return;
    }

    isSubmitting = true;
    setFeedback("FBX 파일 선택 창을 여는 중입니다.", "loading");
    refreshControls();

    try {
      const selectedPath = await chooser();
      if (!selectedPath) {
        setFeedback("파일 선택을 취소했습니다.", "info");
        return;
      }

      setFeedback("FBX 가져오기 요청을 보내는 중입니다.", "loading");
      const result = await postImportFbx({
        apiBaseUrl: getBridgeConfig().apiBaseUrl ?? DEFAULT_API_BASE_URL,
        fbxPath: selectedPath
      });
      appendLog("HTTP 응답", result);
      setFeedback("FBX 가져오기 요청을 보냈습니다.", "success");
    } catch (error) {
      setFeedback(error.message, "error");
    } finally {
      isSubmitting = false;
      refreshControls();
    }
  });

  function initializePanelSwitching() {
    if (!elements.shell || panelTargets.length === 0 || panelViews.length === 0) {
      return;
    }

    for (const target of panelTargets) {
      target.addEventListener("click", () => {
        selectPanel(target.dataset.panelTarget);
      });
    }

    selectPanel(elements.shell.dataset.activePanel || "onboarding");
  }

  function selectPanel(panelName) {
    if (!panelName) {
      return;
    }

    elements.shell.dataset.activePanel = panelName;
    for (const target of panelTargets) {
      const isActive = target.dataset.panelTarget === panelName;
      setPanelTargetState(target, isActive);
    }

    for (const view of panelViews) {
      view.hidden = view.dataset.panelView !== panelName;
    }

    if (panelName === "developer") {
      void ensureWorkbenchLoaded();
    }
  }

  // 개발자 모드 패널이 처음 열릴 때만 Workbench URL을 가져와 iframe에 탑재한다.
  // 실패하면 다시 시도 버튼을 열어 재기동할 수 있게 한다.
  async function ensureWorkbenchLoaded() {
    if (workbenchRequested || !elements.workbenchFrame || !elements.workbenchStatus) {
      return;
    }
    workbenchRequested = true;
    elements.workbenchStatus.dataset.tone = "info";
    elements.workbenchStatus.hidden = false;
    elements.workbenchStatus.textContent = "Workbench를 시작하는 중입니다.";
    if (elements.workbenchRetry) {
      elements.workbenchRetry.hidden = true;
    }

    const getUrl = globalThis.window?.settingsShell?.getWorkbenchUrl;
    if (typeof getUrl !== "function") {
      showWorkbenchError("현재 환경에서는 Workbench를 사용할 수 없습니다.");
      return;
    }

    try {
      const url = await getUrl();
      if (!url) {
        showWorkbenchError("Workbench 서버를 시작하지 못했습니다. boogle-sdk 설치를 확인하세요.");
        return;
      }
      elements.workbenchFrame.src = url;
      elements.workbenchFrame.hidden = false;
      // iframe load가 끝나지 않으면 오류 상태로 전환해 다시 시도 경로를 연다.
      workbenchLoadTimer = setTimeout(() => {
        workbenchLoadTimer = null;
        showWorkbenchError("Workbench 화면 로드 시간이 초과됐습니다.");
      }, workbenchLoadTimeoutMs);
      workbenchLoadTimer.unref?.();
    } catch {
      showWorkbenchError("Workbench 주소를 가져오지 못했습니다.");
    }
  }

  // 다시 시도·다시 불러오기는 요청 상태를 초기화하고 iframe을 비운 뒤 URL부터 다시 받는다.
  function reloadWorkbench() {
    clearTimeout(workbenchLoadTimer);
    workbenchLoadTimer = null;
    workbenchRequested = false;
    if (elements.workbenchFrame) {
      elements.workbenchFrame.hidden = true;
      elements.workbenchFrame.src = "about:blank";
    }
    void ensureWorkbenchLoaded();
  }

  function showWorkbenchError(message) {
    elements.workbenchStatus.dataset.tone = "error";
    elements.workbenchStatus.textContent = message;
    if (elements.workbenchRetry) {
      elements.workbenchRetry.hidden = false;
    }
  }

  function setPanelTargetState(target, isActive) {
    const activeClass = target.className.includes("tree-item") ? "selected" : "active";
    target.className = toggleClassName(target.className, activeClass, isActive);
    if (isActive) {
      target.setAttribute("aria-current", "page");
      return;
    }

    target.removeAttribute("aria-current");
  }

  function refreshControls() {
    elements.importButton.disabled = isSubmitting;
    elements.importButton.textContent = isSubmitting ? "가져오는 중..." : "FBX 가져오기";
  }

  function setConnectionStatus(status) {
    elements.statusBadge.dataset.status = status;
    elements.statusText.textContent = statusLabels[status] ?? status;
  }

  function setFeedback(message, tone) {
    elements.feedback.dataset.tone = tone;
    elements.feedback.textContent = message;
  }

  function appendLog(label, payload) {
    if (elements.log.dataset.empty === "true") {
      elements.log.textContent = "";
      elements.log.dataset.empty = "false";
    }

    const line = document.createElement("div");
    line.className = "log-line";
    line.textContent = `[${new Date().toLocaleTimeString()}] ${label}: ${formatPayload(payload)}`;
    elements.log.prepend(line);
  }

  function renderLogEmptyState() {
    elements.log.dataset.empty = "true";
    elements.log.textContent = "아직 수신한 메시지가 없습니다.";
  }
}

function toggleClassName(className, classToken, enabled) {
  const tokens = new Set(String(className || "").split(/\s+/).filter(Boolean));
  if (enabled) {
    tokens.add(classToken);
  } else {
    tokens.delete(classToken);
  }

  return Array.from(tokens).join(" ");
}

function formatPayload(payload) {
  if (typeof payload === "string") {
    return payload;
  }

  return JSON.stringify(payload);
}

function getBridgeConfig() {
  const config = globalThis.window?.mainRecordingSettingsBridgeConfig;
  return config && typeof config === "object" ? config : {};
}

if (typeof document !== "undefined") {
  bootstrapSettingsUi(document);
}
