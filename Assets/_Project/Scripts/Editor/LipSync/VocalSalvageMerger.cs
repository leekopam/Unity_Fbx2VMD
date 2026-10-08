using System;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 정제된 리드 보컬의 과감쇠(무음 처리) 구간을 분리 보컬 스템으로 채운다.
    /// 체인 출력이 침묵인데 원 보컬에 소리가 남은 구간은 리드가 모델과 함께
    /// 지워진 것이므로, 보컬 스템을 감쇠 게인으로 그 구간만 메운다.
    /// </summary>
    public static class VocalSalvageMerger
    {
        /// <summary>윈도우 크기(프레임). 44.1kHz 기준 약 46ms.</summary>
        public const int WindowFrames = 2048;
        /// <summary>정제 출력이 이 RMS 이하면 침묵으로 본다(=-55dBFS).</summary>
        public const float CleanFloor = 0.0018f;
        /// <summary>보컬 스템이 이 RMS 이상이면 실제 내용으로 본다(=-40dBFS).</summary>
        public const float SalvageGate = 0.01f;
        /// <summary>채울 때 보컬 스템에 곱하는 게인 — 잔류 코러스를 억누른다.</summary>
        public const float SalvageGain = 0.6f;

        /// <summary>
        /// clean/salvage(채널 인터리브 float)를 머지해 새 버퍼를 반환한다.
        /// filledRatio에는 채워진 윈도우 비율이 담긴다. 길이가 다르면 짧은 쪽에 맞춘다.
        /// </summary>
        public static float[] MergeBuffers(float[] clean, float[] salvage,
            int channels, out float filledRatio)
        {
            int frames = Math.Min(clean.Length, salvage.Length) / channels;
            int windowSamples = WindowFrames * channels;
            var output = (float[])clean.Clone();
            int filled = 0;
            int windowCount = frames / WindowFrames;
            for (int w = 0; w < windowCount; w++)
            {
                int start = w * windowSamples;
                float cleanRms = Rms(clean, start, windowSamples);
                if (cleanRms >= CleanFloor)
                {
                    continue;
                }
                float salvageRms = Rms(salvage, start, windowSamples);
                if (salvageRms <= SalvageGate)
                {
                    continue;
                }
                // 경계 클릭 방지용으로 윈도우 양 끝 128프레임은 선형으로 페이드한다.
                FillWindow(output, salvage, start, windowSamples, channels);
                filled++;
            }
            filledRatio = windowCount > 0 ? (float)filled / windowCount : 0f;
            return output;
        }

        /// <summary>
        /// cleanPath의 침묵 구간을 salvagePath로 채워 outPath에 PCM16 WAV로 쓴다.
        /// 성공 시 outPath, 포맷 불일치/읽기 실패 시 null과 error 사유를 반환한다.
        /// Unity API를 쓰지 않아 정제 체인의 워커 스레드(Task.Run)에서 호출 가능하다.
        /// </summary>
        public static string Merge(string cleanPath, string salvagePath,
            string outPath, out float filledRatio, out string error)
        {
            filledRatio = 0f;
            error = string.Empty;
            if (!WavFileReader.TryLoadSamples(cleanPath, out float[] cleanData,
                    out int cleanCh, out int cleanFreq, out error))
            {
                return null;
            }
            if (!WavFileReader.TryLoadSamples(salvagePath, out float[] salvageData,
                    out int salvageCh, out int salvageFreq, out error))
            {
                return null;
            }
            if (cleanCh != salvageCh || cleanFreq != salvageFreq)
            {
                error = $"채널/샘플레이트 불일치({cleanCh}ch {cleanFreq}Hz vs "
                    + $"{salvageCh}ch {salvageFreq}Hz)";
                return null;
            }
            float[] merged = MergeBuffers(cleanData, salvageData,
                cleanCh, out filledRatio);
            try
            {
                WriteWav16(outPath, merged, cleanCh, cleanFreq);
            }
            catch (Exception e)
            {
                error = $"WAV 쓰기 실패: {e.Message}";
                return null;
            }
            return outPath;
        }

        private static float Rms(float[] samples, int start, int count)
        {
            double sum = 0;
            for (int i = start; i < start + count; i++)
            {
                sum += samples[i] * (double)samples[i];
            }
            return (float)Math.Sqrt(sum / count);
        }

        /// <summary>윈도우 구간에 salvage*게인을 쓰되 양 끝 128프레임은 선형 페이드.</summary>
        private static void FillWindow(float[] output, float[] salvage,
            int start, int length, int channels)
        {
            int fadeFrames = Math.Min(128, length / channels / 4);
            int fadeSamples = fadeFrames * channels;
            for (int i = 0; i < length; i++)
            {
                float ramp = 1f;
                if (i < fadeSamples)
                {
                    ramp = (float)i / fadeSamples;
                }
                else if (i >= length - fadeSamples)
                {
                    ramp = (float)(length - 1 - i) / fadeSamples;
                }
                output[start + i] = salvage[start + i] * SalvageGain * ramp;
            }
        }

        /// <summary>float 샘플을 PCM16 RIFF/WAVE로 쓴다(스테레오/모노 모두).</summary>
        private static void WriteWav16(string path, float[] samples,
            int channels, int frequency)
        {
            int dataSize = samples.Length * 2;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(0x46464952);            // "RIFF"
                writer.Write(36 + dataSize);
                writer.Write(0x45564157);            // "WAVE"
                writer.Write(0x20746D66);            // "fmt "
                writer.Write(16);
                writer.Write((short)1);              // PCM
                writer.Write((short)channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * 2);
                writer.Write((short)(channels * 2)); // block align
                writer.Write((short)16);             // bits
                writer.Write(0x61746164);            // "data"
                writer.Write(dataSize);
                for (int i = 0; i < samples.Length; i++)
                {
                    short v = (short)Mathf.Clamp(
                        Mathf.RoundToInt(samples[i] * 32767f), -32768, 32767);
                    writer.Write(v);
                }
            }
        }
    }
}
