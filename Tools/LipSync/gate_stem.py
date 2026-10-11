# 보컬 활동 구간 스템 게이트 — 분리된 보컬 wav에서 노래가 없는 구간을 무음으로 만든다.
# 간주·인트로에 남은 잔류 악기/코러스 블리드가 립싱크나 후속 체인을 흔들지 못하게 한다.
# 사용: python gate_stem.py <vocal.wav> --output <gated.wav>
#       [--pad-ms 120] [--fade-ms 30] [--merge-ms 300] [--no-vad]
# VAD(silero)를 못 쓰면 RMS 에너지 임계로 폴백한다.
import argparse
import sys

import numpy as np


def _speech_regions_silero(wav16, sr=16000):
    """silero-vad 음성 구간 [(시작샘플, 끝샘플)]. 실패 시 None."""
    try:
        import torch
        from silero_vad import load_silero_vad, get_speech_timestamps
        vad = load_silero_vad()
        spans = get_speech_timestamps(torch.from_numpy(wav16), vad,
                                      sampling_rate=sr)
        return [(s["start"], s["end"]) for s in spans]
    except Exception as exc:
        print("VAD 건너뜀: %s — RMS 게이트로 폴백" % exc, flush=True)
        return None


def _energy_regions(wav, sr, frame_sec=0.02, thresh_db=-40.0):
    """RMS 에너지가 최대 대비 thresh_db 이상인 프레임을 구간으로 묶어 돌려준다."""
    n = max(1, int(sr * frame_sec))
    rms = np.array([np.sqrt(np.mean(wav[i:i + n] ** 2))
                    for i in range(0, len(wav), n)])
    peak = rms.max()
    if peak <= 0:
        return []
    active = rms > peak * (10.0 ** (thresh_db / 20.0))
    regions, start = [], None
    for i, on in enumerate(active):
        if on and start is None:
            start = i * n
        elif not on and start is not None:
            regions.append((start, i * n))
            start = None
    if start is not None:
        regions.append((start, len(wav)))
    return regions


def _merge(regions, gap):
    """gap 샘플 미만으로 가까운 구간은 호흡/띄어쓰기로 보고 합친다."""
    merged = []
    for lo, hi in regions:
        if merged and lo - merged[-1][1] < gap:
            merged[-1] = (merged[-1][0], hi)
        else:
            merged.append((lo, hi))
    return merged


def _gain_mask(n_samples, regions, sr, pad, fade):
    """구간 안 1.0, 밖 0.0의 게인 곡선 — 경계는 코사인 페이드로 클릭을 막는다."""
    gain = np.zeros(n_samples, dtype=np.float32)
    ramp = 0.5 - 0.5 * np.cos(np.linspace(0.0, np.pi, fade))
    for lo, hi in regions:
        a = max(0, lo - pad)
        b = min(n_samples, hi + pad)
        gain[a:b] = 1.0
        fi = min(fade, b - a)
        if fi > 0:
            gain[a:a + fi] *= ramp[:fi]
            gain[b - fi:b] *= ramp[::-1][:fi]
    return gain


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input")
    ap.add_argument("--output", required=True)
    ap.add_argument("--pad-ms", type=float, default=120.0,
                    help="구간 앞뒤로 살리는 여유")
    ap.add_argument("--fade-ms", type=float, default=30.0,
                    help="게이트 경계 페이드 길이")
    ap.add_argument("--merge-ms", type=float, default=300.0,
                    help="이 간격 미만의 인접 구간을 하나로 합침")
    ap.add_argument("--no-vad", action="store_true",
                    help="silero-vad 없이 RMS 에너지 게이트만 사용")
    args = ap.parse_args()

    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass

    import librosa
    import soundfile as sf

    data, sr = sf.read(args.input, always_2d=True)
    mono = data.mean(axis=1).astype(np.float32)
    wav16 = librosa.resample(mono, orig_sr=sr, target_sr=16000) \
        if sr != 16000 else mono

    regions = None
    if not args.no_vad:
        spans = _speech_regions_silero(wav16)
        if spans is not None:
            regions = [(int(s * sr / 16000), int(e * sr / 16000))
                       for s, e in spans]
    if regions is None:
        regions = _energy_regions(mono, sr)

    gap = int(sr * args.merge_ms / 1000.0)
    regions = _merge(regions, gap)
    gain = _gain_mask(len(mono), regions, sr,
                      pad=int(sr * args.pad_ms / 1000.0),
                      fade=int(sr * args.fade_ms / 1000.0))
    gated = data * gain[:, None]
    from pathlib import Path
    Path(args.output).parent.mkdir(parents=True, exist_ok=True)
    sf.write(args.output, gated, sr)

    kept = float((gain > 0).mean())
    print("구간 %d개, 유지율 %.1f%% → %s" % (len(regions), kept * 100.0,
                                          args.output), flush=True)


if __name__ == "__main__":
    main()
