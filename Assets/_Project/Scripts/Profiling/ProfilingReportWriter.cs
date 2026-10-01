using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.Profiling
{
    /// <summary>
    /// ProfilingRunRecord를 JSON 파일로 저장합니다.
    /// 에디터는 프로젝트 루트 Artifacts/Profiling(gitignore 대상), 플레이어는 persistentDataPath에 기록합니다.
    /// </summary>
    public static class ProfilingReportWriter
    {
        private const string DirectoryName = "Profiling";

        public static string OutputDirectory
        {
            get
            {
#if UNITY_EDITOR
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectRoot, "Artifacts", DirectoryName);
#else
                return Path.Combine(Application.persistentDataPath, DirectoryName);
#endif
            }
        }

        /// <summary>런 기록을 JSON으로 저장하고 저장 경로를 반환합니다.</summary>
        public static string Write(ProfilingRunRecord record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            Directory.CreateDirectory(OutputDirectory);
            string safeLabel = SanitizeFileName(record.label);
            string fileName = $"run-{SanitizeFileName(record.runId)}-{safeLabel}.json";
            string path = Path.Combine(OutputDirectory, fileName);
            File.WriteAllText(path, JsonUtility.ToJson(record, true));
            return path;
        }

        /// <summary>저장된 런 기록 파일 목록(최신순)을 반환합니다.</summary>
        public static string[] ListReports()
        {
            if (!Directory.Exists(OutputDirectory))
            {
                return Array.Empty<string>();
            }

            // trace·jev 사이드카도 run-*.json 패턴에 걸리므로 파일명 규약으로 걸러낸다.
            // 라벨이 ".trace"/".jev"로 끝나는 정상 리포트를 숨기지 않도록
            // 같은 이름의 리포트 파일이 있는 경우에만 사이드카로 판정한다.
            string[] files = Directory.GetFiles(OutputDirectory, "run-*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(files);
            var allFiles = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);

            var reports = new List<string>(files.Length);
            foreach (string file in files)
            {
                if (IsSidecar(file, ".trace.json", allFiles) ||
                    IsSidecar(file, ".jev.json", allFiles))
                {
                    continue;
                }
                reports.Add(file);
            }

            return reports.ToArray();
        }

        public static ProfilingRunRecord Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            return JsonUtility.FromJson<ProfilingRunRecord>(File.ReadAllText(path));
        }

        /// <summary>
        /// 같은 라벨·머신의 과거 런을 최신순으로 최대 maxCount개 로드합니다.
        /// 파이프라인 이력 분석과 리포트 창 추세 표시가 공용하는 필터입니다. 개별 파일 파싱 실패는 건너뜁니다.
        /// </summary>
        public static List<ProfilingRunRecord> LoadMatching(ProfilingRunRecord current, int maxCount = 20)
        {
            var history = new List<ProfilingRunRecord>();
            if (current == null || maxCount <= 0)
            {
                return history;
            }

            foreach (string path in GetHistoryCandidatePaths(current))
            {
                if (history.Count >= maxCount)
                {
                    break;
                }

                ProfilingRunRecord past;
                try { past = Load(path); }
                catch { continue; }
                if (!IsHistoryMatch(current, past))
                {
                    continue;
                }
                history.Add(past);
            }

            return history;
        }

        /// <summary>
        /// 파일명 규약(run-{id}-{label}.json)으로 이력 후보 경로만 걸러냅니다(파싱 없음).
        /// 뷰어의 증분 스캔이 1차 필터로 사용하며, 정확한 일치 여부는 IsHistoryMatch로 확인합니다.
        /// </summary>
        public static List<string> GetHistoryCandidatePaths(ProfilingRunRecord current)
        {
            var paths = new List<string>();
            if (current == null)
            {
                return paths;
            }

            string needle = SanitizeFileName(current.label);
            foreach (string path in ListReports())
            {
                if (!string.IsNullOrEmpty(needle) &&
                    Path.GetFileNameWithoutExtension(path)
                        .IndexOf(needle, StringComparison.Ordinal) < 0)
                {
                    continue;
                }
                paths.Add(path);
            }
            return paths;
        }

        /// <summary>이력 비교의 정확한 일치 조건입니다(같은 라벨·머신, 자기 자신 제외).</summary>
        public static bool IsHistoryMatch(ProfilingRunRecord current, ProfilingRunRecord past)
        {
            return current != null && past != null &&
                past.runId != current.runId &&
                string.Equals(past.label, current.label, StringComparison.Ordinal) &&
                string.Equals(past.deviceModel, current.deviceModel, StringComparison.Ordinal);
        }

        /// <summary>리포트에서 outcome/startedUtc만 읽습니다(목록 필터 스캔용 경량 파싱).</summary>
        public static bool TryReadMeta(string path, out string outcome, out string startedUtc)
        {
            outcome = null;
            startedUtc = null;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return false;
                }
                var stub = JsonUtility.FromJson<MetaStub>(File.ReadAllText(path));
                if (stub == null)
                {
                    return false;
                }
                outcome = stub.outcome;
                startedUtc = stub.startedUtc;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>run-yyyyMMdd-HHmmss-xxxx-label 파일명 규약에서 런 시작 시각(UTC)을 읽습니다.</summary>
        public static bool TryGetRunTimestampUtc(string path, out DateTime utc)
        {
            utc = default;
            string name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(name) || name.Length < 19 ||
                !name.StartsWith("run-", StringComparison.Ordinal))
            {
                return false;
            }
            return DateTime.TryParseExact(
                name.Substring(4, 15),
                "yyyyMMdd-HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal |
                    System.Globalization.DateTimeStyles.AdjustToUniversal,
                out utc);
        }

        [Serializable]
        private class MetaStub
        {
            public string outcome;
            public string startedUtc;
        }

        /// <summary>
        /// 파일이 run-*{suffix} 형태의 사이드카인지 판정합니다.
        /// 접미사를 제거한 리포트 본체가 같은 목록에 있을 때만 사이드카로 봅니다.
        /// </summary>
        private static bool IsSidecar(
            string file, string suffix, HashSet<string> allFiles)
        {
            if (!file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string reportBody =
                file.Substring(0, file.Length - suffix.Length) + ".json";
            return allFiles.Contains(reportBody);
        }

        internal static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "run";
            }

            string safe = name.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(invalid, '_');
            }

            return safe.Length > 40 ? safe.Substring(0, 40) : safe;
        }
    }
}
