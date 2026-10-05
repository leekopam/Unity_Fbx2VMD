using System.Collections.Generic;
using System.IO;
using Fbx2Vmd.LipSync;
using NUnit.Framework;
using UnityEngine;
using VRM;

namespace Fbx2Vmd.Tests.LipSync
{
    /// <summary>
    /// 분리 래퍼의 인자 생성/출력 해석, WAV 파서, 립싱크 커브 생성을 단위 테스트한다.
    /// 실제 python/모델 없이 결정적 검증만 수행한다.
    /// </summary>
    public class VocalLipSyncTests
    {
        private readonly List<Object> _cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _cleanup)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }
            _cleanup.Clear();
        }

        // ---------- 분리 인자/출력 해석 ----------

        [Test]
        public void BuildArguments_AudioSeparator_모델과출력폴더포함()
        {
            string args = VocalStemSeparator.BuildArguments(
                VocalStemSeparator.Engine.AudioSeparator,
                "C:\\음악\\song a.mp3", "D:\\out dir", "model.ckpt");
            StringAssert.Contains("-m audio_separator", args);
            StringAssert.Contains("\"C:\\음악\\song a.mp3\"", args);
            StringAssert.Contains("--output_dir \"D:\\out dir\"", args);
            StringAssert.Contains("--model_filename \"model.ckpt\"", args);
        }

        [Test]
        public void BuildArguments_Demucs_보컬전용분리()
        {
            string args = VocalStemSeparator.BuildArguments(
                VocalStemSeparator.Engine.Demucs, "in.mp3", "out", "");
            StringAssert.Contains("-m demucs --two-stems=vocals", args);
            StringAssert.Contains(VocalStemSeparator.DefaultDemucsModel, args);
        }

        [Test]
        public void ResolveOutputs_AudioSeparator_보컬과반주찾기()
        {
            string dir = Path.Combine(Path.GetTempPath(), "lipsync_sep_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                string vocal = Path.Combine(dir, "song_(Vocals)_model_bs_roformer.wav");
                string inst = Path.Combine(dir, "song_(Instrumental)_model_bs_roformer.wav");
                string other = Path.Combine(dir, "song_(Drums)_model.wav");
                File.WriteAllText(vocal, "x");
                File.WriteAllText(inst, "x");
                File.WriteAllText(other, "x");

                VocalStemSeparator.ResolveOutputs(
                    VocalStemSeparator.Engine.AudioSeparator, dir,
                    Path.Combine(dir, "song.mp3"),
                    out string vocalPath, out string instPath);

                Assert.AreEqual(vocal, vocalPath);
                Assert.AreEqual(inst, instPath);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void ResolveOutputs_Demucs_모델하위폴더에서찾기()
        {
            string dir = Path.Combine(Path.GetTempPath(), "lipsync_demucs_" + Path.GetRandomFileName());
            string sub = Path.Combine(dir, "htdemucs_ft", "song");
            Directory.CreateDirectory(sub);
            try
            {
                string vocal = Path.Combine(sub, "vocals.wav");
                string inst = Path.Combine(sub, "no_vocals.wav");
                File.WriteAllText(vocal, "x");
                File.WriteAllText(inst, "x");

                VocalStemSeparator.ResolveOutputs(
                    VocalStemSeparator.Engine.Demucs, dir,
                    Path.Combine(dir, "song.mp3"),
                    out string vocalPath, out string instPath);

                Assert.AreEqual(vocal, vocalPath);
                Assert.AreEqual(inst, instPath);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // ---------- WAV 파서 ----------

        [Test]
        public void WavParse_PCM16_왕복()
        {
            // 1채널 8kHz PCM16 WAV를 직접 만든다.
            var bytes = BuildWavBytes(formatTag: 1, bits: 16, channels: 1,
                freq: 8000, samples: new short[] { 0, 16000, -16000, 32767, -32768 });
            AudioClip clip = WavFileReader.Parse(bytes, "test", out string error);
            _cleanup.Add(clip);

            Assert.IsNotNull(clip, error);
            Assert.AreEqual(8000, clip.frequency);
            Assert.AreEqual(1, clip.channels);
            Assert.AreEqual(5, clip.samples);

            var data = new float[5];
            clip.GetData(data, 0);
            Assert.AreEqual(0f, data[0], 1e-4f);
            Assert.AreEqual(16000f / 32768f, data[1], 1e-4f);
            Assert.AreEqual(-16000f / 32768f, data[2], 1e-4f);
        }

        [Test]
        public void WavParse_비WAV는에러()
        {
            AudioClip clip = WavFileReader.Parse(new byte[] { 1, 2, 3, 4 }, "bad", out string error);
            Assert.IsNull(clip);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void WavParse_PCM24는미지원에러()
        {
            var bytes = BuildWavBytes(formatTag: 1, bits: 24, channels: 1,
                freq: 8000, samples: new short[] { 0 });
            AudioClip clip = WavFileReader.Parse(bytes, "x", out string error);
            Assert.IsNull(clip);
            StringAssert.Contains("지원하지 않는", error);
        }

        // ---------- 커브 생성 ----------

        [Test]
        public void BakeClip_VRM바인딩해석_커브생성()
        {
            // Face SMR + blendShape "mouthA"를 가진 캐릭터와 A 프리셋 클립을 구성한다.
            var root = new GameObject("Root");
            var face = new GameObject("Face");
            face.transform.SetParent(root.transform, false);
            _cleanup.Add(root);

            var mesh = new Mesh { name = "m" };
            mesh.vertices = new[] { Vector3.zero };
            mesh.AddBlendShapeFrame("mouthA", 100f, new Vector3[1], new Vector3[1], new Vector3[1]);
            _cleanup.Add(mesh);
            var smr = face.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;

            var avatar = ScriptableObject.CreateInstance<BlendShapeAvatar>();
            var aClip = ScriptableObject.CreateInstance<BlendShapeClip>();
            aClip.Preset = BlendShapePreset.A;
            aClip.Values = new[]
            {
                new BlendShapeBinding { RelativePath = "Face", Index = 0, Weight = 100f },
            };
            avatar.Clips = new List<BlendShapeClip> { aClip };
            _cleanup.Add(avatar);
            _cleanup.Add(aClip);

            var proxy = root.AddComponent<VRMBlendShapeProxy>();
            proxy.BlendShapeAvatar = avatar;

            // 음소 A 비율 1, 큰 음량의 프레임 2개를 가진 BakedData.
            var data = ScriptableObject.CreateInstance<uLipSync.BakedData>();
            _cleanup.Add(data);
            data.duration = 2f / 60f;
            for (int i = 0; i < 2; i++)
            {
                data.frames.Add(new uLipSync.BakedFrame
                {
                    volume = 0.5f, // 정규화 음량 > 0이 되는 크기
                    phonemes = new List<uLipSync.BakedPhonemeRatio>
                    {
                        new uLipSync.BakedPhonemeRatio { phoneme = "A", ratio = 1f },
                    },
                });
            }

            var warnings = new List<string>();
            AnimationClip clip = VocalLipSyncBaker.BakeClip(data, proxy, 0.02f, warnings);
            _cleanup.Add(clip);

            Assert.IsEmpty(warnings);
            var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
            Assert.AreEqual(1, bindings.Length);
            Assert.AreEqual("Face", bindings[0].path);
            Assert.AreEqual("blendShape.mouthA", bindings[0].propertyName);

            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, bindings[0]);
            Assert.AreEqual(2, curve.length);
            Assert.Greater(curve.keys[0].value, 0f, "음소 A가 음량과 곱해져 0보다 커야 함");
            Assert.LessOrEqual(curve.keys[0].value, 100f);
        }

        [Test]
        public void BakeClip_무음게이트_0처리()
        {
            var (proxy, _) = BuildProxyWithAClip();
            var data = ScriptableObject.CreateInstance<uLipSync.BakedData>();
            _cleanup.Add(data);
            data.duration = 1f / 60f;
            data.frames.Add(new uLipSync.BakedFrame
            {
                volume = 0f, // 무음
                phonemes = new List<uLipSync.BakedPhonemeRatio>
                {
                    new uLipSync.BakedPhonemeRatio { phoneme = "A", ratio = 1f },
                },
            });

            AnimationClip clip = VocalLipSyncBaker.BakeClip(data, proxy, 0.02f);
            _cleanup.Add(clip);
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(
                clip, UnityEditor.AnimationUtility.GetCurveBindings(clip)[0]);
            Assert.AreEqual(0f, curve.keys[0].value, "무음 구간은 입을 닫아야 함");
        }

        [Test]
        public void BakeClip_대상SMR없으면경고()
        {
            var root = new GameObject("Root");
            _cleanup.Add(root);
            var avatar = ScriptableObject.CreateInstance<BlendShapeAvatar>();
            var aClip = ScriptableObject.CreateInstance<BlendShapeClip>();
            aClip.Preset = BlendShapePreset.A;
            aClip.Values = new[]
            {
                new BlendShapeBinding { RelativePath = "NoFace", Index = 0, Weight = 100f },
            };
            avatar.Clips = new List<BlendShapeClip> { aClip };
            _cleanup.Add(avatar);
            _cleanup.Add(aClip);
            var proxy = root.AddComponent<VRMBlendShapeProxy>();
            proxy.BlendShapeAvatar = avatar;

            var data = ScriptableObject.CreateInstance<uLipSync.BakedData>();
            _cleanup.Add(data);
            data.duration = 1f / 60f;
            data.frames.Add(new uLipSync.BakedFrame
            {
                volume = 0.5f,
                phonemes = new List<uLipSync.BakedPhonemeRatio>
                {
                    new uLipSync.BakedPhonemeRatio { phoneme = "A", ratio = 1f },
                },
            });

            var warnings = new List<string>();
            AnimationClip clip = VocalLipSyncBaker.BakeClip(data, proxy, 0.02f, warnings);
            _cleanup.Add(clip);
            Assert.AreEqual(1, warnings.Count, "없는 SMR 경로는 경고로 보고해야 함");
        }

        private (VRMBlendShapeProxy proxy, BlendShapeAvatar avatar) BuildProxyWithAClip()
        {
            var root = new GameObject("Root");
            var face = new GameObject("Face");
            face.transform.SetParent(root.transform, false);
            _cleanup.Add(root);
            var mesh = new Mesh { name = "m" };
            mesh.vertices = new[] { Vector3.zero };
            mesh.AddBlendShapeFrame("mouthA", 100f, new Vector3[1], new Vector3[1], new Vector3[1]);
            _cleanup.Add(mesh);
            face.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;

            var avatar = ScriptableObject.CreateInstance<BlendShapeAvatar>();
            var aClip = ScriptableObject.CreateInstance<BlendShapeClip>();
            aClip.Preset = BlendShapePreset.A;
            aClip.Values = new[]
            {
                new BlendShapeBinding { RelativePath = "Face", Index = 0, Weight = 100f },
            };
            avatar.Clips = new List<BlendShapeClip> { aClip };
            _cleanup.Add(avatar);
            _cleanup.Add(aClip);
            var proxy = root.AddComponent<VRMBlendShapeProxy>();
            proxy.BlendShapeAvatar = avatar;
            return (proxy, avatar);
        }

        private static byte[] BuildWavBytes(int formatTag, int bits, int channels,
            int freq, short[] samples)
        {
            // fmt + data 청크만 가진 최소 WAV를 직렬화한다.
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                int dataSize = samples.Length * (bits / 8) * channels;
                int byteRate = freq * channels * (bits / 8);
                int blockAlign = channels * (bits / 8);

                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataSize);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)formatTag);
                w.Write((short)channels);
                w.Write(freq);
                w.Write(byteRate);
                w.Write((short)blockAlign);
                w.Write((short)bits);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataSize);
                foreach (short s in samples)
                {
                    if (bits == 16)
                    {
                        w.Write(s);
                    }
                    else if (bits == 24)
                    {
                        w.Write((byte)(s & 0xFF));
                        w.Write((byte)((s >> 8) & 0xFF));
                        w.Write((byte)((s >> 4) & 0xFF));
                    }
                }
                return ms.ToArray();
            }
        }
    }
}
