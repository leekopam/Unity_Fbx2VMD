import { test } from "e2e";

// M4 게이트: 모델 없는 scripted executor가 agent.act/assert 스텝으로
// Boogle 명령을 실행하는지 확인한다(짧은 환경 캡처만 사용).
test("unity scripted executor runs commands", { platforms: ["unity"] }, async (fixtures) => {
  const agent = fixtures.agent;
  await agent.act("unity capture_e2e_environment");
  // Play 상태는 attempt 복원이 보장하는 유일한 결정적 필드다.
  await agent.assert("unity env play_mode == false");
});
