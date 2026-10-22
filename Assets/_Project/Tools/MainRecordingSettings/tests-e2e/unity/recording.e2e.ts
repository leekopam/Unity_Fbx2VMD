import { randomUUID } from "node:crypto";

import { test } from "e2e";

import { prepareMainAuto, readStatusJson } from "./mainAuto.js";
import { UNITY_COMMANDS } from "./unityClient.js";
import type { UnityFixture } from "./unityFixture.js";

// 영상 녹화 경로(Game View→Unity Recorder)의 e2e 증거다.
// F15가 "버튼을 누르면 녹화가 켜지는가"를 본다면, 여기서는 녹화 산출물과
// 구간·스킵·차단 동작이 의도대로인지를 상태 JSON으로 판정한다.
// satisfaction_2는 선두 ~7초의 정지 구간이 실측된 입력이라 스킵 검증에 쓴다.

type Fixtures = { unity: UnityFixture };

interface RecordingEvidence {
  output_exists?: boolean;
  output_file_size_bytes?: number;
  recorded_pose_ticks?: number;
  expected_pose_ticks?: number;
  resolved_start_seconds?: number;
  resolved_end_seconds?: number;
  clip_frame_rate?: number;
  video_frame_rate?: number;
  import_rejected?: boolean;
  still_recording_final?: boolean;
  events?: string[];
}

function assertRecordingEvidence(
  evidence: RecordingEvidence, label: string): void {
  if (evidence.output_exists !== true ||
      (evidence.output_file_size_bytes ?? 0) <= 0) {
    throw new Error(
      `${label}: 녹화 산출물이 없거나 비어 있습니다 ` +
      `(exists=${evidence.output_exists} size=${evidence.output_file_size_bytes})`);
  }
  if ((evidence.recorded_pose_ticks ?? 0) <= 0) {
    throw new Error(`${label}: 캡처된 포즈 틱이 없습니다`);
  }
  if (evidence.still_recording_final === true) {
    throw new Error(`${label}: 증거 수집 후에도 녹화가 종료되지 않았습니다`);
  }
}

// R01 — 기본 녹화가 mp4를 산출하고, 녹화 중 재임포트가 차단되는지.
test("R01 recording output and import block", {
  platforms: ["unity"], timeout: 900_000
}, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  await prepareMainAuto(unity);
  await unity.enterPlay();
  const status = await unity.run(UNITY_COMMANDS.motionRecording, {
    run_id: randomUUID(),
    recording_skip_still: 0,
    recording_stop_after_seconds: 2,
    recording_try_import_during: true
  }, 840_000);
  const evidence = await readStatusJson(status.manifest_path) as RecordingEvidence;
  assertRecordingEvidence(evidence, "R01");
  if (evidence.import_rejected !== true) {
    throw new Error(
      `R01: 녹화 중 임포트가 차단되지 않았습니다 ` +
      `(events=${(evidence.events ?? []).join(",")})`);
  }
});

// R02 — 구간 녹화: 모션 프레임 300~420(양끝 포함)만 녹화하고
// 해석된 초와 실제 캡처 포즈 수가 기대와 정확히 일치하는지.
test("R02 recording frame range", {
  platforms: ["unity"], timeout: 900_000
}, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  await prepareMainAuto(unity);
  await unity.enterPlay();
  const startFrame = 300;
  const endFrame = 420;
  const status = await unity.run(UNITY_COMMANDS.motionRecording, {
    run_id: randomUUID(),
    recording_skip_still: 0,
    recording_range_enabled: true,
    recording_start_frame: startFrame,
    recording_end_frame: endFrame,
    recording_stop_after_seconds: 0
  }, 840_000);
  const evidence = await readStatusJson(status.manifest_path) as RecordingEvidence;
  assertRecordingEvidence(evidence, "R02");
  const clipRate = evidence.clip_frame_rate ?? 0;
  if (clipRate <= 0) {
    throw new Error("R02: 클립 프레임률이 증거에 없습니다");
  }
  const expectedStart = startFrame / clipRate;
  const expectedEnd = endFrame / clipRate;
  if (Math.abs((evidence.resolved_start_seconds ?? -1) - expectedStart) > 0.02 ||
      Math.abs((evidence.resolved_end_seconds ?? -1) - expectedEnd) > 0.02) {
    throw new Error(
      `R02: 구간 해석 불일치 start=${evidence.resolved_start_seconds}s ` +
      `(기대 ${expectedStart}s) end=${evidence.resolved_end_seconds}s (기대 ${expectedEnd}s)`);
  }
  // 시작 자세는 녹화 전 Seek로 적용되어 틱 집계에서 빠진다. 부동소수 경계에서
  // 마지막 틱 시각이 종단을 살짝 넘기면 틱이 1개 더 생기므로 expected~expected+1을 허용.
  const expected = evidence.expected_pose_ticks ?? -1;
  const tickDiff = (evidence.recorded_pose_ticks ?? -1) - expected;
  if (expected > 0 && (tickDiff < 0 || tickDiff > 1)) {
    throw new Error(
      `R02: 캡처 포즈 수 불일치 actual=${evidence.recorded_pose_ticks} ` +
      `expected=${expected}~${expected + 1}`);
  }
});

// R03 — 선두 정지 스킵: satisfaction_2의 ~7초 준비 구간을 건너뛰고
// 첫 움직임부터 녹화가 시작되는지.
test("R03 recording skips leading still", {
  platforms: ["unity"], timeout: 900_000
}, async (f) => {
  const unity = (f as unknown as Fixtures).unity;
  await prepareMainAuto(unity);
  await unity.enterPlay();
  const status = await unity.run(UNITY_COMMANDS.motionRecording, {
    run_id: randomUUID(),
    recording_skip_still: 1,
    recording_stop_after_seconds: 2
  }, 840_000);
  const evidence = await readStatusJson(status.manifest_path) as RecordingEvidence;
  assertRecordingEvidence(evidence, "R03");
  const start = evidence.resolved_start_seconds ?? -1;
  if (start < 5 || start > 10) {
    throw new Error(
      `R03: 선두 정지 스킵이 satisfaction_2의 안무 시작(~7s)을 찾지 못했습니다 ` +
      `(resolved_start=${start}s)`);
  }
});
