using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 외부 CLI로 음원을 보컬/반주 스템으로 분리한다.
    /// audio-separator(python 패키지, UVR 모델군)와 demucs 두 엔진을 지원하며
    /// 실제 프로세스 실행과 순수 인자/결과 해석을 분리해 테스트 가능하게 한다.
    /// </summary>
    public static class VocalStemSeparator
    {
        public enum Engine
        {
            AudioSeparator = 0,
            Demucs = 1,
        }

        /// <summary>audio-separator 기본 모델 — MVSEP 기준 보컬 분리 상위권 BS-RoFormer.</summary>
        public const string DefaultModel = "model_bs_roformer_ep_317_sdr_12.9755.ckpt";
        public const string DefaultDemucsModel = "htdemucs_ft";
        public const string PythonPathEnv = "UNITY_FBX2VMD_PYTHON";

        public sealed class Result
        {
            public bool success;
            public string vocalPath = string.Empty;
            public string instrumentalPath = string.Empty;
            public string error = string.Empty;
            public string logTail = string.Empty;
        }

        /// <summary>
        /// 엔진별 CLI 인자열을 만든다. program은 python 또는 audio-separator 실행 파일.
        /// modelDir을 주면 모델 캐시를 그 폴더에 고정한다(audio-separator 전용).
        /// </summary>
        public static string BuildArguments(Engine engine, string inputPath, string outputDir,
            string model, string modelDir = null)
        {
            switch (engine)
            {
                case Engine.AudioSeparator:
                    // audio-separator는 __main__이 없어 -m으로 못 쓴다.
                    // console entry(audio_separator.utils.cli:main)를 -c로 호출한다.
                    return "-c \"from audio_separator.utils.cli import main; main()\" "
                        + Quote(inputPath)
                        + " --output_dir " + Quote(outputDir)
                        + " --output_format wav"
                        + (string.IsNullOrEmpty(model)
                            ? string.Empty
                            : " --model_filename " + Quote(model))
                        + (string.IsNullOrEmpty(modelDir)
                            ? string.Empty
                            : " --model_file_dir " + Quote(modelDir));
                case Engine.Demucs:
                    return "-m demucs --two-stems=vocals"
                        + " -n " + (string.IsNullOrEmpty(model) ? DefaultDemucsModel : model)
                        + " -o " + Quote(outputDir)
                        + " " + Quote(inputPath);
                default:
                    throw new ArgumentOutOfRangeException(nameof(engine));
            }
        }

        /// <summary>출력 디렉터리에서 보컬/반주 파일을 찾는다. 순수 함수라 테스트 가능.
        /// minWriteUtc를 주면 그 시각 이후에 기록된 파일만 대상으로 삼아 이전 실행 산출물을 배제한다.</summary>
        public static void ResolveOutputs(Engine engine, string outputDir, string inputPath,
            out string vocalPath, out string instrumentalPath,
            DateTime? minWriteUtc = null)
        {
            vocalPath = null;
            instrumentalPath = null;
            if (!Directory.Exists(outputDir))
            {
                return;
            }

            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            var files = new List<string>(Directory.GetFiles(outputDir, "*.wav", SearchOption.AllDirectories));
            if (minWriteUtc.HasValue)
            {
                files = files.Where(f => File.GetLastWriteTimeUtc(f) >= minWriteUtc.Value).ToList();
            }
            // 이전 실행의 stale 파일이 남아 있을 수 있으니 최신 기록 파일을 우선한다.
            files.Sort((a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));

            foreach (string f in files)
            {
                if (vocalPath != null && instrumentalPath != null)
                {
                    break; // 최신순 정렬이라 첫 매치가 최신 — 둘 다 찾으면 종료
                }
                string name = Path.GetFileName(f);
                if (engine == Engine.Demucs)
                {
                    // demucs: <out>/<model>/<곡명>/vocals.wav, no_vocals.wav
                    // 곡명은 부모 디렉터리명과 정확히 일치해야 한다(경로 부분 문자열 오매치 방지).
                    string parent = Path.GetFileName(Path.GetDirectoryName(f));
                    bool nameMatch = string.Equals(parent, baseName, StringComparison.OrdinalIgnoreCase);
                    if (name.Equals("vocals.wav", StringComparison.OrdinalIgnoreCase) && nameMatch)
                    {
                        vocalPath ??= f;
                    }
                    else if (name.Equals("no_vocals.wav", StringComparison.OrdinalIgnoreCase) && nameMatch)
                    {
                        instrumentalPath ??= f;
                    }
                }
                else
                {
                    // audio-separator: <곡명>_(Vocals)_<모델>.wav / _(Instrumental)_
                    // baseName 뒤에 반드시 '_'가 와야 해서 song→song2 오매치를 막는다.
                    bool nameMatch = name.StartsWith(baseName + "_", StringComparison.OrdinalIgnoreCase);
                    if (nameMatch
                        && name.IndexOf("(Vocals)", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        vocalPath ??= f;
                    }
                    else if (nameMatch
                        && name.IndexOf("(Instrumental)", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        instrumentalPath ??= f;
                    }
                }
            }
        }

        /// <summary>python 실행 파일을 찾는다. 환경변수 → PATH → 일반 설치 경로 순.</summary>
        public static string FindPython()
        {
            string env = Environment.GetEnvironmentVariable(PythonPathEnv);
            if (!string.IsNullOrEmpty(env) && File.Exists(env))
            {
                return env;
            }
            // PATH 검색은 별도 분리(테스트에서 PATH 오염 방지 위해 호출부가 주입 가능).
            string[] names = { "python.exe", "python3.exe", "py.exe" };
            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string dir in pathVar.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }
                foreach (string name in names)
                {
                    string candidate = Path.Combine(dir.Trim(), name);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            return null;
        }

        /// <summary>엔진 패키지가 python에 설치됐는지 확인한다.</summary>
        public static bool CheckInstalled(Engine engine, string pythonPath, out string error)
        {
            string module = engine == Engine.Demucs ? "demucs" : "audio_separator";
            int code = RunSync(pythonPath,
                "-c \"import " + module + "\"", null, out string output, 120);
            if (code == 0)
            {
                error = string.Empty;
                return true;
            }
            string package = engine == Engine.Demucs ? "demucs" : "audio-separator";
            error = $"{package} 패키지가 없습니다. 설치: \"{pythonPath}\" -m pip install {package}\n{output}";
            return false;
        }

        /// <summary>분리를 백그라운드에서 실행한다. 호출부는 Task 완료를 폴링한다.</summary>
        public static Task<Result> SeparateAsync(Engine engine, string pythonPath,
            string inputPath, string outputDir, string model,
            CancellationToken ct = default,
            IReadOnlyDictionary<string, string> env = null,
            string modelDir = null)
        {
            return Task.Run(() => Separate(
                engine, pythonPath, inputPath, outputDir, model, ct, env, modelDir), ct);
        }

        public static Result Separate(Engine engine, string pythonPath,
            string inputPath, string outputDir, string model,
            CancellationToken ct = default,
            IReadOnlyDictionary<string, string> env = null,
            string modelDir = null)
        {
            var result = new Result();
            if (string.IsNullOrEmpty(pythonPath) || !File.Exists(pythonPath))
            {
                result.error = "python 실행 파일을 찾을 수 없습니다.";
                return result;
            }
            if (!File.Exists(inputPath))
            {
                result.error = $"음원 파일이 없습니다: {inputPath}";
                return result;
            }
            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception error)
            {
                result.error = $"출력 폴더를 만들 수 없습니다: {error.Message}";
                return result;
            }

            string args = BuildArguments(engine, inputPath, outputDir, model, modelDir);
            // FAT32/exFAT의 mtime 2초 단위 때문에 실행 시각보다 살짝 여유를 둔다.
            DateTime runStartUtc = DateTime.UtcNow.AddSeconds(-2);
            int code = RunSync(pythonPath, args, outputDir, out string output,
                timeoutSec: 3600, ct: ct, env: env);
            result.logTail = Tail(output, 4000);
            if (code != 0)
            {
                result.error = $"분리 실패(exit {code}):\n{result.logTail}";
                return result;
            }

            ResolveOutputs(engine, outputDir, inputPath,
                out result.vocalPath, out result.instrumentalPath,
                minWriteUtc: runStartUtc);
            if (result.vocalPath == null)
            {
                result.error = "분리는 성공했으나 보컬 출력을 찾지 못했습니다:\n" + result.logTail;
                return result;
            }
            result.success = true;
            return result;
        }

        /// <summary>
        /// 동기 프로세스 실행 — 프로비저너와 공유한다. ct 취소 시 프로세스를 죽인다.
        /// env를 주면 기존 환경변수 위에 덮어쓴다(모델 캐시 경로 고정 등).
        /// </summary>
        internal static int RunSync(string program, string arguments, string workDir,
            out string output, int timeoutSec, CancellationToken ct = default,
            IReadOnlyDictionary<string, string> env = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = program,
                Arguments = arguments,
                WorkingDirectory = string.IsNullOrEmpty(workDir) ? "." : workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            if (env != null)
            {
                foreach (var kv in env)
                {
                    startInfo.EnvironmentVariables[kv.Key] = kv.Value;
                }
            }
            var sb = new StringBuilder();
            var sbLock = new object();
            try
            {
                using (var process = Process.Start(startInfo))
                {
                    // stdout/stderr를 동시에 비동기로 읽어 버퍼 데드락을 막는다.
                    // 두 핸들러는 별도 스레드에서 오므로 Append는 lock으로 직렬화한다.
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null) lock (sbLock) sb.AppendLine(e.Data);
                    };
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null) lock (sbLock) sb.AppendLine(e.Data);
                    };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    using (ct.Register(() => TryKill(process)))
                    {
                        if (!process.WaitForExit(timeoutSec * 1000))
                        {
                            TryKill(process);
                            lock (sbLock) output = sb + "\n[timeout]";
                            return -1;
                        }
                    }
                    // 비동기 출력 핸들러의 잔여 라인까지 플러시되길 한 번 더 기다린다.
                    process.WaitForExit();
                    lock (sbLock) output = sb.ToString();
                    if (ct.IsCancellationRequested)
                    {
                        output += "\n[cancelled]";
                        return -1;
                    }
                    return process.ExitCode;
                }
            }
            catch (OperationCanceledException)
            {
                lock (sbLock) output = sb + "\n[cancelled]";
                return -1;
            }
            catch (Exception error)
            {
                output = error.ToString();
                return -2;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process.HasExited)
                {
                    return;
                }
                // Process.Kill은 직계 프로세스만 종료하므로 demucs 워커 같은
                // 손자 프로세스까지 끝내려고 Windows taskkill /T를 먼저 쓴다.
                try
                {
                    using (var killer = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {process.Id} /T /F",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }))
                    {
                        killer?.WaitForExit(5000);
                    }
                }
                catch (Exception)
                {
                    // taskkill 실패 시 직계 종료로 폴백한다.
                }
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception)
            {
                // 이미 종료된 프로세스는 무시한다.
            }
        }

        private static string Quote(string path)
        {
            return "\"" + path + "\"";
        }

        private static string Tail(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text ?? string.Empty;
            }
            return text.Substring(text.Length - maxChars);
        }
    }
}
