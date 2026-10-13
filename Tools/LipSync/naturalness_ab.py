# 립싱크 자연스러움 A/B — 음소 CSV로 베이크 파이프라인을 재현해
# 레거시(절대 게이트 + 하강 감쇠)와 신규(클립 자동정규화 + 채널 스무딩)의
# 수치를 비교한다. VocalLipSyncBaker.cs와 동일 수식 — C# 변경 시 여기도 맞출 것.
#
# 용도: 단계3(60ms 미만 전환 비율 측정) + 단계4(실곡 A/B 지표).
#   python naturalness_ab.py <phonemes.csv> [더.csv ...]
import csv
import json
import sys
from pathlib import Path

import numpy as np

# uLipSync BakedData.GetLipSyncInfo의 고정 로그 구간 — C#과 동일 상수.
LOG_MIN, LOG_MAX = -2.5, -1.5


def load(csv_path: str):
    with open(csv_path, encoding="utf-8") as f:
        meta = json.loads(f.readline())
        rows = np.array([[float(x) for x in r] for r in csv.reader(f) if r])
    return float(meta.get("fps", 50.0)), rows[:, 0], rows[:, 1:]


def norm_vols(raw: np.ndarray) -> np.ndarray:
    """원시 음량 → GetLipSyncInfo 로그 정규화(0~1)와 동일."""
    out = np.zeros_like(raw)
    pos = raw > 0
    out[pos] = np.clip((np.log10(raw[pos]) - LOG_MIN) / (LOG_MAX - LOG_MIN), 0.0, 1.0)
    return out


def normalize_clip(vols: np.ndarray, gamma: float) -> np.ndarray:
    """클립 p95 기준 정규화 + 지수 부양 — NormalizeVolumes와 동일."""
    pos = vols[vols > 0]
    if pos.size == 0:
        return vols.copy()
    ref = np.percentile(pos, 95)
    if ref <= 0:
        return vols.copy()
    n = np.clip(vols / ref, 0.0, 1.0)
    return n if abs(gamma - 1.0) < 1e-4 else np.power(n, max(gamma, 0.05))


def gate(vols: np.ndarray, g: float) -> np.ndarray:
    """슈미트 트리거 + 소프트 니 — BuildGatedVolumes와 동일."""
    out = np.zeros_like(vols)
    close, knee_end = g * 0.5, min(g * 2, 1.0)
    rng = max(knee_end - close, 1e-6)
    open_ = False
    for i, v in enumerate(vols):
        if open_:
            if v < close:
                open_ = False
        elif v > g:
            open_ = True
        if not open_:
            continue
        out[i] = v if v >= knee_end else v * max(0.0, v - close) / rng
    return out


def smooth_channel(vols: np.ndarray, open_ms: float, close_ms: float,
                   fps: float) -> np.ndarray:
    """비대칭 원폴 — SmoothChannel과 동일."""
    a_o = 1.0 - np.exp(-1.0 / (fps * open_ms / 1000.0)) if open_ms > 0 else 1.0
    a_c = 1.0 - np.exp(-1.0 / (fps * close_ms / 1000.0)) if close_ms > 0 else 1.0
    out = np.empty_like(vols)
    cur = 0.0
    for i, v in enumerate(vols):
        cur += (v - cur) * (a_o if v > cur else a_c)
        out[i] = cur
    return out


def one_pole(series: np.ndarray, ms: float, fps: float) -> np.ndarray:
    a = 1.0 - np.exp(-1.0 / (fps * ms / 1000.0)) if ms > 0 else 1.0
    out = np.empty_like(series)
    cur = np.zeros(series.shape[1] if series.ndim > 1 else ())
    for i in range(len(series)):
        cur += (series[i] - cur) * a
        out[i] = cur
    return out


def run_stats(labels: np.ndarray, fps: float, skip: int = -1) -> dict:
    """지배 음소 연속 구간 길이 분포 — 단계3 최소 간격 판단 근거.
    skip 라벨(무발음)은 구간 자체를 세되 결과에서 제외해,
    무음을 사이에 둔 같은 모음이 하나로 합쳐지지 않게 한다."""
    if labels.size == 0:
        return {"runs": 0}
    edges = np.r_[True, labels[1:] != labels[:-1], True]
    bounds = np.flatnonzero(edges)
    runs = np.diff(bounds)
    labels_of_runs = labels[bounds[:-1]]
    runs = runs[labels_of_runs != skip]
    if runs.size == 0:
        return {"runs": 0}
    ms = runs * 1000.0 / fps
    return {
        "runs": int(len(runs)),
        "run_ms_p10": round(float(np.percentile(ms, 10)), 1),
        "run_ms_p50": round(float(np.percentile(ms, 50)), 1),
        "run_ms_p90": round(float(np.percentile(ms, 90)), 1),
        "under_60ms": int((ms < 60).sum()),
        "under_60ms_ratio": round(float((ms < 60).mean()), 3),
    }


def mouth_metrics(values: np.ndarray, fps: float) -> dict:
    """개구량(모음 값 합, 0~100) 계열 지표."""
    active = values > 0.5
    d = np.abs(np.diff(values))
    return {
        "open_frame_ratio": round(float(active.mean()), 3),
        "mean_open_when_active": round(float(values[active].mean()), 2)
        if active.any() else 0.0,
        "max_frame_jump": round(float(d.max()), 2),
        "mean_frame_delta": round(float(d.mean()), 3),
    }


def apply_hold(groups: np.ndarray, silence_vols: np.ndarray) -> np.ndarray:
    """발성 중 무판정 구간 모음 유지 — VocalLipSyncBaker vowelFrames 경로와 동일.
    어느 음소도 비율이 없는 프레임에서 게이트 후 음량>0이면 직전 판정 프레임의
    유성 형태(ratio>0이던 모음 전체)를 유지하고, 무음(음량 0)이면 해제한다.
    silence_vols는 개구 스무딩 '전' 게이트 출력이어야 한다 — 원폴 꼬리는 0에
    도달하지 않으므로 스무딩된 채널로는 무음을 검출할 수 없다(C#과 동일)."""
    out = groups.copy()
    has_vowel = groups.max(axis=1) > 1e-4
    held: dict = {}
    for i in range(len(out)):
        if has_vowel[i]:
            held = {k: float(out[i, k]) for k in range(out.shape[1])
                    if out[i, k] > 1e-4}
        elif silence_vols[i] <= 0.0:
            held = {}
        elif held:
            for k, m in held.items():
                out[i, k] = m
    return out


def analyze(csv_path: str, gate_: float = 0.02, gamma: float = 0.7,
            open_ms: float = 50, close_ms: float = 130, trans_ms: float = 80,
            damp: float = 0.35, hold: bool = True) -> dict:
    fps, raw, groups = load(csv_path)
    nv = norm_vols(raw)
    w = 100.0  # 바인딩 가중치 — 비교 지표이므로 100 고정

    # 레거시: 절대 게이트 → 음소×음량 곱에 하강만 지수 감쇠(어택 즉시).
    leg_vol = gate(nv, gate_)
    leg = np.zeros((len(raw), groups.shape[1]))
    for k in range(groups.shape[1]):
        cur = 0.0
        for i in range(len(raw)):
            cur = max(groups[i, k] * leg_vol[i] * w, cur * damp)
            leg[i, k] = cur
    leg_open = leg.sum(axis=1)

    # 신규: 클립 자동정규화 → 같은 게이트 → 개구 비대칭 스무딩 → 모음 전이 스무딩.
    # 무음 판정은 스무딩 전 게이트 출력(new_gate)으로 — C# BuildCurve와 동일.
    new_gate = gate(normalize_clip(nv, gamma), gate_)
    new_vol = smooth_channel(new_gate, open_ms, close_ms, fps)
    held_groups = apply_hold(groups, new_gate) if hold else groups
    new = one_pole(held_groups, trans_ms, fps) * new_vol[:, None] * w
    new_open = new.sum(axis=1)

    # 저음량 발성 구간 — 정규화 음량 하위 50%(발성 중)에서의 개구율.
    voiced = nv > 0
    quiet = np.zeros(len(nv), bool)
    if voiced.any():
        med = np.median(nv[voiced])
        quiet = voiced & (nv <= med)
    quiet_stat = {
        "quiet_frames": int(quiet.sum()),
        "legacy_open_ratio": round(float((leg_open[quiet] > 0.5).mean()), 3)
        if quiet.any() else 0.0,
        "new_open_ratio": round(float((new_open[quiet] > 0.5).mean()), 3)
        if quiet.any() else 0.0,
        "legacy_mean_open": round(float(leg_open[quiet].mean()), 2)
        if quiet.any() else 0.0,
        "new_mean_open": round(float(new_open[quiet].mean()), 2)
        if quiet.any() else 0.0,
    }

    # 지배 음소 전환 — 단계3 측정은 원본 라벨 기준(베이크 전).
    dom = groups.argmax(axis=1)
    dom[groups.max(axis=1) < 1e-4] = -1  # 무발음 프레임 제외 라벨
    # 유지 적용 후의 실효 지배 시퀀스 — 캐릭터가 실제로 표현할 비즘 구간.
    dom_held = held_groups.argmax(axis=1)
    dom_held[(held_groups.max(axis=1) < 1e-4) | (new_gate <= 0)] = -1

    return {
        "file": Path(csv_path).name,
        "fps": fps,
        "seconds": round(len(raw) / fps, 2),
        "quiet_section": quiet_stat,
        "legacy": mouth_metrics(leg_open, fps),
        "new": mouth_metrics(new_open, fps),
        "transitions_raw": run_stats(dom, fps),
        "transitions_held": run_stats(dom_held, fps),
    }


def main():
    out = [analyze(p) for p in sys.argv[1:]]
    print(json.dumps(out, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
