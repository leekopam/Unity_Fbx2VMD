import path from "node:path";
import { fileURLToPath } from "node:url";

import type { E2EConfig } from "e2e";

import { boogleSealReporter } from "./tests-e2e/unity/boogleSealReporter.js";
import { unityEngine } from "./tests-e2e/unity/unityEngine.js";
import { unityScriptedExecutor } from "./tests-e2e/unity/unityExecutor.js";

// Unity 에디터를 대상으로 하는 e2e 설정이다.
// Electron 설정창용 e2e.config.ts와 분리해 두 surface의 테스트가 서로를
// 플랫폼 필터 없이 오염시키지 않게 한다. 실행은 runE2e.mjs에 --config로 넘긴다.
// Unity 에디터는 사용자가 열어둔 인스턴스의 폴러를 쓰므로 workers는 항상 1이다.

export default {
  projectId: "fbx2vmd-unity",
  tests: ["tests-e2e/unity/**/*.e2e.ts"],
  workers: 1,
  // BOOGLE_SEAL=1이면 실행 결과를 boogle-sdk로 봉인한다(비교·릴리스 실행용).
  reporters: [
    "list",
    "junit",
    ...(process.env.BOOGLE_SEAL
      ? [boogleSealReporter({
        sdkRunnerPath: path.join(
          path.dirname(fileURLToPath(import.meta.url)),
          "node_modules", "boogle-sdk", "dist", "runner.js")
      })]
      : [])
  ],
  output: ".e2e-unity",
  // Unity 명령(임포트·녹화·풀클립 계측)은 웹 동작보다 훨씬 느리다.
  timeout: 600_000,
  launchTimeout: 240_000,
  actionTimeout: 180_000,
  assertionTimeout: 30_000,
  cleanupTimeout: 120_000,
  retries: 0,
  targets: [
    {
      name: "unity",
      engine: unityEngine(),
      app: { identity: "fbx2vmd-editor" }
    }
  ],
  // Boogle 명령을 모델 없이 그대로 실행하는 스크립트 executor다.
  agents: {
    default: { executor: unityScriptedExecutor() }
  }
} satisfies E2EConfig;
