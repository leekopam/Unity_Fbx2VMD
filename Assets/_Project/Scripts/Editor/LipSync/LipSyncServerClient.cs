using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 로컬 립싱크 모델 서버(Tools/LipSync/server.py) 클라이언트.
    /// 분리·정제·음소 추출·가사 수집을 에디터 프로세스 밖에서 실행해
    /// 도메인 리로드·에디터 종료에 무관하게 작업을 지속시킨다.
    /// 서버가 꺼져 있으면 각 호출자가 기존 로컬 프로세스 경로로 폴백한다.
    /// </summary>
    internal static class LipSyncServerClient
    {
        /// <summary>서버 주소 — 환경변수로 바꿀 수 있게 한다(기본 17845).</summary>
        internal static string BaseUrl
        {
            get
            {
                string url = Environment.GetEnvironmentVariable("LIPSYNC_SERVER_URL");
                return string.IsNullOrEmpty(url) ? "http://127.0.0.1:17845" : url;
            }
        }

        /// <summary>서버가 제출을 거부한 경우(경로 제한 위반 등) — 호출자는 로컬 경로로 폴백한다.
        /// 작업이 한번 서버에 등록된 뒤의 폴링 실패는 이 타입이 아니다(서버에 작업이
        /// 남아 있을 수 있으므로 로컬 재실행은 중복 실행이 된다).</summary>
        public sealed class SubmitRejectedException : Exception
        {
            public SubmitRejectedException(string message) : base(message) { }
        }

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        // health 응답은 수 초간 재사용 — 폴링마다 probe가 튀지 않게 한다.
        private static bool _available;
        private static DateTime _lastProbeUtc = DateTime.MinValue;

        /// <summary>서버 생존 여부 — 3초 캐시 + 1.5초 타임아웃 probe.</summary>
        public static async Task<bool> IsAvailableAsync()
        {
            if ((DateTime.UtcNow - _lastProbeUtc).TotalSeconds < 3.0)
            {
                return _available;
            }
            _lastProbeUtc = DateTime.UtcNow;
            try
            {
                using (CancellationTokenSource timeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(1.5)))
                using (HttpResponseMessage res = await Http.GetAsync(
                    BaseUrl + "/health", timeout.Token).ConfigureAwait(false))
                {
                    _available = res.IsSuccessStatusCode;
                }
            }
            catch (Exception)
            {
                _available = false;
            }
            return _available;
        }

        /// <summary>작업 제출 — 동일 kind+key가 이미 실행 중이면(409) 기존 job_id를 돌려준다.
        /// 전송 오류 시 한 번 재시도한다(서버 dedup가 수신된 요청을 흡수해 중복 실행을 막는다).</summary>
        public static async Task<string> SubmitAsync(string kind, string key,
            JObject parms, CancellationToken ct)
        {
            try
            {
                return await SubmitOnceAsync(kind, key, parms, ct)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                try
                {
                    return await SubmitOnceAsync(kind, key, parms, ct)
                        .ConfigureAwait(false);
                }
                catch (HttpRequestException e)
                {
                    // health 통과 직후 서버가 죽는 등 — 작업이 서버에 남지
                    // 않았으므로 로컬 폴백해도 중복 실행이 되지 않는다.
                    throw new SubmitRejectedException("서버 제출 실패: " + e.Message);
                }
            }
        }

        private static async Task<string> SubmitOnceAsync(string kind, string key,
            JObject parms, CancellationToken ct)
        {
            JObject body = new JObject
            {
                ["kind"] = kind,
                ["key"] = key ?? "",
                ["params"] = parms ?? new JObject(),
            };
            string text;
            using (HttpResponseMessage res = await Http.PostAsync(BaseUrl + "/jobs",
                new StringContent(body.ToString(), Encoding.UTF8, "application/json"),
                ct).ConfigureAwait(false))
            {
                text = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if ((int)res.StatusCode == 409)
                {
                    string existing = JObject.Parse(text)["detail"]?["job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(existing))
                    {
                        return existing;
                    }
                }
                if (!res.IsSuccessStatusCode)
                {
                    // 제출 단계의 거부는 호출자의 로컬 폴백 대상이다.
                    // 폴링 중 연결 실패는 SubmitRejectedException이 아니라
                    // HttpRequestException으로 올라와 실패로 전파된다.
                    throw new SubmitRejectedException(
                        $"작업 제출 실패({(int)res.StatusCode}): {text}");
                }
            }
            return JObject.Parse(text)["job_id"].ToString();
        }

        /// <summary>작업 상태 조회 — 404(서버 재시작 등으로 유실)는 null을 돌려준다.
        /// 그 외 연결 실패는 호출자 예외로 전파된다.</summary>
        public static async Task<JObject> GetJobAsync(string jobId, CancellationToken ct)
        {
            using (HttpResponseMessage res = await Http.GetAsync(
                BaseUrl + "/jobs/" + jobId, ct).ConfigureAwait(false))
            {
                if ((int)res.StatusCode == 404)
                {
                    return null;
                }
                res.EnsureSuccessStatusCode();
                string text = await res.Content.ReadAsStringAsync()
                    .ConfigureAwait(false);
                return JObject.Parse(text);
            }
        }

        /// <summary>실패한 작업의 로그 tail — 진단용. 실패해도 조용히 빈 문자열.</summary>
        public static async Task<string> GetLogTailAsync(string jobId, int tail = 30)
        {
            try
            {
                string text = await Http.GetStringAsync(
                    BaseUrl + "/jobs/" + jobId + "/log?tail=" + tail)
                    .ConfigureAwait(false);
                JArray lines = JObject.Parse(text)["lines"] as JArray;
                if (lines == null || lines.Count == 0)
                {
                    return "";
                }
                return "\n" + string.Join("\n", lines);
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>완료까지 폴링. propagateCancel()이 참일 때 취소되면 서버에도
        /// cancel을 보낸 뒤 취소를 전파한다. 도메인 리로드·창 닫힘처럼 작업을
        /// 서버에서 계속 돌려야 하는 취소는 propagateCancel=false로 넘긴다.</summary>
        public static async Task<JObject> WaitAsync(string jobId, CancellationToken ct,
            Action<float> onProgress = null, Action<string> onStage = null,
            Func<bool> propagateCancel = null)
        {
            try
            {
                while (true)
                {
                    JObject job = await GetJobAsync(jobId, ct).ConfigureAwait(false);
                    if (job == null)
                    {
                        throw new InvalidOperationException(
                            "서버 작업을 찾지 못했습니다(서버 재시작으로 상태 유실)");
                    }
                    string status = job["status"]?.ToString();
                    onProgress?.Invoke((float?)job["progress"] ?? 0f);
                    string stage = job["stage"]?.ToString();
                    if (!string.IsNullOrEmpty(stage))
                    {
                        onStage?.Invoke(stage);
                    }
                    if (status == "done")
                    {
                        return job;
                    }
                    if (status == "failed")
                    {
                        string logTail = await GetLogTailAsync(jobId)
                            .ConfigureAwait(false);
                        throw new InvalidOperationException(
                            (job["error"]?.ToString() ?? "서버 작업이 실패했습니다.")
                            + logTail);
                    }
                    if (status == "cancelled")
                    {
                        throw new OperationCanceledException();
                    }
                    await Task.Delay(800, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                if (propagateCancel == null || propagateCancel())
                {
                    await CancelAsync(jobId).ConfigureAwait(false);
                }
                throw;
            }
        }

        /// <summary>폴링 + 추적 해제 규칙 — 정상 완료·실패·서버에 통지된 취소는
        /// 해제하고, 리로드용 조용한 취소는 재부착을 위해 추적을 유지한다.</summary>
        private static async Task<JObject> WaitTrackedAsync(string kind,
            string jobId, CancellationToken ct, Func<bool> propagateCancel,
            Action<float> onProgress = null, Action<string> onStage = null)
        {
            try
            {
                JObject job = await WaitAsync(jobId, ct, onProgress, onStage,
                    propagateCancel).ConfigureAwait(false);
                ServerJobTracker.Untrack(kind);
                return job;
            }
            catch (OperationCanceledException)
            {
                if (propagateCancel == null || propagateCancel())
                {
                    ServerJobTracker.Untrack(kind);
                }
                throw;
            }
            catch
            {
                ServerJobTracker.Untrack(kind);
                throw;
            }
        }

        /// <summary>서버에 취소 통지 — 실패해도(이미 종료 등) 조용히 넘긴다.</summary>
        public static async Task CancelAsync(string jobId)
        {
            try
            {
                using (await Http.PostAsync(
                    BaseUrl + "/jobs/" + jobId + "/cancel", null)
                    .ConfigureAwait(false))
                {
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>서버 경로 분리 — Result는 로컬 SeparateAsync와 동일 계약이다.
        /// propagateCancel은 "취소를 서버에 통지할지"를 돌려주는 델리게이트
        /// (창 닫힘·도메인 리로드 시엔 false → 서버 작업은 계속되고 재부착된다).</summary>
        public static async Task<VocalStemSeparator.Result> SeparateViaServerAsync(
            VocalStemSeparator.Engine engine, string inputPath, string outputDir,
            string model, string modelDir, bool highQuality,
            CancellationToken ct, Action<float> onProgress,
            Func<bool> propagateCancel = null, string resumeContext = null,
            string ensemblePreset = null)
        {
            string kind = engine == VocalStemSeparator.Engine.Demucs
                ? "demucs" : "audio_separator";
            JObject parms = new JObject
            {
                ["engine"] = kind,
                ["input"] = Path.GetFullPath(inputPath),
                ["output_dir"] = Path.GetFullPath(outputDir),
                ["model"] = model ?? "",
                ["model_dir"] = modelDir ?? "",
                ["high_quality"] = highQuality,
                ["ensemble_preset"] = ensemblePreset ?? "",
            };
            string jobId = await SubmitAsync("separate",
                Path.GetFullPath(inputPath), parms, ct).ConfigureAwait(false);
            // 재부착 컨텍스트는 창이 만드는 7필드 계약(입력|출력|engine|clean|wpe|hq|preset)과
            // 맞춘다 — clean/wpe는 이 시그니처에 없어 0으로 두고 hq·preset은 실제 값을 쓴다.
            ServerJobTracker.Track("separate", jobId, resumeContext
                ?? string.Join("|", Path.GetFullPath(inputPath),
                    Path.GetFullPath(outputDir), kind, "0", "0",
                    highQuality ? "1" : "0", ensemblePreset ?? ""));
            JObject job = await WaitTrackedAsync("separate", jobId, ct,
                propagateCancel, onProgress).ConfigureAwait(false);
            return SeparateResult(job);
        }

        /// <summary>서버 작업 결과 → Result 변환(분리).</summary>
        internal static VocalStemSeparator.Result SeparateResult(JObject job)
        {
            JObject outputs = job["outputs"] as JObject;
            return new VocalStemSeparator.Result
            {
                success = true,
                vocalPath = outputs?["vocal"]?.ToString() ?? "",
                instrumentalPath = outputs?["instrumental"]?.ToString() ?? "",
            };
        }

        /// <summary>도메인 리로드 후 실행 중이던 분리 작업에 다시 붙는다.
        /// 컨텍스트는 `입력경로|출력dir|engine` — 스냅샷 복원에 쓴다.</summary>
        public static async Task<VocalStemSeparator.Result> ResumeSeparateAsync(
            string jobId, CancellationToken ct, Action<float> onProgress = null,
            Func<bool> propagateCancel = null)
        {
            JObject job = await WaitTrackedAsync("separate", jobId, ct,
                propagateCancel, onProgress).ConfigureAwait(false);
            return SeparateResult(job);
        }

        /// <summary>서버 경로 정제 — 체인은 서버가 돌리고 살베지 병합(순수 C#)은 여기서 한다.</summary>
        public static async Task<VocalStemSeparator.Result> CleanViaServerAsync(
            string vocalPath, string outputDir, bool wpe,
            string modelDir, bool highQuality, string originalMixPath,
            string salvageSourcePath,
            CancellationToken ct, Action<float> onProgress, Action<string> onStage,
            Func<bool> propagateCancel = null)
        {
            JObject parms = new JObject
            {
                ["input"] = Path.GetFullPath(vocalPath),
                ["output_dir"] = Path.GetFullPath(outputDir),
                ["model_dir"] = modelDir ?? "",
                ["high_quality"] = highQuality,
                ["wpe"] = wpe,
            };
            if (!string.IsNullOrEmpty(originalMixPath))
            {
                parms["original_mix"] = Path.GetFullPath(originalMixPath);
            }
            string jobId = await SubmitAsync("clean",
                Path.GetFullPath(vocalPath), parms, ct).ConfigureAwait(false);
            ServerJobTracker.Track("clean", jobId,
                Path.GetFullPath(vocalPath) + "|" + Path.GetFullPath(outputDir));
            JObject job = await WaitTrackedAsync("clean", jobId, ct,
                propagateCancel, onProgress, onStage).ConfigureAwait(false);
            return CleanResult(job, vocalPath, salvageSourcePath, outputDir);
        }

        /// <summary>서버 작업 결과 → Result 변환(정제) + 살베지 병합.</summary>
        internal static VocalStemSeparator.Result CleanResult(JObject job,
            string vocalPath, string salvageSourcePath, string outputDir)
        {
            string cleanPath = (job["outputs"] as JObject)?["vocal"]?.ToString();
            var result = new VocalStemSeparator.Result { success = true };
            result.vocalPath = cleanPath ?? "";
            // 로컬 경로와 동일하게 과감쇠 구간을 분리 보컬로 채운다.
            if (!string.IsNullOrEmpty(salvageSourcePath)
                && File.Exists(result.vocalPath))
            {
                string baseName = Path.GetFileNameWithoutExtension(vocalPath);
                if (baseName.Length > 60)
                {
                    baseName = baseName.Substring(0, 60);
                }
                string merged = VocalSalvageMerger.Merge(
                    result.vocalPath, salvageSourcePath,
                    Path.Combine(outputDir, baseName + "_vocal_lead.wav"),
                    out float filledRatio, out string salvageError);
                if (merged != null)
                {
                    result.vocalPath = merged;
                    if (filledRatio > 0f)
                    {
                        result.warnings.Add(
                            $"살베지: 과감쇠 구간 {filledRatio:P0}를 보컬 스템으로 채웠습니다.");
                    }
                }
                else if (!string.IsNullOrEmpty(salvageError))
                {
                    result.warnings.Add("살베지 건너뜀: " + salvageError);
                }
            }
            return result;
        }

        /// <summary>도메인 리로드 후 실행 중이던 정제 작업에 다시 붙는다.
        /// 컨텍스트는 `입력wav|출력dir` — 원본 보컬이 살베지 소스도 겸한다.</summary>
        public static async Task<VocalStemSeparator.Result> ResumeCleanAsync(
            string jobId, string context, CancellationToken ct,
            Action<float> onProgress = null, Action<string> onStage = null,
            Func<bool> propagateCancel = null)
        {
            string[] parts = (context ?? "").Split('|');
            string vocalPath = parts.Length > 0 ? parts[0] : "";
            string outputDir = parts.Length > 1 ? parts[1] : "";
            JObject job = await WaitTrackedAsync("clean", jobId, ct,
                propagateCancel, onProgress, onStage).ConfigureAwait(false);
            return CleanResult(job, vocalPath, vocalPath, outputDir);
        }

        /// <summary>서버 경로 음소 추출 — CSV는 phoneme_extract.py가 디스크에 쓴다.</summary>
        public static async Task<(bool ok, string error)> PhonemeViaServerAsync(
            string wavPath, string outputCsv, string model, string lyricsPath,
            CancellationToken ct, Action<float> onProgress,
            Func<bool> propagateCancel = null)
        {
            JObject parms = new JObject
            {
                ["input"] = Path.GetFullPath(wavPath),
                ["output"] = Path.GetFullPath(outputCsv),
                ["model"] = model ?? "",
            };
            if (!string.IsNullOrEmpty(lyricsPath) && File.Exists(lyricsPath))
            {
                parms["lyrics"] = Path.GetFullPath(lyricsPath);
            }
            string jobId = await SubmitAsync("phoneme",
                Path.GetFullPath(wavPath), parms, ct).ConfigureAwait(false);
            ServerJobTracker.Track("phoneme", jobId,
                Path.GetFullPath(wavPath) + "|" + Path.GetFullPath(outputCsv));
            JObject job = await WaitTrackedAsync("phoneme", jobId, ct,
                propagateCancel, onProgress).ConfigureAwait(false);
            string csv = (job["outputs"] as JObject)?["csv"]?.ToString()
                ?? outputCsv;
            return File.Exists(csv)
                ? (true, (string)null)
                : (false, "음소 추출 산출물이 없습니다: " + csv);
        }

        /// <summary>도메인 리로드 후 실행 중이던 음소 추출에 다시 붙는다
        /// (컨텍스트 = `입력wav|출력csv`).</summary>
        public static async Task<(bool ok, string error)> ResumePhonemeAsync(
            string jobId, string outputCsv, CancellationToken ct,
            Action<float> onProgress = null, Func<bool> propagateCancel = null)
        {
            await WaitTrackedAsync("phoneme", jobId, ct, propagateCancel,
                onProgress).ConfigureAwait(false);
            return File.Exists(outputCsv)
                ? (true, (string)null)
                : (false, "음소 추출 산출물이 없습니다: " + outputCsv);
        }

        /// <summary>서버 경로 가사 수집 — 수집기가 쓴 result JSON을 읽어 기존 파서로 변환한다.</summary>
        public static async Task<(bool ok, LyricsFetcher.Candidate candidate, string error)>
            LyricsViaServerAsync(string audioPath, string outBase, string title,
                string artist, string lang, CancellationToken ct,
                Func<bool> propagateCancel = null)
        {
            string jsonPath = outBase + "_result.json";
            try { if (File.Exists(jsonPath)) File.Delete(jsonPath); }
            catch (IOException) { }
            JObject parms = new JObject
            {
                ["audio"] = Path.GetFullPath(audioPath),
                ["out"] = Path.GetFullPath(outBase),
                ["title"] = title ?? "",
                ["artist"] = artist ?? "",
                ["lang"] = lang ?? "",
                ["json"] = Path.GetFullPath(jsonPath),
            };
            string jobId = await SubmitAsync("lyrics",
                Path.GetFullPath(audioPath), parms, ct).ConfigureAwait(false);
            ServerJobTracker.Track("lyrics", jobId, Path.GetFullPath(outBase));
            await WaitTrackedAsync("lyrics", jobId, ct, propagateCancel)
                .ConfigureAwait(false);
            return ParseLyricsResult(jsonPath);
        }

        /// <summary>도메인 리로드 후 실행 중이던 가사 수집에 다시 붙는다(컨텍스트 = outBase).</summary>
        public static async Task<(bool ok, LyricsFetcher.Candidate candidate, string error)>
            ResumeLyricsAsync(string jobId, string outBase, CancellationToken ct,
                Func<bool> propagateCancel = null)
        {
            await WaitTrackedAsync("lyrics", jobId, ct, propagateCancel)
                .ConfigureAwait(false);
            return ParseLyricsResult(outBase + "_result.json");
        }

        private static (bool ok, LyricsFetcher.Candidate candidate, string error)
            ParseLyricsResult(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                return (false, null, "가사 결과 JSON이 생성되지 않았습니다.");
            }
            bool ok = LyricsFetcher.ParseResult(File.ReadAllText(jsonPath),
                out LyricsFetcher.Candidate candidate, out string error);
            return (ok, candidate, error);
        }
    }

    /// <summary>
    /// 실행 중인 서버 작업 id를 `Library/VocalSeparation/server_jobs.json`에 남긴다.
    /// SessionState가 아니라 파일을 쓰는 이유: SessionState는 워커 스레드에서
    /// 호출하면 에디터가 교착하고(실측), 파일은 도메인 리로드와 에디터
    /// 재시작 양쪽을 넘어 서버 재시작 복구(result.json)까지 같은 계약으로 커버한다.
    /// 컨텍스트는 `|` 구분 재부착 정보(Windows 경로에 `|`는 불가).
    /// </summary>
    internal static class ServerJobTracker
    {
        private static readonly object Gate = new object();

        private static string StorePath
        {
            get
            {
                // Application.dataPath는 Assets 폴더 — 프로젝트 루트는 그 상위.
                return Path.Combine(Path.GetDirectoryName(
                    UnityEngine.Application.dataPath),
                    "Library", "VocalSeparation", "server_jobs.json");
            }
        }

        private static Dictionary<string, string> ReadAll()
        {
            try
            {
                if (!File.Exists(StorePath))
                {
                    return new Dictionary<string, string>();
                }
                JObject root = JObject.Parse(File.ReadAllText(StorePath));
                var map = new Dictionary<string, string>();
                foreach (KeyValuePair<string, JToken> kv in root)
                {
                    map[kv.Key] = kv.Value?.ToString() ?? "";
                }
                return map;
            }
            catch (Exception)
            {
                return new Dictionary<string, string>();
            }
        }

        private static void WriteAll(Dictionary<string, string> map)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath));
                File.WriteAllText(StorePath,
                    JObject.FromObject(map).ToString());
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    "[LipSync] 서버 작업 추적 파일 쓰기 실패: " + e.Message);
            }
        }

        /// <summary>컨텍스트는 재부착에 필요한 경로 정보(경로는 `|`를 포함하지 않는다).</summary>
        public static void Track(string kind, string jobId, string context = "")
        {
            lock (Gate)
            {
                Dictionary<string, string> map = ReadAll();
                map[kind] = (jobId ?? "") + "|" + (context ?? "");
                WriteAll(map);
            }
        }

        public static void Untrack(string kind)
        {
            lock (Gate)
            {
                Dictionary<string, string> map = ReadAll();
                if (map.Remove(kind))
                {
                    WriteAll(map);
                }
            }
        }

        /// <summary>리로드 전 실행 중이던 작업 id — 없으면 null.</summary>
        public static string PendingJobId(string kind)
        {
            lock (Gate)
            {
                string raw;
                if (!ReadAll().TryGetValue(kind, out raw))
                {
                    return null;
                }
                string id = raw.Split('|')[0];
                return string.IsNullOrEmpty(id) ? null : id;
            }
        }

        /// <summary>리로드 전 작업의 재부착 컨텍스트(입력·출력 경로 등) — 없으면 빈 문자열.</summary>
        public static string PendingContext(string kind)
        {
            lock (Gate)
            {
                string raw;
                if (!ReadAll().TryGetValue(kind, out raw))
                {
                    return "";
                }
                int sep = raw.IndexOf('|');
                return sep < 0 ? "" : raw.Substring(sep + 1);
            }
        }
    }
}
