import { test } from "e2e";

import type { UnityFixture } from "./unityFixture.js";

// M3 게이트: Play 진입 → Game View MP4 녹화 → 정지 → attempt 산출물에 영상 파일.
test("unity video artifact", { platforms: ["unity"] }, async (fixtures) => {
  const unity = (fixtures as { unity: UnityFixture }).unity;
  const env = await unity.environment();
  await unity.run("enter_e2e_play", { scene_path: env.scene_path }, 90_000);
  await unity.startVideo();
  await new Promise((resolve) => setTimeout(resolve, 3000));
  const videos = await unity.stopVideo();
  if (videos.length === 0 || !videos[0].endsWith(".mp4")) {
    throw new Error(`영상 산출물이 없습니다: ${JSON.stringify(videos)}`);
  }
});
