const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("settingsShell", {
  chooseFbxFile: () => ipcRenderer.invoke("settings:choose-fbx-file")
});
