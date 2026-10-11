"""_job_clean 회귀 테스트 — 정제 스텝의 앙상블 프리셋이 분리기로 전달되는지 검증.

버그 이력: DEFAULT_CLEAN_STEPS·C# 계약은 camelCase "ensemblePreset"을 쓰는데
_job_clean이 step.get("ensemble_preset")만 읽어 karaoke 패스가 프리셋 없이
실행되던 침묵 버그가 있었다(2026-10-12). camelCase/snake_case 양쪽을 덮는다.
실제 분리는 _audio_separator_run을 목으로 대체하고, 스템 산출물만 가짜 파일로 만든다.
"""

import asyncio
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import server

_STEMS = ["Vocals", "noreverb", "dry"]


def _run_clean(params):
    with tempfile.TemporaryDirectory() as tmp_s:
        tmp = Path(tmp_s)
        (tmp / "song_vocal.wav").write_bytes(b"RIFF")
        (tmp / "song.wav").write_bytes(b"RIFF")
        calls = []

        def fake_sep_run(job, input_path, out_dir, model,
                         model_dir, ensemble_preset, high_quality):
            calls.append({"input": input_path, "model": model,
                          "preset": ensemble_preset})
            # resolve_stem_output이 찾는 명명 규칙: <입력stem>_..._(<스템>)_x.wav
            stem_file = (Path(out_dir)
                         / f"{Path(input_path).stem}_({_STEMS[len(calls) - 1]})_fake.wav")
            stem_file.write_bytes(b"RIFF")
            return [str(stem_file)]

        params = dict(params)
        params.setdefault("input", str(tmp / "song_vocal.wav"))
        params.setdefault("output_dir", str(tmp / "out"))
        params.setdefault("model_dir", "")
        params.setdefault("original_mix", str(tmp / "song.wav"))
        job = server.Job("clean", str(tmp / "song_vocal.wav"), params)
        with patch.object(server, "WARM", True), \
                patch.object(server, "_audio_separator_run",
                             side_effect=fake_sep_run):
            asyncio.run(server._job_clean(job))
        return calls, job


class JobCleanPresetTest(unittest.TestCase):

    def test_기본체인_1패스는카라오케프리셋으로실행(self):
        calls, job = _run_clean({"high_quality": False})
        self.assertEqual(len(calls), 3)
        # 내부 기본값(camelCase ensemblePreset)도 분리기에 전달되어야 한다.
        self.assertEqual(calls[0]["preset"], "karaoke")
        self.assertEqual(calls[1]["model"],
                         "deverb_bs_roformer_8_384dim_10depth.ckpt")
        self.assertIn("_vocal_clean.wav", job.outputs["vocal"])

    def test_외부페이로드_snake_case도동일하게동작(self):
        steps = [
            {"model": None, "stemKeyword": "Vocals", "label": "카라오케",
             "ensemble_preset": "karaoke", "useOriginalMix": True,
             "script": None},
            {"model": "deverb_bs_roformer_8_384dim_10depth.ckpt",
             "stemKeyword": "noreverb", "label": "디리버브",
             "ensemble_preset": "", "useOriginalMix": False, "script": None},
            {"model": "denoise_mel_band_roformer_aufr33_sdr_27.9959.ckpt",
             "stemKeyword": "dry", "label": "디노이즈",
             "ensemble_preset": "", "useOriginalMix": False, "script": None},
        ]
        calls, _ = _run_clean({"high_quality": False, "steps": steps})
        self.assertEqual(calls[0]["preset"], "karaoke")


if __name__ == "__main__":
    unittest.main()
