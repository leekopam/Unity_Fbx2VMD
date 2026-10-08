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

# 모음 연장 표기(IPA ː, 가타카나 ー) — 확률이 지배적이면 직전 프레임 모음을 계승한다.
LONG_MARKS = ("ː", "ー")


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


def _hangul_group(ch):
    """한글 음절의 중성 → 모음 그룹. 음절이 아니면 None."""
    s = ord(ch) - 0xAC00
    if not (0 <= s < 11172):
        return None
    jung = (s % 588) // 28
    if jung <= 3:
        return "a"   # ㅏㅐㅑㅒ
    if jung <= 7:
        return "e"   # ㅓㅔㅕㅖ
    if jung <= 12:
        return "o"   # ㅗㅘㅙㅚㅛ
    if jung <= 18:
        return "u"   # ㅜㅝㅞㅟㅠㅡ
    return "i"       # ㅣㅢ


def _vocab_kind(vocab):
    """어휘 문자 집합으로 매핑 방식을 고른다: 한글 음절 > 가나 > IPA."""
    for k in vocab:
        if any("가" <= c <= "힣" for c in k):
            return "hangul"
        if any("ぁ" <= c <= "ヶ" for c in k):
            return "kana"
    return "ipa"


def _token_group(tok, kind, kana_map):
    """토큰 → 모음 그룹. 다중 문자 토큰은 끝 문자부터 역순 탐색한다
    (CTC 토큰 경계는 토큰 끝에 정렬되므로 aɪ→i, 니가→a처럼 마지막 모음이 지배)."""
    text = tok.strip()
    if not text:
        return None
    if kind == "hangul":
        for ch in reversed(text):
            g = _hangul_group(ch)
            if g:
                return g
        return None
    if kind == "kana":
        return kana_map.get(text)
    for ch in reversed(text):
        g = VOWEL_GROUPS.get(ch)
        if g:
            return g
    return None


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
    vocab_kind = _vocab_kind(vocab)
    kana_map = _kana_groups() if vocab_kind == "kana" else {}

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
    long_ids = []
    for idx, tok in id2tok.items():
        text = tok.strip()
        g = _token_group(text, vocab_kind, kana_map)
        if g is not None:
            groups[:, "aiueo".index(g)] += probs[:, idx]
        elif text in LONG_MARKS:
            long_ids.append(idx)
    # 장음(ー·ː)은 앞 모음의 연장이라 직전 프레임의 모음 벡터를 이어받는다.
    # 매핑에 넣지 않으면 노래의 지속 모음 구간이 자음 취급돼 입이 닫힌다.
    if long_ids:
        longp = probs[:, long_ids].sum(axis=1)
        for i in range(1, n_frames):
            if longp[i] > groups[i].max():
                groups[i] = groups[i - 1]
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
