using System;
using System.Collections.Generic;
using UnityEngine;
using VRM;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 보컬 AudioClip을 uLipSync로 60fps 분석해 BakedData를 만들고,
    /// VRM BlendShapeAvatar의 모음 클립 바인딩을 해석해 AnimationClip으로 굽는다.
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

        /// <summary>
        /// BakedData를 VRM BlendShapeAvatar 바인딩으로 해석해 AnimationClip을 만든다.
        /// curve 키는 "blendShape.&lt;SMR 블렌드셰이프명&gt;", 경로는 클립의 RelativePath 기준.
        /// minVolumeGate 미만 정규화 음량은 무음(0)으로 처리해 잡음 구간의 입 벌림을 막는다.
        /// </summary>
        public static AnimationClip BakeClip(
            uLipSync.BakedData data,
            VRMBlendShapeProxy proxy,
            float minVolumeGate = 0.02f,
            IList<string> warnings = null)
        {
            if (data == null || !data.isValid)
            {
                throw new ArgumentException("베이크 데이터가 비어 있습니다.", nameof(data));
            }
            if (proxy == null || proxy.BlendShapeAvatar == null)
            {
                throw new ArgumentException("캐릭터에 VRMBlendShapeProxy/BlendShapeAvatar가 없습니다.", nameof(proxy));
            }

            var clip = new AnimationClip
            {
                frameRate = BakeFrameRate,
                legacy = false,
            };

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
                    string shapeName = smr.sharedMesh.GetBlendShapeName(binding.Index);
                    var curve = BuildCurve(data, phoneme, binding.Weight, minVolumeGate);
                    if (curve.length == 0)
                    {
                        continue;
                    }
                    clip.SetCurve(binding.RelativePath, typeof(SkinnedMeshRenderer),
                        "blendShape." + shapeName, curve);
                }
            }
            return clip;
        }

        /// <summary>한 음소의 시간축 커브를 만든다. 값 = 음소비율 × 정규화 음량 × 바인딩 가중치(0~100).</summary>
        private static AnimationCurve BuildCurve(
            uLipSync.BakedData data, string phoneme, float bindingWeight, float minVolumeGate)
        {
            var keys = new List<Keyframe>();
            float dt = 1f / BakeFrameRate;
            for (int i = 0; i < data.frames.Count; i++)
            {
                var info = uLipSync.BakedData.GetLipSyncInfo(data.frames[i]);
                float volume = info.volume >= minVolumeGate ? info.volume : 0f;
                float ratio = 0f;
                if (info.phonemeRatios != null)
                {
                    info.phonemeRatios.TryGetValue(phoneme, out ratio);
                }
                float value = ratio * volume * bindingWeight;
                keys.Add(new Keyframe(i * dt, value));
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
