const { contextBridge, ipcRenderer } = require("electron");

// 첫 렌더러 요청부터 소유한 브리지만 사용하도록 문서 스크립트보다 먼저 주입함.
const bridgeConfig = ipcRenderer.sendSync("settings:get-bridge-config");
if (!bridgeConfig) throw new Error("설정 브리지가 준비되지 않았습니다.");
contextBridge.exposeInMainWorld("mainRecordingSettingsBridgeConfig", bridgeConfig);

contextBridge.exposeInMainWorld("settingsShell", {
  chooseFbxFile: () => ipcRenderer.invoke("settings:choose-fbx-file"),
  chooseAudioFile: () => ipcRenderer.invoke("settings:choose-audio-file"),
  chooseLyricsFile: () => ipcRenderer.invoke("settings:choose-lyrics-file"),
  chooseDirectory: () => ipcRenderer.invoke("settings:choose-directory"),
  chooseVrmFile: () => ipcRenderer.invoke("settings:choose-vrm-file"),
  openPath: (targetPath) => ipcRenderer.invoke("settings:open-path", targetPath),
  readTextPreview: (filePath, maxBytes) =>
    ipcRenderer.invoke("settings:read-text-preview", filePath, maxBytes),
  getWorkbenchUrl: () => ipcRenderer.invoke("boogle:get-workbench-url")
});
