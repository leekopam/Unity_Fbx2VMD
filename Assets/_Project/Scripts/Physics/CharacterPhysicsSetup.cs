using System.Collections.Generic;
using System.Linq;
using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 캐릭터에 MC2 물리를 자동 세팅하는 오케스트레이터.
    /// ContextMenu로 실행하며, 콜라이더 자동 생성 → 머리카락 체인 탐지 →
    /// 부위별 BoneCloth 생성 순으로 진행한다.
    /// </summary>
    public class CharacterPhysicsSetup : MonoBehaviour
    {
        public const string ClothNamePrefix = "MC2Cloth_";

        [Header("대상")]
        [Tooltip("비워두면 자식에서 Animator를 자동 탐색")]
        public Animator targetAnimator;

        [Tooltip("사용자 지정 머리카락 루트 본. 지정 시 자동 탐지 무시")]
        public List<Transform> explicitHairRoots = new List<Transform>();

        [Header("콜라이더 옵션")]
        public bool useSymmetry = true;
        public bool includeLegs = true;
        [Range(1.0f, 1.5f)]
        public float colliderRadiusScale = 1.15f;

        [Header("머리카락 옵션")]
        [Tooltip("이름 규칙에 맞지 않는 미매핑 본도 머리카락 후보로 포함")]
        public bool includeUnknownBones = false;

        [Header("흔들림 조정")]
        public HairTuning tuning = HairTuning.Default;

        [Header("스커트 옵션")]
        [Tooltip("스커트 본 감지 시 MeshCloth를 자동 생성 (공식 런타임 vertexAttributeList 경로)")]
        public bool autoSkirtCloth = true;

        [Tooltip("버텍스가 스커트로 분류되는 스커트 본 가중치 합 임계값")]
        [Range(0.1f, 1f)]
        public float skirtWeightThreshold = 0.5f;

        [Tooltip("체인 루트부터 이 깊이까지의 스커트 본에 붙은 버텍스를 고정(Fixed) — 허리 고정부")]
        [Range(0, 3)]
        public int skirtFixedChainDepth = 0;

        [Tooltip("스커트 프록시 메시 단순화 배율. 1.0 = 기본(체형 비례). 크면 입자 수가 줄어 성능↑ 디테일↓")]
        [Range(0.5f, 3.0f)]
        public float skirtReductionScale = 1.0f;

        [Tooltip("스커트에 백스톱 적용 — 스킨 포즈 기준 내측 진입을 제한해 다리 관통을 막는다. " +
                 "MC2 2.18.1에는 Penetration 제약이 없어 이것이 대체 수단. " +
                 "옷이 다리 사이로 들어가는 정상 동작까지 딱딱해지면 끈다")]
        public bool useSkirtBackstop = true;

        [Tooltip("스커트 백스톱 허용 거리(m). 작을수록 스킨 포즈에 강하게 붙는다")]
        [Min(0f)]
        public float skirtBackstopDistance = 0.005f;

        [Header("베이스라인 / 컬링")]
        [Tooltip("공식 MC2 프리셋(JSON)을 베이스라인으로 먼저 가져와 관리 필드 외도 공식값으로 채운다. " +
                 "끄면 기존 수작업 파라미터 경로만 사용")]
        public bool usePresetBaseline = true;

        [Tooltip("카메라 컬링 모드. AnimatorLinkage = 연동 Animator의 CullingMode를 따라감")]
        public CullingSettings.CameraCullingMode cameraCullingMode =
            CullingSettings.CameraCullingMode.AnimatorLinkage;

        [Tooltip("이 거리(m)를 넘는 클로스 시뮬레이션을 끈다. 0 = 거리 컬링 끔(녹화·소규모 씬 기본값)")]
        [Min(0f)]
        public float distanceCullingMeters = 0f;

        [Tooltip("거리 컬링 종료 직전 페이드 구간 비율(0~1)")]
        [Range(0f, 1f)]
        public float distanceCullingFadeRatio = 0.2f;

        [Header("런타임 옵션")]
        [Tooltip("MC2 Pre-build 사용. 활성화하려면 생성된 MagicaCloth 인스펙터에서 Pre-build 데이터도 함께 생성해야 함")]
        public bool usePreBuild = false;

        [Tooltip("비활성→활성 전환 시 클로스가 무효면 자동 재설정. MC2는 비활성 시 팀을 해제해 재활성만으로는 복구되지 않으므로 기본 켜기")]
        public bool autoRebuildOnEnable = true;

        [Tooltip("다른 에셋이 PlayerLoop를 덮어써 MC2 시뮬레이션이 멈추는 경우를 대비해 " +
                 "주기적으로 MagicaManager.InitCustomGameLoop()를 재호출한다 (등록 확인 후 누락 시에만 재등록)")]
        public bool playerLoopGuard = true;

        [Header("결과 (읽기 전용)")]
        public List<ColliderComponent> generatedColliders = new List<ColliderComponent>();
        public List<MagicaCloth> generatedCloths = new List<MagicaCloth>();
        public List<HairPart> generatedParts = new List<HairPart>();
        public List<bool> generatedIsLong = new List<bool>();
        [TextArea]
        [System.NonSerialized] public string lastReport = "";

        /// <summary>
        /// 자동 물리 설정을 실행한다. 기존 자동 생성물은 정리 후 재생성.
        /// 단, 플레이 중 기존 클로스가 전부 유효/빌드 중이면 재생성을 생략한다 —
        /// 빌드 진행 중인 클로스를 파괴하면 MC2 스타트업이 취소돼 무효 클로스가 남는다.
        /// 강제 재생성은 '자동 생성 물리 제거' 후 실행하거나 Rebuild()를 사용.
        /// </summary>
        [ContextMenu("자동 물리 설정 실행")]
        public void Setup()
        {
            // 유효 클로스가 있어도 스커트 등 기대 생성물이 없으면 완전하지 않다 —
            // 씬 직렬화 클로스만 유효한 플레이에서 런타임 전용 스커트가 영구 누락되는 것을 막는다.
            if (Application.isPlaying && generatedCloths.Count > 0 &&
                AllClothsAlive() && IsSetupComplete())
            {
                lastReport = $"이미 설정 완료 상태 — 클로스 {generatedCloths.Count}개 유효/빌드 중 (재실행 생략). " +
                    "재생성하려면 '자동 생성 물리 제거' 후 실행하세요.";
                Debug.Log("[CharacterPhysicsSetup] " + lastReport, this);
                return;
            }
            // 빌드 중인 클로스를 파괴하면 MC2 스타트업이 취소되므로 완료까지 지연한다.
            if (Application.isPlaying && generatedCloths.Any(c =>
                    c != null && c.Process.IsState(ClothProcess.State_Build)))
            {
                deferredSetup = true;
                lastReport = "클로스 빌드 진행 중 — 완료 후 자동 재설정 예약";
                Debug.Log("[CharacterPhysicsSetup] " + lastReport, this);
                return;
            }
            SetupInternal();
        }

        /// <summary>기대 생성물이 전부 있는지 — 스커트 본이 있으면 MeshCloth 존재 여부까지 확인.</summary>
        bool IsSetupComplete()
        {
            if (!autoSkirtCloth)
                return true;
            var animator = ResolveAnimator();
            if (animator == null ||
                SkirtClothBuilder.CollectSkirtBoneDepths(animator.transform).Count == 0)
                return true; // 스커트 비대상 모델
            return generatedCloths.Any(c =>
                c != null && c.SerializeData.clothType == ClothProcess.ClothType.MeshCloth);
        }

        /// <summary>빌드 중 클로스가 있어 지연된 셋업 요청.</summary>
        bool deferredSetup;

        /// <summary>기존 클로스가 있어도 강제로 재생성한다.</summary>
        public void Rebuild()
        {
            ClearGenerated();
            SetupInternal();
        }

        // 비동기 init+빌드 완료 유예 (프레임). 이 기간의 !IsValid는 '방금 생성/방금 활성'으로 간주.
        // 죽은 클로스(SetActive로 팀 해제)와 init 비동기 진행 중은 둘 다 !IsValid라
        // 유예 종료 후에도 무효인 클로스만 죽은 것으로 판정한다.
        const int RebuildGraceFrames = 60;
        // 씬 직렬화 클로스도 플레이 시작 직후엔 init 중이므로 초기값 자체가 유예 구간.
        int graceUntilFrame = RebuildGraceFrames;

        /// <summary>플레이 중 클로스가 살아있는지(실행·빌드 중·유예 구간) 판정한다.</summary>
        bool IsClothAlive(MagicaCloth cloth)
        {
            if (cloth == null || !cloth.isActiveAndEnabled)
                return false;
            if (cloth.Process.IsState(ClothProcess.State_Build) || cloth.Process.IsRunning())
                return true;
            // 유예 구간의 !Running은 'init/빌드 대기' — 유예 후에도 미실행이면 죽은 클로스
            return Time.frameCount <= graceUntilFrame;
        }

        bool AllClothsAlive()
        {
            foreach (var cloth in generatedCloths)
                if (!IsClothAlive(cloth))
                    return false;
            return true;
        }

        void SetupInternal()
        {
            var report = new List<string>();
            ClearGenerated();

            Animator animator = ResolveAnimator();
            if (animator == null)
            {
                lastReport = "실패: Animator를 찾을 수 없습니다.";
                Debug.LogError(lastReport, this);
                return;
            }
            if (!animator.isHuman)
            {
                // 프리팹 Animator에 Avatar가 비어있으면 원본 모델에서 복구 시도 (에디터 전용)
#if UNITY_EDITOR
                if (TryAssignAvatarFromPrefabSource(animator))
                    report.Add("Avatar를 원본 모델에서 복구해 할당했습니다.");
#endif
            }

            // Humanoid면 Avatar 매핑으로, 아니면 본 이름 패턴으로 해석한다.
            bool humanoid = animator.isHuman;
            System.Func<HumanBodyBones, Transform> resolver;
            HashSet<Transform> bodyBones;
            float torso;
            if (humanoid)
            {
                resolver = animator.GetBoneTransform;
                bodyBones = BodyColliderBuilder.CollectBodyBoneSet(animator);
                torso = BodyColliderBuilder.MeasureTorso(animator);
            }
            else
            {
                var nameMap = NamePatternBoneResolver.Resolve(animator.transform);
                if (!nameMap.TryGetValue(HumanBodyBones.Head, out _) ||
                    !nameMap.TryGetValue(HumanBodyBones.Hips, out _))
                {
                    lastReport = "실패: Avatar가 Humanoid가 아니고 본 이름 패턴으로도 " +
                        "Head/Hips를 해석할 수 없습니다. (수동 본 지정 필요)\n" +
                        string.Join("\n", report);
                    Debug.LogError(lastReport, this);
                    return;
                }
                resolver = b => nameMap.TryGetValue(b, out var t) ? t : null;
                bodyBones = new HashSet<Transform>(nameMap.Values);
                torso = BodyColliderBuilder.MeasureTorso(
                    nameMap[HumanBodyBones.Hips], nameMap[HumanBodyBones.Head]);
                report.Add($"Humanoid 아님 — 이름 패턴 폴백으로 신체 본 {nameMap.Count}개 해석");
            }

            // 1. 버텍스 수집 → 콜라이더 생성 (Humanoid만 시메트리 가능, 폴백은 미러 직접 생성)
            var smrs = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var boneVerts = BodyColliderBuilder.GatherBoneVertices(smrs);
            var colResult = BodyColliderBuilder.Build(
                resolver, torso, boneVerts, humanoid && useSymmetry, includeLegs, colliderRadiusScale);
            generatedColliders = colResult.colliders;
            report.Add($"콜라이더 {colResult.colliders.Count}개 생성");
            report.AddRange(colResult.warnings.Select(w => "경고: " + w));

            // 2. 머리카락 체인 탐지
            Transform head = resolver(HumanBodyBones.Head);
            if (head == null)
            {
                report.Add("경고: Head 본이 없어 머리카락 설정을 건너뜀");
                Finish(report);
                return;
            }
            var detect = HairChainDetector.Detect(
                head, t => bodyBones.Contains(t), explicitHairRoots, includeUnknownBones);

            if (detect.skipped.Count > 0)
                report.Add($"미분류 본 {detect.skipped.Count}개 건너뜀: " +
                    string.Join(", ", detect.skipped.Select(t => t.name).Take(10)));

            // 2.5. 신체 측(허리/가슴 등)에 매달린 장식물 체인 탐지 — 머리 외 부착점
            var accRoots = new List<Transform>();
            var accClaimed = new HashSet<Transform>();
            foreach (var t in animator.GetComponentsInChildren<Transform>(true))
            {
                if (accClaimed.Contains(t) || t.IsChildOf(head) || bodyBones.Contains(t)
                    || t.name.StartsWith("MC2"))
                    continue; // 상위 장식물 체인 내부·머리 쪽·신체 본·생성물은 제외
                if (t.childCount > 0 && HairBoneClassifier.Classify(t.name) == HairPart.Accessory)
                {
                    accRoots.Add(t);
                    foreach (var d in t.GetComponentsInChildren<Transform>(true))
                        accClaimed.Add(d); // 하위 체인 본은 중복 루트로 잡지 않음
                }
            }
            if (accRoots.Count > 0)
            {
                var acc = HairChainDetector.Detect(head, x => bodyBones.Contains(x), accRoots);
                detect.chains.AddRange(acc.chains);
                detect.skipped.AddRange(acc.skipped);
            }

            // 스커트 본 감지 → MeshCloth 자동 생성 (vertexAttributeList로 고정/이동 자동 결정)
            var skirtBoneDepths = SkirtClothBuilder.CollectSkirtBoneDepths(animator.transform);
            if (skirtBoneDepths.Count > 0)
            {
                if (!autoSkirtCloth)
                {
                    report.Add($"스커트 본 {skirtBoneDepths.Count}개 감지 — autoSkirtCloth 꺼짐, 수동 설정 필요");
                }
                else
                {
                    var skirtColliders = HairPartColliderMapper.Select(
                        ColliderCategory.Waist | ColliderCategory.Hips | ColliderCategory.Legs
                            | ColliderCategory.Feet | ColliderCategory.Hands,
                        colResult.byCategory, 32);
                    var sr = SkirtClothBuilder.Create(
                        transform, smrs, skirtBoneDepths, skirtColliders, torso,
                        skirtWeightThreshold, skirtFixedChainDepth,
                        skirtReductionScale, useSkirtBackstop, skirtBackstopDistance,
                        usePresetBaseline);
                    if (sr.cloth != null)
                    {
                        sr.cloth.GetSerializeData2().preBuildData.enabled = usePreBuild;
                        ApplyCulling(sr.cloth);
                        sr.cloth.OnBuildComplete += HandleClothBuildComplete;
                        generatedCloths.Add(sr.cloth);
                        generatedParts.Add(HairPart.Unknown);
                        generatedIsLong.Add(false);
                        report.Add($"스커트 MeshCloth 자동 생성: 고정 {sr.fixedVertexCount} + " +
                            $"이동 {sr.moveVertexCount} 버텍스 ({sr.rendererCount}개 렌더러, " +
                            $"콜라이더 {skirtColliders.Count}개)");
                        if (sr.extractedMeshes.Count > 0)
                            report.Add($"스커트 메시 추출 {sr.extractedMeshes.Count}개 " +
                                "(65535 버텍스 한도 대응 — 원본 메시에서 스커트 삼각형 분리)");
                    }
                    else
                    {
                        report.Add($"스커트 본 {skirtBoneDepths.Count}개 감지 — 스커트 버텍스를 찾지 못해 " +
                            "MeshCloth 미생성 (메시가 스커트 본에 스키닝되지 않았거나 Read/Write 필요)");
                    }
                    foreach (var w in sr.warnings)
                        report.Add("경고: " + w);
                }
            }

            // 3. 부위별 BoneCloth 생성
            var groups = detect.chains.GroupBy(c => c.part);
            foreach (var group in groups)
            {
                var cloth = CreateHairCloth(
                    group.Key, group.ToList(), colResult.byCategory, torso, head, report);
                if (cloth != null)
                    generatedCloths.Add(cloth);
            }

            report.Add($"BoneCloth {generatedCloths.Count}개 생성");
            graceUntilFrame = Time.frameCount + RebuildGraceFrames;
            Finish(report);
        }

        /// <summary>생성물 전체 제거 (재실행 안전). 추출된 스커트 메시도 복원한다.</summary>
        [ContextMenu("자동 생성 물리 제거")]
        public void ClearGenerated()
        {
            SkirtClothBuilder.CleanupExtractions(transform);
            BodyColliderBuilder.ClearGenerated(transform);
            var doomed = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(ClothNamePrefix))
                    doomed.Add(child.gameObject);
            }
            foreach (var go in doomed)
            {
                if (Application.isPlaying)
                    Destroy(go);
                else
                    DestroyImmediate(go);
            }
            generatedColliders.Clear();
            generatedCloths.Clear();
            generatedParts.Clear();
            generatedIsLong.Clear();
        }

        MagicaCloth CreateHairCloth(
            HairPart part, List<HairChain> chains,
            Dictionary<ColliderCategory, List<ColliderComponent>> collidersByCategory,
            float torso, Transform head, List<string> report)
        {
            if (chains.Count == 0)
                return null;

            var go = new GameObject(ClothNamePrefix + part);
            go.transform.SetParent(transform, false);
            var cloth = go.AddComponent<MagicaCloth>();
            var sdata = cloth.SerializeData;

            sdata.clothType = ClothProcess.ClothType.BoneCloth;
            sdata.rootBones.Clear();
            foreach (var chain in chains)
                sdata.rootBones.Add(chain.rootBone);

            float maxLen = chains.Max(c => c.worldLength);
            bool isLong = torso > 0f && maxLen >= torso * 0.8f;

            // 부위별 파라미터 템플릿 + 튜닝 배율
            HairPhysicsParameters.Apply(part, sdata, torso, head, isLong, tuning, usePresetBaseline);

            // 관련 콜라이더만 등록 (32개 제한 관리)
            // Pre-build 데이터는 SerializeData2에 위치 — 에디터에서 Pre-build 생성도 필요
            cloth.GetSerializeData2().preBuildData.enabled = usePreBuild;
            var categories = HairPartColliderMapper.Map(part, maxLen, torso);
            var selected = HairPartColliderMapper.Select(categories, collidersByCategory, 32);
            sdata.colliderCollisionConstraint.colliderList.Clear();
            sdata.colliderCollisionConstraint.colliderList.AddRange(selected);
            ApplyCulling(cloth);
            cloth.OnBuildComplete += HandleClothBuildComplete;

            report.Add($"{part}: 체인 {chains.Count}개, 최대길이 {maxLen:F2}m, " +
                $"콜라이더 {selected.Count}개{(isLong ? " [장발]" : "")}");
            generatedParts.Add(part);
            generatedIsLong.Add(isLong);
            return cloth;
        }

        /// <summary>
        /// 이미 생성된 클로스에 현재 튜닝값만 다시 적용한다 (콜라이더/체인 재생성 없음).
        /// 인스펙터 슬라이더 조정 후 실행.
        /// </summary>
        [ContextMenu("흔들림 파라미터 재적용")]
        public void ReapplyParameters()
        {
            if (generatedCloths.Count == 0)
            {
                lastReport = "재적용할 클로스가 없습니다. 자동 물리 설정을 먼저 실행하세요.";
                Debug.LogWarning(lastReport, this);
                return;
            }
            Animator animator = ResolveAnimator();
            float torso = 0.6f;
            Transform head = null;
            if (animator != null && animator.isHuman)
            {
                torso = BodyColliderBuilder.MeasureTorso(animator);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }
            else if (animator != null)
            {
                var nameMap = NamePatternBoneResolver.Resolve(animator.transform);
                nameMap.TryGetValue(HumanBodyBones.Head, out head);
                if (head != null && nameMap.TryGetValue(HumanBodyBones.Hips, out var hips))
                    torso = BodyColliderBuilder.MeasureTorso(hips, head);
            }

            int applied = 0;
            for (int i = 0; i < generatedCloths.Count; i++)
            {
                var cloth = generatedCloths[i];
                if (cloth == null || cloth.SerializeData.clothType != ClothProcess.ClothType.BoneCloth)
                    continue; // MeshCloth(스커트)는 헤어 튜닝 적용 대상이 아님
                var part = i < generatedParts.Count ? generatedParts[i] : HairPart.Unknown;
                bool isLong = i < generatedIsLong.Count && generatedIsLong[i];
                HairPhysicsParameters.Apply(part, cloth.SerializeData, torso, head, isLong, tuning, usePresetBaseline);
                ApplyCulling(cloth);
                NotifyParameterChange(cloth);
                applied++;
            }
            lastReport = $"튜닝 재적용 완료: {applied}개 클로스 " +
                $"(sway={tuning.sway:F2}, damp={tuning.dampingScale:F2}, grav={tuning.gravityScale:F2})";
            Debug.Log("[CharacterPhysicsSetup] " + lastReport, this);
        }

        /// <summary>
        /// 리스폰·순간이동 후 시뮬레이션을 초기 상태로 리셋한다.
        /// keepPose=true면 현재 자세를 유지한 채 재개, false면 바인드 포즈로 리셋.
        /// </summary>
        /// <param name="keepPose">true=자세 유지 리셋(순간이동 폭주 방지)</param>
        public void ResetPhysics(bool keepPose = false)
        {
            foreach (var cloth in generatedCloths)
            {
                if (cloth != null && cloth.IsValid())
                    cloth.ResetCloth(keepPose);
            }
        }

        /// <summary>
        /// 비활성→활성 전환 시 생성된 클로스의 무결성을 확인한다.
        /// OnEnable 직후에는 MC2 비동기 빌드가 진행 중일 수 있으므로
        /// 짧은 유예 시간 뒤 Update에서 판정한다.
        /// </summary>
        int integrityCheckDelay;

        void OnEnable()
        {
            if (Application.isPlaying && generatedCloths.Count > 0)
            {
                graceUntilFrame = Time.frameCount + RebuildGraceFrames;
                integrityCheckDelay = RebuildGraceFrames + 5; // 유예 종료 후 무결성 판정
            }
        }

        void Update()
        {
            // 다른 에셋이 PlayerLoop를 덮어쓰면 MC2 시뮬레이션이 조용히 멈춘다.
            // InitCustomGameLoop는 이미 등록돼 있으면 즉시 반환하는 멱등 호출.
            if (playerLoopGuard && Application.isPlaying && Time.frameCount % 120 == 0)
                MagicaManager.InitCustomGameLoop();

            // 빌드 완료를 기다리던 지연 셋업 — 빌드 중 클로스가 없어지면 실행
            if (deferredSetup && !generatedCloths.Any(c =>
                    c != null && c.Process.IsState(ClothProcess.State_Build)))
            {
                deferredSetup = false;
                SetupInternal();
                return;
            }
            if (integrityCheckDelay <= 0)
                return;
            if (--integrityCheckDelay > 0)
                return;

            int invalid = 0;
            foreach (var cloth in generatedCloths)
                if (!IsClothAlive(cloth))
                    invalid++;
            if (invalid == 0)
                return;
            if (autoRebuildOnEnable)
            {
                Debug.Log($"[CharacterPhysicsSetup] 무효 클로스 {invalid}개 — 자동 재설정 실행", this);
                Rebuild();
            }
            else
            {
                Debug.LogWarning($"[CharacterPhysicsSetup] 무효 클로스 {invalid}개 감지. " +
                    "ContextMenu '자동 물리 설정 실행'으로 재설정하거나 autoRebuildOnEnable을 켜세요.", this);
            }
        }

        /// <summary>생성 클로스에 컬링 옵션을 적용한다 (프리셋 JSON은 컬링을 저장하지 않아 여기서 설정).</summary>
        void ApplyCulling(MagicaCloth cloth)
        {
            var culling = cloth.SerializeData.cullingSettings;
            culling.cameraCullingMode = cameraCullingMode;
            culling.distanceCullingLength.SetValue(distanceCullingMeters > 0f, distanceCullingMeters);
            culling.distanceCullingFadeRatio = Mathf.Clamp01(distanceCullingFadeRatio);
        }

        /// <summary>클로스 비동기 빌드 실패를 리포트에 남긴다.</summary>
        void HandleClothBuildComplete(MagicaCloth cloth, bool success)
        {
            if (success)
                return;
            string clothName = cloth != null ? cloth.name : "?";
            lastReport = $"클로스 비동기 빌드 실패 ({clothName}) — " +
                "SerializeData/메시 상태를 확인하세요";
            Debug.LogWarning("[CharacterPhysicsSetup] " + lastReport, this);
        }

        /// <summary>플레이 중 파라미터 변경을 MC2에 알린다 (공식 API).</summary>
        static void NotifyParameterChange(MagicaCloth cloth)
        {
            if (!Application.isPlaying)
                return;
            cloth.SetParameterChange();
        }

        Animator ResolveAnimator()
        {
            if (targetAnimator != null)
                return targetAnimator;
            targetAnimator = GetComponent<Animator>();
            if (targetAnimator == null)
                targetAnimator = GetComponentInChildren<Animator>(true);
            return targetAnimator;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Avatar가 비어있을 때 모델 원본에서 Humanoid Avatar를 찾아 할당한다.
        /// 1순위: SkinnedMeshRenderer.sharedMesh의 원본 에셋(FBX) 안의 Avatar
        /// 2순위: 프리팹 소스와 같은 폴더의 Avatar
        /// 할당 후 Head 본 해결로 호환성을 검증하고, 실패 시 원복한다.
        /// </summary>
        bool TryAssignAvatarFromPrefabSource(Animator animator)
        {
            if (animator.avatar != null)
                return animator.avatar.isHuman;

            var candidatePaths = new List<string>();

            // 1) SMR 메시의 원본 에셋 경로 — 프리팹 연결 없이도 정확
            foreach (var smr in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null)
                    continue;
                string p = UnityEditor.AssetDatabase.GetAssetPath(smr.sharedMesh);
                if (!string.IsNullOrEmpty(p))
                    candidatePaths.Add(p);
            }

            // 2) 프리팹 소스와 같은 폴더
            string prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
            if (string.IsNullOrEmpty(prefabPath))
            {
                var src = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                if (src != null)
                    prefabPath = UnityEditor.AssetDatabase.GetAssetPath(src);
            }
            if (!string.IsNullOrEmpty(prefabPath))
            {
                string dir = System.IO.Path.GetDirectoryName(prefabPath).Replace('\\', '/');
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Avatar", new[] { dir }))
                {
                    string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    if (!candidatePaths.Contains(p))
                        candidatePaths.Add(p);
                }
            }

            var tried = new List<Avatar>();
            foreach (var path in candidatePaths)
            {
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Avatar avatar && avatar.isHuman && !tried.Contains(avatar))
                        tried.Add(avatar);
                }
            }

            foreach (var avatar in tried)
            {
                animator.avatar = avatar;
                // 호환성 검증: Head 본이 해결되지 않으면 다른 스켈레톤의 Avatar
                if (animator.isHuman && animator.GetBoneTransform(HumanBodyBones.Head) != null)
                    return true;
            }
            animator.avatar = null;
            return false;
        }
#endif

        void Finish(List<string> report)
        {
            lastReport = string.Join("\n", report);
            Debug.Log("[CharacterPhysicsSetup]\n" + lastReport, this);
        }
    }
}
