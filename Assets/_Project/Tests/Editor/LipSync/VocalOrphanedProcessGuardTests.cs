using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Fbx2Vmd.LipSync;
using NUnit.Framework;

namespace Fbx2Vmd.Tests.LipSync
{
    /// <summary>
    /// 고아 프로세스 마커의 직렬화/검증/소거를 단위 테스트한다.
    /// 실제 python 대신 수명을 제어할 수 있는 ping/cmd 프로세스를 띄워 검증한다.
    /// </summary>
    public class VocalOrphanedProcessGuardTests
    {
        private readonly List<Process> _procs = new List<Process>();
        private string _tempDir;
        private Func<string, bool> _savedFilter;
        private Func<int, string> _savedImageProvider;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(),
                "vocal-orphan-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _savedFilter = VocalOrphanedProcessGuard.ProcessNameFilter;
            _savedImageProvider = VocalOrphanedProcessGuard.ImageFileNameProvider;
            // 테스트 프로세스(ping/cmd)를 추적 대상으로 인정하게 필터를 열어 둔다.
            VocalOrphanedProcessGuard.ProcessNameFilter = _ => true;
        }

        [TearDown]
        public void TearDown()
        {
            VocalOrphanedProcessGuard.ProcessNameFilter = _savedFilter;
            VocalOrphanedProcessGuard.ImageFileNameProvider = _savedImageProvider;
            VocalOrphanedProcessGuard.MarkerDirOverride = null;
            foreach (Process p in _procs)
            {
                try { if (!p.HasExited) p.Kill(); } catch (Exception) { }
                p.Dispose();
            }
            _procs.Clear();
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
            catch (Exception) { }
        }

        private Process SpawnLongRunning()
        {
            // 약 29초 동안 살아 있는 프로세스 — 테스트 내에서 명시적으로 종료한다.
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = "ping",
                Arguments = "-n 30 127.0.0.1",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
            _procs.Add(p);
            return p;
        }

        // 원본 핸들의 StartTime은 Unity Mono에서 불안정하므로 PID 재오픈으로 읽는다.
        private static DateTime StartUtc(Process p)
        {
            using (Process q = Process.GetProcessById(p.Id))
            {
                return q.StartTime.ToUniversalTime();
            }
        }

        // ---------- 직렬화 ----------

        [Test]
        public void Entry_직렬화_왕복()
        {
            var e = new VocalOrphanedProcessGuard.Entry
            {
                Pid = 12345,
                StartedUtc = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc),
                Purpose = "clean",
                Key = "D:\\proj\\out\\song_vocal.wav",
            };
            string line = VocalOrphanedProcessGuard.SerializeEntry(e);
            Assert.IsTrue(VocalOrphanedProcessGuard.TryParseEntry(line, out var parsed));
            Assert.AreEqual(e.Pid, parsed.Pid);
            Assert.AreEqual(e.StartedUtc, parsed.StartedUtc);
            Assert.AreEqual(e.Purpose, parsed.Purpose);
            Assert.AreEqual(e.Key, parsed.Key);
        }

        [Test]
        public void TryParseEntry_형식오류_거부()
        {
            Assert.IsFalse(VocalOrphanedProcessGuard.TryParseEntry("", out _));
            Assert.IsFalse(VocalOrphanedProcessGuard.TryParseEntry(null, out _));
            Assert.IsFalse(VocalOrphanedProcessGuard.TryParseEntry("a|b|c", out _));
            Assert.IsFalse(VocalOrphanedProcessGuard.TryParseEntry("x|2|clean|k", out _));
            Assert.IsFalse(VocalOrphanedProcessGuard.TryParseEntry("1|x|clean|k", out _));
        }

        // ---------- 마커 파일 ----------

        [Test]
        public void Register_Unregister_마커생성과제거()
        {
            Process p = SpawnLongRunning();
            VocalOrphanedProcessGuard.Register(p, "clean", "k1", _tempDir);

            string path = VocalOrphanedProcessGuard.MarkerPath(_tempDir);
            Assert.IsTrue(File.Exists(path));
            var lines = File.ReadAllLines(path);
            Assert.AreEqual(1, lines.Length);
            Assert.IsTrue(VocalOrphanedProcessGuard.TryParseEntry(lines[0], out var e));
            Assert.AreEqual(p.Id, e.Pid);
            Assert.AreEqual("clean", e.Purpose);
            Assert.AreEqual("k1", e.Key);

            VocalOrphanedProcessGuard.Unregister(p.Id, _tempDir);
            Assert.AreEqual(0, File.ReadAllLines(path).Length);
        }

        // ---------- 프로세스 검증 ----------

        [Test]
        public void TryAttach_생존프로세스_핸들반환()
        {
            Process p = SpawnLongRunning();
            Assert.IsTrue(VocalOrphanedProcessGuard.TryAttach(
                p.Id, StartUtc(p), out Process attached));
            Assert.IsFalse(attached.HasExited);
            attached.Dispose();
        }

        [Test]
        public void TryAttach_시작시각불일치_거부()
        {
            Process p = SpawnLongRunning();
            // 기록된 시작 시각이 실제보다 미래면 PID 재사용의 다른 프로세스로 본다.
            Assert.IsFalse(VocalOrphanedProcessGuard.TryAttach(
                p.Id, DateTime.UtcNow.AddHours(1), out _));
        }

        [Test]
        public void TryAttach_이미지명불일치_거부()
        {
            Process p = SpawnLongRunning();
            VocalOrphanedProcessGuard.ProcessNameFilter =
                n => n.StartsWith("python", StringComparison.OrdinalIgnoreCase);
            Assert.IsFalse(VocalOrphanedProcessGuard.TryAttach(
                p.Id, StartUtc(p), out _));
        }

        // ---------- 생존 탐지(중복 실행 방지) ----------

        [Test]
        public void TryFindLive_같은키만_매칭()
        {
            Process p = SpawnLongRunning();
            VocalOrphanedProcessGuard.Register(p, "separate", "songA.mp3", _tempDir);

            Assert.IsTrue(VocalOrphanedProcessGuard.TryFindLive(
                "separate", "songA.mp3", out var found, _tempDir));
            Assert.AreEqual(p.Id, found.Pid);
            Assert.IsFalse(VocalOrphanedProcessGuard.TryFindLive(
                "separate", "songB.mp3", out _, _tempDir));
            Assert.IsFalse(VocalOrphanedProcessGuard.TryFindLive(
                "clean", "songA.mp3", out _, _tempDir));
        }

        [Test]
        public void TryFindLive_종료된프로세스_미탐지()
        {
            var exited = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd", Arguments = "/c exit 0",
                CreateNoWindow = true, UseShellExecute = false,
            });
            _procs.Add(exited);
            exited.WaitForExit();
            VocalOrphanedProcessGuard.Register(exited, "clean", "k", _tempDir);
            Assert.IsFalse(VocalOrphanedProcessGuard.TryFindLive(
                "clean", "k", out _, _tempDir));
        }

        // ---------- 소거 ----------

        [Test]
        public void SweepOrphans_죽은항목_제거()
        {
            var exited = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd", Arguments = "/c exit 0",
                CreateNoWindow = true, UseShellExecute = false,
            });
            _procs.Add(exited);
            exited.WaitForExit();
            VocalOrphanedProcessGuard.Register(exited, "clean", "k", _tempDir);

            VocalOrphanedProcessGuard.SweepOrphans(_tempDir);
            Assert.AreEqual(0, File.ReadAllLines(
                VocalOrphanedProcessGuard.MarkerPath(_tempDir)).Length);
        }

        [Test]
        public void SweepOrphans_젊은생존항목_유지()
        {
            Process p = SpawnLongRunning();
            VocalOrphanedProcessGuard.Register(p, "clean", "k", _tempDir);

            VocalOrphanedProcessGuard.SweepOrphans(_tempDir);
            Assert.AreEqual(1, File.ReadAllLines(
                VocalOrphanedProcessGuard.MarkerPath(_tempDir)).Length);
            Assert.IsFalse(p.HasExited); // 젊은 고아는 죽이지 않고 채택 경로에 맡긴다.
        }

        [Test]
        public void SweepOrphans_타임아웃초과_종료()
        {
            Process p = SpawnLongRunning();
            VocalOrphanedProcessGuard.Register(p, "clean", "k", _tempDir);

            // 기준 시각을 미래로 고정해 수명을 초과한 좀비 상황을 만든다.
            DateTime future = DateTime.UtcNow
                .AddSeconds(VocalOrphanedProcessGuard.MaxOrphanAgeSeconds + 60);
            VocalOrphanedProcessGuard.SweepOrphans(_tempDir, nowUtc: future);

            Assert.AreEqual(0, File.ReadAllLines(
                VocalOrphanedProcessGuard.MarkerPath(_tempDir)).Length);
            p.WaitForExit(5000);
            Assert.IsTrue(p.HasExited); // 트리째(taskkill /T) 종료됐어야 한다.
        }

        [Test]
        public void SweepOrphans_검증불가생존항목_유지()
        {
            // 살아 있지만 이미지명을 조회할 수 없는 프로세스는
            // 죽은 것으로 오인해 추적을 삭제하면 안 된다(좀비 은닉 방지).
            Process p = SpawnLongRunning();
            VocalOrphanedProcessGuard.Register(p, "clean", "k", _tempDir);
            VocalOrphanedProcessGuard.ImageFileNameProvider = _ => null;

            VocalOrphanedProcessGuard.SweepOrphans(_tempDir);
            Assert.AreEqual(1, File.ReadAllLines(
                VocalOrphanedProcessGuard.MarkerPath(_tempDir)).Length);
            Assert.IsFalse(p.HasExited); // 검증 불가 항목도 죽이지 않는다.
        }

        [Test]
        public void SweepOrphans_이미지불일치생존항목_제거()
        {
            // PID는 살아 있지만 이미지가 python이 아니면
            // 고아는 이미 죽고 PID가 재사용된 것 — 항목을 지운다.
            Process p = SpawnLongRunning();
            VocalOrphanedProcessGuard.Register(p, "clean", "k", _tempDir);
            VocalOrphanedProcessGuard.ImageFileNameProvider = _ => "notepad.exe";
            VocalOrphanedProcessGuard.ProcessNameFilter = _savedFilter;

            VocalOrphanedProcessGuard.SweepOrphans(_tempDir);
            Assert.AreEqual(0, File.ReadAllLines(
                VocalOrphanedProcessGuard.MarkerPath(_tempDir)).Length);
            Assert.IsFalse(p.HasExited); // 남의 프로세스는 절대 죽이지 않는다.
        }

        // ---------- Job Object(Phase 3) ----------

        [Test]
        public void Job_TryAssign_생존프로세스_성공()
        {
            if (!VocalOrphanedProcessJob.IsAvailable)
            {
                Assert.Ignore("Job Object 미지원 환경 — 마커 폴백만 사용한다.");
            }
            Process p = SpawnLongRunning();
            Assert.IsTrue(VocalOrphanedProcessJob.TryAssign(p));
        }

        [Test]
        public void Job_TryAssign_null_안전()
        {
            Assert.IsFalse(VocalOrphanedProcessJob.TryAssign(null));
        }

        [Test]
        public void Job_TryAssign_종료프로세스_실패지만_예외없음()
        {
            var exited = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd", Arguments = "/c exit 0",
                CreateNoWindow = true, UseShellExecute = false,
            });
            _procs.Add(exited);
            exited.WaitForExit();
            // 종료된 프로세스 핸들 접근은 이 호출만 false — Job 자체는 유지된다.
            Assert.IsFalse(VocalOrphanedProcessJob.TryAssign(exited));
            Process p = SpawnLongRunning();
            // 직전 실패가 Job을 비활성화하지 않았다면 Windows에서는 성공한다.
            Assert.AreEqual(VocalOrphanedProcessJob.IsAvailable,
                VocalOrphanedProcessJob.TryAssign(p));
        }

        [Test]
        public void Job_킬온클로즈_등록확인()
        {
            // Job 등록이 실제로 성립했는지 IsProcessInJob으로 확인한다.
            if (!VocalOrphanedProcessJob.IsAvailable)
            {
                Assert.Ignore("Job Object 미지원 환경.");
            }
            Process p = SpawnLongRunning();
            Assert.IsTrue(VocalOrphanedProcessJob.TryAssign(p));
            Assert.IsTrue(VocalOrphanedProcessJob.IsInJob(p));
        }
    }
}
