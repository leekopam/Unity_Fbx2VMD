using System.Collections.Generic;
using System.Text;
using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 재생 중 머리카락 본과 신체 콜라이더의 침투를 측정하는 검증 프로브.
    /// 매 N프레임마다 모든 체인 본의 각 콜라이더에 대한 부호 거리를 계산하고
    /// 최대 침투량·발생 프레임·가해 콜라이더를 기록한다.
    /// 두피 근접 본은 콜라이더 내부가 정상이므로 초기 프레임을 기저선으로 삼아
    /// 움직임 중 기저선을 초과한 침투만 경고한다.
    ///
    /// 주의: BoneCloth의 파티클은 본 위에 놓이므로 본 위치 침투를 측정한다.
    /// 대칭 콜라이더의 미러 측은 별도로 확장 계산한다.
    /// </summary>
    public class PhysicsValidationProbe : MonoBehaviour
    {
        [Tooltip("비워두면 같은 오브젝트의 CharacterPhysicsSetup 사용")]
        public CharacterPhysicsSetup setup;

        [Tooltip("샘플링 간격 (프레임)")]
        [Range(1, 30)]
        public int sampleInterval = 5;

        [Tooltip("이 깊이(m) 이상 침투 시 경고 기록")]
        public float penetrationWarningDepth = 0.005f;

        [Tooltip("루트에서 이 깊이 미만의 체인 본은 측정 제외. 두피 근접 구간은 콜라이더 내부 위치가 정상이라 오탐 방지용")]
        [Range(1, 5)]
        public int minChainDepth = 2;

        [Tooltip("연속 이 횟수(샘플) 이상 경고 깊이 초과 시 걸림 의심으로 집계")]
        [Range(2, 20)]
        public int stuckSampleThreshold = 3;

        [Header("결과")]
        public float worstPenetration;       // 가장 깊은 침투 (m, 음수=없음이 아니라 깊이 양수)
        public int worstFrame = -1;
        [System.NonSerialized] public string worstInfo = "";
        public Vector3 worstPoint;           // 최악 침투 발생 위치 (기즈모 표시용)
        public int penetrationFrameCount;    // 경고 깊이 이상 침투가 발생한 프레임 수
        public int sampledFrames;
        // 기저선 자체의 최대 침투 — 시작 자세가 이미 콜라이더에 파고든 경우
        // 초과분 기반이라 경고에 잡히지 않으므로 별도 보고해 기저선 오염을 노출한다.
        public float baselineWorstPenetration;
        // 걸림 지표: 머리카락 본이 콜라이더 안에 연속 머문 정도 — 손발 꼬임의 수치 대용
        public int maxConsecutivePenetrationSamples; // 한 본의 최장 연속 침투 샘플 수
        public int suspectedStuckBoneCount;          // 임계 이상 연속 침투한 본 수

        readonly List<(Transform bone, MagicaCloth cloth)> trackedBones =
            new List<(Transform, MagicaCloth)>();
        // 각 본의 초기(바인드) 침투 깊이 — 두피 근접 본은 콜라이더 내부가 정상이라
        // 초과분(depth - baseline)만 경고 대상으로 본다.
        readonly List<float> baselines = new List<float>();
        readonly List<int> consecutivePenetrations = new List<int>(); // 본별 연속 침투 샘플 수
        readonly HashSet<int> stuckBoneIndexes = new HashSet<int>();
        int frame;
        bool tracking;

        void Start()
        {
            if (setup == null)
                setup = GetComponent<CharacterPhysicsSetup>();
            RebuildTracking();
        }

        /// <summary>
        /// 추적 대상(모든 머리카락 체인 본)을 다시 수집한다.
        /// 자동 설정 이후 또는 애니메이션 변경 시 호출.
        /// </summary>
        [ContextMenu("추적 대상 재수집")]
        public void RebuildTracking()
        {
            trackedBones.Clear();
            if (setup == null)
                return;
            foreach (var cloth in setup.generatedCloths)
            {
                if (cloth == null)
                    continue;
                foreach (var root in cloth.SerializeData.rootBones)
                {
                    if (root == null)
                        continue;
                    // 루트 본은 고정 파티클이라 콜라이더 내부 위치가 정상 — 자식부터 추적
                    // 두피 근접 구간(minChainDepth 미만)도 정상 내부 위치라 제외
                    var tmp = new List<Transform>();
                    CollectChainBones(root, minChainDepth, tmp);
                    foreach (var t in tmp)
                        trackedBones.Add((t, cloth));
                }
            }
            baselines.Clear();
            consecutivePenetrations.Clear();
            stuckBoneIndexes.Clear();
            for (int i = 0; i < trackedBones.Count; i++)
            {
                baselines.Add(float.NaN);
                consecutivePenetrations.Add(0);
            }
            tracking = trackedBones.Count > 0;
        }

        /// <summary>
        /// 루트의 하위 체인 본을 수집한다. depth는 루트의 직접 자식이 1.
        /// minDepth 미만(두피 근접 구간)은 제외한다.
        /// </summary>
        public static void CollectChainBones(Transform root, int minDepth, List<Transform> output)
        {
            if (root == null || output == null)
                return;
            var stack = new Stack<(Transform t, int depth)>();
            for (int i = root.childCount - 1; i >= 0; i--)
                stack.Push((root.GetChild(i), 1));
            while (stack.Count > 0)
            {
                var (t, d) = stack.Pop();
                if (d >= minDepth)
                    output.Add(t);
                for (int i = t.childCount - 1; i >= 0; i--)
                    stack.Push((t.GetChild(i), d + 1));
            }
        }

        void LateUpdate()
        {
            if (!tracking)
                return;
            frame++;
            if (frame % sampleInterval != 0)
                return;

            sampledFrames++;
            bool penetratedThisFrame = false;

            for (int i = 0; i < trackedBones.Count; i++)
            {
                var (bone, cloth) = trackedBones[i];
                Vector3 p = bone.position;
                float maxDepth = 0f;
                string maxCol = "";
                foreach (var col in cloth.SerializeData.colliderCollisionConstraint.colliderList)
                {
                    if (col == null)
                        continue;
                    float depth = MeasurePenetration(p, col);
                    if (depth > maxDepth)
                    {
                        maxDepth = depth;
                        maxCol = col.name;
                    }
                }
                if (float.IsNaN(baselines[i]))
                {
                    baselines[i] = maxDepth; // 초기 프레임을 정적 기저선으로 채택
                    if (maxDepth > baselineWorstPenetration)
                        baselineWorstPenetration = maxDepth;
                    continue;
                }
                float excess = maxDepth - baselines[i];
                if (excess >= penetrationWarningDepth)
                {
                    consecutivePenetrations[i]++;
                    if (consecutivePenetrations[i] > maxConsecutivePenetrationSamples)
                        maxConsecutivePenetrationSamples = consecutivePenetrations[i];
                    if (consecutivePenetrations[i] >= stuckSampleThreshold)
                        stuckBoneIndexes.Add(i);
                    penetratedThisFrame = true;
                }
                else
                {
                    consecutivePenetrations[i] = 0;
                }
                if (excess > worstPenetration)
                {
                    worstPenetration = excess;
                    worstFrame = frame;
                    worstPoint = p;
                    worstInfo = $"{bone.name} -> {maxCol} ({cloth.name}) " +
                        $"[기저 {baselines[i] * 1000f:F0}mm]";
                }
                suspectedStuckBoneCount = stuckBoneIndexes.Count;
            }
            if (penetratedThisFrame)
                penetrationFrameCount++;
        }

        /// <summary>본 위치가 콜라이더 안으로 들어간 깊이(m). 양수=침투.</summary>
        public static float MeasurePenetration(Vector3 point, ColliderComponent col)
        {
            float scale = col.transform.lossyScale.x; // MC2 콜라이더는 균일 스케일 전제
            if (col is MagicaSphereCollider sphere)
            {
                Vector3 c = sphere.transform.TransformPoint(sphere.center);
                float r = sphere.GetSize().x * scale;
                float sd = ColliderMath.SignedDistanceSphere(point, c, r);
                float d = -sd;
                // 대칭 콜라이더의 미러 측 검사
                float md = MirrorPenetration(point, col);
                return Mathf.Max(d, md);
            }
            if (col is MagicaCapsuleCollider cap)
            {
                var size = cap.GetSize(); // (startR, endR, len)
                Vector3 localDir = cap.GetLocalDir();
                Vector3 center = cap.center;
                Vector3 aLocal = center - localDir * (size.z * 0.5f);
                Vector3 bLocal = center + localDir * (size.z * 0.5f);
                Vector3 a = cap.transform.TransformPoint(aLocal);
                Vector3 b = cap.transform.TransformPoint(bLocal);
                float r = Mathf.Max(size.x, size.y) * scale;
                float sd = ColliderMath.SignedDistanceCapsule(point, a, b, r);
                float md = MirrorPenetration(point, col);
                return Mathf.Max(-sd, md);
            }
            return 0f;
        }

        /// <summary>
        /// MC2 대칭 콜라이더의 월드 포즈를 계산한다.
        /// ColliderManager.UpdateColliders의 시뮬레이션 수식을 재현한다:
        /// 소스 콜라이더의 로컬 포즈를 대칭 축 기준으로 반사하고
        /// ActiveSymmetryTarget의 월드 포즈를 부모로 사용한다.
        /// </summary>
        public static bool TryComputeSymmetryPose(
            ColliderComponent col,
            out Vector3 worldPos,
            out Quaternion worldRot,
            out Vector3 worldScale)
        {
            if (col == null)
            {
                worldPos = default;
                worldRot = default;
                worldScale = default;
                return false;
            }
            // ActiveSymmetryMode는 MC2 런타임이 계산하기 전까지 null — 미해석은 None과 동일하게 취급한다.
            return TryComputeSymmetryPose(
                col.ActiveSymmetryMode ?? ColliderSymmetryMode.None,
                col.transform, col.ActiveSymmetryTarget,
                out worldPos, out worldRot, out worldScale);
        }

        /// <summary>대칭 모드·소스 포즈·대칭 타겟으로 대칭 콜라이더 월드 포즈를 계산한다.</summary>
        public static bool TryComputeSymmetryPose(
            ColliderSymmetryMode mode,
            Transform src,
            Transform target,
            out Vector3 worldPos,
            out Quaternion worldRot,
            out Vector3 worldScale)
        {
            worldPos = default;
            worldRot = default;
            worldScale = default;
            if (target == null || src == null || mode == ColliderSymmetryMode.None)
                return false;

            Vector3 lpos = src.localPosition;
            Vector3 lrotDeg = src.localEulerAngles;
            switch (mode)
            {
                case ColliderSymmetryMode.X_Symmetry:
                    lpos.x = -lpos.x;
                    lrotDeg.y = -lrotDeg.y;
                    lrotDeg.z = -lrotDeg.z;
                    break;
                case ColliderSymmetryMode.Y_Symmetry:
                    lpos.y = -lpos.y;
                    lrotDeg.x = -lrotDeg.x;
                    lrotDeg.z = -lrotDeg.z;
                    break;
                case ColliderSymmetryMode.Z_Symmetry:
                    lpos.z = -lpos.z;
                    lrotDeg.x = -lrotDeg.x;
                    lrotDeg.y = -lrotDeg.y;
                    break;
                case ColliderSymmetryMode.XYZ_Symmetry:
                    lpos = -lpos;
                    break;
                default:
                    // AutomaticHumanBody/AutomaticTarget은 런타임에 구체 축으로 해석됨.
                    // 해석되지 않은 상태면 대칭 인스턴스가 없으므로 측정 대상 아님.
                    return false;
            }

            Vector3 pscl = target.lossyScale;
            Vector3 sclSign = new Vector3(
                Mathf.Sign(pscl.x), Mathf.Sign(pscl.y), Mathf.Sign(pscl.z));
            Vector3 sclEulerSign = Vector3.one;
            if (pscl.x < 0f || pscl.y < 0f || pscl.z < 0f)
                sclEulerSign = -sclSign;

            Quaternion prot = target.rotation;
            worldPos = prot * Vector3.Scale(pscl, lpos) + target.position;
            worldRot = prot * Quaternion.Euler(Vector3.Scale(lrotDeg, sclEulerSign));
            worldScale = Vector3.Scale(pscl, src.localScale);
            return true;
        }

        /// <summary>대칭 콜라이더 로컬 공간 점을 월드로 변환한다 (MC2 center 오프셋 규칙 재현).</summary>
        static Vector3 SymmetryLocalToWorld(
            Vector3 local, Vector3 wpos, Quaternion wrot, Vector3 wscl, Vector3 sclSign)
        {
            return wpos + Vector3.Scale(
                wrot * Vector3.Scale(local, sclSign),
                Vector3.Scale(wscl, sclSign));
        }

        // 대칭 콜라이더 인스턴스에 대한 침투 검사 — MC2의 대칭 포즈 수식을 재현해 측정한다.
        static float MirrorPenetration(Vector3 point, ColliderComponent col)
        {
            if (!TryComputeSymmetryPose(col, out Vector3 wpos, out Quaternion wrot, out Vector3 wscl))
                return 0f;

            Transform target = col.ActiveSymmetryTarget;
            Vector3 pscl = target.lossyScale;
            var sclSign = new Vector3(
                Mathf.Sign(pscl.x), Mathf.Sign(pscl.y), Mathf.Sign(pscl.z));
            float radiusScale = Mathf.Max(
                Mathf.Abs(wscl.x), Mathf.Abs(wscl.y), Mathf.Abs(wscl.z));

            if (col is MagicaSphereCollider sphere)
            {
                // 소스 center의 대칭 축 반사 (ColliderManager와 동일 규칙)
                Vector3 c = MirrorLocalOffset(
                    col.ActiveSymmetryMode ?? ColliderSymmetryMode.None, sphere.center);
                Vector3 cWorld = SymmetryLocalToWorld(c, wpos, wrot, wscl, sclSign);
                float r = sphere.GetSize().x * radiusScale;
                return -ColliderMath.SignedDistanceSphere(point, cWorld, r);
            }
            if (col is MagicaCapsuleCollider cap)
            {
                var size = cap.GetSize(); // (startR, endR, len)
                Vector3 localDir = cap.GetLocalDir();
                Vector3 center = MirrorLocalOffset(
                    col.ActiveSymmetryMode ?? ColliderSymmetryMode.None, cap.center);
                // 캡슐은 축 대칭 도형이라 방향 반전 플래그는 형상에 영향 없음
                Vector3 aLocal = center - localDir * (size.z * 0.5f);
                Vector3 bLocal = center + localDir * (size.z * 0.5f);
                Vector3 a = SymmetryLocalToWorld(aLocal, wpos, wrot, wscl, sclSign);
                Vector3 b = SymmetryLocalToWorld(bLocal, wpos, wrot, wscl, sclSign);
                float r = Mathf.Max(size.x, size.y) * radiusScale;
                return -ColliderMath.SignedDistanceCapsule(point, a, b, r);
            }
            return 0f;
        }

        /// <summary>콜라이더 로컬 오프셋(center)을 대칭 축 기준으로 반사한다.</summary>
        static Vector3 MirrorLocalOffset(ColliderSymmetryMode mode, Vector3 v)
        {
            switch (mode)
            {
                case ColliderSymmetryMode.X_Symmetry: v.x = -v.x; break;
                case ColliderSymmetryMode.Y_Symmetry: v.y = -v.y; break;
                case ColliderSymmetryMode.Z_Symmetry: v.z = -v.z; break;
                case ColliderSymmetryMode.XYZ_Symmetry: v = -v; break;
            }
            return v;
        }

        /// <summary>측정 결과를 문자열로 반환한다.</summary>
        [ContextMenu("리포트 출력")]
        public void Report()
        {
            Debug.Log(BuildReport(), this);
        }

        public string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[PhysicsValidationProbe] {sampledFrames}프레임 샘플링");
            sb.AppendLine($"최대 초과 침투(기저선 대비): {worstPenetration * 1000f:F1}mm @프레임 {worstFrame} ({worstInfo})");
            sb.AppendLine($"경고 프레임 수: {penetrationFrameCount}");
            sb.AppendLine($"기저선 최대 침투(시작 자세): {baselineWorstPenetration * 1000f:F1}mm");
            sb.AppendLine($"걸림 지표: 최장 연속 침투 {maxConsecutivePenetrationSamples}샘플 " +
                $"(≈{maxConsecutivePenetrationSamples * sampleInterval}프레임), " +
                $"걸림 의심 본 {suspectedStuckBoneCount}개");
            if (baselineWorstPenetration >= penetrationWarningDepth)
                sb.AppendLine("경고: 시작 자세에서 이미 콜라이더 내부에 파고든 본이 있습니다. " +
                    "초과 침투 0이어도 초기 자세는 불량일 수 있으니 바인드 포즈를 확인하세요.");
            return sb.ToString();
        }

        // 씬뷰에 생성 콜라이더(녹색)와 최악 침투 지점(적색)을 기즈모로 표시
        void OnDrawGizmos()
        {
            var s = setup != null ? setup : GetComponent<CharacterPhysicsSetup>();
            if (s == null)
                return;
            Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
            foreach (var col in s.generatedColliders)
            {
                if (col == null)
                    continue;
                DrawCollider(col);
            }
            if (worstFrame >= 0 && worstPenetration > 0f)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
                Gizmos.DrawSphere(worstPoint, 0.01f);
            }
        }

        static void DrawCollider(ColliderComponent col)
        {
            float scale = col.transform.lossyScale.x;
            if (col is MagicaSphereCollider sphere)
            {
                Vector3 c = sphere.transform.TransformPoint(sphere.center);
                Gizmos.DrawWireSphere(c, sphere.GetSize().x * scale);
            }
            else if (col is MagicaCapsuleCollider cap)
            {
                var size = cap.GetSize();
                Vector3 dir = cap.GetLocalDir();
                Vector3 a = cap.transform.TransformPoint(cap.center - dir * size.z * 0.5f);
                Vector3 b = cap.transform.TransformPoint(cap.center + dir * size.z * 0.5f);
                float r = Mathf.Max(size.x, size.y) * scale;
                Gizmos.DrawWireSphere(a, r);
                Gizmos.DrawWireSphere(b, r);
                Gizmos.DrawLine(a + Vector3.up * r, b + Vector3.up * r);
                Gizmos.DrawLine(a - Vector3.up * r, b - Vector3.up * r);
            }
        }

        /// <summary>기록 초기화. 기저선도 함께 재무장해 다음 샘플 프레임에서 다시 채택한다 —
        /// 자세·콜라이더 변경 후 초기화 시 낡은 기저선과 비교해 침투가 누락되는 것을 막는다.</summary>
        [ContextMenu("기록 초기화")]
        public void ResetStats()
        {
            worstPenetration = 0f;
            worstFrame = -1;
            worstInfo = "";
            penetrationFrameCount = 0;
            sampledFrames = 0;
            baselineWorstPenetration = 0f;
            maxConsecutivePenetrationSamples = 0;
            suspectedStuckBoneCount = 0;
            stuckBoneIndexes.Clear();
            for (int i = 0; i < consecutivePenetrations.Count; i++)
                consecutivePenetrations[i] = 0;
            frame = 0;
            for (int i = 0; i < baselines.Count; i++)
                baselines[i] = float.NaN;
        }
    }
}
