# 립싱크 로컬 모델 서버 — 보컬/BGM 분리·정제·음소 추출·가사 수집을
# Unity 에디터 프로세스 밖에서 실행한다. 클라이언트(Unity 창·미래 Electron)는
# HTTP로 작업을 제출하고 폴링한다.
#
# 실행: <venv>/Scripts/python.exe Tools/LipSync/server.py [--port 17845]
# 서버는 자신을 실행한 venv의 python을 워커에 그대로 사용한다(sys.executable).
#
# 작업 규칙:
# - GPU 계열 작업은 큐로 직렬화한다(동시 1개).
# - 동일 kind+key의 running 작업이 있으면 409로 기존 job_id를 돌려준다 —
#   Unity 측 TryFindLive 의미론을 서버로 옮긴 것이다.
# - 완료 작업은 출력 폴더에 result.json을 남겨 서버 재시작 후에도
#   파일 기준 채택이 가능하게 한다(Unity TryAdopt*와 같은 계약).
import argparse
import asyncio
import contextlib
import datetime
import io
import json
import os
import re
import sys
import traceback
import uuid
from pathlib import Path

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

PROJECT_ROOT = Path(__file__).resolve().parents[2]
TOOLS_DIR = Path(__file__).resolve().parent

# 모델 캐시를 프로젝트 안쪽으로 고정 — C# PythonEnvProvisioner.ProcessEnv와
# 동일한 값이다. 서버는 클라이언트 없이도 단독 기동되므로 여기서 기본값을 둔다
# (warm 경로는 서버 프로세스의 os.environ을, subprocess는 상속받는다).
MODELS_DIR = TOOLS_DIR / ".models"
os.environ.setdefault("TORCH_HOME", str(MODELS_DIR / "torch"))
os.environ.setdefault("HF_HOME", str(MODELS_DIR / "hf"))
os.environ.setdefault("PYTHONUTF8", "1")
os.environ.setdefault("PIP_DISABLE_PIP_VERSION_CHECK", "1")

app = FastAPI(title="Fbx2Vmd LipSync Server", version="1.0")
# 로컬 전용 도구 — localhost 바인딩 + localhost 오리진만 허용한다.
app.add_middleware(
    CORSMiddleware,
    allow_origin_regex=r"^https?://(localhost|127\.0\.0\.1)(:\d+)?$",
    allow_methods=["*"],
    allow_headers=["*"],
)

PROGRESS_RE = re.compile(r"(\d{1,3})%")
WPE_SCRIPT = TOOLS_DIR / "wpe_dereverb.py"
PHONEME_SCRIPT = TOOLS_DIR / "phoneme_extract.py"
LYRICS_SCRIPT = TOOLS_DIR / "lyrics_fetch.py"

# C# VocalStemSeparator.DefaultCleanupSteps와 동일한 기본 정제 체인.
# 양쪽이 따로 존재하므로 한쪽을 바꾸면 다른 쪽도 맞춰야 한다.
DEFAULT_CLEAN_STEPS = [
    {"model": None, "stemKeyword": "Vocals", "label": "코러스 분리(원곡)",
     "ensemblePreset": "karaoke", "useOriginalMix": True, "script": None},
    {"model": "deverb_bs_roformer_8_384dim_10depth.ckpt", "stemKeyword": "noreverb",
     "label": "잔향/에코 제거", "ensemblePreset": None, "useOriginalMix": False,
     "script": None},
    {"model": "denoise_mel_band_roformer_aufr33_sdr_27.9959.ckpt",
     "stemKeyword": "dry", "label": "노이즈 제거",
     "ensemblePreset": None, "useOriginalMix": False, "script": None},
]
WPE_CLEAN_STEP = {"model": None, "stemKeyword": None, "label": "잔향 제거(WPE)",
                  "ensemblePreset": None, "useOriginalMix": False,
                  "script": str(WPE_SCRIPT)}


def clean_steps(use_wpe: bool):
    steps = [dict(s) for s in DEFAULT_CLEAN_STEPS]
    if use_wpe:
        steps[1] = dict(WPE_CLEAN_STEP)
    return steps


def _quote(a: str) -> str:
    return '"' + a.replace('"', "") + '"'


def build_separate_args(engine: str, input_path: str, output_dir: str,
                        model: str = "", model_dir: str = "",
                        ensemble_preset: str = "", high_quality: bool = False):
    """C# VocalStemSeparator.BuildArguments와 동일한 인자열을 만든다."""
    if engine == "audio_separator":
        args = ("-c \"from audio_separator.utils.cli import main; main()\" "
                + _quote(input_path)
                + " --output_dir " + _quote(output_dir)
                + " --output_format wav")
        if ensemble_preset:
            args += " --ensemble_preset " + _quote(ensemble_preset)
        elif model:
            args += " --model_filename " + _quote(model)
        if model_dir:
            args += " --model_file_dir " + _quote(model_dir)
        if high_quality:
            args += " --mdx_overlap 0.5"
        return args
    if engine == "demucs":
        args = ("-m demucs --two-stems=vocals -n " + _quote(model or "htdemucs_ft")
                + " -o " + _quote(output_dir) + " " + _quote(input_path))
        if high_quality:
            args += " --shifts 2"
        return args
    raise ValueError(f"알 수 없는 엔진: {engine}")


def resolve_stem_output(output_dir: str, input_path: str,
                        stem_keyword: str, min_write_utc: float):
    """C# ResolveStemOutput과 동일 — 마지막 `_(스템)_` 그룹이 스템이다."""
    out = Path(output_dir)
    if not out.is_dir():
        return None
    input_base = Path(input_path).stem
    best, best_time = None, -1.0
    stem_re = re.compile(r"_\(([^()]*)\)_[^()]*\.wav$", re.IGNORECASE)
    for f in out.rglob("*.wav"):
        if not f.name.startswith(input_base + "_"):
            continue
        mtime = f.stat().st_mtime
        if mtime < min_write_utc:
            continue
        m = stem_re.search(f.name)
        stem = m.group(1) if m else ""
        if stem.lower() == (stem_keyword or "").lower() and mtime > best_time:
            best, best_time = str(f), mtime
    return best


def resolve_separate_outputs(engine: str, output_dir: str,
                             input_path: str, min_write_utc: float = 0.0):
    """C# ResolveOutputs와 동일한 명명 규칙으로 보컬/반주를 찾는다."""
    out = Path(output_dir)
    vocal = instrumental = None
    if not out.is_dir():
        return None, None
    base = Path(input_path).stem
    files = [f for f in out.rglob("*.wav") if f.stat().st_mtime >= min_write_utc]
    files.sort(key=lambda f: f.stat().st_mtime, reverse=True)
    for f in files:
        name = f.name
        if engine == "demucs":
            parent = f.parent.name
            match = parent.lower() == base.lower()
            if name.lower() == "vocals.wav" and match:
                vocal = vocal or str(f)
            elif name.lower() == "no_vocals.wav" and match:
                instrumental = instrumental or str(f)
        else:
            match = name.startswith(base + "_")
            if match and "(Vocals)" in name:
                vocal = vocal or str(f)
            elif match and "(Instrumental)" in name:
                instrumental = instrumental or str(f)
        if vocal and instrumental:
            break
    return vocal, instrumental


# ---------- warm 실행 계층 ----------
# 서버 프로세스가 상주하므로 torch/CUDA/모델을 메모리에 둔 채 작업을 실행할 수 있다.
# CLI 서브프로세스는 호출마다 기동+import+CUDA 초기화+모델 로드 비용을 반복하는데,
# warm 경로는 이를 서버 수명 동안 1회로 줄인다. 실패 시 subprocess 경로로 폴백한다.
# LIPSYNC_NO_WARM=1이면 전부 subprocess로 돌아간다(디버깅/비교용).
WARM = os.environ.get("LIPSYNC_NO_WARM", "") != "1"

# (model_dir, model, ensemble_preset, high_quality) → audio_separator.Separator
_audio_separators: dict = {}
# (model, shifts) → demucs.api.Separator
_demucs_separators: dict = {}


class _JobLogWriter(io.TextIOBase):
    """인프로세스 호출의 print/logging 출력을 job 로그로 중계한다."""

    def __init__(self, job: "Job"):
        self.job = job

    def writable(self):
        return True

    def write(self, s):
        for line in str(s).splitlines():
            if line.strip():
                _log(self.job, line)
        return len(s)


def _capture_to(job: "Job", fn):
    w = _JobLogWriter(job)
    with contextlib.redirect_stdout(w), contextlib.redirect_stderr(w):
        return fn()


def _audio_separator_run(job: "Job", input_path: str, out_dir: str,
                         model: str, model_dir: str,
                         ensemble_preset: str, high_quality: bool):
    """audio_separator를 서버 프로세스 안에서 실행한다.
    CLI의 --ensemble_preset/--mdx_overlap 인자와 동등한 옵션을 생성자로 넘긴다."""
    from audio_separator.separator import Separator
    key = (model_dir or "", model or "",
           ensemble_preset or "", bool(high_quality))
    sep = _audio_separators.get(key)
    if sep is None:
        kwargs = {"model_file_dir": model_dir or None,
                  "output_dir": out_dir, "output_format": "wav"}
        if ensemble_preset:
            kwargs["ensemble_preset"] = ensemble_preset
        if high_quality:
            kwargs["mdx_params"] = {"overlap": 0.5}
        sep = Separator(**kwargs)
        # separate()는 모델 미초기화 시 명시적 예외를 던진다 — ensemble 프리셋은
        # 무인자 load_model()이 프리셋 모델 목록을 채우고, 단일 모델은 파일명을 넘긴다.
        # CLI와 같은 우선순위: 프리셋이 있으면 모델 파일명은 무시한다.
        # 캐시 키에 모델/프리셋이 포함되므로 로드는 인스턴스 생성 시 한 번만 한다.
        if ensemble_preset:
            sep.load_model()
        elif model:
            sep.load_model(model_filename=model)
        else:
            sep.load_model()
        _audio_separators[key] = sep
    else:
        sep.output_dir = out_dir
    _log(job, f"[warm] audio_separator 실행: {model or ensemble_preset}")
    return sep.separate(input_path)


def _demucs_run(job: "Job", input_path: str, out_dir: str,
                model: str, high_quality: bool):
    """demucs를 서버 프로세스 안에서 실행한다.
    CLI `--two-stems=vocals`와 동등하게 vocals + no_vocals(=원곡-vocals)를 쓴다.
    출력은 <out_dir>/<곡명>/vocals.wav — C# ResolveOutputs의 탐색 규칙과 일치."""
    import torch  # noqa: F401
    from demucs.api import Separator as DemucsSeparator
    from demucs.audio import save_audio
    name = model or "htdemucs_ft"
    shifts = 2 if high_quality else 0
    key = (name, shifts)
    sep = _demucs_separators.get(key)
    if sep is None:
        # progress=True로 tqdm 바를 켜 로그/진행률 중계가 \r 출력을 받게 한다.
        sep = DemucsSeparator(model=name, shifts=shifts, progress=True)
        _demucs_separators[key] = sep
        _log(job, f"[warm] demucs 모델 로드: {name}")
    else:
        _log(job, f"[warm] demucs 모델 재사용: {name}")
    origin, stems = sep.separate_audio_file(Path(input_path))
    vocals = stems["vocals"]
    no_vocals = origin - vocals
    dest = Path(out_dir) / Path(input_path).stem
    dest.mkdir(parents=True, exist_ok=True)
    save_audio(vocals, str(dest / "vocals.wav"), sep.samplerate)
    save_audio(no_vocals, str(dest / "no_vocals.wav"), sep.samplerate)


async def _try_warm(job: "Job", fn):
    """인프로세스 실행 — 예외 시 False를 돌려 subprocess 폴백을 유도한다."""
    if not WARM:
        return False
    try:
        await asyncio.to_thread(_capture_to, job, fn)
        return True
    except Exception as e:
        _log(job, f"[warm] 인프로세스 실행 실패, subprocess 폴백: {e}")
        return False


# ---------- 작업 테이블 ----------

# 경로 인자로 받는 params 키 — 제출 시 프로젝트 루트 안쪽으로 제한한다.
# 임의 경로 접근(경로 탈출, 외부 파일 읽기)을 막기 위한 경계 검증.
_PATH_PARAMS = ("input", "output_dir", "output", "audio", "out",
                "json", "lyrics", "original_mix")


def _check_paths(params: dict):
    """params 안의 경로 값이 프로젝트 루트 밖이면 400."""
    for name in _PATH_PARAMS:
        value = params.get(name)
        if not value:
            continue
        if not isinstance(value, str):
            raise HTTPException(400, f"경로는 문자열이어야 합니다: {name}")
        try:
            resolved = Path(value).resolve()
        except (OSError, ValueError):
            raise HTTPException(400, f"잘못된 경로: {name}")
        if PROJECT_ROOT not in resolved.parents and resolved != PROJECT_ROOT:
            raise HTTPException(
                400, f"경로가 프로젝트 밖입니다: {name}={value}")


# kind별 필수 파라미터 — 러너 안의 KeyError 대신 제출 시점에 400으로 거부한다.
_REQUIRED_PARAMS = {
    "separate": ("engine", "input", "output_dir"),
    "clean": ("input", "output_dir"),
    "phoneme": ("input", "output"),
    "lyrics": ("audio", "out"),
}


class JobRequest(BaseModel):
    kind: str  # separate | clean | phoneme | lyrics
    key: str   # 중복 방지 키 — 보통 입력 파일의 절대 경로
    params: dict = {}


class Job:
    def __init__(self, kind, key, params):
        self.id = uuid.uuid4().hex[:12]
        self.kind = kind
        self.key = key
        self.params = params
        self.status = "queued"   # queued|running|done|failed|cancelled
        self.progress = 0.0
        # 정제처럼 내부에 여러 패스가 있는 작업은 로그의 %를
        # base..base+span 구간으로 눌러 전체 진행률로 환산한다.
        self.progress_base = 0.0
        self.progress_span = 1.0
        self.stage = ""
        self.error = ""
        self.outputs = {}
        self.log_lines = []
        self.proc: asyncio.subprocess.Process | None = None
        self.started_utc = 0.0
        self.output_dir = params.get("output_dir", "")

    def to_dict(self):
        return {"job_id": self.id, "kind": self.kind, "key": self.key,
                "status": self.status, "progress": round(self.progress, 3),
                "stage": self.stage, "error": self.error,
                "outputs": self.outputs}


JOBS: dict[str, Job] = {}
_QUEUE: asyncio.Queue[Job] = asyncio.Queue()


def _log(job: Job, line: str):
    job.log_lines.append(line)
    if len(job.log_lines) > 500:
        del job.log_lines[:250]
    # tqdm 출력은 한 줄에 %가 여러 번 나타날 수 있어 마지막 값을 쓴다.
    matches = PROGRESS_RE.findall(line)
    if matches:
        job.progress = min(1.0, job.progress_base
                           + job.progress_span * int(matches[-1]) / 100.0)


def _merged_env(extra: dict | None):
    """클라이언트가 보낸 환경변수를 os.environ 위에 얹는다(미지정 시 상속)."""
    if not isinstance(extra, dict) or not extra:
        return None
    env = dict(os.environ)
    env.update({str(k): str(v) for k, v in extra.items()})
    return env


async def _run_proc(job: Job, args: str, work_dir: str, env: dict,
                    timeout: float = 3600.0):
    """서브프로세스 실행 — stdout 라인을 읽어 진행률/로그로 중계한다.
    timeout 초과 시 자식 트리를 종료하고 예외를 던진다(로컬 RunSync의
    타임아웃과 같은 안전장치 — 멈춘 워커가 큐를 영구 점유하는 것을 막는다)."""
    job.proc = await asyncio.create_subprocess_shell(
        f'"{sys.executable}" {args}',
        cwd=work_dir,
        env=_merged_env(env),
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.STDOUT,
    )
    assert job.proc.stdout is not None

    async def _pump():
        async for raw in job.proc.stdout:
            # tqdm은 \r로 같은 줄을 덮어쓰므로 \r 기준으로도 나눈다.
            for seg in raw.decode("utf-8", "replace").split("\r"):
                line = seg.rstrip("\n")
                if line.strip():
                    _log(job, line)
        return await job.proc.wait()

    try:
        return await asyncio.wait_for(_pump(), timeout)
    except asyncio.TimeoutError:
        _log(job, f"작업 시간 초과({int(timeout)}초) — 프로세스를 종료합니다.")
        await _kill_proc(job)
        raise RuntimeError(f"작업 시간 초과({int(timeout)}초)")


def _write_result(job: Job):
    """완료 계약 — 출력 폴더의 result.json으로 재부착·채택을 지원한다."""
    if not job.output_dir:
        return
    try:
        Path(job.output_dir).mkdir(parents=True, exist_ok=True)
        path = Path(job.output_dir) / f"{job.kind}_result.json"
        path.write_text(json.dumps(job.to_dict(), ensure_ascii=False, indent=2),
                        encoding="utf-8")
    except OSError:
        pass


async def _kill_proc(job: Job):
    """자식 트리 전체를 종료한다(Windows taskkill /T — Unity TryKill과 동등)."""
    proc = job.proc
    if proc is None or proc.returncode is not None:
        return
    try:
        kill = await asyncio.create_subprocess_exec(
            "taskkill", "/PID", str(proc.pid), "/T", "/F",
            stdout=asyncio.subprocess.DEVNULL,
            stderr=asyncio.subprocess.DEVNULL)
        await kill.wait()
    except Exception:
        try:
            proc.kill()
        except Exception:
            pass


async def _job_separate(job: Job):
    p = job.params
    Path(p["output_dir"]).mkdir(parents=True, exist_ok=True)
    started = datetime.datetime.now(datetime.timezone.utc).timestamp() - 2
    engine, model = p["engine"], p.get("model", "")
    warm_ok = await _try_warm(job, lambda: (
        _demucs_run(job, p["input"], p["output_dir"], model,
                    bool(p.get("high_quality")))
        if engine == "demucs"
        else _audio_separator_run(
            job, p["input"], p["output_dir"], model,
            p.get("model_dir", ""), p.get("ensemble_preset", ""),
            bool(p.get("high_quality")))))
    if not warm_ok:
        args = build_separate_args(
            engine, p["input"], p["output_dir"],
            model=model, model_dir=p.get("model_dir", ""),
            ensemble_preset=p.get("ensemble_preset", ""),
            high_quality=bool(p.get("high_quality")))
        code = await _run_proc(job, args, p["output_dir"], p.get("env"))
        if code != 0:
            raise RuntimeError(f"분리 실패(exit {code})")
    vocal, inst = resolve_separate_outputs(
        p["engine"], p["output_dir"], p["input"], started)
    if not vocal:
        raise RuntimeError("분리는 끝났지만 산출물을 찾지 못했습니다.")
    job.outputs = {"vocal": vocal, "instrumental": inst}


async def _job_clean(job: Job):
    """C# CleanVocal과 동일한 다중 패스 체인을 서버에서 수행한다."""
    p = job.params
    steps = p.get("steps") or clean_steps(bool(p.get("wpe")))
    out_dir = Path(p["output_dir"])
    out_dir.mkdir(parents=True, exist_ok=True)
    input_path = str(Path(p["input"]).resolve())
    original_mix = p.get("original_mix")
    total = len(steps)
    for i, step in enumerate(steps):
        if job.status == "cancelled":
            return
        job.stage = step.get("label") or ""
        # 이 패스의 서브로그 %는 전체 구간의 i/total..(i+1)/total로 환산한다.
        job.progress_base = i / total
        job.progress_span = 1.0 / total
        run_start = datetime.datetime.now(
            datetime.timezone.utc).timestamp() - 2
        step_input = original_mix if (step.get("useOriginalMix")
                                      and original_mix) else input_path
        if step.get("script"):
            script_out = str(out_dir / f"clean_pass{i + 1}_script.wav")
            args = (_quote(step["script"])
                    + " --input " + _quote(step_input)
                    + " --output " + _quote(script_out))
            code = await _run_proc(job, args, str(out_dir), p.get("env"))
            if code != 0:
                raise RuntimeError(f"정제 실패({job.stage}, exit {code})")
        else:
            script_out = None
            # steps 키는 내부 기본값(camelCase)과 외부 snake_case 페이로드를 둘 다 받는다.
            step_preset = (step.get("ensemble_preset")
                           or step.get("ensemblePreset") or "")
            warm_ok = await _try_warm(job, lambda: _audio_separator_run(
                job, step_input, str(out_dir), step.get("model") or "",
                p.get("model_dir", ""), step_preset,
                bool(p.get("high_quality"))))
            if not warm_ok:
                args = build_separate_args(
                    "audio_separator", step_input, str(out_dir),
                    model=step.get("model") or "",
                    model_dir=p.get("model_dir", ""),
                    ensemble_preset=step_preset,
                    high_quality=bool(p.get("high_quality")))
                code = await _run_proc(job, args, str(out_dir), p.get("env"))
                if code != 0:
                    raise RuntimeError(f"정제 실패({job.stage}, exit {code})")
        job.progress = (i + 1) / total
        nxt = (script_out if script_out and Path(script_out).exists()
               else resolve_stem_output(str(out_dir), step_input,
                                        step.get("stemKeyword"), run_start))
        if not nxt:
            raise RuntimeError(
                f"정제 패스({job.stage})가 스템 '{step.get('stemKeyword')}'을 출력하지 못했습니다.")
        # 체인 출력명이 패스마다 누적돼 MAX_PATH를 넘을 수 있어 고정명으로 옮긴다.
        fixed = str(out_dir / f"clean_pass{i + 1}.wav")
        os.replace(nxt, fixed)
        input_path = fixed
    base = Path(p["input"]).stem
    if len(base) > 60:
        base = base[:60]
    final = str(out_dir / f"{base}_vocal_clean.wav")
    os.replace(input_path, final)
    job.outputs = {"vocal": final}


async def _job_phoneme(job: Job):
    p = job.params
    Path(p["output"]).parent.mkdir(parents=True, exist_ok=True)
    args = (_quote(str(PHONEME_SCRIPT)) + " " + _quote(p["input"])
            + " --output " + _quote(p["output"]))
    if p.get("model"):
        args += " --model " + _quote(p["model"])
    if p.get("device"):
        args += " --device " + p["device"]
    if p.get("backend"):
        args += " --backend " + p["backend"]
    if p.get("lyrics"):
        args += " --lyrics " + _quote(p["lyrics"])
    if p.get("no_vad"):
        args += " --no-vad"
    if p.get("loudness"):
        args += " --loudness " + p["loudness"]
    code = await _run_proc(job, args, str(Path(p["output"]).parent), p.get("env"))
    if code != 0 or not Path(p["output"]).exists():
        raise RuntimeError(f"음소 추출 실패(exit {code})")
    job.outputs = {"csv": p["output"]}


async def _job_lyrics(job: Job):
    p = job.params
    Path(p["out"]).parent.mkdir(parents=True, exist_ok=True)
    args = (_quote(str(LYRICS_SCRIPT)) + " --audio " + _quote(p["audio"])
            + " --out " + _quote(p["out"]))
    if p.get("title"):
        args += " --title " + _quote(p["title"])
    if p.get("artist"):
        args += " --artist " + _quote(p["artist"])
    if p.get("lang"):
        args += " --lang " + p["lang"]
    if p.get("json"):
        args += " --json " + _quote(p["json"])
    if p.get("offline"):
        args += " --offline"
    code = await _run_proc(job, args, str(Path(p["out"]).parent), p.get("env"))
    if code != 0:
        raise RuntimeError(f"가사 수집 실패(exit {code})")
    job.outputs = {"json": p.get("json"),
                   "txt": p["out"] + ".txt", "lrc": p["out"] + ".lrc"}


KIND_RUNNERS = {
    "separate": _job_separate,
    "clean": _job_clean,
    "phoneme": _job_phoneme,
    "lyrics": _job_lyrics,
}


async def _worker_loop():
    """GPU 작업 직렬 실행 루프 — 큐에서 하나씩 꺼내 처리한다."""
    while True:
        job = await _QUEUE.get()
        if job.status == "cancelled":
            continue
        job.status = "running"
        job.started_utc = datetime.datetime.now(datetime.timezone.utc).timestamp()
        try:
            await KIND_RUNNERS[job.kind](job)
            if job.status != "cancelled":
                job.status = "done"
                job.progress = 1.0
        except asyncio.CancelledError:
            raise
        except Exception as e:
            if job.status != "cancelled":
                job.status = "failed"
                job.error = str(e)
                _log(job, traceback.format_exc())
        finally:
            job.proc = None
            _write_result(job)


@contextlib.asynccontextmanager
async def _lifespan(_app: FastAPI):
    # on_event는 deprecated — 워커 루프는 lifespan에서 시작한다.
    asyncio.create_task(_worker_loop())
    yield


app.router.lifespan_context = _lifespan


# ---------- API ----------

@app.get("/health", summary="서버 생존·python 경로·프로젝트 루트 확인")
def health():
    return {"ok": True, "version": "1.0", "python": sys.executable,
            "project": str(PROJECT_ROOT)}


@app.post("/jobs", status_code=201,
          summary="작업 제출 — 동일 kind+key가 실행 중이면 409로 기존 job_id 반환")
async def submit(req: JobRequest):
    if req.kind not in KIND_RUNNERS:
        raise HTTPException(400, f"알 수 없는 kind: {req.kind}")
    _check_paths(req.params)
    for name in _REQUIRED_PARAMS[req.kind]:
        if not req.params.get(name):
            raise HTTPException(400, f"필수 파라미터 누락: {name}")
    # 동일 작업 running이면 신규 실행 대신 기존을 돌려준다.
    for j in JOBS.values():
        if (j.kind == req.kind and j.key == req.key
                and j.status in ("queued", "running")):
            raise HTTPException(409, detail={"job_id": j.id,
                                             "message": "동일 작업이 이미 실행 중입니다."})
    job = Job(req.kind, req.key, req.params)
    JOBS[job.id] = job
    await _QUEUE.put(job)
    return {"job_id": job.id}


@app.get("/jobs/{job_id}",
         summary="작업 상태 폴링 — status/progress/stage/error/outputs 반환")
def get_job(job_id: str):
    job = JOBS.get(job_id)
    if not job:
        raise HTTPException(404, "job not found")
    return job.to_dict()


@app.get("/jobs/{job_id}/log", summary="작업 로그 tail 조회")
def get_log(job_id: str, tail: int = 100):
    job = JOBS.get(job_id)
    if not job:
        raise HTTPException(404, "job not found")
    return {"lines": job.log_lines[-tail:]}


@app.post("/jobs/{job_id}/cancel",
          summary="작업 취소 — 자식 프로세스 트리를 taskkill /T로 종료")
async def cancel(job_id: str):
    job = JOBS.get(job_id)
    if not job:
        raise HTTPException(404, "job not found")
    if job.status in ("queued", "running"):
        # 완료/실패한 작업을 소급 취소하지 않는다 — 결과 상태를 보존한다.
        job.status = "cancelled"
        await _kill_proc(job)
        _write_result(job)
    return {"ok": True}


@app.get("/result/{kind}",
         summary="같은 키의 마지막 완료 작업 — 서버 재시작 후 재부착용")
def last_result(kind: str, key: str = ""):
    done = [j for j in JOBS.values()
            if j.kind == kind and j.status == "done"
            and (not key or j.key == key)]
    if not done:
        raise HTTPException(404, "no completed job")
    done.sort(key=lambda j: j.started_utc, reverse=True)
    return done[0].to_dict()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", type=int,
                    default=int(os.environ.get("LIPSYNC_SERVER_PORT", "17845")))
    args = ap.parse_args()
    import uvicorn
    uvicorn.run(app, host="127.0.0.1", port=args.port, log_level="warning")


if __name__ == "__main__":
    main()
