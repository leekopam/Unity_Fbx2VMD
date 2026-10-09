#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""UnderPressure 오프라인 접지 채점기.

라이선스 경계: UnderPressure는 Limited Software Evaluation License — 본 스크립트는
자동 라벨의 품질을 "비교 측정"하는 오프라인 평가 전용. 생성 결과를 human-labels.csv
등 제품 라벨 소스에 기록하지 않는다.

입력: --positions joint-positions.jsonl (프레임당 {"frame":N,"joints":{"Hips":[x,y,z],...}},
      미터 단위 월드 좌표). 본 이름은 UnderPressure TOPOLOGY 또는 Unity HumanBodyBones.
      --labels human-labels.csv (from_frame,to_frame,side,contact_label,...)
출력: JSON — 좌/우 발 프레임 단위 precision/recall/F1
"""
import argparse, csv, json, sys
from pathlib import Path

REPO_DIR = Path(__file__).resolve().parent / "external" / "UnderPressure"
sys.path.insert(0, str(REPO_DIR))

# Unity HumanBodyBones -> UnderPressure TOPOLOGY 이름 매핑
UNITY_TO_UP = {
    "Hips": "pelvis", "Spine": "spine_1", "Chest": "spine_3", "UpperChest": "spine_4",
    "Neck": "neck", "Head": "head",
    "LeftShoulder": "left_clavicle", "LeftUpperArm": "left_shoulder",
    "LeftLowerArm": "left_elbow", "LeftHand": "left_wrist",
    "RightShoulder": "right_clavicle", "RightUpperArm": "right_shoulder",
    "RightLowerArm": "right_elbow", "RightHand": "right_wrist",
    "LeftUpperLeg": "left_hip", "LeftLowerLeg": "left_knee",
    "LeftFoot": "left_ankle", "LeftToes": "left_foot",
    "RightUpperLeg": "right_hip", "RightLowerLeg": "right_knee",
    "RightFoot": "right_ankle", "RightToes": "right_foot",
}
# 라벨 어휘는 한국어 원형(validate-human-labels.mjs 기준). 영어 별칭도 허용.
CONTACT_POSITIVE = {"앞꿈치", "뒤꿈치", "발 전체", "toe", "heel", "full_foot"}
CONTACT_NEGATIVE = {"공중", "airborne"}  # 불확실/공백은 평가 제외


def load_positions(path):
    import torch
    frames, names = [], None
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        rec = json.loads(line)
        if names is None:
            names = list(rec["joints"].keys())
        frames.append([rec["joints"][n] for n in names])
    return torch.tensor(frames, dtype=torch.float32), names  # T x J x 3


def predict_contacts(positions, names, fps, device="cpu"):
    import torch, anim, models, util
    from data import TOPOLOGY, FRAMERATE
    # demo.py의 visualization(panda3d)은 추론에 불필요 — 미설치 환경에서 스텁으로 대체함.
    import types as _types
    _viz = _types.ModuleType("visualization")
    _viz.MocapAndvGRFsApp = object
    sys.modules.setdefault("visualization", _viz)
    from demo import retarget_to_underpressure

    # dataset 미배포(README만)로 Skeletons.all() 불가 — 데모 제공 샘플의 skeleton 사용
    skeleton = torch.load(REPO_DIR / "footskate_samples" / "0.pt",
                          map_location="cpu", weights_only=False)["skeleton"].to(device)

    model = models.DeepNetwork(
        state_dict=torch.load(REPO_DIR / "pretrained.tar", map_location="cpu")["model"]
    ).to(device).eval()

    up_names = names if all(n in TOPOLOGY for n in names) else [
        UNITY_TO_UP.get(n, n) for n in names]
    missing = [n for n, u in zip(names, up_names) if u not in TOPOLOGY]
    if missing:
        raise ValueError("매핑 불가 본: %s" % missing)

    angles, trajectory = retarget_to_underpressure(
        positions.to(device), up_names, niters=150, skeleton=skeleton)

    out_n = round(trajectory.shape[-3] / fps * FRAMERATE)
    angles = util.resample(angles, out_n, dim=-3, interpolation_fn=util.SU2.slerp)
    trajectory = util.resample(trajectory, out_n)
    fk = anim.FK(angles, skeleton, trajectory, TOPOLOGY)
    # contacts: T x FB x LR — 발 관련 본 중 어느 하나라도 접지면 접지
    contacts = model.contacts(fk.unsqueeze(0)).squeeze(0).detach()
    contacts = util.resample(contacts.float(), positions.shape[0])
    probs = contacts.amax(dim=-2)  # T x 2 — 발 본 중 최대 접촉 확률
    return probs, (probs >= 0.5)  # T x 2 (left,right)


def load_labels(path):
    rows = []
    with open(path, newline="", encoding="utf-8-sig") as f:
        for r in csv.DictReader(f):
            label = (r.get("contact_label") or "").strip()
            if not r.get("from_frame") or label not in CONTACT_POSITIVE | CONTACT_NEGATIVE:
                continue
            rows.append({"f0": int(r["from_frame"]), "f1": int(r["to_frame"]),
                         "side": r["side"].strip().lower(),
                         "positive": label in CONTACT_POSITIVE})
    return rows


def frame_truth(rows, nframes):
    truth = {"left": [None] * nframes, "right": [None] * nframes}
    for r in rows:
        for f in range(max(0, r["f0"]), min(nframes, r["f1"] + 1)):
            truth[r["side"]][f] = r["positive"]
    return truth


def prf(pred, truth):
    tp = sum(1 for p, t in zip(pred, truth) if p and t)
    fp = sum(1 for p, t in zip(pred, truth) if p and t is False)
    fn = sum(1 for p, t in zip(pred, truth) if not p and t)
    prec = tp / (tp + fp) if tp + fp else 0.0
    rec = tp / (tp + fn) if tp + fn else 0.0
    f1 = 2 * prec * rec / (prec + rec) if prec + rec else 0.0
    return {"tp": tp, "fp": fp, "fn": fn, "precision": round(prec, 4),
            "recall": round(rec, 4), "f1": round(f1, 4)}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--positions", required=True, help="joint-positions.jsonl")
    ap.add_argument("--labels", required=True, help="human-labels.csv")
    ap.add_argument("--fps", type=float, default=60.0)
    ap.add_argument("--out", default=None)
    ap.add_argument("--contacts-out", default=None,
                  help="프레임별 접지 결과 JSONL — 합의 정답지 입력용(평가 전용)")
    args = ap.parse_args()

    report = {"status": "OK", "inputs": vars(args)}
    if not Path(args.positions).exists():
        report.update(status="BLOCKED",
                      reason="joint-positions.jsonl 없음 - Unity 전신 관절 위치 덤프 선행 필요")
    else:
        positions, names = load_positions(args.positions)
        probs, contacts = predict_contacts(positions, names, args.fps)  # T x 2
        truth = frame_truth(load_labels(args.labels), contacts.shape[0])
        report["left"] = prf(contacts[:, 0].tolist(), truth["left"])
        report["right"] = prf(contacts[:, 1].tolist(), truth["right"])
        if args.contacts_out:
            Path(args.contacts_out).parent.mkdir(parents=True, exist_ok=True)
            with open(args.contacts_out, "w", encoding="utf-8") as f:
                for i, p in enumerate(probs.tolist()):
                    f.write(json.dumps({"frame": i, "left": p[0],
                                        "right": p[1]}) + "\n")

    text = json.dumps(report, ensure_ascii=False, indent=2)
    print(text)
    if args.out:
        Path(args.out).parent.mkdir(parents=True, exist_ok=True)
        Path(args.out).write_text(text, encoding="utf-8")


if __name__ == "__main__":
    main()
