using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// 베이크된 립싱크 AnimationClip을 플레이 모드 없이 에디트 모드에서 캐릭터에 재생한다.
    /// AnimationMode 프리뷰로 샘플링하므로 정지하면 원래 포즈/모프가 자동 복원된다.
    /// </summary>
    [InitializeOnLoad]
    public static class LipSyncClipPreview
    {
        private const string SessionKey = "Fbx2Vmd.LipSync.PreviewActive";

        private static AnimationClip _clip;
        private static GameObject _target;
        private static double _startTime;
        private static bool _subscribed;
        private static bool _enteredMode; // 우리가 연 AnimationMode인지 — 타 도구 세션을 끊지 않기 위해 추적

        public static bool IsPlaying => _clip != null;
        public static AnimationClip Clip => _clip;
        public static GameObject Target => _target;

        /// <summary>재생 위치(초). 재생 중이 아니면 0.</summary>
        public static float Time
        {
            get
            {
                if (_clip == null)
                {
                    return 0f;
                }
                return Mathf.Min((float)(EditorApplication.timeSinceStartup - _startTime), _clip.length);
            }
        }

        static LipSyncClipPreview()
        {
            // 도메인 리로드로 정적 상태가 날아가도 AnimationMode는 에디터 전역 상태라 남는다.
            // 플래그가 세워져 있으면 재개하지 않고 정지해 씬 오염을 막는다.
            if (SessionState.GetBool(SessionKey, false))
            {
                SessionState.SetBool(SessionKey, false);
                if (AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }
            }
        }

        /// <summary>클립 재생 시작. 끝까지 재생하면 자동 정지한다.
        /// 다른 도구가 이미 AnimationMode를 사용 중이면 false를 반환하고 아무것도 건드리지 않는다.</summary>
        public static bool Start(AnimationClip clip, GameObject target)
        {
            Stop();
            if (clip == null || target == null || AnimationMode.InAnimationMode())
            {
                return false;
            }
            AnimationMode.StartAnimationMode();
            _enteredMode = true;
            _clip = clip;
            _target = target;
            _startTime = EditorApplication.timeSinceStartup;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.update += Tick;
            _subscribed = true;
            Tick();
            return true;
        }

        /// <summary>재생 정지 — 샘플링된 포즈/모프를 원래 값으로 복원한다.
        /// 클립 에셋이 재생 중 파괴되어도 정리가 항상 실행되도록 상태와 무관하게 정리한다.</summary>
        public static void Stop()
        {
            _clip = null;
            _target = null;
            SessionState.SetBool(SessionKey, false);
            if (_subscribed)
            {
                EditorApplication.update -= Tick;
                _subscribed = false;
            }
            if (_enteredMode && AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }
            _enteredMode = false;
        }

        private static void Tick()
        {
            if (_clip == null || _target == null)
            {
                Stop();
                return;
            }
            // 샘플링 먼저 — 길이 0짜리 단일키 클립도 최초 1회는 적용된다.
            float elapsed = (float)(EditorApplication.timeSinceStartup - _startTime);
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_target, _clip, Mathf.Min(elapsed, _clip.length));
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
            if (elapsed > _clip.length)
            {
                Stop();
            }
        }
    }
}
