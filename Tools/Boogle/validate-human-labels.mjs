import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readCsv } from "./manual-compare.mjs";

const header = "from_frame,to_frame,side,contact_label,motion_label,reviewer,notes";
const contacts = new Set(["공중", "앞꿈치", "뒤꿈치", "발 전체", "불확실"]);
const motions = new Set(["고정", "구르기", "의도된 이동", "불확실"]);

export function validateHumanLabels(labelsSource, templateSource) {
  const errors = [];
  if (labelsSource.replace(/^\uFEFF/, "").split(/\r?\n/, 1)[0] !== header ||
      templateSource.replace(/^\uFEFF/, "").split(/\r?\n/, 1)[0] !== header)
    return { status: "INVALID", errors: ["표식 CSV 헤더 불일치"] };
  let labels;
  let template;
  try {
    labels = readCsv(labelsSource.replace(/^\uFEFF/, ""));
    template = readCsv(templateSource.replace(/^\uFEFF/, ""));
  } catch (error) {
    return { status: "INVALID", errors: [error.message] };
  }
  const key = (row) => `${row.from_frame}:${row.to_frame}:${row.side}`;
  const expected = new Set(template.map(key));
  const seen = new Set();
  if (expected.size !== template.length || labels.length !== template.length)
    errors.push("표식 행 수 또는 원본 구간이 일치하지 않음");
  for (const row of labels) {
    const rowKey = key(row);
    if (!expected.has(rowKey) || seen.has(rowKey))
      errors.push(`예상 밖 또는 중복 구간: ${rowKey}`);
    seen.add(rowKey);
    if (!contacts.has(row.contact_label)) errors.push(`접촉 표식 누락·오류: ${rowKey}`);
    if (!motions.has(row.motion_label)) errors.push(`이동 표식 누락·오류: ${rowKey}`);
    if (!row.reviewer?.trim()) errors.push(`검토자 누락: ${rowKey}`);
  }
  return { status: errors.length ? "INVALID" : "MANUAL_REVIEW_REQUIRED",
    labelRows: labels.length, expectedRows: template.length, errors };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)),
    "../../Docs/Workflow/Local/evidence/boogle");
  const input = path.resolve(process.argv[2] || "");
  const relative = path.relative(root, input);
  if (!process.argv[2] || !relative || relative.startsWith("..") || path.isAbsolute(relative) ||
      path.basename(input) !== "human-labels.csv") {
    throw new Error("로컬 evidence 안의 human-labels.csv 경로가 필요합니다.");
  }
  const result = validateHumanLabels(await readFile(input, "utf8"),
    await readFile(path.join(path.dirname(input), "human-labels-template.csv"), "utf8"));
  process.stdout.write(`${JSON.stringify(result)}\n`);
  process.exitCode = result.status === "INVALID" ? 1 : 0;
}
