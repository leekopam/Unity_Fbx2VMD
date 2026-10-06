using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 분리된 스템/원본 음원을 에디터에서 미리듣기한다.
    /// Unity 비공개 AudioUtil 프리뷰 API를 리플렉션으로 호출해 플레이 모드 없이 재생하고,
    /// 프로젝트 밖 파일은 WavFileReader로 AudioClip을 만든다.
    /// 한 번에 하나만 재생한다(재생 중 다른 버튼을 누르면 이전 것은 정지).
    /// </summary>
    public static class StemAudioPreview
    {
        /// <summary>경로가 어떤 로더로 재생 가능한지 — 순수 판별이라 테스트 가능.</summary>
        public enum Loader
        {
            Unsupported = 0,
            AssetDatabase = 1, // 프로젝트 안 — 임포터가 mp3/ogg/flac 등도 처리
            WavFile = 2,       // 프로젝트 밖 — PCM WAV만
        }

        /// <summary>현재 재생 중인 경로. 없으면 null.</summary>
        public static string PlayingPath { get; private set; }

        static AudioClip _clip;
        static bool _ownsClip; // WavFileReader로 만든 클립만 정지 시 파괴

        static MethodInfo _play;
        static MethodInfo _stopAll;
        static MethodInfo _isPlaying;
        static bool _resolved;

        /// <summary>
        /// 경로의 로더를 판별한다.
        /// FileUtil.GetProjectRelativePath는 프로젝트 밖 경로에 빈 문자열을 돌려준다.
        /// </summary>
        public static Loader Classify(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Loader.Unsupported;
            }
            // 슬래시로 통일 — FileUtil/GetFullPath는 역슬래시 섞인 입력을 오판할 수 있다.
            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return Loader.AssetDatabase;
            }
            // 임포터는 Assets/ 아래만 처리한다 — 프로젝트 안이어도 Assets 밖(Tools 등)은 WavFile로.
            string rel = FileUtil.GetProjectRelativePath(normalized);
            if (!string.IsNullOrEmpty(rel)
                && rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return Loader.AssetDatabase;
            }
            return string.Equals(Path.GetExtension(path), ".wav",
                StringComparison.OrdinalIgnoreCase)
                ? Loader.WavFile
                : Loader.Unsupported;
        }

        /// <summary>
        /// 해당 경로를 재생한다. 같은 경로가 재생 중이면 정지(토글).
        /// 반환값은 에러 메시지 — null이면 성공.
        /// </summary>
        public static string Toggle(string path)
        {
            if (PlayingPath == path && IsPlaying())
            {
                Stop();
                return null;
            }
            Stop();

            Resolve();
            // _isPlaying 미해석이면 ClearIfFinished가 즉시 정지시키므로 여기서 차단한다.
            if (_play == null || _stopAll == null || _isPlaying == null)
            {
                return "에디터 오디오 프리뷰 API를 찾지 못했습니다.";
            }

            AudioClip clip = Load(path, out string error);
            if (clip == null)
            {
                return error;
            }

            _clip = clip;
            PlayingPath = path;
            _play.Invoke(null, new object[] { clip, 0, false });
            return null;
        }

        /// <summary>재생을 멈추고 로드한 클립을 해제한다.</summary>
        public static void Stop()
        {
            Resolve();
            _stopAll?.Invoke(null, null);
            if (_ownsClip && _clip != null)
            {
                UnityEngine.Object.DestroyImmediate(_clip);
            }
            _clip = null;
            _ownsClip = false;
            PlayingPath = null;
        }

        /// <summary>자연 종료됐는지 — 윈도우 Update에서 버튼 라벨 복귀용으로 폴링한다.</summary>
        public static bool IsPlaying()
        {
            Resolve();
            return _isPlaying != null
                && _isPlaying.Invoke(null, null) is bool playing && playing;
        }

        /// <summary>자연 종료 시 상태를 비운다. 상태가 바뀌었으면 true.</summary>
        public static bool ClearIfFinished()
        {
            if (PlayingPath == null || IsPlaying())
            {
                return false;
            }
            Stop();
            return true;
        }

        static AudioClip Load(string path, out string error)
        {
            error = null;
            if (!File.Exists(path))
            {
                error = "파일이 없습니다: " + path;
                return null;
            }
            switch (Classify(path))
            {
                case Loader.AssetDatabase:
                    // GetProjectRelativePath는 절대 경로만 변환한다 — 이미 상대 경로면 그대로 쓴다.
                    string normalized = path.Replace('\\', '/');
                    string rel = normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                        || normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)
                        ? normalized
                        : FileUtil.GetProjectRelativePath(normalized);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(rel);
                    if (clip == null
                        && string.Equals(Path.GetExtension(path), ".wav",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        // 외부 프로세스가 방금 쓴 wav는 아직 임포트 전이라 AssetDatabase가 null을 준다.
                        // WAV는 직접 파싱이 가능하므로 폴백으로 재생한다.
                        var fallback = WavFileReader.Load(path, out error);
                        if (fallback != null)
                        {
                            _ownsClip = true;
                        }
                        return fallback;
                    }
                    if (clip == null)
                    {
                        error = "오디오로 임포트되지 않았습니다: " + rel;
                    }
                    return clip;
                case Loader.WavFile:
                    // 성공 확인 후에 소유권을 표시한다 — 실패 시 잔여 상태 방지.
                    var wavClip = WavFileReader.Load(path, out error);
                    if (wavClip != null)
                    {
                        _ownsClip = true;
                    }
                    return wavClip;
                default:
                    error = "미리듣기는 WAV 또는 프로젝트 내 오디오 파일만 지원합니다.";
                    return null;
            }
        }

        /// <summary>
        /// AudioUtil의 프리뷰 API는 Unity 버전별로 이름이 갈린다
        /// (PreviewClip 계열과 Clip 계열이 공존/개명됐다) — 양쪽 이름을 모두 탐색한다.
        /// </summary>
        static void Resolve()
        {
            if (_resolved)
            {
                return;
            }
            _resolved = true;
            Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtil == null)
            {
                return;
            }
            foreach (MethodInfo m in audioUtil.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                ParameterInfo[] ps = m.GetParameters();
                if ((m.Name == "PlayPreviewClip" || m.Name == "PlayClip")
                    && ps.Length == 3
                    && ps[0].ParameterType == typeof(AudioClip)
                    && ps[1].ParameterType == typeof(int)
                    && ps[2].ParameterType == typeof(bool))
                {
                    _play = m;
                }
                else if ((m.Name == "StopAllPreviewClips" || m.Name == "StopAllClips")
                    && ps.Length == 0)
                {
                    _stopAll = m;
                }
                else if ((m.Name == "IsPreviewClipPlaying" || m.Name == "IsClipPlaying")
                    && ps.Length == 0)
                {
                    _isPlaying = m;
                }
            }
        }
    }
}
