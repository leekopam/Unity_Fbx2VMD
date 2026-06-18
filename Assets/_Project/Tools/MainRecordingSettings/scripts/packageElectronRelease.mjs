import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

export const SETTINGS_FOLDER_NAME = "MainRecordingSettings";
export const SETTINGS_EXECUTABLE_FILE_NAME = "Unity_Fbx2VMD_Settings.exe";
export const PACKAGE_MODE = "electron";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const defaultAppRoot = path.resolve(__dirname, "..");
const defaultProjectRoot = path.resolve(defaultAppRoot, "../../../..");
const defaultElectronDist = path.join(defaultAppRoot, "node_modules", "electron", "dist");
const defaultOutputDir = path.join(
  defaultProjectRoot,
  "Builds",
  "Local",
  "MainRecordingRelease",
  SETTINGS_FOLDER_NAME
);

const runtimeAppEntries = [
  "package.json",
  "build",
  "client",
  "electron",
  "server",
  "node_modules/ws"
];

export async function packageElectronRelease({
  appRoot = defaultAppRoot,
  electronDist = defaultElectronDist,
  outputDir = defaultOutputDir
} = {}) {
  const resolvedAppRoot = path.resolve(appRoot);
  const resolvedElectronDist = path.resolve(electronDist);
  const resolvedOutputDir = path.resolve(outputDir);
  const resourcesAppPath = path.join(resolvedOutputDir, "resources", "app");
  const archivePath = path.join(resolvedOutputDir, "resources", "app.asar");
  const executablePath = path.join(resolvedOutputDir, SETTINGS_EXECUTABLE_FILE_NAME);

  await assertDirectory(resolvedAppRoot, "settings app root");
  await assertDirectory(resolvedElectronDist, "Electron runtime dist");
  await assertFile(path.join(resolvedElectronDist, "electron.exe"), "Electron runtime executable");
  ensureSafeOutputDirectory(resolvedOutputDir, resolvedAppRoot);

  await fs.rm(resolvedOutputDir, { recursive: true, force: true });
  await fs.mkdir(resolvedOutputDir, { recursive: true });
  await copyDirectory(resolvedElectronDist, resolvedOutputDir);
  await fs.rename(path.join(resolvedOutputDir, "electron.exe"), executablePath);
  await fs.rm(path.join(resolvedOutputDir, "resources", "default_app.asar"), {
    force: true
  });

  await fs.rm(resourcesAppPath, { recursive: true, force: true });
  await fs.mkdir(resourcesAppPath, { recursive: true });

  const copiedAppEntries = [];
  for (const entry of runtimeAppEntries) {
    const source = path.join(resolvedAppRoot, ...entry.split("/"));
    const destination = path.join(resourcesAppPath, ...entry.split("/"));
    await assertPath(source, `runtime app entry ${entry}`);
    await copyPath(source, destination);
    copiedAppEntries.push(entry);
  }

  await createAsarArchive(resourcesAppPath, archivePath);

  return {
    packageMode: PACKAGE_MODE,
    requiredRuntimeCommands: 0,
    outputDir: resolvedOutputDir,
    executablePath,
    resourcesAppPath,
    archivePath,
    copiedAppEntries
  };
}

function parseArguments(argv) {
  const options = {};
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === "--output") {
      options.outputDir = argv[index + 1];
      index += 1;
      continue;
    }

    if (arg.startsWith("--output=")) {
      options.outputDir = arg.slice("--output=".length);
      continue;
    }

    if (arg === "--app-root") {
      options.appRoot = argv[index + 1];
      index += 1;
      continue;
    }

    if (arg.startsWith("--app-root=")) {
      options.appRoot = arg.slice("--app-root=".length);
      continue;
    }

    if (arg === "--electron-dist") {
      options.electronDist = argv[index + 1];
      index += 1;
      continue;
    }

    if (arg.startsWith("--electron-dist=")) {
      options.electronDist = arg.slice("--electron-dist=".length);
    }
  }

  return options;
}

async function copyPath(source, destination) {
  const stat = await fs.stat(source);
  if (stat.isDirectory()) {
    await copyDirectory(source, destination);
    return;
  }

  if (shouldSkipPath(source)) {
    return;
  }

  await fs.mkdir(path.dirname(destination), { recursive: true });
  await fs.copyFile(source, destination);
}

async function copyDirectory(sourceDirectory, destinationDirectory) {
  await fs.mkdir(destinationDirectory, { recursive: true });
  const entries = await fs.readdir(sourceDirectory, { withFileTypes: true });
  for (const entry of entries) {
    const source = path.join(sourceDirectory, entry.name);
    const destination = path.join(destinationDirectory, entry.name);
    if (shouldSkipPath(source)) {
      continue;
    }

    if (entry.isDirectory()) {
      await copyDirectory(source, destination);
      continue;
    }

    if (entry.isFile()) {
      await fs.copyFile(source, destination);
    }
  }
}

function shouldSkipPath(filePath) {
  return path.basename(filePath).endsWith(".meta");
}

async function createAsarArchive(sourceDirectory, archivePath) {
  const files = [];
  const header = { files: {} };
  await appendAsarDirectory({
    sourceDirectory,
    currentDirectory: sourceDirectory,
    headerDirectory: header,
    files
  });

  let offset = 0;
  for (const file of files) {
    file.header.offset = String(offset);
    file.header.size = file.buffer.byteLength;
    offset += file.buffer.byteLength;
  }

  const headerPickle = createPickleString(JSON.stringify(header));
  const sizePickle = createPickleUInt32(headerPickle.byteLength);
  await fs.mkdir(path.dirname(archivePath), { recursive: true });
  await fs.writeFile(archivePath, Buffer.concat([
    sizePickle,
    headerPickle,
    ...files.map((file) => file.buffer)
  ]));
}

async function appendAsarDirectory({
  sourceDirectory,
  currentDirectory,
  headerDirectory,
  files
}) {
  const entries = (await fs.readdir(currentDirectory, { withFileTypes: true }))
    .filter((entry) => !shouldSkipPath(entry.name))
    .sort((left, right) => left.name.localeCompare(right.name, "en"));

  for (const entry of entries) {
    const source = path.join(currentDirectory, entry.name);
    const relativePath = path.relative(sourceDirectory, source).replaceAll(path.sep, "/");
    if (entry.isDirectory()) {
      const child = { files: {} };
      headerDirectory.files[entry.name] = child;
      await appendAsarDirectory({
        sourceDirectory,
        currentDirectory: source,
        headerDirectory: child,
        files
      });
      continue;
    }

    if (entry.isFile()) {
      const fileHeader = {};
      headerDirectory.files[entry.name] = fileHeader;
      files.push({
        relativePath,
        header: fileHeader,
        buffer: await fs.readFile(source)
      });
    }
  }
}

function createPickleUInt32(value) {
  const buffer = Buffer.alloc(8);
  buffer.writeUInt32LE(4, 0);
  buffer.writeUInt32LE(value, 4);
  return buffer;
}

function createPickleString(value) {
  const stringBuffer = Buffer.from(value, "utf8");
  const unpaddedPayloadSize = 4 + stringBuffer.byteLength;
  const payloadSize = roundUpToInt32(4 + stringBuffer.byteLength);
  const buffer = Buffer.alloc(4 + payloadSize);
  buffer.writeUInt32LE(payloadSize, 0);
  buffer.writeUInt32LE(stringBuffer.byteLength, 4);
  stringBuffer.copy(buffer, 8);
  if (payloadSize < unpaddedPayloadSize) {
    throw new Error("Invalid ASAR pickle payload alignment.");
  }

  return buffer;
}

function roundUpToInt32(value) {
  return Math.ceil(value / 4) * 4;
}

async function assertDirectory(directoryPath, label) {
  const stat = await assertPath(directoryPath, label);
  if (!stat.isDirectory()) {
    throw new Error(`${label} is not a directory: ${directoryPath}`);
  }
}

async function assertFile(filePath, label) {
  const stat = await assertPath(filePath, label);
  if (!stat.isFile()) {
    throw new Error(`${label} is not a file: ${filePath}`);
  }
}

async function assertPath(filePath, label) {
  try {
    return await fs.stat(filePath);
  } catch (error) {
    if (error.code === "ENOENT") {
      throw new Error(`${label} does not exist: ${filePath}`);
    }

    throw error;
  }
}

function ensureSafeOutputDirectory(outputDir, appRoot) {
  const parsed = path.parse(outputDir);
  if (outputDir === parsed.root) {
    throw new Error(`Refusing to package into a filesystem root: ${outputDir}`);
  }

  const relativeToAppRoot = path.relative(appRoot, outputDir);
  if (relativeToAppRoot === "" || !relativeToAppRoot.startsWith("..")) {
    throw new Error(`Refusing to package into the source app root: ${outputDir}`);
  }
}

async function main() {
  const result = await packageElectronRelease(parseArguments(process.argv.slice(2)));
  console.log(JSON.stringify(result, null, 2));
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error.stack ?? error.message ?? String(error));
    process.exitCode = 1;
  });
}
