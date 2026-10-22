import { randomUUID } from "node:crypto";
import fs from "node:fs";

import { test } from "e2e";

import type { UnityStatus } from "./unityClient.js";
import type { UnityFixture } from "./unityFixture.js";

// M6: 기존 Boogle 모드별 실행을 e2e 테스트로 이식한다.
// 각 테스트는 하나의 attempt를 쓰며 endAttempt가 에디터 상태를 복원한다.
// 사람 판정이 필요한 산출물(포즈 프레임, 영상 화면)은 여기서 완전성만 검증하고
// "visual-review" 태그로 표시한다 — 이진 PASS를 사람 판정으로 오인시키지 않기 위함.

const MAIN_AUTO = "Assets/_Project/Scene/Main_Auto.unity";

type Fixtures = { unity: UnityFixture };

/** Main_Auto를 Edit 상태로 열고 F계열 선행조건(비녹화·비재생·깨끗한 씬)을 확인한다. */
async function prepareMainAuto(unity: UnityFixture): Promise<void> {
  await unity.run("open_e2e_scene", { scene_path: MAIN_AUTO });
  const env = await unity.environment();
  const problems: string[] = [];
  if (env.play_mode === true) problems.push("이미 Play 중");
  if (env.scene_path !== MAIN_AUTO) problems.push(`씬 불일치: ${env.scene_path}`);
  if (env.scene_dirty === true) problems.push("씬이 dirty");
  if (env.avatar_valid !== true) problems.push("Humanoid Avatar 없음");
  if (env.is_recording === true || env.recorder_recording === true) problems.push("녹화 중");
  if (env.capture_framerate !== 0) problems.push("captureFramerate 잔류");
  if (env.has_prepared_motion === true) problems.push("준비된 모션 잔류");
  if (problems.length > 0) {
    throw new Error(`Main_Auto 선행조건 불만족: ${problems.join("; ")}`);
  }
}

async function readJson(pathValue: unknown): Promise<Record<string, unknown>> {
  if (typeof pathValue !== "string" || pathValue.length === 0) {
    throw new Error("상태 경로가 status에 없습니다");
  }
  return JSON.parse(await fs.promises.readFile(pathValue, "utf8")) as Record<string, unknown>;
}

// F01 — 선택 전 상태: 모델·Avatar·바닥·카메라 구조 검증 + 증거 PNG.
// 시각 판정은 사람 검토 대상이다(visual_review_required가 true여야 정상).
test("F01 preselection state", { platforms: ["unity"], tags: ["visual-review"] }, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  await prepareMainAuto(unity);
  await unity.enterPlay();
  const status = await unity.run("capture_preselection_state");
  const state = await readJson(status.preselection_state_path);
  if (state.structural_passed !== true || state.visual_review_required !== true) {
    throw new Error(`F01 증거 불완전: structural=${state.structural_passed} visual=${state.visual_review_required}`);
  }
});

// F02~F05·F11 — 재생 탐색 증거: 임포트→재생→지정 프레임 캡처(게임뷰+측면).
test("F02-F11 playback seek evidence", {
  platforms: ["unity"], timeout: 1_300_000, tags: ["visual-review"]
}, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  await prepareMainAuto(unity);
  await unity.enterPlay();
  const status = await unity.run("capture_playback_seek_evidence", {
    side_capture_frames: [0, 166, 544]
  }, 1_260_000);
  await readJson(status.playback_state_path);
});

// F07 — 잘못된 입력(없는 FBX·무효 Avatar)이 제품 검증에서 거절되는지.
test("F07 invalid input evidence", { platforms: ["unity"], timeout: 180_000 }, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  await prepareMainAuto(unity);
  await unity.enterPlay();
  const status = await unity.run("capture_invalid_input_evidence", undefined, 120_000);
  const state = await readJson(status.failure_evidence_path);
  if (state.test_passed !== true) {
    throw new Error(`F07 거절 검증 실패: ${JSON.stringify(state).slice(0, 400)}`);
  }
});

// F09 — tetoris 라이브 발 증거(실재생 중 지면 근거 프레임).
test("F09 foot live evidence", {
  platforms: ["unity"], timeout: 1_300_000, tags: ["visual-review"]
}, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  await prepareMainAuto(unity);
  await unity.enterPlay();
  const status = await unity.run("capture_tetoris_live_foot_evidence", {
    run_id: randomUUID()
  }, 1_260_000);
  await readJson(status.foot_live_state_path);
});

// F10 — satisfaction_2 풀클립 발 지표(208초, all-frames CSV).
test("F10 full clip metrics", {
  platforms: ["unity"], timeout: 1_980_000, tags: ["metrics"]
}, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  await prepareMainAuto(unity);
  await unity.enterPlay();
  const status = await unity.fullClipMetrics({ run_id: randomUUID() });
  const state = await readJson(status.full_clip_state_path);
  if (typeof state.row_count !== "number" || (state.row_count as number) <= 0) {
    throw new Error("F10 all-frames CSV 행이 없습니다");
  }
});

// F12 — 풀클립 named VMD 산출 + VRM 익스포트.
test("F12 full named vmd + vrm export", {
  platforms: ["unity"], timeout: 2_400_000, tags: ["output"]
}, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  await prepareMainAuto(unity);
  await unity.enterPlay();
  const vmdStatus: UnityStatus = await unity.run("capture_satisfaction_full_named_vmd", {
    run_id: randomUUID()
  }, 2_100_000);
  if (typeof vmdStatus.output_path !== "string" || vmdStatus.output_path.length === 0) {
    throw new Error("F12 VMD 산출 경로가 없습니다");
  }
  await unity.exitPlay();
  const vrm = await unity.run("capture_satisfaction_vrm", undefined, 120_000);
  if (typeof vrm.vrm_output_path !== "string" || (vrm.vrm_file_size_bytes ?? 0) <= 0) {
    throw new Error("F12 VRM 산출이 없습니다");
  }
});

// F02/F06 — 2초 quick VMD smoke(기존 러너의 기본 smoke 명령).
test("F06 quick vmd smoke", { platforms: ["unity"], timeout: 300_000 }, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  await prepareMainAuto(unity);
  await unity.enterPlay();
  const status = await unity.run("capture_satisfaction_quick_vmd_smoke_2s", {
    run_id: randomUUID()
  }, 240_000);
  if (typeof status.output_path !== "string" || status.output_path.length === 0) {
    throw new Error("F06 VMD 산출 경로가 없습니다");
  }
});

// F14 — testPrefab 대체 모델 풀클립 하체 계측.
test("F14 alternate model metrics", {
  platforms: ["unity"], timeout: 2_400_000, tags: ["metrics"]
}, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  await prepareMainAuto(unity);
  await unity.enterPlay();
  const status = await unity.run("capture_tetoris_testprefab_full_clip_metrics", {
    run_id: randomUUID(), lower_body_only: true
  }, 2_100_000);
  await readJson(status.full_clip_state_path);
});

// F15 — 제품 UI 플로우 3해상도(가져오기→재생→일시정지→녹화→정지→오류 표시).
const PRODUCT_UI_SIZES = [
  { w: 1280, h: 720 }, { w: 1600, h: 900 }, { w: 1920, h: 1080 }
];

test("F15 product ui flow", {
  platforms: ["unity"], timeout: 1_800_000, tags: ["visual-review"]
}, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  const runId = randomUUID();
  await prepareMainAuto(unity);
  await unity.enterPlay();
  for (const size of PRODUCT_UI_SIZES) {
    // 각 흐름의 invalid_input 단계가 파이프라인 세션을 Failed로 남긴다.
    // Play를 재진입해 파이프라인을 새로 초기화하지 않으면 다음 흐름이
    // 시작 즉시 Failed 가드(product_error)에 걸린다.
    const env = await unity.environment();
    if (env.session_state === "Failed") {
      await unity.exitPlay();
      await unity.enterPlay();
    }
    const status = await unity.run("capture_product_ui_flow", {
      run_id: runId, screen_width: size.w, screen_height: size.h
    }, 480_000);
    const state = await readJson(status.manifest_path);
    const steps = (state.events as { step: string }[] | undefined)?.map((entry) => entry.step) ?? [];
    for (const expected of ["before_import", "import_ready", "playing", "paused",
      "recording", "recording_stopped", "stopped", "invalid_input_error"]) {
      if (!steps.includes(expected)) {
        throw new Error(`F15 ${size.w}x${size.h}: step 누락 ${expected} (recorded=${steps.join(",")})`);
      }
    }
  }
});
