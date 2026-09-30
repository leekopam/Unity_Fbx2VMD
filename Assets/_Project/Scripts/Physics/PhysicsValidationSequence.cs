using System.Collections;
using System.Collections.Generic;
using System.Text;
using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 검증용 동작 시퀀스 — 회전·팔 휘두름·몸 굽힘을 순차 재생하며
    /// PhysicsValidationProbe가 침투·걸림을 측정한다. 종료 시 자동 리포트.
    /// VRMSpringBoneTool의 달리기/댄스 검증 버튼과 동일한 개념.
    /// 플레이 모드에서만 동작한다.
    /// </summary>
    public class PhysicsValidationSequence : MonoBehaviour
    {
        [Tooltip("비워두면 같은 오브젝트에서 자동 탐색")]
        public CharacterPhysicsSetup setup;
        public PhysicsValidationProbe probe;

        [Header("시퀀스 파라미터")]
        [Tooltip("몸 회전 각도(도)")]
        public float rotateDegrees = 120f;
        [Tooltip("팔 휘두름 반복 횟수")]
        public int armSwingCount = 3;
        [Tooltip("팔 휘두름 진폭(도)")]
        public float armSwingDegrees = 70f;
        [Tooltip("몸 굽힘 각도(도)")]
        public float bendDegrees = 35f;
        [Tooltip("각 단계 길이(초)")]
        public float phaseDuration = 1.2f;

        [Tooltip("시퀀스 동안 Animator를 일시 비활성화한다. Animator가 본 회전을 덮어써 " +
            "검증 포즈가 무효화되는 것을 방지하기 위함. 기존 애니메이션 재생 중 꼬임을 보려면 끄고 Probe만 사용")]
        public bool disableAnimatorDuringSequence = true;

        [Header("결과")]
        [TextArea]
        [System.NonSerialized] public string lastReport = "";
        public bool isRunning;

        Coroutine routine;
        Animator suppressedAnimator;
        readonly List<(Transform bone, Quaternion rest)> movedBones =
            new List<(Transform, Quaternion)>();

        /// <summary>검증 시퀀스를 시작한다. 이미 실행 중이면 재시작하지 않음.</summary>
        [ContextMenu("검증 시퀀스 실행")]
        public void Run()
        {
            if (!Application.isPlaying)
            {
                lastReport = "플레이 모드에서만 실행 가능합니다.";
                Debug.LogWarning("[PhysicsValidationSequence] " + lastReport, this);
                return;
            }
            if (isRunning)
                return;
            routine = StartCoroutine(RunSequence());
        }

        /// <summary>진행 중인 시퀀스를 중단하고 본과 Animator를 원위치로 되돌린다.</summary>
        [ContextMenu("검증 시퀀스 중단")]
        public void Stop()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            RestoreBones();
            RestoreAnimator();
            isRunning = false;
        }

        IEnumerator RunSequence()
        {
            isRunning = true;
            movedBones.Clear();
            var report = new StringBuilder();
            report.AppendLine("[검증 시퀀스] 시작");
            bool completed = false;
            try
            {
                if (setup == null)
                    setup = GetComponent<CharacterPhysicsSetup>();
                if (probe == null)
                    probe = GetComponent<PhysicsValidationProbe>();
                if (probe != null)
                {
                    probe.ResetStats();
                    probe.RebuildTracking();
                }

                // Animator가 Update 단계에서 본 회전을 덮어쓰므로 시퀀스 중에는 비활성화한다.
                // 비활성화해도 isHuman/GetBoneTransform의 Avatar 매핑은 유지된다.
                Animator animator = setup != null ? setup.targetAnimator : null;
                if (animator == null)
                    animator = GetComponent<Animator>();
                if (animator != null && animator.enabled && disableAnimatorDuringSequence)
                {
                    animator.enabled = false;
                    suppressedAnimator = animator;
                    report.AppendLine("Animator를 일시 비활성화해 검증 포즈를 구동함");
                }

                // 1. 몸 회전 — 긴 머리카락 관성 침투 유발
                // 루트도 movedBones에 넣어 중단(Stop) 시에도 원위치 복원되게 한다.
                Track(transform);
                Quaternion bodyRest = transform.rotation;
                yield return RunPhase("몸 회전", phaseDuration, t =>
                {
                    float a = Mathf.Sin(t * Mathf.PI * 2f) * rotateDegrees * 0.5f;
                    transform.rotation = bodyRest * Quaternion.Euler(0, a, 0);
                });

                // 2. 팔 휘두름 — 트윈테일/긴 머리카락 궤적과 교차, 손발 꼬임 유발
                Transform lArm = ResolveBone(HumanBodyBones.LeftUpperArm);
                Transform rArm = ResolveBone(HumanBodyBones.RightUpperArm);
                if (lArm == null && rArm == null)
                    report.AppendLine("팔 본을 해석할 수 없어 팔 휘두름을 건너뜀");
                else
                {
                    Track(lArm);
                    Track(rArm);
                    Quaternion lRest = lArm != null ? lArm.rotation : Quaternion.identity;
                    Quaternion rRest = rArm != null ? rArm.rotation : Quaternion.identity;
                    yield return RunPhase("팔 휘두름", phaseDuration * armSwingCount, t =>
                    {
                        float a = Mathf.Sin(t * armSwingCount * Mathf.PI * 2f) * armSwingDegrees;
                        if (lArm != null)
                            lArm.rotation = lRest * Quaternion.Euler(0, 0, a);
                        if (rArm != null)
                            rArm.rotation = rRest * Quaternion.Euler(0, 0, -a);
                    });
                }

                // 3. 몸 굽힘 — 앞머리 백스톱/가슴 콜라이더 검증
                Transform spine = ResolveBone(HumanBodyBones.Chest) ?? ResolveBone(HumanBodyBones.Spine);
                if (spine != null)
                {
                    Track(spine);
                    Quaternion spineRest = spine.localRotation;
                    yield return RunPhase("몸 굽힘", phaseDuration, t =>
                    {
                        float a = Mathf.Sin(t * Mathf.PI) * bendDegrees;
                        spine.localRotation = spineRest * Quaternion.Euler(a, 0, 0);
                    });
                }
                completed = true;
            }
            finally
            {
                // 예외·외부 중단 어느 경로든 본과 Animator를 반드시 원위치시키고 실행 플래그를 해제한다.
                RestoreBones();
                RestoreAnimator();
                if (!completed)
                {
                    isRunning = false;
                    routine = null;
                    report.AppendLine("[검증 시퀀스] 예외 또는 중단으로 종료 — 본/Animator 복원 완료");
                    if (probe != null)
                    {
                        try { report.AppendLine(probe.BuildReport()); }
                        catch (System.Exception e)
                        {
                            report.AppendLine($"프로브 리포트 생성 실패: {e.Message}");
                        }
                    }
                    lastReport = report.ToString();
                    Debug.LogWarning(lastReport, this);
                }
            }

            // 복원 후 안정화 대기
            yield return new WaitForSeconds(0.5f);

            // 리포트 생성 실패가 실행 플래그를 웨지시키지 않도록 플래그부터 해제하고 예외를 흡수한다.
            isRunning = false;
            routine = null;
            report.AppendLine("[검증 시퀀스] 완료");
            if (probe != null)
            {
                try { report.AppendLine(probe.BuildReport()); }
                catch (System.Exception e)
                {
                    report.AppendLine($"프로브 리포트 생성 실패: {e.Message}");
                }
            }
            lastReport = report.ToString();
            Debug.Log(lastReport, this);
        }

        void Track(Transform bone)
        {
            if (bone != null)
                movedBones.Add((bone, bone.rotation));
        }

        void RestoreBones()
        {
            foreach (var (bone, rest) in movedBones)
                if (bone != null)
                    bone.rotation = rest;
            movedBones.Clear();
        }

        void RestoreAnimator()
        {
            if (suppressedAnimator != null)
            {
                suppressedAnimator.enabled = true;
                suppressedAnimator = null;
            }
        }

        IEnumerator RunPhase(string name, float duration, System.Action<float> step)
        {
            float t = 0f;
            while (t < duration)
            {
                // EditMode 수동 펌프(테스트)에서는 Time.deltaTime이 0이라 영구 정지하므로
                // 0일 때만 최소 프레임 스텝으로 대체한다. 플레이 중에는 deltaTime을 그대로 쓴다.
                float dt = Time.deltaTime > 0f ? Time.deltaTime : 1f / 60f;
                t += dt;
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }
        }

        Transform ResolveBone(HumanBodyBones bone)
        {
            if (setup == null || setup.targetAnimator == null)
                return null;
            var animator = setup.targetAnimator;
            if (animator.isHuman)
            {
                var t = animator.GetBoneTransform(bone);
                if (t != null)
                    return t;
            }
            var map = NamePatternBoneResolver.Resolve(animator.transform);
            return map.TryGetValue(bone, out var found) ? found : null;
        }

        void OnDisable()
        {
            Stop();
        }
    }
}
