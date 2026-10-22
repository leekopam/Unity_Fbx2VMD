import { expect } from "e2e";
import { test } from "@e2e-dev/web";

// 최소 스모크 — Electron 창이 열리고 핵심 셸 요소가 보이는지 확인한다.
test("설정창이 열리고 FBX 가져오기 버튼이 보인다", async ({ app, screen }) => {
  await app.open();
  await expect(screen.getByRole("button", "FBX 파일 가져오기")).toBeVisible();
  await expect(screen.getByRole("button", "메뉴")).toBeVisible();
});
