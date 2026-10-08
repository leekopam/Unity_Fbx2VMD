using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 보컬 분리용 프로젝트 로컬 python 가상환경을 자동 준비한다.
    /// Tools/LipSync/.venv(깃 추적 제외)에 엔진 패키지를 requirements 핀 고정으로 설치하고,
    /// 엔진별 설치 마커에 requirements 해시를 기록해 변경 시 자동 재설치한다.
    /// 모델 파일도 Tools/LipSync/.models 아래에 두어 Assets/Git 밖을 지킨다.
    /// </summary>
    public static class PythonEnvProvisioner
    {
        public const string ToolDir = "Tools/LipSync";

        public sealed class Result
        {
            public bool success;
            public string pythonExe = string.Empty;
            public string venvDir = string.Empty;
            public string error = string.Empty;
            public string logTail = string.Empty;
            /// <summary>true면 설치 없이 기존 환경을 재사용했다.</summary>
            public bool reused;
        }

        /// <summary>엔진별 requirements 파일명(프로젝트 루트 기준 상대 경로).</summary>
        public static string RequirementsFileName(VocalStemSeparator.Engine engine)
        {
            return engine == VocalStemSeparator.Engine.Demucs
                ? "requirements-demucs.txt"
                : "requirements-audio-separator.txt";
        }

        /// <summary>엔진 설치 완료 마커 파일명. 내부에 requirements 해시를 기록한다.</summary>
        public static string MarkerFileName(VocalStemSeparator.Engine engine)
        {
            return ".installed-"
                + (engine == VocalStemSeparator.Engine.Demucs ? "demucs" : "audio-separator");
        }

        public static string VenvDir(string projectRoot)
        {
            return Path.Combine(projectRoot, "Tools", "LipSync", ".venv");
        }

        /// <summary>분리 모델/토치 허브 캐시. Git·Assets 밖에 둔다.</summary>
        public static string ModelsDir(string projectRoot)
        {
            return Path.Combine(projectRoot, "Tools", "LipSync", ".models");
        }

        /// <summary>venv 안의 python 실행 파일 경로(Windows 기준).</summary>
        public static string VenvPythonPath(string venvDir)
        {
            return Path.Combine(venvDir, "Scripts", "python.exe");
        }

        /// <summary>requirements 파일 내용 해시 — 마커와 비교해 재설치 여부를 판단한다.</summary>
        public static string RequirementsHash(string requirementsText)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(requirementsText ?? string.Empty));
                var sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>분리/설치 프로세스에 줄 환경변수 — 모델 캐시를 프로젝트 로컬에 고정한다.</summary>
        public static Dictionary<string, string> ProcessEnv(string projectRoot)
        {
            string torch = Path.Combine(ModelsDir(projectRoot), "torch");
            return new Dictionary<string, string>
            {
                { "TORCH_HOME", torch },
                // demucs가 HF 허브로 폴백할 때도 캐시가 프로젝트 로컬에 머물도록 한다.
                { "HF_HOME", Path.Combine(ModelsDir(projectRoot), "hf") },
                { "PYTHONUTF8", "1" },
                { "PIP_DISABLE_PIP_VERSION_CHECK", "1" },
            };
        }

        /// <summary>
        /// venv 생성에 쓸 베이스 python을 찾는다.
        /// env변수 → py 런처(-3) → PATH python 순으로 시도하고,
        /// py 런처를 쓸 때는 prefixArgs에 "-3"을 담는다.
        /// </summary>
        public static bool FindBasePython(out string program, out string prefixArgs)
        {
            prefixArgs = string.Empty;
            string env = Environment.GetEnvironmentVariable(VocalStemSeparator.PythonPathEnv);
            if (!string.IsNullOrEmpty(env) && File.Exists(env))
            {
                program = env;
                return true;
            }

            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var dirs = new List<string>();
            foreach (string dir in pathVar.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    dirs.Add(dir.Trim());
                }
            }

            foreach (string dir in dirs)
            {
                string py = Path.Combine(dir, "py.exe");
                if (IsUsableBasePython(py, "-3"))
                {
                    program = py;
                    prefixArgs = "-3";
                    return true;
                }
            }
            foreach (string dir in dirs)
            {
                foreach (string name in new[] { "python.exe", "python3.exe" })
                {
                    string candidate = Path.Combine(dir, name);
                    if (IsUsableBasePython(candidate, string.Empty))
                    {
                        program = candidate;
                        return true;
                    }
                }
            }
            program = null;
            return false;
        }

        /// <summary>후보가 실제 실행 가능한 Python>=3.10인지 확인한다.
        /// Microsoft Store 스텁(WindowsApps)은 존재하지만 실행이 안 되므로 제외.</summary>
        private static bool IsUsableBasePython(string program, string prefixArgs)
        {
            if (string.IsNullOrEmpty(program) || !File.Exists(program))
            {
                return false;
            }
            if (program.IndexOf("\\Microsoft\\WindowsApps\\", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = program,
                    Arguments = (string.IsNullOrEmpty(prefixArgs) ? "" : prefixArgs + " ") + "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(startInfo))
                {
                    if (p == null || !p.WaitForExit(10000) || p.ExitCode != 0)
                    {
                        return false;
                    }
                    string text = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    var m = Regex.Match(text, @"Python\s+(\d+)\.(\d+)");
                    if (!m.Success)
                    {
                        return false;
                    }
                    int major = int.Parse(m.Groups[1].Value);
                    int minor = int.Parse(m.Groups[2].Value);
                    return major > 3 || (major == 3 && minor >= 10);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>백그라운드 Task로 환경 준비를 돌린다. 호출부는 완료를 폴링한다.</summary>
        public static Task<Result> EnsureReadyAsync(string projectRoot,
            VocalStemSeparator.Engine engine, CancellationToken ct)
        {
            return Task.Run(() => EnsureReady(projectRoot, engine, ct), ct);
        }

        /// <summary>
        /// venv 생성 → pip install -r requirements → import 검증 → 마커 기록.
        /// 이미 설치됐고 requirements 해시가 같으면 검증만 하고 재사용한다.
        /// </summary>
        public static Result EnsureReady(string projectRoot,
            VocalStemSeparator.Engine engine, CancellationToken ct)
        {
            var result = new Result();
            string venv = VenvDir(projectRoot);
            string pyExe = VenvPythonPath(venv);
            string markerPath = Path.Combine(venv, MarkerFileName(engine));
            string reqPath = Path.Combine(projectRoot, ToolDir, RequirementsFileName(engine));
            result.venvDir = venv;

            if (!File.Exists(reqPath))
            {
                result.error = "requirements 파일이 없습니다: " + reqPath;
                return result;
            }
            string reqHash = RequirementsHash(File.ReadAllText(reqPath));
            var env = ProcessEnv(projectRoot);

            // 기존 설치 재사용 — 마커 해시가 같고 import가 되면 끝.
            if (File.Exists(pyExe) && File.Exists(markerPath)
                && File.ReadAllText(markerPath).Trim() == reqHash
                && ImportCheck(engine, pyExe, env, ct, out _))
            {
                result.success = true;
                result.reused = true;
                result.pythonExe = pyExe;
                TryEnableGpuTorch(pyExe, env, ct);
                return result;
            }

            ct.ThrowIfCancellationRequested();

            if (!File.Exists(pyExe))
            {
                if (!FindBasePython(out string program, out string prefixArgs))
                {
                    result.error = "베이스 python을 찾을 수 없습니다.\n"
                        + "수동 설치: winget install Python.Python.3.11\n"
                        + "또는 " + VocalStemSeparator.PythonPathEnv + " 환경변수로 경로 지정";
                    return result;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(venv));
                string venvArgs = (prefixArgs.Length > 0 ? prefixArgs + " " : "")
                    + "-m venv \"" + venv + "\"";
                int code = VocalStemSeparator.RunSync(program, venvArgs, null,
                    out string output, 180, ct, env,
                    orphanPurpose: "provision", orphanKey: venv);
                result.logTail = Tail(output);
                if (code != 0)
                {
                    ct.ThrowIfCancellationRequested();
                    result.error = $"venv 생성 실패(exit {code}):\n{result.logTail}";
                    return result;
                }
                // 구형 pip는 최신 wheel 태그를 못 읽을 수 있어 먼저 올린다(실패해도 계속).
                VocalStemSeparator.RunSync(pyExe,
                    "-m pip install --upgrade pip", null, out _, 300, ct, env,
                    orphanPurpose: "provision", orphanKey: venv);
            }

            ct.ThrowIfCancellationRequested();

            int installCode = VocalStemSeparator.RunSync(pyExe,
                "-m pip install -r \"" + reqPath + "\"", null,
                out string installLog, 3600, ct, env,
                orphanPurpose: "provision", orphanKey: venv);
            result.logTail = Tail(installLog);
            if (installCode != 0)
            {
                ct.ThrowIfCancellationRequested();
                result.error = $"패키지 설치 실패(exit {installCode}):\n{result.logTail}";
                return result;
            }

            if (!ImportCheck(engine, pyExe, env, ct, out string importOutput))
            {
                ct.ThrowIfCancellationRequested(); // 체크 중 취소는 실패가 아니라 취소로 보고
                result.error = "설치 후 import 검증 실패:\n" + Tail(importOutput);
                return result;
            }

            File.WriteAllText(markerPath, reqHash + "\n");
            result.success = true;
            result.pythonExe = pyExe;
            TryEnableGpuTorch(pyExe, env, ct);
            return result;
        }

        // cu130 채널에서 torch/torchvision/torchaudio가 같은 세대로 존재하는 최신 조합.
        // (cu126/cu128은 torchaudio가 2.11에서 멈춰 있고 torch 2.14용 조합이 없다)
        private const string GpuTorchIndexUrl = "https://download.pytorch.org/whl/cu130";
        private const string GpuTorchVersion = "2.11.0+cu130";
        private const string GpuTorchVisionVersion = "0.26.0+cu130";
        private const string GpuTorchAudioVersion = "2.11.0+cu130";

        /// <summary>GPU 판정 마커 — 확정 결과("cuda"/"nogpu")를 기록해 매 실행 재검사를 피한다.
        /// GPU를 나중에 장착하면 이 파일을 지우면 된다. 설치 실패 시엔 쓰지 않아 다음 실행에 재시도한다.</summary>
        private static string GpuMarkerPath(string pyExe)
        {
            return Path.Combine(Path.GetDirectoryName(pyExe), ".gpu-torch");
        }

        /// <summary>NVIDIA GPU가 있고 torch가 CPU 빌드면 CUDA 빌드로 승격한다.
        /// 실패해도 CPU 경로로 계속 쓸 수 있으므로 프로비저닝을 실패시키지 않는다.</summary>
        internal static void TryEnableGpuTorch(string pyExe,
            IReadOnlyDictionary<string, string> env, CancellationToken ct)
        {
            string marker = GpuMarkerPath(pyExe);
            if (File.Exists(marker))
            {
                return; // 이전 실행에서 판정 확정 — nvidia-smi/torch import 재검사 비용 절약
            }
            if (!HasNvidiaGpu(env, ct))
            {
                File.WriteAllText(marker, "nogpu\n");
                return;
            }
            if (IsTorchCudaAvailable(pyExe, env, ct))
            {
                File.WriteAllText(marker, "cuda\n");
                return;
            }
            VocalStemSeparator.RunSync(pyExe,
                "-m pip install"
                + " torch==" + GpuTorchVersion
                + " torchvision==" + GpuTorchVisionVersion
                + " torchaudio==" + GpuTorchAudioVersion
                + " --index-url " + GpuTorchIndexUrl,
                null, out string installLog, 3600, ct, env,
                orphanPurpose: "provision", orphanKey: pyExe);
            if (IsTorchCudaAvailable(pyExe, env, ct))
            {
                File.WriteAllText(marker, "cuda\n");
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[LipSync] CUDA torch 설치는 됐지만 GPU 인식이 안 됩니다. CPU로 진행합니다.\n"
                    + Tail(installLog));
            }
        }

        /// <summary>nvidia-smi가 응답하면 CUDA 가능 GPU가 있는 것으로 본다.</summary>
        private static bool HasNvidiaGpu(IReadOnlyDictionary<string, string> env,
            CancellationToken ct)
        {
            return VocalStemSeparator.RunSync("nvidia-smi", "-L", null,
                out _, 15, ct, env) == 0;
        }

        /// <summary>venv torch가 CUDA를 인식하는지 확인한다.</summary>
        private static bool IsTorchCudaAvailable(string pyExe,
            IReadOnlyDictionary<string, string> env, CancellationToken ct)
        {
            return VocalStemSeparator.RunSync(pyExe,
                "-c \"import torch,sys;sys.exit(0 if torch.cuda.is_available() else 1)\"",
                null, out _, 120, ct, env) == 0;
        }

        /// <summary>venv python에서 엔진 모듈 import가 되는지 확인한다.</summary>
        private static bool ImportCheck(VocalStemSeparator.Engine engine, string pyExe,
            IReadOnlyDictionary<string, string> env, CancellationToken ct, out string output)
        {
            string module = engine == VocalStemSeparator.Engine.Demucs
                ? "demucs"
                : "audio_separator";
            int code = VocalStemSeparator.RunSync(pyExe,
                "-c \"import " + module + "\"", null, out output, 120, ct, env);
            return code == 0;
        }

        private static string Tail(string text)
        {
            const int maxChars = 4000;
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text ?? string.Empty;
            }
            return text.Substring(text.Length - maxChars);
        }
    }
}
