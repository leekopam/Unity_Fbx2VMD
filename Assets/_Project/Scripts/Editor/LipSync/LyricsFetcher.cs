using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// lyrics_fetch.py 수집기 실행 + 결과 JSON 파싱.
    /// 로컬/태그/LRCLIB/VocaDB 후보를 돌려주며 적용 여부는 창이 사용자 승인으로 정한다.
    /// </summary>
    public static class LyricsFetcher
    {
        public const string ScriptRelPath = "Tools/LipSync/lyrics_fetch.py";

        [Serializable]
        public class Candidate
        {
            public string source;
            public string title;
            public string artist;
            public float score;
            public string path;
            public string lrcPath;
            public string preview;
            public int timedLines;
            public bool romanized;
        }

        [Serializable]
        private class FetchResult
        {
            public bool ok;
            public string error;
            public string title;
            public string artist;
            public List<Candidate> candidates;
            public List<string> warnings;
        }

        /// <summary>동기 실행 — 백그라운드 스레드에서 호출한다.</summary>
        public static bool Fetch(string pythonPath, string projectRoot,
            string audioPath, string outBase, string title, string artist,
            string lang, CancellationToken ct,
            out Candidate candidate, out string error)
        {
            candidate = null;
            error = null;
            string script = Path.Combine(projectRoot, ScriptRelPath.Replace('/', '\\'));
            if (!File.Exists(script))
            {
                error = "가사 수집기가 없습니다: " + script;
                return false;
            }
            string jsonPath = outBase + "_result.json";
            // 이전 실행의 잔여 결과 파일이 새 실패를 성공처럼 보이게 하지 않게 지운다.
            try { if (File.Exists(jsonPath)) File.Delete(jsonPath); }
            catch (IOException) { }
            string args = "\"" + script + "\""
                + " --audio \"" + Path.GetFullPath(audioPath) + "\""
                + " --out \"" + Path.GetFullPath(outBase) + "\""
                + " --lang " + lang
                + " --json \"" + Path.GetFullPath(jsonPath) + "\""
                + (string.IsNullOrEmpty(title)
                    ? "" : " --title \"" + title.Replace("\"", "") + "\"")
                + (string.IsNullOrEmpty(artist)
                    ? "" : " --artist \"" + artist.Replace("\"", "") + "\"");
            int code = VocalStemSeparator.RunSync(pythonPath, args,
                Path.GetDirectoryName(Path.GetFullPath(outBase)),
                out string log, 120, ct,
                orphanPurpose: "lyrics", orphanKey: audioPath);
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(jsonPath))
            {
                int tail = log != null && log.Length > 500 ? log.Length - 500 : 0;
                error = $"가사 수집 실패(exit {code}):\n"
                    + (log != null ? log.Substring(tail) : "");
                return false;
            }
            return ParseResult(File.ReadAllText(jsonPath),
                out candidate, out error);
        }

        /// <summary>수집기 JSON → 후보 파싱(테스트 가능한 순수 변환).</summary>
        internal static bool ParseResult(string json,
            out Candidate candidate, out string error)
        {
            candidate = null;
            error = null;
            FetchResult result;
            try
            {
                result = UnityEngine.JsonUtility.FromJson<FetchResult>(json);
            }
            catch (Exception exc)
            {
                error = "가사 결과 JSON 파싱 실패: " + exc.Message;
                return false;
            }
            if (result == null)
            {
                error = "가사 결과 JSON이 비어 있습니다.";
                return false;
            }
            if (result.warnings != null)
            {
                foreach (string w in result.warnings)
                {
                    UnityEngine.Debug.LogWarning("[가사 수집] " + w);
                }
            }
            if (!result.ok || result.candidates == null
                || result.candidates.Count == 0)
            {
                error = result.error ?? "가사를 찾지 못했습니다.";
                return false;
            }
            candidate = result.candidates[0];
            return true;
        }

        public static Task<(bool ok, Candidate candidate, string error)> FetchAsync(
            string pythonPath, string projectRoot, string audioPath,
            string outBase, string title, string artist, string lang,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                bool ok = Fetch(pythonPath, projectRoot, audioPath, outBase,
                    title, artist, lang, ct,
                    out Candidate candidate, out string error);
                return (ok, candidate, error);
            }, ct);
        }
    }
}
