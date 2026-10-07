using System.Collections.Generic;
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

        // 경로별로 재매핑된 임시 클립과 샘플 대상 오브젝트
        private sealed class SampleTarget
        {
            public GameObject GameObject;
            public AnimationClip Clip;
        }

        private static AnimationClip _clip;
        private static GameObject _target;
        private static SampleTarget[] _sampleTargets;
        private static double _startTime;
        private static bool _subscribed;
        private static bool _enteredMode; // 우리가 연 AnimationMode인지 — 타 도구 세션을 끊지 않기 위해 추적

        public static bool IsPlaying => _clip != null;
        public static AnimationClip Clip => _clip;
        public static GameObject Target => _target;

        /// <summary>마지막 Start 실패 사유. 성공하면 null.</summary>
        public static string LastError { get; private set; }

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
            // 리로드 전에 임시 클립(HideAndDontSave)을 해제하지 않으면 에디터 세션에 누수된다.
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        /// <summary>클립 재생 시작. 끝까지 재생하면 자동 정지한다.
        /// 다른 도구가 이미 AnimationMode를 사용 중이면 false를 반환하고 아무것도 건드리지 않는다.</summary>
        public static bool Start(AnimationClip clip, GameObject target)
        {
            Stop();
            LastError = null;
            if (clip == null || target == null)
            {
                return false;
            }
            if (AnimationMode.InAnimationMode())
            {
                LastError = "다른 도구가 애니메이션 미리보기를 사용 중입니다.";
                return false;
            }
            var targets = BuildSampleTargets(clip, target);
            if (targets.Length == 0)
            {
                LastError = $"클립 '{clip.name}'의 바인딩이 대상 '{target.name}' 아래에서 하나도 확인되지 않습니다.";
                UnityEngine.Debug.LogWarning("[LipSyncClipPreview] " + LastError);
                return false;
            }
            AnimationMode.StartAnimationMode();
            _enteredMode = true;
            _clip = clip;
            _target = target;
            _sampleTargets = targets;
            _startTime = EditorApplication.timeSinceStartup;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.update += Tick;
            _subscribed = true;
            Tick();
            return true;
        }

        /// <summary>클립 커브를 바인딩 경로별로 묶어, 확인된 대상 오브젝트 각각에 직접 적용할 임시 클립으로 만든다.
        /// 루트를 통째로 샘플링하면 AnimationMode가 Animator의 미적용 본을 기본 포즈로 리셋해
        /// 휴머노이드 캐릭터가 무너지므로, 커브가 실제로 붙은 잎 오브젝트 단위로 나눠 샘플링한다.</summary>
        private static SampleTarget[] BuildSampleTargets(AnimationClip clip, GameObject target)
        {
            var byPath = new Dictionary<string, List<EditorCurveBinding>>();
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (!byPath.TryGetValue(b.path, out var list))
                {
                    list = new List<EditorCurveBinding>();
                    byPath.Add(b.path, list);
                }
                list.Add(b);
            }
            var targets = new List<SampleTarget>();
            foreach (var kv in byPath)
            {
                var t = string.IsNullOrEmpty(kv.Key) ? target.transform : target.transform.Find(kv.Key);
                if (t == null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[LipSyncClipPreview] 경로 '{kv.Key}'를 대상 '{target.name}' 아래에서 찾지 못해 해당 커브 {kv.Value.Count}개를 건너뜁니다.");
                    continue;
                }
                // Animator가 있는 오브젝트에 샘플링하면 클립에 없는 휴머노이드 본이 기본값으로
                // 리셋돼 포즈가 무너진다. 잎 분할로 보통 회피되지만 루트 직접 바인딩은 잔여 위험이 있다.
                if (t.GetComponent<Animator>() != null
                    && kv.Value.TrueForAll(b => b.type != typeof(Animator)))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[LipSyncClipPreview] 경로 '{kv.Key}'의 오브젝트에 Animator가 있어 미리보기 중 포즈가 기본값으로 리셋될 수 있습니다.");
                }
                var remapped = new AnimationClip
                {
                    name = $"LipSyncPreview_{clip.name}_{(string.IsNullOrEmpty(kv.Key) ? "root" : kv.Key)}",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                foreach (var b in kv.Value)
                {
                    // 대상 컴포넌트가 없으면 커브가 묵시적으로 무시되므로 미리 경고한다.
                    if (b.type != typeof(Transform) && b.type != typeof(GameObject)
                        && t.GetComponent(b.type) == null)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[LipSyncClipPreview] '{t.name}'에 {b.type.Name}가 없어 '{b.propertyName}' 커브가 적용되지 않습니다.");
                    }
                    remapped.SetCurve("", b.type, b.propertyName, AnimationUtility.GetEditorCurve(clip, b));
                }
                targets.Add(new SampleTarget { GameObject = t.gameObject, Clip = remapped });
            }
            return targets.ToArray();
        }

        /// <summary>재생 정지 — 샘플링된 포즈/모프를 원래 값으로 복원한다.
        /// 클립 에셋이 재생 중 파괴되어도 정리가 항상 실행되도록 상태와 무관하게 정리한다.</summary>
        public static void Stop()
        {
            _clip = null;
            _target = null;
            if (_sampleTargets != null)
            {
                foreach (var st in _sampleTargets)
                {
                    if (st.Clip != null)
                    {
                        Object.DestroyImmediate(st.Clip);
                    }
                }
                _sampleTargets = null;
            }
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

        /// <summary>재생 위치를 지정 초로 이동하고 즉시 샘플링한다. 재생 중이 아니면 무시.</summary>
        public static void Seek(float time)
        {
            if (_clip == null || _target == null)
            {
                return;
            }
            _startTime = EditorApplication.timeSinceStartup
                - Mathf.Clamp(time, 0f, _clip.length);
            Tick();
        }

        private static void Tick()
        {
            if (_clip == null || _target == null || _sampleTargets == null)
            {
                Stop();
                return;
            }
            // 샘플링 먼저 — 길이 0짜리 단일키 클립도 최초 1회는 적용된다.
            float elapsed = (float)(EditorApplication.timeSinceStartup - _startTime);
            float t = Mathf.Min(elapsed, _clip.length);
            foreach (var st in _sampleTargets)
            {
                if (st.GameObject == null)
                {
                    continue;
                }
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(st.GameObject, st.Clip, t);
                AnimationMode.EndSampling();
            }
            SceneView.RepaintAll();
            if (elapsed > _clip.length)
            {
                Stop();
            }
        }
    }
}
