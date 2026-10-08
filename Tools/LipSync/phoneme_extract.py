# wav2vec2-IPA 음소 추출 — 리드 보컬 wav를 프레임별 모음 확률(v,a,i,u,e,o) CSV로 변환한다.
# 코러스 잔류가 있어도 MFCC 기반보다 음소 인식이 흔들리지 않아 uLipSync 대안으로 쓴다.
# 사용: python phoneme_extract.py <input.wav> --output <out.csv> [--model HF_ID]
#       [--device cuda|cpu] [--backend auto|torch|onnx] [--lyrics lyrics.txt]
#       [--no-vad] [--no-viterbi] [--loudness k|rms]
# 참고: Wav2Vec2PhonemeCTCTokenizer는 phonemizer+espeak-ng를 요구하므로(Windows 설치 곤란)
#       feature extractor로 입력을 만들고 vocab.json만 직접 읽어 토큰 id를 해석한다.
import argparse
import csv
import json
import math
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

# wav2vec2 출력 프레임 스트라이드(16kHz × 20ms)
STRIDE = 320
# 청크 추론 — 진행률 보고와 메모리 상한을 위해 25초 단위로 나누고 경계 0.5초는 버린다.
CHUNK_SEC = 25.0
EDGE_SEC = 0.5


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
        if text in kana_map:
            return kana_map[text]
        # 어휘가 가타카나고 가사가 히라가나인 경우 등을 대비한 평행 이동 폴백.
        for ch in reversed(text):
            if "ァ" <= ch <= "ヶ":
                ch = chr(ord(ch) - 0x60)
            elif "ぁ" <= ch <= "ゖ":
                ch = chr(ord(ch) + 0x60)
            g = kana_map.get(ch)
            if g:
                return g
        return None
    for ch in reversed(text):
        g = VOWEL_GROUPS.get(ch)
        if g:
            return g
    return None


def _infer_torch(model, extractor, wav, device, on_progress):
    """25초 청크로 나눠 추론한다. 경계 프레임은 문맥 부족으로 오인식되므로
    입력을 앞뒤 0.5초씩 중첩해 읽고, 중첩분에 해당하는 출력 프레임만 버린다.
    (비중첩 입력에서 프레임을 버리면 경계당 ~1초의 오디오가 유실돼
    이후 타임라인 전체가 앞당겨진다.)"""
    chunk = int(CHUNK_SEC * 16000)
    edge = int(EDGE_SEC * 16000)
    logits_parts = []
    n_chunks = max(1, math.ceil(len(wav) / chunk))
    for ci in range(n_chunks):
        start = ci * chunk
        end = min(len(wav), start + chunk)
        lead = min(edge, start)
        tail = min(edge, len(wav) - end)
        seg = wav[start - lead:end + tail]
        inputs = extractor(seg, sampling_rate=16000, return_tensors="pt")
        with torch.no_grad():
            lg = model(inputs.input_values.to(device)).logits[0]
        lf = lg.shape[0]
        drop_h = min(lf // 4, lead // STRIDE)
        drop_t = min(lf // 4, tail // STRIDE)
        logits_parts.append(lg[drop_h:lf - drop_t].cpu())
        on_progress(ci + 1, n_chunks)
    return torch.cat(logits_parts, dim=0)


def _infer_onnx(model, extractor, wav, device, onnx_path, on_progress):
    """torch 모델을 ONNX로보낸 뒤 onnxruntime으로 청크 추론한다.
    실패하면 예외를 올려 호출부가 torch 경로로 폴백하게 한다."""
    import onnxruntime as ort

    inputs = extractor(wav[:16000], sampling_rate=16000, return_tensors="pt")
    torch.onnx.export(
        model, inputs.input_values.to(device), onnx_path,
        input_names=["input_values"], output_names=["logits"],
        dynamic_axes={"input_values": {1: "time"}, "logits": {1: "frames"}},
        opset_version=18)
    providers = [p for p in ("CUDAExecutionProvider", "DmlExecutionProvider",
                             "CPUExecutionProvider")
                 if p in ort.get_available_providers()]
    sess = ort.InferenceSession(onnx_path, providers=providers)
    chunk = int(CHUNK_SEC * 16000)
    edge = int(EDGE_SEC * 16000)
    parts = []
    n_chunks = max(1, math.ceil(len(wav) / chunk))
    for ci in range(n_chunks):
        start = ci * chunk
        end = min(len(wav), start + chunk)
        lead = min(edge, start)
        tail = min(edge, len(wav) - end)
        seg = wav[start - lead:end + tail]
        inp = extractor(seg, sampling_rate=16000, return_tensors="np")
        lg = sess.run(["logits"], {"input_values": inp.input_values})[0][0]
        lf = lg.shape[0]
        drop_h = min(lf // 4, lead // STRIDE)
        drop_t = min(lf // 4, tail // STRIDE)
        parts.append(torch.from_numpy(lg[drop_h:lf - drop_t]))
        on_progress(ci + 1, n_chunks)
    return torch.cat(parts, dim=0)


def _viterbi_smooth(groups):
    """모음 사후확률 열에 자기루프 전이 비용을 둔 Viterbi 최적 경로.
    프레임 독립 판정의 깜빡임을 상태 전이 확률로 억제한다.
    상태 = a,i,u,e,o + none(모음 덩어리 없음). 출력은 디코딩된 상태의 원핫 —
    크기는 해당 프레임의 원래 모음 총량(vsum)을 보존한다."""
    n = groups.shape[0]
    if n == 0:
        return groups
    eps = 1e-8
    none = np.clip(1.0 - groups.sum(axis=1), 0.0, 1.0)[:, None]
    emit = np.log(np.concatenate([groups, none], axis=1) + eps)  # [T,6]
    stay = 0.75
    switch = (1.0 - stay) / 5.0
    trans = np.full((6, 6), math.log(switch))
    np.fill_diagonal(trans, math.log(stay))

    dp = emit[0].copy()
    back = np.zeros((n, 6), dtype=np.int64)
    for t in range(1, n):
        cand = dp[:, None] + trans          # [prev 6, cur 6]
        back[t] = cand.argmax(axis=0)
        dp = cand.max(axis=0) + emit[t]
    seq = np.zeros(n, dtype=np.int8)
    seq[-1] = int(dp.argmax())
    for t in range(n - 1, 0, -1):
        seq[t - 1] = back[t, seq[t]]

    out = np.zeros_like(groups)
    for t in range(n):
        s = seq[t]
        if s < 5:
            out[t, s] = min(1.0, float(groups[t].sum()))
    return out


def _speech_mask(wav, n_frames):
    """silero-vad로 보컬 존재 구간을 찾아 프레임 단위 마스크로 만든다.
    패키지가 없으면 None을 반환해 호출부가 게이트 없이 진행하게 한다."""
    try:
        from silero_vad import load_silero_vad, get_speech_timestamps
    except Exception:
        print("VAD 건너뜀: silero-vad 미설치", flush=True)
        return None
    try:
        vad = load_silero_vad()
        spans = get_speech_timestamps(torch.from_numpy(wav), vad,
                                      sampling_rate=16000)
        mask = np.zeros(n_frames, dtype=bool)
        for sp in spans:
            f0 = max(0, sp["start"] // STRIDE)
            f1 = min(n_frames, (sp["end"] + STRIDE - 1) // STRIDE)
            mask[f0:f1] = True
        return mask
    except Exception as exc:  # 모델 다운로드 실패 등 — VAD 없이 계속 진행
        print("VAD 건너뜀: %s" % exc, flush=True)
        return None


def _kweighted_vols(wav, n_frames):
    """ITU-R BS.1770 K-weighting(고주파 선행 강조 + RLB 고역통과)을 건 뒤
    프레임별 RMS를 돌려준다. 저주파 BGM 잔여가 음량을 부풀리는 것을 막는다.
    pyloudnorm이 없으면 평탄 RMS로 폴백한다."""
    filt = wav
    try:
        from pyloudnorm import IIRfilter
        import scipy.signal
        shelf = IIRfilter(4.0, 1.0 / math.sqrt(2.0), 1500.0, 16000, "high_shelf")
        highp = IIRfilter(0.0, 0.5, 38.0, 16000, "high_pass")
        filt = scipy.signal.lfilter(shelf.b, shelf.a, wav)
        filt = scipy.signal.lfilter(highp.b, highp.a, filt)
    except Exception as exc:
        print("K-weighting 건너뜀: %s" % exc, flush=True)
    vols = np.zeros(n_frames, dtype=np.float32)
    for i in range(n_frames):
        seg = filt[i * STRIDE:(i + 1) * STRIDE]
        if seg.size:
            vols[i] = float(np.sqrt(np.mean(seg ** 2)))
    return vols


def _load_lyrics(path, vocab, pad_id):
    """가사 파일을 읽어 어휘 토큰 id 열로 변환한다.
    어휘에 없는 문자(공백·한자 등)는 건너뛴다 — 정렬 대상은 발음 토큰뿐이다."""
    with open(path, encoding="utf-8") as f:
        text = f.read()
    ids = []
    skipped = 0
    for ch in text:
        c = ch.strip()
        if not c:
            continue
        # 어휘 표기와 가사 표기의 가나 계열이 다를 수 있어 양방향을 시도한다.
        cands = [c]
        if "ァ" <= c <= "ヶ":
            cands.append(chr(ord(c) - 0x60))
        elif "ぁ" <= c <= "ゖ":
            cands.append(chr(ord(c) + 0x60))
        hit = next((vocab[cx] for cx in cands if cx in vocab), None)
        if hit is None or hit == pad_id:
            skipped += 1
            continue
        ids.append(hit)
    if skipped:
        print("가사 정렬: 어휘에 없는 문자 %d개 건너뜀" % skipped, flush=True)
    return ids


def _forced_align(log_probs, lyric_ids, pad_id, id2tok, kind, kana_map):
    """CTC 강제 정렬 — 가사 토큰 열로 제한한 Viterbi 경로를 찾아
    각 프레임이 어느 가사 토큰에 대응하는지 돌려준다.
    반환: 프레임별 모음 그룹 원핫(무토큰 프레임은 0)."""
    n = log_probs.shape[0]
    if n == 0 or not lyric_ids:
        return None
    # 확장 열: blank, tok1, blank, tok2, ... , tokN, blank
    ext = []
    for t in lyric_ids:
        ext.append(pad_id)
        ext.append(t)
    ext.append(pad_id)
    s_len = len(ext)

    neg = -1e30
    dp = np.full(s_len, neg)
    dp[0] = log_probs[0, pad_id]
    if s_len > 1:
        dp[1] = log_probs[0, ext[1]]
    back = np.zeros((n, s_len), dtype=np.int64)
    for t in range(1, n):
        ndp = np.full(s_len, neg)
        lp = log_probs[t]
        for j in range(s_len):
            cands = [dp[j]]
            if j >= 1:
                cands.append(dp[j - 1])
            # 같은 토큰이 blank를 사이에 두고 반복될 때만 j-2 도약 허용.
            if j >= 2 and ext[j] != pad_id and ext[j] != ext[j - 2]:
                cands.append(dp[j - 2])
            best = int(np.argmax(cands))
            back[t, j] = j - best
            ndp[j] = cands[best] + lp[ext[j]]
        dp = ndp

    # 끝 상태는 마지막 blank 또는 마지막 토큰.
    end = s_len - 1 if dp[s_len - 1] >= dp[s_len - 2] else s_len - 2
    if dp[end] <= neg / 2:
        return None  # 가사와 음성이 전혀 안 맞으면 폴백
    state_path = np.zeros(n, dtype=np.int64)
    state_path[-1] = end
    for t in range(n - 1, 0, -1):
        state_path[t - 1] = back[t, state_path[t]]

    out = np.zeros((n, 5), dtype=np.float32)
    for t in range(n):
        j = state_path[t]
        if ext[j] == pad_id:
            continue
        tok = id2tok.get(ext[j], "")
        g = _token_group(tok, kind, kana_map)
        if g is not None:
            out[t, "aiueo".index(g)] = 1.0
        elif tok.strip() in LONG_MARKS and t > 0:
            out[t] = out[t - 1]
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input")
    ap.add_argument("--output", required=True)
    ap.add_argument("--model",
                    default="jonatasgrosman/wav2vec2-large-xlsr-53-japanese")
    ap.add_argument("--device", default=None)
    ap.add_argument("--backend", default="auto",
                    choices=["auto", "torch", "onnx"])
    ap.add_argument("--lyrics", default=None,
                    help="가사 텍스트 파일 — 있으면 CTC 강제 정렬로 음소 오류를 없앤다")
    ap.add_argument("--no-vad", action="store_true",
                    help="silero-vad 보컬 구간 게이트를 끈다")
    ap.add_argument("--no-viterbi", action="store_true",
                    help="모음 확률 열의 Viterbi 스무딩을 끈다")
    ap.add_argument("--loudness", default="k", choices=["k", "rms"],
                    help="volume 채널 계산 방식 — k=ITU-R BS.1770 K-weighting")
    args = ap.parse_args()

    # torch.onnx 내부 출력에 이모지가 섞여 있어 cp949 콘솔에서 깨진다 — 파이프 출력을 UTF-8로 고정한다.
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass

    import librosa
    from huggingface_hub import hf_hub_download
    from transformers import AutoFeatureExtractor, Wav2Vec2ForCTC

    device = args.device or ("cuda" if torch.cuda.is_available() else "cpu")
    print("10% 모델 로딩", flush=True)
    extractor = AutoFeatureExtractor.from_pretrained(args.model)
    model = Wav2Vec2ForCTC.from_pretrained(args.model).to(device).eval()
    with open(hf_hub_download(args.model, "vocab.json"), encoding="utf-8") as f:
        vocab = json.load(f)
    id2tok = {v: k for k, v in vocab.items()}
    # CTC blank 인덱스는 모델 config의 pad_token_id가 정본이다 — 모델마다
    # "<pad>"(0번)와 "[PAD]"(끝 인덱스, kresnik 한국어=1204)로 어휘 표기가 달라
    # 어휘 조회만으로는 blank를 못 지울 수 있다.
    pad_id = getattr(model.config, "pad_token_id", None)
    if pad_id is None:
        pad_id = next((vocab[k] for k in ("<pad>", "[PAD]", "[pad]", "<PAD>")
                       if k in vocab), 0)
    print("30% wav 로딩", flush=True)
    vocab_kind = _vocab_kind(vocab)
    kana_map = _kana_groups() if vocab_kind == "kana" else {}

    wav, _ = librosa.load(args.input, sr=16000, mono=True)

    # 보컬 존재 구간 마스크 — 간주/인스트 구간의 잔류 BGM이 립을 흔들지 못하게 한다.
    mask = None
    if not args.no_vad:
        mask = _speech_mask(wav, max(1, math.ceil(len(wav) / STRIDE)))

    def chunk_progress(done, total):
        # 파서가 선행 백분율을 읽으므로 추론 구간을 전체 40~80%로 환산해 출력한다.
        print("%d%% wav2vec2 추론 %d/%d" % (40 + int(40.0 * done / total),
                                          done, total), flush=True)

    backend = args.backend
    logits = None
    if backend in ("auto", "onnx"):
        try:
            import os
            onnx_path = args.output + ".onnx"
            logits = _infer_onnx(model, extractor, wav, device,
                                 onnx_path, chunk_progress)
            backend = "onnx"
            try:
                # InferenceSession이 파일을 잠그고 있어 GC 후에도 실패할 수 있다 —
                # 잔여 .onnx는 출력 폴더(gitignore 대상)에 남는 것뿐이라 무시한다.
                import gc
                gc.collect()
                os.remove(onnx_path)
            except OSError:
                pass
        except Exception as exc:
            if backend == "onnx":
                raise
            print("ONNX 경로 실패, torch로 폴백: %s" % exc, flush=True)
            logits = None
    if logits is None:
        logits = _infer_torch(model, extractor, wav, device, chunk_progress)
        backend = "torch"
    print("80%% 모음 그룹 집계(backend=%s)" % backend, flush=True)

    probs = torch.softmax(logits, dim=-1)
    # 가사 강제 정렬은 blank 확률이 필요하므로 제거 전 원본 로그확률을 보존한다.
    log_probs_full = torch.log(probs.clamp_min(1e-8)).cpu().numpy()
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

    lyric_groups = None
    if args.lyrics:
        lyric_ids = _load_lyrics(args.lyrics, vocab, pad_id)
        lyric_groups = _forced_align(log_probs_full, lyric_ids, pad_id,
                                     id2tok, vocab_kind, kana_map)
        if lyric_groups is None:
            print("가사 정렬 실패 — blind 추출 결과로 진행", flush=True)

    if lyric_groups is not None:
        groups = lyric_groups
    elif not args.no_viterbi:
        groups = _viterbi_smooth(groups)

    # 가사 정렬 출력은 이미 원핫이라 재정규화 불필요. blind 경로만 모음 덩어리가
    # 충분한 프레임을 지배 모음 기준으로 정규화하고 임계 미만은 입 닫힘(0)이다.
    if lyric_groups is None:
        vsum = groups.sum(axis=1, keepdims=True)
        m = (vsum[:, 0] > 0.15)
        groups[m] /= vsum[m]
        groups[~m] = 0.0

    # 지각 음량 — K-weighting 필터 후 프레임 RMS.
    vols = _kweighted_vols(wav, n_frames) if args.loudness == "k" \
        else np.array([float(np.sqrt(np.mean(
            wav[i * STRIDE:(i + 1) * STRIDE] ** 2)))
            if wav[i * STRIDE:(i + 1) * STRIDE].size else 0.0
            for i in range(n_frames)], dtype=np.float32)

    # VAD 무음 구간은 모음·음량 모두 0 — 베이크 게이트가 확실히 닫히게 한다.
    if mask is not None:
        dead = np.where(~mask[:n_frames])[0]
        groups[dead] = 0.0
        vols[dead] = 0.0

    print("95% CSV 기록", flush=True)
    with open(args.output, "w", newline="", encoding="utf-8") as f:
        f.write(json.dumps({"model": args.model, "sr": 16000,
                            "fps": 16000.0 / STRIDE}) + "\n")
        w = csv.writer(f)
        for i in range(n_frames):
            w.writerow(["%.4f" % vols[i]] + ["%.4f" % x for x in groups[i]])
    print("frames=%d fps=%.2f" % (n_frames, 16000.0 / STRIDE))


if __name__ == "__main__":
    sys.exit(main())
