import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";

import type { FinishedRun, Reporter, ReporterSummary } from "e2e";

// M5: e2e 실행 결과를 boogle-sdk runWithAdapter로 봉인하는 리포터다.
// e2e는 봉인·해시·수동판정 계약이 없으므로 기존 SDK 계약을 그대로 재사용한다
// (Tools/Boogle/run-product-smoke.mjs와 같은 evidenceRoot/매니페스트 형태).
// artifacts.json + artifacts/ 폴더를 만들어 collectSubmittedArtifacts에 넘긴다.

const ARTIFACT_KIND_BY_EXT = new Map<string, "capture" | "video" | "output" | "trace">([
  [".png", "capture"], [".jpg", "capture"], [".jpeg", "capture"], [".webp", "capture"],
  [".mp4", "video"], [".webm", "video"],
  [".md", "trace"], [".har", "trace"],
  [".json", "output"], [".jsonl", "output"], [".xml", "output"], [".csv", "output"],
  [".log", "output"], [".txt", "output"], [".vmd", "output"]
]);

const MAX_ARTIFACTS = 256;

export interface BoogleSealReporterOptions {
  /** boogle-sdk dist/runner.js의 절대 경로. */
  sdkRunnerPath: string;
  /** 봉인 러닝 루트(evidenceRoot). 기본은 Unity 프로젝트의 boogle 증거 폴더. */
  evidenceRoot?: string;
  testCaseIds?: string;
}

export function boogleSealReporter(options: BoogleSealReporterOptions): Reporter {
  return {
    name: "boogle-seal",
    async onRunFinished(run: FinishedRun, signal: AbortSignal): Promise<ReporterSummary> {
      const unityProjectRoot = findUnityProjectRoot(run.projectRoot);
      const evidenceRoot = options.evidenceRoot ??
        path.join(unityProjectRoot, "Docs", "Workflow", "Local", "evidence", "boogle", "e2e-sealed");
      const staging = path.join(run.projectRoot, ".e2e-unity", "sealed-submission");
      const artifactDir = path.join(staging, "artifacts");
      await fs.promises.rm(staging, { recursive: true, force: true });
      await fs.promises.mkdir(artifactDir, { recursive: true });

      const manifest = await stageArtifacts(run, artifactDir);
      await fs.promises.writeFile(
        path.join(staging, "artifacts.json"),
        JSON.stringify({ artifacts: manifest }, null, 2));

      const { runWithAdapter } = await import(
        pathToFileURL(options.sdkRunnerPath).href) as {
        runWithAdapter: (
          root: string,
          config: Record<string, unknown>,
          execute: (tempPath: string) => Promise<Record<string, unknown>>,
          signal?: AbortSignal
        ) => Promise<{ runId: string; status: string; sealedPath: string }>;
      };
      const { collectSubmittedArtifacts } = await import(
        pathToFileURL(path.join(path.dirname(options.sdkRunnerPath), "artifact.js")).href) as {
        collectSubmittedArtifacts: (submissionRoot: string, tempPath: string) => Promise<unknown[]>;
      };

      const summary = (run.report as { run?: { summary?: Record<string, number> } }).run?.summary ?? {};
      const sealed = await runWithAdapter(evidenceRoot, {
        protocolVersion: "0.1.0",
        // 기존 봉인 러너와 동일한 프로젝트 ID로 같은 runs 계보에 쌓는다.
        projectId: "f2f44dc8-83ef-46d0-9d26-b0e52d1c4d20",
        adapter: { id: "fbx2vmd-e2e", version: "0.1.0" },
        testPack: { id: "fbx2vmd-unity-e2e", version: "0.1.0" },
        retries: 0,
        inputConditions: {
          testCaseIds: options.testCaseIds ?? "F01-F15",
          e2eRunId: run.report.run.id,
          e2eReportSha256: await hashOptionalFile(run.reportPath)
        },
        environmentConditions: collectEnvironment(unityProjectRoot)
      }, async (temporaryPath) => ({
        status: mapRunStatus(run),
        exitCode: run.exitCode,
        testSummary: {
          total: summary.executed ?? 0,
          passed: summary.passed ?? 0,
          failed: summary.failed ?? 0,
          inconclusive: summary.flaky ?? 0,
          // interrupted는 판정까지 못 간 취소이므로 skipped 버킷에 합산한다.
          skipped: (summary.skipped ?? 0) + (summary.interrupted ?? 0)
        },
        artifacts: await collectSubmittedArtifacts(staging, temporaryPath)
      }), signal);

      // 봉인이 끝난 스테이징 사본은 남기지 않는다.
      await fs.promises.rm(staging, { recursive: true, force: true }).catch(() => {});

      // E2E 판정을 통합 검증 기록에 남겨 verify_gate·핸드오프가 같은 파일을 읽게 함.
      try {
        const reportDir = path.join(unityProjectRoot,
          "Docs", "Workflow", "Local", "artifacts", "harness");
        await fs.promises.mkdir(reportDir, { recursive: true });
        await fs.promises.appendFile(
          path.join(reportDir, "verification-report.jsonl"),
          JSON.stringify({
            ts: new Date().toISOString(),
            cmd: `e2e unity run ${run.report.run.id}`,
            verdict: sealed.status,
            passed: sealed.status === "PASS",
            runId: sealed.runId
          }) + "\n");
      } catch (error) {
        process.stderr.write(
          `통합 검증 리포트 기록 실패(판정 무영향): ${(error as Error).message}\n`);
      }

      return [
        { label: "sealed run", text: `${sealed.runId} → ${sealed.status}` },
        { label: "sealed path", text: sealed.sealedPath }
      ];
    }
  };
}

// e2e RunStatus → boogle RunStatus.
// 통과했지만 visual-review 태그가 붙은 테스트는 사람 검토가 남아 있으므로
// 이진 PASS로 봉인하지 않고 MANUAL_REVIEW_REQUIRED로 낮춘다(기존 러너 계약).
const MANUAL_REVIEW_TAGS = new Set(["visual-review", "manual-review"]);

function mapRunStatus(run: FinishedRun): string {
  if (run.status === "passed" && needsManualReview(run)) {
    return "MANUAL_REVIEW_REQUIRED";
  }
  switch (run.status) {
    case "passed": return "PASS";
    case "failed": return "FAIL";
    case "blocked": return "BLOCKED";
    case "interrupted": return "CANCELLED";
    default: return "INFRA_ERROR";
  }
}

interface ReportResultEntry { status?: string; tags?: string[] }

function needsManualReview(run: FinishedRun): boolean {
  const results = (run.report as { run?: { results?: ReportResultEntry[] } })
    .run?.results;
  return Array.isArray(results) && results.some((entry) =>
    entry?.status === "passed" &&
    Array.isArray(entry.tags) &&
    entry.tags.some((tag) => MANUAL_REVIEW_TAGS.has(tag)));
}

/** report·junit·trace·attempt 산출물을 artifacts/에 평탄화해 모으고 매니페스트를 돌려준다. */
async function stageArtifacts(
  run: FinishedRun,
  artifactDir: string
): Promise<{ fileName: string; kind: "capture" | "video" | "output" | "trace" }[]> {
  const manifest: { fileName: string; kind: "capture" | "video" | "output" | "trace" }[] = [];
  const used = new Set<string>();
  const seenHashes = new Set<string>();
  const push = async (source: string, kind: "capture" | "video" | "output" | "trace") => {
    if (manifest.length >= MAX_ARTIFACTS) return;
    // 내용이 같으면 sha256이 같아 SDK가 중복 제출로 거부한다 — 먼저 해시로 걸러낸다.
    const digest = await hashOptionalFile(source);
    if (digest === "missing" || seenHashes.has(digest)) return;
    seenHashes.add(digest);
    const base = path.basename(source).replace(/[^A-Za-z0-9._-]/g, "_").replace(/^[^A-Za-z0-9]+/, "");
    if (!base) return;
    // 128자 절단은 stem에만 적용하고 확장자는 보존한다 — 확장자가 잘리면
    // SDK의 확장자 검증에서 거부되고, 충돌 접미사까지 잘리면 무한 루프다.
    const ext = path.extname(base);
    const stem = path.basename(base, ext);
    let fileName = `${stem.slice(0, 128 - ext.length)}${ext}`;
    let counter = 1;
    while (used.has(fileName)) {
      const suffix = `-${counter++}`;
      fileName = `${stem.slice(0, 128 - ext.length - suffix.length)}${suffix}${ext}`;
    }
    used.add(fileName);
    await fs.promises.copyFile(source, path.join(artifactDir, fileName));
    manifest.push({ fileName, kind });
  };

  if (run.reportPath) await push(run.reportPath, "output");
  const junitPath = path.join(path.dirname(run.artifactsRoot), "junit.xml");
  await push(junitPath, "output").catch(() => {});
  for (const tracePath of run.traces.values()) {
    await push(path.resolve(run.projectRoot, tracePath), "trace").catch(() => {});
  }
  await walkArtifacts(run.artifactsRoot, push);
  return manifest;
}

async function walkArtifacts(
  root: string,
  push: (source: string, kind: "capture" | "video" | "output" | "trace") => Promise<void>
): Promise<void> {
  let entries: fs.Dirent[];
  try {
    entries = await fs.promises.readdir(root, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const full = path.join(root, entry.name);
    if (entry.isDirectory()) {
      await walkArtifacts(full, push);
    } else if (entry.isFile()) {
      const kind = ARTIFACT_KIND_BY_EXT.get(path.extname(entry.name).toLowerCase());
      if (kind) await push(full, kind).catch(() => {});
    }
  }
}

function collectEnvironment(unityProjectRoot: string): Record<string, string> {
  const env: Record<string, string> = {
    nodeVersion: process.versions.node,
    os: `${process.platform} ${process.arch}`
  };
  try {
    const versionFile = path.join(unityProjectRoot, "ProjectSettings", "ProjectVersion.txt");
    const text = fs.readFileSync(versionFile, "utf8");
    env.unityVersion = text.match(/^m_EditorVersion:\s*(\S+)/m)?.[1] ?? "unknown";
  } catch {
    env.unityVersion = "unknown";
  }
  try {
    env.gitRevision = execFileSync("git", ["rev-parse", "HEAD"], {
      cwd: unityProjectRoot, encoding: "utf8"
    }).trim();
    const status = execFileSync("git", ["status", "--porcelain"], {
      cwd: unityProjectRoot, encoding: "utf8"
    });
    env.workingTreeStatusSha256 = createHash("sha256").update(status).digest("hex");
  } catch {
    // git 정보가 없어도 봉인은 진행한다.
  }
  return env;
}

function findUnityProjectRoot(from: string): string {
  // 이 파일은 Assets/_Project/Tools/MainRecordingSettings/tests-e2e/unity 아래에 있다.
  return path.resolve(from, "..", "..", "..", "..");
}

async function hashOptionalFile(filePath: string | undefined): Promise<string> {
  if (!filePath) return "missing";
  try {
    const hash = createHash("sha256");
    for await (const chunk of fs.createReadStream(filePath)) hash.update(chunk);
    return hash.digest("hex");
  } catch {
    return "missing";
  }
}
