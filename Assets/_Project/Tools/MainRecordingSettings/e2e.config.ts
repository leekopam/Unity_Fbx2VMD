import { web } from "@e2e-dev/web";
import type { E2EConfig } from "e2e";

import { E2E_ENTRY_URL, electronProvider } from "./tests-e2e/electronProvider.js";

// Electron 설정창을 CDP로 붙여 구동하는 e2e 설정이다.
// Electron은 네이티브 미지원이라 커스텀 provider가 attempt마다
// --remote-debugging-port로 띄운 인스턴스를 엔진에 넘긴다.
// e2e는 file:// 탐색을 거부하므로 provider가 build/를 4199 포트로
// 정적 서빙하고 Electron 창이 그 http URL을 로드한다.

export default {
  projectId: "main-recording-settings",
  tests: ["tests-e2e/**/*.e2e.ts"],
  workers: 1,
  reporters: ["list", "junit"],
  output: ".e2e",
  targets: [
    {
      name: "electron",
      engine: web({ browser: electronProvider(), viewport: null }),
      app: { url: E2E_ENTRY_URL }
    }
  ]
} satisfies E2EConfig;
