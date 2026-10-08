using System;
using System.IO;
using NAudio.Wave;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 분리된 스템/원본 음원을 에디터에서 미리듣기한다.
    /// Unity의 AudioUtil 프리뷰는 출력이 나지 않는 환경이 있어(IsPlaying=true인데
    /// 실제 무음) NAudio를 에디터 내에서 직접 재생한다 — 실제 사운드 출력이며
    /// 위치/길이를 MediaFoundationReader에서 실측으로 읽는다.
    /// 출력 장치는 WinMM 출력 장치 중에서 선택할 수 있다
    /// (기본 장치와 실제로 듣는 장치가 다를 때의 무음 방지). 한 번에 하나만 재생한다.
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

        const int MaxOutputRetries = 3;
        const string DeviceNumPrefKey = "Fbx2Vmd.LipSync.PreviewDevice";
        const string DeviceNamePrefKey = "Fbx2Vmd.LipSync.PreviewDeviceName";

        /// <summary>현재 재생 중인 경로. 없으면 null.</summary>
        public static string PlayingPath { get; private set; }

        static GatedWaveProvider _reader; // wav/mp3/m4a 모두 지원 — Read/Dispose 직렬화 래퍼
        static WaveOutEvent _out;
        static bool _finished;                // 자연 종료 감지(PlaybackStopped 이벤트)
        static int _outputRetries;            // 출력 스레드 사망 시 재시작 횟수
        static bool _retryPending;            // winmm 스레드 → 메인 스레드 재시작 요청

        // winmm 콜백 스레드(PlaybackStopped)와 메인 스레드의 공유 상태 직렬화.
        // lock 안에서는 참조/플래그만 다루고 waveOut 호출은 절대 하지 않는다 —
        // waveOutReset이 콜백 스레드 완료를 기다리므로 lock을 잡은 채 호출하면 교착한다.
        static readonly object _gate = new object();

        // 장치 이름 캐시 — OnGUI마다 장치당 P/Invoke + 마샬링을 하지 않게 한다.
        static string[] _deviceNameCache;
        static double _deviceNameCacheAt;
        const double DeviceCacheTtlSec = 5.0;

        static StemAudioPreview()
        {
            // 도메인 리로드/에디터 종료 시 NAudio 스레드가 살아 남지 않게 정리한다.
            // 주의: waveOutReset은 메인 스레드를 블록할 수 있어 리로드 경로에서는
            // 백그라운드로 넘긴다 — 재생 중 리로드하면 에디터가 멈출 수 있다.
            AssemblyReloadEvents.beforeAssemblyReload += StopAsync;
            EditorApplication.quitting += StopAsync;
            // 출력 사망 재시작은 메인 스레드에서 처리 — 콜백 스레드에서 정적 상태를
            // 바꾸지 않는다.
            EditorApplication.update += PumpRetry;
        }

        /// <summary>클립 길이(초). 재생 중이 아니면 0.</summary>
        public static float DurationSec
        {
            get
            {
                var r = _reader;
                if (r == null)
                {
                    return 0f;
                }
                try { return (float)r.TotalTime.TotalSeconds; } catch { return 0f; }
            }
        }

        /// <summary>재생 위치(초) — 리더 실측값.</summary>
        public static float PositionSec
        {
            get
            {
                var r = _reader;
                if (r == null)
                {
                    return 0f;
                }
                try { return (float)r.CurrentTime.TotalSeconds; } catch { return 0f; }
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

            string error = StartPlayback(path, 0f);
            if (error != null)
            {
                return error;
            }
            PlayingPath = path;
            return null;
        }

        /// <summary>
        /// 재생 위치를 지정 초로 이동한다. 재생 중이 아니면 아무것도 하지 않는다.
        /// 자연 종료 상태에서 시크하면 그 위치부터 다시 재생된다.
        /// </summary>
        public static void Seek(float seconds)
        {
            if (PlayingPath == null)
            {
                return;
            }
            float t = Mathf.Max(0f, seconds);
            if (DurationSec > 0f)
            {
                t = Mathf.Min(t, DurationSec);
            }
            var reader = _reader;
            var output = _out;
            if (reader == null || output == null)
            {
                // 도달하면 정리가 반만 된 상태 — 같은 파일을 해당 위치부터 다시 연다.
                if (File.Exists(PlayingPath))
                {
                    StartPlayback(PlayingPath, t);
                }
                return;
            }
            try
            {
                reader.CurrentTime = TimeSpan.FromSeconds(t);
            }
            catch { return; }
            _finished = false;
            if (output.PlaybackState != PlaybackState.Playing)
            {
                try { output.Play(); } catch { }
            }
        }

        /// <summary>재생을 멈춘다.</summary>
        public static void Stop()
        {
            WaveOutEvent outToKill;
            GatedWaveProvider readerToKill;
            lock (_gate)
            {
                PlayingPath = null;
                _finished = false;
                _retryPending = false;
                _outputRetries = MaxOutputRetries; // 진행 중 재시도 요청 차단
                outToKill = _out;
                readerToKill = _reader;
                _out = null;
                _reader = null;
            }
            // winmm 호출은 lock 밖에서 — 콜백 스레드와의 교착 방지.
            try { outToKill?.Stop(); } catch { }
            try { outToKill?.Dispose(); } catch { }
            try { readerToKill?.Dispose(); } catch { }
        }

        /// <summary>리로드 경로용 — winmm 정리를 백그라운드로 넘겨 메인 스레드를 블록하지 않는다.</summary>
        static void StopAsync()
        {
            WaveOutEvent outToKill;
            GatedWaveProvider readerToKill;
            lock (_gate)
            {
                PlayingPath = null;
                _finished = false;
                _retryPending = false;
                _outputRetries = MaxOutputRetries;
                outToKill = _out;
                readerToKill = _reader;
                _out = null;
                _reader = null;
            }
            if (outToKill == null && readerToKill == null)
            {
                return;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try { outToKill?.Stop(); } catch { }
                try { outToKill?.Dispose(); } catch { }
                try { readerToKill?.Dispose(); } catch { }
            });
        }

        /// <summary>재생 중인지 — 실제 재생 상태 기준.</summary>
        public static bool IsPlaying()
        {
            var output = _out;
            if (output == null || PlayingPath == null || _finished)
            {
                return false;
            }
            if (output.PlaybackState != PlaybackState.Playing)
            {
                return false;
            }
            float dur = DurationSec;
            return dur <= 0f || PositionSec < dur;
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

        // ---------- 출력 장치 선택 ----------

        /// <summary>WinMM 출력 장치 이름 목록. index -1은 사운드 매퍼(기본 장치 추적).</summary>
        public static string[] GetOutputDeviceNames()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_deviceNameCache != null && now - _deviceNameCacheAt < DeviceCacheTtlSec)
            {
                return _deviceNameCache;
            }
            var names = new System.Collections.Generic.List<string>();
            try
            {
                int n = WaveInterop.waveOutGetNumDevs();
                for (int i = -1; i < n; i++)
                {
                    var caps = new WaveOutCapabilities();
                    WaveInterop.waveOutGetDevCaps(new IntPtr(i), out caps,
                        System.Runtime.InteropServices.Marshal.SizeOf(caps));
                    names.Add(i == -1
                        ? caps.ProductName + " (Windows 기본 장치)"
                        : caps.ProductName);
                }
            }
            catch { }
            _deviceNameCache = names.ToArray();
            _deviceNameCacheAt = now;
            return _deviceNameCache;
        }

        /// <summary>선택된 WinMM 장치 번호. -1이면 Windows 기본 장치(사운드 매퍼).</summary>
        public static int SelectedDeviceNumber
        {
            get { return EditorPrefs.GetInt(DeviceNumPrefKey, -1); }
            set
            {
                EditorPrefs.SetInt(DeviceNumPrefKey, value);
                // 장치 순번은 탈착으로 바뀌므로 이름도 함께 저장해 불일치를 감지한다.
                EditorPrefs.SetString(DeviceNamePrefKey, GetDeviceName(value) ?? "");
            }
        }

        /// <summary>해당 번호 장치의 현재 이름. 장치가 없으면 null.</summary>
        static string GetDeviceName(int deviceNumber)
        {
            try
            {
                if (deviceNumber < -1 || deviceNumber >= WaveInterop.waveOutGetNumDevs())
                {
                    return null;
                }
                var caps = new WaveOutCapabilities();
                WaveInterop.waveOutGetDevCaps(new IntPtr(deviceNumber), out caps,
                    System.Runtime.InteropServices.Marshal.SizeOf(caps));
                return caps.ProductName;
            }
            catch { return null; }
        }

        /// <summary>
        /// 재생에 사용할 장치 번호. 저장된 이름과 현재 이름이 다르면(장치 재배열)
        /// 같은 이름의 장치를 찾고, 못 찾으면 매퍼(-1)로 폴백한다.
        /// </summary>
        static int ResolveDeviceNumber()
        {
            int num = SelectedDeviceNumber;
            if (num == -1)
            {
                return -1;
            }
            string savedName = EditorPrefs.GetString(DeviceNamePrefKey, "");
            string currentName = GetDeviceName(num);
            if (currentName == savedName)
            {
                return num;
            }
            if (!string.IsNullOrEmpty(savedName))
            {
                try
                {
                    int n = WaveInterop.waveOutGetNumDevs();
                    for (int i = 0; i < n; i++)
                    {
                        var caps = new WaveOutCapabilities();
                        WaveInterop.waveOutGetDevCaps(new IntPtr(i), out caps,
                            System.Runtime.InteropServices.Marshal.SizeOf(caps));
                        if (caps.ProductName == savedName)
                        {
                            return i;
                        }
                    }
                }
                catch { }
            }
            return -1; // 장치를 못 찾으면 Windows 기본 장치로 폴백
        }

        // ---------- 내부 ----------

        static string StartPlayback(string path, float startSec)
        {
            GatedWaveProvider reader;
            try
            {
                reader = new GatedWaveProvider(
                    new MediaFoundationReader(Path.GetFullPath(path)));
            }
            catch (Exception e)
            {
                return "오디오를 열지 못했습니다: " + e.Message;
            }
            WaveOutEvent output = null;
            try
            {
                // Mono의 waveOut 콜백 지터를 흡수하게 버퍼를 넉넉히 — 기본값(2개)은
                // 버퍼 언더런 시 WaveHeaderUnprepared로 출력 스레드가 죽는다.
                output = new WaveOutEvent
                {
                    DeviceNumber = ResolveDeviceNumber(),
                    DesiredLatency = 200,
                    NumberOfBuffers = 5,
                };
                output.PlaybackStopped += OnPlaybackStopped;
                if (startSec > 0f)
                {
                    reader.CurrentTime = TimeSpan.FromSeconds(startSec);
                }
                output.Init(reader);
            }
            catch (Exception e)
            {
                try { output?.Dispose(); } catch { }
                try { reader.Dispose(); } catch { }
                return "재생을 시작하지 못했습니다: " + e.Message;
            }
            lock (_gate)
            {
                _reader = reader;
                _out = output;
                _finished = false;
                _outputRetries = 0;
            }
            try
            {
                output.Play();
            }
            catch (Exception e)
            {
                lock (_gate) { _out = null; _reader = null; }
                try { output.Dispose(); } catch { }
                try { reader.Dispose(); } catch { }
                return "재생을 시작하지 못했습니다: " + e.Message;
            }
            return null;
        }

        /// <summary>
        /// winmm 재생 스레드 콜백 — 플래그만 세우고 실제 재시작은 메인 스레드
        /// (EditorApplication.update → PumpRetry)에서 한다. 여기서 Unity API나
        /// 출력 객체를 만들면 스레드 경합이 생긴다.
        /// </summary>
        static void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            lock (_gate)
            {
                // Stop() 직후 새 재생이 시작된 경우 구 출력의 이벤트가 새 상태를
                // 오염시키지 않게 한다.
                if (!ReferenceEquals(sender, _out))
                {
                    return;
                }
                // Mono에서 WaveOutEvent는 간헐적으로 WaveHeaderUnprepared로 출력
                // 스레드가 죽는다 — 남은 구간이 있으면 재시작을 요청한다.
                if (e.Exception != null && PlayingPath != null
                    && _reader != null && _outputRetries < MaxOutputRetries)
                {
                    _outputRetries++;
                    _retryPending = true;
                    return;
                }
                _finished = true;
            }
            if (e.Exception != null)
            {
                UnityEngine.Debug.LogWarning(
                    "[StemAudioPreview] 재생 오류: " + e.Exception.Message);
            }
        }

        /// <summary>메인 스레드(update)에서 죽은 출력을 재생성해 이어서 재생한다.</summary>
        static void PumpRetry()
        {
            GatedWaveProvider reader;
            string path;
            lock (_gate)
            {
                if (!_retryPending)
                {
                    return;
                }
                _retryPending = false;
                reader = _reader;
                path = PlayingPath;
                // 재시도 조건 재확인 — 요청 후 Stop/Seek이 끼어들었을 수 있다.
                if (reader == null || path == null || _finished)
                {
                    return;
                }
                if (PositionSec >= DurationSec - 0.05f)
                {
                    _finished = true;
                    return;
                }
            }
            var fresh = new WaveOutEvent
            {
                DeviceNumber = ResolveDeviceNumber(),
                DesiredLatency = 200,
                NumberOfBuffers = 5,
            };
            fresh.PlaybackStopped += OnPlaybackStopped;
            try
            {
                fresh.Init(reader);
            }
            catch (Exception ex)
            {
                try { fresh.Dispose(); } catch { }
                UnityEngine.Debug.LogWarning(
                    "[StemAudioPreview] 출력 재시작 실패: " + ex.Message);
                lock (_gate) { _finished = true; }
                return;
            }
            WaveOutEvent stale = null;
            bool adopted = false;
            lock (_gate)
            {
                // 그 사이 Stop()이 _out을 비웠을 수 있다 — 비어 있을 때만 교체.
                if (PlayingPath == null)
                {
                    _finished = true;
                }
                else
                {
                    adopted = true;
                    // 교체되는 구 출력의 hWaveOut 핸들이 누수되지 않게 회수한다.
                    stale = _out;
                    _out = fresh;
                }
            }
            // winmm 호출은 lock 밖에서 — 콜백 스레드와의 교착 방지.
            if (!adopted)
            {
                try { fresh.Dispose(); } catch { }
                return;
            }
            try { stale?.Dispose(); } catch { }
            try { fresh.Play(); } catch { }
        }

        /// <summary>
        /// Read와 Dispose를 한 lock으로 직렬화하는 리더 래퍼.
        /// Mono에서 WaveOutEvent 재생 스레드는 Stop() 후에도 즉시 죽지 않을 수 있고,
        /// 그 스레드가 IMFSourceReader.ReadSample 안에 있는 동안 Dispose가 COM 객체를
        /// 해제하면 mfreadwrite.dll에서 Access Violation으로 에디터가 네이티브 크래시한다.
        /// Dispose는 진행 중 Read가 끝날 때까지 기다리고, Dispose 후의 Read는 0을 반환해
        /// 재생 스레드가 end-of-stream으로 자연 종료되게 한다.
        /// </summary>
        sealed class GatedWaveProvider : IWaveProvider, IDisposable
        {
            readonly object _sync = new object();
            readonly MediaFoundationReader _inner;
            bool _disposed;

            internal GatedWaveProvider(MediaFoundationReader inner)
            {
                _inner = inner;
            }

            public WaveFormat WaveFormat => _inner.WaveFormat;

            public int Read(byte[] buffer, int offset, int count)
            {
                lock (_sync)
                {
                    return _disposed ? 0 : _inner.Read(buffer, offset, count);
                }
            }

            internal TimeSpan TotalTime
            {
                get { lock (_sync) { return _disposed ? TimeSpan.Zero : _inner.TotalTime; } }
            }

            internal TimeSpan CurrentTime
            {
                get { lock (_sync) { return _disposed ? TimeSpan.Zero : _inner.CurrentTime; } }
                set { lock (_sync) { if (!_disposed) _inner.CurrentTime = value; } }
            }

            public void Dispose()
            {
                lock (_sync)
                {
                    _disposed = true;
                    _inner.Dispose();
                }
            }
        }
    }
}
