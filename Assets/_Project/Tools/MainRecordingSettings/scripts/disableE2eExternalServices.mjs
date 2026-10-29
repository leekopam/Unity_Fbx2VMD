import { createHash } from "node:crypto";
import fs from "node:fs";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

const require = createRequire(import.meta.url);
const disabledMessage = "이 프로젝트에서는 e2e 외부 전송 기능을 제거했습니다. Jev는 별도 실행 경로를 사용하세요.";
const header = "// FBX2VMD: 외부 전송 구현을 제거한 로컬 전용 모듈임.\n";
const rejectFunctions = (names) => names.map((name) =>
  `export function ${name}() { throw new Error(${JSON.stringify(disabledMessage)}); }`
).join("\n");

// 고정 버전의 검토된 원본만 교체하여 업데이트 때 새 전송 경로를 놓치지 않도록 함.
const patches = [
  ["telemetry/telemetry.js", "348fdddfd4257f81cef9718792ecc369e29134205a37f70824b2e3383c15421e", `
export const NOTICE_VERSION = 3;
export class Telemetry {
  enabled = false;
  disabledBy = "E2E_TELEMETRY_DISABLED";
  debug = false;
  distinctId = undefined;
  sessionId = "";
  preferencesPath = "";
  setEnabled(value) { if (value) throw new Error(${JSON.stringify(disabledMessage)}); }
  notice() {}
  session() {}
  discardSession() {}
  failSession() {}
  endSession() {}
  record() {}
  async flush() {}
  async sendQueued() {}
}`],
  ["telemetry/posthog.js", "d6a3ab612f30ea457f407531250b29322960e22707ad7eef4f601ce0bdf4d76f", `
export const POSTHOG_HOST = "";
export const POSTHOG_PROJECT_KEY = "";
export async function postBatch() { return false; }`],
  ["cli/feedback.js", "30e6d68694ffc99183e5c3c30148106c24b3c9c5419c3ec0500029543f1e31bd", `
export const FEEDBACK_TYPES = ["bug", "docs", "feature", "other"];
export const FEEDBACK_LIMITS = { message: 2000, task: 1500, expected: 1500, actual: 1500, approach: 1500, repro: 1500, command: 200, agent: 200 };
export const ERROR_CODE_LIMIT = 64;
${rejectFunctions(["feedback", "olderThan"])}`],
  ["cli/init.js", "49a8b95b30c9caaee99441f5416ffbebc2f79eb7b438c73c375cd14f9f28adf1",
    rejectFunctions(["init"])],
  ["oauth/cli.js", "331ce03a106545f94d2301b97afaa555747b76a9fe33e800782c4968adce7ae7",
    rejectFunctions(["runLogin", "runLogout", "runModels"])],
  ["oauth/fetch.js", "d5fe054ed0d486ae4249a7e72861d8efc9d18915586e11dfbb16168ddb04ee1d",
    rejectFunctions(["createOAuthFetch"])],
  ["oauth/providers.js", "500550b47480e1f02d55587cb7a3e16c490cc73504a44259e675ec498b89a69f", `
export const PROVIDER_IDS = [];
export function isProviderId() { return false; }
${rejectFunctions(["getProvider", "loginHint", "apiKeyFor", "credentialSource"])}`],
  ["oauth/providers/openai.js", "24686641a1b8a09bedcdc7436fe9a937d8d4a5d160de271cb717f53b40136efa",
    rejectFunctions(["createCodexProvider", "extractAccountId", "parseAuthorizationInput", "sendCodexRequest"])],
  ["oauth/providers/github-copilot.js", "567c0c91326fef5684f7e5608d2a3094f99b23db1cd160e70793e074f0d09d68", `
export const COPILOT_API_URL = "";
${rejectFunctions(["enterpriseHost", "copilotBaseUrl", "createCopilotProvider", "sendCopilotRequest", "copilotProtocolFor", "copilotRefusal", "isCopilotRefusal"])}`],
  ["oauth/providers/opencode-console.js", "3f9d1b95d2ac7a1dbfbdcfc2310f70c8291e145b99b6b3224b4e12980f6f132e",
    rejectFunctions(["createOpencodeConsoleProvider", "opencodeConsoleChatRoute", "opencodeConsoleRouteFor"])],
  ["oauth/providers/xai.js", "a23f8c760041d0dce90ab95fbee452d7ed643fde8a601245a119e5e804974d16",
    rejectFunctions(["createXaiProvider"])],
  ["oauth/token-endpoint.js", "ed550ecedbe69952bcd48a6d4ccff9f3988c24a2bd9d124f60b497f7410c04d9",
    rejectFunctions(["postForm", "requestTokens", "parseTokens", "positiveSeconds", "expiryFrom"])],
  ["agent/ai-sdk.js", "79bfcdb3f7e87e6342cee5cdc380f8510cb969b99ecbac7f7068842ebcfd3fcb", `
export function loadedAiSdk() { return undefined; }
export async function loadAiSdkIfInstalled() { return undefined; }
${rejectFunctions(["asSdkLanguageModel", "loadAiSdk", "aiSdk"])}`]
];

export function disableE2eExternalServices() {
  const dist = path.dirname(require.resolve("e2e"));
  const packageInfo = JSON.parse(fs.readFileSync(path.join(dist, "..", "package.json"), "utf8"));
  if (packageInfo.version !== "0.21.2") {
    throw new Error("e2e 버전이 변경되었습니다. 외부 전송 경로를 다시 검토하기 전에는 실행할 수 없습니다.");
  }

  const pending = patches.map(([relativePath, originalHash, replacement]) => {
    const target = path.join(dist, relativePath);
    const current = fs.readFileSync(target, "utf8");
    const content = header + replacement.trim() + "\n";
    const hash = createHash("sha256").update(current).digest("hex");
    if (current !== content && hash !== originalHash) {
      throw new Error(`검토되지 않은 e2e 파일입니다: ${relativePath}`);
    }
    return { target, content, isChanged: current !== content };
  });

  // 모든 원본 검사를 통과한 뒤에만 쓰며, 중단 시 다음 실행에서 나머지를 처리함.
  for (const { target, content, isChanged } of pending) {
    if (isChanged) fs.writeFileSync(target, content, "utf8");
  }
  return pending.filter((entry) => entry.isChanged).length;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  console.log(`e2e 외부 전송 제거: ${disableE2eExternalServices()}개 모듈 변경`);
}
