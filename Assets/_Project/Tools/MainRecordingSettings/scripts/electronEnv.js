export function createElectronRuntimeEnv(sourceEnv = process.env) {
  const env = { ...sourceEnv };
  delete env.ELECTRON_RUN_AS_NODE;
  return env;
}
