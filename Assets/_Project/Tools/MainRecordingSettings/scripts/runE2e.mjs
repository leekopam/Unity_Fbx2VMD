import { spawn } from "node:child_process";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

// e2e CLI 래퍼 — 텔레메트리를 환경에 관계없이 끄고 인자를 그대로 전달한다.
process.env.E2E_TELEMETRY_DISABLED ??= "1";

const require = createRequire(import.meta.url);
// exports 맵에 없는 bin 경로라 메인 엔트리 위치에서 dist/cli로 내려간다.
const e2eBin = path.join(path.dirname(require.resolve("e2e")), "cli", "bin.js");
const __dirname = path.dirname(fileURLToPath(import.meta.url));

const child = spawn(process.execPath, [e2eBin, ...process.argv.slice(2)], {
  cwd: path.resolve(__dirname, ".."),
  env: process.env,
  stdio: "inherit",
  windowsHide: false
});

child.on("exit", (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
    return;
  }

  process.exit(code ?? 1);
});

child.on("error", (error) => {
  console.error(error);
  process.exit(1);
});
