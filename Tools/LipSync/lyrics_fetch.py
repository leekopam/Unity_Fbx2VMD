#!/usr/bin/env python3
# usage: lyrics_fetch.py --audio <원곡> --out <저장기본경로> [--title] [--artist]
#        [--lang ja|ko|en] [--json <결과json>] [--offline]
# 가사 수집기 — 로컬 파일 → 임베디드 태그 → LRCLIB → VocaDB 순 폴백.
# 결과는 stdout 로그 + --json 경로의 JSON(후보 목록·점수·미리보기)으로 낸다.
# 수집한 가사는 <out>.txt(정렬용 정규화 텍스트)와 <out>.lrc(타임스탬프 보존)로 쓴다.
import argparse
import json
import os
import re
import sys
import urllib.parse
import urllib.request

UA = "Fbx2Vmd-LipSync/1.0 (local editor tool)"
LRC_TAG = re.compile(r"\[([^\[\]]*)\]")
LRC_TIME = re.compile(r"^(\d+):(\d+(?:\.\d+)?)$")


def _http_json(url, timeout=10):
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read().decode("utf-8"))


# ---------- LRC ----------

def parse_lrc(text):
    """LRC 텍스트 → (lines=[(sec, text)], offset_sec). 태그/빈 줄 제거.
    [t1][t2]가사처럼 한 줄에 여러 타임스탬프가 붙은 반복 표기는
    각 시각의 별도 항목으로 전개한다(코러스 반복 구간 정렬에 필수)."""
    lines, offset_ms = [], 0.0
    for raw in text.splitlines():
        tags = LRC_TAG.findall(raw)
        body = LRC_TAG.sub("", raw).strip()
        times = []
        for t in tags:
            key, _, val = t.partition(":")
            m = LRC_TIME.match(t.strip())
            if m:
                times.append(int(m.group(1)) * 60 + float(m.group(2)))
            elif key.strip() == "offset":
                try:
                    offset_ms = float(val)
                except ValueError:
                    pass
        for sec in times:
            if body:
                lines.append((sec, body))
        if not times and body and not tags:
            lines.append((None, body))
    lines.sort(key=lambda x: (x[0] is None, x[0] or 0))
    return lines, offset_ms / 1000.0


def lrc_plain(lines):
    return "\n".join(t for _, t in lines).strip() + "\n"


# ---------- 표기 변환 ----------

_ROMAJI = [
    ("kya", "きゃ"), ("kyu", "きゅ"), ("kyo", "きょ"), ("sha", "しゃ"),
    ("shu", "しゅ"), ("sho", "しょ"), ("cha", "ちゃ"), ("chu", "ちゅ"),
    ("cho", "ちょ"), ("nya", "にゃ"), ("nyu", "にゅ"), ("nyo", "にょ"),
    ("hya", "ひゃ"), ("hyu", "ひゅ"), ("hyo", "ひょ"), ("mya", "みゃ"),
    ("myu", "みゅ"), ("myo", "みょ"), ("rya", "りゃ"), ("ryu", "りゅ"),
    ("ryo", "りょ"), ("gya", "ぎゃ"), ("gyu", "ぎゅ"), ("gyo", "ぎょ"),
    ("ja", "じゃ"), ("ju", "じゅ"), ("jo", "じょ"), ("bya", "びゃ"),
    ("byu", "びゅ"), ("byo", "びょ"), ("pya", "ぴゃ"), ("pyu", "ぴゅ"),
    ("pyo", "ぴょ"), ("tsu", "つ"), ("shi", "し"), ("chi", "ち"),
    ("ka", "か"), ("ki", "き"), ("ku", "く"), ("ke", "け"), ("ko", "こ"),
    ("sa", "さ"), ("su", "す"), ("se", "せ"), ("so", "そ"), ("ta", "た"),
    ("te", "て"), ("to", "と"), ("na", "な"), ("ni", "に"), ("nu", "ぬ"),
    ("ne", "ね"), ("no", "の"), ("ha", "は"), ("hi", "ひ"), ("fu", "ふ"),
    ("he", "へ"), ("ho", "ほ"), ("ma", "ま"), ("mi", "み"), ("mu", "む"),
    ("me", "め"), ("mo", "も"), ("ya", "や"), ("yu", "ゆ"), ("yo", "よ"),
    ("ra", "ら"), ("ri", "り"), ("ru", "る"), ("re", "れ"), ("ro", "ろ"),
    ("wa", "わ"), ("wo", "を"), ("ga", "が"), ("gi", "ぎ"), ("gu", "ぐ"),
    ("ge", "げ"), ("go", "ご"), ("za", "ざ"), ("ji", "じ"), ("zu", "ず"),
    ("ze", "ぜ"), ("zo", "ぞ"), ("da", "だ"), ("de", "で"), ("do", "ど"),
    ("ba", "ば"), ("bi", "び"), ("bu", "ぶ"), ("be", "べ"), ("bo", "ぼ"),
    ("pa", "ぱ"), ("pi", "ぴ"), ("pu", "ぷ"), ("pe", "ぺ"), ("po", "ぽ"),
    ("a", "あ"), ("i", "い"), ("u", "う"), ("e", "え"), ("o", "お"),
    ("n", "ん"),
]


def romaji_to_kana(text):
    """로마자 가사 → 가나(장음은 ー). 임베디드/수동 로마자 가사용 폴백 변환."""
    out = []
    for word in text.lower().split():
        i = 0
        buf = []
        while i < len(word):
            ch = word[i]
            if ch in "āīūēō":
                buf.append("ー")
                i += 1
                continue
            hit = None
            for L in (3, 2, 1):
                seg = word[i:i + L]
                for k, v in _ROMAJI:
                    if len(k) == L and seg == k:
                        hit = v
                        i += L
                        break
                if hit:
                    break
            if hit is None:
                if ch == "n" and (i + 1 >= len(word)
                                  or word[i + 1] not in "aiueoy"):
                    hit = "ん"
                    i += 1
                elif i + 1 < len(word) and ch == word[i + 1] \
                        and ch in "kstnpbgzmr":
                    hit = "っ"
                    i += 1
                else:
                    i += 1
                    continue
            buf.append(hit)
        out.append("".join(buf))
    return " ".join(out)


_KANA_OK = re.compile(r"[ぁ-ヶー・'\s]")


def normalize_ja(text, warnings):
    """한자 포함 일본어 → 가나. fugashi+unidic-lite가 있으면 독서 추출,
    없으면 가나/장음만 남기고 한자 비율을 경고한다."""
    try:
        import fugashi
        import unidic_lite  # noqa: F401 — 사전 경로 등록용
        tagger = fugashi.Tagger()
        parts = []
        for w in tagger(text):
            kana = getattr(w.feature, "kana", None) or w.surface
            parts.append(kana)
        return " ".join("".join(parts).splitlines())
    except ImportError:
        pass
    kept = "".join(c for c in text if _KANA_OK.match(c))
    kanji = sum(1 for c in text if "一" <= c <= "鿿")
    if kanji:
        warnings.append(
            "한자 %d자 제거됨 — fugashi+unidic-lite 설치 시 독서 변환으로 정확도↑"
            % kanji)
    return kept


def normalize(text, lang, warnings):
    """CTC 강제 정렬 입력용 정규화 — 어휘 문자만 남기는 방향으로 정리한다."""
    text = re.sub(r"\s+", " ", text).strip()
    if lang == "ja":
        # 로마자 비중이 높으면 로마자→가나로 본다.
        alpha = sum(1 for c in text if c.isascii() and c.isalpha())
        total = max(1, len(text.replace(" ", "")))
        if alpha / total > 0.7:
            text = romaji_to_kana(text)
            warnings.append("로마자 가사를 규칙 변환으로 가나화했다(검토 권장)")
        else:
            text = normalize_ja(text, warnings)
    elif lang == "en":
        try:
            import eng_to_ipa
            text = eng_to_ipa.convert(text)
        except ImportError:
            warnings.append(
                "영어 모델은 IPA 어휘 — eng-to-ipa 미설치로 원문 유지(정렬 실패 가능)")
    return text.strip()


# ---------- 소스 ----------

def _read_text(path):
    """일본 레거시 가사는 Shift_JIS인 경우가 있어 UTF-8 실패 시 폴백한다."""
    try:
        with open(path, encoding="utf-8-sig") as f:
            return f.read()
    except UnicodeDecodeError:
        with open(path, encoding="cp932", errors="replace") as f:
            return f.read()


def from_local(audio, warnings):
    stem = os.path.splitext(audio)[0]
    for ext in (".lrc", ".LRC", ".txt"):
        p = stem + ext
        if os.path.isfile(p):
            try:
                raw = _read_text(p)
            except OSError as exc:
                warnings.append("로컬 가사 읽기 실패: %s" % exc)
                continue
            return {"source": "local", "raw": raw,
                    "is_lrc": ext.lower() == ".lrc", "score": 1.0,
                    "title": os.path.basename(stem), "artist": ""}
    return None


def from_tags(audio, warnings):
    try:
        import mutagen
    except ImportError:
        return None
    try:
        f = mutagen.File(audio)
        if f is None:
            return None
        text = None
        for key in f.keys() if hasattr(f, "keys") else []:
            k = str(key).lower()
            # USLT(비동기)·SYLT(동기) 프레임 — repr이 아니라 .text를 읽는다.
            if "uslt" in k or "sylt" in k or "lyr" in k or "©lyr" in k:
                v = f[key]
                v = v[0] if isinstance(v, (list, tuple)) else v
                text = getattr(v, "text", None) or str(v)
                if text and text.strip():
                    break
        if text and text.strip():
            return {"source": "tags", "raw": text.strip(), "is_lrc": "[" in text,
                    "score": 0.95, "title": "", "artist": ""}
    except Exception as exc:
        warnings.append("태그 가사 읽기 실패: %s" % exc)
    return None


def _duration(audio, warnings):
    try:
        import librosa
        return float(librosa.get_duration(path=audio))
    except Exception as exc:
        warnings.append("곡 길이 측정 실패: %s" % exc)
        return None


def _sim(a, b):
    import difflib
    return difflib.SequenceMatcher(None, (a or "").lower(),
                                   (b or "").lower()).ratio()


def from_lrclib(title, artist, duration, warnings):
    if not title:
        return None
    q = {"track_name": title}
    if artist:
        q["artist_name"] = artist
    url = "https://lrclib.net/api/search?" + urllib.parse.urlencode(q)
    try:
        hits = _http_json(url)
    except Exception as exc:
        warnings.append("LRCLIB 조회 실패: %s" % exc)
        return None
    if not isinstance(hits, list):  # 오류 응답은 {"error":...} 객체다
        warnings.append("LRCLIB 응답 형식 이상 — 건너뜀")
        return None
    best = None
    for h in hits[:10]:
        if h.get("instrumental"):
            continue
        raw = h.get("syncedLyrics") or h.get("plainLyrics")
        if not raw:
            continue
        score = _sim(title, h.get("trackName", ""))
        if artist:
            score = 0.5 * score + 0.5 * _sim(artist, h.get("artistName", ""))
        if duration:
            dd = abs((h.get("duration") or 0) - duration)
            if dd > 5:
                continue
            score *= max(0.0, 1.0 - dd / 5.0)
        if best is None or score > best["score"]:
            best = {"source": "lrclib", "raw": raw,
                    "is_lrc": bool(h.get("syncedLyrics")),
                    "score": round(score, 3),
                    "title": h.get("trackName", ""),
                    "artist": h.get("artistName", "")}
    return best


def from_vocadb(title, warnings):
    if not title:
        return None
    url = ("https://vocadb.net/api/songs?query="
           + urllib.parse.quote(title)
           + "&fields=Lyrics&maxResults=5&preferCompleteMatch=true")
    try:
        hits = _http_json(url)
    except Exception as exc:
        warnings.append("VocaDB 조회 실패: %s" % exc)
        return None
    items = hits.get("items", hits) if isinstance(hits, dict) else hits
    if not isinstance(items, list):
        warnings.append("VocaDB 응답 형식 이상 — 건너뜀")
        return None
    best = None
    for h in items[:5]:
        lyrics = h.get("lyrics") or []
        original = next((l for l in lyrics
                         if l.get("translationType") == "Original"), None)
        roman = next((l for l in lyrics
                      if "Roman" in str(l.get("translationType"))), None)
        pick = original or roman
        if not pick or not pick.get("value"):
            continue
        score = _sim(title, h.get("name", "")) * (1.0 if original else 0.85)
        if best is None or score > best["score"]:
            best = {"source": "vocadb", "raw": pick["value"],
                    "is_lrc": False, "score": round(score, 3),
                    "title": h.get("name", ""),
                    "artist": h.get("artistString", ""),
                    "romanized": original is None}
    return best


# ---------- 메인 ----------

def _write(out_base, cand, lang, warnings):
    """후보 가사를 정규화해 <out>.txt로 쓴다. LRC 원문이면 타임스탬프를 유지한
    정규화 .lrc도 함께 쓰고 path는 그쪽을 가리킨다 — phoneme_extract의
    줄 단위 시간 창 정렬이 활성화된다. 원문 .lrc는 한자/IPA 미변환이라
    정렬 입력으로 쓸 수 없어 정규화본으로 대체한다."""
    raw = cand["raw"]
    if cand["is_lrc"]:
        lines, offset = parse_lrc(raw)
        plain = lrc_plain(lines)
        # 로마자 여부는 본문 전체로 한 번 판별하고 줄 단위 변환에 같은 방식을 쓴다.
        alpha = sum(1 for c in plain if c.isascii() and c.isalpha())
        romanized = lang == "ja" and alpha / max(1, len(plain.replace(" ", ""))) > 0.7

        ja_dropped = [0]

        def norm_line(body):
            if lang == "ja":
                if romanized:
                    return romaji_to_kana(body)
                lw = []
                out = normalize_ja(body, lw)
                # 폴백(가나만 남김) 경로에서만 이 줄의 한자가 제거된 것이다.
                if lw:
                    ja_dropped[0] += sum(
                        1 for c in body if "一" <= c <= "鿿")
                return out
            return normalize(body, lang, warnings)

        if romanized:
            warnings.append("로마자 가사를 규칙 변환으로 가나화했다(검토 권장)")
        norm_lines = [(sec, norm_line(t).strip()) for sec, t in lines]
        if ja_dropped[0]:
            warnings.append(
                "한자 %d자 제거됨 — fugashi+unidic-lite 설치 시 독서 변환으로 정확도↑"
                % ja_dropped[0])
        if not any(t for _, t in norm_lines):
            return None
        lrc_path = out_base + ".lrc"
        with open(lrc_path, "w", encoding="utf-8") as f:
            if offset:
                f.write("[offset:%d]\n" % int(offset * 1000))
            for sec, t in norm_lines:
                if sec is None:
                    f.write(t + "\n")
                else:
                    m, s = divmod(sec, 60)
                    f.write("[%02d:%05.2f]%s\n" % (int(m), s, t))
        txt_path = out_base + ".txt"
        with open(txt_path, "w", encoding="utf-8") as f:
            f.write("\n".join(t for _, t in norm_lines).strip() + "\n")
        cand["timedLines"] = sum(1 for s, _ in norm_lines if s is not None)
        cand["path"] = lrc_path if cand["timedLines"] else txt_path
        cand["lrcPath"] = lrc_path
        cand["preview"] = " / ".join(
            t for _, t in norm_lines if t)[:160]
        return cand
    norm = normalize(raw, lang, warnings)
    if not norm:
        return None
    txt_path = out_base + ".txt"
    with open(txt_path, "w", encoding="utf-8") as f:
        f.write(norm + "\n")
    cand["path"] = txt_path
    cand["lrcPath"] = None
    cand["timedLines"] = 0
    cand["preview"] = " / ".join(
        x.strip() for x in norm.splitlines()[:3] if x.strip())[:160]
    return cand


def _meta_from_name(audio):
    """'artist - title' 파일명 관례 파싱."""
    stem = os.path.splitext(os.path.basename(audio))[0]
    if " - " in stem:
        a, t = stem.split(" - ", 1)
        return t.strip(), a.strip()
    return stem.strip(), ""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--audio", required=True, help="원곡(메타데이터·동명 파일 탐색용)")
    ap.add_argument("--out", required=True, help="출력 기본 경로(확장자 제외)")
    ap.add_argument("--title", default=None)
    ap.add_argument("--artist", default=None)
    ap.add_argument("--lang", default="ja", choices=["ja", "ko", "en"])
    ap.add_argument("--json", default=None, help="결과 JSON 출력 경로")
    ap.add_argument("--offline", action="store_true", help="네트워크 소스 생략")
    args = ap.parse_args()

    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass

    warnings = []
    result = {"ok": False, "candidates": [], "error": None}

    title, artist = args.title, args.artist
    if not title:
        title, fa = _meta_from_name(args.audio)
        artist = artist or fa

    found = from_local(args.audio, warnings) or from_tags(args.audio, warnings)
    if found is None and not args.offline:
        duration = _duration(args.audio, warnings)
        found = from_lrclib(title, artist, duration, warnings)
        # 보컬로이드/우타이테 계열은 VocaDB 커버리지가 더 높다 — 일본어면 견줘본다.
        if (found is None or found["score"] < 0.5) and args.lang == "ja":
            vb = from_vocadb(title, warnings)
            if vb and (found is None or vb["score"] > found["score"]):
                found = vb

    if found is not None:
        cand = _write(args.out, found, args.lang, warnings)
        if cand:
            result["ok"] = True
            result["candidates"].append(cand)
        else:
            result["error"] = "정규화 결과가 비어 채택 불가"
            warnings.append(result["error"])
    else:
        result["error"] = "가사를 찾지 못했습니다(로컬/태그/LRCLIB/VocaDB 모두 실패)"
    result["warnings"] = warnings
    result["title"] = title
    result["artist"] = artist

    line = json.dumps(result, ensure_ascii=False)
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            f.write(line)
    print("RESULT_JSON:" + line, flush=True)
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
