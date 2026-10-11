import assert from "node:assert/strict";
import test from "node:test";

import { bootstrapLipSyncPanel } from "../client/lipsyncPanel.js";

// 분리 제출이 앙상블 프리셋을 서버 params로 보내는지 검증한다.
// Unity 창(_ensemblePreset 기본 vocal_clean)과의 정합이 리뷰 차단 항목이었다.

test("분리 제출이 선택된 앙상블 프리셋을 params.ensemble_preset으로 보낸다", async () => {
  const requests = [];
  const elements = {
    lipSeparate: fakeElement("button"),
    lipSourcePath: fakeElement("input"),
    lipOutputDir: fakeElement("input"),
    lipModel: fakeElement("select"),
    lipEnsemblePreset: fakeElement("select"),
    lipEngineSeg: segWith("audioseparator"),
    lipHighQualitySeg: segWith("off"),
    lipCleanVocalSeg: segWith("off"),
    lipEndpoint: fakeElement("input")
  };
  elements.lipSourcePath.value = "D:/song/input.wav";
  elements.lipOutputDir.value = "D:/song/out";
  elements.lipEnsemblePreset.value = "vocal_clean";

  bootstrapPanel(elements, requests);
  await elements.lipSeparate.dispatch("click");
  await flushAsync();

  const submit = requests.find((r) => String(r.url).endsWith("/jobs"));
  assert.ok(submit, "분리 작업이 /jobs로 제출돼야 함");
  const params = JSON.parse(submit.init.body).params;
  assert.equal(params.engine, "audio_separator");
  assert.equal(params.ensemble_preset, "vocal_clean");
});

test("프리셋을 비우면 빈 문자열로 보내 단일 모델 경로를 쓴다", async () => {
  const requests = [];
  const elements = {
    lipSeparate: fakeElement("button"),
    lipSourcePath: fakeElement("input"),
    lipOutputDir: fakeElement("input"),
    lipModel: fakeElement("select"),
    lipEnsemblePreset: fakeElement("select"),
    lipEngineSeg: segWith("audioseparator"),
    lipHighQualitySeg: segWith("off"),
    lipCleanVocalSeg: segWith("off"),
    lipEndpoint: fakeElement("input")
  };
  elements.lipSourcePath.value = "D:/song/input.wav";
  elements.lipOutputDir.value = "D:/song/out";
  elements.lipModel.value = "model_bs_roformer_ep_317_sdr_12.9755.ckpt";
  elements.lipEnsemblePreset.value = "";

  bootstrapPanel(elements, requests);
  await elements.lipSeparate.dispatch("click");
  await flushAsync();

  const submit = requests.find((r) => String(r.url).endsWith("/jobs"));
  assert.ok(submit, "분리 작업이 /jobs로 제출돼야 함");
  const params = JSON.parse(submit.init.body).params;
  assert.equal(params.ensemble_preset, "");
  assert.equal(params.model, "model_bs_roformer_ep_317_sdr_12.9755.ckpt");
});

test("미리듣기 URL이 파일명의 #·? 등을 세그먼트별로 인코딩한다", async () => {
  const played = [];
  const elements = {
    lipPlaySource: fakeElement("button"),
    lipSourcePath: fakeElement("input"),
    lipLoopSeg: segWith("off")
  };
  elements.lipSourcePath.value = "D:/song/곡 #1?.wav";

  bootstrapPanel(elements, [], {
    createAudio: (src) => {
      played.push(src);
      return {
        loop: false,
        duration: 0,
        currentTime: 0,
        pause() {},
        play() { return Promise.resolve(); }
      };
    }
  });
  await elements.lipPlaySource.dispatch("click");
  await flushAsync();

  assert.equal(played.length, 1);
  // 드라이브 문자는 보존하고 파일명의 특수문자만 인코딩한다.
  assert.match(played[0], /^file:\/\/\/D:\/song\//);
  assert.ok(played[0].includes("%23"), "#이 인코딩돼야 함");
  assert.ok(played[0].includes("%3F"), "?가 인코딩돼야 함");
  assert.ok(!played[0].includes("#"), "원본 #이 URL에 남으면 안 됨");
});

// 클릭 핸들러가 void runSeparate()로 비동기 체인을 시작하므로 몇 틱 기다린다.
async function flushAsync() {
  for (let i = 0; i < 5; i++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
}

function bootstrapPanel(elements, requests, extraDeps = {}) {
  const root = {
    getElementById: (id) => elements[id] ?? null,
    createElement: () => fakeElement("div")
  };
  const fetchImpl = async (url, init = {}) => {
    if (String(url).includes("/health")) {
      return ok({ project: "D:/project", python: "python" });
    }
    requests.push({ url, init });
    if (init.method === "POST") {
      return ok({ job_id: "job-1" });
    }
    // 폴링 응답 — 완료된 작업으로 돌려 바로 종료한다
    return ok({ status: "done", kind: "separate", job_id: "job-1", outputs: {} });
  };
  const storage = new Map();
  bootstrapLipSyncPanel(root, {
    fetchImpl,
    ...extraDeps,
    storage: {
      getItem: (k) => storage.get(k) ?? null,
      setItem: (k, v) => storage.set(k, v),
      removeItem: (k) => storage.delete(k)
    },
    setTimeoutImpl: () => 1,
    clearTimeoutImpl: () => {}
  });
}

function ok(body) {
  return {
    ok: true,
    status: 200,
    json: async () => body,
    text: async () => JSON.stringify(body)
  };
}

function segWith(val) {
  return {
    querySelector: () => ({ dataset: { val } }),
    addEventListener() {}
  };
}

function fakeElement(tagName) {
  const handlers = new Map();
  return {
    tagName,
    children: [],
    className: "",
    dataset: {},
    disabled: false,
    hidden: false,
    textContent: "",
    value: "",
    title: "",
    addEventListener(type, handler) {
      handlers.set(type, handler);
    },
    async dispatch(type) {
      const handler = handlers.get(type);
      if (typeof handler === "function") {
        await handler();
      }
    },
    appendChild(child) {
      this.children.push(child);
    },
    querySelector() {
      return null;
    }
  };
}
