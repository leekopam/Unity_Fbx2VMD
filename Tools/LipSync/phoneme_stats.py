# 음소 CSV 통계 — 분리 후보별 입모양 품질의 대리 지표.
# 해석: active 구간에서 max_prob가 높고 transitions_per_sec이 낮을수록
# 인식이 안정적(깜빡임 적음) = 분리 품질이 좋다고 읽는다.
import csv
import json
import sys
from pathlib import Path

import numpy as np


def stats(csv_path: str) -> dict:
    with open(csv_path, encoding="utf-8") as f:
        meta = json.loads(f.readline())
        rows = np.array([[float(x) for x in r] for r in csv.reader(f) if r])
    fps = float(meta.get("fps", 50.0))
    vols, groups = rows[:, 0], rows[:, 1:]
    active = vols > 0.0
    if not active.any():
        return {"file": Path(csv_path).name, "error": "활성 프레임 없음"}
    g = groups[active]
    arg = g.argmax(axis=1)
    # 지배 그룹이 바뀌는 횟수 — 깜빡임(플리커)의 상한으로 읽는다.
    transitions = int((arg[1:] != arg[:-1]).sum())
    active_sec = active.sum() / fps
    return {
        "file": Path(csv_path).name,
        "frames": int(len(rows)),
        "seconds": round(len(rows) / fps, 2),
        "active_ratio": round(float(active.mean()), 3),
        "vsum_mean": round(float(groups[active].sum(axis=1).mean()), 3),
        "max_prob_mean": round(float(g.max(axis=1).mean()), 3),
        "transitions_per_sec": round(transitions / max(active_sec, 1e-6), 2),
    }


def main():
    out = [stats(p) for p in sys.argv[1:]]
    print(json.dumps(out, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
