import assert from "node:assert/strict";
import { test } from "node:test";
import { validateHumanLabels } from "./validate-human-labels.mjs";

test("사람 표식은 원래 구간·양발·허용 표식·검토자를 모두 요구한다", () => {
  const template = "from_frame,to_frame,side,contact_label,motion_label,reviewer,notes\n" +
    "790,791,left,,,,\n790,791,right,,,,\n";
  const filled = "from_frame,to_frame,side,contact_label,motion_label,reviewer,notes\n" +
    "790,791,left,앞꿈치,구르기,검토자,\n" +
    "790,791,right,공중,불확실,검토자,\n";
  assert.equal(validateHumanLabels(filled, template).status, "MANUAL_REVIEW_REQUIRED");
  assert.equal(validateHumanLabels(filled.replace("공중,불확실,검토자", "공중,,검토자"),
    template).status, "INVALID");
  assert.equal(validateHumanLabels(filled.split("\n").slice(0, 2).join("\n") + "\n",
    template).status, "INVALID");
});
