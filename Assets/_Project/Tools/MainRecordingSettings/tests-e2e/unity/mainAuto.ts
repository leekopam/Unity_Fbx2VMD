import fs from "node:fs";

import type { UnityFixture } from "./unityFixture.js";

export const MAIN_AUTO_SCENE = "Assets/_Project/Scene/Main_Auto.unity";

/** Main_Auto를 Edit 상태로 열고 F계열 선행조건(비녹화·비재생·깨끗한 씬)을 확인한다. */
export async function prepareMainAuto(unity: UnityFixture): Promise<void> {
  await unity.run("open_e2e_scene", { scene_path: MAIN_AUTO_SCENE });
  const env = await unity.environment();
  const problems: string[] = [];
  if (env.play_mode === true) problems.push("이미 Play 중");
  if (env.scene_path !== MAIN_AUTO_SCENE) problems.push(`씬 불일치: ${env.scene_path}`);
  if (env.scene_dirty === true) problems.push("씬이 dirty");
  if (env.avatar_valid !== true) problems.push("Humanoid Avatar 없음");
  if (env.is_recording === true || env.recorder_recording === true) problems.push("녹화 중");
  if (env.capture_framerate !== 0) problems.push("captureFramerate 잔류");
  if (env.has_prepared_motion === true) problems.push("준비된 모션 잔류");
  if (problems.length > 0) {
    throw new Error(`Main_Auto 선행조건 불만족: ${problems.join("; ")}`);
  }
}

/** status의 *_path 필드가 가리키는 JSON을 읽어 돌려준다. */
export async function readStatusJson(
  pathValue: unknown
): Promise<Record<string, unknown>> {
  if (typeof pathValue !== "string" || pathValue.length === 0) {
    throw new Error("상태 경로가 status에 없습니다");
  }
  return JSON.parse(await fs.promises.readFile(pathValue, "utf8")) as Record<string, unknown>;
}
