import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import { copyFile, lstat, mkdir, readFile, readdir, rm, writeFile } from "node:fs/promises";
import path from "node:path";

const artifactKinds = new Map([
  [".png", "capture"], [".mp4", "video"], [".log", "trace"],
  [".json", "output"], [".csv", "output"]
]);
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

async function hashFile(file) {
  const hash = createHash("sha256");
  for await (const chunk of createReadStream(file)) hash.update(chunk);
  return hash.digest("hex");
}

export async function collectProductArtifacts(evidenceRoot, temporaryPath, runId,
  collectSubmittedArtifacts, recordingsRoot = null) {
  if (!uuid.test(runId)) throw new Error("SDK 실행 ID가 올바르지 않습니다.");
  const families = (await readdir(evidenceRoot, { withFileTypes: true }))
    .filter((entry) => entry.isDirectory() && entry.name !== "projects");
  const files = [];
  const videoFiles = new Set();
  const requestIds = new Set();
  async function walk(directory) {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) await walk(file);
      else if (entry.isFile() && artifactKinds.has(path.extname(entry.name).toLowerCase())) {
        files.push(file);
        if (entry.name === "manifest.json") {
          const contents = await readFile(file, "utf8");
          for (const match of contents.matchAll(/"(?:requestId|request_id)"\s*:\s*"([0-9a-f-]{36})"/gi)) {
            if (uuid.test(match[1])) requestIds.add(match[1]);
          }
        }
        if (recordingsRoot && entry.name === "state.json") {
          const relative = path.relative(path.join(evidenceRoot, "product-ui"), file);
          if (relative && !relative.startsWith("..") && !path.isAbsolute(relative)) {
            const state = JSON.parse(await readFile(file, "utf8"));
            if (state.status === "manual_review_required") {
              const prefix = "FBX 모션 영상 저장 완료: ";
              if (typeof state.video_result_message !== "string" ||
                  !state.video_result_message.startsWith(prefix))
                throw new Error("F15 녹화 MP4 경로가 없습니다.");
              const videoFile = path.resolve(state.video_result_message.slice(prefix.length));
              const videoRelative = path.relative(recordingsRoot, videoFile);
              if (!videoRelative || videoRelative.startsWith("..") ||
                  path.isAbsolute(videoRelative) || path.extname(videoFile).toLowerCase() !== ".mp4")
                throw new Error("F15 녹화 MP4가 프로젝트 Recordings 밖에 있습니다.");
              const info = await lstat(videoFile);
              if (!info.isFile() || info.size === 0)
                throw new Error("F15 녹화 MP4가 비어 있거나 일반 파일이 아닙니다.");
              files.push(videoFile);
              videoFiles.add(videoFile);
            }
          }
        }
      }
    }
  }
  async function collectId(id) {
    for (const family of families) {
      const directory = path.join(evidenceRoot, family.name, id);
      try {
        if ((await lstat(directory)).isDirectory()) await walk(directory);
      } catch (error) {
        if (error.code !== "ENOENT") throw error;
      }
    }
  }
  await collectId(runId);
  for (const id of requestIds) await collectId(id);

  const submissionRoot = path.join(temporaryPath, "artifact-submission");
  await mkdir(path.join(submissionRoot, "artifacts"), { recursive: true });
  try {
    const artifacts = [];
    const sources = [];
    const submitted = new Set();
    for (const file of files) {
      const extension = path.extname(file).toLowerCase();
      const fileName = `artifact-${String(artifacts.length + 1).padStart(3, "0")}${extension}`;
      const stagedFile = path.join(submissionRoot, "artifacts", fileName);
      await copyFile(file, stagedFile);
      const fingerprint = `${await hashFile(stagedFile)}${extension}`;
      if (submitted.has(fingerprint)) {
        await rm(stagedFile);
        continue;
      }
      if (artifacts.length === 255) throw new Error("SDK artifact 256개 제한을 초과했습니다.");
      submitted.add(fingerprint);
      artifacts.push({ fileName, kind: artifactKinds.get(extension) });
      sources.push({ sdkPath: `artifacts/${fingerprint}`,
        source: videoFiles.has(file)
          ? `Recordings/${path.relative(recordingsRoot, file).replaceAll(path.sep, "/")}`
          : path.relative(evidenceRoot, file).replaceAll(path.sep, "/") });
    }
    if (sources.length) {
      await writeFile(path.join(submissionRoot, "artifacts", "source-map.json"),
        JSON.stringify({ sources }));
      artifacts.push({ fileName: "source-map.json", kind: "output" });
    }
    await writeFile(path.join(submissionRoot, "artifacts.json"), JSON.stringify({ artifacts }));
    return await collectSubmittedArtifacts(submissionRoot, temporaryPath);
  } finally {
    await rm(submissionRoot, { recursive: true, force: true });
  }
}
