import {
  DEFAULT_LIPSYNC_BASE_URL,
  getHealth,
  submitJob,
  getJob,
  getJobLog,
  cancelJob
} from "./lipsyncApi.js";

// 립싱크 패널과 로컬 서버(Tools/LipSync/server.py)를 연결한다.
// 서버가 작업을 소유하므로 창을 닫아도 작업은 계속되고,
// localStorage에 남긴 job_id로 재부착해 진행률·산출물만 회수한다.

const JOBS_STORAGE_KEY = "lipsync.activeJobs.v1";
const POLL_INTERVAL_MS = 1000;
const LOG_TAIL_LINES = 30;

const ENGINE_MAP = { audioseparator: "audio_separator", demucs: "demucs" };

export function bootstrapLipSyncPanel(root = document, deps = {}) {
  const {
    fetchImpl = globalThis.fetch,
    shell = globalThis.window?.settingsShell ?? {},
    storage = globalThis.localStorage,
    setTimeoutImpl = globalThis.setTimeout,
    clearTimeoutImpl = globalThis.clearTimeout,
    createAudio = (src) => new Audio(src)
  } = deps;

  const $ = (id) => root.getElementById ? root.getElementById(id) : root.querySelector(`#${id}`);
  const el = {
    progressText: $("lipProgressText"),
    progressSub: $("lipProgressSub"),
    progressBar: $("lipProgressBar"),
    sourcePath: $("lipSourcePath"),
    browseAudio: $("lipBrowseAudio"),
    playSource: $("lipPlaySource"),
    seekSlider: $("lipSeekSlider"),
    seekValue: $("lipSeekValue"),
    seekTime: $("lipSeekTime"),
    loopSeg: $("lipLoopSeg"),
    outputDevice: $("lipOutputDevice"),
    outputDir: $("lipOutputDir"),
    browseDir: $("lipBrowseDir"),
    engineSeg: $("lipEngineSeg"),
    model: $("lipModel"),
    ensemblePreset: $("lipEnsemblePreset"),
    cleanVocalSeg: $("lipCleanVocalSeg"),
    wpeSeg: $("lipWpeSeg"),
    highQualitySeg: $("lipHighQualitySeg"),
    outputFiles: $("lipOutputFiles"),
    phonemeSeg: $("lipPhonemeSeg"),
    phonemeLang: $("lipPhonemeLang"),
    lyricsPath: $("lipLyricsPath"),
    browseLyrics: $("lipBrowseLyrics"),
    deviceSeg: $("lipDeviceSeg"),
    vadSeg: $("lipVadSeg"),
    phonemeCsvName: $("lipPhonemeCsvName"),
    lyricsTitle: $("lipLyricsTitle"),
    lyricsArtist: $("lipLyricsArtist"),
    lyricsSearchInline: $("lipLyricsSearchInline"),
    lyricsCard: $("lipLyricsCard"),
    lyricsCardTitle: $("lipLyricsCardTitle"),
    lyricsCardMeta: $("lipLyricsCardMeta"),
    lyricsPreview: $("lipLyricsPreview"),
    lyricsApply: $("lipLyricsApply"),
    lyricsIgnore: $("lipLyricsIgnore"),
    endpoint: $("lipEndpoint"),
    pingStatus: $("lipPingStatus"),
    venvPath: $("lipVenvPath"),
    openApi: $("lipOpenApi"),
    serverLog: $("lipServerLog"),
    separateBtn: $("lipSeparate"),
    phonemeBtn: $("lipPhonemeRun"),
    lyricsSearchBtn: $("lipLyricsSearch"),
    cancelBtn: $("lipCancelJob"),
    restartBtn: $("lipRestartServer"),
    pingBtn: $("lipPing")
  };

  let projectRoot = "";
  let serverOnline = false;
  let activeJobId = null;
  let activeKind = null;
  let pollTimer = null;
  let previewAudio = null;
  let outputs = { vocal: "", cleanVocal: "", instrumental: "", csv: "" };

  function baseUrl() {
    return (el.endpoint?.value || "").trim() || DEFAULT_LIPSYNC_BASE_URL;
  }

  // 상대 경로(Assets/...)는 서버가 프로젝트 루트 기준으로만 해석하므로
  // health에서 받은 루트와 결합해 절대 경로로 보낸다.
  function resolvePath(value) {
    const p = (value || "").trim();
    if (!p) return "";
    if (/^[a-zA-Z]:[\\/]/.test(p) || p.startsWith("\\\\") || p.startsWith("/")) {
      return p;
    }
    if (!projectRoot) return p;
    return `${projectRoot.replace(/[\\/]+$/, "")}/${p.replace(/\\/g, "/")}`;
  }

  function setStatus(main, sub = "", progress = null) {
    if (el.progressText) el.progressText.textContent = `진행 상태: ${main}`;
    if (el.progressSub) el.progressSub.textContent = sub;
    if (el.progressBar && progress != null) el.progressBar.value = progress;
  }

  function appendLog(text) {
    if (!el.serverLog || !text) return;
    const current = el.serverLog.textContent;
    el.serverLog.textContent = current ? `${current}\n${text}` : text;
    el.serverLog.scrollTop = el.serverLog.scrollHeight;
  }

  function segValue(seg) {
    return seg?.querySelector?.('button[data-val][aria-pressed="true"]')?.dataset?.val ?? "";
  }

  function setBusy(busy) {
    for (const btn of [el.separateBtn, el.phonemeBtn, el.lyricsSearchBtn, el.lyricsSearchInline]) {
      if (btn) btn.disabled = busy || !serverOnline;
    }
    if (el.cancelBtn) el.cancelBtn.disabled = !busy;
  }

  function setOffline(online) {
    serverOnline = online;
    if (el.pingStatus) {
      el.pingStatus.textContent = online ? "Status: 200 OK" : "Status: 연결 실패";
      el.pingStatus.className = online ? "lb-chip-ok" : "lb-chip";
    }
    setBusy(activeJobId != null);
    if (!online) {
      setStatus("서버 연결 안 됨, 작업을 제출할 수 없습니다", "Ping으로 상태를 확인하거나 서버를 기동하세요", null);
    }
  }

  // ---------- 작업 추적(localStorage 재부착) ----------

  function loadTracked() {
    try {
      return JSON.parse(storage?.getItem?.(JOBS_STORAGE_KEY) || "{}") || {};
    } catch {
      return {};
    }
  }

  function trackJob(kind, jobId) {
    const map = loadTracked();
    map[kind] = jobId;
    try { storage?.setItem?.(JOBS_STORAGE_KEY, JSON.stringify(map)); } catch { /* 무시 */ }
  }

  function untrackJob(kind) {
    const map = loadTracked();
    delete map[kind];
    try { storage?.setItem?.(JOBS_STORAGE_KEY, JSON.stringify(map)); } catch { /* 무시 */ }
  }

  // ---------- 작업 폴링 ----------

  async function pollOnce() {
    if (!activeJobId) return;
    let job;
    try {
      job = await getJob({ apiBaseUrl: baseUrl(), jobId: activeJobId, fetchImpl });
    } catch (error) {
      if (error?.status === 404) {
        setStatus("작업을 찾을 수 없습니다, 서버가 재기동됐을 수 있습니다", "", null);
        finishJob();
      }
      return;
    }

    const pct = Math.round((job.progress ?? 0) * 100);
    const kindLabel = { separate: "보컬/BGM 분리", clean: "보컬 정제", phoneme: "음소 추출", lyrics: "가사 검색" }[job.kind] ?? job.kind;
    if (job.status === "queued" || job.status === "running") {
      setStatus(`${kindLabel} ${job.stage ? `· ${job.stage}` : ""} ${pct}%`, `작업 ID: ${job.job_id}`, job.progress ?? 0);
      refreshLogTail();
      return;
    }

    finishJob();
    untrackJob(job.kind);
    if (job.status === "done") {
      adoptOutputs(job);
      if (job.kind === "separate" && segValue(el.cleanVocalSeg) === "on") {
        void runCleanChain(job.outputs?.vocal);
        return;
      }
      if (job.kind === "lyrics") {
        void showLyricsCandidate(job.outputs);
      }
      setStatus(`${kindLabel} 완료`, "산출물이 출력 폴더에 저장됐습니다", 1);
      return;
    }
    if (job.status === "cancelled") {
      setStatus(`${kindLabel} 취소됨`, "", null);
      return;
    }
    setStatus(`${kindLabel} 실패, ${job.error || "알 수 없는 오류"}`, "다시 시도하거나 로그를 확인하세요", null);
  }

  function startPolling(jobId, kind) {
    activeJobId = jobId;
    activeKind = kind;
    setBusy(true);
    if (pollTimer != null) clearTimeoutImpl(pollTimer);
    const loop = () => {
      if (!activeJobId) return;
      void pollOnce().finally(() => {
        if (activeJobId) pollTimer = setTimeoutImpl(loop, POLL_INTERVAL_MS);
      });
    };
    void loop();
  }

  function finishJob() {
    activeJobId = null;
    activeKind = null;
    if (pollTimer != null) {
      clearTimeoutImpl(pollTimer);
      pollTimer = null;
    }
    setBusy(false);
  }

  async function refreshLogTail() {
    if (!el.serverLog || !activeJobId) return;
    try {
      const log = await getJobLog({ apiBaseUrl: baseUrl(), jobId: activeJobId, tail: LOG_TAIL_LINES, fetchImpl });
      if (Array.isArray(log?.lines)) {
        el.serverLog.textContent = log.lines.join("\n");
        el.serverLog.scrollTop = el.serverLog.scrollHeight;
      }
    } catch { /* 로그 실패는 무시, 상태 폴링이 계속된다 */ }
  }

  // ---------- 산출물 반영 ----------

  function adoptOutputs(job) {
    const out = job.outputs || {};
    if (job.kind === "separate") {
      outputs.vocal = out.vocal || "";
      outputs.instrumental = out.instrumental || "";
      outputs.cleanVocal = "";
    }
    if (job.kind === "clean") {
      outputs.cleanVocal = out.vocal || "";
    }
    if (job.kind === "phoneme") {
      outputs.csv = out.csv || "";
      if (el.phonemeCsvName && out.csv) {
        el.phonemeCsvName.textContent = fileName(out.csv);
      }
    }
    renderOutputFiles();
  }

  function renderOutputFiles() {
    if (!el.outputFiles) return;
    const rows = [
      { chip: "원시 보컬", path: outputs.vocal, selected: !outputs.cleanVocal && !!outputs.vocal },
      { chip: "정제 보컬 (Clean)", path: outputs.cleanVocal, selected: !!outputs.cleanVocal },
      { chip: "MR / 반주음", path: outputs.instrumental, selected: false }
    ].filter((r) => r.path);

    el.outputFiles.textContent = "";
    if (rows.length === 0) {
      const empty = root.createElement("div");
      empty.className = "lb-file-row";
      empty.innerHTML = '<span class="lb-chip">산출물 없음</span><span class="lb-file-name">분리 작업을 실행하면 여기에 표시됩니다</span>';
      el.outputFiles.appendChild(empty);
      return;
    }
    for (const row of rows) {
      const div = root.createElement("div");
      div.className = "lb-file-row";
      const chip = root.createElement("span");
      chip.className = row.selected ? "lb-chip-ok" : "lb-chip";
      chip.textContent = row.chip;
      const name = root.createElement("span");
      name.className = "lb-file-name";
      name.textContent = fileName(row.path);
      name.title = row.path;
      div.appendChild(chip);
      div.appendChild(name);
      if (row.selected) {
        const sel = root.createElement("span");
        sel.className = "lb-chip-ok";
        sel.textContent = "선택됨";
        div.appendChild(sel);
      }
      const play = root.createElement("button");
      play.className = "lb-btn lb-btn-mini";
      play.type = "button";
      play.textContent = "미리듣기";
      play.addEventListener("click", () => playFile(row.path));
      div.appendChild(play);
      el.outputFiles.appendChild(div);
    }
  }

  // ---------- 작업 제출 ----------

  async function runSeparate() {
    const input = resolvePath(el.sourcePath?.value);
    const outputDir = resolvePath(el.outputDir?.value);
    if (!input) { setStatus("원본 음원 경로를 입력하세요", "", null); return; }
    const engine = ENGINE_MAP[segValue(el.engineSeg)] ?? "audio_separator";
    try {
      const result = await submitJob({
        apiBaseUrl: baseUrl(), fetchImpl,
        kind: "separate",
        key: input,
        params: {
          engine,
          input,
          output_dir: outputDir,
          model: el.model?.value || "",
          ensemble_preset: el.ensemblePreset?.value || "",
          model_dir: projectRoot ? `${projectRoot.replace(/[\\/]+$/, "")}/Tools/LipSync/.models` : "",
          high_quality: segValue(el.highQualitySeg) === "on"
        }
      });
      trackJob("separate", result.job_id);
      setStatus("분리 작업 제출됨", result.reused ? "이미 실행 중인 작업에 재부착했습니다" : "", 0);
      startPolling(result.job_id, "separate");
    } catch (error) {
      setStatus(`분리 작업 제출 실패, ${error.message}`, "", null);
    }
  }

  async function runCleanChain(vocalPath) {
    if (!vocalPath) return;
    const outputDir = resolvePath(el.outputDir?.value);
    try {
      const result = await submitJob({
        apiBaseUrl: baseUrl(), fetchImpl,
        kind: "clean",
        key: vocalPath,
        params: {
          input: vocalPath,
          output_dir: outputDir,
          wpe: segValue(el.wpeSeg) === "on",
          original_mix: resolvePath(el.sourcePath?.value),
          model_dir: projectRoot ? `${projectRoot.replace(/[\\/]+$/, "")}/Tools/LipSync/.models` : ""
        }
      });
      trackJob("clean", result.job_id);
      setStatus("보컬 정제 체인 시작", "", 0);
      startPolling(result.job_id, "clean");
    } catch (error) {
      setStatus(`정제 작업 제출 실패, ${error.message}`, "", null);
    }
  }

  async function runPhoneme() {
    const input = outputs.cleanVocal || outputs.vocal || resolvePath(el.sourcePath?.value);
    if (!input) { setStatus("음소 추출 입력이 없습니다, 음원 경로를 입력하세요", "", null); return; }
    const outputDir = resolvePath(el.outputDir?.value);
    const base = fileStem(input);
    try {
      const result = await submitJob({
        apiBaseUrl: baseUrl(), fetchImpl,
        kind: "phoneme",
        key: `${input}|${base}`,
        params: {
          input,
          output: `${outputDir}/${base}_phonemes.csv`,
          model: el.phonemeLang?.value || "",
          device: segValue(el.deviceSeg) || "cuda",
          lyrics: resolvePath(el.lyricsPath?.value) || undefined,
          no_vad: segValue(el.vadSeg) === "off"
        }
      });
      trackJob("phoneme", result.job_id);
      setStatus("음소 추출 작업 제출됨", result.reused ? "이미 실행 중인 작업에 재부착했습니다" : "", 0);
      startPolling(result.job_id, "phoneme");
    } catch (error) {
      setStatus(`음소 추출 제출 실패, ${error.message}`, "", null);
    }
  }

  async function runLyrics() {
    const audio = resolvePath(el.sourcePath?.value);
    if (!audio) { setStatus("가사 검색 대상 음원 경로를 입력하세요", "", null); return; }
    const outputDir = resolvePath(el.outputDir?.value);
    const base = fileStem(audio);
    const out = `${outputDir}/${base}_lyrics`;
    try {
      const result = await submitJob({
        apiBaseUrl: baseUrl(), fetchImpl,
        kind: "lyrics",
        key: audio,
        params: {
          audio,
          out,
          title: el.lyricsTitle?.value?.trim() || "",
          artist: el.lyricsArtist?.value?.trim() || "",
          json: `${out}.json`
        }
      });
      trackJob("lyrics", result.job_id);
      setStatus("가사 검색 작업 제출됨", result.reused ? "이미 실행 중인 작업에 재부착했습니다" : "", 0);
      startPolling(result.job_id, "lyrics");
    } catch (error) {
      setStatus(`가사 검색 제출 실패, ${error.message}`, "", null);
    }
  }

  async function showLyricsCandidate(out) {
    if (!el.lyricsCard) return;
    let title = "가사 후보";
    let meta = "";
    if (typeof shell.readTextPreview === "function" && out?.json) {
      try {
        const text = await shell.readTextPreview(out.json, 4000);
        const info = JSON.parse(text);
        title = info?.title ? `${info.title}${info.artist ? ` - ${info.artist}` : ""}` : title;
        const score = info?.score != null ? `매칭 점수: ${info.score}` : "";
        meta = [score, info?.source ? `출처: ${info.source}` : ""].filter(Boolean).join(" · ");
      } catch { /* json이 없거나 형식이 다르면 경로만 표시 */ }
    }
    if (el.lyricsCardTitle) el.lyricsCardTitle.textContent = title;
    if (el.lyricsCardMeta) el.lyricsCardMeta.textContent = meta || fileName(out?.txt || "");
    el.lyricsCard.hidden = false;
    if (typeof shell.readTextPreview === "function" && out?.txt && el.lyricsPreview) {
      try {
        el.lyricsPreview.textContent = await shell.readTextPreview(out.txt, 2000);
      } catch {
        el.lyricsPreview.textContent = `가사 파일: ${out.txt}`;
      }
    }
    if (el.lyricsApply && out?.txt) {
      el.lyricsApply.onclick = () => {
        if (el.lyricsPath) el.lyricsPath.value = out.txt;
        el.lyricsCard.hidden = true;
        setStatus("가사를 적용했습니다, 음소 추출에 사용됩니다", fileName(out.txt), null);
      };
    }
    if (el.lyricsIgnore) {
      el.lyricsIgnore.onclick = () => { el.lyricsCard.hidden = true; };
    }
  }

  // ---------- 미리듣기 ----------

  function toFileUrl(path) {
    // encodeURI는 #·?를 그대로 둬 파일명이 URL을 깨뜨린다 — 세그먼트별로 인코딩한다.
    // 드라이브 문자(D:)는 %3A로 바뀌면 Chromium이 못 읽으므로 인코딩 대상에서 뺀다.
    const segments = String(path || "").replace(/\\/g, "/").split("/").filter(Boolean);
    const drive = /^[a-zA-Z]:$/.test(segments[0] || "") ? segments.shift() : null;
    const encoded = segments.map(encodeURIComponent).join("/");
    return `file:///${drive ? `${drive}/` : ""}${encoded}`;
  }

  async function playFile(path) {
    if (!path) return;
    try {
      if (previewAudio) { previewAudio.pause(); }
      previewAudio = createAudio(toFileUrl(resolvePath(path)));
      previewAudio.loop = segValue(el.loopSeg) === "on";
      const deviceId = el.outputDevice?.value;
      if (deviceId && typeof previewAudio.setSinkId === "function") {
        try { await previewAudio.setSinkId(deviceId); } catch { /* 장치 지정 실패 시 기본 장치 */ }
      }
      previewAudio.ontimeupdate = updateSeekUi;
      previewAudio.onloadedmetadata = updateSeekUi;
      previewAudio.onended = () => setStatus("미리듣기 종료", "", null);
      await previewAudio.play();
      setStatus(`미리듣기: ${fileName(path)}`, "", null);
    } catch {
      setStatus("미리듣기 실패, 파일 경로와 형식을 확인하세요", "", null);
    }
  }

  function updateSeekUi() {
    if (!previewAudio || !el.seekSlider) return;
    const duration = Number.isFinite(previewAudio.duration) ? previewAudio.duration : 0;
    el.seekSlider.max = Math.max(1, Math.floor(duration));
    el.seekSlider.value = Math.floor(previewAudio.currentTime || 0);
    if (el.seekValue) el.seekValue.textContent = String(Math.floor(previewAudio.currentTime || 0));
    if (el.seekTime) {
      el.seekTime.textContent = `${formatTime(previewAudio.currentTime)} / ${formatTime(duration)}`;
    }
  }

  async function populateOutputDevices() {
    if (!el.outputDevice || !globalThis.navigator?.mediaDevices?.enumerateDevices) return;
    try {
      const devices = await globalThis.navigator.mediaDevices.enumerateDevices();
      const outputsList = devices.filter((d) => d.kind === "audiooutput");
      if (outputsList.length === 0) return;
      el.outputDevice.textContent = "";
      const def = root.createElement("option");
      def.value = "";
      def.textContent = "기본 출력 장치";
      el.outputDevice.appendChild(def);
      for (const d of outputsList) {
        const opt = root.createElement("option");
        opt.value = d.deviceId;
        opt.textContent = d.label || `장치 ${d.deviceId.slice(0, 6)}`;
        el.outputDevice.appendChild(opt);
      }
    } catch { /* 장치 열거 실패 시 기본 옵션 유지 */ }
  }

  // ---------- 이벤트 배선 ----------

  async function ping() {
    try {
      const health = await getHealth({ apiBaseUrl: baseUrl(), fetchImpl });
      projectRoot = health?.project || projectRoot;
      if (el.venvPath && health?.python) el.venvPath.value = health.python;
      setOffline(true);
      setStatus("서버 연결됨", `프로젝트: ${projectRoot || "알 수 없음"}`, null);
      await resumeTrackedJobs();
      return true;
    } catch {
      setOffline(false);
      return false;
    }
  }

  // localStorage에 남은 job_id를 확인해 아직 실행 중이면 폴링을 재개한다.
  async function resumeTrackedJobs() {
    if (activeJobId) return;
    const tracked = loadTracked();
    for (const [kind, jobId] of Object.entries(tracked)) {
      try {
        const job = await getJob({ apiBaseUrl: baseUrl(), jobId, fetchImpl });
        if (job.status === "queued" || job.status === "running") {
          setStatus(`실행 중인 ${kind} 작업에 재부착했습니다`, `작업 ID: ${jobId}`, job.progress ?? 0);
          startPolling(jobId, kind);
          return;
        }
        if (job.status === "done") {
          adoptOutputs(job);
        }
        untrackJob(kind);
      } catch {
        untrackJob(kind);
      }
    }
  }

  el.pingBtn?.addEventListener("click", () => void ping());
  el.separateBtn?.addEventListener("click", () => { if (!activeJobId) void runSeparate(); });
  el.phonemeBtn?.addEventListener("click", () => { if (!activeJobId) void runPhoneme(); });
  el.lyricsSearchBtn?.addEventListener("click", () => { if (!activeJobId) void runLyrics(); });
  el.lyricsSearchInline?.addEventListener("click", () => { if (!activeJobId) void runLyrics(); });
  el.cancelBtn?.addEventListener("click", async () => {
    if (!activeJobId) return;
    try {
      await cancelJob({ apiBaseUrl: baseUrl(), jobId: activeJobId, fetchImpl });
      setStatus("취소 요청을 보냈습니다", "", null);
    } catch (error) {
      setStatus(`취소 실패, ${error.message}`, "", null);
    }
  });
  el.restartBtn?.addEventListener("click", () => {
    setStatus(
      "서버 재기동은 수동으로 진행하세요",
      "Tools/LipSync/start-server.ps1 실행 후 Ping으로 확인",
      null
    );
  });
  el.openApi?.addEventListener("click", async () => {
    try {
      const spec = await fetchJsonOrThrow(`${baseUrl()}/openapi.json`, fetchImpl);
      const count = Object.keys(spec?.paths ?? {}).length;
      appendLog(`[openapi] endpoint ${count}개, ${Object.keys(spec?.paths ?? {}).join(", ")}`);
      setStatus(`openapi 스키마 조회 완료, endpoint ${count}개`, "", null);
    } catch (error) {
      setStatus(`openapi 조회 실패, ${error.message}`, "", null);
    }
  });

  el.browseAudio?.addEventListener("click", async () => {
    if (typeof shell.chooseAudioFile !== "function") return;
    const picked = await shell.chooseAudioFile();
    if (picked && el.sourcePath) el.sourcePath.value = picked;
  });
  el.browseDir?.addEventListener("click", async () => {
    if (typeof shell.chooseDirectory !== "function") return;
    const picked = await shell.chooseDirectory();
    if (picked && el.outputDir) el.outputDir.value = picked;
  });
  el.browseLyrics?.addEventListener("click", async () => {
    if (typeof shell.chooseLyricsFile !== "function") return;
    const picked = await shell.chooseLyricsFile();
    if (picked && el.lyricsPath) el.lyricsPath.value = picked;
  });
  el.playSource?.addEventListener("click", () => void playFile(el.sourcePath?.value));
  el.seekSlider?.addEventListener("input", () => {
    if (previewAudio && Number.isFinite(previewAudio.duration)) {
      previewAudio.currentTime = Number(el.seekSlider.value);
      updateSeekUi();
    }
  });
  el.loopSeg?.addEventListener("click", () => {
    if (previewAudio) previewAudio.loop = segValue(el.loopSeg) === "on";
  });

  if (el.lyricsCard) el.lyricsCard.hidden = true;
  renderOutputFiles();
  void populateOutputDevices();
  void ping();

  return {
    ping,
    get activeJobId() { return activeJobId; },
    isServerOnline: () => serverOnline
  };
}

// ---------- 유틸 ----------

function fileName(path) {
  return String(path || "").split(/[\\/]/).pop() || "";
}

function fileStem(path) {
  const name = fileName(path);
  const dot = name.lastIndexOf(".");
  return (dot > 0 ? name.slice(0, dot) : name) || "output";
}

function formatTime(seconds) {
  const s = Math.max(0, Math.floor(seconds || 0));
  return `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
}

async function fetchJsonOrThrow(url, fetchImpl) {
  const response = await fetchImpl(url, { method: "GET" });
  if (!response.ok) {
    throw new Error(`HTTP 오류 ${response.status}`);
  }
  return typeof response.json === "function" ? await response.json() : {};
}

if (typeof document !== "undefined") {
  bootstrapLipSyncPanel(document);
}
