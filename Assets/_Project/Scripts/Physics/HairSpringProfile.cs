using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 머리카락 다이내믹스 백엔드 선택.
    /// MagicaCloth2 = 현재 구현(기본). SpringBones = 외부 본 스프링 컴포넌트 경로
    /// (에셋이 프로젝트에 있을 때만 동작, 없으면 MC2로 폴백).
    /// </summary>
    public enum HairDynamicsBackend
    {
        MagicaCloth2 = 0,
        SpringBones = 1,
    }

    /// <summary>
    /// 본 스프링 식 깊이 커브 — 외부 본 스프링 에셋의 Chain.CurveType과 동일한 의미·순서.
    /// t는 체인 깊이(0=루트, 1=끝). 외부 본 스프링 에셋 적용 시 (int)캐스팅이 그대로 통한다.
    /// </summary>
    public enum SpringCurveType
    {
        ConstantOne = 0,
        ConstantHalf = 1,
        ConstantZero = 2,
        RootOneTailHalf = 3,
        RootOneTailZero = 4,
        RootHalfTailOne = 5,
        RootZeroTailOne = 6,
        Custom = 7,
    }

    /// <summary>
    /// 깊이 방향 값 하나. 커브 타입 프리셋 또는 Custom AnimationCurve.
    /// </summary>
    [System.Serializable]
    public struct DepthSpringCurve
    {
        public SpringCurveType type;
        public AnimationCurve customCurve;

        public DepthSpringCurve(SpringCurveType type)
        {
            this.type = type;
            customCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        }

        /// <summary>외부 본 스프링 에셋 Chain.EvaluateCurve와 동일한 평가식.</summary>
        public float Evaluate(float t)
        {
            switch (type)
            {
                case SpringCurveType.ConstantOne: return 1f;
                case SpringCurveType.ConstantHalf: return 0.5f;
                case SpringCurveType.ConstantZero: return 0f;
                case SpringCurveType.RootOneTailHalf: return 1f - 0.5f * Mathf.Clamp01(t);
                case SpringCurveType.RootOneTailZero: return 1f - Mathf.Clamp01(t);
                case SpringCurveType.RootHalfTailOne: return 0.5f + 0.5f * Mathf.Clamp01(t);
                case SpringCurveType.RootZeroTailOne: return Mathf.Clamp01(t);
                case SpringCurveType.Custom:
                    return customCurve != null ? customCurve.Evaluate(t) : 0f;
                default: return 0f;
            }
        }

        /// <summary>깊이 전체의 평균값 — 단일 스칼라로 바꿔야 할 때 사용.</summary>
        public float Average(int samples = 8)
        {
            if (samples <= 1) return Evaluate(0f);
            float sum = 0f;
            for (int i = 0; i < samples; i++)
                sum += Evaluate(i / (float)(samples - 1));
            return sum / samples;
        }
    }

    /// <summary>
    /// 외부 본 스프링 의미론의 머리카락 다이내믹스 프로파일.
    /// MC2 경로에서는 ApplyToMc2가 각 필드를 MC2 파라미터로 근사 변환하고,
    /// 외부 본 스프링 에셋 경로에서는 필드가 1:1로 대응해 그대로 주입된다.
    /// enabled=false가 기본 — 기존 HairTuning/프리셋 경로를 그대로 유지한다.
    /// </summary>
    [System.Serializable]
    public struct HairSpringProfile
    {
        [Tooltip("본 스프링 식 파라미터를 MC2에 반영할지. 꺼두면 기존 프리셋+tuning 경로 그대로")]
        public bool enabled;

        [Header("스프링 응답")]
        [Range(0f, 10f)]
        [Tooltip("스프링 고유 진동수(Hz). 클수록 애니메이션 포즈로 빨리 복귀. 본 스프링 기본 5")]
        public float frequency;

        [Range(0f, 1.5f)]
        [Tooltip("감쇠비. 0=감쇠 없음(계속 출렁), 1=임계 감쇠(오버슈트 없이 정착). 본 스프링 기본 0.5")]
        public float dampingRatio;

        [Header("깊이 커브 (0=루트, 1=끝)")]
        [Tooltip("애니메이션 혼합 — 1이면 해당 깊이는 애니메이션 포즈 100% 추종. " +
                 "RootOneTailZero가 '뿌리 고정 + 끝만 물리'의 표준")]
        public DepthSpringCurve animationBlend;

        [Tooltip("포즈 유지 강성 — 부모 스프링 기준 로컬 자세 유지율. 높으면 형태가 덜 무너짐")]
        public DepthSpringCurve poseStiffness;

        [Tooltip("굽힘 상한 커브 — maxBendAngleDeg에 곱해지는 비율")]
        public DepthSpringCurve bendAngleCap;

        [Range(0f, 180f)]
        [Tooltip("루트 축 기준 최대 굽힘각(도). 180=제한 없음")]
        public float maxBendAngleDeg;

        [Tooltip("충돌 반경 커브 — maxCollisionRadius에 곱해지는 비율")]
        public DepthSpringCurve collisionRadius;

        [Tooltip("본당 최대 충돌 반경(m, 체형 단위 기준 스케일됨). 상호/자기 충돌 크기")]
        public float maxCollisionRadius;

        [Header("외력")]
        [Tooltip("중력 가속도(m/s²). 본 스프링 기본 0 — 쳐짐은 애니메이션 포즈 자체가 담당")]
        public float gravity;

        [Tooltip("체인끼리 구-구 상호 충돌. MC2에서는 클로스별 FullMesh 상호충돌로 변환")]
        public bool interChainCollision;

        public static HairSpringProfile Default => new HairSpringProfile
        {
            enabled = false,
            frequency = 5f,
            dampingRatio = 0.5f,
            animationBlend = new DepthSpringCurve(SpringCurveType.RootOneTailZero),
            poseStiffness = new DepthSpringCurve(SpringCurveType.ConstantZero),
            bendAngleCap = new DepthSpringCurve(SpringCurveType.ConstantOne),
            maxBendAngleDeg = 180f,
            collisionRadius = new DepthSpringCurve(SpringCurveType.RootOneTailZero),
            maxCollisionRadius = 0.02f,
            gravity = 0f,
            interChainCollision = false,
        };

        /// <summary>
        /// 프로파일을 MC2 ClothSerializeData로 근사 변환한다.
        /// 부위 템플릿 적용 이후 호출 — 백스톱/텔레포트 등 구조적 안전값은 템플릿 것을 유지.
        /// MC2와 본 스프링은 모델이 달라(제약 수렴 vs 스프링 추적) 각 항목은 근사다.
        /// </summary>
        public void ApplyToMc2(ClothSerializeData s, float unit)
        {
            if (!enabled || s == null)
                return;

            // 진동수 → 복원 강성 근사 (10Hz = 최대 강성). 포즈 강성 커브로 깊이별 차등.
            var rest = s.angleRestorationConstraint;
            rest.useAngleRestoration = true;
            float baseStiffness = Mathf.Clamp01(frequency / 10f);
            rest.stiffness.SetValue(baseStiffness, BuildCurve(poseStiffness));
            // 감쇠비 → 속도 감쇠. ζ=1(임계)이면 최대 감쇠.
            rest.velocityAttenuation = Mathf.Clamp01(dampingRatio);
            // 전역 감쇠 — MC2 damping은 프리셋 0.1 기준. ζ=0.5 → 0.1이 되도록 근사.
            s.damping.SetValue(Mathf.Clamp01(dampingRatio) * 0.2f);

            // 애니메이션 혼합 평균 → animationPoseRatio (MC2는 본별 커브가 없어 평균 사용)
            s.animationPoseRatio = Mathf.Clamp01(animationBlend.Average());

            // 굽힘 상한 → angleLimit (커브 = 최대각 대비 비율)
            var limit = s.angleLimitConstraint;
            if (maxBendAngleDeg < 179.5f)
            {
                limit.useAngleLimit = true;
                limit.limitAngle.SetValue(maxBendAngleDeg, BuildCurve(bendAngleCap));
            }
            else
            {
                limit.useAngleLimit = false;
            }

            // 본 반경 → 파티클 굵기 (충돌·상호충돌 크기에 그대로 반영)
            s.radius.SetValue(
                maxCollisionRadius * unit, BuildCurve(collisionRadius));

            // 본 스프링은 중력이 독립 파라미터 — 절대값으로 덮어쓴다
            s.gravity = gravity;
            s.gravityDirection = Vector3.down;

            // 자기 충돌 두께도 본 반경 기준으로 정렬 (켜져 있을 때만 덮어씀)
            if (s.selfCollisionConstraint.selfMode != SelfCollisionConstraint.SelfCollisionMode.None)
            {
                s.selfCollisionConstraint.surfaceThickness.SetValue(
                    maxCollisionRadius * unit * 0.5f, BuildCurve(collisionRadius));
            }
        }

        /// <summary>깊이 커브를 MC2 CurveSerializeData용 AnimationCurve로 변환한다.</summary>
        AnimationCurve BuildCurve(DepthSpringCurve c)
        {
            if (c.type == SpringCurveType.Custom && c.customCurve != null)
                return c.customCurve;
            return new AnimationCurve(
                new Keyframe(0f, c.Evaluate(0f)),
                new Keyframe(0.5f, c.Evaluate(0.5f)),
                new Keyframe(1f, c.Evaluate(1f)));
        }
    }
}
