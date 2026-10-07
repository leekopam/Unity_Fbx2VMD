# wav2vec2-IPA 음소 추출 — 리드 보컬 wav를 프레임별 모음 확률(v,a,i,u,e,o) CSV로 변환한다.
# 코러스 잔류가 있어도 MFCC 기반보다 음소 인식이 흔들리지 않아 uLipSync 대안으로 쓴다.
# 사용: python phoneme_extract.py <input.wav> --output <out.csv> [--model HF_ID] [--device cuda|cpu]
# 참고: Wav2Vec2PhonemeCTCTokenizer는 phonemizer+espeak-ng를 요구하므로(Windows 설치 곤란)
#       feature extractor로 입력을 만들고 vocab.json만 직접 읽어 토큰 id를 해석한다.
import argparse
import csv
import json
import sys

import numpy as np
import torch

# IPA 기호 → 모음 그룹. 일본어 비원순 /u/(ɯ)를 U에 매핑한다.
# 매핑되지 않은 자음·기호는 어느 그룹에도 안 들어가 해당 프레임의 모음 확률이 낮아진다.
VOWEL_GROUPS = {
    "a": "a", "ä": "a", "ɐ": "a", "æ": "a", "ɑ": "a", "ɒ": "a",
    "e": "e", "ɛ": "e", "ə": "e", "ɜ": "e", "ɞ": "e", "œ": "e", "ø": "e", "ɘ": "e",
    "i": "i", "ɪ": "i", "ɨ": "i", "y": "i", "ʏ": "i",
    "u": "u", "ʊ": "u", "ɯ": "u", "ʉ": "u", "ɤ": "u", "ɵ": "u",
    "o": "o", "ɔ": "o", "ʌ": "o",
}

# 가나 → 모음 그룹(일본어 ASR 모델용). 가나는 자음+모음 결합 문자라
# 행(あ段〜お段) 기준으로 매핑한다. っ/ン 같은 무모음 토큰은 제외한다.
_KANA_ROWS = {
    "a": "あかさたなはまやらわがざだばぱぁゃゎ",
    "i": "いきしちにひみりぎじぢびぴぃゐ",
    "u": "うくすつぬふむゆるぐずづぶぷぅゅゔ",
    "e": "えけせてねへめれげぜでべぺぇゑ",
    "o": "おこそとのほもよろをごぞどぼぽぉょ",
}


def _kana_groups():
    """가나 어휘용 매핑을 만든다. 가타카나는 히라가나로 평행 이동한다."""
    groups = {}
    for vowel, row in _KANA_ROWS.items():
        for ch in row:
            groups[ch] = vowel
            kata = chr(ord(ch) + 0x60)
            if "ァ" <= kata <= "ヶ":
                groups[kata] = vowel
    return groups


def _groups_for(vocab):
    """어휘를 보고 IPA/가나 매핑 중 맞는 쪽을 고른다."""
    if any("ぁ" <= k <= "ヶ" for k in vocab):
        return _kana_groups()
    return VOWEL_GROUPS


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input")
    ap.add_argument("--output", required=True)
    ap.add_argument("--model",
                    default="jonatasgrosman/wav2vec2-large-xlsr-53-japanese")
    ap.add_argument("--device", default=None)
    args = ap.parse_args()

    import librosa
    from huggingface_hub import hf_hub_download
    from transformers import AutoFeatureExtractor, Wav2Vec2ForCTC

    device = args.device or ("cuda" if torch.cuda.is_available() else "cpu")
    extractor = AutoFeatureExtractor.from_pretrained(args.model)
    model = Wav2Vec2ForCTC.from_pretrained(args.model).to(device).eval()
    with open(hf_hub_download(args.model, "vocab.json"), encoding="utf-8") as f:
        vocab = json.load(f)
    id2tok = {v: k for k, v in vocab.items()}
    pad_id = vocab.get("<pad>", 0)
    groups_map = _groups_for(vocab)

    wav, _ = librosa.load(args.input, sr=16000, mono=True)
    inputs = extractor(wav, sampling_rate=16000, return_tensors="pt")
    with torch.no_grad():
        logits = model(inputs.input_values.to(device)).logits[0]
    probs = torch.softmax(logits, dim=-1)
    # CTC blank(<pad>)이 대부분 프레임을 지배해 모음 확률이 0으로 눌린다.
    # blank를 빼고 나머지 토큰으로 재정규화해 모음 비율이 살아나게 한다.
    probs[:, pad_id] = 0.0
    probs = probs / probs.sum(dim=-1, keepdim=True).clamp_min(1e-8)
    probs = probs.cpu().numpy()

    n_frames = probs.shape[0]
    groups = np.zeros((n_frames, 5))
    for idx, tok in id2tok.items():
        g = groups_map.get(tok.strip())
        if g is not None:
            groups[:, "aiueo".index(g)] += probs[:, idx]
    # 모음 덩어리가 충분한 프레임은 지배 모음이 모프를 온전히 구동하게
    # 모음 내 정규화한다. 임계 미만(자음/불확실)은 전부 0 → 입 닫힘.
    vsum = groups.sum(axis=1, keepdims=True)
    mask = (vsum[:, 0] > 0.15)
    groups[mask] /= vsum[mask]
    groups[~mask] = 0.0

    # wav2vec2 프레임 스트라이드(320샘플)에 맞춰 프레임별 RMS 음량을 계산한다.
    stride = int(round(16000 * 20 / 1000))
    vols = np.zeros(n_frames, dtype=np.float32)
    for i in range(n_frames):
        seg = wav[i * stride:(i + 1) * stride]
        if seg.size:
            vols[i] = float(np.sqrt(np.mean(seg ** 2)))

    with open(args.output, "w", newline="", encoding="utf-8") as f:
        f.write(json.dumps({"model": args.model, "sr": 16000,
                            "fps": 16000.0 / stride}) + "\n")
        w = csv.writer(f)
        for i in range(n_frames):
            w.writerow(["%.4f" % vols[i]] + ["%.4f" % x for x in groups[i]])
    print("frames=%d fps=%.2f" % (n_frames, 16000.0 / stride))


if __name__ == "__main__":
    sys.exit(main())
