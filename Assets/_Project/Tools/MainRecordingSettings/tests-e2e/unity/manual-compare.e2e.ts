import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";

import { test } from "e2e";

import { createUnityClient, defaultUnityProjectRoot } from "./unityClient.js";
import type { UnityFixture } from "./unityFixture.js";

// F13 — 수동 기준(Sub_Manual_F13)과 자동 경로의 수치·캡처 쌍 비교.
// Unity 측 비교 실행은 yyb_visual_compare_* 프로토콜, 수치 판정은
// Tools/Boogle/manual-compare.mjs의 compareManualCapture를 그대로 재사용한다.
test("F13 manual compare", {
  platforms: ["unity"], timeout: 1_300_000, tags: ["visual-review"]
}, async (f) => {
  const unity = (f as unknown as { unity: UnityFixture }).unity;
  const projectRoot = defaultUnityProjectRoot();
  const visual = createUnityClient({
    projectRoot,
    requestFileName: "yyb_visual_compare_request.json",
    statusFileName: "yyb_visual_compare_status.json"
  });

  await unity.run("open_e2e_scene", { scene_path: "Assets/_Project/Scene/Main_Auto.unity" });
  const env = await unity.environment();
  if (env.play_mode === true || env.scene_dirty === true) {
    throw new Error("F13 선행조건: Main_Auto 비재생·비dirty 상태가 필요합니다");
  }

  // Unity 측 비교 실행 — 수동·자동 쌍 캡처와 비교 CSV/PNG를 만든다.
  const status = await visual.send("run", {
    fields: {
      fbx_file: "satisfaction_2.fbx",
      duration_seconds: 3,
      finger_closeups: false,
      f13_pair_only: true
    },
    timeoutMs: 1_200_000
  });

  const summaryPath = status.summary_json_path;
  if (typeof summaryPath !== "string" || summaryPath.length === 0) {
    throw new Error("F13 비교 요약이 없습니다(BLOCKED)");
  }
  const resolved = path.resolve(projectRoot, summaryPath);
  const summaryText = (await fs.promises.readFile(resolved, "utf8"))
    // JsonUtility는 NaN/Infinity를 비표준 리터럴로 출력한다 — 파싱 전 정규화.
    .replace(/^﻿/, "")
    .replace(/:\s*(NaN|Infinity|-Infinity)/g, ": null");
  const summary = JSON.parse(summaryText);

  const manualComparePath = path.join(
    projectRoot, "Tools", "Boogle", "manual-compare.mjs");
  const { compareManualCapture } = await import(pathToFileURL(manualComparePath).href) as {
    compareManualCapture: (summary: unknown, root: string) => Promise<{
      status: string; reason?: string; comparedFrames?: number;
    }>;
  };
  const result = await compareManualCapture(summary, projectRoot);
  // 수치·캡처 쌍이 완전해도 최종 판정은 사람 검토 — e2e에서는 비교 가능성만 검증한다.
  if (result.status !== "MANUAL_REVIEW_REQUIRED") {
    throw new Error(`F13 비교 불가: ${result.status} — ${result.reason ?? ""}`);
  }
});
