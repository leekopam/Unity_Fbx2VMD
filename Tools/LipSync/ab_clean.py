# 정제 체인 A/B — server.py의 _job_clean과 동일한 순차 체인을 후보별로 돌린다.
# 각 체인은 (model 또는 ensemble_preset, stemKeyword) 패스 목록이다.
import argparse
import json
import time
from pathlib import Path

MODEL_DIR = str(Path(__file__).resolve().parent / ".models")

# 패스: (label, model, ensemble_preset, stem_keyword, use_original_mix)
PASS_KARAOKE = ("karaoke", None, "karaoke", "Vocals", True)
PASS_VOCALCLEAN = ("vocal_clean", None, "vocal_clean", "Vocals", True)
PASS_BLEED = ("bleed", "mel_band_roformer_bleed_suppressor_v1.ckpt",
              None, "Instrumental", False)
PASS_DEVERB = ("deverb", "deverb_bs_roformer_8_384dim_10depth.ckpt",
               None, "noreverb", False)
PASS_DENOISE = ("denoise", "denoise_mel_band_roformer_aufr33_sdr_27.9959.ckpt",
                None, "dry", False)

# 후보 체인 — 기본값은 현행(카라오케→디리버브→디노이즈).
CHAINS = {
    "current": [PASS_KARAOKE, PASS_DEVERB, PASS_DENOISE],
    "bleed_before_deverb": [PASS_KARAOKE, PASS_BLEED, PASS_DEVERB, PASS_DENOISE],
    "vocalclean_lead": [PASS_VOCALCLEAN, PASS_DEVERB, PASS_DENOISE],
    "no_deverb": [PASS_KARAOKE, PASS_DENOISE],
    "no_denoise": [PASS_KARAOKE, PASS_DEVERB],
    "karaoke_only": [PASS_KARAOKE],
}


def _find_stem(out_dir: Path, keyword: str, after: float):
    """_job_clean의 resolve_stem_output와 동일 규칙 — `_(키워드)_` 스템 파일."""
    import re
    stem_re = re.compile(r"_\(([^()]*)\)_[^()]*\.wav$", re.IGNORECASE)
    best, best_t = None, -1.0
    for f in out_dir.glob("*.wav"):
        m = stem_re.search(f.name)
        if not m or m.group(1).lower() != keyword.lower():
            continue
        t = f.stat().st_mtime
        if t > after and t > best_t:
            best, best_t = f, t
    return best


def run_pass(sep_by_key, label, model, preset, keyword, src, out_dir, started):
    from audio_separator.separator import Separator

    key = model or preset
    sep = sep_by_key.get(key)
    if sep is None:
        kwargs = {"model_file_dir": MODEL_DIR, "output_dir": str(out_dir),
                  "output_format": "wav", "mdx_params": {"overlap": 0.5}}
        if preset:
            kwargs["ensemble_preset"] = preset
        sep = Separator(**kwargs)
        sep_by_key[key] = sep
    else:
        sep.output_dir = str(out_dir)
    if model:
        sep.load_model(model_filename=model)
    else:
        sep.load_model()
    sep.separate(str(src))
    # 로드된 Separator는 model_dir 아래에도 파일을 만들 수 있어 out_dir만 본다.
    stem = _find_stem(out_dir, keyword, started)
    if stem is None:
        raise RuntimeError(f"{label} 패스가 '{keyword}' 스템을 출력하지 못함")
    return stem


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input")
    ap.add_argument("--out", required=True)
    ap.add_argument("--chains", default=",".join(CHAINS))
    ap.add_argument("--original", default=None,
                    help="use_original_mix 패스의 입력(기본: input)")
    args = ap.parse_args()
    original = args.original or args.input
    results = []
    for name in args.chains.split(","):
        cdir = Path(args.out) / name
        cdir.mkdir(parents=True, exist_ok=True)
        print(f"[chain] {name} 시작", flush=True)
        t0 = time.time()
        src = Path(args.input)
        seps = {}
        try:
            for i, (label, model, preset, keyword, use_orig) in enumerate(
                    CHAINS[name]):
                pin = Path(original) if use_orig else src
                started = time.time()
                stem = run_pass(seps, label, model, preset, keyword,
                                pin, cdir, started)
                fixed = cdir / f"pass{i + 1}.wav"
                stem.replace(fixed)
                src = fixed
                print(f"[chain] {name} {label} 완료", flush=True)
            final = cdir / f"{Path(args.input).stem}_vocal_clean.wav"
            src.replace(final)
            r = {"chain": name, "vocal": str(final),
                 "seconds": round(time.time() - t0, 1)}
        except Exception as e:
            r = {"chain": name, "error": str(e)}
        results.append(r)
        print(f"[chain] {name}: {json.dumps(r, ensure_ascii=False)}", flush=True)
    report = Path(args.out) / f"{Path(args.input).stem}_chain_report.json"
    report.write_text(json.dumps(results, ensure_ascii=False, indent=1),
                      encoding="utf-8")


if __name__ == "__main__":
    main()
