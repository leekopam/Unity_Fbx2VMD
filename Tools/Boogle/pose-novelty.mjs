import { lstat, readdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

// Phase P 자체 자세 사전 — 근육 공간 kNN 거리로 자세 낯섦(pose_novelty)과
// 프레임 전이 이탈(transition_novelty)을 점수화함. 외부 가중치 없이
// 프로젝트가 보유한 소스 모션 코퍼스가 곧 "자연 자세" 분포임.
const corpusLimit = 60000;
const defaultK = 5;

export function euclidean(a, b) {
  let sum = 0;
  for (let index = 0; index < a.length; index++) {
    const d = a[index] - b[index];
    sum += d * d;
  }
  return Math.sqrt(sum);
}

// k개 최근접 평균 거리 — Pose-NDF의 kNN 근사를 근육 공간에 적용한 형태.
export function knnMeanDistance(vector, corpus, k = defaultK) {
  const distances = [];
  for (const item of corpus) distances.push(euclidean(vector, item));
  distances.sort((a, b) => a - b);
  const count = Math.min(k, distances.length);
  if (!count) return null;
  let sum = 0;
  for (let index = 0; index < count; index++) sum += distances[index];
  return sum / count;
}

export function percentile(values, fraction) {
  const sorted = values.filter(Number.isFinite).sort((a, b) => a - b);
  return sorted.length ? sorted[Math.floor((sorted.length - 1) * fraction)] : null;
}

export function parseMuscleLines(text) {
  return String(text).split(/\r?\n/).filter(Boolean).map(line => {
    const parsed = JSON.parse(line);
    if (!Array.isArray(parsed.muscles) || !Number.isInteger(parsed.frame))
      throw new Error("pose-muscles 행에 frame/muscles가 없음");
    return { frame: parsed.frame, muscles: parsed.muscles.map(Number) };
  });
}

// 코퍼스가 크면 균등 스트라이드로 줄여 브루트포스 비용을 상한 안에 둠.
export function subsample(vectors, limit = corpusLimit) {
  if (vectors.length <= limit) return vectors;
  const stride = vectors.length / limit;
  const picked = [];
  for (let index = 0; index < limit; index++)
    picked.push(vectors[Math.floor(index * stride)]);
  return picked;
}

// transition_novelty: 코퍼스의 프레임간 변화량 분포를 기준으로 대상 변화량이
// 몇 백분위인지(0~1). 시퀀스 경계(프레임 불연속)는 건너뜀.
export function scoreTransitions(frames, corpusDeltaP95) {
  const out = new Map();
  for (let index = 1; index < frames.length; index++) {
    if (frames[index].frame !== frames[index - 1].frame + 1) continue;
    const delta = euclidean(frames[index].muscles, frames[index - 1].muscles);
    out.set(frames[index].frame,
      corpusDeltaP95 > 0 ? delta / corpusDeltaP95 : null);
  }
  return out;
}

export function corpusDeltaPercentile(files) {
  const deltas = [];
  for (const frames of files) {
    for (let index = 1; index < frames.length; index++) {
      if (frames[index].frame !== frames[index - 1].frame + 1) continue;
      deltas.push(euclidean(frames[index].muscles, frames[index - 1].muscles));
    }
  }
  return percentile(deltas, 0.95);
}

export async function loadCorpus(directory, limit = corpusLimit) {
  const vectors = [];
  const sequences = [];
  const entries = await readdir(directory);
  // 증거 폴더에는 joint-positions.jsonl 등 다른 스키마의 jsonl이 같이 있으므로
  // 근육 덤프 파일명만 코퍼스로 받음.
  for (const entry of entries.filter(name => name.includes("muscle") &&
      name.endsWith(".jsonl"))) {
    const frames = parseMuscleLines(
      await readFile(path.join(directory, entry), "utf8"));
    sequences.push(frames);
    for (const frame of frames) vectors.push(frame.muscles);
  }
  return { vectors: subsample(vectors, limit), sequences };
}

// 대상 클립 각 프레임의 점수를 {frame → {pose, transition}}로 돌려줌.
export function scoreFrames(target, corpus, corpusP95) {
  const transitions = scoreTransitions(target, corpusP95);
  const scores = new Map();
  for (const item of target) {
    scores.set(item.frame, {
      pose_novelty: knnMeanDistance(item.muscles, corpus.vectors),
      transition_novelty: transitions.get(item.frame) ?? null,
    });
  }
  return scores;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [corpusDir, targetPath, outputPath] = process.argv.slice(2);
  if (!corpusDir || !targetPath || !outputPath) {
    console.error("사용법: pose-novelty.mjs <코퍼스 jsonl 폴더> <대상 pose-muscles.jsonl> <출력 csv>");
    process.exit(2);
  }
  const outputStat = await lstat(outputPath).catch(error => {
    if (error.code === "ENOENT") return null;
    throw error;
  });
  if (outputStat?.isSymbolicLink())
    throw new Error("점수 파일은 심볼릭 링크일 수 없음");
  const corpus = await loadCorpus(corpusDir);
  if (!corpus.vectors.length) throw new Error("코퍼스가 비어 있음");
  const corpusP95 = corpusDeltaPercentile(corpus.sequences);
  const target = parseMuscleLines(await readFile(targetPath, "utf8"));
  const scores = scoreFrames(target, corpus, corpusP95);
  const lines = ["frame,pose_novelty,transition_novelty"];
  for (const item of target) {
    const score = scores.get(item.frame);
    lines.push([item.frame,
      score.pose_novelty === null ? "" : score.pose_novelty.toFixed(6),
      score.transition_novelty === null || score.transition_novelty === undefined
        ? "" : score.transition_novelty.toFixed(6)].join(","));
  }
  await writeFile(outputPath, lines.join("\n") + "\n", "utf8");
  process.stdout.write(`${JSON.stringify({ frames: target.length,
    corpus_vectors: corpus.vectors.length, resultPath: outputPath })}\n`);
}
