const { contextBridge, ipcRenderer } = require("electron");

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
