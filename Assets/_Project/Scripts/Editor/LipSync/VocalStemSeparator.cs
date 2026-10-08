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

        /// <summary>보컬 정제 1패스 — model을 input에 적용해 stemKeyword 스템을 다음 입력으로 넘긴다.
        /// ensemblePreset이 있으면 단일 모델 대신 앙상블 프리셋으로 돌린다.
        /// useOriginalMix이면 이 패스 입력은 체인 출력이 아니라 원곡이다.</summary>
        public sealed class CleanupStep
        {
            public readonly string model;
            /// <summary>출력 파일명 `_(스템)_`에 넣을 키워드(모델 yaml의 instruments 값).</summary>
            public readonly string stemKeyword;
            /// <summary>진행률 라벨(디리버브/리드 추출/디노이즈 등).</summary>
            public readonly string label;
            /// <summary>audio-separator ensemble preset 이름(karaoke 등). 지정 시 model 무시.</summary>
            public readonly string ensemblePreset;
            /// <summary>분리 보컬이 아니라 원곡을 이 패스 입력으로 쓸지 여부.</summary>
            public readonly bool useOriginalMix;
            /// <summary>audio-separator 대신 실행할 python 스크립트의 프로젝트 상대 경로
            /// (예: WPE 디리버브). 지정 시 model/ensemblePreset은 무시되고
            /// `script --input X --output Y` 규약으로 실행한다.</summary>
            public readonly string scriptRelPath;

            public CleanupStep(string model, string stemKeyword, string label,
                string ensemblePreset = null, bool useOriginalMix = false,
                string scriptRelPath = null)
            {
                this.model = model;
                this.stemKeyword = stemKeyword;
                this.label = label;
                this.ensemblePreset = ensemblePreset;
                this.useOriginalMix = useOriginalMix;
                this.scriptRelPath = scriptRelPath;
            }
        }

        /// <summary>
        /// 기본 정제 체인 — 리드 추출(원곡 직접) → 디리버브 → 디노이즈.
        /// 카라오케 모델은 풀 믹스 입력 기준으로 학습됐고, 분리 보컬을 거치면
        /// 1차 분리에서 잘린 리드는 복구 불가라(MVSEP 리드 SDR ~0.2↓) 원곡에 직접 건다.
        /// 스템명은 각 모델 config yaml의 instruments/target_instrument로 확인했다.
        /// </summary>
        public static readonly CleanupStep[] DefaultCleanupSteps =
        {
            // Vocals = 리드 보컬만. karaoke 프리셋 = 멜밴드 카라오케 3종(aufr33/viperx,
            // gabox_v2, becruily)의 avg_wave 앙상블 — 단일 모델 대비 코러스 잔류가 적다.
            new CleanupStep(null, "Vocals", "코러스 분리(원곡)",
                ensemblePreset: "karaoke", useOriginalMix: true),
            // noreverb = 잔향+에코 제거(anvuew BS-Roformer). 비중앙 화성도 함께 걸러져
            // 스테레오 코러스 잔류를 추가로 줄인다.
            new CleanupStep("deverb_bs_roformer_8_384dim_10depth.ckpt",
                "noreverb", "잔향/에코 제거"),
            // dry = 노이즈 제거(Aufr33 denoise, 단일 스템 모델)
            new CleanupStep("denoise_mel_band_roformer_aufr33_sdr_27.9959.ckpt",
                "dry", "노이즈 제거"),
        };

        /// <summary>WPE 디리버브 스크립트의 프로젝트 상대 경로.</summary>
        public const string WpeScriptRelPath = "Tools/LipSync/wpe_dereverb.py";

        /// <summary>
        /// 신경망 디리버브 대신 WPE(nara_wpe, 결정적 선형 예측)를 쓰는 정제 체인 변형.
        /// 리드가 잔향으로 과인식돼 신경망이 보컬을 깎는 곡에서 사용한다.
        /// </summary>
        public static CleanupStep[] CleanupStepsWithWpe()
        {
            var steps = (CleanupStep[])DefaultCleanupSteps.Clone();
            steps[1] = new CleanupStep(null, null, "잔향 제거(WPE)",
                scriptRelPath: WpeScriptRelPath);
            return steps;
        }

        public sealed class Result
        {
            public bool success;
            public string vocalPath = string.Empty;
            public string instrumentalPath = string.Empty;
            public string error = string.Empty;
            public string logTail = string.Empty;
            /// <summary>치명적이지 않은 주의 사항(살베지 실패, 결과 이상 등).</summary>
            public readonly List<string> warnings = new List<string>();
        }

        /// <summary>
        /// 엔진별 CLI 인자열을 만든다. program은 python 또는 audio-separator 실행 파일.
        /// modelDir을 주면 모델 캐시를 그 폴더에 고정한다(audio-separator 전용).
        /// highQuality이면 아키텍처가 지원하는 품질 옵션만 추가한다 — 우리 모델군은
        /// 전부 MDX 아키텍처(RoFormer/멜밴드)라 vr_*/mdxc_* 플래그는 건드리지 않는다.
        /// </summary>
        public static string BuildArguments(Engine engine, string inputPath, string outputDir,
            string model, string modelDir = null, string ensemblePreset = null,
            bool highQuality = false)
        {
            switch (engine)
            {
                case Engine.AudioSeparator:
                    // audio-separator는 __main__이 없어 -m으로 못 쓴다.
                    // console entry(audio_separator.utils.cli:main)를 -c로 호출한다.
                    // --ensemble_preset은 --model_filename 미지정(기본값)일 때만 발동하므로
                    // 프리셋 사용 시 모델 인자를 생략한다.
                    return "-c \"from audio_separator.utils.cli import main; main()\" "
                        + Quote(inputPath)
                        + " --output_dir " + Quote(outputDir)
                        + " --output_format wav"
                        + (string.IsNullOrEmpty(ensemblePreset)
                            ? string.Empty
                            : " --ensemble_preset " + Quote(ensemblePreset))
                        + (string.IsNullOrEmpty(model) || !string.IsNullOrEmpty(ensemblePreset)
                            ? string.Empty
                            : " --model_filename " + Quote(model))
                        + (string.IsNullOrEmpty(modelDir)
                            ? string.Empty
                            : " --model_file_dir " + Quote(modelDir))
                        // mdx_overlap(0.001~0.999)은 예측 윈도 중첩 비율 — 높을수록 정확하지만 느리다.
                        + (highQuality ? " --mdx_overlap 0.5" : string.Empty);
                case Engine.Demucs:
                    return "-m demucs --two-stems=vocals"
                        + " -n " + Quote(string.IsNullOrEmpty(model) ? DefaultDemucsModel : model)
                        + " -o " + Quote(outputDir)
                        + " " + Quote(inputPath)
                        // shifts는 랜덤 시프트 앙상블 횟수 — 1→2로 품질↑, 시간 2배.
                        + (highQuality ? " --shifts 2" : string.Empty);
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

        /// <summary>분리를 백그라운드에서 실행한다. 호출부는 Task 완료를 폴링한다.
        /// onProgress(0~1)는 출력 로그의 tqdm 진행률을 파싱해 워커 스레드에서 통지한다.</summary>
        public static Task<Result> SeparateAsync(Engine engine, string pythonPath,
            string inputPath, string outputDir, string model,
            CancellationToken ct = default,
            IReadOnlyDictionary<string, string> env = null,
            string modelDir = null, Action<float> onProgress = null,
            bool highQuality = false)
        {
            return Task.Run(() => Separate(
                engine, pythonPath, inputPath, outputDir, model, ct, env, modelDir,
                onProgress, highQuality), ct);
        }

        public static Result Separate(Engine engine, string pythonPath,
            string inputPath, string outputDir, string model,
            CancellationToken ct = default,
            IReadOnlyDictionary<string, string> env = null,
            string modelDir = null, Action<float> onProgress = null,
            bool highQuality = false)
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
            // 상대 경로가 WorkingDirectory와 이중으로 붙지 않도록 절대 경로로 정규화한다.
            // outputDir 정규화도 try 안에 둬서 잘못된 경로 문자열이 예외가 아닌 result.error가 되게 한다.
            inputPath = Path.GetFullPath(inputPath);
            try
            {
                outputDir = Path.GetFullPath(outputDir);
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception error)
            {
                result.error = $"출력 폴더를 만들 수 없습니다: {error.Message}";
                return result;
            }

            string args = BuildArguments(engine, inputPath, outputDir, model, modelDir,
                highQuality: highQuality);
            // FAT32/exFAT의 mtime 2초 단위 때문에 실행 시각보다 살짝 여유를 둔다.
            DateTime runStartUtc = DateTime.UtcNow.AddSeconds(-2);
            int code = RunSync(pythonPath, args, outputDir, out string output,
                timeoutSec: 3600, ct: ct, env: env, onProgress: onProgress);
            result.logTail = Tail(output, 4000);
            if (code != 0)
            {
                ct.ThrowIfCancellationRequested(); // 취소로 죽은 프로세스는 실패가 아니라 취소로 보고
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
        /// 정제 체인을 순서대로 적용한다(리드 추출 → 디리버브 → 디노이즈).
        /// vocalPath는 분리된 보컬 — 살베지 소스와 최종 파일명 기준으로 쓴다.
        /// originalMixPath는 useOriginalMix 패스의 입력으로 쓰는 원곡이다.
        /// salvageSourcePath를 주면 체인 완료 후 과감쇠로 지워진 구간을 해당 보컬로 채운다.
        /// onProgress는 전체 체인 기준 0~1(패스 i는 (i+p)/steps.Length로 환산).
        /// onStage가 있으면 각 패스 시작 시 라벨을 통지한다.
        /// </summary>
        public static Result CleanVocal(string pythonPath, string vocalPath,
            string outputDir, CleanupStep[] steps = null,
            CancellationToken ct = default,
            IReadOnlyDictionary<string, string> env = null,
            string modelDir = null,
            Action<float> onProgress = null,
            Action<string> onStage = null,
            string originalMixPath = null,
            string salvageSourcePath = null,
            bool highQuality = false,
            string projectRoot = null)
        {
            var result = new Result();
            if (string.IsNullOrEmpty(pythonPath) || !File.Exists(pythonPath))
            {
                result.error = "python 실행 파일을 찾을 수 없습니다.";
                return result;
            }
            if (!File.Exists(vocalPath))
            {
                result.error = $"보컬 파일이 없습니다: {vocalPath}";
                return result;
            }
            if (steps == null || steps.Length == 0)
            {
                steps = DefaultCleanupSteps;
            }
            try
            {
                outputDir = Path.GetFullPath(outputDir);
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception error)
            {
                result.error = $"출력 폴더를 만들 수 없습니다: {error.Message}";
                return result;
            }

            string input = Path.GetFullPath(vocalPath);
            var log = new StringBuilder();
            for (int i = 0; i < steps.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                CleanupStep step = steps[i];
                onStage?.Invoke(step.label);
                DateTime runStartUtc = DateTime.UtcNow.AddSeconds(-2);
                // useOriginalMix 패스는 체인 중간 결과가 아니라 원곡을 입력으로 쓴다.
                string stepInput = step.useOriginalMix
                    && !string.IsNullOrEmpty(originalMixPath)
                        ? Path.GetFullPath(originalMixPath)
                        : input;
                string args;
                string scriptOut = null;
                if (step.scriptRelPath != null)
                {
                    // 스크립트 패스(WPE 등): --input/--output 규약으로 실행하고
                    // 출력은 고정 경로라 스템명 해석을 건너뛴다.
                    string scriptPath = Path.Combine(projectRoot ?? ".", step.scriptRelPath);
                    if (!File.Exists(scriptPath))
                    {
                        result.error = $"정제 스크립트가 없습니다: {scriptPath}";
                        result.logTail = Tail(log.ToString(), 4000);
                        return result;
                    }
                    scriptOut = Path.Combine(outputDir, $"clean_pass{i + 1}_script.wav");
                    args = Quote(scriptPath)
                        + " --input " + Quote(stepInput)
                        + " --output " + Quote(scriptOut);
                }
                else
                {
                    args = BuildArguments(Engine.AudioSeparator, stepInput, outputDir,
                        step.model, modelDir, step.ensemblePreset, highQuality);
                }
                // 패스 진행률을 전체 체인 범위로 환산해 통지한다.
                int pass = i;
                int code = RunSync(pythonPath, args, outputDir, out string output,
                    timeoutSec: 3600, ct: ct, env: env,
                    onProgress: onProgress == null
                        ? null
                        : p => onProgress((pass + p) / steps.Length));
                log.AppendLine(output);
                if (code != 0)
                {
                    ct.ThrowIfCancellationRequested(); // 취소로 죽은 프로세스는 실패가 아니라 취소로 보고
                    result.error = $"보컬 정제 실패({step.label}, exit {code}):\n{Tail(output, 4000)}";
                    result.logTail = Tail(log.ToString(), 4000);
                    return result;
                }

                input = scriptOut != null && File.Exists(scriptOut)
                    ? scriptOut
                    : ResolveStemOutput(outputDir, stepInput, step.stemKeyword, runStartUtc);
                if (input == null)
                {
                    result.error = $"정제 패스({step.label})가 목표 스템 '{step.stemKeyword}'을 출력하지 못했습니다.";
                    result.logTail = Tail(log.ToString(), 4000);
                    return result;
                }
                // 체인 출력명에 직전 스템명이 계속 누적돼(…_(Vocals)_…_(dry)_…) MAX_PATH를
                // 넘을 수 있으므로 다음 패스 입력은 짧은 고정명으로 옮겨 둔다.
                // (audio-separator가 선행 '_'를 제거하므로 고정명에 언더스코어 접두는 쓰지 않는다)
                input = MoveTo(input, Path.Combine(outputDir, $"clean_pass{i + 1}.wav"));
            }
            onProgress?.Invoke(1f);
            // 최종 결과물만 원래 곡명 기준의 짧은 이름으로 남긴다.
            string baseName = Path.GetFileNameWithoutExtension(Path.GetFileName(vocalPath));
            if (baseName.Length > 60)
            {
                baseName = baseName.Substring(0, 60);
            }
            result.vocalPath = MoveTo(input,
                Path.Combine(outputDir, baseName + "_vocal_clean.wav"));

            // 과감쇠로 지워진 구간을 분리 보컬로 채운다 — 립싱크엔 잔류 코러스보다
            // 리드 소실이 더 치명적이라 침묵 구간만 보수적으로 회수한다.
            if (!string.IsNullOrEmpty(salvageSourcePath))
            {
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
            result.success = true;
            result.logTail = Tail(log.ToString(), 4000);
            return result;
        }

        public static Task<Result> CleanVocalAsync(string pythonPath, string vocalPath,
            string outputDir, CleanupStep[] steps = null,
            CancellationToken ct = default,
            IReadOnlyDictionary<string, string> env = null,
            string modelDir = null,
            Action<float> onProgress = null,
            Action<string> onStage = null,
            string originalMixPath = null,
            string salvageSourcePath = null,
            bool highQuality = false,
            string projectRoot = null)
        {
            return Task.Run(() => CleanVocal(pythonPath, vocalPath, outputDir, steps,
                ct, env, modelDir, onProgress, onStage,
                originalMixPath, salvageSourcePath, highQuality, projectRoot), ct);
        }

        /// <summary>
        /// 한 패스의 출력에서 목표 스템 wav를 고른다.
        /// audio-separator 출력명은 `&lt;입력&gt;_(스템)_&lt;모델&gt;.wav`이므로
        /// 마지막 `_(...)_` 그룹이 스템이다 — 입력명에 이미 스템명이 들어가 있어도
        /// 마지막 그룹만 비교하면 오매치가 없다(예: `_(No dry)_`는 `dry`와 다름).
        /// minWriteUtc 이후 기록된 파일만 대상으로 해 이전 실행 산출물을 배제한다.
        /// </summary>
        public static string ResolveStemOutput(string outputDir, string inputPath,
            string stemKeyword, DateTime minWriteUtc)
        {
            if (!Directory.Exists(outputDir))
            {
                return null;
            }
            string inputBase = Path.GetFileNameWithoutExtension(
                Path.GetFileName(inputPath));
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (string f in Directory.GetFiles(outputDir, "*.wav",
                SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(f);
                // 출력명은 `<입력명>_(스템)_<모델>` — 입력명 뒤에 반드시 '_'가 와야 오매치가 없다.
                if (!name.StartsWith(inputBase + "_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                int open = name.LastIndexOf("_(", StringComparison.Ordinal);
                int close = open < 0 ? -1 : name.IndexOf(")_", open + 2,
                    StringComparison.Ordinal);
                if (open < 0 || close < 0)
                {
                    continue;
                }
                string stem = name.Substring(open + 2, close - open - 2);
                if (!string.Equals(stem, stemKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                DateTime t = File.GetLastWriteTimeUtc(f);
                if (t >= minWriteUtc && t > bestTime)
                {
                    best = f;
                    bestTime = t;
                }
            }
            return best;
        }

        /// <summary>src를 dst로 이동한다. dst가 있으면 덮어쓰고, src==dst면 그대로 둔다.</summary>
        internal static string MoveTo(string src, string dst)
        {
            if (string.Equals(src, dst, StringComparison.OrdinalIgnoreCase))
            {
                return src;
            }
            if (File.Exists(dst))
            {
                File.Delete(dst);
            }
            File.Move(src, dst);
            return dst;
        }

        /// <summary>
        /// 동기 프로세스 실행 — 프로비저너와 공유한다. ct 취소 시 프로세스를 죽인다.
        /// env를 주면 기존 환경변수 위에 덮어쓴다(모델 캐시 경로 고정 등).
        /// onProgress가 있으면 출력 라인의 tqdm "NN%"를 파싱해 0~1로 통지한다.
        /// </summary>
        internal static int RunSync(string program, string arguments, string workDir,
            out string output, int timeoutSec, CancellationToken ct = default,
            IReadOnlyDictionary<string, string> env = null,
            Action<float> onProgress = null)
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
                        if (e.Data == null) return;
                        lock (sbLock) sb.AppendLine(e.Data);
                        ReportProgress(e.Data, onProgress);
                    };
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data == null) return;
                        lock (sbLock) sb.AppendLine(e.Data);
                        ReportProgress(e.Data, onProgress);
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

        private static readonly System.Text.RegularExpressions.Regex ProgressPattern =
            new System.Text.RegularExpressions.Regex(@"(\d{1,3})\s*%",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>tqdm 라인(` 45%|██|` — \r로 이어진 갱신이 한 덩어리로 올 수 있음)에서
        /// 마지막 퍼센트를 읽어 진행률로 통지한다. 퍼센트가 없으면 아무 것도 안 한다.</summary>
        internal static void ReportProgress(string line, Action<float> onProgress)
        {
            if (onProgress == null || string.IsNullOrEmpty(line))
            {
                return;
            }
            var matches = ProgressPattern.Matches(line);
            if (matches.Count == 0)
            {
                return;
            }
            if (int.TryParse(matches[matches.Count - 1].Groups[1].Value, out int pct))
            {
                onProgress(Math.Min(1f, Math.Max(0f, pct / 100f)));
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
