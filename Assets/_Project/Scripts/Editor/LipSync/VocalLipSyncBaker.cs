using System;
using System.Collections.Generic;
using UnityEngine;
using VRM;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 보컬 AudioClip을 uLipSync로 60fps 분석해 BakedData를 만들고,
    /// VRM(BlendShapeAvatar) 또는 비VRM(SMR 모음 모프 자동 스캔) 바인딩을 해석해 AnimationClip으로 굽는다.
    /// 굽힌 클립은 Face SMR의 blendShape.* 커브라서 녹화/프리뷰 경로와 바로 호환된다.
    /// </summary>
    public static class VocalLipSyncBaker
    {
        public const int BakeFrameRate = 60;

        /// <summary>uLipSync 음소명 → VRM 모음 프리셋. N/무음은 입 닫힘으로 매핑하지 않는다.</summary>
        public static readonly IReadOnlyDictionary<string, BlendShapePreset> PhonemeToPreset =
            new Dictionary<string, BlendShapePreset>(StringComparer.OrdinalIgnoreCase)
            {
                { "A", BlendShapePreset.A },
                { "I", BlendShapePreset.I },
                { "U", BlendShapePreset.U },
                { "E", BlendShapePreset.E },
                { "O", BlendShapePreset.O },
            };

        /// <summary>음성 분석만 수행해 BakedData(비저장 인스턴스)를 돌려준다.</summary>
        public static uLipSync.BakedData BakeAnalysis(AudioClip clip, uLipSync.Profile profile)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile), "uLipSync 프로필이 필요합니다.");
            }

            var go = new GameObject("~LipSyncBake");
            uLipSync.BakedData data = null;
            try
            {
                var analyzer = go.AddComponent<uLipSync.uLipSync>();
                data = ScriptableObject.CreateInstance<uLipSync.BakedData>();
                data.profile = profile;
                data.audioClip = clip;
                data.bakedProfile = profile;
                data.bakedAudioClip = clip;
                data.duration = clip.length;

                int samplesPerFrame = clip.frequency / BakeFrameRate * clip.channels;
                if (samplesPerFrame <= 0)
                {
                    throw new InvalidOperationException("AudioClip 샘플 레이트가 올바르지 않습니다.");
                }
                var buffer = new float[clip.samples * clip.channels];
                var temp = new float[samplesPerFrame];
                clip.GetData(buffer, 0);

                analyzer.OnBakeStart(profile);
                for (int offset = 0; offset + samplesPerFrame <= buffer.Length; offset += samplesPerFrame)
                {
                    Array.Copy(buffer, offset, temp, 0, samplesPerFrame);
                    analyzer.OnBakeUpdate(temp, clip.channels);

                    var frame = new uLipSync.BakedFrame
                    {
                        volume = analyzer.result.rawVolume,
                        phonemes = new List<uLipSync.BakedPhonemeRatio>(),
                    };
                    foreach (var kv in analyzer.result.phonemeRatios)
                    {
                        frame.phonemes.Add(new uLipSync.BakedPhonemeRatio
                        {
                            phoneme = kv.Key,
                            ratio = kv.Value,
                        });
                    }
                    data.frames.Add(frame);
                }
                analyzer.OnBakeEnd();
                var done = data;
                data = null; // 소유권을 호출자에게 이전 — 반환값은 파괴하지 않는다
                return done;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                // 분석 도중 예외로 빠진 비저장 인스턴스 누수를 막는다.
                if (data != null)
                {
                    UnityEngine.Object.DestroyImmediate(data);
                }
            }
        }

        /// <summary>해석된 단일 바인딩 — SMR 상대 경로, 모프명, 음소, 가중치.</summary>
        public struct ResolvedBinding
        {
            public string relativePath;
            public string shapeName;
            public string phoneme;
            public float weight;
        }

        /// <summary>
        /// BakedData를 VRM BlendShapeAvatar 바인딩으로 해석해 AnimationClip을 만든다.
        /// curve 키는 "blendShape.&lt;SMR 블렌드셰이프명&gt;", 경로는 클립의 RelativePath 기준.
        /// minVolumeGate 미만 정규화 음량은 무음(0)으로 처리해 잡음 구간의 입 벌림을 막는다.
        /// </summary>
        public static AnimationClip BakeClip(
            uLipSync.BakedData data,
            VRMBlendShapeProxy proxy,
            float minVolumeGate = 0.02f,
            IList<string> warnings = null,
            float releaseDamp = 0f,
            float curveEpsilon = 0f,
            BakeTuning? tuning = null)
        {
            if (data == null || !data.isValid)
            {
                throw new ArgumentException("베이크 데이터가 비어 있습니다.", nameof(data));
            }
            if (proxy == null || proxy.BlendShapeAvatar == null)
            {
                throw new ArgumentException("캐릭터에 VRMBlendShapeProxy/BlendShapeAvatar가 없습니다.", nameof(proxy));
            }
            var bindings = new List<ResolvedBinding>();
            if (proxy.BlendShapeAvatar.Clips == null)
            {
                return BakeClipFromBindings(data, bindings, minVolumeGate, releaseDamp,
                    warnings, curveEpsilon, tuning);
            }
            foreach (BlendShapeClip shapeClip in proxy.BlendShapeAvatar.Clips)
            {
                string phoneme = PhonemeForPreset(shapeClip.Preset);
                if (phoneme == null || shapeClip.Values == null)
                {
                    continue;
                }
                foreach (BlendShapeBinding binding in shapeClip.Values)
                {
                    Transform target = proxy.transform.Find(binding.RelativePath);
                    var smr = target != null ? target.GetComponent<SkinnedMeshRenderer>() : null;
                    if (smr == null || smr.sharedMesh == null
                        || binding.Index >= smr.sharedMesh.blendShapeCount)
                    {
                        warnings?.Add($"블렌드셰이프 대상을 찾지 못했습니다: {binding.RelativePath}[{binding.Index}]");
                        continue;
                    }
                    bindings.Add(new ResolvedBinding
                    {
                        relativePath = binding.RelativePath,
                        shapeName = smr.sharedMesh.GetBlendShapeName(binding.Index),
                        phoneme = phoneme,
                        weight = binding.Weight,
                    });
                }
            }
            return BakeClipFromBindings(data, bindings, minVolumeGate, releaseDamp,
                warnings, curveEpsilon, tuning);
        }

        /// <summary>
        /// 캐릭터 루트 기준 베이크 — VRM 프록시가 있으면 아바타 바인딩을 쓰고,
        /// 없으면 MMD식 모음 모프명(あ/い/う/え/お, a/i/u/e/o 꼬리)을 스캔해 자동 바인딩한다.
        /// </summary>
        public static AnimationClip BakeClip(
            uLipSync.BakedData data,
            GameObject characterRoot,
            float minVolumeGate = 0.02f,
            IList<string> warnings = null,
            float releaseDamp = 0f,
            float curveEpsilon = 0f,
            BakeTuning? tuning = null)
        {
            VRMBlendShapeProxy proxy = FindProxy(characterRoot);
            if (proxy != null)
            {
                return BakeClip(data, proxy, minVolumeGate, warnings, releaseDamp,
                    curveEpsilon, tuning);
            }
            var bindings = ResolveVowelBindings(characterRoot);
            if (bindings.Count == 0)
            {
                throw new ArgumentException(
                    "대상에 VRMBlendShapeProxy가 없고 모음 블렌드셰이프(あ/い/う/え/お)도 찾지 못했습니다.",
                    nameof(characterRoot));
            }
            // 부분 커버리지도 결과는 만들되, 누락 음소는 경고로 보고한다.
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (ResolvedBinding b in bindings)
            {
                found.Add(b.phoneme);
            }
            foreach (string p in new[] { "A", "I", "U", "E", "O" })
            {
                if (!found.Contains(p))
                {
                    warnings?.Add($"모음 모프를 찾지 못했습니다: {p}");
                }
            }
            return BakeClipFromBindings(data, bindings, minVolumeGate, releaseDamp,
                warnings, curveEpsilon, tuning);
        }

        /// <summary>
        /// 비VRM 모델용 모음 바인딩 스캔. SMR 모프명에서 꼬리 모음 문자를 찾는다.
        /// "88.xあ"·"あ" 같은 MMD 명명과 "mouth_a"/"A" 같은 영문 명명을 인식한다.
        /// 각 음소당 첫 매치만 쓰고, 변형(あ２ 등 숫자 꼬리)은 끝 문자가 모음이 아니라 자연 배제된다.
        /// </summary>
        public static List<ResolvedBinding> ResolveVowelBindings(GameObject characterRoot)
        {
            var result = new List<ResolvedBinding>();
            if (characterRoot == null)
            {
                return result;
            }
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var smr in characterRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null)
                {
                    continue;
                }
                string path = GetRelativePath(smr.transform, characterRoot.transform);
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    string phoneme = PhonemeForShapeName(smr.sharedMesh.GetBlendShapeName(i));
                    if (phoneme == null || !found.Add(phoneme))
                    {
                        continue; // 음소당 첫 매치만
                    }
                    result.Add(new ResolvedBinding
                    {
                        relativePath = path,
                        shapeName = smr.sharedMesh.GetBlendShapeName(i),
                        phoneme = phoneme,
                        weight = 100f,
                    });
                }
            }
            return result;
        }

        /// <summary>모프명 끝 문자를 음소로 해석한다. 선행 숫자/점 접두는 제거.
        /// 일본어 모음은 "xあ"형 접두(x·_·공백만)까지만 인정해 "笑い" 같은 표정 모프를 배제한다.
        /// 영문은 단독 글자 또는 _/x 접두만 인정해 "switch A"·"meta" 오매치를 막는다.</summary>
        internal static string PhonemeForShapeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            // "88.xあ" → "xあ": 선행 숫자와 점을 벗겨 변형 접두만 남긴다.
            int s = 0;
            while (s < name.Length && (char.IsDigit(name[s]) || name[s] == '.'))
            {
                s++;
            }
            string stripped = name.Substring(s);
            if (stripped.Length == 0)
            {
                return null;
            }
            char last = stripped[stripped.Length - 1];
            string jp;
            switch (last)
            {
                case 'あ': jp = "A"; break;
                case 'い': jp = "I"; break;
                case 'う': jp = "U"; break;
                case 'え': jp = "E"; break;
                case 'お': jp = "O"; break;
                default: jp = null; break;
            }
            if (jp != null)
            {
                // 모음 단독이거나 앞부분이 x/_/공백 접두일 때만(笑い·なごみ 등 배제).
                string prefix = stripped.Substring(0, stripped.Length - 1);
                bool ok = true;
                foreach (char c in prefix)
                {
                    if (c != 'x' && c != 'X' && c != '_' && c != ' ')
                    {
                        ok = false;
                        break;
                    }
                }
                return ok ? jp : null;
            }
            char lower = char.ToLowerInvariant(last);
            string ph = lower == 'a' ? "A" : lower == 'i' ? "I" : lower == 'u' ? "U"
                : lower == 'e' ? "E" : lower == 'o' ? "O" : null;
            if (ph != null && (stripped.Length == 1
                || stripped[stripped.Length - 2] == '_'
                || stripped[stripped.Length - 2] == 'x'
                || stripped[stripped.Length - 2] == 'X'))
            {
                return ph;
            }
            return null;
        }

        private static string GetRelativePath(Transform target, Transform root)
        {
            var parts = new List<string>();
            for (Transform t = target; t != null && t != root; t = t.parent)
            {
                parts.Insert(0, t.name);
            }
            return string.Join("/", parts);
        }

        /// <summary>해석된 바인딩 목록으로 AnimationClip을 만든다(공통 코어).</summary>
        private static AnimationClip BakeClipFromBindings(
            uLipSync.BakedData data,
            List<ResolvedBinding> bindings,
            float minVolumeGate,
            float releaseDamp,
            IList<string> warnings = null,
            float curveEpsilon = 0f,
            BakeTuning? tuning = null)
        {
            if (data == null || !data.isValid)
            {
                throw new ArgumentException("베이크 데이터가 비어 있습니다.", nameof(data));
            }
            var clip = new AnimationClip
            {
                frameRate = BakeFrameRate,
                legacy = false,
            };
            try
            {
                var t = tuning ?? BakeTuning.Legacy;
                // 게이트 상태는 모든 음소 커브가 공유해야 하므로 음량 열을 먼저 계산한다.
                float[] vols = BuildGatedVolumes(data, minVolumeGate,
                    t.autoVolumeNorm, t.volumeGamma);
                float[] openVols = vols;
                if (t.channelSmoothing)
                {
                    // 개구 채널은 전 커브 공유 — 비대칭 원폴로 1프레임 스냅을 제거한다.
                    openVols = SmoothChannel(vols, t.openMs, t.closeMs);
                }
                // 모음 유지 옵션용 — 어느 음소든 비율이 있는 프레임을 미리 표시한다.
                bool[] vowelFrames = null;
                if (t.holdVowelWhileVoiced)
                {
                    vowelFrames = new bool[data.frames.Count];
                    for (int i = 0; i < data.frames.Count; i++)
                    {
                        if (data.frames[i].phonemes == null)
                        {
                            continue;
                        }
                        var info = uLipSync.BakedData.GetLipSyncInfo(data.frames[i]);
                        foreach (var kv in info.phonemeRatios)
                        {
                            if (kv.Value > 1e-4f)
                            {
                                vowelFrames[i] = true;
                                break;
                            }
                        }
                    }
                }
                int curveCount = 0;
                float maxValue = 0f;
                foreach (ResolvedBinding b in bindings)
                {
                    // 무음 해제 판정에는 스무딩 전 게이트 출력을 넘긴다 — 원폴 꼬리는
                    // 수학적으로 0에 도달하지 않아 스무딩 채널로는 무음을 검출할 수 없다.
                    var curve = BuildCurve(data, openVols, b.phoneme, b.weight, releaseDamp,
                        t.channelSmoothing ? t.transitionMs : -1f, vowelFrames, vols);
                    curve = SimplifyCurve(curve, curveEpsilon);
                    if (curve.length == 0)
                    {
                        continue;
                    }
                    clip.SetCurve(b.relativePath, typeof(SkinnedMeshRenderer),
                        "blendShape." + b.shapeName, curve);
                    curveCount++;
                    foreach (Keyframe k in curve.keys)
                    {
                        maxValue = Mathf.Max(maxValue, Mathf.Abs(k.value));
                    }
                }
                // 키는 있는데 값이 전부 0인 "빈 베이크"는 재생해도 입이 안 움직이므로 원인 추적 전에 알린다.
                if (curveCount == 0)
                {
                    warnings?.Add("베이크 결과 애니메이션 커브가 하나도 없습니다. 블렌드셰이프 바인딩을 확인하세요.");
                }
                else if (maxValue < 0.01f)
                {
                    warnings?.Add("베이크 결과 모든 모프 가중치가 0입니다. 입력이 무음이거나 음소 분석이 비어 있을 수 있습니다.");
                }
                return clip;
            }
            catch
            {
                // 예외로 빠지면 미저장 클립이 누수된다(호출부는 vocal/data만 파괴).
                UnityEngine.Object.DestroyImmediate(clip);
                throw;
            }
        }

        /// <summary>베이크 튜닝 — 음량 정규화와 채널 스무딩 파라미터 묶음.
        /// channelSmoothing이 켜지면 레거시 releaseDamp 대신 개구/전이 원폴 스무딩을 쓴다.</summary>
        public struct BakeTuning
        {
            /// <summary>true면 클립 자체 p95를 완전 개구 기준으로 음량을 정규화한다.</summary>
            public bool autoVolumeNorm;
            /// <summary>정규화 후 적용할 지수(1=끔, &lt;1이면 조용한 발성 구간을 부양).</summary>
            public float volumeGamma;
            /// <summary>입 열림 시상수(ms). 채널 스무딩 시에만 사용.</summary>
            public float openMs;
            /// <summary>입 닫힘 시상수(ms). 채널 스무딩 시에만 사용.</summary>
            public float closeMs;
            /// <summary>모음 전이 시상수(ms). 채널 스무딩 시에만 사용.</summary>
            public float transitionMs;
            /// <summary>true면 채널 분리 스무딩 경로(개구/전이)를 사용한다.</summary>
            public bool channelSmoothing;
            /// <summary>true면 발성 중 무판정(어느 음소 비율도 없음) 구간에서 마지막 유성 모음을
            /// 유지한다 — wav2vec2 원핫이 1프레임 펄스로 나오는 데이터에서 입이
            /// 모음 사이마다 닫히는 파닥거림을 막는다. 무음(게이트 후 음량 0)이면 해제.</summary>
            public bool holdVowelWhileVoiced;

            /// <summary>기존 동작 그대로 — 정규화·스무딩 모두 끔.</summary>
            public static readonly BakeTuning Legacy = new BakeTuning { volumeGamma = 1f };
        }

        /// <summary>
        /// 슈미트 트리거 히스테리시스로 음량 열을 게이트한다 — 열림(>gate)과
        /// 닫힘(<gate×0.5) 임계를 분리해 경계 음량에서 on/off가 진동하지 않는다.
        /// 열림 상태에서 closeGate~kneeEnd(=2×gate) 구간은 닫힘 임계부터 원본 크기까지
        /// 단조 증가하는 단일 램프로 감쇠한다(게이트 경계에서 출력이 튀지 않게).
        /// 반환 배열은 모든 음소 커브가 공유하는 게이트 후 음량이다.
        /// autoVolumeNorm이면 게이트 전에 클립 p95 기준으로 정규화해 곡별 레벨 차를 없앤다.
        /// </summary>
        internal static float[] BuildGatedVolumes(uLipSync.BakedData data,
            float minVolumeGate, bool autoVolumeNorm = false, float volumeGamma = 1f)
        {
            var vols = new float[data.frames.Count];
            for (int i = 0; i < data.frames.Count; i++)
            {
                var frame = data.frames[i];
                // 역직렬화/외부 생성 데이터는 phonemes가 null일 수 있다 — 음량만 필요하므로
                // 직접 읽고 null이면 무음으로 본다.
                float v = frame.volume;
                if (frame.phonemes != null)
                {
                    v = uLipSync.BakedData.GetLipSyncInfo(frame).volume;
                }
                if (float.IsNaN(v) || float.IsInfinity(v))
                {
                    v = 0f;
                }
                vols[i] = v;
            }
            if (autoVolumeNorm)
            {
                NormalizeVolumes(vols, volumeGamma);
            }
            float closeGate = minVolumeGate * 0.5f;
            float kneeEnd = Mathf.Min(minVolumeGate * 2f, 1f);
            float rampRange = Mathf.Max(kneeEnd - closeGate, 1e-6f);
            bool open = false;
            for (int i = 0; i < data.frames.Count; i++)
            {
                float v = vols[i];
                if (open)
                {
                    if (v < closeGate)
                    {
                        open = false;
                    }
                }
                else if (v > minVolumeGate)
                {
                    open = true;
                }
                if (!open)
                {
                    vols[i] = 0f;
                    continue;
                }
                if (v < kneeEnd)
                {
                    // 소프트 니+히스테리시스 대역을 하나의 단조 램프로 —
                    // closeGate에서 0, kneeEnd에서 원본 크기(v×1)에 도달한다.
                    v *= Mathf.Max(0f, v - closeGate) / rampRange;
                }
                vols[i] = v;
            }
            return vols;
        }

        /// <summary>
        /// 클립 자체의 p95 음량을 완전 개구 기준으로 정규화한다 — 고정 캘리브레이션
        /// 구간(log10 -2.5~-1.5) 아래로 눌린 조용한 곡·구간도 상대 크기로 열리게 한다.
        /// volumeGamma&lt;1이면 정규화 후 지수를 걸어 조용한 발성 구간을 추가로 부양한다
        /// (VAD 무음=0은 지수를 걸어도 0이라 닫힘이 유지된다).
        /// </summary>
        internal static void NormalizeVolumes(float[] vols, float volumeGamma)
        {
            var positive = new List<float>(vols.Length);
            foreach (float v in vols)
            {
                if (v > 0f)
                {
                    positive.Add(v);
                }
            }
            if (positive.Count == 0)
            {
                return;
            }
            positive.Sort();
            float refVol = positive[Mathf.Min(positive.Count - 1,
                Mathf.FloorToInt(positive.Count * 0.95f))];
            if (refVol <= 0f)
            {
                return;
            }
            float gamma = Mathf.Max(volumeGamma, 0.05f);
            for (int i = 0; i < vols.Length; i++)
            {
                float n = Mathf.Clamp01(vols[i] / refVol);
                vols[i] = Mathf.Approximately(gamma, 1f) ? n : Mathf.Pow(n, gamma);
            }
        }

        /// <summary>
        /// 비대칭 원폴 필터 — 목표가 오르면 openMs, 내리면 closeMs 시상수로 추종한다.
        /// 사람 턱의 개폐 속도(열림 ~50ms·닫힘 ~130ms)에 맞춰 프레임 스냅을 제거한다.
        /// 모든 음소 커브가 공유하는 개구 채널이므로 커브 생성 전에 한 번 적용한다.
        /// </summary>
        internal static float[] SmoothChannel(float[] src, float openMs, float closeMs)
        {
            var dst = new float[src.Length];
            float aOpen = AlphaFor(openMs);
            float aClose = AlphaFor(closeMs);
            float cur = 0f;
            for (int i = 0; i < src.Length; i++)
            {
                float a = src[i] > cur ? aOpen : aClose;
                cur += (src[i] - cur) * a;
                dst[i] = cur;
            }
            return dst;
        }

        /// <summary>시상수(ms)를 60fps 프레임 단계 계수로 환산한다. 0이면 즉시 추종.</summary>
        private static float AlphaFor(float tauMs)
        {
            if (tauMs <= 0f)
            {
                return 1f;
            }
            return 1f - Mathf.Exp(-1f / (BakeFrameRate * (tauMs / 1000f)));
        }

        /// <summary>
        /// 한 음소의 시간축 커브를 만든다. 값 = 음소비율 × 게이트 후 음량 × 바인딩 가중치(0~100).
        /// transitionMs&gt;=0이면 채널 스무딩 경로 — gatedVols는 이미 개구 스무딩이 끝난 공유 채널이고
        /// 여기서는 모음 비율에만 원폴 스무딩을 걸어 전이를 사람 발음 속도로 제한한다
        /// (레거시 releaseDamp는 채널 스무딩 경로에서 적용하지 않는다 — 이중 감쇠 방지).
        /// transitionMs&lt;0이면 레거시: releaseDamp(0~0.95)가 하강만 지수 감쇠하고 어택은 즉시 추종한다.
        /// vowelFrames!=null이면 모음 유지 — 프레임에 어느 음소 비율도 없고 게이트 후 음량이
        /// 있으면(발성 중 무판정) 마지막 유성 형태(ratio>0이던 모음 전체)를 유지한다.
        /// 무음이면 유지를 해제한다 — 무음 판정은 silenceVols(스무딩 전 게이트 출력)로 하며,
        /// null이면 gatedVols로 대체한다. 개구 스무딩 채널은 원폴 꼬리가 0에 수렴할 뿐
        /// 도달하지 않으므로 그 값으로 무음을 검출할 수 없다.
        /// </summary>
        private static AnimationCurve BuildCurve(
            uLipSync.BakedData data, float[] gatedVols, string phoneme,
            float bindingWeight, float releaseDamp, float transitionMs = -1f,
            bool[] vowelFrames = null, float[] silenceVols = null)
        {
            var keys = new List<Keyframe>();
            float dt = 1f / BakeFrameRate;
            // 1.0은 영구 피크홀드라 입이 안 닫힌다 — 문서 계약(0~0.95)에 맞게 상한 클램프.
            float damp = Mathf.Clamp(releaseDamp, 0f, 0.95f);
            float alphaT = AlphaFor(transitionMs);
            float smoothed = 0f;
            float sratio = 0f;
            float[] sil = silenceVols ?? gatedVols;
            // 모음 유지 상태 — 이 커브의 모음이 마지막 판정 프레임에서 비율이 있었을 때만 유지 대상.
            bool held = false;
            float heldRatio = 0f;
            for (int i = 0; i < data.frames.Count; i++)
            {
                // phonemes가 null인 프레임(외부/역직렬화 데이터)은 음소비율 0으로 본다.
                float ratio = 0f;
                if (data.frames[i].phonemes != null)
                {
                    var info = uLipSync.BakedData.GetLipSyncInfo(data.frames[i]);
                    info.phonemeRatios?.TryGetValue(phoneme, out ratio);
                }
                if (vowelFrames != null)
                {
                    if (vowelFrames[i])
                    {
                        // 새 판정 프레임 — 이 모음의 비율이 있으면 유지 대상이 된다.
                        held = ratio > 1e-4f;
                        heldRatio = ratio;
                    }
                    else if (sil[i] <= 0f)
                    {
                        held = false; // 무음 구간을 넘긴 형태는 유지하지 않는다.
                    }
                    else if (held)
                    {
                        ratio = heldRatio;
                    }
                }
                if (transitionMs >= 0f)
                {
                    sratio += (ratio - sratio) * alphaT;
                    keys.Add(new Keyframe(i * dt, sratio * gatedVols[i] * bindingWeight));
                }
                else
                {
                    float raw = ratio * gatedVols[i] * bindingWeight;
                    smoothed = Mathf.Max(raw, smoothed * damp);
                    keys.Add(new Keyframe(i * dt, smoothed));
                }
            }
            return new AnimationCurve(keys.ToArray());
        }

        /// <summary>
        /// Ramer–Douglas–Peucker 계열 키프레임 간소화 — 양 끝을 잇는 선분에서
        /// 수직 오차가 epsilon을 넘는 최대 편차 키만 재귀적으로 남긴다.
        /// 값 단위는 모프 가중치(0~100). epsilon 0이면 원본을 그대로 돌려준다.
        /// </summary>
        internal static AnimationCurve SimplifyCurve(AnimationCurve curve,
            float epsilon)
        {
            if (curve == null || epsilon <= 0f || curve.length <= 2)
            {
                return curve;
            }
            var keys = curve.keys;
            int n = keys.Length;
            var keep = new bool[n];
            keep[0] = keep[n - 1] = true;
            var stack = new Stack<int>();
            stack.Push(0);
            stack.Push(n - 1);
            while (stack.Count > 0)
            {
                int b = stack.Pop();
                int a = stack.Pop();
                if (b <= a + 1)
                {
                    continue;
                }
                float ta = keys[a].time, va = keys[a].value;
                float tb = keys[b].time, vb = keys[b].value;
                float span = Mathf.Max(tb - ta, 1e-9f);
                int maxI = -1;
                float maxErr = epsilon;
                for (int i = a + 1; i < b; i++)
                {
                    float t = (keys[i].time - ta) / span;
                    float chord = va + (vb - va) * t;
                    float err = Mathf.Abs(keys[i].value - chord);
                    if (err > maxErr)
                    {
                        maxErr = err;
                        maxI = i;
                    }
                }
                if (maxI < 0)
                {
                    continue;
                }
                keep[maxI] = true;
                stack.Push(a);
                stack.Push(maxI);
                stack.Push(maxI);
                stack.Push(b);
            }
            var outKeys = new List<Keyframe>(n);
            for (int i = 0; i < n; i++)
            {
                if (keep[i])
                {
                    outKeys.Add(keys[i]);
                }
            }
            // RDP 오차 상한은 선형 보간 기준 — 자동 스무딩 탄젠트면 평가값이
            // 선분을 벗어나므로 남은 키 사이 기울기로 탄젠트를 고정한다.
            for (int i = 0; i < outKeys.Count - 1; i++)
            {
                var a = outKeys[i];
                var c = outKeys[i + 1];
                float slope = (c.value - a.value)
                    / Mathf.Max(c.time - a.time, 1e-9f);
                a.outTangent = slope;
                c.inTangent = slope;
                outKeys[i] = a;
                outKeys[i + 1] = c;
            }
            var result = new AnimationCurve(outKeys.ToArray());
            result.preWrapMode = curve.preWrapMode;
            result.postWrapMode = curve.postWrapMode;
            return result;
        }

        /// <summary>VRM 프리셋에 대응하는 uLipSync 음소명을 돌려준다(역방향 맵).</summary>
        private static string PhonemeForPreset(BlendShapePreset preset)
        {
            switch (preset)
            {
                case BlendShapePreset.A: return "A";
                case BlendShapePreset.I: return "I";
                case BlendShapePreset.U: return "U";
                case BlendShapePreset.E: return "E";
                case BlendShapePreset.O: return "O";
                default: return null;
            }
        }

        /// <summary>캐릭터 루트에서 BlendShapeProxy를 찾는다(없으면 null).</summary>
        public static VRMBlendShapeProxy FindProxy(GameObject characterRoot)
        {
            return characterRoot != null
                ? characterRoot.GetComponentInChildren<VRMBlendShapeProxy>(true)
                : null;
        }
    }
}
