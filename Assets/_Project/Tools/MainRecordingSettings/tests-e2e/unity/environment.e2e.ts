import { test } from "e2e";

import type { UnityFixture } from "./unityFixture.js";

// M1/M2 게이트: unity fixture가 환경 상태를 읽고, 엔진의 startAttempt/endAttempt가
// 복원 기준선·검증을 오류 없이 수행하는지 확인한다.
test("unity environment capture and restore baseline", { platforms: ["unity"] },
  async (fixtures) => {
    const unity = (fixtures as { unity: UnityFixture }).unity;
    const env = await unity.environment();
    if (typeof env.scene !== "string" || env.scene.length === 0) {
      throw new Error(`환경 상태에 scene이 없습니다: ${JSON.stringify(env)}`);
    }
    if (env.play_mode !== true && env.play_mode !== false) {
      throw new Error(`환경 상태에 play_mode가 없습니다: ${JSON.stringify(env)}`);
    }
  });
