import { spawn } from "node:child_process";
import { readFile, mkdtemp, mkdir, rm, stat, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const sdkCli = path.join(
  projectRoot,
  "Assets/_Project/Tools/MainRecordingSettings/node_modules/boogle-sdk/dist/cli.js"
);
const evidenceRoot = path.join(projectRoot, "Docs/Workflow/Local/evidence/boogle");
const defaultTestFilter =
  "Tests.Editor.FBXImporter.FbxPlaybackSmokeAutomationStoreTests.Given_TemporaryProjectRoot_When_CreatingStore_Then_UsesRuntimeArtifactPaths";

async function main() {
  const [editorPath, testFilter = defaultTestFilter] = process.argv.slice(2);
  if (!editorPath || !path.isAbsolute(editorPath) || !testFilter) {
    throw new Error("사용법: node Tools/Boogle/run-unity-test.mjs <Unity.exe 절대 경로> [테스트 전체 이름]");
  }
  if (Number(process.versions.node.split(".")[0]) !== 24) {
    throw new Error("Node.js 24가 필요합니다.");
  }
  if (!(await stat(editorPath)).isFile() || !(await stat(sdkCli)).isFile()) {
    throw new Error("Unity 실행 파일 또는 설치된 Boogle SDK를 찾을 수 없습니다.");
  }

  const projectVersionText = await readFile(
    path.join(projectRoot, "ProjectSettings/ProjectVersion.txt"),
    "utf8"
  );
  const unityVersion = projectVersionText.match(/^m_EditorVersion:\s*(\S+)/m)?.[1];
  if (!unityVersion) {
    throw new Error("프로젝트의 Unity 버전을 확인할 수 없습니다.");
  }

  const config = {
    protocolVersion: "0.1.0",
    projectId: "f2f44dc8-83ef-46d0-9d26-b0e52d1c4d20",
    adapter: { id: "unity", version: "0.1.0" },
    testPack: { id: "fbx2vmd-unity-test", version: "0.1.0" },
    retries: 0,
    unity: {
      editorPath,
      projectPath: projectRoot,
      testPlatform: "EditMode",
      testFilter,
      timeoutMs: 600000
    },
    inputConditions: { testCaseId: "SDK-CONNECTION-01", testFilter },
    environmentConditions: { unityVersion, nodeVersion: process.versions.node }
  };

  const tempDirectory = await mkdtemp(path.join(tmpdir(), "fbx2vmd-boogle-"));
  try {
    const configPath = path.join(tempDirectory, "unity-test.json");
    await writeFile(configPath, JSON.stringify(config), "utf8");
    await mkdir(evidenceRoot, { recursive: true });
    const child = spawn(
      process.execPath,
      [sdkCli, "run", configPath, "--data-root", evidenceRoot],
      { cwd: projectRoot, stdio: "inherit", windowsHide: true }
    );
    process.exitCode = await new Promise((resolve, reject) => {
      child.once("error", reject);
      child.once("close", (code) => resolve(code ?? 2));
    });
  } finally {
    const absolute = path.resolve(tempDirectory);
    if (path.dirname(absolute) !== path.resolve(tmpdir()) ||
        !path.basename(absolute).startsWith("fbx2vmd-boogle-")) {
      throw new Error("Unity 테스트 임시 설정 폴더 범위를 확인할 수 없습니다.");
    }
    await rm(absolute, { recursive: true, force: true });
  }
}

try {
  await main();
} catch (error) {
  process.stderr.write(`${error.message}\n`);
  process.exitCode = 2;
}
