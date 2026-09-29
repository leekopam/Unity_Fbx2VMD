import assert from "node:assert/strict";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { probeMp4VideoFrameCount } from "./mp4-probe.mjs";

function box(type, payload) {
  const head = Buffer.alloc(8);
  head.writeUInt32BE(payload.length + 8, 0);
  head.write(type, 4, "latin1");
  return Buffer.concat([head, payload]);
}

// vide 트랙(stsz)과 soun 트랙을 가진 최소 MP4를 메모리에서 만든다.
function fakeMp4(videoSamples, audioSamples = 0) {
  const stsz = Buffer.alloc(12);
  stsz.writeUInt32BE(videoSamples, 8);
  const videTrak = box("trak", Buffer.concat([
    box("mdia", Buffer.concat([
      box("hdlr", Buffer.concat([Buffer.alloc(8), Buffer.from("vide", "latin1")])),
      box("minf", box("stbl", box("stsz", stsz)))
    ]))
  ]));
  const sounStsz = Buffer.alloc(12);
  sounStsz.writeUInt32BE(audioSamples, 8);
  const sounTrak = box("trak", Buffer.concat([
    box("mdia", Buffer.concat([
      box("hdlr", Buffer.concat([Buffer.alloc(8), Buffer.from("soun", "latin1")])),
      box("minf", box("stbl", box("stsz", sounStsz)))
    ]))
  ]));
  return Buffer.concat([
    box("ftyp", Buffer.from("isom\0\0\0\0isom", "latin1")),
    box("moov", Buffer.concat([videTrak, sounTrak]))
  ]);
}

test("MP4 비디오 트랙 샘플 수를 읽고 비영상 파일을 거절한다", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "fbx2vmd-mp4-probe-"));
  try {
    const mp4 = path.join(root, "clip.mp4");
    await writeFile(mp4, fakeMp4(41, 200));
    assert.equal(await probeMp4VideoFrameCount(mp4), 41);

    const audioOnly = path.join(root, "audio.mp4");
    await writeFile(audioOnly,
      box("ftyp", Buffer.from("isom\0\0\0\0isom", "latin1")));
    await assert.rejects(probeMp4VideoFrameCount(audioOnly), /비디오 트랙/);

    const text = path.join(root, "not.mp4");
    await writeFile(text, "mp4가 아님");
    await assert.rejects(probeMp4VideoFrameCount(text), /비디오 트랙/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
