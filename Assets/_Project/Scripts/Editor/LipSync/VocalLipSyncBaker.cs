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
            try
            {
                var analyzer = go.AddComponent<uLipSync.uLipSync>();
                var data = ScriptableObject.CreateInstance<uLipSync.BakedData>();
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
                return data;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
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
            float releaseDamp = 0f)
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
                return BakeClipFromBindings(data, bindings, minVolumeGate, releaseDamp, warnings);
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
            return BakeClipFromBindings(data, bindings, minVolumeGate, releaseDamp, warnings);
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
            float releaseDamp = 0f)
        {
            VRMBlendShapeProxy proxy = FindProxy(characterRoot);
            if (proxy != null)
            {
                return BakeClip(data, proxy, minVolumeGate, warnings, releaseDamp);
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
            return BakeClipFromBindings(data, bindings, minVolumeGate, releaseDamp, warnings);
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
            IList<string> warnings = null)
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
            int curveCount = 0;
            float maxValue = 0f;
            foreach (ResolvedBinding b in bindings)
            {
                var curve = BuildCurve(data, b.phoneme, b.weight, minVolumeGate, releaseDamp);
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

        /// <summary>
        /// 한 음소의 시간축 커브를 만든다. 값 = 음소비율 × 정규화 음량 × 바인딩 가중치(0~100).
        /// 게이트~2×게이트 구간은 소프트 니로 선형 감쇠해 경계 근처 노이즈 블립을 억제한다.
        /// releaseDamp(0~0.95)가 0보다 크면 하강을 지수 감쇠해 잡음성 떨림과 급격한 입 닫힘을 완화한다.
        /// 어택은 즉시 추종해 발음 타이밍을 보존한다.
        /// </summary>
        private static AnimationCurve BuildCurve(
            uLipSync.BakedData data, string phoneme, float bindingWeight,
            float minVolumeGate, float releaseDamp)
        {
            var keys = new List<Keyframe>();
            float dt = 1f / BakeFrameRate;
            float kneeEnd = Mathf.Min(minVolumeGate * 2f, 1f);
            float kneeRange = Mathf.Max(kneeEnd - minVolumeGate, 1e-6f);
            // 1.0은 영구 피크홀드라 입이 안 닫힌다 — 문서 계약(0~0.95)에 맞게 상한 클램프.
            float damp = Mathf.Clamp(releaseDamp, 0f, 0.95f);
            float smoothed = 0f;
            for (int i = 0; i < data.frames.Count; i++)
            {
                var info = uLipSync.BakedData.GetLipSyncInfo(data.frames[i]);
                float volume = info.volume;
                if (volume <= minVolumeGate)
                {
                    volume = 0f;
                }
                else if (volume < kneeEnd)
                {
                    volume *= (volume - minVolumeGate) / kneeRange; // 소프트 니 감쇠
                }
                float ratio = 0f;
                if (info.phonemeRatios != null)
                {
                    info.phonemeRatios.TryGetValue(phoneme, out ratio);
                }
                float raw = ratio * volume * bindingWeight;
                smoothed = Mathf.Max(raw, smoothed * damp);
                keys.Add(new Keyframe(i * dt, smoothed));
            }
            return new AnimationCurve(keys.ToArray());
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
