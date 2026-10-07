using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 분리된 스템/원본 음원을 에디터에서 미리듣기한다.
    /// Unity의 AudioUtil 프리뷰는 출력이 나지 않는 환경이 있어(IsPlaying=true인데
    /// 실제 무음) 외부 호스트 프로세스(Tools/LipSync/preview_host.ps1, WPF
    /// MediaPlayer)로 OS 기본 디바이스에 직접 재생한다.
    /// 위치/길이는 호스트가 status 파일로 실측 보고하므로 진행바가 실제 재생과
    /// 일치한다. 한 번에 하나만 재생한다.
    /// </summary>
    [InitializeOnLoad]
    public static class StemAudioPreview
    {
        /// <summary>경로가 어떤 로더로 재생 가능한지 — 순수 판별이라 테스트 가능.</summary>
        public enum Loader
        {
            Unsupported = 0,
            AssetDatabase = 1, // 프로젝트 안 — 임포터가 mp3/ogg/flac 등도 처리
            WavFile = 2,       // 프로젝트 밖 — PCM WAV만
        }

        /// <summary>현재 재생 중인 경로. 없으면 null.</summary>
        public static string PlayingPath { get; private set; }

        static Process _host;
        static string _dir;       // 호스트 인스턴스별 고유 cmd/status/hb 디렉터리
        static int _seq;          // 명령 일련번호(중복 실행 방지)
        static double _lastHb;
        static float _posSec;
        static float _durSec;
        static float _probedDurSec; // 호스트 보고 전 wav 길이
        static bool _hostPlaying;
        static double _statusReadAt;
        static double _posHoldUntil; // Seek 직후 호스트의 stale pos로 덮어쓰지 않게 보류 시간
        static string _hostError;

        static StemAudioPreview()
        {
            // 도메인이 살아 있는 동안 하트비트를 유지한다 — 호스트는 hb가
            // 8초 이상 정체되면 자동 종료하므로 리로드/종료 시 고아 프로세스가 안 생긴다.
            EditorApplication.update += Heartbeat;
        }

        /// <summary>클립 길이(초). 재생 중이 아니면 0.</summary>
        public static float DurationSec
        {
            get
            {
                RefreshStatus();
                return _durSec > 0f ? _durSec : _probedDurSec;
            }
        }

        /// <summary>재생 위치(초) — 호스트 실측값.</summary>
        public static float PositionSec
        {
            get
            {
                RefreshStatus();
                return _posSec;
            }
        }

        /// <summary>
        /// 경로의 로더를 판별한다.
        /// FileUtil.GetProjectRelativePath는 프로젝트 밖 경로에 빈 문자열을 돌려준다.
        /// </summary>
        public static Loader Classify(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Loader.Unsupported;
            }
            // 슬래시로 통일 — FileUtil/GetFullPath는 역슬래시 섞인 입력을 오판할 수 있다.
            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return Loader.AssetDatabase;
            }
            // 임포터는 Assets/ 아래만 처리한다 — 프로젝트 안이어도 Assets 밖(Tools 등)은 WavFile로.
            string rel = FileUtil.GetProjectRelativePath(normalized);
            if (!string.IsNullOrEmpty(rel)
                && rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return Loader.AssetDatabase;
            }
            return string.Equals(Path.GetExtension(path), ".wav",
                StringComparison.OrdinalIgnoreCase)
                ? Loader.WavFile
                : Loader.Unsupported;
        }

        /// <summary>
        /// 해당 경로를 재생한다. 같은 경로가 재생 중이면 정지(토글).
        /// 반환값은 에러 메시지 — null이면 성공.
        /// </summary>
        public static string Toggle(string path)
        {
            if (PlayingPath == path && IsPlaying())
            {
                Stop();
                return null;
            }
            Stop();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return "파일이 없습니다: " + path;
            }
            // 외부 호스트는 wav/mp3/m4a 등 MediaPlayer가 아는 포맷을 전부 재생한다.
            string error = EnsureHost();
            if (error != null)
            {
                return error;
            }

            PlayingPath = path;
            _probedDurSec = ProbeDuration(path);
            _posSec = 0f;
            _durSec = 0f;
            _hostPlaying = true;
            // 호스트는 외부 프로세스라 프로젝트 상대 경로를 모른다 — 절대 경로로 보낸다.
            Send("play|0|" + Path.GetFullPath(path));
            return null;
        }

        /// <summary>
        /// 재생 위치를 지정 초로 이동한다. 재생 중이 아니면 아무것도 하지 않는다.
        /// 종료/정지 상태에서 시크하면 그 위치부터 다시 재생된다.
        /// </summary>
        public static void Seek(float seconds)
        {
            if (PlayingPath == null)
            {
                return;
            }
            float t = Mathf.Max(0f, seconds);
            float dur = DurationSec; // 호스트 보고 전엔 프로브 길이로 상한을 잡는다.
            if (dur > 0f)
            {
                t = Mathf.Min(t, dur);
            }
            Send("seek|" + (int)(t * 1000));
            _posSec = t;
            _hostPlaying = true;
            // 호스트가 시크를 처리해 status가 따라잡기까지 이전 위치로 덮어쓰지 않는다.
            _posHoldUntil = EditorApplication.timeSinceStartup + 0.5;
        }

        /// <summary>재생을 멈춘다.</summary>
        public static void Stop()
        {
            if (_host != null && !_host.HasExited)
            {
                Send("stop");
            }
            PlayingPath = null;
            _hostPlaying = false;
            _posSec = 0f;
            _durSec = 0f;
            _probedDurSec = 0f;
            _posHoldUntil = 0;
            _hostError = null;
        }

        /// <summary>재생 중인지 — 호스트 상태 파일 기준.</summary>
        public static bool IsPlaying()
        {
            RefreshStatus();
            if (!_hostPlaying || PlayingPath == null)
            {
                return false;
            }
            float dur = DurationSec;
            return dur <= 0f || _posSec < dur;
        }

        /// <summary>자연 종료 시 상태를 비운다. 상태가 바뀌었으면 true.</summary>
        public static bool ClearIfFinished()
        {
            if (PlayingPath == null || IsPlaying())
            {
                return false;
            }
            Stop();
            return true;
        }

        // ---------- 호스트 프로세스 ----------

        static string EnsureHost()
        {
            if (_host != null && !_host.HasExited)
            {
                return null;
            }
            string script = Path.GetFullPath(Path.Combine(
                Application.dataPath, "../Tools/LipSync/preview_host.ps1"));
            if (!File.Exists(script))
            {
                return "미리듣기 호스트가 없습니다: " + script;
            }
            // 인스턴스마다 고유 디렉터리 — 리로드 후 8초 내 재기동해도 구 호스트의
            // hb 갱신과 충돌하지 않는다(구 호스트는 정체 감지로 자동 종료).
            _dir = Path.Combine(Path.GetTempPath(), "lipsync_preview_"
                + Process.GetCurrentProcess().Id + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Directory.CreateDirectory(_dir);
                File.WriteAllText(HbPath, DateTime.Now.Ticks.ToString());
            }
            catch (Exception e)
            {
                return "미리듣기 작업 디렉터리를 만들지 못했습니다: " + e.Message;
            }
            _lastHb = EditorApplication.timeSinceStartup;

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -STA -ExecutionPolicy Bypass"
                    + " -WindowStyle Hidden -File \"" + script
                    + "\" -Dir \"" + _dir + "\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            try
            {
                _host = Process.Start(psi);
            }
            catch (Exception e)
            {
                _host = null;
                return "미리듣기 호스트를 실행하지 못했습니다: " + e.Message;
            }
            if (_host == null)
            {
                return "미리듣기 호스트를 시작하지 못했습니다.";
            }
            // 호스트가 첫 status를 쓸 때까지 최대 4초 기다린다.
            double deadline = EditorApplication.timeSinceStartup + 4.0;
            while (EditorApplication.timeSinceStartup < deadline)
            {
                if (File.Exists(StatusPath))
                {
                    return null;
                }
                if (_host.HasExited)
                {
                    break;
                }
                System.Threading.Thread.Sleep(50);
            }
            KillHost();
            return "미리듣기 호스트를 시작하지 못했습니다.";
        }

        static void Send(string command)
        {
            if (_dir == null)
            {
                return;
            }
            _seq++;
            try
            {
                // 임시 파일 + 원자적 이동 — 호스트가 부분만 쓰인 명령을 읽지 않게 한다.
                string tmp = Path.Combine(_dir, "cmd.tmp");
                File.WriteAllText(tmp, _seq + "|" + command);
                if (File.Exists(CmdPath))
                {
                    File.Delete(CmdPath);
                }
                File.Move(tmp, CmdPath);
            }
            catch (Exception e)
            {
                _hostError = "명령 전송 실패: " + e.Message;
                Debug.LogWarning("[StemAudioPreview] " + _hostError);
            }
        }

        static string CmdPath => Path.Combine(_dir, "cmd.txt");
        static string StatusPath => Path.Combine(_dir, "status.txt");
        static string HbPath => Path.Combine(_dir, "hb.txt");

        /// <summary>상태 파일을 50ms마다 한 번씩 읽어 위치/길이/재생을 갱신한다.</summary>
        static void RefreshStatus()
        {
            // 정지 직후 status에 아직 playing=1이 남아 있을 수 있으므로
            // 재생할 대상이 없으면 읽지 않는다.
            if (PlayingPath == null)
            {
                _hostPlaying = false;
                return;
            }
            // 호스트가 죽었으면 마지막 status 파일이 남아 있어도 재생 중으로 간주하지 않는다.
            if (_host != null && _host.HasExited)
            {
                _hostPlaying = false;
                return;
            }
            if (_dir == null
                || EditorApplication.timeSinceStartup - _statusReadAt < 0.05)
            {
                return;
            }
            _statusReadAt = EditorApplication.timeSinceStartup;
            string text;
            try
            {
                text = File.ReadAllText(StatusPath);
            }
            catch
            {
                return; // 호스트가 아직 안 썼거나 종료 중 — 이전 값 유지
            }
            foreach (string kv in text.Trim().Split(';'))
            {
                int eq = kv.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                string val = kv.Substring(eq + 1);
                switch (kv.Substring(0, eq))
                {
                    case "pos":
                        if (EditorApplication.timeSinceStartup >= _posHoldUntil)
                        {
                            int.TryParse(val, out int ms);
                            _posSec = ms / 1000f;
                        }
                        break;
                    case "dur": int.TryParse(val, out int dms); if (dms > 0) _durSec = dms / 1000f; break;
                    case "playing":
                        // 시크 직후 홀드 중엔 호스트의 이전 상태로 덮어쓰지 않는다.
                        if (EditorApplication.timeSinceStartup >= _posHoldUntil)
                        {
                            _hostPlaying = val == "1";
                        }
                        break;
                    case "err":
                        // 호스트 보고 오류는 로그로 표면화 — "재생 중인데 무음" 재발 방지.
                        if (val.Length > 0 && _hostError != val)
                        {
                            _hostError = val;
                            Debug.LogWarning("[StemAudioPreview] 호스트 오류: " + val);
                        }
                        break;
                }
            }
        }

        static void Heartbeat()
        {
            if (_host == null || _host.HasExited || _dir == null)
            {
                return;
            }
            if (EditorApplication.timeSinceStartup - _lastHb < 1.0)
            {
                return;
            }
            _lastHb = EditorApplication.timeSinceStartup;
            try { File.WriteAllText(HbPath, DateTime.Now.Ticks.ToString()); }
            catch { /* 호스트가 디렉터리를 정리했으면 무시 */ }
        }

        static void KillHost()
        {
            try
            {
                if (_host != null && !_host.HasExited)
                {
                    _host.Kill();
                }
            }
            catch { }
            _host = null;
        }

        /// <summary>wav는 헤더 파싱으로 즉시 길이를 알고, 나머지는 호스트 보고를 기다린다.</summary>
        static float ProbeDuration(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".wav",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 0f;
            }
            var clip = WavFileReader.Load(path, out _);
            if (clip == null)
            {
                return 0f;
            }
            float len = clip.length;
            UnityEngine.Object.DestroyImmediate(clip);
            return len;
        }
    }
}
