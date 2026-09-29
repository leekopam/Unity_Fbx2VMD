import { readFile } from "node:fs/promises";

// MP4 박스 트리에서 비디오 트랙의 stsz sample_count를 읽어 녹화 프레임 수를 확인함.
// 외부 도구(ffprobe) 없이 계측-영상 프레임 대응을 검증하기 위한 최소 파서.
const CONTAINERS = new Set(["moov", "trak", "mdia", "minf", "stbl"]);
const VIDEO_HANDLER = "vide";

function* boxes(buffer, start, end) {
  let offset = start;
  while (offset + 8 <= end) {
    const size32 = buffer.readUInt32BE(offset);
    const type = buffer.toString("latin1", offset + 4, offset + 8);
    let headerSize = 8;
    let size = size32;
    if (size32 === 1) {
      if (offset + 16 > end) return;
      size = Number(buffer.readBigUInt64BE(offset + 8));
      headerSize = 16;
    } else if (size32 === 0) {
      size = end - offset;
    }
    if (size < headerSize || offset + size > end) return;
    yield { type, head: offset + headerSize, end: offset + size };
    offset += size;
  }
}

function findBox(buffer, start, end, type) {
  for (const box of boxes(buffer, start, end))
    if (box.type === type) return box;
  return null;
}

export async function probeMp4VideoFrameCount(filePath) {
  const buffer = await readFile(filePath);
  const counts = [];
  for (const top of boxes(buffer, 0, buffer.length)) {
    if (top.type !== "moov") continue;
    for (const trak of boxes(buffer, top.head, top.end)) {
      if (trak.type !== "trak") continue;
      const mdia = findBox(buffer, trak.head, trak.end, "mdia");
      const hdlr = mdia && findBox(buffer, mdia.head, mdia.end, "hdlr");
      if (!hdlr || buffer.toString("latin1", hdlr.head + 8, hdlr.head + 12) !== VIDEO_HANDLER)
        continue;
      const minf = findBox(buffer, mdia.head, mdia.end, "minf");
      const stbl = minf && findBox(buffer, minf.head, minf.end, "stbl");
      const stsz = stbl && findBox(buffer, stbl.head, stbl.end, "stsz");
      if (!stsz || stsz.end - stsz.head < 12)
        throw new Error(`MP4 비디오 트랙의 stsz 박스가 없습니다: ${filePath}`);
      // stsz: version/flags 4 + sample_size 4 + sample_count 4
      const sampleSize = buffer.readUInt32BE(stsz.head + 4);
      const sampleCount = buffer.readUInt32BE(stsz.head + 8);
      if (sampleSize === 0 && sampleCount === 0)
        throw new Error(`MP4 비디오 트랙의 샘플이 비어 있습니다: ${filePath}`);
      counts.push(sampleCount);
    }
  }
  if (counts.length === 0)
    throw new Error(`MP4 비디오 트랙을 찾지 못했습니다: ${filePath}`);
  return Math.max(...counts);
}
