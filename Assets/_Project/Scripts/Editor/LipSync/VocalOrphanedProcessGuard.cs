using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 보컬 분리/정제 같은 장시간 외부 프로세스의 신원(PID·시작 시각·작업 키)을
    /// 디스크 마커에 남겨, 도메인 리로드·창 닫힘·크래시로 관리 Task가 소실돼도
    /// 고아 프로세스를 추적한다. 매 도메인 초기화([InitializeOnLoad])마다 소거한다.
    /// - 이미 종료된 항목은 제거한다.
    /// - 젊은 생존 프로세스는 유지한다 — 완료되면 출력 파일이 채택 경로로 회수된다.
    /// - MaxOrphanAge를 넘은 프로세스는 감시자(이전 도메인의 ct 타임아웃)를 잃은
    ///   좀비로 보고 프로세스 트리째 종료한다.
    /// </summary>
    [InitializeOnLoad]
    internal static class VocalOrphanedProcessGuard
    {
        /// <summary>RunSync의 개별 프로세스 타임아웃(3600s)보다 살짝 크게 —
        /// 이 나이를 넘은 고아는 더 이상 정상 감시 하에 있지 않다.</summary>
        internal const double MaxOrphanAgeSeconds = 3900;

        /// <summary>마커 한 항목 — pid|startedUtcTicks|purpose|key 한 줄로 직렬화한다.</summary>
        internal sealed class Entry
        {
            public int Pid;
            public DateTime StartedUtc;
            /// <summary>작업 종류 — "separate"(분리), "clean"(정제) 등.</summary>
            public string Purpose;
            /// <summary>작업 동일성 식별자 — separate는 입력 원곡, clean은 정제 대상 보컬 경로.</summary>
            public string Key;
        }

        /// <summary>테스트에서 마커 위치를 임시 디렉터리로 바꿀 때 쓴다(null이면 기본).</summary>
        internal static string MarkerDirOverride;
        /// <summary>테스트에서 프로세스 이미지명 검증을 바꿀 때 쓴다.
        /// 기본은 python 계열만 인정해 PID 재사용으로 엉뚱한 프로세스를 죽이지 않게 한다.</summary>
        internal static Func<string, bool> ProcessNameFilter =
            name => name != null
                && name.StartsWith("python", StringComparison.OrdinalIgnoreCase);

        /// <summary>테스트에서 이미지명 조회를 바꿀 때 쓴다 — 기본은 kernel32 조회.</summary>
        internal static Func<int, string> ImageFileNameProvider = QueryImageFileName;

        static VocalOrphanedProcessGuard()
        {
            try
            {
                SweepOrphans();
            }
            catch (Exception e)
            {
                // 마커 파일 공유 위반 등이 도메인 초기화를 막지 않게 한다.
                UnityEngine.Debug.LogWarning(
                    "[보컬 분리] 고아 프로세스 소거 실패: " + e.Message);
            }
        }

        /// <summary>마커 파일 경로 — 프로젝트 Library 아래에 둬서
        /// 프로젝트별로 격리하고 에디터 재시작 후에도 남게 한다.</summary>
        internal static string MarkerPath(string markerDir = null)
        {
            string dir = markerDir ?? MarkerDirOverride
                ?? Path.Combine(Application.dataPath, "..", "Library", "VocalSeparation");
            return Path.Combine(dir, "orphans.txt");
        }

        /// <summary>프로세스 시작 직후 마커에 남긴다. 실패해도 실행 경로를 막지 않는다.
        /// 원본 핸들의 StartTime은 Unity Mono에서 불안정하므로 PID로 재오픈해 읽는다.</summary>
        internal static void Register(Process process, string purpose, string key,
            string markerDir = null)
        {
            try
            {
                DateTime startedUtc;
                using (Process probe = Process.GetProcessById(process.Id))
                {
                    startedUtc = probe.StartTime.ToUniversalTime();
                }
                string path = MarkerPath(markerDir);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path, SerializeEntry(new Entry
                {
                    Pid = process.Id,
                    StartedUtc = startedUtc,
                    Purpose = purpose,
                    Key = key,
                }) + Environment.NewLine);
                // 에디터 종료·크래시 시 OS가 자식 트리를 즉시 정리하게 Job에 묶는다.
                // 실패해도 마커 소거 경로가 남아 있으므로 실행을 막지 않는다.
                if (!VocalOrphanedProcessJob.TryAssign(process))
                {
                    UnityEngine.Debug.Log(
                        $"[보컬 분리] Job Object 미등록(PID {process.Id}) — 마커 소거만 사용합니다.");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    "[보컬 분리] 고아 프로세스 마커 기록 실패: " + e.Message);
            }
        }

        /// <summary>프로세스 종료 후 마커에서 지운다 — 정상 경로에서는 호출부 finally가 보장한다.</summary>
        internal static void Unregister(int pid, string markerDir = null)
        {
            try
            {
                string path = MarkerPath(markerDir);
                if (!File.Exists(path))
                {
                    return;
                }
                var kept = new List<string>();
                foreach (string line in File.ReadAllLines(path))
                {
                    if (TryParseEntry(line, out Entry e) && e.Pid == pid)
                    {
                        continue;
                    }
                    kept.Add(line);
                }
                WriteLines(path, kept);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    "[보컬 분리] 고아 프로세스 마커 해제 실패: " + e.Message);
            }
        }

        /// <summary>purpose·key가 일치하고 아직 살아 있는 마커 항목을 찾는다 —
        /// 재시도 시 같은 작업의 프로세스를 중복으로 띄우지 않게 한다.</summary>
        internal static bool TryFindLive(string purpose, string key, out Entry entry,
            string markerDir = null)
        {
            entry = null;
            string path = MarkerPath(markerDir);
            if (string.IsNullOrEmpty(key) || !File.Exists(path))
            {
                return false;
            }
            foreach (string line in File.ReadAllLines(path))
            {
                if (!TryParseEntry(line, out Entry e))
                {
                    continue;
                }
                if (!string.Equals(e.Purpose, purpose, StringComparison.Ordinal)
                    || !string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (TryAttach(e.Pid, e.StartedUtc, out Process p))
                {
                    p.Dispose();
                    entry = e;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Attach 판정 — Unknown은 "살아 있지만 신원 검증 불가"로
        /// 죽은 것과 구분한다(추적 유실 방지).</summary>
        internal enum AttachStatus { Attached, Dead, Mismatch, Unknown }

        /// <summary>마커의 PID를 실제 프로세스 핸들로 연다 —
        /// 시작 시각(±2분)과 이미지 파일명을 함께 맞춰 PID 재사용 오살을 막는다.</summary>
        internal static bool TryAttach(int pid, DateTime startedUtc, out Process process)
            => AttachDetailed(pid, startedUtc, out process) == AttachStatus.Attached;

        internal static AttachStatus AttachDetailed(
            int pid, DateTime startedUtc, out Process process)
        {
            process = null;
            Process p;
            try
            {
                p = Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                return AttachStatus.Dead; // 그 PID의 프로세스가 없다.
            }
            catch (Exception)
            {
                return AttachStatus.Unknown; // 조회 자체가 안 됨(권한·일시 오류) — 살았는지 모름
            }
            try
            {
                // 종료 직후엔 GetProcessById가 성공해도 StartTime 조회에서 던지는
                // 좀비 상태가 있으므로 HasExited를 먼저 본다.
                if (p.HasExited)
                {
                    p.Dispose();
                    return AttachStatus.Dead;
                }
            }
            catch (Exception)
            {
                // HasExited 조회 불가는 StartTime 검증에 맡긴다.
            }
            DateTime started;
            try
            {
                started = p.StartTime.ToUniversalTime();
            }
            catch (Exception)
            {
                p.Dispose();
                return AttachStatus.Unknown; // 살아 있지만 시작 시각 조회 불가
            }
            TimeSpan drift = started - startedUtc;
            if (drift < TimeSpan.Zero || drift > TimeSpan.FromMinutes(2))
            {
                p.Dispose();
                return AttachStatus.Mismatch; // 시작 시각이 달라 PID 재사용된 다른 프로세스
            }
            // ProcessName은 Unity Mono에서 Unsupported라 이미지 경로를 직접 조회한다.
            string imageName = ImageFileNameProvider(pid);
            if (imageName == null)
            {
                p.Dispose();
                return AttachStatus.Unknown; // 살아 있지만 이미지 조회 불가
            }
            if (!ProcessNameFilter(imageName))
            {
                p.Dispose();
                return AttachStatus.Mismatch; // 다른 이미지의 프로세스
            }
            process = p;
            return AttachStatus.Attached;
        }

        /// <summary>도메인 초기화마다 호출되는 소거 — 죽은 항목 정리 + 오래된 좀비 종료.
        /// nowUtc는 테스트에서 기준 시각을 고정할 때만 넘긴다.</summary>
        internal static void SweepOrphans(string markerDir = null, DateTime? nowUtc = null)
        {
            string path = MarkerPath(markerDir);
            if (!File.Exists(path))
            {
                return;
            }
            var kept = new List<string>();
            bool changed = false;
            foreach (string line in File.ReadAllLines(path))
            {
                if (!TryParseEntry(line, out Entry e))
                {
                    changed = true; // 깨진 줄은 정리한다.
                    continue;
                }
                AttachStatus status = AttachDetailed(e.Pid, e.StartedUtc, out Process p);
                if (status == AttachStatus.Attached)
                {
                    using (p)
                    {
                        double ageSec = ((nowUtc ?? DateTime.UtcNow)
                            - e.StartedUtc).TotalSeconds;
                        if (ageSec >= MaxOrphanAgeSeconds)
                        {
                            // 감시 Task가 소실돼 자체 타임아웃 없이 도는 좀비 — 트리째 종료한다.
                            UnityEngine.Debug.Log(
                                $"[보컬 분리] 타임아웃 초과 고아 프로세스 종료: PID {e.Pid} ({e.Purpose})");
                            VocalStemSeparator.TryKill(p);
                            changed = true;
                            continue;
                        }
                        UnityEngine.Debug.Log(
                            $"[보컬 분리] 이전 실행 프로세스 생존: PID {e.Pid} ({e.Purpose})"
                            + " — 완료되면 결과가 채택됩니다.");
                    }
                    kept.Add(line);
                    continue;
                }
                if (status == AttachStatus.Unknown)
                {
                    // 살아 있지만 신원 검증이 안 된다 — 추적을 유지하고 다음 소거에서 재판정.
                    kept.Add(line);
                    continue;
                }
                changed = true; // Dead(소멸) 또는 Mismatch(PID 재사용된 다른 프로세스) — 항목 제거
            }
            if (changed)
            {
                WriteLines(path, kept);
            }
        }

        internal static string SerializeEntry(Entry e)
            => $"{e.Pid}|{e.StartedUtc.Ticks}|{e.Purpose}|{e.Key}";

        internal static bool TryParseEntry(string line, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }
            string[] parts = line.Split('|');
            if (parts.Length != 4
                || !int.TryParse(parts[0], out int pid)
                || !long.TryParse(parts[1], out long ticks))
            {
                return false;
            }
            entry = new Entry
            {
                Pid = pid,
                StartedUtc = new DateTime(ticks, DateTimeKind.Utc),
                Purpose = parts[2],
                Key = parts[3],
            };
            return true;
        }

        /// <summary>프로세스 이미지의 파일명(예: python.exe)을 조회한다.
        /// Process.ProcessName은 Unity Mono에서 동작하지 않아 kernel32를 직접 쓴다.
        /// 조회 불가(권한 없음/비Windows 환경)면 null을 반환한다.</summary>
        private static string QueryImageFileName(int pid)
        {
            IntPtr h = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (h == IntPtr.Zero)
            {
                return null;
            }
            try
            {
                var sb = new System.Text.StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size)
                    ? Path.GetFileName(sb.ToString()) : null;
            }
            finally
            {
                CloseHandle(h);
            }
        }

        private const uint ProcessQueryLimitedInformation = 0x1000;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true,
            CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(
            IntPtr hProcess, uint flags, System.Text.StringBuilder text, ref uint size);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>다른 에디터 인스턴스의 동시 기록과 겹칠 수 있어 공유 위반 시 한 번 재시도한다.</summary>
        private static void WriteLines(string path, List<string> lines)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    File.WriteAllLines(path, lines);
                    return;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }
    }
}
