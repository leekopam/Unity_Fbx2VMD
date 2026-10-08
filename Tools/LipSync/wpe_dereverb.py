# WPE(Weighted Prediction Error) 디리버브 — nara_wpe 결정적 알고리즘.
# 잔향의 꼬리 성분이 과거 프레임의 선형 결합으로 예측된다는 물리 모델을
# 추정해 빼내므로, 신경망 디리버브처럼 리드 보컬을 과제거할 위험이 없다.
# 사용: python wpe_dereverb.py --input in.wav --output out.wav [--taps 10] [--delay 3] [--iterations 5]
import argparse
import sys

import numpy as np


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", required=True)
    ap.add_argument("--output", required=True)
    ap.add_argument("--taps", type=int, default=10)
    ap.add_argument("--delay", type=int, default=3)
    ap.add_argument("--iterations", type=int, default=5)
    ap.add_argument("--size", type=int, default=1024)
    ap.add_argument("--shift", type=int, default=256)
    args = ap.parse_args()

    import soundfile as sf
    from nara_wpe.utils import istft, stft
    from nara_wpe.wpe import wpe

    data, sr = sf.read(args.input, always_2d=True)   # (T, C)
    sig = np.ascontiguousarray(data.T, dtype=np.float64)  # (C, T)
    print("10% STFT", flush=True)
    Y = stft(sig, size=args.size, shift=args.shift)  # (C, T, F)
    print("40% WPE 추정", flush=True)
    # wpe는 (F, D, T) 순서 — stft 출력 (C,T,F)에서 축을 바꿔 넣는다.
    Z = wpe(Y.transpose(2, 0, 1), taps=args.taps, delay=args.delay,
            iterations=args.iterations)
    print("80% ISTFT", flush=True)
    out = istft(Z.transpose(1, 2, 0), size=args.size, shift=args.shift)  # (C, T)
    out = np.asarray(out)
    if out.ndim == 1:
        out = out[None, :]
    # 길이를 원본에 맞춘다.
    n = data.shape[0]
    if out.shape[1] < n:
        out = np.pad(out, ((0, 0), (0, n - out.shape[1])))
    sf.write(args.output, out[:, :n].T.astype(np.float32), sr)
    print("frames=%d sr=%d" % (n, sr), flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
