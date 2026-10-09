import { readdir, readFile, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const evidenceRoot = path.resolve(toolDir, "../../Docs/Workflow/Local/evidence/boogle");
const refRoot = path.resolve(toolDir, "../../Docs/Workflow/Local/ReferenceAnalysis");
const findingsRoot = path.resolve(toolDir, "../../Docs/Workflow/Local/artifacts/harness");
const outputPath = path.resolve(findingsRoot, "baseline-inventory-20261009.json");

// Phase 0 기준선: 사람 라벨 정답지·증거 매트릭스·영상 산출물을 한 번에 집계함.
async function walk(dir, acc = []) {
  let entries;
  try { entries = await readdir(dir, { withFileTypes: true }); }
  catch { return acc; }
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) await walk(full, acc);
    else acc.push(full);
  }
  return acc;
}

function parseCsvLine(line) {
  return line.split(",").map((cell) => cell.replace(/^"|"$/g, ""));
}

async function inventoryLabels(files) {
  const vocab = {}, sides = {}, reviewers = {};
  let rows = 0, filled = 0;
  const perFile = [];
  for (const file of files) {
    const lines = (await readFile(file, "utf8"))
      .split(/\r?\n/).filter(Boolean).slice(1);
    let fileFilled = 0;
    for (const line of lines) {
      const c = parseCsvLine(line);
      rows++;
      if (!c[3]) continue;
      filled++; fileFilled++;
      const key = `${c[3]}|${c[4] || ""}`;
      vocab[key] = (vocab[key] || 0) + 1;
      sides[c[2]] = (sides[c[2]] || 0) + 1;
      reviewers[c[5] || "(none)"] = (reviewers[c[5] || "(none)"] || 0) + 1;
    }
    perFile.push({ run: path.relative(evidenceRoot, path.dirname(file)),
      rows: lines.length, filled: fileFilled });
  }
  return { files: files.length, rows, filled, vocab, sides, reviewers, perFile };
}

async function evidenceMatrix(allFiles) {
  const markers = { frames: "all-frames.csv", template: "human-labels-template.csv",
    labels: "human-labels.csv", jev: "jev-review.json",
    events: "contact-events.json" };
  const runs = new Map();
  for (const file of allFiles) {
    for (const [key, name] of Object.entries(markers)) {
      if (!file.endsWith(name)) continue;
      const run = path.relative(evidenceRoot, path.dirname(file));
      if (!runs.has(run)) runs.set(run, {});
      runs.get(run)[key] = true;
    }
  }
  return [...runs.entries()].map(([run, flags]) => ({ run, ...flags }))
    .sort((a, b) => a.run.localeCompare(b.run));
}

async function inventoryVideos() {
  const files = await walk(refRoot);
  const intervals = files.filter((f) => f.endsWith("contact_intervals.csv"));
  const maps = files.filter((f) => f.endsWith("correction_map.csv"));
  const poses = files.filter((f) => f.endsWith("pose2d.jsonl"));
  const runs = [];
  for (const file of intervals) {
    const lines = (await readFile(file, "utf8"))
      .split(/\r?\n/).filter(Boolean);
    const dir = path.basename(path.dirname(file));
    const sameDirMaps = maps.filter((m) => path.dirname(m) === path.dirname(file));
    runs.push({ dir, intervals: lines.length - 1,
      hasCorrectionMap: sameDirMaps.length > 0 });
  }
  return { intervalFiles: intervals.length, mapFiles: maps.length,
    poseFiles: poses.length, runs };
}

async function collectDefectCases() {
  const files = (await walk(findingsRoot))
    .filter((f) => /review-findings-.*\.md$/.test(f));
  const pattern = /발|foot|무릎|knee|관절|joint|침투|penetrat|미끄|slide|slip|지터|jitter|자세|pose|균형|balance/i;
  const hits = [];
  for (const file of files) {
    const text = await readFile(file, "utf8");
    const matched = text.split(/\r?\n/)
      .filter((l) => pattern.test(l) && /이상|파손|어색|문제|anomal|broken|bug/i.test(l));
    if (matched.length) hits.push({ file: path.basename(file), hits: matched.length });
  }
  return hits;
}

const allEvidence = await walk(evidenceRoot);
const result = {
  generated_at: new Date().toISOString(),
  api_key_present: Boolean(process.env.TYPESAFE_API_KEY),
  labels: await inventoryLabels(
    allEvidence.filter((f) => f.endsWith("human-labels.csv"))),
  matrix: await evidenceMatrix(allEvidence),
  videos: await inventoryVideos(),
  defectCaseSources: await collectDefectCases(),
};

await writeFile(outputPath, JSON.stringify(result, null, 2) + "\n", "utf8");
const { labels, videos, matrix } = result;
console.log(`labels: ${labels.files} files, ${labels.rows} rows, ${labels.filled} filled`);
console.log(`vocab: ${JSON.stringify(labels.vocab)}`);
console.log(`runs: ${matrix.length} (frames=${matrix.filter((r) => r.frames).length},` +
  ` labels=${matrix.filter((r) => r.labels).length}, jev=${matrix.filter((r) => r.jev).length})`);
console.log(`videos: ${videos.intervalFiles} contact runs, ${videos.mapFiles} maps`);
console.log(`api_key: ${result.api_key_present ? "present" : "absent (dry-run 경로)"}`);
console.log(`wrote ${outputPath}`);
