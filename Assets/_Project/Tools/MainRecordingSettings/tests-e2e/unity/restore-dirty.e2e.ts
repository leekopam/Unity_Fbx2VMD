import { test } from "e2e";

import type { UnityFixture } from "./unityFixture.js";

// M2 복원 게이트: Play에 진입한 채 attempt를 끝내면 엔진의 endAttempt가
// 기준선(편집 모드)으로 되돌려야 한다. attempt는 테스트 단위라 같은 파일의
// 다음 테스트가 play_mode=false를 보는지로 검증한다(serial 그룹은 attempt를 공유해 부적합).

test("leaves play mode running", { platforms: ["unity"] }, async (fixtures) => {
  const unity = (fixtures as { unity: UnityFixture }).unity;
  // 현재 로드된 씬으로 진입한다 — 씬 미지정 시 Main_Auto 선행조건이 검증된다.
  const before = await unity.environment();
  await unity.run("enter_e2e_play", { scene_path: before.scene_path }, 90_000);
  const env = await unity.environment();
  if (env.play_mode !== true) {
    throw new Error("Play 진입이 확인되지 않았습니다.");
  }
  // 의도적으로 exitPlay를 호출하지 않는다.
});

test("editor restored to edit mode by endAttempt", { platforms: ["unity"] }, async (fixtures) => {
  const unity = (fixtures as { unity: UnityFixture }).unity;
  const env = await unity.environment();
  if (env.play_mode !== false) {
    throw new Error(`endAttempt 복원이 동작하지 않았습니다: play_mode=${env.play_mode}`);
  }
});
