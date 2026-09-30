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

        public static HairTuning Default => new HairTuning
        {
            sway = 1f, dampingScale = 1f, gravityScale = 1f, radiusScale = 1f, inertiaScale = 1f
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

        /// <summary>리본·장식물 — MC2 Accessory 프리셋 기준, 빳빳하고 안정적.</summary>
        public static void ApplyAccessory(ClothSerializeData s, float unit)
        {
            s.gravity = 0f;
            s.damping.SetValue(0.1f);
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
        /// </summary>
        public static void Apply(HairPart part, ClothSerializeData sdata, float torsoLength,
            Transform headBone, bool isLong, in HairTuning tuning, bool usePresetBaseline)
        {
            float unit = Mathf.Max(torsoLength, 0.1f) / 0.6f;
            if (usePresetBaseline)
                ClothPresetLibrary.TryImport(sdata, PresetName(part, isLong));
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
            ApplyTuning(sdata, tuning);
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
        static void ApplyTuning(ClothSerializeData s, in HairTuning t)
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
