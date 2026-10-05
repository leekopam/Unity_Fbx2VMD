using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        /// </summary>
        public static string BuildArguments(Engine engine, string inputPath, string outputDir, string model)
        {
            switch (engine)
            {
                case Engine.AudioSeparator:
                    // -m audio_separator 형태로 호출한다(스크립트 경로 불필요).
                    return "-m audio_separator " + Quote(inputPath)
                        + " --output_dir " + Quote(outputDir)
                        + " --output_format wav"
                        + (string.IsNullOrEmpty(model)
                            ? string.Empty
                            : " --model_filename " + Quote(model));
                case Engine.Demucs:
                    return "-m demucs --two-stems=vocals"
                        + " -n " + (string.IsNullOrEmpty(model) ? DefaultDemucsModel : model)
                        + " -o " + Quote(outputDir)
                        + " " + Quote(inputPath);
                default:
                    throw new ArgumentOutOfRangeException(nameof(engine));
            }
        }

        /// <summary>출력 디렉터리에서 보컬/반주 파일을 찾는다. 순수 함수라 테스트 가능.</summary>
        public static void ResolveOutputs(Engine engine, string outputDir, string inputPath,
            out string vocalPath, out string instrumentalPath)
        {
            vocalPath = null;
            instrumentalPath = null;
            if (!Directory.Exists(outputDir))
            {
                return;
            }

            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            var files = new List<string>(Directory.GetFiles(outputDir, "*.wav", SearchOption.AllDirectories));

            foreach (string f in files)
            {
                string name = Path.GetFileName(f);
                if (engine == Engine.Demucs)
                {
                    // demucs: <out>/<model>/<곡명>/vocals.wav, no_vocals.wav
                    if (name.Equals("vocals.wav", StringComparison.OrdinalIgnoreCase)
                        && f.Contains(baseName))
                    {
                        vocalPath = f;
                    }
                    else if (name.Equals("no_vocals.wav", StringComparison.OrdinalIgnoreCase)
                        && f.Contains(baseName))
                    {
                        instrumentalPath = f;
                    }
                }
                else
                {
                    // audio-separator: <곡명>_(Vocals)_<모델>.wav / _(Instrumental)_
                    if (name.IndexOf("(Vocals)", StringComparison.OrdinalIgnoreCase) >= 0
                        && name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                    {
                        vocalPath = f;
                    }
                    else if (name.IndexOf("(Instrumental)", StringComparison.OrdinalIgnoreCase) >= 0
                        && name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                    {
                        instrumentalPath = f;
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
                "-c \"import " + module + "\"", null, out string output, 30);
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
            string inputPath, string outputDir, string model)
        {
            return Task.Run(() => Separate(engine, pythonPath, inputPath, outputDir, model));
        }

        public static Result Separate(Engine engine, string pythonPath,
            string inputPath, string outputDir, string model)
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
            Directory.CreateDirectory(outputDir);

            string args = BuildArguments(engine, inputPath, outputDir, model);
            int code = RunSync(pythonPath, args, outputDir, out string output, timeoutSec: 1800);
            result.logTail = Tail(output, 4000);
            if (code != 0)
            {
                result.error = $"분리 실패(exit {code}):\n{result.logTail}";
                return result;
            }

            ResolveOutputs(engine, outputDir, inputPath,
                out result.vocalPath, out result.instrumentalPath);
            if (result.vocalPath == null)
            {
                result.error = "분리는 성공했으나 보컬 출력을 찾지 못했습니다:\n" + result.logTail;
                return result;
            }
            result.success = true;
            return result;
        }

        private static int RunSync(string program, string arguments, string workDir,
            out string output, int timeoutSec)
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
            var sb = new StringBuilder();
            try
            {
                using (var process = Process.Start(startInfo))
                {
                    // stdout/stderr를 동시에 비동기로 읽어 버퍼 데드락을 막는다.
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(timeoutSec * 1000))
                    {
                        process.Kill();
                        output = sb + "\n[timeout]";
                        return -1;
                    }
                    output = sb.ToString();
                    return process.ExitCode;
                }
            }
            catch (Exception error)
            {
                output = error.ToString();
                return -2;
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
