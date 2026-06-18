import assert from "node:assert/strict";
import test from "node:test";

import {
  createFbxFileDialogOptions,
  extractSelectedFbxPath
} from "../electron/fileDialog.js";

test("createFbxFileDialogOptions limits selection to a single FBX file", () => {
  assert.deepEqual(createFbxFileDialogOptions(), {
    title: "FBX 파일 선택",
    properties: ["openFile"],
    filters: [
      { name: "FBX 파일", extensions: ["fbx"] },
      { name: "모든 파일", extensions: ["*"] }
    ]
  });
});

test("extractSelectedFbxPath returns the first selected path", () => {
  assert.equal(
    extractSelectedFbxPath({
      canceled: false,
      filePaths: ["C:/Motion/sample.fbx"]
    }),
    "C:/Motion/sample.fbx"
  );
});

test("extractSelectedFbxPath returns empty string when canceled", () => {
  assert.equal(
    extractSelectedFbxPath({
      canceled: true,
      filePaths: ["C:/Motion/sample.fbx"]
    }),
    ""
  );
});
