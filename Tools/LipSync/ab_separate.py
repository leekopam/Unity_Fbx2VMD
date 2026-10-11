# 분리 품질 A/B 러너 — 서버 warm 경로(_audio_separator_run)와 동일한
# Separator kwargs로 후보별 보컬 스템을 만든다. 평가는 phoneme_stats.py가 한다.
import argparse
import json
import time
from pathlib import Path

# 프로젝트 모델 캐시 — 서버와 같은 디렉터리를 써야 다운로드가 공유된다.
MODEL_DIR = str(Path(__file__).resolve().parent / ".models")

# 후보별 Separator 생성자 인자. None 값은 생략해 기본값을 따른다.
VARIANTS = {
    "current": {"model_filename": "model_bs_roformer_ep_317_sdr_12.9755.ckpt"},
    "vocal_balanced": {"ensemble_preset": "vocal_balanced"},
    "vocal_clean": {"ensemble_preset": "vocal_clean"},
}


def run_variant(name: str, input_path: str, out_dir: Path):
    """단일 후보 분리 — 서버 warm 경로와 kwargs를 맞춘다(overlap 0.5 = high_quality)."""
    from audio_separator.separator import Separator

    spec = VARIANTS[name]
    kwargs = {"model_file_dir": MODEL_DIR, "output_dir": str(out_dir),
              "output_format": "wav", "mdx_params": {"overlap": 0.5}}
    if "ensemble_preset" in spec:
        kwargs["ensemble_preset"] = spec["ensemble_preset"]
    out_dir.mkdir(parents=True, exist_ok=True)
    t0 = time.time()
    sep = Separator(**kwargs)
    if "model_filename" in spec:
        sep.load_model(model_filename=spec["model_filename"])
    else:
        sep.load_model()
    outputs = sep.separate(input_path)
    elapsed = time.time() - t0
    # 반환값은 출력 경로 목록 — 보컬 스템만 골라 기록한다.
    vocal = next((o for o in outputs if "(Vocals)" in Path(o).name), None)
    return {"variant": name, "vocal": vocal, "seconds": round(elapsed, 1),
            "outputs": outputs}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input")
    ap.add_argument("--out", required=True)
    ap.add_argument("--variants", default=",".join(VARIANTS))
    args = ap.parse_args()
    base = Path(args.input).stem
    results = []
    for name in args.variants.split(","):
        vdir = Path(args.out) / name
        print(f"[ab] {name} 시작", flush=True)
        try:
            r = run_variant(name, args.input, vdir)
        except Exception as e:
            r = {"variant": name, "error": str(e)}
        r["input"] = base
        results.append(r)
        print(f"[ab] {name} 완료: {json.dumps(r, ensure_ascii=False)}", flush=True)
    report = Path(args.out) / f"{base}_separate_report.json"
    report.write_text(json.dumps(results, ensure_ascii=False, indent=1),
                      encoding="utf-8")
    print(f"[ab] 리포트: {report}")


if __name__ == "__main__":
    main()
