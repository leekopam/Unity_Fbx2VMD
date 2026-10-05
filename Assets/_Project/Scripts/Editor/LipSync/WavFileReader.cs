using System;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// Assets 밖의 WAV 파일을 AudioClip으로 읽는다(PCM16/float32, RIFF/WAVE만).
    /// 분리 결과물이 프로젝트 폴더 밖에 놓여도 바로 립싱크 베이크에 쓸 수 있게 한다.
    /// </summary>
    public static class WavFileReader
    {
        /// <summary>성공 시 AudioClip, 실패 시 error에 한국어 설명.</summary>
        public static AudioClip Load(string path, out string error)
        {
            error = string.Empty;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                return Parse(bytes, Path.GetFileNameWithoutExtension(path), out error);
            }
            catch (Exception e)
            {
                error = $"WAV 읽기 실패: {e.Message}";
                return null;
            }
        }

        /// <summary>순수 바이트 파서 — 테스트 가능.</summary>
        public static AudioClip Parse(byte[] bytes, string clipName, out string error)
        {
            error = string.Empty;
            if (bytes == null || bytes.Length < 44
                || BitConverter.ToInt32(bytes, 0) != 0x46464952 // "RIFF"
                || BitConverter.ToInt32(bytes, 8) != 0x45564157) // "WAVE"
            {
                error = "RIFF/WAVE 형식이 아닙니다.";
                return null;
            }

            int offset = 12;
            int channels = 0;
            int frequency = 0;
            int bitsPerSample = 0;
            int formatTag = 0; // 1=PCM, 3=float
            byte[] data = null;

            // 청크를 순회해 fmt/data를 찾는다(중간 청크 건너뜀).
            while (offset + 8 <= bytes.Length)
            {
                int chunkId = BitConverter.ToInt32(bytes, offset);
                int chunkSize = BitConverter.ToInt32(bytes, offset + 4);
                int body = offset + 8;
                if (chunkSize < 0)
                {
                    break; // 음수 크기는 손상 파일 — offset 정체로 인한 무한루프 방지
                }
                if (chunkSize > bytes.Length - body)
                {
                    // body+chunkSize 대신 이 형태로 비교해 int 오버플로를 피한다.
                    chunkSize = bytes.Length - body;
                }

                if (chunkId == 0x20746D66 && chunkSize >= 16) // "fmt "
                {
                    formatTag = BitConverter.ToInt16(bytes, body);
                    channels = BitConverter.ToInt16(bytes, body + 2);
                    frequency = BitConverter.ToInt32(bytes, body + 4);
                    bitsPerSample = BitConverter.ToInt16(bytes, body + 14);
                    // WAVE_FORMAT_EXTENSIBLE(0xFFFE) — SubFormat GUID 앞 2바이트가 실제 포맷
                    if (formatTag == unchecked((short)0xFFFE) && chunkSize >= 40)
                    {
                        formatTag = BitConverter.ToInt16(bytes, body + 24);
                    }
                }
                else if (chunkId == 0x61746164) // "data"
                {
                    data = new byte[chunkSize];
                    Array.Copy(bytes, body, data, 0, chunkSize);
                }
                offset = body + chunkSize + (chunkSize & 1); // 청크는 2바이트 정렬
            }

            if (data == null || channels <= 0 || frequency <= 0)
            {
                error = "fmt/data 청크가 없거나 손상됐습니다.";
                return null;
            }

            int frameCount;
            float[] samples;
            if (formatTag == 1 && bitsPerSample == 16)
            {
                frameCount = data.Length / (2 * channels);
                samples = new float[frameCount * channels];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = BitConverter.ToInt16(data, i * 2) / 32768f;
                }
            }
            else if (formatTag == 3 && bitsPerSample == 32)
            {
                frameCount = data.Length / (4 * channels);
                samples = new float[frameCount * channels];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = BitConverter.ToSingle(data, i * 4);
                }
            }
            else
            {
                error = $"지원하지 않는 WAV 포맷(format=0x{formatTag & 0xFFFF:X4}, bits={bitsPerSample}). PCM16 또는 float32만 지원합니다.";
                return null;
            }

            var clip = AudioClip.Create(clipName, frameCount, channels, frequency, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
