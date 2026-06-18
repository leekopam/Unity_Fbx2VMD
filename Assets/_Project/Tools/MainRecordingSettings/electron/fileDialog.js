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
