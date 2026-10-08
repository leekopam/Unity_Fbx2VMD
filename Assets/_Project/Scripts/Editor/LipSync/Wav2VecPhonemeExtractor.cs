using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// wav2vec2-IPA 음소 추출기 실행 + 결과 CSV → BakedData 변환.
    /// MFCC 기반 uLipSync와 달리 음소 인식이라 코러스 잔류/노이즈에 강하다.
    /// 출력은 프레임별 모음 그룹 확률(a,i,u,e,o) + RMS 음량 CSV다.
    /// </summary>
    public static class Wav2VecPhonemeExtractor
    {
        /// <summary>음소 인식 언어. 어휘 문자 집합(가나/IPA/한글)은 스크립트가 자동 판별한다.</summary>
        public enum PhonemeLanguage { Japanese, English, Korean }
        /// <summary>프로젝트 루트 기준 추출 스크립트 상대 경로.</summary>
        public const string ScriptRelPath = "Tools/LipSync/phoneme_extract.py";
        public const string DefaultModel = "jonatasgrosman/wav2vec2-large-xlsr-53-japanese";

        /// <summary>언어별 검증된 wav2vec2 모델. 한국어는 한글 음절 어휘, 영어는 IPA 어휘 모델.</summary>
        public static string ModelFor(PhonemeLanguage lang)
        {
            switch (lang)
            {
                case PhonemeLanguage.English:
                    // espeak IPA 어휘 — AutoProcessor 대신 FeatureExtractor+vocab.json만 써서 phonemizer 불필요.
                    return "facebook/wav2vec2-lv-60-espeak-cv-ft";
                case PhonemeLanguage.Korean:
                    return "kresnik/wav2vec2-large-xlsr-korean";
                default:
                    return DefaultModel;
            }
        }

        /// <summary>추출 스크립트를 동기 실행해 CSV를 만든다. 성공 시 true.</summary>
        public static bool Extract(string pythonPath, string projectRoot,
            string wavPath, string outputCsv, string model,
            IReadOnlyDictionary<string, string> env,
            CancellationToken ct, Action<float> onProgress, out string error)
        {
            error = null;
            string script = Path.Combine(projectRoot, ScriptRelPath.Replace('/', '\\'));
            if (!File.Exists(script))
            {
                error = "추출 스크립트가 없습니다: " + script;
                return false;
            }
            // 상대 경로가 자식 프로세스 WorkingDirectory 기준으로 이중 해석되지 않게 정규화한다.
            wavPath = Path.GetFullPath(wavPath);
            outputCsv = Path.GetFullPath(outputCsv);
            string args = Quote(script)
                + " " + Quote(wavPath)
                + " --model " + Quote(string.IsNullOrEmpty(model) ? DefaultModel : model)
                + " --output " + Quote(outputCsv);
            int code = VocalStemSeparator.RunSync(pythonPath, args,
                Path.GetDirectoryName(outputCsv), out string log,
                3600, ct, env, onProgress);
            // 취소된 실행을 "추출 실패(exit -1)"로 보고하지 않게 취소를 전파한다.
            ct.ThrowIfCancellationRequested();
            if (code != 0 || !File.Exists(outputCsv))
            {
                int tail = log != null && log.Length > 800 ? log.Length - 800 : 0;
                error = $"음소 추출 실패(exit {code}):\n"
                    + (log != null ? log.Substring(tail) : "");
                return false;
            }
            return true;
        }

        /// <summary>백그라운드 추출 — 완료 후 호출자가 CSV를 읽어 CsvToBakedData로 변환한다.</summary>
        public static Task<(bool ok, string error)> ExtractAsync(
            string pythonPath, string projectRoot, string wavPath, string outputCsv,
            string model, IReadOnlyDictionary<string, string> env,
            CancellationToken ct, Action<float> onProgress)
        {
            return Task.Run(() =>
            {
                bool ok = Extract(pythonPath, projectRoot, wavPath, outputCsv,
                    model, env, ct, onProgress, out string extractError);
                return (ok, extractError);
            }, ct);
        }

        private static string Quote(string path)
        {
            return "\"" + path + "\"";
        }

        /// <summary>
        /// 추출 CSV를 60fps BakedData로 변환한다(순수 변환 — 테스트 가능).
        /// 첫 줄은 {"fps":..} JSON 헤더, 이후 행은 v,a,i,u,e,o다.
        /// </summary>
        public static uLipSync.BakedData CsvToBakedData(string csvText,
            float duration, int targetFps = VocalLipSyncBaker.BakeFrameRate)
        {
            if (string.IsNullOrEmpty(csvText))
            {
                throw new ArgumentException("CSV 내용이 비어 있습니다.", nameof(csvText));
            }
            var lines = csvText.Split('\n');
            float srcFps = 0f;
            var rows = new List<float[]>();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line[0] == '{')
                {
                    srcFps = ExtractFps(line);
                    continue;
                }
                var parts = line.Split(',');
                if (parts.Length < 6)
                {
                    continue;
                }
                var row = new float[6];
                for (int c = 0; c < 6; c++)
                {
                    float.TryParse(parts[c], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out row[c]);
                    // "nan"/"inf" 문자열이 파싱돼 커브로 새어나가는 것을 막는다.
                    if (float.IsNaN(row[c]) || float.IsInfinity(row[c]))
                    {
                        row[c] = 0f;
                    }
                }
                rows.Add(row);
            }
            if (rows.Count == 0 || srcFps <= 0f)
            {
                throw new InvalidOperationException("음소 CSV에 유효한 프레임이 없습니다.");
            }

            var data = ScriptableObject.CreateInstance<uLipSync.BakedData>();
            data.duration = duration;
            int frameCount = Mathf.Max(1, Mathf.RoundToInt(duration * targetFps));
            string[] names = { "A", "I", "U", "E", "O" };
            for (int i = 0; i < frameCount; i++)
            {
                // 소스 fps → 60fps 선형 보간.
                float src = i * srcFps / targetFps;
                int i0 = Mathf.Clamp((int)src, 0, rows.Count - 1);
                int i1 = Mathf.Min(i0 + 1, rows.Count - 1);
                float t = Mathf.Clamp01(src - i0);
                var frame = new uLipSync.BakedFrame
                {
                    volume = Mathf.Lerp(rows[i0][0], rows[i1][0], t),
                    phonemes = new List<uLipSync.BakedPhonemeRatio>(5),
                };
                for (int v = 0; v < 5; v++)
                {
                    frame.phonemes.Add(new uLipSync.BakedPhonemeRatio
                    {
                        phoneme = names[v],
                        ratio = Mathf.Lerp(rows[i0][v + 1], rows[i1][v + 1], t),
                    });
                }
                data.frames.Add(frame);
            }
            return data;
        }

        /// <summary>헤더 JSON에서 "fps" 값만 꺼낸다(정식 JSON 파서 없이 접두 탐색).</summary>
        private static float ExtractFps(string jsonLine)
        {
            const string key = "\"fps\"";
            int at = jsonLine.IndexOf(key, StringComparison.Ordinal);
            if (at < 0)
            {
                return 0f;
            }
            int colon = jsonLine.IndexOf(':', at + key.Length);
            if (colon < 0)
            {
                return 0f;
            }
            int start = colon + 1;
            while (start < jsonLine.Length && jsonLine[start] == ' ')
            {
                start++;
            }
            int end = start;
            while (end < jsonLine.Length
                && (char.IsDigit(jsonLine[end]) || jsonLine[end] == '.' || jsonLine[end] == '-'))
            {
                end++;
            }
            float.TryParse(jsonLine.Substring(start, end - start),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float fps);
            return fps;
        }
    }
}
