using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 인스펙터에서 노출하는 머리카락 흔들림 조정값.
    /// 템플릿 적용 후 배율로 곱해지며, 모든 값 1.0이면 프리셋 그대로.
    /// </summary>
    [System.Serializable]
    public struct HairTuning
    {
        [Range(0f, 2f)]
        [Tooltip("흔들림 강도. 1=표준, 0=거의 고정, 2=많이 흔들림")]
        public float sway;

        [Range(0f, 2f)]
        [Tooltip("감쇠 배율. 클수록 흔들림이 빨리 멈춤")]
        public float dampingScale;

        [Range(0f, 2f)]
        [Tooltip("중력 배율. 클수록 더 무겁게 처짐")]
        public float gravityScale;

        [Range(0.5f, 2f)]
        [Tooltip("파티클 굵기 배율. 크면 관통에 안전하지만 머리카락이 몸에서 뜸")]
        public float radiusScale;

        [Range(0.5f, 1.5f)]
        [Tooltip("관성 배율. 작으면 캐릭터 급동작 시 머리카락이 덜 따라옴")]
        public float inertiaScale;

        [Header("깊이 커브")]
        [Range(0f, 2f)]
        [Tooltip("복원 강성 커브의 끝단(깊이 1.0) 배율. 작으면 끝이 더 자유롭게 흔들림")]
        public float tipStiffnessScale;

        [Range(0f, 2f)]
        [Tooltip("감쇠 커브의 끝단(깊이 1.0) 배율")]
        public float tipDampingScale;

        [Header("포즈/관성")]
        [Range(0f, 1f)]
        [Tooltip("애니메이션 포즈 반영률. 0=완전 시뮬레이션, 1=애니메이션 포즈 복원 기준. " +
                 "VMD처럼 포즈가 확정된 클립은 0.5 전후가 안정적")]
        public float animationPoseRatio;

        [Tooltip("깊이 관성을 수동 설정 — 체인 깊이에 따른 관성 감쇠. 켜면 아래 값이 프리셋을 덮어씀")]
        public bool overrideDepthInertia;

        [Range(0f, 1f)]
        [Tooltip("깊이 관성 — 클수록 체인 깊은 곳의 월드 관성 영향을 줄임")]
        public float depthInertia;

        [Header("꼬임 방지")]
        [Tooltip("같은 클로스 내 체인끼리의 자기 충돌 — 긴 머리카락 다발이 서로 엉키는 걸 막는다. 계산 부하 증가")]
        public bool useSelfCollision;

        [Range(0.5f, 2f)]
        [Tooltip("자기 충돌 두께 배율. 크면 다발 사이 간격이 넓어짐")]
        public float selfCollisionScale;

        [Tooltip("부위별 클로스끼리 상호 충돌 — 루트 본이 가장 많은 클로스를 앵커로 별형 연결")]
        public bool useMutualCollision;

        [Header("캐릭터 프리셋")]
        [Tooltip("캐릭터 전용 프리셋 키. 지정하면 Resources/PhysicsPresets/Character/" +
                 "{키}_{클로스명|프리셋명}.json을 공식 프리셋보다 우선 로드")]
        public string characterPresetKey;

        public static HairTuning Default => new HairTuning
        {
            sway = 1f, dampingScale = 1f, gravityScale = 1f, radiusScale = 1f, inertiaScale = 1f,
            tipStiffnessScale = 1f, tipDampingScale = 1f, animationPoseRatio = 0f,
            overrideDepthInertia = false, depthInertia = 0.7f,
            useSelfCollision = true, selfCollisionScale = 1f, useMutualCollision = true,
            characterPresetKey = null
        };
    }

    /// <summary>
    /// 부위별 MC2 BoneCloth 파라미터 템플릿.
    /// MC2 공식 프리셋(LongHair/ShortHair/FrontHair/Accessory)의 실측값을 기준으로
    /// 관통·꼬임 억제용 조정값(백스톱/텔레포트/Edge)을 얹는다.
    /// 거리 단위 파라미터는 unit(=상반신길이/0.6m)에 비례해 스케일한다.
    /// </summary>
    public static class HairPhysicsParameters
    {
        /// <summary>
        /// 공통 안전값: 프리셋 공통 + 텔레포트 Keep(폭주 방지), 낮은 마찰(팔 스침 시 미끄러짐).
        /// </summary>
        static void ApplyCommon(ClothSerializeData s, float unit,
            float moveLimit, float rotLimit, float particleLimit)
        {
            var inertia = s.inertiaConstraint;
            inertia.worldInertia = 1.0f;
            inertia.movementInertiaSmoothing = 0.4f;
            inertia.movementSpeedLimit.SetValue(true, moveLimit * unit);
            inertia.rotationSpeedLimit.SetValue(true, rotLimit);
            inertia.localInertia = 1.0f;
            inertia.particleSpeedLimit.SetValue(true, particleLimit * unit);
            // 프리셋은 None+0.5m/90°이나, Keep이 순간이동 후 폭주에 안전
            inertia.teleportMode = InertiaConstraint.TeleportMode.Keep;
            inertia.teleportDistance = 0.5f * unit;
            inertia.teleportRotation = 90f;

            var collision = s.colliderCollisionConstraint;
            collision.friction = 0.05f;
        }

        /// <summary>긴 머리(뒷머리/트윈테일) — MC2 LongHair 프리셋 기준.</summary>
        public static void ApplyLongHair(ClothSerializeData s, float unit)
        {
            s.gravity = 5.0f;
            s.gravityDirection = Vector3.down;
            s.gravityFalloff = 0.8f;
            s.damping.SetValue(0.1f);
            s.radius.SetValue(0.02f * unit);

            s.tetherConstraint.distanceCompression = 0.4f;
            s.distanceConstraint.stiffness.SetValue(1.0f);

            var rest = s.angleRestorationConstraint;
            rest.useAngleRestoration = true;
            rest.stiffness.SetValue(0.2f, 1.0f, 0.11f); // 끝으로 갈수록 약하게
            rest.velocityAttenuation = 0.8f;
            rest.gravityFalloff = 0.5f;

            s.angleLimitConstraint.useAngleLimit = true;
            s.angleLimitConstraint.limitAngle.SetValue(75f, 0f, 1f);
            s.angleLimitConstraint.stiffness = 0.5f;

            // 긴 체인은 Edge 모드로 콜라이더 사이 빠짐 방지 (부하 높지만 정확)
            s.colliderCollisionConstraint.mode = ColliderCollisionConstraint.Mode.Edge;
            ApplyCommon(s, unit, 5.0f, 720f, 3.0f);
        }

        /// <summary>짧은 머리/옆머리 — MC2 ShortHair 프리셋 기준.</summary>
        public static void ApplyShortHair(ClothSerializeData s, float unit)
        {
            s.gravity = 2.0f;
            s.gravityDirection = Vector3.down;
            s.damping.SetValue(0.1f);
            s.radius.SetValue(0.02f * unit);

            s.tetherConstraint.distanceCompression = 0.2f;
            s.distanceConstraint.stiffness.SetValue(1.0f);

            var rest = s.angleRestorationConstraint;
            rest.useAngleRestoration = true;
            rest.stiffness.SetValue(0.15f, 1.0f, 0.3f);
            rest.velocityAttenuation = 0.6f;

            s.angleLimitConstraint.useAngleLimit = true;
            s.angleLimitConstraint.limitAngle.SetValue(45f, 0f, 1f);
            s.angleLimitConstraint.stiffness = 0.5f;

            // 짧은 체인은 Point 모드로 충분 (Edge보다 가벼움)
            s.colliderCollisionConstraint.mode = ColliderCollisionConstraint.Mode.Point;
            ApplyCommon(s, unit, 5.0f, 720f, 4.0f);
        }

        /// <summary>
        /// 앞머리 — MC2 FrontHair 프리셋 + 백스톱 활성화(얼굴 관통 방지, 콜라이더보다 가볍고 강함).
        /// normalAlignment는 머리 중심 방사형.
        /// </summary>
        public static void ApplyFrontHair(ClothSerializeData s, float unit, Transform headBone)
        {
            s.gravity = 4.0f;
            s.gravityDirection = Vector3.down;
            s.gravityFalloff = 0.8f;
            s.damping.SetValue(0.1f);
            s.radius.SetValue(0.02f * unit);

            s.tetherConstraint.distanceCompression = 0.1f;
            s.distanceConstraint.stiffness.SetValue(1.0f);

            var rest = s.angleRestorationConstraint;
            rest.useAngleRestoration = true;
            rest.stiffness.SetValue(0.15f, 1.0f, 0.3f);
            rest.velocityAttenuation = 0.7f;
            rest.gravityFalloff = 0.5f;

            // 얼굴 방향 꺾임 제한 15° — 관통의 주 원인 차단
            s.angleLimitConstraint.useAngleLimit = true;
            s.angleLimitConstraint.limitAngle.SetValue(15f, 0f, 1f);
            s.angleLimitConstraint.stiffness = 0.5f;

            var motion = s.motionConstraint;
            motion.useBackstop = true;
            motion.backstopRadius = 0.1f * unit;    // 백스톱 구 반경 (프리셋 기본 10은 비활성용)
            motion.backstopDistance.SetValue(0.005f * unit, 1.0f, 1.0f);
            motion.stiffness = 1.0f;

            if (headBone != null)
            {
                s.normalAlignmentSetting.alignmentMode = NormalAlignmentSettings.AlignmentMode.Transform;
                s.normalAlignmentSetting.adjustmentTransform = headBone;
            }

            s.colliderCollisionConstraint.mode = ColliderCollisionConstraint.Mode.Point;
            ApplyCommon(s, unit, 3.0f, 360f, 4.0f);
        }

        /// <summary>
        /// 리본·장식물 — MC2 Accessory 프리셋 기준 + 의상식 감쇠 램프.
        /// PMX 의상 강체(넥타이 실측)는 끝으로 갈수록 감쇠가 커져 팁이 과하게
        /// 출렁이지 않는다 — 머리카락과 반대 방향의 깊이 커브가 필요하다.
        /// </summary>
        public static void ApplyAccessory(ClothSerializeData s, float unit)
        {
            s.gravity = 0f;
            ApplyGarmentDamping(s);
            s.radius.SetValue(0.02f * unit);

            s.tetherConstraint.distanceCompression = 0.1f;
            s.distanceConstraint.stiffness.SetValue(1.0f);

            var rest = s.angleRestorationConstraint;
            rest.useAngleRestoration = true;
            rest.stiffness.SetValue(0.15f, 1.0f, 0.3f);
            rest.velocityAttenuation = 0.6f;

            s.angleLimitConstraint.useAngleLimit = true;
            s.angleLimitConstraint.limitAngle.SetValue(45f, 0f, 1f);
            s.angleLimitConstraint.stiffness = 0.5f;

            s.colliderCollisionConstraint.mode = ColliderCollisionConstraint.Mode.Point;
            ApplyCommon(s, unit, 5.0f, 720f, 4.0f);
        }

        /// <summary>
        /// 의상/장식물 전용 감쇠 램프 — 유효값 루트 0.10 → 끝 0.50 (value×커브).
        /// PMX 강체의 감쇠 형태(위치 감쇠 루트 0.10 → 끝 1.00)를 MC2 damping
        /// 통상 범위(0.03~0.5)로 환산한 근사. 스프링 프로파일이 damping을
        /// 평탄값으로 덮어쓰므로 Apply()에서 Accessory에 한해 재적용된다.
        /// </summary>
        internal static void ApplyGarmentDamping(ClothSerializeData s)
        {
            s.damping.SetValue(0.5f, 0.2f, 1.0f);
        }

        /// <summary>부위와 길이로 적절한 템플릿을 선택 적용한다.</summary>
        /// <param name="torsoLength">머리~골반 거리 (unit 기준값 산출용)</param>
        public static void Apply(HairPart part, ClothSerializeData sdata, float torsoLength,
            Transform headBone, bool isLong)
        {
            Apply(part, sdata, torsoLength, headBone, isLong, HairTuning.Default);
        }

        /// <summary>템플릿 적용 후 튜닝 배율을 반영한다.</summary>
        public static void Apply(HairPart part, ClothSerializeData sdata, float torsoLength,
            Transform headBone, bool isLong, in HairTuning tuning)
        {
            Apply(part, sdata, torsoLength, headBone, isLong, tuning, true);
        }

        /// <summary>
        /// usePresetBaseline이면 공식 프리셋을 먼저 베이스라인으로 가져와
        /// 템플릿이 다루지 않는 필드도 공식값으로 채운다. 템플릿 관리 필드는
        /// 이후 ApplyXxx가 덮어쓰므로 의도된 델타만 남는다.
        /// spring.enabled면 마지막에 Boing 식 프로파일이 결과를 덮어쓴다.
        /// </summary>
        public static void Apply(HairPart part, ClothSerializeData sdata, float torsoLength,
            Transform headBone, bool isLong, in HairTuning tuning, bool usePresetBaseline,
            in HairSpringProfile spring = default)
        {
            float unit = Mathf.Max(torsoLength, 0.1f) / 0.6f;
            if (usePresetBaseline)
                ClothPresetLibrary.TryImport(sdata, PresetName(part, isLong),
                    tuning.characterPresetKey, CharacterPhysicsSetup.ClothNamePrefix + part);
            switch (part)
            {
                case HairPart.Front:
                case HairPart.Ahoge:
                    ApplyFrontHair(sdata, unit, headBone);
                    break;
                case HairPart.Accessory:
                    ApplyAccessory(sdata, unit);
                    break;
                default:
                    if (isLong)
                        ApplyLongHair(sdata, unit);
                    else
                        ApplyShortHair(sdata, unit);
                    break;
            }
            ApplyTuning(sdata, tuning, unit);
            // Boing 식 프로파일이 켜져 있으면 위 결과를 스프링 의미론으로 덮어쓴다.
            if (spring.enabled)
            {
                spring.ApplyToMc2(sdata, unit);
                if (part == HairPart.Accessory)
                {
                    // damping은 의상 체인의 깊이 램프가 정본 — 스프링의 평탄값 덮어쓰기를
                    // 복원하되, 함께 지워진 감쇠 튜닝 채널(sway·dampingScale·tip)도 되살린다.
                    // 무조건 재적용하면 스프링 없이도 ApplyTuning 결과가 지워진다.
                    ApplyGarmentDamping(sdata);
                    float inv = 1f / Mathf.Max(tuning.sway, 0.05f);
                    ScaleCurve(sdata.damping, inv * tuning.dampingScale, 1f);
                    ScaleCurveTip(sdata.damping, tuning.tipDampingScale);
                }
            }
        }

        /// <summary>부위에 대응하는 공식 프리셋 이름 (Resources/PhysicsPresets 기준).</summary>
        static string PresetName(HairPart part, bool isLong)
        {
            switch (part)
            {
                case HairPart.Front:
                case HairPart.Ahoge:
                    return ClothPresetLibrary.FrontHair;
                case HairPart.Accessory:
                    return ClothPresetLibrary.Accessory;
                default:
                    return isLong ? ClothPresetLibrary.LongHair : ClothPresetLibrary.ShortHair;
            }
        }

        /// <summary>
        /// 템플릿 위에 사용자 배율을 곱한다.
        /// sway는 복원 강성과 감쇠를 역비례로 조정한다(흔들림 ↑ = 강성·감쇠 ↓).
        /// </summary>
        static void ApplyTuning(ClothSerializeData s, in HairTuning t, float unit)
        {
            float inv = 1f / Mathf.Max(t.sway, 0.05f);
            ScaleCurve(s.angleRestorationConstraint.stiffness, inv, 1f);
            ScaleCurve(s.damping, inv * t.dampingScale, 1f);
            s.gravity *= t.gravityScale;
            ScaleCurve(s.radius, t.radiusScale, 0.5f);
            s.inertiaConstraint.worldInertia =
                Mathf.Clamp01(s.inertiaConstraint.worldInertia * t.inertiaScale);
            s.inertiaConstraint.localInertia =
                Mathf.Clamp01(s.inertiaConstraint.localInertia * t.inertiaScale);
            // 끝단 커브 조정 — 프로급 실루엣은 깊이별 강성 차이에서 나온다
            ScaleCurveTip(s.angleRestorationConstraint.stiffness, t.tipStiffnessScale);
            ScaleCurveTip(s.damping, t.tipDampingScale);
            s.animationPoseRatio = Mathf.Clamp01(t.animationPoseRatio);
            if (t.overrideDepthInertia)
                s.inertiaConstraint.depthInertia = Mathf.Clamp01(t.depthInertia);
            // 자기 충돌 — BoneCloth는 Edge-Edge 조합으로 같은 클로스 내 체인 엉킴을 막는다
            // (MC2 베타 기능, 부하 높음). 두께는 쌍방 합산이라 절반 수준으로 잡는다.
            var self = s.selfCollisionConstraint;
            if (t.useSelfCollision)
            {
                self.selfMode = SelfCollisionConstraint.SelfCollisionMode.FullMesh;
                self.surfaceThickness.SetValue(
                    0.005f * unit * Mathf.Max(t.selfCollisionScale, 0.1f));
            }
            else
            {
                self.selfMode = SelfCollisionConstraint.SelfCollisionMode.None;
            }
        }

        /// <summary>
        /// 커브의 끝단(깊이 1.0)만 배율한다 — 키의 time이 깊이(0=루트, 1=끝).
        /// 커브 미사용 시 플랫값 전체를 배율한다.
        /// </summary>
        internal static void ScaleCurveTip(CurveSerializeData c, float tipScale)
        {
            if (!c.useCurve || c.curve == null)
            {
                c.value = Mathf.Max(0f, c.value * tipScale);
                return;
            }
            var keys = c.curve.keys;
            for (int i = 0; i < keys.Length; i++)
                keys[i].value = Mathf.Max(0f,
                    keys[i].value * Mathf.Lerp(1f, tipScale, keys[i].time));
            c.curve.keys = keys;
        }

        /// <summary>커브 직렬화 값의 기본값과 커브 키를 함께 배율한다.</summary>
        static void ScaleCurve(CurveSerializeData c, float mult, float maxValue)
        {
            c.value = Mathf.Min(c.value * mult, maxValue);
            if (!c.useCurve || c.curve == null)
                return;
            var keys = c.curve.keys;
            for (int i = 0; i < keys.Length; i++)
                keys[i].value = Mathf.Min(keys[i].value * mult, maxValue);
            c.curve.keys = keys;
        }
    }
}
