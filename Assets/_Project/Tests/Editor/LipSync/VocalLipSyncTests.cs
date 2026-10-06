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
            StringAssert.Contains("audio_separator.utils.cli", args);
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

        [Test]
        public void ResolveStemOutput_괄호경계매칭()
        {
            string dir = Path.Combine(Path.GetTempPath(), "lipsync_stem_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                // 디리버브 패스: 목표는 _(dry)_, _(No dry)_는 버려야 한다.
                string dry = Path.Combine(dir, "song_(Vocals)_sep_(dry)_dereverb.wav");
                string nodry = Path.Combine(dir, "song_(Vocals)_sep_(No dry)_dereverb.wav");
                File.WriteAllText(dry, "x");
                File.WriteAllText(nodry, "x");
                File.SetLastWriteTimeUtc(dry, System.DateTime.UtcNow);
                File.SetLastWriteTimeUtc(nodry, System.DateTime.UtcNow);

                string got = VocalStemSeparator.ResolveStemOutput(dir,
                    Path.Combine(dir, "song_(Vocals)_sep.wav"), "dry",
                    System.DateTime.UtcNow.AddMinutes(-1));
                Assert.AreEqual(dry, got);

                // 카라오케 패스: 마지막 괄호 그룹만 비교하므로 입력 파일명 속 _(Vocals)_는 무시되고
                // _(Instrumental)_ 산출물은 제외된다. 입력 파일 자체는 mtime을 과거로 돌려 배제.
                File.SetLastWriteTimeUtc(dry, System.DateTime.UtcNow.AddMinutes(-10));
                string lead = Path.Combine(dir, "song_(Vocals)_sep_(dry)_dereverb_(Vocals)_karaoke.wav");
                string back = Path.Combine(dir, "song_(Vocals)_sep_(dry)_dereverb_(Instrumental)_karaoke.wav");
                File.WriteAllText(lead, "x");
                File.WriteAllText(back, "x");
                File.SetLastWriteTimeUtc(lead, System.DateTime.UtcNow);
                File.SetLastWriteTimeUtc(back, System.DateTime.UtcNow);

                got = VocalStemSeparator.ResolveStemOutput(dir, dry, "Vocals",
                    System.DateTime.UtcNow.AddMinutes(-1));
                Assert.AreEqual(lead, got);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void ResolveStemOutput_시간필터로이전패스배제()
        {
            string dir = Path.Combine(Path.GetTempPath(), "lipsync_stem_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                // 이전 패스 산출물(mtime 과거)과 새 산출물이 같은 스템 키워드를 가질 때 새 것을 골라야 한다.
                string oldFile = Path.Combine(dir, "song_(dry)_old.wav");
                string newFile = Path.Combine(dir, "song_(dry)_new.wav");
                File.WriteAllText(oldFile, "x");
                File.WriteAllText(newFile, "x");
                File.SetLastWriteTimeUtc(oldFile, System.DateTime.UtcNow.AddMinutes(-10));
                File.SetLastWriteTimeUtc(newFile, System.DateTime.UtcNow);

                string got = VocalStemSeparator.ResolveStemOutput(dir,
                    Path.Combine(dir, "song.wav"), "dry",
                    System.DateTime.UtcNow.AddMinutes(-1));
                Assert.AreEqual(newFile, got);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void MoveTo_기존파일덮어쓰기()
        {
            string dir = Path.Combine(Path.GetTempPath(), "lipsync_move_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                string src = Path.Combine(dir, "a.wav");
                string dst = Path.Combine(dir, "b.wav");
                File.WriteAllText(src, "new");
                File.WriteAllText(dst, "old");
                Assert.AreEqual(dst, VocalStemSeparator.MoveTo(src, dst));
                Assert.IsFalse(File.Exists(src));
                Assert.AreEqual("new", File.ReadAllText(dst));
                // src == dst면 그대로 유지
                Assert.AreEqual(dst, VocalStemSeparator.MoveTo(dst, dst));
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

        [Test]
        public void WavParse_ExtensiblePcm16_SubFormat으로읽기()
        {
            // torchaudio/soundfile 계열이 내보내는 WAVE_FORMAT_EXTENSIBLE(0xFFFE) +
            // SubFormat PCM GUID. demucs 출력에서 흔한 형태다.
            var bytes = BuildExtensibleWavBytes(subFormatTag: 1, bits: 16,
                channels: 1, freq: 44100,
                pcm16: new short[] { 0, 10000, -10000 });
            AudioClip clip = WavFileReader.Parse(bytes, "ext", out string error);
            _cleanup.Add(clip);

            Assert.IsNotNull(clip, error);
            Assert.AreEqual(44100, clip.frequency);
            Assert.AreEqual(3, clip.samples);
            var data = new float[3];
            clip.GetData(data, 0);
            Assert.AreEqual(10000f / 32768f, data[1], 1e-4f);
        }

        [Test]
        public void WavParse_ExtensibleFloat32_SubFormat으로읽기()
        {
            var bytes = BuildExtensibleWavBytes(subFormatTag: 3, bits: 32,
                channels: 2, freq: 48000,
                float32: new float[] { 0f, 0.5f, -0.5f, 1f });
            AudioClip clip = WavFileReader.Parse(bytes, "extf", out string error);
            _cleanup.Add(clip);

            Assert.IsNotNull(clip, error);
            Assert.AreEqual(2, clip.channels);
            Assert.AreEqual(2, clip.samples);
            var data = new float[4];
            clip.GetData(data, 0);
            Assert.AreEqual(0.5f, data[1], 1e-6f);
            Assert.AreEqual(-0.5f, data[2], 1e-6f);
        }

        // ---------- 프로비저너 순수 함수 ----------

        [Test]
        public void Provisioner_엔진별파일명_구분()
        {
            StringAssert.Contains("audio-separator",
                PythonEnvProvisioner.RequirementsFileName(
                    VocalStemSeparator.Engine.AudioSeparator));
            StringAssert.Contains("demucs",
                PythonEnvProvisioner.RequirementsFileName(
                    VocalStemSeparator.Engine.Demucs));
            Assert.AreNotEqual(
                PythonEnvProvisioner.MarkerFileName(VocalStemSeparator.Engine.AudioSeparator),
                PythonEnvProvisioner.MarkerFileName(VocalStemSeparator.Engine.Demucs));
        }

        [Test]
        public void Provisioner_venv경로와파이썬경로()
        {
            string venv = PythonEnvProvisioner.VenvDir("C:\\proj");
            StringAssert.Contains(Path.Combine("Tools", "LipSync"), venv);
            string py = PythonEnvProvisioner.VenvPythonPath(venv);
            StringAssert.Contains("Scripts", py);
            StringAssert.EndsWith("python.exe", py);
        }

        [Test]
        public void Provisioner_해시는결정적_내용다르면다름()
        {
            string h1 = PythonEnvProvisioner.RequirementsHash("a==1.0\n");
            Assert.AreEqual(h1, PythonEnvProvisioner.RequirementsHash("a==1.0\n"));
            Assert.AreNotEqual(h1, PythonEnvProvisioner.RequirementsHash("a==2.0\n"));
        }

        // ---------- 스템 미리듣기 로더 판별 ----------

        [Test]
        public void StemPreview_프로젝트안파일은AssetDatabase()
        {
            Assert.AreEqual(StemAudioPreview.Loader.AssetDatabase,
                StemAudioPreview.Classify("Assets/_Project/x.mp3"));
            string underAssets = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "stem.wav"));
            Assert.AreEqual(StemAudioPreview.Loader.AssetDatabase,
                StemAudioPreview.Classify(underAssets));
        }

        [Test]
        public void StemPreview_프로젝트안이어도Assets밖은Wav()
        {
            // 임포터는 Assets/ 아래만 본다 — Tools/ 같은 형제 폴더의 wav는 파일 로더로.
            string toolsWav = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "../Tools/LipSync/x.wav"));
            Assert.AreEqual(StemAudioPreview.Loader.WavFile,
                StemAudioPreview.Classify(toolsWav));
        }

        [Test]
        public void StemPreview_프로젝트밖은Wav만()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "stem.wav");
            Assert.AreEqual(StemAudioPreview.Loader.WavFile,
                StemAudioPreview.Classify(tmp));
            Assert.AreEqual(StemAudioPreview.Loader.Unsupported,
                StemAudioPreview.Classify(Path.ChangeExtension(tmp, ".mp3")));
            Assert.AreEqual(StemAudioPreview.Loader.Unsupported,
                StemAudioPreview.Classify(null));
            Assert.AreEqual(StemAudioPreview.Loader.Unsupported,
                StemAudioPreview.Classify(""));
        }

        [Test]
        public void StemPreview_없는파일은에러반환()
        {
            string missing = Path.Combine(Path.GetTempPath(),
                "no_such_stem_" + Path.GetRandomFileName() + ".wav");
            string error = StemAudioPreview.Toggle(missing);
            Assert.IsNotEmpty(error);
            Assert.IsNull(StemAudioPreview.PlayingPath);
        }

        [Test]
        public void StemPreview_미임포트Assets안Wav는직접파싱폴백()
        {
            // 외부 프로세스가 방금 쓴 wav는 임포트 전이라 AssetDatabase가 못 읽는다 — WAV 파서 폴백으로 재생돼야 한다.
            string dir = Path.Combine(UnityEngine.Application.dataPath,
                "Generated/LipSync/preview_test_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            string wav = Path.Combine(dir, "unimported.wav");
            File.WriteAllBytes(wav, BuildWavBytes(1, 16, 1, 8000,
                new short[] { 0, 16000, -16000, 0 }));
            try
            {
                Assert.AreEqual(StemAudioPreview.Loader.AssetDatabase,
                    StemAudioPreview.Classify(wav));
                string error = StemAudioPreview.Toggle(wav);
                Assert.IsNull(error);
                Assert.AreEqual(wav, StemAudioPreview.PlayingPath);
            }
            finally
            {
                StemAudioPreview.Stop();
                Directory.Delete(dir, true);
            }
        }

        // ---------- 분리 진행률 파싱 ----------

        [Test]
        public void ReportProgress_tqdm퍼센트를파싱()
        {
            float last = -1f;
            VocalStemSeparator.ReportProgress(" 42%|████      | 3/7 [00:05<00:07]", p => last = p);
            Assert.AreEqual(0.42f, last, 0.001f);
        }

        [Test]
        public void ReportProgress_여러퍼센트는마지막것()
        {
            // \r로 이어진 tqdm 갱신이 한 라인으로 오면 마지막 %를 쓴다.
            float last = -1f;
            VocalStemSeparator.ReportProgress("\r 10%|#|\r 55%|#####|\r 99%|#########|", p => last = p);
            Assert.AreEqual(0.99f, last, 0.001f);
        }

        [Test]
        public void ReportProgress_퍼센트없으면무시_콜백null도무시()
        {
            bool called = false;
            VocalStemSeparator.ReportProgress("loading model...", p => called = true);
            Assert.IsFalse(called);
            VocalStemSeparator.ReportProgress("50%", null); // 예외 없어야 함
        }

        private static byte[] BuildExtensibleWavBytes(int subFormatTag, int bits,
            int channels, int freq, short[] pcm16 = null, float[] float32 = null)
        {
            // fmt(40B, tag=0xFFFE) + data 청크로 구성한 EXTENSIBLE WAV.
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                int bytesPerSample = bits / 8;
                int sampleCount = pcm16?.Length ?? float32?.Length ?? 0;
                int dataSize = sampleCount * bytesPerSample;
                int byteRate = freq * channels * bytesPerSample;
                int blockAlign = channels * bytesPerSample;

                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(4 + (8 + 40) + (8 + dataSize));
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(40);
                w.Write(unchecked((short)0xFFFE));
                w.Write((short)channels);
                w.Write(freq);
                w.Write(byteRate);
                w.Write((short)blockAlign);
                w.Write((short)bits);
                w.Write((short)22);           // cbSize
                w.Write((short)bits);         // validBitsPerSample
                w.Write(channels == 1 ? 0x4 : 0x3); // channelMask
                w.Write(subFormatTag);        // SubFormat GUID Data1(앞 2B=실제 포맷)
                w.Write((short)0);            // Data2
                w.Write(new byte[] { 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA,
                                     0x00, 0x38, 0x9B, 0x71 }); // Data3+Data4(10B)
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataSize);
                if (pcm16 != null)
                {
                    foreach (short s in pcm16) w.Write(s);
                }
                else
                {
                    foreach (float f in float32) w.Write(f);
                }
                return ms.ToArray();
            }
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

        // ---------- 비VRM 모델(모음 모프 스캔) ----------

        [Test]
        public void PhonemeForShapeName_MMD와영문_모음인식()
        {
            Assert.AreEqual("A", VocalLipSyncBaker.PhonemeForShapeName("88.xあ"));
            Assert.AreEqual("I", VocalLipSyncBaker.PhonemeForShapeName("92.xい"));
            Assert.AreEqual("U", VocalLipSyncBaker.PhonemeForShapeName("97.xう"));
            Assert.AreEqual("E", VocalLipSyncBaker.PhonemeForShapeName("98.xえ"));
            Assert.AreEqual("O", VocalLipSyncBaker.PhonemeForShapeName("99.xお"));
            Assert.AreEqual("A", VocalLipSyncBaker.PhonemeForShapeName("mouth_a"));
            Assert.AreEqual("A", VocalLipSyncBaker.PhonemeForShapeName("A"));
            // 변형/무관 모프는 배제 — 실제 미쿠 모델에서 잡힌 오매치 포함
            Assert.IsNull(VocalLipSyncBaker.PhonemeForShapeName("89.xあ２"));
            Assert.IsNull(VocalLipSyncBaker.PhonemeForShapeName("13.まばたき"));
            Assert.IsNull(VocalLipSyncBaker.PhonemeForShapeName("14.笑い"));   // 'い'로 끝나지만 표정
            Assert.IsNull(VocalLipSyncBaker.PhonemeForShapeName("85.Earphone switch A"));
            Assert.IsNull(VocalLipSyncBaker.PhonemeForShapeName("meta"));
            Assert.IsNull(VocalLipSyncBaker.PhonemeForShapeName(null));
        }

        [Test]
        public void BakeClip_비VRM모델_모음모프자동바인딩()
        {
            // VRM 프록시 없이 MMD식 모프명만 가진 캐릭터
            var root = new GameObject("MmdChar");
            var face = new GameObject("Face");
            face.transform.SetParent(root.transform, false);
            _cleanup.Add(root);
            var mesh = new Mesh { name = "mmd" };
            mesh.vertices = new[] { Vector3.zero };
            string[] shapes = { "88.xあ", "92.xい", "97.xう", "98.xえ", "99.xお",
                "89.xあ２", "13.まばたき" };
            foreach (string s in shapes)
            {
                mesh.AddBlendShapeFrame(s, 100f,
                    new Vector3[1], new Vector3[1], new Vector3[1]);
            }
            _cleanup.Add(mesh);
            face.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;

            var bindings = VocalLipSyncBaker.ResolveVowelBindings(root);
            Assert.AreEqual(5, bindings.Count, "모음 5종만 잡혀야 함(変형・변형・無관 제외)");
            foreach (var b in bindings)
            {
                Assert.AreEqual("Face", b.relativePath);
                Assert.AreEqual(100f, b.weight);
            }

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
            AnimationClip clip = VocalLipSyncBaker.BakeClip(data, root, 0.02f, warnings);
            _cleanup.Add(clip);
            var curves = UnityEditor.AnimationUtility.GetCurveBindings(clip);
            Assert.AreEqual(5, curves.Length, "모음 5개 커브가 생성돼야 함");
            Assert.IsTrue(System.Linq.Enumerable.Any(curves,
                c => c.propertyName == "blendShape.88.xあ"));
        }

        [Test]
        public void BakeClip_모프없는모델은예외()
        {
            var root = new GameObject("NoShapes");
            _cleanup.Add(root);
            var mesh = new Mesh { name = "plain" };
            mesh.vertices = new[] { Vector3.zero };
            _cleanup.Add(mesh);
            root.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;

            var data = ScriptableObject.CreateInstance<uLipSync.BakedData>();
            _cleanup.Add(data);
            data.duration = 1f / 60f;
            data.frames.Add(new uLipSync.BakedFrame { volume = 0.5f });

            Assert.Throws<System.ArgumentException>(
                () => VocalLipSyncBaker.BakeClip(data, root));
        }

        [Test]
        public void BakeClip_모음일부만있으면_누락경고()
        {
            // 'あ'만 있는 모델 — 클립은 만들되 나머지 음소 누락을 경고해야 함
            var root = new GameObject("Partial");
            _cleanup.Add(root);
            var mesh = new Mesh { name = "m" };
            mesh.vertices = new[] { Vector3.zero };
            mesh.AddBlendShapeFrame("あ", 100f, new Vector3[1], new Vector3[1], new Vector3[1]);
            _cleanup.Add(mesh);
            root.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;

            var data = ScriptableObject.CreateInstance<uLipSync.BakedData>();
            _cleanup.Add(data);
            data.duration = 1f / 60f;
            data.frames.Add(new uLipSync.BakedFrame
            {
                volume = 0.5f,
                phonemes = new List<uLipSync.BakedPhonemeRatio>(),
            });

            var warnings = new List<string>();
            AnimationClip clip = VocalLipSyncBaker.BakeClip(data, root, 0.02f, warnings);
            _cleanup.Add(clip);
            Assert.AreEqual(1, UnityEditor.AnimationUtility.GetCurveBindings(clip).Length);
            Assert.AreEqual(4, warnings.Count, "I/U/E/O 4개 누락 경고");
        }

        // ---------- 클립 미리보기 재생 ----------

        [Test]
        public void ClipPreview_재생시모프적용_정지시복원()
        {
            var root = new GameObject("PrevChar");
            _cleanup.Add(root);
            var mesh = new Mesh { name = "m" };
            mesh.vertices = new[] { Vector3.zero };
            mesh.AddBlendShapeFrame("あ", 100f, new Vector3[1], new Vector3[1], new Vector3[1]);
            _cleanup.Add(mesh);
            var smr = root.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;

            var data = ScriptableObject.CreateInstance<uLipSync.BakedData>();
            _cleanup.Add(data);
            data.duration = 2f / 60f; // 2프레임 — 길이 0 클립은 재생 상태가 즉시 끝나므로
            data.frames.Add(new uLipSync.BakedFrame
            {
                volume = 1f,
                phonemes = new List<uLipSync.BakedPhonemeRatio>
                {
                    new uLipSync.BakedPhonemeRatio { phoneme = "A", ratio = 1f },
                },
            });
            data.frames.Add(new uLipSync.BakedFrame
            {
                volume = 1f,
                phonemes = new List<uLipSync.BakedPhonemeRatio>
                {
                    new uLipSync.BakedPhonemeRatio { phoneme = "A", ratio = 1f },
                },
            });
            AnimationClip clip = VocalLipSyncBaker.BakeClip(data, root);
            _cleanup.Add(clip);

            try
            {
                LipSyncClipPreview.Start(clip, root);
                Assert.IsTrue(LipSyncClipPreview.IsPlaying);
                Assert.IsTrue(UnityEditor.AnimationMode.InAnimationMode());
                Assert.Greater(smr.GetBlendShapeWeight(0), 50f,
                    "재생 시작 시 첫 프레임 모프 가중치가 적용돼야 함");
            }
            finally
            {
                LipSyncClipPreview.Stop();
            }
            Assert.IsFalse(LipSyncClipPreview.IsPlaying);
            Assert.IsFalse(UnityEditor.AnimationMode.InAnimationMode());
            Assert.AreEqual(0f, smr.GetBlendShapeWeight(0), "정지 후 원래 가중치로 복원돼야 함");
        }

        [Test]
        public void ClipPreview_null입력은무시()
        {
            LipSyncClipPreview.Start(null, null);
            Assert.IsFalse(LipSyncClipPreview.IsPlaying);
        }

        // ---------- 노이즈 감쇠(소프트 니 + 릴리즈) ----------

        [Test]
        public void BuildCurve_소프트니_게이트근처감쇠()
        {
            // GetLipSyncInfo가 raw volume을 log10→[-2.5,-1.5] 구간으로 정규화한다.
            // normVol 0.15를 얻으려면 raw = 10^(-2.35) ≈ 0.00447.
            // gate=0.1 → 니 밴드 0.1~0.2, 팩터 0.5 → 값 = 1×(0.15×0.5)×100 = 7.5
            var clip = BakeAOnlyClip(new[] { 0.00447f }, gate: 0.1f, releaseDamp: 0f);
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(
                clip, UnityEditor.AnimationUtility.GetCurveBindings(clip)[0]);
            Assert.AreEqual(7.5f, curve.Evaluate(0f), 0.2f, "소프트 니로 게이트 직상 신호는 절반 감쇠");
        }

        [Test]
        public void BuildCurve_릴리즈감쇄_하강만스무딩()
        {
            // 피크 후 무음 — releaseDamp=0.5면 다음 프레임에 50% 잔존, 어택(상승)은 즉시
            var clip = BakeAOnlyClip(new[] { 1f, 0f, 0f }, gate: 0.02f, releaseDamp: 0.5f);
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(
                clip, UnityEditor.AnimationUtility.GetCurveBindings(clip)[0]);
            float dt = 1f / 60f;
            Assert.AreEqual(100f, curve.Evaluate(0f), 1f);         // 어택 즉시
            Assert.AreEqual(50f, curve.Evaluate(dt), 1f);          // 감쇄 1단계
            Assert.AreEqual(25f, curve.Evaluate(dt * 2f), 1f);     // 감쇄 2단계
        }

        [Test]
        public void BuildCurve_감쇄없으면_즉시0()
        {
            var clip = BakeAOnlyClip(new[] { 1f, 0f }, gate: 0.02f, releaseDamp: 0f);
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(
                clip, UnityEditor.AnimationUtility.GetCurveBindings(clip)[0]);
            Assert.AreEqual(0f, curve.Evaluate(1f / 60f), "감쇄 없으면 무음 프레임 즉시 0");
        }

        /// <summary>'あ' 모프 하나만 가진 모델에 A=1 음소 데이터를 베이크한다.</summary>
        private AnimationClip BakeAOnlyClip(float[] volumes, float gate, float releaseDamp)
        {
            var root = new GameObject("DampChar");
            _cleanup.Add(root);
            var mesh = new Mesh { name = "m" };
            mesh.vertices = new[] { Vector3.zero };
            mesh.AddBlendShapeFrame("あ", 100f, new Vector3[1], new Vector3[1], new Vector3[1]);
            _cleanup.Add(mesh);
            root.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;

            var data = ScriptableObject.CreateInstance<uLipSync.BakedData>();
            _cleanup.Add(data);
            data.duration = volumes.Length / 60f;
            foreach (float v in volumes)
            {
                data.frames.Add(new uLipSync.BakedFrame
                {
                    volume = v,
                    phonemes = new List<uLipSync.BakedPhonemeRatio>
                    {
                        new uLipSync.BakedPhonemeRatio { phoneme = "A", ratio = 1f },
                    },
                });
            }
            var clip = VocalLipSyncBaker.BakeClip(data, root, gate, null, releaseDamp);
            _cleanup.Add(clip);
            return clip;
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
