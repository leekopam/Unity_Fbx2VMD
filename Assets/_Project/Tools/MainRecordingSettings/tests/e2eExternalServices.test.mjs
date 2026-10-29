import assert from "node:assert/strict";
import fs from "node:fs";
import { createRequire } from "node:module";
import path from "node:path";
import { pathToFileURL } from "node:url";
import test from "node:test";

import { disableE2eExternalServices } from "../scripts/disableE2eExternalServices.mjs";

const require = createRequire(import.meta.url);
const dist = path.dirname(require.resolve("e2e"));
const loadModule = (relativePath) => import(pathToFileURL(path.join(dist, relativePath)).href);
const removed = /외부 전송 기능을 제거/;

test("e2e 외부 전송 제거는 반복 실행해도 동일한 파일을 유지한다", () => {
  disableE2eExternalServices();
  assert.equal(disableE2eExternalServices(), 0);
});

test("텔레메트리와 직접 전송 함수는 fetch를 호출하지 않는다", async (context) => {
  let requests = 0;
  const fetch = () => { requests += 1; throw new Error("전송 금지"); };
  context.mock.method(globalThis, "fetch", fetch);
  const { Telemetry } = await loadModule("telemetry/telemetry.js");
  const telemetry = new Telemetry({ version: "0.21.2", env: {}, fetch });
  telemetry.session("run");
  telemetry.record({ event: "검증" });
  await telemetry.sendQueued();
  await telemetry.flush();
  assert.equal(telemetry.enabled, false);
  assert.throws(() => telemetry.setEnabled(true), removed);
  const { postBatch, POSTHOG_HOST, POSTHOG_PROJECT_KEY } = await loadModule("telemetry/posthog.js");
  assert.equal(await postBatch([], { fetch }), false);
  assert.equal(POSTHOG_HOST, "");
  assert.equal(POSTHOG_PROJECT_KEY, "");
  assert.equal(requests, 0);
});

test("피드백·인증·모델 진입점은 네트워크 호출 전에 거부한다", async (context) => {
  let requests = 0;
  context.mock.method(globalThis, "fetch", () => { requests += 1; throw new Error("전송 금지"); });
  const cases = {
    "cli/feedback.js": ["feedback"],
    "cli/init.js": ["init"],
    "oauth/cli.js": ["runLogin", "runModels"],
    "oauth/fetch.js": ["createOAuthFetch"],
    "oauth/providers.js": ["getProvider"],
    "oauth/providers/openai.js": ["createCodexProvider", "sendCodexRequest"],
    "oauth/providers/github-copilot.js": ["createCopilotProvider", "sendCopilotRequest"],
    "oauth/providers/opencode-console.js": ["createOpencodeConsoleProvider", "opencodeConsoleRouteFor"],
    "oauth/providers/xai.js": ["createXaiProvider"],
    "oauth/token-endpoint.js": ["postForm", "requestTokens"],
    "agent/ai-sdk.js": ["loadAiSdk", "asSdkLanguageModel"]
  };
  for (const [relativePath, names] of Object.entries(cases)) {
    const module = await loadModule(relativePath);
    for (const name of names) assert.throws(() => module[name](), removed);
  }
  assert.equal(requests, 0);
});

test("미검토 버전과 파일은 아무 파일도 쓰기 전에 거부한다", (context) => {
  const readFile = fs.readFileSync;
  let writes = 0;
  context.mock.method(fs, "writeFileSync", () => { writes += 1; });
  const reader = context.mock.method(fs, "readFileSync", (target, ...args) => {
    if (String(target) === path.join(dist, "..", "package.json")) return '{"version":"0.22.0"}';
    return readFile(target, ...args);
  });
  assert.throws(disableE2eExternalServices, /버전이 변경/);
  reader.mock.mockImplementation((target, ...args) => {
    if (String(target) === path.join(dist, "agent", "ai-sdk.js")) return "검토되지 않은 파일";
    return readFile(target, ...args);
  });
  assert.throws(disableE2eExternalServices, /검토되지 않은 e2e 파일/);
  assert.equal(writes, 0);
});

test("설치와 실행 경로 모두 외부 전송 제거를 적용한다", () => {
  const packageInfo = JSON.parse(fs.readFileSync(new URL("../package.json", import.meta.url), "utf8"));
  assert.equal(packageInfo.scripts.postinstall, "node scripts/disableE2eExternalServices.mjs");
  const launcher = fs.readFileSync(new URL("../scripts/runE2e.mjs", import.meta.url), "utf8");
  assert.match(launcher, /E2E_TELEMETRY_DISABLED = "1"/);
  assert.match(launcher, /disableE2eExternalServices\(\)/);
});
