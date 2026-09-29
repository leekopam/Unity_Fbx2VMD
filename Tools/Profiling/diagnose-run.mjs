import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

// 프로파일링 런의 자동 분석(findings)을 JEV 모델로 2차 진단하는 도구입니다.
// Boogle의 review-contact-intent.mjs와 동일한 choice/확신도 패턴을 따릅니다.
// 키가 없으면 pending_request만 jev.json에 남기고, 모델 판정은 항상 보조(자동 수정 없음)입니다.

const profilingRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)),
  "../../Artifacts/Profiling");
const endpoint = "https://api.typesafe.ai/v1/systemone";

const causeNames = {
  io_load: "파일/디스크 로딩",
  compute_cpu: "CPU 연산(파싱·변환·보정)",
  engine_pipeline: "Unity 엔진 파이프라인(임포트·아바타·애니메이션)",
  memory_gc: "메모리 할당/GC",
  uncertain: "측정값으로 판별 불가" };

function readChoice(response, question, names) {
  const answer = response?.answers?.[question];
  if (answer?.type !== "choice" || !Object.hasOwn(names, answer.choice) ||
      !Number.isFinite(answer.confidence) || answer.confidence < 0 ||
      answer.confidence > 1 || !answer.probabilities ||
      Object.keys(names).some(name => !Number.isFinite(answer.probabilities[name]) ||
        answer.probabilities[name] < 0 || answer.probabilities[name] > 1))
    throw new Error(`Jev ${question} 응답 형식 오류`);
  return { choice: answer.choice, label: names[answer.choice],
    probabilities: answer.probabilities, confidence: answer.confidence };
}

async function askJev(payload, apiKey, fetchImpl) {
  for (let attempt = 0; attempt < 3; attempt++) {
    const response = await fetchImpl(endpoint, { method: "POST",
      headers: { Authorization: `Bearer ${apiKey}`, "Content-Type": "application/json" },
      body: JSON.stringify(payload), signal: AbortSignal.timeout(30000) });
    if ((response.status === 429 || response.status === 529) && attempt < 2) {
      await new Promise(resolve => setTimeout(resolve, 500 * 2 ** attempt));
      continue;
    }
    if (!response.ok) throw new Error(`Jev HTTP ${response.status}`);
    const body = await response.json();
    if (typeof body?.model !== "string" || !body.answers)
      throw new Error("Jev 응답 형식 오류");
    return body;
  }
}

function makeDiagnosisRequest(record, finding, index) {
  // 구 버전 리포트의 프레임 단위 샘플도 스테이지명별로 합산해 보낸다.
  const byStage = new Map();
  for (const s of record.stages || []) {
    const key = s?.stage ?? "";
    const prev = byStage.get(key) ?? { stage: key, delta_ms: 0, message: undefined };
    prev.delta_ms += Math.round(s?.delta_ms ?? s?.deltaMs ?? 0);
    prev.message = s?.message ?? prev.message;
    byStage.set(key, prev);
  }
  const stages = [...byStage.values()];
  const scopeNames = Object.fromEntries(
    (finding.suggestedScopes || finding.suggested_scopes || [])
      .map(scope => [scope, `PerfScope 삽입 후보: ${scope}`]));
  const questions = {
    cause_class: { type: "choice",
      instructions: "A profiling run of a Unity FBX-to-VMD conversion pipeline flagged this stage. Which cause class is most plausible? Choose uncertain when the evidence cannot discriminate.",
      criteria: causeNames } };
  if (Object.keys(scopeNames).length > 1) {
    questions.deepen_target = { type: "choice",
      instructions: "Which single candidate should get deeper PerfScope instrumentation first to localize the issue? Choose uncertain if none is clearly more promising.",
      criteria: { ...scopeNames, uncertain: "후보 중 우선순위를 정할 수 없음" } };
  }
  return { model: "jev-latest", state: {
    source_stage: "Automated analysis of a Unity FBX-to-VMD pipeline profiling run (wall-clock stages, GC is current-thread only)",
    run_label: record.label, outcome: record.outcome,
    total_ms: Math.round(record.totalMs ?? record.total_ms ?? 0),
    finding_kind: finding.kind, finding_stage: finding.stage,
    finding_detail: finding.detail,
    stage_table: stages
  }, questions };
}

export async function diagnoseRun(record, { apiKey = "", fetchImpl = fetch } = {}) {
  if (!record || typeof record !== "object" || !Array.isArray(record.stages))
    return { status: "BLOCKED", reason: "런 기록 형식이 올바르지 않음" };
  if (!Array.isArray(record.analysis) || record.analysis.length === 0)
    return { status: "BLOCKED", reason: "자동 분석 findings가 없어 진단할 대상이 없음" };

  const result = { status: apiKey ? "DIAGNOSIS_COMPLETE" : "SKIPPED_NO_API_KEY",
    run_id: record.runId ?? record.run_id, model: null, entries: [] };
  for (const [index, finding] of record.analysis.entries()) {
    const request = makeDiagnosisRequest(record, finding, index);
    const entry = { index, kind: finding.kind, stage: finding.stage,
      detail: finding.detail, cause: null, deepen: null };
    if (!apiKey) {
      entry.pending_request = request;
      result.entries.push(entry);
      continue;
    }
    try {
      const response = await askJev(request, apiKey, fetchImpl);
      result.model = response.model;
      entry.cause = readChoice(response, "cause_class", causeNames);
      if (request.questions.deepen_target) {
        const names = { ...request.questions.deepen_target.criteria };
        const picked = readChoice(response, "deepen_target", names);
        entry.deepen = { choice: picked.choice, label: names[picked.choice],
          confidence: picked.confidence };
      }
      entry.usage = response.usage;
    } catch (error) {
      entry.error = error instanceof TypeError ? "Jev 네트워크 오류" : error.message;
      result.status = "ADVISORY_ERROR";
      result.entries.push(entry);
      break;
    }
    result.entries.push(entry);
  }
  return result;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const reportPath = path.resolve(process.argv[2] || "");
  const relative = path.relative(profilingRoot, reportPath);
  // run-*.json 런 기록만 받는다. trace·jev 사이드카는 같은 패턴에 걸리므로 명시 제외한다.
  const base = path.basename(reportPath);
  if (!process.argv[2] || relative.startsWith("..") || path.isAbsolute(relative) ||
      relative.includes(path.sep) || !base.startsWith("run-") ||
      !base.endsWith(".json") || base.endsWith(".trace.json") || base.endsWith(".jev.json"))
    throw new Error("Artifacts/Profiling 바로 아래의 run-*.json 경로가 필요합니다.");
  const record = JSON.parse(await readFile(reportPath, "utf8"));
  const result = await diagnoseRun(record,
    { apiKey: process.env.TYPESAFE_API_KEY?.trim() || "" });
  const outPath = reportPath.replace(/\.json$/, ".jev.json");
  await writeFile(outPath, JSON.stringify(result, null, 2));
  process.stdout.write(`${JSON.stringify({ status: result.status,
    entries: result.entries.length, outPath })}\n`);
  process.exitCode = ["BLOCKED", "ADVISORY_ERROR"].includes(result.status) ? 1 : 0;
}
