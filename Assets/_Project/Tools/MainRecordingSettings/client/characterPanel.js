import {
  CHARACTER_ACTIONS,
  getCharacters,
  getCommandResult,
  sendCharacterCommand
} from "./characterApi.js";
import { DEFAULT_API_BASE_URL } from "./settingsApi.js";

// 캐릭터 라이브러리 패널 ↔ 브리지 배선.
// 읽기는 /characters 스냅샷, 쓰기·씬 조작은 /character-command로 Unity에 위임한다.
// Unity가 lastCommandResult를 기록할 때까지 폴링해 결과 카드에 반영한다.

const RESULT_POLL_INTERVAL_MS = 800;
const LIBRARY_REFRESH_MS = 2500;
const RESULT_POLL_TIMEOUT_MS = 120000;

export function bootstrapCharacterPanel(root = document, deps = {}) {
  const {
    fetchImpl = globalThis.fetch,
    shell = globalThis.window?.settingsShell ?? {},
    // did-finish-load 주입보다 bootstrap이 먼저 실행될 수 있으므로
    // apiBaseUrl도 요청 시점에 지연 해석한다(토큰 resolveBridgeToken과 동일 패턴).
    apiBaseUrl = () => globalThis.window?.mainRecordingSettingsBridgeConfig?.apiBaseUrl
      || DEFAULT_API_BASE_URL,
    setTimeoutImpl = globalThis.setTimeout,
    clearTimeoutImpl = globalThis.clearTimeout,
    setIntervalImpl = globalThis.setInterval,
    clearIntervalImpl = globalThis.clearInterval,
    confirmImpl = (message) => globalThis.window?.confirm?.(message) ?? true
  } = deps;
  const apiBase = () => typeof apiBaseUrl === "function" ? apiBaseUrl() : apiBaseUrl;

  const $ = (id) => root.getElementById ? root.getElementById(id) : root.querySelector(`#${id}`);
  const el = {
    addVrm: $("libAddVrm"),
    refresh: $("libRefresh"),
    openFolder: $("libOpenFolder"),
    search: $("libSearch"),
    sort: $("libSort"),
    count: $("libCount"),
    pipelineText: $("libPipelineText"),
    pipelineChip: $("libPipelineChip"),
    cancelLoad: $("libCancelLoad"),
    listBody: $("libListBody"),
    presetTarget: $("libPresetTarget"),
    presetName: $("libPresetName"),
    presetFields: $("libPresetFields"),
    presetReset: $("libPresetReset"),
    presetCopy: $("libPresetCopy"),
    presetCommit: $("libPresetCommit"),
    restoreRun: $("libRestoreRun"),
    resultText: $("libCmdResultText"),
    resultChip: $("libCmdResultChip")
  };

  let library = { characters: [], activeCharacterId: "", libraryDirectory: "" };
  let pendingCommandId = "";
  let pendingActionLabel = "";
  let resultTimer = null;
  let resultDeadline = 0;
  let editingPreset = null; // {characterId, presetId}

  // ---------- 표시 ----------

  function setResult(state, text) {
    if (el.resultText) el.resultText.textContent = text;
    if (el.resultChip) {
      el.resultChip.textContent = state === "success" ? "Done"
        : state === "error" ? "Error"
        : state === "running" ? "Running"
        : "Idle";
      el.resultChip.className = state === "error" ? "lb-chip-err"
        : state ? "lb-chip-ok" : "lb-chip";
    }
  }

  function setPipeline() {
    const active = library.characters.find((c) => c.id === library.activeCharacterId);
    if (el.pipelineText) {
      el.pipelineText.textContent = active
        ? `현재 활성: ${active.displayName}`
        : "씬에 라이브러리 캐릭터가 없습니다";
    }
    if (el.pipelineChip) {
      el.pipelineChip.textContent = active ? "Loaded" : "Idle";
      el.pipelineChip.className = active ? "lb-chip-ok" : "lb-chip";
    }
  }

  function statusChip(status) {
    if (status === "ready") return { cls: "lb-chip-ok", label: "Ready" };
    if (status === "missing") return { cls: "lb-chip-err", label: "MISSING" };
    if (status === "loading") return { cls: "lb-chip", label: "로드 중" };
    return { cls: "lb-chip", label: status || "알 수 없음" };
  }

  function renderList() {
    if (!el.listBody) return;
    const keyword = (el.search?.value || "").trim().toLowerCase();
    const sortMode = el.sort?.value || "recent";
    let rows = [...library.characters];
    if (keyword) {
      rows = rows.filter((c) =>
        c.displayName.toLowerCase().includes(keyword) ||
        c.meta.author.toLowerCase().includes(keyword));
    }
    if (sortMode === "name") {
      rows.sort((a, b) => a.displayName.localeCompare(b.displayName));
    } else if (sortMode === "registered") {
      rows.sort((a, b) => a.registeredAtUtc.localeCompare(b.registeredAtUtc));
    } else {
      rows.sort((a, b) => b.lastUsedAtUtc.localeCompare(a.lastUsedAtUtc));
    }

    if (el.count) {
      el.count.textContent = `${library.characters.length}개 항목 등록됨`;
    }

    el.listBody.textContent = "";
    if (rows.length === 0) {
      const empty = root.createElement("p");
      empty.className = "lb-hint";
      empty.textContent = library.characters.length === 0
        ? "등록된 캐릭터가 없습니다. VRM 파일 추가로 라이브러리를 시작하세요."
        : "검색 조건에 맞는 캐릭터가 없습니다.";
      el.listBody.appendChild(empty);
      return;
    }

    for (const character of rows) {
      el.listBody.appendChild(renderCharacterRow(character));
      const divider = root.createElement("hr");
      divider.className = "lb-param-divider";
      el.listBody.appendChild(divider);
    }
    el.listBody.lastElementChild?.remove();
  }

  function renderCharacterRow(character) {
    const wrap = root.createElement("div");
    const isActive = character.id === library.activeCharacterId;
    const missing = character.status === "missing";

    const row = root.createElement("div");
    row.className = "char-row";
    const thumb = root.createElement("div");
    thumb.className = "char-thumb";
    thumb.setAttribute("aria-hidden", "true");
    if (character.thumbnailPath) {
      const img = root.createElement("img");
      img.src = `file:///${character.thumbnailPath.replace(/\\/g, "/")}`;
      img.alt = "";
      img.onerror = () => { img.remove(); };
      thumb.appendChild(img);
    }
    row.appendChild(thumb);

    const info = root.createElement("div");
    info.className = "char-info";
    const badges = root.createElement("div");
    badges.className = "char-badges";
    for (const chip of [
      { cls: "lb-chip", label: `VRM ${character.vrmSpecVersion || "?"}` },
      statusChip(character.status),
      isActive ? { cls: "lb-chip-ok", label: "활성 (Active)" } : null,
      character.humanoidValid ? { cls: "lb-chip-ok", label: "Humanoid" } : null
    ].filter(Boolean)) {
      const span = root.createElement("span");
      span.className = chip.cls;
      span.textContent = chip.label;
      badges.appendChild(span);
    }
    info.appendChild(badges);
    const name = root.createElement("strong");
    name.className = "char-name";
    name.textContent = character.displayName || character.id;
    info.appendChild(name);
    const metaLine = root.createElement("span");
    metaLine.className = "char-meta";
    metaLine.textContent = missing
      ? `원래 경로를 찾을 수 없습니다: ${character.sourcePath}`
      : [
          character.meta.author ? `제작: ${character.meta.author}` : "",
          character.fileSize ? `파일: ${(character.fileSize / 1048576).toFixed(1)} MB` : "",
          character.sourcePath
        ].filter(Boolean).join(" · ");
    info.appendChild(metaLine);
    row.appendChild(info);

    const actions = root.createElement("div");
    actions.className = "char-actions";
    if (missing) {
      actions.appendChild(miniButton("다시 연결 (Relink)", async () => {
        const picked = await pickVrm();
        if (picked) send(CHARACTER_ACTIONS.reconnectCharacter, { characterId: character.id, path: picked });
      }));
    } else if (!isActive) {
      actions.appendChild(miniButton("불러오기 (Load)", () =>
        send(CHARACTER_ACTIONS.loadCharacter, { characterId: character.id })));
    }
    actions.appendChild(miniButton("제거", () => {
      if (confirmImpl(`'${character.displayName}'을(를) 라이브러리에서 제거하시겠습니까? 파일은 삭제되지 않습니다.`)) {
        send(CHARACTER_ACTIONS.removeCharacter, { characterId: character.id });
      }
    }));
    row.appendChild(actions);
    wrap.appendChild(row);

    if (!missing) {
      wrap.appendChild(renderPresetRow(character));
    }
    return wrap;
  }

  function renderPresetRow(character) {
    const wrap = root.createElement("div");
    const presetLine = root.createElement("div");
    presetLine.className = "char-preset";
    const label = root.createElement("span");
    label.className = "lb-param-name";
    label.textContent = "프리셋";
    presetLine.appendChild(label);

    const select = root.createElement("select");
    select.className = "lb-select";
    select.setAttribute("aria-label", `${character.displayName} 프리셋`);
    if (character.presets.length === 0) {
      const opt = root.createElement("option");
      opt.textContent = "프리셋 없음";
      opt.value = "";
      select.appendChild(opt);
      select.disabled = true;
    } else {
      for (const preset of character.presets) {
        const opt = root.createElement("option");
        opt.value = preset.id;
        opt.textContent = `${preset.isDefault ? "◆ " : ""}${preset.name}`;
        if (preset.isDefault) opt.selected = true;
        select.appendChild(opt);
      }
    }
    presetLine.appendChild(select);
    wrap.appendChild(presetLine);

    const actions = root.createElement("div");
    actions.className = "char-actions char-preset-actions";
    const presetId = () => select.value;
    const hasPresets = character.presets.length > 0;
    const apply = miniButton("적용", () =>
      send(CHARACTER_ACTIONS.applyPreset, { characterId: character.id, presetId: presetId() }));
    const makeDefault = miniButton("기본 지정", () =>
      send(CHARACTER_ACTIONS.setDefaultPreset, { characterId: character.id, presetId: presetId() }));
    const rename = miniButton("이름 변경", () => {
      // Electron은 window.prompt를 지원하지 않으므로 프리셋 편집기의 이름 입력을 사용한다.
      openPresetEditor(character, presetId());
      el.presetName?.focus();
      setResult("", "이름을 수정한 뒤 '편집 내용 저장'을 누르세요.");
    });
    const edit = miniButton("파라미터 편집", () => openPresetEditor(character, presetId()));
    const remove = miniButton("삭제", () => {
      const presetName = selectedPresetName(character, presetId());
      if (confirmImpl(`프리셋 '${presetName}'을(를) 삭제하시겠습니까?`)) {
        send(CHARACTER_ACTIONS.deletePreset, { characterId: character.id, presetId: presetId() });
      }
    });
    for (const btn of [apply, makeDefault, rename, edit, remove]) {
      btn.disabled = !hasPresets;
      actions.appendChild(btn);
    }
    actions.appendChild(miniButton("현재 상태 저장", () => {
      const next = el.presetName?.value?.trim();
      send(CHARACTER_ACTIONS.savePreset, {
        characterId: character.id,
        params: next ? { presetName: next } : {}
      });
    }));
    wrap.appendChild(actions);
    return wrap;
  }

  function selectedPresetName(character, presetId) {
    return character.presets.find((p) => p.id === presetId)?.name || "";
  }

  function miniButton(label, onClick) {
    const button = root.createElement("button");
    button.className = "lb-btn lb-btn-mini";
    button.type = "button";
    button.textContent = label;
    button.addEventListener("click", () => void onClick());
    return button;
  }

  // ---------- 프리셋 편집기 ----------

  function openPresetEditor(character, presetId) {
    const preset = character.presets.find((p) => p.id === presetId);
    if (!preset || !el.presetFields) return;
    editingPreset = { characterId: character.id, presetId: preset.id };
    if (el.presetTarget) {
      el.presetTarget.textContent = `선택 대상: ${character.displayName} · ${preset.name}`;
    }
    if (el.presetName) el.presetName.value = preset.name;
    renderPresetFields(preset);
  }

  // 프리셋 섹션은 key-type-value 쌍 목록 — 편집기는 전체 필드를 그대로 보내고
  // Unity 측 병합(UpdatePreset)이 실제 변경분만 집계한다.
  function renderPresetFields(preset) {
    el.presetFields.textContent = "";
    const sections = preset.sections || {};
    const groups = [
      ["retarget", "리타깃/배치 파라미터"],
      ["placement", "기본 배치 트랜스폼"],
      ["physics", "VRM 스프링 본 물리"],
      ["appearance", "외형"]
    ];
    let rendered = 0;
    for (const [key, title] of groups) {
      const fields = Array.isArray(sections[key]) ? sections[key] : [];
      if (fields.length === 0) continue;
      const desc = root.createElement("p");
      desc.className = "lb-desc";
      desc.textContent = title;
      el.presetFields.appendChild(desc);
      for (const field of fields) {
        el.presetFields.appendChild(renderFieldRow(key, field));
        rendered++;
      }
    }
    if (rendered === 0) {
      const empty = root.createElement("p");
      empty.className = "lb-hint";
      empty.textContent = "편집 가능한 파라미터가 없습니다. 현재 상태 저장으로 캡처하면 채워집니다.";
      el.presetFields.appendChild(empty);
    }
  }

  function renderFieldRow(section, field) {
    const row = root.createElement("div");
    row.className = "lb-param";
    const labelWrap = root.createElement("div");
    labelWrap.className = "lb-param-label";
    const name = root.createElement("span");
    name.className = "lb-param-name lb-sub";
    name.textContent = field.key;
    labelWrap.appendChild(name);
    row.appendChild(labelWrap);

    const ctl = root.createElement("div");
    ctl.className = "lb-param-ctl";
    const type = String(field.type || "string");
    if (type === "bool") {
      const input = root.createElement("input");
      input.type = "checkbox";
      input.checked = field.value === "true" || field.value === "1";
      ctl.appendChild(input);
    } else if (type === "vector3" || type === "quaternion") {
      const parts = String(field.value || "").split(",").map((v) => v.trim());
      const vec = root.createElement("div");
      vec.className = "lb-vec";
      const size = type === "vector3" ? 3 : 4;
      const axes = type === "vector3" ? ["X", "Y", "Z"] : ["X", "Y", "Z", "W"];
      for (let i = 0; i < size; i++) {
        const lbl = root.createElement("label");
        lbl.textContent = axes[i];
        const input = root.createElement("input");
        input.className = "lb-num";
        input.type = "number";
        input.step = "0.01";
        input.value = parts[i] ?? "0";
        lbl.appendChild(input);
        vec.appendChild(lbl);
      }
      ctl.appendChild(vec);
    } else {
      const input = root.createElement("input");
      input.className = "lb-num";
      const numeric = type === "int" || type === "float";
      input.type = numeric ? "number" : "text";
      if (numeric) input.step = type === "int" ? "1" : "0.01";
      input.value = field.value ?? "";
      ctl.appendChild(input);
    }

    row.dataset.section = section;
    row.dataset.key = field.key;
    row.dataset.type = type;
    row.appendChild(ctl);
    return row;
  }

  function collectEditedSections() {
    const sections = {};
    for (const row of el.presetFields?.querySelectorAll?.(".lb-param[data-key]") ?? []) {
      const type = row.dataset.type;
      let value = "";
      if (type === "bool") {
        value = row.querySelector("input")?.checked ? "true" : "false";
      } else if (type === "vector3" || type === "quaternion") {
        value = [...row.querySelectorAll("input")].map((i) => i.value || "0").join(",");
      } else {
        value = row.querySelector("input")?.value ?? "";
      }
      (sections[row.dataset.section] ??= []).push(
        { key: row.dataset.key, type, value });
    }
    return sections;
  }

  // ---------- 명령 발행·결과 회수 ----------

  async function send(action, { characterId = "", presetId = "", path = "", params = null } = {}) {
    try {
      const response = await sendCharacterCommand({
        apiBaseUrl: apiBase(), fetchImpl, action, characterId, presetId, path, params
      });
      pendingCommandId = response.commandId || "";
      pendingActionLabel = action;
      setResult("running", `${action} 요청을 Unity에 전달했습니다 (commandId: ${pendingCommandId})`);
      startResultPolling();
    } catch (error) {
      setResult("error", `${action} 요청 실패: ${error.message}`);
    }
  }

  function startResultPolling() {
    if (resultTimer != null) clearTimeoutImpl(resultTimer);
    resultDeadline = Date.now() + RESULT_POLL_TIMEOUT_MS;
    const loop = async () => {
      if (!pendingCommandId) return;
      try {
        const result = await getCommandResult({ apiBaseUrl: apiBase(), fetchImpl });
        if (result?.commandId === pendingCommandId && result.state && result.state !== "running") {
          const done = result.state === "success";
          setResult(result.state, `${pendingActionLabel}: ${result.message || (done ? "완료" : "실패")}`);
          // 저장·갱신 성공 후에는 이름 입력을 비워 다른 캐릭터에 잔여 이름이 섞이지 않게 한다.
          if (done && (pendingActionLabel === CHARACTER_ACTIONS.savePreset ||
            pendingActionLabel === CHARACTER_ACTIONS.updatePreset)) {
            if (el.presetName) el.presetName.value = "";
            editingPreset = null;
          }
          pendingCommandId = "";
          void refreshLibrary();
          return;
        }
      } catch { /* 브리지 일시 실패 — 다음 폴링에서 재시도 */ }

      if (Date.now() > resultDeadline) {
        setResult("error", `${pendingActionLabel}: 결과 대기 시간을 초과했습니다. Unity 에디터 상태를 확인하세요.`);
        pendingCommandId = "";
        return;
      }
      resultTimer = setTimeoutImpl(loop, RESULT_POLL_INTERVAL_MS);
    };
    resultTimer = setTimeoutImpl(loop, RESULT_POLL_INTERVAL_MS);
  }

  async function refreshLibrary() {
    try {
      library = await getCharacters({ apiBaseUrl: apiBase(), fetchImpl });
      setPipeline();
      renderList();
    } catch (error) {
      setResult("error", `라이브러리 조회 실패: ${error.message}`);
    }
  }

  async function pickVrm() {
    if (typeof shell.chooseVrmFile !== "function") {
      setResult("error", "파일 선택기를 사용할 수 없습니다.");
      return "";
    }
    return (await shell.chooseVrmFile()) || "";
  }

  // ---------- 이벤트 배선 ----------

  el.addVrm?.addEventListener("click", async () => {
    const picked = await pickVrm();
    if (picked) send(CHARACTER_ACTIONS.registerVrm, { path: picked });
  });
  el.refresh?.addEventListener("click", () => void refreshLibrary());
  el.openFolder?.addEventListener("click", async () => {
    if (typeof shell.openPath === "function" && library.libraryDirectory) {
      await shell.openPath(library.libraryDirectory);
    }
  });
  el.cancelLoad?.addEventListener("click", () => send(CHARACTER_ACTIONS.cancelLoad, {}));
  el.restoreRun?.addEventListener("click", () => {
    if (confirmImpl("씬 캐릭터를 복원하시겠습니까? 현재 라이브러리 캐릭터는 언로드됩니다.")) {
      send(CHARACTER_ACTIONS.restoreSceneCharacter, {});
    }
  });
  el.search?.addEventListener("input", renderList);
  el.sort?.addEventListener("change", renderList);

  el.presetCommit?.addEventListener("click", () => {
    if (!editingPreset) {
      setResult("error", "편집할 프리셋을 먼저 선택하세요.");
      return;
    }
    const params = { sections: collectEditedSections() };
    const nameValue = el.presetName?.value?.trim();
    if (nameValue) params.presetName = nameValue;
    send(CHARACTER_ACTIONS.updatePreset, { ...editingPreset, params });
  });
  el.presetCopy?.addEventListener("click", () => {
    if (!editingPreset) {
      setResult("error", "복제할 프리셋을 먼저 선택하세요.");
      return;
    }
    send(CHARACTER_ACTIONS.copyPreset, editingPreset);
  });
  el.presetReset?.addEventListener("click", () => {
    if (!editingPreset) return;
    const character = library.characters.find((c) => c.id === editingPreset.characterId);
    const preset = character?.presets.find((p) => p.id === editingPreset.presetId);
    if (preset) renderPresetFields(preset);
  });

  void refreshLibrary();
  const refreshTimer = typeof setIntervalImpl === "function"
    ? setIntervalImpl(() => {
      if (!pendingCommandId) void refreshLibrary();
    }, LIBRARY_REFRESH_MS)
    : null;

  return {
    refresh: refreshLibrary,
    dispose() {
      if (resultTimer != null) clearTimeoutImpl(resultTimer);
      if (refreshTimer != null && typeof clearIntervalImpl === "function") {
        clearIntervalImpl(refreshTimer);
      }
    }
  };
}

if (typeof document !== "undefined") {
  const boot = () => {
    const panel = document.querySelector('[data-panel-view="library"]');
    if (panel) bootstrapCharacterPanel(document);
  };
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
}
