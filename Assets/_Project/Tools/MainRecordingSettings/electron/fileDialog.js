export function createFbxFileDialogOptions() {
  return {
    title: "FBX 파일 선택",
    properties: ["openFile"],
    filters: [
      { name: "FBX 파일", extensions: ["fbx"] },
      { name: "모든 파일", extensions: ["*"] }
    ]
  };
}

export function extractSelectedFbxPath(result) {
  if (result == null || result.canceled || !Array.isArray(result.filePaths)) {
    return "";
  }

  return typeof result.filePaths[0] === "string" ? result.filePaths[0] : "";
}

export function createAudioFileDialogOptions() {
  return {
    title: "오디오 파일 선택",
    properties: ["openFile"],
    filters: [
      { name: "오디오 파일", extensions: ["wav", "mp3", "flac", "m4a", "ogg"] },
      { name: "모든 파일", extensions: ["*"] }
    ]
  };
}

export function createVrmFileDialogOptions() {
  return {
    title: "VRM 파일 선택",
    properties: ["openFile"],
    filters: [
      { name: "VRM 파일", extensions: ["vrm"] },
      { name: "모든 파일", extensions: ["*"] }
    ]
  };
}

export function createLyricsFileDialogOptions() {
  return {
    title: "가사 파일 선택",
    properties: ["openFile"],
    filters: [
      { name: "가사 파일", extensions: ["txt", "lrc"] },
      { name: "모든 파일", extensions: ["*"] }
    ]
  };
}

export function createDirectoryDialogOptions() {
  return {
    title: "출력 폴더 선택",
    properties: ["openDirectory", "createDirectory"]
  };
}

export function extractSelectedPath(result) {
  if (result == null || result.canceled || !Array.isArray(result.filePaths)) {
    return "";
  }

  return typeof result.filePaths[0] === "string" ? result.filePaths[0] : "";
}
