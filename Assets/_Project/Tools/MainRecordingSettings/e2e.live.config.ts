import fs from "node:fs";
import path from "node:path";
import { web } from "@e2e-dev/web";
import type { E2EConfig } from "e2e";
import { E2E_ENTRY_URL, electronProvider } from "./tests-e2e/electronProvider.js";

const fixturePath = process.env.FBX2VMD_E2E_SESSION_FILE;
if (!fixturePath || !path.isAbsolute(fixturePath)) {
  throw new Error("준비된 Unity E2E 입력 문서의 절대 경로가 필요합니다.");
}
const fixture = JSON.parse(fs.readFileSync(fixturePath, "utf8"));
const { settingsPath, libraryPath, fbxPath, vrmPath } = fixture;
const session = JSON.parse(fs.readFileSync(path.join(path.dirname(settingsPath), "session.json"), "utf8"));
if (session.phase !== "prepared" || session.runId !== fixture.runId ||
    session.settingsPath !== settingsPath || session.libraryPath !== libraryPath) {
  throw new Error("Unity 시험 세션이 준비되지 않았거나 입력 문서와 일치하지 않습니다.");
}

export default {
  projectId: "main-recording-settings-live",
  tests: ["tests-e2e/product-settings.live.ts"],
  workers: 1,
  reporters: ["list", "junit"],
  output: path.join(".e2e", "live", fixture.runId),
  targets: [{
    name: "electron",
    engine: web({ browser: electronProvider({ unitySession: { settingsPath, libraryPath, fbxPath, vrmPath } }), viewport: null }),
    app: { url: E2E_ENTRY_URL }
  }]
} satisfies E2EConfig;
