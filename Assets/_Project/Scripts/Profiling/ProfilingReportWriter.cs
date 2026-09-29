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
            string[] files = Directory.GetFiles(OutputDirectory, "run-*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(files);

            var reports = new List<string>(files.Length);
            foreach (string file in files)
            {
                if (!file.EndsWith(".trace.json", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith(".jev.json", StringComparison.OrdinalIgnoreCase))
                {
                    reports.Add(file);
                }
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
