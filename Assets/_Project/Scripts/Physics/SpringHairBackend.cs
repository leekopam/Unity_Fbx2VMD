using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 스프링 본 에셋(BoingBones) 백엔드 어댑터.
    /// 에셋이 프로젝트에 없어도 컴파일되도록 전부 리플렉션으로 접근한다 —
    /// 스프링 본 에셋을 임포트하면 코드 수정 없이 자동으로 활성화된다.
    /// HairSpringProfile의 필드는 BoingBones의 파라미터와 1:1 대응한다.
    /// </summary>
    public static class SpringHairBackend
    {
        /// <summary>셋업이 만든 스프링 본 에셋 물리 오브젝트 이름 — MC2Cloth_ 접두사로 정리 경로와 공유.</summary>
        public const string ProductName = CharacterPhysicsSetup.ClothNamePrefix + "SpringHair";

        const string BoingBonesTypeName = "BoingBones";
        const string SharedParamsTypeName = "SharedBoingParams";

        static Type _springBonesType;
        static Type _sharedParamsType;
        static bool _resolved;

        /// <summary>스프링 본 에셋이 로드돼 있는지.</summary>
        public static bool Available
        {
            get
            {
                ResolveTypes();
                return _springBonesType != null && _sharedParamsType != null;
            }
        }

        static void ResolveTypes()
        {
            if (_resolved)
                return;
            _resolved = true;
            _springBonesType = FindType(BoingBonesTypeName);
            _sharedParamsType = FindType(SharedParamsTypeName);
        }

        /// <summary>
        /// 체인 목록으로 BoingBones 컴포넌트 하나를 만든다.
        /// 체인 루트는 이미 감지된 HairChain.rootBone을 그대로 사용해
        /// MC2 경로와 동일한 본 분류 결과를 공유한다.
        /// colliderSources가 주어지면 각 Transform에 스프링 본 콜라이더를 붙여
        /// 머리↔몸 충돌(캡슐→구 근사)을 연결한다.
        /// </summary>
        /// <returns>생성된 컴포넌트가 달린 GameObject. 스프링 본 에셋 미설치 시 null.</returns>
        public static GameObject Build(
            Transform host, IList<HairChain> chains,
            in HairSpringProfile spec, List<string> report,
            IEnumerable<Component> colliderSources = null)
        {
            if (!Available || chains == null || chains.Count == 0)
                return null;

            var go = new GameObject(ProductName);
            go.transform.SetParent(host, false);
            var spring = go.AddComponent(_springBonesType);
            Reapply(spring, chains, spec);
            WireBodyColliders(spring, colliderSources, report);

            report?.Add($"본 스프링 컴포넌트 생성: 체인 {chains.Count}개 " +
                $"(f={spec.frequency:F1}Hz, ζ={spec.dampingRatio:F2})");
            return go;
        }

        /// <summary>
        /// 기존 BoingBones 컴포넌트의 파라미터를 프로파일로 다시 쓴다.
        /// 체인 목록이 주어지면 체인 배열도 재구성한다.
        /// </summary>
        public static void Reapply(
            Component spring, IList<HairChain> chains, in HairSpringProfile spec)
        {
            if (spring == null || spring.GetType() != _springBonesType)
                return;

            // 체인별 공유 파라미터 오버라이드 (주파수/감쇠비) — 기존 에셋이 있으면 재사용
            UnityEngine.Object sharedParams = ResolveSharedParams(spring, spec);

            Type chainType = _springBonesType.GetNestedType("Chain");
            var chainArray = Array.CreateInstance(chainType, chains?.Count ?? 0);
            for (int i = 0; i < chainArray.Length; i++)
            {
                var chain = Activator.CreateInstance(chainType);
                ConfigureChain(chainType, chain, chains[i], spec, sharedParams);
                chainArray.SetValue(chain, i);
            }
            _springBonesType.GetField("BoneChains").SetValue(spring, chainArray);
            // 런타임 생성 시 OnEnable 스캔은 이미 끝난 상태 — 재스캔해 BoneData를 재구성한다
            _springBonesType.GetMethod("RescanBoneChains")?.Invoke(spring, null);
        }

        /// <summary>호스트 아래의 스프링 본 에셋 생성물을 찾는다 (재적용·정리 경로용).</summary>
        public static GameObject FindProduct(Transform host)
        {
            if (host == null)
                return null;
            var child = host.Find(ProductName);
            return child != null ? child.gameObject : null;
        }

        /// <summary>
        /// 이미 생성된 BoingBones의 체인 배열을 유지한 채 파라미터만 재적용한다.
        /// 재적용 시 본 계층을 다시 스캔하지 않고 컴포넌트가 보관한 Root를 그대로 쓴다.
        /// </summary>
        public static bool ReapplyExisting(Transform host, in HairSpringProfile spec)
        {
            if (!Available)
                return false;
            var product = FindProduct(host);
            var spring = product != null
                ? product.GetComponent(_springBonesType)
                : null;
            if (spring == null)
                return false;

            var chainsField = _springBonesType.GetField("BoneChains");
            var existing = chainsField.GetValue(spring) as Array;
            if (existing == null || existing.Length == 0)
                return false;

            UnityEngine.Object sharedParams = ResolveSharedParams(spring, spec);
            Type chainType = _springBonesType.GetNestedType("Chain");
            foreach (var chain in existing)
                ConfigureChain(chainType, chain, null, spec, sharedParams);
            return true;
        }

        /// <summary>
        /// 이미 배선된 체인의 ParamsOverride가 있으면 그 에셋을 갱신해 재사용한다 —
        /// 매번 CreateInstance하면 비에셋 SO가 누수되고 씬 저장 시 참조가 유실된다.
        /// </summary>
        static UnityEngine.Object ResolveSharedParams(Component spring, in HairSpringProfile spec)
        {
            var chainType = _springBonesType.GetNestedType("Chain");
            var poField = chainType?.GetField("ParamsOverride");
            var existing = _springBonesType.GetField("BoneChains")?.GetValue(spring) as Array;
            if (existing != null && existing.Length > 0 && poField != null)
            {
                if (poField.GetValue(existing.GetValue(0)) is UnityEngine.Object so && so != null)
                {
                    UpdateSharedParams(so, spec);
                    return so;
                }
            }
            return CreateSharedParams(spec);
        }

        /// <summary>
        /// 스프링 본 에셋 SharedBoingParams(ScriptableObject)를 만들어 스프링 파라미터를 주입한다.
        /// Boing Params 구조체의 필드명은 에셋 그대로 — OscillationByDampingRatio 모드를 사용.
        /// </summary>
        static UnityEngine.Object CreateSharedParams(in HairSpringProfile spec)
        {
            var asset = ScriptableObject.CreateInstance(_sharedParamsType);
            UpdateSharedParams(asset, spec);
            return asset;
        }

        /// <summary>SharedBoingParams.Params 구조체에 스프링 파라미터를 기록한다.</summary>
        static void UpdateSharedParams(UnityEngine.Object asset, in HairSpringProfile spec)
        {
            var paramsField = _sharedParamsType.GetField("Params");
            object p = paramsField.GetValue(asset);
            SetField(ref p, "PositionParameterMode", 2); // OscillationByDampingRatio
            SetField(ref p, "RotationParameterMode", 2);
            SetField(ref p, "PositionOscillationFrequency", spec.frequency);
            SetField(ref p, "RotationOscillationFrequency", spec.frequency);
            SetField(ref p, "PositionOscillationDampingRatio",
                Mathf.Clamp01(spec.dampingRatio));
            SetField(ref p, "RotationOscillationDampingRatio",
                Mathf.Clamp01(spec.dampingRatio));
            paramsField.SetValue(asset, p);
        }

        /// <summary>
        /// 생성된 몸통 콜라이더 Transform에 스프링 본 콜라이더를 붙이고
        /// 본 스프링 콜라이더 배열로 연결한다. 캡슐은 구로 근사한다.
        /// </summary>
        static void WireBodyColliders(
            Component spring, IEnumerable<Component> sources, List<string> report)
        {
            if (sources == null)
                return;
            var colType = FindType("BoingBoneCollider");
            if (colType == null)
                return;
            var list = new List<Component>();
            foreach (var src in sources)
            {
                if (src == null)
                    continue;
                var bc = src.GetComponent(colType) ?? src.gameObject.AddComponent(colType);
                SetFieldObj(colType, bc, "Radius", EstimateRadius(src));
                list.Add(bc);
            }
            if (list.Count == 0)
                return;

            var arr = Array.CreateInstance(colType, list.Count);
            for (int i = 0; i < list.Count; i++)
                arr.SetValue(list[i], i);
            SetFieldObj(_springBonesType, spring, "BoingColliders", arr);

            // 각 체인이 본체 콜라이더와 충돌하도록 활성화
            var chainType = _springBonesType.GetNestedType("Chain");
            if (_springBonesType.GetField("BoneChains")?.GetValue(spring) is Array chainsArr)
                foreach (var c in chainsArr)
                    SetFieldObj(chainType, c, "EnableBoingKitCollision", true);
            report?.Add($"스프링 본 콜라이더 {list.Count}개 연결 (캡슐→구 근사)");
        }

        /// <summary>MC2 콜라이더의 크기에서 구 반경을 추정한다 (캡슐 x/y 최대, 구 x).</summary>
        static float EstimateRadius(Component src)
        {
            var m = src.GetType().GetMethod("GetSize");
            if (m != null && m.Invoke(src, null) is Vector3 size)
                return Mathf.Max(size.x, size.y);
            return 0.05f;
        }

        /// <summary>
        /// 네임스페이스를 적지 않고 단순 이름으로 타입을 찾는다 —
        /// 에셋 패키지명을 소스에 남기지 않기 위한 의도적 회피.
        /// </summary>
        static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name);
                if (t != null)
                    return t;
                // 동적(Reflection.Emit) 어셈블리는 GetTypes를 지원하지 않는다
                if (asm.IsDynamic)
                    continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                catch (NotSupportedException) { continue; }
                foreach (var cand in types)
                    if (cand != null && cand.Name == name)
                        return cand;
            }
            return null;
        }

        /// <summary>
        /// 체인 하나를 프로파일로 설정한다 (체인 필드 직접 매핑).
        /// src가 null이면 Root를 덮지 않는다(기존 체인 재적용 경로).
        /// </summary>
        static void ConfigureChain(Type chainType, object chain, HairChain src,
            in HairSpringProfile spec, UnityEngine.Object sharedParams)
        {
            if (src != null)
                SetFieldObj(chainType, chain, "Root", src.rootBone);
            SetFieldObj(chainType, chain, "Exclusion", new Transform[0]);
            SetFieldObj(chainType, chain, "EffectorReaction", true);
            SetFieldObj(chainType, chain, "LooseRoot", false);
            SetFieldObj(chainType, chain, "ParamsOverride", sharedParams);
            // Boing CurveType은 enum 순서가 같으므로 int 캐스팅이 통한다
            SetFieldObj(chainType, chain, "AnimationBlendCurveType",
                Enum.ToObject(chainType.GetField("AnimationBlendCurveType").FieldType,
                    (int)spec.animationBlend.type));
            SetFieldObj(chainType, chain, "AnimationBlendCustomCurve",
                SafeCurve(spec.animationBlend));
            SetFieldObj(chainType, chain, "PoseStiffnessCurveType",
                Enum.ToObject(chainType.GetField("PoseStiffnessCurveType").FieldType,
                    (int)spec.poseStiffness.type));
            SetFieldObj(chainType, chain, "PoseStiffnessCustomCurve",
                SafeCurve(spec.poseStiffness));
            SetFieldObj(chainType, chain, "MaxBendAngleCap", spec.maxBendAngleDeg);
            SetFieldObj(chainType, chain, "BendAngleCapCurveType",
                Enum.ToObject(chainType.GetField("BendAngleCapCurveType").FieldType,
                    (int)spec.bendAngleCap.type));
            SetFieldObj(chainType, chain, "BendAngleCapCustomCurve",
                SafeCurve(spec.bendAngleCap));
            SetFieldObj(chainType, chain, "MaxCollisionRadius", spec.maxCollisionRadius);
            SetFieldObj(chainType, chain, "CollisionRadiusCurveType",
                Enum.ToObject(chainType.GetField("CollisionRadiusCurveType").FieldType,
                    (int)spec.collisionRadius.type));
            SetFieldObj(chainType, chain, "CollisionRadiusCustomCurve",
                SafeCurve(spec.collisionRadius));
            SetFieldObj(chainType, chain, "EnableInterChainCollision",
                spec.interChainCollision);
            SetFieldObj(chainType, chain, "Gravity", Vector3.down * spec.gravity);
        }

        /// <summary>스프링 본 에셋은 Custom 커브를 무방어로 Evaluate — null이면 안전 기본값.</summary>
        static AnimationCurve SafeCurve(DepthSpringCurve c)
        {
            return c.customCurve != null
                ? c.customCurve
                : AnimationCurve.Linear(0f, 1f, 1f, 0f);
        }

        static void SetField(ref object box, string name, object value)
        {
            FieldInfo f = box.GetType().GetField(name);
            if (f != null)
                f.SetValue(box, Coerce(f.FieldType, value));
        }

        static void SetFieldObj(Type t, object target, string name, object value)
        {
            FieldInfo f = t.GetField(name);
            if (f != null)
                f.SetValue(target, Coerce(f.FieldType, value));
        }

        /// <summary>enum 필드에 int를 대입하면 SetValue가 예외를 던지므로 미리 변환한다.</summary>
        static object Coerce(Type fieldType, object value)
        {
            if (fieldType.IsEnum && value != null && !(value is Enum))
                return Enum.ToObject(fieldType, value);
            return value;
        }
    }
}
