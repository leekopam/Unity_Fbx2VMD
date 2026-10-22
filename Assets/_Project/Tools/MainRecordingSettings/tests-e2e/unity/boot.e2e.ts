import { expect, test } from "e2e";

// M0 스파이크: 엔진이 Unity 에디터를 observe하고 Game View 스크린샷을
// attempt 산출물로 남기는지 확인하는 최소 테스트다.
test("unity editor boots and captures", { platforms: ["unity"] }, async ({ app }) => {
  const shot = await app.screenshot("boot");
  expect(shot).toContain(".png");
});
