using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MagicaCloth2;
using Unity.Profiling;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 자기·상호 충돌 ON/OFF 성능 비교 프로브 — 플레이 모드에서만 동작.
    /// 같은 씬에서 생성 클로스의 selfCollisionConstraint를 켠 상태/끈 상태로
    /// StartClothUpdate.Time 마커를 각각 N프레임 계측해 비용 차이를 남긴다.
    /// 계측이 바꾼 파라미터는 정상 종료는 물론 컴포넌트 비활성화로 코루틴이
    /// 강제 종료되는 경로에서도 OnDisable이 원복한다 (PhysicsPerfRunner와 같은 패턴).
    /// </summary>
    public class SelfCollisionPerfProbe : MonoBehaviour
    {
        [Tooltip("비워두면 같은 오브젝트의 CharacterPhysicsSetup 사용")]
        public CharacterPhysicsSetup setup;
        [Tooltip("상태당 측정 프레임 수")]
        [Range(30, 1200)] public int measureFrames = 180;
        [Tooltip("상태 전환 후 마커 안정화 유예 프레임")]
        [Range(5, 300)] public int settleFrames = 30;

        [System.NonSerialized] public string lastSummary = "";
        [System.NonSerialized] public bool isRunning;

        Coroutine routine;
        ProfilerRecorder recorder;
        double sumNs, maxNs;
        int countedFrames;
        // 코루틴 강제 종료(OnDisable) 경로에서도 원복할 수 있게 필드로 둔다
        bool paramsPatched;
        List<(MagicaCloth cloth,
            SelfCollisionConstraint.SelfCollisionMode self,
            SelfCollisionConstraint.SelfCollisionMode sync)> savedParams;

        [ContextMenu("자기충돌 성능 비교 실행")]
        public void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SelfCollisionPerfProbe] 플레이 모드에서만 실행 가능", this);
                return;
            }
            if (!isRunning)
                routine = StartCoroutine(RunMeasurement());
        }

        /// <summary>계측 중단 — 변경한 파라미터와 레코더를 즉시 정리한다.
        /// StopCoroutine은 finally를 실행하지 않으므로 레코더도 여기서 해제해야 한다.</summary>
        [ContextMenu("자기충돌 성능 비교 중단")]
        public void Stop()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            RestoreParams();
            if (recorder.Valid) recorder.Dispose();
            recorder = default;
            // 완료된 측정의 요약을 덮어쓰지 않도록 실행 중일 때만 중단 표기
            if (isRunning) lastSummary = "중단됨";
            isRunning = false;
        }

        void OnDisable()
        {
            // 비활성화 시 Unity가 코루틴을 강제 종료 — finally가 안 타므로 여기서 원복
            Stop();
        }

        IEnumerator RunMeasurement()
        {
            isRunning = true;
            // 초기 구간 예외도 isRunning 고착을 막아야 하므로 본문 전체를 try로 감싼다
            var sb = new StringBuilder();
            try
            {
                if (setup == null) setup = GetComponent<CharacterPhysicsSetup>();
                var cloths = setup != null ? setup.generatedCloths
                    .Where(c => c != null && c.IsValid()).ToList() : null;
                if (cloths == null || cloths.Count == 0)
                {
                    sb.Append("[SelfCollisionPerfProbe] 유효 클로스 없음");
                    yield break;
                }

                // 원래 설정 백업 — BoneSpring은 selfMode가 강제 None이라 측정 대상에서 제외
                savedParams = cloths.Where(c => !c.SerializeData.IsBoneSpring())
                    .Select(c => (c, c.SerializeData.selfCollisionConstraint.selfMode,
                        c.SerializeData.selfCollisionConstraint.syncMode)).ToList();
                paramsPatched = true;

                // 계측 중 외부가 공용 필드를 바꿔도 일관되도록 스냅샷
                int measure = measureFrames, settle = settleFrames;
                sb.AppendLine($"[SelfCollisionPerfProbe] 대상 {savedParams.Count}클로스");
                recorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Scripts, "StartClothUpdate.Time", 1);
                if (!recorder.Valid)
                    sb.AppendLine("경고: StartClothUpdate 마커 레코더 무효 — 마커값 0 기록");
                // ON: 저장된 원래 설정 그대로 먼저 측정
                yield return Measure("ON", sb, measure, settle);
                // OFF: 자기·상호 충돌을 끄고 동일 프레임 계측
                foreach (var (c, _, _) in savedParams)
                {
                    if (c == null) continue;
                    var p = c.SerializeData.selfCollisionConstraint;
                    p.selfMode = SelfCollisionConstraint.SelfCollisionMode.None;
                    p.syncMode = SelfCollisionConstraint.SelfCollisionMode.None;
                    c.SetParameterChange();
                }
                yield return Measure("OFF", sb, measure, settle);
            }
            finally
            {
                RestoreParams();
                if (recorder.Valid) recorder.Dispose();
                recorder = default;
                lastSummary = sb.ToString();
                Debug.Log(lastSummary, this);
                isRunning = false;
                routine = null;
            }
        }

        /// <summary>계측이 바꾼 selfMode/syncMode를 백업값으로 원복한다.</summary>
        void RestoreParams()
        {
            if (!paramsPatched) return;
            paramsPatched = false;
            if (savedParams == null) return;
            foreach (var (c, self, sync) in savedParams)
            {
                // 계측 중 클로스가 재생성으로 파괴됐을 수 있으므로 생존 분만 원복
                if (c == null) continue;
                var p = c.SerializeData.selfCollisionConstraint;
                p.selfMode = self;
                p.syncMode = sync;
                c.SetParameterChange();
            }
            savedParams = null;
        }

        IEnumerator Measure(string tag, StringBuilder sb, int measure, int settle)
        {
            sumNs = maxNs = 0;
            countedFrames = 0;
            // 에디터에서 StartClothUpdate 마커는 잡스케줄 비용만 잡아 0에 수렴하므로
            // 프레임 시간(deltaTime)을 병행 누적해 전체 비용 차이를 본다.
            double dtSum = 0, dtMax = 0;
            for (int i = 0; i < settle; i++)
                yield return null;
            for (int i = 0; i < measure; i++)
            {
                yield return null;
                double ns = recorder.Valid ? recorder.LastValue : 0;
                if (ns > 0)
                {
                    sumNs += ns;
                    if (ns > maxNs) maxNs = ns;
                    countedFrames++;
                }
                double dt = Time.unscaledDeltaTime * 1000.0;
                dtSum += dt;
                if (dt > dtMax) dtMax = dt;
            }
            sb.AppendLine($"{tag}: simMean={(countedFrames > 0 ? sumNs / countedFrames / 1e6 : 0):F3}ms " +
                $"simMax={maxNs / 1e6:F3}ms frames={countedFrames} " +
                $"frameMean={dtSum / measure:F2}ms frameMax={dtMax:F1}ms");
        }
    }
}
