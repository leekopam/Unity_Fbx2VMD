using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 곡/클립 시간에 따라 MC2 애니메이션 노출 프로퍼티 7종을 구동하고
    /// 탐색(seek)·컷 전환·클립 변경 같은 불연속에서 시뮬레이션을 리셋한다.
    /// MC2가 애니메이션 제어를 지원하는 값만 다룬다:
    /// blendWeight / gravity / damping / worldInertia / localInertia /
    /// windInfluence / animationPoseRatio.
    /// updateMode과 시간 진행은 MC2 PlayerLoop가 소유 — 이 컴포넌트는
    /// 별도 타이밍 시스템 없이 '정규화 클립 시간'만 입력받아 값을 매핑한다.
    /// </summary>
    public class ClothAnimationDriver : MonoBehaviour
    {
        [Header("대상")]
        public CharacterPhysicsSetup target;
        [Tooltip("VMD 파이프라인 — 지정하면 재생 시간·클립 변경을 자동 추적")]
        public Fbx2Vmd.FBXImporter.FBXVmdPipeline vmdPipeline;

        [Header("클립 구동 커브 (시간 0~1 = 클립 처음~끝, 비워두면 해당 값 미구동)")]
        public AnimationCurve blendWeight;
        public AnimationCurve gravity;
        public AnimationCurve damping;
        public AnimationCurve worldInertia;
        public AnimationCurve localInertia;
        public AnimationCurve windInfluence;
        public AnimationCurve animationPoseRatio;

        [Header("불연속 리셋")]
        [Tooltip("재생 시간이 이 임계(초) 이상 점프하면 시뮬레이션을 리셋 — 컷/탐색 시 폭주 방지")]
        [Min(0f)] public float seekResetThreshold = 0.25f;
        [Tooltip("리셋 시 현재 자세 유지 (true=keepPose, false=바인드 포즈)")]
        public bool resetKeepPose = true;

        float lastNormalizedTime = -1f;
        string lastClipName;
        /// <summary>마지막으로 수행된 동작 — 검증·디버그용</summary>
        [System.NonSerialized] public string lastAction = "";

        void Update()
        {
            if (vmdPipeline == null || !vmdPipeline.IsImportedMotionPlaying)
                return;
            float len = Mathf.Max(vmdPipeline.ImportedMotionClipLengthSeconds, 0.001f);
            ApplyClipTime(
                vmdPipeline.ImportedMotionCurrentTimeSeconds / len,
                vmdPipeline.ImportedMotionClipName);
        }

        /// <summary>
        /// 정규화 클립 시간(0~1)을 입력받아 불연속을 감지하고 커브를 적용한다.
        /// 외부 재생 시스템이 매 프레임 호출한다.
        /// </summary>
        public void ApplyClipTime(float normalizedTime, string clipName = null)
        {
            if (clipName != null && lastClipName != null && clipName != lastClipName)
                DoReset($"클립 변경: {clipName}");
            if (lastNormalizedTime >= 0f)
            {
                float len = vmdPipeline != null
                    ? Mathf.Max(vmdPipeline.ImportedMotionClipLengthSeconds, 0.001f) : 1f;
                if (ShouldReset(lastNormalizedTime, normalizedTime, len, seekResetThreshold))
                    DoReset($"시간 점프 {Mathf.Abs(normalizedTime - lastNormalizedTime) * len:F2}s");
            }
            lastNormalizedTime = normalizedTime;
            if (clipName != null)
                lastClipName = clipName;
            ApplyCurves(normalizedTime);
        }

        /// <summary>시간 점프가 리셋이 필요한 불연속인지 판정한다.</summary>
        public static bool ShouldReset(
            float prevNormalized, float currNormalized, float clipSeconds, float threshold)
        {
            return Mathf.Abs(currNormalized - prevNormalized) * Mathf.Max(clipSeconds, 0.001f)
                > threshold;
        }

        /// <summary>씬 전환·리스폰·일시정지 복귀 등 외부 이벤트에서 호출하는 명시적 리셋.</summary>
        public void NotifyDiscontinuity(string reason = "수동")
        {
            lastNormalizedTime = -1f;
            lastClipName = null;
            DoReset(reason);
        }

        void DoReset(string reason)
        {
            if (target == null)
                return;
            target.ResetPhysics(resetKeepPose);
            lastAction = $"리셋({reason})";
        }

        /// <summary>설정된 커브를 평가해 대상의 모든 유효 클로스에 적용한다.</summary>
        public void ApplyCurves(float normalizedTime)
        {
            if (target == null || target.generatedCloths == null)
                return;
            bool hasCurve = blendWeight != null || gravity != null || damping != null
                || worldInertia != null || localInertia != null
                || windInfluence != null || animationPoseRatio != null;
            if (!hasCurve)
                return;
            foreach (var cloth in target.generatedCloths)
            {
                if (cloth == null || !cloth.IsValid())
                    continue;
                ApplyCurvesTo(cloth.SerializeData, normalizedTime);
                if (Application.isPlaying)
                    cloth.SetParameterChange();
            }
            lastAction = $"커브 적용 t={normalizedTime:F2}";
        }

        /// <summary>클로스 데이터 1개에 커브를 적용한다 (테스트 가능한 순수 경로).</summary>
        public void ApplyCurvesTo(ClothSerializeData s, float t)
        {
            if (s == null)
                return;
            if (blendWeight != null)
                s.blendWeight = Mathf.Clamp01(blendWeight.Evaluate(t));
            if (gravity != null)
                s.gravity = gravity.Evaluate(t);
            if (damping != null)
                s.damping.value = damping.Evaluate(t);
            if (worldInertia != null)
                s.inertiaConstraint.worldInertia = Mathf.Clamp01(worldInertia.Evaluate(t));
            if (localInertia != null)
                s.inertiaConstraint.localInertia = Mathf.Clamp01(localInertia.Evaluate(t));
            if (windInfluence != null)
                s.wind.influence = Mathf.Clamp01(windInfluence.Evaluate(t));
            if (animationPoseRatio != null)
                s.animationPoseRatio = Mathf.Clamp01(animationPoseRatio.Evaluate(t));
        }
    }
}
