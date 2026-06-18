import { runBridgeE2e } from "./bridgeE2e.js";

try {
  const result = await runBridgeE2e();
  console.log(JSON.stringify(result, null, 2));
  process.exitCode = result.ok ? 0 : 1;
} catch (error) {
  console.error(error.stack ?? error.message ?? String(error));
  process.exitCode = 1;
}
